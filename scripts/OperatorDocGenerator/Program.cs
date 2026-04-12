using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;

var repoRoot = ResolveRepoRoot(args);
var overwrite = args.Any(arg => string.Equals(arg, "--overwrite", StringComparison.OrdinalIgnoreCase));
var enforceVersionBump = args.Any(arg => string.Equals(arg, "--enforce-version-bump", StringComparison.OrdinalIgnoreCase));
var operatorDocsRoot = Path.Combine(repoRoot, "docs", "算子资料");
var docsRoot = Path.Combine(operatorDocsRoot, "算子名片");
var legacyMirrorRoot = Path.Combine(repoRoot, "算子资料");
var generatedAt = DateTimeOffset.Now;
var qualityContext = BuildQualityContext(repoRoot, docsRoot);

Directory.CreateDirectory(operatorDocsRoot);
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
var operators = candidates
    .Select(item => ToCatalogOperator(item, qualityContext))
    .OrderBy(item => item.Type)
    .ToList();

GenerateCatalogJson(operators, docsRoot, generatedAt);
GenerateCatalogMarkdown(operators, docsRoot, "./", generatedAt);
var versionTracking = GenerateVersionTrackingArtifacts(candidates, operators, qualityContext, docsRoot, generatedAt);
SyncRootCatalogArtifacts(operators, docsRoot, operatorDocsRoot, generatedAt);
SyncLegacyMirrorArtifacts(operatorDocsRoot, docsRoot, legacyMirrorRoot);

Console.WriteLine($"repoRoot={repoRoot} operatorDocsRoot={operatorDocsRoot} cardsRoot={docsRoot} operators={candidates.Count} generated={generated} skipped={skipped} overwrite={overwrite}");
Console.WriteLine($"catalogJson={Path.Combine(operatorDocsRoot, "算子目录.json")} catalogMarkdown={Path.Combine(operatorDocsRoot, "算子目录.md")}");
Console.WriteLine($"changelog={Path.Combine(operatorDocsRoot, "算子变更记录.md")} versionHistory={Path.Combine(operatorDocsRoot, "算子版本记录.json")}");

if (versionTracking.Violations.Count > 0)
{
    Console.WriteLine($"[WARN] detected {versionTracking.Violations.Count} operator(s) with source changes but unchanged version:");
    foreach (var violation in versionTracking.Violations)
    {
        Console.WriteLine($"[WARN] {violation.OperatorId}: version {violation.CurrentVersion} unchanged while source hash changed.");
    }
}

if (enforceVersionBump && versionTracking.Violations.Count > 0)
{
    Console.Error.WriteLine("[ERROR] version bump enforcement failed. Re-run after updating operator versions.");
    return 2;
}

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

