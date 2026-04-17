using Acme.Product.Core.ValueObjects;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

internal enum IndustrialCaliperPolarity
{
    DarkToLight = 0,
    LightToDark = 1
}

internal readonly record struct IndustrialCaliperEdge(
    double Position,
    double Strength,
    IndustrialCaliperPolarity Polarity);

internal readonly record struct IndustrialCaliperEdgePair(
    IndustrialCaliperEdge First,
    IndustrialCaliperEdge Second);

internal static class IndustrialCaliperKernel
{
    public static double[] SampleBandProfile(Mat gray, Point2d start, Point2d end, double averagingThickness, int sampleCount)
    {
        var profile = new double[Math.Max(sampleCount, 2)];
        var length = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
        if (length <= 1e-9)
        {
            return profile;
        }

        var dirX = (end.X - start.X) / length;
        var dirY = (end.Y - start.Y) / length;
        var normalX = -dirY;
        var normalY = dirX;

        var acrossCount = Math.Max(1, (int)Math.Ceiling(Math.Max(1.0, averagingThickness)));
        var halfThickness = averagingThickness / 2.0;

        for (var i = 0; i < profile.Length; i++)
        {
            var t = profile.Length <= 1 ? 0.0 : (double)i / (profile.Length - 1);
            var centerX = start.X + ((end.X - start.X) * t);
            var centerY = start.Y + ((end.Y - start.Y) * t);

            double sum = 0.0;
            for (var j = 0; j < acrossCount; j++)
            {
                var offset = acrossCount == 1
                    ? 0.0
                    : -halfThickness + (averagingThickness * j / (acrossCount - 1));

                var sampleX = centerX + (normalX * offset);
                var sampleY = centerY + (normalY * offset);
                sum += SampleGrayBilinear(gray, sampleX, sampleY);
            }

            profile[i] = sum / acrossCount;
        }

        return profile;
    }

    public static double EstimateEdgeThreshold(IReadOnlyList<double> profile, double minimumThreshold = 3.0)
    {
        if (profile.Count < 3)
        {
            return minimumThreshold;
        }

        var smoothed = GaussianSmooth(profile, 1.0);
        var derivative = ComputeDerivative(smoothed);
        var magnitudes = derivative.Select(Math.Abs).Where(value => double.IsFinite(value)).ToArray();
        if (magnitudes.Length == 0)
        {
            return minimumThreshold;
        }

        var median = ComputePercentile(magnitudes, 0.5);
        var deviations = magnitudes.Select(value => Math.Abs(value - median)).ToArray();
        var mad = ComputePercentile(deviations, 0.5) * 1.4826;
        if (mad <= 1e-6)
        {
            return Math.Max(minimumThreshold, median * 0.75);
        }

        return Math.Max(minimumThreshold, median + (mad * 2.0));
    }

    public static List<IndustrialCaliperEdge> DetectEdges(
        IReadOnlyList<double> profile,
        double threshold,
        string polarity,
        double sigma = 1.2)
    {
        var edges = new List<IndustrialCaliperEdge>();
        if (profile.Count < 5)
        {
            return edges;
        }

        var smoothed = GaussianSmooth(profile, sigma);
        var derivative = ComputeDerivative(smoothed);
        var minSeparation = Math.Max(1, profile.Count / 128);

        for (var i = 1; i < derivative.Length - 1; i++)
        {
            var current = derivative[i];
            var currentMagnitude = Math.Abs(current);
            if (currentMagnitude < threshold)
            {
                continue;
            }

            if (currentMagnitude < Math.Abs(derivative[i - 1]) || currentMagnitude < Math.Abs(derivative[i + 1]))
            {
                continue;
            }

            var edgePolarity = current >= 0
                ? IndustrialCaliperPolarity.DarkToLight
                : IndustrialCaliperPolarity.LightToDark;

            if (!PolarityMatches(edgePolarity, polarity))
            {
                continue;
            }

            var offset = QuadraticPeakOffset(Math.Abs(derivative[i - 1]), currentMagnitude, Math.Abs(derivative[i + 1]));
            var position = Math.Clamp(i + offset, 0.0, profile.Count - 1.0);
            var edge = new IndustrialCaliperEdge(position, currentMagnitude, edgePolarity);

            if (edges.Count > 0 && position - edges[^1].Position < minSeparation)
            {
                if (edge.Strength > edges[^1].Strength)
                {
                    edges[^1] = edge;
                }

                continue;
            }

            edges.Add(edge);
        }

        return edges;
    }

