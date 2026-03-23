// IAutoTuneService.cs
// 自动调参服务接口
// 【Phase 4】LLM 闭环验证 - 自动调参
// 作者：架构修复方案 v2

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;

namespace Acme.Product.Core.Services;

/// <summary>
/// 自动调参服务
/// 职责：根据执行反馈自动调整参数，迭代优化直到达到目标
/// </summary>
public interface IAutoTuneService
{
    /// <summary>
    /// 自动调参
    /// </summary>
    /// <param name="type">算子类型</param>
    /// <param name="inputImage">输入图像</param>
    /// <param name="initialParameters">初始参数</param>
    /// <param name="goal">调参目标</param>
    /// <param name="maxIterations">最大迭代次数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>调参结果</returns>
    Task<AutoTuneResult> AutoTuneOperatorAsync(
        OperatorType type,
        byte[] inputImage,
        Dictionary<string, object> initialParameters,
        AutoTuneGoal goal,
        int maxIterations = 5,
        CancellationToken ct = default);

    /// <summary>
    /// 带预览的自动调参
    /// </summary>
    /// <param name="flow">完整流程</param>
    /// <param name="targetNodeId">目标节点ID</param>
    /// <param name="inputImage">输入图像</param>
    /// <param name="initialParameters">初始参数</param>
    /// <param name="goal">调参目标</param>
    /// <param name="maxIterations">最大迭代次数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>调参结果</returns>
    Task<AutoTuneResult> AutoTuneInFlowAsync(
        OperatorFlow flow,
        Guid targetNodeId,
        byte[] inputImage,
        Dictionary<string, object> initialParameters,
        AutoTuneGoal goal,
        int maxIterations = 5,
        CancellationToken ct = default);

    /// <summary>
    /// 场景级自动调参
    /// </summary>
    /// <param name="scenarioKey">场景键</param>
    /// <param name="flow">完整流程</param>
    /// <param name="inputImage">输入图像</param>
    /// <param name="goal">调参目标</param>
    /// <param name="maxIterations">最大迭代次数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>场景级调参结果</returns>
    Task<ScenarioAutoTuneResult> AutoTuneScenarioAsync(
        string scenarioKey,
        OperatorFlow flow,
        byte[] inputImage,
        AutoTuneGoal goal,
        int maxIterations = 5,
        CancellationToken ct = default);
}

/// <summary>
/// 调参策略
/// </summary>
public enum TuningStrategy
{
    /// <summary>
    /// 二分搜索（适用于阈值类参数）
    /// </summary>
    BinarySearch,

    /// <summary>
    /// 梯度下降（适用于连续参数）
    /// </summary>
    GradientDescent,

    /// <summary>
    /// 网格搜索（适用于多参数组合）
    /// </summary>
    GridSearch,

    /// <summary>
    /// 启发式搜索（基于规则的智能调整）
    /// </summary>
    Heuristic
}

/// <summary>
/// 参数范围定义
/// </summary>
public class ParameterRange
{
    /// <summary>
    /// 参数名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 最小值
    /// </summary>
    public double Min { get; set; }

    /// <summary>
    /// 最大值
    /// </summary>
    public double Max { get; set; }

    /// <summary>
    /// 步长
    /// </summary>
    public double Step { get; set; } = 1;

    /// <summary>
    /// 当前值
    /// </summary>
    public double Current { get; set; }

    /// <summary>
    /// 数据类型
    /// </summary>
    public string DataType { get; set; } = "double";
}