static void GenerateCatalogJson(IReadOnlyList<CatalogOperator> operators, string docsRoot, DateTimeOffset generatedAt)
{
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
        Operators = operators.ToList()
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

static void GenerateCatalogMarkdown(IReadOnlyList<CatalogOperator> operators, string docsRoot, string docLinkPrefix, DateTimeOffset generatedAt)
{
    var markdown = BuildCatalogMarkdown(operators, generatedAt, docLinkPrefix);
    File.WriteAllText(Path.Combine(docsRoot, "CATALOG.md"), markdown, new UTF8Encoding(false));
}

static string BuildCatalogMarkdown(IReadOnlyList<CatalogOperator> operators, DateTimeOffset generatedAt, string docLinkPrefix)
{
    var grouped = operators
        .GroupBy(item => NormalizeCategory(item.Category))
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .ToList();

    var normalizedPrefix = string.IsNullOrWhiteSpace(docLinkPrefix) ? "./" : docLinkPrefix.Trim();
    if (!normalizedPrefix.EndsWith('/'))
    {
        normalizedPrefix += "/";
    }

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

    var averageQuality = operators.Count == 0
        ? 0
        : operators.Average(op => op.Quality.TotalScore);

    var qualityLevelGroups = operators
        .GroupBy(op => op.Quality.Level)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .ToList();

    sb.AppendLine();
    sb.AppendLine("## 质量评分 / Quality Score");
    sb.AppendLine($"- 平均分 / Average: **{averageQuality.ToString("0.0", CultureInfo.InvariantCulture)}**");
    sb.AppendLine("| 等级 (Level) | 数量 (Count) |");
    sb.AppendLine("|------|------:|");
    foreach (var levelGroup in qualityLevelGroups)
    {
        sb.AppendLine($"| {EscapeCell(levelGroup.Key)} | {levelGroup.Count()} |");
    }

    sb.AppendLine();
    sb.AppendLine("## 分类索引 / Grouped Index");
    foreach (var categoryGroup in grouped)
    {
        sb.AppendLine();
        sb.AppendLine($"### {categoryGroup.Key} ({categoryGroup.Count()})");
        sb.AppendLine("| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |");
        sb.AppendLine("|------|------|------:|------:|------:|------|------|------|------|");

        foreach (var op in categoryGroup.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            var algorithm = string.IsNullOrWhiteSpace(op.Algorithm) ? "-" : EscapeCell(op.Algorithm!);
            var linkPath = $"{normalizedPrefix}{op.Id}.md";
            var quality = $"{op.Quality.TotalScore} ({op.Quality.Level})";
            sb.AppendLine(
                $"| `OperatorType.{op.Id}` | {EscapeCell(op.DisplayName)} | {op.InputPorts.Count} | {op.OutputPorts.Count} | {op.Parameters.Count} | {quality} | `{op.Version}` | {algorithm} | [{op.Id}]({linkPath}) |");
        }
    }

    return sb.ToString();
}

static void SyncRootCatalogArtifacts(IReadOnlyList<CatalogOperator> operators, string docsRoot, string docsParentRoot, DateTimeOffset generatedAt)
{
    Directory.CreateDirectory(docsParentRoot);

    var catalogJsonPath = Path.Combine(docsRoot, "catalog.json");
    if (File.Exists(catalogJsonPath))
    {
        var json = File.ReadAllText(catalogJsonPath);
        File.WriteAllText(Path.Combine(docsParentRoot, "算子目录.json"), json, new UTF8Encoding(false));
    }

    var rootMarkdown = BuildCatalogMarkdown(operators, generatedAt, "./算子名片/");
    File.WriteAllText(Path.Combine(docsParentRoot, "算子目录.md"), rootMarkdown, new UTF8Encoding(false));

    foreach (var artifact in new[] { "CHANGELOG.md", "version-history.json" })
    {
        var sourcePath = Path.Combine(docsRoot, artifact);
        if (File.Exists(sourcePath))
        {
            var content = File.ReadAllText(sourcePath);
            var destinationFileName = artifact == "CHANGELOG.md"
                ? "算子变更记录.md"
                : "算子版本记录.json";
            File.WriteAllText(Path.Combine(docsParentRoot, destinationFileName), content, new UTF8Encoding(false));
        }
    }
}

static void SyncLegacyMirrorArtifacts(string activeDocsRoot, string activeCardsRoot, string legacyMirrorRoot)
{
    Directory.CreateDirectory(legacyMirrorRoot);

    foreach (var fileName in new[] { "算子目录.json", "算子目录.md", "算子变更记录.md", "算子版本记录.json", "算子手册.md", "导航.md" })
    {
        var sourcePath = Path.Combine(activeDocsRoot, fileName);
        if (!File.Exists(sourcePath))
        {
            continue;
        }

        File.Copy(sourcePath, Path.Combine(legacyMirrorRoot, fileName), overwrite: true);
    }

    var legacyCardsRoot = Path.Combine(legacyMirrorRoot, "算子名片");
    Directory.CreateDirectory(legacyCardsRoot);

    foreach (var cardPath in Directory.EnumerateFiles(activeCardsRoot, "*.md", SearchOption.TopDirectoryOnly))
    {
        File.Copy(cardPath, Path.Combine(legacyCardsRoot, Path.GetFileName(cardPath)), overwrite: true);
    }

    foreach (var artifact in new[] { "catalog.json", "CATALOG.md", "CHANGELOG.md", "version-history.json" })
    {
        var sourcePath = Path.Combine(activeCardsRoot, artifact);
        if (!File.Exists(sourcePath))
        {
            continue;
        }

        File.Copy(sourcePath, Path.Combine(legacyCardsRoot, artifact), overwrite: true);
    }
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
    sb.AppendLine($"| {NormalizeSemVersion(item.Meta.Version)} | {DateTime.UtcNow:yyyy-MM-dd} | 自动生成文档骨架 / Generated skeleton |");

    return sb.ToString();
}

static CatalogOperator ToCatalogOperator(OperatorDocModel item, QualityContext qualityContext)
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
        Version = NormalizeSemVersion(item.Meta.Version),
        Tags = BuildOperatorTags(item),
        Algorithm = ResolveCatalogAlgorithm(item, qualityContext),
        InputPorts = inputPorts,
        OutputPorts = outputPorts,
        Parameters = parameters,
        Quality = ComputeQuality(item, qualityContext),
        DocPath = $"算子资料/算子名片/{id}.md"
    };
}

