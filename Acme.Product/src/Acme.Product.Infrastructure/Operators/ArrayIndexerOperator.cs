using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "数组索引器",
    Description = "从列表中按索引或条件提取元素",
    Category = "数据处理",
    IconName = "index"
)]
[InputPort("List", "列表", PortDataType.Any, IsRequired = true)]
[OutputPort("Item", "元素", PortDataType.Any)]
[OutputPort("Found", "是否找到", PortDataType.Boolean)]
[OutputPort("Index", "原始索引", PortDataType.Integer)]
[OperatorParam("Mode", "提取模式", "enum", DefaultValue = "Index", Options = new[] { "Index|按索引", "MaxConfidence|最大置信度", "MaxArea|最大面积", "MinArea|最小面积", "First|第一个", "Last|最后一个" })]
[OperatorParam("Index", "索引", "int", DefaultValue = 0)]
[OperatorParam("LabelFilter", "标签过滤", "string", DefaultValue = "")]
public class ArrayIndexerOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ArrayIndexer;

    public ArrayIndexerOperator(ILogger<ArrayIndexerOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("ArrayIndexer 算子需要输入数据"));
        }

        if (!inputs.TryGetValue("List", out var itemsObj) || itemsObj == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供 List 输入"));
        }

        var candidates = ParseItems(itemsObj);
        if (candidates == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"List 必须是可枚举类型，实际类型: {itemsObj.GetType().Name}"));
        }

        if (candidates.Count == 0)
        {
            return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
            {
                { "Item", null! },
                { "Found", false },
                { "Index", -1 }
            }));
        }

        var mode = GetStringParam(@operator, "Mode", "Index");
        var index = GetIntParam(@operator, "Index", 0);
        var labelFilter = GetStringParam(@operator, "LabelFilter", string.Empty);
        if (!TryValidateCandidateMode(candidates, mode, labelFilter, out var candidateValidationError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(candidateValidationError));
        }

        if (!string.IsNullOrWhiteSpace(labelFilter))
        {
            candidates = candidates.Where(i => MatchesLabel(i.Item, labelFilter)).ToList();
            if (candidates.Count == 0)
            {
                return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
                {
                    { "Item", null! },
                    { "Found", false },
                    { "Index", -1 },
                    { "Message", $"未找到标签为 '{labelFilter}' 的项" }
                }));
            }
        }

        object? result = null;
        var resultIndex = -1;
        var message = string.Empty;

        switch (mode.ToLowerInvariant())
        {
            case "index":
                if (index < 0 || index >= candidates.Count)
                {
                    return Task.FromResult(OperatorExecutionOutput.Failure($"索引越界: {index}，有效范围: 0-{candidates.Count - 1}"));
                }

                result = candidates[index].Item;
                resultIndex = candidates[index].OriginalIndex;
                break;

            case "maxconfidence":
            {
                var (selected, metric) = SelectByMetric(candidates, GetConfidence, preferMax: true);
                result = selected.Item;
                resultIndex = selected.OriginalIndex;
                message = $"最大置信度: {metric:F4}";
                break;
            }

            case "minarea":
            {
                var (selected, metric) = SelectByMetric(candidates, GetArea, preferMax: false);
                result = selected.Item;
                resultIndex = selected.OriginalIndex;
                message = $"最小面积: {metric:F2}";
                break;
            }

            case "maxarea":
            {
                var (selected, metric) = SelectByMetric(candidates, GetArea, preferMax: true);
                result = selected.Item;
                resultIndex = selected.OriginalIndex;
                message = $"最大面积: {metric:F2}";
                break;
            }

            case "first":
                result = candidates[0].Item;
                resultIndex = candidates[0].OriginalIndex;
                break;

            case "last":
                result = candidates[^1].Item;
                resultIndex = candidates[^1].OriginalIndex;
                break;

            default:
                return Task.FromResult(OperatorExecutionOutput.Failure($"不支持的模式: {mode}"));
        }

        Logger.LogDebug("[ArrayIndexer] 模式={Mode}, 结果原始索引={Index}, 可选项数量={Count}", mode, resultIndex, candidates.Count);

        return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            { "Item", result! },
            { "Found", result != null },
            { "Index", resultIndex },
            { "TotalCount", candidates.Count },
            { "Message", message }
        }));
    }

    private static List<IndexedItem>? ParseItems(object itemsObj)
    {
        if (itemsObj is DetectionList detectionList)
        {
            return detectionList.Detections
                .Select((item, idx) => new IndexedItem(item, idx))
                .ToList();
        }

        if (itemsObj is System.Collections.IEnumerable enumerable && itemsObj is not string)
        {
            var result = new List<IndexedItem>();
            var idx = 0;
            foreach (var item in enumerable)
            {
                result.Add(new IndexedItem(item!, idx));
                idx++;
            }

            return result;
        }

        return null;
    }

    private static (IndexedItem selected, float metric) SelectByMetric(
        List<IndexedItem> candidates,
        Func<object, float> selector,
        bool preferMax)
    {
        var selected = candidates[0];
        var bestMetric = selector(selected.Item);

        for (var i = 1; i < candidates.Count; i++)
        {
            var current = candidates[i];
            var currentMetric = selector(current.Item);
            var isBetter = preferMax ? currentMetric > bestMetric : currentMetric < bestMetric;
            if (!isBetter)
            {
                continue;
            }

            selected = current;
            bestMetric = currentMetric;
        }

        return (selected, bestMetric);
    }

    private static bool MatchesLabel(object item, string label)
    {
        if (item is DetectionResult detection)
        {
            return detection.Label.Equals(label, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static float GetConfidence(object item)
    {
        if (item is DetectionResult detection)
        {
            return detection.Confidence;
        }

        return 0f;
    }

    private static float GetArea(object item)
    {
        if (item is DetectionResult detection)
        {
            return detection.Area;
        }

        return 0f;
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var mode = GetStringParam(@operator, "Mode", "Index");
        var index = GetIntParam(@operator, "Index", 0);

        var validModes = new[] { "Index", "MaxConfidence", "MinArea", "MaxArea", "First", "Last" };
        if (!validModes.Contains(mode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid($"Mode 必须是以下之一: {string.Join(", ", validModes)}");
        }

        if (mode.Equals("Index", StringComparison.OrdinalIgnoreCase) && index < 0)
        {
            return ValidationResult.Invalid("Index 模式下 Index 必须 >= 0");
        }

        return ValidationResult.Valid();
    }

    private readonly record struct IndexedItem(object Item, int OriginalIndex);

    private static bool TryValidateCandidateMode(
        List<IndexedItem> candidates,
        string mode,
        string labelFilter,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        var requiresDetectionResult =
            !string.IsNullOrWhiteSpace(labelFilter) ||
            mode.Equals("MaxConfidence", StringComparison.OrdinalIgnoreCase) ||
            mode.Equals("MaxArea", StringComparison.OrdinalIgnoreCase) ||
            mode.Equals("MinArea", StringComparison.OrdinalIgnoreCase);

        if (!requiresDetectionResult)
        {
            return true;
        }

        if (candidates.All(candidate => candidate.Item is DetectionResult))
        {
            return true;
        }

        errorMessage = $"Mode '{mode}' requires DetectionResult items.";
        return false;
    }
}
