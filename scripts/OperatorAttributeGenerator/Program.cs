using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Services;

var repoRoot = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
var apply = args.Length > 1 && string.Equals(args[1], "--apply", StringComparison.OrdinalIgnoreCase);

var operatorsRoot = Path.Combine(repoRoot, "Acme.Product", "src", "Acme.Product.Infrastructure", "Operators");
if (!Directory.Exists(operatorsRoot))
{
    Console.Error.WriteLine($"Operators root not found: {operatorsRoot}");
    return 2;
}

var metadataByType = new OperatorFactory()
    .GetAllMetadata()
    .GroupBy(m => m.Type)
    .ToDictionary(g => g.Key, g => g.First());

var files = Directory.GetFiles(operatorsRoot, "*.cs", SearchOption.AllDirectories);
var operatorTypeRegex = new Regex(@"public\s+override\s+OperatorType\s+OperatorType\s*=>\s*OperatorType\.(?<type>\w+)\s*;", RegexOptions.Compiled);
var classRegex = new Regex(@"(?m)^public\s+(?:sealed\s+|abstract\s+)?class\s+\w+\b", RegexOptions.Compiled);
var hasMetaRegex = new Regex(@"\[OperatorMeta\(", RegexOptions.Compiled);

var updated = 0;
var already = 0;
var noOperatorType = 0;
var noClass = 0;
var noMetadata = 0;
var noChange = 0;
var missingTypes = new HashSet<string>(StringComparer.Ordinal);

foreach (var file in files)
{
    var bytes = File.ReadAllBytes(file);
    var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
    var text = File.ReadAllText(file);

    if (hasMetaRegex.IsMatch(text))
    {
        already++;
        continue;
    }

    var typeMatch = operatorTypeRegex.Match(text);
    if (!typeMatch.Success)
    {
        noOperatorType++;
        continue;
    }

    var typeName = typeMatch.Groups["type"].Value;
    if (!Enum.TryParse<OperatorType>(typeName, out var operatorType))
    {
        noMetadata++;
        missingTypes.Add(typeName + "(enum-parse-failed)");
        continue;
    }

    if (!metadataByType.TryGetValue(operatorType, out var metadata))
    {
        noMetadata++;
        missingTypes.Add(typeName);
        continue;
    }

    var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
    var newText = text;

    if (!newText.Contains("using Acme.Product.Core.Attributes;", StringComparison.Ordinal))
    {
        newText = InsertUsing(newText, "using Acme.Product.Core.Attributes;", newline);
    }

    var classMatch = classRegex.Match(newText);
    if (!classMatch.Success)
    {
        noClass++;
        continue;
    }

    var attributeBlock = BuildAttributeBlock(metadata, newline);
    newText = newText.Insert(classMatch.Index, attributeBlock + newline);

    if (string.Equals(newText, text, StringComparison.Ordinal))
    {
        noChange++;
        continue;
    }

    if (apply)
    {
        var encoding = new UTF8Encoding(hasBom);
        File.WriteAllText(file, newText, encoding);
    }

    updated++;
}

Console.WriteLine($"apply={apply} files={files.Length} updated={updated} already={already} noOperatorType={noOperatorType} noClass={noClass} noMetadata={noMetadata} noChange={noChange}");
if (missingTypes.Count > 0)
{
    Console.WriteLine("missingTypes=" + string.Join(", ", missingTypes.OrderBy(x => x)));
}

return 0;

static string InsertUsing(string text, string usingLine, string newline)
{
    var usingRegex = new Regex(@"(?m)^using\s+[^\r\n;]+;\s*$");
    var matches = usingRegex.Matches(text);
    if (matches.Count > 0)
    {
        var last = matches[^1];
        return text.Insert(last.Index + last.Length, newline + usingLine);
    }

    var namespaceRegex = new Regex(@"(?m)^namespace\s+");
    var namespaceMatch = namespaceRegex.Match(text);
    if (namespaceMatch.Success)
    {
        return text.Insert(namespaceMatch.Index, usingLine + newline + newline);
    }

    return usingLine + newline + newline + text;
}

