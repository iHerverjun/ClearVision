// ResultOutputOperator.cs
// 结果输出算子执行器
// 作者：蘅芜君
using System.Globalization;
using System.Text;
using System.Text.Json;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 透传并汇总结果，支持生成文本化输出并按需落盘。
/// </summary>
[OperatorMeta(
    DisplayName = "结果输出",
    Description = "汇总检测结果并输出，支持 JSON/CSV/Text 格式，可选保存到文件",
    Category = "输出",
    IconName = "output",
    Keywords = new[] { "输出", "结果", "结束", "呈现", "记录", "Output", "Result", "Display" },
    Version = "1.0.1"
)]
[InputPort("Image", "图像", PortDataType.Image, IsRequired = false)]
[InputPort("Result", "结果", PortDataType.Any, IsRequired = false)]
[InputPort("Text", "文本", PortDataType.String, IsRequired = false)]
[InputPort("Data", "数据", PortDataType.Any, IsRequired = false)]
[OutputPort("Output", "输出", PortDataType.Any)]
[OutputPort("Image", "图像", PortDataType.Image)]
[OutputPort("Result", "结果", PortDataType.Any)]
[OutputPort("Text", "文本", PortDataType.String)]
[OutputPort("Data", "数据", PortDataType.Any)]
[OutputPort("FilePath", "文件路径", PortDataType.String)]
[OperatorParam("Format", "输出格式", "enum", DefaultValue = "JSON", Options = new[] { "JSON|JSON", "CSV|CSV", "Text|Text" })]
[OperatorParam("SaveToFile", "保存到文件", "bool", DefaultValue = false)]
public class ResultOutputOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ResultOutput;

    public ResultOutputOperator(ILogger<ResultOutputOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var format = GetStringParam(@operator, "Format", "JSON");
        var saveToFile = GetBoolParam(@operator, "SaveToFile", false);

        var output = new Dictionary<string, object>();

        if (inputs?.TryGetValue("Image", out var image) == true)
            output["Image"] = PreserveOutputValue(image);

        if (inputs?.TryGetValue("Result", out var result) == true)
            output["Result"] = PreserveOutputValue(result);

        if (inputs?.TryGetValue("Text", out var text) == true)
            output["Text"] = text?.ToString() ?? string.Empty;

        if (inputs?.TryGetValue("Data", out var data) == true)
            output["Data"] = PreserveOutputValue(data);

        if (inputs != null)
        {
            foreach (var kvp in inputs)
            {
                if (!output.ContainsKey(kvp.Key))
                {
                    output[kvp.Key] = PreserveOutputValue(kvp.Value);
                }
            }
        }

        var formattedText = BuildFormattedOutput(output, format);
        if (!string.IsNullOrWhiteSpace(formattedText))
        {
            output["Output"] = formattedText;
            if (!output.ContainsKey("Text"))
            {
                output["Text"] = formattedText;
            }

            if (saveToFile)
            {
                try
                {
                    var filePath = SaveFormattedOutput(formattedText, format);
                    output["FilePath"] = filePath;
                }
                catch (Exception ex)
                {
                    // Result output should remain usable even if the local filesystem is unavailable
                    // (e.g. restricted CI environments or temp folder issues).
                    Logger.LogWarning(ex, "Failed to save formatted output to file. Format={Format}", format);
                    output["FilePath"] = string.Empty;
                    output["SaveError"] = ex.Message;
                }
            }
        }
        else if (output.TryGetValue("Result", out var resultValue))
        {
            output["Output"] = resultValue;
        }
        else if (output.TryGetValue("Data", out var dataValue))
        {
            output["Output"] = dataValue;
        }
        else if (output.TryGetValue("Text", out var textValue))
        {
            output["Output"] = textValue;
        }
        else if (output.TryGetValue("Image", out var imageValue))
        {
            output["Output"] = imageValue;
        }
        else
        {
            output["Output"] = string.Empty;
        }

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    private static object PreserveOutputValue(object value)
    {
        if (value is ImageWrapper wrapper)
            return wrapper.AddRef();
        return value;
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var format = GetStringParam(@operator, "Format", "JSON");
        var validFormats = new[] { "JSON", "CSV", "Text" };
        return validFormats.Contains(format, StringComparer.OrdinalIgnoreCase)
            ? ValidationResult.Valid()
            : ValidationResult.Invalid("Format must be JSON, CSV or Text.");
    }

    private static string BuildFormattedOutput(Dictionary<string, object> output, string format)
    {
        if (output.TryGetValue("Text", out var text) && text is string textValue && !string.IsNullOrWhiteSpace(textValue))
        {
            if (format.Equals("Text", StringComparison.OrdinalIgnoreCase))
            {
                return textValue;
            }
        }

        var exportPayload = new Dictionary<string, object>();
        foreach (var (key, value) in output)
        {
            if (key.Equals("Image", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Output", StringComparison.OrdinalIgnoreCase)
                || key.Equals("FilePath", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            exportPayload[key] = NormalizeForExport(value);
        }

        if (exportPayload.Count == 0)
        {
            return string.Empty;
        }

        if (format.Equals("CSV", StringComparison.OrdinalIgnoreCase))
        {
            return BuildCsv(exportPayload);
        }

        if (format.Equals("Text", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join(Environment.NewLine, exportPayload.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
        }

        return JsonSerializer.Serialize(exportPayload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static object? NormalizeForExport(object? value)
    {
        return value switch
        {
            null => null,
            ImageWrapper wrapper => new { wrapper.Width, wrapper.Height, wrapper.Channels },
            DetectionList detectionList => detectionList.Detections.Select(NormalizeForExport).ToList(),
            DetectionResult detection => new
            {
                detection.Label,
                detection.Confidence,
                detection.X,
                detection.Y,
                detection.Width,
                detection.Height,
                detection.CenterX,
                detection.CenterY,
                detection.Area
            },
            Position position => new { position.X, position.Y },
            IEnumerable<KeyValuePair<string, object>> dict => dict.ToDictionary(kvp => kvp.Key, kvp => NormalizeForExport(kvp.Value)),
            IEnumerable<object> list => list.Select(NormalizeForExport).ToList(),
            _ => value
        };
    }

    private static string BuildCsv(Dictionary<string, object> exportPayload)
    {
        var lines = new List<string> { "Key,Value" };
        foreach (var (key, value) in exportPayload)
        {
            var escapedKey = EscapeCsv(key);
            var escapedValue = EscapeCsv(value?.ToString() ?? string.Empty);
            lines.Add($"{escapedKey},{escapedValue}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string EscapeCsv(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@')
        {
            value = "'" + value;
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private static string SaveFormattedOutput(string formattedText, string format)
    {
        var extension = format.ToUpperInvariant() switch
        {
            "CSV" => ".csv",
            "TEXT" => ".txt",
            _ => ".json"
        };

        var directory = Path.Combine(Path.GetTempPath(), "Acme.Product", "result-output", DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, $"result_{DateTime.UtcNow:HHmmssfff}_{Guid.NewGuid():N}{extension}");
        File.WriteAllText(filePath, formattedText, Encoding.UTF8);
        return filePath;
    }
}
