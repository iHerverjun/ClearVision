using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Services;
using Acme.Product.Application.DTOs;

namespace Acme.Product.Desktop.Endpoints;

internal static class FlowEntityMapper
{
    private static readonly OperatorFactory OperatorFactory = new();

    public static OperatorFlow ToEntity(CanvasFlowDataDto flowData)
    {
        ArgumentNullException.ThrowIfNull(flowData);
        return BuildFlow(
            flowData.Id,
            string.IsNullOrWhiteSpace(flowData.Name) ? "PreviewFlow" : flowData.Name,
            flowData.Operators,
            flowData.Connections.Select(connection => new ConnectionShape(
                connection.SourceOperatorId,
                connection.SourcePortId,
                connection.TargetOperatorId,
                connection.TargetPortId)));
    }

    public static OperatorFlow ToEntity(FlowDataDto flowData)
    {
        ArgumentNullException.ThrowIfNull(flowData);

        if (flowData.Operators.Count > 0)
        {
            return BuildFlow(
                flowData.Id,
                string.IsNullOrWhiteSpace(flowData.Name) ? "AutoTuneFlow" : flowData.Name,
                flowData.Operators,
                flowData.Connections.Select(connection => new ConnectionShape(
                    connection.SourceOperatorId != Guid.Empty ? connection.SourceOperatorId : connection.SourceId,
                    connection.SourcePortId,
                    connection.TargetOperatorId != Guid.Empty ? connection.TargetOperatorId : connection.TargetId,
                    connection.TargetPortId)));
        }

        return BuildLegacyFlow(flowData);
    }

    public static OperatorFlow ToEntity(UpdateFlowRequest flowData, string flowName = "PreviewFlow", Guid? flowId = null)
    {
        ArgumentNullException.ThrowIfNull(flowData);

        var dto = new OperatorFlowDto
        {
            Id = flowId ?? Guid.Empty,
            Name = string.IsNullOrWhiteSpace(flowName) ? "PreviewFlow" : flowName,
            Operators = flowData.Operators,
            Connections = flowData.Connections
        };

        var flow = dto.ToEntity();
        if (flowId.HasValue && flowId.Value != Guid.Empty)
        {
            SetId(flow, flowId.Value);
        }

        return flow;
    }

    private static OperatorFlow BuildFlow(
        Guid flowId,
        string flowName,
        IEnumerable<CanvasOperatorDataDto> operators,
        IEnumerable<ConnectionShape> connections)
    {
        var flow = new OperatorFlow(flowName);
        if (flowId != Guid.Empty)
        {
            SetId(flow, flowId);
        }

        foreach (var operatorData in operators)
        {
            var type = OperatorTypeAliasResolver.Resolve(Enum.Parse<OperatorType>(operatorData.Type, ignoreCase: true));
            var @operator = OperatorFactory.CreateOperator(
                type,
                string.IsNullOrWhiteSpace(operatorData.Name) ? type.ToString() : operatorData.Name,
                operatorData.X,
                operatorData.Y);

            if (operatorData.Id != Guid.Empty)
            {
                SetId(@operator, operatorData.Id);
            }

            if (operatorData.InputPorts?.Count > 0)
            {
                @operator.InputPorts.Clear();
                foreach (var port in operatorData.InputPorts)
                {
                    @operator.LoadInputPort(
                        port.Id == Guid.Empty ? Guid.NewGuid() : port.Id,
                        port.Name,
                        Enum.Parse<PortDataType>(port.DataType, ignoreCase: true),
                        port.IsRequired);
                }
            }

            if (operatorData.OutputPorts?.Count > 0)
            {
                @operator.OutputPorts.Clear();
                foreach (var port in operatorData.OutputPorts)
                {
                    @operator.LoadOutputPort(
                        port.Id == Guid.Empty ? Guid.NewGuid() : port.Id,
                        port.Name,
                        Enum.Parse<PortDataType>(port.DataType, ignoreCase: true));
                }
            }

            if (operatorData.Parameters != null)
            {
                foreach (var (name, value) in operatorData.Parameters)
                {
                    var existing = @operator.Parameters.FirstOrDefault(parameter =>
                        string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase));

                    if (existing != null)
                    {
                        existing.SetValue(value);
                        continue;
                    }

                    @operator.AddParameter(new Parameter(
                        Guid.NewGuid(),
                        name,
                        name,
                        string.Empty,
                        ResolveDataType(value),
                        value,
                        null,
                        null,
                        false,
                        null));
                }
            }

            flow.AddOperator(@operator);
        }

        foreach (var connection in connections)
        {
            var sourceOperator = flow.Operators.FirstOrDefault(@operator => @operator.Id == connection.SourceOperatorId);
            var targetOperator = flow.Operators.FirstOrDefault(@operator => @operator.Id == connection.TargetOperatorId);
            if (sourceOperator == null || targetOperator == null)
            {
                continue;
            }

            if (!TryResolveConnectionPorts(
                    sourceOperator,
                    targetOperator,
                    connection.SourcePortId,
                    connection.TargetPortId,
                    out var sourcePortId,
                    out var targetPortId))
            {
                continue;
            }

            flow.AddConnection(new OperatorConnection(
                sourceOperator.Id,
                sourcePortId,
                targetOperator.Id,
                targetPortId));
        }

