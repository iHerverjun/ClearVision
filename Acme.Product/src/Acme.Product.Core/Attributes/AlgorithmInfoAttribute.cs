// AlgorithmInfoAttribute.cs
// 算法信息特性
// 为算法类型提供说明、分类与元数据标注
// 作者：蘅芜君
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
