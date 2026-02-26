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
