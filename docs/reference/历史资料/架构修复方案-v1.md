# ClearVision 架构修复方案

**版本**: v1.0  
**日期**: 2026-03-18  
**基于**: ARCHITECTURE_ISSUES_2026-03-17.md

---

## 方案概述

本方案针对诊断报告中识别的 5 个架构问题，按优先级分为三个阶段实施：

| 阶段 | 周期 | 目标 | 解决的问题 |
|------|------|------|-----------|
| **Phase 1** | 1 周 | 核心稳定 | 问题 1（Scoped 生命周期 Bug） |
| **Phase 2** | 2-3 周 | 架构统一 | 问题 2（通信机制分裂） |
| **Phase 3** | 3-4 周 | 产品增强 | 问题 3、4（预览子图 + LLM 闭环） |

---

## Phase 1: 修复 Scoped 生命周期 Bug（P0）

### 问题
`InspectionService` 作为 Scoped 服务，却在实例字段中保存全局运行状态（`_realtimeCtsMap`、`_realtimeTasks`），导致跨请求状态丢失。

### 方案

#### 1.1 新增进程级单例协调器

```csharp
// Acme.Product.Application/Services/IInspectionRuntimeCoordinator.cs
public interface IInspectionRuntimeCoordinator
{
    // 启动实时检测
    Task<bool> TryStartAsync(
        Guid projectId, 
        Guid sessionId,
        Func<CancellationToken, Task> runLoop,
        CancellationToken ct);
    
    // 停止实时检测
    Task<bool> TryStopAsync(Guid projectId, CancellationToken ct);
    
    // 查询状态
    InspectionRuntimeState? GetState(Guid projectId);
    
    // 订阅状态变更事件
    event EventHandler<InspectionStateChangedEventArgs> StateChanged;
}

// 运行时状态
public record InspectionRuntimeState(
    Guid ProjectId,
    Guid SessionId,
    InspectionState State,  // Idle, Starting, Running, Stopping, Faulted
    DateTimeOffset StartedAt,
    DateTimeOffset? StoppedAt,
    string? ErrorMessage
);
```

#### 1.2 实现协调器

```csharp
// Acme.Product.Application/Services/InspectionRuntimeCoordinator.cs
public sealed class InspectionRuntimeCoordinator : IInspectionRuntimeCoordinator, IDisposable
{
    private readonly ILogger<InspectionRuntimeCoordinator> _logger;
    private readonly ConcurrentDictionary<Guid, RuntimeEntry> _entries = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();
    
    public event EventHandler<InspectionStateChangedEventArgs>? StateChanged;
    
    private class RuntimeEntry
    {
        public Guid SessionId { get; init; }
        public CancellationTokenSource Cts { get; init; } = null!;
        public Task Task { get; init; } = null!;
        public InspectionState State { get; set; }
        public DateTimeOffset StartedAt { get; init; }
        public string? ErrorMessage { get; set; }
    }
    
    public async Task<bool> TryStartAsync(
        Guid projectId, 
        Guid sessionId,
        Func<CancellationToken, Task> runLoop,
        CancellationToken ct)
    {
        var lockObj = _locks.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));
        await lockObj.WaitAsync(ct);
        
        try
        {
            // 检查是否已存在运行中的任务
            if (_entries.TryGetValue(projectId, out var existing) && 
                existing.State is InspectionState.Running or InspectionState.Starting)
            {
                _logger.LogWarning("Project {ProjectId} already has a running session {SessionId}", 
                    projectId, existing.SessionId);
                return false;
            }
            
            // 清理旧状态
            if (existing is not null)
            {
                await CleanupAsync(projectId, existing);
            }
            
            // 创建新会话
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var entry = new RuntimeEntry
            {
                SessionId = sessionId,
                Cts = cts,
                State = InspectionState.Starting,
                StartedAt = DateTimeOffset.UtcNow
            };
            
            // 启动任务
            entry.Task = Task.Run(async () =>
            {
                try
                {
                    entry.State = InspectionState.Running;
                    RaiseStateChanged(projectId, entry);
                    
                    await runLoop(cts.Token);
                    
                    entry.State = InspectionState.Stopped;
                }
                catch (OperationCanceledException)
                {
                    entry.State = InspectionState.Stopped;
                }
                catch (Exception ex)
                {
                    entry.State = InspectionState.Faulted;
                    entry.ErrorMessage = ex.Message;
                    _logger.LogError(ex, "Runtime loop failed for project {ProjectId}", projectId);
                }
                finally
                {
                    RaiseStateChanged(projectId, entry);
                }
            }, ct);
            
            _entries[projectId] = entry;
            RaiseStateChanged(projectId, entry);
            
            return true;
        }
        finally
        {
            lockObj.Release();
        }
    }
    
    public async Task<bool> TryStopAsync(Guid projectId, CancellationToken ct)
    {
        if (!_entries.TryGetValue(projectId, out var entry))
            return false;
            
        entry.State = InspectionState.Stopping;
        RaiseStateChanged(projectId, entry);
        
        await entry.Cts.CancelAsync();
        
        try
        {
            await entry.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        }
        catch (TimeoutException)
        {
            _logger.LogError("Stop timeout for project {ProjectId}", projectId);
            return false;
        }
        
        return true;
    }
    
    private void RaiseStateChanged(Guid projectId, RuntimeEntry entry)
    {
        StateChanged?.Invoke(this, new InspectionStateChangedEventArgs(
            projectId, 
            entry.SessionId,
            entry.State,
            entry.ErrorMessage
        ));
    }
    
    public void Dispose()
    {
        foreach (var entry in _entries.Values)
        {
            entry.Cts.Cancel();
            entry.Cts.Dispose();
        }
        _entries.Clear();
    }
}
```

