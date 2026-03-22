// FlowExecutionService.cs
// 流程执行服务实现
// 作者：蘅芜君

using System.Collections;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.Services;
using Acme.Product.Infrastructure.Logging;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// 流程执行服务实现
/// </summary>
public class FlowExecutionService : IFlowExecutionService, IDisposable
{
    private static readonly TimeSpan DebugCleanupInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DebugSessionTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ExecutionStatusTtl = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<Guid, FlowExecutionStatus> _executionStatuses = new();
    private readonly Dictionary<OperatorType, IOperatorExecutor> _executors;
    private readonly ILogger<FlowExecutionService> _logger;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _executionCancellations = new();
    private readonly IVariableContext _variableContext;

    // 调试模式：缓存中间结果 - Key: (DebugSessionId, OperatorId)
    private readonly ConcurrentDictionary<(Guid DebugSessionId, Guid OperatorId), Dictionary<string, object>> _debugCache = new();
    private readonly ConcurrentDictionary<(Guid DebugSessionId, Guid OperatorId), string> _debugCacheFingerprints = new();
    private readonly ConcurrentDictionary<Guid, DebugOptions> _debugOptions = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _debugSessionLastAccess = new();
    private readonly Timer _debugCacheCleanupTimer;
    private bool _disposed;

    public FlowExecutionService(
        IEnumerable<IOperatorExecutor> executors,
        ILogger<FlowExecutionService> logger,
        IVariableContext variableContext)
    {
        _executors = executors.ToDictionary(e => e.OperatorType);
        _logger = logger;
        _variableContext = variableContext;
        _debugCacheCleanupTimer = new Timer(CleanupStaleDebugSessions, null, DebugCleanupInterval, DebugCleanupInterval);
    }

