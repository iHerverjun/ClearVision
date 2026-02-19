// ForEachOperator.cs
// ForEach 子图执行机制 - Sprint 2 Task 2.1
// 支持 IoMode 双模式：Parallel（并行纯计算）/ Sequential（串行含 I/O）
// 作者：蘅芜君

using System.Collections.Concurrent;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.Services;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// ForEach 子图执行算子 - 对集合中的每个元素执行子流程
/// 
/// 核心特性：
/// 1. IoMode=Parallel: 纯计算子图，使用 Parallel.ForEachAsync 并行执行
/// 2. IoMode=Sequential: 含 I/O 的串行子图，顺序执行保护硬件连接
/// 
/// 使用场景：
/// - 多目标检测后的逐条处理（如每个缺陷上报 MES）
/// - 批量 HTTP 校验
/// - 并行图像处理（滤波、裁剪等）
/// </summary>
public class ForEachOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ForEach;

    private readonly IFlowExecutionService _subFlowExecutor;

    public ForEachOperator(
        ILogger<ForEachOperator> logger,
        IFlowExecutionService subFlowExecutor) : base(logger)
    {
        _subFlowExecutor = subFlowExecutor ?? throw new ArgumentNullException(nameof(subFlowExecutor));
    }

    /// <summary>
    /// 子图流程定义
    /// </summary>
    public OperatorFlow? SubGraph { get; set; }

    protected override async Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        // 1. 获取输入集合
        if (inputs == null)
        {
            return OperatorExecutionOutput.Failure("ForEach 算子需要输入数据");
        }

        // 尝试获取 Items（支持任何列表类型）
        if (!inputs.TryGetValue("Items", out var itemsObj) || itemsObj == null)
        {
            return OperatorExecutionOutput.Failure("未提供 Items 输入");
        }

        // 解析输入集合 - 使用 object 泛型处理任何类型
        var items = ParseItems(itemsObj);
        if (items == null)
        {
            return OperatorExecutionOutput.Failure($"Items 必须是可枚举类型，实际类型: {itemsObj.GetType().Name}");
        }

        if (items.Count == 0)
        {
            return OperatorExecutionOutput.Success(new Dictionary<string, object>
            {
                { "Results", new List<object>() },
                { "Count", 0 },
                { "PassCount", 0 },
                { "AllPass", true },
                { "SuccessCount", 0 }
            });
        }

        // 2. 获取参数
        var ioMode = GetStringParam(@operator, "IoMode", "Parallel");
        int maxParallelism = GetIntParam(@operator, "MaxParallelism", Environment.ProcessorCount, 1, 64);
        bool orderResults = GetBoolParam(@operator, "OrderResults", true);
        bool failFast = GetBoolParam(@operator, "FailFast", false);
        int timeoutMs = GetIntParam(@operator, "TimeoutMs", 30000, 1000, 300000);

        Logger.LogInformation("[ForEach] 开始执行: 项目数={Count}, IoMode={IoMode}, MaxParallelism={MaxParallelism}, FailFast={FailFast}",
            items.Count, ioMode, maxParallelism, failFast);

        // 3. 获取或加载子图
        var subGraph = GetSubGraph(@operator);
        if (subGraph == null)
        {
            return OperatorExecutionOutput.Failure("ForEach 算子未配置子图（SubGraph）");
        }

        // 4. 根据 IoMode 执行
        try
        {
            var results = ioMode.Equals("Sequential", StringComparison.OrdinalIgnoreCase)
                ? await ExecuteSequentialAsync(items, subGraph, timeoutMs, failFast, cancellationToken)
                : await ExecuteParallelAsync(items, subGraph, maxParallelism, timeoutMs, failFast, orderResults, cancellationToken);

            var aggregateResult = BuildAggregateResult(results, orderResults);

            Logger.LogInformation("[ForEach] 执行完成: 成功={SuccessCount}/{Total}, AllPass={AllPass}",
                aggregateResult.SuccessCount, aggregateResult.Count, aggregateResult.AllPass);

            return OperatorExecutionOutput.Success(new Dictionary<string, object>
            {
                { "Results", aggregateResult.Results },
                { "Count", aggregateResult.Count },
                { "PassCount", aggregateResult.PassCount },
                { "AllPass", aggregateResult.AllPass },
                { "SuccessCount", aggregateResult.SuccessCount }
            });
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("[ForEach] 执行被取消");
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[ForEach] 执行异常");
            return OperatorExecutionOutput.Failure($"ForEach 执行失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 解析输入为列表
    /// </summary>
    private List<object>? ParseItems(object itemsObj)
    {
        if (itemsObj is System.Collections.IEnumerable enumerable && itemsObj is not string)
        {
            return enumerable.Cast<object>().ToList();
        }
        return null;
    }

    /// <summary>
    /// 并行模式：适用于纯计算子图（测量、图像处理、AI推理）
    /// 各迭代项相互独立，无通信副作用
    /// </summary>
    private async Task<List<ForEachItemResult>> ExecuteParallelAsync(
        List<object> items,
        OperatorFlow subGraph,
        int maxParallelism,
        int timeoutMs,
        bool failFast,
        bool orderResults,
        CancellationToken cancellationToken)
    {
        var results = new ConcurrentBag<(int Index, ForEachItemResult Result)>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs * items.Count)); // 总超时

        Logger.LogDebug("[ForEach] 并行模式: MaxParallelism={MaxParallelism}", maxParallelism);

        await Parallel.ForEachAsync(
            items.Select((item, idx) => (item, idx)),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = maxParallelism,
                CancellationToken = cts.Token
            },
            async (entry, ct) =>
            {
                var (item, index) = entry;
                try
                {
                    var subResult = await ExecuteSubGraphAsync(
                        subGraph,
                        BuildSubInputs(item, index, items.Count),
                        timeoutMs,
                        ct);

                    // 从 OutputData 中获取 Result 字段
                    var resultValue = subResult.OutputData?.GetValueOrDefault("Result") ?? false;

                    results.Add((index, new ForEachItemResult
                    {
                        Index = index,
                        Item = item,
                        Result = resultValue,
                        Success = subResult.IsSuccess,
                        ErrorMessage = subResult.ErrorMessage
                    }));

                    if (failFast && !subResult.IsSuccess)
                    {
                        Logger.LogWarning("[ForEach] FailFast 触发: 第 {Index} 项失败，取消后续执行", index);
                        cts.Cancel();
                    }
                }
                catch (OperationCanceledException)
                {
                    // FailFast 或外部取消，正常流程
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "[ForEach] 第 {Index} 项执行异常", index);
                    results.Add((index, new ForEachItemResult
                    {
                        Index = index,
                        Item = item,
                        Result = false,
                        Success = false,
                        ErrorMessage = ex.Message
                    }));

                    if (failFast)
                    {
                        cts.Cancel();
                    }
                }
            });

        return results.Select(r => r.Result).ToList();
    }

    /// <summary>
    /// 串行模式：适用于含通信算子的子图（逐条 HTTP 校验、逐个上报 MES）
    /// 保证对外部设备的访问严格串行，防止连接耗尽和报文错乱
    /// 牺牲并发性能换取正确性
    /// </summary>
    private async Task<List<ForEachItemResult>> ExecuteSequentialAsync(
        List<object> items,
        OperatorFlow subGraph,
        int timeoutMs,
        bool failFast,
        CancellationToken cancellationToken)
    {
        var results = new List<ForEachItemResult>();

        Logger.LogDebug("[ForEach] 串行模式: 共 {Count} 项", items.Count);

        foreach (var (item, index) in items.Select((item, idx) => (item, idx)))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

                var subResult = await ExecuteSubGraphAsync(
                    subGraph,
                    BuildSubInputs(item, index, items.Count),
                    timeoutMs,
                    linkedCts.Token);

                // 从 OutputData 中获取 Result 字段
                var resultValue = subResult.OutputData?.GetValueOrDefault("Result") ?? false;

                results.Add(new ForEachItemResult
                {
                    Index = index,
                    Item = item,
                    Result = resultValue,
                    Success = subResult.IsSuccess,
                    ErrorMessage = subResult.ErrorMessage
                });

                // 串行模式下 FailFast 语义：任一子图失败即立刻中断
                if (failFast && !subResult.IsSuccess)
                {
                    Logger.LogWarning("[ForEach] FailFast 触发(串行模式): 第 {Index} 项失败，中断执行", index);
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                // 超时或其他取消，记录并继续或中断
                results.Add(new ForEachItemResult
                {
                    Index = index,
                    Item = item,
                    Result = false,
                    Success = false,
                    ErrorMessage = "执行超时"
                });

                if (failFast)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[ForEach] 第 {Index} 项执行异常", index);
                results.Add(new ForEachItemResult
                {
                    Index = index,
                    Item = item,
                    Result = false,
                    Success = false,
                    ErrorMessage = ex.Message
                });

                if (failFast)
                {
                    break;
                }
            }
        }

        return results;
    }

    /// <summary>
    /// 执行子图
    /// </summary>
    private async Task<FlowExecutionResult> ExecuteSubGraphAsync(
        OperatorFlow subGraph,
        Dictionary<string, object> inputs,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        // 对于子图执行，禁用并行模式（子图内部已经是并行了）
        return await _subFlowExecutor.ExecuteFlowAsync(
            subGraph,
            inputs,
            enableParallel: false,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 构建子图输入数据
    /// </summary>
    private static Dictionary<string, object> BuildSubInputs(
        object item, int index, int total)
    {
        return new Dictionary<string, object>
        {
            ["CurrentItem"] = item,
            ["CurrentIndex"] = index,
            ["TotalCount"] = total
        };
    }

    /// <summary>
    /// 聚合结果
    /// </summary>
    private static AggregateResult BuildAggregateResult(List<ForEachItemResult> results, bool orderResults)
    {
        var ordered = orderResults
            ? results.OrderBy(r => r.Index).ToList()
            : results;

        return new AggregateResult
        {
            Results = ordered.Select(r => r.Result).ToList(),
            Count = ordered.Count,
            PassCount = ordered.Count(r => r.Result is true),
            AllPass = ordered.All(r => r.Result is true),
            SuccessCount = ordered.Count(r => r.Success)
        };
    }

    /// <summary>
    /// 从算子参数中获取子图定义
    /// </summary>
    private OperatorFlow? GetSubGraph(Operator @operator)
    {
        // 优先使用运行时设置的 SubGraph 属性
        if (SubGraph != null)
        {
            return SubGraph;
        }

        // 尝试从算子参数中获取
        var subGraphParam = @operator.Parameters.FirstOrDefault(p => p.Name == "SubGraph");
        if (subGraphParam?.Value != null)
        {
            try
            {
                // 反序列化子图定义
                var json = System.Text.Json.JsonSerializer.Serialize(subGraphParam.Value);
                return System.Text.Json.JsonSerializer.Deserialize<OperatorFlow>(json);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[ForEach] 反序列化子图失败");
            }
        }

        return null;
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var ioMode = GetStringParam(@operator, "IoMode", "Parallel");
        var maxParallelism = GetIntParam(@operator, "MaxParallelism", Environment.ProcessorCount);
        var timeoutMs = GetIntParam(@operator, "TimeoutMs", 30000);

        if (!ioMode.Equals("Parallel", StringComparison.OrdinalIgnoreCase) &&
            !ioMode.Equals("Sequential", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("IoMode 必须是 'Parallel' 或 'Sequential'");
        }

        if (maxParallelism < 1 || maxParallelism > 64)
        {
            return ValidationResult.Invalid("MaxParallelism 必须在 1-64 之间");
        }

        if (timeoutMs < 1000 || timeoutMs > 300000)
        {
            return ValidationResult.Invalid("TimeoutMs 必须在 1000-300000 之间");
        }

        return ValidationResult.Valid();
    }

    /// <summary>
    /// 单个迭代项的执行结果
    /// </summary>
    private class ForEachItemResult
    {
        public int Index { get; set; }
        public object Item { get; set; } = null!;
        public object Result { get; set; } = false;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// 聚合结果
    /// </summary>
    private class AggregateResult
    {
        public List<object> Results { get; set; } = new();
        public int Count { get; set; }
        public int PassCount { get; set; }
        public bool AllPass { get; set; }
        public int SuccessCount { get; set; }
    }
}
