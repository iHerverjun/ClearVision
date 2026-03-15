// GeometricFittingOperator.cs
// 几何拟合算子
// 对输入点集执行直线、圆或椭圆拟合
// 作者：蘅芜君
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
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
[OperatorParam("ContourSelection", "Contour Selection", "enum", DefaultValue = "LargestContour", Options = new[] { "LargestContour|Largest Contour", "AllContours|All Contours", "FirstContour|First Contour" })]
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
        var contourSelection = GetStringParam(@operator, "ContourSelection", "LargestContour");
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
        var selectedContours = SelectContours(validContours, contourSelection);
        if (validContours.Count == 0)
        {
            var noContourData = new Dictionary<string, object>
            {
                { "FitResult", CreateFailedFitResult("No valid contour found.", fitType, robustMethod, contourSelection, 0, 0) }
            };
            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, noContourData)));
        }

        var allPoints = selectedContours
            .SelectMany(c => c)
            .Select(p => new Point2f(p.X, p.Y))
            .ToArray();

        if (allPoints.Length < minPoints)
        {
            var insufficientData = new Dictionary<string, object>
            {
                { "FitResult", CreateFailedFitResult($"Insufficient points. Need {minPoints}, got {allPoints.Length}.", fitType, robustMethod, contourSelection, validContours.Count, selectedContours.Count) }
            };
            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, insufficientData)));
        }

        var fitResult = new Dictionary<string, object>
        {
            { "Success", true },
            { "FitType", fitType },
            { "RequestedRobustMethod", robustMethod },
            { "AppliedRobustMethod", useRansac ? "Ransac" : "LeastSquares" },
            { "RobustMethod", useRansac ? "Ransac" : "LeastSquares" },
            { "ContourSelection", contourSelection },
            { "SourceContourCount", validContours.Count },
            { "SelectedContourCount", selectedContours.Count },
            { "PointCount", allPoints.Length },
            { "Geometry", new Dictionary<string, object>() }
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
                FitEllipse(allPoints, resultImage, fitResult, useRansac, ransacIterations, ransacInlierThreshold);
                break;
            default:
                FitCircle(allPoints, resultImage, fitResult, useRansac, ransacIterations, ransacInlierThreshold);
                break;
        }

        for (var i = 0; i < selectedContours.Count; i++)
        {
            Cv2.DrawContours(resultImage, selectedContours, i, new Scalar(255, 0, 0), 1);
        }

        var additionalData = new Dictionary<string, object>
        {
            { "FitResult", fitResult },
            { "FitType", fitType },
            { "PointCount", allPoints.Length },
            { "ContourCount", validContours.Count },
            { "SelectedContourCount", selectedContours.Count }
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
        LineModel? ransacModel = null;
        LineResidualStats? ransacResidualStats = null;
        if (useRansac &&
            TryEstimateLineModelRansac(points, ransacIterations, inlierThreshold, out var bestLineModel, out var lineInliers, out var lineResidualStats) &&
            lineInliers.Length >= 2)
        {
            fitPoints = lineInliers;
            ransacModel = bestLineModel;
            ransacResidualStats = lineResidualStats;
            fitResult["InlierCount"] = lineInliers.Length;
            fitResult["InlierRatio"] = (double)lineInliers.Length / points.Length;
            fitResult["RansacMeanResidual"] = lineResidualStats.MeanResidual;
            fitResult["RansacMaxResidual"] = lineResidualStats.MaxResidual;
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
        var geometry = GetGeometry(fitResult);
        geometry["Type"] = "Line";
        geometry["Line"] = new Dictionary<string, object>
        {
            { "Vx", vx },
            { "Vy", vy },
            { "X0", x0 },
            { "Y0", y0 },
            { "Angle", angle }
        };

        var refinedModel = CreateLineModelFromFit(vx, vy, x0, y0);
        var allPointResiduals = CalculateLineResidualStats(points, refinedModel);
        fitResult["ResidualMean"] = allPointResiduals.MeanResidual;
        fitResult["ResidualMax"] = allPointResiduals.MaxResidual;

        if (ransacModel.HasValue && ransacResidualStats.HasValue)
        {
            fitResult["RansacModel"] = new Dictionary<string, object>
            {
                { "A", ransacModel.Value.A },
                { "B", ransacModel.Value.B },
                { "C", ransacModel.Value.C },
                { "MeanResidual", ransacResidualStats.Value.MeanResidual },
                { "MaxResidual", ransacResidualStats.Value.MaxResidual }
            };
        }
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
        CircleModel? ransacModel = null;
        CircleResidualStats? ransacResidualStats = null;
        if (useRansac &&
            TryEstimateCircleModelRansac(points, ransacIterations, inlierThreshold, out var bestCircleModel, out var circleInliers, out var circleResidualStats) &&
            circleInliers.Length >= 3)
        {
            fitPoints = circleInliers;
            ransacModel = bestCircleModel;
            ransacResidualStats = circleResidualStats;
            fitResult["InlierCount"] = circleInliers.Length;
            fitResult["InlierRatio"] = (double)circleInliers.Length / points.Length;
            fitResult["RansacMeanResidual"] = circleResidualStats.MeanResidual;
            fitResult["RansacMaxResidual"] = circleResidualStats.MaxResidual;
        }

        var (cx, cy, r) = FitCircleLeastSquares(fitPoints);
        if (r > 0)
        {
            var center = new Point((int)Math.Round(cx), (int)Math.Round(cy));
            Cv2.Circle(resultImage, center, (int)Math.Round(r), new Scalar(0, 255, 0), 2);
            Cv2.Circle(resultImage, center, 3, new Scalar(0, 0, 255), -1);

            var geometry = GetGeometry(fitResult);
            geometry["Type"] = "Circle";
            geometry["Center"] = new Position(cx, cy);
            geometry["Radius"] = r;
            geometry["Circle"] = new Dictionary<string, object>
            {
                { "Center", new Position(cx, cy) },
                { "Radius", r }
            };

            var allPointResiduals = CalculateCircleResidualStats(points, new CircleModel(cx, cy, r));
            fitResult["ResidualMean"] = allPointResiduals.MeanResidual;
            fitResult["ResidualMax"] = allPointResiduals.MaxResidual;

            if (ransacModel.HasValue && ransacResidualStats.HasValue)
            {
                fitResult["RansacModel"] = new Dictionary<string, object>
                {
                    { "Center", new Position(ransacModel.Value.CenterX, ransacModel.Value.CenterY) },
                    { "Radius", ransacModel.Value.Radius },
                    { "MeanResidual", ransacResidualStats.Value.MeanResidual },
                    { "MaxResidual", ransacResidualStats.Value.MaxResidual }
                };
            }
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
        Dictionary<string, object> fitResult,
        bool useRansac,
        int ransacIterations,
        double inlierThreshold)
    {
        if (points.Length < 5)
        {
            fitResult["Success"] = false;
            fitResult["Message"] = "Ellipse fit needs at least 5 points.";
            return;
        }

        var fitPoints = points;
        RotatedRect? ransacModel = null;
        EllipseResidualStats? ransacResidualStats = null;
        if (useRansac &&
            TryEstimateEllipseModelRansac(points, ransacIterations, inlierThreshold, out var bestEllipseModel, out var ellipseInliers, out var ellipseResidualStats) &&
            ellipseInliers.Length >= 5)
        {
            fitPoints = ellipseInliers;
            ransacModel = bestEllipseModel;
            ransacResidualStats = ellipseResidualStats;
            fitResult["InlierCount"] = ellipseInliers.Length;
            fitResult["InlierRatio"] = (double)ellipseInliers.Length / points.Length;
            fitResult["RansacMeanResidual"] = ellipseResidualStats.MeanResidual;
            fitResult["RansacMaxResidual"] = ellipseResidualStats.MaxResidual;
        }

        try
        {
            var rotatedRect = Cv2.FitEllipse(fitPoints);
            Cv2.Ellipse(resultImage, rotatedRect, new Scalar(0, 255, 0), 2);
            Cv2.Circle(resultImage, (Point)rotatedRect.Center, 3, new Scalar(0, 0, 255), -1);

            var geometry = GetGeometry(fitResult);
            geometry["Type"] = "Ellipse";
            geometry["Center"] = new Position(rotatedRect.Center.X, rotatedRect.Center.Y);
            geometry["MajorAxis"] = rotatedRect.Size.Width;
            geometry["MinorAxis"] = rotatedRect.Size.Height;
            geometry["Angle"] = rotatedRect.Angle;

            var allPointResiduals = CalculateEllipseResidualStats(points, rotatedRect);
            fitResult["ResidualMean"] = allPointResiduals.MeanResidual;
            fitResult["ResidualMax"] = allPointResiduals.MaxResidual;

            if (ransacModel.HasValue && ransacResidualStats.HasValue)
            {
                fitResult["RansacModel"] = new Dictionary<string, object>
                {
                    { "Center", new Position(ransacModel.Value.Center.X, ransacModel.Value.Center.Y) },
                    { "MajorAxis", ransacModel.Value.Size.Width },
                    { "MinorAxis", ransacModel.Value.Size.Height },
                    { "Angle", ransacModel.Value.Angle },
                    { "MeanResidual", ransacResidualStats.Value.MeanResidual },
                    { "MaxResidual", ransacResidualStats.Value.MaxResidual }
                };
            }
        }
        catch
        {
            fitResult["Success"] = false;
            fitResult["Message"] = "Ellipse fit failed.";
        }
    }

    private static Dictionary<string, object> CreateFailedFitResult(
        string message,
        string fitType,
        string robustMethod,
        string contourSelection,
        int sourceContourCount,
        int selectedContourCount)
    {
        return new Dictionary<string, object>
        {
            { "Success", false },
            { "FitType", fitType },
            { "RequestedRobustMethod", robustMethod },
            { "AppliedRobustMethod", robustMethod },
            { "RobustMethod", robustMethod },
            { "ContourSelection", contourSelection },
            { "SourceContourCount", sourceContourCount },
            { "SelectedContourCount", selectedContourCount },
            { "PointCount", 0 },
            { "Geometry", new Dictionary<string, object>() },
            { "Message", message }
        };
    }

    private static List<Point[]> SelectContours(List<Point[]> validContours, string contourSelection)
    {
        return contourSelection.ToLowerInvariant() switch
        {
            "allcontours" => validContours,
            "firstcontour" => validContours.Take(1).ToList(),
            _ => validContours
                .OrderByDescending(contour => Cv2.ContourArea(contour))
                .Take(1)
                .ToList()
        };
    }

    private static Dictionary<string, object> GetGeometry(Dictionary<string, object> fitResult)
    {
        if (fitResult.TryGetValue("Geometry", out var geometry) && geometry is Dictionary<string, object> typed)
        {
            return typed;
        }

        var created = new Dictionary<string, object>();
        fitResult["Geometry"] = created;
        return created;
    }

    private static bool TryEstimateLineModelRansac(
        Point2f[] points,
        int iterations,
        double threshold,
        out LineModel bestModel,
        out Point2f[] inliers,
        out LineResidualStats residualStats)
    {
        bestModel = default;
        inliers = Array.Empty<Point2f>();
        residualStats = default;
        if (points.Length < 2)
        {
            return false;
        }

        var random = new Random(points.Length * 397 + iterations);
        var bestInlierIndices = Array.Empty<int>();
        var bestMeanResidual = double.MaxValue;
        var bestMaxResidual = double.MaxValue;

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

            CollectLineInliers(points, model, threshold, out var current, out var currentStats);

            if (current.Count > bestInlierIndices.Length
                || (current.Count == bestInlierIndices.Length && current.Count > 0 && currentStats.MeanResidual < bestMeanResidual)
                || (current.Count == bestInlierIndices.Length && current.Count > 0 && Math.Abs(currentStats.MeanResidual - bestMeanResidual) < 1e-9 && currentStats.MaxResidual < bestMaxResidual))
            {
                bestInlierIndices = current.ToArray();
                bestModel = model;
                bestMeanResidual = currentStats.MeanResidual;
                bestMaxResidual = currentStats.MaxResidual;
            }
        }

        if (bestInlierIndices.Length < 2)
        {
            return false;
        }

        var provisionalInliers = bestInlierIndices.Select(index => points[index]).ToArray();
        var refinedLine = Cv2.FitLine(provisionalInliers, DistanceTypes.L2, 0, 0.01, 0.01);
        bestModel = CreateLineModelFromFit(refinedLine.Vx, refinedLine.Vy, refinedLine.X1, refinedLine.Y1);

        CollectLineInliers(points, bestModel, threshold, out var refinedIndices, out var refinedStats);
        if (refinedIndices.Count >= 2)
        {
            inliers = refinedIndices.Select(index => points[index]).ToArray();
            residualStats = refinedStats;
            return true;
        }

        inliers = provisionalInliers;
        residualStats = CalculateLineResidualStats(inliers, bestModel);
        return true;
    }

    private static void CollectLineInliers(
        Point2f[] points,
        LineModel model,
        double threshold,
        out List<int> indices,
        out LineResidualStats residualStats)
    {
        indices = new List<int>();
        var residuals = new List<double>();
        for (var p = 0; p < points.Length; p++)
        {
            var residual = DistancePointToLine(points[p], model);
            if (residual <= threshold)
            {
                indices.Add(p);
                residuals.Add(residual);
            }
        }

        residualStats = residuals.Count == 0
            ? default
            : new LineResidualStats(residuals.Average(), residuals.Max());
    }

    private static LineResidualStats CalculateLineResidualStats(Point2f[] points, LineModel model)
    {
        if (points.Length == 0)
        {
            return default;
        }

        var residuals = points.Select(point => DistancePointToLine(point, model)).ToArray();
        return new LineResidualStats(residuals.Average(), residuals.Max());
    }

    private static bool TryEstimateCircleModelRansac(
        Point2f[] points,
        int iterations,
        double threshold,
        out CircleModel bestModel,
        out Point2f[] inliers,
        out CircleResidualStats residualStats)
    {
        bestModel = default;
        inliers = Array.Empty<Point2f>();
        residualStats = default;
        if (points.Length < 3)
        {
            return false;
        }

        var random = new Random((points.Length * 733) + iterations);
        var bestInlierIndices = Array.Empty<int>();
        var bestMeanResidual = double.MaxValue;
        var bestMaxResidual = double.MaxValue;

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

            CollectCircleInliers(points, model, threshold, out var current, out var currentStats);

            if (current.Count > bestInlierIndices.Length
                || (current.Count == bestInlierIndices.Length && current.Count > 0 && currentStats.MeanResidual < bestMeanResidual)
                || (current.Count == bestInlierIndices.Length && current.Count > 0 && Math.Abs(currentStats.MeanResidual - bestMeanResidual) < 1e-9 && currentStats.MaxResidual < bestMaxResidual))
            {
                bestInlierIndices = current.ToArray();
                bestModel = model;
                bestMeanResidual = currentStats.MeanResidual;
                bestMaxResidual = currentStats.MaxResidual;
            }
        }

        if (bestInlierIndices.Length < 3)
        {
            return false;
        }

        var provisionalInliers = bestInlierIndices.Select(index => points[index]).ToArray();
        var (refinedCx, refinedCy, refinedR) = FitCircleLeastSquares(provisionalInliers);
        if (refinedR > 0)
        {
            bestModel = new CircleModel(refinedCx, refinedCy, refinedR);
        }

        CollectCircleInliers(points, bestModel, threshold, out var refinedIndices, out var refinedStats);
        if (refinedIndices.Count >= 3)
        {
            inliers = refinedIndices.Select(index => points[index]).ToArray();
            residualStats = refinedStats;
            return true;
        }

        inliers = provisionalInliers;
        residualStats = CalculateCircleResidualStats(inliers, bestModel);
        return true;
    }

    private static void CollectCircleInliers(
        Point2f[] points,
        CircleModel model,
        double threshold,
        out List<int> indices,
        out CircleResidualStats residualStats)
    {
        indices = new List<int>();
        var residuals = new List<double>();
        for (var p = 0; p < points.Length; p++)
        {
            var residual = Math.Abs(DistancePointToCircle(points[p], model));
            if (residual <= threshold)
            {
                indices.Add(p);
                residuals.Add(residual);
            }
        }

        residualStats = residuals.Count == 0
            ? default
            : new CircleResidualStats(residuals.Average(), residuals.Max());
    }

    private static CircleResidualStats CalculateCircleResidualStats(Point2f[] points, CircleModel model)
    {
        if (points.Length == 0)
        {
            return default;
        }

        var residuals = points.Select(point => Math.Abs(DistancePointToCircle(point, model))).ToArray();
        return new CircleResidualStats(residuals.Average(), residuals.Max());
    }

    private static double DistancePointToCircle(Point2f point, CircleModel circle)
    {
        var distanceToCenter = Math.Sqrt(Math.Pow(point.X - circle.CenterX, 2) + Math.Pow(point.Y - circle.CenterY, 2));
        return distanceToCenter - circle.Radius;
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

    private static bool TryPickDistinctIndices5(Random random, int upperExclusive, out int[] indices)
    {
        indices = new int[5];
        for (int i = 0; i < 5; i++)
        {
            indices[i] = random.Next(upperExclusive);
            for (int j = 0; j < i; j++)
            {
                if (indices[i] == indices[j])
                    return false;
            }
        }
        return true;
    }

    private static bool TryEstimateEllipseModelRansac(
        Point2f[] points,
        int iterations,
        double threshold,
        out RotatedRect bestModel,
        out Point2f[] inliers,
        out EllipseResidualStats residualStats)
    {
        bestModel = default;
        inliers = Array.Empty<Point2f>();
        residualStats = default;
        if (points.Length < 5)
        {
            return false;
        }

        var random = new Random((points.Length * 991) + iterations);
        var bestInlierIndices = Array.Empty<int>();
        var bestMeanResidual = double.MaxValue;
        var bestMaxResidual = double.MaxValue;

        for (var i = 0; i < iterations; i++)
        {
            if (!TryPickDistinctIndices5(random, points.Length, out var idx5))
            {
                continue;
            }

            var samplePts = new Point2f[]
            {
                points[idx5[0]], points[idx5[1]], points[idx5[2]], points[idx5[3]], points[idx5[4]]
            };

            RotatedRect model;
            try
            {
                model = Cv2.FitEllipse(samplePts);
                if (model.Size.Width <= 0 || model.Size.Height <= 0) continue;
            }
            catch
            {
                continue;
            }

            CollectEllipseInliers(points, model, threshold, out var current, out var currentStats);

            if (current.Count > bestInlierIndices.Length
                || (current.Count == bestInlierIndices.Length && current.Count > 0 && currentStats.MeanResidual < bestMeanResidual)
                || (current.Count == bestInlierIndices.Length && current.Count > 0 && Math.Abs(currentStats.MeanResidual - bestMeanResidual) < 1e-9 && currentStats.MaxResidual < bestMaxResidual))
            {
                bestInlierIndices = current.ToArray();
                bestModel = model;
                bestMeanResidual = currentStats.MeanResidual;
                bestMaxResidual = currentStats.MaxResidual;
            }
        }

        if (bestInlierIndices.Length < 5)
        {
            return false;
        }

        var provisionalInliers = bestInlierIndices.Select(index => points[index]).ToArray();
        
        try
        {
            bestModel = Cv2.FitEllipse(provisionalInliers);
        }
        catch
        {
            return false;
        }

        CollectEllipseInliers(points, bestModel, threshold, out var refinedIndices, out var refinedStats);
        if (refinedIndices.Count >= 5)
        {
            inliers = refinedIndices.Select(index => points[index]).ToArray();
            residualStats = refinedStats;
            return true;
        }

        inliers = provisionalInliers;
        residualStats = CalculateEllipseResidualStats(inliers, bestModel);
        return true;
    }

    private static void CollectEllipseInliers(
        Point2f[] points,
        RotatedRect model,
        double threshold,
        out List<int> indices,
        out EllipseResidualStats residualStats)
    {
        indices = new List<int>();
        var residuals = new List<double>();
        for (var p = 0; p < points.Length; p++)
        {
            var residual = Math.Abs(DistancePointToEllipse(points[p], model));
            if (residual <= threshold)
            {
                indices.Add(p);
                residuals.Add(residual);
            }
        }

        residualStats = residuals.Count == 0
            ? default
            : new EllipseResidualStats(residuals.Average(), residuals.Max());
    }

    private static EllipseResidualStats CalculateEllipseResidualStats(Point2f[] points, RotatedRect model)
    {
        if (points.Length == 0)
        {
            return default;
        }

        var residuals = points.Select(point => Math.Abs(DistancePointToEllipse(point, model))).ToArray();
        return new EllipseResidualStats(residuals.Average(), residuals.Max());
    }

    private static double DistancePointToEllipse(Point2f point, RotatedRect ellipse)
    {
        // 简单采用代数距离的绝对值作为 RANSAC 判断依据
        // 或者使用几何距离估算：先将点平移旋转到椭圆标准坐标系，然后计算到标准椭圆的代数距离
        var a = ellipse.Size.Width / 2.0;
        var b = ellipse.Size.Height / 2.0;
        if (a < 1e-6 || b < 1e-6) return double.MaxValue;

        var angleRad = ellipse.Angle * Math.PI / 180.0;
        var cosA = Math.Cos(angleRad);
        var sinA = Math.Sin(angleRad);

        var dx = point.X - ellipse.Center.X;
        var dy = point.Y - ellipse.Center.Y;

        var localX = (dx * cosA) + (dy * sinA);
        var localY = (-dx * sinA) + (dy * cosA);

        var distSquare = Math.Pow(localX / a, 2) + Math.Pow(localY / b, 2);
        
        // 粗略几何距离近似 (根据 |distSquare - 1| 缩放到像素距离)
        // 这个近似对于距离边界不远的点是足够判断 inlier 的
        var algebraicDist = Math.Abs(distSquare - 1.0);
        var radiusEstimate = Math.Sqrt(Math.Pow(localX, 2) + Math.Pow(localY, 2));
        var theoreticalRadius = radiusEstimate / (Math.Sqrt(distSquare) + 1e-9);

        return Math.Abs(radiusEstimate - theoreticalRadius);
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

    private static LineModel CreateLineModelFromFit(double vx, double vy, double x0, double y0)
    {
        var norm = Math.Sqrt((vx * vx) + (vy * vy));
        if (norm < 1e-9)
        {
            return default;
        }

        var a = vy / norm;
        var b = -vx / norm;
        var c = -((a * x0) + (b * y0));
        return new LineModel(a, b, c);
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

        var contourSelection = GetStringParam(@operator, "ContourSelection", "LargestContour");
        var validSelections = new[] { "LargestContour", "AllContours", "FirstContour" };
        if (!validSelections.Contains(contourSelection, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("ContourSelection must be LargestContour, AllContours or FirstContour.");
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

    private readonly record struct LineResidualStats(double MeanResidual, double MaxResidual);

    private readonly record struct CircleResidualStats(double MeanResidual, double MaxResidual);

    private readonly record struct EllipseResidualStats(double MeanResidual, double MaxResidual);

    private readonly record struct CircleModel(double CenterX, double CenterY, double Radius);
}