    public async Task<FlowExecutionResult> ExecuteFlowAsync(
        OperatorFlow flow,
        Dictionary<string, object>? inputData = null,
        bool enableParallel = false,
        CancellationToken cancellationToken = default)
    {
        // 【第三优先级】递增循环计数器
        _variableContext.IncrementCycleCount();
        _logger.LogDebug("[FlowExecution] 循环计数: {CycleCount}", _variableContext.CycleCount);

        var result = new FlowExecutionResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 创建链接的 CancellationTokenSource
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _executionCancellations[flow.Id] = cts;

        try
        {
            // Sprint 1 Task 1.1: 预分析扇出度，为 ImageWrapper 引用计数做准备
            var fanOutDegrees = AnalyzeFanOutDegrees(flow);

            // 获取执行顺序（拓扑排序）
            var executionOrder = flow.GetExecutionOrder().ToList();

            // 初始化执行状态
            var status = new FlowExecutionStatus
            {
                FlowId = flow.Id,
                IsExecuting = true,
                StartTime = DateTime.UtcNow,
                ProgressPercentage = 0
            };
            _executionStatuses[flow.Id] = status;

            // 存储每个算子的输出 - 使用 ConcurrentDictionary 支持并行执行
            var operatorOutputs = new ConcurrentDictionary<Guid, Dictionary<string, object>>();

            // 设置初始输入数据
            if (inputData != null)
            {
                operatorOutputs[Guid.Empty] = inputData;
            }

            if (enableParallel && executionOrder.Count > 1)
            {
                // 并行执行模式
                await ExecuteFlowParallelAsync(flow, executionOrder, operatorOutputs, result, status, cts.Token, fanOutDegrees);
            }
            else
            {
                // 顺序执行模式
                await ExecuteFlowSequentialAsync(flow, executionOrder, operatorOutputs, result, status, cts.Token, fanOutDegrees);
            }

            stopwatch.Stop();
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            // 检查是否因为取消而中断
            if (cts.Token.IsCancellationRequested)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Flow was canceled.";
            }
            else
            {
                result.IsSuccess = result.OperatorResults.All(r => r.IsSuccess);
            }

            // 记录流程执行完成日志
            _logger.LogFlowExecution(flow.Id, executionOrder.Count, stopwatch.ElapsedMilliseconds, result.IsSuccess);

            // 获取最后一个算子的输出作为流程输出
            if (executionOrder.Any() && operatorOutputs.ContainsKey(executionOrder.Last().Id))
            {
                result.OutputData = ConvertImageWrappersToBytes(operatorOutputs[executionOrder.Last().Id]);
            }

            status.IsExecuting = false;
            status.ProgressPercentage = 100;
            status.CompletedAt = DateTime.UtcNow;

            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            result.IsSuccess = false;
            result.ErrorMessage = "Flow was canceled.";
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.IsSuccess = false;
            result.ErrorMessage = $"流程执行异常: {ex.Message}";
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            _logger.LogError(ex, "流程执行异常: {FlowId}", flow.Id);
            return result;
        }
        finally
        {
            // 清理 CancellationTokenSource
            if (_executionCancellations.TryRemove(flow.Id, out var removedCts))
            {
                removedCts.Dispose();
            }

            if (_executionStatuses.TryGetValue(flow.Id, out var finalStatus))
            {
                finalStatus.IsExecuting = false;
                finalStatus.ProgressPercentage = 100;
                finalStatus.CompletedAt ??= DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// 顺序执行流程
    /// </summary>
    private async Task ExecuteFlowSequentialAsync(
        OperatorFlow flow,
        List<Operator> executionOrder,
        ConcurrentDictionary<Guid, Dictionary<string, object>> operatorOutputs,
        FlowExecutionResult result,
        FlowExecutionStatus status,
        CancellationToken cancellationToken,
        Dictionary<string, int>? fanOutDegrees = null)
    {
        int completedCount = 0;
        foreach (var op in executionOrder)
        {
            // 检查取消
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (!TryResolveExecutor(op.Type, out var executor))
            {
                result.OperatorResults.Add(new OperatorExecutionResult
                {
                    OperatorId = op.Id,
                    OperatorName = op.Name,
                    IsSuccess = false,
                    ErrorMessage = $"未找到类型为 {op.Type} 的算子执行器"
                });
                continue;
            }

            // 更新当前执行状态
            status.CurrentOperatorId = op.Id;
            status.ProgressPercentage = (double)completedCount / executionOrder.Count * 100;

            // 准备输入数据
            var inputs = PrepareOperatorInputs(flow, op, operatorOutputs);

            // 执行算子
            var opResult = await ExecuteOperatorInternalAsync(op, executor, inputs, cancellationToken);
            result.OperatorResults.Add(opResult);

            if (!opResult.IsSuccess)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"算子 '{op.Name}' 执行失败: {opResult.ErrorMessage}";
                break;
            }

            var outputs = opResult.OutputData ?? new Dictionary<string, object>();
            operatorOutputs[op.Id] = outputs;

            // Sprint 1 Task 1.1: 应用扇出引用计数
            if (fanOutDegrees != null)
            {
                ApplyFanOutRefCounts(op, outputs, fanOutDegrees);
            }

            completedCount++;
        }
    }

    /// <summary>
    /// 并行执行流程 - 按层级并行执行无依赖的算子
    /// </summary>
    private async Task ExecuteFlowParallelAsync(
        OperatorFlow flow,
        List<Operator> executionOrder,
        ConcurrentDictionary<Guid, Dictionary<string, object>> operatorOutputs,
        FlowExecutionResult result,
        FlowExecutionStatus status,
        CancellationToken cancellationToken,
        Dictionary<string, int>? fanOutDegrees = null)
    {
        // 构建执行层级（哪些算子可以并行执行）
        var executionLayers = BuildExecutionLayers(flow, executionOrder);
        var completedOperators = new HashSet<Guid>();
        var failed = false;

        foreach (var layer in executionLayers)
        {
            if (failed || cancellationToken.IsCancellationRequested)
                break;

            // 更新状态
            status.CurrentOperatorId = layer.First().Id;
            status.ProgressPercentage = (double)completedOperators.Count / executionOrder.Count * 100;

            // 并行执行当前层的所有算子
            var layerTasks = layer.Select(async op =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return new OperatorExecutionResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Flow was canceled.",
                        OperatorId = op.Id,
                        OperatorName = op.Name
                    };
                }

                if (!TryResolveExecutor(op.Type, out var executor))
                {
                    return new OperatorExecutionResult
                    {
                        OperatorId = op.Id,
                        OperatorName = op.Name,
                        IsSuccess = false,
                        ErrorMessage = $"未找到类型为 {op.Type} 的算子执行器"
                    };
                }

                // 准备输入数据
                var inputs = PrepareOperatorInputs(flow, op, operatorOutputs);

                // 执行算子
                var opResult = await ExecuteOperatorInternalAsync(op, executor, inputs, cancellationToken);

                if (opResult.IsSuccess)
                {
                    var outputs = opResult.OutputData ?? new Dictionary<string, object>();
                    operatorOutputs[op.Id] = outputs;

                    // Sprint 1 Task 1.1: 应用扇出引用计数
                    if (fanOutDegrees != null)
                    {
                        ApplyFanOutRefCounts(op, outputs, fanOutDegrees);
                    }
                }

                return opResult;
            }).ToList();

            // 等待当前层所有算子执行完成
            var layerResults = await Task.WhenAll(layerTasks);
            result.OperatorResults.AddRange(layerResults);

            // 检查是否有失败的算子
            if (layerResults.Any(r => !r.IsSuccess))
            {
                failed = true;
                var failedOp = layerResults.First(r => !r.IsSuccess);
                result.IsSuccess = false;
                result.ErrorMessage = cancellationToken.IsCancellationRequested
                    ? "Flow was canceled."
                    : $"算子 '{failedOp.OperatorName}' 执行失败: {failedOp.ErrorMessage}";
            }

