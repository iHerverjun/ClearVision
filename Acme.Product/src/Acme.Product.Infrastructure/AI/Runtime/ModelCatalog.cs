using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acme.Product.Infrastructure.AI.Runtime;

public sealed class ModelCatalogDocument
{
    [JsonPropertyName("models")]
    public List<ModelCatalogEntry> Models { get; init; } = [];
}

public sealed class ModelCatalogEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("path")]
    public string ArtifactPath { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0.0";

    [JsonPropertyName("source")]
    public string? Source { get; init; }

    [JsonPropertyName("input_size")]
    public int[] InputSize { get; init; } = [];

    [JsonPropertyName("num_classes")]
    public int NumClasses { get; init; }

    [JsonPropertyName("class_names")]
    public string[] ClassNames { get; init; } = [];

    [JsonPropertyName("execution_provider")]
    public string ExecutionProvider { get; init; } = "cpu";
}

public sealed class ResolvedModelCatalogEntry
{
    public required ModelCatalogEntry Entry { get; init; }

    public required string CatalogPath { get; init; }

    public required string ArtifactPath { get; init; }
}

public static class ModelCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static string ResolveCatalogPath(string? catalogPath = null)
    {
        if (!string.IsNullOrWhiteSpace(catalogPath))
        {
            var resolved = Path.GetFullPath(catalogPath);
            if (!File.Exists(resolved))
            {
                throw new FileNotFoundException($"Model catalog not found: {resolved}", resolved);
            }

            return resolved;
        }

        foreach (var start in EnumerateCandidateRoots())
        {
            var resolved = SearchUpwards(start);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }
        }

        throw new FileNotFoundException("Model catalog not found. Expected models/model_catalog.json.");
    }

    public static ModelCatalogDocument Load(string? catalogPath = null)
    {
        var resolvedCatalogPath = ResolveCatalogPath(catalogPath);
        var json = File.ReadAllText(resolvedCatalogPath);
        var document = JsonSerializer.Deserialize<ModelCatalogDocument>(json, JsonOptions) ?? new ModelCatalogDocument();
        return new ModelCatalogDocument
        {
            Models = document.Models
                .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                .ToList()
        };
    }

    public static bool TryResolve(
        string? modelId,
        string? catalogPath,
        IReadOnlyCollection<string>? expectedTypes,
        out ResolvedModelCatalogEntry? resolved,
        out string? error)
    {
        resolved = null;
        error = null;

        if (string.IsNullOrWhiteSpace(modelId))
        {
            error = "ModelId is required.";
            return false;
        }

        ModelCatalogDocument document;
        string resolvedCatalogPath;
        try
        {
            resolvedCatalogPath = ResolveCatalogPath(catalogPath);
            document = Load(resolvedCatalogPath);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }

        var entry = document.Models.FirstOrDefault(x => string.Equals(x.Id, modelId, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
        {
            error = $"ModelId not found in catalog: {modelId}";
            return false;
        }

        if (expectedTypes != null && expectedTypes.Count > 0)
        {
            var expectedTypeSet = new HashSet<string>(expectedTypes.Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
            if (expectedTypeSet.Count > 0 && !expectedTypeSet.Contains(entry.Type))
            {
                error = $"Model '{modelId}' type '{entry.Type}' is not supported here. Expected: {string.Join(", ", expectedTypeSet)}";
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(entry.ArtifactPath))
        {
            error = $"Model '{modelId}' does not define a path in the catalog.";
            return false;
        }

        var artifactPath = ResolveArtifactPath(resolvedCatalogPath, entry.ArtifactPath);
        resolved = new ResolvedModelCatalogEntry
        {
            Entry = entry,
            CatalogPath = resolvedCatalogPath,
            ArtifactPath = artifactPath
        };
        return true;
    }

    public static string ResolveExplicitOrCatalogPath(
        string? explicitPath,
        string? modelId,
        string? catalogPath,
        IReadOnlyCollection<string>? expectedTypes,
        out ModelCatalogEntry? entry)
    {
        entry = null;

        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(explicitPath);
        }

        if (!TryResolve(modelId, catalogPath, expectedTypes, out var resolved, out var error) || resolved == null)
        {
            throw new InvalidOperationException(error ?? "Unable to resolve model path.");
        }

        entry = resolved.Entry;
        return resolved.ArtifactPath;
    }

    private static IEnumerable<string> EnumerateCandidateRoots()
    {
        yield return Directory.GetCurrentDirectory();
        yield return AppContext.BaseDirectory;

        var entryAssemblyLocation = System.Reflection.Assembly.GetEntryAssembly()?.Location;
        if (!string.IsNullOrWhiteSpace(entryAssemblyLocation))
        {
            yield return Path.GetDirectoryName(entryAssemblyLocation)!;
        }
    }

    private static string? SearchUpwards(string startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath))
        {
            return null;
        }

        var directory = new DirectoryInfo(Path.GetFullPath(startPath));
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "models", "model_catalog.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string ResolveArtifactPath(string catalogPath, string artifactPath)
    {
        if (Path.IsPathRooted(artifactPath))
        {
            return artifactPath;
        }

        var catalogDirectory = Path.GetDirectoryName(catalogPath) ?? Directory.GetCurrentDirectory();
        var repoRoot = Directory.GetParent(catalogDirectory)?.FullName ?? catalogDirectory;
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(catalogDirectory, artifactPath)),
            Path.GetFullPath(Path.Combine(repoRoot, artifactPath))
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[1];
    }
}
