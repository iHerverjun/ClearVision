// OperatorParamAttribute.cs
// 算子参数特性
// 描述算子参数类型、默认值与配置项
// 作者：蘅芜君
using System;

namespace Acme.Product.Core.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class OperatorParamAttribute : Attribute
{
    public OperatorParamAttribute(string name, string displayName, string dataType)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
    }

    public string Name { get; }

    public string DisplayName { get; }

    public string DataType { get; }

    public string? Description { get; set; }

    public object? DefaultValue { get; set; }

    public object? Min { get; set; }

    public object? Max { get; set; }

    public bool IsRequired { get; set; } = true;

    // Option format supports "value" or "value|label".
    public string[]? Options { get; set; }
}
