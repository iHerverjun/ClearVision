using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.Services;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 双模态投票算子 - 结合深度学习和传统算法结果进行投票决策
/// </summary>
/// <remarks>
/// 支持多种投票策略：
/// - Unanimous: 一致同意，两个算法都判定为OK才算OK
/// - Majority: 多数表决，取多数结果
/// - WeightedAverage: 加权平均，按权重加权计算
/// - PrioritizeDeepLearning: 优先深度学习，DL权重更高
/// - PrioritizeTraditional: 优先传统算法，传统算法权重更高
/// </remarks>
public class DualModalVotingOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.DualModalVoting;

    public DualModalVotingOperator(ILogger<DualModalVotingOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        // 获取参数
        var strategy = GetStringParam(@operator, "VotingStrategy", "WeightedAverage");
        var dlWeight = GetDoubleParam(@operator, "DLWeight", 0.6, 0.0, 1.0);
        var traditionalWeight = GetDoubleParam(@operator, "TraditionalWeight", 0.4, 0.0, 1.0);
        var confidenceThreshold = GetDoubleParam(@operator, "ConfidenceThreshold", 0.5, 0.0, 1.0);
        var okOutputValue = GetStringParam(@operator, "OkOutputValue", "1");
        var ngOutputValue = GetStringParam(@operator, "NgOutputValue", "0");

        // 从输入中获取深度学习结果
        if (!TryGetInputValue(inputs, "DLResult", out DetectionResult? dlResult) || dlResult == null)
        {
            // 尝试从布尔值和置信度构建结果
            if (TryGetInputValue(inputs, "DLIsOk", out bool dlIsOk) && 
                TryGetInputValue(inputs, "DLConfidence", out double dlConfidence))
            {
                dlResult = DetectionResult.Success(dlIsOk, dlConfidence);
            }
            else
            {
                return Task.FromResult(CreateNgOutput("未找到深度学习结果输入(DLResult)", ngOutputValue));
            }
        }

        // 从输入中获取传统算法结果
        if (!TryGetInputValue(inputs, "TraditionalResult", out DetectionResult? traditionalResult) || traditionalResult == null)
        {
            // 尝试从布尔值和置信度构建结果
            if (TryGetInputValue(inputs, "TraditionalIsOk", out bool traditionalIsOk) && 
                TryGetInputValue(inputs, "TraditionalConfidence", out double traditionalConfidence))
            {
                traditionalResult = DetectionResult.Success(traditionalIsOk, traditionalConfidence);
            }
            else
            {
                return Task.FromResult(CreateNgOutput("未找到传统算法结果输入(TraditionalResult)", ngOutputValue));
            }
        }

        // 执行投票决策
        var (isOk, confidence, details) = ExecuteVoting(
            dlResult, 
            traditionalResult, 
            strategy, 
            dlWeight, 
            traditionalWeight,
            confidenceThreshold);

        // 准备输出
        var outputValue = isOk ? okOutputValue : ngOutputValue;
        var judgmentResult = isOk ? "OK" : "NG";

        var outputData = new Dictionary<string, object>
        {
            { "JudgmentResult", judgmentResult },
            { "JudgmentValue", outputValue },
            { "Confidence", confidence },
            { "VotingStrategy", strategy },
            { "IsOk", isOk },
            { "Details", details },
            { "DLIsOk", dlResult.IsOk },
            { "DLConfidence", dlResult.Confidence },
            { "TraditionalIsOk", traditionalResult.IsOk },
            { "TraditionalConfidence", traditionalResult.Confidence }
        };

        // 同时输出数值类型的判定值（便于PLC写入）
        if (int.TryParse(outputValue, out var intValue))
        {
            outputData["JudgmentValueInt"] = intValue;
        }

        Logger.LogInformation(
            "[DualModalVoting] 投票完成: 策略={Strategy}, DL结果={DLIsOk}({DLConfidence:F2}), 传统结果={TraditionalIsOk}({TraditionalConfidence:F2}), 最终结果={JudgmentResult}({Confidence:F2})",
            strategy, dlResult.IsOk, dlResult.Confidence, traditionalResult.IsOk, traditionalResult.Confidence, judgmentResult, confidence);

        return Task.FromResult(OperatorExecutionOutput.Success(outputData));
    }

    private (bool isOk, double confidence, string details) ExecuteVoting(
        DetectionResult dlResult,
        DetectionResult traditionalResult,
        string strategy,
        double dlWeight,
        double traditionalWeight,
        double confidenceThreshold)
    {
        switch (strategy.ToLower())
        {
            case "unanimous":
                // 一致同意：两个算法都判定为OK才算OK
                var unanimousOk = dlResult.IsOk && traditionalResult.IsOk;
                var unanimousConfidence = Math.Min(dlResult.Confidence, traditionalResult.Confidence);
                var unanimousDetails = $"一致同意策略: DL={dlResult.IsOk}, 传统={traditionalResult.IsOk}, 结果={unanimousOk}";
                return (unanimousOk, unanimousConfidence, unanimousDetails);

            case "majority":
                // 多数表决：取多数结果
                var majorityOk = (dlResult.IsOk == traditionalResult.IsOk) 
                    ? dlResult.IsOk 
                    : (dlResult.Confidence > traditionalResult.Confidence ? dlResult.IsOk : traditionalResult.IsOk);
                var majorityConfidence = (dlResult.IsOk == traditionalResult.IsOk)
                    ? Math.Max(dlResult.Confidence, traditionalResult.Confidence)
                    : (dlResult.Confidence > traditionalResult.Confidence ? dlResult.Confidence : traditionalResult.Confidence);
                var majorityDetails = $"多数表决策略: DL={dlResult.IsOk}({dlResult.Confidence:F2}), 传统={traditionalResult.IsOk}({traditionalResult.Confidence:F2}), 结果={majorityOk}";
                return (majorityOk, majorityConfidence, majorityDetails);

            case "weightedaverage":
                // 加权平均：按权重加权计算
                var weightedScore = (dlResult.IsOk ? dlResult.Confidence : 1 - dlResult.Confidence) * dlWeight +
                                    (traditionalResult.IsOk ? traditionalResult.Confidence : 1 - traditionalResult.Confidence) * traditionalWeight;
                var weightedOk = weightedScore >= confidenceThreshold;
                var weightedConfidence = weightedScore;
                var weightedDetails = $"加权平均策略: 加权分数={weightedScore:F4}(阈值{confidenceThreshold}), DL权重={dlWeight}, 传统权重={traditionalWeight}, 结果={weightedOk}";
                return (weightedOk, weightedConfidence, weightedDetails);

            case "prioritizedeeplearning":
                // 优先深度学习：DL权重更高（使用动态权重）
                var dlPrioritizedWeight = 0.7;
                var traditionalPrioritizedWeight = 0.3;
                var dlPrioritizedScore = (dlResult.IsOk ? dlResult.Confidence : 1 - dlResult.Confidence) * dlPrioritizedWeight +
                                         (traditionalResult.IsOk ? traditionalResult.Confidence : 1 - traditionalResult.Confidence) * traditionalPrioritizedWeight;
                var dlPrioritizedOk = dlPrioritizedScore >= confidenceThreshold;
                var dlPrioritizedConfidence = dlPrioritizedScore;
                var dlPrioritizedDetails = $"优先DL策略: 加权分数={dlPrioritizedScore:F4}(阈值{confidenceThreshold}), DL权重={dlPrioritizedWeight}, 传统权重={traditionalPrioritizedWeight}, 结果={dlPrioritizedOk}";
                return (dlPrioritizedOk, dlPrioritizedConfidence, dlPrioritizedDetails);

            case "prioritizetraditional":
                // 优先传统算法：传统算法权重更高（使用动态权重）
                var traditionalPrioritizedWeight2 = 0.7;
                var dlPrioritizedWeight2 = 0.3;
                var traditionalPrioritizedScore = (dlResult.IsOk ? dlResult.Confidence : 1 - dlResult.Confidence) * dlPrioritizedWeight2 +
                                                  (traditionalResult.IsOk ? traditionalResult.Confidence : 1 - traditionalResult.Confidence) * traditionalPrioritizedWeight2;
                var traditionalPrioritizedOk = traditionalPrioritizedScore >= confidenceThreshold;
                var traditionalPrioritizedConfidence = traditionalPrioritizedScore;
                var traditionalPrioritizedDetails = $"优先传统策略: 加权分数={traditionalPrioritizedScore:F4}(阈值{confidenceThreshold}), DL权重={dlPrioritizedWeight2}, 传统权重={traditionalPrioritizedWeight2}, 结果={traditionalPrioritizedOk}";
                return (traditionalPrioritizedOk, traditionalPrioritizedConfidence, traditionalPrioritizedDetails);

            default:
                // 默认使用加权平均
                var defaultScore = (dlResult.IsOk ? dlResult.Confidence : 1 - dlResult.Confidence) * dlWeight +
                                   (traditionalResult.IsOk ? traditionalResult.Confidence : 1 - traditionalResult.Confidence) * traditionalWeight;
                var defaultOk = defaultScore >= confidenceThreshold;
                var defaultConfidence = defaultScore;
                var defaultDetails = $"默认(加权平均)策略: 加权分数={defaultScore:F4}(阈值{confidenceThreshold}), 结果={defaultOk}";
                return (defaultOk, defaultConfidence, defaultDetails);
        }
    }

    private OperatorExecutionOutput CreateNgOutput(string details, string ngOutputValue)
    {
        var outputData = new Dictionary<string, object>
        {
            { "JudgmentResult", "NG" },
            { "JudgmentValue", ngOutputValue },
            { "Confidence", 0.0 },
            { "Details", details },
            { "IsOk", false }
        };

        if (int.TryParse(ngOutputValue, out var intValue))
        {
            outputData["JudgmentValueInt"] = intValue;
        }

        Logger.LogWarning("[DualModalVoting] 投票失败: {Details}", details);

        return OperatorExecutionOutput.Success(outputData);
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var strategy = GetStringParam(@operator, "VotingStrategy", "WeightedAverage");

        var validStrategies = new[]
        {
            "Unanimous", "Majority", "WeightedAverage", 
            "PrioritizeDeepLearning", "PrioritizeTraditional"
        };

        if (!validStrategies.Contains(strategy, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid($"投票策略必须是以下之一: {string.Join(", ", validStrategies)}");
        }

        var dlWeight = GetDoubleParam(@operator, "DLWeight", 0.6, 0.0, 1.0);
        var traditionalWeight = GetDoubleParam(@operator, "TraditionalWeight", 0.4, 0.0, 1.0);

        // 检查权重和是否合理（允许一定误差）
        var weightSum = dlWeight + traditionalWeight;
        if (Math.Abs(weightSum - 1.0) > 0.01 && strategy.Equals("WeightedAverage", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid($"加权平均策略下，DL权重({dlWeight})与传统算法权重({traditionalWeight})之和应接近1.0(当前={weightSum:F2})");
        }

        return ValidationResult.Valid();
    }
}
