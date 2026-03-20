// AiFlowValidator.cs
// AI 流程校验器
// 对 AI 生成流程进行结构与规则校验
// 作者：蘅芜君
using Acme.Product.Core.DTOs;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using System.Globalization;

namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// 校验 AI 生成的工作流是否满足所有约束
/// </summary>
public class AiFlowValidator : IAiFlowValidator
{
    private readonly IOperatorFactory _operatorFactory;

    public AiFlowValidator(IOperatorFactory operatorFactory)
    {
        _operatorFactory = operatorFactory;
    }

    public AiValidationResult Validate(AiGeneratedFlowJson generatedFlow)
    {
        var result = new AiValidationResult();

        if (generatedFlow.Operators == null || generatedFlow.Operators.Count == 0)
        {
            result.AddError(
                "AI 未生成任何算子",
                code: "empty_workflow",
                category: "structure",
                relatedFields: ["operators"],
                repairHint: "请至少生成一个有效算子，并补齐 operators 数组。");
            return result;
        }

        // 建立 tempId → 算子元数据 的映射，用于后续校验
        var operatorMetaMap = new Dictionary<string, OperatorMetadata>();

        // 1. 校验算子类型合法性
        ValidateOperatorTypes(generatedFlow, result, operatorMetaMap);

        // 如果算子类型校验失败，后续校验意义不大
        if (!result.IsValid)
            return result;

        // 2. 校验端口名合法性和类型兼容性
        ValidateConnections(generatedFlow, result, operatorMetaMap);

        // 3. 校验无环路
        ValidateNoCycles(generatedFlow, result);

        // 4. 校验输入端口不重复占用
        ValidateNoDuplicateInputs(generatedFlow, result);

        // 5. 校验参数合法性（数值范围、枚举类型）
        ValidateParameters(generatedFlow, result, operatorMetaMap);

        // 6. 警告（不阻止生成，但记录）
        ValidateHasSourceAndOutput(generatedFlow, result, operatorMetaMap);

        return result;
    }

    private void ValidateOperatorTypes(
        AiGeneratedFlowJson flow,
        AiValidationResult result,
        Dictionary<string, OperatorMetadata> metaMap)
    {
        var allTempIds = new HashSet<string>();

        for (var index = 0; index < flow.Operators.Count; index++)
        {
            var op = flow.Operators[index];
            var operatorField = $"operators[{index}]";

            // 检查 tempId 格式
            if (string.IsNullOrWhiteSpace(op.TempId))
            {
                result.AddError(
                    "存在算子的 tempId 为空",
                    code: "missing_temp_id",
                    category: "structure",
                    relatedFields: [$"{operatorField}.tempId"],
                    repairHint: "为每个算子补充唯一 tempId，例如 op_1、op_2。");
                continue;
            }

            if (allTempIds.Contains(op.TempId))
            {
                result.AddError(
                    $"tempId 重复：{op.TempId}",
                    code: "duplicate_temp_id",
                    category: "structure",
                    relatedFields: [$"{operatorField}.tempId"],
                    operatorId: op.TempId,
                    repairHint: "确保每个算子都使用唯一 tempId。");
                continue;
            }
            allTempIds.Add(op.TempId);

            // 检查算子类型是否在枚举中
            if (!Enum.TryParse<OperatorType>(op.OperatorType, out var operatorType))
            {
                result.AddError(
                    $"算子类型不存在：{op.OperatorType}（tempId={op.TempId}）。请使用算子目录中的 operator_id 值。",
                    code: "unknown_operator_type",
                    category: "operator",
                    relatedFields: [$"{operatorField}.operatorType"],
                    operatorId: op.TempId,
                    repairHint: "把 operatorType 改成已注册的 OperatorType 枚举名。");
                continue;
            }

            // 检查算子元数据是否已注册
            var metadata = _operatorFactory.GetMetadata(operatorType);
            if (metadata == null)
            {
                result.AddError(
                    $"算子 {op.OperatorType} 未在算子工厂中注册",
                    code: "operator_not_registered",
                    category: "operator",
                    relatedFields: [$"{operatorField}.operatorType"],
                    operatorId: op.TempId,
                    repairHint: "请改用已经注册的算子类型，或移除该无效算子。");
                continue;
            }

            metaMap[op.TempId] = metadata;
        }
    }

