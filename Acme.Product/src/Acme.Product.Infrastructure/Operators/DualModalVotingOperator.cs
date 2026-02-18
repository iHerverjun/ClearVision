// DualModalVotingOperator.cs
// 双模态投票算子 - 结合深度学习和传统算法结果进行投票决策
// 作者：蘅芜君

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
        // 1. 获取参数
        var strategy = GetStringParam(@operator, "VotingStrategy", "WeightedAverage");
        var dlWeight = GetDoubleParam(@operator, "DLWeight", 0.6);
        var traditionalWeight = GetDoubleParam(@operator, "TraditionalWeight", 0.4);
        var confidenceThreshold = GetDoubleParam(@operator, "ConfidenceThreshold", 0.5);
        var okValue = GetStringParam(@operator, "OkOutputValue", "1");
        var ngValue = GetStringParam(@operator, "NgOutputValue", "0");

        // 2. 获取输入 (智能解析)
        var dlResult = ExtractDetectionResult(inputs, "DLResult");
        var traditionalResult = ExtractDetectionResult(inputs, "TraditionalResult");

        // 3. 执行投票逻辑
        bool isOk = false;
        double confidence = 0.0;
        string details = "";

        // 如果两个输入都缺失，则无法判定
        if (dlResult == null && traditionalResult == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未接收到任何有效的检测结果输入"));
        }

        // 补全缺失的输入 (视为 NG且置信度为0)
        dlResult ??= DetectionResult.Failed("未接收到深度学习结果");
        traditionalResult ??= DetectionResult.Failed("未接收到传统算法结果");

        switch (strategy)
        {
            case "WeightedAverage":
                confidence = (dlResult.Confidence * dlWeight + traditionalResult.Confidence * traditionalWeight) / (dlWeight + traditionalWeight);
                isOk = confidence >= confidenceThreshold;
                details = $"加权平均: DL={dlResult.Confidence:F2}*{dlWeight} + Traditional={traditionalResult.Confidence:F2}*{traditionalWeight} -> {confidence:F2}";
                break;

            case "Unanimous":
                isOk = dlResult.IsOk && traditionalResult.IsOk;
                confidence = Math.Min(dlResult.Confidence, traditionalResult.Confidence);
                details = $"一致同意: DL={dlResult.IsOk}, Traditional={traditionalResult.IsOk}";
                break;

            case "Majority":
                // 仅两个输入时，多数表决等同于一致同意或加权（需3个以上才有意义），这里简化为任一OK即OK，或者加权
                // 为了避免歧义，这里实现为：如果两个结果一致则采用，不一致则看置信度高的
                if (dlResult.IsOk == traditionalResult.IsOk)
                {
                    isOk = dlResult.IsOk;
                    confidence = Math.Max(dlResult.Confidence, traditionalResult.Confidence);
                }
                else
                {
                    // 冲突时信赖置信度高的
                    if (dlResult.Confidence > traditionalResult.Confidence)
                    {
                        isOk = dlResult.IsOk;
                        confidence = dlResult.Confidence;
                    }
                    else
                    {
                        isOk = traditionalResult.IsOk;
                        confidence = traditionalResult.Confidence;
                    }
                }
                details = $"多数/冲突处理: IsOk={isOk}, Conf={confidence:F2}";
                break;

            case "PrioritizeDeepLearning":
                isOk = dlResult.IsOk;
                confidence = dlResult.Confidence;
                details = $"优先DL: IsOk={isOk}, Conf={confidence:F2}";
                break;

            case "PrioritizeTraditional":
                isOk = traditionalResult.IsOk;
                confidence = traditionalResult.Confidence;
                details = $"优先传统: IsOk={isOk}, Conf={confidence:F2}";
                break;
        }

        var outputValue = isOk ? okValue : ngValue;

        // 4. 构建输出
        var outputData = new Dictionary<string, object>
        {
            { "IsOk", isOk },
            { "Confidence", confidence },
            { "JudgmentValue", outputValue }
        };

        Logger.LogInformation("[DualModalVoting] 投票完成. 策略: {Strategy}, 结果: {IsOk}, 置信度: {Confidence:F2}, 详情: {Details}",
            strategy, isOk, confidence, details);

        return Task.FromResult(OperatorExecutionOutput.Success(outputData));
    }

    /// <summary>
    /// 从上游输入中智能提取检测结果
    /// 支持 DetectionResult 对象 和 Dictionary<string, object> 两种格式
    /// </summary>
    private DetectionResult? ExtractDetectionResult(Dictionary<string, object>? inputs, string key)
    {
        if (inputs == null || !inputs.TryGetValue(key, out var value) || value == null)
            return null;

        // 情况1：直接是 DetectionResult 对象
        if (value is DetectionResult dr)
            return dr;

        // 情况2：是 Dictionary（上游算子的原始输出）
        if (value is Dictionary<string, object> dict)
        {
            // 尝试直接获取 IsOk 和 Confidence
            if (dict.TryGetValue("IsOk", out var okVal) && dict.TryGetValue("Confidence", out var confVal))
            {
                bool ok = Convert.ToBoolean(okVal);
                double conf = Convert.ToDouble(confVal);
                return DetectionResult.Success(ok, conf);
            }

            // 特殊情况：深度学习算子输出 (DefectCount, Defects)
            // 推断逻辑：DefectCount == 0 => OK (IsOk=true), DefectCount > 0 => NG (IsOk=false)
            if (dict.TryGetValue("DefectCount", out var defectCountVal))
            {
                int defectCount = Convert.ToInt32(defectCountVal);
                bool isOk = defectCount == 0;
                double maxConf = 0.0;

                // 尝试从 Defects 列表中提取最大置信度
                if (dict.TryGetValue("Defects", out var defectsVal) && defectsVal is IEnumerable<object> defectsList)
                {
                    foreach (var defect in defectsList)
                    {
                        if (defect is Dictionary<string, object> defectDict &&
                            defectDict.TryGetValue("Confidence", out var dConfVal))
                        {
                            double dConf = Convert.ToDouble(dConfVal);
                            if (dConf > maxConf)
                                maxConf = dConf;
                        }
                    }
                }

                // 如果是 OK (无缺陷)，置信度通常设为 1.0 或由模型提供的背景置信度(这里简化为1.0)
                // 如果是 NG (有缺陷)，置信度为缺陷的最大置信度
                double finalConf = isOk ? 1.0 : maxConf;

                return DetectionResult.Success(isOk, finalConf);
            }
        }

        return null;
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
