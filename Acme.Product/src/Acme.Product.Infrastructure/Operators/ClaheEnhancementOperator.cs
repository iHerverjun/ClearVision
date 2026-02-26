// ClaheEnhancementOperator.cs
// CLAHE自适应直方图均衡化算子 - 专门用于局部对比度增强
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// CLAHE自适应直方图均衡化算子 - 专门用于局部对比度增强
/// 【第三优先级】图像预处理算子扩展
/// </summary>
[OperatorMeta(
    DisplayName = "CLAHE增强",
    Description = "自适应直方图均衡化，用于局部对比度增强",
    Category = "预处理",
    IconName = "clahe"
)]
[InputPort("Image", "图像", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "增强图像", PortDataType.Image)]
[OperatorParam("ClipLimit", "裁剪限制", "double", Description = "对比度限制阈值，防止过度放大噪声", DefaultValue = 2.0, Min = 0, Max = 40)]
[OperatorParam("TileWidth", "网格宽度", "int", DefaultValue = 8, Min = 2, Max = 64)]
[OperatorParam("TileHeight", "网格高度", "int", DefaultValue = 8, Min = 2, Max = 64)]
[OperatorParam("ColorSpace", "颜色空间", "enum", DefaultValue = "Lab", Options = new[] { "Lab|Lab - L通道", "HSV|HSV - V通道", "Gray|灰度", "All|所有通道" })]
public class ClaheEnhancementOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ClaheEnhancement;

    public ClaheEnhancementOperator(ILogger<ClaheEnhancementOperator> logger) : base(logger)
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

        // 获取参数
        var clipLimit = GetDoubleParam(@operator, "ClipLimit", 2.0, min: 0, max: 40);
        var tileWidth = GetIntParam(@operator, "TileWidth", 8, min: 2, max: 64);
        var tileHeight = GetIntParam(@operator, "TileHeight", 8, min: 2, max: 64);
        var colorSpace = GetStringParam(@operator, "ColorSpace", "Lab"); // Lab / HSV / Gray
        var channel = GetStringParam(@operator, "Channel", "L"); // L / V / Y / All

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        var dst = new Mat();

        // 创建CLAHE对象
        using var clahe = Cv2.CreateCLAHE(clipLimit, new Size(tileWidth, tileHeight));

        if (src.Channels() == 1 || colorSpace.ToLower() == "gray")
        {
            // 单通道图像直接处理
            if (src.Channels() == 3)
            {
                Cv2.CvtColor(src, dst, ColorConversionCodes.BGR2GRAY);
                using var gray = dst.Clone();
                clahe.Apply(gray, dst);
            }
            else
            {
                clahe.Apply(src, dst);
            }
        }
        else if (colorSpace.ToLower() == "lab")
        {
            // Lab颜色空间 - 对L通道处理
            using var lab = new Mat();
            Cv2.CvtColor(src, lab, ColorConversionCodes.BGR2Lab);
            Cv2.Split(lab, out var channels);

            using var lChannel = new Mat();
            clahe.Apply(channels[0], lChannel);

            using var merged = new Mat();
            Cv2.Merge(new Mat[] { lChannel, channels[1], channels[2] }, merged);

            foreach (var ch in channels) ch.Dispose();

            Cv2.CvtColor(merged, dst, ColorConversionCodes.Lab2BGR);
        }
        else if (colorSpace.ToLower() == "hsv")
        {
            // HSV颜色空间 - 对V通道处理
            using var hsv = new Mat();
            Cv2.CvtColor(src, hsv, ColorConversionCodes.BGR2HSV);
            Cv2.Split(hsv, out var channels);

            using var vChannel = new Mat();
            clahe.Apply(channels[2], vChannel);

            using var merged = new Mat();
            Cv2.Merge(new Mat[] { channels[0], channels[1], vChannel }, merged);

            foreach (var ch in channels) ch.Dispose();

            Cv2.CvtColor(merged, dst, ColorConversionCodes.HSV2BGR);
        }
        else if (colorSpace.ToLower() == "all")
        {
            // 对每个通道分别处理
            Cv2.Split(src, out var channels);
            var processedChannels = new Mat[channels.Length];

            for (int i = 0; i < channels.Length; i++)
            {
                processedChannels[i] = new Mat();
                clahe.Apply(channels[i], processedChannels[i]);
                channels[i].Dispose();
            }

            Cv2.Merge(processedChannels, dst);
            foreach (var mat in processedChannels) mat.Dispose();
        }
        else
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"不支持的颜色空间: {colorSpace}"));
        }

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst, new Dictionary<string, object>
        {
            { "ClipLimit", clipLimit },
            { "TileSize", $"{tileWidth}x{tileHeight}" },
            { "ColorSpace", colorSpace }
        })));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var clipLimit = GetDoubleParam(@operator, "ClipLimit", 2.0);
        if (clipLimit < 0 || clipLimit > 40)
            return ValidationResult.Invalid("裁剪限制必须在 0-40 之间");

        var tileWidth = GetIntParam(@operator, "TileWidth", 8);
        if (tileWidth < 2 || tileWidth > 64)
            return ValidationResult.Invalid("网格宽度必须在 2-64 之间");

        var tileHeight = GetIntParam(@operator, "TileHeight", 8);
        if (tileHeight < 2 || tileHeight > 64)
            return ValidationResult.Invalid("网格高度必须在 2-64 之间");

        var colorSpace = GetStringParam(@operator, "ColorSpace", "Lab").ToLower();
        var validSpaces = new[] { "lab", "hsv", "gray", "all" };
        if (!validSpaces.Contains(colorSpace))
            return ValidationResult.Invalid($"不支持的颜色空间: {colorSpace}");

        return ValidationResult.Valid();
    }
}
