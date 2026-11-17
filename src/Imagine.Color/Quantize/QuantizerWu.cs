namespace Imagine.Color;

/// <summary>
/// Квантизатор Сяолиня Ву: делит RGB-куб на коробки, минимизируя дисперсию.
/// Быстро даёт хорошие стартовые кластеры для <see cref="QuantizerWsmeans"/>.
/// </summary>
public sealed class QuantizerWu
{
    private const int IndexBits = 5;
    private const int IndexCount = 33; // (1 << IndexBits) + 1
    private const int TotalSize = 35937; // IndexCount^3

    private long[] _weights = [];
    private long[] _momentsR = [];
    private long[] _momentsG = [];
    private long[] _momentsB = [];
    private double[] _moments = [];
    private Box[] _cubes = [];

    private enum Direction { Red, Green, Blue }

    private sealed class Box
    {
        public int R0, R1, G0, G1, B0, B1, Vol;
    }

    public IReadOnlyList<uint> Quantize(ReadOnlySpan<uint> pixels, int maxColors)
    {
        var map = new Dictionary<uint, int>();
        foreach (var p in pixels)
        {
            ref var c = ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(map, p, out _);
            c++;
        }
        ConstructHistogram(map);
        CreateMoments();
        int resultCount = CreateBoxes(maxColors);
        return CreateResult(resultCount);
    }

    private static int GetIndex(int r, int g, int b)
        => (r << (IndexBits * 2)) + (r << (IndexBits + 1)) + r + (g << IndexBits) + g + b;

    private void ConstructHistogram(Dictionary<uint, int> pixels)
    {
        _weights = new long[TotalSize];
        _momentsR = new long[TotalSize];
        _momentsG = new long[TotalSize];
        _momentsB = new long[TotalSize];
        _moments = new double[TotalSize];
        const int bitsToRemove = 8 - IndexBits;
        foreach (var (pixel, count) in pixels)
        {
            int red = ColorUtils.RedFromArgb(pixel), green = ColorUtils.GreenFromArgb(pixel), blue = ColorUtils.BlueFromArgb(pixel);
            int index = GetIndex((red >> bitsToRemove) + 1, (green >> bitsToRemove) + 1, (blue >> bitsToRemove) + 1);
            _weights[index] += count;
            _momentsR[index] += (long)red * count;
            _momentsG[index] += (long)green * count;
            _momentsB[index] += (long)blue * count;
            _moments[index] += (double)count * (red * red + green * green + blue * blue);
        }
    }

    private void CreateMoments()
    {
        for (int r = 1; r < IndexCount; r++)
        {
            var area = new long[IndexCount];
            var areaR = new long[IndexCount];
            var areaG = new long[IndexCount];
            var areaB = new long[IndexCount];
            var area2 = new double[IndexCount];
            for (int g = 1; g < IndexCount; g++)
            {
                long line = 0, lineR = 0, lineG = 0, lineB = 0;
                double line2 = 0.0;
                for (int b = 1; b < IndexCount; b++)
                {
                    int index = GetIndex(r, g, b);
                    line += _weights[index];
                    lineR += _momentsR[index];
                    lineG += _momentsG[index];
                    lineB += _momentsB[index];
                    line2 += _moments[index];
                    area[b] += line;
                    areaR[b] += lineR;
                    areaG[b] += lineG;
                    areaB[b] += lineB;
                    area2[b] += line2;
                    int previousIndex = GetIndex(r - 1, g, b);
                    _weights[index] = _weights[previousIndex] + area[b];
                    _momentsR[index] = _momentsR[previousIndex] + areaR[b];
                    _momentsG[index] = _momentsG[previousIndex] + areaG[b];
                    _momentsB[index] = _momentsB[previousIndex] + areaB[b];
                    _moments[index] = _moments[previousIndex] + area2[b];
                }
            }
        }
    }

