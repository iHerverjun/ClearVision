#!/usr/bin/env dotnet-script
// Usage:
//   dotnet script tools/generate_operator_catalog.csx [repoRoot]
//
// This script builds Acme.Product.Infrastructure, instantiates
// Acme.Product.Infrastructure.Services.OperatorFactory, reads GetAllMetadata(),
// and exports:
//   - docs/operator_catalog.json
//   - docs/OPERATOR_CATALOG.md

using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;

var repoRoot = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Directory.GetCurrentDirectory();

var infrastructureProject = Path.Combine(
    repoRoot,
    "Acme.Product",
    "src",
    "Acme.Product.Infrastructure",
    "Acme.Product.Infrastructure.csproj");

if (!File.Exists(infrastructureProject))
{
    Console.Error.WriteLine($"[ERROR] Project not found: {infrastructureProject}");
    return 1;
}

if (RunProcess("dotnet", $"build \"{infrastructureProject}\" -c Release", repoRoot) != 0)
{
    return 2;
}

var infrastructureDll = Path.Combine(
    Path.GetDirectoryName(infrastructureProject)!,
    "bin",
    "Release",
    "net8.0",
    "Acme.Product.Infrastructure.dll");

if (!File.Exists(infrastructureDll))
{
    Console.Error.WriteLine($"[ERROR] Build output not found: {infrastructureDll}");
    return 3;
}

var infrastructureAssembly = Assembly.LoadFrom(infrastructureDll);
var factoryType = infrastructureAssembly.GetType("Acme.Product.Infrastructure.Services.OperatorFactory");

if (factoryType == null)
{
    Console.Error.WriteLine("[ERROR] Cannot locate OperatorFactory type.");
    return 4;
}

var factory = Activator.CreateInstance(factoryType);
if (factory == null)
{
    Console.Error.WriteLine("[ERROR] Cannot create OperatorFactory instance.");
    return 5;
}

var getAllMetadataMethod = factoryType.GetMethod("GetAllMetadata", BindingFlags.Public | BindingFlags.Instance);
if (getAllMetadataMethod == null)
{
    Console.Error.WriteLine("[ERROR] Cannot locate OperatorFactory.GetAllMetadata().");
    return 6;
}

var metadataEnumerable = getAllMetadataMethod.Invoke(factory, null) as IEnumerable;
if (metadataEnumerable == null)
{
    Console.Error.WriteLine("[ERROR] OperatorFactory.GetAllMetadata() returned null.");
    return 7;
}

var catalogItems = new List<Dictionary<string, object?>>();
foreach (var metadata in metadataEnumerable)
{
    if (metadata == null)
    {
        continue;
    }

    var typeObject = GetProperty(metadata, "Type");
    var typeName = typeObject?.ToString() ?? "Unknown";
    var typeValue = typeObject != null
        ? Convert.ToInt32(typeObject, CultureInfo.InvariantCulture)
        : -1;

    var inputPorts = ConvertPortList(GetProperty(metadata, "InputPorts"));
    var outputPorts = ConvertPortList(GetProperty(metadata, "OutputPorts"));
    var parameters = ConvertParameterList(GetProperty(metadata, "Parameters"));

    catalogItems.Add(new Dictionary<string, object?>
    {
        ["type"] = typeValue,
        ["typeName"] = typeName,
        ["displayName"] = GetProperty(metadata, "DisplayName")?.ToString() ?? string.Empty,
        ["description"] = GetProperty(metadata, "Description")?.ToString() ?? string.Empty,
        ["category"] = GetProperty(metadata, "Category")?.ToString() ?? string.Empty,
        ["iconName"] = GetProperty(metadata, "IconName")?.ToString(),
        ["keywords"] = ConvertStringArray(GetProperty(metadata, "Keywords")),
        ["tags"] = ConvertStringArray(GetProperty(metadata, "Tags")),
        ["version"] = GetProperty(metadata, "Version")?.ToString() ?? "1.0.0",
        ["inputPorts"] = inputPorts,
        ["outputPorts"] = outputPorts,
        ["parameters"] = parameters
    });
}

