// GapMeasurementOperator.cs
// 间隙测量算子
// 计算目标边缘或轮廓之间的间隙距离
// 作者：蘅芜君
using System.Collections;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "间隙测量",
    Description = "Measures spacing using points or image projection.",
    Category = "检测",
    IconName = "gap",
    Keywords = new[] { "gap", "spacing", "pitch", "distance" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = false)]
[InputPort("Points", "Points", PortDataType.PointList, IsRequired = false)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OutputPort("Gaps", "Gaps", PortDataType.Any)]
[OutputPort("MeanGap", "Mean Gap", PortDataType.Float)]
[OutputPort("MinGap", "Min Gap", PortDataType.Float)]
[OutputPort("MaxGap", "Max Gap", PortDataType.Float)]
[OutputPort("P95Gap", "P95 Gap", PortDataType.Float)]
[OutputPort("StdDev", "StdDev", PortDataType.Float)]
[OutputPort("ValidSampleRate", "Valid Sample Rate", PortDataType.Float)]
[OutputPort("Count", "Count", PortDataType.Integer)]
[OperatorParam("Direction", "Direction", "enum", DefaultValue = "Auto", Options = new[] { "Horizontal|Horizontal", "Vertical|Vertical", "Auto|Auto" })]
[OperatorParam("MinGap", "Min Gap", "double", DefaultValue = 0.0, Min = 0.0, Max = 1000000.0)]
[OperatorParam("MaxGap", "Max Gap", "double", DefaultValue = 0.0, Min = 0.0, Max = 1000000.0)]
[OperatorParam("ExpectedCount", "Expected Count", "int", DefaultValue = 0, Min = 0, Max = 10000)]
[OperatorParam("RobustMode", "Robust Mode", "bool", DefaultValue = true)]
[OperatorParam("OutlierSigmaK", "Outlier Sigma K", "double", DefaultValue = 3.0, Min = 0.5, Max = 10.0)]
[OperatorParam("MinValidSamples", "Min Valid Samples", "int", DefaultValue = 0, Min = 0, Max = 256)]
[OperatorParam("MultiScanCount", "Multi Scan Count", "int", DefaultValue = 8, Min = 1, Max = 64)]
public class GapMeasurementOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.GapMeasurement;

    public GapMeasurementOperator(ILogger<GapMeasurementOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var direction = GetStringParam(@operator, "Direction", "Auto");
        var minGapFilter = GetDoubleParam(@operator, "MinGap", 0.0, 0.0, 1_000_000);
        var maxGapFilter = GetDoubleParam(@operator, "MaxGap", 0.0, 0.0, 1_000_000);
        var expectedCount = GetIntParam(@operator, "ExpectedCount", 0, 0, 10_000);
        var robustMode = GetBoolParam(@operator, "RobustMode", true);
        var outlierSigmaK = GetDoubleParam(@operator, "OutlierSigmaK", 3.0, 0.5, 10.0);
        var minValidSamples = GetIntParam(@operator, "MinValidSamples", 0, 0, 256);
        var multiScanCount = GetIntParam(@operator, "MultiScanCount", 8, 1, 64);

        List<double> gaps;
        Mat? imageToDraw = null;
        var diagnostics = GapDiagnostics.None;

        if (TryGetPointList(inputs, out var points) && points.Count >= 2)
        {
            var useHorizontal = ResolveDirectionByPoints(direction, points);
            gaps = ComputeGapsFromPoints(points, useHorizontal);

            if (TryGetInputImage(inputs, out var sourceImage) && sourceImage != null)
            {
                var src = sourceImage.GetMat();
                if (!src.Empty())
                {
                    imageToDraw = src.Clone();
                    DrawPointGaps(imageToDraw, points, useHorizontal);
                }
            }
        }
        else if (TryGetInputImage(inputs, out var imageWrapper) && imageWrapper != null)
        {
            var src = imageWrapper.GetMat();
            if (src.Empty())
            {
                return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid"));
            }

            imageToDraw = src.Clone();
            gaps = ComputeGapsFromImage(src, direction, robustMode, multiScanCount, out var featurePositions, out var useHorizontal, out diagnostics);
            DrawProjectionFeatures(imageToDraw, featurePositions, useHorizontal);
        }
        else
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Either Image or Points input is required"));
        }

        var rawGapCount = gaps.Count;
        if (minGapFilter > 0)
        {
            gaps = gaps.Where(g => g >= minGapFilter).ToList();
        }

        if (maxGapFilter > 0)
        {
            gaps = gaps.Where(g => g <= maxGapFilter).ToList();
        }

        if (expectedCount > 0 && gaps.Count > expectedCount)
        {
            gaps = gaps.Take(expectedCount).ToList();
        }

        if (robustMode)
        {
            gaps = ApplyGapOutlierFilter(gaps, outlierSigmaK);
        }

        var count = gaps.Count;
        if (minValidSamples > 0 && count < minValidSamples)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(
                $"[NoFeature] Valid gap samples {count} are below MinValidSamples {minValidSamples}"));
        }

        if (count == 0 && diagnostics.LowContrast)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("[LowContrast] Gap profile contrast is too low"));
        }

        if (count == 0 && diagnostics.OverExposed)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("[NoFeature] Overexposed scene suppressed stable peak detection"));
        }

        var mean = count > 0 ? gaps.Average() : 0.0;
        var min = count > 0 ? gaps.Min() : 0.0;
        var max = count > 0 ? gaps.Max() : 0.0;
        var p95 = count > 0 ? ComputePercentile(gaps.OrderBy(g => g).ToArray(), 0.95) : 0.0;
        var stdDev = count > 1 ? ComputeStdDev(gaps, mean) : 0.0;
        var validSampleRate = rawGapCount > 0 ? count / (double)rawGapCount : 0.0;
        var statusCode = count > 0 ? "OK" : "NoFeature";
        var statusMessage = count > 0 ? "Success" : "No gap found";
        var confidence = count > 0 ? Math.Clamp(validSampleRate, 0.0, 1.0) : 0.0;
        var uncertainty = count > 0 ? stdDev : double.NaN;

        var outputData = new Dictionary<string, object>
        {
            { "Gaps", gaps },
            { "MeanGap", mean },
            { "MinGap", min },
            { "MaxGap", max },
            { "P95Gap", p95 },
            { "StdDev", stdDev },
            { "ValidSampleRate", validSampleRate },
            { "Count", count },
            { "RawCount", rawGapCount },
            { "RobustMode", robustMode },
            { "OutlierSigmaK", outlierSigmaK },
            { "MultiScanCount", multiScanCount },
            { "StatusCode", statusCode },
            { "StatusMessage", statusMessage },
            { "Confidence", confidence },
            { "UncertaintyPx", uncertainty },
            { "LowContrast", diagnostics.LowContrast },
            { "OverExposed", diagnostics.OverExposed },
            { "WideBrightStripe", diagnostics.WideBrightStripe }
        };

        if (imageToDraw != null)
        {
            Cv2.PutText(
                imageToDraw,
                $"Gap Count:{count} Mean:{mean:F2}px",
                new Point(8, 24),
                HersheyFonts.HersheySimplex,
                0.6,
                new Scalar(255, 255, 255),
                2);

            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(imageToDraw, outputData)));
        }

        return Task.FromResult(OperatorExecutionOutput.Success(outputData));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var direction = GetStringParam(@operator, "Direction", "Auto");
        var validDirections = new[] { "Horizontal", "Vertical", "Auto" };
        if (!validDirections.Contains(direction, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Direction must be Horizontal, Vertical or Auto");
        }

        var minGap = GetDoubleParam(@operator, "MinGap", 0.0);
        var maxGap = GetDoubleParam(@operator, "MaxGap", 0.0);
        if (minGap < 0 || maxGap < 0)
        {
            return ValidationResult.Invalid("MinGap and MaxGap must be greater than or equal to 0");
        }

        if (maxGap > 0 && minGap > maxGap)
        {
            return ValidationResult.Invalid("MinGap cannot be greater than MaxGap");
        }

        var outlierSigmaK = GetDoubleParam(@operator, "OutlierSigmaK", 3.0);
        if (outlierSigmaK < 0.5 || outlierSigmaK > 10.0)
        {
            return ValidationResult.Invalid("OutlierSigmaK must be within [0.5, 10.0]");
        }

        var minValidSamples = GetIntParam(@operator, "MinValidSamples", 0);
        if (minValidSamples < 0 || minValidSamples > 256)
        {
            return ValidationResult.Invalid("MinValidSamples must be within [0, 256]");
        }

        var multiScanCount = GetIntParam(@operator, "MultiScanCount", 8);
        if (multiScanCount < 1 || multiScanCount > 64)
        {
            return ValidationResult.Invalid("MultiScanCount must be within [1, 64]");
        }

        return ValidationResult.Valid();
    }

    private static bool ResolveDirectionByPoints(string direction, IReadOnlyList<Position> points)
    {
        if (direction.Equals("Horizontal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (direction.Equals("Vertical", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var xRange = points.Max(p => p.X) - points.Min(p => p.X);
        var yRange = points.Max(p => p.Y) - points.Min(p => p.Y);
        return xRange >= yRange;
    }

    private static List<double> ComputeGapsFromPoints(IReadOnlyList<Position> points, bool horizontal)
    {
        var sorted = horizontal
            ? points.OrderBy(p => p.X).ToList()
            : points.OrderBy(p => p.Y).ToList();

        var gaps = new List<double>(Math.Max(0, sorted.Count - 1));
        for (var i = 1; i < sorted.Count; i++)
        {
            var gap = horizontal
                ? sorted[i].X - sorted[i - 1].X
                : sorted[i].Y - sorted[i - 1].Y;
            if (gap > 0)
            {
                gaps.Add(gap);
            }
        }

        return gaps;
    }

    private static List<double> ComputeGapsFromImage(
        Mat src,
        string direction,
        bool robustMode,
        int multiScanCount,
        out List<double> positions,
        out bool horizontal,
        out GapDiagnostics diagnostics)
    {
        using var gray = new Mat();
        if (src.Channels() == 1)
        {
            src.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }

        var xProjection = BuildAggregatedProjection(gray, true, multiScanCount);
        var yProjection = BuildAggregatedProjection(gray, false, multiScanCount);

        if (direction.Equals("Horizontal", StringComparison.OrdinalIgnoreCase))
        {
            horizontal = true;
        }
        else if (direction.Equals("Vertical", StringComparison.OrdinalIgnoreCase))
        {
            horizontal = false;
        }
        else
        {
            horizontal = ComputeVariance(xProjection) >= ComputeVariance(yProjection);
        }

        var profile = horizontal ? xProjection : yProjection;
        diagnostics = AssessDiagnostics(gray, profile);
        var analysis = AnalyzeProfileFeatures(profile, robustMode);
        positions = analysis.FeaturePositions;
        return analysis.Gaps;
    }

    private static double[] BuildAggregatedProjection(Mat gray, bool horizontal, int multiScanCount)
    {
        var scanCount = Math.Clamp(multiScanCount, 1, 64);
        if (scanCount <= 1)
        {
            return GetProjection(gray, horizontal);
        }

        var profileLength = horizontal ? gray.Cols : gray.Rows;
        var accumulated = new double[profileLength];
        var bands = horizontal ? gray.Rows : gray.Cols;
        var effectiveScans = 0;

        for (var i = 0; i < scanCount; i++)
        {
            var start = i * bands / scanCount;
            var end = (i + 1) * bands / scanCount;
            var length = Math.Max(1, end - start);

            Rect roi;
            if (horizontal)
            {
                roi = new Rect(0, start, gray.Cols, length);
            }
            else
            {
                roi = new Rect(start, 0, length, gray.Rows);
            }

            using var band = new Mat(gray, roi);
            var projection = GetProjection(band, horizontal);
            for (var p = 0; p < projection.Length; p++)
            {
                accumulated[p] += projection[p];
            }

            effectiveScans++;
        }

        if (effectiveScans <= 1)
        {
            return accumulated;
        }

        for (var i = 0; i < accumulated.Length; i++)
        {
            accumulated[i] /= effectiveScans;
        }

        return accumulated;
    }

    private static double[] GetProjection(Mat gray, bool horizontal)
    {
        using var projection = new Mat();
        Cv2.Reduce(gray, projection, horizontal ? ReduceDimension.Row : ReduceDimension.Column, ReduceTypes.Avg, MatType.CV_64F);

        var length = horizontal ? projection.Cols : projection.Rows;
        var values = new double[length];

        for (var i = 0; i < length; i++)
        {
            values[i] = horizontal ? projection.At<double>(0, i) : projection.At<double>(i, 0);
        }

        return values;
    }

    private static double ComputeVariance(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var mean = values.Average();
        var sum = 0.0;
        for (var i = 0; i < values.Count; i++)
        {
            var diff = values[i] - mean;
            sum += diff * diff;
        }

        return sum / values.Count;
    }

    private static ProjectionGapAnalysis AnalyzeProfileFeatures(IReadOnlyList<double> profile, bool robustMode)
    {
        var featurePositions = new List<double>();
        var gaps = new List<double>();
        if (profile.Count < 5)
        {
            return new ProjectionGapAnalysis(featurePositions, gaps);
        }

        var threshold = IndustrialCaliperKernel.EstimateEdgeThreshold(profile, minimumThreshold: 2.0);
        var rawEdges = IndustrialCaliperKernel.DetectEdges(profile, threshold, "Both", sigma: robustMode ? 1.6 : 1.2)
            .OrderBy(edge => edge.Position)
            .ToList();
        if (rawEdges.Count < 2)
        {
            return new ProjectionGapAnalysis(featurePositions, gaps);
        }

        var minPeakDistance = Math.Max(6.0, profile.Count / 150.0);
        var edges = new List<IndustrialCaliperEdge>(rawEdges.Count);
        foreach (var edge in rawEdges)
        {
            if (edges.Count > 0 && edge.Position - edges[^1].Position < minPeakDistance)
            {
                if (edge.Strength > edges[^1].Strength)
                {
                    edges[^1] = edge;
                }

                continue;
            }

            edges.Add(edge);
        }

        featurePositions = edges.Select(edge => edge.Position).ToList();
        var brightStripePairs = IndustrialCaliperKernel.BuildPairs(edges, "positive_to_negative", int.MaxValue);
        var darkStripePairs = IndustrialCaliperKernel.BuildPairs(edges, "negative_to_positive", int.MaxValue);
        var brightGaps = ComputeInterStripeGaps(brightStripePairs);
        var darkGaps = ComputeInterStripeGaps(darkStripePairs);
        gaps = ChooseMoreStableGapSet(brightGaps, darkGaps);
        if (gaps.Count == 0)
        {
            var centerFallback = BuildCenterFallbackGaps(profile, robustMode);
            if (centerFallback.Count > 0)
            {
                gaps = centerFallback;
            }
        }

        return new ProjectionGapAnalysis(featurePositions, gaps);
    }

    private static List<double> BuildCenterFallbackGaps(IReadOnlyList<double> profile, bool robustMode)
    {
        var centers = IndustrialCaliperKernel.DetectBrightStripeCenters(profile, IndustrialCaliperKernel.EstimateEdgeThreshold(profile, minimumThreshold: 2.0), sigma: robustMode ? 1.6 : 1.2);
        if (centers.Count < 2)
        {
            return new List<double>();
        }

        var gaps = new List<double>(centers.Count - 1);
        foreach (var pair in centers.OrderBy(static value => value).Zip(centers.OrderBy(static value => value).Skip(1)))
        {
            var gap = pair.Second - pair.First;
            if (gap > 0)
            {
                gaps.Add(gap);
            }
        }

        return gaps;
    }

    private static List<double> ComputeInterStripeGaps(IReadOnlyList<IndustrialCaliperEdgePair> stripePairs)
    {
        var gaps = new List<double>(Math.Max(0, stripePairs.Count - 1));
        for (var i = 1; i < stripePairs.Count; i++)
        {
            var gap = stripePairs[i].First.Position - stripePairs[i - 1].Second.Position;
            if (gap > 0)
            {
                gaps.Add(gap);
            }
        }

        return gaps;
    }

    private static List<double> ChooseMoreStableGapSet(
        IReadOnlyList<double> brightGaps,
        IReadOnlyList<double> darkGaps)
    {
        if (brightGaps.Count == 0)
        {
            return darkGaps.ToList();
        }

        if (darkGaps.Count == 0)
        {
            return brightGaps.ToList();
        }

        if (brightGaps.Count != darkGaps.Count)
        {
            return brightGaps.Count > darkGaps.Count
                ? brightGaps.ToList()
                : darkGaps.ToList();
        }

        var brightStdDev = ComputeStdDev(brightGaps, brightGaps.Average());
        var darkStdDev = ComputeStdDev(darkGaps, darkGaps.Average());
        return brightStdDev <= darkStdDev
            ? brightGaps.ToList()
            : darkGaps.ToList();
    }

    private static double[] SmoothProfile(IReadOnlyList<double> profile, int radius)
    {
        var smoothed = new double[profile.Count];
        if (profile.Count == 0)
        {
            return smoothed;
        }

        for (var i = 0; i < profile.Count; i++)
        {
            var start = Math.Max(0, i - radius);
            var end = Math.Min(profile.Count - 1, i + radius);
            var sum = 0.0;
            var count = 0;

            for (var j = start; j <= end; j++)
            {
                sum += profile[j];
                count++;
            }

            smoothed[i] = count > 0 ? sum / count : profile[i];
        }

        return smoothed;
    }

    private static double ComputeRobustThreshold(IReadOnlyList<double> values, double median, double robustSigma)
    {
        if (values.Count == 0)
        {
            return double.PositiveInfinity;
        }

        if (robustSigma > 1e-6)
        {
            return median + robustSigma * 2.0;
        }

        var positive = values.Where(v => v > median + 1e-6).OrderBy(v => v).ToArray();
        if (positive.Length == 0)
        {
            return double.PositiveInfinity;
        }

        var lowerDecile = ComputePercentile(positive, 0.1);
        return Math.Max(median + 1.0, lowerDecile * 0.9);
    }

    private static double ComputeMedian(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0.0;
        }

        var sorted = values.OrderBy(v => v).ToArray();
        return ComputePercentile(sorted, 0.5);
    }

    private static double ComputePercentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
        {
            return 0.0;
        }

        if (sortedValues.Count == 1)
        {
            return sortedValues[0];
        }

        var p = Math.Clamp(percentile, 0.0, 1.0);
        var position = p * (sortedValues.Count - 1);
        var lowerIndex = (int)Math.Floor(position);
        var upperIndex = (int)Math.Ceiling(position);
        if (lowerIndex == upperIndex)
        {
            return sortedValues[lowerIndex];
        }

        var ratio = position - lowerIndex;
        return sortedValues[lowerIndex] * (1 - ratio) + sortedValues[upperIndex] * ratio;
    }

    private static List<double> ApplyGapOutlierFilter(IReadOnlyList<double> gaps, double sigmaK)
    {
        if (gaps.Count <= 2)
        {
            return gaps.ToList();
        }

        var ordered = gaps.OrderBy(v => v).ToArray();
        var median = ComputePercentile(ordered, 0.5);
        var deviations = gaps.Select(v => Math.Abs(v - median)).OrderBy(v => v).ToArray();
        var mad = ComputePercentile(deviations, 0.5);
        var robustSigma = mad * 1.4826;
        if (robustSigma < 1e-6)
        {
            return gaps.ToList();
        }

        var threshold = robustSigma * sigmaK;
        return gaps.Where(v => Math.Abs(v - median) <= threshold).ToList();
    }

    private static double ComputeStdDev(IReadOnlyList<double> values, double mean)
    {
        if (values.Count <= 1)
        {
            return 0.0;
        }

        var variance = values.Select(v => (v - mean) * (v - mean)).Sum() / (values.Count - 1);
        return Math.Sqrt(Math.Max(0.0, variance));
    }

    private static GapDiagnostics AssessDiagnostics(Mat gray, IReadOnlyList<double> profile)
    {
        Cv2.MeanStdDev(gray, out _, out var stdDev);
        var lowContrast = stdDev.Val0 < 8.0;

        using var overExposedMask = new Mat();
        Cv2.Threshold(gray, overExposedMask, 245, 255, ThresholdTypes.Binary);
        var overExposedRatio = gray.Total() > 0
            ? Cv2.CountNonZero(overExposedMask) / (double)gray.Total()
            : 0.0;
        var overExposed = overExposedRatio >= 0.35;

        var sorted = profile.OrderBy(v => v).ToArray();
        var median = ComputePercentile(sorted, 0.5);
        var brightThreshold = median + Math.Max(12.0, stdDev.Val0 * 2.0);
        var widestRun = GetMaxRunLength(profile, v => v >= brightThreshold);
        var wideBrightStripe = profile.Count > 0 && widestRun >= Math.Max(6, profile.Count / 8);

        return new GapDiagnostics(lowContrast, overExposed, wideBrightStripe);
    }

    private static int GetMaxRunLength(IReadOnlyList<double> values, Func<double, bool> predicate)
    {
        var maxRun = 0;
        var current = 0;

        for (var i = 0; i < values.Count; i++)
        {
            if (predicate(values[i]))
            {
                current++;
                if (current > maxRun)
                {
                    maxRun = current;
                }
            }
            else
            {
                current = 0;
            }
        }

        return maxRun;
    }

    private static void DrawProjectionFeatures(Mat image, IReadOnlyList<double> positions, bool horizontal)
    {
        foreach (var pos in positions)
        {
            var rounded = (int)Math.Round(pos);
            if (horizontal)
            {
                Cv2.Line(image, new Point(rounded, 0), new Point(rounded, image.Rows - 1), new Scalar(0, 255, 255), 1);
            }
            else
            {
                Cv2.Line(image, new Point(0, rounded), new Point(image.Cols - 1, rounded), new Scalar(0, 255, 255), 1);
            }
        }
    }

    private static void DrawPointGaps(Mat image, IReadOnlyList<Position> points, bool horizontal)
    {
        foreach (var point in points)
        {
            Cv2.Circle(image, new Point((int)Math.Round(point.X), (int)Math.Round(point.Y)), 3, new Scalar(0, 255, 0), -1);
        }

        var sorted = horizontal
            ? points.OrderBy(p => p.X).ToList()
            : points.OrderBy(p => p.Y).ToList();

        for (var i = 1; i < sorted.Count; i++)
        {
            var p1 = new Point((int)Math.Round(sorted[i - 1].X), (int)Math.Round(sorted[i - 1].Y));
            var p2 = new Point((int)Math.Round(sorted[i].X), (int)Math.Round(sorted[i].Y));
            Cv2.Line(image, p1, p2, new Scalar(255, 0, 0), 1);
        }
    }

    private static bool TryGetPointList(Dictionary<string, object>? inputs, out List<Position> points)
    {
        points = new List<Position>();

        if (inputs == null || !inputs.TryGetValue("Points", out var pointsObj) || pointsObj == null)
        {
            return false;
        }

        if (pointsObj is IEnumerable<Position> typed)
        {
            points = typed.ToList();
            return points.Count > 0;
        }

        if (pointsObj is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (TryParsePoint(item, out var point))
                {
                    points.Add(point);
                }
            }
        }

        return points.Count > 0;
    }

    private static bool TryParsePoint(object? obj, out Position point)
    {
        point = new Position(0, 0);
        if (obj == null)
        {
            return false;
        }

        if (obj is Position p)
        {
            point = p;
            return true;
        }

        if (obj is Point cvPoint)
        {
            point = new Position(cvPoint.X, cvPoint.Y);
            return true;
        }

        if (obj is Point2f cvPointF)
        {
            point = new Position(cvPointF.X, cvPointF.Y);
            return true;
        }

        if (obj is Point2d cvPointD)
        {
            point = new Position(cvPointD.X, cvPointD.Y);
            return true;
        }

        if (obj is IDictionary<string, object> dict &&
            TryGetDouble(dict, "X", out var x) &&
            TryGetDouble(dict, "Y", out var y))
        {
            point = new Position(x, y);
            return true;
        }

        if (obj is IDictionary legacy)
        {
            var normalized = legacy.Cast<DictionaryEntry>()
                .Where(e => e.Key != null)
                .ToDictionary(e => e.Key!.ToString() ?? string.Empty, e => e.Value ?? 0.0, StringComparer.OrdinalIgnoreCase);
            return TryParsePoint(normalized, out point);
        }

        return false;
    }

    private static bool TryGetDouble(IDictionary<string, object> dict, string key, out double value)
    {
        value = 0;
        if (!dict.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        return raw switch
        {
            double d => (value = d) == d,
            float f => (value = f) == f,
            int i => (value = i) == i,
            long l => (value = l) == l,
            _ => double.TryParse(raw.ToString(), out value)
        };
    }

    private readonly record struct GapDiagnostics(bool LowContrast, bool OverExposed, bool WideBrightStripe)
    {
        public static GapDiagnostics None => new(false, false, false);
    }

    private readonly record struct ProjectionGapAnalysis(
        List<double> FeaturePositions,
        List<double> Gaps);
}

