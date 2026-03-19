// AutoTuneService.cs
// 自动调参服务实现
// 【Phase 4】LLM 闭环验证 - 自动调参服务
// 作者：架构修复方案 v2

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System.Diagnostics;

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// 自动调参服务实现
/// 使用启发式搜索 + 二分搜索的智能调参策略
/// </summary>
public class AutoTuneService : IAutoTuneService
{
    private readonly ILogger<AutoTuneService> _logger;
    private readonly IFlowExecutionService _flowExecution;
    private readonly IPreviewMetricsAnalyzer _metricsAnalyzer;

    // 调参策略配置 - 映射到实际 OperatorType 值
    private static readonly Dictionary<OperatorType, TuningStrategy> StrategyMap = new()
    {
        [OperatorType.Thresholding] = TuningStrategy.BinarySearch,
        [OperatorType.GaussianBlur] = TuningStrategy.GradientDescent,
        [OperatorType.Morphology] = TuningStrategy.Heuristic,
        [OperatorType.ContourDetection] = TuningStrategy.Heuristic,
        [OperatorType.BlobAnalysis] = TuningStrategy.Heuristic,
        [OperatorType.EdgeDetection] = TuningStrategy.BinarySearch,
        [OperatorType.AdaptiveThreshold] = TuningStrategy.BinarySearch
    };

    // 参数范围定义
    private static readonly Dictionary<string, ParameterRange> ParameterRanges = new()
    {
        ["Threshold"] = new ParameterRange { Name = "Threshold", Min = 0, Max = 255, Step = 1, DataType = "int" },
        ["MaxValue"] = new ParameterRange { Name = "MaxValue", Min = 0, Max = 255, Step = 1, DataType = "int" },
        ["KernelSize"] = new ParameterRange { Name = "KernelSize", Min = 1, Max = 31, Step = 2, DataType = "int" },
        ["SigmaX"] = new ParameterRange { Name = "SigmaX", Min = 0.1, Max = 10, Step = 0.1, DataType = "double" },
        ["SigmaY"] = new ParameterRange { Name = "SigmaY", Min = 0.1, Max = 10, Step = 0.1, DataType = "double" },
        ["Iterations"] = new ParameterRange { Name = "Iterations", Min = 1, Max = 10, Step = 1, DataType = "int" },
        ["MinArea"] = new ParameterRange { Name = "MinArea", Min = 0, Max = 100000, Step = 1, DataType = "double" },
        ["MaxArea"] = new ParameterRange { Name = "MaxArea", Min = 0, Max = 1000000, Step = 1, DataType = "double" },
        ["MinCircularity"] = new ParameterRange { Name = "MinCircularity", Min = 0, Max = 1, Step = 0.01, DataType = "double" },
        ["MaxCircularity"] = new ParameterRange { Name = "MaxCircularity", Min = 0, Max = 1, Step = 0.01, DataType = "double" },
        ["CannyThreshold1"] = new ParameterRange { Name = "CannyThreshold1", Min = 0, Max = 500, Step = 1, DataType = "double" },
        ["CannyThreshold2"] = new ParameterRange { Name = "CannyThreshold2", Min = 0, Max = 500, Step = 1, DataType = "double" }
    };

    public AutoTuneService(
        ILogger<AutoTuneService> logger,
        IFlowExecutionService flowExecution,
        IPreviewMetricsAnalyzer metricsAnalyzer)
    {
        _logger = logger;
        _flowExecution = flowExecution;
        _metricsAnalyzer = metricsAnalyzer;
    }

