// OperatorMetadataScanner.cs
// 算子元数据扫描器
// 通过反射扫描算子定义并构建元数据索引
// 作者：蘅芜君
using System.Reflection;
using System.Runtime.CompilerServices;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// Scans operator classes decorated by metadata attributes and converts them
/// to the runtime <see cref="OperatorMetadata"/> model.
/// </summary>
public class OperatorMetadataScanner
{
    private readonly ILogger<OperatorMetadataScanner>? _logger;

    public OperatorMetadataScanner(ILogger<OperatorMetadataScanner>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Scan the infrastructure operator assembly.
    /// </summary>
    public List<OperatorMetadata> Scan()
    {
        return Scan(new[] { typeof(OperatorBase).Assembly });
    }

    /// <summary>
    /// Scan one assembly for operator metadata.
    /// </summary>
    public List<OperatorMetadata> Scan(Assembly assembly)
    {
        if (assembly == null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }

        return Scan(new[] { assembly });
    }

    /// <summary>
    /// Scan multiple assemblies for operator metadata.
    /// </summary>
    public List<OperatorMetadata> Scan(IEnumerable<Assembly> assemblies)
    {
        if (assemblies == null)
        {
            throw new ArgumentNullException(nameof(assemblies));
        }

        var metadataByType = new Dictionary<OperatorType, OperatorMetadata>();
        foreach (var assembly in assemblies.Where(a => a != null).Distinct())
        {
            foreach (var operatorClrType in GetCandidateOperatorTypes(assembly))
            {
                var metadata = TryBuildMetadata(operatorClrType);
                if (metadata == null)
                {
                    continue;
                }

                if (!metadataByType.TryAdd(metadata.Type, metadata))
                {
                    _logger?.LogWarning(
                        "Duplicate operator metadata scanned for {OperatorType}. Existing={ExistingType}, Ignored={IgnoredType}",
                        metadata.Type,
                        metadataByType[metadata.Type].DisplayName,
                        operatorClrType.FullName);
                }
            }
        }

        return metadataByType.Values
            .OrderBy(m => m.Type)
            .ToList();
    }

    private static IEnumerable<Type> GetCandidateOperatorTypes(Assembly assembly)
    {
        return GetLoadableTypes(assembly)
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => typeof(OperatorBase).IsAssignableFrom(t))
            .Where(t => t.GetCustomAttribute<OperatorMetaAttribute>(inherit: false) != null);
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }

    private OperatorMetadata? TryBuildMetadata(Type operatorClrType)
    {
        var operatorMeta = operatorClrType.GetCustomAttribute<OperatorMetaAttribute>(inherit: false);
        if (operatorMeta == null)
        {
            return null;
        }

        if (!TryResolveOperatorType(operatorClrType, out var operatorType))
        {
            _logger?.LogWarning(
                "Cannot resolve OperatorType from {OperatorClass}; skipped metadata scan.",
                operatorClrType.FullName);
            return null;
        }

        var metadata = new OperatorMetadata
        {
            Type = operatorType,
            DisplayName = operatorMeta.DisplayName,
            Description = operatorMeta.Description,
            Category = operatorMeta.Category,
            IconName = operatorMeta.IconName,
            Keywords = operatorMeta.Keywords,
            Tags = operatorMeta.Tags,
            Version = string.IsNullOrWhiteSpace(operatorMeta.Version) ? "1.0.0" : operatorMeta.Version.Trim(),
            InputPorts = operatorClrType
                .GetCustomAttributes<InputPortAttribute>(inherit: false)
                .Select(attr => new PortDefinition
                {
                    Name = attr.Name,
                    DisplayName = attr.DisplayName,
                    DataType = attr.DataType,
                    IsRequired = attr.IsRequired
                })
                .ToList(),
            OutputPorts = operatorClrType
                .GetCustomAttributes<OutputPortAttribute>(inherit: false)
                .Select(attr => new PortDefinition
                {
                    Name = attr.Name,
                    DisplayName = attr.DisplayName,
                    DataType = attr.DataType
                })
                .ToList(),
            Parameters = operatorClrType
                .GetCustomAttributes<OperatorParamAttribute>(inherit: false)
                .Select(attr => new ParameterDefinition
                {
                    Name = attr.Name,
                    DisplayName = attr.DisplayName,
                    Description = attr.Description,
                    DataType = attr.DataType,
                    DefaultValue = attr.DefaultValue,
                    MinValue = attr.Min,
                    MaxValue = attr.Max,
                    IsRequired = attr.IsRequired,
                    Options = BuildOptions(attr.Options)
                })
                .ToList()
        };

        return metadata;
    }

    private static bool TryResolveOperatorType(Type operatorClrType, out OperatorType operatorType)
    {
        operatorType = default;

        var property = operatorClrType.GetProperty(nameof(OperatorBase.OperatorType), BindingFlags.Public | BindingFlags.Instance);
        if (property?.PropertyType == typeof(OperatorType) && property.GetMethod != null)
        {
            try
            {
                var uninitialized = RuntimeHelpers.GetUninitializedObject(operatorClrType);
                var value = property.GetValue(uninitialized);
                if (value is OperatorType resolvedType)
                {
                    operatorType = resolvedType;
                    return true;
                }
            }
            catch
            {
                // Fall back to class-name parsing.
            }
        }

        const string suffix = "Operator";
        var className = operatorClrType.Name;
        if (className.EndsWith(suffix, StringComparison.Ordinal))
        {
            className = className[..^suffix.Length];
        }

        return Enum.TryParse(className, out operatorType);
    }

    private static List<ParameterOption>? BuildOptions(string[]? options)
    {
        if (options == null || options.Length == 0)
        {
            return null;
        }

        var result = new List<ParameterOption>(options.Length);
        foreach (var option in options)
        {
            if (string.IsNullOrWhiteSpace(option))
            {
                continue;
            }

            var parts = option.Split('|', 2, StringSplitOptions.TrimEntries);
            var value = parts[0];
            var label = parts.Length > 1 ? parts[1] : parts[0];

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            result.Add(new ParameterOption
            {
                Value = value,
                Label = string.IsNullOrWhiteSpace(label) ? value : label
            });
        }

        return result.Count == 0 ? null : result;
    }
}
