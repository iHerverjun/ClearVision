using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.Services;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 变量写入算子 - 写入值到全局变量表
/// 【第三优先级】变量表/全局上下文功能
/// </summary>
public class VariableWriteOperator : OperatorBase
{
    private readonly IVariableContext _variableContext;
    
    public override OperatorType OperatorType => OperatorType.VariableWrite;

    public VariableWriteOperator(
        ILogger<VariableWriteOperator> logger,
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
        var dataType = GetStringParam(@operator, "DataType", "String");
        var useInputValue = GetBoolParam(@operator, "UseInputValue", true);

        if (string.IsNullOrWhiteSpace(variableName))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("变量名不能为空"));
        }

        object value;

        if (useInputValue && inputs != null)
        {
            // 优先使用上游输入
            if (inputs.TryGetValue("Value", out var inputValue))
            {
                value = inputValue;
            }
            else if (inputs.TryGetValue(variableName, out var namedValue))
            {
                value = namedValue;
            }
            else
            {
                // 使用参数面板中的静态值
                value = GetStaticValue(@operator, dataType);
            }
        }
        else
        {
            // 使用参数面板中的静态值
            value = GetStaticValue(@operator, dataType);
        }

        // 写入变量
        switch (dataType.ToLower())
        {
            case "int":
            case "integer":
                _variableContext.SetValue(variableName, Convert.ToInt64(value));
                break;
            case "double":
            case "float":
                _variableContext.SetValue(variableName, Convert.ToDouble(value));
                break;
            case "bool":
            case "boolean":
                _variableContext.SetValue(variableName, Convert.ToBoolean(value));
                break;
            default:
                _variableContext.SetValue(variableName, value?.ToString() ?? "");
                break;
        }

        Logger.LogInformation("[VariableWrite] 写入变量 {VariableName} = {Value}", variableName, value);

        return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            { "VariableName", variableName },
            { "Value", value },
            { "CycleCount", _variableContext.CycleCount }
        }));
    }

    private object GetStaticValue(Operator @operator, string dataType)
    {
        var staticValue = GetStringParam(@operator, "StaticValue", "");

        switch (dataType.ToLower())
        {
            case "int":
            case "integer":
                return long.TryParse(staticValue, out var intVal) ? intVal : 0L;
            case "double":
            case "float":
                return double.TryParse(staticValue, out var doubleVal) ? doubleVal : 0.0;
            case "bool":
            case "boolean":
                return bool.TryParse(staticValue, out var boolVal) ? boolVal : false;
            default:
                return staticValue;
        }
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
