using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Text Save",
    Description = "Saves text or structured data to text/csv/json file.",
    Category = "Logic Tools",
    IconName = "save-text",
    Keywords = new[] { "save text", "export csv", "log", "json export" }
)]
[InputPort("Data", "Data", PortDataType.Any, IsRequired = false)]
[InputPort("Text", "Text", PortDataType.String, IsRequired = false)]
[OutputPort("FilePath", "File Path", PortDataType.String)]
[OutputPort("Success", "Success", PortDataType.Boolean)]
[OperatorParam("FilePath", "File Path", "file", DefaultValue = "output_{date}_{time}.txt")]
[OperatorParam("Format", "Format", "enum", DefaultValue = "Text", Options = new[] { "Text|Text", "CSV|CSV", "JSON|JSON" })]
[OperatorParam("AppendMode", "Append Mode", "bool", DefaultValue = true)]
[OperatorParam("AddTimestamp", "Add Timestamp", "bool", DefaultValue = true)]
[OperatorParam("Encoding", "Encoding", "enum", DefaultValue = "UTF8", Options = new[] { "UTF8|UTF8", "GBK|GBK" })]
public class TextSaveOperator : OperatorBase
{
    private static readonly ConcurrentDictionary<string, object> FileLocks = new(StringComparer.OrdinalIgnoreCase);

    public override OperatorType OperatorType => OperatorType.TextSave;

    public TextSaveOperator(ILogger<TextSaveOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var filePathTemplate = GetStringParam(@operator, "FilePath", "output_{date}_{time}.txt");
        var format = GetStringParam(@operator, "Format", "Text");
        var appendMode = GetBoolParam(@operator, "AppendMode", true);
        var addTimestamp = GetBoolParam(@operator, "AddTimestamp", true);
        var encodingName = GetStringParam(@operator, "Encoding", "UTF8");

        if (string.IsNullOrWhiteSpace(filePathTemplate))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("FilePath is required."));
        }

        try
        {
            var filePath = ResolvePath(filePathTemplate);
            var content = BuildContent(format, inputs);
            if (addTimestamp)
            {
                content = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {content}";
            }

            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var encoding = ResolveEncoding(encodingName);
            WriteContentThreadSafe(filePath, content + Environment.NewLine, appendMode, encoding);

            return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
            {
                { "FilePath", filePath },
                { "Success", true }
            }));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save text.");
            return Task.FromResult(OperatorExecutionOutput.Failure($"Failed to save text: {ex.Message}"));
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var filePath = GetStringParam(@operator, "FilePath", string.Empty);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return ValidationResult.Invalid("FilePath cannot be empty.");
        }

        var format = GetStringParam(@operator, "Format", "Text");
        var validFormats = new[] { "Text", "CSV", "JSON" };
        if (!validFormats.Contains(format, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Format must be Text, CSV or JSON.");
        }

        var encoding = GetStringParam(@operator, "Encoding", "UTF8");
        var validEncodings = new[] { "UTF8", "GBK" };
        if (!validEncodings.Contains(encoding, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Encoding must be UTF8 or GBK.");
        }

        return ValidationResult.Valid();
    }

    private static string ResolvePath(string template)
    {
        var resolved = template
            .Replace("{date}", DateTime.Now.ToString("yyyyMMdd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{time}", DateTime.Now.ToString("HHmmss"), StringComparison.OrdinalIgnoreCase);
        return Path.GetFullPath(resolved);
    }

    private static string BuildContent(string format, Dictionary<string, object>? inputs)
    {
        inputs ??= new Dictionary<string, object>();
        inputs.TryGetValue("Text", out var textObj);
        inputs.TryGetValue("Data", out var dataObj);

        var text = textObj?.ToString() ?? string.Empty;
        var data = dataObj ?? textObj;

        switch (format.ToLowerInvariant())
        {
            case "json":
                return JsonSerializer.Serialize(data ?? new { Text = text }, new JsonSerializerOptions { WriteIndented = true });

            case "csv":
                if (data is IEnumerable<object> enumerable && data is not string)
                {
                    return string.Join(",", enumerable.Select(ToCsvCell));
                }

                if (data is System.Collections.IEnumerable legacyEnumerable && data is not string)
                {
                    return string.Join(",", legacyEnumerable.Cast<object?>().Select(ToCsvCell));
                }

                return ToCsvCell(data ?? text);

            default:
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }

                return data?.ToString() ?? string.Empty;
        }
    }

    private static string ToCsvCell(object? value)
    {
        var text = value?.ToString() ?? string.Empty;
        if (text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r'))
        {
            return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return text;
    }

    private static Encoding ResolveEncoding(string encodingName)
    {
        if (encodingName.Equals("GBK", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return Encoding.GetEncoding(936);
            }
            catch
            {
                return Encoding.UTF8;
            }
        }

        return Encoding.UTF8;
    }

    private static void WriteContentThreadSafe(string filePath, string content, bool appendMode, Encoding encoding)
    {
        var fileLock = FileLocks.GetOrAdd(filePath, _ => new object());
        lock (fileLock)
        {
            if (appendMode)
            {
                File.AppendAllText(filePath, content, encoding);
            }
            else
            {
                File.WriteAllText(filePath, content, encoding);
            }
        }
    }
}
