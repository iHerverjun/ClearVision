using Acme.Product.Application.Services;
using Acme.Product.Core.Cameras;
using Acme.Product.Core.Interfaces;
using Acme.Product.Core.Operators;
using Acme.Product.Core.Services;
using Acme.Product.Infrastructure.Cameras;
using Acme.Product.Infrastructure.Data;
using Acme.Product.Infrastructure.Logging;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Infrastructure.Repositories;
using Acme.Product.Infrastructure.Services;
using IConfigurationService = Acme.Product.Core.Interfaces.IConfigurationService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using IImageAcquisitionService = Acme.Product.Application.Services.IImageAcquisitionService;

namespace Acme.Product.Desktop;

/// <summary>
/// 依赖注入配置
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 注册服务
    /// </summary>
    public static IServiceCollection AddVisionServices(this IServiceCollection services)
    {
        // 配置日志
        var loggerFactory = SerilogConfiguration.ConfigureSerilog();
        services.AddSingleton<ILoggerFactory>(loggerFactory);

        // 数据库 - 使用 Scoped 生命周期（EF Core DbContext 不是线程安全的）
        services.AddDbContext<VisionDbContext>(options =>
        {
            options.UseSqlite("Data Source=vision.db");
        });

        // 仓储 - 使用 Scoped 生命周期（DbContext 是 Scoped，Repository 也必须是 Scoped 或 Transient）
        services.AddScoped(typeof(IRepository<>), typeof(RepositoryBase<>));
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IOperatorRepository, OperatorRepository>();
        services.AddScoped<IInspectionResultRepository, InspectionResultRepository>();
        services.AddSingleton<IImageCacheRepository, ImageCacheRepository>(); // 缓存可以是 Singleton

        // 应用服务
        services.AddScoped<ProjectService>();
        services.AddScoped<IInspectionService, InspectionService>();

        // 领域服务
        services.AddScoped<IFlowExecutionService, FlowExecutionService>();
        services.AddSingleton<IOperatorFactory, OperatorFactory>();

        // 算子执行器 - 基础算子
        services.AddSingleton<IOperatorExecutor, ImageAcquisitionOperator>();
        services.AddSingleton<IOperatorExecutor, GaussianBlurOperator>();
        services.AddSingleton<IOperatorExecutor, CannyEdgeOperator>();
        services.AddSingleton<IOperatorExecutor, ThresholdOperator>();
        services.AddSingleton<IOperatorExecutor, MorphologyOperator>();
        services.AddSingleton<IOperatorExecutor, BlobDetectionOperator>();
        services.AddSingleton<IOperatorExecutor, TemplateMatchOperator>();
        services.AddSingleton<IOperatorExecutor, FindContoursOperator>();
        services.AddSingleton<IOperatorExecutor, MeasureDistanceOperator>();
        services.AddSingleton<IOperatorExecutor, ResultOutputOperator>();
        services.AddSingleton<IOperatorExecutor, DeepLearningOperator>();

        // Phase 1 新增算子
        services.AddSingleton<IOperatorExecutor, MedianBlurOperator>();
        services.AddSingleton<IOperatorExecutor, BilateralFilterOperator>();
        services.AddSingleton<IOperatorExecutor, ImageResizeOperator>();
        services.AddSingleton<IOperatorExecutor, ImageCropOperator>();
        services.AddSingleton<IOperatorExecutor, ImageRotateOperator>();
        services.AddSingleton<IOperatorExecutor, PerspectiveTransformOperator>();
        services.AddSingleton<IOperatorExecutor, CodeRecognitionOperator>();
        services.AddSingleton<IOperatorExecutor, CircleMeasurementOperator>();
        services.AddSingleton<IOperatorExecutor, LineMeasurementOperator>();
        services.AddSingleton<IOperatorExecutor, ContourMeasurementOperator>();

        // Phase 2 新增算子
        services.AddSingleton<IOperatorExecutor, AngleMeasurementOperator>();
        services.AddSingleton<IOperatorExecutor, GeometricToleranceOperator>();
        services.AddSingleton<IOperatorExecutor, CameraCalibrationOperator>();
        services.AddSingleton<IOperatorExecutor, UndistortOperator>();
        services.AddSingleton<IOperatorExecutor, CoordinateTransformOperator>();

        // Phase 3 新增算子
        services.AddSingleton<IOperatorExecutor, ModbusCommunicationOperator>();
        services.AddSingleton<IOperatorExecutor, TcpCommunicationOperator>();
        services.AddSingleton<IOperatorExecutor, DatabaseWriteOperator>();
        services.AddSingleton<IOperatorExecutor, ConditionalBranchOperator>();

        // PLC 通信算子
        services.AddSingleton<IOperatorExecutor, SiemensS7CommunicationOperator>();
        services.AddSingleton<IOperatorExecutor, MitsubishiMcCommunicationOperator>();
        services.AddSingleton<IOperatorExecutor, OmronFinsCommunicationOperator>();

        // 孤立算子修复（原复用枚举，现分配独立枚举值 38-40）
        services.AddSingleton<IOperatorExecutor, ColorConversionOperator>();
        services.AddSingleton<IOperatorExecutor, AdaptiveThresholdOperator>();
        services.AddSingleton<IOperatorExecutor, HistogramEqualizationOperator>();

        // Phase 1 关键能力补齐
        services.AddSingleton<IOperatorExecutor, GeometricFittingOperator>();
        services.AddSingleton<IOperatorExecutor, RoiManagerOperator>();
        services.AddSingleton<IOperatorExecutor, ShapeMatchingOperator>();
        services.AddSingleton<IOperatorExecutor, SubpixelEdgeDetectionOperator>();

        // Phase 3 新增算子（算法深度提升）
        services.AddSingleton<IOperatorExecutor, ColorDetectionOperator>();
        services.AddSingleton<IOperatorExecutor, SerialCommunicationOperator>();

        // 应用服务 - Sprint 4新增
        services.AddScoped<IOperatorService, OperatorService>();
        services.AddScoped<IImageAcquisitionService, ImageAcquisitionService>();
        services.AddScoped<DemoProjectService>();
        services.AddScoped<IResultAnalysisService, ResultAnalysisService>();

        // 相机
        services.AddSingleton<ICameraManager, CameraManager>();

        // 注册 MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Acme.Product.Application.Commands.Projects.CreateProjectCommand).Assembly));

        // 注册 AutoMapper
        services.AddAutoMapper(typeof(Acme.Product.Application.Commands.Projects.CreateProjectCommand).Assembly);

        // 序列化与导出
        services.AddSingleton<IProjectSerializer, ProjectJsonSerializer>();
        services.AddSingleton<IResultExporter, CsvResultExporter>();

        // 配置服务
        services.AddSingleton<IConfigurationService, JsonConfigurationService>();

        // 用户系统 - User System
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<UserManagementService>();

        return services;
    }
}

