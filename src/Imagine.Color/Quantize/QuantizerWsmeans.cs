namespace Imagine.Color;

/// <summary>
/// Взвешенные k-средние в пространстве L*a*b* с ускорением по неравенству треугольника.
/// Уточняет кластеры, полученные от <see cref="QuantizerWu"/>.
/// </summary>
public static class QuantizerWsmeans
{
    private const int MaxIterations = 10;
    private const double MinMovementDistance = 3.0;

    private struct DistanceAndIndex : IComparable<DistanceAndIndex>
    {
        public double Distance;
        public int Index;
        public readonly int CompareTo(DistanceAndIndex other) => Distance.CompareTo(other.Distance);
    }

    public static Dictionary<uint, int> Quantize(ReadOnlySpan<uint> inputPixels, IReadOnlyList<uint> startingClusters, int maxColors)
    {
        var random = new JavaRandom(0x42688);
        var pixelToCount = new Dictionary<uint, int>();
        var points = new List<double[]>();
        var pixels = new List<uint>();
        foreach (var inputPixel in inputPixels)
        {
            if (pixelToCount.TryGetValue(inputPixel, out int pixelCount))
            {
                pixelToCount[inputPixel] = pixelCount + 1;
            }
            else
            {
                points.Add(ColorUtils.LabFromArgb(inputPixel));
                pixels.Add(inputPixel);
                pixelToCount[inputPixel] = 1;
            }
        }
        int pointCount = points.Count;
        var counts = new int[pointCount];
        for (int i = 0; i < pointCount; i++) counts[i] = pixelToCount[pixels[i]];

        int clusterCount = Math.Min(maxColors, pointCount);
        if (startingClusters.Count != 0) clusterCount = Math.Min(clusterCount, startingClusters.Count);
        if (clusterCount == 0) return [];

        var clusters = new double[clusterCount][];
        int created = 0;
        for (int i = 0; i < startingClusters.Count && i < clusterCount; i++)
        {
            clusters[i] = ColorUtils.LabFromArgb(startingClusters[i]);
            created++;
        }
        for (int i = created; i < clusterCount; i++)
        {
            double l = random.NextInt(101), a = random.NextInt(201) - 100, b = random.NextInt(201) - 100;
            clusters[i] = [l, a, b];
        }

        var clusterIndices = new int[pointCount];
        for (int i = 0; i < pointCount; i++) clusterIndices[i] = random.NextInt(clusterCount);

        var distanceToIndexMatrix = new DistanceAndIndex[clusterCount][];
        for (int i = 0; i < clusterCount; i++) distanceToIndexMatrix[i] = new DistanceAndIndex[clusterCount];
        var pixelCountSums = new int[clusterCount];

        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            for (int i = 0; i < clusterCount; i++)
            {
                for (int j = i + 1; j < clusterCount; j++)
                {
                    double distance = Distance(clusters[i], clusters[j]);
                    distanceToIndexMatrix[j][i] = new DistanceAndIndex { Distance = distance, Index = i };
                    distanceToIndexMatrix[i][j] = new DistanceAndIndex { Distance = distance, Index = j };
                }
                // Стабильная сортировка, как Arrays.sort для объектов в Java.
                var sorted = distanceToIndexMatrix[i].Select((d, k) => (d, k)).OrderBy(x => x.d.Distance).ThenBy(x => x.k).Select(x => x.d).ToArray();
                distanceToIndexMatrix[i] = sorted;
            }

            int pointsMoved = 0;
            for (int i = 0; i < pointCount; i++)
            {
                var point = points[i];
                int previousClusterIndex = clusterIndices[i];
                double previousDistance = Distance(point, clusters[previousClusterIndex]);
                double minimumDistance = previousDistance;
                int newClusterIndex = -1;
                for (int j = 0; j < clusterCount; j++)
                {
                    if (distanceToIndexMatrix[previousClusterIndex][j].Distance >= 4 * previousDistance) continue;
                    double distance = Distance(point, clusters[j]);
                    if (distance < minimumDistance)
                    {
                        minimumDistance = distance;
                        newClusterIndex = j;
                    }
                }
                if (newClusterIndex != -1)
                {
                    double distanceChange = Math.Abs(Math.Sqrt(minimumDistance) - Math.Sqrt(previousDistance));
                    if (distanceChange > MinMovementDistance)
                    {
                        pointsMoved++;
                        clusterIndices[i] = newClusterIndex;
                    }
                }
            }
            if (pointsMoved == 0 && iteration != 0) break;

            var aSums = new double[clusterCount];
            var bSums = new double[clusterCount];
            var cSums = new double[clusterCount];
            Array.Fill(pixelCountSums, 0);
            for (int i = 0; i < pointCount; i++)
            {
                int ci = clusterIndices[i];
                var point = points[i];
                int count = counts[i];
                pixelCountSums[ci] += count;
                aSums[ci] += point[0] * count;
                bSums[ci] += point[1] * count;
                cSums[ci] += point[2] * count;
            }
            for (int i = 0; i < clusterCount; i++)
            {
                int count = pixelCountSums[i];
                clusters[i] = count == 0 ? [0.0, 0.0, 0.0] : [aSums[i] / count, bSums[i] / count, cSums[i] / count];
            }
        }

        var argbToPopulation = new Dictionary<uint, int>();
        for (int i = 0; i < clusterCount; i++)
        {
            int count = pixelCountSums[i];
            if (count == 0) continue;
            uint possibleNewCluster = ColorUtils.ArgbFromLab(clusters[i][0], clusters[i][1], clusters[i][2]);
            argbToPopulation.TryAdd(possibleNewCluster, count);
        }
        return argbToPopulation;
    }

    private static double Distance(double[] a, double[] b)
    {
        double d0 = a[0] - b[0], d1 = a[1] - b[1], d2 = a[2] - b[2];
        return d0 * d0 + d1 * d1 + d2 * d2;
    }
}
