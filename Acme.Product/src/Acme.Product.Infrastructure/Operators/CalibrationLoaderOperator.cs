using System.Text.Json;
using System.Xml.Linq;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

public class CalibrationLoaderOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.CalibrationLoader;

    public CalibrationLoaderOperator(ILogger<CalibrationLoaderOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var filePath = GetStringParam(@operator, "FilePath", string.Empty);
        var fileFormat = GetStringParam(@operator, "FileFormat", "JSON");

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("FilePath is required"));
        }

        if (!File.Exists(filePath))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"Calibration file not found: {filePath}"));
        }

        try
        {
            var format = fileFormat.Trim().ToLowerInvariant();

            var output = format switch
            {
                "json" => LoadFromJson(filePath),
                "xml" => LoadFromXml(filePath),
                "yaml" or "yml" => LoadFromYaml(filePath),
                _ => LoadFromJson(filePath)
            };

            return Task.FromResult(OperatorExecutionOutput.Success(output));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load calibration file: {FilePath}", filePath);
            return Task.FromResult(OperatorExecutionOutput.Failure($"Failed to load calibration file: {ex.Message}"));
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var filePath = GetStringParam(@operator, "FilePath", string.Empty);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return ValidationResult.Invalid("FilePath is required");
        }

        var fileFormat = GetStringParam(@operator, "FileFormat", "JSON");
        var validFormats = new[] { "JSON", "XML", "YAML" };
        if (!validFormats.Contains(fileFormat, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("FileFormat must be JSON/XML/YAML");
        }

        return ValidationResult.Valid();
    }

    private static Dictionary<string, object> LoadFromJson(string filePath)
    {
        var text = File.ReadAllText(filePath);
        using var document = JsonDocument.Parse(text);

        var root = document.RootElement;

        var transformMatrix = FindJsonValue(root,
            "TransformMatrix", "transformMatrix", "Matrix", "matrix", "Homography", "homography");

        var cameraMatrix = FindJsonValue(root,
            "CameraMatrix", "cameraMatrix");

        var distCoeffs = FindJsonValue(root,
            "DistCoeffs", "distCoeffs", "DistortionCoefficients", "distortionCoefficients");

        var pixelSize = FindDouble(root,
            "PixelSize", "pixelSize", "ScaleX", "scaleX");

        return new Dictionary<string, object>
        {
            { "TransformMatrix", transformMatrix ?? Array.Empty<object>() },
            { "CameraMatrix", cameraMatrix ?? Array.Empty<object>() },
            { "DistCoeffs", distCoeffs ?? Array.Empty<object>() },
            { "PixelSize", pixelSize }
        };
    }

    private static Dictionary<string, object> LoadFromXml(string filePath)
    {
        var document = XDocument.Load(filePath);
        var root = document.Root;
        if (root == null)
        {
            throw new InvalidOperationException("Invalid XML content");
        }

        var transformText = FindXmlElementValue(root,
            "TransformMatrix", "transformMatrix", "Matrix", "matrix", "Homography", "homography");

        var cameraText = FindXmlElementValue(root,
            "CameraMatrix", "cameraMatrix");

        var distText = FindXmlElementValue(root,
            "DistCoeffs", "distCoeffs", "DistortionCoefficients", "distortionCoefficients");

        var pixelText = FindXmlElementValue(root,
            "PixelSize", "pixelSize", "ScaleX", "scaleX");

        _ = double.TryParse(pixelText, out var pixelSize);

        return new Dictionary<string, object>
        {
            { "TransformMatrix", ParseInlineMatrix(transformText) },
            { "CameraMatrix", ParseInlineMatrix(cameraText) },
            { "DistCoeffs", ParseInlineArray(distText) },
            { "PixelSize", pixelSize }
        };
    }

    private static Dictionary<string, object> LoadFromYaml(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
            {
                continue;
            }

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"', '\'');
            if (!string.IsNullOrWhiteSpace(key))
            {
                map[key] = value;
            }
        }

        map.TryGetValue("TransformMatrix", out var transformText);
        map.TryGetValue("CameraMatrix", out var cameraText);
        map.TryGetValue("DistCoeffs", out var distText);

        if (!map.TryGetValue("PixelSize", out var pixelText))
        {
            map.TryGetValue("ScaleX", out pixelText);
        }

        _ = double.TryParse(pixelText, out var pixelSize);

        return new Dictionary<string, object>
        {
            { "TransformMatrix", ParseInlineMatrix(transformText) },
            { "CameraMatrix", ParseInlineMatrix(cameraText) },
            { "DistCoeffs", ParseInlineArray(distText) },
            { "PixelSize", pixelSize }
        };
    }

    private static object? FindJsonValue(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryFindJsonProperty(element, name, out var value))
            {
                return JsonSerializer.Deserialize<object>(value.GetRawText());
            }
        }

        return null;
    }

    private static double FindDouble(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryFindJsonProperty(element, name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out number))
            {
                return number;
            }
        }

        return 0;
    }

    private static bool TryFindJsonProperty(JsonElement element, string targetName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }

                if (TryFindJsonProperty(property.Value, targetName, out value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryFindJsonProperty(item, targetName, out value))
                {
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string? FindXmlElementValue(XElement root, params string[] names)
    {
        foreach (var element in root.Descendants())
        {
            foreach (var name in names)
            {
                if (element.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return element.Value;
                }
            }
        }

        return null;
    }

    private static object ParseInlineMatrix(string? matrixText)
    {
        if (string.IsNullOrWhiteSpace(matrixText))
        {
            return Array.Empty<object>();
        }

        var rows = matrixText.Trim().Trim('[', ']').Split(';', StringSplitOptions.RemoveEmptyEntries);
        var matrix = new List<double[]>();

        foreach (var rowText in rows)
        {
            var values = rowText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(v => double.TryParse(v, out var parsed) ? parsed : 0.0)
                .ToArray();
            if (values.Length > 0)
            {
                matrix.Add(values);
            }
        }

        return matrix.Count > 0 ? matrix.ToArray() : Array.Empty<object>();
    }

    private static object ParseInlineArray(string? arrayText)
    {
        if (string.IsNullOrWhiteSpace(arrayText))
        {
            return Array.Empty<double>();
        }

        var values = arrayText.Trim().Trim('[', ']')
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => double.TryParse(v, out var parsed) ? parsed : 0.0)
            .ToArray();

        return values;
    }
}
