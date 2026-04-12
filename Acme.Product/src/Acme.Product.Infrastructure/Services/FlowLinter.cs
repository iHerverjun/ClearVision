// FlowLinter.cs
// 工程静态检查器 - Sprint 4 Task 4.1
// 三层检查规则：结构合法性、语义安全、参数值合理性
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Calibration;

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// 流程静态检查器（Linter）- Sprint 4 完整实现
/// 
/// 三层检查规则：
/// 第一层：结构合法性（算子类型合法、端口 ID 存在、类型兼容、无环路）
/// 第二层：语义安全（SAFETY_001, SAFETY_002, SAFETY_003）
/// 第三层：参数值合理性（PARAM_001~004）
/// </summary>
public class FlowLinter
{
    private static readonly HashSet<OperatorType> CalibrationBundleConsumers = new()
    {
        OperatorType.CoordinateTransform,
        OperatorType.Undistort,
        OperatorType.FisheyeUndistort,
        OperatorType.PixelToWorldTransform
    };

    /// <summary>
    /// 检查流程安全性（完整三层规则）
    /// </summary>
    public LintResult Lint(OperatorFlow flow)
    {
        var result = new LintResult();
        var issues = new List<LintIssue>();

        // 第一层：结构合法性
        issues.AddRange(CheckStructuralValidity(flow));

        // 第二层：语义安全
        issues.AddRange(CheckSemanticSafety(flow));

        // 第三层：参数值合理性
        issues.AddRange(CheckParameterValidity(flow));

        result.Issues = issues;
        result.HasErrors = issues.Any(i => i.Severity == LintSeverity.Error);
        result.HasWarnings = issues.Any(i => i.Severity == LintSeverity.Warning);
        result.ErrorCount = issues.Count(i => i.Severity == LintSeverity.Error);
        result.WarningCount = issues.Count(i => i.Severity == LintSeverity.Warning);

        return result;
    }

    #region 第一层：结构合法性

    /// <summary>
    /// 检查结构合法性
    /// </summary>
    private IEnumerable<LintIssue> CheckStructuralValidity(OperatorFlow flow)
    {
        // STRUCT_001: 算子类型合法性检查
        foreach (var op in flow.Operators)
        {
            if (!IsValidOperatorType(op.Type))
            {
                yield return new LintIssue
                {
                    Code = "STRUCT_001",
                    Severity = LintSeverity.Error,
                    OperatorId = op.Id,
                    OperatorName = op.Name,
                    Message = $"算子 [{op.Name}] 使用了未定义的算子类型: {op.Type}",
                    Suggestion = "请使用支持的算子类型，或更新算子注册表。"
                };
            }
        }

        // STRUCT_002: 端口连接检查（目标端口存在性）
        foreach (var conn in flow.Connections)
        {
            var sourceOp = flow.Operators.FirstOrDefault(o => o.Id == conn.SourceOperatorId);
            var targetOp = flow.Operators.FirstOrDefault(o => o.Id == conn.TargetOperatorId);

            if (sourceOp == null)
            {
                yield return new LintIssue
                {
                    Code = "STRUCT_002",
                    Severity = LintSeverity.Error,
                    Message = $"连接引用了不存在的源算子: {conn.SourceOperatorId}",
                    Suggestion = "请删除无效连接或重新配置。"
                };
            }

            if (targetOp == null)
            {
                yield return new LintIssue
                {
                    Code = "STRUCT_002",
                    Severity = LintSeverity.Error,
                    Message = $"连接引用了不存在的目标算子: {conn.TargetOperatorId}",
                    Suggestion = "请删除无效连接或重新配置。"
                };
            }
        }

        // STRUCT_003: DAG 无环检查
        if (HasCycle(flow))
        {
            yield return new LintIssue
            {
                Code = "STRUCT_003",
                Severity = LintSeverity.Error,
                Message = "流程图中检测到环路，违反了 DAG（有向无环图）原则。",
                Suggestion = "请移除环路连接，确保数据流向单向。"
            };
        }

        // STRUCT_004: 端口类型兼容性检查
        foreach (var conn in flow.Connections)
        {
            var sourceType = GetSourcePortDataType(flow, conn);
            var targetType = GetTargetPortDataType(flow, conn);

            if (sourceType.HasValue && targetType.HasValue && !AreTypesCompatible(sourceType.Value, targetType.Value))
            {
                yield return new LintIssue
                {
                    Code = "STRUCT_004",
                    Severity = LintSeverity.Error,
                    Message = $"端口类型不兼容: 源端口输出 {sourceType} 无法连接到目标端口输入 {targetType}",
                    Suggestion = "请检查端口数据类型匹配，或使用 TypeConvert 算子进行转换。"
                };
            }
        }
        foreach (var calibOp in flow.Operators.Where(op => RequiresAcceptedCalibrationBundle(op.Type)))
        {
            if (!HasCalibrationDataUpstream(flow, calibOp, out var upstreamCalibrationSource) ||
                upstreamCalibrationSource == null ||
                !IsPreviewOnlyCalibrationProducer(upstreamCalibrationSource))
            {
                continue;
            }

            yield return new LintIssue
            {
                Code = "SAFETY_003",
                Severity = LintSeverity.Error,
                OperatorId = calibOp.Id,
                OperatorName = calibOp.Name,
                Message = $"Calibration consumer [{calibOp.Name}] is connected to preview-only calibration producer [{upstreamCalibrationSource.Name}].",
                Suggestion = "Use a producer mode that can emit Accepted=true CalibrationBundleV2 output."
            };
        }
    }

