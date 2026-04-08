// InspectionWorker.cs
// 实时检测工作器
// 职责：在独立 Scope 中执行检测循环
// 生命周期：IHostedService（Singleton）
// 作者：架构修复方案 v2

using System.Collections.Concurrent;
using Acme.Product.Application.Analysis;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Events;
using Acme.Product.Core.Interfaces;
using Acme.Product.Core.Services;
using Acme.Product.Application.Services;
using Acme.Product.Infrastructure.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// 实时检测工作器实现
/// 特性：
/// 1. IHostedService - 纳入 Host 生命周期管理
/// 2. 三重异常保护
/// 3. 优雅关机支持
/// 4. 独立 Scope 执行
/// </summary>
public class InspectionWorker : IHostedService, IInspectionWorker, IAsyncDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IInspectionRuntimeCoordinator _coordinator;
    private readonly IInspectionEventBus _eventBus;
    private readonly ILogger<InspectionWorker> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly InspectionMetrics _metrics;
    private readonly IImageCacheRepository _imageCacheRepository;
    private readonly IAnalysisDataBuilder _analysisDataBuilder;

    // 并发控制：跟踪运行中的任务
    private readonly ConcurrentDictionary<Guid, RunningTaskEntry> _runningTasks = new();
    
    // 关机控制
    private readonly SemaphoreSlim _shutdownLock = new(1, 1);
    private readonly object _shutdownTaskSync = new();
    private Task? _gracefulShutdownTask;
    private volatile bool _isShuttingDown = false;
    private int _disposeState = 0;
    
    // 未处理异常记录
    private readonly ConcurrentDictionary<Guid, ExceptionRecord> _unhandledExceptions = new();

    private record RunningTaskEntry
    {
        public required Guid ProjectId { get; init; }
        public required Guid SessionId { get; init; }
        public required Task Task { get; init; }
        public required CancellationTokenSource Cts { get; init; }
        public required DateTime StartedAt { get; init; }
        public TaskCompletionSource<bool> ExitCompletion { get; init; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private class ExceptionRecord
    {
        public required Exception Exception { get; init; }
        public required DateTime Timestamp { get; init; }
        public required string Context { get; init; }
    }

    public InspectionWorker(
        IServiceScopeFactory scopeFactory,
        IInspectionRuntimeCoordinator coordinator,
        IInspectionEventBus eventBus,
        ILogger<InspectionWorker> logger,
        IHostApplicationLifetime lifetime,
        InspectionMetrics metrics,
        IImageCacheRepository imageCacheRepository,
        IAnalysisDataBuilder analysisDataBuilder)
    {
        _scopeFactory = scopeFactory;
        _coordinator = coordinator;
        _eventBus = eventBus;
        _logger = logger;
        _lifetime = lifetime;
        _metrics = metrics;
        _imageCacheRepository = imageCacheRepository;
        _analysisDataBuilder = analysisDataBuilder;

        // 订阅应用关闭事件
        _lifetime.ApplicationStopping.Register(OnApplicationStopping);
    }

    public InspectionWorker(
        IServiceScopeFactory scopeFactory,
        IInspectionRuntimeCoordinator coordinator,
        IInspectionEventBus eventBus,
        ILogger<InspectionWorker> logger,
        IHostApplicationLifetime lifetime,
        InspectionMetrics metrics)
        : this(
            scopeFactory,
            coordinator,
            eventBus,
            logger,
            lifetime,
            metrics,
            new Acme.Product.Infrastructure.Repositories.ImageCacheRepository(),
            new AnalysisDataBuilder())
    {
    }

    public InspectionWorker(
        IServiceScopeFactory scopeFactory,
        IInspectionRuntimeCoordinator coordinator,
        IInspectionEventBus eventBus,
        ILogger<InspectionWorker> logger,
        IHostApplicationLifetime lifetime,
        InspectionMetrics metrics,
        IAnalysisDataBuilder analysisDataBuilder)
        : this(
            scopeFactory,
            coordinator,
            eventBus,
            logger,
            lifetime,
            metrics,
            new Acme.Product.Infrastructure.Repositories.ImageCacheRepository(),
            analysisDataBuilder)
    {
    }

    #region IHostedService Implementation

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[InspectionWorker] 工作器已启动，准备接受任务");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await EnsureGracefulShutdownAsync(cancellationToken);
    }

    #endregion

    #region IInspectionWorker Implementation

    public async Task<bool> TryStartRunAsync(Guid projectId, Guid sessionId, OperatorFlow flow, string? cameraId)
    {
        if (_isShuttingDown)
        {
            _logger.LogWarning("[InspectionWorker] 拒绝新任务，正在关机中: {ProjectId}", projectId);
            return false;
        }

        // 防重复启动检查
        if (_runningTasks.ContainsKey(projectId))
        {
            _logger.LogWarning("[InspectionWorker] 项目 {ProjectId} 已有运行中任务", projectId);
            return false;
        }

        // 创建新的 CancellationTokenSource（与 HTTP 请求无关）
        var coordinatorToken = _coordinator.GetCancellationToken(projectId);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(coordinatorToken);
        var exitCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        
        // 创建任务（使用 Task.Run 但不会立即等待）
        var task = Task.Run(async () =>
        {
            await RunWithTripleExceptionProtectionAsync(projectId, sessionId, flow, cameraId, cts.Token);
        }, cts.Token);

        var taskEntry = new RunningTaskEntry
        {
            ProjectId = projectId,
            SessionId = sessionId,
            Cts = cts,
            StartedAt = DateTime.UtcNow,
            Task = task,
            ExitCompletion = exitCompletion
        };

        // 注册到运行任务字典
        if (!_runningTasks.TryAdd(projectId, taskEntry))
        {
            // 竞争失败，取消任务
            await cts.CancelAsync();
            cts.Dispose();
            return false;
        }

        // 任务完成后清理
        // Clean up bookkeeping when the background task exits.
        _ = task
            .ContinueWith(_ => CleanupTaskAsync(projectId, taskEntry), TaskScheduler.Default)
            .Unwrap();

        _metrics.IncrementActiveWorkers();
        _metrics.UpdateActiveSessions(_runningTasks.Count);

        _logger.LogInformation("[InspectionWorker] 任务已启动: {ProjectId}, Session: {SessionId}", 
            projectId, sessionId);

        return true;
    }

    public async Task<bool> WaitForRunExitAsync(
        Guid projectId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (!_runningTasks.TryGetValue(projectId, out var entry))
        {
            return true;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await entry.ExitCompletion.Task.WaitAsync(timeoutCts.Token);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    #endregion

    #region Core Execution Logic

    /// <summary>
    /// 三重异常保护
    /// 第一层：Worker 级
    /// 第二层：Coordinator 级
    /// 第三层：Scope 级（在 RunWithScopeAsync 中）
    /// </summary>
    private async Task RunWithTripleExceptionProtectionAsync(
        Guid projectId,
        Guid sessionId,
        OperatorFlow flow,
        string? cameraId,
        CancellationToken ct)
    {
        // 第一层保护：Worker 级
        try
        {
            // 更新状态为 Running
            _coordinator.UpdateSessionStatus(projectId, sessionId, RuntimeStatus.Running);

            await RunWithScopeAsync(projectId, sessionId, flow, cameraId, ct);
            EnsureStoppedState(projectId, sessionId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation("[InspectionWorker] 任务正常取消: {ProjectId}", projectId);
            await _eventBus.PublishAsync(new InspectionStateChangedEvent
            {
                ProjectId = projectId,
                SessionId = sessionId,
                OldState = "Running",
                NewState = "Stopped"
            }, CancellationToken.None);
            _coordinator.MarkAsStopped(projectId, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[InspectionWorker] 任务异常: {ProjectId}", projectId);

            // 第二层保护：Coordinator 级
            try
            {
                _coordinator.MarkAsFaulted(projectId, sessionId, ex.Message);
            }
            catch (Exception coordEx)
            {
                _logger.LogCritical(coordEx, "[InspectionWorker] Coordinator 也失败，记录到未处理异常");
                _unhandledExceptions[sessionId] = new ExceptionRecord
                {
                    Exception = ex,
                    Timestamp = DateTime.UtcNow,
                    Context = $"Project: {projectId}, Session: {sessionId}"
                };
            }
        }
    }

    /// <summary>
    /// 在独立 Scope 中执行检测循环
    /// 第三层保护：Scope 级
    /// </summary>
    private async Task RunWithScopeAsync(
        Guid projectId,
        Guid sessionId,
        OperatorFlow flow,
        string? cameraId,
        CancellationToken ct)
    {
        // 第三层保护：Scope 级
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            
            // 解析 Scoped 服务
            var flowExecution = scope.ServiceProvider.GetRequiredService<IFlowExecutionService>();
            var imageAcquisition = scope.ServiceProvider.GetRequiredService<IImageAcquisitionService>();
            var resultChannelWriter = scope.ServiceProvider.GetRequiredService<IInspectionResultChannelWriter>();
            var projectRepository = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<InspectionWorker>>();

            // 创建日志上下文
            // Create the logging / correlation scope for this run.
            using var inspectionScope = InspectionContext.BeginScope(projectId, sessionId);
            var correlationId = InspectionContext.Current?.CorrelationId ?? sessionId;

            using (logger.BeginInspectionScope(correlationId, projectId, sessionId))
            {
                logger.LogInformation("[InspectionWorker] 开始检测循环");

                // 发布状态变更事件：Running
                await _eventBus.PublishAsync(new InspectionStateChangedEvent
                {
                    ProjectId = projectId,
                    SessionId = sessionId,
                    OldState = "Starting",
                    NewState = "Running"
                }, ct);

                // 执行检测循环
                await RunRealtimeLoopAsync(
                    projectId,
                    sessionId,
                    flow,
                    cameraId,
                    flowExecution,
                    imageAcquisition,
                    resultChannelWriter,
                    ct);

                logger.LogInformation("[InspectionWorker] 检测循环结束");

                // 发布状态变更事件：Stopped
                await _eventBus.PublishAsync(new InspectionStateChangedEvent
                {
                    ProjectId = projectId,
                    SessionId = sessionId,
                    OldState = "Running",
                    NewState = "Stopped"
                }, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[InspectionWorker] Scope 级异常: {ProjectId}", projectId);
            
            // 发布故障事件
            await _eventBus.PublishAsync(new InspectionStateChangedEvent
            {
                ProjectId = projectId,
                SessionId = sessionId,
                OldState = "Running",
                NewState = "Faulted",
                ErrorMessage = ex.Message
            }, CancellationToken.None);
            
            throw; // 抛到外层，由第一层保护处理
        }
    }

    /// <summary>
    /// 实时检测循环
    /// </summary>
    private async Task RunRealtimeLoopAsync(
        Guid projectId,
        Guid sessionId,
        OperatorFlow flow,
        string? cameraId,
        IFlowExecutionService flowExecution,
        IImageAcquisitionService imageAcquisition,
        IInspectionResultChannelWriter resultChannelWriter,
        CancellationToken ct)
    {
        const int minIntervalMs = 100;
        const int maxIntervalMs = 5000;
        int currentIntervalMs = 500;
        int cycleCount = 0;
        int consecutiveNgCount = 0;

        while (!ct.IsCancellationRequested)
        {
            var startTime = DateTime.UtcNow;
            cycleCount++;

            try
            {
                _logger.LogDebug("[InspectionWorker] 开始第 {CycleCount} 轮检测", cycleCount);

                // 执行单轮检测
                var result = await ExecuteCycleAsync(
                    projectId, flow, cameraId, flowExecution, imageAcquisition, ct);

                // 保存结果(异步非阻塞)
                resultChannelWriter.TryWrite(result);

                // sessionId belongs to the realtime event stream. The InspectionResult aggregate itself
                // only captures the project-scoped inspection payload and does not persist session identifiers.
                await _eventBus.PublishAsync(new InspectionResultEvent
                {
                    ProjectId = projectId,
                    SessionId = sessionId,
                    ResultId = result.Id,
                    ImageId = result.ImageId,
                    Status = result.Status.ToString(),
                    DefectCount = result.Defects.Count,
                    ProcessingTimeMs = result.ProcessingTimeMs,
                    OutputImageBase64 = result.OutputImage != null ? Convert.ToBase64String(result.OutputImage) : null,
                    OutputData = AnalysisPayloadSerialization.DeserializeJsonDictionary(result.OutputDataJson),
                    AnalysisData = AnalysisPayloadSerialization.DeserializeJsonDictionary(result.AnalysisDataJson)
                }, ct);
                _metrics.RecordDetectionLatency(result.ProcessingTimeMs, result.Status.ToString());
                _metrics.RecordInspectionCompleted(result.Status.ToString(), result.Defects.Count);

                // 【架构修复 v2】发布进度事件
                await _eventBus.PublishAsync(new InspectionProgressEvent
                {
                    ProjectId = projectId,
                    SessionId = sessionId,
                    ProcessedCount = cycleCount
                }, ct);

                // 连续 NG 检查
                if (result.Status == InspectionStatus.NG)
                {
                    consecutiveNgCount++;
                    // TODO: 从配置读取阈值
                    if (consecutiveNgCount >= 5)
                    {
                        _logger.LogWarning("连续 NG 达到阈值，停止检测: {ProjectId}", projectId);
                        break;
                    }
                }
                else
                {
                    consecutiveNgCount = 0;
                }

                currentIntervalMs = 500;
            }
            catch (OperationCanceledException)
            {
                throw; // 正常取消，退出循环
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[InspectionWorker] 第 {CycleCount} 轮检测异常", cycleCount);
                _metrics.RecordInspectionFailed(ex.GetType().Name);
                currentIntervalMs = Math.Min(currentIntervalMs * 2, maxIntervalMs);
            }

            // 计算间隔
            var elapsedMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            var delayMs = Math.Max(currentIntervalMs - elapsedMs, minIntervalMs);

            try
            {
                await Task.Delay(delayMs, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("[InspectionWorker] 检测循环结束，共执行 {CycleCount} 轮", cycleCount);
    }

    /// <summary>
    /// 执行单轮检测
    /// </summary>
    private async Task<InspectionResult> ExecuteCycleAsync(
        Guid projectId,
        OperatorFlow flow,
        string? cameraId,
        IFlowExecutionService flowExecution,
        IImageAcquisitionService imageAcquisition,
        CancellationToken ct)
    {
        // InspectionResult is a project-level domain entity. Session metadata is propagated via events,
        // not stored on the aggregate itself.
        var result = new InspectionResult(projectId);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // 准备输入数据
            var inputData = new Dictionary<string, object>();

            // 如果指定了相机，预加载图像
            if (!string.IsNullOrEmpty(cameraId))
            {
                try
                {
                    var imageDto = await imageAcquisition.AcquireFromCameraAsync(cameraId);
                    if (!string.IsNullOrEmpty(imageDto.DataBase64))
                    {
                        var imageData = Convert.FromBase64String(imageDto.DataBase64);
                        inputData["Image"] = imageData;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[InspectionWorker] 预加载相机图像失败");
                }
            }

            // 执行流程
            var flowResult = await flowExecution.ExecuteFlowAsync(flow, inputData, cancellationToken: ct);

            stopwatch.Stop();
            _metrics.RecordFlowExecutionLatency(flowResult.ExecutionTimeMs, flowResult.IsSuccess);

            // 判定状态
            InspectionStatus status;
            if (!flowResult.IsSuccess)
            {
                status = InspectionStatus.Error;
            }
            else
            {
                status = DetermineStatusFromFlowOutput(flowResult.OutputData);
            }

            result.SetResult(status, flowResult.ExecutionTimeMs, null, flowResult.ErrorMessage);

            // 提取输出图像
            if (flowResult.OutputData?.TryGetValue("Image", out var outputImage) == true
                && outputImage is byte[] imageBytes)
            {
                result.SetOutputImage(imageBytes);
            }

            // 提取缺陷列表
            if (flowResult.OutputData?.TryGetValue("Defects", out var defectsObj) == true
                && defectsObj is System.Collections.IList defectsList)
            {
                foreach (var item in defectsList)
                {
                    if (item is Dictionary<string, object> defectDict)
                    {
                        var defect = new Defect(
                            result.Id,
                            DefectType.Other,
                            Convert.ToDouble(defectDict.GetValueOrDefault("X", 0.0)),
                            Convert.ToDouble(defectDict.GetValueOrDefault("Y", 0.0)),
                            Convert.ToDouble(defectDict.GetValueOrDefault("Width", 0.0)),
                            Convert.ToDouble(defectDict.GetValueOrDefault("Height", 0.0)),
                            Convert.ToDouble(defectDict.GetValueOrDefault("Confidence", 0.0)),
                            defectDict.GetValueOrDefault("ClassName", "unknown")?.ToString() ?? "unknown"
                        );
                        result.AddDefect(defect);
                    }
                }
            }

            var analysisData = _analysisDataBuilder.Build(flow, flowResult, status);
            AnalysisPayloadSerialization.TrySetOutputDataJson(result, flowResult.OutputData, _logger);
            AnalysisPayloadSerialization.TrySetAnalysisDataJson(result, analysisData, _logger);
            await CacheResultImageAsync(result);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.MarkAsError(ex.Message);
            throw;
        }
    }

    private InspectionStatus DetermineStatusFromFlowOutput(Dictionary<string, object>? outputData)
    {
        if (outputData == null)
            return InspectionStatus.OK;

        if (outputData.TryGetValue("JudgmentResult", out var judgmentResult)
            && judgmentResult is string judgment)
        {
            return judgment.Equals("OK", StringComparison.OrdinalIgnoreCase)
                ? InspectionStatus.OK
                : InspectionStatus.NG;
        }

        if (outputData.TryGetValue("IsOk", out var isOk) && isOk is bool isOkBool)
        {
            return isOkBool ? InspectionStatus.OK : InspectionStatus.NG;
        }

        if (outputData.TryGetValue("DefectCount", out var dc) && dc is int defectCount)
        {
            return defectCount > 0 ? InspectionStatus.NG : InspectionStatus.OK;
        }

        return InspectionStatus.OK;
    }

    private async Task CacheResultImageAsync(InspectionResult result)
    {
        if (result.OutputImage == null || result.OutputImage.Length == 0)
        {
            return;
        }

        try
        {
            var imageId = await _imageCacheRepository.AddAsync(result.OutputImage, GuessImageFormat(result.OutputImage));
            if (imageId != Guid.Empty)
            {
                result.SetImageId(imageId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[InspectionWorker] 结果图像缓存失败");
        }
    }

    private static string GuessImageFormat(byte[] bytes)
    {
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return "png";
        }

        if (bytes.Length >= 3 &&
            bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "jpg";
        }

        if (bytes.Length >= 2 && bytes[0] == 0x42 && bytes[1] == 0x4D)
        {
            return "bmp";
        }

        return "bin";
    }

    #endregion

    #region Graceful Shutdown

    private void OnApplicationStopping()
    {
        _logger.LogInformation("[InspectionWorker] 应用程序正在关闭...");
        _isShuttingDown = true;
    }

    private Task EnsureGracefulShutdownAsync(CancellationToken cancellationToken)
    {
        Task shutdownTask;

        lock (_shutdownTaskSync)
        {
            _gracefulShutdownTask ??= PerformGracefulShutdownCoreAsync();
            shutdownTask = _gracefulShutdownTask;
        }

        return cancellationToken.CanBeCanceled
            ? shutdownTask.WaitAsync(cancellationToken)
            : shutdownTask;
    }

    private async Task PerformGracefulShutdownCoreAsync()
    {
        _isShuttingDown = true;

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await _shutdownLock.WaitAsync(timeoutCts.Token);
        try
        {
            var activeTasks = _runningTasks.Values.ToList();
            _logger.LogInformation("[InspectionWorker] 开始优雅关机，等待 {Count} 个任务完成", activeTasks.Count);

            // 1. 取消所有运行中的任务
            foreach (var entry in activeTasks)
            {
                try
                {
                    await entry.Cts.CancelAsync();
                    _logger.LogDebug("[InspectionWorker] 已发送取消信号: {ProjectId}", entry.ProjectId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[InspectionWorker] 取消任务失败: {ProjectId}", entry.ProjectId);
                }
            }

            // 2. 等待所有任务完成
            if (activeTasks.Count > 0)
            {
                var tasks = activeTasks.Select(e => e.Task).ToArray();
                try
                {
                    await Task.WhenAll(tasks).WaitAsync(timeoutCts.Token);
                    _logger.LogInformation("[InspectionWorker] 所有任务已正常完成");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("[InspectionWorker] 优雅关机超时，强制退出");
                }
            }

            // 3. 报告未处理异常
            if (!_unhandledExceptions.IsEmpty)
            {
                _logger.LogError("[InspectionWorker] 关机时发现 {Count} 个未处理异常", 
                    _unhandledExceptions.Count);
                foreach (var record in _unhandledExceptions.Values)
                {
                    _logger.LogError(record.Exception, "[InspectionWorker] 未处理异常: {Context}", record.Context);
                }
            }
        }
        finally
        {
            _shutdownLock.Release();
        }
    }

    private async Task CleanupTaskAsync(Guid projectId, RunningTaskEntry entry)
    {
        var removed = _runningTasks.TryRemove(projectId, out _);
        EnsureStoppedState(projectId, entry.SessionId);
        
        try
        {
            await entry.Cts.CancelAsync();
            entry.Cts.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[InspectionWorker] 清理任务资源时异常: {ProjectId}", projectId);
        }

        if (removed)
        {
            _metrics.DecrementActiveWorkers();
            _metrics.UpdateActiveSessions(_runningTasks.Count);
        }

        entry.ExitCompletion.TrySetResult(true);
    }

    private void EnsureStoppedState(Guid projectId, Guid sessionId)
    {
        var state = _coordinator.GetState(projectId);
        if (state == null)
        {
            return;
        }

        if (state.SessionId != sessionId)
        {
            return;
        }

        if (state.Status is RuntimeStatus.Starting or RuntimeStatus.Running or RuntimeStatus.Stopping)
        {
            _coordinator.MarkAsStopped(projectId, sessionId);
        }
    }

    #endregion

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        try
        {
            await EnsureGracefulShutdownAsync(CancellationToken.None);
        }
        finally
        {
            _shutdownLock.Dispose();
        }
    }

    #endregion
}
