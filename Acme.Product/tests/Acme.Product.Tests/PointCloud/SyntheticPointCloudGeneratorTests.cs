using System.Numerics;
using Acme.Product.Infrastructure.PointCloud;
using FluentAssertions;
using Xunit;

namespace Acme.Product.Tests.PointCloud;

public sealed class SyntheticPointCloudGeneratorTests
{
    [Fact]
    public void GeneratePlane_ShouldKeepPointsCloseToPlane_WhenNoiseSmall()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 7);

        using var cloud = gen.GeneratePlane(
            center: new Vector3(0, 0, 0),
            normal: new Vector3(0, 0, 1),
            size: (1.0f, 1.0f),
            density: 2000,
            noise: 0.0005f,
            includeColors: true,
            includeNormals: true,
            outlierRatio: 0);

        cloud.Count.Should().Be(2000);
        cloud.Colors.Should().NotBeNull();
        cloud.Normals.Should().NotBeNull();

        var idx = cloud.Points.GetGenericIndexer<float>();
        float maxAbsDist = 0;
        for (int i = 0; i < cloud.Count; i++)
        {
            var z = idx[i, 2];
            maxAbsDist = Math.Max(maxAbsDist, Math.Abs(z));
        }

        // 6-sigma bound for gaussian + some sampling drift.
        maxAbsDist.Should().BeLessThan(0.004f);
    }

    [Fact]
    public void GenerateSphere_ShouldApproximateRadius_WhenNoiseSmall()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 9);

        var center = new Vector3(0.1f, -0.2f, 0.3f);
        const float radius = 0.05f;
        using var cloud = gen.GenerateSphere(center, radius, numPoints: 3000, noise: 0.0005f, includeColors: true, includeNormals: true);

        var idx = cloud.Points.GetGenericIndexer<float>();
        double sum = 0;
        for (int i = 0; i < cloud.Count; i++)
        {
            var p = new Vector3(idx[i, 0], idx[i, 1], idx[i, 2]);
            sum += (p - center).Length();
        }

        var mean = sum / cloud.Count;
        mean.Should().BeApproximately(radius, 0.0015);
    }

    [Fact]
    public void GenerateCylinder_ShouldApproximateRadialDistanceToAxis()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 11);

        var center = Vector3.Zero;
        var axis = Vector3.UnitZ;
        const float radius = 0.03f;
        using var cloud = gen.GenerateCylinder(center, axis, radius, height: 0.1f, numPoints: 2500, noise: 0.0005f, includeColors: false, includeNormals: true);

        var idx = cloud.Points.GetGenericIndexer<float>();
        double sum = 0;
        for (int i = 0; i < cloud.Count; i++)
        {
            var p = new Vector3(idx[i, 0], idx[i, 1], idx[i, 2]);
            var proj = Vector3.Dot(p - center, axis);
            var closest = center + (axis * proj);
            sum += (p - closest).Length();
        }

        var mean = sum / cloud.Count;
        mean.Should().BeApproximately(radius, 0.0015);
    }

    [Fact]
    public void GenerateCube_ShouldFillFacesAndRespectExtents()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 13);

        var center = new Vector3(0.02f, 0.03f, -0.01f);
        const float edge = 0.2f;
        using var cloud = gen.GenerateCube(center, edgeLength: edge, numPoints: 3000, noise: 0.0005f, includeColors: true, includeNormals: true);

        var aabb = cloud.GetAABB();
        aabb.Extent.X.Should().BeApproximately(edge, 0.01f);
        aabb.Extent.Y.Should().BeApproximately(edge, 0.01f);
        aabb.Extent.Z.Should().BeApproximately(edge, 0.01f);
    }

    [Fact]
    public void AddOutliers_ShouldIncreaseCount()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 15);
        using var baseCloud = gen.GenerateSphere(Vector3.Zero, radius: 0.05f, numPoints: 1000, noise: 0.0003f, includeColors: false, includeNormals: false);

        using var withOutliers = gen.AddOutliers(baseCloud, outlierRatio: 0.1f);
        withOutliers.Count.Should().BeGreaterThan(baseCloud.Count);
    }
}