            foreach (var op in layer)
            {
                completedOperators.Add(op.Id);
            }
        }
    }

    /// <summary>
    /// 构建执行层级 - 将算子分组，同一层的算子可以并行执行
    /// </summary>
    private List<List<Operator>> BuildExecutionLayers(OperatorFlow flow, List<Operator> executionOrder)
    {
        var layers = new List<List<Operator>>();
        var executed = new HashSet<Guid>();
        var remaining = new HashSet<Operator>(executionOrder);

        while (remaining.Any())
        {
            // 找出当前可以执行的算子（所有依赖都已执行）
            var currentLayer = remaining.Where(op =>
            {
                // 获取该算子的所有依赖（输入连接）
                var dependencies = flow.Connections
                    .Where(c => c.TargetOperatorId == op.Id)
                    .Select(c => c.SourceOperatorId);

                // 检查所有依赖是否已执行
                return dependencies.All(depId => executed.Contains(depId));
            }).ToList();

            if (!currentLayer.Any())
            {
                // 如果没有可以执行的算子，说明有循环依赖或其他问题
                // 将剩余的算子作为一个层级执行
                currentLayer = remaining.ToList();
            }

            layers.Add(currentLayer);

            foreach (var op in currentLayer)
            {
                executed.Add(op.Id);
                remaining.Remove(op);
            }
        }

        return layers;
    }

    // 默认算子执行超时时间（30秒）
    private const int DefaultOperatorTimeoutMs = 30000;

    /// <summary>
    /// 内部执行单个算子（带超时保护）
    /// </summary>
    private async Task<OperatorExecutionResult> ExecuteOperatorInternalAsync(
        Operator op,
        IOperatorExecutor executor,
        Dictionary<string, object> inputs,
        CancellationToken cancellationToken = default)
    {
        op.MarkExecutionStarted();
        var opStopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // 为算子执行添加全局超时保护
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(DefaultOperatorTimeoutMs));

            // O1.3: 传递 CancellationToken 给算子执行器，支持取消操作
            var opResult = await executor.ExecuteAsync(op, inputs, timeoutCts.Token);
            opStopwatch.Stop();

            if (cancellationToken.IsCancellationRequested)
            {
                return new OperatorExecutionResult
                {
                    OperatorId = op.Id,
                    OperatorName = op.Name,
                    IsSuccess = false,
                    ExecutionTimeMs = opStopwatch.ElapsedMilliseconds,
                    ErrorMessage = "Operator execution was canceled."
                };
            }

            if (opResult.IsSuccess)
            {
                op.MarkExecutionCompleted(opStopwatch.ElapsedMilliseconds);
                _logger.LogOperatorExecution(op.Id, op.Name, opStopwatch.ElapsedMilliseconds, true);

                return new OperatorExecutionResult
                {
                    OperatorId = op.Id,
                    OperatorName = op.Name,
                    IsSuccess = true,
                    ExecutionTimeMs = opStopwatch.ElapsedMilliseconds,
                    OutputData = opResult.OutputData
                };
            }
            else
            {
                op.MarkExecutionFailed(opResult.ErrorMessage ?? "未知错误");
                _logger.LogOperatorExecution(op.Id, op.Name, opStopwatch.ElapsedMilliseconds, false);
                _logger.LogError("算子执行失败: {OperatorName} ({OperatorId}), 错误: {ErrorMessage}",
                    op.Name, op.Id, opResult.ErrorMessage);

                return new OperatorExecutionResult
                {
                    OperatorId = op.Id,
                    OperatorName = op.Name,
                    IsSuccess = false,
                    ExecutionTimeMs = opStopwatch.ElapsedMilliseconds,
                    ErrorMessage = opResult.ErrorMessage
                };
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            opStopwatch.Stop();
            op.MarkExecutionFailed($"Operator timed out ({DefaultOperatorTimeoutMs / 1000}s)");
            _logger.LogError("算子执行超时: {OperatorName} ({OperatorId})", op.Name, op.Id);

            return new OperatorExecutionResult
            {
                OperatorId = op.Id,
                OperatorName = op.Name,
                IsSuccess = false,
                ExecutionTimeMs = opStopwatch.ElapsedMilliseconds,
                ErrorMessage = $"Operator '{op.Name}' timed out ({DefaultOperatorTimeoutMs / 1000}s)"
            };
        }
        catch (Exception ex)
        {
            opStopwatch.Stop();
            op.MarkExecutionFailed(ex.Message);
            _logger.LogError(ex, "算子执行异常: {OperatorName} ({OperatorId})", op.Name, op.Id);

            return new OperatorExecutionResult
            {
                OperatorId = op.Id,
                OperatorName = op.Name,
                IsSuccess = false,
                ExecutionTimeMs = opStopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<OperatorExecutionResult> ExecuteOperatorAsync(Operator @operator, Dictionary<string, object>? inputs = null)
    {
        if (!TryResolveExecutor(@operator.Type, out var executor))
        {
            return new OperatorExecutionResult
            {
                OperatorId = @operator.Id,
                OperatorName = @operator.Name,
                IsSuccess = false,
                ErrorMessage = $"未找到类型为 {@operator.Type} 的算子执行器"
            };
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // 对于单独执行算子，目前不支持取消
            var opResult = await executor.ExecuteAsync(@operator, inputs ?? new Dictionary<string, object>());
            stopwatch.Stop();

            _logger.LogOperatorExecution(@operator.Id, @operator.Name, stopwatch.ElapsedMilliseconds, opResult.IsSuccess);

            return new OperatorExecutionResult
            {
                OperatorId = @operator.Id,
                OperatorName = @operator.Name,
                IsSuccess = opResult.IsSuccess,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                OutputData = opResult.OutputData,
                ErrorMessage = opResult.ErrorMessage
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "算子执行异常: {OperatorName} ({OperatorId})", @operator.Name, @operator.Id);
            return new OperatorExecutionResult
            {
                OperatorId = @operator.Id,
                OperatorName = @operator.Name,
                IsSuccess = false,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message
            };
        }
    }

    public FlowValidationResult ValidateFlow(OperatorFlow flow)
    {
        var result = new FlowValidationResult();

        // 检查是否有算子
        if (!flow.Operators.Any())
        {
            result.Errors.Add("流程中没有任何算子");
            return result;
        }

        // 检查是否有图像采集算子作为输入
        var hasInputOperator = flow.Operators.Any(o => o.Type == OperatorType.ImageAcquisition);
        if (!hasInputOperator)
        {
            result.Warnings.Add("流程缺少图像采集算子作为输入");
        }

        // 检查是否有结果输出算子
        var hasOutputOperator = flow.Operators.Any(o => o.Type == OperatorType.ResultOutput);
        if (!hasOutputOperator)
        {
            result.Warnings.Add("流程缺少结果输出算子");
        }

        // 验证每个算子的参数
        foreach (var op in flow.Operators)
        {
            if (TryResolveExecutor(op.Type, out var executor))
            {
                var validation = executor.ValidateParameters(op);
                if (!validation.IsValid)
                {
                    foreach (var error in validation.Errors)
                    {
                        result.Errors.Add($"算子 '{op.Name}': {error}");
                    }
                }
            }
        }

        result.IsValid = !result.Errors.Any();
        return result;
    }

    public FlowExecutionStatus? GetExecutionStatus(Guid flowId)
    {
        return _executionStatuses.TryGetValue(flowId, out var status) ? status : null;
    }

    private bool TryResolveExecutor(OperatorType operatorType, out IOperatorExecutor executor)
    {
        if (_executors.TryGetValue(operatorType, out executor!))
        {
            return true;
        }

        var resolvedType = OperatorTypeAliasResolver.Resolve(operatorType);
        if (resolvedType != operatorType && _executors.TryGetValue(resolvedType, out executor!))
        {
            return true;
        }

        executor = null!;
        return false;
    }

    public Task CancelExecutionAsync(Guid flowId)
    {
        if (_executionCancellations.TryGetValue(flowId, out var cts))
        {
            try
            {
                cts.Cancel();
                _logger.LogInformation("Cancellation requested for flow: {FlowId}", flowId);
            }
            catch (ObjectDisposedException)
            {
                // 忽略已释放的对象异常
            }
        }

        if (_executionStatuses.TryGetValue(flowId, out var status))
        {
            status.IsExecuting = false;
            status.CompletedAt = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// <summary>
    /// 规范化流程输出，避免将 OpenCvSharp.Mat 等非 JSON 安全对象直接暴露到上层。
    /// </summary>
    private Dictionary<string, object> ConvertImageWrappersToBytes(Dictionary<string, object>? outputData)
    {
        if (outputData == null)
            return new Dictionary<string, object>();

        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in outputData)
        {
            if (TryNormalizeOutputValue(kvp.Value, out var normalized))
            {
                result[kvp.Key] = normalized!;
            }
        }
        return result;
    }

    private static bool TryNormalizeOutputValue(object? value, out object? normalized, int depth = 0)
    {
        const int maxDepth = 8;
        if (depth > maxDepth)
        {
            normalized = value?.ToString();
            return normalized != null;
        }

        switch (value)
        {
            case null:
                normalized = null;
                return true;
            case ImageWrapper wrapper:
                normalized = wrapper.GetBytes();
                return true;
            case Mat mat:
                normalized = mat.ToBytes(".png");
                return true;
            case byte[] bytes:
                normalized = bytes;
                return true;
            case string or bool or char or sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal or DateTime or DateTimeOffset or TimeSpan or Guid:
                normalized = value;
                return true;
        }

        var type = value.GetType();
        if (type.IsEnum)
        {
            normalized = value.ToString() ?? string.Empty;
            return true;
        }

        if (value is IDictionary<string, object> typedDict)
        {
            var dictResult = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, dictValue) in typedDict)
            {
                if (TryNormalizeOutputValue(dictValue, out var child, depth + 1))
                {
                    dictResult[key] = child;
                }
            }

            normalized = dictResult;
            return true;
        }

        if (value is IDictionary dictionary)
        {
            var dictResult = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = entry.Key?.ToString();
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (TryNormalizeOutputValue(entry.Value, out var child, depth + 1))
                {
                    dictResult[key] = child;
                }
            }

            normalized = dictResult;
            return true;
        }

        if (value is IEnumerable enumerable)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
            {
                if (TryNormalizeOutputValue(item, out var child, depth + 1))
                {
                    list.Add(child);
                }
            }

            normalized = list;
            return true;
        }

        try
        {
            JsonSerializer.Serialize(value);
            normalized = value;
            return true;
        }
        catch
        {
            normalized = value.ToString();
            return normalized != null;
        }
    }

    #region Sprint 1 Task 1.1: 扇出预分析与引用计数管理

    /// <summary>
    /// 预分析 DAG 中每个输出端口的扇出度（下游连接数）。
    /// 用于决定 ImageWrapper 的引用计数初始值。
    /// </summary>
    private Dictionary<string, int> AnalyzeFanOutDegrees(OperatorFlow flow)
    {
        var targetsBySourcePort = new Dictionary<string, HashSet<Guid>>();
        foreach (var conn in flow.Connections)
        {
            var key = $"{conn.SourceOperatorId}:{conn.SourcePortId}";
            if (!targetsBySourcePort.TryGetValue(key, out var targets))
            {
                targets = new HashSet<Guid>();
                targetsBySourcePort[key] = targets;
            }

            // One downstream operator executes once even if multiple input ports are wired
            // to the same source output port, so fan-out should be counted by unique consumers.
            targets.Add(conn.TargetOperatorId);
        }

        return targetsBySourcePort.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count);
    }

    /// <summary>
    /// 根据扇出度为算子输出的 ImageWrapper 设置引用计数。
    /// 扇出度为 N 时，AddRef (N-1) 次，使总引用计数为 N。
    /// </summary>
    private void ApplyFanOutRefCounts(
        Operator op,
        Dictionary<string, object> outputs,
        Dictionary<string, int> fanOutDegrees)
    {
        foreach (var (portName, value) in outputs)
        {
            if (value is not ImageWrapper img)
                continue;

            // 尝试通过名称查找端口 ID，以匹配扇出度分析使用的 Key
            var port = op.OutputPorts.FirstOrDefault(p => p.Name == portName);
            var portKey = port != null
                ? $"{op.Id}:{port.Id}"
                : $"{op.Id}:{portName}";

            int fanOut = fanOutDegrees.GetValueOrDefault(portKey, 1);

            // 引用计数初始为 1，每多一个下游消费者 AddRef 一次
            for (int i = 1; i < fanOut; i++)
            {
                img.AddRef();
            }

            _logger.LogDebug("[FlowExecution] Set ref count: Operator={OperatorName}, Port={PortName}, FanOut={FanOut}, RefCount={RefCount}",
                op.Name, portName, fanOut, img.RefCount);
        }
    }

    #endregion

    private Dictionary<string, object> PrepareOperatorInputs(OperatorFlow flow, Operator op, IDictionary<Guid, Dictionary<string, object>> operatorOutputs)
    {
        var inputs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        // 1. 【基础注入】首先将算子自身的参数合并到输入中作为默认值。
        // 这确保了如果没有外部连线，算子依然能拿到 UI 属性面板设置的参数（例如 filePath）。
        foreach (var param in op.Parameters)
        {
            if (param.Value != null)
            {
                inputs[param.Name] = param.Value;
            }
        }

        // 查找连接到该算子的所有连线
        var incomingConnections = flow.Connections
            .Where(c => c.TargetOperatorId == op.Id)
            .ToList();

        // 如果没有输入连接，尝试从初始输入数据获取 (Guid.Empty)
        if (!incomingConnections.Any())
        {
            if (operatorOutputs.TryGetValue(Guid.Empty, out var initialInputs))
            {
                foreach (var kvp in initialInputs)
                {
                    // Use case-insensitive key matching to avoid "image" vs "Image" mismatches.
                    if (!inputs.ContainsKey(kvp.Key))
                    {
                        inputs[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
        else
        {
            foreach (var connection in incomingConnections)
            {
                if (operatorOutputs.TryGetValue(connection.SourceOperatorId, out var sourceOutputs))
                {
                    // 【条件分支路由修复】检查源算子是否为条件分支算子
                    var sourceOperator = flow.Operators.FirstOrDefault(o => o.Id == connection.SourceOperatorId);

                    if (sourceOperator?.Type == OperatorType.ConditionalBranch)
                    {
                        // 对于条件分支算子，只传递与连接端口名称匹配的数据
                        // 获取源端口名称（True / False）
                        var sourcePort = sourceOperator.OutputPorts.FirstOrDefault(p => p.Id == connection.SourcePortId);
                        if (sourcePort != null)
                        {
                            var portName = sourcePort.Name;
                            var targetPort = op.InputPorts.FirstOrDefault(p => p.Id == connection.TargetPortId);
                            // 检查输出数据中是否有对应端口的数据且不为null
                            if (sourceOutputs.TryGetValue(portName, out var portData) && portData != null)
                            {
                                if (targetPort != null)
                                {
                                    inputs[targetPort.Name] = portData;
                                }

                                if (!inputs.ContainsKey(portName))
                                {
                                    inputs[portName] = portData;
                                }
                                // 同时传递判断结果等通用信息
                                if (sourceOutputs.TryGetValue("Result", out var result))
                                    inputs["ConditionResult"] = result;
                                if (sourceOutputs.TryGetValue("Condition", out var condition))
                                    inputs["Condition"] = condition;
                                if (sourceOutputs.TryGetValue("ActualValue", out var actualValue))
                                    inputs["ActualValue"] = actualValue;
                            }
                            // 如果端口数据为 null，说明条件分支走的是另一分支，不传递任何数据
                        }
                    }
                    else
                    {
                        // 普通算子：执行增强的端口映射逻辑

                        // 尝试获取连线两端的端口定义
                        // 注意：SourceOperator 可能不在当前上下文（虽然不太可能），但我们要防御性编程
                        if (sourceOperator != null)
                        {
                            var sourcePort = sourceOperator.OutputPorts.FirstOrDefault(p => p.Id == connection.SourcePortId);
                            var targetPort = op.InputPorts.FirstOrDefault(p => p.Id == connection.TargetPortId);

                            // 【Bug 4 修复】基于端口名称的精确映射
                            if (sourcePort != null && targetPort != null)
                            {
                                // 尝试从源输出中获取与源端口名匹配的数据
                                if (sourceOutputs.TryGetValue(sourcePort.Name, out var data))
                                {
                                    // 将数据映射到目标端口
                                    // 例如：源输出 "Image" -> 目标输入 "Background"
                                    inputs[targetPort.Name] = data;
                                }
                            }
                        }

                        // 【兼容性兜底】
                        // 如果没有通过端口成功映射（可能是旧版数据、端口名未定义、或旨在传递隐式数据）
                        // 或者为了向后兼容（防止某些未走端口定义的隐式数据丢失，避免 ResultOutput 所需的额外信息缺失）
                        // 我们依然执行全量合并，但跳过已存在的键（避免覆盖精确映射的结果）
                        foreach (var kvp in sourceOutputs)
                        {
                            if (!inputs.ContainsKey(kvp.Key))
                            {
                                // Never implicitly propagate reference-counted image payloads across
                                // unrelated ports. This avoids hidden ImageWrapper consumers that are
                                // invisible to fan-out analysis and can cause premature disposal.
                                if (ShouldSkipImplicitFallbackValue(kvp.Key, kvp.Value, sourceOperator, connection.SourcePortId))
                                {
                                    _logger.LogDebug(
                                        "[FlowExecution] Skip implicit fallback key '{Key}' from {SourceOperator} to {TargetOperator} to avoid hidden ImageWrapper propagation.",
                                        kvp.Key,
                                        sourceOperator?.Name ?? connection.SourceOperatorId.ToString(),
                                        op.Name);
                                    continue;
                                }

                                inputs[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                }
            }
        }

        return inputs;
    }

    private static bool ShouldSkipImplicitFallbackValue(
        string key,
        object? value,
        Operator? sourceOperator,
        Guid sourcePortId)
    {
        if (!ContainsImageWrapperReference(value))
            return false;

        if (sourceOperator == null)
            return true;

        var connectedSourcePort = sourceOperator.OutputPorts.FirstOrDefault(p => p.Id == sourcePortId);
        if (connectedSourcePort == null)
            return true;

        // Exact match: port name == key name (original logic)
        if (string.Equals(connectedSourcePort.Name, key, StringComparison.OrdinalIgnoreCase))
            return false;

        // Supplemental match: if this operator has only one output port and the key is a
        // well-known image output key produced by OperatorBase.CreateImageOutput, treat it
        // as the port's actual output rather than an implicit propagation.
        // This handles the common mismatch where port is named "Output" but the data key is "Image".
        if (sourceOperator.OutputPorts.Count == 1 && IsStandardImageOutputKey(key))
            return false;

        return true;
    }

    /// <summary>
    /// Returns true for well-known output keys produced by OperatorBase.CreateImageOutput
    /// and other standard operator output methods.
    /// </summary>
    private static bool IsStandardImageOutputKey(string key)
    {
        return string.Equals(key, "Image", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "Edges", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "Mask", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsImageWrapperReference(object? value, int depth = 0)
    {
        const int maxDepth = 6;
        if (value == null || depth > maxDepth)
            return false;

        if (value is ImageWrapper)
            return true;

        if (value is string or byte[] or Mat)
            return false;

        if (value is IDictionary<string, object> typedDict)
        {
            return typedDict.Values.Any(child => ContainsImageWrapperReference(child, depth + 1));
        }

        if (value is IDictionary dict)
        {
            foreach (DictionaryEntry entry in dict)
            {
                if (ContainsImageWrapperReference(entry.Value, depth + 1))
                    return true;
            }
            return false;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (ContainsImageWrapperReference(item, depth + 1))
                    return true;
            }
        }

        return false;
    }

    #region 调试功能实现

    /// <summary>
    /// 调试执行流程 - 支持断点和单步执行
    /// </summary>
    public async Task<FlowDebugExecutionResult> ExecuteFlowDebugAsync(
        OperatorFlow flow,
        DebugOptions options,
        Dictionary<string, object>? inputData = null,
        CancellationToken cancellationToken = default)
    {
        var result = new FlowDebugExecutionResult
        {
            DebugSessionId = options.DebugSessionId
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 保存调试选项
        _debugOptions[options.DebugSessionId] = options;
        TouchDebugSession(options.DebugSessionId);

        // 创建链接的 CancellationTokenSource
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _executionCancellations[flow.Id] = cts;

        try
        {
            // 获取执行顺序（拓扑排序）
            var executionOrder = flow.GetExecutionOrder().ToList();
            var fanOutDegrees = AnalyzeFanOutDegrees(flow);

            // 初始化执行状态
            var status = new FlowExecutionStatus
            {
                FlowId = flow.Id,
                IsExecuting = true,
                StartTime = DateTime.UtcNow,
                ProgressPercentage = 0
            };
            _executionStatuses[flow.Id] = status;

            // 存储每个算子的输出
            var operatorOutputs = new ConcurrentDictionary<Guid, Dictionary<string, object>>();

            // 设置初始输入数据
            if (inputData != null)
            {
                operatorOutputs[Guid.Empty] = inputData;
            }

            // 顺序执行（调试模式不支持并行）
            int completedCount = 0;
            Guid? pausedOperatorId = null;

            foreach (var op in executionOrder)
            {
                // 检查取消
                if (cts.Token.IsCancellationRequested)
                {
                    break;
                }

                // 检查是否命中断点
                if (options.Breakpoints.Contains(op.Id))
                {
                    pausedOperatorId = op.Id;
                    result.BreakpointHit = true;
                    result.PausedOperatorId = pausedOperatorId;
                    _logger.LogInformation("[调试] 命中断点: {OperatorName} ({OperatorId})", op.Name, op.Id);

                    if (options.StepMode)
                    {
                        // 单步模式：暂停执行
                        break;
                    }
                }

                if (!TryResolveExecutor(op.Type, out var executor))
                {
                    var debugResult = new OperatorDebugResult
                    {
                        OperatorId = op.Id,
                        OperatorName = op.Name,
                        IsSuccess = false,
                        ErrorMessage = $"未找到类型为 {op.Type} 的算子执行器",
                        ExecutionOrder = completedCount,
                        IsBreakpoint = options.Breakpoints.Contains(op.Id)
                    };
                    result.DebugOperatorResults.Add(debugResult);
                    result.OperatorResults.Add(debugResult);
                    continue;
                }

                // 更新当前执行状态
                status.CurrentOperatorId = op.Id;
                status.ProgressPercentage = (double)completedCount / executionOrder.Count * 100;

                // 准备输入数据
                var inputs = PrepareOperatorInputs(flow, op, operatorOutputs);
                var normalizedInputSnapshot = ConvertImageWrappersToBytes(inputs);
                var cacheKey = (options.DebugSessionId, op.Id);
                var cacheFingerprint = CreateDebugCacheFingerprint(op, normalizedInputSnapshot);

                if (options.EnableIntermediateCache &&
                    _debugCache.TryGetValue(cacheKey, out var cachedOutputs) &&
                    _debugCacheFingerprints.TryGetValue(cacheKey, out var cachedFingerprint) &&
                    string.Equals(cachedFingerprint, cacheFingerprint, StringComparison.Ordinal))
                {
                    var cachedCopy = CloneNormalizedDictionary(cachedOutputs);
                    operatorOutputs[op.Id] = cachedCopy;

                    var cachedDebugResult = new OperatorDebugResult
                    {
                        OperatorId = op.Id,
                        OperatorName = op.Name,
                        IsSuccess = true,
                        ExecutionTimeMs = 0,
                        ExecutionOrder = completedCount,
                        StartTime = DateTime.UtcNow,
                        EndTime = DateTime.UtcNow,
                        IsBreakpoint = options.Breakpoints.Contains(op.Id),
                        InputSnapshot = CloneNormalizedDictionary(normalizedInputSnapshot),
                        OutputData = CloneNormalizedDictionary(cachedCopy),
                        OutputSnapshot = CloneNormalizedDictionary(cachedCopy)
                    };

                    result.DebugOperatorResults.Add(cachedDebugResult);
                    result.OperatorResults.Add(cachedDebugResult);
                    result.IntermediateResults[op.Id] = CloneNormalizedDictionary(cachedCopy);
                    TouchDebugSession(options.DebugSessionId);
                    completedCount++;

                    if (options.BreakAtOperatorId.HasValue && op.Id == options.BreakAtOperatorId.Value)
                    {
                        pausedOperatorId = op.Id;
                        result.PausedOperatorId = pausedOperatorId;
                        _logger.LogInformation("[调试] 复用缓存并停在断点算子: {OperatorName} ({OperatorId})", op.Name, op.Id);
                        break;
                    }

                    continue;
                }

                // 执行算子
                var opResult = await ExecuteOperatorInternalAsync(op, executor, inputs, cts.Token);
                var normalizedOutputData = ConvertImageWrappersToBytes(opResult.OutputData);

                // 创建调试结果
                var debugOpResult = new OperatorDebugResult
                {
                    OperatorId = op.Id,
                    OperatorName = op.Name,
                    IsSuccess = opResult.IsSuccess,
                    ExecutionTimeMs = opResult.ExecutionTimeMs,
                    ErrorMessage = opResult.ErrorMessage,
                    OutputData = CloneNormalizedDictionary(normalizedOutputData),
                    ExecutionOrder = completedCount,
                    StartTime = DateTime.UtcNow.AddMilliseconds(-opResult.ExecutionTimeMs),
                    EndTime = DateTime.UtcNow,
                    IsBreakpoint = options.Breakpoints.Contains(op.Id),
                    InputSnapshot = CloneNormalizedDictionary(normalizedInputSnapshot),
                    OutputSnapshot = CloneNormalizedDictionary(normalizedOutputData)
                };

                result.DebugOperatorResults.Add(debugOpResult);
                result.OperatorResults.Add(debugOpResult);

                if (!opResult.IsSuccess)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"算子 '{op.Name}' 执行失败: {opResult.ErrorMessage}";
                    break;
                }

                // 保存输出
                var outputs = opResult.OutputData ?? new Dictionary<string, object>();
                operatorOutputs[op.Id] = outputs;
                ApplyFanOutRefCounts(op, outputs, fanOutDegrees);

                // 调试模式：缓存中间结果
                if (options.EnableIntermediateCache && normalizedOutputData.Count > 0)
                {
                    var normalizedOutputCopy = CloneNormalizedDictionary(normalizedOutputData);
                    _debugCache[cacheKey] = normalizedOutputCopy;
                    _debugCacheFingerprints[cacheKey] = cacheFingerprint;
                    result.IntermediateResults[op.Id] = CloneNormalizedDictionary(normalizedOutputCopy);
                    TouchDebugSession(options.DebugSessionId);
                }

                TouchDebugSession(options.DebugSessionId);
                completedCount++;

                // 【Phase 3】检查是否到达指定的断点算子
                if (options.BreakAtOperatorId.HasValue && op.Id == options.BreakAtOperatorId.Value)
                {
                    pausedOperatorId = op.Id;
                    result.PausedOperatorId = pausedOperatorId;
                    _logger.LogInformation("[调试] 到达断点算子: {OperatorName} ({OperatorId})，停止执行", op.Name, op.Id);
                    break;
                }
            }

            stopwatch.Stop();
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            // 检查是否因为取消而中断
            if (cts.Token.IsCancellationRequested)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Flow was canceled.";
            }
            else
            {
                result.IsSuccess = result.OperatorResults.All(r => r.IsSuccess);
            }

            // 获取最后一个算子的输出作为流程输出
            if (executionOrder.Any() && operatorOutputs.ContainsKey(executionOrder.Last().Id))
            {
                result.OutputData = ConvertImageWrappersToBytes(operatorOutputs[executionOrder.Last().Id]);
            }

            status.IsExecuting = false;
            status.ProgressPercentage = 100;
            status.CompletedAt = DateTime.UtcNow;

            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            result.IsSuccess = false;
            result.ErrorMessage = "Flow was canceled.";
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.IsSuccess = false;
            result.ErrorMessage = $"流程执行异常: {ex.Message}";
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            _logger.LogError(ex, "调试流程执行异常: {FlowId}", flow.Id);
            return result;
        }
        finally
        {
            if (_executionCancellations.TryRemove(flow.Id, out var removedCts))
            {
                removedCts.Dispose();
            }

            if (_executionStatuses.TryGetValue(flow.Id, out var finalStatus))
            {
                finalStatus.IsExecuting = false;
                finalStatus.ProgressPercentage = 100;
                finalStatus.CompletedAt ??= DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// 获取调试中间结果
    /// </summary>
    public Dictionary<string, object>? GetDebugIntermediateResult(Guid debugSessionId, Guid operatorId)
    {
        if (_debugCache.TryGetValue((debugSessionId, operatorId), out var result))
        {
            TouchDebugSession(debugSessionId);
            return CloneNormalizedDictionary(result);
        }
        return null;
    }

    /// <summary>
    /// 清除调试缓存
    /// </summary>
    public Task ClearDebugCacheAsync(Guid debugSessionId)
    {
        // 清除该会话的所有缓存
        var keysToRemove = _debugCache.Keys.Where(k => k.DebugSessionId == debugSessionId).ToList();
        foreach (var key in keysToRemove)
        {
            _debugCache.TryRemove(key, out _);
            _debugCacheFingerprints.TryRemove(key, out _);
        }

        _debugOptions.TryRemove(debugSessionId, out _);
        _debugSessionLastAccess.TryRemove(debugSessionId, out _);

        _logger.LogInformation("[Debug] Cleared debug cache: {DebugSessionId}", debugSessionId);
        return Task.CompletedTask;
    }

    private static Dictionary<string, object> CloneNormalizedDictionary(Dictionary<string, object> source)
    {
        var clone = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in source)
        {
            clone[key] = CloneNormalizedValue(value)!;
        }

        return clone;
    }

    private static object? CloneNormalizedValue(object? value)
    {
        return value switch
        {
            null => null,
            byte[] bytes => bytes.ToArray(),
            Dictionary<string, object> dictionary => CloneNormalizedDictionary(dictionary),
            IDictionary<string, object> typedDictionary => CloneNormalizedDictionary(
                typedDictionary.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)),
            IEnumerable enumerable when value is not string => enumerable.Cast<object?>().Select(CloneNormalizedValue).ToList(),
            _ => value
        };
    }

    private static string CreateDebugCacheFingerprint(Operator op, Dictionary<string, object> inputs)
    {
        var fingerprintPayload = new
        {
            operatorId = op.Id,
            operatorType = op.Type.ToString(),
            parameters = op.Parameters
                .OrderBy(parameter => parameter.Name, StringComparer.Ordinal)
                .ToDictionary(
                    parameter => parameter.Name,
                    parameter => NormalizeFingerprintValue(parameter.GetValue()),
                    StringComparer.Ordinal),
            inputs = inputs
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(
                    pair => pair.Key,
                    pair => NormalizeFingerprintValue(pair.Value),
                    StringComparer.Ordinal)
        };

        var json = JsonSerializer.Serialize(fingerprintPayload);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash);
    }

    private static object? NormalizeFingerprintValue(object? value)
    {
        if (!TryNormalizeOutputValue(value, out var normalized))
        {
            return null;
        }

        return normalized switch
        {
            byte[] bytes => new
            {
                byteCount = bytes.Length,
                sha256 = Convert.ToHexString(SHA256.HashData(bytes))
            },
            IDictionary<string, object?> dictionary => dictionary
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => NormalizeFingerprintValue(pair.Value), StringComparer.Ordinal),
            IEnumerable enumerable when normalized is not string => enumerable.Cast<object?>().Select(NormalizeFingerprintValue).ToList(),
            _ => normalized
        };
    }

    private void TouchDebugSession(Guid debugSessionId)
    {
        _debugSessionLastAccess[debugSessionId] = DateTime.UtcNow;
    }

    private void CleanupStaleDebugSessions(object? state)
    {
        try
        {
            var staleBefore = DateTime.UtcNow - DebugSessionTtl;
            foreach (var entry in _debugSessionLastAccess)
            {
                if (entry.Value < staleBefore)
                {
                    _ = ClearDebugCacheAsync(entry.Key);
                }
            }

            CleanupStaleExecutionStatuses(DateTime.UtcNow - ExecutionStatusTtl);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[Debug] Failed to cleanup stale debug sessions.");
        }
    }

    private void CleanupStaleExecutionStatuses(DateTime staleBefore)
    {
        foreach (var entry in _executionStatuses)
        {
            if (entry.Value.IsExecuting)
            {
                continue;
            }

            if (entry.Value.CompletedAt is DateTime completedAt && completedAt < staleBefore)
            {
                _executionStatuses.TryRemove(entry.Key, out _);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _debugCacheCleanupTimer.Dispose();
        _disposed = true;
    }

    #endregion
}

