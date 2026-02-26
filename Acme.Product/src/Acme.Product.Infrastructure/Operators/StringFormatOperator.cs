// StringFormatOperator.cs
// 字符串格式化算子 - Sprint 3 Task 3.6a
// 支持模板替换和字符串拼接
// 作者：蘅芜君

using System.Text;
using System.Text.RegularExpressions;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 字符串格式化算子
/// 
/// 功能：
/// - 模板替换：{0}, {1}, {Name}, {Value}
/// - 字符串拼接
/// - 日期格式化
/// 
/// 使用场景：
/// - 报告生成
/// - 日志拼装
/// - 文件名生成
/// </summary>
[OperatorMeta(
    DisplayName = "字符串格式化",
    Description = "按模板生成字符串",
    Category = "通用",
    IconName = "text"
)]
[InputPort("Arg1", "参数 1", PortDataType.Any, IsRequired = false)]
[InputPort("Arg2", "参数 2", PortDataType.Any, IsRequired = false)]
[OutputPort("Result", "结果", PortDataType.String)]
[OperatorParam("Template", "模板", "string", DefaultValue = "Result is {0} and {1}")]
public class StringFormatOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.StringFormat;

    public StringFormatOperator(ILogger<StringFormatOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("StringFormat 算子需要输入数据"));
        }

        // 获取参数
        var template = GetStringParam(@operator, "Template", "");
        var separator = GetStringParam(@operator, "Separator", "");
        var mode = GetStringParam(@operator, "Mode", "Template"); // Template, Join, Date

        string result;

        switch (mode.ToLower())
        {
            case "template":
                result = FormatTemplate(template, inputs);
                break;

            case "join":
                result = string.Join(separator, inputs.Values.Select(v => v?.ToString() ?? ""));
                break;

            case "date":
                var format = GetStringParam(@operator, "DateFormat", "yyyy-MM-dd HH:mm:ss");
                result = DateTime.Now.ToString(format);
                break;

            default:
                return Task.FromResult(OperatorExecutionOutput.Failure($"不支持的模式: {mode}"));
        }

        Logger.LogDebug("[StringFormat] 模式={Mode}, 结果={Result}", mode, result);

        return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            { "Result", result },
            { "Length", result.Length },
            { "IsEmpty", string.IsNullOrEmpty(result) }
        }));
    }

    /// <summary>
    /// 模板替换
    /// 支持 {0}, {1}, ... 和 {KeyName}
    /// </summary>
    private string FormatTemplate(string template, Dictionary<string, object> inputs)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Join("", inputs.Values);
        }

        var result = template;

        // 替换 {KeyName}
        var keyPattern = @"\{(\w+)\}";
        result = Regex.Replace(result, keyPattern, match =>
        {
            var key = match.Groups[1].Value;
            if (inputs.TryGetValue(key, out var value) && value != null)
            {
                return value.ToString() ?? "";
            }
            return match.Value; // 保留原样
        });

        // 替换 {0}, {1}, ...（按输入顺序）
        var index = 0;
        foreach (var value in inputs.Values)
        {
            var placeholder = $"{{{index}}}";
            if (result.Contains(placeholder))
            {
                result = result.Replace(placeholder, value?.ToString() ?? "");
            }
            index++;
        }

        return result;
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var mode = GetStringParam(@operator, "Mode", "Template");

        var validModes = new[] { "Template", "Join", "Date" };
        if (!validModes.Contains(mode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid($"Mode 必须是以下之一: {string.Join(", ", validModes)}");
        }

        return ValidationResult.Valid();
    }
}