    /// <inheritdoc />
    public async Task<AutoTuneResult> AutoTuneOperatorAsync(
        OperatorType type,
        byte[] inputImage,
        Dictionary<string, object> initialParameters,
        AutoTuneGoal goal,
        int maxIterations = 5,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new AutoTuneResult();
        var iterations = new List<AutoTuneIteration>();

        try
        {
            _logger.LogInformation(
                "[AutoTune] 开始调参: Type={Type}, MaxIterations={MaxIter}, TargetBlobCount={Target}",
                type, maxIterations, goal.TargetBlobCount);

            // 获取调参策略
            var strategy = StrategyMap.GetValueOrDefault(type, TuningStrategy.Heuristic);
            var currentParams = new Dictionary<string, object>(initialParameters);

            // 获取可调参数列表
            var tunableParams = GetTunableParameters(type, currentParams);

            double bestScore = 0;
            Dictionary<string, object> bestParams = new(currentParams);
            PreviewMetrics? bestMetrics = null;

            // 将输入图像转为 Mat
            using var inputMat = Cv2.ImDecode(inputImage, ImreadModes.Color);

            // 主循环：迭代调参
            for (int iteration = 1; iteration <= maxIterations; iteration++)
            {
                ct.ThrowIfCancellationRequested();

                var iterSw = Stopwatch.StartNew();

                // 创建临时算子执行
                var tempOperator = CreateOperator(type, currentParams);
                var tempFlow = new OperatorFlow($"AutoTune_{type}_{iteration}");
                tempFlow.AddOperator(tempOperator);

                // 执行获取结果
                var executionResult = await ExecuteOperatorAsync(type, inputMat, currentParams, ct);

                // 获取输出图像
                Mat? outputImage = null;
                if (executionResult.OutputData?.TryGetValue("Image", out var imgObj) == true && imgObj is Mat mat)
                {
                    outputImage = mat;
                }
                else if (executionResult.OutputData?.TryGetValue("OutputImage", out var outImgObj) == true && outImgObj is Mat outMat)
                {
                    outputImage = outMat;
                }

                if (outputImage == null)
                {
                    _logger.LogWarning("[AutoTune] 迭代 {Iteration}: 未获取到输出图像", iteration);
                    break;
                }

                // 分析指标
                var metrics = _metricsAnalyzer.Analyze(outputImage, executionResult.OutputData, goal);

                // 计算评分
                double score = CalculateScore(metrics, goal);

                // 记录迭代
                var iterationRecord = new AutoTuneIteration
                {
                    Iteration = iteration,
                    Parameters = new Dictionary<string, object>(currentParams),
                    Metrics = metrics,
                    Score = score,
                    ExecutionTimeMs = iterSw.ElapsedMilliseconds
                };
                iterations.Add(iterationRecord);

                _logger.LogDebug(
                    "[AutoTune] 迭代 {Iteration}: Score={Score:F3}, Blobs={Blobs}, Target={Target}",
                    iteration, score, metrics.Goals.CurrentBlobCount, goal.TargetBlobCount);

                // 更新最佳结果
                if (score > bestScore)
                {
                    bestScore = score;
                    bestParams = new Dictionary<string, object>(currentParams);
                    bestMetrics = metrics;
                }

                // 检查是否达到目标
                if (IsGoalAchieved(metrics, goal))
                {
                    _logger.LogInformation(
                        "[AutoTune] 达到目标，提前终止: Iteration={Iteration}, Score={Score:F3}",
                        iteration, score);
                    break;
                }

                // 根据策略调整参数
                var adjustment = strategy switch
                {
                    TuningStrategy.BinarySearch => AdjustBinarySearch(type, currentParams, metrics, goal, iteration),
                    TuningStrategy.GradientDescent => AdjustGradientDescent(type, currentParams, metrics, goal, iteration, iterations),
                    TuningStrategy.GridSearch => AdjustGridSearch(type, currentParams, metrics, goal, iteration, tunableParams),
                    _ => AdjustHeuristic(type, currentParams, metrics, goal, iteration)
                };

                if (adjustment == null || adjustment.Count == 0)
                {
                    _logger.LogInformation("[AutoTune] 无法进一步调整，终止");
                    break;
                }

                currentParams = adjustment;
            }

            // 构建结果
            result.Success = true;
            result.FinalParameters = bestParams;
            result.FinalScore = bestScore;
            result.Iterations = iterations;
            result.TotalIterations = iterations.Count;
            result.TotalExecutionTimeMs = sw.ElapsedMilliseconds;
            result.IsGoalAchieved = bestMetrics != null && IsGoalAchieved(bestMetrics, goal);

            _logger.LogInformation(
                "[AutoTune] 完成: Success={Success}, Iterations={Iterations}, Score={Score:F3}, Time={Time}ms",
                result.Success, result.TotalIterations, result.FinalScore, result.TotalExecutionTimeMs);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[AutoTune] 调参被取消");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AutoTune] 调参失败");
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Iterations = iterations;
            result.TotalIterations = iterations.Count;
            result.TotalExecutionTimeMs = sw.ElapsedMilliseconds;
            return result;
        }
    }

    /// <inheritdoc />
    public async Task<AutoTuneResult> AutoTuneInFlowAsync(
        OperatorFlow flow,
        Guid targetNodeId,
        byte[] inputImage,
        Dictionary<string, object> initialParameters,
        AutoTuneGoal goal,
        int maxIterations = 5,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new AutoTuneResult();
        var iterations = new List<AutoTuneIteration>();

        try
        {
            _logger.LogInformation(
                "[AutoTune] 请求流程节点调参: FlowId={FlowId}, NodeId={NodeId}",
                flow.Id, targetNodeId);

            // 找到目标算子
            var targetOperator = FindOperatorInFlow(flow, targetNodeId);
            if (targetOperator == null)
            {
                throw new ArgumentException($"未找到算子: {targetNodeId}");
            }

            var type = targetOperator.Type;
            var strategy = StrategyMap.GetValueOrDefault(type, TuningStrategy.Heuristic);
            var currentParams = new Dictionary<string, object>(initialParameters);

            using var inputMat = Cv2.ImDecode(inputImage, ImreadModes.Color);

            double bestScore = 0;
            Dictionary<string, object> bestParams = new(currentParams);
            PreviewMetrics? bestMetrics = null;

            for (int iteration = 1; iteration <= maxIterations; iteration++)
            {
                ct.ThrowIfCancellationRequested();
                var iterSw = Stopwatch.StartNew();

                // 更新算子参数
                UpdateOperatorParameters(targetOperator, currentParams);

                // 使用 Debug 模式执行到目标算子
                var options = new DebugOptions
                {
                    DebugSessionId = Guid.NewGuid(),
                    BreakAtOperatorId = targetNodeId,
                    EnableIntermediateCache = true
                };

                var inputData = new Dictionary<string, object> { ["Image"] = inputMat };
                var execResult = await _flowExecution.ExecuteFlowDebugAsync(flow, options, inputData);

                // 获取目标算子输出
                if (!execResult.IntermediateResults.TryGetValue(targetNodeId, out var targetOutput))
                {
                    throw new InvalidOperationException($"无法获取算子 {targetNodeId} 的输出");
                }

                // 获取输出图像
                Mat? outputImage = null;
                if (targetOutput.TryGetValue("Image", out var imgObj) && imgObj is Mat mat)
                {
                    outputImage = mat;
                }
                else if (targetOutput.TryGetValue("OutputImage", out var outImgObj) && outImgObj is Mat outMat)
                {
                    outputImage = outMat;
                }

                if (outputImage == null)
                {
                    _logger.LogWarning("[AutoTune] 迭代 {Iteration}: 未获取到输出图像", iteration);
                    break;
                }

                // 分析指标
                var metrics = _metricsAnalyzer.Analyze(outputImage, targetOutput, goal);
                double score = CalculateScore(metrics, goal);

                iterations.Add(new AutoTuneIteration
                {
                    Iteration = iteration,
                    Parameters = new Dictionary<string, object>(currentParams),
                    Metrics = metrics,
                    Score = score,
                    ExecutionTimeMs = iterSw.ElapsedMilliseconds
                });

                if (score > bestScore)
                {
                    bestScore = score;
                    bestParams = new Dictionary<string, object>(currentParams);
                    bestMetrics = metrics;
                }

                if (IsGoalAchieved(metrics, goal))
                {
                    break;
                }

                // 调整参数
                var adjustment = strategy switch
                {
                    TuningStrategy.BinarySearch => AdjustBinarySearch(type, currentParams, metrics, goal, iteration),
                    TuningStrategy.GradientDescent => AdjustGradientDescent(type, currentParams, metrics, goal, iteration, iterations),
                    _ => AdjustHeuristic(type, currentParams, metrics, goal, iteration)
                };

                if (adjustment == null || adjustment.Count == 0)
                {
                    break;
                }

                currentParams = adjustment;
            }

            result.Success = true;
            result.FinalParameters = bestParams;
            result.FinalScore = bestScore;
            result.Iterations = iterations;
            result.TotalIterations = iterations.Count;
            result.TotalExecutionTimeMs = sw.ElapsedMilliseconds;
            result.IsGoalAchieved = bestMetrics != null && IsGoalAchieved(bestMetrics, goal);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AutoTune] 流程内调参失败");
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Iterations = iterations;
            result.TotalIterations = iterations.Count;
            result.TotalExecutionTimeMs = sw.ElapsedMilliseconds;
            return result;
        }
    }

    #region 调参策略实现

    /// <summary>
    /// 二分搜索调参（适用于阈值类参数）
    /// </summary>
    private Dictionary<string, object>? AdjustBinarySearch(
        OperatorType type,
        Dictionary<string, object> currentParams,
        PreviewMetrics metrics,
        AutoTuneGoal goal,
        int iteration)
    {
        var newParams = new Dictionary<string, object>(currentParams);

        // 根据 Blob 数量与目标的差距调整阈值
        if (goal.TargetBlobCount.HasValue)
        {
            int currentCount = metrics.Goals.CurrentBlobCount;
            int targetCount = goal.TargetBlobCount.Value;

            // 获取当前阈值
            double currentThreshold = GetParamValue(currentParams, "Threshold", 128);
            double minThreshold = 0;
            double maxThreshold = 255;

            // Blob 太多 -> 提高阈值；Blob 太少 -> 降低阈值
            if (currentCount > targetCount * (1 + goal.Tolerance))
            {
                // 太多，提高阈值（向上二分）
                double newThreshold = (currentThreshold + maxThreshold) / 2;
                newParams["Threshold"] = (int)Math.Round(newThreshold);
                _logger.LogDebug("[AutoTune] 二分搜索: 提高阈值 {Old} -> {New}", currentThreshold, newThreshold);
            }
            else if (currentCount < targetCount * (1 - goal.Tolerance))
            {
                // 太少，降低阈值（向下二分）
                double newThreshold = (minThreshold + currentThreshold) / 2;
                newParams["Threshold"] = (int)Math.Round(newThreshold);
                _logger.LogDebug("[AutoTune] 二分搜索: 降低阈值 {Old} -> {New}", currentThreshold, newThreshold);
            }
            else
            {
                return null; // 已达到目标
            }
        }

        return newParams;
    }

    /// <summary>
    /// 梯度下降调参（适用于连续参数）
    /// </summary>
    private Dictionary<string, object>? AdjustGradientDescent(
        OperatorType type,
        Dictionary<string, object> currentParams,
        PreviewMetrics metrics,
        AutoTuneGoal goal,
        int iteration,
        List<AutoTuneIteration> history)
    {
        var newParams = new Dictionary<string, object>(currentParams);

        // 需要至少 2 次迭代计算梯度
        if (history.Count < 2)
        {
            // 第一次：尝试小幅增加
            double currentSigma = GetParamValue(currentParams, "SigmaX", 1.0);
            newParams["SigmaX"] = Math.Min(currentSigma + 0.5, 10.0);
            return newParams;
        }

        // 计算梯度方向
        var last = history[^1];
        var prev = history[^2];

        double scoreDiff = last.Score - prev.Score;
        double paramDiff = GetParamValue(last.Parameters, "SigmaX", 1.0) - GetParamValue(prev.Parameters, "SigmaX", 1.0);

        if (Math.Abs(paramDiff) < 0.001)
        {
            return null; // 收敛
        }

        double gradient = scoreDiff / paramDiff;
        double learningRate = 0.5;
        double currentVal = GetParamValue(currentParams, "SigmaX", 1.0);
        double newVal = currentVal + learningRate * gradient;

        // 限制范围
        newVal = Math.Clamp(newVal, 0.1, 10.0);
        newParams["SigmaX"] = newVal;

        _logger.LogDebug("[AutoTune] 梯度下降: 梯度={Gradient:F4}, 新值={NewVal:F2}", gradient, newVal);

        return newParams;
    }

    /// <summary>
    /// 启发式调参（基于规则）
    /// </summary>
    private Dictionary<string, object>? AdjustHeuristic(
        OperatorType type,
        Dictionary<string, object> currentParams,
        PreviewMetrics metrics,
        AutoTuneGoal goal,
        int iteration)
    {
        var newParams = new Dictionary<string, object>(currentParams);

        switch (type)
        {
            case OperatorType.BlobAnalysis:
                return AdjustBlobAnalysisHeuristic(newParams, metrics, goal);

            case OperatorType.Morphology:
            case OperatorType.MorphologicalOperation:
                return AdjustMorphologyHeuristic(newParams, metrics, goal);

            case OperatorType.GaussianBlur:
                return AdjustBlurHeuristic(newParams, metrics, goal);

            case OperatorType.ContourDetection:
                return AdjustContourDetectionHeuristic(newParams, metrics, goal);

            case OperatorType.Thresholding:
            case OperatorType.AdaptiveThreshold:
                return AdjustThresholdHeuristic(newParams, metrics, goal);

            default:
                return AdjustGenericHeuristic(newParams, metrics, goal);
        }
    }

    /// <summary>
    /// Blob 分析启发式调参
    /// </summary>
    private Dictionary<string, object>? AdjustBlobAnalysisHeuristic(
        Dictionary<string, object> currentParams,
        PreviewMetrics metrics,
        AutoTuneGoal goal)
    {
        var newParams = new Dictionary<string, object>(currentParams);

        // 根据噪声情况调整面积阈值
        if (metrics.Diagnostics.Contains("noise"))
        {
            double currentMinArea = GetParamValue(currentParams, "MinArea", 100);
            newParams["MinArea"] = currentMinArea * 1.5; // 提高最小面积过滤噪声
            _logger.LogDebug("[AutoTune] 启发式: 检测到噪声，提高 MinArea {Old} -> {New}",
                currentMinArea, newParams["MinArea"]);
        }

        // 根据碎片化情况调整
        if (metrics.Diagnostics.Contains("fragmentation"))
        {
            double currentMinCircularity = GetParamValue(currentParams, "MinCircularity", 0);
            newParams["MinCircularity"] = Math.Min(currentMinCircularity + 0.1, 1.0);
            _logger.LogDebug("[AutoTune] 启发式: 检测到碎片化，提高 MinCircularity");
        }

        // 根据目标数量调整
        if (goal.TargetBlobCount.HasValue)
        {
            int currentCount = metrics.Goals.CurrentBlobCount;
            int targetCount = goal.TargetBlobCount.Value;

            if (currentCount > targetCount * 1.5)
            {
                // 数量过多，收紧条件
                double currentMinArea = GetParamValue(newParams, "MinArea", 100);
                newParams["MinArea"] = currentMinArea * 1.3;
            }
            else if (currentCount < targetCount * 0.5)
            {
                // 数量过少，放宽条件
                double currentMinArea = GetParamValue(newParams, "MinArea", 100);
                newParams["MinArea"] = Math.Max(currentMinArea * 0.7, 10);
            }
        }

        return newParams;
    }

    /// <summary>
    /// 形态学操作启发式调参
    /// </summary>
    private Dictionary<string, object>? AdjustMorphologyHeuristic(
        Dictionary<string, object> currentParams,
        PreviewMetrics metrics,
        AutoTuneGoal goal)
    {
        var newParams = new Dictionary<string, object>(currentParams);

        // 碎片化 -> 增加闭运算迭代次数
        if (metrics.Diagnostics.Contains("fragmentation"))
        {
            int currentIterations = (int)GetParamValue(currentParams, "Iterations", 1);
            newParams["Iterations"] = Math.Min(currentIterations + 1, 5);
            newParams["Operation"] = "Close"; // 使用闭运算连接碎片
        }

        // 噪声 -> 增加开运算
        if (metrics.Diagnostics.Contains("noise"))
        {
            int currentIterations = (int)GetParamValue(currentParams, "Iterations", 1);
            newParams["Iterations"] = Math.Min(currentIterations + 1, 5);
            newParams["Operation"] = "Open"; // 使用开运算去除噪声
        }

        return newParams;
    }

    /// <summary>
    /// 高斯模糊启发式调参
    /// </summary>
    private Dictionary<string, object>? AdjustBlurHeuristic(
        Dictionary<string, object> currentParams,
        PreviewMetrics metrics,
        AutoTuneGoal goal)
    {
        var newParams = new Dictionary<string, object>(currentParams);

        // 噪声多 -> 增加模糊程度
        if (metrics.Diagnostics.Contains("noise") || metrics.Diagnostics.Contains("reflection"))
        {
            double currentSigma = GetParamValue(currentParams, "SigmaX", 1.0);
            newParams["SigmaX"] = Math.Min(currentSigma + 0.5, 5.0);
            newParams["SigmaY"] = Math.Min(currentSigma + 0.5, 5.0);
        }

        // 图像太模糊 -> 减小模糊
        if (metrics.ImageStats.LaplacianVariance < 100)
        {
            double currentSigma = GetParamValue(currentParams, "SigmaX", 1.0);
            newParams["SigmaX"] = Math.Max(currentSigma - 0.3, 0.1);
            newParams["SigmaY"] = Math.Max(currentSigma - 0.3, 0.1);
        }

        return newParams;
    }

    /// <summary>
    /// 轮廓检测启发式调参
    /// </summary>
    private Dictionary<string, object>? AdjustContourDetectionHeuristic(
        Dictionary<string, object> currentParams,
        PreviewMetrics metrics,
        AutoTuneGoal goal)
    {
        // 主要通过预处理影响结果，此处可调整检索模式
        var newParams = new Dictionary<string, object>(currentParams);

        if (metrics.Goals.FragmentPenalty > 5)
        {
            // 碎片化严重，使用 RETR_EXTERNAL 只检测外层轮廓
            newParams["RetrievalMode"] = "External";
        }

        return newParams;
    }

    /// <summary>
    /// 阈值启发式调参
    /// </summary>
    private Dictionary<string, object>? AdjustThresholdHeuristic(
        Dictionary<string, object> currentParams,
        PreviewMetrics metrics,
        AutoTuneGoal goal)
    {
        var newParams = new Dictionary<string, object>(currentParams);

        if (!goal.TargetBlobCount.HasValue)
        {
            return newParams;
        }

        int currentCount = metrics.Goals.CurrentBlobCount;
        int targetCount = goal.TargetBlobCount.Value;
        double currentThreshold = GetParamValue(currentParams, "Threshold", 128);

        // 简单反馈：根据数量差异调整阈值
        if (currentCount > targetCount * 1.2)
        {
            // Blob 太多，提高阈值
            newParams["Threshold"] = Math.Min(currentThreshold + 10, 255);
        }
        else if (currentCount < targetCount * 0.8)
        {
            // Blob 太少，降低阈值
            newParams["Threshold"] = Math.Max(currentThreshold - 10, 0);
        }

        return newParams;
    }

    /// <summary>
    /// 通用启发式调参
    /// </summary>
    private Dictionary<string, object>? AdjustGenericHeuristic(
        Dictionary<string, object> currentParams,
        PreviewMetrics metrics,
        AutoTuneGoal goal)
    {
        // 简单反馈：根据数量差异调整
        if (!goal.TargetBlobCount.HasValue)
        {
            return null;
        }

        var newParams = new Dictionary<string, object>(currentParams);
        int currentCount = metrics.Goals.CurrentBlobCount;
        int targetCount = goal.TargetBlobCount.Value;

        if (currentCount > targetCount)
        {
            // 尝试收紧所有数值参数
            foreach (var key in newParams.Keys.ToList())
            {
                if (newParams[key] is double d && d > 0)
                {
                    newParams[key] = d * 1.1;
                }
                else if (newParams[key] is int i && i > 0)
                {
                    newParams[key] = (int)(i * 1.1);
                }
            }
        }
        else if (currentCount < targetCount)
        {
            // 尝试放宽所有数值参数
            foreach (var key in newParams.Keys.ToList())
            {
                if (newParams[key] is double d && d > 0)
                {
                    newParams[key] = d * 0.9;
                }
                else if (newParams[key] is int i && i > 0)
                {
                    newParams[key] = (int)(i * 0.9);
                }
            }
        }

        return newParams;
    }

    /// <summary>
    /// 网格搜索调参（适用于多参数组合）
    /// </summary>
    private Dictionary<string, object>? AdjustGridSearch(
        OperatorType type,
        Dictionary<string, object> currentParams,
        PreviewMetrics metrics,
        AutoTuneGoal goal,
        int iteration,
        List<ParameterRange> tunableParams)
    {
        // 简化实现：轮流调整各个参数
        if (tunableParams.Count == 0)
        {
            return null;
        }

        var newParams = new Dictionary<string, object>(currentParams);
        var paramToAdjust = tunableParams[iteration % tunableParams.Count];

        double currentVal = GetParamValue(currentParams, paramToAdjust.Name, paramToAdjust.Current);
        double step = paramToAdjust.Step;

        // 尝试增加
        double newVal = Math.Min(currentVal + step, paramToAdjust.Max);
        newParams[paramToAdjust.Name] = paramToAdjust.DataType == "int" ? (int)newVal : newVal;

        return newParams;
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 计算评分
    /// </summary>
    private double CalculateScore(PreviewMetrics metrics, AutoTuneGoal goal)
    {
        double score = 0;
        double totalWeight = 0;

        // Blob 数量匹配度（权重 40%）
        if (goal.TargetBlobCount.HasValue)
        {
            double targetCount = goal.TargetBlobCount.Value;
            double actualCount = metrics.Goals.CurrentBlobCount;
            double countDiff = Math.Abs(actualCount - targetCount) / targetCount;
            double countScore = Math.Max(0, 1 - countDiff / goal.Tolerance);
            score += countScore * 0.4;
            totalWeight += 0.4;
        }

        // 面积分布评分（权重 20%）
        score += metrics.Goals.AreaDistributionScore * 0.2;
        totalWeight += 0.2;

        // 形状规则度（权重 20%）
        score += metrics.Goals.ShapeRegularityScore * 0.2;
        totalWeight += 0.2;

        // 惩罚项（权重 20%，负向）
        double penaltyScore = 1.0;
        penaltyScore -= Math.Min(metrics.Goals.NoisePenalty * 0.05, 0.5);
        penaltyScore -= Math.Min(metrics.Goals.FragmentPenalty * 0.05, 0.5);
        score += Math.Max(0, penaltyScore) * 0.2;
        totalWeight += 0.2;

        return totalWeight > 0 ? score / totalWeight : 0;
    }

    /// <summary>
    /// 检查是否达到目标
    /// </summary>
    private bool IsGoalAchieved(PreviewMetrics metrics, AutoTuneGoal goal)
    {
        if (!goal.TargetBlobCount.HasValue)
        {
            return false;
        }

        double targetCount = goal.TargetBlobCount.Value;
        double actualCount = metrics.Goals.CurrentBlobCount;
        double tolerance = goal.Tolerance;

        // 数量在容差范围内
        bool countOk = Math.Abs(actualCount - targetCount) <= targetCount * tolerance;

        // 惩罚项在可接受范围
        bool penaltyOk = metrics.Goals.NoisePenalty <= 2 && metrics.Goals.FragmentPenalty <= 2;

        return countOk && penaltyOk;
    }

    /// <summary>
    /// 获取可调参数列表
    /// </summary>
    private List<ParameterRange> GetTunableParameters(OperatorType type, Dictionary<string, object> currentParams)
    {
        var result = new List<ParameterRange>();

        foreach (var param in currentParams)
        {
            if (ParameterRanges.TryGetValue(param.Key, out var range))
            {
                var rangeCopy = new ParameterRange
                {
                    Name = range.Name,
                    Min = range.Min,
                    Max = range.Max,
                    Step = range.Step,
                    DataType = range.DataType,
                    Current = param.Value is double d ? d : Convert.ToDouble(param.Value)
                };
                result.Add(rangeCopy);
            }
        }

        return result;
    }

    /// <summary>
    /// 获取参数值
    /// </summary>
    private double GetParamValue(Dictionary<string, object> parameters, string key, double defaultValue)
    {
        if (parameters.TryGetValue(key, out var value))
        {
            return value switch
            {
                double d => d,
                int i => i,
                float f => f,
                string s => double.TryParse(s, out var parsed) ? parsed : defaultValue,
                _ => defaultValue
            };
        }
        return defaultValue;
    }

    /// <summary>
    /// 创建临时算子
    /// </summary>
    private Operator CreateOperator(OperatorType type, Dictionary<string, object> parameters)
    {
        var op = new Operator($"AutoTune_{type}", type, 0, 0);

        foreach (var kvp in parameters)
        {
            var param = new Parameter(
                Guid.NewGuid(),
                kvp.Key,
                kvp.Key,
                string.Empty,
                "string",
                kvp.Value,
                null,
                null,
                false,
                null);
            param.SetValue(kvp.Value);
            op.Parameters.Add(param);
        }

        return op;
    }

    /// <summary>
    /// 查找流程中的算子
    /// </summary>
    private Operator? FindOperatorInFlow(OperatorFlow flow, Guid operatorId)
    {
        return flow.Operators.FirstOrDefault(o => o.Id == operatorId);
    }

    /// <summary>
    /// 更新算子参数
    /// </summary>
    private void UpdateOperatorParameters(Operator op, Dictionary<string, object> parameters)
    {
        foreach (var kvp in parameters)
        {
            var existing = op.Parameters.FirstOrDefault(p => p.Name == kvp.Key);
            if (existing != null)
            {
                existing.SetValue(kvp.Value);
            }
            else
            {
                var param = new Parameter(
                    Guid.NewGuid(),
                    kvp.Key,
                    kvp.Key,
                    string.Empty,
                    "string",
                    kvp.Value,
                    null,
                    null,
                    false,
                    null);
                param.SetValue(kvp.Value);
                op.Parameters.Add(param);
            }
        }
    }

    /// <summary>
    /// 执行单个算子
    /// </summary>
    private async Task<FlowDebugExecutionResult> ExecuteOperatorAsync(
        OperatorType type,
        Mat inputImage,
        Dictionary<string, object> parameters,
        CancellationToken ct)
    {
        // 创建临时流程执行
        var op = CreateOperator(type, parameters);
        var flow = new OperatorFlow($"AutoTune_{type}");
        flow.AddOperator(op);

        var inputData = new Dictionary<string, object> { ["Image"] = inputImage };

        var options = new DebugOptions
        {
            DebugSessionId = Guid.NewGuid(),
            EnableIntermediateCache = true
        };

        return await _flowExecution.ExecuteFlowDebugAsync(flow, options, inputData);
    }

    #endregion
}
