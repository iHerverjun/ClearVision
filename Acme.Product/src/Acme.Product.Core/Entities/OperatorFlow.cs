// OperatorFlow.cs
// 算子流程实体 - 包含算子列表和连接关系
// 作者：蘅芜君

using Acme.Product.Core.Entities.Base;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using System.Text.Json.Serialization;

namespace Acme.Product.Core.Entities;

/// <summary>
/// 算子流程实体 - 包含算子列表和连接关系
/// </summary>
public class OperatorFlow : Entity
{
    /// <summary>
    /// 流程名称
    /// </summary>
    [JsonInclude]
    public string Name { get; set; } = string.Empty;

    [JsonInclude]
    public List<Operator> Operators { get; set; } = [];

    /// <summary>
    /// 连接关系列表
    /// </summary>
    [JsonInclude]
    public List<OperatorConnection> Connections { get; set; } = [];

    [JsonConstructor]
    public OperatorFlow()
    {
        Name = "默认流程";
    }

    public OperatorFlow(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// 用于 Table Splitting 时同步 ID
    /// </summary>
    internal OperatorFlow(Guid id, string name = "默认流程")
    {
        Id = id;
        Name = name;
    }

    /// <summary>
    /// 添加算子
    /// </summary>
    public void AddOperator(Operator op)
    {
        if (op == null)
            throw new ArgumentNullException(nameof(op));

        if (Operators.Any(o => o.Id == op.Id))
            throw new InvalidOperationException($"算子 {op.Id} 已存在");

        Operators.Add(op);
        MarkAsModified();
    }

    /// <summary>
    /// 移除算子
    /// </summary>
    public void RemoveOperator(Guid operatorId)
    {
        var op = Operators.FirstOrDefault(o => o.Id == operatorId);
        if (op == null)
            throw new InvalidOperationException($"算子 {operatorId} 不存在");

        // 移除相关连接
        Connections.RemoveAll(c => c.SourceOperatorId == operatorId || c.TargetOperatorId == operatorId);

        Operators.Remove(op);
        MarkAsModified();
    }

    /// <summary>
    /// 清空所有算子
    /// </summary>
    public void ClearOperators()
    {
        Operators.Clear();
        MarkAsModified();
    }

    /// <summary>
    /// 添加连接
    /// </summary>
    public void AddConnection(OperatorConnection connection)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));

        // 验证连接有效性
        ValidateConnection(connection);

        Connections.Add(connection);
        MarkAsModified();
    }

    /// <summary>
    /// 移除连接
    /// </summary>
    public void RemoveConnection(Guid connectionId)
    {
        Connections.RemoveAll(c => c.Id == connectionId);
        MarkAsModified();
    }

    /// <summary>
    /// 清空所有连接
    /// </summary>
    public void ClearConnections()
    {
        Connections.Clear();
        MarkAsModified();
    }

    /// <summary>
    /// 获取执行顺序（拓扑排序）
    /// </summary>
    public IEnumerable<Operator> GetExecutionOrder()
    {
        var visited = new HashSet<Guid>();
        var result = new List<Operator>();

        foreach (var op in Operators)
        {
            VisitOperator(op, visited, result);
        }

        return result;
    }

    private void VisitOperator(Operator op, HashSet<Guid> visited, List<Operator> result)
    {
        if (visited.Contains(op.Id))
            return;

        visited.Add(op.Id);

        // 先访问依赖的算子
        var dependencies = Connections
            .Where(c => c.TargetOperatorId == op.Id)
            .Select(c => Operators.FirstOrDefault(o => o.Id == c.SourceOperatorId))
            .Where(o => o != null);

        foreach (var dep in dependencies)
        {
            VisitOperator(dep!, visited, result);
        }

        result.Add(op);
    }

    private void ValidateConnection(OperatorConnection connection)
    {
        var sourceOp = Operators.FirstOrDefault(o => o.Id == connection.SourceOperatorId);
        var targetOp = Operators.FirstOrDefault(o => o.Id == connection.TargetOperatorId);

        if (sourceOp == null)
            throw new InvalidOperationException($"源算子 {connection.SourceOperatorId} 不存在");

        if (targetOp == null)
            throw new InvalidOperationException($"目标算子 {connection.TargetOperatorId} 不存在");

        // 检查数据类型兼容性
        var sourcePort = sourceOp.OutputPorts.FirstOrDefault(p => p.Id == connection.SourcePortId);
        var targetPort = targetOp.InputPorts.FirstOrDefault(p => p.Id == connection.TargetPortId);

        if (sourcePort == null)
            throw new InvalidOperationException($"源端口 {connection.SourcePortId} 不存在");

        if (targetPort == null)
            throw new InvalidOperationException($"目标端口 {connection.TargetPortId} 不存在");

        if (sourcePort.DataType != targetPort.DataType &&
            sourcePort.DataType != PortDataType.Any &&
            targetPort.DataType != PortDataType.Any)
        {
            throw new InvalidOperationException($"端口数据类型不匹配: {sourcePort.DataType} -> {targetPort.DataType}");
        }

        // 检查是否形成循环
        if (WouldCreateCycle(connection))
        {
            throw new InvalidOperationException("连接会形成循环依赖");
        }
    }

    private bool WouldCreateCycle(OperatorConnection newConnection)
    {
        if (newConnection.SourceOperatorId == newConnection.TargetOperatorId)
            return true;

        var visited = new HashSet<Guid>();
        return HasCycle(newConnection.TargetOperatorId, newConnection.SourceOperatorId, visited);
    }

    private bool HasCycle(Guid current, Guid target, HashSet<Guid> visited)
    {
        if (current == target)
            return true;

        if (!visited.Add(current))
            return false;

        var nextOperators = Connections
            .Where(c => c.SourceOperatorId == current)
            .Select(c => c.TargetOperatorId)
            .Distinct();

        foreach (var next in nextOperators)
        {
            if (HasCycle(next, target, visited))
                return true;
        }

        return false;
    }
}
