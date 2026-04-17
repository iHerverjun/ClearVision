using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.Services;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Dual Modal Voting",
    Description = "Combines deep learning and traditional inspection results into a final judgment.",
    Category = "AI Detection",
    IconName = "voting"
)]
[InputPort("DLResult", "Deep learning result", PortDataType.Any, IsRequired = true)]
[InputPort("TraditionalResult", "Traditional result", PortDataType.Any, IsRequired = true)]
[OutputPort("IsOk", "Whether the final result is OK", PortDataType.Boolean)]
[OutputPort("Confidence", "Confidence of the final judgment", PortDataType.Float)]
[OutputPort("JudgmentValue", "Final judgment value", PortDataType.String)]
[OperatorParam("VotingStrategy", "Voting strategy", "enum", DefaultValue = "WeightedAverage", Options = new[] { "WeightedAverage|Weighted average", "Unanimous|Unanimous", "Majority|Majority", "PrioritizeDeepLearning|Prioritize deep learning", "PrioritizeTraditional|Prioritize traditional" })]
[OperatorParam("DLWeight", "Deep learning weight", "double", DefaultValue = 0.6, Min = 0.0, Max = 1.0)]
[OperatorParam("TraditionalWeight", "Traditional weight", "double", DefaultValue = 0.4, Min = 0.0, Max = 1.0)]
[OperatorParam("ConfidenceThreshold", "Confidence threshold", "double", DefaultValue = 0.5, Min = 0.0, Max = 1.0)]
[OperatorParam("OkOutputValue", "OK output value", "string", DefaultValue = "1")]
[OperatorParam("NgOutputValue", "NG output value", "string", DefaultValue = "0")]
public class DualModalVotingOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.DualModalVoting;

    public DualModalVotingOperator(ILogger<DualModalVotingOperator> logger)
        : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var strategy = GetStringParam(@operator, "VotingStrategy", "WeightedAverage");
        var dlWeight = GetDoubleParam(@operator, "DLWeight", 0.6);
        var traditionalWeight = GetDoubleParam(@operator, "TraditionalWeight", 0.4);
        var confidenceThreshold = GetDoubleParam(@operator, "ConfidenceThreshold", 0.5);
        var okValue = GetStringParam(@operator, "OkOutputValue", "1");
        var ngValue = GetStringParam(@operator, "NgOutputValue", "0");

        var dlResult = ExtractDetectionResult(inputs, "DLResult");
        var traditionalResult = ExtractDetectionResult(inputs, "TraditionalResult");

        if (dlResult == null && traditionalResult == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("No valid detection result input was received."));
        }

        dlResult ??= DetectionResult.Failed("Deep learning result was not received.");
        traditionalResult ??= DetectionResult.Failed("Traditional result was not received.");

        var dlOkProbability = ToOkProbability(dlResult);
        var traditionalOkProbability = ToOkProbability(traditionalResult);

        bool isOk;
        double confidence;
        string details;

        switch (strategy)
        {
            case "WeightedAverage":
            {
                var totalWeight = dlWeight + traditionalWeight;
                if (totalWeight <= 1e-12)
                {
                    return Task.FromResult(OperatorExecutionOutput.Failure("WeightedAverage requires DLWeight + TraditionalWeight > 0."));
                }

                var weightedOkProbability =
                    (dlOkProbability * dlWeight + traditionalOkProbability * traditionalWeight) / totalWeight;
                isOk = weightedOkProbability >= confidenceThreshold;
                confidence = ToOutputConfidence(isOk, weightedOkProbability);
                details =
                    $"WeightedAverage: DLOkProb={dlOkProbability:F2}*{dlWeight} + TraditionalOkProb={traditionalOkProbability:F2}*{traditionalWeight} -> OkProb={weightedOkProbability:F2}, DecisionConf={confidence:F2}";
                break;
            }

            case "Unanimous":
            {
                isOk = dlResult.IsOk && traditionalResult.IsOk;
                var unanimousOkProbability = Math.Min(dlOkProbability, traditionalOkProbability);
                confidence = ToOutputConfidence(isOk, unanimousOkProbability);
                details =
                    $"Unanimous: DL={dlResult.IsOk}/{GetLabelConfidence(dlResult):F2}, Traditional={traditionalResult.IsOk}/{GetLabelConfidence(traditionalResult):F2}, DecisionConf={confidence:F2}";
                break;
            }

            case "Majority":
            {
                if (dlResult.IsOk == traditionalResult.IsOk)
                {
                    isOk = dlResult.IsOk;
                    var majorityOkProbability = (dlOkProbability + traditionalOkProbability) / 2.0;
                    confidence = ToOutputConfidence(isOk, majorityOkProbability);
                }
                else if (GetLabelConfidence(dlResult) >= GetLabelConfidence(traditionalResult))
                {
                    isOk = dlResult.IsOk;
                    confidence = GetLabelConfidence(dlResult);
                }
                else
                {
                    isOk = traditionalResult.IsOk;
                    confidence = GetLabelConfidence(traditionalResult);
                }

                details = $"Majority: IsOk={isOk}, DecisionConf={confidence:F2}";
                break;
            }

            case "PrioritizeDeepLearning":
                isOk = dlResult.IsOk;
                confidence = GetLabelConfidence(dlResult);
                details = $"PrioritizeDeepLearning: IsOk={isOk}, DecisionConf={confidence:F2}";
                break;

            case "PrioritizeTraditional":
                isOk = traditionalResult.IsOk;
                confidence = GetLabelConfidence(traditionalResult);
                details = $"PrioritizeTraditional: IsOk={isOk}, DecisionConf={confidence:F2}";
                break;

            default:
                return Task.FromResult(OperatorExecutionOutput.Failure($"Unsupported voting strategy: {strategy}"));
        }

        var outputData = new Dictionary<string, object>
        {
            { "IsOk", isOk },
            { "Confidence", confidence },
            { "JudgmentValue", isOk ? okValue : ngValue }
        };

        Logger.LogInformation(
            "[DualModalVoting] Voting completed. Strategy: {Strategy}, IsOk: {IsOk}, Confidence: {Confidence:F2}, Details: {Details}",
            strategy,
            isOk,
            confidence,
            details);

        return Task.FromResult(OperatorExecutionOutput.Success(outputData));
    }

    private DetectionResult? ExtractDetectionResult(Dictionary<string, object>? inputs, string key)
    {
        if (inputs == null || !inputs.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is DetectionResult detectionResult)
        {
            return detectionResult;
        }

        if (value is not Dictionary<string, object> dict)
        {
            return null;
        }

        if (dict.TryGetValue("IsOk", out var okVal) && dict.TryGetValue("Confidence", out var confVal))
        {
            return DetectionResult.Success(
                Convert.ToBoolean(okVal),
                Math.Clamp(Convert.ToDouble(confVal), 0.0, 1.0));
        }

        if (!dict.TryGetValue("DefectCount", out var defectCountVal))
        {
            return null;
        }

        var defectCount = Convert.ToInt32(defectCountVal);
        var isOk = defectCount == 0;
        var maxDefectConfidence = 0.0;

        if (dict.TryGetValue("Defects", out var defectsVal) && defectsVal is IEnumerable<object> defectsList)
        {
            foreach (var defect in defectsList)
            {
                if (defect is Dictionary<string, object> defectDict &&
                    defectDict.TryGetValue("Confidence", out var defectConfidenceValue))
                {
                    maxDefectConfidence = Math.Max(maxDefectConfidence, Convert.ToDouble(defectConfidenceValue));
                }
            }
        }

        var labelConfidence = isOk ? 1.0 : Math.Clamp(maxDefectConfidence, 0.0, 1.0);
        return DetectionResult.Success(isOk, labelConfidence);
    }

    private static double ToOkProbability(DetectionResult result)
    {
        if (!result.IsSuccess)
        {
            return 0.5;
        }

        var labelConfidence = GetLabelConfidence(result);
        return result.IsOk ? labelConfidence : 1.0 - labelConfidence;
    }

    private static double GetLabelConfidence(DetectionResult result)
    {
        return result.IsSuccess
            ? Math.Clamp(result.Confidence, 0.0, 1.0)
            : 0.0;
    }

    private static double ToOutputConfidence(bool isOk, double okProbability)
    {
        var normalizedOkProbability = Math.Clamp(okProbability, 0.0, 1.0);
        return isOk ? normalizedOkProbability : 1.0 - normalizedOkProbability;
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var strategy = GetStringParam(@operator, "VotingStrategy", "WeightedAverage");
        var validStrategies = new[]
        {
            "Unanimous",
            "Majority",
            "WeightedAverage",
            "PrioritizeDeepLearning",
            "PrioritizeTraditional"
        };

        if (!validStrategies.Contains(strategy, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid($"VotingStrategy must be one of: {string.Join(", ", validStrategies)}");
        }

        var dlWeight = GetDoubleParam(@operator, "DLWeight", 0.6, 0.0, 1.0);
        var traditionalWeight = GetDoubleParam(@operator, "TraditionalWeight", 0.4, 0.0, 1.0);
        var weightSum = dlWeight + traditionalWeight;

        if (strategy.Equals("WeightedAverage", StringComparison.OrdinalIgnoreCase) && weightSum <= 1e-12)
        {
            return ValidationResult.Invalid("WeightedAverage requires DLWeight + TraditionalWeight > 0.");
        }

        if (strategy.Equals("WeightedAverage", StringComparison.OrdinalIgnoreCase) && Math.Abs(weightSum - 1.0) > 0.01)
        {
            return ValidationResult.Invalid(
                $"In WeightedAverage mode, DLWeight ({dlWeight}) + TraditionalWeight ({traditionalWeight}) must be approximately 1.0 (current={weightSum:F2}).");
        }

        return ValidationResult.Valid();
    }
}
