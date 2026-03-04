// TypeConvertOperator.cs
// 类型转换算子
// 在常见数据类型之间执行安全转换
// 作者：蘅芜君
using System.Globalization;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Type Convert",
    Description = "Converts input data across String/Float/Integer/Boolean types.",
    Category = "General",
    IconName = "convert"
)]
[InputPort("Input", "Input", PortDataType.Any, IsRequired = true)]
[OutputPort("Output", "Output", PortDataType.Any)]
[OperatorParam("TargetType", "Target Type", "enum", DefaultValue = "String", Options = new[] { "String|String", "Float|Float", "Integer|Integer", "Boolean|Boolean" })]
[OperatorParam("Format", "Format", "string", DefaultValue = "")]
public class TypeConvertOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.TypeConvert;

    public TypeConvertOperator(ILogger<TypeConvertOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryReadInputValue(inputs, out var value))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("TypeConvert requires 'Input' value."));
        }

        var format = GetStringParam(@operator, "Format", "");
        var targetType = GetStringParam(@operator, "TargetType", "String");

        var asString = ConvertToString(value!, format);
        var asFloat = ConvertToFloat(value!);
        var asInteger = ConvertToInteger(value!);
        var asBoolean = ConvertToBoolean(value!);

        object outputValue = targetType.ToLowerInvariant() switch
        {
            "string" => asString,
            "float" => asFloat,
            "integer" => asInteger,
            "boolean" => asBoolean,
            _ => asString
        };

        Logger.LogDebug(
            "[TypeConvert] Source={SourceType}, Target={TargetType}, Output={Output}",
            value!.GetType().Name,
            targetType,
            outputValue);

        return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            { "Output", outputValue },
            { "Value", outputValue },
            { "AsString", asString },
            { "AsFloat", asFloat },
            { "AsInteger", asInteger },
            { "AsBoolean", asBoolean },
            { "OriginalType", value.GetType().Name }
        }));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var targetType = GetStringParam(@operator, "TargetType", "String");
        var validTargetTypes = new[] { "String", "Float", "Integer", "Boolean" };
        if (!validTargetTypes.Contains(targetType, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("TargetType must be String, Float, Integer or Boolean.");
        }

        return ValidationResult.Valid();
    }

    private static bool TryReadInputValue(Dictionary<string, object>? inputs, out object? value)
    {
        value = null;
        if (inputs == null)
        {
            return false;
        }

        // Primary key: Input (matches InputPort contract).
        if (inputs.TryGetValue("Input", out value) && value != null)
        {
            return true;
        }

        // Backward compatibility for old flows/tests that still use "Value".
        if (inputs.TryGetValue("Value", out value) && value != null)
        {
            return true;
        }

        return false;
    }

    private static string ConvertToString(object value, string format)
    {
        if (!string.IsNullOrEmpty(format) && value is IFormattable formattable)
        {
            try
            {
                return formattable.ToString(format, CultureInfo.InvariantCulture);
            }
            catch
            {
                // Fallback to default formatting.
            }
        }

        return value.ToString() ?? string.Empty;
    }

    private static float ConvertToFloat(object value)
    {
        return value switch
        {
            float f => f,
            double d => (float)d,
            int i => i,
            bool b => b ? 1f : 0f,
            _ => float.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0f
        };
    }

    private static int ConvertToInteger(object value)
    {
        return value switch
        {
            int i => i,
            float f => (int)f,
            double d => (int)d,
            bool b => b ? 1 : 0,
            _ => int.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0
        };
    }

    private static bool ConvertToBoolean(object value)
    {
        return value switch
        {
            bool b => b,
            int i => i != 0,
            float f => Math.Abs(f) > float.Epsilon,
            double d => Math.Abs(d) > double.Epsilon,
            _ => TryParseBoolean(value.ToString())
        };
    }

    private static bool TryParseBoolean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (bool.TryParse(normalized, out var parsedBool))
        {
            return parsedBool;
        }

        if (double.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedDouble))
        {
            return Math.Abs(parsedDouble) > double.Epsilon;
        }

        return true;
    }
}
