using System.Numerics;
using Acme.Product.Infrastructure.PointCloud;
using Acme.Product.Infrastructure.PointCloud.Filters;
using FluentAssertions;
using Xunit;

namespace Acme.Product.Tests.PointCloud;

public sealed class StatisticalOutlierRemovalTests
{
    [Fact]
    public void Filter_ShouldRemoveMostRedOutliers_WhenOutliersAreSparse()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 77);

        // Note: AddOutliers disposes the input cloud and returns a new one.
        var baseCloud = gen.GeneratePlane(
            center: Vector3.Zero,
            normal: Vector3.UnitZ,
            size: (1.0f, 1.0f),
            density: 5_000,
            noise: 0.0003f,
            includeColors: true,
            includeNormals: false,
            outlierRatio: 0.0f);

        var farBounds = new AxisAlignedBoundingBox
        {
            Min = new Vector3(-50, -50, -50),
            Max = new Vector3(50, 50, 50)
        };

        using var cloud = gen.AddOutliers(baseCloud, outlierRatio: 0.2f, bounds: farBounds);

        var sor = new StatisticalOutlierRemoval();
        using var filtered = sor.Filter(cloud, meanK: 30, stddevMul: 1.0);

        cloud.Colors.Should().NotBeNull();
        filtered.Colors.Should().NotBeNull();

        int redIn = CountRed(cloud);
        int redOut = CountRed(filtered);

        redIn.Should().BeGreaterThan(0);
        redOut.Should().BeLessThan(redIn);

        // Expect most red outliers to be removed for sparse outliers.
        redOut.Should().BeLessThanOrEqualTo((int)(redIn * 0.4));

        // Inliers should mostly remain.
        int inliersIn = cloud.Count - redIn;
        int inliersOut = filtered.Count - redOut;
        inliersOut.Should().BeGreaterThanOrEqualTo((int)(inliersIn * 0.9));
    }

    [Fact]
    public void Filter_NoOutliers_ShouldKeepMostPoints()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 79);
        using var cloud = gen.GenerateSphere(
            center: Vector3.Zero,
            radius: 0.3f,
            numPoints: 3_000,
            noise: 0.0004f,
            includeColors: true,
            includeNormals: true,
            outlierRatio: 0.0f);

        var sor = new StatisticalOutlierRemoval();
        using var filtered = sor.Filter(cloud, meanK: 20, stddevMul: 2.0);

        // SOR is a statistical threshold, so it may remove a small tail even without explicit injected outliers.
        filtered.Count.Should().BeLessThanOrEqualTo(cloud.Count);
        filtered.Count.Should().BeGreaterThanOrEqualTo((int)(cloud.Count * 0.97));
        filtered.Colors.Should().NotBeNull();
        filtered.Normals.Should().NotBeNull();
    }

    private static int CountRed(Acme.Product.Infrastructure.PointCloud.PointCloud cloud)
    {
        if (cloud.Colors == null)
        {
            return 0;
        }

        var c = cloud.Colors.GetGenericIndexer<byte>();
        int red = 0;
        for (int i = 0; i < cloud.Count; i++)
        {
            if (c[i, 0] == 255 && c[i, 1] == 0 && c[i, 2] == 0)
            {
                red++;
            }
        }
        return red;
    }
}
