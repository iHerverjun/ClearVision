using Acme.Product.Application.Services;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Interfaces;
using Acme.Product.Infrastructure.Services;
using FluentAssertions;
using NSubstitute;

namespace Acme.Product.Tests.Services;

public class OperatorServiceTests
{
    [Fact]
    public async Task GetLibraryAsync_ShouldIncludeLegacyAndFactoryBackfilledOperators()
    {
        var sut = CreateSut();

        var library = (await sut.GetLibraryAsync()).ToList();

        library.Should().Contain(item => item.Type == "ImageAcquisition");
        library.Should().Contain(item => item.Type == nameof(OperatorType.AnomalyDetection));
        library.Should().Contain(item => item.Type == nameof(OperatorType.DetectionSequenceJudge));
    }

    [Fact]
    public async Task GetMetadataAsync_ForAnomalyDetection_ShouldExposeFactoryParameters()
    {
        var sut = CreateSut();

        var metadata = await sut.GetMetadataAsync(OperatorType.AnomalyDetection);

        metadata.Should().NotBeNull();
        metadata!.Parameters.Should().Contain(parameter => parameter.Name == "FeatureExtractorId");
        metadata.Outputs.Should().Contain(output => output.Name == "Diagnostics");
    }

    private static OperatorService CreateSut()
    {
        var repository = Substitute.For<IOperatorRepository>();
        var factory = new OperatorFactory();
        return new OperatorService(repository, factory);
    }
}