static string? ResolveCatalogAlgorithm(OperatorDocModel item, QualityContext qualityContext)
{
    if (!string.IsNullOrWhiteSpace(item.Algo?.Name))
    {
        return item.Algo!.Name.Trim();
    }

    if (item.OperatorType is null)
    {
        return null;
    }

    var docPath = Path.Combine(qualityContext.DocsRoot, $"{item.OperatorType.Value}.md");
    if (!File.Exists(docPath))
    {
        return null;
    }

    var content = File.ReadAllText(docPath);
    const string marker = "## 算法原理 / Algorithm Principle";
    var startIndex = content.IndexOf(marker, StringComparison.Ordinal);
    if (startIndex < 0)
    {
        return null;
    }

    startIndex += marker.Length;
    var nextIndex = content.IndexOf("\n## ", startIndex, StringComparison.Ordinal);
    var body = nextIndex >= 0
        ? content[startIndex..nextIndex]
        : content[startIndex..];

    var summary = body
        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
        .Select(line => line.Trim())
        .FirstOrDefault(line =>
            !string.IsNullOrWhiteSpace(line) &&
            !line.StartsWith(">", StringComparison.Ordinal) &&
            !line.StartsWith("|", StringComparison.Ordinal));

    if (string.IsNullOrWhiteSpace(summary))
    {
        return null;
    }

    summary = summary
        .Replace("`", string.Empty, StringComparison.Ordinal)
        .Replace("**", string.Empty, StringComparison.Ordinal)
        .Replace("__", string.Empty, StringComparison.Ordinal);

    return summary.Length <= 42
        ? summary
        : summary[..42] + "…";
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

static List<string> BuildOperatorTags(OperatorDocModel item)
{
    var tags = new HashSet<string>(StringComparer.Ordinal);

    if (item.Meta.Tags != null)
    {
        foreach (var rawTag in item.Meta.Tags)
        {
            if (!string.IsNullOrWhiteSpace(rawTag))
            {
                tags.Add(rawTag.Trim());
            }
        }
    }

    tags.Add($"功能域:{ResolveDomainTag(item)}");
    tags.Add($"算法类型:{ResolveAlgorithmTag(item)}");
    tags.Add("成熟度:稳定");

    return tags
        .OrderBy(tag => tag, StringComparer.Ordinal)
        .ToList();
}

static string ResolveDomainTag(OperatorDocModel item)
{
    if (item.OperatorType is null)
    {
        return "检测";
    }

    return item.OperatorType.Value switch
    {
        OperatorType.CameraCalibration
            or OperatorType.Undistort
            or OperatorType.CoordinateTransform
            or OperatorType.NPointCalibration
            or OperatorType.CalibrationLoader
            or OperatorType.TranslationRotationCalibration
            => "标定",

        OperatorType.ModbusCommunication
            or OperatorType.TcpCommunication
            or OperatorType.SerialCommunication
            or OperatorType.SiemensS7Communication
            or OperatorType.MitsubishiMcCommunication
            or OperatorType.OmronFinsCommunication
            or OperatorType.ModbusRtuCommunication
            or OperatorType.HttpRequest
            or OperatorType.MqttPublish
            => "通信",

        OperatorType.DeepLearning
            or OperatorType.OnnxInference
            or OperatorType.DualModalVoting
            or OperatorType.SurfaceDefectDetection
            or OperatorType.EdgePairDefect
            or OperatorType.BoxNms
            or OperatorType.BoxFilter
            => "AI",

        OperatorType.ConditionalBranch
            or OperatorType.ResultJudgment
            or OperatorType.ResultOutput
            or OperatorType.DatabaseWrite
            or OperatorType.VariableRead
            or OperatorType.VariableWrite
            or OperatorType.VariableIncrement
            or OperatorType.TryCatch
            or OperatorType.CycleCounter
            or OperatorType.ForEach
            or OperatorType.ArrayIndexer
            or OperatorType.JsonExtractor
            or OperatorType.MathOperation
            or OperatorType.LogicGate
            or OperatorType.TypeConvert
            or OperatorType.StringFormat
            or OperatorType.Aggregator
            or OperatorType.Comment
            or OperatorType.Comparator
            or OperatorType.Delay
            or OperatorType.UnitConvert
            or OperatorType.TimerStatistics
            or OperatorType.ScriptOperator
            or OperatorType.TriggerModule
            or OperatorType.TextSave
            => "流程",

        OperatorType.Measurement
            or OperatorType.CircleMeasurement
            or OperatorType.LineMeasurement
            or OperatorType.ContourMeasurement
            or OperatorType.AngleMeasurement
            or OperatorType.GeometricTolerance
            or OperatorType.GeometricFitting
            or OperatorType.CaliperTool
            or OperatorType.WidthMeasurement
            or OperatorType.PointLineDistance
            or OperatorType.LineLineDistance
            or OperatorType.GapMeasurement
            or OperatorType.GeoMeasurement
            or OperatorType.SharpnessEvaluation
            or OperatorType.ColorMeasurement
            => "测量",

        OperatorType.CornerDetection
            or OperatorType.EdgeIntersection
            or OperatorType.ParallelLineFind
            or OperatorType.QuadrilateralFind
            or OperatorType.RectangleDetection
            or OperatorType.PositionCorrection
            or OperatorType.PointAlignment
            or OperatorType.PointCorrection
            => "定位",

        _ => "检测"
    };
}

static string ResolveAlgorithmTag(OperatorDocModel item)
{
    if (item.Algo?.Dependencies != null && item.Algo.Dependencies.Length > 0)
    {
        if (item.Algo.Dependencies.Any(dep =>
                dep.Contains("OpenCv", StringComparison.OrdinalIgnoreCase)))
        {
            return "基于OpenCV";
        }

        return "第三方SDK";
    }

    if (item.Meta.Keywords != null && item.Meta.Keywords.Any(keyword =>
            keyword.Contains("OpenCV", StringComparison.OrdinalIgnoreCase)))
    {
        return "基于OpenCV";
    }

    return "自研";
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

static string NormalizeSemVersion(string? version)
{
    if (string.IsNullOrWhiteSpace(version))
    {
        return "1.0.0";
    }

    var normalized = version.Trim();
    var isValid = Regex.IsMatch(
        normalized,
        @"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$",
        RegexOptions.CultureInvariant);

    return isValid ? normalized : "1.0.0";
}

static VersionTrackingResult GenerateVersionTrackingArtifacts(
    IReadOnlyList<OperatorDocModel> candidates,
    IReadOnlyList<CatalogOperator> operators,
    QualityContext qualityContext,
    string docsRoot,
    DateTimeOffset generatedAt)
{
    var historyPath = Path.Combine(docsRoot, "version-history.json");
    var changelogPath = Path.Combine(docsRoot, "CHANGELOG.md");

    var historyDocument = LoadVersionHistory(historyPath);
    var historyById = historyDocument.Operators
        .Where(item => !string.IsNullOrWhiteSpace(item.Id))
        .ToDictionary(item => item.Id, item => item, StringComparer.Ordinal);

    var operatorById = candidates
        .Where(item => item.OperatorType != null)
        .ToDictionary(item => item.OperatorType!.Value.ToString(), item => item, StringComparer.Ordinal);

    var violations = new List<VersionBumpViolation>();
    var recordedAt = generatedAt.ToString("o", CultureInfo.InvariantCulture);

    foreach (var op in operators)
    {
        if (!operatorById.TryGetValue(op.Id, out var docModel))
        {
            continue;
        }

        if (!qualityContext.SourceTextByTypeName.TryGetValue(docModel.ClrType.Name, out var sourceText))
        {
            sourceText = string.Empty;
        }

        var sourceHash = ComputeSha256(sourceText);
        if (!historyById.TryGetValue(op.Id, out var historyEntry))
        {
            historyEntry = new OperatorVersionHistoryEntry
            {
                Id = op.Id,
                DisplayName = op.DisplayName,
                Category = op.Category
            };
            historyById[op.Id] = historyEntry;
        }
        else
        {
            if (!string.Equals(historyEntry.DisplayName, op.DisplayName, StringComparison.Ordinal))
            {
                historyEntry.DisplayName = op.DisplayName;
            }

            if (!string.Equals(historyEntry.Category, op.Category, StringComparison.Ordinal))
            {
                historyEntry.Category = op.Category;
            }
        }

        var latest = historyEntry.Records.LastOrDefault();
        if (latest == null)
        {
            historyEntry.Records.Add(new OperatorVersionRecord
            {
                Version = op.Version,
                SourceHash = sourceHash,
                RecordedAt = recordedAt
            });
            continue;
        }

        var sourceChanged = !string.Equals(latest.SourceHash, sourceHash, StringComparison.Ordinal);
        var versionChanged = !string.Equals(latest.Version, op.Version, StringComparison.Ordinal);

        if (sourceChanged && !versionChanged)
        {
            violations.Add(new VersionBumpViolation
            {
                OperatorId = op.Id,
                CurrentVersion = op.Version
            });
        }

        if (sourceChanged || versionChanged)
        {
            historyEntry.Records.Add(new OperatorVersionRecord
            {
                Version = op.Version,
                SourceHash = sourceHash,
                RecordedAt = recordedAt
            });
        }
    }

    var historyGeneratedAt = string.IsNullOrWhiteSpace(historyDocument.GeneratedAt)
        ? recordedAt
        : historyDocument.GeneratedAt;

    var mergedHistory = new OperatorVersionHistoryDocument
    {
        GeneratedAt = historyGeneratedAt,
        Operators = historyById.Values
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToList()
    };

    var changelogGeneratedAt = generatedAt;
    if (DateTimeOffset.TryParse(
            historyGeneratedAt,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsedGeneratedAt))
    {
        changelogGeneratedAt = parsedGeneratedAt;
    }

    SaveVersionHistory(historyPath, mergedHistory);
    File.WriteAllText(changelogPath, BuildVersionChangelogMarkdown(operators, mergedHistory, changelogGeneratedAt), new UTF8Encoding(false));

    return new VersionTrackingResult(violations);
}

static OperatorVersionHistoryDocument LoadVersionHistory(string historyPath)
{
    if (!File.Exists(historyPath))
    {
        return new OperatorVersionHistoryDocument();
    }

    try
    {
        var json = File.ReadAllText(historyPath);
        var model = JsonSerializer.Deserialize<OperatorVersionHistoryDocument>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return model ?? new OperatorVersionHistoryDocument();
    }
    catch
    {
        return new OperatorVersionHistoryDocument();
    }
}

static void SaveVersionHistory(string historyPath, OperatorVersionHistoryDocument history)
{
    var json = JsonSerializer.Serialize(
        history,
        new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

    File.WriteAllText(historyPath, json + Environment.NewLine, new UTF8Encoding(false));
}

static string BuildVersionChangelogMarkdown(
    IReadOnlyList<CatalogOperator> operators,
    OperatorVersionHistoryDocument history,
    DateTimeOffset generatedAt)
{
    var sb = new StringBuilder();
    sb.AppendLine("# 算子版本变更记录 / Operator Version Changelog");
    sb.AppendLine();
    sb.AppendLine($"> 生成时间 / Generated At: `{generatedAt:yyyy-MM-dd HH:mm:ss zzz}`");
    sb.AppendLine($"> 算子总数 / Total Operators: **{operators.Count}**");
    sb.AppendLine();

    sb.AppendLine("## 当前版本快照 / Current Snapshot");
    sb.AppendLine("| 枚举 (Enum) | 显示名 (DisplayName) | 分类 (Category) | 版本 (Version) |");
    sb.AppendLine("|------|------|------|------|");
    foreach (var op in operators.OrderBy(item => item.Category, StringComparer.Ordinal).ThenBy(item => item.Id, StringComparer.Ordinal))
    {
        sb.AppendLine($"| `OperatorType.{op.Id}` | {EscapeCell(op.DisplayName)} | {EscapeCell(op.Category)} | `{op.Version}` |");
    }

    var historyById = history.Operators
        .Where(item => !string.IsNullOrWhiteSpace(item.Id))
        .ToDictionary(item => item.Id, item => item, StringComparer.Ordinal);

    var changedOperators = operators
        .Where(op => historyById.TryGetValue(op.Id, out var entry) && entry.Records.Count > 1)
        .OrderBy(op => op.Id, StringComparer.Ordinal)
        .ToList();

    sb.AppendLine();
    sb.AppendLine("## 历史变更 / Historical Changes");
    if (changedOperators.Count == 0)
    {
        sb.AppendLine("- 当前暂无历史版本变更（均为基线版本记录）。");
        return sb.ToString();
    }

    foreach (var op in changedOperators)
    {
        var historyEntry = historyById[op.Id];
        sb.AppendLine();
        sb.AppendLine($"### OperatorType.{op.Id} / {EscapeCell(historyEntry.DisplayName)}");
        sb.AppendLine("| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |");
        sb.AppendLine("|------|------|------|");
        foreach (var record in historyEntry.Records.AsEnumerable().Reverse())
        {
            var hash = record.SourceHash.Length > 12 ? record.SourceHash[..12] : record.SourceHash;
            sb.AppendLine($"| `{record.Version}` | `{record.RecordedAt}` | `{hash}` |");
        }
    }

    return sb.ToString();
}

static string ComputeSha256(string text)
{
    var bytes = Encoding.UTF8.GetBytes(text);
    var hash = SHA256.HashData(bytes);
    return Convert.ToHexString(hash);
}

static QualityContext BuildQualityContext(string repoRoot, string docsRoot)
{
    var sourceIndex = BuildOperatorSourceIndex(repoRoot);
    var testIndex = BuildOperatorTestIndex(repoRoot);

    return new QualityContext(
        repoRoot,
        docsRoot,
        sourceIndex.SourcePathByTypeName,
        sourceIndex.SourceTextByTypeName,
        testIndex);
}

static (Dictionary<string, string> SourcePathByTypeName, Dictionary<string, string> SourceTextByTypeName) BuildOperatorSourceIndex(string repoRoot)
{
    var operatorsRoot = Path.Combine(repoRoot, "Acme.Product", "src", "Acme.Product.Infrastructure", "Operators");
    var pathByType = new Dictionary<string, string>(StringComparer.Ordinal);
    var textByType = new Dictionary<string, string>(StringComparer.Ordinal);

    if (!Directory.Exists(operatorsRoot))
    {
        return (pathByType, textByType);
    }

    var classPattern = new Regex(@"\bclass\s+([A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.Compiled);

    foreach (var filePath in Directory.EnumerateFiles(operatorsRoot, "*.cs", SearchOption.AllDirectories))
    {
        var sourceText = File.ReadAllText(filePath);
        var matches = classPattern.Matches(sourceText);

        foreach (Match match in matches)
        {
            var className = match.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(className) || pathByType.ContainsKey(className))
            {
                continue;
            }

            pathByType[className] = filePath;
            textByType[className] = sourceText;
        }
    }

    return (pathByType, textByType);
}

static HashSet<string> BuildOperatorTestIndex(string repoRoot)
{
    var testsRoot = Path.Combine(repoRoot, "Acme.Product", "tests", "Acme.Product.Tests", "Operators");
    var index = new HashSet<string>(StringComparer.Ordinal);

    if (!Directory.Exists(testsRoot))
    {
        return index;
    }

    foreach (var filePath in Directory.EnumerateFiles(testsRoot, "*Tests.cs", SearchOption.AllDirectories))
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        if (fileName.EndsWith("Tests", StringComparison.Ordinal))
        {
            var candidate = fileName[..^"Tests".Length];
            index.Add(candidate);

            if (candidate.EndsWith("Operator", StringComparison.Ordinal))
            {
                index.Add(candidate[..^"Operator".Length]);
            }
        }
    }

    return index;
}

static CatalogQuality ComputeQuality(OperatorDocModel item, QualityContext qualityContext)
{
    var operatorId = item.OperatorType!.Value.ToString();
    var typeName = item.ClrType.Name;

    var documentationScore = EvaluateDocumentationScore(operatorId, qualityContext);
    var testCoverageScore = EvaluateTestCoverageScore(operatorId, typeName, qualityContext);
    var parameterValidationScore = EvaluateParameterValidationScore(typeName, qualityContext);
    var errorHandlingScore = EvaluateErrorHandlingScore(typeName, qualityContext);

    var totalScore = (int)Math.Round(
        (documentationScore + testCoverageScore + parameterValidationScore + errorHandlingScore) / 4.0,
        MidpointRounding.AwayFromZero);

    return new CatalogQuality
    {
        TotalScore = totalScore,
        Level = MapQualityLevel(totalScore),
        DocumentationScore = documentationScore,
        TestCoverageScore = testCoverageScore,
        ParameterValidationScore = parameterValidationScore,
        ErrorHandlingScore = errorHandlingScore,
        Summary = BuildQualitySummary(documentationScore, testCoverageScore, parameterValidationScore, errorHandlingScore)
    };
}

static int EvaluateDocumentationScore(string operatorId, QualityContext qualityContext)
{
    var docPath = Path.Combine(qualityContext.DocsRoot, $"{operatorId}.md");
    if (!File.Exists(docPath))
    {
        return 0;
    }

    var content = File.ReadAllText(docPath);
    var placeholderTokens = new[]
    {
        "TODO",
        "TBD",
        "O(?)",
        "~?ms",
        "占位符"
    };

    var hasPlaceholder = placeholderTokens.Any(token =>
        content.Contains(token, StringComparison.OrdinalIgnoreCase));

    return hasPlaceholder ? 60 : 100;
}

static int EvaluateTestCoverageScore(string operatorId, string typeName, QualityContext qualityContext)
{
    return qualityContext.TestIndex.Contains(typeName) || qualityContext.TestIndex.Contains(operatorId)
        ? 100
        : 30;
}

static int EvaluateParameterValidationScore(string typeName, QualityContext qualityContext)
{
    if (!qualityContext.SourceTextByTypeName.TryGetValue(typeName, out var source))
    {
        return 0;
    }

    if (!source.Contains("ValidateParameters(", StringComparison.Ordinal))
    {
        return 0;
    }

    var hasInvalidBranch =
        source.Contains("ValidationResult.Invalid", StringComparison.Ordinal) ||
        source.Contains("Errors", StringComparison.Ordinal);

    if (hasInvalidBranch)
    {
        return 100;
    }

    var hasValid = source.Contains("ValidationResult.Valid", StringComparison.Ordinal);
    var hasConditional = Regex.IsMatch(source, @"\bif\s*\(", RegexOptions.CultureInvariant);

    if (hasValid && hasConditional)
    {
        return 80;
    }

    if (hasValid)
    {
        return 55;
    }

    return 50;
}

static int EvaluateErrorHandlingScore(string typeName, QualityContext qualityContext)
{
    if (!qualityContext.SourceTextByTypeName.TryGetValue(typeName, out var source))
    {
        return 0;
    }

    var hasTry = source.Contains("try", StringComparison.Ordinal);
    var hasCatch = source.Contains("catch", StringComparison.Ordinal);
    var failureCount = Regex.Matches(source, @"OperatorExecutionOutput\.Failure\s*\(", RegexOptions.CultureInvariant).Count;

    if (hasTry && hasCatch && failureCount >= 1)
    {
        return 100;
    }

    if (hasTry && hasCatch)
    {
        return 90;
    }

    if (failureCount >= 4)
    {
        return 85;
    }

    if (failureCount >= 2)
    {
        return 75;
    }

    if (failureCount >= 1)
    {
        return 60;
    }

    return 35;
}

static string MapQualityLevel(int score)
{
    return score switch
    {
        >= 85 => "A",
        >= 70 => "B",
        >= 55 => "C",
        _ => "D"
    };
}

static string BuildQualitySummary(int documentationScore, int testCoverageScore, int parameterValidationScore, int errorHandlingScore)
{
    return $"Doc={documentationScore}, Test={testCoverageScore}, Validation={parameterValidationScore}, ErrorHandling={errorHandlingScore}";
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

    public string Version { get; set; } = "1.0.0";

    public List<string> Tags { get; set; } = new();

    public string? Algorithm { get; set; }

    public List<CatalogPort> InputPorts { get; set; } = new();

    public List<CatalogPort> OutputPorts { get; set; } = new();

    public List<CatalogParameter> Parameters { get; set; } = new();

    public CatalogQuality Quality { get; set; } = new();

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

internal sealed class CatalogQuality
{
    public int TotalScore { get; set; }

    public string Level { get; set; } = string.Empty;

    public int DocumentationScore { get; set; }

    public int TestCoverageScore { get; set; }

    public int ParameterValidationScore { get; set; }

    public int ErrorHandlingScore { get; set; }

    public string Summary { get; set; } = string.Empty;
}

internal sealed class OperatorVersionHistoryDocument
{
    public string GeneratedAt { get; set; } = string.Empty;

    public List<OperatorVersionHistoryEntry> Operators { get; set; } = new();
}

internal sealed class OperatorVersionHistoryEntry
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public List<OperatorVersionRecord> Records { get; set; } = new();
}

internal sealed class OperatorVersionRecord
{
    public string Version { get; set; } = "1.0.0";

    public string SourceHash { get; set; } = string.Empty;

    public string RecordedAt { get; set; } = string.Empty;
}

internal sealed class VersionBumpViolation
{
    public string OperatorId { get; set; } = string.Empty;

    public string CurrentVersion { get; set; } = "1.0.0";
}

internal sealed record VersionTrackingResult(IReadOnlyList<VersionBumpViolation> Violations);

internal sealed record QualityContext(
    string RepoRoot,
    string DocsRoot,
    IReadOnlyDictionary<string, string> SourcePathByTypeName,
    IReadOnlyDictionary<string, string> SourceTextByTypeName,
    IReadOnlySet<string> TestIndex);

