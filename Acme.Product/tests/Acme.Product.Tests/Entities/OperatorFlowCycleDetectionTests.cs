using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using FluentAssertions;

namespace Acme.Product.Tests.Entities;

public class OperatorFlowCycleDetectionTests
{
    [Fact]
    public void AddConnection_WhenEdgeCreatesRealCycle_ShouldThrow()
    {
        var flow = new OperatorFlow("CycleFlow");

        var opA = CreateNode("A", OperatorType.Filtering, inputCount: 1, outputCount: 1);
        var opB = CreateNode("B", OperatorType.Filtering, inputCount: 1, outputCount: 1);

        flow.AddOperator(opA);
        flow.AddOperator(opB);

        flow.AddConnection(Connect(opA, 0, opB, 0));

        Action act = () => flow.AddConnection(Connect(opB, 0, opA, 0));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddConnection_WhenGraphIsDagWithConvergingBranches_ShouldNotThrow()
    {
        // Regression case:
        // Two edges from op3 to op7 (different target ports), with a non-topological
        // insertion order. This is a valid DAG and must not be treated as a cycle.
        var flow = new OperatorFlow("CycleDetectionRegression");

        var op1 = CreateNode("op1", OperatorType.ImageAcquisition, inputCount: 0, outputCount: 1);
        var op2 = CreateNode("op2", OperatorType.Filtering, inputCount: 1, outputCount: 1);
        var op3 = CreateNode("op3", OperatorType.EdgeDetection, inputCount: 1, outputCount: 2);
        var op4 = CreateNode("op4", OperatorType.Thresholding, inputCount: 1, outputCount: 2);
        var op5 = CreateNode("op5", OperatorType.GaussianBlur, inputCount: 1, outputCount: 0);
        var op6 = CreateNode("op6", OperatorType.GaussianBlur, inputCount: 1, outputCount: 0);
        var op7 = CreateNode("op7", OperatorType.ResultOutput, inputCount: 2, outputCount: 0);

        flow.AddOperator(op1);
        flow.AddOperator(op2);
        flow.AddOperator(op3);
        flow.AddOperator(op4);
        flow.AddOperator(op5);
        flow.AddOperator(op6);
        flow.AddOperator(op7);

        // Intentionally problematic insertion order.
        flow.AddConnection(Connect(op3, 0, op7, 0));
        flow.AddConnection(Connect(op3, 1, op7, 1));
        flow.AddConnection(Connect(op3, 1, op4, 0));
        flow.AddConnection(Connect(op4, 0, op5, 0));
        flow.AddConnection(Connect(op4, 1, op6, 0));

        Action addUpstreamEdge = () => flow.AddConnection(Connect(op2, 0, op3, 0));
        addUpstreamEdge.Should().NotThrow();

        flow.AddConnection(Connect(op1, 0, op2, 0));
        flow.Connections.Should().HaveCount(7);
    }

    private static Operator CreateNode(string name, OperatorType type, int inputCount, int outputCount)
    {
        var op = new Operator(name, type, 0, 0);

        for (var i = 0; i < inputCount; i++)
            op.AddInputPort($"In{i}", PortDataType.Any, true);

        for (var i = 0; i < outputCount; i++)
            op.AddOutputPort($"Out{i}", PortDataType.Any);

        return op;
    }

    private static OperatorConnection Connect(Operator source, int sourcePortIndex, Operator target, int targetPortIndex)
        => new(
            source.Id,
            source.OutputPorts[sourcePortIndex].Id,
            target.Id,
            target.InputPorts[targetPortIndex].Id);
}

