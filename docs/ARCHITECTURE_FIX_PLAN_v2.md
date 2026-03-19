# ClearVision 架构修复方案 v2

**版本**: v2.0  
**日期**: 2026-03-18  
**状态**: 已通过评审，附条件实施  

---

## 评审后修正清单

根据评审报告，以下问题必须在实施前/中修正：

| 优先级 | 修正项 | 影响 | 解决章节 |
|--------|--------|------|----------|
| **P0** | `SubscribeInterface` 委托类型转换 bug | 运行时必崩 | [2.2 事件总线修正](#22-事件总线修正) |
| **P0** | Worker 优雅关机机制 | 应用关闭时资源泄漏 | [1.3 Worker 生命周期](#13-worker-生命周期管理) |
| **P1** | Worker 并发控制 + 双重异常保护 | 生产稳定性 | [1.2 Worker 实现](#12-inspectionworker-实现) |
| **P1** | 事件回放存储策略 | 断线重连功能 | [2.3 断线重连设计](#23-断线重连设计) |
| **P2** | SSE 心跳保活 | NAT/代理可靠性 | [2.4 SSE 心跳机制](#24-sse-心跳机制) |
| **P2** | 验证集样本量增至 30-40 张 | 统计检验力 | [5. 科学实验设计](#5-科学实验设计) |

---

## 1. Phase 1: 核心稳定（Scoped Bug 根治）

### 1.1 架构设计

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           HTTP 请求 (Scoped)                              │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │  InspectionService (Scoped) - API 门面                            │   │
│  │  - 参数校验                                                        │   │
│  │  - 调用协调器 TryStartAsync                                        │   │
│  │  - 返回 sessionId                                                  │   │
│  └─────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                           进程级 (Singleton)                              │
│  ┌─────────────────────────┐    ┌─────────────────────────────────────┐ │
│  │ InspectionRuntime       │    │ InspectionWorker (IHostedService)    │ │
│  │ Coordinator             │    │                                      │ │
│  │ ─────────────────────── │    │ - ConcurrentDictionary 跟踪任务       │ │
│  │ - _sessions: 会话状态    │◄──►│ - 每个执行周期 CreateAsyncScope      │ │
│  │ - _ctsMap: 取消令牌      │    │ - 优雅关机支持                       │ │
│  │ - TryStart/StopAsync    │    │ - 双重异常保护                       │ │
│  └─────────────────────────┘    └─────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────┘
```

### 1.2 InspectionWorker 实现（含并发控制和异常保护）

```csharp
// 新增：InspectionWorker.cs
public class InspectionWorker : IHostedService, IAsyncDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IInspectionRuntimeCoordinator _coordinator;
    private readonly ILogger<InspectionWorker> _logger;
    
    // 并发控制：跟踪运行中的任务
    private readonly ConcurrentDictionary<Guid, RunningTaskEntry> _runningTasks = new();
    private readonly SemaphoreSlim _shutdownLock = new(1, 1);
    private volatile bool _isShuttingDown = false;
    
    // 双重异常保护统计
    private readonly ConcurrentDictionary<Guid, Exception> _unhandledExceptions = new();
    
    private class RunningTaskEntry
    {
        public required Guid ProjectId { get; init; }
        public required Guid SessionId { get; init; }
        public required Task Task { get; init; }
        public required CancellationTokenSource Cts { get; init; }
        public required DateTime StartedAt { get; init; }
    }
    
    public async Task StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("[InspectionWorker] 启动完成，准备接受任务");
        await Task.CompletedTask;
    }
    
    public async Task StopAsync(CancellationToken ct)
    {
        _isShuttingDown = true;
        
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
        
        await _shutdownLock.WaitAsync(timeoutCts.Token);
        try
        {
            _logger.LogInformation("[InspectionWorker] 开始优雅关机，等待 {Count} 个任务完成", 
                _runningTasks.Count);
            
            // 1. 取消所有运行中的任务
            foreach (var entry in _runningTasks.Values)
            {
                try
                {
                    await entry.Cts.CancelAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[InspectionWorker] 取消任务失败: {ProjectId}", entry.ProjectId);
                }
            }
            
            // 2. 等待所有任务完成（或超时）
            var tasks = _runningTasks.Values.Select(e => e.Task).ToArray();
            try
            {
                await Task.WhenAll(tasks).WaitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("[InspectionWorker] 优雅关机超时，强制退出");
            }
            
            // 3. 报告未处理异常
            if (!_unhandledExceptions.IsEmpty)
            {
                _logger.LogError("[InspectionWorker] 关机时发现 {Count} 个未处理异常", 
                    _unhandledExceptions.Count);
            }
        }
        finally
        {
            _shutdownLock.Release();
        }
    }
    
    public async Task<bool> TryStartRunAsync(
        Guid projectId, 
        Guid sessionId, 
        OperatorFlow flow, 
        string? cameraId)
    {
        if (_isShuttingDown)
        {
            _logger.LogWarning("[InspectionWorker] 拒绝新任务，正在关机中");
            return false;
        }
        
        // 防重复启动检查（Coordinator 已做，这里二次保护）
        if (_runningTasks.ContainsKey(projectId))
        {
            _logger.LogWarning("[InspectionWorker] 项目 {ProjectId} 已在运行", projectId);
            return false;
        }
        
        var cts = new CancellationTokenSource();
        var entry = new RunningTaskEntry
        {
            ProjectId = projectId,
            SessionId = sessionId,
            Cts = cts,
            StartedAt = DateTime.UtcNow,
            Task = null!  // 将在后面设置
        };
        
        // 创建任务（不立即启动，先注册）
        var task = RunWithTripleExceptionProtectionAsync(projectId, sessionId, flow, cameraId, cts.Token);
        entry = entry with { Task = task };
        
        if (!_runningTasks.TryAdd(projectId, entry))
        {
            await cts.CancelAsync();
            cts.Dispose();
            return false;
        }
        
        // 任务完成后清理
        _ = task.ContinueWith(async _ =>
        {
            _runningTasks.TryRemove(projectId, out _);
            await cts.CancelAsync();
            cts.Dispose();
        }, TaskScheduler.Default);
        
        return true;
    }
    
    /// <summary>
    /// 三重异常保护：Worker 级、Scope 级、Coordinator 级
    /// </summary>
    private async Task RunWithTripleExceptionProtectionAsync(
        Guid projectId, 
        Guid sessionId, 
        OperatorFlow flow, 
        string? cameraId, 
        CancellationToken ct)
    {
        // 第一层：Worker 级保护
        try
        {
            await RunWithScopeAsync(projectId, sessionId, flow, cameraId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation("[InspectionWorker] 任务正常取消: {ProjectId}", projectId);
            _coordinator.MarkAsStopped(projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[InspectionWorker] 任务异常: {ProjectId}", projectId);
            
            // 第二层：Coordinator 级保护（即使 MarkAsFaulted 也失败）
            try
            {
                _coordinator.MarkAsFaulted(projectId, ex.Message);
            }
            catch (Exception coordEx)
            {
                _logger.LogCritical(coordEx, "[InspectionWorker] Coordinator 也失败，记录到未处理异常");
                _unhandledExceptions[sessionId] = ex;
            }
        }
    }
    
    private async Task RunWithScopeAsync(
        Guid projectId, 
        Guid sessionId, 
        OperatorFlow flow, 
        string? cameraId, 
        CancellationToken ct)
    {
        // 第三层：Scope 级保护
        await using var scope = _scopeFactory.CreateAsyncScope();
        
        var flowExecution = scope.ServiceProvider.GetRequiredService<IFlowExecutionService>();
        var imageAcquisition = scope.ServiceProvider.GetRequiredService<IImageAcquisitionService>();
        var resultRepository = scope.ServiceProvider.GetRequiredService<IInspectionResultRepository>();
        
        // 执行循环...
        while (!ct.IsCancellationRequested)
        {
            // 单次检测执行
            var cycleResult = await ExecuteCycleAsync(flow, cameraId, flowExecution, imageAcquisition, ct);
            
            // 发布结果...
        }
    }
    
    private async Task<InspectionResult> ExecuteCycleAsync(...)
    {
        // 单次检测逻辑
    }
    
    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        _shutdownLock.Dispose();
    }
}
```

### 1.3 Worker 生命周期管理

```csharp
// DependencyInjection.cs 注册修正
public static IServiceCollection AddVisionServices(this IServiceCollection services)
{
    // ... 现有注册 ...
    
    // Worker 作为 HostedService，纳入 Host 生命周期管理
    services.AddSingleton<IInspectionRuntimeCoordinator, InspectionRuntimeCoordinator>();
    services.AddHostedService<InspectionWorker>();  // 关键：使用 HostedService 而非 Singleton
    services.AddSingleton<IInspectionWorker>(sp => 
        (InspectionWorker)sp.GetRequiredService<IHostedService>());
    
    // InspectionService 保持 Scoped
    services.AddScoped<IInspectionService, InspectionService>();
}

// Program.cs 关机超时配置
var host = builder.Build();
host.RunAsync(new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token);
```

---

## 2. Phase 2: 事件机制（双栈过渡）

### 2.1 事件总线修正

```csharp
// 修正：SubscribeInterface 的委托类型问题
public class InMemoryInspectionEventBus : IInspectionEventBus
{
    // 存储包装后的适配器委托（解决类型转换问题）
    private readonly ConcurrentDictionary<Type, ImmutableList<Func<object, CancellationToken, Task>>> 
        _interfaceHandlers = new();
    
    public IDisposable SubscribeInterface<TInterface>(
        Func<TInterface, CancellationToken, Task> handler) where TInterface : class
    {
        var type = typeof(TInterface);
        
        // 关键修正：创建适配器委托，而不是直接 cast
        Func<object, CancellationToken, Task> wrapper = (obj, ct) =>
        {
            if (obj is TInterface typedObj)
                return handler(typedObj, ct);
            
            _logger.LogWarning("事件类型不匹配，期望 {Expected}，实际 {Actual}", 
                typeof(TInterface).Name, obj.GetType().Name);
            return Task.CompletedTask;
        };
        
        _interfaceHandlers.AddOrUpdate(
            type,
            _ => ImmutableList.Create(wrapper),
            (_, list) => list.Add(wrapper));
        
        return new Subscription(() =>
        {
            _interfaceHandlers.AddOrUpdate(
                type,
                _ => ImmutableList<Func<object, CancellationToken, Task>>.Empty,
                (_, list) => list.Remove(wrapper));
        });
    }
    
    public async Task PublishAsync<T>(T eventData, CancellationToken ct = default) where T : IInspectionEvent
    {
        var type = typeof(T);
        var exceptions = new List<Exception>();
        
        // 1. 精确类型订阅者（异常隔离）
        if (_handlers.TryGetValue(type, out var handlers))
        {
            foreach (var handler in handlers)
            {
                try
                {
                    await ((Func<T, CancellationToken, Task>)handler)(eventData, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "事件处理器失败: {HandlerType}", handler.GetType().Name);
                    exceptions.Add(ex);
                    // 继续执行后续 handler
                }
            }
        }
        
        // 2. 接口订阅者（异常隔离）
        foreach (var (ifaceType, ifaceHandlers) in _interfaceHandlers)
        {
            if (ifaceType.IsAssignableFrom(type))
            {
                foreach (var handler in ifaceHandlers)
                {
                    try
                    {
                        await handler(eventData, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "接口事件处理器失败: {InterfaceType}", ifaceType.Name);
                        exceptions.Add(ex);
                    }
                }
            }
        }
        
        // 如有异常，抛 AggregateException
        if (exceptions.Count > 0)
        {
            throw new AggregateException("一个或多个事件处理器失败", exceptions);
        }
    }
}
```

### 2.2 事件回放存储策略

```csharp
// 新增：IEventStore.cs - 事件回放存储
public interface IEventStore
{
    void Append(Guid projectId, IInspectionEvent evt);
    IReadOnlyList<IInspectionEvent> GetEventsAfter(Guid projectId, long sequenceId);
    void Cleanup(Guid projectId);
}

// 实现：内存环形缓冲区（限制大小防内存泄漏）
public class InMemoryEventStore : IEventStore
{
    // 每个项目的最大事件数（防止内存无限增长）
    private const int MaxEventsPerProject = 100;
    private const int MaxTotalEvents = 10000;
    
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<StoredEvent>> _store = new();
    private long _globalSequenceId = 0;
    
    private class StoredEvent
    {
        public required long SequenceId { get; init; }
        public required IInspectionEvent Event { get; init; }
        public required DateTime StoredAt { get; init; }
    }
    
    public void Append(Guid projectId, IInspectionEvent evt)
    {
        var queue = _store.GetOrAdd(projectId, _ => new ConcurrentQueue<StoredEvent>());
        
        var stored = new StoredEvent
        {
            SequenceId = Interlocked.Increment(ref _globalSequenceId),
            Event = evt,
            StoredAt = DateTime.UtcNow
        };
        
        queue.Enqueue(stored);
        
        // 限制单个项目事件数
        while (queue.Count > MaxEventsPerProject && queue.TryDequeue(out _)) { }
        
        // 全局清理（简单策略：超过阈值时清理最老的项目）
        if (_store.Count > 100)  // 最多保留 100 个项目的历史
        {
            // 异步清理，不阻塞发布
            _ = Task.Run(() => CleanupOldProjects());
        }
    }
    
    public IReadOnlyList<IInspectionEvent> GetEventsAfter(Guid projectId, long sequenceId)
    {
        if (!_store.TryGetValue(projectId, out var queue))
            return Array.Empty<IInspectionEvent>();
        
        return queue
            .Where(e => e.SequenceId > sequenceId)
            .OrderBy(e => e.SequenceId)
            .Select(e => e.Event)
            .ToList();
    }
    
    private void CleanupOldProjects()
    {
        // 保留最近有活动的 50 个项目
        var toRemove = _store
            .OrderBy(kvp => kvp.Value.LastOrDefault()?.StoredAt ?? DateTime.MinValue)
            .Take(_store.Count - 50)
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var projectId in toRemove)
        {
            _store.TryRemove(projectId, out _);
        }
    }
}
```

### 2.3 断线重连设计（完整版）

```csharp
app.MapGet("/api/inspection/realtime/{projectId:guid}/events", async (
    Guid projectId,
    HttpContext context,
    IInspectionEventBus eventBus,
    IEventStore eventStore,  // 新增：事件存储
    IInspectionRuntimeCoordinator coordinator,
    CancellationToken ct) =>
{
    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");
    
    var channel = Channel.CreateUnbounded<SseMessage>();
    long lastSequenceId = 0;
    
    // 解析重连位置
    if (context.Request.Headers.TryGetValue("Last-Event-ID", out var lastEventIdHeader) 
        && long.TryParse(lastEventIdHeader.FirstOrDefault(), out var parsedId))
    {
        lastSequenceId = parsedId;
    }
    
    // 1. 发送当前状态
    var state = coordinator.GetState(projectId);
    await channel.Writer.WriteAsync(new SseMessage(0, "initialState", state));
    
    // 2. 回放缺失事件（基于存储）
    if (lastSequenceId > 0)
    {
        var missedEvents = eventStore.GetEventsAfter(projectId, lastSequenceId);
        foreach (var evt in missedEvents)
        {
            await channel.Writer.WriteAsync(new SseMessage(
                GetSequenceId(evt), 
                evt.GetType().Name, 
                evt));
        }
    }
    
    // 3. 订阅新事件
    using var subscription = eventBus.SubscribeInterface<IInspectionEvent>(async (evt, _) =>
    {
        if (evt.ProjectId != projectId) return;
        
        // 存储事件
        eventStore.Append(projectId, evt);
        
        await channel.Writer.WriteAsync(new SseMessage(
            Interlocked.Increment(ref lastSequenceId),
            evt.GetType().Name,
            evt));
    });
    
    // 4. 转发到 SSE（含心跳）
    await foreach (var msg in channel.Reader.ReadAllAsync(ct))
    {
        var json = JsonSerializer.Serialize(msg.Data);
        await context.Response.WriteAsync($"id: {msg.SequenceId}\nevent: {msg.EventType}\ndata: {json}\n\n");
        await context.Response.Body.FlushAsync(ct);
    }
});

record SseMessage(long SequenceId, string EventType, object Data);
```

### 2.4 SSE 心跳机制

```csharp
// 新增：HeartbeatBackgroundService.cs
public class SseHeartbeatService : BackgroundService
{
    private readonly IInspectionEventBus _eventBus;
    private readonly ILogger<SseHeartbeatService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 发布心跳事件（所有活跃项目）
                await _eventBus.PublishAsync(new HeartbeatEvent 
                { 
                    Timestamp = DateTime.UtcNow 
                }, stoppingToken);
                
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "心跳发送失败");
            }
        }
    }
}

