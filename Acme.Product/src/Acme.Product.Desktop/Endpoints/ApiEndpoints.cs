using Acme.Product.Application.DTOs;
using Acme.Product.Application.Services;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Interfaces;
using Acme.Product.Core.Services;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Acme.Product.Desktop.Endpoints;

/// <summary>
/// API 端点配置
/// </summary>
public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapVisionApiEndpoints(this IEndpointRouteBuilder app)
    {
        // 健康检查
        app.MapGet("/api/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));

        // 工程相关端点
        MapProjectEndpoints(app);

        // 检测相关端点
        MapInspectionEndpoints(app);

        // 算子库端点
        MapOperatorEndpoints(app);

        // 图像相关端点
        MapImageEndpoints(app);

        return app;
    }

    private static void MapProjectEndpoints(IEndpointRouteBuilder app)
    {
        // 获取工程列表
        app.MapGet("/api/projects", async (ProjectService service) =>
        {
            var projects = await service.GetAllAsync();
            return Results.Ok(projects);
        });

        // 获取最近打开的工程
        app.MapGet("/api/projects/recent", async (ProjectService service, int count = 10) =>
        {
            var projects = await service.GetRecentlyOpenedAsync(count);
            return Results.Ok(projects);
        });

        // 搜索工程
        app.MapGet("/api/projects/search", async (ProjectService service, string keyword) =>
        {
            var projects = await service.SearchAsync(keyword);
            return Results.Ok(projects);
        });

        // 获取工程详情
        app.MapGet("/api/projects/{id:guid}", async (Guid id, ProjectService service) =>
        {
            var project = await service.GetByIdAsync(id);
            return project != null ? Results.Ok(project) : Results.NotFound();
        });

        // 创建工程
        app.MapPost("/api/projects", async (CreateProjectRequest request, ProjectService service) =>
        {
            try
            {
                var project = await service.CreateAsync(request);
                return Results.Created($"/api/projects/{project.Id}", project);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        // 更新工程
        app.MapPut("/api/projects/{id:guid}", async (Guid id, UpdateProjectRequest request, ProjectService service) =>
        {
            try
            {
                var project = await service.UpdateAsync(id, request);
                return Results.Ok(project);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        // 删除工程
        app.MapDelete("/api/projects/{id:guid}", async (Guid id, ProjectService service) =>
        {
            try
            {
                await service.DeleteAsync(id);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        // 更新流程
        app.MapPut("/api/projects/{id:guid}/flow", async (Guid id, UpdateFlowRequest request, ProjectService service) =>
        {
            try
            {
                // 使用 ProjectService 处理更新，它现在使用文件存储
                // 这种方式完全绕过了 EF Core 的复杂状态管理和 Table Splitting 问题
                await service.UpdateFlowAsync(id, request);

                return Results.Ok(new { Message = "流程已更新 (File Based)", OperatorCount = request.Operators.Count, ConnectionCount = request.Connections.Count });
            }
            catch (Exception ex)
            {
                // 日志已由全局异常中间件记录
                return Results.BadRequest(new { Error = ex.Message });
            }
        });
    }

    private static void MapInspectionEndpoints(IEndpointRouteBuilder app)
    {
        // 执行检测
        app.MapPost("/api/inspection/execute", async (ExecuteInspectionRequest request, Core.Services.IInspectionService service) =>
        {
            try
            {
                if (!string.IsNullOrEmpty(request.ImageBase64))
                {
                    var imageData = Convert.FromBase64String(request.ImageBase64);
                    var result = await service.ExecuteSingleAsync(request.ProjectId, imageData);
                    return Results.Ok(result);
                }
                else if (!string.IsNullOrEmpty(request.CameraId))
                {
                    var result = await service.ExecuteSingleAsync(request.ProjectId, request.CameraId);
                    return Results.Ok(result);
                }
                else
                {
                    // 【关键修复】如果前端提供了流程数据，则转换并使用
                    // 这确保前端编辑的参数值能正确传递到后端执行
                    OperatorFlow? flow = request.FlowData?.ToEntity();
                    // 前端流程数据已通过日志中间件记录

                    var result = await service.ExecuteSingleAsync(request.ProjectId, (byte[])null!, flow);
                    return Results.Ok(result);
                }
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        // 获取检测历史
        app.MapGet("/api/inspection/history/{projectId:guid}", async (
        Guid projectId,
        Core.Services.IInspectionService service,
        DateTime? startTime,
        DateTime? endTime,
        int pageIndex = 0,
        int pageSize = 20) =>
        {
            var results = await service.GetInspectionHistoryAsync(projectId, startTime, endTime, pageIndex, pageSize);
            return Results.Ok(results);
        });

        // 获取统计信息
        app.MapGet("/api/inspection/statistics/{projectId:guid}", async (
        Guid projectId,
        Core.Services.IInspectionService service,
        DateTime? startTime,
        DateTime? endTime) =>
        {
            var statistics = await service.GetStatisticsAsync(projectId, startTime, endTime);
            return Results.Ok(statistics);
        });

        // 【第二优先级】启动实时检测
        app.MapPost("/api/inspection/realtime/start", async (
            StartRealtimeInspectionRequest request,
            Core.Services.IInspectionService service,
            ProjectService projectService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                // 根据运行模式选择启动方式
                var runMode = request.RunMode?.ToLower() ?? "camera";
                
                if (runMode == "flow" && request.FlowData != null)
                {
                    // 流程驱动模式
                    var flow = request.FlowData.ToEntity();
                    if (flow == null)
                    {
                        return Results.BadRequest(new { Error = "无效的流程数据" });
                    }
                    
                    await service.StartRealtimeInspectionFlowAsync(
                        request.ProjectId, 
                        flow, 
                        request.CameraId, 
                        cancellationToken);
                    
                    return Results.Ok(new { 
                        Message = "实时检测已启动 (流程驱动模式)", 
                        ProjectId = request.ProjectId,
                        RunMode = "flow",
                        CameraId = request.CameraId 
                    });
                }
                else
                {
                    // 相机驱动模式
                    await service.StartRealtimeInspectionAsync(
                        request.ProjectId, 
                        request.CameraId, 
                        cancellationToken);
                    
                    return Results.Ok(new { 
                        Message = "实时检测已启动 (相机驱动模式)", 
                        ProjectId = request.ProjectId,
                        RunMode = "camera",
                        CameraId = request.CameraId 
                    });
                }
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { Error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        // 【第二优先级】停止实时检测
        app.MapPost("/api/inspection/realtime/stop", async (
            StopRealtimeInspectionRequest request,
            Core.Services.IInspectionService service) =>
        {
            try
            {
                await service.StopRealtimeInspectionAsync(request.ProjectId);
                return Results.Ok(new { 
                    Message = "实时检测已停止", 
                    ProjectId = request.ProjectId 
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });
    }

    private static void MapOperatorEndpoints(IEndpointRouteBuilder app)
    {
        // 获取算子库
        app.MapGet("/api/operators/library", (IOperatorFactory factory) =>
        {
            var metadata = factory.GetAllMetadata();
            return Results.Ok(metadata);
        });

        // 获取支持的算子类型
        app.MapGet("/api/operators/types", (IOperatorFactory factory) =>
        {
            var types = factory.GetSupportedOperatorTypes();
            return Results.Ok(types);
        });

        // 获取算子元数据
        app.MapGet("/api/operators/{type}/metadata", (Core.Enums.OperatorType type, IOperatorFactory factory) =>
        {
            var metadata = factory.GetMetadata(type);
            return metadata != null ? Results.Ok(metadata) : Results.NotFound();
        });
    }

    private static void MapImageEndpoints(IEndpointRouteBuilder app)
    {
        // 上传图像
        app.MapPost("/api/images/upload", async (UploadImageRequest request, IImageCacheRepository cache) =>
        {
            try
            {
                var imageData = Convert.FromBase64String(request.DataBase64);
                var imageId = await cache.AddAsync(imageData, "png");
                return Results.Ok(new { ImageId = imageId });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        // 获取图像
        app.MapGet("/api/images/{id:guid}", async (Guid id, IImageCacheRepository cache) =>
        {
            var imageData = await cache.GetAsync(id);
            if (imageData == null)
            {
                return Results.NotFound();
            }

            return Results.File(imageData, "image/png");
        });
    }
}