    private void ValidateConnections(
        AiGeneratedFlowJson flow,
        AiValidationResult result,
        Dictionary<string, OperatorMetadata> metaMap)
    {
        if (flow.Connections == null)
            return;

        for (var index = 0; index < flow.Connections.Count; index++)
        {
            var conn = flow.Connections[index];
            var connectionField = $"connections[{index}]";

            // 检查源算子存在
            if (!metaMap.TryGetValue(conn.SourceTempId, out var sourceMeta))
            {
                result.AddError(
                    $"连线引用了不存在的源算子 tempId：{conn.SourceTempId}",
                    code: "missing_source_operator",
                    category: "connection",
                    relatedFields:
                    [
                        $"{connectionField}.sourceTempId"
                    ],
                    sourceTempId: conn.SourceTempId,
                    repairHint: "修正 sourceTempId，确保它引用已定义的算子 tempId。");
                continue;
            }

            // 检查目标算子存在
            if (!metaMap.TryGetValue(conn.TargetTempId, out var targetMeta))
            {
                result.AddError(
                    $"连线引用了不存在的目标算子 tempId：{conn.TargetTempId}",
                    code: "missing_target_operator",
                    category: "connection",
                    relatedFields:
                    [
                        $"{connectionField}.targetTempId"
                    ],
                    targetTempId: conn.TargetTempId,
                    repairHint: "修正 targetTempId，确保它引用已定义的算子 tempId。");
                continue;
            }

            // 检查源端口存在
            var sourcePort = sourceMeta.OutputPorts.FirstOrDefault(p => p.Name == conn.SourcePortName);
            if (sourcePort == null)
            {
                result.AddError(
                    $"算子 {conn.SourceTempId}({sourceMeta.DisplayName}) 没有名为 '{conn.SourcePortName}' 的输出端口。" +
                    $"可用输出端口：{string.Join(", ", sourceMeta.OutputPorts.Select(p => p.Name))}",
                    code: "missing_output_port",
                    category: "connection",
                    relatedFields:
                    [
                        $"{connectionField}.sourceTempId",
                        $"{connectionField}.sourcePortName"
                    ],
                    sourceTempId: conn.SourceTempId,
                    sourcePortName: conn.SourcePortName,
                    repairHint: "把 sourcePortName 改成该源算子的有效输出端口名。");
                continue;
            }

            // 检查目标端口存在
            var targetPort = targetMeta.InputPorts.FirstOrDefault(p => p.Name == conn.TargetPortName);
            if (targetPort == null)
            {
                result.AddError(
                    $"算子 {conn.TargetTempId}({targetMeta.DisplayName}) 没有名为 '{conn.TargetPortName}' 的输入端口。" +
                    $"可用输入端口：{string.Join(", ", targetMeta.InputPorts.Select(p => p.Name))}",
                    code: "missing_input_port",
                    category: "connection",
                    relatedFields:
                    [
                        $"{connectionField}.targetTempId",
                        $"{connectionField}.targetPortName"
                    ],
                    targetTempId: conn.TargetTempId,
                    targetPortName: conn.TargetPortName,
                    repairHint: "把 targetPortName 改成该目标算子的有效输入端口名。");
                continue;
            }

            // 检查类型兼容性
            if (!AreTypesCompatible(sourcePort.DataType, targetPort.DataType))
            {
                result.AddError(
                    $"端口类型不兼容：{conn.SourceTempId}.{conn.SourcePortName}({sourcePort.DataType}) → " +
                    $"{conn.TargetTempId}.{conn.TargetPortName}({targetPort.DataType})",
                    code: "incompatible_port_type",
                    category: "connection",
                    relatedFields:
                    [
                        $"{connectionField}.sourcePortName",
                        $"{connectionField}.targetPortName"
                    ],
                    sourceTempId: conn.SourceTempId,
                    sourcePortName: conn.SourcePortName,
                    targetTempId: conn.TargetTempId,
                    targetPortName: conn.TargetPortName,
                    repairHint: "请改用类型兼容的端口连线，或补充中间转换算子。");
            }
        }
    }

