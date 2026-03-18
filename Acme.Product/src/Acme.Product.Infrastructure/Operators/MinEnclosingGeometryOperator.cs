// MinEnclosingGeometryOperator.cs
// 最小外接几何体与圆弧拟合算子
// 对标 Halcon: smallest_circle / smallest_rectangle2 / fit_circle_contour_xld (圆弧)
// 作者：AI Assistant

using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 最小外接几何体与圆弧拟合算子
/// 对标 Halcon smallest_circle / smallest_rectangle2 / fit_circle_contour_xld
/// </summary>
[OperatorMeta(
    DisplayName = "Min Enclosing Geometry",
    Description = "Computes minimum enclosing geometry (circle, rectangle, triangle) and robust arc fitting with RANSAC.",
    Category = "Measurement",
    IconName = "enclosing-geometry",
    Keywords = new[] { "MinEnclosing", "SmallestCircle", "MinAreaRect", "ArcFit", "RANSAC", "Geometry" }
)]
[InputPort("Image", "Input Image", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "Result Image", PortDataType.Image)]
[OutputPort("GeometryResult", "Geometry Result", PortDataType.Any)]
[OperatorParam("Operation", "Operation", "enum", DefaultValue = "SmallestCircle", Options = new[] { 
    "SmallestCircle|Smallest Enclosing Circle",
    "MinAreaRect|Minimum Area Rectangle", 
    "MinAreaTriangle|Minimum Area Triangle",
    "ConvexHull|Convex Hull",
    "FitArc|Fit Arc (RANSAC)",
    "FitCircleRobust|Fit Circle (Robust)",
    "FitEllipseDirect|Fit Ellipse (Direct)"
})]
[OperatorParam("Threshold", "Binary Threshold", "double", DefaultValue = 127.0, Min = 0.0, Max = 255.0)]
[OperatorParam("MinArea", "Min Contour Area", "int", DefaultValue = 100, Min = 0)]
[OperatorParam("ContourSelection", "Contour Selection", "enum", DefaultValue = "LargestContour", Options = new[] { "LargestContour|Largest Contour", "AllContours|All Contours", "FirstContour|First Contour" })]
[OperatorParam("RansacIterations", "RANSAC Iterations", "int", DefaultValue = 500, Min = 10, Max = 5000)]
[OperatorParam("RansacInlierThreshold", "RANSAC Inlier Threshold (px)", "double", DefaultValue = 2.0, Min = 0.1, Max = 50.0)]
[OperatorParam("MinArcAngle", "Min Arc Angle (degrees)", "double", DefaultValue = 30.0, Min = 5.0, Max = 350.0)]
[OperatorParam("MaxArcAngle", "Max Arc Angle (degrees)", "double", DefaultValue = 330.0, Min = 10.0, Max = 360.0)]
[OperatorParam("OutlierRatio", "Expected Outlier Ratio", "double", DefaultValue = 0.3, Min = 0.0, Max = 0.9)]
[OperatorParam("CheckConditionNumber", "Check Condition Number", "bool", DefaultValue = true)]
public class MinEnclosingGeometryOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.MinEnclosingGeometry;

    public MinEnclosingGeometryOperator(ILogger<MinEnclosingGeometryOperator> logger) : base(logger)
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

        var operation = GetStringParam(@operator, "Operation", "SmallestCircle");
        var threshold = GetDoubleParam(@operator, "Threshold", 127.0, 0.0, 255.0);
        var minArea = GetIntParam(@operator, "MinArea", 100, 0);
        var contourSelection = GetStringParam(@operator, "ContourSelection", "LargestContour");
        var ransacIterations = GetIntParam(@operator, "RansacIterations", 500, 10, 5000);
        var ransacThreshold = GetDoubleParam(@operator, "RansacInlierThreshold", 2.0, 0.1, 50.0);
        var minArcAngle = GetDoubleParam(@operator, "MinArcAngle", 30.0, 5.0, 350.0);
        var maxArcAngle = GetDoubleParam(@operator, "MaxArcAngle", 330.0, 10.0, 360.0);
        var outlierRatio = GetDoubleParam(@operator, "OutlierRatio", 0.3, 0.0, 0.9);
        var checkConditionNumber = GetBoolParam(@operator, "CheckConditionNumber", true);

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid."));
        }

        var resultImage = src.Clone();

        using var gray = new Mat();
        if (src.Channels() == 1)
            src.CopyTo(gray);
        else
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        using var binary = new Mat();
        Cv2.Threshold(gray, binary, threshold, 255, ThresholdTypes.Binary);

        Cv2.FindContours(binary, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        var validContours = contours.Where(c => Cv2.ContourArea(c) >= minArea).ToList();

        if (validContours.Count == 0)
        {
            return Task.FromResult(CreateFailureOutput(resultImage, "No valid contour found.", operation));
        }

        var selectedContours = SelectContours(validContours, contourSelection);
        var allPoints = selectedContours
            .SelectMany(c => c)
            .Select(p => new Point2f(p.X, p.Y))
            .ToArray();

        if (allPoints.Length < 3)
        {
            return Task.FromResult(CreateFailureOutput(resultImage, "Insufficient points (need at least 3).", operation));
        }

        var result = operation.ToLowerInvariant() switch
        {
            "smallestcircle" => ComputeSmallestCircle(allPoints, resultImage, checkConditionNumber),
            "minarearect" => ComputeMinAreaRect(allPoints, resultImage),
            "minareatriangle" => ComputeMinAreaTriangle(allPoints, resultImage),
            "convexhull" => ComputeConvexHull(allPoints, resultImage),
            "fitarc" => FitArcRansac(allPoints, resultImage, ransacIterations, ransacThreshold, minArcAngle, maxArcAngle, outlierRatio),
            "fitcirclerobust" => FitCircleRobust(allPoints, resultImage, ransacIterations, ransacThreshold, outlierRatio, checkConditionNumber),
            "fitellipsedirect" => FitEllipseDirect(allPoints, resultImage, checkConditionNumber),
            _ => ComputeSmallestCircle(allPoints, resultImage, checkConditionNumber)
        };

        // 绘制轮廓
        for (var i = 0; i < selectedContours.Count; i++)
        {
            Cv2.DrawContours(resultImage, selectedContours, i, new Scalar(255, 0, 0), 1);
        }

        var outputData = new Dictionary<string, object>
        {
            { "GeometryResult", result },
            { "Operation", operation },
            { "PointCount", allPoints.Length },
            { "ContourCount", selectedContours.Count }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, outputData)));
    }

    private Dictionary<string, object> ComputeSmallestCircle(Point2f[] points, Mat resultImage, bool checkCondition)
    {
        var result = new Dictionary<string, object>
        {
            { "Success", true },
            { "GeometryType", "SmallestCircle" }
        };

        try
        {
            // OpenCV 最小外接圆 (Welzl算法实现)
            Cv2.MinEnclosingCircle(points, out var center, out var radius);

            // 数值稳定性检查：验证点分布
            if (checkCondition)
            {
                var conditionNumber = CalculatePointDistributionCondition(points);
                result["ConditionNumber"] = conditionNumber;
                result["Quality"] = conditionNumber > 1e6 ? "Poor" : conditionNumber > 1e4 ? "Fair" : "Good";
            }

            // 绘制结果
            var centerPoint = new Point((int)center.X, (int)center.Y);
            Cv2.Circle(resultImage, centerPoint, (int)radius, new Scalar(0, 255, 0), 2);
            Cv2.Circle(resultImage, centerPoint, 4, new Scalar(0, 0, 255), -1);
            Cv2.PutText(resultImage, $"C:({center.X:F1},{center.Y:F1}) R:{radius:F1}",
                new Point(centerPoint.X + 10, centerPoint.Y - 10),
                HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 255, 0), 1);

            // 计算包围率（多少点被包含）
            var enclosedCount = points.Count(p => 
                Math.Sqrt(Math.Pow(p.X - center.X, 2) + Math.Pow(p.Y - center.Y, 2)) <= radius + 1);
            var enclosureRatio = (double)enclosedCount / points.Length;

            result["Center"] = new Position(center.X, center.Y);
            result["Radius"] = radius;
            result["EnclosedPoints"] = enclosedCount;
            result["EnclosureRatio"] = enclosureRatio;
            result["IsValid"] = enclosureRatio > 0.95;
        }
        catch (Exception ex)
        {
            result["Success"] = false;
            result["Error"] = ex.Message;
        }

        return result;
    }

    private Dictionary<string, object> ComputeMinAreaRect(Point2f[] points, Mat resultImage)
    {
        var result = new Dictionary<string, object>
        {
            { "Success", true },
            { "GeometryType", "MinAreaRect" }
        };

        try
        {
            // 最小面积旋转矩形
            var rotatedRect = Cv2.MinAreaRect(points);
            
            // 绘制矩形
            var vertices = rotatedRect.Points();
            for (int i = 0; i < 4; i++)
            {
                var pt1 = new Point((int)vertices[i].X, (int)vertices[i].Y);
                var pt2 = new Point((int)vertices[(i + 1) % 4].X, (int)vertices[(i + 1) % 4].Y);
                Cv2.Line(resultImage, pt1, pt2, new Scalar(0, 255, 0), 2);
            }

            // 标记中心
            var center = rotatedRect.Center;
            Cv2.Circle(resultImage, (Point)center, 4, new Scalar(0, 0, 255), -1);

            // 计算矩形属性
            var width = rotatedRect.Size.Width;
            var height = rotatedRect.Size.Height;
            var aspectRatio = Math.Max(width, height) / Math.Max(Math.Min(width, height), 1e-6);

            Cv2.PutText(resultImage, $"W:{width:F1} H:{height:F1} A:{rotatedRect.Angle:F1}°",
                new Point((int)center.X + 10, (int)center.Y - 10),
                HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 255, 0), 1);

            result["Center"] = new Position(center.X, center.Y);
            result["Size"] = new { Width = width, Height = height };
            result["Angle"] = rotatedRect.Angle;
            result["AspectRatio"] = aspectRatio;
            result["Area"] = width * height;
            result["Vertices"] = vertices.Select(v => new Position(v.X, v.Y)).ToList();
        }
        catch (Exception ex)
        {
            result["Success"] = false;
            result["Error"] = ex.Message;
        }

        return result;
    }

    private Dictionary<string, object> ComputeMinAreaTriangle(Point2f[] points, Mat resultImage)
    {
        var result = new Dictionary<string, object>
        {
            { "Success", true },
            { "GeometryType", "MinAreaTriangle" }
        };

        try
        {
            // 计算凸包
            var hull = Cv2.ConvexHull(points);
            
            if (hull.Length < 3)
            {
                result["Success"] = false;
                result["Error"] = "Need at least 3 points for triangle";
                return result;
            }

            // 使用旋转卡壳法找到最小面积包围三角形
            var (triangle, area) = FindMinEnclosingTriangle(hull);

            // 绘制三角形
            for (int i = 0; i < 3; i++)
            {
                var pt1 = new Point((int)triangle[i].X, (int)triangle[i].Y);
                var pt2 = new Point((int)triangle[(i + 1) % 3].X, (int)triangle[(i + 1) % 3].Y);
                Cv2.Line(resultImage, pt1, pt2, new Scalar(255, 0, 255), 2);
            }

            // 标记顶点
            foreach (var pt in triangle)
            {
                Cv2.Circle(resultImage, (Point)pt, 4, new Scalar(255, 255, 0), -1);
            }

            Cv2.PutText(resultImage, $"Area:{area:F1}",
                new Point((int)triangle[0].X, (int)triangle[0].Y - 10),
                HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 0, 255), 1);

            result["Vertices"] = triangle.Select(p => new Position(p.X, p.Y)).ToList();
            result["Area"] = area;
        }
        catch (Exception ex)
        {
            result["Success"] = false;
            result["Error"] = ex.Message;
        }

        return result;
    }

    private Dictionary<string, object> ComputeConvexHull(Point2f[] points, Mat resultImage)
    {
        var result = new Dictionary<string, object>
        {
            { "Success", true },
            { "GeometryType", "ConvexHull" }
        };

        try
        {
            var hull = Cv2.ConvexHull(points);
            
            // 绘制凸包
            for (int i = 0; i < hull.Length; i++)
            {
                var pt1 = new Point((int)hull[i].X, (int)hull[i].Y);
                var pt2 = new Point((int)hull[(i + 1) % hull.Length].X, (int)hull[(i + 1) % hull.Length].Y);
                Cv2.Line(resultImage, pt1, pt2, new Scalar(255, 255, 0), 2);
            }

            // 计算面积和周长
            var hullArea = Cv2.ContourArea(hull);
            var hullPerimeter = Cv2.ArcLength(hull, true);
            var originalArea = Cv2.ContourArea(points);
            var convexity = originalArea / Math.Max(hullArea, 1e-6);

            Cv2.PutText(resultImage, $"Hull Area:{hullArea:F1} Peri:{hullPerimeter:F1}",
                new Point(10, resultImage.Height - 20),
                HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 255, 0), 1);

            result["HullVertices"] = hull.Select(p => new Position(p.X, p.Y)).ToList();
            result["HullArea"] = hullArea;
            result["HullPerimeter"] = hullPerimeter;
            result["Convexity"] = convexity;
            result["VertexCount"] = hull.Length;
        }
        catch (Exception ex)
        {
            result["Success"] = false;
            result["Error"] = ex.Message;
        }

        return result;
    }

    private Dictionary<string, object> FitArcRansac(
        Point2f[] points, Mat resultImage,
        int iterations, double threshold, double minArcAngle, double maxArcAngle,
        double outlierRatio)
    {
        var result = new Dictionary<string, object>
        {
            { "Success", true },
            { "GeometryType", "ArcFit" }
        };

        if (points.Length < 5)
        {
            result["Success"] = false;
            result["Error"] = "Need at least 5 points for arc fitting";
            return result;
        }

        try
        {
            // RANSAC圆弧拟合
            var (circle, inliers, startAngle, endAngle, arcAngle) = FitArcWithRansac(
                points, iterations, threshold, minArcAngle, maxArcAngle);

            if (circle.Radius <= 0 || inliers.Length < 5)
            {
                result["Success"] = false;
                result["Error"] = "Failed to fit arc - insufficient inliers";
                return result;
            }

            var inlierRatio = (double)inliers.Length / points.Length;
            var center = new Point((int)circle.Center.X, (int)circle.Center.Y);

            // 绘制完整圆（虚线表示）
            Cv2.Circle(resultImage, center, (int)circle.Radius, new Scalar(100, 100, 100), 1, LineTypes.Link8);

            // 绘制圆弧部分（实线）
            DrawArc(resultImage, circle.Center, circle.Radius, startAngle, endAngle, new Scalar(0, 255, 0), 2);

            // 绘制端点和中心
            Cv2.Circle(resultImage, center, 4, new Scalar(0, 0, 255), -1);
            
            var startPt = new Point(
                (int)(center.X + circle.Radius * Math.Cos(startAngle * Math.PI / 180)),
                (int)(center.Y + circle.Radius * Math.Sin(startAngle * Math.PI / 180)));
            var endPt = new Point(
                (int)(center.X + circle.Radius * Math.Cos(endAngle * Math.PI / 180)),
                (int)(center.Y + circle.Radius * Math.Sin(endAngle * Math.PI / 180)));
            
            Cv2.Circle(resultImage, startPt, 4, new Scalar(255, 0, 0), -1);
            Cv2.Circle(resultImage, endPt, 4, new Scalar(255, 0, 0), -1);

            // 标注信息
            Cv2.PutText(resultImage, $"Arc: R={circle.Radius:F1} ∠={arcAngle:F1}°",
                new Point(center.X + 10, center.Y - 10),
                HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 255, 0), 1);
            Cv2.PutText(resultImage, $"Inliers: {inliers.Length}/{points.Length} ({inlierRatio:P0})",
                new Point(center.X + 10, center.Y + 20),
                HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 255, 0), 1);

            result["Center"] = new Position(circle.Center.X, circle.Center.Y);
            result["Radius"] = circle.Radius;
            result["StartAngle"] = startAngle;
            result["EndAngle"] = endAngle;
            result["ArcAngle"] = arcAngle;
            result["InlierCount"] = inliers.Length;
            result["InlierRatio"] = inlierRatio;
            result["StartPoint"] = new Position(startPt.X, startPt.Y);
            result["EndPoint"] = new Position(endPt.X, endPt.Y);
            result["IsValid"] = inlierRatio > (1 - outlierRatio - 0.1);
        }
        catch (Exception ex)
        {
            result["Success"] = false;
            result["Error"] = ex.Message;
        }

        return result;
    }

    private Dictionary<string, object> FitCircleRobust(
        Point2f[] points, Mat resultImage,
        int iterations, double threshold, double outlierRatio,
        bool checkCondition)
    {
        var result = new Dictionary<string, object>
        {
            { "Success", true },
            { "GeometryType", "RobustCircleFit" }
        };

        if (points.Length < 5)
        {
            result["Success"] = false;
            result["Error"] = "Need at least 5 points for robust fitting";
            return result;
        }

        try
        {
            // 使用MSAC (M-estimator Sample Consensus) 改进的RANSAC
            var (bestCircle, inliers, meanResidual, maxResidual) = FitCircleMsac(points, iterations, threshold);

            if (bestCircle.Radius <= 0 || inliers.Length < 5)
            {
                result["Success"] = false;
                result["Error"] = "Failed to fit circle - insufficient inliers";
                return result;
            }

            var inlierRatio = (double)inliers.Length / points.Length;
            var center = new Point((int)bestCircle.Center.X, (int)bestCircle.Center.Y);

            // 使用内点进行最小二乘精修
            var refined = RefineCircleLeastSquares(inliers);
            if (refined.Radius > 0)
            {
                bestCircle = refined;
                center = new Point((int)bestCircle.Center.X, (int)bestCircle.Center.Y);
            }

            // 数值稳定性检查
            if (checkCondition)
            {
                var conditionNumber = CalculateCircleFitCondition(inliers, bestCircle);
                result["ConditionNumber"] = conditionNumber;
                result["FitQuality"] = conditionNumber > 1e6 ? "Poor" : conditionNumber > 1e4 ? "Fair" : "Good";
            }

            // 绘制结果
            Cv2.Circle(resultImage, center, (int)bestCircle.Radius, new Scalar(0, 255, 0), 2);
            Cv2.Circle(resultImage, center, 4, new Scalar(0, 0, 255), -1);

            // 绘制内点（绿色）和外点（红色）
            foreach (var pt in points)
            {
                var isInlier = inliers.Any(i => Math.Abs(i.X - pt.X) < 0.1 && Math.Abs(i.Y - pt.Y) < 0.1);
                var color = isInlier ? new Scalar(0, 255, 0) : new Scalar(0, 0, 255);
                Cv2.Circle(resultImage, (Point)pt, 2, color, -1);
            }

            Cv2.PutText(resultImage, $"R={bestCircle.Radius:F2}",
                new Point(center.X + 10, center.Y - 10),
                HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 255, 0), 1);
            Cv2.PutText(resultImage, $"Inliers:{inliers.Length}/{points.Length}",
                new Point(center.X + 10, center.Y + 20),
                HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 255, 0), 1);

            result["Center"] = new Position(bestCircle.Center.X, bestCircle.Center.Y);
            result["Radius"] = bestCircle.Radius;
            result["InlierCount"] = inliers.Length;
            result["OutlierCount"] = points.Length - inliers.Length;
            result["InlierRatio"] = inlierRatio;
            result["MeanResidual"] = meanResidual;
            result["MaxResidual"] = maxResidual;
            result["IsValid"] = inlierRatio > (1 - outlierRatio - 0.1) && meanResidual < threshold * 2;
        }
        catch (Exception ex)
        {
            result["Success"] = false;
            result["Error"] = ex.Message;
        }

        return result;
    }

    private Dictionary<string, object> FitEllipseDirect(Point2f[] points, Mat resultImage, bool checkCondition)
    {
        var result = new Dictionary<string, object>
        {
            { "Success", true },
            { "GeometryType", "DirectEllipseFit" }
        };

        if (points.Length < 5)
        {
            result["Success"] = false;
            result["Error"] = "Need at least 5 points for ellipse fitting";
            return result;
        }

        try
        {
            // Fitzgibbon的直接最小二乘椭圆拟合（已归一化）
            var rotatedRect = Cv2.FitEllipse(points);

            // 数值稳定性：检查椭圆参数
            if (checkCondition)
            {
                var condition = CalculateEllipseCondition(points, rotatedRect);
                result["ConditionNumber"] = condition;
                result["FitQuality"] = condition > 1e6 ? "Poor" : condition > 1e4 ? "Fair" : "Good";
            }

            // 验证椭圆有效性
            if (rotatedRect.Size.Width <= 0 || rotatedRect.Size.Height <= 0 ||
                double.IsNaN(rotatedRect.Angle))
            {
                result["Success"] = false;
                result["Error"] = "Invalid ellipse parameters";
                return result;
            }

            // 绘制椭圆
            Cv2.Ellipse(resultImage, rotatedRect, new Scalar(0, 255, 0), 2);
            Cv2.Circle(resultImage, (Point)rotatedRect.Center, 4, new Scalar(0, 0, 255), -1);

            // 计算几何属性
            var a = rotatedRect.Size.Width / 2;  // 半长轴
            var b = rotatedRect.Size.Height / 2; // 半短轴
            var eccentricity = Math.Sqrt(1 - Math.Min(a, b) * Math.Min(a, b) / (Math.Max(a, b) * Math.Max(a, b)));

            Cv2.PutText(resultImage, $"A:{rotatedRect.Size.Width:F1} B:{rotatedRect.Size.Height:F1}",
                new Point((int)rotatedRect.Center.X + 10, (int)rotatedRect.Center.Y - 10),
                HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 255, 0), 1);

            result["Center"] = new Position(rotatedRect.Center.X, rotatedRect.Center.Y);
            result["MajorAxis"] = rotatedRect.Size.Width;
            result["MinorAxis"] = rotatedRect.Size.Height;
            result["Angle"] = rotatedRect.Angle;
            result["Eccentricity"] = eccentricity;
            result["Area"] = Math.PI * a * b;
        }
        catch (Exception ex)
        {
            result["Success"] = false;
            result["Error"] = ex.Message;
        }

        return result;
    }

    #region Helper Methods

    private (Circle circle, Point2f[] inliers, double meanResidual, double maxResidual) FitCircleMsac(
        Point2f[] points, int iterations, double threshold)
    {
        var random = new Random(points.Length * 397);
        Circle bestCircle = default;
        var bestInliers = Array.Empty<Point2f>();
        var bestScore = double.MaxValue;
        var bestMeanResidual = 0.0;
        var bestMaxResidual = 0.0;

        for (int i = 0; i < iterations; i++)
        {
            // 随机采样3点
            if (!TryPickDistinctIndices(random, points.Length, out var i1, out var i2, out var i3))
                continue;

            if (!TryCreateCircleModel(points[i1], points[i2], points[i3], out var circle))
                continue;

            // 计算所有点的残差
            var residuals = points.Select(p => Math.Abs(DistancePointToCircle(p, circle))).ToArray();
            var inlierIndices = residuals.Select((r, idx) => new { r, idx })
                .Where(x => x.r <= threshold)
                .Select(x => x.idx)
                .ToArray();

            if (inlierIndices.Length < 5)
                continue;

            // MSAC评分：内点用残差，外点用惩罚常数
            var inlierResidualSum = inlierIndices.Sum(idx => residuals[idx]);
            var outlierPenalty = (points.Length - inlierIndices.Length) * threshold;
            var score = inlierResidualSum + outlierPenalty;

            if (score < bestScore)
            {
                bestScore = score;
                bestCircle = circle;
                bestInliers = inlierIndices.Select(idx => points[idx]).ToArray();
                bestMeanResidual = inlierIndices.Average(idx => residuals[idx]);
                bestMaxResidual = inlierIndices.Max(idx => residuals[idx]);
            }
        }

        return (bestCircle, bestInliers, bestMeanResidual, bestMaxResidual);
    }

    private (Circle circle, Point2f[] inliers, double startAngle, double endAngle, double arcAngle) FitArcWithRansac(
        Point2f[] points, int iterations, double threshold, double minArcAngle, double maxArcAngle)
    {
        var random = new Random(points.Length * 733);
        Circle bestCircle = default;
        var bestInliers = Array.Empty<Point2f>();
        double bestStartAngle = 0, bestEndAngle = 0, bestArcAngle = 0;
        var bestScore = double.MaxValue;

        for (int i = 0; i < iterations; i++)
        {
            if (!TryPickDistinctIndices(random, points.Length, out var i1, out var i2, out var i3))
                continue;

            if (!TryCreateCircleModel(points[i1], points[i2], points[i3], out var circle))
                continue;

            // 计算所有点的角度和残差
            var angleResiduals = points.Select(p => new
            {
                Point = p,
                Angle = Math.Atan2(p.Y - circle.Center.Y, p.X - circle.Center.X) * 180 / Math.PI,
                Residual = Math.Abs(DistancePointToCircle(p, circle))
            }).ToArray();

            // 找到内点
            var inliers = angleResiduals.Where(ar => ar.Residual <= threshold).ToArray();
            if (inliers.Length < 5)
                continue;

            // 计算角度范围
            var angles = inliers.Select(a => NormalizeAngle(a.Angle)).OrderBy(a => a).ToArray();
            var (startAngle, endAngle, arcAngle) = CalculateArcAngle(angles);

            // 检查圆弧角度约束
            if (arcAngle < minArcAngle || arcAngle > maxArcAngle)
                continue;

            // 评分：考虑内点数和角度范围
            var score = -inliers.Length * arcAngle; // 内点多且角度大的得分高

            if (score < bestScore)
            {
                bestScore = score;
                bestCircle = circle;
                bestInliers = inliers.Select(a => a.Point).ToArray();
                bestStartAngle = startAngle;
                bestEndAngle = endAngle;
                bestArcAngle = arcAngle;
            }
        }

        return (bestCircle, bestInliers, bestStartAngle, bestEndAngle, bestArcAngle);
    }

    private Circle RefineCircleLeastSquares(Point2f[] points)
    {
        // Kasa算法（简化版最小二乘圆拟合）
        var n = points.Length;
        double sumX = 0, sumY = 0, sumX2 = 0, sumY2 = 0, sumXY = 0;
        double sumX3 = 0, sumY3 = 0, sumX2Y = 0, sumXY2 = 0;

        for (int i = 0; i < n; i++)
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

        var a = 2 * (sumX * sumX - n * sumX2);
        var b = 2 * (sumX * sumY - n * sumXY);
        var c = 2 * (sumY * sumY - n * sumY2);
        var d = sumX2 * sumX + sumY2 * sumX - n * sumX3 - n * sumXY2;
        var e = sumX2 * sumY + sumY2 * sumY - n * sumX2Y - n * sumY3;

        var det = a * c - b * b;
        if (Math.Abs(det) < 1e-10)
            return default;

        var cx = (d * c - b * e) / det;
        var cy = (a * e - b * d) / det;
        var r = Math.Sqrt(sumX2 / n - 2 * cx * sumX / n + cx * cx + sumY2 / n - 2 * cy * sumY / n + cy * cy);

        return new Circle(new Point2f((float)cx, (float)cy), (float)r);
    }

    private (Point2f[] triangle, double area) FindMinEnclosingTriangle(Point2f[] hull)
    {
        // 简化的最小包围三角形算法（使用凸包顶点枚举）
        var minArea = double.MaxValue;
        var bestTriangle = new Point2f[3];

        // 限制检查顶点数以提高性能
        var step = hull.Length > 50 ? hull.Length / 50 : 1;

        for (int i = 0; i < hull.Length; i += step)
        {
            for (int j = i + 1; j < hull.Length; j += step)
            {
                for (int k = j + 1; k < hull.Length; k += step)
                {
                    var triangle = new[] { hull[i], hull[j], hull[k] };
                    var area = CalculateTriangleArea(triangle);

                    // 检查是否包含所有点
                    if (area < minArea && hull.All(p => IsPointInTriangle(p, triangle)))
                    {
                        minArea = area;
                        bestTriangle = triangle;
                    }
                }
            }
        }

        return (bestTriangle, minArea);
    }

    private double CalculateTriangleArea(Point2f[] triangle)
    {
        if (triangle.Length != 3) return 0;
        var a = triangle[0];
        var b = triangle[1];
        var c = triangle[2];
        return Math.Abs((b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y)) / 2;
    }

    private bool IsPointInTriangle(Point2f p, Point2f[] triangle)
    {
        var (a, b, c) = (triangle[0], triangle[1], triangle[2]);
        
        var denom = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
        if (Math.Abs(denom) < 1e-10) return false;

        var w1 = ((b.Y - c.Y) * (p.X - c.X) + (c.X - b.X) * (p.Y - c.Y)) / denom;
        var w2 = ((c.Y - a.Y) * (p.X - c.X) + (a.X - c.X) * (p.Y - c.Y)) / denom;
        var w3 = 1 - w1 - w2;

        return w1 >= 0 && w2 >= 0 && w3 >= 0;
    }

    private void DrawArc(Mat image, Point2f center, float radius, double startAngle, double endAngle, Scalar color, int thickness)
    {
        // OpenCV的ellipse函数绘制圆弧
        var angleStep = (endAngle - startAngle) / 36; // 每10度一个点
        if (angleStep == 0) angleStep = 10;

        var prevPt = new Point(
            (int)(center.X + radius * Math.Cos(startAngle * Math.PI / 180)),
            (int)(center.Y + radius * Math.Sin(startAngle * Math.PI / 180)));

        for (double angle = startAngle + Math.Abs(angleStep); 
             (angleStep > 0 ? angle <= endAngle : angle >= endAngle); 
             angle += angleStep)
        {
            var pt = new Point(
                (int)(center.X + radius * Math.Cos(angle * Math.PI / 180)),
                (int)(center.Y + radius * Math.Sin(angle * Math.PI / 180)));
            Cv2.Line(image, prevPt, pt, color, thickness);
            prevPt = pt;
        }

        // 最后一段
        var lastPt = new Point(
            (int)(center.X + radius * Math.Cos(endAngle * Math.PI / 180)),
            (int)(center.Y + radius * Math.Sin(endAngle * Math.PI / 180)));
        Cv2.Line(image, prevPt, lastPt, color, thickness);
    }

    private (double startAngle, double endAngle, double arcAngle) CalculateArcAngle(double[] sortedAngles)
    {
        if (sortedAngles.Length == 0) return (0, 0, 0);
        if (sortedAngles.Length == 1) return (sortedAngles[0], sortedAngles[0], 0);

        // 找到最大间隙
        var maxGap = 0.0;
        var maxGapIndex = 0;

        for (int i = 0; i < sortedAngles.Length; i++)
        {
            var next = (i + 1) % sortedAngles.Length;
            var gap = next == 0 ? sortedAngles[0] + 360 - sortedAngles[i] : sortedAngles[next] - sortedAngles[i];
            if (gap > maxGap)
            {
                maxGap = gap;
                maxGapIndex = i;
            }
        }

        // 圆弧在最大间隙的对面
        var startIdx = (maxGapIndex + 1) % sortedAngles.Length;
        var endIdx = maxGapIndex;

        var startAngle = sortedAngles[startIdx];
        var endAngle = sortedAngles[endIdx];
        var arcAngle = 360 - maxGap;

        return (startAngle, endAngle, arcAngle);
    }

    private double NormalizeAngle(double angle)
    {
        while (angle < 0) angle += 360;
        while (angle >= 360) angle -= 360;
        return angle;
    }

    private double CalculatePointDistributionCondition(Point2f[] points)
    {
        // 计算点分布的条件数（用于评估最小外接圆的稳定性）
        var meanX = points.Average(p => p.X);
        var meanY = points.Average(p => p.Y);

        double sxx = 0, syy = 0, sxy = 0;
        foreach (var p in points)
        {
            var dx = p.X - meanX;
            var dy = p.Y - meanY;
            sxx += dx * dx;
            syy += dy * dy;
            sxy += dx * dy;
        }

        var trace = sxx + syy;
        var det = sxx * syy - sxy * sxy;
        if (det < 1e-10) return double.MaxValue;

        var e1 = (trace + Math.Sqrt(trace * trace - 4 * det)) / 2;
        var e2 = (trace - Math.Sqrt(trace * trace - 4 * det)) / 2;

        return Math.Max(e1, e2) / Math.Max(e2, 1e-10);
    }

    private double CalculateCircleFitCondition(Point2f[] points, Circle circle)
    {
        // 计算圆拟合的条件数（基于雅可比矩阵）
        var n = points.Length;
        var jtj = new double[3, 3]; // J^T * J 近似

        for (int i = 0; i < n; i++)
        {
            var dx = points[i].X - circle.Center.X;
            var dy = points[i].Y - circle.Center.Y;
            var r = Math.Sqrt(dx * dx + dy * dy);
            if (r < 1e-6) continue;

            // 关于(cx, cy, r)的导数
            var dfdc = -dx / r;
            var dfdy = -dy / r;
            var dfdr = -1;

            jtj[0, 0] += dfdc * dfdc;
            jtj[0, 1] += dfdc * dfdy;
            jtj[0, 2] += dfdc * dfdr;
            jtj[1, 1] += dfdy * dfdy;
            jtj[1, 2] += dfdy * dfdr;
            jtj[2, 2] += dfdr * dfdr;
        }

        jtj[1, 0] = jtj[0, 1];
        jtj[2, 0] = jtj[0, 2];
        jtj[2, 1] = jtj[1, 2];

        // 计算条件数
        using var mat = new Mat(3, 3, MatType.CV_64FC1, jtj);
        using var w = new Mat();
        using var u = new Mat();
        using var vt = new Mat();
        Cv2.SVDecomp(mat, w, u, vt);
        var singularValues = ReadVector(w);

        if (singularValues.Length < 3 || singularValues[^1] < 1e-10) return double.MaxValue;
        return singularValues[0] / singularValues[^1];
    }

    private double CalculateEllipseCondition(Point2f[] points, RotatedRect ellipse)
    {
        // 简化版：基于长短轴比
        var ratio = Math.Max(ellipse.Size.Width, ellipse.Size.Height) / 
                    Math.Max(Math.Min(ellipse.Size.Width, ellipse.Size.Height), 1);
        return ratio * ratio; // 条件数近似
    }

    private List<Point[]> SelectContours(List<Point[]> validContours, string contourSelection)
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

    private static bool TryPickDistinctIndices(Random random, int upperExclusive, out int i1, out int i2, out int i3)
    {
        i1 = random.Next(upperExclusive);
        i2 = random.Next(upperExclusive);
        i3 = random.Next(upperExclusive);
        return i1 != i2 && i1 != i3 && i2 != i3;
    }

    private static bool TryCreateCircleModel(Point2f p1, Point2f p2, Point2f p3, out Circle circle)
    {
        circle = default;
        var x1 = p1.X; var y1 = p1.Y;
        var x2 = p2.X; var y2 = p2.Y;
        var x3 = p3.X; var y3 = p3.Y;

        var a = x1 * (y2 - y3) - y1 * (x2 - x3) + x2 * y3 - x3 * y2;
        if (Math.Abs(a) < 1e-6) return false;

        var x1Sq = x1 * x1 + y1 * y1;
        var x2Sq = x2 * x2 + y2 * y2;
        var x3Sq = x3 * x3 + y3 * y3;

        var cx = (x1Sq * (y3 - y2) + x2Sq * (y1 - y3) + x3Sq * (y2 - y1)) / (2 * a);
        var cy = (x1Sq * (x2 - x3) + x2Sq * (x3 - x1) + x3Sq * (x1 - x2)) / (2 * a);
        var r = Math.Sqrt((x1 - cx) * (x1 - cx) + (y1 - cy) * (y1 - cy));

        if (double.IsNaN(r) || r <= 0) return false;

        circle = new Circle(new Point2f((float)cx, (float)cy), (float)r);
        return true;
    }

    private static double DistancePointToCircle(Point2f point, Circle circle)
    {
        var distToCenter = Math.Sqrt(
            Math.Pow(point.X - circle.Center.X, 2) + 
            Math.Pow(point.Y - circle.Center.Y, 2));
        return distToCenter - circle.Radius;
    }

    private OperatorExecutionOutput CreateFailureOutput(Mat input, string message, string operation)
    {
        var output = input.Clone();
        Cv2.PutText(output, $"NG: {message}", new Point(10, 30),
            HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 0, 255), 2);

        return OperatorExecutionOutput.Success(CreateImageOutput(output, new Dictionary<string, object>
        {
            { "Success", false },
            { "Operation", operation },
            { "Error", message }
        }));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var threshold = GetDoubleParam(@operator, "Threshold", 127.0);
        if (threshold < 0 || threshold > 255)
            return ValidationResult.Invalid("Threshold must be between 0 and 255.");

        var ransacIterations = GetIntParam(@operator, "RansacIterations", 500);
        if (ransacIterations < 10 || ransacIterations > 5000)
            return ValidationResult.Invalid("RANSAC iterations must be between 10 and 5000.");

        var ransacThreshold = GetDoubleParam(@operator, "RansacInlierThreshold", 2.0);
        if (ransacThreshold <= 0 || ransacThreshold > 50)
            return ValidationResult.Invalid("RANSAC threshold must be between 0 and 50.");

        return ValidationResult.Valid();
    }

    private readonly record struct Circle(Point2f Center, float Radius);

    private static double[] ReadVector(Mat mat)
    {
        if (mat.Empty())
        {
            return Array.Empty<double>();
        }

        var count = mat.Rows * mat.Cols;
        var values = new double[count];
        for (var i = 0; i < count; i++)
        {
            var row = mat.Rows == 1 ? 0 : i;
            var col = mat.Rows == 1 ? i : 0;
            values[i] = mat.At<double>(row, col);
        }

        return values;
    }

    #endregion
}
