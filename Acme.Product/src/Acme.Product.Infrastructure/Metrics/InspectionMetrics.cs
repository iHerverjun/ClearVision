// InspectionMetrics.cs
// 检测系统指标收集
// 作者：架构修复方案 v2

using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Metrics;

/// <summary>
/// 检测系统指标
/// 使用 System.Diagnostics.Metrics（.NET 6+ 推荐方式）
/// </summary>
public class InspectionMetrics
{
    private readonly Meter _meter;
    private readonly Histogram<double> _detectionLatency;
    private readonly Histogram<double> _flowExecutionLatency;
    private readonly UpDownCounter<int> _activeWorkers;
    private readonly Counter<long> _inspectionsTotal;
    private readonly Counter<long> _inspectionsFailed;
    private readonly ObservableGauge<int> _activeSessionsGauge;
    
    // 用于 ObservableGauge 的回调
    private int _activeSessionsCount = 0;

    public InspectionMetrics()
    {
        _meter = new Meter("Acme.Product.Inspection", "1.0.0");
        
        // 检测延迟（毫秒）
        _detectionLatency = _meter.CreateHistogram<double>(
            "inspection.detection.latency_ms",
            unit: "ms",
            description: "单次检测延迟");
        
        // 流程执行延迟（毫秒）
        _flowExecutionLatency = _meter.CreateHistogram<double>(
            "inspection.flow_execution.latency_ms",
            unit: "ms",
            description: "流程执行延迟");
        
        // 活跃 Worker 数（可增减）
        _activeWorkers = _meter.CreateUpDownCounter<int>(
            "inspection.workers.active",
            description: "当前活跃的检测 Worker 数");
        
        // 检测总次数（按状态分类）
        _inspectionsTotal = _meter.CreateCounter<long>(
            "inspection.total",
            description: "检测总次数",
            unit: "1");
        
        // 检测失败次数
        _inspectionsFailed = _meter.CreateCounter<long>(
            "inspection.failed",
            description: "检测失败次数",
            unit: "1");
        
        // 活跃会话数（ObservableGauge - 被动查询）
        _activeSessionsGauge = _meter.CreateObservableGauge(
            "inspection.sessions.active",
            () => new Measurement<int>(_activeSessionsCount),
            description: "当前活跃的实时检测会话数");
    }

    /// <summary>
    /// 记录检测延迟
    /// </summary>
    /// <param name="latencyMs">延迟（毫秒）</param>
    /// <param name="status">检测结果状态</param>
    public void RecordDetectionLatency(double latencyMs, string status = "OK")
    {
        _detectionLatency.Record(latencyMs, 
            new KeyValuePair<string, object?>("status", status));
    }

    /// <summary>
    /// 记录流程执行延迟
    /// </summary>
    public void RecordFlowExecutionLatency(double latencyMs, bool success)
    {
        _flowExecutionLatency.Record(latencyMs,
            new KeyValuePair<string, object?>("success", success));
    }

    /// <summary>
    /// 增加活跃 Worker 计数
    /// </summary>
    public void IncrementActiveWorkers()
    {
        _activeWorkers.Add(1);
    }

    /// <summary>
    /// 减少活跃 Worker 计数
    /// </summary>
    public void DecrementActiveWorkers()
    {
        _activeWorkers.Add(-1);
    }

    /// <summary>
    /// 记录检测完成
    /// </summary>
    public void RecordInspectionCompleted(string status, int defectCount = 0)
    {
        _inspectionsTotal.Add(1,
            new KeyValuePair<string, object?>("status", status),
            new KeyValuePair<string, object?>("has_defects", defectCount > 0));
    }

    /// <summary>
    /// 记录检测失败
    /// </summary>
    public void RecordInspectionFailed(string errorType)
    {
        _inspectionsFailed.Add(1,
            new KeyValuePair<string, object?>("error_type", errorType));
    }

    /// <summary>
    /// 更新活跃会话数（供 ObservableGauge 使用）
    /// </summary>
    public void UpdateActiveSessions(int count)
    {
        Interlocked.Exchange(ref _activeSessionsCount, count);
    }

    /// <summary>
    /// 获取 Meter 实例（用于注册到 MeterProvider）
    /// </summary>
    public Meter GetMeter() => _meter;
}

/// <summary>
/// 检测上下文（用于 Correlation ID 传递）
/// </summary>
public class InspectionContext
{
    private static readonly AsyncLocal<InspectionContext?> _current = new();
    
    /// <summary>
    /// 当前上下文
    /// </summary>
    public static InspectionContext? Current
    {
        get => _current.Value;
        private set => _current.Value = value;
    }

    /// <summary>
    /// 关联 ID（全链路追踪）
    /// </summary>
    public Guid CorrelationId { get; } = Guid.NewGuid();
    
    /// <summary>
    /// 项目 ID
    /// </summary>
    public Guid? ProjectId { get; set; }
    
    /// <summary>
    /// 会话 ID
    /// </summary>
    public Guid? SessionId { get; set; }
    
    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime StartedAt { get; } = DateTime.UtcNow;

    /// <summary>
    /// 创建新的上下文作用域
    /// </summary>
    public static IDisposable BeginScope(Guid? projectId = null, Guid? sessionId = null)
    {
        var previous = Current;
        Current = new InspectionContext
        {
            ProjectId = projectId,
            SessionId = sessionId
        };

        return new ContextScope(previous);
    }

    private class ContextScope : IDisposable
    {
        private readonly InspectionContext? _previousContext;
        private bool _disposed;

        public ContextScope(InspectionContext? previousContext)
        {
            _previousContext = previousContext;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Current = _previousContext;
                _disposed = true;
            }
        }
    }
}

/// <summary>
/// 日志作用域扩展
/// </summary>
public static class InspectionLoggingExtensions
{
    /// <summary>
    /// 创建检测日志作用域（包含 Correlation ID）
    /// </summary>
    public static IDisposable? BeginInspectionScope(
        this ILogger logger,
        Guid correlationId,
        Guid? projectId = null,
        Guid? sessionId = null)
    {
        return logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["ProjectId"] = projectId,
            ["SessionId"] = sessionId
        });
    }
}
