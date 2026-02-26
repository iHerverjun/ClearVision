using System;

namespace Acme.Product.Core.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class AlgorithmInfoAttribute : Attribute
{
    public string Name { get; set; } = string.Empty;

    public string? CoreApi { get; set; }

    public string? TimeComplexity { get; set; }

    public string? SpaceComplexity { get; set; }

    public string[]? Dependencies { get; set; }
}
