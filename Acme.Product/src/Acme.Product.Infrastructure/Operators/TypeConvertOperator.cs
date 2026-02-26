// TypeConvertOperator.cs
// 类型转换算子 - Sprint 3 Task 3.3
// 支持：String/Float/Integer/Boolean 互转
// 作者：蘅芜君

using System.Globalization;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 类型转换算子
/// 
/// 功能：
/// - 输入 Value(Any)
/// - 输出：AsString, AsFloat, AsInteger, AsBoolean
/// - 参数 Format：转字符串的格式，如 "F2" 保留两位小数
/// 
/// 使用场景：
/// - 数值转字符串用于显示
/// - 字符串解析为数值用于计算
/// - 类型统一转换
/// </summary>
[OperatorMeta(
    DisplayName = "类型转换",
    Description = "在不同数据类型间进行强制转换",
    Category = "通用",
    IconName = "convert"
)]
[InputPort("Input", "输入", PortDataType.Any, IsRequired = true)]
[OutputPort("Output", "输出", PortDataType.Any)]
[OperatorParam("TargetType", "目标类型", "enum", DefaultValue = "String", Options = new[] { "String|String", "Float|Float", "Integer|Integer", "Boolean|Boolean" })]
[OperatorParam("Format", "格式字符串", "string", DefaultValue = "")]
public class TypeConvertOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.TypeConvert;

    public TypeConvertOperator(ILogger<TypeConvertOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null || !inputs.TryGetValue("Value", out var value) || value == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("TypeConvert 算子需要 Value 输入"));
        }

        // 获取参数
        var format = GetStringParam(@operator, "Format", "");

        // 执行各种类型转换
        var asString = ConvertToString(value, format);
        var asFloat = ConvertToFloat(value);
        var asInteger = ConvertToInteger(value);
        var asBoolean = ConvertToBoolean(value);

        Logger.LogDebug("[TypeConvert] {Value} -> String:{String}, Float:{Float}, Int:{Int}, Bool:{Bool}",
            value, asString, asFloat, asInteger, asBoolean);

        return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            { "Value", value },
            { "AsString", asString },
            { "AsFloat", asFloat },
            { "AsInteger", asInteger },
            { "AsBoolean", asBoolean },
            { "OriginalType", value.GetType().Name }
        }));
    }

    /// <summary>
    /// 转换为字符串
    /// </summary>
    private static string ConvertToString(object value, string format)
    {
        if (value == null) return string.Empty;

        if (!string.IsNullOrEmpty(format) && value is IFormattable formattable)
        {
            try
            {
                return formattable.ToString(format, CultureInfo.InvariantCulture);
            }
            catch
            {
                // 格式无效，回退到默认
            }
        }

        return value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 转换为浮点数
    /// </summary>
    private static float ConvertToFloat(object value)
    {
        if (value == null) return 0f;

        if (value is float f) return f;
        if (value is double d) return (float)d;
        if (value is int i) return i;
        if (value is bool b) return b ? 1f : 0f;

        if (float.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return 0f;
    }

    /// <summary>
    /// 转换为整数
    /// </summary>
    private static int ConvertToInteger(object value)
    {
        if (value == null) return 0;

        if (value is int i) return i;
        if (value is float f) return (int)f;
        if (value is double d) return (int)d;
        if (value is bool b) return b ? 1 : 0;

        if (int.TryParse(value.ToString(), out var result))
        {
            return result;
        }

        return 0;
    }

    /// <summary>
    /// 转换为布尔值
    /// </summary>
    private static bool ConvertToBoolean(object value)
    {
        if (value == null) return false;

        if (value is bool b) return b;
        if (value is int i) return i != 0;
        if (value is float f) return f != 0;
        if (value is double d) return d != 0;

        var str = value.ToString()?.ToLowerInvariant();
        if (bool.TryParse(str, out var result))
        {
            return result;
        }

        // 字符串非空为 true
        return !string.IsNullOrWhiteSpace(str);
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        // TypeConvert 算子参数验证较宽松，大部分参数有默认值
        return ValidationResult.Valid();
    }
}
