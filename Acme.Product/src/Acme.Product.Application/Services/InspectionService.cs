// InspectionService.cs
// 检测应用服务
// 职责：API 门面，协调单次检测和实时检测
// 生命周期：Scoped（无状态，不保存运行时状态）
// 作者：蘅芜君 + 架构修复方案 v2

using Acme.Product.Application.Analysis;
using Acme.Product.Application.DTOs;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Exceptions;
using Acme.Product.Core.Interfaces;
using Acme.Product.Core.Services;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Text.Json;

// 【架构修复 v2】IInspectionWorker 从 Infrastructure 移到 Core

namespace Acme.Product.Application.Services;

/// <summary>
/// 检测应用服务
/// 【架构修复 v2】移除实例字段，改为纯门面模式
/// 实时检测状态由 IInspectionRuntimeCoordinator（Singleton）管理
/// 实时检测执行由 IInspectionWorker（HostedService）执行
/// </summary>
public class InspectionService : IInspectionService
{
    private readonly IInspectionResultRepository _resultRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IFlowExecutionService _flowExecutionService;
    private readonly IImageAcquisitionService _imageAcquisitionService;
    private readonly IConfigurationService _configurationService;
    private readonly IInspectionRuntimeCoordinator _coordinator;
    private readonly IInspectionWorker _worker;
    private readonly IImageCacheRepository _imageCacheRepository;
    private readonly IAnalysisDataBuilder _analysisDataBuilder;
    private readonly IProjectFlowStorage _flowStorage;
    private readonly ILogger<InspectionService> _logger;
    private static readonly JsonSerializerOptions FlowJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public InspectionService(
        IInspectionResultRepository resultRepository,
        IProjectRepository projectRepository,
        IFlowExecutionService flowExecutionService,
        IImageAcquisitionService imageAcquisitionService,
        IConfigurationService configurationService,
        IInspectionRuntimeCoordinator coordinator,
        IInspectionWorker worker,
        IImageCacheRepository imageCacheRepository,
        IAnalysisDataBuilder analysisDataBuilder,
        IProjectFlowStorage flowStorage,
        ILogger<InspectionService> logger)
    {
        _resultRepository = resultRepository;
        _projectRepository = projectRepository;
        _flowExecutionService = flowExecutionService;
        _imageAcquisitionService = imageAcquisitionService;
        _configurationService = configurationService;
        _coordinator = coordinator;
        _worker = worker;
        _imageCacheRepository = imageCacheRepository;
        _analysisDataBuilder = analysisDataBuilder;
        _flowStorage = flowStorage;
        _logger = logger;
    }

    public InspectionService(
        IInspectionResultRepository resultRepository,
        IProjectRepository projectRepository,
        IFlowExecutionService flowExecutionService,
        IImageAcquisitionService imageAcquisitionService,
        IConfigurationService configurationService,
        IInspectionRuntimeCoordinator coordinator,
        IInspectionWorker worker,
        IImageCacheRepository imageCacheRepository,
        ILogger<InspectionService> logger)
        : this(
            resultRepository,
            projectRepository,
            flowExecutionService,
            imageAcquisitionService,
            configurationService,
            coordinator,
            worker,
            imageCacheRepository,
            new AnalysisDataBuilder(),
            new NoOpProjectFlowStorage(),
            logger)
    {
    }

    public InspectionService(
        IInspectionResultRepository resultRepository,
        IProjectRepository projectRepository,
        IFlowExecutionService flowExecutionService,
        IImageAcquisitionService imageAcquisitionService,
        IConfigurationService configurationService,
        IInspectionRuntimeCoordinator coordinator,
        IInspectionWorker worker,
        IAnalysisDataBuilder analysisDataBuilder,
        ILogger<InspectionService> logger)
        : this(
            resultRepository,
            projectRepository,
            flowExecutionService,
            imageAcquisitionService,
            configurationService,
            coordinator,
            worker,
            new NoOpImageCacheRepository(),
            analysisDataBuilder,
            new NoOpProjectFlowStorage(),
            logger)
    {
    }

