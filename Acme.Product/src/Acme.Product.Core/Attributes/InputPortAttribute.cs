// InputPortAttribute.cs
// 输入端口特性
// 定义算子输入端口的名称、类型与约束信息
// 作者：蘅芜君
using System;
using Acme.Product.Core.Enums;

namespace Acme.Product.Core.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class InputPortAttribute : Attribute
{
    public InputPortAttribute(string name, string displayName, PortDataType dataType)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        DataType = dataType;
    }

    public string Name { get; }

    public string DisplayName { get; }

    public PortDataType DataType { get; }

    public bool IsRequired { get; set; } = true;
}
