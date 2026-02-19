// ArrayIndexerOperator.cs
// 数组索引器算子 - Sprint 2 Task 2.2
// 从 DetectionList 按索引或条件提取单个 DetectionResult
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 数组索引器算子 - 从列表/数组中提取单个元素
/// 
/// 功能：
/// 1. 按索引提取（Index 模式）
/// 2. 按最大置信度提取（MaxConfidence 模式）
/// 3. 按最小面积提取（MinArea 模式）
/// 4. 按最大面积提取（MaxArea 模式）
/// 
/// 使用场景：
/// - 从检测结果中提取最佳匹配项
/// - 获取最大/最小缺陷
/// </summary>
public class ArrayIndexerOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ArrayIndexer;

    public ArrayIndexerOperator(ILogger<ArrayIndexerOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("ArrayIndexer 算子需要输入数据"));
        }

        // 获取输入列表
        if (!inputs.TryGetValue("Items", out var itemsObj) || itemsObj == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供 Items 输入"));
        }

        // 解析输入列表
        var items = ParseItems(itemsObj);
        if (items == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"Items 必须是可枚举类型，实际类型: {itemsObj.GetType().Name}"));
        }

        if (items.Count == 0)
        {
            return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
            {
                { "Result", null },
                { "Found", false },
                { "Index", -1 }
            }));
        }

        // 获取参数
        var mode = GetStringParam(@operator, "Mode", "Index");
        int index = GetIntParam(@operator, "Index", 0);
        var labelFilter = GetStringParam(@operator, "LabelFilter", "");

        // 如果有标签过滤，先筛选
        if (!string.IsNullOrEmpty(labelFilter))
        {
            items = items.Where(i => MatchesLabel(i, labelFilter)).ToList();
            if (items.Count == 0)
            {
                return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
                {
                    { "Result", null },
                    { "Found", false },
                    { "Index", -1 },
                    { "Message", $"未找到标签为 '{labelFilter}' 的项" }
                }));
            }
        }

        // 根据模式提取
        object? result = null;
        int resultIndex = -1;
        string message = "";

        switch (mode.ToLower())
        {
            case "index":
                if (index < 0 || index >= items.Count)
                {
                    return Task.FromResult(OperatorExecutionOutput.Failure($"索引越界: {index}，有效范围: 0-{items.Count - 1}"));
                }
                result = items[index];
                resultIndex = index;
                break;

            case "maxconfidence":
                var maxConfResult = items
                    .Select((item, idx) => (item, idx, confidence: GetConfidence(item)))
                    .OrderByDescending(x => x.confidence)
                    .First();
                result = maxConfResult.item;
                resultIndex = maxConfResult.idx;
                message = $"最大置信度: {maxConfResult.confidence:F4}";
                break;

            case "minarea":
                var minAreaResult = items
                    .Select((item, idx) => (item, idx, area: GetArea(item)))
                    .OrderBy(x => x.area)
                    .First();
                result = minAreaResult.item;
                resultIndex = minAreaResult.idx;
                message = $"最小面积: {minAreaResult.area:F2}";
                break;

            case "maxarea":
                var maxAreaResult = items
                    .Select((item, idx) => (item, idx, area: GetArea(item)))
                    .OrderByDescending(x => x.area)
                    .First();
                result = maxAreaResult.item;
                resultIndex = maxAreaResult.idx;
                message = $"最大面积: {maxAreaResult.area:F2}";
                break;

            case "first":
                result = items.First();
                resultIndex = 0;
                break;

            case "last":
                result = items.Last();
                resultIndex = items.Count - 1;
                break;

            default:
                return Task.FromResult(OperatorExecutionOutput.Failure($"不支持的模式: {mode}"));
        }

        Logger.LogDebug("[ArrayIndexer] 模式={Mode}, 结果索引={Index}, 总数={Count}", mode, resultIndex, items.Count);

        return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            { "Result", result },
            { "Found", result != null },
            { "Index", resultIndex },
            { "TotalCount", items.Count },
            { "Message", message }
        }));
    }

    /// <summary>
    /// 解析输入为列表
    /// </summary>
    private List<object>? ParseItems(object itemsObj)
    {
        if (itemsObj is DetectionList detectionList)
        {
            return detectionList.Detections.Cast<object>().ToList();
        }
        if (itemsObj is System.Collections.IEnumerable enumerable && itemsObj is not string)
        {
            return enumerable.Cast<object>().ToList();
        }
        return null;
    }

    /// <summary>
    /// 检查标签是否匹配
    /// </summary>
    private static bool MatchesLabel(object item, string label)
    {
        if (item is DetectionResult dr)
        {
            return dr.Label.Equals(label, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    /// <summary>
    /// 获取置信度
    /// </summary>
    private static float GetConfidence(object item)
    {
        if (item is DetectionResult dr)
        {
            return dr.Confidence;
        }
        return 0;
    }

    /// <summary>
    /// 获取面积
    /// </summary>
    private static float GetArea(object item)
    {
        if (item is DetectionResult dr)
        {
            return dr.Area;
        }
        return 0;
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
}
