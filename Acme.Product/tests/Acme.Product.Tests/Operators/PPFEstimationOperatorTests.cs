using System.Numerics;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Infrastructure.PointCloud;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

public sealed class PPFEstimationOperatorTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidPointCloud_ShouldReturnPPFMapAndNormalsCloud()
    {
        var sut = new PPFEstimationOperator(Substitute.For<ILogger<PPFEstimationOperator>>());
        var op = new Operator("ppf", OperatorType.PPFEstimation, 0, 0);
        op.AddParameter(new Parameter(Guid.NewGuid(), "NormalRadius", "NormalRadius", string.Empty, "double", 0.03));
        op.AddParameter(new Parameter(Guid.NewGuid(), "FeatureRadius", "FeatureRadius", string.Empty, "double", 0.06));
        op.AddParameter(new Parameter(Guid.NewGuid(), "UseExistingNormals", "UseExistingNormals", string.Empty, "bool", true));

        var gen = new SyntheticPointCloudGenerator(seed: 141);
        using var cloud = gen.GenerateSphere(
            center: Vector3.Zero,
            radius: 0.2f,
            numPoints: 800,
            noise: 0.0002f,
            includeColors: true,
            includeNormals: true,
            outlierRatio: 0.0f);

        var inputs = new Dictionary<string, object> { ["PointCloud"] = cloud };
        var result = await sut.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData.Should().ContainKey("PPFMap");
        result.OutputData.Should().ContainKey("PointCloudWithNormals");
        result.OutputData.Should().ContainKey("PointCount");

        var map = result.OutputData!["PPFMap"].Should().BeAssignableTo<Dictionary<int, List<Acme.Product.Infrastructure.PointCloud.Features.PPFFeature>>>().Subject;
        map.Should().ContainKey(0);

        var cloudWithNormals = result.OutputData["PointCloudWithNormals"].Should().BeOfType<Acme.Product.Infrastructure.PointCloud.PointCloud>().Subject;
        cloudWithNormals.Normals.Should().NotBeNull();
        cloudWithNormals.Colors.Should().NotBeNull();
        cloudWithNormals.Count.Should().Be(cloud.Count);
    }
}

