using System.Collections;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "点位修正",
    Description = "Computes translation/rotation correction from detected to reference point.",
    Category = "数据处理",
    IconName = "point-correction",
    Keywords = new[] { "correction", "compensation", "robot", "pick place" }
)]
[InputPort("DetectedPoint", "Detected Point", PortDataType.Point, IsRequired = true)]
[InputPort("DetectedAngle", "Detected Angle", PortDataType.Float, IsRequired = false)]
[InputPort("ReferencePoint", "Reference Point", PortDataType.Point, IsRequired = true)]
[InputPort("ReferenceAngle", "Reference Angle", PortDataType.Float, IsRequired = false)]
[OutputPort("CorrectionX", "Correction X", PortDataType.Float)]
[OutputPort("CorrectionY", "Correction Y", PortDataType.Float)]
[OutputPort("CorrectionAngle", "Correction Angle", PortDataType.Float)]
[OutputPort("TransformMatrix", "Transform Matrix", PortDataType.Any)]
[OperatorParam("CorrectionMode", "Correction Mode", "enum", DefaultValue = "TranslationOnly", Options = new[] { "TranslationOnly|TranslationOnly", "TranslationRotation|TranslationRotation" })]
[OperatorParam("OutputUnit", "Output Unit", "enum", DefaultValue = "Pixel", Options = new[] { "Pixel|Pixel", "mm|mm" })]
[OperatorParam("PixelSize", "Pixel Size", "double", DefaultValue = 1.0, Min = 1E-09, Max = 1000000.0)]
public class PointCorrectionOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.PointCorrection;

    public PointCorrectionOperator(ILogger<PointCorrectionOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("DetectedPoint and ReferencePoint are required"));
        }

        if (!inputs.TryGetValue("DetectedPoint", out var detectedObj) || !TryParsePoint(detectedObj, out var detectedPoint))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input 'DetectedPoint' is missing or invalid"));
        }

        if (!inputs.TryGetValue("ReferencePoint", out var referenceObj) || !TryParsePoint(referenceObj, out var referencePoint))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input 'ReferencePoint' is missing or invalid"));
        }

        var mode = GetStringParam(@operator, "CorrectionMode", "TranslationOnly");
        var outputUnit = GetStringParam(@operator, "OutputUnit", "Pixel");
        var pixelSize = GetDoubleParam(@operator, "PixelSize", 1.0, 1e-9, 1_000_000);

        var detectedAngle = ResolveInputOrParameter(inputs, @operator, "DetectedAngle", 0.0);
        var referenceAngle = ResolveInputOrParameter(inputs, @operator, "ReferenceAngle", 0.0);

        var correctionX = referencePoint.X - detectedPoint.X;
        var correctionY = referencePoint.Y - detectedPoint.Y;
        var correctionAngle = 0.0;

        var matrix = new double[2][]
        {
            [1.0, 0.0, correctionX],
            [0.0, 1.0, correctionY]
        };

        if (mode.Equals("TranslationRotation", StringComparison.OrdinalIgnoreCase))
        {
            correctionAngle = referenceAngle - detectedAngle;
            var rad = correctionAngle * Math.PI / 180.0;
            var cos = Math.Cos(rad);
            var sin = Math.Sin(rad);

            var tx = referencePoint.X - (cos * detectedPoint.X - sin * detectedPoint.Y);
            var ty = referencePoint.Y - (sin * detectedPoint.X + cos * detectedPoint.Y);

            correctionX = tx;
            correctionY = ty;
            matrix =
            [
                [cos, -sin, tx],
                [sin, cos, ty]
            ];
        }

        if (outputUnit.Equals("mm", StringComparison.OrdinalIgnoreCase))
        {
            correctionX *= pixelSize;
            correctionY *= pixelSize;
            matrix[0][2] *= pixelSize;
            matrix[1][2] *= pixelSize;
        }

        var output = new Dictionary<string, object>
        {
            { "CorrectionX", correctionX },
            { "CorrectionY", correctionY },
            { "CorrectionAngle", correctionAngle },
            { "TransformMatrix", matrix }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var mode = GetStringParam(@operator, "CorrectionMode", "TranslationOnly");
        var validModes = new[] { "TranslationOnly", "TranslationRotation" };
        if (!validModes.Contains(mode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("CorrectionMode must be TranslationOnly or TranslationRotation");
        }

        var unit = GetStringParam(@operator, "OutputUnit", "Pixel");
        var validUnits = new[] { "Pixel", "mm" };
        if (!validUnits.Contains(unit, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("OutputUnit must be Pixel or mm");
        }

        var pixelSize = GetDoubleParam(@operator, "PixelSize", 1.0);
        if (pixelSize <= 0)
        {
            return ValidationResult.Invalid("PixelSize must be greater than 0");
        }

        return ValidationResult.Valid();
    }

    private static double ResolveInputOrParameter(Dictionary<string, object>? inputs, Operator @operator, string key, double fallback)
    {
        if (inputs != null && inputs.TryGetValue(key, out var raw) && TryConvertToDouble(raw, out var value))
        {
            return value;
        }

        var paramValue = @operator.Parameters.FirstOrDefault(p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;
        return TryConvertToDouble(paramValue, out var converted) ? converted : fallback;
    }

    private static bool TryParsePoint(object? obj, out Position point)
    {
        point = new Position(0, 0);
        if (obj == null)
        {
            return false;
        }

        switch (obj)
        {
            case Position p:
                point = p;
                return true;
            case Point p:
                point = new Position(p.X, p.Y);
                return true;
            case Point2f p:
                point = new Position(p.X, p.Y);
                return true;
            case Point2d p:
                point = new Position(p.X, p.Y);
                return true;
        }

        if (obj is IDictionary<string, object> dict &&
            TryGetDouble(dict, "X", out var x) &&
            TryGetDouble(dict, "Y", out var y))
        {
            point = new Position(x, y);
            return true;
        }

        if (obj is IDictionary legacy)
        {
            var normalized = legacy.Cast<DictionaryEntry>()
                .Where(e => e.Key != null)
                .ToDictionary(e => e.Key!.ToString() ?? string.Empty, e => e.Value ?? 0.0, StringComparer.OrdinalIgnoreCase);
            return TryParsePoint(normalized, out point);
        }

        return false;
    }

    private static bool TryGetDouble(IDictionary<string, object> dict, string key, out double value)
    {
        value = 0;
        if (!dict.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        return TryConvertToDouble(raw, out value);
    }

    private static bool TryConvertToDouble(object? raw, out double value)
    {
        value = 0;
        if (raw == null)
        {
            return false;
        }

        return raw switch
        {
            double d => (value = d) == d,
            float f => (value = f) == f,
            int i => (value = i) == i,
            long l => (value = l) == l,
            _ => double.TryParse(raw.ToString(), out value)
        };
    }
}
