using System.Numerics;
using Acme.Product.Infrastructure.PointCloud;
using Acme.Product.Infrastructure.PointCloud.Segmentation;
using FluentAssertions;
using OpenCvSharp;
using Xunit;
using PointCloudModel = Acme.Product.Infrastructure.PointCloud.PointCloud;
using MatPool = Acme.Product.Infrastructure.Memory.MatPool;

namespace Acme.Product.Tests.PointCloud;

public sealed class EuclideanClusterExtractionTests
{
    [Fact]
    public void Extract_TwoSeparatedClusters_ShouldReturnTwoClusters()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 111);
        using var c1 = gen.GenerateSphere(
            center: new Vector3(-0.3f, 0, 0),
            radius: 0.08f,
            numPoints: 1500,
            noise: 0.0004f,
            includeColors: true,
            includeNormals: false,
            outlierRatio: 0.0f);

        using var c2 = gen.GenerateSphere(
            center: new Vector3(0.3f, 0, 0),
            radius: 0.08f,
            numPoints: 1400,
            noise: 0.0004f,
            includeColors: true,
            includeNormals: false,
            outlierRatio: 0.0f);

        using var cloud = MergeTwo(c1, c2);

        var extractor = new EuclideanClusterExtraction();
        var clusters = extractor.Extract(cloud, clusterTolerance: 0.03f, minClusterSize: 200, maxClusterSize: 10_000);

        clusters.Count.Should().Be(2);
        clusters[0].Length.Should().BeGreaterThan(500);
        clusters[1].Length.Should().BeGreaterThan(500);
        (clusters[0].Length + clusters[1].Length).Should().Be(cloud.Count);
    }

    [Fact]
    public void Extract_WithMinMaxFilter_ShouldDropSmallCluster()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 113);
        using var big = gen.GenerateCube(
            center: Vector3.Zero,
            edgeLength: 0.2f,
            numPoints: 1200,
            noise: 0.0003f,
            includeColors: false,
            includeNormals: false,
            outlierRatio: 0.0f);

        using var small = gen.GenerateCube(
            center: new Vector3(0.6f, 0, 0),
            edgeLength: 0.03f,
            numPoints: 50,
            noise: 0.0002f,
            includeColors: false,
            includeNormals: false,
            outlierRatio: 0.0f);

        using var cloud = MergeTwo(big, small);

        var extractor = new EuclideanClusterExtraction();
        var clusters = extractor.Extract(cloud, clusterTolerance: 0.04f, minClusterSize: 200, maxClusterSize: 10_000);

        clusters.Count.Should().Be(1);
        clusters[0].Length.Should().BeGreaterThanOrEqualTo(1000);
    }

    private static PointCloudModel MergeTwo(PointCloudModel a, PointCloudModel b)
    {
        var pool = MatPool.Shared;
        var total = a.Count + b.Count;

        var points = pool.Rent(width: 3, height: total, type: MatType.CV_32FC1);
        a.Points.CopyTo(points.RowRange(0, a.Count));
        b.Points.CopyTo(points.RowRange(a.Count, total));

        Mat? colors = null;
        if (a.Colors != null && b.Colors != null)
        {
            colors = pool.Rent(width: 3, height: total, type: MatType.CV_8UC1);
            a.Colors.CopyTo(colors.RowRange(0, a.Count));
            b.Colors.CopyTo(colors.RowRange(a.Count, total));
        }

        Mat? normals = null;
        if (a.Normals != null && b.Normals != null)
        {
            normals = pool.Rent(width: 3, height: total, type: MatType.CV_32FC1);
            a.Normals.CopyTo(normals.RowRange(0, a.Count));
            b.Normals.CopyTo(normals.RowRange(a.Count, total));
        }

        return new PointCloudModel(points, colors, normals, isOrganized: false, pool: pool);
    }
}
