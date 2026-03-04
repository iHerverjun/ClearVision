// RuntimeProfile.cs
// 运行时配置档案
// 集中定义 OperatorLibrary 的运行时常量与默认行为
// 作者：蘅芜君
namespace Acme.OperatorLibrary.Abstractions;

/// <summary>
/// Build profile flags for package/host compatibility.
/// </summary>
public static class RuntimeProfile
{
#if ACME_OPERATORLIB_PACKAGE
    public const string Name = "PackageLinked";
    public const bool UsesLinkedProductSources = true;
#else
    public const string Name = "Standalone";
    public const bool UsesLinkedProductSources = false;
#endif
}