    /// <summary>
    /// 检查是否为有效的算子类型
    /// </summary>
    private bool IsValidOperatorType(OperatorType type)
    {
        return Enum.IsDefined(typeof(OperatorType), type);
    }

    /// <summary>
    /// 检测流程图是否存在环路
    /// </summary>
    private bool HasCycle(OperatorFlow flow)
    {
        var visited = new HashSet<Guid>();
        var recursionStack = new HashSet<Guid>();

        foreach (var op in flow.Operators)
        {
            if (HasCycleDFS(op.Id, flow, visited, recursionStack))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasCycleDFS(Guid opId, OperatorFlow flow, HashSet<Guid> visited, HashSet<Guid> recursionStack)
    {
        visited.Add(opId);
        recursionStack.Add(opId);

        var downstreamConnections = flow.Connections
            .Where(c => c.SourceOperatorId == opId);

        foreach (var conn in downstreamConnections)
        {
            var targetId = conn.TargetOperatorId;

            if (!visited.Contains(targetId))
            {
                if (HasCycleDFS(targetId, flow, visited, recursionStack))
                {
                    return true;
                }
            }
            else if (recursionStack.Contains(targetId))
            {
                return true;
            }
        }

        recursionStack.Remove(opId);
        return false;
    }

    /// <summary>
    /// 获取连接源端口的数据类型
    /// </summary>
    private PortDataType? GetSourcePortDataType(OperatorFlow flow, OperatorConnection conn)
    {
        var op = flow.Operators.FirstOrDefault(o => o.Id == conn.SourceOperatorId);
        if (op == null)
            return null;

        var port = op.OutputPorts.FirstOrDefault(p => p.Id == conn.SourcePortId);
        return port?.DataType;
    }

    /// <summary>
    /// 获取连接目标端口的数据类型
    /// </summary>
    private PortDataType? GetTargetPortDataType(OperatorFlow flow, OperatorConnection conn)
    {
        var op = flow.Operators.FirstOrDefault(o => o.Id == conn.TargetOperatorId);
        if (op == null)
            return null;

        var port = op.InputPorts.FirstOrDefault(p => p.Id == conn.TargetPortId);
        return port?.DataType;
    }

    /// <summary>
    /// 检查两种数据类型是否兼容
    /// </summary>
    private bool AreTypesCompatible(PortDataType source, PortDataType target)
    {
        if (source == PortDataType.Any || target == PortDataType.Any)
            return true;

        if (source == target)
            return true;

        if ((source == PortDataType.Integer && target == PortDataType.Float) ||
            (source == PortDataType.Float && target == PortDataType.Integer))
            return true;

        return false;
    }

    #endregion

    #region 第二层：语义安全

    /// <summary>
    /// 检查语义安全
    /// </summary>
    private IEnumerable<LintIssue> CheckSemanticSafety(OperatorFlow flow)
    {
        // SAFETY_001: 通信类算子上游必须有 ConditionalBranch 或 ResultJudgment
        foreach (var commOp in flow.Operators.Where(op => IsCommunicationOperator(op.Type)))
        {
            var hasGuardianUpstream = HasGuardianUpstream(flow, commOp);
            if (!hasGuardianUpstream)
            {
                yield return new LintIssue
                {
                    Code = "SAFETY_001",
                    Severity = LintSeverity.Error,
                    OperatorId = commOp.Id,
                    OperatorName = commOp.Name,
                    Message = $"通信算子 [{commOp.Name}] 上游没有 ConditionalBranch 或 ResultJudgment 保护。",
                    Suggestion = "请在通信算子前添加条件分支或结果判定，防止无条件触发外部设备动作。"
                };
            }
        }

        // SAFETY_002: ForEach 子图安全检查
        foreach (var issue in CheckForEachSubGraphSafety(flow))
        {
            yield return issue;
        }

        // SAFETY_003: 标定消费者必须显式提供 CalibrationData（V2）
        foreach (var calibOp in flow.Operators.Where(op => RequiresAcceptedCalibrationBundle(op.Type)))
        {
            var calibrationDataParam = FindCalibrationDataParameter(calibOp);
            var calibrationData = GetParamStringValue(calibrationDataParam);
            var hasUpstreamBundleConnection = HasCalibrationDataUpstream(flow, calibOp, out var upstreamCalibrationSource);

            if (string.IsNullOrWhiteSpace(calibrationData) && !hasUpstreamBundleConnection)
            {
                yield return new LintIssue
                {
                    Code = "SAFETY_003",
                    Severity = LintSeverity.Error,
                    OperatorId = calibOp.Id,
                    OperatorName = calibOp.Name,
                    Message = $"标定消费者算子 [{calibOp.Name}] 缺少 CalibrationData（CalibrationBundleV2 JSON）。",
                    Suggestion = "请通过上游标定节点或参数注入 CalibrationBundleV2，并确保质量状态为 Accepted。"
                };
            }
        }
    }

    /// <summary>
    /// 检查算子上游是否有保护性算子
    /// </summary>
    private bool HasGuardianUpstream(OperatorFlow flow, Operator op)
    {
        var upstreamOps = flow.Connections
            .Where(c => c.TargetOperatorId == op.Id)
            .Select(c => flow.Operators.FirstOrDefault(o => o.Id == c.SourceOperatorId))
            .Where(o => o != null)
            .ToList();

        if (!upstreamOps.Any())
            return false;

        foreach (var upstream in upstreamOps)
        {
            if (upstream!.Type == OperatorType.ConditionalBranch ||
                upstream.Type == OperatorType.ResultJudgment)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// SAFETY_002: ForEach 子图安全检查
    /// </summary>
    private IEnumerable<LintIssue> CheckForEachSubGraphSafety(OperatorFlow flow)
    {
        foreach (var forEachOp in flow.Operators.Where(op => op.Type == OperatorType.ForEach))
        {
            var ioModeParam = forEachOp.Parameters.FirstOrDefault(p => p.Name == "IoMode");
            var ioMode = GetParamStringValue(ioModeParam) ?? "Parallel";

            var subGraph = GetSubGraphFromOperator(forEachOp);
            if (subGraph == null)
                continue;

            var commOps = subGraph.Operators
                .Where(op => IsCommunicationOperator(op.Type))
                .ToList();

            if (commOps.Count == 0)
                continue;

            if (ioMode.Equals("Parallel", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var op in commOps)
                {
                    yield return new LintIssue
                    {
                        Code = "SAFETY_002",
                        Severity = LintSeverity.Warning,
                        OperatorId = op.Id,
                        OperatorName = op.Name,
                        Message = $"ForEach（IoMode=Parallel）子图中检测到通信算子 [{op.Name}]。",
                        Suggestion = "如果业务上需要逐条通信，请将 ForEach.IoMode 改为 Sequential。"
                    };
                }
            }
        }
    }

    /// <summary>
    /// 判断是否为通信算子
    /// </summary>
    private bool IsCommunicationOperator(OperatorType type)
    {
        return type switch
        {
            OperatorType.ModbusCommunication => true,
            OperatorType.TcpCommunication => true,
            OperatorType.DatabaseWrite => true,
            OperatorType.SerialCommunication => true,
            OperatorType.SiemensS7Communication => true,
            OperatorType.MitsubishiMcCommunication => true,
            OperatorType.OmronFinsCommunication => true,
            OperatorType.ModbusRtuCommunication => true,
            OperatorType.HttpRequest => true,
            OperatorType.MqttPublish => true,
            _ => false
        };
    }

    /// <summary>
    /// 从 ForEach 算子参数中解析子图
    /// </summary>
    private OperatorFlow? GetSubGraphFromOperator(Operator op)
    {
        var subGraphParam = op.Parameters.FirstOrDefault(p => p.Name == "SubGraph");
        if (subGraphParam?.Value == null)
            return null;

        // 优先尝试直接类型转换（内存对象模式）
        if (subGraphParam.Value is OperatorFlow flow)
            return flow;

        try
        {
            string? json = subGraphParam.Value switch
            {
                string s => s,
                System.Text.Json.JsonElement e when e.ValueKind == System.Text.Json.JsonValueKind.String => e.GetString(),
                System.Text.Json.JsonElement e => e.GetRawText(),
                _ => System.Text.Json.JsonSerializer.Serialize(subGraphParam.Value)
            };

            if (string.IsNullOrWhiteSpace(json))
                return null;

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            var resultFlow = new OperatorFlow(
                root.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() ?? "SubGraph" : "SubGraph");

            if (root.TryGetProperty("Operators", out var opArray) && opArray.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var opEl in opArray.EnumerateArray())
                {
                    var opName = opEl.TryGetProperty("Name", out var np) ? np.GetString() ?? "Op" : "Op";
                    var opTypeInt = opEl.TryGetProperty("Type", out var tp) ? tp.GetInt32() : 0;
                    var opId = opEl.TryGetProperty("Id", out var idp) && idp.TryGetGuid(out var parsedId) ? parsedId : Guid.NewGuid();

                    var workOp = new Operator(opId, opName, (OperatorType)opTypeInt, 0, 0);
                    resultFlow.AddOperator(workOp);
                }
            }
            return resultFlow;
        }
        catch
        {
            return null;
        }
    }

    private string? GetParamStringValue(Parameter? param)
    {
        if (param?.Value == null)
            return null;
        if (param.Value is string s)
            return s;
        if (param.Value is System.Text.Json.JsonElement element)
        {
            if (element.ValueKind == System.Text.Json.JsonValueKind.String)
                return element.GetString();
            return element.GetRawText();
        }
        return param.Value.ToString();
    }

    private static bool RequiresAcceptedCalibrationBundle(OperatorType type)
    {
        return CalibrationBundleConsumers.Contains(type);
    }

    private static Parameter? FindCalibrationDataParameter(Operator op)
    {
        return op.Parameters.FirstOrDefault(p =>
            p.Name.Equals("CalibrationData", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasCalibrationDataUpstream(OperatorFlow flow, Operator op, out Operator? upstreamCalibrationSource)
    {
        upstreamCalibrationSource = null;

        foreach (var connection in flow.Connections.Where(c => c.TargetOperatorId == op.Id))
        {
            var targetPort = op.InputPorts.FirstOrDefault(p => p.Id == connection.TargetPortId);
            if (targetPort == null || !targetPort.Name.Equals("CalibrationData", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var sourceOp = flow.Operators.FirstOrDefault(candidate => candidate.Id == connection.SourceOperatorId);
            if (sourceOp == null)
            {
                continue;
            }

            var sourcePort = sourceOp.OutputPorts.FirstOrDefault(p => p.Id == connection.SourcePortId);
            if (sourcePort == null || !sourcePort.Name.Equals("CalibrationData", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            upstreamCalibrationSource = sourceOp;
            return true;
        }

        return false;
    }

    private static bool IsPreviewOnlyCalibrationProducer(Operator op)
    {
        static bool Matches(Parameter? parameter, string expected)
        {
            var value = parameter?.Value?.ToString();
            return value != null && value.Equals(expected, StringComparison.OrdinalIgnoreCase);
        }

        return op.Type switch
        {
            OperatorType.CameraCalibration or OperatorType.FisheyeCalibration
                => Matches(op.Parameters.FirstOrDefault(p => p.Name.Equals("Mode", StringComparison.OrdinalIgnoreCase)), "SingleImage"),
            OperatorType.StereoCalibration
                => Matches(op.Parameters.FirstOrDefault(p => p.Name.Equals("Mode", StringComparison.OrdinalIgnoreCase)), "SinglePair"),
            _ => false
        };
    }

    #endregion

    #region 第三层：参数值合理性

    /// <summary>
    /// 检查参数值合理性
    /// </summary>
    private IEnumerable<LintIssue> CheckParameterValidity(OperatorFlow flow)
    {
        foreach (var op in flow.Operators)
        {
            // PARAM_001: 标定消费者的 CalibrationData 必须是 Accepted 的 CalibrationBundleV2
            if (RequiresAcceptedCalibrationBundle(op.Type))
            {
                var calibrationDataParam = FindCalibrationDataParameter(op);
                var calibrationData = GetParamStringValue(calibrationDataParam);
                if (!string.IsNullOrWhiteSpace(calibrationData))
                {
                    if (!CalibrationBundleV2Json.TryDeserialize(calibrationData!, out var bundle, out var deserializeError))
                    {
                        yield return new LintIssue
                        {
                            Code = "PARAM_001",
                            Severity = LintSeverity.Error,
                            OperatorId = op.Id,
                            OperatorName = op.Name,
                            Message = $"算子 [{op.Name}] 的 CalibrationData 不是有效 CalibrationBundleV2：{deserializeError}",
                            Suggestion = "请提供 schemaVersion=2 且包含 calibrationKind/sourceFrame/targetFrame/quality 的 JSON。"
                        };
                        continue;
                    }

                    if (!CalibrationBundleV2Json.TryRequireAccepted(bundle, out var acceptedError))
                    {
                        yield return new LintIssue
                        {
                            Code = "PARAM_001",
                            Severity = LintSeverity.Error,
                            OperatorId = op.Id,
                            OperatorName = op.Name,
                            Message = $"算子 [{op.Name}] 的 CalibrationData 未通过验收：{acceptedError}",
                            Suggestion = "请使用 Quality.Accepted=true 的标定结果驱动生产链路。"
                        };
                    }
                }
            }

            // PARAM_002: 任意数值参数超出 minValue~maxValue
            foreach (var param in op.Parameters)
            {
                if (IsNumericParameter(param) &&
                    double.TryParse(GetParamStringValue(param), out var value))
                {
                    var minValue = GetMinValue(param);
                    var maxValue = GetMaxValue(param);

                    if (minValue.HasValue && value < minValue.Value)
                    {
                        yield return new LintIssue
                        {
                            Code = "PARAM_002",
                            Severity = LintSeverity.Warning,
                            OperatorId = op.Id,
                            OperatorName = op.Name,
                            Message = $"参数 [{param.Name}] 的值 {value} 小于最小值 {minValue.Value}。",
                            Suggestion = "请调整参数值到有效范围内。"
                        };
                    }

                    if (maxValue.HasValue && value > maxValue.Value)
                    {
                        yield return new LintIssue
                        {
                            Code = "PARAM_002",
                            Severity = LintSeverity.Warning,
                            OperatorId = op.Id,
                            OperatorName = op.Name,
                            Message = $"参数 [{param.Name}] 的值 {value} 大于最大值 {maxValue.Value}。",
                            Suggestion = "请调整参数值到有效范围内。"
                        };
                    }
                }
            }

            // PARAM_003: DeepLearning.Confidence 超出 (0, 1]
            if (op.Type == OperatorType.DeepLearning)
            {
                var confidenceParam = op.Parameters.FirstOrDefault(p =>
                    p.Name.Equals("Confidence", StringComparison.OrdinalIgnoreCase));

                if (confidenceParam != null &&
                    float.TryParse(GetParamStringValue(confidenceParam), out var confidence))
                {
                    if (confidence <= 0 || confidence > 1)
                    {
                        yield return new LintIssue
                        {
                            Code = "PARAM_003",
                            Severity = LintSeverity.Error,
                            OperatorId = op.Id,
                            OperatorName = op.Name,
                            Message = $"Confidence 阈值 {confidence} 超出有效范围 (0, 1]。",
                            Suggestion = "置信度阈值应在 0 到 1 之间（如 0.5 表示 50%）。"
                        };
                    }
                }
            }

            // PARAM_004: MathOperation.Divide 且无上游保证 ValueB ≠ 0
            if (op.Type == OperatorType.MathOperation)
            {
                var operationParam = op.Parameters.FirstOrDefault(p =>
                    p.Name.Equals("Operation", StringComparison.OrdinalIgnoreCase));

                var opVal = GetParamStringValue(operationParam);
                if (opVal != null && opVal.Equals("Divide", StringComparison.OrdinalIgnoreCase))
                {
                    var hasZeroCheck = HasGuardianUpstream(flow, op);
                    if (!hasZeroCheck)
                    {
                        yield return new LintIssue
                        {
                            Code = "PARAM_004",
                            Severity = LintSeverity.Warning,
                            OperatorId = op.Id,
                            OperatorName = op.Name,
                            Message = $"除法运算 [{op.Name}] 没有检测到除数非零保护。",
                            Suggestion = "请在除法算子上游添加 ConditionalBranch 检查 ValueB ≠ 0，避免除以零错误。"
                        };
                    }
                }
            }
        }
    }

    /// <summary>
    /// 判断是否为数值参数
    /// </summary>
    private bool IsNumericParameter(Parameter param)
    {
        var numericTypes = new[] { "int", "integer", "float", "double", "decimal", "number" };
        return numericTypes.Contains(param.DataType.ToLower());
    }

    /// <summary>
    /// 获取参数最小值
    /// </summary>
    private double? GetMinValue(Parameter param)
    {
        if (param.MinValue != null && double.TryParse(param.MinValue.ToString(), out var min))
            return min;
        return null;
    }

    /// <summary>
    /// 获取参数最大值
    /// </summary>
    private double? GetMaxValue(Parameter param)
    {
        if (param.MaxValue != null && double.TryParse(param.MaxValue.ToString(), out var max))
            return max;
        return null;
    }

    #endregion
}

#region Lint 结果类型

/// <summary>
/// Lint 检查结果
/// </summary>
public class LintResult
{
    public List<LintIssue> Issues { get; set; } = new();
    public bool HasErrors { get; set; }
    public bool HasWarnings { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public bool IsValid => !HasErrors;
    public bool StructureValid => !Issues.Any(i => i.Code.StartsWith("STRUCT_") && i.Severity == LintSeverity.Error);
    public bool SemanticValid => !Issues.Any(i => i.Code.StartsWith("SAFETY_") && i.Severity == LintSeverity.Error);
    public bool ParameterValid => !Issues.Any(i => i.Code.StartsWith("PARAM_") && i.Severity == LintSeverity.Error);
}

/// <summary>
/// Lint 检查问题
/// </summary>
public class LintIssue
{
    public string Code { get; set; } = string.Empty;
    public LintSeverity Severity { get; set; }
    public Guid OperatorId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Suggestion { get; set; } = string.Empty;

    public string Layer => Code.Split('_')[0] switch
    {
        "STRUCT" => "结构合法性",
        "SAFETY" => "语义安全",
        "PARAM" => "参数合理性",
        _ => "未知"
    };
}

/// <summary>
/// Lint 严重程度
/// </summary>
public enum LintSeverity
{
    Information = 0,
    Warning = 1,
    Error = 2
}

#endregion
