// ComparatorOperator.cs
// 比较算子
// 对数值或文本输入执行条件比较与分支判断
// 作者：蘅芜君
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "数值比较",
    Description = "比较两个数值的大小关系，输出布尔判定结果与差值",
    Category = "逻辑控制",
    IconName = "compare",
    Keywords = new[] { "比较", "判断", "大于", "小于", "等于", "超限", "阈值判定", "公差", "Compare", "Threshold", "GreaterThan", "LessThan" }
)]
[InputPort("ValueA", "数值 A", PortDataType.Float, IsRequired = true)]
[InputPort("ValueB", "数值 B", PortDataType.Float, IsRequired = false)]
[OutputPort("Result", "判定结果", PortDataType.Boolean)]
[OutputPort("Difference", "差值", PortDataType.Float)]
[OperatorParam("Condition", "比较条件", "enum", DefaultValue = "GreaterThan", Options = new[] { "GreaterThan|大于", "GreaterThanOrEqual|大于等于", "LessThan|小于", "LessThanOrEqual|小于等于", "Equal|等于", "NotEqual|不等于", "InRange|在范围内" })]
[OperatorParam("CompareValue", "默认比较值", "double", Description = "当 ValueB 未连线时使用此值", DefaultValue = 0.0)]
[OperatorParam("Tolerance", "容差", "double", Description = "等于/不等于判断的容差", DefaultValue = 0.0001, Min = 0.0)]
[OperatorParam("RangeMin", "范围下限", "double", Description = "InRange 模式的下限", DefaultValue = 0.0)]
[OperatorParam("RangeMax", "范围上限", "double", Description = "InRange 模式的上限", DefaultValue = 1.0)]
public class ComparatorOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.Comparator;

    public ComparatorOperator(ILogger<ComparatorOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(Operator @operator, Dictionary<string, object>? inputs, CancellationToken cancellationToken)
    {
        if (!TryReadRequired(inputs, "ValueA", out var a, out var aError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(aError));
        }

        var hasExplicitNonNullB = inputs != null &&
            inputs.TryGetValue("ValueB", out var rawB) &&
            rawB != null;
        var hasB = TryReadOptional(inputs, "ValueB", out var b, out var bError);
        if (hasExplicitNonNullB && !hasB)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(bError));
        }

        if (!hasB)
        {
            b = GetDoubleParam(@operator, "CompareValue", 0);
        }

        var condition = GetStringParam(@operator, "Condition", "GreaterThan");
        var tolerance = Math.Abs(GetDoubleParam(@operator, "Tolerance", 0.0001));
        var min = GetDoubleParam(@operator, "RangeMin", 0);
        var max = GetDoubleParam(@operator, "RangeMax", 1);
        var diff = a - b;

        var result = condition.ToLower() switch
        {
            "greaterthan" => a > b,
            "greaterthanorequal" => a >= b,
            "lessthan" => a < b,
            "lessthanorequal" => a <= b,
            "equal" => Math.Abs(diff) <= tolerance,
            "notequal" => Math.Abs(diff) > tolerance,
            "inrange" => a >= min && a <= max,
            _ => false
        };

        return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            ["Result"] = result,
            ["Difference"] = diff
        }));
    }

    private static bool TryReadRequired(Dictionary<string, object>? inputs, string key, out double value, out string error)
    {
        value = 0;
        error = string.Empty;

        if (inputs == null || !inputs.TryGetValue(key, out var obj) || obj == null)
        {
            error = $"{key} input is required.";
            return false;
        }

        if (!TryConvertToDouble(obj, out value))
        {
            error = $"{key} must be a numeric value.";
            return false;
        }

        return true;
    }

    private static bool TryReadOptional(Dictionary<string, object>? inputs, string key, out double value, out string error)
    {
        value = 0;
        error = string.Empty;

        if (inputs == null || !inputs.TryGetValue(key, out var obj) || obj == null)
        {
            return false;
        }

        if (!TryConvertToDouble(obj, out value))
        {
            error = $"{key} must be a numeric value.";
            return false;
        }

        return true;
    }

    private static bool TryConvertToDouble(object raw, out double value)
    {
        value = 0;
        return raw switch
        {
            double d => (value = d) == d,
            float f => (value = f) == f,
            int i => (value = i) == i,
            long l => (value = l) == l,
            decimal m => (value = (double)m) == (double)m,
            _ => double.TryParse(raw.ToString(), out value)
        };
    }

    public override ValidationResult ValidateParameters(Operator @operator) => ValidationResult.Valid();
}
