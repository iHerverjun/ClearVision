// DependencyInjection.cs
// 注册服务
// 作者：蘅芜君

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

        // 【第一优先级】结果判定算子
        services.AddSingleton<IOperatorExecutor, ResultJudgmentOperator>();

        // 【第三优先级】新增算子 - 预处理
        services.AddSingleton<IOperatorExecutor, ClaheEnhancementOperator>();
        services.AddSingleton<IOperatorExecutor, MorphologicalOperationOperator>();
        services.AddSingleton<IOperatorExecutor, LaplacianSharpenOperator>();
        services.AddSingleton<IOperatorExecutor, ImageAddOperator>();
        services.AddSingleton<IOperatorExecutor, ImageSubtractOperator>();
        services.AddSingleton<IOperatorExecutor, ImageBlendOperator>();

        // 【第三优先级】变量和流程控制
        services.AddSingleton<IVariableContext, VariableContext>();
        services.AddSingleton<IOperatorExecutor, VariableReadOperator>();
        services.AddSingleton<IOperatorExecutor, VariableWriteOperator>();
        services.AddSingleton<IOperatorExecutor, VariableIncrementOperator>();
        services.AddSingleton<IOperatorExecutor, CycleCounterOperator>();
        services.AddSingleton<IOperatorExecutor, TryCatchOperator>();

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

        // ==================== 清霜V3迁移：特征匹配算子 ====================
        services.AddSingleton<IOperatorExecutor, AkazeFeatureMatchOperator>();
        services.AddSingleton<IOperatorExecutor, OrbFeatureMatchOperator>();
        services.AddSingleton<IOperatorExecutor, GradientShapeMatchOperator>();
        services.AddSingleton<IOperatorExecutor, PyramidShapeMatchOperator>();

        // ==================== 清霜V3迁移：Phase 2 智能检测机制 ====================
        services.AddSingleton<IOperatorExecutor, DualModalVotingOperator>();

        // ==================== Sprint 2: ForEach 与数据操作算子 ====================
        services.AddSingleton<IOperatorExecutor, ForEachOperator>();
        services.AddSingleton<IOperatorExecutor, ArrayIndexerOperator>();
        services.AddSingleton<IOperatorExecutor, JsonExtractorOperator>();

        // ==================== Sprint 3: 算子全面扩充 ====================
        services.AddSingleton<IOperatorExecutor, MathOperationOperator>();
        services.AddSingleton<IOperatorExecutor, LogicGateOperator>();
        services.AddSingleton<IOperatorExecutor, TypeConvertOperator>();
        services.AddSingleton<IOperatorExecutor, HttpRequestOperator>();
        services.AddSingleton<IOperatorExecutor, MqttPublishOperator>();
        services.AddSingleton<IOperatorExecutor, StringFormatOperator>();
        services.AddSingleton<IOperatorExecutor, ImageSaveOperator>();

        // ==================== Sprint 3: 缺失补齐 ====================
        services.AddSingleton<Acme.Product.Infrastructure.Services.OcrEngineProvider>();
        services.AddSingleton<IOperatorExecutor, OcrRecognitionOperator>();
        services.AddSingleton<IOperatorExecutor, ImageDiffOperator>();
        services.AddSingleton<IOperatorExecutor, StatisticsOperator>();

        // ==================== Sprint 4: AI 安全沙盒 ====================
        services.AddSingleton<FlowLinter>();
        services.AddScoped<Acme.Product.Infrastructure.AI.DryRun.DryRunService>();

        // ==================== Sprint 5: AI 编排接入 ====================
        services.AddSingleton<Acme.Product.Infrastructure.AI.AIPromptBuilder>();
        services.AddScoped<Acme.Product.Infrastructure.AI.AIGeneratedFlowParser>();
        services.AddSingleton<Acme.Product.Infrastructure.AI.DryRun.DryRunStubRegistry>();
        services.AddSingleton<Acme.Product.Infrastructure.AI.DryRun.StubRegistryBuilder>();
        services.AddScoped<Acme.Product.Infrastructure.AI.AIWorkflowService>();

        // 应用服务 - Sprint 4新增
        services.AddScoped<IOperatorService, OperatorService>();
        services.AddScoped<IImageAcquisitionService, ImageAcquisitionService>();
        services.AddScoped<IHandEyeCalibrationService, HandEyeCalibrationService>();
        services.AddScoped<DemoProjectService>();
        services.AddScoped<IResultAnalysisService, ResultAnalysisService>();
        services.AddScoped<ISystemStatsService, SystemStatsService>();

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
