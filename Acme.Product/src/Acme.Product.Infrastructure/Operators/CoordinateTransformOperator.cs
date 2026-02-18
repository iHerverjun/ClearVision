// CoordinateTransformOperator.cs
// 坐标转换算子 - 像素坐标到物理坐标转换
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System.Text.Json;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 坐标转换算子 - 像素坐标到物理坐标转换
/// </summary>
public class CoordinateTransformOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.CoordinateTransform;

    public CoordinateTransformOperator(ILogger<CoordinateTransformOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        // 获取输入图像（可选）
        ImageWrapper? imageWrapper = null;
        if (inputs != null && inputs.TryGetValue("Image", out var imgObj))
        {
            ImageWrapper.TryGetFromObject(imgObj, out imageWrapper);
        }

        // 获取像素坐标
        var pixelX = GetParameterOrInput(inputs, @operator, "PixelX", 0.0);
        var pixelY = GetParameterOrInput(inputs, @operator, "PixelY", 0.0);

        // 获取参数
        var pixelSize = GetDoubleParam(@operator, "PixelSize", 0.01, 0.0001, 100.0);
        var calibrationFile = GetStringParam(@operator, "CalibrationFile", "");

        // 从标定文件加载变换矩阵（如果有）
        double originX = 0;
        double originY = 0;
        double scaleX = pixelSize;
        double scaleY = pixelSize;

        if (!string.IsNullOrEmpty(calibrationFile) && File.Exists(calibrationFile))
        {
            try
            {
                var calibrationData = File.ReadAllText(calibrationFile);
                var calInfo = JsonSerializer.Deserialize<CalibrationInfo>(calibrationData);
                if (calInfo != null)
                {
                    originX = calInfo.OriginX;
                    originY = calInfo.OriginY;
                    scaleX = calInfo.ScaleX > 0 ? calInfo.ScaleX : pixelSize;
                    scaleY = calInfo.ScaleY > 0 ? calInfo.ScaleY : pixelSize;
                }
            }
            catch { }
        }

        // 计算物理坐标
        var physicalX = originX + pixelX * scaleX;
        var physicalY = originY + pixelY * scaleY;

        // 准备输出图像
        Dictionary<string, object> outputData;

        if (imageWrapper != null)
        {
            using var src = imageWrapper.GetMat();
            if (!src.Empty())
            {
                using var resultImage = src.Clone();

                // 绘制标记
                var center = new Point((int)pixelX, (int)pixelY);
                Cv2.Circle(resultImage, center, 5, new Scalar(0, 0, 255), -1);
                Cv2.Circle(resultImage, center, 10, new Scalar(0, 255, 0), 2);

                // 显示坐标信息
                var text = $"Pixel: ({pixelX:F1}, {pixelY:F1})";
                var physText = $"Phys: ({physicalX:F3}, {physicalY:F3})";
                Cv2.PutText(resultImage, text, new Point(10, 30),
                    HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 255, 255), 2);
                Cv2.PutText(resultImage, physText, new Point(10, 55),
                    HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 255, 255), 2);

                outputData = CreateImageOutput(resultImage, new Dictionary<string, object>
                {
                    { "PixelX", pixelX },
                    { "PixelY", pixelY },
                    { "PhysicalX", physicalX },
                    { "PhysicalY", physicalY },
                    { "PixelSize", pixelSize },
                    { "ScaleX", scaleX },
                    { "ScaleY", scaleY }
                });
            }
            else
            {
                outputData = CreateImageOutput(src, new Dictionary<string, object>
                {
                    { "PixelX", pixelX },
                    { "PixelY", pixelY },
                    { "PhysicalX", physicalX },
                    { "PhysicalY", physicalY },
                    { "PixelSize", pixelSize },
                    { "ScaleX", scaleX },
                    { "ScaleY", scaleY }
                });
            }
        }
        else
        {
            outputData = new Dictionary<string, object>
            {
                { "Image", new byte[0] },
                { "Width", 0 },
                { "Height", 0 },
                { "PixelX", pixelX },
                { "PixelY", pixelY },
                { "PhysicalX", physicalX },
                { "PhysicalY", physicalY },
                { "PixelSize", pixelSize },
                { "ScaleX", scaleX },
                { "ScaleY", scaleY }
            };
        }

        return Task.FromResult(OperatorExecutionOutput.Success(outputData));
    }

    private double GetParameterOrInput(Dictionary<string, object>? inputs, Operator @operator, string paramName, double defaultValue)
    {
        // 优先从inputs获取
        if (inputs != null && inputs.TryGetValue(paramName, out var val))
        {
            try
            {
                return Convert.ToDouble(val);
            }
            catch { }
        }

        // 从参数获取
        return GetDoubleParam(@operator, paramName, defaultValue);
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var pixelSize = GetDoubleParam(@operator, "PixelSize", 0.01);
        if (pixelSize <= 0 || pixelSize > 100)
        {
            return ValidationResult.Invalid("像素尺寸必须在 0-100 mm/px 之间");
        }
        return ValidationResult.Valid();
    }

    private class CalibrationInfo
    {
        public double OriginX { get; set; }
        public double OriginY { get; set; }
        public double ScaleX { get; set; }
        public double ScaleY { get; set; }
    }
}