    private int CreateBoxes(int maxColorCount)
    {
        _cubes = new Box[maxColorCount];
        for (int i = 0; i < maxColorCount; i++) _cubes[i] = new Box();
        var volumeVariance = new double[maxColorCount];
        var first = _cubes[0];
        first.R1 = IndexCount - 1;
        first.G1 = IndexCount - 1;
        first.B1 = IndexCount - 1;
        int generatedColorCount = maxColorCount;
        int next = 0;
        for (int i = 1; i < maxColorCount; i++)
        {
            if (Cut(_cubes[next], _cubes[i]))
            {
                volumeVariance[next] = _cubes[next].Vol > 1 ? Variance(_cubes[next]) : 0.0;
                volumeVariance[i] = _cubes[i].Vol > 1 ? Variance(_cubes[i]) : 0.0;
            }
            else
            {
                volumeVariance[next] = 0.0;
                i--;
            }
            next = 0;
            double temp = volumeVariance[0];
            for (int j = 1; j <= i; j++)
            {
                if (volumeVariance[j] > temp)
                {
                    temp = volumeVariance[j];
                    next = j;
                }
            }
            if (temp <= 0.0)
            {
                generatedColorCount = i + 1;
                break;
            }
        }
        return generatedColorCount;
    }

    private List<uint> CreateResult(int colorCount)
    {
        var colors = new List<uint>();
        for (int i = 0; i < colorCount; i++)
        {
            var cube = _cubes[i];
            long weight = Volume(cube, _weights);
            if (weight <= 0) continue;
            int r = (int)(Volume(cube, _momentsR) / weight);
            int g = (int)(Volume(cube, _momentsG) / weight);
            int b = (int)(Volume(cube, _momentsB) / weight);
            colors.Add(ColorUtils.ArgbFromRgb(r, g, b));
        }
        return colors;
    }

    private double Variance(Box cube)
    {
        double dr = Volume(cube, _momentsR), dg = Volume(cube, _momentsG), db = Volume(cube, _momentsB);
        double xx = _moments[GetIndex(cube.R1, cube.G1, cube.B1)]
                    - _moments[GetIndex(cube.R1, cube.G1, cube.B0)]
                    - _moments[GetIndex(cube.R1, cube.G0, cube.B1)]
                    + _moments[GetIndex(cube.R1, cube.G0, cube.B0)]
                    - _moments[GetIndex(cube.R0, cube.G1, cube.B1)]
                    + _moments[GetIndex(cube.R0, cube.G1, cube.B0)]
                    + _moments[GetIndex(cube.R0, cube.G0, cube.B1)]
                    - _moments[GetIndex(cube.R0, cube.G0, cube.B0)];
        double hypotenuse = dr * dr + dg * dg + db * db;
        double volume = Volume(cube, _weights);
        return xx - hypotenuse / volume;
    }

    private bool Cut(Box one, Box two)
    {
        long wholeR = Volume(one, _momentsR), wholeG = Volume(one, _momentsG), wholeB = Volume(one, _momentsB), wholeW = Volume(one, _weights);
        var (cutR, maxR) = Maximize(one, Direction.Red, one.R0 + 1, one.R1, wholeR, wholeG, wholeB, wholeW);
        var (cutG, maxG) = Maximize(one, Direction.Green, one.G0 + 1, one.G1, wholeR, wholeG, wholeB, wholeW);
        var (cutB, maxB) = Maximize(one, Direction.Blue, one.B0 + 1, one.B1, wholeR, wholeG, wholeB, wholeW);
        Direction cutDirection;
        if (maxR >= maxG && maxR >= maxB)
        {
            if (cutR < 0) return false;
            cutDirection = Direction.Red;
        }
        else if (maxG >= maxR && maxG >= maxB) cutDirection = Direction.Green;
        else cutDirection = Direction.Blue;

        two.R1 = one.R1; two.G1 = one.G1; two.B1 = one.B1;
        switch (cutDirection)
        {
            case Direction.Red:
                one.R1 = cutR; two.R0 = one.R1; two.G0 = one.G0; two.B0 = one.B0; break;
            case Direction.Green:
                one.G1 = cutG; two.R0 = one.R0; two.G0 = one.G1; two.B0 = one.B0; break;
            default:
                one.B1 = cutB; two.R0 = one.R0; two.G0 = one.G0; two.B0 = one.B1; break;
        }
        one.Vol = (one.R1 - one.R0) * (one.G1 - one.G0) * (one.B1 - one.B0);
        two.Vol = (two.R1 - two.R0) * (two.G1 - two.G0) * (two.B1 - two.B0);
        return true;
    }