    private bool AreTypesCompatible(PortDataType source, PortDataType target)
    {
        // Any 类型与任何类型兼容
        if (source == PortDataType.Any || target == PortDataType.Any)
            return true;
        // 相同类型兼容
        if (source == target)
            return true;

        // 数值类型互通（Integer ↔ Float）
        var numericTypes = new[] { PortDataType.Integer, PortDataType.Float };
        if (numericTypes.Contains(source) && numericTypes.Contains(target))
            return true;

        // 几何类型互通（Point ↔ Rectangle）
        var geometryTypes = new[] { PortDataType.Point, PortDataType.Rectangle };
        if (geometryTypes.Contains(source) && geometryTypes.Contains(target))
            return true;

        // String 可以作为数值类型的输入（运行时转换）
        if (source == PortDataType.String && numericTypes.Contains(target))
            return true;

        // Boolean 可以作为 Integer 的输入（true=1, false=0）
        if (source == PortDataType.Boolean && target == PortDataType.Integer)
            return true;

        return false;
    }

    private void ValidateNoCycles(AiGeneratedFlowJson flow, AiValidationResult result)
    {
        if (flow.Connections == null || flow.Connections.Count == 0)
            return;

        // 构建邻接表
        var adjacency = new Dictionary<string, List<string>>();
        foreach (var op in flow.Operators)
            adjacency[op.TempId] = new List<string>();

        foreach (var conn in flow.Connections)
        {
            if (adjacency.ContainsKey(conn.SourceTempId))
                adjacency[conn.SourceTempId].Add(conn.TargetTempId);
        }

        // DFS 检测环路
        var visited = new HashSet<string>();
        var inStack = new HashSet<string>();

        foreach (var node in adjacency.Keys)
        {
            if (HasCycle(node, adjacency, visited, inStack))
            {
                result.AddError(
                    "工作流中存在环路（循环依赖），请重新设计流程结构",
                    code: "cycle_detected",
                    category: "graph",
                    relatedFields: ["connections"],
                    repairHint: "移除形成回路的连线，保持流程为 DAG。");
                return;
            }
        }
    }

    private bool HasCycle(
        string node,
        Dictionary<string, List<string>> adjacency,
        HashSet<string> visited,
        HashSet<string> inStack)
    {
        if (inStack.Contains(node))
            return true;
        if (visited.Contains(node))
            return false;

        visited.Add(node);
        inStack.Add(node);

        foreach (var neighbor in adjacency.GetValueOrDefault(node, new List<string>()))
        {
            if (HasCycle(neighbor, adjacency, visited, inStack))
                return true;
        }

        inStack.Remove(node);
        return false;
    }

    private void ValidateNoDuplicateInputs(AiGeneratedFlowJson flow, AiValidationResult result)
    {
        if (flow.Connections == null)
            return;

        var inputPortUsage = new HashSet<string>();
        for (var index = 0; index < flow.Connections.Count; index++)
        {
            var conn = flow.Connections[index];
            var key = $"{conn.TargetTempId}:{conn.TargetPortName}";
            if (!inputPortUsage.Add(key))
            {
                result.AddError(
                    $"输入端口被重复连接：算子 {conn.TargetTempId} 的 {conn.TargetPortName} 端口只能接收一条连线",
                    code: "duplicate_input_connection",
                    category: "connection",
                    relatedFields:
                    [
                        $"connections[{index}].targetTempId",
                        $"connections[{index}].targetPortName"
                    ],
                    targetTempId: conn.TargetTempId,
                    targetPortName: conn.TargetPortName,
                    repairHint: "删除重复连线，确保每个输入端口最多接收一条连接。");
            }
        }
    }

