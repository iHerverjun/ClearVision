// PointCorrectionOperator.cs
// 点位修正算子
// 根据参考点对输入坐标执行偏移修正
// 作者：蘅芜君
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
    Description = "Pixel-space rigid correction helper. Do not use it as a physical-world conversion substitute without calibration.",
    Category = "数据处理",
    IconName = "point-correction",
    Keywords = new[] { "correction", "compensation", "robot", "pick place" },
    Version = "1.0.3"
)]
[InputPort("DetectedPoint", "Detected Point", PortDataType.Point, IsRequired = true)]
[InputPort("DetectedAngle", "Detected Angle", PortDataType.Float, IsRequired = false)]
[InputPort("ReferencePoint", "Reference Point", PortDataType.Point, IsRequired = true)]
[InputPort("ReferenceAngle", "Reference Angle", PortDataType.Float, IsRequired = false)]
[OutputPort("CorrectionX", "Correction X", PortDataType.Float)]
[OutputPort("CorrectionY", "Correction Y", PortDataType.Float)]
[OutputPort("CorrectionAngle", "Correction Angle", PortDataType.Float)]
[OutputPort("TransformMatrix", "Transform Matrix", PortDataType.Any)]
[OutputPort("TransformUnit", "Transform Unit", PortDataType.String)]
[OperatorParam("CorrectionMode", "Correction Mode", "enum", DefaultValue = "TranslationOnly", Options = new[] { "TranslationOnly|TranslationOnly", "TranslationRotation|TranslationRotation" })]
[OperatorParam("OutputUnit", "Output Unit", "enum", DefaultValue = "Pixel", Options = new[] { "Pixel|Pixel", "mm|mm" })]
[OperatorParam("PixelSize", "Pixel Size", "double", DefaultValue = 1.0, Min = 1E-09, Max = 1000000.0)]
[OperatorParam("MaxAllowedDistance", "Max Allowed Distance", "double", DefaultValue = 0.0, Min = 0.0, Max = 1000000.0)]
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
        if (!mode.Equals("TranslationOnly", StringComparison.OrdinalIgnoreCase) &&
            !mode.Equals("TranslationRotation", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("CorrectionMode must be TranslationOnly or TranslationRotation"));
        }

        if (!outputUnit.Equals("Pixel", StringComparison.OrdinalIgnoreCase) &&
            !outputUnit.Equals("mm", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("OutputUnit must be Pixel or mm"));
        }

        if (!TryGetFiniteDoubleParameter(@operator, "PixelSize", 1.0, out var pixelSize))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("PixelSize must be a positive finite number"));
        }

        if (!double.IsFinite(pixelSize) || pixelSize <= 0)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("PixelSize must be a positive finite number"));
        }

        if (!TryGetFiniteDoubleParameter(@operator, "MaxAllowedDistance", 0.0, out var maxAllowedDistance))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("MaxAllowedDistance must be a finite number greater than or equal to 0"));
        }

        if (!double.IsFinite(maxAllowedDistance) || maxAllowedDistance < 0)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("MaxAllowedDistance must be a finite number greater than or equal to 0"));
        }

        if (!TryResolveFiniteInputOrParameter(inputs, @operator, "DetectedAngle", 0.0, out var detectedAngle, out var detectedAngleError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(detectedAngleError ?? "DetectedAngle must be a finite number"));
        }

        if (!TryResolveFiniteInputOrParameter(inputs, @operator, "ReferenceAngle", 0.0, out var referenceAngle, out var referenceAngleError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(referenceAngleError ?? "ReferenceAngle must be a finite number"));
        }

        var detectedToReferenceDistance = Math.Sqrt(
            (referencePoint.X - detectedPoint.X) * (referencePoint.X - detectedPoint.X) +
            (referencePoint.Y - detectedPoint.Y) * (referencePoint.Y - detectedPoint.Y));

        var distanceForThreshold = outputUnit.Equals("mm", StringComparison.OrdinalIgnoreCase)
            ? detectedToReferenceDistance * pixelSize
            : detectedToReferenceDistance;

        if (maxAllowedDistance > 0 && distanceForThreshold > maxAllowedDistance)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"DetectedPoint is too far from ReferencePoint. Distance={distanceForThreshold:F6}, MaxAllowedDistance={maxAllowedDistance:F6}"));
        }

        var correctionXPixel = referencePoint.X - detectedPoint.X;
        var correctionYPixel = referencePoint.Y - detectedPoint.Y;
        var correctionAngle = 0.0;

        var matrix = new double[2][]
        {
            [1.0, 0.0, correctionXPixel],
            [0.0, 1.0, correctionYPixel]
        };

        if (mode.Equals("TranslationRotation", StringComparison.OrdinalIgnoreCase))
        {
            correctionAngle = NormalizeAngle(referenceAngle - detectedAngle);
            var rad = correctionAngle * Math.PI / 180.0;
            var cos = Math.Cos(rad);
            var sin = Math.Sin(rad);

            var tx = referencePoint.X - (cos * detectedPoint.X - sin * detectedPoint.Y);
            var ty = referencePoint.Y - (sin * detectedPoint.X + cos * detectedPoint.Y);

            correctionXPixel = tx;
            correctionYPixel = ty;
            matrix =
            [
                [cos, -sin, tx],
                [sin, cos, ty]
            ];
        }

        var correctionX = correctionXPixel;
        var correctionY = correctionYPixel;
        if (outputUnit.Equals("mm", StringComparison.OrdinalIgnoreCase))
        {
            correctionX *= pixelSize;
            correctionY *= pixelSize;
        }

        var output = new Dictionary<string, object>
        {
            { "CorrectionX", correctionX },
            { "CorrectionY", correctionY },
            { "CorrectionAngle", correctionAngle },
            { "TransformMatrix", matrix },
            { "TransformUnit", "Pixel" }
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

        if (!TryGetFiniteDoubleParameter(@operator, "PixelSize", 1.0, out var pixelSize))
        {
            return ValidationResult.Invalid("PixelSize must be a positive finite number");
        }

        if (!double.IsFinite(pixelSize) || pixelSize <= 0)
        {
            return ValidationResult.Invalid("PixelSize must be a positive finite number");
        }

        if (!TryGetFiniteDoubleParameter(@operator, "MaxAllowedDistance", 0.0, out var maxAllowedDistance))
        {
            return ValidationResult.Invalid("MaxAllowedDistance must be a finite number greater than or equal to 0");
        }

        if (!double.IsFinite(maxAllowedDistance) || maxAllowedDistance < 0)
        {
            return ValidationResult.Invalid("MaxAllowedDistance must be a finite number greater than or equal to 0");
        }

        return ValidationResult.Valid();
    }

    private static bool TryResolveFiniteInputOrParameter(
        Dictionary<string, object>? inputs,
        Operator @operator,
        string key,
        double fallback,
        out double value,
        out string? error)
    {
        value = fallback;
        error = null;

        if (inputs != null && inputs.TryGetValue(key, out var inputRaw))
        {
            if (!TryConvertToFiniteDouble(inputRaw, out value))
            {
                error = $"{key} must be a finite number";
                return false;
            }

            return true;
        }

        var paramValue = @operator.Parameters.FirstOrDefault(p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;
        if (paramValue == null)
        {
            return true;
        }

        if (!TryConvertToFiniteDouble(paramValue, out value))
        {
            error = $"{key} must be a finite number";
            return false;
        }

        return true;
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
                return TryCreateFinitePoint(p.X, p.Y, out point);
            case Point p:
                return TryCreateFinitePoint(p.X, p.Y, out point);
            case Point2f p:
                return TryCreateFinitePoint(p.X, p.Y, out point);
            case Point2d p:
                return TryCreateFinitePoint(p.X, p.Y, out point);
        }

        if (obj is IDictionary<string, object> dict &&
            TryGetDouble(dict, "X", out var x) &&
            TryGetDouble(dict, "Y", out var y))
        {
            return TryCreateFinitePoint(x, y, out point);
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
        if (!TryGetCaseInsensitiveValue(dict, key, out var raw) || raw == null)
        {
            return false;
        }

        return TryConvertToFiniteDouble(raw, out value);
    }

    private static bool TryCreateFinitePoint(double x, double y, out Position point)
    {
        point = new Position(0, 0);
        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            return false;
        }

        point = new Position(x, y);
        return true;
    }

    private static bool TryConvertToFiniteDouble(object? raw, out double value)
    {
        value = 0;
        if (raw == null)
        {
            return false;
        }

        var converted = raw switch
        {
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            _ => double.TryParse(raw.ToString(), out var parsed) ? parsed : double.NaN
        };

        if (!double.IsFinite(converted))
        {
            return false;
        }

        value = converted;
        return true;
    }

    private static double NormalizeAngle(double angleDegrees)
    {
        var normalized = angleDegrees % 360.0;
        if (normalized >= 180.0)
        {
            normalized -= 360.0;
        }
        else if (normalized < -180.0)
        {
            normalized += 360.0;
        }

        return normalized;
    }

    private static bool TryGetFiniteDoubleParameter(Operator @operator, string name, double defaultValue, out double value)
    {
        value = defaultValue;

        var parameterValue = @operator.Parameters
            .FirstOrDefault(parameter => parameter.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?.Value;

        if (parameterValue == null)
        {
            return true;
        }

        return TryConvertToFiniteDouble(parameterValue, out value);
    }

    private static bool TryGetCaseInsensitiveValue(IDictionary<string, object> dict, string key, out object? value)
    {
        if (dict.TryGetValue(key, out value))
        {
            return true;
        }

        foreach (var pair in dict)
        {
            if (pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = null;
        return false;
    }
}