#### 1.3 修改 DI 注册

```csharp
// Acme.Product.Desktop/DependencyInjection.cs
// 改为单例
services.AddSingleton<IInspectionRuntimeCoordinator, InspectionRuntimeCoordinator>();

// InspectionService 保持 Scoped，但不再保存全局状态
services.AddScoped<IInspectionService, InspectionService>();
```

#### 1.4 修改 InspectionService

```csharp
// Acme.Product.Application/Services/InspectionService.cs
public class InspectionService : IInspectionService
{
    private readonly IInspectionRuntimeCoordinator _coordinator;
    // 删除：实例字段 _realtimeCtsMap 和 _realtimeTasks
    
    public InspectionService(
        IInspectionRuntimeCoordinator coordinator,
        // ... 其他依赖
    )
    {
        _coordinator = coordinator;
    }
    
    public async Task<RealtimeStartResult> StartRealtimeAsync(
        Guid projectId, 
        RealtimeOptions options,
        CancellationToken ct)
    {
        var sessionId = Guid.NewGuid();
        
        var started = await _coordinator.TryStartAsync(
            projectId,
            sessionId,
            runLoop: async token => await RunRealtimeLoopAsync(projectId, options, token),
            ct);
            
        return new RealtimeStartResult(started, sessionId);
    }
    
    public Task<bool> StopRealtimeAsync(Guid projectId, CancellationToken ct)
    {
        return _coordinator.TryStopAsync(projectId, ct);
    }
}
```

### 改动清单

| 文件 | 操作 | 说明 |
|------|------|------|
| `IInspectionRuntimeCoordinator.cs` | 新增 | 协调器接口 |
| `InspectionRuntimeCoordinator.cs` | 新增 | 进程级单例实现 |
| `InspectionService.cs` | 修改 | 移除实例字段，调用协调器 |
| `DependencyInjection.cs` | 修改 | 注册协调器为单例 |

---

## Phase 2: 统一命令面与事件面（P1）

### 问题
- 命令面（启动/停止）：已迁移到 HTTP API
- 事件面（结果推送）：仍依赖 WebMessage，只能推送给单个 WebView2

### 方案

#### 2.1 引入事件总线

