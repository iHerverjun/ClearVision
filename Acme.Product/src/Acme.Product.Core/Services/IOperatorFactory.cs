// IOperatorFactory.cs
// 选项列表（用于enum类型）
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;

namespace Acme.Product.Core.Services;

/// <summary>
/// 算子工厂接口 - 用于创建和管理算子实例
/// </summary>
public interface IOperatorFactory
{
    /// <summary>
    /// 创建算子
    /// </summary>
    /// <param name="type">算子类型</param>
    /// <param name="name">算子名称</param>
    /// <param name="x">X坐标</param>
    /// <param name="y">Y坐标</param>
    /// <returns>创建的算子</returns>
    Operator CreateOperator(OperatorType type, string name, double x, double y);

    /// <summary>
    /// 获取算子元数据
    /// </summary>
    /// <param name="type">算子类型</param>
    /// <returns>算子元数据</returns>
    OperatorMetadata? GetMetadata(OperatorType type);

    /// <summary>
    /// 获取所有算子元数据
    /// </summary>
    /// <returns>算子元数据列表</returns>
    IEnumerable<OperatorMetadata> GetAllMetadata();

    /// <summary>
    /// 获取支持的算子类型列表
    /// </summary>
    /// <returns>算子类型列表</returns>
    IEnumerable<OperatorType> GetSupportedOperatorTypes();

    /// <summary>
    /// 注册自定义算子
    /// </summary>
    /// <param name="metadata">算子元数据</param>
    void RegisterOperator(OperatorMetadata metadata);
}

/// <summary>
/// 算子元数据
/// </summary>
public class OperatorMetadata
{
    /// <summary>
    /// 算子类型
    /// </summary>
    public OperatorType Type { get; set; }

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 类别（用于分组显示）
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 图标名称或路径
    /// </summary>
    public string? IconName { get; set; }

    /// <summary>
    /// 输入端口定义
    /// </summary>
    public List<PortDefinition> InputPorts { get; set; } = new();

    /// <summary>
    /// 输出端口定义
    /// </summary>
    public List<PortDefinition> OutputPorts { get; set; } = new();

    /// <summary>
    /// 参数定义
    /// </summary>
    public List<ParameterDefinition> Parameters { get; set; } = new();
}

/// <summary>
/// 端口定义
/// </summary>
public class PortDefinition
{
    /// <summary>
    /// 端口名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 数据类型
    /// </summary>
    public PortDataType DataType { get; set; }

    /// <summary>
    /// 是否必需
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// 参数定义
/// </summary>
public class ParameterDefinition
{
    /// <summary>
    /// 参数名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 数据类型
    /// </summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    /// 默认值
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// 最小值（用于数值类型）
    /// </summary>
    public object? MinValue { get; set; }

    /// <summary>
    /// 最大值（用于数值类型）
    /// </summary>
    public object? MaxValue { get; set; }

    /// <summary>
    /// 是否必需
    /// </summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// 选项列表（用于enum类型）
    /// </summary>
    public List<ParameterOption>? Options { get; set; }
}
