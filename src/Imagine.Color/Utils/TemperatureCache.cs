namespace Imagine.Color;

/// <summary>
/// «Температура» цвета (тёплый ↔ холодный) по Уго и др. Даёт аналогичные цвета
/// и дополнительный цвет, который ощущается противоположным по теплоте.
/// </summary>
public sealed class TemperatureCache(Hct input)
{
    private Hct[]? _hctsByHue;
    private double[]? _tempsByHue;
    private double _inputTemp = double.NaN;
    private int _coldestIndex = -1, _warmestIndex = -1;
    private double _coldestTemp, _warmestTemp;
    private Hct? _complement;

    public Hct Input { get; } = input;

    private Hct[] HctsByHue
    {
        get
        {
            if (_hctsByHue is not null) return _hctsByHue;
            var hcts = new Hct[361];
            for (int hue = 0; hue <= 360; hue++) hcts[hue] = Hct.From(hue, Input.Chroma, Input.Tone);
            _hctsByHue = hcts;
            return hcts;
        }
    }

    private double[] TempsByHue
    {
        get
        {
            if (_tempsByHue is not null) return _tempsByHue;
            var temps = new double[361];
            for (int i = 0; i <= 360; i++) temps[i] = RawTemperature(HctsByHue[i]);
            _tempsByHue = temps;
            _inputTemp = RawTemperature(Input);
            // Самый холодный/тёплый среди 361 оттенка и самого входного цвета (как в эталонной реализации).
            _coldestTemp = double.MaxValue; _warmestTemp = double.MinValue;
            for (int i = 0; i <= 360; i++)
            {
                if (temps[i] < _coldestTemp) { _coldestTemp = temps[i]; _coldestIndex = i; }
                if (temps[i] >= _warmestTemp) { _warmestTemp = temps[i]; _warmestIndex = i; }
            }
            if (_inputTemp < _coldestTemp) { _coldestTemp = _inputTemp; _coldestIndex = -2; }
            if (_inputTemp >= _warmestTemp) { _warmestTemp = _inputTemp; _warmestIndex = -2; }
            return temps;
        }
    }

    public Hct Coldest { get { _ = TempsByHue; return _coldestIndex == -2 ? Input : HctsByHue[_coldestIndex]; } }

    public Hct Warmest { get { _ = TempsByHue; return _warmestIndex == -2 ? Input : HctsByHue[_warmestIndex]; } }

    private double TempOf(int hueIndex) => TempsByHue[hueIndex];

    /// <summary>Относительная температура 0 (самый холодный) … 1 (самый тёплый).</summary>
    private double RelativeTemperature(double temp)
    {
        _ = TempsByHue;
        double range = _warmestTemp - _coldestTemp;
        return range == 0.0 ? 0.5 : (temp - _coldestTemp) / range;
    }

    public double InputRelativeTemperature { get { _ = TempsByHue; return RelativeTemperature(_inputTemp); } }

    /// <summary>Цвет, «противоположный» по теплоте (не обязательно на 180° по кругу).</summary>
    public Hct Complement
    {
        get
        {
            if (_complement is not null) return _complement;
            _ = TempsByHue;
            double coldestHue = Coldest.Hue, warmestHue = Warmest.Hue;
            double range = _warmestTemp - _coldestTemp;
            int inputHueIndex = MathUtils.RoundHalfUp(Input.Hue) % 361;
            if (range == 0.0) return _complement = HctsByHue[inputHueIndex];
            bool startIsColdestToWarmest = IsBetween(Input.Hue, coldestHue, warmestHue);
            double startHue = startIsColdestToWarmest ? warmestHue : coldestHue;
            double endHue = startIsColdestToWarmest ? coldestHue : warmestHue;
            double smallestError = 1000.0;
            var answer = HctsByHue[inputHueIndex];
            double complementRelativeTemp = 1.0 - InputRelativeTemperature;
            for (int hueAddend = 0; hueAddend <= 360; hueAddend++)
            {
                double hue = MathUtils.SanitizeDegreesDouble(startHue + hueAddend);
                if (!IsBetween(hue, startHue, endHue)) continue;
                int idx = MathUtils.RoundHalfUp(hue);
                double relativeTemp = (TempOf(idx) - _coldestTemp) / range;
                double error = Math.Abs(complementRelativeTemp - relativeTemp);
                if (error < smallestError)
                {
                    smallestError = error;
                    answer = HctsByHue[idx];
                }
            }
            return _complement = answer;
        }
    }

