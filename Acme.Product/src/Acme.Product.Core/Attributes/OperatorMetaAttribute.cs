using System;

namespace Acme.Product.Core.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class OperatorMetaAttribute : Attribute
{
    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string? IconName { get; set; }

    public string[]? Keywords { get; set; }
}
