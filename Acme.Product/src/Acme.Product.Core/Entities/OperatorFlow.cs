// OperatorFlow.cs
// 算子流程实体 - 包含算子列表和连接关系
// 作者：蘅芜君

using Acme.Product.Core.Entities.Base;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;

namespace Acme.Product.Core.Entities;

/// <summary>
/// 算子流程实体 - 包含算子列表和连接关系
/// </summary>
public class OperatorFlow : Entity
{
    /// <summary>
    /// 流程名称
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// 算子列表
    /// </summary>
    private readonly List<Operator> _operators = [];
    public IReadOnlyCollection<Operator> Operators => _operators.AsReadOnly();

    /// <summary>
    /// 连接关系列表
    /// </summary>
    private readonly List<OperatorConnection> _connections = [];
    public IReadOnlyCollection<OperatorConnection> Connections => _connections.AsReadOnly();

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

        if (_operators.Any(o => o.Id == op.Id))
            throw new InvalidOperationException($"算子 {op.Id} 已存在");

        _operators.Add(op);
        MarkAsModified();
    }

    /// <summary>
    /// 移除算子
    /// </summary>
    public void RemoveOperator(Guid operatorId)
    {
        var op = _operators.FirstOrDefault(o => o.Id == operatorId);
        if (op == null)
            throw new InvalidOperationException($"算子 {operatorId} 不存在");

        // 移除相关连接
        _connections.RemoveAll(c => c.SourceOperatorId == operatorId || c.TargetOperatorId == operatorId);

        _operators.Remove(op);
        MarkAsModified();
    }

    /// <summary>
    /// 清空所有算子
    /// </summary>
    public void ClearOperators()
    {
        _operators.Clear();
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

        _connections.Add(connection);
        MarkAsModified();
    }

    /// <summary>
    /// 移除连接
    /// </summary>
    public void RemoveConnection(Guid connectionId)
    {
        _connections.RemoveAll(c => c.Id == connectionId);
        MarkAsModified();
    }

    /// <summary>
    /// 清空所有连接
    /// </summary>
    public void ClearConnections()
    {
        _connections.Clear();
        MarkAsModified();
    }

    /// <summary>
    /// 获取执行顺序（拓扑排序）
    /// </summary>
    public IEnumerable<Operator> GetExecutionOrder()
    {
        var visited = new HashSet<Guid>();
        var result = new List<Operator>();

        foreach (var op in _operators)
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
        var dependencies = _connections
            .Where(c => c.TargetOperatorId == op.Id)
            .Select(c => _operators.FirstOrDefault(o => o.Id == c.SourceOperatorId))
            .Where(o => o != null);

        foreach (var dep in dependencies)
        {
            VisitOperator(dep!, visited, result);
        }

        result.Add(op);
    }

    private void ValidateConnection(OperatorConnection connection)
    {
        var sourceOp = _operators.FirstOrDefault(o => o.Id == connection.SourceOperatorId);
        var targetOp = _operators.FirstOrDefault(o => o.Id == connection.TargetOperatorId);

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
        // 简化的循环检测
        var visited = new HashSet<Guid>();
        return HasCycle(newConnection.TargetOperatorId, newConnection.SourceOperatorId, visited);
    }

    private bool HasCycle(Guid current, Guid target, HashSet<Guid> visited)
    {
        if (current == target)
            return true;

        if (!visited.Add(current))
            return true; // 已访问过，说明有环

        var nextOperators = _connections
            .Where(c => c.SourceOperatorId == current)
            .Select(c => c.TargetOperatorId);

        foreach (var next in nextOperators)
        {
            if (HasCycle(next, target, visited))
                return true;
        }

        return false;
    }
}
