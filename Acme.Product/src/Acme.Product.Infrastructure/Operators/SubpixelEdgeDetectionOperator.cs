// SubpixelEdgeDetectionOperator.cs
// 亚像素边缘提取算子 - 高精度边缘定位
// 使用 Steger 算法 (基于 Hessian 矩阵的亚像素定位)
// 参考论文: C. Steger, "An unbiased detector of curvilinear structures", IEEE TPAMI, 1998
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.ImageProcessing;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 亚像素边缘提取算子 - 高精度边缘定位
/// 
/// 支持两种算法:
/// 1. Steger - 基于 Hessian 矩阵的亚像素定位 (0.01-0.1px 精度)
/// 2. GradientInterp - 梯度插值法 (简化版本)
/// 3. GaussianFit - 高斯拟合法
/// </summary>
public class SubpixelEdgeDetectionOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.SubpixelEdgeDetection;

    public SubpixelEdgeDetectionOperator(ILogger<SubpixelEdgeDetectionOperator> logger) : base(logger)
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
        var lowThreshold = GetDoubleParam(@operator, "LowThreshold", 50.0, min: 0.0, max: 255.0);
        var highThreshold = GetDoubleParam(@operator, "HighThreshold", 150.0, min: 0.0, max: 255.0);
        var sigma = GetDoubleParam(@operator, "Sigma", 1.0, min: 0.1, max: 10.0);
        var method = GetStringParam(@operator, "Method", "Steger");
        var edgeThreshold = GetDoubleParam(@operator, "EdgeThreshold", 10.0, min: 0.0, max: 1000.0);

        // 3. 获取 Mat
        using var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        return RunCpuBoundWork(() =>
        {
            // 创建结果图像
            using var resultImage = src.Clone();

            List<SubpixelEdgePoint> edgePoints;
            int contourCount = 0;

            if (method == "Steger")
            {
                // 使用 Steger 算法
                using var detector = new StegerSubpixelEdgeDetector
                {
                    EdgeThreshold = edgeThreshold,
                    MaxOffset = 0.5
                };

                edgePoints = detector.DetectEdges(src, lowThreshold, highThreshold);
                contourCount = edgePoints.Count > 0 ? 1 : 0; // Steger 不直接返回轮廓数量
            }
            else
            {
                // 使用传统方法
                var (points, contours) = DetectEdgesTraditional(src, lowThreshold, highThreshold, sigma, method);
                edgePoints = points;
                contourCount = contours;
            }

            // 绘制边缘点
            foreach (var point in edgePoints)
            {
                Cv2.Circle(resultImage,
                    new Point((int)point.X, (int)point.Y),
                    1, new Scalar(0, 255, 0), -1);

                // 绘制法向方向
                var endX = (int)(point.X + point.NormalX * 5);
                var endY = (int)(point.Y + point.NormalY * 5);
                Cv2.Line(resultImage,
                    new Point((int)point.X, (int)point.Y),
                    new Point(endX, endY),
                    new Scalar(255, 0, 0), 1);
            }

            // 显示统计信息
            var info = $"{method}: {edgePoints.Count} edges";
            Cv2.PutText(resultImage, info, new Point(10, 30),
                HersheyFonts.HersheySimplex, 0.7, new Scalar(255, 255, 0), 2);

            // 转换为输出格式
            var subpixelEdges = edgePoints.Select(p => new Dictionary<string, object>
            {
                { "X", p.X },
                { "Y", p.Y },
                { "NormalX", p.NormalX },
                { "NormalY", p.NormalY },
                { "Strength", p.Strength }
            }).ToList();

            var additionalData = new Dictionary<string, object>
            {
                { "Edges", subpixelEdges },
                { "EdgeCount", edgePoints.Count },
                { "ContourCount", contourCount },
                { "Method", method }
            };

            return OperatorExecutionOutput.Success(CreateImageOutput(resultImage, additionalData));
        }, cancellationToken);
    }

    /// <summary>
    /// 传统边缘检测方法 (GradientInterp / GaussianFit)
    /// </summary>
    private (List<SubpixelEdgePoint> points, int contourCount) DetectEdgesTraditional(
        Mat src, double lowThreshold, double highThreshold, double sigma, string method)
    {
        var edgePoints = new List<SubpixelEdgePoint>();

        // 灰度 → GaussianBlur → Canny → FindContours
        using var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        using var blurred = new Mat();
        var kernelSize = (int)(sigma * 6) | 1; // 确保为奇数
        Cv2.GaussianBlur(gray, blurred, new Size(kernelSize, kernelSize), sigma);

        using var edges = new Mat();
        Cv2.Canny(blurred, edges, lowThreshold, highThreshold);

        Cv2.FindContours(edges, out var contours, out _,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        // 计算 Sobel 梯度
        using var sobelX = new Mat();
        using var sobelY = new Mat();
        Cv2.Sobel(blurred, sobelX, MatType.CV_64F, 1, 0, 3);
        Cv2.Sobel(blurred, sobelY, MatType.CV_64F, 0, 1, 3);

        foreach (var contour in contours)
        {
            foreach (var point in contour)
            {
                var subpixelPoint = CalculateSubpixelEdgeTraditional(
                    point, gray, sobelX, sobelY, method);

                if (subpixelPoint != null)
                {
                    edgePoints.Add(subpixelPoint);
                }
            }
        }

        return (edgePoints, contours.Length);
    }

    private SubpixelEdgePoint? CalculateSubpixelEdgeTraditional(
        Point pixelPoint, Mat gray, Mat sobelX, Mat sobelY, string method)
    {
        int x = pixelPoint.X;
        int y = pixelPoint.Y;

        // 边界检查
        if (x <= 0 || x >= gray.Width - 1 || y <= 0 || y >= gray.Height - 1)
            return null;

        // 获取梯度值
        double dx = sobelX.At<double>(y, x);
        double dy = sobelY.At<double>(y, x);

        // 计算梯度方向和大小
        double gradMag = Math.Sqrt(dx * dx + dy * dy);
        if (gradMag < 1e-6)
            return null;

        double dxNorm = dx / gradMag;
        double dyNorm = dy / gradMag;

        double subpixelX, subpixelY;
        double strength = gradMag;

        if (method == "GradientInterp")
        {
            // 梯度插值法
            double g1 = BilinearInterpolate(gray, x - dxNorm, y - dyNorm);
            double g2 = gray.At<byte>(y, x);
            double g3 = BilinearInterpolate(gray, x + dxNorm, y + dyNorm);

            double denom = g1 - 2 * g2 + g3;
            if (Math.Abs(denom) < 1e-10)
                return null;

            double offset = 0.5 * (g1 - g3) / denom;

            subpixelX = x + offset * dxNorm;
            subpixelY = y + offset * dyNorm;
        }
        else // GaussianFit
        {
            double g1 = BilinearInterpolate(gray, x - dxNorm, y - dyNorm);
            double g2 = gray.At<byte>(y, x);
            double g3 = BilinearInterpolate(gray, x + dxNorm, y + dyNorm);

            double denom = g1 - 2 * g2 + g3;
            if (Math.Abs(denom) < 1e-10)
                return null;

            double offset = 0.5 * (g1 - g3) / denom;
            offset = offset * Math.Exp(-offset * offset / 2);

            subpixelX = x + offset * dxNorm;
            subpixelY = y + offset * dyNorm;
        }

        return new SubpixelEdgePoint
        {
            X = subpixelX,
            Y = subpixelY,
            NormalX = dxNorm,
            NormalY = dyNorm,
            Strength = strength
        };
    }

    private double BilinearInterpolate(Mat image, double x, double y)
    {
        int x0 = (int)Math.Floor(x);
        int y0 = (int)Math.Floor(y);
        int x1 = Math.Min(x0 + 1, image.Width - 1);
        int y1 = Math.Min(y0 + 1, image.Height - 1);

        double dx = x - x0;
        double dy = y - y0;

        double v00 = image.At<byte>(y0, x0);
        double v01 = image.At<byte>(y0, x1);
        double v10 = image.At<byte>(y1, x0);
        double v11 = image.At<byte>(y1, x1);

        return v00 * (1 - dx) * (1 - dy) +
               v01 * dx * (1 - dy) +
               v10 * (1 - dx) * dy +
               v11 * dx * dy;
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var lowThreshold = GetDoubleParam(@operator, "LowThreshold", 50.0);
        if (lowThreshold < 0 || lowThreshold > 255)
            return ValidationResult.Invalid("低阈值必须在 0-255 之间");

        var highThreshold = GetDoubleParam(@operator, "HighThreshold", 150.0);
        if (highThreshold < 0 || highThreshold > 255)
            return ValidationResult.Invalid("高阈值必须在 0-255 之间");

        if (lowThreshold >= highThreshold)
            return ValidationResult.Invalid("低阈值必须小于高阈值");

        var sigma = GetDoubleParam(@operator, "Sigma", 1.0);
        if (sigma < 0.1 || sigma > 10.0)
            return ValidationResult.Invalid("高斯Sigma必须在 0.1-10.0 之间");

        var method = GetStringParam(@operator, "Method", "Steger");
        var validMethods = new[] { "Steger", "GradientInterp", "GaussianFit" };
        if (!validMethods.Contains(method))
            return ValidationResult.Invalid($"亚像素方法必须是: {string.Join(", ", validMethods)}");

        var edgeThreshold = GetDoubleParam(@operator, "EdgeThreshold", 10.0);
        if (edgeThreshold < 0 || edgeThreshold > 1000)
            return ValidationResult.Invalid("边缘阈值必须在 0-1000 之间");

        return ValidationResult.Valid();
    }
}
