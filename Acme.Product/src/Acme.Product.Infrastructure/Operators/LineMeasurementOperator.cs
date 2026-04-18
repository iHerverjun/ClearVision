using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "直线测量",
    Description = "Detects line features and reports line direction, span and fitting diagnostics.",
    Category = "检测",
    IconName = "line-measure",
    Keywords = new[] { "直线", "线段", "角度", "霍夫", "Line", "Hough", "FitLine" }
)]
[InputPort("Image", "输入图像", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "结果图像", PortDataType.Image)]
[OutputPort("Angle", "角度", PortDataType.Float)]
[OutputPort("Length", "长度", PortDataType.Float)]
[OutputPort("Line", "直线数据", PortDataType.LineData)]
[OutputPort("LineCount", "直线数量", PortDataType.Integer)]
[OperatorParam("Method", "检测方法", "enum", DefaultValue = "ProbabilisticHough", Options = new[] { "HoughLines|霍夫直线", "ProbabilisticHough|概率霍夫直线", "FitLine|拟合直线" })]
[OperatorParam("Threshold", "累加阈值", "int", DefaultValue = 100, Min = 1)]
[OperatorParam("MinLength", "最小长度", "double", DefaultValue = 50.0, Min = 0.0)]
[OperatorParam("MaxGap", "最大间隙", "double", DefaultValue = 10.0, Min = 0.0)]
public class LineMeasurementOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.LineMeasurement;

    public LineMeasurementOperator(ILogger<LineMeasurementOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, "Image", out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供输入图像"));
        }

        var method = ResolveMethod(GetStringParam(@operator, "Method", "ProbabilisticHough"));
        if (method == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Method must be HoughLines, ProbabilisticHough or FitLine"));
        }

        var threshold = GetIntParam(@operator, "Threshold", 100, min: 1);
        var minLength = GetDoubleParam(@operator, "MinLength", 50.0, min: 0);
        var maxGap = GetDoubleParam(@operator, "MaxGap", 10.0, min: 0);

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        var resultImage = src.Clone();
        using var gray = new Mat();
        if (src.Channels() == 1)
        {
            src.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }

        using var edges = new Mat();
        Cv2.Canny(gray, edges, 50, 150);

        var lineResults = (method switch
        {
            "HoughLines" => DetectHoughLines(gray, edges, resultImage, src.Width, src.Height, threshold),
            "ProbabilisticHough" => DetectProbabilisticHough(gray, edges, resultImage, threshold, minLength, maxGap),
            "FitLine" => DetectFitLine(gray, edges, resultImage, src.Width, src.Height, threshold, minLength, maxGap),
            _ => new List<LineMeasurementResult>()
        })
        .OrderBy(result => double.IsFinite(result.ResidualMean) ? result.ResidualMean : double.PositiveInfinity)
        .ThenByDescending(result => result.Length)
        .ToList();

        if (lineResults.Count == 0)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("No valid line found"));
        }

        var firstLine = lineResults[0];
        var additionalData = new Dictionary<string, object>
        {
            { "LineCount", lineResults.Count },
            { "Line", firstLine.Line },
            { "Angle", firstLine.Angle },
            { "Length", firstLine.Length },
            { "ResidualMean", firstLine.ResidualMean },
            { "ResidualMax", firstLine.ResidualMax },
            { "Method", method },
            { "Lines", lineResults.Select(BuildLinePayload).ToList() },
            { "StatusCode", "OK" },
            { "StatusMessage", "Success" },
            { "Confidence", 1.0 },
            { "UncertaintyPx", double.IsFinite(firstLine.ResidualMean) ? firstLine.ResidualMean : 0.2 }
        };

        foreach (var kvp in firstLine.Diagnostics)
        {
            additionalData[kvp.Key] = kvp.Value;
        }

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, additionalData)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var method = ResolveMethod(GetStringParam(@operator, "Method", "ProbabilisticHough"));
        var threshold = GetIntParam(@operator, "Threshold", 100);
        var minLength = GetDoubleParam(@operator, "MinLength", 50.0);
        var maxGap = GetDoubleParam(@operator, "MaxGap", 10.0);

        if (method == null)
        {
            return ValidationResult.Invalid("检测方法必须是 HoughLines、ProbabilisticHough 或 FitLine");
        }

        if (threshold < 1)
        {
            return ValidationResult.Invalid("累加阈值必须大于等于 1");
        }

        if (minLength < 0)
        {
            return ValidationResult.Invalid("最小长度不能为负数");
        }

        if (maxGap < 0)
        {
            return ValidationResult.Invalid("最大间隙不能为负数");
        }

        return ValidationResult.Valid();
    }

    private static string? ResolveMethod(string? raw)
    {
        return raw?.Trim() switch
        {
            "HoughLine" => "HoughLines",
            "HoughLines" => "HoughLines",
            "ProbabilisticHough" => "ProbabilisticHough",
            "FitLine" => "FitLine",
            _ => null
        };
    }

    private static List<LineMeasurementResult> DetectHoughLines(Mat gray, Mat edges, Mat resultImage, int width, int height, int threshold)
    {
        var results = new List<LineMeasurementResult>();
        var lines = Cv2.HoughLines(edges, 1, Math.PI / 180, threshold);
        if (lines == null)
        {
            return results;
        }

        foreach (var line in lines)
        {
            if (!TryCreateImageSpan(line.Rho, line.Theta, width, height, out var imageSpan))
            {
                continue;
            }

            var pt1 = new Point((int)Math.Round(imageSpan.StartX), (int)Math.Round(imageSpan.StartY));
            var pt2 = new Point((int)Math.Round(imageSpan.EndX), (int)Math.Round(imageSpan.EndY));
            Cv2.Line(resultImage, pt1, pt2, new Scalar(0, 255, 0), 2);

            var refinedLine = imageSpan;
            double residualMean = double.NaN;
            double residualMax = double.NaN;
            var diagnostics = new Dictionary<string, object>
            {
                { "Rho", line.Rho },
                { "Theta", line.Theta }
            };

            if (TryRefineLineUsingCaliper(gray, imageSpan, out var caliperLine, out residualMean, out residualMax, out var refinedPointCount))
            {
                refinedLine = caliperLine;
                diagnostics["RefinedPointCount"] = refinedPointCount;
            }

            var angleDegrees = MeasurementGeometryHelper.NormalizeLineDirectionDegrees(refinedLine.Angle);
            results.Add(new LineMeasurementResult(
                refinedLine,
                angleDegrees,
                refinedLine.Length,
                residualMean,
                residualMax,
                diagnostics));
        }

        return results;
    }

    private static List<LineMeasurementResult> DetectProbabilisticHough(Mat gray, Mat edges, Mat resultImage, int threshold, double minLength, double maxGap)
    {
        var results = new List<LineMeasurementResult>();
        var lines = Cv2.HoughLinesP(edges, 1, Math.PI / 180, threshold, minLength, maxGap);
        if (lines == null)
        {
            return results;
        }

        foreach (var line in lines)
        {
            var lineData = new LineData(line.P1.X, line.P1.Y, line.P2.X, line.P2.Y);
            var pt1 = new Point((int)Math.Round(lineData.StartX), (int)Math.Round(lineData.StartY));
            var pt2 = new Point((int)Math.Round(lineData.EndX), (int)Math.Round(lineData.EndY));
            Cv2.Line(resultImage, pt1, pt2, new Scalar(0, 255, 0), 2);

            var refinedLine = lineData;
            double residualMean = double.NaN;
            double residualMax = double.NaN;
            var diagnostics = new Dictionary<string, object>();
            if (TryRefineLineUsingCaliper(gray, lineData, out var caliperLine, out residualMean, out residualMax, out var refinedPointCount))
            {
                refinedLine = caliperLine;
                diagnostics["RefinedPointCount"] = refinedPointCount;
            }

            results.Add(new LineMeasurementResult(
                refinedLine,
                MeasurementGeometryHelper.NormalizeLineDirectionDegrees(refinedLine.Angle),
                refinedLine.Length,
                residualMean,
                residualMax,
                diagnostics));
        }

        return results;
    }

    private static List<LineMeasurementResult> DetectFitLine(Mat gray, Mat edges, Mat resultImage, int width, int height, int threshold, double minLength, double maxGap)
    {
        var seedSegments = Cv2.HoughLinesP(edges, 1, Math.PI / 180, Math.Max(40, threshold / 2), minLength, Math.Max(maxGap, 8.0));
        if (seedSegments != null && seedSegments.Length > 0)
        {
            var candidates = seedSegments
                .Select(segment => new LineData(segment.P1.X, segment.P1.Y, segment.P2.X, segment.P2.Y))
                .OrderByDescending(segment => segment.Length)
                .ToList();

            foreach (var candidate in candidates)
            {
                if (!TryRefineLineUsingCaliper(gray, candidate, out var refinedLine, out var residualMean, out var residualMax, out var refinedPointCount))
                {
                    continue;
                }

                if (refinedLine.Length < minLength)
                {
                    continue;
                }

                var refinedPt1 = new Point((int)Math.Round(refinedLine.StartX), (int)Math.Round(refinedLine.StartY));
                var refinedPt2 = new Point((int)Math.Round(refinedLine.EndX), (int)Math.Round(refinedLine.EndY));
                Cv2.Line(resultImage, refinedPt1, refinedPt2, new Scalar(0, 255, 0), 2);

                return new List<LineMeasurementResult>
                {
                    new(
                        refinedLine,
                        MeasurementGeometryHelper.NormalizeLineDirectionDegrees(refinedLine.Angle),
                        refinedLine.Length,
                        residualMean,
                        residualMax,
                        new Dictionary<string, object>
                        {
                            { "FitPointCount", refinedPointCount }
                        })
                };
            }
        }

        var points = CollectEdgePoints(edges);
        if (points.Count < 2)
        {
            return new List<LineMeasurementResult>();
        }

        var lineParams = Cv2.FitLine(points.ToArray(), DistanceTypes.L2, 0, 0.01, 0.01);
        var vx = lineParams.Vx;
        var vy = lineParams.Vy;
        var x0 = lineParams.X1;
        var y0 = lineParams.Y1;

        if (!TryCreateImageSpanFromDirection(vx, vy, x0, y0, width, height, out var lineData))
        {
            return new List<LineMeasurementResult>();
        }

        if (lineData.Length < minLength)
        {
            return new List<LineMeasurementResult>();
        }

        var pt1 = new Point((int)Math.Round(lineData.StartX), (int)Math.Round(lineData.StartY));
        var pt2 = new Point((int)Math.Round(lineData.EndX), (int)Math.Round(lineData.EndY));
        Cv2.Line(resultImage, pt1, pt2, new Scalar(0, 255, 0), 2);

        var residuals = points
            .Select(point => MeasurementGeometryHelper.DistancePointToInfiniteLine(point.X, point.Y, lineData))
            .ToList();

        var meanResidual = residuals.Average();
        var maxResidual = residuals.Max();
        return new List<LineMeasurementResult>
        {
            new(
                lineData,
                MeasurementGeometryHelper.NormalizeLineDirectionDegrees(Math.Atan2(vy, vx) * 180.0 / Math.PI),
                lineData.Length,
                meanResidual,
                maxResidual,
                new Dictionary<string, object>
                {
                    { "FitPointCount", points.Count }
                })
        };
    }

    private static bool TryRefineLineUsingCaliper(
        Mat gray,
        LineData seedLine,
        out LineData refinedLine,
        out double residualMean,
        out double residualMax,
        out int refinedPointCount)
    {
        refinedLine = seedLine;
        residualMean = double.NaN;
        residualMax = double.NaN;
        refinedPointCount = 0;

        var length = seedLine.Length;
        if (length < 12)
        {
            return false;
        }

        var dx = (seedLine.EndX - seedLine.StartX) / length;
        var dy = (seedLine.EndY - seedLine.StartY) / length;
        var normalX = -dy;
        var normalY = dx;
        var searchHalfWidth = Math.Clamp(length / 30.0, 6.0, 18.0);
        var averagingThickness = Math.Clamp(length / 80.0, 3.0, 7.0);
        var sampleCount = 41;
        var sections = Math.Clamp((int)Math.Ceiling(length / 10.0), 16, 128);
        var refinedCenters = new List<Point2f>(sections);

        for (var i = 0; i < sections; i++)
        {
            var t = sections <= 1 ? 0.5 : 0.05 + (0.90 * i / (sections - 1));
            var anchorX = seedLine.StartX + ((seedLine.EndX - seedLine.StartX) * t);
            var anchorY = seedLine.StartY + ((seedLine.EndY - seedLine.StartY) * t);
            var scanStart = new Point2d(anchorX - (normalX * searchHalfWidth), anchorY - (normalY * searchHalfWidth));
            var scanEnd = new Point2d(anchorX + (normalX * searchHalfWidth), anchorY + (normalY * searchHalfWidth));

            var profile = IndustrialCaliperKernel.SampleBandProfile(gray, scanStart, scanEnd, averagingThickness, sampleCount);
            var threshold = IndustrialCaliperKernel.EstimateEdgeThreshold(profile, minimumThreshold: 4.0);
            var centers = IndustrialCaliperKernel.DetectStripeCenters(profile, threshold, "Auto", sigma: 1.2, maxCenters: 1);
            if (centers.Count == 0)
            {
                continue;
            }

            var center = IndustrialCaliperKernel.InterpolatePosition(scanStart, scanEnd, centers[0], sampleCount);
            refinedCenters.Add(new Point2f((float)center.X, (float)center.Y));
        }

        if (refinedCenters.Count < 4)
        {
            return false;
        }

        refinedPointCount = refinedCenters.Count;
        var lineParams = Cv2.FitLine(refinedCenters.ToArray(), DistanceTypes.L2, 0, 0.01, 0.01);
        if (!TryCreateImageSpanFromDirection(lineParams.Vx, lineParams.Vy, lineParams.X1, lineParams.Y1, gray.Width, gray.Height, out refinedLine))
        {
            return false;
        }

        var lineForResidual = refinedLine;
        var residuals = refinedCenters
            .Select(point => MeasurementGeometryHelper.DistancePointToInfiniteLine(point.X, point.Y, lineForResidual))
            .ToList();
        residualMean = residuals.Average();
        residualMax = residuals.Max();
        return true;
    }

    private static List<Point2f> CollectEdgePoints(Mat edges)
    {
        var points = new List<Point2f>();
        for (var y = 0; y < edges.Rows; y++)
        {
            for (var x = 0; x < edges.Cols; x++)
            {
                if (edges.At<byte>(y, x) > 0)
                {
                    points.Add(new Point2f(x, y));
                }
            }
        }

        return points;
    }

    private static Dictionary<string, object> BuildLinePayload(LineMeasurementResult result)
    {
        var payload = new Dictionary<string, object>
        {
            { "Line", result.Line },
            { "StartX", result.Line.StartX },
            { "StartY", result.Line.StartY },
            { "EndX", result.Line.EndX },
            { "EndY", result.Line.EndY },
            { "Angle", result.Angle },
            { "Length", result.Length },
            { "ResidualMean", result.ResidualMean },
            { "ResidualMax", result.ResidualMax }
        };

        foreach (var kvp in result.Diagnostics)
        {
            payload[kvp.Key] = kvp.Value;
        }

        return payload;
    }

    private static bool TryCreateImageSpan(double rho, double theta, int width, int height, out LineData span)
    {
        var intersections = new List<Point2d>(4);
        var cosTheta = Math.Cos(theta);
        var sinTheta = Math.Sin(theta);

        if (Math.Abs(sinTheta) > 1e-9)
        {
            var yAtLeft = rho / sinTheta;
            if (yAtLeft >= 0 && yAtLeft <= height - 1)
            {
                intersections.Add(new Point2d(0, yAtLeft));
            }

            var yAtRight = (rho - ((width - 1) * cosTheta)) / sinTheta;
            if (yAtRight >= 0 && yAtRight <= height - 1)
            {
                intersections.Add(new Point2d(width - 1, yAtRight));
            }
        }

        if (Math.Abs(cosTheta) > 1e-9)
        {
            var xAtTop = rho / cosTheta;
            if (xAtTop >= 0 && xAtTop <= width - 1)
            {
                intersections.Add(new Point2d(xAtTop, 0));
            }

            var xAtBottom = (rho - ((height - 1) * sinTheta)) / cosTheta;
            if (xAtBottom >= 0 && xAtBottom <= width - 1)
            {
                intersections.Add(new Point2d(xAtBottom, height - 1));
            }
        }

        return TryCreateSpanFromIntersections(intersections, out span);
    }

    private static bool TryCreateImageSpanFromDirection(double vx, double vy, double x0, double y0, int width, int height, out LineData span)
    {
        var intersections = new List<Point2d>(4);

        if (Math.Abs(vx) > 1e-9)
        {
            var tLeft = (0 - x0) / vx;
            var yLeft = y0 + (tLeft * vy);
            if (yLeft >= 0 && yLeft <= height - 1)
            {
                intersections.Add(new Point2d(0, yLeft));
            }

            var tRight = ((width - 1) - x0) / vx;
            var yRight = y0 + (tRight * vy);
            if (yRight >= 0 && yRight <= height - 1)
            {
                intersections.Add(new Point2d(width - 1, yRight));
            }
        }

        if (Math.Abs(vy) > 1e-9)
        {
            var tTop = (0 - y0) / vy;
            var xTop = x0 + (tTop * vx);
            if (xTop >= 0 && xTop <= width - 1)
            {
                intersections.Add(new Point2d(xTop, 0));
            }

            var tBottom = ((height - 1) - y0) / vy;
            var xBottom = x0 + (tBottom * vx);
            if (xBottom >= 0 && xBottom <= width - 1)
            {
                intersections.Add(new Point2d(xBottom, height - 1));
            }
        }

        return TryCreateSpanFromIntersections(intersections, out span);
    }

    private static bool TryCreateSpanFromIntersections(IReadOnlyCollection<Point2d> intersections, out LineData span)
    {
        span = new LineData();
        var uniquePoints = intersections
            .GroupBy(point => $"{Math.Round(point.X, 4)}:{Math.Round(point.Y, 4)}")
            .Select(group => group.First())
            .ToList();

        if (uniquePoints.Count < 2)
        {
            return false;
        }

        var start = uniquePoints[0];
        var end = uniquePoints[1];
        span = new LineData((float)start.X, (float)start.Y, (float)end.X, (float)end.Y);
        return true;
    }

    private readonly record struct LineMeasurementResult(
        LineData Line,
        double Angle,
        double Length,
        double ResidualMean,
        double ResidualMax,
        Dictionary<string, object> Diagnostics);
}
