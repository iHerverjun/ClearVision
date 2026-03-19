// AutoTuneEndpoints.cs
// 自动调参 API 端点
// 【Phase 4】LLM 闭环验证 - 自动调参端点
// 作者：架构修复方案 v2

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Desktop.Endpoints;

/// <summary>
/// 自动调参相关 API 端点
/// </summary>
public static class AutoTuneEndpoints
{
    /// <summary>
    /// 注册自动调参端点
    /// </summary>
    public static IEndpointRouteBuilder MapAutoTuneEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/autotune")
            .WithTags("AutoTune")
            .RequireAuthorization();

        // POST /api/autotune/operator - 单个算子自动调参
        group.MapPost("/operator", async (
            OperatorAutoTuneRequest request,
            IAutoTuneService autoTuneService,
            ILogger<AutoTuneService> logger,
            CancellationToken ct) =>
        {
            try
            {
                logger.LogInformation(
                    "[AutoTuneAPI] 请求算子调参: Type={Type}, MaxIterations={MaxIter}",
                    request.OperatorType, request.MaxIterations);

                var result = await autoTuneService.AutoTuneOperatorAsync(
                    request.OperatorType,
                    request.InputImage,
                    request.InitialParameters,
                    request.Goal,
                    request.MaxIterations,
                    ct);

                return result.Success
                    ? Results.Ok(new AutoTuneResponse
                    {
                        Success = true,
                        FinalParameters = result.FinalParameters,
                        FinalScore = result.FinalScore,
                        TotalIterations = result.TotalIterations,
                        TotalExecutionTimeMs = result.TotalExecutionTimeMs,
                        IsGoalAchieved = result.IsGoalAchieved,
                        Iterations = result.Iterations.Select(i => new AutoTuneIterationDto
                        {
                            Iteration = i.Iteration,
                            Parameters = i.Parameters,
                            Score = i.Score,
                            ExecutionTimeMs = i.ExecutionTimeMs,
                            Metrics = MapMetricsToDto(i.Metrics)
                        }).ToList()
                    })
                    : Results.BadRequest(new AutoTuneResponse
                    {
                        Success = false,
                        ErrorMessage = result.ErrorMessage,
                        Iterations = result.Iterations.Select(i => new AutoTuneIterationDto
                        {
                            Iteration = i.Iteration,
                            Parameters = i.Parameters,
                            Score = i.Score,
                            ExecutionTimeMs = i.ExecutionTimeMs
                        }).ToList()
                    });
            }
            catch (OperationCanceledException)
            {
                return Results.StatusCode(499); // Client Closed Request
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[AutoTuneAPI] 算子调参失败");
                return Results.Problem(ex.Message);
            }
        })
        .WithName("AutoTuneOperator")
        .WithDescription("对单个算子进行自动调参");

        // POST /api/autotune/flow-node - 流程内节点自动调参
        group.MapPost("/flow-node", async (
            FlowNodeAutoTuneRequest request,
            IAutoTuneService autoTuneService,
            ILogger<AutoTuneService> logger,
            CancellationToken ct) =>
        {
            try
            {
                logger.LogInformation(
                    "[AutoTuneAPI] 请求流程节点调参: FlowId={FlowId}, NodeId={NodeId}",
                    request.FlowId, request.TargetNodeId);

                // 转换流程数据
                var flow = request.FlowData.ToEntity();

                var result = await autoTuneService.AutoTuneInFlowAsync(
                    flow,
                    request.TargetNodeId,
                    request.InputImage,
                    request.InitialParameters,
                    request.Goal,
                    request.MaxIterations,
                    ct);

                return result.Success
                    ? Results.Ok(new AutoTuneResponse
                    {
                        Success = true,
                        FinalParameters = result.FinalParameters,
                        FinalScore = result.FinalScore,
                        TotalIterations = result.TotalIterations,
                        TotalExecutionTimeMs = result.TotalExecutionTimeMs,
                        IsGoalAchieved = result.IsGoalAchieved,
                        Iterations = result.Iterations.Select(i => new AutoTuneIterationDto
                        {
                            Iteration = i.Iteration,
                            Parameters = i.Parameters,
                            Score = i.Score,
                            ExecutionTimeMs = i.ExecutionTimeMs,
                            Metrics = MapMetricsToDto(i.Metrics)
                        }).ToList()
                    })
                    : Results.BadRequest(new AutoTuneResponse
                    {
                        Success = false,
                        ErrorMessage = result.ErrorMessage
                    });
            }
            catch (OperationCanceledException)
            {
                return Results.StatusCode(499);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[AutoTuneAPI] 流程节点调参失败");
                return Results.Problem(ex.Message);
            }
        })
        .WithName("AutoTuneFlowNode")
        .WithDescription("对流程中的特定节点进行自动调参");

