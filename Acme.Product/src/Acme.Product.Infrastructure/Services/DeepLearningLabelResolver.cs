using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.ML.OnnxRuntime;

namespace Acme.Product.Infrastructure.Services;

internal static class DeepLearningLabelResolver
{
    private static readonly Regex MetadataNamePairRegex = new(
        @"['""]?(?<key>\d+)['""]?\s*:\s*(?<quote>['""])(?<value>.*?)\k<quote>",
        RegexOptions.Compiled);

    public static bool AreLabelsResolvable(
        string? explicitLabelPath,
        string? modelPath,
        string targetClassesStr,
        out string? resolvedPath)
    {
        if (TryResolveExistingLabelPath(explicitLabelPath, modelPath, out resolvedPath))
        {
            return true;
        }

        var requiredLabels = ParseNamedTargetClasses(targetClassesStr);
        if (requiredLabels.Length == 0)
        {
            resolvedPath = null;
            return true;
        }

        if (TryLoadMetadataLabels(modelPath, out var metadataLabels) &&
            requiredLabels.All(requiredLabel =>
                metadataLabels.Any(label => string.Equals(label, requiredLabel, StringComparison.OrdinalIgnoreCase))))
        {
            resolvedPath = null;
            return true;
        }

        resolvedPath = TryResolveBundledLabelsPath(requiredLabels);
        return !string.IsNullOrWhiteSpace(resolvedPath);
    }

    public static string? TryResolveBundledLabelsPath(string targetClassesStr)
    {
        return TryResolveBundledLabelsPath(ParseNamedTargetClasses(targetClassesStr));
    }

    public static string? TryResolveBundledLabelsPath(IEnumerable<string> requiredLabels)
    {
        var required = requiredLabels
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var candidate in EnumerateBundledLabelCandidates())
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            if (required.Length == 0)
            {
                return candidate;
            }

            var labels = ReadLabelsFromFile(candidate);
            if (required.All(requiredLabel =>
                    labels.Any(label => string.Equals(label, requiredLabel, StringComparison.OrdinalIgnoreCase))))
            {
                return candidate;
            }
        }

        return null;
    }

    public static string[] ParseNamedTargetClasses(string targetClassesStr)
    {
        if (string.IsNullOrWhiteSpace(targetClassesStr))
        {
            return Array.Empty<string>();
        }

        return targetClassesStr
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(label => !string.IsNullOrWhiteSpace(label) && !int.TryParse(label, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string[] GetMetadataLabels(InferenceSession session)
    {
        if (session == null)
        {
            return Array.Empty<string>();
        }

        try
        {
            return ExtractMetadataLabels(session.ModelMetadata?.CustomMetadataMap);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public static bool TryLoadMetadataLabels(string? modelPath, out string[] labels)
    {
        labels = Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
        {
            return false;
        }

        try
        {
            using var session = new InferenceSession(modelPath);
            labels = GetMetadataLabels(session);
            return labels.Length > 0;
        }
        catch
        {
            labels = Array.Empty<string>();
            return false;
        }
    }

    public static string[] ExtractMetadataLabels(IReadOnlyDictionary<string, string>? customMetadataMap)
    {
        if (customMetadataMap == null ||
            !customMetadataMap.TryGetValue("names", out var rawNames))
        {
            return Array.Empty<string>();
        }

        return ParseMetadataNames(rawNames);
    }

    public static string[] ParseMetadataNames(string? rawNames)
    {
        if (string.IsNullOrWhiteSpace(rawNames))
        {
            return Array.Empty<string>();
        }

        var trimmed = rawNames.Trim();

        if (trimmed.StartsWith('['))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<string[]>(trimmed);
                if (parsed != null)
                {
                    return parsed.Where(label => !string.IsNullOrWhiteSpace(label)).ToArray();
                }
            }
            catch
            {
                // Fall through to regex-based parsing.
            }
        }

        if (trimmed.StartsWith('{'))
        {
            try
            {
                using var document = JsonDocument.Parse(trimmed);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    return document.RootElement
                        .EnumerateObject()
                        .Where(property => int.TryParse(property.Name, out _) && property.Value.ValueKind == JsonValueKind.String)
                        .OrderBy(property => int.Parse(property.Name))
                        .Select(property => property.Value.GetString())
                        .Where(label => !string.IsNullOrWhiteSpace(label))
                        .Cast<string>()
                        .ToArray();
                }
            }
            catch
            {
                // Fall through to regex-based parsing for Ultralytics metadata.
            }
        }

        var matches = MetadataNamePairRegex.Matches(trimmed);
        if (matches.Count == 0)
        {
            return Array.Empty<string>();
        }

        return matches
            .Cast<Match>()
            .Where(match => match.Success)
            .Select(match => new
            {
                Key = int.Parse(match.Groups["key"].Value),
                Value = match.Groups["value"].Value.Trim()
            })
            .OrderBy(item => item.Key)
            .Select(item => item.Value)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToArray();
    }

    public static string[] ReadLabelsFromFile(string labelPath)
    {
        return File.ReadAllLines(labelPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }

    private static bool TryResolveExistingLabelPath(
        string? explicitLabelPath,
        string? modelPath,
        out string? resolvedPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitLabelPath) && File.Exists(explicitLabelPath))
        {
            resolvedPath = explicitLabelPath;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(modelPath))
        {
            var modelDir = Path.GetDirectoryName(modelPath);
            if (!string.IsNullOrWhiteSpace(modelDir))
            {
                var autoLabelFile = Path.Combine(modelDir, "labels.txt");
                if (File.Exists(autoLabelFile))
                {
                    resolvedPath = autoLabelFile;
                    return true;
                }
            }
        }

        resolvedPath = null;
        return false;
    }

    private static IEnumerable<string> EnumerateBundledLabelCandidates()
    {
        foreach (var root in EnumerateSearchRoots())
        {
            yield return Path.Combine(root, "线序检测", "scenario-package-wire-sequence", "labels", "labels.txt");
            yield return Path.Combine(root, "scenario-package-wire-sequence", "labels", "labels.txt");
        }
    }

    private static IEnumerable<string> EnumerateSearchRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            if (string.IsNullOrWhiteSpace(start))
            {
                continue;
            }

            var current = new DirectoryInfo(Path.GetFullPath(start));
            while (current != null && seen.Add(current.FullName))
            {
                yield return current.FullName;
                current = current.Parent;
            }
        }
    }
}