    public static List<IndustrialCaliperEdgePair> BuildPairs(
        IReadOnlyList<IndustrialCaliperEdge> edges,
        string pairDirection,
        int maxPairs)
    {
        var pairs = new List<IndustrialCaliperEdgePair>();
        if (edges.Count < 2 || maxPairs <= 0)
        {
            return pairs;
        }

        var normalizedDirection = pairDirection?.Trim().ToLowerInvariant() ?? "any";
        for (var i = 0; i < edges.Count - 1 && pairs.Count < maxPairs; i++)
        {
            var first = edges[i];
            var second = edges[i + 1];
            if (first.Polarity == second.Polarity)
            {
                continue;
            }

            if (!PairDirectionMatches(first.Polarity, second.Polarity, normalizedDirection))
            {
                continue;
            }

            pairs.Add(new IndustrialCaliperEdgePair(first, second));
            i++;
        }

        return pairs;
    }

    public static List<double> DetectBrightStripeCenters(
        IReadOnlyList<double> profile,
        double threshold,
        double sigma = 1.2,
        int? maxCenters = null)
    {
        var edges = DetectEdges(profile, threshold, "Both", sigma);
        var pairs = BuildPairs(edges, "positive_to_negative", maxCenters ?? int.MaxValue);
        return pairs.Select(static pair => (pair.First.Position + pair.Second.Position) * 0.5).ToList();
    }

    public static List<double> DetectStripeCenters(
        IReadOnlyList<double> profile,
        double threshold,
        string stripePolarity = "Auto",
        double sigma = 1.2,
        int? maxCenters = null)
    {
        var edges = DetectEdges(profile, threshold, "Both", sigma);
        var maxPairCount = maxCenters ?? int.MaxValue;
        var centers = new List<double>();

        if (!stripePolarity.Equals("Dark", StringComparison.OrdinalIgnoreCase))
        {
            centers.AddRange(BuildPairs(edges, "positive_to_negative", maxPairCount)
                .Select(static pair => (pair.First.Position + pair.Second.Position) * 0.5));
        }

        if (!stripePolarity.Equals("Bright", StringComparison.OrdinalIgnoreCase))
        {
            centers.AddRange(BuildPairs(edges, "negative_to_positive", maxPairCount)
                .Select(static pair => (pair.First.Position + pair.Second.Position) * 0.5));
        }

        return centers
            .OrderBy(static value => value)
            .Take(maxPairCount)
            .ToList();
    }

    public static Position InterpolatePosition(Position start, Position end, double profilePosition, int sampleCount)
    {
        var t = sampleCount <= 1
            ? 0.0
            : Math.Clamp(profilePosition / (sampleCount - 1), 0.0, 1.0);

        return new Position(
            start.X + ((end.X - start.X) * t),
            start.Y + ((end.Y - start.Y) * t));
    }

    public static Position InterpolatePosition(Point2d start, Point2d end, double profilePosition, int sampleCount)
    {
        var t = sampleCount <= 1
            ? 0.0
            : Math.Clamp(profilePosition / (sampleCount - 1), 0.0, 1.0);

        return new Position(
            start.X + ((end.X - start.X) * t),
            start.Y + ((end.Y - start.Y) * t));
    }

    private static bool PolarityMatches(IndustrialCaliperPolarity detected, string polarity)
    {
        return polarity.Trim().ToLowerInvariant() switch
        {
            "darktolight" => detected == IndustrialCaliperPolarity.DarkToLight,
            "lighttodark" => detected == IndustrialCaliperPolarity.LightToDark,
            "both" => true,
            _ => true
        };
    }

    private static bool PairDirectionMatches(
        IndustrialCaliperPolarity first,
        IndustrialCaliperPolarity second,
        string pairDirection)
    {
        return pairDirection switch
        {
            "positive_to_negative" => first == IndustrialCaliperPolarity.DarkToLight && second == IndustrialCaliperPolarity.LightToDark,
            "negative_to_positive" => first == IndustrialCaliperPolarity.LightToDark && second == IndustrialCaliperPolarity.DarkToLight,
            "any" => true,
            _ => true
        };
    }

