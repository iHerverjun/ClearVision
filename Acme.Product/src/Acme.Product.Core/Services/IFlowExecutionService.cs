// IFlowExecutionService.cs
// 开始时间
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.ValueObjects;

namespace Acme.Product.Core.Services;

/// <summary>
/// 流程执行服务接口
/// </summary>
public interface IFlowExecutionService
{
    /// <summary>
    /// 执行算子流程
    /// </summary>
    /// <param name="flow">算子流程</param>
    /// <param name="inputData">输入数据</param>
    /// <param name="enableParallel">是否启用并行执行</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果</returns>
    Task<FlowExecutionResult> ExecuteFlowAsync(OperatorFlow flow, Dictionary<string, object>? inputData = null, bool enableParallel = false, System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行单个算子
    /// </summary>
    /// <param name="operator">算子</param>
    /// <param name="inputs">输入数据</param>
    /// <returns>算子执行结果</returns>
    Task<OperatorExecutionResult> ExecuteOperatorAsync(Operator @operator, Dictionary<string, object>? inputs = null);

    /// <summary>
    /// 验证流程有效性
    /// </summary>
    /// <param name="flow">算子流程</param>
    /// <returns>验证结果</returns>
    FlowValidationResult ValidateFlow(OperatorFlow flow);

    /// <summary>
    /// 获取流程执行状态
    /// </summary>
    /// <param name="flowId">流程ID</param>
    /// <returns>执行状态</returns>
    FlowExecutionStatus? GetExecutionStatus(Guid flowId);

    /// <summary>
    /// 取消流程执行
    /// </summary>
    /// <param name="flowId">流程ID</param>
    Task CancelExecutionAsync(Guid flowId);

    /// <summary>
    /// 调试执行流程 - 支持断点和单步执行
    /// </summary>
    /// <param name="flow">算子流程</param>
    /// <param name="options">调试选项</param>
    /// <param name="inputData">输入数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>调试执行结果</returns>
    Task<FlowDebugExecutionResult> ExecuteFlowDebugAsync(
        OperatorFlow flow,
        DebugOptions options,
        Dictionary<string, object>? inputData = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取调试中间结果
    /// </summary>
    /// <param name="debugSessionId">调试会话ID</param>
    /// <param name="operatorId">算子ID</param>
    /// <returns>中间结果数据</returns>
    Dictionary<string, object>? GetDebugIntermediateResult(Guid debugSessionId, Guid operatorId);

    /// <summary>
    /// 清除调试缓存
    /// </summary>
    /// <param name="debugSessionId">调试会话ID</param>
    Task ClearDebugCacheAsync(Guid debugSessionId);
}

/// <summary>
/// 流程执行结果
/// </summary>
public class FlowExecutionResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 执行时间（毫秒）
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// 输出数据
    /// </summary>
    public Dictionary<string, object>? OutputData { get; set; }

    /// <summary>
    /// 各算子执行结果
    /// </summary>
    public List<OperatorExecutionResult> OperatorResults { get; set; } = new();

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 算子执行结果
/// </summary>
public class OperatorExecutionResult
{
    /// <summary>
    /// 算子ID
    /// </summary>
    public Guid OperatorId { get; set; }

    /// <summary>
    /// 算子名称
    /// </summary>
    public string OperatorName { get; set; } = string.Empty;

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 执行时间（毫秒）
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// 输出数据
    /// </summary>
    public Dictionary<string, object>? OutputData { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 流程验证结果
/// </summary>
public class FlowValidationResult
{
    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 错误信息列表
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// 警告信息列表
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// 流程执行状态
/// </summary>
public class FlowExecutionStatus
{
    /// <summary>
    /// 流程ID
    /// </summary>
    public Guid FlowId { get; set; }

    /// <summary>
    /// 是否正在执行
    /// </summary>
    public bool IsExecuting { get; set; }

    /// <summary>
    /// 当前执行的算子ID
    /// </summary>
    public Guid? CurrentOperatorId { get; set; }

    /// <summary>
    /// 进度百分比（0-100）
    /// </summary>
    public double ProgressPercentage { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartTime { get; set; }
}