catalogItems = catalogItems
    .OrderBy(item => Convert.ToInt32(item["type"], CultureInfo.InvariantCulture))
    .ToList();

var generatedAt = DateTimeOffset.Now;
var jsonModel = new Dictionary<string, object?>
{
    ["generatedAt"] = generatedAt.ToString("o", CultureInfo.InvariantCulture),
    ["totalCount"] = catalogItems.Count,
    ["operators"] = catalogItems
};

var docsDirectory = Path.Combine(repoRoot, "docs");
Directory.CreateDirectory(docsDirectory);

var jsonPath = Path.Combine(docsDirectory, "operator_catalog.json");
var markdownPath = Path.Combine(docsDirectory, "OPERATOR_CATALOG.md");

var jsonOptions = new JsonSerializerOptions
{
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    WriteIndented = true
};

File.WriteAllText(
    jsonPath,
    JsonSerializer.Serialize(jsonModel, jsonOptions) + Environment.NewLine,
    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

var markdown = BuildMarkdownCatalog(catalogItems, generatedAt);
File.WriteAllText(
    markdownPath,
    markdown,
    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

Console.WriteLine($"[OK] Exported {catalogItems.Count} operators.");
Console.WriteLine($"[OK] JSON: {jsonPath}");
Console.WriteLine($"[OK] Markdown: {markdownPath}");

return 0;

static int RunProcess(string fileName, string arguments, string workingDirectory)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = fileName,
        Arguments = arguments,
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };

    using var process = Process.Start(startInfo);
    if (process == null)
    {
        Console.Error.WriteLine($"[ERROR] Failed to start process: {fileName}");
        return -1;
    }

    process.OutputDataReceived += (_, args) =>
    {
        if (args.Data != null)
        {
            Console.WriteLine(args.Data);
        }
    };

    process.ErrorDataReceived += (_, args) =>
    {
        if (args.Data != null)
        {
            Console.Error.WriteLine(args.Data);
        }
    };

    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    process.WaitForExit();
    return process.ExitCode;
}

static object? GetProperty(object instance, string propertyName)
{
    var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
    return property?.GetValue(instance);
}

static List<string> ConvertStringArray(object? value)
{
    if (value is not IEnumerable enumerable || value is string)
    {
        return new List<string>();
    }

    return enumerable
        .Cast<object?>()
        .Select(item => item?.ToString())
        .Where(text => !string.IsNullOrWhiteSpace(text))
        .Select(text => text!.Trim())
        .Distinct(StringComparer.Ordinal)
        .OrderBy(text => text, StringComparer.Ordinal)
        .ToList();
}

static List<Dictionary<string, object?>> ConvertPortList(object? value)
{
    if (value is not IEnumerable enumerable || value is string)
    {
        return new List<Dictionary<string, object?>>();
    }

    var result = new List<Dictionary<string, object?>>();
    foreach (var item in enumerable)
    {
        if (item == null)
        {
            continue;
        }

        var entry = new Dictionary<string, object?>
        {
            ["name"] = GetProperty(item, "Name")?.ToString() ?? string.Empty,
            ["displayName"] = GetProperty(item, "DisplayName")?.ToString() ?? string.Empty,
            ["dataType"] = GetProperty(item, "DataType")?.ToString() ?? string.Empty,
            ["isRequired"] = GetProperty(item, "IsRequired") is bool b && b,
            ["description"] = GetProperty(item, "Description")?.ToString()
        };

        result.Add(entry);
    }

    return result;
}

static List<Dictionary<string, object?>> ConvertParameterList(object? value)
{
    if (value is not IEnumerable enumerable || value is string)
    {
        return new List<Dictionary<string, object?>>();
    }

    var result = new List<Dictionary<string, object?>>();
    foreach (var item in enumerable)
    {
        if (item == null)
        {
            continue;
        }

        var options = ConvertParameterOptions(GetProperty(item, "Options"));
        var entry = new Dictionary<string, object?>
        {
            ["name"] = GetProperty(item, "Name")?.ToString() ?? string.Empty,
            ["displayName"] = GetProperty(item, "DisplayName")?.ToString() ?? string.Empty,
            ["description"] = GetProperty(item, "Description")?.ToString(),
            ["dataType"] = GetProperty(item, "DataType")?.ToString() ?? string.Empty,
            ["defaultValue"] = NormalizeValue(GetProperty(item, "DefaultValue")),
            ["minValue"] = NormalizeValue(GetProperty(item, "MinValue")),
            ["maxValue"] = NormalizeValue(GetProperty(item, "MaxValue")),
            ["isRequired"] = GetProperty(item, "IsRequired") is bool b && b,
            ["options"] = options.Count == 0 ? null : options
        };

        result.Add(entry);
    }

    return result;
}