    private static double[] GaussianSmooth(IReadOnlyList<double> profile, double sigma)
    {
        if (profile.Count == 0)
        {
            return Array.Empty<double>();
        }

        var radius = Math.Clamp((int)Math.Ceiling(sigma * 3.0), 1, 8);
        var kernel = BuildGaussianKernel(radius, sigma);
        var smoothed = new double[profile.Count];

        for (var i = 0; i < profile.Count; i++)
        {
            double sum = 0.0;
            double weightSum = 0.0;
            for (var k = -radius; k <= radius; k++)
            {
                var idx = Math.Clamp(i + k, 0, profile.Count - 1);
                var weight = kernel[k + radius];
                sum += profile[idx] * weight;
                weightSum += weight;
            }

            smoothed[i] = weightSum > 0 ? sum / weightSum : profile[i];
        }

        return smoothed;
    }

    private static double[] ComputeDerivative(IReadOnlyList<double> profile)
    {
        var derivative = new double[profile.Count];
        if (profile.Count == 0)
        {
            return derivative;
        }

        derivative[0] = profile.Count > 1 ? profile[1] - profile[0] : 0.0;
        derivative[^1] = profile.Count > 1 ? profile[^1] - profile[^2] : 0.0;

        for (var i = 1; i < profile.Count - 1; i++)
        {
            derivative[i] = (profile[i + 1] - profile[i - 1]) * 0.5;
        }

        return derivative;
    }

    private static double QuadraticPeakOffset(double left, double center, double right)
    {
        var denominator = left - (2.0 * center) + right;
        if (Math.Abs(denominator) <= 1e-9)
        {
            return 0.0;
        }

        return Math.Clamp(0.5 * (left - right) / denominator, -0.5, 0.5);
    }

    private static double[] BuildGaussianKernel(int radius, double sigma)
    {
        var kernel = new double[(radius * 2) + 1];
        double sum = 0.0;

        for (var i = -radius; i <= radius; i++)
        {
            var value = Math.Exp(-(i * i) / (2.0 * sigma * sigma));
            kernel[i + radius] = value;
            sum += value;
        }

        if (sum <= 1e-12)
        {
            return kernel;
        }

        for (var i = 0; i < kernel.Length; i++)
        {
            kernel[i] /= sum;
        }

        return kernel;
    }

    private static double ComputePercentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            return 0.0;
        }

        var ordered = values.OrderBy(static value => value).ToArray();
        if (ordered.Length == 1)
        {
            return ordered[0];
        }

        var position = Math.Clamp(percentile, 0.0, 1.0) * (ordered.Length - 1);
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return ordered[lower];
        }

        var ratio = position - lower;
        return ordered[lower] * (1.0 - ratio) + ordered[upper] * ratio;
    }

    private static double SampleGrayBilinear(Mat gray, double x, double y)
    {
        var clampedX = Math.Clamp(x, 0.0, gray.Width - 1.0);
        var clampedY = Math.Clamp(y, 0.0, gray.Height - 1.0);
        var x0 = (int)Math.Floor(clampedX);
        var y0 = (int)Math.Floor(clampedY);
        var x1 = Math.Min(x0 + 1, gray.Width - 1);
        var y1 = Math.Min(y0 + 1, gray.Height - 1);
        var fx = clampedX - x0;
        var fy = clampedY - y0;

        var v00 = SampleGray(gray, x0, y0);
        var v10 = SampleGray(gray, x1, y0);
        var v01 = SampleGray(gray, x0, y1);
        var v11 = SampleGray(gray, x1, y1);

        var top = v00 + ((v10 - v00) * fx);
        var bottom = v01 + ((v11 - v01) * fx);
        return top + ((bottom - top) * fy);
    }

    private static double SampleGray(Mat gray, int x, int y)
    {
        return gray.Depth() switch
        {
            MatType.CV_8U => gray.At<byte>(y, x),
            MatType.CV_32F => gray.At<float>(y, x),
            MatType.CV_64F => gray.At<double>(y, x),
            _ => gray.At<byte>(y, x)
        };
    }
}
