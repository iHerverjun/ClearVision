// OutputPortAttribute.cs
// 输出端口特性
// 定义算子输出端口的名称、类型与描述信息
// 作者：蘅芜君
using System;
using Acme.Product.Core.Enums;

namespace Acme.Product.Core.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class OutputPortAttribute : Attribute
{
    public OutputPortAttribute(string name, string displayName, PortDataType dataType)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        DataType = dataType;
    }

    public string Name { get; }

    public string DisplayName { get; }

    public PortDataType DataType { get; }
}
