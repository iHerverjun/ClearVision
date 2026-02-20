// RoiManagerOperator.cs
// ROI管理器算子 - 矩形// 功能实现圆形// 功能实现多边形区域选择
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System.Text.Json;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// ROI管理器算子 - 矩形/圆形/多边形区域选择
/// </summary>
public class RoiManagerOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.RoiManager;

    public RoiManagerOperator(ILogger<RoiManagerOperator> logger) : base(logger)
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
        var shape = GetStringParam(@operator, "Shape", "Rectangle");
        var operation = GetStringParam(@operator, "Operation", "Crop");
        var x = GetIntParam(@operator, "X", 0, min: 0);
        var y = GetIntParam(@operator, "Y", 0, min: 0);
        var width = GetIntParam(@operator, "Width", 200, min: 1);
        var height = GetIntParam(@operator, "Height", 200, min: 1);
        var centerX = GetIntParam(@operator, "CenterX", 100);
        var centerY = GetIntParam(@operator, "CenterY", 100);
        var radius = GetIntParam(@operator, "Radius", 50, min: 1);
        var polygonPoints = GetStringParam(@operator, "PolygonPoints", "[[10,10],[200,10],[200,200],[10,200]]");

        // 3. 获取 Mat
        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        // 边界检查
        width = Math.Min(width, src.Width - x);
        height = Math.Min(height, src.Height - y);
        if (width <= 0 || height <= 0)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("ROI区域超出图像边界"));
        }

        Mat resultImage;
        Mat mask = new Mat(src.Size(), MatType.CV_8UC1, Scalar.All(0));

        try
        {
            switch (shape)
            {
                case "Rectangle":
                    ProcessRectangle(src, out resultImage, mask, operation, x, y, width, height);
                    break;
                case "Circle":
                    ProcessCircle(src, out resultImage, mask, operation, centerX, centerY, radius);
                    break;
                case "Polygon":
                    ProcessPolygon(src, out resultImage, mask, operation, polygonPoints);
                    break;
                default:
                    ProcessRectangle(src, out resultImage, mask, operation, x, y, width, height);
                    break;
            }

            var additionalData = new Dictionary<string, object>
            {
                { "Shape", shape },
                { "Operation", operation },
                { "Mask", new ImageWrapper(mask.Clone()) }
            };

            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, additionalData)));
        }
        finally
        {
            mask.Dispose();
        }
    }

    private void ProcessRectangle(Mat src, out Mat resultImage, Mat mask, string operation, int x, int y, int width, int height)
    {
        var rect = new Rect(x, y, width, height);

        if (operation == "Crop")
        {
            // 裁剪模式
            resultImage = new Mat(src, rect);
            // 创建完整尺寸的掩膜
            Cv2.Rectangle(mask, rect, Scalar.All(255), -1);
        }
        else
        {
            // 掩膜模式 - 保留原图，应用掩膜
            resultImage = src.Clone();
            Cv2.Rectangle(mask, rect, Scalar.All(255), -1);
            Cv2.BitwiseAnd(src, src, resultImage, mask);
        }
    }

    private void ProcessCircle(Mat src, out Mat resultImage, Mat mask, string operation, int centerX, int centerY, int radius)
    {
        var center = new Point(centerX, centerY);

        // 计算外接矩形
        var rectX = Math.Max(0, centerX - radius);
        var rectY = Math.Max(0, centerY - radius);
        var rectWidth = Math.Min(radius * 2, src.Width - rectX);
        var rectHeight = Math.Min(radius * 2, src.Height - rectY);

        // 在掩膜上绘制圆形
        Cv2.Circle(mask, center, radius, Scalar.All(255), -1);

        if (operation == "Crop")
        {
            // 裁剪模式 - 裁剪外接矩形并应用圆形掩膜
            var rect = new Rect(rectX, rectY, rectWidth, rectHeight);
            using var cropped = new Mat(src, rect);
            using var croppedMask = new Mat(mask, rect);
            resultImage = new Mat(cropped.Size(), src.Type(), Scalar.All(0));
            Cv2.BitwiseAnd(cropped, cropped, resultImage, croppedMask);
        }
        else
        {
            // 掩膜模式
            resultImage = src.Clone();
            Cv2.BitwiseAnd(src, src, resultImage, mask);
        }
    }

    private void ProcessPolygon(Mat src, out Mat resultImage, Mat mask, string operation, string polygonPointsJson)
    {
        Point[][]? points = null;
        try
        {
            var pointArrays = JsonSerializer.Deserialize<int[][]>(polygonPointsJson);
            if (pointArrays != null && pointArrays.Length >= 3)
            {
                points = new[] { pointArrays.Select(p => new Point(p[0], p[1])).ToArray() };
            }
        }
        catch
        {
            points = null;
        }

        if (points == null)
        {
            // 解析失败，使用默认矩形
            points = new[] { new[] { new Point(10, 10), new Point(200, 10), new Point(200, 200), new Point(10, 200) } };
        }

        // 在掩膜上填充多边形
        Cv2.FillPoly(mask, points, Scalar.All(255));

        if (operation == "Crop")
        {
            // 裁剪模式 - 计算多边形外接矩形
            var allPoints = points[0];
            var minX = allPoints.Min(p => p.X);
            var minY = allPoints.Min(p => p.Y);
            var maxX = allPoints.Max(p => p.X);
            var maxY = allPoints.Max(p => p.Y);

            minX = Math.Max(0, minX);
            minY = Math.Max(0, minY);
            maxX = Math.Min(src.Width, maxX);
            maxY = Math.Min(src.Height, maxY);

            var rect = new Rect(minX, minY, maxX - minX, maxY - minY);
            using var cropped = new Mat(src, rect);
            using var croppedMask = new Mat(mask, rect);
            resultImage = new Mat(cropped.Size(), src.Type(), Scalar.All(0));
            Cv2.BitwiseAnd(cropped, cropped, resultImage, croppedMask);
        }
        else
        {
            // 掩膜模式
            resultImage = src.Clone();
            Cv2.BitwiseAnd(src, src, resultImage, mask);
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var shape = GetStringParam(@operator, "Shape", "Rectangle");
        var validShapes = new[] { "Rectangle", "Circle", "Polygon" };
        if (!validShapes.Contains(shape))
            return ValidationResult.Invalid($"形状必须是: {string.Join(", ", validShapes)}");

        var operation = GetStringParam(@operator, "Operation", "Crop");
        var validOperations = new[] { "Crop", "Mask" };
        if (!validOperations.Contains(operation))
            return ValidationResult.Invalid($"操作必须是: {string.Join(", ", validOperations)}");

        var width = GetIntParam(@operator, "Width", 200);
        if (width < 1)
            return ValidationResult.Invalid("宽度必须大于0");

        var height = GetIntParam(@operator, "Height", 200);
        if (height < 1)
            return ValidationResult.Invalid("高度必须大于0");

        var radius = GetIntParam(@operator, "Radius", 50);
        if (radius < 1)
            return ValidationResult.Invalid("半径必须大于0");

        return ValidationResult.Valid();
    }
}
