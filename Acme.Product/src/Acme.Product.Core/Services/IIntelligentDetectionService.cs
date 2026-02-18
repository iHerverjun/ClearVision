// IIntelligentDetectionService.cs
// 优先传统算法 - 传统算法权重更高
// 作者：蘅芜君

using Acme.Product.Core.Cameras;
using Acme.Product.Core.Entities;

namespace Acme.Product.Core.Services;

/// <summary>
/// 智能检测服务接口 - 支持自适应重拍和NG重试机制
/// </summary>
public interface IIntelligentDetectionService
{
    /// <summary>
    /// 执行带重试机制的检测流程
    /// </summary>
    /// <param name="cameraManager">相机管理器</param>
    /// <param name="cameraId">相机ID</param>
    /// <param name="flowService">流程执行服务</param>
    /// <param name="flow">检测流程</param>
    /// <param name="policy">重试策略</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检测结果</returns>
    Task<DetectionResult> ExecuteWithRetryAsync(
        ICameraManager cameraManager,
        string cameraId,
        IFlowExecutionService flowService,
        OperatorFlow flow,
        RetryPolicy policy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 自适应曝光调整
    /// </summary>
    /// <param name="camera">相机实例</param>
    /// <param name="imageBrightness">当前图像亮度</param>
    /// <param name="targetBrightness">目标亮度</param>
    /// <returns>调整后的曝光时间</returns>
    Task<double> AdjustExposureAsync(ICamera camera, double imageBrightness, double targetBrightness = 128.0);

    /// <summary>
    /// 计算图像亮度
    /// </summary>
    /// <param name="imageData">图像数据</param>
    /// <returns>平均亮度值</returns>
    double CalculateImageBrightness(byte[] imageData);

    /// <summary>
    /// 双模态投票 - 结合深度学习和传统算法结果
    /// </summary>
    /// <param name="dlResult">深度学习结果</param>
    /// <param name="traditionalResult">传统算法结果</param>
    /// <param name="strategy">投票策略</param>
    /// <returns>最终判定结果</returns>
    DetectionResult DualModalVoting(DetectionResult dlResult, DetectionResult traditionalResult, VotingStrategy strategy);
}

/// <summary>
/// 检测结果
/// </summary>
public class DetectionResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 是否合格(OK/NG)
    /// </summary>
    public bool IsOk { get; set; }

    /// <summary>
    /// 置信度
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// 图像数据
    /// </summary>
    public byte[]? ImageData { get; set; }

    /// <summary>
    /// 检测到的缺陷/目标列表
    /// </summary>
    public List<DetectionItem> Items { get; set; } = new();

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 执行时间(毫秒)
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static DetectionResult Failed(string errorMessage)
    {
        return new DetectionResult
        {
            IsSuccess = false,
            IsOk = false,
            ErrorMessage = errorMessage,
            Confidence = 0
        };
    }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static DetectionResult Success(bool isOk, double confidence, List<DetectionItem>? items = null)
    {
        return new DetectionResult
        {
            IsSuccess = true,
            IsOk = isOk,
            Confidence = confidence,
            Items = items ?? new List<DetectionItem>()
        };
    }
}

/// <summary>
/// 检测项
/// </summary>
public class DetectionItem
{
    /// <summary>
    /// 标签/类别
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// 置信度
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// 边界框(X, Y, Width, Height)
    /// </summary>
    public Rect BoundingBox { get; set; }

    /// <summary>
    /// 面积
    /// </summary>
    public double Area { get; set; }
}

/// <summary>
/// 矩形结构
/// </summary>
public struct Rect
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public Rect(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}

/// <summary>
/// 重试策略
/// </summary>
public class RetryPolicy
{
    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>
    /// 最小置信度阈值
    /// </summary>
    public double MinConfidence { get; set; } = 0.7;

    /// <summary>
    /// 是否启用自适应曝光
    /// </summary>
    public bool EnableAdaptiveExposure { get; set; } = true;

    /// <summary>
    /// 重试间隔(毫秒)
    /// </summary>
    public int RetryIntervalMs { get; set; } = 500;

    /// <summary>
    /// 目标亮度
    /// </summary>
    public double TargetBrightness { get; set; } = 128.0;
}

/// <summary>
/// 投票策略
/// </summary>
public enum VotingStrategy
{
    /// <summary>
    /// 一致同意 - 两个算法都判定为OK才算OK
    /// </summary>
    Unanimous,

    /// <summary>
    /// 多数表决 - 取多数结果
    /// </summary>
    Majority,

    /// <summary>
    /// 加权平均 - 按权重加权
    /// </summary>
    WeightedAverage,

    /// <summary>
    /// 优先深度学习 - DL权重更高
    /// </summary>
    PrioritizeDeepLearning,

    /// <summary>
    /// 优先传统算法 - 传统算法权重更高
    /// </summary>
    PrioritizeTraditional
}
