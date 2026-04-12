// PointAlignmentOperator.cs
// 点集对齐算子
// 计算源点集到目标点集的对齐变换
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
    DisplayName = "点位对齐",
    Description = "Pixel-space alignment helper for offsets and repeatability checks. Physical-world interpretation requires calibration.",
    Category = "数据处理",
    IconName = "align-point",
    Keywords = new[] { "alignment", "offset", "reference point", "distance" },
    Version = "1.0.3"
)]
[InputPort("CurrentPoint", "Current Point", PortDataType.Point, IsRequired = true)]
[InputPort("ReferencePoint", "Reference Point", PortDataType.Point, IsRequired = true)]
[OutputPort("OffsetX", "Offset X", PortDataType.Float)]
[OutputPort("OffsetY", "Offset Y", PortDataType.Float)]
[OutputPort("Distance", "Distance", PortDataType.Float)]
[OperatorParam("OutputUnit", "Output Unit", "enum", DefaultValue = "Pixel", Options = new[] { "Pixel|Pixel", "mm|mm" })]
[OperatorParam("PixelSize", "Pixel Size", "double", DefaultValue = 1.0, Min = 1E-09, Max = 1000000.0)]
public class PointAlignmentOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.PointAlignment;

    public PointAlignmentOperator(ILogger<PointAlignmentOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("CurrentPoint and ReferencePoint are required"));
        }

        if (!inputs.TryGetValue("CurrentPoint", out var currentObj) || !TryParsePoint(currentObj, out var currentPoint))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input 'CurrentPoint' is missing or invalid"));
        }

        if (!inputs.TryGetValue("ReferencePoint", out var referenceObj) || !TryParsePoint(referenceObj, out var referencePoint))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input 'ReferencePoint' is missing or invalid"));
        }

        var outputUnit = GetStringParam(@operator, "OutputUnit", "Pixel");
        if (outputUnit is not "Pixel" and not "mm" &&
            !outputUnit.Equals("Pixel", StringComparison.OrdinalIgnoreCase) &&
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

        var offsetX = currentPoint.X - referencePoint.X;
        var offsetY = currentPoint.Y - referencePoint.Y;
        var distance = Math.Sqrt(offsetX * offsetX + offsetY * offsetY);

        if (outputUnit.Equals("mm", StringComparison.OrdinalIgnoreCase))
        {
            offsetX *= pixelSize;
            offsetY *= pixelSize;
            distance *= pixelSize;
        }

        var output = new Dictionary<string, object>
        {
            { "OffsetX", offsetX },
            { "OffsetY", offsetY },
            { "Distance", distance }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var outputUnit = GetStringParam(@operator, "OutputUnit", "Pixel");
        var validUnits = new[] { "Pixel", "mm" };
        if (!validUnits.Contains(outputUnit, StringComparer.OrdinalIgnoreCase))
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

        return ValidationResult.Valid();
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

    private static bool TryConvertToFiniteDouble(object raw, out double value)
    {
        value = 0;
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

