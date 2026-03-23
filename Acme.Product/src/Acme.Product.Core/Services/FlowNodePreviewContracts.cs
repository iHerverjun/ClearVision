using Acme.Product.Core.Entities;

namespace Acme.Product.Core.Services;

/// <summary>
/// 流程节点预览服务
/// </summary>
public interface IFlowNodePreviewService
{
    /// <summary>
    /// 执行目标节点上游子图并返回结构化预览结果
    /// </summary>
    Task<FlowNodePreviewWithMetricsResult> PreviewWithMetricsAsync(
        OperatorFlow flow,
        Guid targetNodeId,
        byte[]? inputImage,
        CancellationToken ct = default);
}

/// <summary>
/// 流程节点预览结果
/// </summary>
public sealed class FlowNodePreviewWithMetricsResult
{
    public bool Success { get; init; }
    public Guid TargetNodeId { get; init; }
    public byte[]? InputImage { get; init; }
    public byte[]? PreviewImage { get; init; }
    public Dictionary<string, object> Outputs { get; init; } = new();
    public PreviewMetrics? Metrics { get; init; }
    public List<string> DiagnosticCodes { get; init; } = new();
    public List<ParameterSuggestion> Suggestions { get; init; } = new();
    public List<PreviewMissingResource> MissingResources { get; init; } = new();
    public string? ErrorMessage { get; init; }
    public Guid? FailedOperatorId { get; init; }
    public string? FailedOperatorName { get; init; }
    public List<ExecutedOperatorTrace> ExecutedOperators { get; init; } = new();
}

/// <summary>
/// 预览缺失资源
/// </summary>
public sealed class PreviewMissingResource
{
    public string ResourceType { get; init; } = string.Empty;
    public string ResourceKey { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string DiagnosticCode { get; init; } = string.Empty;
}

/// <summary>
/// 调试执行轨迹
/// </summary>
public sealed class ExecutedOperatorTrace
{
    public Guid OperatorId { get; init; }
    public string OperatorName { get; init; } = string.Empty;
    public int ExecutionOrder { get; init; }
    public long ExecutionTimeMs { get; init; }
    public bool IsSuccess { get; init; }
}

/// <summary>
/// 场景级自动调参结果
/// </summary>
public sealed class ScenarioAutoTuneResult
{
    public bool Success { get; init; }
    public string ScenarioKey { get; init; } = string.Empty;
    public Dictionary<string, object> FinalParameters { get; init; } = new();
    public List<AutoTuneIteration> Iterations { get; init; } = new();
    public int TotalIterations { get; init; }
    public long TotalExecutionTimeMs { get; init; }
    public bool IsGoalAchieved { get; init; }
    public string? ErrorMessage { get; init; }
    public List<string> DiagnosticCodes { get; init; } = new();
    public List<PreviewMissingResource> MissingResources { get; init; } = new();
    public FlowNodePreviewWithMetricsResult? FinalPreview { get; init; }
}