    private void ValidateParameters(
        AiGeneratedFlowJson flow,
        AiValidationResult result,
        Dictionary<string, OperatorMetadata> metaMap)
    {
        for (var index = 0; index < flow.Operators.Count; index++)
        {
            var op = flow.Operators[index];
            var operatorField = $"operators[{index}]";
            if (!metaMap.TryGetValue(op.TempId, out var metadata))
                continue;

            op.Parameters ??= new Dictionary<string, string>();
            ApplyIntelligentDefaults(op, metadata, result, operatorField);

            foreach (var requiredParam in metadata.Parameters.Where(p => p.IsRequired))
            {
                if (!op.Parameters.ContainsKey(requiredParam.Name))
                {
                    result.AddWarning(
                        $"算子 {op.TempId}({metadata.DisplayName}) 缺少必填参数 '{requiredParam.Name}'，且无可用默认值",
                        code: "missing_required_parameter",
                        category: "parameter",
                        relatedFields: [$"{operatorField}.parameters.{requiredParam.Name}"],
                        operatorId: op.TempId,
                        parameterName: requiredParam.Name,
                        repairHint: $"请为算子 {op.TempId} 补齐参数 {requiredParam.Name}。");
                }
            }

            foreach (var kvp in op.Parameters.ToList())
            {
                var paramName = kvp.Key;
                var paramValueStr = kvp.Value?.ToString() ?? string.Empty;

                var paramDef = metadata.Parameters.FirstOrDefault(p => p.Name == paramName);
                if (paramDef == null)
                {
                    // 参数不存在，仅作为警告
                    result.AddWarning(
                        $"算子 {op.TempId}({metadata.DisplayName}) 生成了未知的参数 '{paramName}'",
                        code: "unknown_parameter",
                        category: "parameter",
                        relatedFields: [$"{operatorField}.parameters.{paramName}"],
                        operatorId: op.TempId,
                        parameterName: paramName,
                        repairHint: "请移除未知参数，或改成该算子定义中存在的参数名。");
                    continue;
                }

                // 数值范围校验 + 自动 Clamp
                if (TryParseDouble(paramValueStr, out var numValue))
                {
                    var hasMin = TryParseDouble(paramDef.MinValue, out var minValue);
                    var hasMax = TryParseDouble(paramDef.MaxValue, out var maxValue);

                    var clamped = numValue;
                    if (hasMin && clamped < minValue)
                        clamped = minValue;
                    if (hasMax && clamped > maxValue)
                        clamped = maxValue;

                    if (Math.Abs(clamped - numValue) > double.Epsilon)
                    {
                        var clampedValue = FormatNumericValue(clamped, paramDef.DataType);
                        op.Parameters[paramName] = clampedValue;
                        result.AddWarning(
                            $"算子 {op.TempId}({metadata.DisplayName}) 的参数 '{paramName}' 值 {numValue} 超出范围，已自动调整为 {clampedValue}",
                            code: "parameter_clamped",
                            category: "parameter",
                            relatedFields: [$"{operatorField}.parameters.{paramName}"],
                            operatorId: op.TempId,
                            parameterName: paramName,
                            repairHint: $"请在下一轮直接生成 {paramName} 的合法范围值。");
                    }
                }

                // 枚举值校验
                if (paramDef.DataType.Equals("enum", StringComparison.OrdinalIgnoreCase) && paramDef.Options != null && paramDef.Options.Count > 0)
                {
                    var validValues = paramDef.Options.Select(o => o.Value).ToList();
                    if (!validValues.Contains(paramValueStr))
                    {
                        result.AddWarning(
                            $"算子 {op.TempId}({metadata.DisplayName}) 的枚举参数 '{paramName}' 值为 '{paramValueStr}' 不合法，有效值为: {string.Join(", ", validValues)}",
                            code: "invalid_enum_value",
                            category: "parameter",
                            relatedFields: [$"{operatorField}.parameters.{paramName}"],
                            operatorId: op.TempId,
                            parameterName: paramName,
                            repairHint: $"请把 {paramName} 改成有效枚举值之一：{string.Join(", ", validValues)}。");
                    }
                }
            }
        }
    }

