using System.Diagnostics;
using System.Globalization;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using Acme.Product.Core.ValueObjects;

namespace Acme.Product.Infrastructure.Services;

internal static class OperatorFactoryMetadataMerge
{
    public static void Apply(Dictionary<OperatorType, OperatorMetadata> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var hardcodedTypes = metadata.Keys.ToHashSet();
        List<OperatorMetadata> scannedMetadata;

        try
        {
            scannedMetadata = new OperatorMetadataScanner().Scan();
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[OperatorFactory] Attribute metadata scan failed: {ex.Message}");
            return;
        }

        var attributeByType = scannedMetadata
            .GroupBy(m => m.Type)
            .ToDictionary(group => group.Key, group => group.First());

        ValidateDifferences(metadata, hardcodedTypes, attributeByType);

        foreach (var attributeMetadata in scannedMetadata)
        {
            if (metadata.ContainsKey(attributeMetadata.Type))
            {
                continue;
            }

            metadata[attributeMetadata.Type] = attributeMetadata;
        }

#if DEBUG
        LogMetadataSource(metadata.Keys, hardcodedTypes, attributeByType.Keys.ToHashSet());
#endif
    }

    private static void ValidateDifferences(
        IReadOnlyDictionary<OperatorType, OperatorMetadata> hardcodedMetadata,
        HashSet<OperatorType> hardcodedTypes,
        IReadOnlyDictionary<OperatorType, OperatorMetadata> attributeByType)
    {
        foreach (var operatorType in hardcodedTypes.OrderBy(type => type))
        {
            if (!attributeByType.TryGetValue(operatorType, out var attributeMetadata))
            {
                continue;
            }

            var hardcoded = hardcodedMetadata[operatorType];
            var differences = CollectDifferences(hardcoded, attributeMetadata);
            if (differences.Count == 0)
            {
                continue;
            }

            Trace.TraceWarning(
                $"[OperatorFactory] Metadata mismatch for {operatorType}. Hardcode wins. Differences: {string.Join(", ", differences)}");
        }
    }

#if DEBUG
    private static void LogMetadataSource(
        IEnumerable<OperatorType> mergedTypes,
        HashSet<OperatorType> hardcodedTypes,
        HashSet<OperatorType> attributeTypes)
    {
        foreach (var operatorType in mergedTypes.OrderBy(type => type))
        {
            var source = hardcodedTypes.Contains(operatorType)
                ? attributeTypes.Contains(operatorType)
                    ? "Hardcode (attribute ignored)"
                    : "Hardcode"
                : "Attribute";

            Debug.WriteLine($"[OperatorFactory] Metadata source: {operatorType} => {source}");
        }
    }
#endif

    private static List<string> CollectDifferences(OperatorMetadata hardcoded, OperatorMetadata attribute)
    {
        var differences = new List<string>();

        if (!TextEqual(hardcoded.DisplayName, attribute.DisplayName))
        {
            differences.Add(nameof(OperatorMetadata.DisplayName));
        }

        if (!TextEqual(hardcoded.Description, attribute.Description))
        {
            differences.Add(nameof(OperatorMetadata.Description));
        }

        if (!TextEqual(hardcoded.Category, attribute.Category))
        {
            differences.Add(nameof(OperatorMetadata.Category));
        }

        if (!TextEqual(hardcoded.IconName, attribute.IconName))
        {
            differences.Add(nameof(OperatorMetadata.IconName));
        }

        if (!StringArrayEqual(hardcoded.Keywords, attribute.Keywords))
        {
            differences.Add(nameof(OperatorMetadata.Keywords));
        }

        if (!PortListEqual(hardcoded.InputPorts, attribute.InputPorts))
        {
            differences.Add(nameof(OperatorMetadata.InputPorts));
        }

        if (!PortListEqual(hardcoded.OutputPorts, attribute.OutputPorts))
        {
            differences.Add(nameof(OperatorMetadata.OutputPorts));
        }

        if (!ParameterListEqual(hardcoded.Parameters, attribute.Parameters))
        {
            differences.Add(nameof(OperatorMetadata.Parameters));
        }

        return differences;
    }

    private static bool PortListEqual(IReadOnlyList<PortDefinition> left, IReadOnlyList<PortDefinition> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            var x = left[i];
            var y = right[i];

            if (!TextEqual(x.Name, y.Name))
            {
                return false;
            }

            if (!TextEqual(x.DisplayName, y.DisplayName))
            {
                return false;
            }

            if (x.DataType != y.DataType)
            {
                return false;
            }

            if (x.IsRequired != y.IsRequired)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ParameterListEqual(IReadOnlyList<ParameterDefinition> left, IReadOnlyList<ParameterDefinition> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            var x = left[i];
            var y = right[i];

            if (!TextEqual(x.Name, y.Name))
            {
                return false;
            }

            if (!TextEqual(x.DisplayName, y.DisplayName))
            {
                return false;
            }

            if (!TextEqual(x.Description, y.Description))
            {
                return false;
            }

            if (!TextEqual(x.DataType, y.DataType))
            {
                return false;
            }

            if (!ValueEqual(x.DefaultValue, y.DefaultValue))
            {
                return false;
            }

            if (!ValueEqual(x.MinValue, y.MinValue))
            {
                return false;
            }

            if (!ValueEqual(x.MaxValue, y.MaxValue))
            {
                return false;
            }

            if (x.IsRequired != y.IsRequired)
            {
                return false;
            }

            if (!ParameterOptionListEqual(x.Options, y.Options))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ParameterOptionListEqual(IReadOnlyList<ParameterOption>? left, IReadOnlyList<ParameterOption>? right)
    {
        var leftOptions = left ?? Array.Empty<ParameterOption>();
        var rightOptions = right ?? Array.Empty<ParameterOption>();

        if (leftOptions.Count != rightOptions.Count)
        {
            return false;
        }

        for (var i = 0; i < leftOptions.Count; i++)
        {
            if (!TextEqual(leftOptions[i].Label, rightOptions[i].Label))
            {
                return false;
            }

            if (!TextEqual(leftOptions[i].Value, rightOptions[i].Value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool StringArrayEqual(string[]? left, string[]? right)
    {
        return (left ?? Array.Empty<string>())
            .SequenceEqual(right ?? Array.Empty<string>(), StringComparer.Ordinal);
    }

    private static bool TextEqual(string? left, string? right)
    {
        return string.Equals(
            left?.Trim() ?? string.Empty,
            right?.Trim() ?? string.Empty,
            StringComparison.Ordinal);
    }

    private static bool ValueEqual(object? left, object? right)
    {
        return string.Equals(
            NormalizeValue(left),
            NormalizeValue(right),
            StringComparison.Ordinal);
    }

    private static string NormalizeValue(object? value)
    {
        if (value == null)
        {
            return "<null>";
        }

        if (value is string text)
        {
            return text.Trim();
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        return value.ToString() ?? string.Empty;
    }
}
