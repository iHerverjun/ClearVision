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
    private static readonly IReadOnlyDictionary<OperatorType, OperatorType> LegacyAliasMap =
        new Dictionary<OperatorType, OperatorType>
        {
            [OperatorType.Preprocessing] = OperatorType.Filtering,
            [OperatorType.GaussianBlur] = OperatorType.Filtering,
            [OperatorType.OnnxInference] = OperatorType.DeepLearning,
            [OperatorType.ModbusRtuCommunication] = OperatorType.ModbusCommunication
        };

    public static bool IsLegacyAlias(OperatorType type) => LegacyAliasMap.ContainsKey(type);

    public static void Apply(Dictionary<OperatorType, OperatorMetadata> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

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

        // Attribute metadata is the single source of truth after migration.
        foreach (var attributeMetadata in scannedMetadata)
        {
            metadata[attributeMetadata.Type] = attributeMetadata;
        }

        // Keep UI-facing names/categories aligned with pre-migration Chinese catalog.
        OperatorMetadataLocalization.Apply(metadata.Values);

        ApplyLegacyAliases(metadata);

        Trace.TraceInformation($"[OperatorFactory] Loaded {metadata.Count} metadata items from attribute scan.");
    }

    private static void ApplyLegacyAliases(Dictionary<OperatorType, OperatorMetadata> metadata)
    {
        foreach (var (legacyType, mappedType) in LegacyAliasMap)
        {
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
