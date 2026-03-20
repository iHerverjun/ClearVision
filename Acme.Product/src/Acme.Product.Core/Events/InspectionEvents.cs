// InspectionEvents.cs
// 检测事件定义
// 作者：架构修复方案 v2

using Acme.Product.Core.Enums;

namespace Acme.Product.Core.Events;

/// <summary>
/// 状态变更事件
/// </summary>
public record InspectionStateChangedEvent : IInspectionEvent
{
    public required Guid ProjectId { get; init; }
    public required Guid SessionId { get; init; }
    public required string NewState { get; init; }  // Starting, Running, Stopping, Stopped, Faulted
    public required string OldState { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 检测结果事件
/// </summary>
public record InspectionResultEvent : IInspectionEvent
{
    public required Guid ProjectId { get; init; }
    public required Guid SessionId { get; init; }
    public required Guid ResultId { get; init; }
    public required string Status { get; init; }  // OK, NG, Error
    public required int DefectCount { get; init; }
    public required long ProcessingTimeMs { get; init; }
    public string? OutputImageBase64 { get; init; }
    public Dictionary<string, object>? OutputData { get; init; }
    public Dictionary<string, object>? AnalysisData { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 进度事件
/// </summary>
public record InspectionProgressEvent : IInspectionEvent
{
    public required Guid ProjectId { get; init; }
    public required Guid SessionId { get; init; }
    public required int ProcessedCount { get; init; }
    public int? TotalCount { get; init; }
    public double? ProgressPercentage { get; init; }
    public string? CurrentOperator { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 心跳事件（SSE 保活）
/// </summary>
public record HeartbeatEvent : IInspectionEvent
{
    public Guid ProjectId { get; init; } = Guid.Empty;  // 心跳不关联特定项目
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 初始状态事件（SSE 连接时发送）
/// </summary>
public record InitialStateEvent : IInspectionEvent
{
    public required Guid ProjectId { get; init; }
    public required object State { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
