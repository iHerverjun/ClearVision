namespace Acme.Product.Infrastructure.Services;

internal static class DeepLearningLabelResolver
{
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

        if (ParseNamedTargetClasses(targetClassesStr).Length == 0)
        {
            resolvedPath = null;
            return true;
        }

        resolvedPath = TryResolveBundledLabelsPath(targetClassesStr);
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

            var labels = File.ReadAllLines(candidate)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();
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