    private static void ApplyIntelligentDefaults(
        AiGeneratedOperator op,
        OperatorMetadata metadata,
        AiValidationResult result,
        string operatorField)
    {
        foreach (var paramDef in metadata.Parameters.Where(p => p.IsRequired))
        {
            if (op.Parameters.ContainsKey(paramDef.Name) &&
                !string.IsNullOrWhiteSpace(op.Parameters[paramDef.Name]))
            {
                continue;
            }

            var defaultValue = ConvertParameterValueToString(paramDef.DefaultValue);
            if (string.IsNullOrWhiteSpace(defaultValue))
                continue;

            op.Parameters[paramDef.Name] = defaultValue;
            result.AddWarning(
                $"算子 {op.TempId}({metadata.DisplayName}) 的必填参数 '{paramDef.Name}' 缺失，已自动填充默认值 {defaultValue}",
                code: "default_parameter_applied",
                category: "parameter",
                relatedFields: [$"{operatorField}.parameters.{paramDef.Name}"],
                operatorId: op.TempId,
                parameterName: paramDef.Name,
                repairHint: $"如默认值不符合场景，请在下一轮明确给出 {paramDef.Name}。");
        }
    }

    private static string ConvertParameterValueToString(object? value)
    {
        if (value == null)
            return string.Empty;

        if (value is bool boolean)
            return boolean ? "true" : "false";

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string FormatNumericValue(double value, string dataType)
    {
        if (dataType.Equals("int", StringComparison.OrdinalIgnoreCase) ||
            dataType.Equals("integer", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToInt32(Math.Round(value, MidpointRounding.AwayFromZero))
                .ToString(CultureInfo.InvariantCulture);
        }

        return value.ToString("0.########", CultureInfo.InvariantCulture);
    }

    private static bool TryParseDouble(object? value, out double parsed)
    {
        if (value == null)
        {
            parsed = 0;
            return false;
        }

        var text = value.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            parsed = 0;
            return false;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ||
               double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed);
    }

    private void ValidateHasSourceAndOutput(
        AiGeneratedFlowJson flow,
        AiValidationResult result,
        Dictionary<string, OperatorMetadata> metaMap)
    {
        // 警告：没有源算子
        var hasSource = flow.Operators.Any(op =>
            metaMap.TryGetValue(op.TempId, out var meta) &&
            meta.InputPorts.Count == 0);

        if (!hasSource)
        {
            result.AddWarning(
                "工作流没有图像源算子（无输入端口的算子），建议添加 ImageAcquisition",
                code: "missing_image_source",
                category: "completeness",
                relatedFields: ["operators"],
                repairHint: "请补充图像源算子，例如 ImageAcquisition。");
        }

        // 警告：没有 ResultOutput
        var hasOutput = flow.Operators.Any(op =>
            op.OperatorType == "ResultOutput" ||
            (metaMap.TryGetValue(op.TempId, out var meta) && meta.Category == "输出"));

        if (!hasOutput)
        {
            result.AddWarning(
                "工作流没有结果输出算子，建议添加 ResultOutput",
                code: "missing_result_output",
                category: "completeness",
                relatedFields: ["operators"],
                repairHint: "请补充 ResultOutput 或其他输出类算子，保证结果可消费。");
        }
    }
}
