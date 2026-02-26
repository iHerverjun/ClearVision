using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;

var repoRoot = ResolveRepoRoot(args);
var overwrite = args.Any(arg => string.Equals(arg, "--overwrite", StringComparison.OrdinalIgnoreCase));
var docsRoot = Path.Combine(repoRoot, "docs", "operators");
var generatedAt = DateTimeOffset.Now;

Directory.CreateDirectory(docsRoot);

var candidates = typeof(OperatorBase).Assembly
    .GetTypes()
    .Where(type => type.IsClass && !type.IsAbstract)
    .Where(type => typeof(OperatorBase).IsAssignableFrom(type))
    .Select(type => new
    {
        Type = type,
        Meta = type.GetCustomAttribute<OperatorMetaAttribute>(inherit: false),
        Algo = type.GetCustomAttribute<AlgorithmInfoAttribute>(inherit: false),
        Inputs = type.GetCustomAttributes<InputPortAttribute>(inherit: false).ToArray(),
        Outputs = type.GetCustomAttributes<OutputPortAttribute>(inherit: false).ToArray(),
        Parameters = type.GetCustomAttributes<OperatorParamAttribute>(inherit: false).ToArray()
    })
    .Where(x => x.Meta != null)
    .Select(x => new OperatorDocModel(
        x.Type,
        x.Meta!,
        x.Algo,
        x.Inputs,
        x.Outputs,
        x.Parameters,
        ResolveOperatorType(x.Type)))
    .Where(x => x.OperatorType != null)
    .OrderBy(x => x.OperatorType!.Value.ToString(), StringComparer.Ordinal)
    .ToList();

var (generated, skipped) = GenerateOperatorDocuments(candidates, docsRoot, overwrite);
GenerateCatalogJson(candidates, docsRoot, generatedAt);
GenerateCatalogMarkdown(candidates, docsRoot, generatedAt);

Console.WriteLine($"repoRoot={repoRoot} docsRoot={docsRoot} operators={candidates.Count} generated={generated} skipped={skipped} overwrite={overwrite}");
Console.WriteLine($"catalogJson={Path.Combine(docsRoot, "catalog.json")} catalogMarkdown={Path.Combine(docsRoot, "CATALOG.md")}");

return 0;

static (int generated, int skipped) GenerateOperatorDocuments(IReadOnlyList<OperatorDocModel> candidates, string docsRoot, bool overwrite)
{
    var generated = 0;
    var skipped = 0;

    foreach (var item in candidates)
    {
        var fileName = $"{item.OperatorType}.md";
        var filePath = Path.Combine(docsRoot, fileName);
        if (File.Exists(filePath) && !overwrite)
        {
            skipped++;
            continue;
        }

        File.WriteAllText(filePath, BuildDocument(item), new UTF8Encoding(false));
        generated++;
    }

    return (generated, skipped);
}

static void GenerateCatalogJson(IReadOnlyList<OperatorDocModel> candidates, string docsRoot, DateTimeOffset generatedAt)
{
    var operators = candidates
        .Select(ToCatalogOperator)
        .OrderBy(item => item.Type)
        .ToList();

    var categories = operators
        .GroupBy(item => NormalizeCategory(item.Category))
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .ToDictionary(
            group => group.Key,
            group => new CatalogCategorySummary
            {
                Count = group.Count(),
                Operators = group
                    .Select(op => op.Id)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToList()
            },
            StringComparer.Ordinal);

    var model = new CatalogDocument
    {
        GeneratedAt = generatedAt.ToString("o", CultureInfo.InvariantCulture),
        TotalCount = operators.Count,
        Categories = categories,
        Operators = operators
    };

    var options = new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    var json = JsonSerializer.Serialize(model, options);
    File.WriteAllText(Path.Combine(docsRoot, "catalog.json"), json + Environment.NewLine, new UTF8Encoding(false));
}