        return flow;
    }

    private static OperatorFlow BuildLegacyFlow(FlowDataDto flowData)
    {
        var flow = new OperatorFlow(string.IsNullOrWhiteSpace(flowData.Name) ? "AutoTuneFlow" : flowData.Name);
        if (flowData.Id != Guid.Empty)
        {
            SetId(flow, flowData.Id);
        }

        foreach (var node in flowData.Nodes)
        {
            var @operator = OperatorFactory.CreateOperator(
                node.Type,
                string.IsNullOrWhiteSpace(node.Name) ? node.Type.ToString() : node.Name,
                node.Position.X,
                node.Position.Y);

            if (node.Id != Guid.Empty)
            {
                SetId(@operator, node.Id);
            }

            foreach (var (name, value) in node.Parameters)
            {
                var parameter = @operator.Parameters.FirstOrDefault(item =>
                    string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));

                if (parameter != null)
                {
                    parameter.SetValue(value);
                    continue;
                }

                @operator.AddParameter(new Parameter(
                    Guid.NewGuid(),
                    name,
                    name,
                    string.Empty,
                    ResolveDataType(value),
                    value,
                    null,
                    null,
                    false,
                    null));
            }

            flow.AddOperator(@operator);
        }

        foreach (var connection in flowData.Connections)
        {
            var sourceOperator = flow.Operators.FirstOrDefault(@operator => @operator.Id == connection.SourceId);
            var targetOperator = flow.Operators.FirstOrDefault(@operator => @operator.Id == connection.TargetId);
            if (sourceOperator == null || targetOperator == null)
            {
                continue;
            }

            if (!TryResolveConnectionPorts(
                    sourceOperator,
                    targetOperator,
                    connection.SourcePortId,
                    connection.TargetPortId,
                    out var sourcePortId,
                    out var targetPortId))
            {
                continue;
            }

            flow.AddConnection(new OperatorConnection(
                sourceOperator.Id,
                sourcePortId,
                targetOperator.Id,
                targetPortId));
        }

        return flow;
    }

    private static bool TryResolveConnectionPorts(
        Operator sourceOperator,
        Operator targetOperator,
        Guid preferredSourcePortId,
        Guid preferredTargetPortId,
        out Guid sourcePortId,
        out Guid targetPortId)
    {
        var sourcePort = preferredSourcePortId != Guid.Empty
            ? sourceOperator.OutputPorts.FirstOrDefault(port => port.Id == preferredSourcePortId)
            : null;
        var targetPort = preferredTargetPortId != Guid.Empty
            ? targetOperator.InputPorts.FirstOrDefault(port => port.Id == preferredTargetPortId)
            : null;

        if (sourcePort == null && targetPort != null)
        {
            sourcePort = FindMatchingOutputPort(sourceOperator, targetPort);
        }

        if (targetPort == null && sourcePort != null)
        {
            targetPort = FindMatchingInputPort(targetOperator, sourcePort);
        }

        if (sourcePort == null || targetPort == null)
        {
            foreach (var candidateSource in sourceOperator.OutputPorts)
            {
                var candidateTarget = FindMatchingInputPort(targetOperator, candidateSource);
                if (candidateTarget == null)
                {
                    continue;
                }

                sourcePort = candidateSource;
                targetPort = candidateTarget;
                break;
            }
        }

        if (sourcePort == null || targetPort == null)
        {
            sourcePortId = Guid.Empty;
            targetPortId = Guid.Empty;
            return false;
        }

        sourcePortId = sourcePort.Id;
        targetPortId = targetPort.Id;
        return true;
    }

    private static Port? FindMatchingOutputPort(Operator sourceOperator, Port targetPort)
    {
        return sourceOperator.OutputPorts.FirstOrDefault(port =>
                   string.Equals(port.Name, targetPort.Name, StringComparison.OrdinalIgnoreCase)) ??
               sourceOperator.OutputPorts.FirstOrDefault(port => IsCompatible(port.DataType, targetPort.DataType)) ??
               sourceOperator.OutputPorts.FirstOrDefault();
    }

    private static Port? FindMatchingInputPort(Operator targetOperator, Port sourcePort)
    {
        return targetOperator.InputPorts.FirstOrDefault(port =>
                   string.Equals(port.Name, sourcePort.Name, StringComparison.OrdinalIgnoreCase)) ??
               targetOperator.InputPorts.FirstOrDefault(port => IsCompatible(sourcePort.DataType, port.DataType)) ??
               targetOperator.InputPorts.FirstOrDefault();
    }

    private static bool IsCompatible(PortDataType source, PortDataType target)
    {
        return source == target || source == PortDataType.Any || target == PortDataType.Any;
    }

    private static string ResolveDataType(object? value)
    {
        return value switch
        {
            null => "string",
            bool => "bool",
            byte or sbyte or short or ushort or int or uint or long or ulong => "int",
            float or double or decimal => "double",
            _ => "string"
        };
    }

    private static void SetId(object entity, Guid id)
    {
        entity.GetType().GetProperty("Id")?.SetValue(entity, id);
    }

    private readonly record struct ConnectionShape(
        Guid SourceOperatorId,
        Guid SourcePortId,
        Guid TargetOperatorId,
        Guid TargetPortId);
}