        // POST /api/autotune/suggest - 获取参数建议（快速建议，不调参）
        group.MapPost("/suggest", async (
            ParameterSuggestionRequest request,
            IPreviewMetricsAnalyzer metricsAnalyzer,
            ILogger<AutoTuneService> logger,
            CancellationToken ct) =>
        {
            try
            {
                logger.LogInformation("[AutoTuneAPI] 请求参数建议: Type={Type}", request.OperatorType);

                // 分析当前指标
                var metrics = metricsAnalyzer.Analyze(
                    request.CurrentOutputData.TryGetValue("Image", out var img) && img is OpenCvSharp.Mat mat ? mat : new OpenCvSharp.Mat(),
                    request.CurrentOutputData,
                    request.Goal);

                var suggestions = metrics.Suggestions.Select(s => new ParameterSuggestionDto
                {
                    ParameterName = s.ParameterName,
                    CurrentValue = s.CurrentValue,
                    SuggestedValue = s.SuggestedValue,
                    Reason = s.Reason,
                    ExpectedImprovement = s.ExpectedImprovement
                }).ToList();

                return Results.Ok(new ParameterSuggestionResponse
                {
                    Success = true,
                    Diagnostics = metrics.Diagnostics,
                    OverallScore = metrics.OverallScore,
                    Suggestions = suggestions,
                    Goals = new OptimizationGoalsDto
                    {
                        CurrentBlobCount = metrics.Goals.CurrentBlobCount,
                        TargetBlobCount = metrics.Goals.TargetBlobCount,
                        CountError = metrics.Goals.CountError,
                        NoisePenalty = metrics.Goals.NoisePenalty,
                        FragmentPenalty = metrics.Goals.FragmentPenalty,
                        AreaDistributionScore = metrics.Goals.AreaDistributionScore,
                        ShapeRegularityScore = metrics.Goals.ShapeRegularityScore
                    }
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[AutoTuneAPI] 获取参数建议失败");
                return Results.Problem(ex.Message);
            }
        })
        .WithName("GetParameterSuggestions")
        .WithDescription("基于当前执行结果获取参数调整建议");

        // GET /api/autotune/strategies - 获取支持的调参策略
        group.MapGet("/strategies", () =>
        {
            var strategies = new[]
            {
                new StrategyInfoDto
                {
                    Name = "BinarySearch",
                    DisplayName = "二分搜索",
                    Description = "适用于阈值类参数，快速收敛",
                    SupportedOperators = new[] { "Threshold", "CannyEdge", "HoughLines" }
                },
                new StrategyInfoDto
                {
                    Name = "GradientDescent",
                    DisplayName = "梯度下降",
                    Description = "适用于连续参数，平滑优化",
                    SupportedOperators = new[] { "GaussianBlur", "BilateralFilter" }
                },
                new StrategyInfoDto
                {
                    Name = "Heuristic",
                    DisplayName = "启发式搜索",
                    Description = "基于规则的智能调整，适用性广",
                    SupportedOperators = new[] { "FilterContours", "Morphology", "FindContours", "BlobDetection" }
                },
                new StrategyInfoDto
                {
                    Name = "GridSearch",
                    DisplayName = "网格搜索",
                    Description = "适用于多参数组合优化",
                    SupportedOperators = new[] { "*" }
                }
            };

            return Results.Ok(strategies);
        })
        .WithName("GetTuningStrategies")
        .WithDescription("获取支持的自动调参策略");

        return app;
    }

    #region DTO 映射

    private static PreviewMetricsDto MapMetricsToDto(PreviewMetrics metrics)
    {
        return new PreviewMetricsDto
        {
            MeanIntensity = metrics.ImageStats.MeanIntensity,
            StdDev = metrics.ImageStats.StdDev,
            LaplacianVariance = metrics.ImageStats.LaplacianVariance,
            Histogram = metrics.ImageStats.Histogram,
            BlobCount = metrics.BlobStats.Count,
            Diagnostics = metrics.Diagnostics,
            OverallScore = metrics.OverallScore,
            Goals = new OptimizationGoalsDto
            {
                CurrentBlobCount = metrics.Goals.CurrentBlobCount,
                TargetBlobCount = metrics.Goals.TargetBlobCount,
                CountError = metrics.Goals.CountError,
                NoisePenalty = metrics.Goals.NoisePenalty,
                FragmentPenalty = metrics.Goals.FragmentPenalty,
                AreaDistributionScore = metrics.Goals.AreaDistributionScore,
                ShapeRegularityScore = metrics.Goals.ShapeRegularityScore
            }
        };
    }

    #endregion
}

#region 请求/响应 DTOs

/// <summary>
/// 算子自动调参请求
/// </summary>
public class OperatorAutoTuneRequest
{
    /// <summary>
    /// 算子类型
    /// </summary>
    public OperatorType OperatorType { get; set; }

