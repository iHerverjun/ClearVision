using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Threshold",
    Description = "Global thresholding with optional Otsu or Triangle auto-thresholding.",
    Category = "Preprocessing",
    IconName = "threshold",
    Keywords = new[] { "threshold", "binarize", "segmentation", "otsu", "triangle" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OperatorParam("Threshold", "Threshold", "double", DefaultValue = 127.0, Min = 0.0, Max = 255.0)]
[OperatorParam("MaxValue", "Max Value", "double", DefaultValue = 255.0, Min = 0.0, Max = 255.0)]
[OperatorParam("Type", "Type", "enum", DefaultValue = "0", Options = new[] { "0|Binary", "1|Binary Inv", "2|Trunc", "3|To Zero", "4|To Zero Inv", "8|Otsu", "16|Triangle" })]
[OperatorParam("UseOtsu", "Use Otsu", "bool", DefaultValue = false)]
public class ThresholdOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.Thresholding;

    public ThresholdOperator(ILogger<ThresholdOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, "Image", out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("No input image provided."));
        }

        var threshold = GetDoubleParam(@operator, "Threshold", 127.0, min: 0, max: 255);
        var maxValue = GetDoubleParam(@operator, "MaxValue", 255.0, min: 0, max: 255);
        var typeValue = GetIntParam(@operator, "Type", 0);
        var useOtsu = GetBoolParam(@operator, "UseOtsu", false);

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid."));
        }

        if (!TryResolveThresholdType(typeValue, useOtsu, out var thresholdType, out var thresholdError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(thresholdError));
        }

        using var gray = new Mat();
        if (src.Channels() > 1)
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            src.CopyTo(gray);
        }

        using var binary = new Mat();
        var actualThreshold = Cv2.Threshold(gray, binary, threshold, maxValue, thresholdType);

        var additionalData = new Dictionary<string, object>
        {
            ["ActualThreshold"] = actualThreshold
        };

        if ((thresholdType & ThresholdTypes.Otsu) == ThresholdTypes.Otsu)
        {
            additionalData["OtsuThreshold"] = actualThreshold;
        }

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(binary.Clone(), additionalData)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var threshold = GetDoubleParam(@operator, "Threshold", 127.0);
        if (threshold < 0 || threshold > 255)
        {
            return ValidationResult.Invalid("Threshold must be between 0 and 255.");
        }

        var maxValue = GetDoubleParam(@operator, "MaxValue", 255.0);
        if (maxValue < 0 || maxValue > 255)
        {
            return ValidationResult.Invalid("MaxValue must be between 0 and 255.");
        }

        var typeValue = GetIntParam(@operator, "Type", 0);
        var useOtsu = GetBoolParam(@operator, "UseOtsu", false);
        if (!TryResolveThresholdType(typeValue, useOtsu, out _, out var thresholdError))
        {
            return ValidationResult.Invalid(thresholdError);
        }

        return ValidationResult.Valid();
    }

    private static bool TryResolveThresholdType(
        int typeValue,
        bool useOtsu,
        out ThresholdTypes thresholdType,
        out string error)
    {
        const int automaticMask = (int)(ThresholdTypes.Otsu | ThresholdTypes.Triangle);

        thresholdType = ThresholdTypes.Binary;
        error = string.Empty;

        var explicitAutomatic = typeValue & automaticMask;
        if (explicitAutomatic == automaticMask)
        {
            error = "Threshold type cannot combine Otsu and Triangle.";
            return false;
        }

        if (useOtsu && explicitAutomatic == (int)ThresholdTypes.Triangle)
        {
            error = "UseOtsu cannot be combined with Triangle threshold type.";
            return false;
        }

        var baseType = typeValue & ~automaticMask;
        if (baseType is not 0
            and not (int)ThresholdTypes.BinaryInv
            and not (int)ThresholdTypes.Trunc
            and not (int)ThresholdTypes.Tozero
            and not (int)ThresholdTypes.TozeroInv)
        {
            error = $"Unsupported threshold type value: {typeValue}.";
            return false;
        }

        var automaticType = explicitAutomatic;
        if (useOtsu)
        {
            automaticType |= (int)ThresholdTypes.Otsu;
        }

        if (automaticType == automaticMask)
        {
            error = "Threshold type cannot combine Otsu and Triangle.";
            return false;
        }

        thresholdType = (ThresholdTypes)(baseType | automaticType);
        return true;
    }
}
