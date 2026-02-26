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
