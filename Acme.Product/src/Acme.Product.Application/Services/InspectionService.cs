// InspectionService.cs
// 检测应用服务
// 职责：API 门面，协调单次检测和实时检测
// 生命周期：Scoped（无状态，不保存运行时状态）
// 作者：蘅芜君 + 架构修复方案 v2

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
    private readonly ILogger<InspectionService> _logger;

    public InspectionService(
        IInspectionResultRepository resultRepository,
        IProjectRepository projectRepository,
        IFlowExecutionService flowExecutionService,
        IImageAcquisitionService imageAcquisitionService,
        IConfigurationService configurationService,
        IInspectionRuntimeCoordinator coordinator,
        IInspectionWorker worker,
        ILogger<InspectionService> logger)
    {
        _resultRepository = resultRepository;
        _projectRepository = projectRepository;
        _flowExecutionService = flowExecutionService;
        _imageAcquisitionService = imageAcquisitionService;
        _configurationService = configurationService;
        _coordinator = coordinator;
        _worker = worker;
        _logger = logger;
    }

    #region 单次检测

    public async Task<InspectionResult> ExecuteSingleAsync(Guid projectId, byte[] imageData)
    {
        return await ExecuteSingleAsync(projectId, imageData, null);
    }

    public async Task<InspectionResult> ExecuteSingleAsync(Guid projectId, byte[] imageData, OperatorFlow? flow)
    {
        OperatorFlow actualFlow;
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

            TrySetOutputDataJson(result, flowResult.OutputData);
            await PersistResultImageAsync(result, CancellationToken.None);
            await _resultRepository.AddAsync(result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[InspectionService] 检测异常: {ErrorMessage}", ex.Message);
            result.MarkAsError(ex.Message);
            await _resultRepository.AddAsync(result);
            throw;
        }
    }

    public async Task<InspectionResult> ExecuteSingleAsync(Guid projectId, string cameraId)
    {
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

    public async Task<IEnumerable<InspectionResult>> GetInspectionHistoryAsync(
        Guid projectId, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize)
    {
        if (startTime.HasValue && endTime.HasValue)
        {
            return await _resultRepository.GetByTimeRangeAsync(projectId, startTime.Value, endTime.Value);
        }
        else
        {
            return await _resultRepository.GetByProjectIdAsync(projectId, pageIndex, pageSize);
        }
    }

    public async Task<InspectionStatistics> GetStatisticsAsync(Guid projectId, DateTime? startTime, DateTime? endTime)
    {
        return await _resultRepository.GetStatisticsAsync(projectId, startTime, endTime);
    }

    #endregion

    #region 辅助方法

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

        if (outputData.TryGetValue("Result", out var resultVal) && resultVal is bool resultBool)
        {
            return resultBool ? InspectionStatus.OK : InspectionStatus.NG;
        }

        if (outputData.TryGetValue("ConditionResult", out var conditionVal) && conditionVal is bool conditionBool)
        {
            return conditionBool ? InspectionStatus.OK : InspectionStatus.NG;
        }

        if (outputData.TryGetValue("DefectCount", out var dc) && dc is int defectCount)
        {
            return defectCount > 0 ? InspectionStatus.NG : InspectionStatus.OK;
        }

        return InspectionStatus.OK;
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

    private void TrySetOutputDataJson(InspectionResult result, Dictionary<string, object>? outputData)
    {
        if (outputData == null || outputData.Count == 0)
            return;

        var serializableData = BuildSerializableOutputData(outputData);
        if (serializableData.Count == 0)
            return;

        try
        {
            var json = JsonSerializer.Serialize(serializableData);
            result.SetOutputDataJson(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[InspectionService] 序列化输出数据失败");
        }
    }

    private static Dictionary<string, object?> BuildSerializableOutputData(Dictionary<string, object> outputData)
    {
        var serializable = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in outputData)
        {
            if (IsExcludedOutput(kvp.Key, kvp.Value))
                continue;

            if (TryConvertOutputValue(kvp.Value, out var converted))
            {
                serializable[kvp.Key] = converted;
            }
        }

        return serializable;
    }

    private static bool IsExcludedOutput(string key, object? value)
    {
        if (key.Equals("Image", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("Defects", StringComparison.OrdinalIgnoreCase))
            return true;

        if (value is byte[])
            return true;

        if (value == null)
            return false;

        return IsKnownImageCarrierType(value.GetType());
    }

    private static bool TryConvertOutputValue(object? value, out object? converted, int depth = 0)
    {
        const int maxDepth = 8;
        if (depth > maxDepth)
        {
            converted = value?.ToString();
            return converted != null;
        }

        if (value == null)
        {
            converted = null;
            return true;
        }

        if (IsKnownImageCarrierType(value.GetType()) || value is byte[])
        {
            converted = null;
            return false;
        }

        if (value is JsonElement jsonElement)
        {
            converted = jsonElement;
            return true;
        }

        if (IsSimpleValue(value))
        {
            converted = value;
            return true;
        }

        if (value is IDictionary<string, object> typedDict)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (k, v) in typedDict)
            {
                if (TryConvertOutputValue(v, out var nested, depth + 1))
                {
                    dict[k] = nested;
                }
            }

            converted = dict;
            return true;
        }

        if (value is IDictionary dictionary)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = entry.Key?.ToString();
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (TryConvertOutputValue(entry.Value, out var nested, depth + 1))
                {
                    dict[key] = nested;
                }
            }

            converted = dict;
            return true;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
            {
                if (TryConvertOutputValue(item, out var nested, depth + 1))
                {
                    list.Add(nested);
                }
            }

            converted = list;
            return true;
        }

        try
        {
            JsonSerializer.Serialize(value);
            converted = value;
            return true;
        }
        catch
        {
            var text = value.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                converted = null;
                return false;
            }

            converted = text;
            return true;
        }
    }

    private static bool IsSimpleValue(object value)
    {
        var type = value.GetType();
        return type.IsPrimitive ||
               type.IsEnum ||
               value is string ||
               value is decimal ||
               value is DateTime ||
               value is DateTimeOffset ||
               value is Guid ||
               value is TimeSpan;
    }

    private static bool IsKnownImageCarrierType(Type type)
    {
        var fullName = type.FullName;
        return string.Equals(fullName, "OpenCvSharp.Mat", StringComparison.Ordinal) ||
               string.Equals(fullName, "Acme.Product.Infrastructure.Operators.ImageWrapper", StringComparison.Ordinal);
    }

    #endregion
}
