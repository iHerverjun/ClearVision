using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

public class CopyMakeBorderOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.CopyMakeBorder;

    public CopyMakeBorderOperator(ILogger<CopyMakeBorderOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is required"));
        }

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid"));
        }

        var top = GetIntParam(@operator, "Top", 0, 0, 10000);
        var bottom = GetIntParam(@operator, "Bottom", 0, 0, 10000);
        var left = GetIntParam(@operator, "Left", 0, 0, 10000);
        var right = GetIntParam(@operator, "Right", 0, 0, 10000);
        var borderType = ParseBorderType(GetStringParam(@operator, "BorderType", "Constant"));
        var color = ParseColor(GetStringParam(@operator, "Color", "#000000"));

        var result = new Mat();
        Cv2.CopyMakeBorder(src, result, top, bottom, left, right, borderType, color);

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(result)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var values = new[]
        {
            GetIntParam(@operator, "Top", 0),
            GetIntParam(@operator, "Bottom", 0),
            GetIntParam(@operator, "Left", 0),
            GetIntParam(@operator, "Right", 0)
        };

        if (values.Any(v => v < 0))
        {
            return ValidationResult.Invalid("Top/Bottom/Left/Right must be >= 0");
        }

        var borderType = GetStringParam(@operator, "BorderType", "Constant");
        var valid = new[] { "Constant", "Replicate", "Reflect", "Wrap" };
        if (!valid.Contains(borderType, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("BorderType must be Constant, Replicate, Reflect or Wrap");
        }

        return ValidationResult.Valid();
    }

    private static BorderTypes ParseBorderType(string text)
    {
        return text.ToLowerInvariant() switch
        {
            "replicate" => BorderTypes.Replicate,
            "reflect" => BorderTypes.Reflect,
            "wrap" => BorderTypes.Wrap,
            _ => BorderTypes.Constant
        };
    }

    private static Scalar ParseColor(string value)
    {
        var text = value?.Trim() ?? "#000000";
        if (text.StartsWith('#'))
        {
            text = text[1..];
        }

        if (text.Length != 6)
        {
            return Scalar.Black;
        }

        if (!byte.TryParse(text[0..2], System.Globalization.NumberStyles.HexNumber, null, out var r) ||
            !byte.TryParse(text[2..4], System.Globalization.NumberStyles.HexNumber, null, out var g) ||
            !byte.TryParse(text[4..6], System.Globalization.NumberStyles.HexNumber, null, out var b))
        {
            return Scalar.Black;
        }

        return new Scalar(b, g, r);
    }
}

