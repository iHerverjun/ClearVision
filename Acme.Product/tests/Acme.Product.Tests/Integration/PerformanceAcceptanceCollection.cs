using Xunit;

namespace Acme.Product.Tests.Integration;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PerformanceAcceptanceCollection
{
    public const string Name = "PerformanceAcceptanceSerial";
}