// SSE 端点处理心跳
// 心跳事件 id 为 0，客户端应忽略
if (msg.EventType == nameof(HeartbeatEvent))
{
    await context.Response.WriteAsync($":keepalive\n\n");  // SSE 注释帧
}
```

---

## 3. Phase 3: 预览子图 Spike

### 3.1 Spike 任务清单（修正版）

| 任务 | 时间 | 目标 | 关键问题 |
|------|------|------|----------|
| Spike-1 | 1 天 | 现有调试缓存机制 | `_debugCache` key 结构、TTL 策略、并发安全性 |
| Spike-2 | 1 天 | 前端预览调用链 | `previewPanel.js`、`propertyPanel.js` 如何调用预览 |
| Spike-3 | 1 天 | 中间算子输入来源 | 是否已有上游输出缓存、缓存失效策略 |
| Spike-4 | **3 天** | PoC 子图执行 | 验证 `BreakAtOperatorId` 实现复杂度 |

> **评审修正**: Spike-4 从 2 天增至 3 天，因为 `BreakAtOperatorId` 需要新增断点机制。

### 3.2 BreakAtOperatorId 实现评估

```csharp
// 现有 ExecuteFlowDebugAsync 分析
public async Task<FlowDebugExecutionResult> ExecuteFlowDebugAsync(...)
{
    // 当前实现：执行所有算子，每个算子后检查断点
    foreach (var op in executionOrder)
    {
        // 执行算子...
        
        // 保存中间结果
        if (options.EnableIntermediateCache)
            _debugCache[(options.DebugSessionId, op.Id)] = ...;
        
        // 【新增】检查是否到达断点
        if (op.Id == options.BreakAtOperatorId)
            break;  // 提前退出
    }
}

