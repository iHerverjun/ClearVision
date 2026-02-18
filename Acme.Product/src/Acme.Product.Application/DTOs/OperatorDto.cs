// OperatorDto.cs
// 算子连接数据传输对象
// 作者：蘅芜君

using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;

namespace Acme.Product.Application.DTOs;

/// <summary>
/// 算子数据传输对象
/// </summary>
public class OperatorDto
{
    /// <summary>
    /// 算子ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 算子名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 算子类型
    /// </summary>
    public OperatorType Type { get; set; }

    /// <summary>
    /// X坐标
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y坐标
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// 输入端口列表
    /// </summary>
    public List<PortDto> InputPorts { get; set; } = new();

    /// <summary>
    /// 输出端口列表
    /// </summary>
    public List<PortDto> OutputPorts { get; set; } = new();

    /// <summary>
    /// 参数列表
    /// </summary>
    public List<ParameterDto> Parameters { get; set; } = new();

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 执行状态
    /// </summary>
    public OperatorExecutionStatus ExecutionStatus { get; set; }

    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    public long? ExecutionTimeMs { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 端口数据传输对象
/// </summary>
public class PortDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PortDirection Direction { get; set; }
    public PortDataType DataType { get; set; }
    public bool IsRequired { get; set; }
}

/// <summary>
/// 参数数据传输对象
/// </summary>
public class ParameterDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DataType { get; set; } = string.Empty;
    public object? Value { get; set; }
    public object? DefaultValue { get; set; }
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
    public bool IsRequired { get; set; }
    public List<ParameterOption>? Options { get; set; }
}

/// <summary>
/// 算子连接数据传输对象
/// </summary>
public class OperatorConnectionDto
{
    public Guid Id { get; set; }
    public Guid SourceOperatorId { get; set; }
    public Guid SourcePortId { get; set; }
    public Guid TargetOperatorId { get; set; }
    public Guid TargetPortId { get; set; }
}