/// <summary>
/// 相机管理器实现
/// </summary>
public class CameraManager : ICameraManager
{
    private readonly Dictionary<string, ICamera> _cameras = new();

    public Task<IEnumerable<CameraInfo>> EnumerateCamerasAsync()
    {
        // 返回模拟相机和文件相机
        var cameras = new List<CameraInfo>
        {
            new() { CameraId = "mock_001", Name = "模拟相机 1", IsConnected = false },
            new() { CameraId = "file_001", Name = "文件相机", IsConnected = false }
        };

        return Task.FromResult<IEnumerable<CameraInfo>>(cameras);
    }

    public Task<ICamera> OpenCameraAsync(string cameraId)
    {
        ICamera camera;

        if (cameraId.StartsWith("mock_"))
        {
            camera = new MockCamera(cameraId, $"模拟相机 {cameraId}");
        }
        else if (cameraId.StartsWith("file_"))
        {
            camera = new FileCamera(cameraId, "文件相机", "sample.jpg");
        }
        else
        {
            throw new ArgumentException($"未知的相机ID: {cameraId}");
        }

        _cameras[cameraId] = camera;
        return Task.FromResult(camera);
    }

    public Task CloseCameraAsync(string cameraId)
    {
        if (_cameras.TryGetValue(cameraId, out var camera))
        {
            camera.Dispose();
            _cameras.Remove(cameraId);
        }

        return Task.CompletedTask;
    }

    public ICamera? GetCamera(string cameraId)
    {
        return _cameras.TryGetValue(cameraId, out var camera) ? camera : null;
    }
}