// 复杂度评估：
// - 修改范围：FlowExecutionService.cs ~50 行
// - 风险：确保 break 后返回的部分结果仍可用
// - 测试：需要验证提前退出后的结果完整性
```

---

## 4. 新增章节：可观测性

### 4.1 结构化日志（Correlation ID）

```csharp
// 新增：InspectionLoggingContext.cs
public class InspectionLoggingContext
{
    private readonly ILogger<InspectionLoggingContext> _logger;
    
    public Guid CorrelationId { get; } = Guid.NewGuid();
    public Guid? ProjectId { get; set; }
    public Guid? SessionId { get; set; }
    
    public IDisposable BeginScope()
    {
        return _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = CorrelationId,
            ["ProjectId"] = ProjectId,
            ["SessionId"] = SessionId
        });
    }
}

// Worker 中使用
public async Task RunWithScopeAsync(...)
{
    var loggingContext = new InspectionLoggingContext();
    using (loggingContext.BeginScope())
    {
        _logger.LogInformation("开始检测循环");  // 自动包含 CorrelationId
        // ...
    }
}
```

### 4.2 关键指标（Metrics）

```csharp
// 新增：InspectionMetrics.cs
public class InspectionMetrics
{
    // 检测延迟
    private readonly Histogram<double> _detectionLatency = 
        Metrics.CreateHistogram("vision_detection_latency_ms", "Detection latency in ms");
    