static void GenerateCatalogMarkdown(IReadOnlyList<OperatorDocModel> candidates, string docsRoot, DateTimeOffset generatedAt)
{
    var operators = candidates
        .Select(ToCatalogOperator)
        .OrderBy(item => item.Type)
        .ToList();

    var grouped = operators
        .GroupBy(item => NormalizeCategory(item.Category))
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .ToList();

    var sb = new StringBuilder();
    sb.AppendLine("# 算子目录 / Operator Catalog");
    sb.AppendLine();
    sb.AppendLine($"> 生成时间 / Generated At: `{generatedAt:yyyy-MM-dd HH:mm:ss zzz}`");
    sb.AppendLine($"> 算子总数 / Total Operators: **{operators.Count}**");
    sb.AppendLine();
    sb.AppendLine("## 分类统计 / Category Summary");
    sb.AppendLine("| 分类 (Category) | 数量 (Count) | 占比 (Ratio) |");
    sb.AppendLine("|------|------:|------:|");
    foreach (var categoryGroup in grouped)
    {
        var ratio = operators.Count == 0 ? 0 : categoryGroup.Count() * 100.0 / operators.Count;
        sb.AppendLine($"| {EscapeCell(categoryGroup.Key)} | {categoryGroup.Count()} | {ratio.ToString("0.0", CultureInfo.InvariantCulture)}% |");
    }

    sb.AppendLine();
    sb.AppendLine("## 分类索引 / Grouped Index");
    foreach (var categoryGroup in grouped)
    {
        sb.AppendLine();
        sb.AppendLine($"### {categoryGroup.Key} ({categoryGroup.Count()})");
        sb.AppendLine("| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |");
        sb.AppendLine("|------|------|------:|------:|------:|------|------|");

        foreach (var op in categoryGroup.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            var algorithm = string.IsNullOrWhiteSpace(op.Algorithm) ? "-" : EscapeCell(op.Algorithm!);
            var linkPath = $"./{op.Id}.md";
            sb.AppendLine(
                $"| `OperatorType.{op.Id}` | {EscapeCell(op.DisplayName)} | {op.InputPorts.Count} | {op.OutputPorts.Count} | {op.Parameters.Count} | {algorithm} | [{op.Id}]({linkPath}) |");
        }
    }

    File.WriteAllText(Path.Combine(docsRoot, "CATALOG.md"), sb.ToString(), new UTF8Encoding(false));
}

static string ResolveRepoRoot(string[] args)
{
    if (args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal))
    {
        return Path.GetFullPath(args[0]);
    }

    return Directory.GetCurrentDirectory();
}

static OperatorType? ResolveOperatorType(Type operatorType)
{
    var property = operatorType.GetProperty(nameof(OperatorBase.OperatorType), BindingFlags.Public | BindingFlags.Instance);
    if (property?.PropertyType == typeof(OperatorType) && property.GetMethod != null)
    {
        try
        {
            var uninitialized = RuntimeHelpers.GetUninitializedObject(operatorType);
            if (property.GetValue(uninitialized) is OperatorType resolved)
            {
                return resolved;
            }
        }
        catch
        {
            // fallback to class name parsing
        }
    }

    var className = operatorType.Name;
    if (className.EndsWith("Operator", StringComparison.Ordinal))
    {
        className = className[..^"Operator".Length];
    }

    return Enum.TryParse<OperatorType>(className, out var parsed) ? parsed : null;
}