static string BuildAttributeBlock(OperatorMetadata metadata, string newline)
{
    var lines = new List<string>();

    var metaProps = new List<string>
    {
        $"DisplayName = {ToStringLiteral(metadata.DisplayName)}",
        $"Description = {ToStringLiteral(metadata.Description)}",
        $"Category = {ToStringLiteral(metadata.Category)}"
    };

    if (!string.IsNullOrWhiteSpace(metadata.IconName))
    {
        metaProps.Add($"IconName = {ToStringLiteral(metadata.IconName!)}");
    }

    if (metadata.Keywords is { Length: > 0 })
    {
        metaProps.Add($"Keywords = new[] {{ {string.Join(", ", metadata.Keywords.Select(ToStringLiteral))} }}");
    }

    lines.Add("[OperatorMeta(");
    for (var i = 0; i < metaProps.Count; i++)
    {
        var suffix = i == metaProps.Count - 1 ? string.Empty : ",";
        lines.Add($"    {metaProps[i]}{suffix}");
    }

    lines.Add(")]");

    foreach (var input in metadata.InputPorts)
    {
        lines.Add($"[InputPort({ToStringLiteral(input.Name)}, {ToStringLiteral(input.DisplayName)}, PortDataType.{input.DataType}, IsRequired = {ToBoolLiteral(input.IsRequired)})]");
    }

    foreach (var output in metadata.OutputPorts)
    {
        lines.Add($"[OutputPort({ToStringLiteral(output.Name)}, {ToStringLiteral(output.DisplayName)}, PortDataType.{output.DataType})]");
    }

    foreach (var parameter in metadata.Parameters)
    {
        var namedArgs = new List<string>();

        if (!string.IsNullOrWhiteSpace(parameter.Description))
        {
            namedArgs.Add($"Description = {ToStringLiteral(parameter.Description!)}");
        }

        if (TryFormatLiteral(parameter.DefaultValue, out var defaultLiteral))
        {
            namedArgs.Add($"DefaultValue = {defaultLiteral}");
        }

        if (TryFormatLiteral(parameter.MinValue, out var minLiteral))
        {
            namedArgs.Add($"Min = {minLiteral}");
        }

        if (TryFormatLiteral(parameter.MaxValue, out var maxLiteral))
        {
            namedArgs.Add($"Max = {maxLiteral}");
        }

        if (!parameter.IsRequired)
        {
            namedArgs.Add("IsRequired = false");
        }

        if (parameter.Options is { Count: > 0 })
        {
            var optionLiterals = parameter.Options.Select(option => ToStringLiteral($"{option.Value}|{option.Label}"));
            namedArgs.Add($"Options = new[] {{ {string.Join(", ", optionLiterals)} }}");
        }

        var head = $"[OperatorParam({ToStringLiteral(parameter.Name)}, {ToStringLiteral(parameter.DisplayName)}, {ToStringLiteral(parameter.DataType)}";
        var tail = namedArgs.Count == 0 ? ")]" : $", {string.Join(", ", namedArgs)})]";
        lines.Add(head + tail);
    }

    return string.Join(newline, lines);
}

static bool TryFormatLiteral(object? value, out string literal)
{
    if (value == null)
    {
        literal = string.Empty;
        return false;
    }

    switch (value)
    {
        case string s:
            literal = ToStringLiteral(s);
            return true;
        case bool b:
            literal = ToBoolLiteral(b);
            return true;
        case byte or sbyte or short or ushort or int or uint:
            literal = Convert.ToString(value, CultureInfo.InvariantCulture)!;
            return true;
        case long l:
            literal = l.ToString(CultureInfo.InvariantCulture) + "L";
            return true;
        case ulong ul:
            literal = ul.ToString(CultureInfo.InvariantCulture) + "UL";
            return true;
        case float f:
            literal = f.ToString("R", CultureInfo.InvariantCulture) + "f";
            return true;
        case double d:
            var ds = d.ToString("R", CultureInfo.InvariantCulture);
            if (!ds.Contains('.', StringComparison.Ordinal) && !ds.Contains('E', StringComparison.OrdinalIgnoreCase))
            {
                ds += ".0";
            }
            literal = ds;
            return true;
        case decimal m:
            literal = m.ToString(CultureInfo.InvariantCulture) + "m";
            return true;
        default:
            literal = ToStringLiteral(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
            return true;
    }
}

static string ToBoolLiteral(bool value) => value ? "true" : "false";

static string ToStringLiteral(string value)
{
    var normalized = value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\r", " ", StringComparison.Ordinal)
        .Replace("\n", " ", StringComparison.Ordinal);
    return $"\"{normalized}\"";
}
