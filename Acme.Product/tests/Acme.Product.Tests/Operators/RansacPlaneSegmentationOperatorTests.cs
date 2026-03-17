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
using PointCloudModel = Acme.Product.Infrastructure.PointCloud.PointCloud;

namespace Acme.Product.Tests.Operators;

public sealed class RansacPlaneSegmentationOperatorTests
{
    [Fact]
    public async Task ExecuteAsync_WithPlanePointCloud_ShouldReturnPlaneAndInliers()
    {
        var sut = new RansacPlaneSegmentationOperator(Substitute.For<ILogger<RansacPlaneSegmentationOperator>>());
        var op = new Operator("ransac_plane", OperatorType.RansacPlaneSegmentation, 0, 0);
        op.AddParameter(new Parameter(Guid.NewGuid(), "DistanceThreshold", "DistanceThreshold", string.Empty, "double", 0.002));
        op.AddParameter(new Parameter(Guid.NewGuid(), "MaxIterations", "MaxIterations", string.Empty, "int", 250));
        op.AddParameter(new Parameter(Guid.NewGuid(), "MinInliers", "MinInliers", string.Empty, "int", 500));

        var gen = new SyntheticPointCloudGenerator(seed: 101);
        using var cloud = gen.GeneratePlane(
            center: Vector3.Zero,
            normal: Vector3.UnitZ,
            size: (1.0f, 1.0f),
            density: 2_000,
            noise: 0.0004f,
            includeColors: true,
            includeNormals: false,
            outlierRatio: 0.0f);

        var inputs = new Dictionary<string, object> { ["PointCloud"] = cloud };
        var result = await sut.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData.Should().ContainKey("PlaneA");
        result.OutputData.Should().ContainKey("PlaneB");
        result.OutputData.Should().ContainKey("PlaneC");
        result.OutputData.Should().ContainKey("PlaneD");
        result.OutputData.Should().ContainKey("InlierCount");
        result.OutputData.Should().ContainKey("InlierRatio");
        result.OutputData.Should().ContainKey("Inliers");
        result.OutputData.Should().ContainKey("InlierPointCloud");

        var inlierCloud = result.OutputData!["InlierPointCloud"].Should().BeOfType<PointCloudModel>().Subject;
        inlierCloud.Count.Should().BeGreaterThan(500);
        inlierCloud.Colors.Should().NotBeNull();
    }
}

