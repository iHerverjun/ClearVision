// SettingsEndpoints.cs
// 设置功能 API 端点
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Cameras;
using Acme.Product.Core.Interfaces;
using Acme.Product.Infrastructure.AI;
using Acme.Product.Infrastructure.Cameras;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Buffers.Binary;
using System.Text.Json;
using System;
using System.Linq;

namespace Acme.Product.Desktop.Endpoints;

/// <summary>
/// 设置功能 API 端点
/// </summary>
public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        // 获取当前配置
        app.MapGet("/api/settings", async (IConfigurationService configService) =>
        {
            var config = await configService.LoadAsync();
            return Results.Ok(config);
        });

        // 更新配置
        app.MapPut("/api/settings", async (AppConfig config, IConfigurationService configService) =>
        {
            try
            {
                await configService.SaveAsync(config);
                return Results.Ok(new { Message = "设置已保存" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        // 重置配置为默认值
        app.MapPost("/api/settings/reset", async (IConfigurationService configService) =>
        {
            var defaultConfig = new AppConfig();
            await configService.SaveAsync(defaultConfig);
            return Results.Ok(defaultConfig);
        });

        // 获取磁盘容量信息（用于设置页存储卡片）
        app.MapGet("/api/settings/disk-usage", (string? path, IConfigurationService configService) =>
        {
            var configuredPath = string.IsNullOrWhiteSpace(path)
                ? configService.GetCurrent().Storage.ImageSavePath
                : path;

            if (!TryBuildDiskUsage(configuredPath, out var usage, out var error))
            {
                return Results.BadRequest(new { Error = error });
            }

            return Results.Ok(usage);
        });

        // ==================== AI 多模型管理 API ====================

        // 获取所有模型（不含 ApiKey）
        app.MapGet("/api/ai/models", (AiConfigStore configStore) =>
        {
            var models = configStore.GetAll();
            var result = models.Select(m => new
            {
                m.Id,
                m.Name,
                m.Provider,
                hasApiKey = !string.IsNullOrWhiteSpace(m.ApiKey), // 前端用此判断是否已配置密钥
                m.Model,
                baseUrl = m.BaseUrl ?? "",
                m.TimeoutMs,
                m.IsActive,
                m.Protocol,
                m.AuthMode,
                m.AuthHeaderName,
                m.ExtraHeaders,
                m.ExtraQuery,
                m.ExtraBody,
                m.RoleBindings,
                m.Priority,
                m.Capabilities
            });
            return Results.Ok(result);
        });

        // 创建新模型
        app.MapPost("/api/ai/models", (AiModelCreateRequest request, AiConfigStore configStore) =>
        {
            try
            {
                var model = new AiModelConfig
                {
                    Id = $"model_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                    Name = request.Name ?? "新建模型",
                    Provider = request.Provider ?? AiModelConfig.GetLegacyProviderByProtocol(request.Protocol),
                    ApiKey = request.ApiKey ?? "",
                    Model = request.Model ?? string.Empty,
                    BaseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? null : request.BaseUrl,
                    TimeoutMs = request.TimeoutMs > 0 ? request.TimeoutMs : 120000,
                    Protocol = request.Protocol,
                    AuthMode = request.AuthMode,
                    AuthHeaderName = request.AuthHeaderName,
                    ExtraHeaders = CloneStringMap(request.ExtraHeaders),
                    ExtraQuery = CloneStringMap(request.ExtraQuery),
                    ExtraBody = CloneJsonMap(request.ExtraBody),
                    RoleBindings = CloneStringList(request.RoleBindings),
                    Priority = request.Priority,
                    IsActive = false,
                    Capabilities = request.Capabilities?.Clone()
                };
                configStore.Add(model);
                return Results.Ok(new { Message = "模型已创建", model.Id });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        // 更新指定模型
        app.MapPut("/api/ai/models/{id}", (string id, AiModelUpdateRequest request, AiConfigStore configStore) =>
        {
            try
            {
                var updated = new AiModelConfig
                {
                    Name = request.Name!,
                    Provider = request.Provider!,
                    ApiKey = request.ApiKey ?? "", // 空字符串 → 保留原值（由 AiConfigStore.Update 处理）
                    Model = request.Model ?? string.Empty,
                    BaseUrl = request.BaseUrl,
                    TimeoutMs = request.TimeoutMs,
                    Protocol = request.Protocol,
                    AuthMode = request.AuthMode,
                    AuthHeaderName = request.AuthHeaderName,
                    ExtraHeaders = CloneStringMap(request.ExtraHeaders),
                    ExtraQuery = CloneStringMap(request.ExtraQuery),
                    ExtraBody = CloneJsonMap(request.ExtraBody),
                    RoleBindings = CloneStringList(request.RoleBindings),
                    Priority = request.Priority,
                    Capabilities = request.Capabilities?.Clone()
                };
                var result = configStore.Update(id, updated);
                if (result == null)
                    return Results.NotFound(new { Error = $"模型 {id} 不存在" });

                return Results.Ok(new { Message = "模型已更新" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        // 删除指定模型
        app.MapDelete("/api/ai/models/{id}", (string id, AiConfigStore configStore) =>
        {
            try
            {
                var ok = configStore.Delete(id);
                return ok
                    ? Results.Ok(new { Message = "模型已删除" })
                    : Results.NotFound(new { Error = $"模型 {id} 不存在" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        // 设为激活模型
        app.MapPost("/api/ai/models/{id}/activate", (string id, AiConfigStore configStore) =>
        {
            var ok = configStore.SetActive(id);
            return ok
                ? Results.Ok(new { Message = "已切换激活模型" })
                : Results.NotFound(new { Error = $"模型 {id} 不存在" });
        });

        // 测试指定模型的连接（使用该模型的真实 Key，不影响全局 active 状态）
        app.MapPost("/api/ai/models/{id}/test", async (string id, AiConfigStore configStore, AiApiClient apiClient) =>
        {
            try
            {
                var model = configStore.GetById(id);
                if (model == null)
                    return Results.NotFound(new { Success = false, Message = $"模型 {id} 不存在" });

                if (string.IsNullOrEmpty(model.ApiKey))
                    return Results.Ok(new { Success = false, Message = "连接失败: 未配置 API Key" });

                var options = model.ToGenerationOptions();
                var response = await apiClient.CompleteAsync(
                    "You are a helpful assistant.",
                    new List<ChatMessage> { new("user", "Reply with exactly: OK") },
                    options,
                    CancellationToken.None);

                return Results.Ok(new { Success = true, Message = "连接成功" });
            }
            catch (Exception ex)
            {
                return Results.Ok(new { Success = false, Message = $"连接失败: {ex.Message}" });
            }
        });

        // ==================== 相机管理 API ====================

        // 搜索在线相机设备
        app.MapGet("/api/cameras/discover", async (Acme.Product.Core.Cameras.ICameraManager cameraManager) =>
        {
            var devices = await cameraManager.EnumerateCamerasAsync();
            return Results.Ok(devices);
        });

        // 仅通过华睿 SDK 搜索在线相机
        app.MapGet("/api/cameras/discover/huaray", (Acme.Product.Core.Cameras.ICameraManager cameraManager) =>
        {
            var devices = CameraProviderFactory.DiscoverHuarayOnly();
            var mapped = MapDiscoveredDevices(devices, cameraManager).ToList();
            var diagnostics = BuildHuarayDiagnostics(mapped.Count);
            return Results.Ok(new { devices = mapped, diagnostics });
        });

        // 仅通过海康 SDK 搜索在线相机
        app.MapGet("/api/cameras/discover/hikvision", (Acme.Product.Core.Cameras.ICameraManager cameraManager) =>
        {
            var devices = CameraProviderFactory.DiscoverHikvisionOnly();
            return Results.Ok(MapDiscoveredDevices(devices, cameraManager));
        });

        // 获取已配置的相机绑定列表
        app.MapGet("/api/cameras/bindings", async (Acme.Product.Core.Cameras.ICameraManager cameraManager) =>
        {
            var bindings = cameraManager.GetBindings();
            var discoveredDevices = await cameraManager.EnumerateCamerasAsync();
            var discoveredLookup = discoveredDevices.ToDictionary(
                device => device.CameraId,
                device => device,
                StringComparer.OrdinalIgnoreCase);

            var payload = bindings.Select(binding =>
            {
                var serialNumber = binding.SerialNumber?.Trim() ?? string.Empty;
                var runtimeCamera = string.IsNullOrWhiteSpace(serialNumber)
                    ? null
                    : cameraManager.GetCamera(serialNumber);
                var isDiscovered = !string.IsNullOrWhiteSpace(serialNumber)
                    && discoveredLookup.TryGetValue(serialNumber, out _);

                var connectionStatus = ResolveBindingConnectionStatus(binding, runtimeCamera, isDiscovered);

                return new
                {
                    binding.Id,
                    binding.DisplayName,
                    DeviceId = serialNumber,
                    binding.SerialNumber,
                    binding.IpAddress,
                    binding.Manufacturer,
                    binding.ModelName,
                    binding.InterfaceType,
                    binding.IsEnabled,
                    binding.ExposureTimeUs,
                    binding.GainDb,
                    binding.TriggerMode,
                    ConnectionStatus = connectionStatus
                };
            });

            return Results.Ok(payload);
        });

        // 更新相机绑定配置
        app.MapPut("/api/cameras/bindings", async (
            Acme.Product.Application.DTOs.UpdateCameraBindingsRequest request,
            Acme.Product.Core.Cameras.ICameraManager cameraManager,
            IConfigurationService configService) =>
        {
            try
            {
                // 1. 更新 CameraManager 内存状态
                cameraManager.UpdateBindings(request.Bindings, request.ActiveCameraId);

                // 2. 持久化到 AppConfig
                var config = await configService.LoadAsync();
                config.Cameras = request.Bindings;
                config.ActiveCameraId = request.ActiveCameraId;
                await configService.SaveAsync(config);

                return Results.Ok(new { Message = "相机配置已保存" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        // 使用绑定参数执行手动软触发抓图（仅用于预览）
        app.MapPost("/api/cameras/soft-trigger-capture", async (
            CameraSoftTriggerCaptureRequest request,
            HttpContext context,
            Acme.Product.Core.Cameras.ICameraManager cameraManager) =>
        {
            if (string.IsNullOrWhiteSpace(request.CameraBindingId))
            {
                return Results.BadRequest(new { Error = "CameraBindingId is required." });
            }

            try
            {
                var binding = cameraManager.GetBindings()
                    .FirstOrDefault(b => b.Id.Equals(request.CameraBindingId, StringComparison.OrdinalIgnoreCase));
                if (binding == null)
                {
                    return Results.NotFound(new { Error = $"Camera binding not found: {request.CameraBindingId}" });
                }

                var camera = await cameraManager.GetOrCreateByBindingAsync(request.CameraBindingId);

                await camera.SetExposureTimeAsync(binding.ExposureTimeUs);
                await camera.SetGainAsync(binding.GainDb);

                // 软触发采图序列已内聚在 AcquireSingleFrameAsync 中
                // （StartGrabbing → TriggerMode=On → TriggerSource=Software → ExecuteSoftwareTrigger → GetFrame）

                var frameBytes = await camera.AcquireSingleFrameAsync();
                if (!TryReadPngDimensions(frameBytes, out var width, out var height))
                {
                    return Results.BadRequest(new { Error = "Camera frame metadata parse failed." });
                }

                context.Response.Headers["X-Image-Width"] = width.ToString();
                context.Response.Headers["X-Image-Height"] = height.ToString();
                context.Response.Headers["X-Camera-Id"] = request.CameraBindingId;
                context.Response.Headers["X-Trigger-Mode"] = "Software";

                return Results.File(
                    frameBytes,
                    contentType: "image/png",
                    fileDownloadName: null);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        return app;
    }

    private static IEnumerable<CameraInfo> MapDiscoveredDevices(
        IEnumerable<CameraDeviceInfo> devices,
        Acme.Product.Core.Cameras.ICameraManager cameraManager)
    {
        return devices.Select(d => new CameraInfo
        {
            CameraId = d.SerialNumber,
            Name = string.IsNullOrEmpty(d.UserDefinedName) ? d.Model : d.UserDefinedName,
            Manufacturer = d.Manufacturer,
            Model = d.Model,
            ConnectionType = d.InterfaceType,
            IsConnected = cameraManager.GetCamera(d.SerialNumber) != null
        });
    }

    private static string ResolveBindingConnectionStatus(
        CameraBindingConfig binding,
        ICamera? runtimeCamera,
        bool isDiscovered)
    {
        if (!binding.IsEnabled)
        {
            return "Disabled";
        }

        if (string.IsNullOrWhiteSpace(binding.SerialNumber))
        {
            return "Unbound";
        }

        if (runtimeCamera?.IsConnected == true)
        {
            return "Connected";
        }

        return isDiscovered ? "Online" : "Offline";
    }

    private static object BuildHuarayDiagnostics(int deviceCount)
    {
        var sdkLoaded = MindVisionCamera.IsSdkLoaded;
        var sdkPath = MindVisionCamera.SdkAssemblyLocation;
        var sdkLoadError = MindVisionCamera.LastSdkLoadError;
        var enumerateDetail = MindVisionCamera.LastEnumerateError;

        string message;
        if (deviceCount > 0)
        {
            message = $"华睿搜索成功，发现 {deviceCount} 台设备。";
        }
        else if (!sdkLoaded)
        {
            message = string.IsNullOrWhiteSpace(sdkLoadError)
                ? "华睿 SDK 未加载成功，请检查 MVSDK_Net.dll。"
                : $"华睿 SDK 未加载成功：{sdkLoadError}";
        }
        else if (!string.IsNullOrWhiteSpace(enumerateDetail))
        {
            message = $"华睿 SDK 已加载，但枚举结果为空：{enumerateDetail}";
        }
        else
        {
            message = "华睿 SDK 已加载，但未发现设备。请检查相机供电、网线与网卡网段。";
        }

        return new
        {
            sdkLoaded,
            sdkPath,
            sdkLoadError,
            enumerateDetail,
            message
        };
    }

    private static bool TryReadPngDimensions(byte[] pngBytes, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (pngBytes.Length < 24)
        {
            return false;
        }

        ReadOnlySpan<byte> pngSignature = stackalloc byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        ReadOnlySpan<byte> ihdrChunkType = stackalloc byte[] { 73, 72, 68, 82 };

        if (!pngBytes.AsSpan(0, 8).SequenceEqual(pngSignature))
        {
            return false;
        }

        if (!pngBytes.AsSpan(12, 4).SequenceEqual(ihdrChunkType))
        {
            return false;
        }

        width = BinaryPrimitives.ReadInt32BigEndian(pngBytes.AsSpan(16, 4));
        height = BinaryPrimitives.ReadInt32BigEndian(pngBytes.AsSpan(20, 4));
        return width > 0 && height > 0;
    }

    private static bool TryBuildDiskUsage(string? targetPath, out object usage, out string error)
    {
        usage = default!;
        error = string.Empty;

        try
        {
            var fullPath = string.IsNullOrWhiteSpace(targetPath)
                ? AppContext.BaseDirectory
                : Path.GetFullPath(targetPath);

            var rootPath = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                rootPath = Path.GetPathRoot(AppContext.BaseDirectory);
            }

            if (string.IsNullOrWhiteSpace(rootPath))
            {
                error = "无法解析磁盘根路径。";
                return false;
            }

            var drive = new DriveInfo(rootPath);
            if (!drive.IsReady)
            {
                error = $"磁盘不可用: {drive.Name}";
                return false;
            }

            var totalBytes = drive.TotalSize;
            var freeBytes = drive.AvailableFreeSpace;
            var usedBytes = totalBytes - freeBytes;
            var usedPercent = totalBytes > 0 ? usedBytes * 100.0 / totalBytes : 0;

            usage = new
            {
                driveName = drive.Name,
                sourcePath = fullPath,
                totalBytes,
                usedBytes,
                freeBytes,
                totalGb = Math.Round(totalBytes / 1024d / 1024d / 1024d, 2),
                usedGb = Math.Round(usedBytes / 1024d / 1024d / 1024d, 2),
                freeGb = Math.Round(freeBytes / 1024d / 1024d / 1024d, 2),
                usedPercent = Math.Round(usedPercent, 2)
            };
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static Dictionary<string, string>? CloneStringMap(Dictionary<string, string>? source)
    {
        if (source == null)
            return null;

        return source.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, JsonElement>? CloneJsonMap(Dictionary<string, JsonElement>? source)
    {
        if (source == null)
            return null;

        var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in source)
        {
            result[kv.Key] = kv.Value.Clone();
        }

        return result;
    }

    private static List<string>? CloneStringList(List<string>? source)
    {
        if (source == null)
            return null;

        return new List<string>(source);
    }
}

/// <summary>创建模型请求</summary>
public class AiModelCreateRequest
{
    public string? Name { get; set; }
    public string? Provider { get; set; }
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
    public string? BaseUrl { get; set; }
    public int TimeoutMs { get; set; }
    public string? Protocol { get; set; }
    public string? AuthMode { get; set; }
    public string? AuthHeaderName { get; set; }
    public Dictionary<string, string>? ExtraHeaders { get; set; }
    public Dictionary<string, string>? ExtraQuery { get; set; }
    public Dictionary<string, JsonElement>? ExtraBody { get; set; }
    public List<string>? RoleBindings { get; set; }
    public int? Priority { get; set; }
    public AiModelCapabilities? Capabilities { get; set; }
}

/// <summary>更新模型请求</summary>
public class AiModelUpdateRequest
{
    public string? Name { get; set; }
    public string? Provider { get; set; }
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
    public string? BaseUrl { get; set; }
    public int TimeoutMs { get; set; }
    public string? Protocol { get; set; }
    public string? AuthMode { get; set; }
    public string? AuthHeaderName { get; set; }
    public Dictionary<string, string>? ExtraHeaders { get; set; }
    public Dictionary<string, string>? ExtraQuery { get; set; }
    public Dictionary<string, JsonElement>? ExtraBody { get; set; }
    public List<string>? RoleBindings { get; set; }
    public int? Priority { get; set; }
    public AiModelCapabilities? Capabilities { get; set; }
}

/// <summary>软触发抓图请求</summary>
public class CameraSoftTriggerCaptureRequest
{
    public string CameraBindingId { get; set; } = string.Empty;
}