    // Worker 活跃数
    private readonly Gauge _activeWorkers = 
        Metrics.CreateGauge("vision_inspection_workers_active", "Number of active workers");
    
    // 队列深度（如使用队列）
    private readonly Gauge _queueDepth = 
        Metrics.CreateGauge("vision_inspection_queue_depth", "Pending inspection count");
    
    // 事件总线吞吐量
    private readonly Counter _eventsPublished = 
        Metrics.CreateCounter("vision_events_published_total", "Total events published", "event_type");
    
    public void RecordDetection(double latencyMs)
    {
        _detectionLatency.Observe(latencyMs);
    }
    
    public void IncrementActiveWorkers() => _activeWorkers.Inc();
    public void DecrementActiveWorkers() => _activeWorkers.Dec();
}
```

---

## 5. 科学实验设计（修正版）

### 5.1 样本量修正

| 数据集 | 原方案 | 修正后 | 说明 |
|--------|--------|--------|------|
| 开发集 | 30 张 | 30 张 | 用于算法调优 |
| 验证集 | 20 张 | **40 张** | 增至 40 张以满足统计检验力 |
| **总计** | 50 张 | **70 张** | McNemar 检验需要足够样本 |

### 5.2 实验设计细节

```
实验：LLM + 闭环反馈方向验证（修正版）

