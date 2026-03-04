// GeometricFittingOperator.cs
// 几何拟合算子
// 对输入点集执行直线、圆或椭圆拟合
// 作者：蘅芜君
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Geometric Fitting",
    Description = "Fits line, circle or ellipse from contour points.",
    Category = "Measurement",
    IconName = "fit",
    Keywords = new[] { "fit", "line fit", "circle fit", "ellipse fit", "ransac" }
)]
[InputPort("Image", "Input Image", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "Result Image", PortDataType.Image)]
[OutputPort("FitResult", "Fit Result", PortDataType.Any)]
[OperatorParam("FitType", "Fit Type", "enum", DefaultValue = "Circle", Options = new[] { "Line|Line", "Circle|Circle", "Ellipse|Ellipse" })]
[OperatorParam("Threshold", "Binary Threshold", "double", DefaultValue = 127.0, Min = 0.0, Max = 255.0)]
[OperatorParam("MinArea", "Min Contour Area", "int", DefaultValue = 100, Min = 0)]
[OperatorParam("MinPoints", "Min Points", "int", DefaultValue = 5, Min = 3, Max = 10000)]
[OperatorParam("RobustMethod", "Robust Method", "enum", DefaultValue = "LeastSquares", Options = new[] { "LeastSquares|LeastSquares", "Ransac|Ransac" })]
[OperatorParam("RansacIterations", "Ransac Iterations", "int", DefaultValue = 200, Min = 10, Max = 5000)]
[OperatorParam("RansacInlierThreshold", "Ransac Inlier Threshold", "double", DefaultValue = 2.0, Min = 0.1, Max = 100.0)]
public class GeometricFittingOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.GeometricFitting;

    public GeometricFittingOperator(ILogger<GeometricFittingOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, "Image", out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is required."));
        }

        var fitType = GetStringParam(@operator, "FitType", "Circle");
        var threshold = GetDoubleParam(@operator, "Threshold", 127.0, min: 0, max: 255);
        var minArea = GetIntParam(@operator, "MinArea", 100, min: 0);
        var minPoints = GetIntParam(@operator, "MinPoints", 5, min: 3, max: 10000);
        var robustMethod = GetStringParam(@operator, "RobustMethod", "LeastSquares");
        var ransacIterations = GetIntParam(@operator, "RansacIterations", 200, min: 10, max: 5000);
        var ransacInlierThreshold = GetDoubleParam(@operator, "RansacInlierThreshold", 2.0, min: 0.1, max: 100.0);
        var useRansac = robustMethod.Equals("Ransac", StringComparison.OrdinalIgnoreCase);

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid."));
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

        using var binary = new Mat();
        Cv2.Threshold(gray, binary, threshold, 255, ThresholdTypes.Binary);

        Cv2.FindContours(binary, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        var validContours = contours.Where(c => Cv2.ContourArea(c) >= minArea).ToList();
        if (validContours.Count == 0)
        {
            var noContourData = new Dictionary<string, object>
            {
                { "FitResult", new { Success = false, Message = "No valid contour found." } }
            };
            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, noContourData)));
        }

        var allPoints = validContours
            .SelectMany(c => c)
            .Select(p => new Point2f(p.X, p.Y))
            .ToArray();

        if (allPoints.Length < minPoints)
        {
            var insufficientData = new Dictionary<string, object>
            {
                { "FitResult", new { Success = false, Message = $"Insufficient points. Need {minPoints}, got {allPoints.Length}." } }
            };
            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, insufficientData)));
        }

        var fitResult = new Dictionary<string, object>
        {
            { "Success", true },
            { "RobustMethod", useRansac ? "Ransac" : "LeastSquares" }
        };

        switch (fitType.ToLowerInvariant())
        {
            case "line":
                FitLine(allPoints, resultImage, fitResult, useRansac, ransacIterations, ransacInlierThreshold);
                break;
            case "circle":
                FitCircle(allPoints, resultImage, fitResult, useRansac, ransacIterations, ransacInlierThreshold);
                break;
            case "ellipse":
                FitEllipse(allPoints, resultImage, fitResult);
                break;
            default:
                FitCircle(allPoints, resultImage, fitResult, useRansac, ransacIterations, ransacInlierThreshold);
                break;
        }

        for (var i = 0; i < validContours.Count; i++)
        {
            Cv2.DrawContours(resultImage, validContours, i, new Scalar(255, 0, 0), 1);
        }

        var additionalData = new Dictionary<string, object>
        {
            { "FitResult", fitResult },
            { "FitType", fitType },
            { "PointCount", allPoints.Length },
            { "ContourCount", validContours.Count }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, additionalData)));
    }

    private void FitLine(
        Point2f[] points,
        Mat resultImage,
        Dictionary<string, object> fitResult,
        bool useRansac,
        int ransacIterations,
        double inlierThreshold)
    {
        var fitPoints = points;
        if (useRansac &&
            TryEstimateLineInliersRansac(points, ransacIterations, inlierThreshold, out var lineInliers) &&
            lineInliers.Length >= 2)
        {
            fitPoints = lineInliers;
            fitResult["InlierCount"] = lineInliers.Length;
            fitResult["InlierRatio"] = (double)lineInliers.Length / points.Length;
        }

        var lineParams = Cv2.FitLine(fitPoints, DistanceTypes.L2, 0, 0.01, 0.01);
        var vx = lineParams.Vx;
        var vy = lineParams.Vy;
        var x0 = lineParams.X1;
        var y0 = lineParams.Y1;

        Point p1;
        Point p2;
        if (Math.Abs(vx) < 1e-6)
        {
            var x = (int)Math.Round(x0);
            p1 = new Point(x, 0);
            p2 = new Point(x, resultImage.Height - 1);
        }
        else
        {
            var leftY = (int)Math.Round(y0 - ((x0 * vy) / vx));
            var rightY = (int)Math.Round(y0 + (((resultImage.Width - x0) * vy) / vx));
            p1 = new Point(0, leftY);
            p2 = new Point(resultImage.Width - 1, rightY);
        }

        Cv2.Line(resultImage, p1, p2, new Scalar(0, 255, 0), 2);

        var angle = Math.Atan2(vy, vx) * 180 / Math.PI;
        fitResult["LineVx"] = vx;
        fitResult["LineVy"] = vy;
        fitResult["LineX0"] = x0;
        fitResult["LineY0"] = y0;
        fitResult["Angle"] = angle;
    }

    private void FitCircle(
        Point2f[] points,
        Mat resultImage,
        Dictionary<string, object> fitResult,
        bool useRansac,
        int ransacIterations,
        double inlierThreshold)
    {
        var fitPoints = points;
        if (useRansac &&
            TryEstimateCircleInliersRansac(points, ransacIterations, inlierThreshold, out var circleInliers) &&
            circleInliers.Length >= 3)
        {
            fitPoints = circleInliers;
            fitResult["InlierCount"] = circleInliers.Length;
            fitResult["InlierRatio"] = (double)circleInliers.Length / points.Length;
        }

        var (cx, cy, r) = FitCircleLeastSquares(fitPoints);
        if (r > 0)
        {
            var center = new Point((int)Math.Round(cx), (int)Math.Round(cy));
            Cv2.Circle(resultImage, center, (int)Math.Round(r), new Scalar(0, 255, 0), 2);
            Cv2.Circle(resultImage, center, 3, new Scalar(0, 0, 255), -1);

            fitResult["CenterX"] = cx;
            fitResult["CenterY"] = cy;
            fitResult["Radius"] = r;
        }
        else
        {
            fitResult["Success"] = false;
            fitResult["Message"] = "Circle fit failed.";
        }
    }

    private static void FitEllipse(
        Point2f[] points,
        Mat resultImage,
        Dictionary<string, object> fitResult)
    {
        if (points.Length < 5)
        {
            fitResult["Success"] = false;
            fitResult["Message"] = "Ellipse fit needs at least 5 points.";
            return;
        }

        try
        {
            var rotatedRect = Cv2.FitEllipse(points);
            Cv2.Ellipse(resultImage, rotatedRect, new Scalar(0, 255, 0), 2);
            Cv2.Circle(resultImage, (Point)rotatedRect.Center, 3, new Scalar(0, 0, 255), -1);

            fitResult["CenterX"] = rotatedRect.Center.X;
            fitResult["CenterY"] = rotatedRect.Center.Y;
            fitResult["MajorAxis"] = rotatedRect.Size.Width;
            fitResult["MinorAxis"] = rotatedRect.Size.Height;
            fitResult["Angle"] = rotatedRect.Angle;
        }
        catch
        {
            fitResult["Success"] = false;
            fitResult["Message"] = "Ellipse fit failed.";
        }
    }

    private static bool TryEstimateLineInliersRansac(
        Point2f[] points,
        int iterations,
        double threshold,
        out Point2f[] inliers)
    {
        inliers = Array.Empty<Point2f>();
        if (points.Length < 2)
        {
            return false;
        }

        var random = new Random(points.Length * 397 + iterations);
        var bestInlierIndices = Array.Empty<int>();

        for (var i = 0; i < iterations; i++)
        {
            var idx1 = random.Next(points.Length);
            var idx2 = random.Next(points.Length);
            if (idx1 == idx2)
            {
                continue;
            }

            if (!TryCreateLineModel(points[idx1], points[idx2], out var model))
            {
                continue;
            }

            var current = new List<int>();
            for (var p = 0; p < points.Length; p++)
            {
                if (DistancePointToLine(points[p], model) <= threshold)
                {
                    current.Add(p);
                }
            }

            if (current.Count > bestInlierIndices.Length)
            {
                bestInlierIndices = current.ToArray();
            }
        }

        if (bestInlierIndices.Length < 2)
        {
            return false;
        }

        inliers = bestInlierIndices.Select(index => points[index]).ToArray();
        return true;
    }

    private static bool TryEstimateCircleInliersRansac(
        Point2f[] points,
        int iterations,
        double threshold,
        out Point2f[] inliers)
    {
        inliers = Array.Empty<Point2f>();
        if (points.Length < 3)
        {
            return false;
        }

        var random = new Random((points.Length * 733) + iterations);
        var bestInlierIndices = Array.Empty<int>();

        for (var i = 0; i < iterations; i++)
        {
            if (!TryPickDistinctIndices(random, points.Length, out var i1, out var i2, out var i3))
            {
                continue;
            }

            if (!TryCreateCircleModel(points[i1], points[i2], points[i3], out var model))
            {
                continue;
            }

            var current = new List<int>();
            for (var p = 0; p < points.Length; p++)
            {
                var d = Math.Sqrt(Math.Pow(points[p].X - model.CenterX, 2) + Math.Pow(points[p].Y - model.CenterY, 2));
                if (Math.Abs(d - model.Radius) <= threshold)
                {
                    current.Add(p);
                }
            }

            if (current.Count > bestInlierIndices.Length)
            {
                bestInlierIndices = current.ToArray();
            }
        }

        if (bestInlierIndices.Length < 3)
        {
            return false;
        }

        inliers = bestInlierIndices.Select(index => points[index]).ToArray();
        return true;
    }

    private static bool TryPickDistinctIndices(Random random, int upperExclusive, out int i1, out int i2, out int i3)
    {
        i1 = random.Next(upperExclusive);
        i2 = random.Next(upperExclusive);
        i3 = random.Next(upperExclusive);
        if (i1 == i2 || i1 == i3 || i2 == i3)
        {
            return false;
        }

        return true;
    }

    private static bool TryCreateLineModel(Point2f p1, Point2f p2, out LineModel model)
    {
        model = default;
        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        var norm = Math.Sqrt((dx * dx) + (dy * dy));
        if (norm < 1e-6)
        {
            return false;
        }

        // Ax + By + C = 0
        var a = dy / norm;
        var b = -dx / norm;
        var c = ((dx * p1.Y) - (dy * p1.X)) / norm;
        model = new LineModel(a, b, c);
        return true;
    }

    private static double DistancePointToLine(Point2f point, LineModel line)
    {
        return Math.Abs((line.A * point.X) + (line.B * point.Y) + line.C);
    }

    private static bool TryCreateCircleModel(Point2f p1, Point2f p2, Point2f p3, out CircleModel model)
    {
        model = default;
        var x1 = p1.X;
        var y1 = p1.Y;
        var x2 = p2.X;
        var y2 = p2.Y;
        var x3 = p3.X;
        var y3 = p3.Y;

        var a = x1 * (y2 - y3) - (y1 * (x2 - x3)) + (x2 * y3) - (x3 * y2);
        if (Math.Abs(a) < 1e-6)
        {
            return false;
        }

        var x1Sq = (x1 * x1) + (y1 * y1);
        var x2Sq = (x2 * x2) + (y2 * y2);
        var x3Sq = (x3 * x3) + (y3 * y3);

        var cx = (x1Sq * (y3 - y2) + x2Sq * (y1 - y3) + x3Sq * (y2 - y1)) / (2 * a);
        var cy = (x1Sq * (x2 - x3) + x2Sq * (x3 - x1) + x3Sq * (x1 - x2)) / (2 * a);
        var r = Math.Sqrt(Math.Pow(x1 - cx, 2) + Math.Pow(y1 - cy, 2));
        if (double.IsNaN(r) || r <= 0)
        {
            return false;
        }

        model = new CircleModel(cx, cy, r);
        return true;
    }

    private static (double cx, double cy, double r) FitCircleLeastSquares(Point2f[] points)
    {
        var n = points.Length;
        double sumX = 0;
        double sumY = 0;
        double sumX2 = 0;
        double sumY2 = 0;
        double sumXY = 0;
        double sumX3 = 0;
        double sumY3 = 0;
        double sumX2Y = 0;
        double sumXY2 = 0;

        for (var i = 0; i < n; i++)
        {
            var x = points[i].X;
            var y = points[i].Y;
            sumX += x;
            sumY += y;
            sumX2 += x * x;
            sumY2 += y * y;
            sumXY += x * y;
            sumX3 += x * x * x;
            sumY3 += y * y * y;
            sumX2Y += x * x * y;
            sumXY2 += x * y * y;
        }

        var a = (n * sumX2) - (sumX * sumX);
        var b = (n * sumXY) - (sumX * sumY);
        var c = (n * sumY2) - (sumY * sumY);
        var d = 0.5 * ((n * sumX3) + (n * sumXY2) - (sumX * sumX2) - (sumX * sumY2));
        var e = 0.5 * ((n * sumX2Y) + (n * sumY3) - (sumY * sumX2) - (sumY * sumY2));

        var det = (a * c) - (b * b);
        if (Math.Abs(det) < 1e-10)
        {
            return (0, 0, 0);
        }

        var cx = ((d * c) - (b * e)) / det;
        var cy = ((a * e) - (b * d)) / det;
        var r = Math.Sqrt((sumX2 / n) - ((2 * cx * sumX) / n) + (cx * cx)
                          + (sumY2 / n) - ((2 * cy * sumY) / n) + (cy * cy));
        return (cx, cy, r);
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var threshold = GetDoubleParam(@operator, "Threshold", 127.0);
        if (threshold < 0 || threshold > 255)
        {
            return ValidationResult.Invalid("Threshold must be between 0 and 255.");
        }

        var minArea = GetIntParam(@operator, "MinArea", 100);
        if (minArea < 0)
        {
            return ValidationResult.Invalid("MinArea must be non-negative.");
        }

        var minPoints = GetIntParam(@operator, "MinPoints", 5);
        if (minPoints < 3)
        {
            return ValidationResult.Invalid("MinPoints must be at least 3.");
        }

        var fitType = GetStringParam(@operator, "FitType", "Circle");
        var validTypes = new[] { "Line", "Circle", "Ellipse" };
        if (!validTypes.Contains(fitType, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid($"FitType must be one of: {string.Join(", ", validTypes)}");
        }

        var robustMethod = GetStringParam(@operator, "RobustMethod", "LeastSquares");
        var validRobustMethods = new[] { "LeastSquares", "Ransac" };
        if (!validRobustMethods.Contains(robustMethod, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid($"RobustMethod must be one of: {string.Join(", ", validRobustMethods)}");
        }

        var iterations = GetIntParam(@operator, "RansacIterations", 200);
        if (iterations < 10 || iterations > 5000)
        {
            return ValidationResult.Invalid("RansacIterations must be between 10 and 5000.");
        }

        var inlierThreshold = GetDoubleParam(@operator, "RansacInlierThreshold", 2.0);
        if (inlierThreshold <= 0 || inlierThreshold > 100.0)
        {
            return ValidationResult.Invalid("RansacInlierThreshold must be in (0, 100].");
        }

        return ValidationResult.Valid();
    }

    private readonly record struct LineModel(double A, double B, double C);

    private readonly record struct CircleModel(double CenterX, double CenterY, double Radius);
}
