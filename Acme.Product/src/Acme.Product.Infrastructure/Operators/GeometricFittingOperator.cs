// GeometricFittingOperator.cs
// 最小二乘圆拟合 (Kasa 方法)
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 几何拟合算子 - 直线/圆/椭圆拟合
/// </summary>
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
        // 1. 获取图像输入
        if (!TryGetInputImage(inputs, "Image", out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供输入图像"));
        }

        // 2. 获取参数
        var fitType = GetStringParam(@operator, "FitType", "Circle");
        var threshold = GetDoubleParam(@operator, "Threshold", 127.0, min: 0, max: 255);
        var minArea = GetIntParam(@operator, "MinArea", 100, min: 0);
        var minPoints = GetIntParam(@operator, "MinPoints", 5, min: 3, max: 10000);

        // 3. 获取 Mat
        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        // 创建结果图像
        var resultImage = src.Clone();

        // 4. 核心算法 - 灰度 → 二值化 → 查找轮廓
        using var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        using var binary = new Mat();
        Cv2.Threshold(gray, binary, threshold, 255, ThresholdTypes.Binary);

        // 查找轮廓
        Cv2.FindContours(binary, out var contours, out _,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        // 过滤面积
        var validContours = contours
            .Where(c => Cv2.ContourArea(c) >= minArea)
            .ToList();

        if (validContours.Count == 0)
        {
            var noContourData = new Dictionary<string, object>
            {
                { "FitResult", new { Success = false, Message = "未找到符合条件的轮廓" } }
            };
            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, noContourData)));
        }

        // 合并所有轮廓点
        var allPoints = validContours
            .SelectMany(c => c)
            .Select(p => new Point2f(p.X, p.Y))
            .ToArray();

        if (allPoints.Length < minPoints)
        {
            var insufficientData = new Dictionary<string, object>
            {
                { "FitResult", new { Success = false, Message = $"点数不足，需要至少 {minPoints} 个点，实际 {allPoints.Length} 个" } }
            };
            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, insufficientData)));
        }

        // 5. 根据拟合类型执行不同算法
        var fitResult = new Dictionary<string, object> { { "Success", true } };

        switch (fitType)
        {
            case "Line":
                FitLine(allPoints, resultImage, fitResult);
                break;
            case "Circle":
                FitCircle(allPoints, resultImage, fitResult, minPoints);
                break;
            case "Ellipse":
                FitEllipse(allPoints, resultImage, fitResult, minPoints);
                break;
            default:
                FitCircle(allPoints, resultImage, fitResult, minPoints);
                break;
        }

        // 绘制轮廓
        for (int i = 0; i < validContours.Count; i++)
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

    private void FitLine(Point2f[] points, Mat resultImage, Dictionary<string, object> fitResult)
    {
        // 使用 OpenCV FitLine
        var lineParams = Cv2.FitLine(points, DistanceTypes.L2, 0, 0.01, 0.01);

        var vx = lineParams.Vx;
        var vy = lineParams.Vy;
        var x0 = lineParams.X1;
        var y0 = lineParams.Y1;

        // 计算直线两个端点用于绘制
        var leftY = (int)(y0 - x0 * vy / vx);
        var rightY = (int)(y0 + (resultImage.Width - x0) * vy / vx);

        Cv2.Line(resultImage, new Point(0, leftY), new Point(resultImage.Width, rightY), new Scalar(0, 255, 0), 2);

        // 计算角度
        var angle = Math.Atan2(vy, vx) * 180 / Math.PI;

        fitResult["LineVx"] = vx;
        fitResult["LineVy"] = vy;
        fitResult["LineX0"] = x0;
        fitResult["LineY0"] = y0;
        fitResult["Angle"] = angle;
    }

    private void FitCircle(Point2f[] points, Mat resultImage, Dictionary<string, object> fitResult, int minPoints)
    {
        // 使用最小二乘圆拟合
        var (cx, cy, r) = FitCircleLeastSquares(points);

        if (r > 0)
        {
            var center = new Point((int)cx, (int)cy);
            Cv2.Circle(resultImage, center, (int)r, new Scalar(0, 255, 0), 2);
            Cv2.Circle(resultImage, center, 3, new Scalar(0, 0, 255), -1);

            fitResult["CenterX"] = cx;
            fitResult["CenterY"] = cy;
            fitResult["Radius"] = r;
        }
        else
        {
            fitResult["Success"] = false;
            fitResult["Message"] = "圆拟合失败";
        }
    }

    private void FitEllipse(Point2f[] points, Mat resultImage, Dictionary<string, object> fitResult, int minPoints)
    {
        // 椭圆拟合需要至少5个点
        if (points.Length < 5)
        {
            fitResult["Success"] = false;
            fitResult["Message"] = "椭圆拟合需要至少5个点";
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
            fitResult["Message"] = "椭圆拟合失败";
        }
    }

    /// <summary>
    /// 最小二乘圆拟合 (Kasa 方法)
    /// </summary>
    private (double cx, double cy, double r) FitCircleLeastSquares(Point2f[] points)
    {
        int n = points.Length;
        double sumX = 0, sumY = 0, sumX2 = 0, sumY2 = 0, sumXY = 0;
        double sumX3 = 0, sumY3 = 0, sumX2Y = 0, sumXY2 = 0;

        for (int i = 0; i < n; i++)
        {
            double x = points[i].X, y = points[i].Y;
            sumX += x; sumY += y;
            sumX2 += x * x; sumY2 += y * y; sumXY += x * y;
            sumX3 += x * x * x; sumY3 += y * y * y;
            sumX2Y += x * x * y; sumXY2 += x * y * y;
        }

        double A = n * sumX2 - sumX * sumX;
        double B = n * sumXY - sumX * sumY;
        double C = n * sumY2 - sumY * sumY;
        double D = 0.5 * (n * sumX3 + n * sumXY2 - sumX * sumX2 - sumX * sumY2);
        double E = 0.5 * (n * sumX2Y + n * sumY3 - sumY * sumX2 - sumY * sumY2);

        double det = A * C - B * B;
        if (Math.Abs(det) < 1e-10) return (0, 0, 0);

        double cx = (D * C - B * E) / det;
        double cy = (A * E - B * D) / det;
        double r = Math.Sqrt(sumX2 / n - 2 * cx * sumX / n + cx * cx
                           + sumY2 / n - 2 * cy * sumY / n + cy * cy);
        return (cx, cy, r);
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var threshold = GetDoubleParam(@operator, "Threshold", 127.0);
        if (threshold < 0 || threshold > 255)
            return ValidationResult.Invalid("二值化阈值必须在 0-255 之间");

        var minArea = GetIntParam(@operator, "MinArea", 100);
        if (minArea < 0)
            return ValidationResult.Invalid("最小轮廓面积不能为负数");

        var minPoints = GetIntParam(@operator, "MinPoints", 5);
        if (minPoints < 3)
            return ValidationResult.Invalid("最少拟合点数不能少于 3");

        var fitType = GetStringParam(@operator, "FitType", "Circle");
        var validTypes = new[] { "Line", "Circle", "Ellipse" };
        if (!validTypes.Contains(fitType))
            return ValidationResult.Invalid($"拟合类型必须是: {string.Join(", ", validTypes)}");

        return ValidationResult.Valid();
    }
}