static string BuildDocument(OperatorDocModel item)
{
    var sb = new StringBuilder();
    var className = item.ClrType.Name;
    var englishName = className.EndsWith("Operator", StringComparison.Ordinal)
        ? className[..^"Operator".Length]
        : className;

    sb.AppendLine($"# {item.Meta.DisplayName} / {englishName}");
    sb.AppendLine();
    sb.AppendLine("## 基本信息 / Basic Info");
    sb.AppendLine("| 项目 (Field) | 值 (Value) |");
    sb.AppendLine("|------|------|");
    sb.AppendLine($"| 类名 (Class) | `{className}` |");
    sb.AppendLine($"| 枚举值 (Enum) | `OperatorType.{item.OperatorType}` |");
    sb.AppendLine($"| 分类 (Category) | {EscapeCell(NormalizeCategory(item.Meta.Category))} |");
    sb.AppendLine("| 成熟度 (Maturity) | 稳定 Stable |");
    sb.AppendLine("| 作者 (Author) | 蘅芜君 |");
    sb.AppendLine();
    sb.AppendLine("## 算法原理 / Algorithm Principle");
    sb.AppendLine($"> 中文：{Fallback(item.Meta.Description, "TODO：补充算法原理")}。");
    sb.AppendLine($"> English: {Fallback(item.Meta.Description, "TODO: Add algorithm principle.")}.");
    sb.AppendLine();
    sb.AppendLine("## 实现策略 / Implementation Strategy");
    sb.AppendLine("> 中文：TODO：补充实现策略与方案对比。");
    sb.AppendLine("> English: TODO: Add implementation strategy and alternatives comparison.");
    sb.AppendLine();
    sb.AppendLine("## 核心 API 调用链 / Core API Call Chain");
    if (!string.IsNullOrWhiteSpace(item.Algo?.CoreApi))
    {
        sb.AppendLine($"- `{item.Algo.CoreApi}`");
    }
    else
    {
        sb.AppendLine("- TODO：补充关键 API 调用链");
    }

    sb.AppendLine();
    sb.AppendLine("## 参数说明 / Parameters");
    sb.AppendLine("| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |");
    sb.AppendLine("|--------|------|--------|------|------|");
    if (item.Parameters.Length == 0)
    {
        sb.AppendLine("| - | - | - | - | - |");
    }
    else
    {
        foreach (var parameter in item.Parameters)
        {
            var range = BuildRange(parameter.Min, parameter.Max);
            sb.AppendLine(
                $"| `{parameter.Name}` | `{parameter.DataType}` | {EscapeCell(FormatValue(parameter.DefaultValue))} | {EscapeCell(range)} | {EscapeCell(Fallback(parameter.Description, "-"))} |");
        }
    }

    sb.AppendLine();
    sb.AppendLine("## 输入/输出端口 / Input/Output Ports");
    sb.AppendLine("### 输入 / Inputs");
    sb.AppendLine("| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |");
    sb.AppendLine("|------|------|------|------|------|");
    if (item.Inputs.Length == 0)
    {
        sb.AppendLine("| - | - | - | - | - |");
    }
    else
    {
        foreach (var input in item.Inputs)
        {
            sb.AppendLine(
                $"| `{input.Name}` | {EscapeCell(input.DisplayName)} | `{input.DataType}` | {BoolToMark(input.IsRequired)} | - |");
        }
    }

    sb.AppendLine();
    sb.AppendLine("### 输出 / Outputs");
    sb.AppendLine("| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |");
    sb.AppendLine("|------|------|------|------|");
    if (item.Outputs.Length == 0)
    {
        sb.AppendLine("| - | - | - | - |");
    }
    else
    {
        foreach (var output in item.Outputs)
        {
            sb.AppendLine($"| `{output.Name}` | {EscapeCell(output.DisplayName)} | `{output.DataType}` | - |");
        }
    }

    sb.AppendLine();
    sb.AppendLine("## 性能特征 / Performance");
    sb.AppendLine("| 指标 (Metric) | 值 (Value) |");
    sb.AppendLine("|------|------|");
    sb.AppendLine($"| 时间复杂度 (Time Complexity) | {EscapeCell(Fallback(item.Algo?.TimeComplexity, "O(?)"))} |");
    sb.AppendLine($"| 典型耗时 (Typical Latency) | {EscapeCell("~?ms (1920x1080)")} |");
    sb.AppendLine($"| 内存特征 (Memory Profile) | {EscapeCell(Fallback(item.Algo?.SpaceComplexity, "?"))} |");

    sb.AppendLine();
    sb.AppendLine("## 适用场景 / Use Cases");
    sb.AppendLine("- 适合 (Suitable)：TODO");
    sb.AppendLine("- 不适合 (Not Suitable)：TODO");

    sb.AppendLine();
    sb.AppendLine("## 已知限制 / Known Limitations");
    sb.AppendLine("1. TODO");

    sb.AppendLine();
    sb.AppendLine("## 变更记录 / Changelog");
    sb.AppendLine("| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |");
    sb.AppendLine("|------|------|----------|");
    sb.AppendLine($"| 0.1.0 | {DateTime.UtcNow:yyyy-MM-dd} | 自动生成文档骨架 / Generated skeleton |");

    return sb.ToString();
}

static CatalogOperator ToCatalogOperator(OperatorDocModel item)
{
    var id = item.OperatorType!.Value.ToString();

    var inputPorts = item.Inputs
        .Select(port => new CatalogPort
        {
            Name = port.Name,
            DisplayName = port.DisplayName,
            DataType = port.DataType.ToString(),
            IsRequired = port.IsRequired
        })
        .ToList();

    var outputPorts = item.Outputs
        .Select(port => new CatalogPort
        {
            Name = port.Name,
            DisplayName = port.DisplayName,
            DataType = port.DataType.ToString(),
            IsRequired = null
        })
        .ToList();

    var parameters = item.Parameters
        .Select(parameter => new CatalogParameter
        {
            Name = parameter.Name,
            DisplayName = parameter.DisplayName,
            DataType = parameter.DataType,
            Description = parameter.Description,
            DefaultValue = NormalizeParameterValue(parameter.DefaultValue),
            Min = NormalizeParameterValue(parameter.Min),
            Max = NormalizeParameterValue(parameter.Max),
            IsRequired = parameter.IsRequired,
            Options = ParseParameterOptions(parameter.Options)
        })
        .ToList();

    return new CatalogOperator
    {
        Id = id,
        Type = (int)item.OperatorType.Value,
        DisplayName = item.Meta.DisplayName,
        Description = item.Meta.Description,
        Category = NormalizeCategory(item.Meta.Category),
        Algorithm = item.Algo?.Name,
        InputPorts = inputPorts,
        OutputPorts = outputPorts,
        Parameters = parameters,
        DocPath = $"docs/operators/{id}.md"
    };
}