```csharp
// Acme.Product.Application/Events/IInspectionEventBus.cs
public interface IInspectionEventBus
{
    // 发布事件
    Task PublishAsync<T>(T eventData, CancellationToken ct = default) where T : IInspectionEvent;
    
    // 订阅事件
    IDisposable Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : IInspectionEvent;
}

// 事件类型
public interface IInspectionEvent 
{
    Guid ProjectId { get; }
    DateTimeOffset Timestamp { get; }
}

public record InspectionStateChangedEvent(
    Guid ProjectId,
    Guid SessionId,
    InspectionState NewState,
    string? ErrorMessage
) : IInspectionEvent;

public record InspectionResultEvent(
    Guid ProjectId,
    Guid SessionId,
    InspectionResult Result
) : IInspectionEvent;

public record InspectionProgressEvent(
    Guid ProjectId,
    int ProcessedCount,
    int? TotalCount
) : IInspectionEvent;
```

#### 2.2 实现内存事件总线

```csharp
// Acme.Product.Infrastructure/Events/InMemoryInspectionEventBus.cs
public class InMemoryInspectionEventBus : IInspectionEventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
    
    public Task PublishAsync<T>(T eventData, CancellationToken ct = default) where T : IInspectionEvent
    {
        if (_handlers.TryGetValue(typeof(T), out var handlers))
        {
            var tasks = handlers
                .OfType<Func<T, CancellationToken, Task>>()
                .Select(h => h(eventData, ct));
            return Task.WhenAll(tasks);
        }
        return Task.CompletedTask;
    }
    
    public IDisposable Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : IInspectionEvent
    {
        var list = _handlers.GetOrAdd(typeof(T), _ => new List<Delegate>());
        list.Add(handler);
        return new Subscription(() => list.Remove(handler));
    }
}
```

#### 2.3 新增 SSE 端点

```csharp
// Acme.Product.Desktop/ApiEndpoints.cs
app.MapGet("/api/inspection/realtime/{projectId:guid}/events", async (
    Guid projectId,
    HttpContext context,
    IInspectionEventBus eventBus,
    CancellationToken ct) =>
{
    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");
    
    var channel = Channel.CreateUnbounded<string>();
    
    // 订阅事件
    using var subscription = eventBus.Subscribe<IInspectionEvent>(async (evt, _) =>
    {
        if (evt.ProjectId != projectId) return;
        
        var json = JsonSerializer.Serialize(evt, evt.GetType());
        await channel.Writer.WriteAsync($"event: {evt.GetType().Name}\ndata: {json}\n\n");
    });
    
    // 发送已有状态
    var coordinator = context.RequestServices.GetRequiredService<IInspectionRuntimeCoordinator>();
    var state = coordinator.GetState(projectId);
    if (state is not null)
    {
        var json = JsonSerializer.Serialize(state);
        await context.Response.WriteAsync($"event: initialState\ndata: {json}\n\n", ct);
    }
    
    // 转发到响应
    await foreach (var message in channel.Reader.ReadAllAsync(ct))
    {
        await context.Response.WriteAsync(message, ct);
        await context.Response.Body.FlushAsync(ct);
    }
});
```

#### 2.4 WebMessageHandler 改为订阅事件

```csharp
// Acme.Product.Desktop/WebMessageHandler.cs
public class WebMessageHandler
{
    private readonly IInspectionEventBus _eventBus;
    private List<IDisposable>? _subscriptions;
    
    public void Initialize()
    {
        _subscriptions = new List<IDisposable>
        {
            _eventBus.Subscribe<InspectionStateChangedEvent>(OnStateChanged),
            _eventBus.Subscribe<InspectionResultEvent>(OnResultProduced)
        };
    }
    
    private async Task OnStateChanged(InspectionStateChangedEvent evt, CancellationToken ct)
    {
        await PostMessageAsync("inspectionStateChanged", new
        {
            projectId = evt.ProjectId,
            state = evt.NewState.ToString(),
            error = evt.ErrorMessage
        });
    }
}
```

