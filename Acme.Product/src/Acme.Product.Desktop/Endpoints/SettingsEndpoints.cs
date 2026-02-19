// SettingsEndpoints.cs
// 设置功能 API 端点
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Interfaces;
using Acme.Product.Infrastructure.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;

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

        // ==================== AI 设置 API ====================

        // 获取当前 AI 配置（ApiKey 脱敏）
        app.MapGet("/api/ai/settings", (AiConfigStore configStore) =>
        {
            return Results.Ok(configStore.GetMasked());
        });

        // 更新 AI 配置
        app.MapPut("/api/ai/settings", (AiSettingsUpdateRequest request, AiConfigStore configStore) =>
        {
            try
            {
                var current = configStore.Get();

                // 仅更新前端提交的字段
                current.Provider = request.Provider ?? current.Provider;
                current.Model = request.Model ?? current.Model;
                current.BaseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? null : request.BaseUrl;

                // ApiKey：如果前端传来的值全是星号或为空，说明用户没修改，保留原值
                if (!string.IsNullOrEmpty(request.ApiKey) && !request.ApiKey.Contains('*'))
                {
                    current.ApiKey = request.ApiKey;
                }

                if (request.MaxRetries.HasValue)
                    current.MaxRetries = request.MaxRetries.Value;
                if (request.TimeoutSeconds.HasValue)
                    current.TimeoutSeconds = request.TimeoutSeconds.Value;
                if (request.MaxTokens.HasValue)
                    current.MaxTokens = request.MaxTokens.Value;
                if (request.Temperature.HasValue)
                    current.Temperature = request.Temperature.Value;

                configStore.Update(current);
                return Results.Ok(new { Message = "AI 配置已保存", Config = configStore.GetMasked() });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        // 测试 AI 连接
        app.MapPost("/api/ai/test", async (AiConfigStore configStore, AiApiClient apiClient) =>
        {
            try
            {
                var options = configStore.Get();
                var response = await apiClient.CompleteAsync(
                    "You are a helpful assistant.",
                    "Reply with exactly: OK",
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

        // 获取已配置的相机绑定列表
        app.MapGet("/api/cameras/bindings", (Acme.Product.Core.Cameras.ICameraManager cameraManager) =>
        {
            var bindings = cameraManager.GetBindings();
            return Results.Ok(bindings);
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

        return app;
    }
}

/// <summary>
/// AI 设置更新请求 DTO
/// </summary>
public class AiSettingsUpdateRequest
{
    public string? Provider { get; set; }
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
    public string? BaseUrl { get; set; }
    public int? MaxRetries { get; set; }
    public int? TimeoutSeconds { get; set; }
    public int? MaxTokens { get; set; }
    public double? Temperature { get; set; }
}