数据集：
- 包装带检测场景 70 张
- 子场景分布：
  * 正常光照：20 张（开发 10，验证 10）
  * 过曝：15 张（开发 8，验证 7）
  * 欠曝：15 张（开发 7，验证 8）
  * 轻微遮挡：20 张（开发 5，验证 15）

基线方法：
1. 人工调参（工程师 5 年经验）
2. 现有 ParameterRecommender（无 LLM）
3. LLM + 闭环反馈（待验证）

重复实验：
- 每种方法在每个子场景上重复 3 次
- **每次使用不同 LLM Temperature（0.7, 0.8, 0.9）**确保结果方差

统计检验：
- 配对检验：McNemar 检验（比较方法 2 和 3）
- 显著性水平：α = 0.05
- 检验力：目标 power ≥ 0.8（40 张验证集可达到）

验收阈值（需同时满足）：
1. 验证集准确率 ≥ 80%（vs 人工基线 ≥ 90%）
2. 平均调参耗时 ≤ 3 分钟（vs 人工基线 10 分钟）
3. 相比无 LLM 基线，准确率提升 ≥ 15%
4. McNemar 检验 p < 0.05（LLM vs 无 LLM 有显著差异）
```

---

## 6. 修正后的实施路线图

```
Week 1: Phase 1 - 核心稳定（Scoped Bug 根治）
├── Day 1-2: 
│   ├── 新增 IInspectionRuntimeCoordinator
│   ├── 新增 InspectionWorker（含并发控制、优雅关机）
│   └── DI 注册修正（HostedService）
├── Day 3-4:
│   ├── InspectionService 改为门面模式
│   ├── 删除实例字段 _realtimeCtsMap / _realtimeTasks
│   └── 三重异常保护实现
├── Day 5:
│   ├── 单元测试
│   ├── 优雅关机测试
│   └── 可观测性（Correlation ID、Metrics）
└── 交付物：代码 PR、测试报告