#### 2.5 前端改为事件驱动

```javascript
// wwwroot/src/controllers/inspectionController.js
class InspectionController {
    constructor() {
        this.eventSource = null;
        this.state = 'idle'; // 从后端推送获得
    }
    
    async subscribeToEvents(projectId) {
        this.eventSource = new EventSource(
            `/api/inspection/realtime/${projectId}/events`
        );
        
        this.eventSource.addEventListener('InspectionStateChangedEvent', (e) => {
            const data = JSON.parse(e.data);
            this.state = data.newState;
            this.emit('stateChanged', data);
        });
        
        this.eventSource.addEventListener('InspectionResultEvent', (e) => {
            const data = JSON.parse(e.data);
            this.emit('resultProduced', data.result);
        });
        
        this.eventSource.addEventListener('initialState', (e) => {
            const data = JSON.parse(e.data);
            this.state = data.state;
        });
    }
}
```

### 改动清单

| 文件 | 操作 | 说明 |
|------|------|------|
| `IInspectionEventBus.cs` 及相关 | 新增 | 事件总线接口和实现 |
| `InMemoryInspectionEventBus.cs` | 新增 | 内存事件总线 |
| `InspectionService.cs` | 修改 | 发布事件 |
| `ApiEndpoints.cs` | 修改 | 新增 SSE 端点 |
| `WebMessageHandler.cs` | 修改 | 订阅事件而非直接调用 |
| `inspectionController.js` | 修改 | 事件驱动状态管理 |

---

## Phase 3: 预览子图与 LLM 闭环（P2/P3）

### 3.1 预览子图（问题 3）

```csharp
// Acme.Product.Infrastructure/Services/FlowNodePreviewService.cs
public interface IFlowNodePreviewService
{
    Task<NodePreviewResult> PreviewInFlowAsync(
        Guid projectId,
        Guid targetNodeId,
        Guid debugSessionId,
        Dictionary<string, object> parameters,
        CancellationToken ct);
}

public class FlowNodePreviewService : IFlowNodePreviewService
{
    private readonly IFlowExecutionService _flowExecution;
    private readonly IImageCacheRepository _cache;
    
    public async Task<NodePreviewResult> PreviewInFlowAsync(...)
    {
        // 1. 获取节点依赖的上游子图
        var upstreamNodes = await GetUpstreamSubgraphAsync(projectId, targetNodeId);
        
        // 2. 计算缓存键
        var upstreamHash = ComputeUpstreamHash(upstreamNodes);
        var cacheKey = $"{debugSessionId}:{targetNodeId}:{upstreamHash}";
        
        // 3. 检查上游输出缓存
        Mat? upstreamOutput = null;
        if (upstreamNodes.Count > 0)
        {
            var cached = await _cache.GetAsync(cacheKey, ct);
            if (cached is not null)
            {
                upstreamOutput = cached;
            }
            else
            {
                // 4. 执行上游子图
                upstreamOutput = await _flowExecution.ExecuteSubgraphAsync(
                    upstreamNodes, ct);
                await _cache.SetAsync(cacheKey, upstreamOutput, TimeSpan.FromMinutes(5), ct);
            }
        }
        
        // 5. 使用上游输出作为输入，预览目标节点
        return await PreviewNodeWithInputAsync(targetNodeId, parameters, upstreamOutput, ct);
    }
}
```

### 3.2 LLM 闭环反馈（问题 4）

