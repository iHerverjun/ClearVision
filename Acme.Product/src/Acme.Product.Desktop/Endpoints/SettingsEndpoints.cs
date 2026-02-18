// SettingsEndpoints.cs
// 设置功能 API 端点
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Interfaces;
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
