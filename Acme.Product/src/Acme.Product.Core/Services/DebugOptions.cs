// DebugOptions.cs
// 输出数据快照
// 作者：蘅芜君

namespace Acme.Product.Core.Services;

/// <summary>
/// 调试选项 - 用于流水线调试模式
/// </summary>
public class DebugOptions
{
    /// <summary>
    /// 设置了断点的算子ID列表
    /// </summary>
    public HashSet<Guid> Breakpoints { get; set; } = new();

    /// <summary>
    /// 是否仅执行到下一个断点
    /// </summary>
    public bool StepMode { get; set; } = false;

    /// <summary>
    /// 保存中间结果的图像格式
    /// </summary>
    public string ImageFormat { get; set; } = ".png";

    /// <summary>
    /// 是否启用中间结果缓存
    /// </summary>
    public bool EnableIntermediateCache { get; set; } = true;

    /// <summary>
    /// 调试会话ID
    /// </summary>
    public Guid DebugSessionId { get; set; } = Guid.NewGuid();
}

/// <summary>
/// 调试执行结果
/// </summary>
public class FlowDebugExecutionResult : FlowExecutionResult
{
    /// <summary>
    /// 调试会话ID
    /// </summary>
    public Guid DebugSessionId { get; set; }

    /// <summary>
    /// 是否命中断点
    /// </summary>
    public bool BreakpointHit { get; set; }

    /// <summary>
    /// 当前暂停的算子ID
    /// </summary>
    public Guid? PausedOperatorId { get; set; }

    /// <summary>
    /// 中间结果缓存 - Key: 算子ID, Value: 算子输出数据
    /// </summary>
    public Dictionary<Guid, Dictionary<string, object>> IntermediateResults { get; set; } = new();

    /// <summary>
    /// 每个算子的执行详情
    /// </summary>
    public List<OperatorDebugResult> DebugOperatorResults { get; set; } = new();
}

/// <summary>
/// 算子调试结果
/// </summary>
public class OperatorDebugResult : OperatorExecutionResult
{
    /// <summary>
    /// 算子在执行顺序中的索引
    /// </summary>
    public int ExecutionOrder { get; set; }

    /// <summary>
    /// 执行开始时间
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 执行结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 是否命中断点
    /// </summary>
    public bool IsBreakpoint { get; set; }

    /// <summary>
    /// 输入数据快照
    /// </summary>
    public Dictionary<string, object>? InputSnapshot { get; set; }

    /// <summary>
    /// 输出数据快照
    /// </summary>
    public Dictionary<string, object>? OutputSnapshot { get; set; }
}
