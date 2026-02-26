// VariableReadOperator.cs
// 变量读取算子 - 从全局变量表读取值
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.Services;
using Microsoft.Extensions.Logging;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 变量读取算子 - 从全局变量表读取值
/// 【第三优先级】变量表/全局上下文功能
/// </summary>
[OperatorMeta(
    DisplayName = "变量读取",
    Description = "从全局变量表读取值",
    Category = "变量",
    IconName = "variable-read"
)]
[OutputPort("Value", "值", PortDataType.Any)]
[OutputPort("Exists", "是否存在", PortDataType.Boolean)]
[OutputPort("CycleCount", "循环计数", PortDataType.Integer)]
[OperatorParam("VariableName", "变量名", "string", Description = "要读取的变量名称", DefaultValue = "")]
[OperatorParam("DefaultValue", "默认值", "string", Description = "变量不存在时的默认值", DefaultValue = "0")]
[OperatorParam("DataType", "数据类型", "enum", DefaultValue = "String", Options = new[] { "String|字符串", "Int|整数", "Double|浮点数", "Bool|布尔值" })]
public class VariableReadOperator : OperatorBase
{
    private readonly IVariableContext _variableContext;
    
    public override OperatorType OperatorType => OperatorType.VariableRead;

    public VariableReadOperator(
        ILogger<VariableReadOperator> logger,
        IVariableContext variableContext) : base(logger)
    {
        _variableContext = variableContext;
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var variableName = GetStringParam(@operator, "VariableName", "");
        var defaultValue = GetStringParam(@operator, "DefaultValue", "0");
        var dataType = GetStringParam(@operator, "DataType", "String"); // String / Int / Double / Bool

        if (string.IsNullOrWhiteSpace(variableName))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("变量名不能为空"));
        }

        object value;
        bool exists = _variableContext.Contains(variableName);

        switch (dataType.ToLower())
        {
            case "int":
            case "integer":
                value = _variableContext.GetValue<long>(variableName, int.TryParse(defaultValue, out var intVal) ? intVal : 0L);
                break;
            case "double":
            case "float":
                value = _variableContext.GetValue<double>(variableName, double.TryParse(defaultValue, out var doubleVal) ? doubleVal : 0.0);
                break;
            case "bool":
            case "boolean":
                value = _variableContext.GetValue<bool>(variableName, bool.TryParse(defaultValue, out var boolVal) ? boolVal : false);
                break;
            default: // string
                value = _variableContext.GetValue<string>(variableName, defaultValue) ?? defaultValue;
                break;
        }

        Logger.LogInformation("[VariableRead] 读取变量 {VariableName} = {Value} (存在: {Exists})", 
            variableName, value, exists);

        return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            { "Value", value },
            { "VariableName", variableName },
            { "Exists", exists },
            { "CycleCount", _variableContext.CycleCount }
        }));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var variableName = GetStringParam(@operator, "VariableName", "");
        if (string.IsNullOrWhiteSpace(variableName))
            return ValidationResult.Invalid("变量名不能为空");

        var validTypes = new[] { "string", "int", "integer", "double", "float", "bool", "boolean" };
        var dataType = GetStringParam(@operator, "DataType", "String").ToLower();
        if (!validTypes.Contains(dataType))
            return ValidationResult.Invalid($"不支持的数据类型: {dataType}");

        return ValidationResult.Valid();
    }
}
