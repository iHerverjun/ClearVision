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

public sealed class EuclideanClusterExtractionOperatorTests
{
    [Fact]
    public async Task ExecuteAsync_WithTwoClusters_ShouldReturnClusterCountAndClouds()
    {
        var sut = new EuclideanClusterExtractionOperator(Substitute.For<ILogger<EuclideanClusterExtractionOperator>>());
        var op = new Operator("cluster", OperatorType.EuclideanClusterExtraction, 0, 0);
        op.AddParameter(new Parameter(Guid.NewGuid(), "ClusterTolerance", "ClusterTolerance", string.Empty, "double", 0.03));
        op.AddParameter(new Parameter(Guid.NewGuid(), "MinClusterSize", "MinClusterSize", string.Empty, "int", 100));
        op.AddParameter(new Parameter(Guid.NewGuid(), "MaxClusterSize", "MaxClusterSize", string.Empty, "int", 100000));

        var gen = new SyntheticPointCloudGenerator(seed: 121);
        using var c1 = gen.GenerateSphere(
            center: new Vector3(-0.2f, 0, 0),
            radius: 0.06f,
            numPoints: 900,
            noise: 0.0004f,
            includeColors: true,
            includeNormals: false,
            outlierRatio: 0.0f);

        using var c2 = gen.GenerateSphere(
            center: new Vector3(0.2f, 0, 0),
            radius: 0.06f,
            numPoints: 850,
            noise: 0.0004f,
            includeColors: true,
            includeNormals: false,
            outlierRatio: 0.0f);

        using var cloud = MergeTwo(c1, c2);

        var inputs = new Dictionary<string, object> { ["PointCloud"] = cloud };
        var result = await sut.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData.Should().ContainKey("ClusterCount");
        result.OutputData.Should().ContainKey("Clusters");
        result.OutputData.Should().ContainKey("ClusterPointClouds");

        Convert.ToInt32(result.OutputData!["ClusterCount"]).Should().Be(2);
    }

    private static Acme.Product.Infrastructure.PointCloud.PointCloud MergeTwo(
        Acme.Product.Infrastructure.PointCloud.PointCloud a,
        Acme.Product.Infrastructure.PointCloud.PointCloud b)
    {
        // Duplicated helper to keep test self-contained.
        var pool = Acme.Product.Infrastructure.Memory.MatPool.Shared;
        var total = a.Count + b.Count;

        var points = pool.Rent(width: 3, height: total, type: OpenCvSharp.MatType.CV_32FC1);
        a.Points.CopyTo(points.RowRange(0, a.Count));
        b.Points.CopyTo(points.RowRange(a.Count, total));

        OpenCvSharp.Mat? colors = null;
        if (a.Colors != null && b.Colors != null)
        {
            colors = pool.Rent(width: 3, height: total, type: OpenCvSharp.MatType.CV_8UC1);
            a.Colors.CopyTo(colors.RowRange(0, a.Count));
            b.Colors.CopyTo(colors.RowRange(a.Count, total));
        }

        return new Acme.Product.Infrastructure.PointCloud.PointCloud(points, colors, normals: null, isOrganized: false, pool: pool);
    }
}

