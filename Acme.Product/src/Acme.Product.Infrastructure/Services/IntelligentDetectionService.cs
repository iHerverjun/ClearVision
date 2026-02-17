using Acme.Product.Core.Cameras;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Services;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// 智能检测服务实现
/// </summary>
public class IntelligentDetectionService : IIntelligentDetectionService
{
    private readonly ILogger<IntelligentDetectionService> _logger;

    public IntelligentDetectionService(ILogger<IntelligentDetectionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 执行带重试机制的检测流程
    /// </summary>
    public async Task<DetectionResult> ExecuteWithRetryAsync(
        ICameraManager cameraManager,
        string cameraId,
        IFlowExecutionService flowService,
        OperatorFlow flow,
        RetryPolicy policy,
        CancellationToken cancellationToken = default)
    {
        DetectionResult? lastResult = null;

        for (int attempt = 0; attempt <= policy.MaxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("[智能检测] 第 {Attempt}/{MaxRetries} 次检测尝试", attempt + 1, policy.MaxRetries + 1);

                // 获取或创建相机
                var camera = await cameraManager.GetOrCreateCameraAsync(cameraId);

                // 采集图像
                var imageBytes = await camera.AcquireSingleFrameAsync();

                // 执行流程
                var flowResult = await flowService.ExecuteFlowAsync(flow, new Dictionary<string, object>
                {
                    { "Image", imageBytes }
                }, cancellationToken: cancellationToken);

                // 解析结果
                var result = ParseFlowResult(flowResult);
                result.RetryCount = attempt;

                // 检查是否满足条件
                if (result.IsSuccess && result.Confidence >= policy.MinConfidence)
                {
                    _logger.LogInformation("[智能检测] 检测成功，置信度: {Confidence:F2}", result.Confidence);
                    return result;
                }

                // NG情况，如果需要重试
                if (attempt < policy.MaxRetries)
                {
                    _logger.LogWarning("[智能检测] 检测结果NG或置信度不足({Confidence:F2})，准备重试...", result.Confidence);
                    lastResult = result;

                    // 自适应曝光调整
                    if (policy.EnableAdaptiveExposure)
                    {
                        var brightness = CalculateImageBrightness(imageBytes);
                        var newExposure = await AdjustExposureAsync(camera, brightness, policy.TargetBrightness);
                        _logger.LogInformation("[智能检测] 自适应调整曝光: {OldBrightness:F0} -> {NewExposure:F0}us", brightness, newExposure);
                    }

                    // 等待重试间隔
                    await Task.Delay(policy.RetryIntervalMs, cancellationToken);
                }
                else
                {
                    // 最后一次尝试
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[智能检测] 检测执行异常");
                
                if (attempt >= policy.MaxRetries)
                {
                    return DetectionResult.Failed($"检测执行异常: {ex.Message}");
                }

                await Task.Delay(policy.RetryIntervalMs, cancellationToken);
            }
        }

        return lastResult ?? DetectionResult.Failed("超过最大重试次数");
    }

    /// <summary>
    /// 自适应曝光调整
    /// </summary>
    public async Task<double> AdjustExposureAsync(ICamera camera, double imageBrightness, double targetBrightness = 128.0)
    {
        try
        {
            var parameters = camera.GetParameters();
            var currentExposure = parameters.ExposureTime;

            // 计算曝光调整比例
            var ratio = targetBrightness / Math.Max(imageBrightness, 1.0);
            
            // 限制调整范围 (0.5x - 2.0x)
            ratio = Math.Clamp(ratio, 0.5, 2.0);
            
            var newExposure = currentExposure * ratio;
            
            // 限制曝光时间范围 (100us - 1000000us)
            newExposure = Math.Clamp(newExposure, 100.0, 1000000.0);

            await camera.SetExposureTimeAsync(newExposure);
            
            return newExposure;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[智能检测] 调整曝光失败");
            return 5000; // 返回默认值
        }
    }

    /// <summary>
    /// 计算图像亮度
    /// </summary>
    public double CalculateImageBrightness(byte[] imageData)
    {
        try
        {
            // 简化的亮度计算 - 基于图像数据的平均值
            // 实际应用中应该使用OpenCvSharp解码图像后计算
            if (imageData == null || imageData.Length == 0)
                return 128.0;

            // 对于JPEG/PNG等格式，这里简化处理
            // 取部分样本计算平均亮度
            var samples = imageData.Length > 1000 ? imageData.Take(1000) : imageData;
            double sum = samples.Sum(b => (double)b);
            return sum / samples.Count();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[智能检测] 计算图像亮度失败");
            return 128.0;
        }
    }

    /// <summary>
    /// 双模态投票
    /// </summary>
    public DetectionResult DualModalVoting(DetectionResult dlResult, DetectionResult traditionalResult, VotingStrategy strategy)
    {
        try
        {
            DetectionResult finalResult;

            switch (strategy)
            {
                case VotingStrategy.Unanimous:
                    // 一致同意：都OK才算OK
                    finalResult = new DetectionResult
                    {
                        IsOk = dlResult.IsOk && traditionalResult.IsOk,
                        Confidence = Math.Min(dlResult.Confidence, traditionalResult.Confidence),
                        IsSuccess = dlResult.IsSuccess && traditionalResult.IsSuccess,
                        Items = MergeItems(dlResult.Items, traditionalResult.Items),
                        ErrorMessage = dlResult.ErrorMessage ?? traditionalResult.ErrorMessage
                    };
                    break;

                case VotingStrategy.Majority:
                    // 多数表决
                    var okCount = (dlResult.IsOk ? 1 : 0) + (traditionalResult.IsOk ? 1 : 0);
                    finalResult = new DetectionResult
                    {
                        IsOk = okCount >= 1, // 至少一个OK就算OK
                        Confidence = (dlResult.Confidence + traditionalResult.Confidence) / 2,
                        IsSuccess = dlResult.IsSuccess || traditionalResult.IsSuccess,
                        Items = MergeItems(dlResult.Items, traditionalResult.Items),
                        ErrorMessage = dlResult.ErrorMessage ?? traditionalResult.ErrorMessage
                    };
                    break;

                case VotingStrategy.WeightedAverage:
                    // 加权平均 (各50%)
                    var weightedOk = (dlResult.IsOk ? 0.5 : 0) + (traditionalResult.IsOk ? 0.5 : 0);
                    finalResult = new DetectionResult
                    {
                        IsOk = weightedOk >= 0.5,
                        Confidence = (dlResult.Confidence + traditionalResult.Confidence) / 2,
                        IsSuccess = dlResult.IsSuccess || traditionalResult.IsSuccess,
                        Items = MergeItems(dlResult.Items, traditionalResult.Items),
                        ErrorMessage = dlResult.ErrorMessage ?? traditionalResult.ErrorMessage
                    };
                    break;

                case VotingStrategy.PrioritizeDeepLearning:
                    // 优先深度学习
                    finalResult = new DetectionResult
                    {
                        IsOk = dlResult.IsOk,
                        Confidence = dlResult.Confidence * 0.7 + traditionalResult.Confidence * 0.3,
                        IsSuccess = dlResult.IsSuccess,
                        Items = dlResult.Items.Any() ? dlResult.Items : traditionalResult.Items,
                        ErrorMessage = dlResult.ErrorMessage ?? traditionalResult.ErrorMessage
                    };
                    break;

                case VotingStrategy.PrioritizeTraditional:
                    // 优先传统算法
                    finalResult = new DetectionResult
                    {
                        IsOk = traditionalResult.IsOk,
                        Confidence = traditionalResult.Confidence * 0.7 + dlResult.Confidence * 0.3,
                        IsSuccess = traditionalResult.IsSuccess,
                        Items = traditionalResult.Items.Any() ? traditionalResult.Items : dlResult.Items,
                        ErrorMessage = traditionalResult.ErrorMessage ?? dlResult.ErrorMessage
                    };
                    break;

                default:
                    // 默认使用多数表决
                    finalResult = new DetectionResult
                    {
                        IsOk = dlResult.IsOk || traditionalResult.IsOk,
                        Confidence = Math.Max(dlResult.Confidence, traditionalResult.Confidence),
                        IsSuccess = dlResult.IsSuccess || traditionalResult.IsSuccess,
                        Items = MergeItems(dlResult.Items, traditionalResult.Items),
                        ErrorMessage = dlResult.ErrorMessage ?? traditionalResult.ErrorMessage
                    };
                    break;
            }

            _logger.LogInformation("[智能检测] 双模态投票结果: {IsOk}, 置信度: {Confidence:F2}, 策略: {Strategy}", 
                finalResult.IsOk, finalResult.Confidence, strategy);

            return finalResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[智能检测] 双模态投票失败");
            return dlResult; // 失败时返回深度学习结果
        }
    }

    /// <summary>
    /// 解析流程执行结果
    /// </summary>
    private DetectionResult ParseFlowResult(FlowExecutionResult flowResult)
    {
        if (!flowResult.IsSuccess)
        {
            return DetectionResult.Failed(flowResult.ErrorMessage ?? "流程执行失败");
        }

        // 从流程结果中解析检测信息
        var isOk = true;
        var confidence = 1.0;
        var items = new List<DetectionItem>();

        // 查找结果判定算子的输出
        var judgmentResult = flowResult.OperatorResults
            .LastOrDefault(r => r.OutputData?.ContainsKey("IsOk") == true);

        if (judgmentResult?.OutputData != null)
        {
            if (judgmentResult.OutputData.TryGetValue("IsOk", out var isOkObj) && isOkObj is bool isOkValue)
            {
                isOk = isOkValue;
            }

            if (judgmentResult.OutputData.TryGetValue("Confidence", out var confObj) && confObj is double confValue)
            {
                confidence = confValue;
            }
        }

        return new DetectionResult
        {
            IsSuccess = true,
            IsOk = isOk,
            Confidence = confidence,
            Items = items,
            ExecutionTimeMs = flowResult.ExecutionTimeMs
        };
    }

    /// <summary>
    /// 合并两个检测结果列表
    /// </summary>
    private List<DetectionItem> MergeItems(List<DetectionItem> items1, List<DetectionItem> items2)
    {
        var merged = new List<DetectionItem>();
        merged.AddRange(items1);
        
        // 添加不重叠的项
        foreach (var item2 in items2)
        {
            bool overlap = items1.Any(item1 => CalculateIoU(item1.BoundingBox, item2.BoundingBox) > 0.5);
            if (!overlap)
            {
                merged.Add(item2);
            }
        }

        return merged;
    }

    /// <summary>
    /// 计算两个矩形的IoU
    /// </summary>
    private double CalculateIoU(Rect rect1, Rect rect2)
    {
        int x1 = Math.Max(rect1.X, rect2.X);
        int y1 = Math.Max(rect1.Y, rect2.Y);
        int x2 = Math.Min(rect1.X + rect1.Width, rect2.X + rect2.Width);
        int y2 = Math.Min(rect1.Y + rect1.Height, rect2.Y + rect2.Height);

        if (x2 <= x1 || y2 <= y1)
            return 0.0;

        double intersectionArea = (x2 - x1) * (y2 - y1);
        double unionArea = rect1.Width * rect1.Height + rect2.Width * rect2.Height - intersectionArea;

        return intersectionArea / unionArea;
    }
}
