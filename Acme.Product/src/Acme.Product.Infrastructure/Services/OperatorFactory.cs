using System.Diagnostics;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using Acme.Product.Core.ValueObjects;

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// Creates operator instances and exposes metadata catalog.
/// Metadata is sourced from attribute scanning via <see cref="OperatorFactoryMetadataMerge"/>.
/// </summary>
public class OperatorFactory : IOperatorFactory
{
    private readonly Dictionary<OperatorType, OperatorMetadata> _metadata = new();
    private readonly Func<List<OperatorMetadata>>? _scanMetadata;
    private readonly bool? _strictMetadataScan;

    public OperatorFactory(
        Func<List<OperatorMetadata>>? scanMetadata = null,
        bool? strictMetadataScan = null)
    {
        _scanMetadata = scanMetadata;
        _strictMetadataScan = strictMetadataScan;
        InitializeDefaultOperators();
    }

    public Operator CreateOperator(OperatorType type, string name, double x, double y)
    {
        var op = new Operator(name, type, x, y);
        var metadata = GetMetadata(type);

        if (metadata == null)
        {
            var message =
                $"[OperatorFactory] Metadata missing for operator type '{type}'. " +
                "Operator creation aborted to prevent fail-open Any-port fallback.";
            Trace.TraceError(message);
            throw new InvalidOperationException(message);
        }

        foreach (var portDef in metadata.InputPorts)
        {
            op.AddInputPort(portDef.Name, portDef.DataType, portDef.IsRequired);
        }

        foreach (var portDef in metadata.OutputPorts)
        {
            op.AddOutputPort(portDef.Name, portDef.DataType);
        }

        foreach (var paramDef in metadata.Parameters)
        {
            var parameter = new Parameter(
                Guid.NewGuid(),
                paramDef.Name,
                paramDef.DisplayName,
                paramDef.Description ?? string.Empty,
                paramDef.DataType,
                paramDef.DefaultValue,
                paramDef.MinValue,
                paramDef.MaxValue,
                paramDef.IsRequired,
                paramDef.Options);
            op.AddParameter(parameter);
        }

        return op;
    }

    public OperatorMetadata? GetMetadata(OperatorType type)
    {
        return _metadata.TryGetValue(type, out var metadata) ? metadata : null;
    }

    public IEnumerable<OperatorMetadata> GetAllMetadata()
    {
        return _metadata.Values.Where(metadata => !OperatorFactoryMetadataMerge.IsLegacyAlias(metadata.Type));
    }

    public IEnumerable<OperatorType> GetSupportedOperatorTypes()
    {
        return _metadata.Keys;
    }

    public void RegisterOperator(OperatorMetadata metadata)
    {
        _metadata[metadata.Type] = metadata;
    }

    private void InitializeDefaultOperators()
    {
        OperatorFactoryMetadataMerge.Apply(_metadata, _scanMetadata, _strictMetadataScan);
    }
}
