// PositionCorrectionOperator.cs
// 位置修正算子
// 基于偏差模型对目标位置进行校正
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
    DisplayName = "位置修正",
    Description = "Corrects downstream ROI coordinates using reference/base offsets.",
    Category = "定位",
    IconName = "position",
    Keywords = new[] { "position correction", "roi offset", "translation", "rotation" }
)]
[InputPort("ReferencePoint", "Reference Point", PortDataType.Point, IsRequired = true)]
[InputPort("BasePoint", "Base Point", PortDataType.Point, IsRequired = true)]
[InputPort("RoiX", "ROI X", PortDataType.Integer, IsRequired = false)]
[InputPort("RoiY", "ROI Y", PortDataType.Integer, IsRequired = false)]
[OutputPort("CorrectedX", "Corrected X", PortDataType.Integer)]
[OutputPort("CorrectedY", "Corrected Y", PortDataType.Integer)]
[OutputPort("OffsetX", "Offset X", PortDataType.Float)]
[OutputPort("OffsetY", "Offset Y", PortDataType.Float)]
[OutputPort("Angle", "Angle", PortDataType.Float)]
[OperatorParam("CorrectionMode", "Correction Mode", "enum", DefaultValue = "Translation", Options = new[] { "Translation|Translation", "TranslationRotation|TranslationRotation" })]
[OperatorParam("ReferenceAngle", "Reference Angle", "double", DefaultValue = 0.0, Min = -360.0, Max = 360.0)]
[OperatorParam("CurrentAngle", "Current Angle", "double", DefaultValue = 0.0, Min = -360.0, Max = 360.0)]
public class PositionCorrectionOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.PositionCorrection;

    public PositionCorrectionOperator(ILogger<PositionCorrectionOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("PositionCorrection requires ReferencePoint and BasePoint"));
        }

        if (!inputs.TryGetValue("ReferencePoint", out var referenceObj) || !TryParsePoint(referenceObj, out var referencePoint))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input 'ReferencePoint' is missing or invalid"));
        }

        if (!inputs.TryGetValue("BasePoint", out var baseObj) || !TryParsePoint(baseObj, out var basePoint))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input 'BasePoint' is missing or invalid"));
        }

        var correctionMode = GetStringParam(@operator, "CorrectionMode", "Translation");
        var referenceAngle = GetDoubleParam(@operator, "ReferenceAngle", 0.0, -360.0, 360.0);

        var roiX = GetInputOrParamDouble(inputs, @operator, "RoiX", 0.0);
        var roiY = GetInputOrParamDouble(inputs, @operator, "RoiY", 0.0);

        var offsetX = referencePoint.X - basePoint.X;
        var offsetY = referencePoint.Y - basePoint.Y;

        var correctedX = roiX + offsetX;
        var correctedY = roiY + offsetY;
        var angleDelta = 0.0;

        if (correctionMode.Equals("TranslationRotation", StringComparison.OrdinalIgnoreCase))
        {
            var currentAngle = GetInputOrParamDouble(inputs, @operator, "CurrentAngle", 0.0);
            if (inputs.TryGetValue("BaseAngle", out var baseAngleObj) && TryConvertToDouble(baseAngleObj, out var baseAngle))
            {
                currentAngle = baseAngle;
            }

            angleDelta = referenceAngle - currentAngle;

            var rad = angleDelta * Math.PI / 180.0;
            var cos = Math.Cos(rad);
            var sin = Math.Sin(rad);

            var localX = roiX - basePoint.X;
            var localY = roiY - basePoint.Y;

            var rotatedX = localX * cos - localY * sin;
            var rotatedY = localX * sin + localY * cos;

            correctedX = referencePoint.X + rotatedX;
            correctedY = referencePoint.Y + rotatedY;
        }

        var output = new Dictionary<string, object>
        {
            { "CorrectedX", (int)Math.Round(correctedX) },
            { "CorrectedY", (int)Math.Round(correctedY) },
            { "OffsetX", offsetX },
            { "OffsetY", offsetY },
            { "Angle", angleDelta }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var mode = GetStringParam(@operator, "CorrectionMode", "Translation");
        var validModes = new[] { "Translation", "TranslationRotation" };
        if (!validModes.Contains(mode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("CorrectionMode must be Translation or TranslationRotation");
        }

        return ValidationResult.Valid();
    }

    private static double GetInputOrParamDouble(Dictionary<string, object>? inputs, Operator @operator, string key, double fallback)
    {
        if (inputs != null && inputs.TryGetValue(key, out var raw) && TryConvertToDouble(raw, out var value))
        {
            return value;
        }

        return @operator.Parameters
            .FirstOrDefault(p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value is { } paramValue &&
            TryConvertToDouble(paramValue, out var paramDouble)
            ? paramDouble
            : fallback;
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
            case Point cvPoint:
                point = new Position(cvPoint.X, cvPoint.Y);
                return true;
            case Point2f cvPoint:
                point = new Position(cvPoint.X, cvPoint.Y);
                return true;
            case Point2d cvPoint:
                point = new Position(cvPoint.X, cvPoint.Y);
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

            if (TryGetDouble(normalized, "X", out var parsedX) &&
                TryGetDouble(normalized, "Y", out var parsedY))
            {
                point = new Position(parsedX, parsedY);
                return true;
            }
        }

        var text = obj.ToString();
        if (!string.IsNullOrWhiteSpace(text))
        {
            var stripped = text.Trim().Trim('(', ')', '[', ']');
            var parts = stripped.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 &&
                double.TryParse(parts[0], out var px) &&
                double.TryParse(parts[1], out var py))
            {
                point = new Position(px, py);
                return true;
            }
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

