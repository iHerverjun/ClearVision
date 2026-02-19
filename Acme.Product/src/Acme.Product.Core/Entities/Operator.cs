// Operator.cs
// 重置执行状态
// 作者：蘅芜君

using Acme.Product.Core.Entities.Base;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using System.Text.Json.Serialization;

namespace Acme.Product.Core.Entities;

/// <summary>
/// 算子实体 - 图像处理单元
/// </summary>
public class Operator : Entity
{
    /// <summary>
    /// 算子名称
    /// </summary>
    [JsonInclude]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 算子类型
    /// </summary>
    [JsonInclude]
    public OperatorType Type { get; set; }

    /// <summary>
    /// 算子在画布上的位置
    /// </summary>
    [JsonInclude]
    public Position Position { get; set; } = new Position(0, 0);

    /// <summary>
    /// 输入端口列表
    /// </summary>
    [JsonInclude]
    public List<Port> InputPorts { get; set; } = [];

    /// <summary>
    /// 输出端口列表
    /// </summary>
    [JsonInclude]
    public List<Port> OutputPorts { get; set; } = [];

    /// <summary>
    /// 参数配置
    /// </summary>
    [JsonInclude]
    public List<Parameter> Parameters { get; set; } = [];

    /// <summary>
    /// 是否启用
    /// </summary>
    [JsonInclude]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 执行状态
    /// </summary>
    public OperatorExecutionStatus ExecutionStatus { get; private set; }

    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    public long? ExecutionTimeMs { get; private set; }

    /// <summary>
    /// 执行错误信息
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// 所属项目ID（外键）
    /// </summary>
    public Guid ProjectId { get; private set; }

    [JsonConstructor]
    private Operator()
    {
        Name = string.Empty;
        Position = new Position(0, 0);
        IsEnabled = true;
        ExecutionStatus = OperatorExecutionStatus.NotExecuted;
    }

    public Operator(string name, OperatorType type, double x, double y) : this()
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("算子名称不能为空", nameof(name));

        Name = name;
        Type = type;
        Position = new Position(x, y);
    }

    /// <summary>
    /// 用于反序列化/DTO转换时的构造函数，保留原始ID
    /// </summary>
    public Operator(Guid id, string name, OperatorType type, double x, double y) : this(name, type, x, y)
    {
        Id = id;
    }

    /// <summary>
    /// 更新名称
    /// </summary>
    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("算子名称不能为空", nameof(name));

        Name = name;
        MarkAsModified();
    }

    /// <summary>
    /// 更新位置
    /// </summary>
    public void UpdatePosition(double x, double y)
    {
        Position = new Position(x, y);
        MarkAsModified();
    }

    /// <summary>
    /// 添加输入端口
    /// </summary>
    public void AddInputPort(string name, PortDataType dataType, bool isRequired = true)
    {
        var port = new Port(Guid.NewGuid(), name, PortDirection.Input, dataType, isRequired);
        InputPorts.Add(port);
        MarkAsModified();
    }

    /// <summary>
    /// 添加输出端口
    /// </summary>
    public void AddOutputPort(string name, PortDataType dataType)
    {
        var port = new Port(Guid.NewGuid(), name, PortDirection.Output, dataType, false);
        OutputPorts.Add(port);
        MarkAsModified();
    }

    /// <summary>
    /// 加载输入端口（用于反序列化/恢复数据）
    /// </summary>
    public void LoadInputPort(Guid id, string name, PortDataType dataType, bool isRequired)
    {
        InputPorts.Add(new Port(id, name, PortDirection.Input, dataType, isRequired));
    }

    /// <summary>
    /// 加载输出端口（用于反序列化/恢复数据）
    /// </summary>
    public void LoadOutputPort(Guid id, string name, PortDataType dataType)
    {
        OutputPorts.Add(new Port(id, name, PortDirection.Output, dataType, false));
    }

    /// <summary>
    /// 添加参数
    /// </summary>
    public void AddParameter(Parameter parameter)
    {
        if (parameter == null)
            throw new ArgumentNullException(nameof(parameter));

        Parameters.Add(parameter);
        MarkAsModified();
    }

    /// <summary>
    /// 更新参数值
    /// </summary>
    public void UpdateParameter(string parameterName, object value)
    {
        var param = Parameters.FirstOrDefault(p => p.Name == parameterName);
        if (param == null)
            throw new InvalidOperationException($"参数 {parameterName} 不存在");

        param.SetValue(value);
        MarkAsModified();
    }

    /// <summary>
    /// 启用算子
    /// </summary>
    public void Enable()
    {
        IsEnabled = true;
        MarkAsModified();
    }

    /// <summary>
    /// 禁用算子
    /// </summary>
    public void Disable()
    {
        IsEnabled = false;
        MarkAsModified();
    }

    /// <summary>
    /// 标记执行开始
    /// </summary>
    public void MarkExecutionStarted()
    {
        ExecutionStatus = OperatorExecutionStatus.Executing;
        ErrorMessage = null;
    }

    /// <summary>
    /// 标记执行成功
    /// </summary>
    public void MarkExecutionCompleted(long executionTimeMs)
    {
        ExecutionStatus = OperatorExecutionStatus.Success;
        ExecutionTimeMs = executionTimeMs;
    }

    /// <summary>
    /// 标记执行失败
    /// </summary>
    public void MarkExecutionFailed(string errorMessage)
    {
        ExecutionStatus = OperatorExecutionStatus.Failed;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// 重置执行状态
    /// </summary>
    public void ResetExecutionStatus()
    {
        ExecutionStatus = OperatorExecutionStatus.NotExecuted;
        ExecutionTimeMs = null;
        ErrorMessage = null;
    }
}
