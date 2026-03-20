// AIGeneratedFlowParser.cs
// AI生成流程解析器 - Sprint 5
// 将 LLM 生成的 JSON 解析为内部流程定义
// 作者：蘅芜君

using System.Text.Json;
using System.Text.Json.Serialization;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Services;

namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// Legacy AI 生成流程解析器。
/// 该解析器用于兼容 Sprint 5 时代的旧 JSON 格式与历史测试，不再作为当前主链的唯一解析入口。
/// 当前正式支持的主链为 <see cref="AiFlowGenerationService"/> 驱动的生成与校验流程。
/// </summary>
[Obsolete("Legacy compatibility parser only. Use the AiFlowGenerationService-driven main chain for the supported AI generation path.")]
public class AIGeneratedFlowParser
{
    private readonly FlowLinter _linter;

    public AIGeneratedFlowParser(FlowLinter linter)
    {
        _linter = linter;
    }

    /// <summary>
    /// 解析 Legacy AI 生成流程 JSON。
    /// </summary>
    public ParseResult Parse(string aiGeneratedJson)
    {
        try
        {
            var aiFlow = JsonSerializer.Deserialize<AIGeneratedFlow>(aiGeneratedJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            });

            if (aiFlow == null)
            {
                return ParseResult.Failure("无法解析 AI 生成的 JSON");
            }

            // 转换为内部 Flow 格式
            var flow = new OperatorFlow(aiFlow.FlowName ?? "AI生成流程");

            // 解析算子
            var operators = new List<Operator>();
            var operatorIdMap = new Dictionary<string, Guid>();

            foreach (var op in aiFlow.Operators)
            {
                var operatorId = Guid.NewGuid();
                operatorIdMap[op.Id] = operatorId;

                var position = new Position(
                    op.Position?.X ?? 100 + operators.Count * 200,
                    op.Position?.Y ?? 100
                );

                var operatorNode = new Operator(operatorId, op.Name, ParseOperatorType(op.Type), position.X, position.Y);

                // 添加参数
                foreach (var param in ParseParameters(op.Parameters))
                {
                    operatorNode.AddParameter(param);
                }

                // 添加端口
                foreach (var port in ParsePorts(op.InputPorts, PortDirection.Input))
                {
                    operatorNode.LoadInputPort(port.Id, port.Name, port.DataType, true);
                }
                foreach (var port in ParsePorts(op.OutputPorts, PortDirection.Output))
                {
                    operatorNode.LoadOutputPort(port.Id, port.Name, port.DataType);
                }

                operators.Add(operatorNode);
                flow.AddOperator(operatorNode);
            }

            // 解析连接
            foreach (var conn in aiFlow.Connections)
            {
                if (!operatorIdMap.TryGetValue(conn.SourceOperatorId, out var sourceOpId))
                {
                    return ParseResult.Failure($"连接引用了不存在的源算子 ID: {conn.SourceOperatorId}");
                }
                if (!operatorIdMap.TryGetValue(conn.TargetOperatorId, out var targetOpId))
                {
                    return ParseResult.Failure($"连接引用了不存在的目标算子 ID: {conn.TargetOperatorId}");
                }

                var connection = new OperatorConnection(
                    sourceOpId,
                    Guid.Parse(conn.SourcePortId),
                    targetOpId,
                    Guid.Parse(conn.TargetPortId)
                );

                flow.AddConnection(connection);
            }

            // 运行 Linter 检查
            var lintResult = _linter.Lint(flow);
            if (lintResult.HasErrors)
            {
                return ParseResult.Failure(
                    $"生成的流程存在以下问题:\n{string.Join("\n", lintResult.Issues.Where(i => i.Severity == LintSeverity.Error).Select(v => $"- [{v.Code}] {v.Message}"))}",
                    flow
                );
            }

            return ParseResult.Success(flow, lintResult.Issues.Where(i => i.Severity == LintSeverity.Warning).ToList());
        }
        catch (JsonException ex)
        {
            return ParseResult.Failure($"JSON 解析错误: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ParseResult.Failure($"解析失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 解析算子类型字符串
    /// </summary>
    private OperatorType ParseOperatorType(string typeString)
    {
        return typeString switch
        {
            "ImageAcquisition" => OperatorType.ImageAcquisition,
            "GaussianBlur" => OperatorType.GaussianBlur,
            "Thresholding" => OperatorType.Thresholding,
            "EdgeDetection" => OperatorType.EdgeDetection,
            "DeepLearning" => OperatorType.DeepLearning,
            "CircleMeasurement" => OperatorType.CircleMeasurement,
            "LineMeasurement" => OperatorType.LineMeasurement,
            "ForEach" => OperatorType.ForEach,
            "ArrayIndexer" => OperatorType.ArrayIndexer,
            "JsonExtractor" => OperatorType.JsonExtractor,
            "MathOperation" => OperatorType.MathOperation,
            "LogicGate" => OperatorType.LogicGate,
            "TypeConvert" => OperatorType.TypeConvert,
            "HttpRequest" => OperatorType.HttpRequest,
            "MqttPublish" => OperatorType.MqttPublish,
            "StringFormat" => OperatorType.StringFormat,
            "ImageSave" => OperatorType.ImageSave,
            "ConditionalBranch" => OperatorType.ConditionalBranch,
            "ResultJudgment" => OperatorType.ResultJudgment,
            "ModbusCommunication" => OperatorType.ModbusCommunication,
            "SiemensS7Communication" => OperatorType.SiemensS7Communication,
            _ => throw new ArgumentException($"未知的算子类型: {typeString}")
        };
    }

    /// <summary>
    /// 解析参数列表
    /// </summary>
    private List<Parameter> ParseParameters(List<AIParameter>? parameters)
    {
        if (parameters == null)
            return new List<Parameter>();

        return parameters.Select(p => new Parameter(
            Guid.NewGuid(),
            p.Name,
            p.Name,  // DisplayName
            string.Empty,  // Description
            ParseParameterDataType(p.Value),
            p.Value,  // defaultValue
            null,  // minValue
            null,  // maxValue
            true,  // isRequired
            null   // options
        )).ToList();
    }

    /// <summary>
    /// 解析端口列表
    /// </summary>
    private List<Port> ParsePorts(List<AIPort>? ports, PortDirection direction)
    {
        if (ports == null)
            return new List<Port>();

        return ports.Select(p => new Port(
            Guid.TryParse(p.Id, out var guid) ? guid : Guid.NewGuid(),
            p.Name,
            direction,
            ParsePortDataType(p.DataType),
            true
        )).ToList();
    }

    /// <summary>
    /// 推断参数数据类型
    /// </summary>
    private string ParseParameterDataType(string value)
    {
        if (bool.TryParse(value, out _))
            return "Boolean";
        if (int.TryParse(value, out _))
            return "Integer";
        if (float.TryParse(value, out _))
            return "Float";
        return "String";
    }

    /// <summary>
    /// 解析端口数据类型
    /// </summary>
    private PortDataType ParsePortDataType(string? dataType)
    {
        return dataType?.ToLower() switch
        {
            "image" => PortDataType.Image,
            "integer" or "int" => PortDataType.Integer,
            "float" or "double" => PortDataType.Float,
            "boolean" or "bool" => PortDataType.Boolean,
            "string" => PortDataType.String,
            "point" => PortDataType.Point,
            "rectangle" or "rect" => PortDataType.Rectangle,
            "contour" => PortDataType.Contour,
            "pointlist" or "point_list" => PortDataType.PointList,
            "detectionresult" or "detection_result" => PortDataType.DetectionResult,
            "detectionlist" or "detection_list" => PortDataType.DetectionList,
            "circledata" or "circle_data" => PortDataType.CircleData,
            "linedata" or "line_data" => PortDataType.LineData,
            _ => PortDataType.Any
        };
    }
}

/// <summary>
/// 解析结果
/// </summary>
public class ParseResult
{
    public bool IsSuccessful { get; }
    public OperatorFlow? Flow { get; }
    public string ErrorMessage { get; }
    public List<LintIssue> Warnings { get; }

    private ParseResult(bool success, OperatorFlow? flow, string errorMessage, List<LintIssue> warnings)
    {
        IsSuccessful = success;
        Flow = flow;
        ErrorMessage = errorMessage;
        Warnings = warnings;
    }

    public static ParseResult Success(OperatorFlow flow, List<LintIssue> warnings)
        => new(true, flow, string.Empty, warnings);

    public static ParseResult Failure(string errorMessage, OperatorFlow? partialFlow = null)
        => new(false, partialFlow, errorMessage, new List<LintIssue>());
}

/// <summary>
/// AI 生成的流程定义（用于反序列化）
/// </summary>
public class AIGeneratedFlow
{
    [JsonPropertyName("flowName")]
    public string? FlowName { get; set; }

    [JsonPropertyName("operators")]
    public List<AIOperator> Operators { get; set; } = new();

    [JsonPropertyName("connections")]
    public List<AIConnection> Connections { get; set; } = new();
}

public class AIOperator
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public List<AIParameter>? Parameters { get; set; }

    [JsonPropertyName("inputPorts")]
    public List<AIPort>? InputPorts { get; set; }

    [JsonPropertyName("outputPorts")]
    public List<AIPort>? OutputPorts { get; set; }

    [JsonPropertyName("position")]
    public AIPosition? Position { get; set; }
}

public class AIParameter
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

public class AIPort
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("dataType")]
    public string? DataType { get; set; }
}

public class AIConnection
{
    [JsonPropertyName("sourceOperatorId")]
    public string SourceOperatorId { get; set; } = string.Empty;

    [JsonPropertyName("sourcePortId")]
    public string SourcePortId { get; set; } = string.Empty;

    [JsonPropertyName("targetOperatorId")]
    public string TargetOperatorId { get; set; } = string.Empty;

    [JsonPropertyName("targetPortId")]
    public string TargetPortId { get; set; } = string.Empty;
}

public class AIPosition
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }
}