Week 2-3: Phase 2 - 事件机制（双栈过渡）【评审：2周偏紧，预留buffer】
├── Week 2:
│   ├── Day 1-2: IInspectionEventBus + 修正 SubscribeInterface
│   ├── Day 3: InMemoryEventStore（事件回放）
│   ├── Day 4: SSE 端点 + 断线重连
│   └── Day 5: SSE 心跳保活
├── Week 3:
│   ├── Day 1-2: WebMessageHandler 改为事件订阅者
│   ├── Day 3: 前端：优先 SSE，回退 WebMessage
│   ├── Day 4: 状态管理改为事件驱动
│   └── Day 5: 测试矩阵 + buffer
└── 交付物：代码 PR、测试矩阵报告

Week 4: Spike - 预览子图可行性
├── Spike-1: 现有调试缓存机制（1 天）
├── Spike-2: 前端预览调用链（1 天）
├── Spike-3: 中间算子输入来源（1 天）
├── Spike-4: PoC 子图执行（**3 天** - 含 BreakAtOperatorId 评估）
└── 交付物：Spike 报告、方案 A/B 决策

Week 5-6: Phase 3 - 预览子图
├── 基于 Spike 结果执行方案 A 或 B
└── 交付物：代码 PR、性能测试报告

Week 7-10: Phase 4 - LLM 闭环
├── 扩展预览反馈信息
├── AutoTuneService 实现
└── 科学实验验证（70 张样本）
```

---

## 7. 遗漏项补充

### 7.1 Coordinator 竞态窗口防护

```csharp
// InspectionRuntimeCoordinator.cs
public class InspectionRuntimeCoordinator : IInspectionRuntimeCoordinator
{
    // 每个 projectId 一把锁，避免全局锁竞争
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();
    
    public async Task<StartResult> TryStartAsync(Guid projectId, Guid sessionId, CancellationToken ct)
    {
        // 获取或创建该 projectId 的锁
        var lockObj = _locks.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));
        
        await lockObj.WaitAsync(ct);
        try
        {
            // 双重检查（避免锁等待期间状态变化）
            if (_sessions.TryGetValue(projectId, out var existing) && 
                existing.State == RuntimeState.Running)
            {
                return StartResult.AlreadyRunning;
            }
            
            // 创建新会话...
            var cts = new CancellationTokenSource();
            _ctsMap[projectId] = cts;
            _sessions[projectId] = new RuntimeSession 
            { 
                ProjectId = projectId,
                SessionId = sessionId,
                State = RuntimeState.Starting,
                StartedAt = DateTime.UtcNow,
                CancellationTokenSource = cts
            };
            
            return StartResult.Success;
        }
        finally
        {
            lockObj.Release();
        }
    }
    
    public CancellationToken GetCancellationToken(Guid projectId)
    {
        return _ctsMap.TryGetValue(projectId, out var cts) 
            ? cts.Token 
            : CancellationToken.None;
    }
}
```

---

**文档版本**: v2.1（评审后修正版）  
**实施状态**: 已准备好进入开发
