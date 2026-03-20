using System.Collections;
using Acme.Product.Application.DTOs;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;

namespace Acme.Product.Application.Analysis;

internal static class AnalysisMapperHelpers
{
    public static bool TryGetValueIgnoreCase(
        IReadOnlyDictionary<string, object>? outputData,
        string key,
        out object? value)
    {
        value = null;
        if (outputData == null)
        {
            return false;
        }

        foreach (var pair in outputData)
        {
            if (pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        return false;
    }

    public static string ResolveStatus(OperatorExecutionResult result)
    {
        return result.IsSuccess ? "OK" : "Error";
    }

    public static double? TryReadDouble(IReadOnlyDictionary<string, object>? outputData, string key)
    {
        if (!TryGetValueIgnoreCase(outputData, key, out var value) || value == null)
        {
            return null;
        }

        return value switch
        {
            byte number => number,
            short number => number,
            int number => number,
            long number => number,
            float number => number,
            double number => number,
            decimal number => (double)number,
            _ when double.TryParse(value.ToString(), out var parsed) => parsed,
            _ => null
        };
    }

    public static string? TryReadString(IReadOnlyDictionary<string, object>? outputData, string key)
    {
        if (!TryGetValueIgnoreCase(outputData, key, out var value) || value == null)
        {
            return null;
        }

        return value.ToString();
    }

    public static object? TryReadObject(IReadOnlyDictionary<string, object>? outputData, string key)
    {
        return TryGetValueIgnoreCase(outputData, key, out var value) ? value : null;
    }

    public static Dictionary<string, object?> BuildMeta(params (string Key, object? Value)[] values)
    {
        return values
            .Where(item => item.Value != null)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
    }
}

public class OcrRecognitionAnalysisCardMapper : IAnalysisCardMapper
{
    public bool CanMap(OperatorType operatorType) => operatorType == OperatorType.OcrRecognition;

    public IEnumerable<AnalysisCardDto> Map(Operator @operator, OperatorExecutionResult result)
    {
        var text = AnalysisMapperHelpers.TryReadString(result.OutputData, "Text");
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var confidence = AnalysisMapperHelpers.TryReadObject(result.OutputData, "Confidence");
        yield return new AnalysisCardDto
        {
            Id = $"{@operator.Id:N}-recognition",
            Category = "recognition",
            SourceOperatorId = @operator.Id,
            SourceOperatorType = @operator.Type.ToString(),
            Title = "OCR 文本识别",
            Status = AnalysisMapperHelpers.ResolveStatus(result),
            Priority = 90,
            Fields =
            [
                new AnalysisFieldDto
                {
                    Key = "text",
                    Label = "识别文本",
                    Value = text,
                    DisplayHint = "code-text"
                }
            ],
            Meta = AnalysisMapperHelpers.BuildMeta(("confidence", confidence))
        };
    }
}

public class CodeRecognitionAnalysisCardMapper : IAnalysisCardMapper
{
    public bool CanMap(OperatorType operatorType) => operatorType == OperatorType.CodeRecognition;

    public IEnumerable<AnalysisCardDto> Map(Operator @operator, OperatorExecutionResult result)
    {
        var text = AnalysisMapperHelpers.TryReadString(result.OutputData, "Text");
        var codeType = AnalysisMapperHelpers.TryReadString(result.OutputData, "CodeType");
        var codeCount = AnalysisMapperHelpers.TryReadDouble(result.OutputData, "CodeCount")
            ?? AnalysisMapperHelpers.TryReadDouble(result.OutputData, "ResultCount");

        if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(codeType) && codeCount is null)
        {
            yield break;
        }

        var fields = new List<AnalysisFieldDto>();
        if (!string.IsNullOrWhiteSpace(text))
        {
            fields.Add(new AnalysisFieldDto
            {
                Key = "text",
                Label = "识别内容",
                Value = text,
                DisplayHint = "code-text"
            });
        }

        if (!string.IsNullOrWhiteSpace(codeType))
        {
            fields.Add(new AnalysisFieldDto
            {
                Key = "codeType",
                Label = "码制类型",
                Value = codeType,
                DisplayHint = "tag"
            });
        }

        if (codeCount is not null)
        {
            fields.Add(new AnalysisFieldDto
            {
                Key = "codeCount",
                Label = "识别数量",
                Value = codeCount
            });
        }

        yield return new AnalysisCardDto
        {
            Id = $"{@operator.Id:N}-recognition",
            Category = "recognition",
            SourceOperatorId = @operator.Id,
            SourceOperatorType = @operator.Type.ToString(),
            Title = "条码识别",
            Status = AnalysisMapperHelpers.ResolveStatus(result),
            Priority = 95,
            Fields = fields,
            Meta = AnalysisMapperHelpers.BuildMeta(("codes", AnalysisMapperHelpers.TryReadObject(result.OutputData, "Codes")))
        };
    }
}

public class WidthMeasurementAnalysisCardMapper : IAnalysisCardMapper
{
    public bool CanMap(OperatorType operatorType) => operatorType == OperatorType.WidthMeasurement;

    public IEnumerable<AnalysisCardDto> Map(Operator @operator, OperatorExecutionResult result)
    {
        var width = AnalysisMapperHelpers.TryReadDouble(result.OutputData, "Width");
        if (width is null)
        {
            yield break;
        }

        var fields = new List<AnalysisFieldDto>
        {
            new()
            {
                Key = "width",
                Label = "宽度",
                Value = width,
                Unit = "px",
                DisplayHint = "big-number"
            }
        };

        var minWidth = AnalysisMapperHelpers.TryReadDouble(result.OutputData, "MinWidth");
        if (minWidth is not null)
        {
            fields.Add(new AnalysisFieldDto
            {
                Key = "minWidth",
                Label = "最小宽度",
                Value = minWidth,
                Unit = "px"
            });
        }

        var maxWidth = AnalysisMapperHelpers.TryReadDouble(result.OutputData, "MaxWidth");
        if (maxWidth is not null)
        {
            fields.Add(new AnalysisFieldDto
            {
                Key = "maxWidth",
                Label = "最大宽度",
                Value = maxWidth,
                Unit = "px"
            });
        }

        yield return new AnalysisCardDto
        {
            Id = $"{@operator.Id:N}-measurement",
            Category = "measurement",
            SourceOperatorId = @operator.Id,
            SourceOperatorType = @operator.Type.ToString(),
            Title = "宽度测量",
            Status = AnalysisMapperHelpers.ResolveStatus(result),
            Priority = 100,
            Fields = fields,
            Meta = AnalysisMapperHelpers.BuildMeta(
                ("sampleCount", AnalysisMapperHelpers.TryReadObject(result.OutputData, "SampleCount")),
                ("refinedSampleCount", AnalysisMapperHelpers.TryReadObject(result.OutputData, "RefinedSampleCount")),
                ("direction", AnalysisMapperHelpers.TryReadObject(result.OutputData, "Direction")))
        };
    }
}
