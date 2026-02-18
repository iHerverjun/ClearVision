// SerilogConfiguration.cs
// 记录流程执行
// 作者：蘅芜君

using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;

namespace Acme.Product.Infrastructure.Logging;

/// <summary>
/// Serilog 日志配置
/// </summary>
public static class SerilogConfiguration
{
    /// <summary>
    /// 配置 Serilog
    /// </summary>
    public static ILoggerFactory ConfigureSerilog()
    {
        var logsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClearVision",
            "Logs"
        );

        Directory.CreateDirectory(logsPath);

        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithExceptionDetails()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .WriteTo.File(
                Path.Combine(logsPath, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}{NewLine}"
            )
            .WriteTo.File(
                Path.Combine(logsPath, "errors-.txt"),
                restrictedToMinimumLevel: LogEventLevel.Error,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}{NewLine}"
            );

        Log.Logger = loggerConfiguration.CreateLogger();

        return LoggerFactory.Create(builder =>
        {
            builder.AddSerilog();
        });
    }

    /// <summary>
    /// 关闭日志
    /// </summary>
    public static void CloseAndFlush()
    {
        Log.CloseAndFlush();
    }
}

/// <summary>
/// 日志扩展方法
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// 记录算子执行
    /// </summary>
    public static void LogOperatorExecution(
        this Microsoft.Extensions.Logging.ILogger logger,
        Guid operatorId,
        string operatorName,
        long executionTimeMs,
        bool isSuccess)
    {
        logger.LogInformation(
            "算子执行完成: {OperatorName} ({OperatorId}), 耗时: {ExecutionTimeMs}ms, 结果: {Result}",
            operatorName,
            operatorId,
            executionTimeMs,
            isSuccess ? "成功" : "失败"
        );
    }

    /// <summary>
    /// 记录检测完成
    /// </summary>
    public static void LogInspectionCompleted(
        this Microsoft.Extensions.Logging.ILogger logger,
        Guid projectId,
        string status,
        int defectCount,
        long processingTimeMs)
    {
        logger.LogInformation(
            "检测完成: 工程 {ProjectId}, 状态: {Status}, 缺陷数: {DefectCount}, 耗时: {ProcessingTimeMs}ms",
            projectId,
            status,
            defectCount,
            processingTimeMs
        );
    }

    /// <summary>
    /// 记录流程执行
    /// </summary>
    public static void LogFlowExecution(
        this Microsoft.Extensions.Logging.ILogger logger,
        Guid flowId,
        int operatorCount,
        long totalExecutionTimeMs,
        bool isSuccess)
    {
        logger.LogInformation(
            "流程执行完成: {FlowId}, 算子数: {OperatorCount}, 总耗时: {TotalExecutionTimeMs}ms, 结果: {Result}",
            flowId,
            operatorCount,
            totalExecutionTimeMs,
            isSuccess ? "成功" : "失败"
        );
    }
}
