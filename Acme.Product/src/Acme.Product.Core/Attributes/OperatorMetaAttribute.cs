// OperatorMetaAttribute.cs
// 算子元数据特性
// 定义算子展示信息、分类与运行标记
// 作者：蘅芜君
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

    /// <summary>
    /// Additional classification tags for quality/catalog indexing.
    /// </summary>
    public string[]? Tags { get; set; }

    /// <summary>
    /// Operator semantic version.
    /// </summary>
    public string Version { get; set; } = "1.0.0";
}
