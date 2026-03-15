// BlobDetectionOperator.cs
// Blob检测算子 - 检测图像中的连通区域
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;


using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// Blob检测算子 - 检测图像中的连通区域
/// </summary>
[OperatorMeta(
    DisplayName = "Blob分析",
    Description = "连通区域分析",
    Category = "特征提取",
    IconName = "blob",
    Keywords = new[] { "连通域", "缺陷区域", "斑点", "面积提取", "缺陷分析", "Blob", "Connected components" }
)]
[InputPort("Image", "图像", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "标记图像", PortDataType.Image)]
[OutputPort("Blobs", "Blob数据", PortDataType.Contour)]
[OutputPort("BlobCount", "Blob数量", PortDataType.Integer)]
[OperatorParam("MinArea", "最小面积", "int", DefaultValue = 100, Min = 0)]
[OperatorParam("MaxArea", "最大面积", "int", DefaultValue = 100000, Min = 0)]
[OperatorParam("Color", "目标颜色", "enum", DefaultValue = "White", Options = new[] { "White|白色", "Black|黑色" })]
[OperatorParam("MinCircularity", "最小圆度", "double", DefaultValue = 0.0, Min = 0.0, Max = 1.0)]
[OperatorParam("MinConvexity", "最小凸度", "double", DefaultValue = 0.0, Min = 0.0, Max = 1.0)]
[OperatorParam("MinInertiaRatio", "最小惯性比", "double", DefaultValue = 0.0, Min = 0.0, Max = 1.0)]
[OperatorParam("EnableColorFilter", "启用颜色过滤", "bool", DefaultValue = false, Description = "启用HSV颜色范围预过滤")]
[OperatorParam("HueLow", "色相下限", "int", DefaultValue = 0, Min = 0, Max = 180)]
[OperatorParam("HueHigh", "色相上限", "int", DefaultValue = 180, Min = 0, Max = 180)]
[OperatorParam("SatLow", "饱和度下限", "int", DefaultValue = 50, Min = 0, Max = 255)]
[OperatorParam("SatHigh", "饱和度上限", "int", DefaultValue = 255, Min = 0, Max = 255)]
[OperatorParam("ValLow", "明度下限", "int", DefaultValue = 50, Min = 0, Max = 255)]
[OperatorParam("ValHigh", "明度上限", "int", DefaultValue = 255, Min = 0, Max = 255)]
public class BlobDetectionOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.BlobAnalysis;

    public BlobDetectionOperator(ILogger<BlobDetectionOperator> logger) : base(logger)
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

        var minArea = GetFloatParam(@operator, "MinArea", 100f, min: 0);
        var maxArea = GetFloatParam(@operator, "MaxArea", 100000f, min: 0);
        var color = GetStringParam(@operator, "Color", "White");
        var minCircularity = GetDoubleParam(@operator, "MinCircularity", 0.0, min: 0, max: 1.0);
        var minConvexity = GetDoubleParam(@operator, "MinConvexity", 0.0, min: 0, max: 1.0);
        var minInertiaRatio = GetDoubleParam(@operator, "MinInertiaRatio", 0.0, min: 0, max: 1.0);
        var enableColorFilter = GetBoolParam(@operator, "EnableColorFilter", false);
        var hueLow = GetIntParam(@operator, "HueLow", 0, 0, 180);
        var hueHigh = GetIntParam(@operator, "HueHigh", 180, 0, 180);
        var satLow = GetIntParam(@operator, "SatLow", 50, 0, 255);
        var satHigh = GetIntParam(@operator, "SatHigh", 255, 0, 255);
        var valLow = GetIntParam(@operator, "ValLow", 50, 0, 255);
        var valHigh = GetIntParam(@operator, "ValHigh", 255, 0, 255);

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }
        
        // 颜色预过滤
        Mat processedSrc = src;
        Mat? colorMask = null;
        if (enableColorFilter)
        {
            colorMask = ApplyColorFilter(src, hueLow, hueHigh, satLow, satHigh, valLow, valHigh);
            if (colorMask != null)
            {
                // 应用掩码到原图
                processedSrc = new Mat();
                Cv2.BitwiseAnd(src, src, processedSrc, colorMask);
            }
        }

        // SimpleBlobDetector 内部会自动处理灰度转换，支持彩色和灰度输入
        var detector = new SimpleBlobDetector.Params();
        detector.FilterByArea = true;
        detector.MinArea = minArea;
        detector.MaxArea = maxArea;
        detector.FilterByColor = true;
        detector.BlobColor = color.Equals("Black", StringComparison.OrdinalIgnoreCase) ? (byte)0 : (byte)255;

        if (minCircularity > 0)
        {
            detector.FilterByCircularity = true;
            detector.MinCircularity = (float)minCircularity;
        }

        if (minConvexity > 0)
        {
            detector.FilterByConvexity = true;
            detector.MinConvexity = (float)minConvexity;
        }

        if (minInertiaRatio > 0)
        {
            detector.FilterByInertia = true;
            detector.MinInertiaRatio = (float)minInertiaRatio;
        }

        using var blobDetector = SimpleBlobDetector.Create(detector);
        var keypoints = blobDetector.Detect(processedSrc);

        // 准备彩色结果图（用于绘制彩色标注）
        var colorSrc = new Mat();
        if (processedSrc.Channels() == 1)
            Cv2.CvtColor(processedSrc, colorSrc, ColorConversionCodes.GRAY2BGR);
        else
            processedSrc.CopyTo(colorSrc);

        foreach (var kp in keypoints)
        {
            Cv2.Circle(colorSrc, (int)kp.Pt.X, (int)kp.Pt.Y, (int)kp.Size / 2, new Scalar(0, 255, 0), 2);
            Cv2.Circle(colorSrc, (int)kp.Pt.X, (int)kp.Pt.Y, 3, new Scalar(0, 0, 255), -1);
        }

        // P0: 使用ImageWrapper实现零拷贝输出
        var additionalData = new Dictionary<string, object>
        {
            { "BlobCount", keypoints.Length },
            { "Blobs", keypoints.Select(kp => new Dictionary<string, object>
            {
                { "X", kp.Pt.X },
                { "Y", kp.Pt.Y },
                { "Size", kp.Size },
                { "Area", Math.PI * Math.Pow(kp.Size / 2, 2) }
            }).ToList() }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(colorSrc, additionalData)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var minArea = GetFloatParam(@operator, "MinArea", 100f);
        var maxArea = GetFloatParam(@operator, "MaxArea", 100000f);

        if (minArea < 0 || maxArea < 0)
        {
            return ValidationResult.Invalid("面积范围不能为负数");
        }

        if (minArea >= maxArea)
        {
            return ValidationResult.Invalid("最小面积必须小于最大面积");
        }

        var color = GetStringParam(@operator, "Color", "White");
        var validColors = new[] { "White", "Black" };
        if (!validColors.Contains(color, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Color must be White or Black.");
        }

        return ValidationResult.Valid();
    }

    /// <summary>
    /// 应用HSV颜色范围过滤
    /// </summary>
    private Mat? ApplyColorFilter(Mat src, int hueLow, int hueHigh, int satLow, int satHigh, int valLow, int valHigh)
    {
        try
        {
            using var hsv = new Mat();
            if (src.Channels() == 3)
            {
                Cv2.CvtColor(src, hsv, ColorConversionCodes.BGR2HSV);
            }
            else if (src.Channels() == 1)
            {
                // 灰度图无法应用HSV过滤，返回空掩码
                return null;
            }
            else
            {
                return null;
            }

            // 创建HSV范围掩码
            var lower = new Scalar(hueLow, satLow, valLow);
            var upper = new Scalar(hueHigh, satHigh, valHigh);
            var mask = new Mat();
            Cv2.InRange(hsv, lower, upper, mask);

            return mask;
        }
        catch
        {
            return null;
        }
    }
}
