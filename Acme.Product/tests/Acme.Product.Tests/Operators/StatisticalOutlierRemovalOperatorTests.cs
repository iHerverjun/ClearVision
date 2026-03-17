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

public sealed class StatisticalOutlierRemovalOperatorTests
{
    [Fact]
    public async Task ExecuteAsync_WithOutliers_ShouldReducePointCount()
    {
        var sut = new StatisticalOutlierRemovalOperator(Substitute.For<ILogger<StatisticalOutlierRemovalOperator>>());
        var op = new Operator("sor", OperatorType.StatisticalOutlierRemoval, 0, 0);
        op.AddParameter(new Parameter(Guid.NewGuid(), "MeanK", "MeanK", string.Empty, "int", 30));
        op.AddParameter(new Parameter(Guid.NewGuid(), "StddevMul", "StddevMul", string.Empty, "double", 1.0));

        var gen = new SyntheticPointCloudGenerator(seed: 81);
        var baseCloud = gen.GenerateCube(
            center: Vector3.Zero,
            edgeLength: 0.4f,
            numPoints: 10_000,
            noise: 0.0004f,
            includeColors: true,
            includeNormals: false,
            outlierRatio: 0.0f);

        var farBounds = new AxisAlignedBoundingBox
        {
            Min = new Vector3(-40, -40, -40),
            Max = new Vector3(40, 40, 40)
        };

        using var cloud = gen.AddOutliers(baseCloud, outlierRatio: 0.15f, bounds: farBounds);

        var inputs = new Dictionary<string, object> { ["PointCloud"] = cloud };
        var result = await sut.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData.Should().ContainKey("PointCloud");
        result.OutputData.Should().ContainKey("PointCount");
        result.OutputData.Should().ContainKey("RemovedCount");

        var outCloud = result.OutputData!["PointCloud"].Should().BeOfType<PointCloudModel>().Subject;
        Convert.ToInt32(result.OutputData["PointCount"]).Should().Be(outCloud.Count);
        Convert.ToInt32(result.OutputData["RemovedCount"]).Should().BeGreaterThan(0);
        outCloud.Count.Should().BeLessThan(cloud.Count);
    }
}