    /// <summary>
    /// 输入图像（Base64 或字节数组）
    /// </summary>
    public byte[] InputImage { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// 初始参数
    /// </summary>
    public Dictionary<string, object> InitialParameters { get; set; } = new();

    /// <summary>
    /// 调参目标
    /// </summary>
    public AutoTuneGoal Goal { get; set; } = new();

    /// <summary>
    /// 最大迭代次数（默认 5）
    /// </summary>
    public int MaxIterations { get; set; } = 5;
}

/// <summary>
/// 流程节点自动调参请求
/// </summary>
public class FlowNodeAutoTuneRequest
{
    /// <summary>
    /// 流程 ID
    /// </summary>
    public Guid FlowId { get; set; }

    /// <summary>
    /// 目标节点 ID
    /// </summary>
    public Guid TargetNodeId { get; set; }

    /// <summary>
    /// 流程数据
    /// </summary>
    public FlowDataDto FlowData { get; set; } = new();

    /// <summary>
    /// 输入图像
    /// </summary>
    public byte[] InputImage { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// 初始参数
    /// </summary>
    public Dictionary<string, object> InitialParameters { get; set; } = new();

    /// <summary>
    /// 调参目标
    /// </summary>
    public AutoTuneGoal Goal { get; set; } = new();

    /// <summary>
    /// 最大迭代次数（默认 5）
    /// </summary>
    public int MaxIterations { get; set; } = 5;
}

/// <summary>
/// 参数建议请求
/// </summary>
public class ParameterSuggestionRequest
{
    /// <summary>
    /// 算子类型
    /// </summary>
    public OperatorType OperatorType { get; set; }

    /// <summary>
    /// 当前输出数据（用于分析）
    /// </summary>
    public Dictionary<string, object> CurrentOutputData { get; set; } = new();

    /// <summary>
    /// 调参目标
    /// </summary>
    public AutoTuneGoal Goal { get; set; } = new();
}

/// <summary>
/// 自动调参响应
/// </summary>
public class AutoTuneResponse
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 最终参数
    /// </summary>
    public Dictionary<string, object> FinalParameters { get; set; } = new();

    /// <summary>
    /// 最终评分
    /// </summary>
    public double FinalScore { get; set; }

    /// <summary>
    /// 总迭代次数
    /// </summary>
    public int TotalIterations { get; set; }

    /// <summary>
    /// 总执行时间（毫秒）
    /// </summary>
    public long TotalExecutionTimeMs { get; set; }

    /// <summary>
    /// 是否达到目标
    /// </summary>
    public bool IsGoalAchieved { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 迭代历史
    /// </summary>
    public List<AutoTuneIterationDto> Iterations { get; set; } = new();
}

/// <summary>
/// 自动调参迭代 DTO
/// </summary>
public class AutoTuneIterationDto
{
    /// <summary>
    /// 迭代序号
    /// </summary>
    public int Iteration { get; set; }

    /// <summary>
    /// 本轮参数
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// 本轮评分
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// 执行时间（毫秒）
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// 本轮指标（可选）
    /// </summary>
    public PreviewMetricsDto? Metrics { get; set; }
}

/// <summary>
/// 预览指标 DTO
/// </summary>
public class PreviewMetricsDto
{
    /// <summary>
    /// 平均亮度
    /// </summary>
    public double MeanIntensity { get; set; }

