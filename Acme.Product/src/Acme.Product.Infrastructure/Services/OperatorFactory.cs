// OperatorFactory.cs
// 算子工厂
// 负责按算子类型创建并组装执行实例
// 作者：蘅芜君
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

    public OperatorFactory()
    {
        InitializeDefaultOperators();
    }

    public Operator CreateOperator(OperatorType type, string name, double x, double y)
    {
        var op = new Operator(name, type, x, y);
        var metadata = GetMetadata(type);

        if (metadata == null)
        {
            // Keep a minimal fallback so unknown types can still flow through the canvas.
            op.AddInputPort("Input", PortDataType.Any, false);
            op.AddOutputPort("Output", PortDataType.Any);
            return op;
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
        OperatorFactoryMetadataMerge.Apply(_metadata);
    }
}