    public InspectionService(
        IInspectionResultRepository resultRepository,
        IProjectRepository projectRepository,
        IFlowExecutionService flowExecutionService,
        IImageAcquisitionService imageAcquisitionService,
        IConfigurationService configurationService,
        IInspectionRuntimeCoordinator coordinator,
        IInspectionWorker worker,
        ILogger<InspectionService> logger)
        : this(
            resultRepository,
            projectRepository,
            flowExecutionService,
            imageAcquisitionService,
            configurationService,
            coordinator,
            worker,
            new NoOpImageCacheRepository(),
            new AnalysisDataBuilder(),
            new NoOpProjectFlowStorage(),
            logger)
    {
    }

    #region 单次检测

    public async Task<InspectionResult> ExecuteSingleAsync(Guid projectId, byte[] imageData)
    {
        return await ExecuteSingleAsync(projectId, imageData, null);
    }

    public async Task<InspectionResult> ExecuteSingleAsync(Guid projectId, byte[] imageData, OperatorFlow? flow)
    {
        return await ExecuteSingleCoreAsync(projectId, imageData, flow);
#if false
        var actualFlow = await ResolveExecutionFlowAsync(projectId, flow);
        if (flow != null)
        {
            actualFlow = flow;
            _logger.LogInformation("[InspectionService] 使用前端提供的流程数据执行检测 (算子数: {OperatorCount})", flow.Operators?.Count ?? 0);
        }
        else
        {
            var project = await _projectRepository.GetWithFlowAsync(projectId);
            if (project == null)
                throw new ProjectNotFoundException(projectId);
            actualFlow = project.Flow;
        }

        var result = new InspectionResult(projectId);

        try
        {
            var flowResult = await _flowExecutionService.ExecuteFlowAsync(
                actualFlow,
                new Dictionary<string, object> { { "Image", imageData } });

            InspectionStatus status;
            if (!flowResult.IsSuccess)
            {
                status = InspectionStatus.Error;
                _logger.LogWarning("[InspectionService] 流程执行失败: {ErrorMessage}", flowResult.ErrorMessage);
            }
            else
            {
                status = DetermineStatusFromFlowOutput(flowResult.OutputData);
                _logger.LogInformation("[InspectionService] 判定结果: {Status}", status);
            }

            result.SetResult(status, flowResult.ExecutionTimeMs, null, flowResult.ErrorMessage);

            if (flowResult.OutputData?.TryGetValue("Image", out var outputImage) == true
                && outputImage is byte[] imageBytes)
            {
                result.SetOutputImage(imageBytes);
            }

            // 提取缺陷列表
            if (flowResult.OutputData?.TryGetValue("Defects", out var defectsObj) == true
                && defectsObj is IList defectsList)
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

            var analysisData = _analysisDataBuilder.Build(actualFlow, flowResult, status);
            AnalysisPayloadSerialization.TrySetOutputDataJson(result, flowResult.OutputData, _logger);
            AnalysisPayloadSerialization.TrySetAnalysisDataJson(result, analysisData, _logger);
            await PersistResultImageAsync(result, CancellationToken.None);
            await CacheResultImageAsync(result);
            await _resultRepository.AddAsync(result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[InspectionService] 检测异常: {ErrorMessage}", ex.Message);
            result.MarkAsError(ex.Message);
            await _resultRepository.AddAsync(result);
            return result;
        }
#endif
    }

    public async Task<InspectionResult> ExecuteSingleAsync(Guid projectId, string cameraId)
    {
        return await ExecuteSingleAsync(projectId, cameraId, null);
#if false
        try
        {
            var imageDto = await _imageAcquisitionService.AcquireFromCameraAsync(cameraId);

            if (string.IsNullOrEmpty(imageDto.DataBase64))
            {
                throw new Exception($"相机 {cameraId} 采集的图像数据为空");
            }

            var imageData = Convert.FromBase64String(imageDto.DataBase64);
            return await ExecuteSingleAsync(projectId, imageData);
        }
        catch (Exception ex)
        {
            var result = new InspectionResult(projectId);
            result.MarkAsError($"相机采集或检测失败: {ex.Message}");
            await _resultRepository.AddAsync(result);
            throw;
        }
#endif
    }

    public async Task<InspectionResult> ExecuteSingleAsync(Guid projectId, string cameraId, OperatorFlow? flow)
    {
        return await ExecuteSingleFromCameraCoreAsync(projectId, cameraId, flow);
    }

    #endregion

    #region 实时检测（门面模式）

    /// <summary>
    /// 【架构修复 v2】改为门面模式：委托给 Coordinator 和 Worker
    /// </summary>
    public async Task StartRealtimeInspectionAsync(
        Guid projectId,
        string? cameraId,
        CancellationToken cancellationToken,
        Action<InspectionResult>? onResultReady = null)
    {
        var project = await _projectRepository.GetWithFlowAsync(projectId);
        if (project == null)
            throw new ProjectNotFoundException(projectId);

        await StartRealtimeInspectionFlowAsync(projectId, project.Flow, cameraId, cancellationToken, onResultReady);
    }

    /// <summary>
    /// 【架构修复 v2】实时检测启动入口
    /// 1. 调用 Coordinator 注册会话
    /// 2. 调用 Worker 启动后台任务
    /// </summary>
    public async Task StartRealtimeInspectionFlowAsync(
        Guid projectId,
        OperatorFlow flow,
        string? cameraId,
        CancellationToken cancellationToken,
        Action<InspectionResult>? onResultReady = null)
    {
        var sessionId = Guid.NewGuid();

        _logger.LogInformation(
            "[InspectionService] 请求启动实时检测: ProjectId={ProjectId}, SessionId={SessionId}, CameraId={CameraId}",
            projectId, sessionId, cameraId ?? "(流程内)");

        // 步骤 1：注册会话（Coordinator 保证原子性）
        var startResult = await _coordinator.TryStartAsync(projectId, sessionId, cancellationToken);

        switch (startResult)
        {
            case StartResult.AlreadyRunning:
                _logger.LogWarning("[InspectionService] 实时检测已在运行: {ProjectId}", projectId);
                throw new InvalidOperationException("实时检测已在运行");

            case StartResult.ShutdownInProgress:
                _logger.LogWarning("[InspectionService] 系统正在关机，无法启动: {ProjectId}", projectId);
                throw new InvalidOperationException("系统正在关机，请稍后重试");
        }

        // 步骤 2：启动 Worker（不等待完成，Fire-and-forget）
        var workerStarted = await _worker.TryStartRunAsync(projectId, sessionId, flow, cameraId);

        if (!workerStarted)
        {
            // Worker 启动失败，回滚 Coordinator 状态
            _logger.LogError("[InspectionService] Worker 启动失败，回滚状态: {ProjectId}", projectId);
            await _coordinator.TryStopAsync(projectId, cancellationToken);
            throw new InvalidOperationException("实时检测启动失败，请重试");
        }

        _logger.LogInformation(
            "[InspectionService] 实时检测已启动: ProjectId={ProjectId}, SessionId={SessionId}",
            projectId, sessionId);

        // 注意：不再使用 Task.Run + onResultReady 回调
        // 结果通过事件总线推送（Phase 2 实现）
    }

    /// <summary>
    /// 【架构修复 v2】实时检测停止入口
    /// 委托给 Coordinator 处理
    /// </summary>
    public async Task StopRealtimeInspectionAsync(Guid projectId)
    {
        _logger.LogInformation("[InspectionService] 请求停止实时检测: {ProjectId}", projectId);

        var stopped = await _coordinator.TryStopAsync(projectId, CancellationToken.None);

        if (stopped)
        {
            var workerExited = await _worker.WaitForRunExitAsync(
                projectId,
                TimeSpan.FromSeconds(3),
                CancellationToken.None);

            if (!workerExited)
            {
                _logger.LogError("[InspectionService] Stop timeout, worker is still running: {ProjectId}", projectId);
                throw new InvalidOperationException("实时检测停止超时，后台任务仍未退出。");
            }

            var stateReleased = await WaitForStateReleaseAsync(projectId, TimeSpan.FromSeconds(3));
            if (!stateReleased)
            {
                _logger.LogError("[InspectionService] Stop completed but runtime state was not released: {ProjectId}", projectId);
                throw new InvalidOperationException("实时检测停止后状态仍未释放。");
            }
            _logger.LogInformation("[InspectionService] 实时检测停止请求已发送: {ProjectId}", projectId);
        }
        else
        {
            _logger.LogWarning("[InspectionService] 未找到运行中的实时检测: {ProjectId}", projectId);
        }
    }

    /// <summary>
    /// 获取实时检测状态
    /// </summary>
    public RuntimeState? GetRealtimeState(Guid projectId)
    {
        return _coordinator.GetState(projectId);
    }

    private async Task<bool> WaitForStateReleaseAsync(Guid projectId, TimeSpan timeout)
    {
        var startedAt = DateTime.UtcNow;
        while (DateTime.UtcNow - startedAt <= timeout)
        {
            if (_coordinator.GetState(projectId) == null)
            {
                return true;
            }

            await Task.Delay(50);
        }

        return _coordinator.GetState(projectId) == null;
    }

    #endregion

    #region 查询方法

    public async Task<InspectionHistoryPage> GetInspectionHistoryAsync(
        Guid projectId,
        DateTime? startTime,
        DateTime? endTime,
        string? status,
        string? defectType,
        int pageIndex,
        int pageSize)
    {
        return await _resultRepository.GetHistoryPageAsync(projectId, startTime, endTime, status, defectType, pageIndex, pageSize);
    }

    public async Task<InspectionStatistics> GetStatisticsAsync(
        Guid projectId,
        DateTime? startTime,
        DateTime? endTime,
        string? status,
        string? defectType)
    {
        return await _resultRepository.GetStatisticsAsync(projectId, startTime, endTime, status, defectType);
    }

    #endregion

    #region 辅助方法

    private async Task<InspectionResult> ExecuteSingleCoreAsync(Guid projectId, byte[]? imageData, OperatorFlow? flow)
    {
        var actualFlow = await ResolveExecutionFlowAsync(projectId, flow);
        var result = new InspectionResult(projectId);

        try
        {
            var executionInputs = new Dictionary<string, object>();
            if (imageData != null && imageData.Length > 0)
            {
                executionInputs["Image"] = imageData;
            }

            var flowResult = await _flowExecutionService.ExecuteFlowAsync(actualFlow, executionInputs);
            var outputData = flowResult.OutputData ?? new Dictionary<string, object>();
            flowResult.OutputData = outputData;

            var judgmentEvaluation = flowResult.IsSuccess
                ? DetermineStatusFromFlowOutput(outputData)
                : new InspectionJudgmentEvaluation(
                    InspectionStatus.Error,
                    "FlowExecution",
                    string.IsNullOrWhiteSpace(flowResult.ErrorMessage)
                        ? "FlowExecutionFailed"
                        : $"FlowExecutionFailed:{flowResult.ErrorMessage}",
                    false);

            SetJudgmentDiagnostics(outputData, judgmentEvaluation);
            var status = judgmentEvaluation.Status;

            if (!flowResult.IsSuccess)
            {
                _logger.LogWarning("[InspectionService] 娴佺▼鎵ц澶辫触: {ErrorMessage}", flowResult.ErrorMessage);
            }
            else
            {
                _logger.LogInformation("[InspectionService] 鍒ゅ畾缁撴灉: {Status}", status);
            }

            var errorMessage = !flowResult.IsSuccess
                ? (string.IsNullOrWhiteSpace(flowResult.ErrorMessage) ? judgmentEvaluation.StatusReason : flowResult.ErrorMessage)
                : (status == InspectionStatus.Error ? judgmentEvaluation.StatusReason : flowResult.ErrorMessage);

            result.SetResult(status, flowResult.ExecutionTimeMs, null, errorMessage);

            if (flowResult.OutputData?.TryGetValue("Image", out var outputImage) == true
                && outputImage is byte[] imageBytes)
            {
                result.SetOutputImage(imageBytes);
            }

            if (flowResult.OutputData?.TryGetValue("Defects", out var defectsObj) == true
                && defectsObj is IList defectsList)
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
                            defectDict.GetValueOrDefault("ClassName", "unknown")?.ToString() ?? "unknown");
                        result.AddDefect(defect);
                    }
                }
            }

            var analysisData = _analysisDataBuilder.Build(actualFlow, flowResult, status);
            AnalysisPayloadSerialization.TrySetOutputDataJson(result, flowResult.OutputData, _logger);
            AnalysisPayloadSerialization.TrySetAnalysisDataJson(result, analysisData, _logger);
            await PersistResultImageAsync(result, CancellationToken.None);
            await CacheResultImageAsync(result);
            await _resultRepository.AddAsync(result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[InspectionService] 妫€娴嬪紓甯? {ErrorMessage}", ex.Message);
            result.MarkAsError(ex.Message);
            await _resultRepository.AddAsync(result);
            return result;
        }
    }

    private async Task<InspectionResult> ExecuteSingleFromCameraCoreAsync(Guid projectId, string cameraId, OperatorFlow? flow)
    {
        try
        {
            var imageDto = await _imageAcquisitionService.AcquireFromCameraAsync(cameraId);

            if (string.IsNullOrEmpty(imageDto.DataBase64))
            {
                throw new Exception($"鐩告満 {cameraId} 閲囬泦鐨勫浘鍍忔暟鎹负绌?");
            }

            var imageData = Convert.FromBase64String(imageDto.DataBase64);
            return await ExecuteSingleCoreAsync(projectId, imageData, flow);
        }
        catch (Exception ex)
        {
            var result = new InspectionResult(projectId);
            result.MarkAsError($"鐩告満閲囬泦鎴栨娴嬪け璐? {ex.Message}");
            await _resultRepository.AddAsync(result);
            return result;
        }
    }

    private async Task<OperatorFlow> ResolveExecutionFlowAsync(Guid projectId, OperatorFlow? flow)
    {
        if (HasExecutableFlow(flow))
        {
            _logger.LogInformation(
                "[InspectionService] 浣跨敤鍓嶇鎻愪緵鐨勬祦绋嬫暟鎹墽琛屾娴? (绠楀瓙鏁? {OperatorCount})",
                flow!.Operators.Count);
            return flow;
        }

        var project = await _projectRepository.GetWithFlowAsync(projectId);
        if (project == null)
        {
            throw new ProjectNotFoundException(projectId);
        }

        if (HasExecutableFlow(project.Flow))
        {
            return project.Flow;
        }

        var fileFlow = await LoadFlowFromStorageAsync(projectId);
        if (HasExecutableFlow(fileFlow))
        {
            _logger.LogWarning(
                "[InspectionService] 椤圭洰 {ProjectId} 鏁版嵁搴撴祦绋嬩负绌猴紝宸插洖閫€鍒?ProjectFlows 鏂囦欢娴? (绠楀瓙鏁? {OperatorCount})",
                projectId,
                fileFlow!.Operators.Count);
            return fileFlow;
        }

        throw new InvalidOperationException($"Project {projectId} does not contain an executable flow.");
    }

    private async Task<OperatorFlow?> LoadFlowFromStorageAsync(Guid projectId)
    {
        try
        {
            var flowJson = await _flowStorage.LoadFlowJsonAsync(projectId);
            if (string.IsNullOrWhiteSpace(flowJson))
            {
                return null;
            }

            var flowDto = JsonSerializer.Deserialize<OperatorFlowDto>(flowJson, FlowJsonOptions);
            if (flowDto?.Operators?.Count > 0)
            {
                return flowDto.ToEntity();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[InspectionService] 鍔犺浇椤圭洰娴佺▼鏂囦欢澶辫触: {ProjectId}", projectId);
        }

        return null;
    }

    private static bool HasExecutableFlow(OperatorFlow? flow)
    {
        return flow?.Operators?.Count > 0;
    }

    private static InspectionJudgmentEvaluation DetermineStatusFromFlowOutput(Dictionary<string, object>? outputData)
    {
        return InspectionJudgmentResolver.DetermineStatusFromFlowOutput(outputData);
    }

    private static void SetJudgmentDiagnostics(
        Dictionary<string, object> outputData,
        InspectionJudgmentEvaluation evaluation)
    {
        outputData["MissingJudgmentSignal"] = evaluation.MissingJudgmentSignal;
        outputData["JudgmentSource"] = evaluation.JudgmentSource;
        outputData["StatusReason"] = evaluation.StatusReason;
    }

    private async Task PersistResultImageAsync(InspectionResult result, CancellationToken cancellationToken)
    {
        if (result.OutputImage == null || result.OutputImage.Length == 0)
        {
            return;
        }

        var config = _configurationService.GetCurrent();
        var storage = config.Storage ?? new StorageConfig();
        if (!ShouldPersistImage(storage.SavePolicy, result.Status))
        {
            return;
        }

        try
        {
            var rootPath = ResolveImageSaveRoot(storage.ImageSavePath);
            var dateFolder = DateTime.Now.ToString("yyyyMMdd");
            var statusFolder = result.Status switch
            {
                InspectionStatus.OK => "OK",
                InspectionStatus.NG => "NG",
                _ => "ERROR"
            };

            var targetDir = Path.Combine(rootPath, dateFolder, statusFolder);
            Directory.CreateDirectory(targetDir);

            var extension = GuessImageExtension(result.OutputImage);
            var fileName = $"{result.ProjectId:N}_{result.Id:N}_{DateTime.Now:HHmmssfff}{extension}";
            var targetPath = Path.Combine(targetDir, fileName);

            await File.WriteAllBytesAsync(targetPath, result.OutputImage, cancellationToken);
            _logger.LogDebug("[InspectionService] 检测图像已落盘: {Path}", targetPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[InspectionService] 检测图像落盘失败");
        }
    }

    private async Task CacheResultImageAsync(InspectionResult result)
    {
        if (result.OutputImage == null || result.OutputImage.Length == 0)
        {
            return;
        }

        try
        {
            var format = GuessImageFormat(result.OutputImage);
            var imageId = await _imageCacheRepository.AddAsync(result.OutputImage, format);
            if (imageId != Guid.Empty)
            {
                result.SetImageId(imageId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[InspectionService] 结果图像缓存失败");
        }
    }

    private static bool ShouldPersistImage(string? savePolicy, InspectionStatus status)
    {
        var policy = (savePolicy ?? "NgOnly").Trim();
        if (policy.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (policy.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (policy.Equals("NgOnly", StringComparison.OrdinalIgnoreCase))
        {
            return status == InspectionStatus.NG;
        }

        return status == InspectionStatus.NG;
    }

    private static string ResolveImageSaveRoot(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        return Path.Combine(AppContext.BaseDirectory, "VisionData", "Images");
    }

    private static string GuessImageExtension(byte[] bytes)
    {
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return ".png";
        }

        if (bytes.Length >= 3 &&
            bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return ".jpg";
        }

        if (bytes.Length >= 2 && bytes[0] == 0x42 && bytes[1] == 0x4D)
        {
            return ".bmp";
        }

        return ".bin";
    }

    private static string GuessImageFormat(byte[] bytes)
    {
        return GuessImageExtension(bytes).TrimStart('.');
    }

    private sealed class NoOpImageCacheRepository : IImageCacheRepository
    {
        public Task<Guid> AddAsync(byte[] imageData, string format)
        {
            return Task.FromResult(Guid.Empty);
        }

        public Task<byte[]?> GetAsync(Guid id)
        {
            return Task.FromResult<byte[]?>(null);
        }

        public Task DeleteAsync(Guid id)
        {
            return Task.CompletedTask;
        }

        public Task CleanExpiredAsync(TimeSpan expiration)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpProjectFlowStorage : IProjectFlowStorage
    {
        public Task SaveFlowJsonAsync(Guid projectId, string flowJson)
        {
            return Task.CompletedTask;
        }

        public Task<string?> LoadFlowJsonAsync(Guid projectId)
        {
            return Task.FromResult<string?>(null);
        }
    }

    #endregion
}