static string NormalizeCategory(string? category)
{
    if (string.IsNullOrWhiteSpace(category))
    {
        return "未分类";
    }

    return category.Trim() switch
    {
        "Preprocessing" => "预处理",
        "Filtering" => "预处理",
        "Calibration" => "标定",
        "Feature Extraction" => "特征提取",
        "General" => "通用",
        "Logic Tools" => "逻辑工具",
        "Matching" => "匹配定位",
        "Measurement" => "检测",
        "测量" => "检测",
        "控制" => "流程控制",
        "逻辑控制" => "流程控制",
        "数据" => "数据处理",
        _ => category.Trim()
    };
}

static List<CatalogParameterOption>? ParseParameterOptions(string[]? options)
{
    if (options == null || options.Length == 0)
    {
        return null;
    }

    var parsed = new List<CatalogParameterOption>(options.Length);
    foreach (var option in options)
    {
        if (string.IsNullOrWhiteSpace(option))
        {
            continue;
        }

        var parts = option.Split('|', 2, StringSplitOptions.TrimEntries);
        var value = parts[0];
        if (string.IsNullOrWhiteSpace(value))
        {
            continue;
        }

        var label = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])
            ? parts[1]
            : value;

        parsed.Add(new CatalogParameterOption
        {
            Value = value,
            Label = label
        });
    }

    return parsed.Count == 0 ? null : parsed;
}

static string? NormalizeParameterValue(object? value)
{
    if (value == null)
    {
        return null;
    }

    if (value is string text)
    {
        return text;
    }

    if (value is bool b)
    {
        return b ? "true" : "false";
    }

    if (value is IFormattable formattable)
    {
        return formattable.ToString(null, CultureInfo.InvariantCulture);
    }

    return Convert.ToString(value, CultureInfo.InvariantCulture);
}

static string BuildRange(object? min, object? max)
{
    var minText = FormatValue(min);
    var maxText = FormatValue(max);
    return (min, max) switch
    {
        (null, null) => "-",
        (_, null) => $">= {minText}",
        (null, _) => $"<= {maxText}",
        _ => $"[{minText}, {maxText}]"
    };
}

static string FormatValue(object? value)
{
    if (value == null)
    {
        return "-";
    }

    if (value is string text)
    {
        return text.Length == 0 ? "\"\"" : text;
    }

    if (value is bool b)
    {
        return b ? "true" : "false";
    }

    if (value is IFormattable formattable)
    {
        return formattable.ToString(null, CultureInfo.InvariantCulture) ?? "-";
    }

    return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "-";
}

static string BoolToMark(bool value) => value ? "Yes" : "No";

static string Fallback(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;

static string EscapeCell(string input) => input.Replace("|", "\\|", StringComparison.Ordinal);

internal sealed record OperatorDocModel(
    Type ClrType,
    OperatorMetaAttribute Meta,
    AlgorithmInfoAttribute? Algo,
    InputPortAttribute[] Inputs,
    OutputPortAttribute[] Outputs,
    OperatorParamAttribute[] Parameters,
    OperatorType? OperatorType);

internal sealed class CatalogDocument
{
    public string GeneratedAt { get; set; } = string.Empty;

    public int TotalCount { get; set; }

    public Dictionary<string, CatalogCategorySummary> Categories { get; set; } = new(StringComparer.Ordinal);

    public List<CatalogOperator> Operators { get; set; } = new();
}

internal sealed class CatalogCategorySummary
{
    public int Count { get; set; }

    public List<string> Operators { get; set; } = new();
}

internal sealed class CatalogOperator
{
    public string Id { get; set; } = string.Empty;

    public int Type { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string? Algorithm { get; set; }

    public List<CatalogPort> InputPorts { get; set; } = new();

    public List<CatalogPort> OutputPorts { get; set; } = new();

    public List<CatalogParameter> Parameters { get; set; } = new();

    public string DocPath { get; set; } = string.Empty;
}

internal sealed class CatalogPort
{
    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string DataType { get; set; } = string.Empty;

    public bool? IsRequired { get; set; }
}

internal sealed class CatalogParameter
{
    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string DataType { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? DefaultValue { get; set; }

    public string? Min { get; set; }

    public string? Max { get; set; }

    public bool IsRequired { get; set; }

    public List<CatalogParameterOption>? Options { get; set; }
}

internal sealed class CatalogParameterOption
{
    public string Value { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
}
