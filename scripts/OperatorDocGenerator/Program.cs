using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;

var repoRoot = ResolveRepoRoot(args);
var overwrite = args.Any(arg => string.Equals(arg, "--overwrite", StringComparison.OrdinalIgnoreCase));
var docsRoot = Path.Combine(repoRoot, "docs", "operators");

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

Console.WriteLine(
    $"repoRoot={repoRoot} docsRoot={docsRoot} operators={candidates.Count} generated={generated} skipped={skipped} overwrite={overwrite}");

return 0;

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
    sb.AppendLine($"| 分类 (Category) | {EscapeCell(item.Meta.Category)} |");
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
