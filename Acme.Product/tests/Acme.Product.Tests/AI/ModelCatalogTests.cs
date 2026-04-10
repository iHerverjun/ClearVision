using Acme.Product.Infrastructure.AI.Runtime;
using FluentAssertions;

namespace Acme.Product.Tests.AI;

public sealed class ModelCatalogTests
{
    [Fact]
    public void Load_ShouldFindDefaultCatalog()
    {
        var catalog = ModelCatalog.Load();

        catalog.Models.Should().NotBeEmpty();
        catalog.Models.Should().Contain(x => x.Id == "semantic_identity_2x2");
        catalog.Models.Should().Contain(x => x.Id == "anomaly_embedding_identity_2x2");
    }

    [Fact]
    public void ResolveExplicitOrCatalogPath_WithModelId_ShouldReturnExistingPath()
    {
        var resolved = ModelCatalog.ResolveExplicitOrCatalogPath(
            explicitPath: null,
            modelId: "semantic_identity_2x2",
            catalogPath: null,
            expectedTypes: ["segmentation"],
            out var entry);

        entry.Should().NotBeNull();
        entry!.Type.Should().Be("segmentation");
        File.Exists(resolved).Should().BeTrue();
    }

    [Fact]
    public void ResolveExplicitOrCatalogPath_WithAnomalyEmbeddingModelId_ShouldReturnExistingPath()
    {
        var resolved = ModelCatalog.ResolveExplicitOrCatalogPath(
            explicitPath: null,
            modelId: "anomaly_embedding_identity_2x2",
            catalogPath: null,
            expectedTypes: ["anomaly_embedding"],
            out var entry);

        entry.Should().NotBeNull();
        entry!.Type.Should().Be("anomaly_embedding");
        File.Exists(resolved).Should().BeTrue();
    }
}
