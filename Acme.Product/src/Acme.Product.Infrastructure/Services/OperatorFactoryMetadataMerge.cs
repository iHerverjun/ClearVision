// OperatorFactoryMetadataMerge.cs
// 算子元数据合并器
// 合并扫描结果与运行时元数据配置
// 作者：蘅芜君
using System.Diagnostics;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using Acme.Product.Core.ValueObjects;

namespace Acme.Product.Infrastructure.Services;

internal static class OperatorFactoryMetadataMerge
{
    private const string StrictMetadataScanSwitchName = "Acme.Product.OperatorFactory.StrictMetadataScan";
    private const string StrictMetadataScanEnvironmentVariable = "ACME_OPERATOR_FACTORY_STRICT_METADATA_SCAN";

    public static bool IsLegacyAlias(OperatorType type) => OperatorTypeAliasResolver.IsLegacyAlias(type);

    public static OperatorType ResolveExecutionType(OperatorType type)
    {
        return OperatorTypeAliasResolver.Resolve(type);
    }

    public static bool IsStrictMetadataScanEnabled(bool? strictModeOverride = null)
    {
        if (strictModeOverride.HasValue)
        {
            return strictModeOverride.Value;
        }

        if (AppContext.TryGetSwitch(StrictMetadataScanSwitchName, out var strictBySwitch))
        {
            return strictBySwitch;
        }

        var raw = Environment.GetEnvironmentVariable(StrictMetadataScanEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (bool.TryParse(raw, out var strictByEnvironment))
        {
            return strictByEnvironment;
        }

        if (string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(raw, "0", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    public static void Apply(
        Dictionary<OperatorType, OperatorMetadata> metadata,
        Func<List<OperatorMetadata>>? scanMetadata = null,
        bool? strictModeOverride = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var strictMode = IsStrictMetadataScanEnabled(strictModeOverride);
        var scanner = scanMetadata ?? (() => new OperatorMetadataScanner().Scan());

        List<OperatorMetadata> scannedMetadata;
        try
        {
            scannedMetadata = scanner();
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[OperatorFactory] Attribute metadata scan failed. StrictMode={strictMode}. Error={ex}");
            if (strictMode)
            {
                throw new InvalidOperationException(
                    "[OperatorFactory] Metadata initialization failed. " +
                    $"Set {StrictMetadataScanEnvironmentVariable}=false to allow degraded startup.",
                    ex);
            }

            return;
        }

        if (scannedMetadata.Count == 0)
        {
            Trace.TraceError($"[OperatorFactory] Attribute metadata scan produced 0 items. StrictMode={strictMode}.");
            if (strictMode)
            {
                throw new InvalidOperationException("[OperatorFactory] Metadata initialization failed because scan returned no metadata.");
            }

            return;
        }

        // Attribute metadata is the single source of truth after migration.
        foreach (var attributeMetadata in scannedMetadata)
        {
            metadata[attributeMetadata.Type] = attributeMetadata;
        }

        // Keep UI-facing names/categories aligned with pre-migration Chinese catalog.
        OperatorMetadataLocalization.Apply(metadata.Values);
        OperatorMetadataTextLocalization.Apply(metadata.Values);

        ApplyLegacyAliases(metadata);

        Trace.TraceInformation($"[OperatorFactory] Loaded {metadata.Count} metadata items from attribute scan.");
    }

    private static void ApplyLegacyAliases(Dictionary<OperatorType, OperatorMetadata> metadata)
    {
        foreach (var legacyType in Enum.GetValues<OperatorType>().Where(OperatorTypeAliasResolver.IsLegacyAlias))
        {
            var mappedType = OperatorTypeAliasResolver.Resolve(legacyType);
            if (metadata.ContainsKey(legacyType))
            {
                continue;
            }

            if (!metadata.TryGetValue(mappedType, out var mappedMetadata))
            {
                continue;
            }

            metadata[legacyType] = CloneMetadata(mappedMetadata, legacyType);
        }
    }

    private static OperatorMetadata CloneMetadata(OperatorMetadata source, OperatorType aliasType)
    {
        return new OperatorMetadata
        {
            Type = aliasType,
            DisplayName = source.DisplayName,
            Description = source.Description,
            Category = source.Category,
            IconName = source.IconName,
            Keywords = source.Keywords?.ToArray(),
            Tags = source.Tags?.ToArray(),
            Version = source.Version,
            InputPorts = source.InputPorts
                .Select(port => new PortDefinition
                {
                    Name = port.Name,
                    DisplayName = port.DisplayName,
                    DataType = port.DataType,
                    IsRequired = port.IsRequired,
                    Description = port.Description
                })
                .ToList(),
            OutputPorts = source.OutputPorts
                .Select(port => new PortDefinition
                {
                    Name = port.Name,
                    DisplayName = port.DisplayName,
                    DataType = port.DataType,
                    IsRequired = port.IsRequired,
                    Description = port.Description
                })
                .ToList(),
            Parameters = source.Parameters
                .Select(parameter => new ParameterDefinition
                {
                    Name = parameter.Name,
                    DisplayName = parameter.DisplayName,
                    Description = parameter.Description,
                    DataType = parameter.DataType,
                    DefaultValue = parameter.DefaultValue,
                    MinValue = parameter.MinValue,
                    MaxValue = parameter.MaxValue,
                    IsRequired = parameter.IsRequired,
                    Options = parameter.Options?
                        .Select(option => new ParameterOption
                        {
                            Label = option.Label,
                            Value = option.Value
                        })
                        .ToList()
                })
                .ToList()
        };
    }
}
