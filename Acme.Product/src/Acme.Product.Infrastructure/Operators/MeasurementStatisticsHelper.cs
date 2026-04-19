namespace Acme.Product.Infrastructure.Operators;

internal static class MeasurementStatisticsHelper
{
    public static double ComputePercentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            return 0.0;
        }

        var ordered = values.OrderBy(value => value).ToArray();
        if (ordered.Length == 1)
        {
            return ordered[0];
        }

        var clampedPercentile = Math.Clamp(percentile, 0.0, 1.0);
        var scaledIndex = clampedPercentile * (ordered.Length - 1);
        var lowerIndex = (int)Math.Floor(scaledIndex);
        var upperIndex = (int)Math.Ceiling(scaledIndex);
        if (lowerIndex == upperIndex)
        {
            return ordered[lowerIndex];
        }

        var weight = scaledIndex - lowerIndex;
        return ordered[lowerIndex] + ((ordered[upperIndex] - ordered[lowerIndex]) * weight);
    }

    public static double ComputeMedian(IReadOnlyList<double> values)
    {
        return ComputePercentile(values, 0.5);
    }

    public static double ComputeMedianAbsoluteDeviation(IReadOnlyList<double> values, double median)
    {
        if (values.Count == 0)
        {
            return 0.0;
        }

        var deviations = values.Select(value => Math.Abs(value - median)).ToArray();
        return ComputeMedian(deviations);
    }

    public static double ComputePopulationStdDev(IReadOnlyList<double> values, double mean)
    {
        if (values.Count == 0)
        {
            return 0.0;
        }

        var variance = values.Sum(value => (value - mean) * (value - mean)) / values.Count;
        return Math.Sqrt(Math.Max(variance, 0.0));
    }

    public static double ComputeStandardError(double stdDev, int sampleCount)
    {
        return sampleCount > 0 ? stdDev / Math.Sqrt(sampleCount) : 0.0;
    }

    public static (double MeanDegrees, double StdDegrees) ComputeCircularStatisticsDegrees(IReadOnlyList<double> anglesDegrees)
    {
        if (anglesDegrees.Count == 0)
        {
            return (double.NaN, double.NaN);
        }

        var radians = anglesDegrees.Select(angle => angle * Math.PI / 180.0).ToArray();
        var sinMean = radians.Average(Math.Sin);
        var cosMean = radians.Average(Math.Cos);

        var meanAngle = Math.Atan2(sinMean, cosMean);
        if (meanAngle < 0.0)
        {
            meanAngle += 2.0 * Math.PI;
        }

        var meanResultantLength = Math.Sqrt((sinMean * sinMean) + (cosMean * cosMean));
        meanResultantLength = Math.Clamp(meanResultantLength, 1e-12, 1.0);
        var stdDegrees = Math.Sqrt(Math.Max(0.0, -2.0 * Math.Log(meanResultantLength))) * 180.0 / Math.PI;

        return (meanAngle * 180.0 / Math.PI, stdDegrees);
    }

    public static double ComputeConfidenceFromUncertainty(double uncertainty)
    {
        if (!double.IsFinite(uncertainty))
        {
            return 0.0;
        }

        return Math.Clamp(1.0 / (1.0 + Math.Max(0.0, uncertainty)), 0.0, 1.0);
    }
}
