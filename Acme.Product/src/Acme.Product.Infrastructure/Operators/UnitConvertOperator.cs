// UnitConvertOperator.cs
// 单位转换算子
// 执行像素、毫米等测量单位之间转换
// 作者：蘅芜君
using System.Globalization;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "单位换算",
    Description = "Converts value between pixel, mm, um and inch.",
    Category = "数据处理",
    IconName = "unit",
    Keywords = new[] { "unit convert", "pixel to mm", "mm", "um", "inch" }
)]
[InputPort("Value", "Value", PortDataType.Float, IsRequired = true)]
[InputPort("PixelSize", "Pixel Size", PortDataType.Float, IsRequired = false)]
[OutputPort("Result", "Result", PortDataType.Float)]
[OutputPort("Unit", "Unit", PortDataType.String)]
[OperatorParam("FromUnit", "From Unit", "enum", DefaultValue = "Pixel", Options = new[] { "Pixel|Pixel", "mm|mm", "um|um", "inch|inch" })]
[OperatorParam("ToUnit", "To Unit", "enum", DefaultValue = "mm", Options = new[] { "Pixel|Pixel", "mm|mm", "um|um", "inch|inch" })]
[OperatorParam("Scale", "Scale", "double", DefaultValue = 1.0, Min = 1E-09, Max = 1000000.0)]
[OperatorParam("UseCalibration", "Use Calibration", "bool", DefaultValue = false)]
public class UnitConvertOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.UnitConvert;

    public UnitConvertOperator(ILogger<UnitConvertOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputDouble(inputs, "Value", out var value))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input 'Value' is required and must be a valid number."));
        }

        if (!double.IsFinite(value))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input 'Value' must be a finite number."));
        }

        var fromUnit = NormalizeUnit(GetStringParam(@operator, "FromUnit", "Pixel"));
        var toUnit = NormalizeUnit(GetStringParam(@operator, "ToUnit", "mm"));
        if (!TryGetFiniteDoubleParameter(@operator, "Scale", 1.0, out var scale))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Scale must be a finite number greater than 0."));
        }

        var useCalibration = GetBoolParam(@operator, "UseCalibration", false);
        var pixelSize = 0.0;

        if (!IsSupportedUnit(fromUnit) || !IsSupportedUnit(toUnit))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Unsupported unit. Supported: Pixel/mm/um/inch"));
        }

        if (!IsPositiveFinite(scale))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Scale must be a finite number greater than 0."));
        }

        if (useCalibration)
        {
            if (!TryGetInputDouble(inputs, "PixelSize", out pixelSize) || !IsPositiveFinite(pixelSize))
            {
                return Task.FromResult(OperatorExecutionOutput.Failure("UseCalibration=true requires finite 'PixelSize' input greater than 0."));
            }
        }

        var requiresPixelSize = fromUnit == "pixel" || toUnit == "pixel";
        if (requiresPixelSize)
        {
            if (!useCalibration)
            {
                pixelSize = scale;
            }

            if (!IsPositiveFinite(pixelSize))
            {
                return Task.FromResult(OperatorExecutionOutput.Failure("Pixel size must be a finite number greater than 0."));
            }
        }

        var mmValue = ConvertToMillimeter(value, fromUnit, pixelSize);
        var result = ConvertFromMillimeter(mmValue, toUnit, pixelSize);

        if (!double.IsFinite(mmValue) || !double.IsFinite(result))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Conversion result must be finite."));
        }

        var output = new Dictionary<string, object>
        {
            { "Result", result },
            { "Unit", ToDisplayUnit(toUnit) }
        };

        if (requiresPixelSize)
        {
            output["UsedPixelSize"] = pixelSize;
        }

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var fromUnit = NormalizeUnit(GetStringParam(@operator, "FromUnit", "Pixel"));
        var toUnit = NormalizeUnit(GetStringParam(@operator, "ToUnit", "mm"));

        if (!IsSupportedUnit(fromUnit) || !IsSupportedUnit(toUnit))
        {
            return ValidationResult.Invalid("FromUnit/ToUnit must be Pixel/mm/um/inch");
        }

        if (!TryGetFiniteDoubleParameter(@operator, "Scale", 1.0, out var scale))
        {
            return ValidationResult.Invalid("Scale must be a finite number greater than 0.");
        }

        if (!IsPositiveFinite(scale))
        {
            return ValidationResult.Invalid("Scale must be a finite number greater than 0.");
        }

        return ValidationResult.Valid();
    }

    private static bool TryGetInputDouble(Dictionary<string, object>? inputs, string key, out double value)
    {
        value = 0;
        if (inputs == null || !inputs.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        return raw switch
        {
            double d => (value = d) == d || double.IsNaN(d),
            float f => (value = f) == f || float.IsNaN(f),
            decimal m => (value = (double)m) == (double)m,
            int i => (value = i) == i,
            uint ui => (value = ui) == ui,
            short s => (value = s) == s,
            ushort us => (value = us) == us,
            long l => (value = l) == l,
            ulong ul => (value = ul) == ul,
            _ => double.TryParse(raw.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value)
        };
    }

    private static bool IsPositiveFinite(double value)
    {
        return double.IsFinite(value) && value > 0;
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

        return TryGetInputDouble(new Dictionary<string, object> { [name] = parameterValue }, name, out value);
    }

    private static string NormalizeUnit(string unit)
    {
        var normalized = unit.Trim().ToLowerInvariant();
        return normalized switch
        {
            "px" => "pixel",
            "μm" => "um",
            _ => normalized
        };
    }

    private static bool IsSupportedUnit(string unit)
    {
        return unit is "pixel" or "mm" or "um" or "inch";
    }

    private static double ConvertToMillimeter(double value, string fromUnit, double pixelSize)
    {
        return fromUnit switch
        {
            "pixel" => value * pixelSize,
            "mm" => value,
            "um" => value / 1000.0,
            "inch" => value * 25.4,
            _ => value
        };
    }

    private static double ConvertFromMillimeter(double mmValue, string toUnit, double pixelSize)
    {
        return toUnit switch
        {
            "pixel" => mmValue / pixelSize,
            "mm" => mmValue,
            "um" => mmValue * 1000.0,
            "inch" => mmValue / 25.4,
            _ => mmValue
        };
    }

    private static string ToDisplayUnit(string unit)
    {
        return unit switch
        {
            "pixel" => "px",
            _ => unit
        };
    }
}