```csharp
// Acme.Product.Infrastructure/Services/PreviewMetricsAnalyzer.cs
public class PreviewMetricsAnalyzer
{
    public PreviewFeedback Analyze(Mat image, OperatorResult result)
    {
        return new PreviewFeedback
        {
            // 1. 图像统计
            ImageStats = new ImageStats
            {
                Mean = Cv2.Mean(image),
                StdDev = ComputeStdDev(image),
                LaplacianVariance = ComputeLaplacianVariance(image),
                Histogram = ComputeHistogram(image)
            },
            
            // 2. Blob 统计
            BlobStats = result.Blobs?.Select(b => new BlobStat
            {
                Area = b.Area,
                Perimeter = b.Perimeter,
                Circularity = b.Circularity,
                RejectReason = b.RejectReason
            }).ToList(),
            
            // 3. 诊断标签
            Diagnostics = new List<string>(),
            
            // 4. 可优化目标
            Goals = new List<OptimizationGoal>()
        };
    }
}

// 扩展预览接口
public interface IOperatorPreviewService
{
    Task<PreviewWithMetricsResult> PreviewWithMetricsAsync(
        OperatorType type,
        Mat inputImage,
        Dictionary<string, object> parameters,
        CancellationToken ct);
}
```

### 3.3 自动调参服务

```csharp
// Acme.Product.Application/Services/AutoTuneService.cs
public interface IAutoTuneService
{
    Task<AutoTuneResult> AutoTuneOperatorAsync(
        OperatorType type,
        Mat inputImage,
        Dictionary<string, object> initialParams,
        AutoTuneGoal goal,
        int maxIterations = 5,
        CancellationToken ct = default);
}

public class AutoTuneService : IAutoTuneService
{
    private readonly IOperatorPreviewService _preview;
    private readonly IParameterRecommender _recommender;
    
    public async Task<AutoTuneResult> AutoTuneOperatorAsync(...)
    {
        var currentParams = initialParams;
        var history = new List<TuneIteration>();
        
        for (int i = 0; i < maxIterations; i++)
        {
            // 1. 执行预览
            var preview = await _preview.PreviewWithMetricsAsync(
                type, inputImage, currentParams, ct);
            
            // 2. 分析结果
            var score = Evaluate(preview, goal);
            history.Add(new TuneIteration(currentParams, preview, score));
            
            // 3. 检查是否达标
            if (score.IsSatisfactory)
                break;
            
            // 4. 获取下一轮参数建议
            currentParams = await _recommender.RecommendNextAsync(
                type, preview, goal, currentParams, ct);
        }
        
        return new AutoTuneResult(history);
    }
}
```

### 新增 API

```csharp
// POST /api/flows/preview-node
// 预览工作流中的节点

// POST /api/operators/{type}/preview-with-metrics
// 带反馈信息的预览

// POST /api/operators/{type}/auto-tune
// 自动调参
```

---

## 实施路线图

```
Week 1: Phase 1 - Scoped Bug 修复
├── Day 1-2: 实现 IInspectionRuntimeCoordinator
├── Day 3-4: 修改 InspectionService 和 DI 注册
└── Day 5: 测试和验证

Week 2-3: Phase 2 - 统一事件机制
├── Week 2:
│   ├── Day 1-2: 实现 IInspectionEventBus
│   ├── Day 3-4: 新增 SSE 端点
│   └── Day 5: 修改 WebMessageHandler
├── Week 3:
│   ├── Day 1-3: 前端改为事件驱动
│   └── Day 4-5: 集成测试

Week 4-6: Phase 3 - 产品增强
├── Week 4: 预览子图实现
├── Week 5: LLM 闭环反馈
└── Week 6: 端到端验证（包装带检测场景）
```

---

## 验证标准

| 项目 | 验收标准 |
|------|----------|
| **Phase 1** | 实时检测可在不同 HTTP 请求间正常启动/停止，无状态丢失 |
| **Phase 2** | 多标签页同时监听同一工程，状态完全一致 |
| **Phase 3** | 包装带检测场景：自动调参成功率 > 70%，耗时 < 5 分钟 |

---

## 风险与缓解

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| SSE 在高并发下性能问题 | 中 | 预留升级到 SignalR 的扩展点 |
| 调试缓存内存泄漏 | 高 | 严格 TTL 管理 + 定期清理 |
| 自动调参收敛失败 | 中 | 设置最大迭代次数 + 人工兜底 |

---

**结论**: 按此方案分阶段实施，可在 6 周内修复核心架构问题，并为 LLM 智能化打下坚实基础。