    private (int Cut, double Max) Maximize(Box cube, Direction direction, int first, int last, long wholeR, long wholeG, long wholeB, long wholeW)
    {
        long bottomR = Bottom(cube, direction, _momentsR), bottomG = Bottom(cube, direction, _momentsG);
        long bottomB = Bottom(cube, direction, _momentsB), bottomW = Bottom(cube, direction, _weights);
        double max = 0.0;
        int cut = -1;
        for (int i = first; i < last; i++)
        {
            long halfR = bottomR + Top(cube, direction, i, _momentsR);
            long halfG = bottomG + Top(cube, direction, i, _momentsG);
            long halfB = bottomB + Top(cube, direction, i, _momentsB);
            long halfW = bottomW + Top(cube, direction, i, _weights);
            if (halfW == 0) continue;
            double temp = ((double)halfR * halfR + (double)halfG * halfG + (double)halfB * halfB) / halfW;
            halfR = wholeR - halfR; halfG = wholeG - halfG; halfB = wholeB - halfB; halfW = wholeW - halfW;
            if (halfW == 0) continue;
            temp += ((double)halfR * halfR + (double)halfG * halfG + (double)halfB * halfB) / halfW;
            if (temp > max) { max = temp; cut = i; }
        }
        return (cut, max);
    }

    private static long Volume(Box c, long[] m)
        => m[GetIndex(c.R1, c.G1, c.B1)] - m[GetIndex(c.R1, c.G1, c.B0)] - m[GetIndex(c.R1, c.G0, c.B1)] + m[GetIndex(c.R1, c.G0, c.B0)]
           - m[GetIndex(c.R0, c.G1, c.B1)] + m[GetIndex(c.R0, c.G1, c.B0)] + m[GetIndex(c.R0, c.G0, c.B1)] - m[GetIndex(c.R0, c.G0, c.B0)];

    private static long Bottom(Box c, Direction d, long[] m) => d switch
    {
        Direction.Red => -m[GetIndex(c.R0, c.G1, c.B1)] + m[GetIndex(c.R0, c.G1, c.B0)] + m[GetIndex(c.R0, c.G0, c.B1)] - m[GetIndex(c.R0, c.G0, c.B0)],
        Direction.Green => -m[GetIndex(c.R1, c.G0, c.B1)] + m[GetIndex(c.R1, c.G0, c.B0)] + m[GetIndex(c.R0, c.G0, c.B1)] - m[GetIndex(c.R0, c.G0, c.B0)],
        _ => -m[GetIndex(c.R1, c.G1, c.B0)] + m[GetIndex(c.R1, c.G0, c.B0)] + m[GetIndex(c.R0, c.G1, c.B0)] - m[GetIndex(c.R0, c.G0, c.B0)],
    };

    private static long Top(Box c, Direction d, int p, long[] m) => d switch
    {
        Direction.Red => m[GetIndex(p, c.G1, c.B1)] - m[GetIndex(p, c.G1, c.B0)] - m[GetIndex(p, c.G0, c.B1)] + m[GetIndex(p, c.G0, c.B0)],
        Direction.Green => m[GetIndex(c.R1, p, c.B1)] - m[GetIndex(c.R1, p, c.B0)] - m[GetIndex(c.R0, p, c.B1)] + m[GetIndex(c.R0, p, c.B0)],
        _ => m[GetIndex(c.R1, c.G1, p)] - m[GetIndex(c.R1, c.G0, p)] - m[GetIndex(c.R0, c.G1, p)] + m[GetIndex(c.R0, c.G0, p)],
    };
}
