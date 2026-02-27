// ImageAddOperator.cs
// 图像加法算子 - 图像叠加// 功能实现合并
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 图像加法算子 - 图像叠加/合并
/// 【第三优先级】图像预处理算子扩展
/// </summary>
[OperatorMeta(
    DisplayName = "图像加法",
    Description = "两幅图像叠加/合并",
    Category = "预处理",
    IconName = "add"
)]
[InputPort("Image1", "图像1", PortDataType.Image, IsRequired = true)]
[InputPort("Image2", "图像2", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "合成图像", PortDataType.Image)]
[OperatorParam("Scale1", "图像1权重", "double", DefaultValue = 1.0, Min = 0, Max = 10.0)]
[OperatorParam("Scale2", "图像2权重", "double", DefaultValue = 1.0, Min = 0, Max = 10.0)]
[OperatorParam("Offset", "亮度偏移", "double", DefaultValue = 0, Min = -255, Max = 255)]
[OperatorParam("SizeMismatchPolicy", "尺寸不一致策略", "enum", DefaultValue = "Resize", Options = new[] { "Resize|Resize", "Fail|Fail", "Crop|CropToOverlap", "AnchorPaste|AnchorPaste" })]
[OperatorParam("OffsetX", "图像2偏移X", "int", DefaultValue = 0, Min = -100000, Max = 100000)]
[OperatorParam("OffsetY", "图像2偏移Y", "int", DefaultValue = 0, Min = -100000, Max = 100000)]
public class ImageAddOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ImageAdd;

    public ImageAddOperator(ILogger<ImageAddOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        // 获取两幅输入图像
        if (!TryGetInputImage(inputs, "Image1", out var image1Wrapper) || image1Wrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供第一幅输入图像(Image1)"));
        }

        if (!TryGetInputImage(inputs, "Image2", out var image2Wrapper) || image2Wrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供第二幅输入图像(Image2)"));
        }

        // 获取参数
        var scale1 = GetDoubleParam(@operator, "Scale1", 1.0, min: 0, max: 10.0);
        var scale2 = GetDoubleParam(@operator, "Scale2", 1.0, min: 0, max: 10.0);
        var offset = GetDoubleParam(@operator, "Offset", 0, min: -255, max: 255);
        var sizeMismatchPolicy = GetStringParam(@operator, "SizeMismatchPolicy", "Resize");
        var offsetX = GetIntParam(@operator, "OffsetX", 0, min: -100000, max: 100000);
        var offsetY = GetIntParam(@operator, "OffsetY", 0, min: -100000, max: 100000);

        var src1 = image1Wrapper.GetMat();
        var src2 = image2Wrapper.GetMat();

        if (src1.Empty() || src2.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        if (!TryPrepareAlignedImage(
                src1,
                src2,
                sizeMismatchPolicy,
                offsetX,
                offsetY,
                out var aligned2,
                out var appliedPolicy,
                out var policyMessage))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(policyMessage));
        }

        using var alignedMat = aligned2;
        var dst = new Mat();
        Cv2.AddWeighted(src1, scale1, alignedMat, scale2, offset, dst);

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst, new Dictionary<string, object>
        {
            { "Scale1", scale1 },
            { "Scale2", scale2 },
            { "Offset", offset },
            { "SizeMismatchPolicy", appliedPolicy },
            { "PolicyMessage", policyMessage },
            { "OffsetX", offsetX },
            { "OffsetY", offsetY }
        })));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var scale1 = GetDoubleParam(@operator, "Scale1", 1.0);
        if (scale1 < 0 || scale1 > 10.0)
            return ValidationResult.Invalid("图像1缩放因子必须在 0-10.0 之间");

        var scale2 = GetDoubleParam(@operator, "Scale2", 1.0);
        if (scale2 < 0 || scale2 > 10.0)
            return ValidationResult.Invalid("图像2缩放因子必须在 0-10.0 之间");

        var policy = GetStringParam(@operator, "SizeMismatchPolicy", "Resize");
        var validPolicies = new[] { "Resize", "Fail", "Crop", "AnchorPaste" };
        if (!validPolicies.Contains(policy, StringComparer.OrdinalIgnoreCase))
            return ValidationResult.Invalid("尺寸不一致策略必须是 Resize / Fail / Crop / AnchorPaste");

        return ValidationResult.Valid();
    }

    private static bool TryPrepareAlignedImage(
        Mat reference,
        Mat source,
        string policyRaw,
        int offsetX,
        int offsetY,
        out Mat aligned,
        out string appliedPolicy,
        out string message)
    {
        aligned = new Mat();
        appliedPolicy = NormalizePolicy(policyRaw);
        message = "Image sizes already match.";

        using var convertedSource = TryConvertToReferenceType(source, reference, out var conversionError);
        if (convertedSource == null)
        {
            message = conversionError;
            return false;
        }

        if (reference.Size() == convertedSource.Size())
        {
            aligned = convertedSource.Clone();
            appliedPolicy = "SameSize";
            return true;
        }

        switch (appliedPolicy)
        {
            case "Resize":
            {
                Cv2.Resize(convertedSource, aligned, reference.Size());
                message = $"Image2 resized from {convertedSource.Width}x{convertedSource.Height} to {reference.Width}x{reference.Height}.";
                return true;
            }

            case "Fail":
            {
                message = $"Image size mismatch: Image1={reference.Width}x{reference.Height}, Image2={convertedSource.Width}x{convertedSource.Height}. Set SizeMismatchPolicy to Resize/Crop/AnchorPaste.";
                return false;
            }

            case "Crop":
            {
                aligned = new Mat(reference.Size(), reference.Type(), Scalar.Black);
                var overlapWidth = Math.Min(reference.Width, convertedSource.Width);
                var overlapHeight = Math.Min(reference.Height, convertedSource.Height);
                if (overlapWidth <= 0 || overlapHeight <= 0)
                {
                    aligned.Dispose();
                    aligned = new Mat();
                    message = "No overlap region between Image1 and Image2 when applying Crop policy.";
                    return false;
                }

                using var srcRoi = new Mat(convertedSource, new Rect(0, 0, overlapWidth, overlapHeight));
                using var dstRoi = new Mat(aligned, new Rect(0, 0, overlapWidth, overlapHeight));
                srcRoi.CopyTo(dstRoi);
                message = $"Image2 cropped to overlap region {overlapWidth}x{overlapHeight} (top-left anchor).";
                return true;
            }

            case "AnchorPaste":
            {
                aligned = new Mat(reference.Size(), reference.Type(), Scalar.Black);
                if (!TryCopyWithOffset(convertedSource, aligned, offsetX, offsetY, out var copiedRect))
                {
                    message = $"Image2 is fully outside Image1 canvas after offset ({offsetX},{offsetY}).";
                    return false;
                }

                message = $"Image2 pasted with offset ({offsetX},{offsetY}), copied region {copiedRect.Width}x{copiedRect.Height}.";
                return true;
            }

            default:
            {
                message = $"Unsupported SizeMismatchPolicy: {policyRaw}";
                return false;
            }
        }
    }

    private static string NormalizePolicy(string policyRaw)
    {
        if (string.IsNullOrWhiteSpace(policyRaw))
        {
            return "Resize";
        }

        if (policyRaw.Equals("CropToOverlap", StringComparison.OrdinalIgnoreCase))
        {
            return "Crop";
        }

        return policyRaw.Trim();
    }

    private static Mat? TryConvertToReferenceType(Mat source, Mat reference, out string error)
    {
        error = string.Empty;
        var converted = source.Clone();
        try
        {
            if (converted.Channels() != reference.Channels())
            {
                using var channelAdjusted = new Mat();
                if (!TryConvertChannels(converted, channelAdjusted, reference.Channels()))
                {
                    converted.Dispose();
                    error = $"Image channel mismatch is unsupported: Image1 channels={reference.Channels()}, Image2 channels={source.Channels()}.";
                    return null;
                }

                converted.Dispose();
                converted = channelAdjusted.Clone();
            }

            if (converted.Type() != reference.Type())
            {
                using var depthAdjusted = new Mat();
                converted.ConvertTo(depthAdjusted, reference.Type());
                converted.Dispose();
                converted = depthAdjusted.Clone();
            }

            return converted;
        }
        catch (Exception ex)
        {
            converted.Dispose();
            error = $"Failed to align Image2 type/channels: {ex.Message}";
            return null;
        }
    }

    private static bool TryConvertChannels(Mat source, Mat destination, int targetChannels)
    {
        if (source.Channels() == targetChannels)
        {
            source.CopyTo(destination);
            return true;
        }

        if (source.Channels() == 1 && targetChannels == 3)
        {
            Cv2.CvtColor(source, destination, ColorConversionCodes.GRAY2BGR);
            return true;
        }

        if (source.Channels() == 1 && targetChannels == 4)
        {
            Cv2.CvtColor(source, destination, ColorConversionCodes.GRAY2BGRA);
            return true;
        }

        if (source.Channels() == 3 && targetChannels == 1)
        {
            Cv2.CvtColor(source, destination, ColorConversionCodes.BGR2GRAY);
            return true;
        }

        if (source.Channels() == 3 && targetChannels == 4)
        {
            Cv2.CvtColor(source, destination, ColorConversionCodes.BGR2BGRA);
            return true;
        }

        if (source.Channels() == 4 && targetChannels == 3)
        {
            Cv2.CvtColor(source, destination, ColorConversionCodes.BGRA2BGR);
            return true;
        }

        if (source.Channels() == 4 && targetChannels == 1)
        {
            Cv2.CvtColor(source, destination, ColorConversionCodes.BGRA2GRAY);
            return true;
        }

        return false;
    }

    private static bool TryCopyWithOffset(Mat source, Mat destination, int offsetX, int offsetY, out Rect copiedRect)
    {
        copiedRect = new Rect();

        var dstX = Math.Max(0, offsetX);
        var dstY = Math.Max(0, offsetY);
        var srcX = Math.Max(0, -offsetX);
        var srcY = Math.Max(0, -offsetY);

        var copyWidth = Math.Min(source.Width - srcX, destination.Width - dstX);
        var copyHeight = Math.Min(source.Height - srcY, destination.Height - dstY);
        if (copyWidth <= 0 || copyHeight <= 0)
        {
            return false;
        }

        copiedRect = new Rect(dstX, dstY, copyWidth, copyHeight);
        using var srcRoi = new Mat(source, new Rect(srcX, srcY, copyWidth, copyHeight));
        using var dstRoi = new Mat(destination, copiedRect);
        srcRoi.CopyTo(dstRoi);
        return true;
    }
}