    /// <summary>
    /// 标准差
    /// </summary>
    public double StdDev { get; set; }

    /// <summary>
    /// 拉普拉斯方差
    /// </summary>
    public double LaplacianVariance { get; set; }

    /// <summary>
    /// 直方图
    /// </summary>
    public int[] Histogram { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Blob 数量
    /// </summary>
    public int BlobCount { get; set; }

    /// <summary>
    /// 诊断标签
    /// </summary>
    public List<string> Diagnostics { get; set; } = new();

    /// <summary>
    /// 综合评分
    /// </summary>
    public double OverallScore { get; set; }

    /// <summary>
    /// 优化目标
    /// </summary>
    public OptimizationGoalsDto Goals { get; set; } = new();
}

/// <summary>
/// 优化目标 DTO
/// </summary>
public class OptimizationGoalsDto
{
    public int? TargetBlobCount { get; set; }
    public int CurrentBlobCount { get; set; }
    public double CountError { get; set; }
    public int NoisePenalty { get; set; }
    public int FragmentPenalty { get; set; }
    public double AreaDistributionScore { get; set; }
    public double ShapeRegularityScore { get; set; }
}

/// <summary>
/// 参数建议响应
/// </summary>
public class ParameterSuggestionResponse
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 诊断标签
    /// </summary>
    public List<string> Diagnostics { get; set; } = new();

    /// <summary>
    /// 综合评分
    /// </summary>
    public double OverallScore { get; set; }

    /// <summary>
    /// 参数建议列表
    /// </summary>
    public List<ParameterSuggestionDto> Suggestions { get; set; } = new();

    /// <summary>
    /// 优化目标状态
    /// </summary>
    public OptimizationGoalsDto Goals { get; set; } = new();
}

/// <summary>
/// 参数建议 DTO
/// </summary>
public class ParameterSuggestionDto
{
    /// <summary>
    /// 参数名称
    /// </summary>
    public string ParameterName { get; set; } = string.Empty;

    /// <summary>
    /// 当前值
    /// </summary>
    public object? CurrentValue { get; set; }

    /// <summary>
    /// 建议值
    /// </summary>
    public object? SuggestedValue { get; set; }

    /// <summary>
    /// 调整原因
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// 预期改进
    /// </summary>
    public string ExpectedImprovement { get; set; } = string.Empty;
}

/// <summary>
/// 调参策略信息 DTO
/// </summary>
public class StrategyInfoDto
{
    /// <summary>
    /// 策略名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 支持的算子列表
    /// </summary>
    public string[] SupportedOperators { get; set; } = Array.Empty<string>();
}

/// <summary>
/// 流程数据 DTO（简化版）
/// </summary>
public class FlowDataDto
{
    /// <summary>
    /// 节点列表
    /// </summary>
    public List<FlowNodeDto> Nodes { get; set; } = new();

    /// <summary>
    /// 连接列表
    /// </summary>
    public List<FlowConnectionDto> Connections { get; set; } = new();

    /// <summary>
    /// 转换为实体
    /// </summary>
    public OperatorFlow ToEntity()
    {
        var flow = new OperatorFlow("AutoTuneFlow");

        // 创建算子
        foreach (var nodeDto in Nodes)
        {
            var op = new Operator(nodeDto.Type.ToString(), nodeDto.Type, nodeDto.Position.X, nodeDto.Position.Y);
            // 添加参数
            foreach (var param in nodeDto.Parameters)
            {
                op.Parameters.Add(new Parameter(
                    Guid.NewGuid(),
                    param.Key,
                    param.Key,
                    string.Empty,
                    "string",
                    param.Value,
                    null, null, false, null));
            }
            flow.Operators.Add(op);
        }

        // 创建连接
        foreach (var conn in Connections)
        {
            flow.Connections.Add(new OperatorConnection(conn.SourceId, Guid.Empty, conn.TargetId, Guid.Empty));
        }

        return flow;
    }
}

public class FlowNodeDto
{
    public Guid Id { get; set; }
    public OperatorType Type { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
    public PositionDto Position { get; set; } = new();
}

public class PositionDto
{
    public double X { get; set; }
    public double Y { get; set; }
}

public class FlowConnectionDto
{
    public Guid SourceId { get; set; }
    public Guid TargetId { get; set; }
}

#endregion