static List<Dictionary<string, string>> ConvertParameterOptions(object? value)
{
    if (value is not IEnumerable enumerable || value is string)
    {
        return new List<Dictionary<string, string>>();
    }

    var result = new List<Dictionary<string, string>>();
    foreach (var option in enumerable)
    {
        if (option == null)
        {
            continue;
        }

        var valueText = GetProperty(option, "Value")?.ToString();
        if (string.IsNullOrWhiteSpace(valueText))
        {
            continue;
        }

        result.Add(new Dictionary<string, string>
        {
            ["value"] = valueText.Trim(),
            ["label"] = (GetProperty(option, "Label")?.ToString() ?? valueText).Trim()
        });
    }

    return result;
}

static object? NormalizeValue(object? value)
{
    if (value == null)
    {
        return null;
    }

    return value switch
    {
        bool b => b,
        byte n => n,
        sbyte n => n,
        short n => n,
        ushort n => n,
        int n => n,
        uint n => n,
        long n => n,
        ulong n => n,
        float n => n,
        double n => n,
        decimal n => n,
        string text => text,
        _ when value.GetType().IsEnum => value.ToString(),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };
}

static string BuildMarkdownCatalog(
    IReadOnlyList<Dictionary<string, object?>> items,
    DateTimeOffset generatedAt)
{
    var sb = new StringBuilder();
    sb.AppendLine("# Operator Catalog");
    sb.AppendLine();
    sb.AppendLine($"> Generated At: `{generatedAt:yyyy-MM-dd HH:mm:ss zzz}`");
    sb.AppendLine($"> Total Operators: **{items.Count}**");
    sb.AppendLine();
    sb.AppendLine("## Summary");
    sb.AppendLine("| Type | Display Name | Category | Inputs | Outputs | Parameters |");
    sb.AppendLine("|------|------|------|------:|------:|------:|");

    foreach (var item in items)
    {
        var inputCount = CountList(item, "inputPorts");
        var outputCount = CountList(item, "outputPorts");
        var parameterCount = CountList(item, "parameters");

        sb.AppendLine(
            $"| `{EscapeCell((item["typeName"] ?? "Unknown").ToString() ?? "Unknown")}` " +
            $"| {EscapeCell((item["displayName"] ?? string.Empty).ToString() ?? string.Empty)} " +
            $"| {EscapeCell((item["category"] ?? string.Empty).ToString() ?? string.Empty)} " +
            $"| {inputCount} | {outputCount} | {parameterCount} |");
    }

    sb.AppendLine();
    sb.AppendLine("## Details");

    foreach (var item in items)
    {
        var typeName = (item["typeName"] ?? "Unknown").ToString() ?? "Unknown";
        var typeValue = item["type"]?.ToString() ?? "-1";
        var description = (item["description"] ?? string.Empty).ToString() ?? string.Empty;
        var category = (item["category"] ?? string.Empty).ToString() ?? string.Empty;

        sb.AppendLine();
        sb.AppendLine($"### OperatorType.{typeName} ({typeValue})");
        sb.AppendLine($"- Display Name: {EscapeText((item["displayName"] ?? string.Empty).ToString() ?? string.Empty)}");
        sb.AppendLine($"- Category: {EscapeText(category)}");
        sb.AppendLine($"- Version: `{EscapeText((item["version"] ?? "1.0.0").ToString() ?? "1.0.0")}`");
        sb.AppendLine($"- Description: {EscapeText(description)}");

        var keywords = item["keywords"] as IEnumerable<string> ?? Array.Empty<string>();
        if (keywords.Any())
        {
            sb.AppendLine($"- Keywords: {string.Join(", ", keywords.Select(EscapeText))}");
        }

        var inputPorts = item["inputPorts"] as IEnumerable<Dictionary<string, object?>> ?? Array.Empty<Dictionary<string, object?>>();
        sb.AppendLine();
        sb.AppendLine("#### Input Ports");
        sb.AppendLine("| Name | Display Name | Data Type | Required |");
        sb.AppendLine("|------|------|------|------|");
        if (!inputPorts.Any())
        {
            sb.AppendLine("| - | - | - | - |");
        }
        else
        {
            foreach (var port in inputPorts)
            {
                sb.AppendLine(
                    $"| `{EscapeCell((port["name"] ?? string.Empty).ToString() ?? string.Empty)}` " +
                    $"| {EscapeCell((port["displayName"] ?? string.Empty).ToString() ?? string.Empty)} " +
                    $"| `{EscapeCell((port["dataType"] ?? string.Empty).ToString() ?? string.Empty)}` " +
                    $"| {BoolToYesNo(port["isRequired"])} |");
            }
        }

        var outputPorts = item["outputPorts"] as IEnumerable<Dictionary<string, object?>> ?? Array.Empty<Dictionary<string, object?>>();
        sb.AppendLine();
        sb.AppendLine("#### Output Ports");
        sb.AppendLine("| Name | Display Name | Data Type |");
        sb.AppendLine("|------|------|------|");
        if (!outputPorts.Any())
        {
            sb.AppendLine("| - | - | - |");
        }
        else
        {
            foreach (var port in outputPorts)
            {
                sb.AppendLine(
                    $"| `{EscapeCell((port["name"] ?? string.Empty).ToString() ?? string.Empty)}` " +
                    $"| {EscapeCell((port["displayName"] ?? string.Empty).ToString() ?? string.Empty)} " +
                    $"| `{EscapeCell((port["dataType"] ?? string.Empty).ToString() ?? string.Empty)}` |");
            }
        }

        var parameters = item["parameters"] as IEnumerable<Dictionary<string, object?>> ?? Array.Empty<Dictionary<string, object?>>();
        sb.AppendLine();
        sb.AppendLine("#### Parameters");
        sb.AppendLine("| Name | Display Name | Data Type | Default | Min | Max | Required |");
        sb.AppendLine("|------|------|------|------|------|------|------|");
        if (!parameters.Any())
        {
            sb.AppendLine("| - | - | - | - | - | - | - |");
        }
        else
        {
            foreach (var parameter in parameters)
            {
                sb.AppendLine(
                    $"| `{EscapeCell((parameter["name"] ?? string.Empty).ToString() ?? string.Empty)}` " +
                    $"| {EscapeCell((parameter["displayName"] ?? string.Empty).ToString() ?? string.Empty)} " +
                    $"| `{EscapeCell((parameter["dataType"] ?? string.Empty).ToString() ?? string.Empty)}` " +
                    $"| {EscapeCell(ValueToCell(parameter["defaultValue"]))} " +
                    $"| {EscapeCell(ValueToCell(parameter["minValue"]))} " +
                    $"| {EscapeCell(ValueToCell(parameter["maxValue"]))} " +
                    $"| {BoolToYesNo(parameter["isRequired"])} |");
            }
        }
    }

    return sb.ToString();
}

static int CountList(IReadOnlyDictionary<string, object?> item, string key)
{
    if (!item.TryGetValue(key, out var value) || value is not IEnumerable enumerable || value is string)
    {
        return 0;
    }

    var count = 0;
    foreach (var _ in enumerable)
    {
        count++;
    }

    return count;
}

static string ValueToCell(object? value)
{
    return value == null ? "-" : value.ToString() ?? "-";
}

static string BoolToYesNo(object? value)
{
    return value is bool b && b ? "Yes" : "No";
}

static string EscapeCell(string value)
{
    return value.Replace("|", "\\|", StringComparison.Ordinal);
}

static string EscapeText(string value)
{
    return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
}
