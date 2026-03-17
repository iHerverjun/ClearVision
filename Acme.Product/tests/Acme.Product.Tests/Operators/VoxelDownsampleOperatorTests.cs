using System.Numerics;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using PointCloudModel = Acme.Product.Infrastructure.PointCloud.PointCloud;
using Acme.Product.Infrastructure.PointCloud;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

public sealed class VoxelDownsampleOperatorTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidPointCloud_ShouldReturnDownsampledCloud()
    {
        var sut = new VoxelDownsampleOperator(Substitute.For<ILogger<VoxelDownsampleOperator>>());
        var op = new Operator("voxel", OperatorType.VoxelDownsample, 0, 0);
        op.AddParameter(new Parameter(Guid.NewGuid(), "LeafSize", "LeafSize", string.Empty, "double", 0.03));

        var gen = new SyntheticPointCloudGenerator(seed: 41);
        using var cloud = gen.GenerateSphere(
            center: Vector3.Zero,
            radius: 0.2f,
            numPoints: 20_000,
            noise: 0.0005f,
            includeColors: true,
            includeNormals: true);

        var inputs = new Dictionary<string, object> { ["PointCloud"] = cloud };
        var result = await sut.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData.Should().ContainKey("PointCloud");
        result.OutputData.Should().ContainKey("PointCount");

        var outCloud = result.OutputData!["PointCloud"].Should().BeOfType<PointCloudModel>().Subject;
        Convert.ToInt32(result.OutputData["PointCount"]).Should().Be(outCloud.Count);
        outCloud.Count.Should().BeLessThan(cloud.Count);
    }
}