    /// <summary>Аналогичные цвета, равномерно распределённые по температуре.</summary>
    public IReadOnlyList<Hct> Analogous(int count = 5, int divisions = 12)
    {
        int startHue = MathUtils.RoundHalfUp(Input.Hue) % 361;
        var startHct = HctsByHue[startHue];
        double lastTemp = RelativeTemperature(TempOf(startHue));
        var allColors = new List<Hct> { startHct };
        double absoluteTotalTempDelta = 0.0;
        for (int i = 0; i < 360; i++)
        {
            int hue = MathUtils.SanitizeDegreesInt(startHue + i);
            double temp = RelativeTemperature(TempOf(hue));
            absoluteTotalTempDelta += Math.Abs(temp - lastTemp);
            lastTemp = temp;
        }
        int hueAddend = 1;
        double tempStep = absoluteTotalTempDelta / divisions;
        double totalTempDelta = 0.0;
        lastTemp = RelativeTemperature(TempOf(startHue));
        while (allColors.Count < divisions)
        {
            int hue = MathUtils.SanitizeDegreesInt(startHue + hueAddend);
            var hct = HctsByHue[hue];
            double temp = RelativeTemperature(TempOf(hue));
            totalTempDelta += Math.Abs(temp - lastTemp);
            double desired = allColors.Count * tempStep;
            bool indexSatisfied = totalTempDelta >= desired;
            int indexAddend = 1;
            while (indexSatisfied && allColors.Count < divisions)
            {
                allColors.Add(hct);
                desired = (allColors.Count + indexAddend) * tempStep;
                indexSatisfied = totalTempDelta >= desired;
                indexAddend++;
            }
            lastTemp = temp;
            hueAddend++;
            if (hueAddend > 360)
            {
                while (allColors.Count < divisions) allColors.Add(hct);
                break;
            }
        }

        var answers = new List<Hct> { Input };
        int increaseHueCount = (int)Math.Floor((count - 1) / 2.0);
        for (int i = 1; i <= increaseHueCount; i++)
        {
            int index = 0 - i;
            while (index < 0) index = allColors.Count + index;
            if (index >= allColors.Count) index %= allColors.Count;
            answers.Insert(0, allColors[index]);
        }
        int decreaseHueCount = count - increaseHueCount - 1;
        for (int i = 1; i <= decreaseHueCount; i++)
        {
            int index = i;
            if (index >= allColors.Count) index %= allColors.Count;
            answers.Add(allColors[index]);
        }
        return answers;
    }

    public static bool IsBetween(double angle, double a, double b)
        => a < b ? a <= angle && angle <= b : a <= angle || angle <= b;

    /// <summary>Сырая температура: ≈ −0.5 (холодный) … ≈ +1.5 (тёплый).</summary>
    public static double RawTemperature(Hct color)
    {
        var lab = ColorUtils.LabFromArgb(color.Argb);
        double hue = MathUtils.SanitizeDegreesDouble(Math.Atan2(lab[2], lab[1]) * 180.0 / Math.PI);
        double chroma = Math.Sqrt(lab[1] * lab[1] + lab[2] * lab[2]);
        return -0.5 + 0.02 * Math.Pow(chroma, 1.07) * Math.Cos(MathUtils.SanitizeDegreesDouble(hue - 50.0) * Math.PI / 180.0);
    }
}
