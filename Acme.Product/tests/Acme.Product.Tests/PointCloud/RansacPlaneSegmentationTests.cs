using System.Numerics;
using Acme.Product.Infrastructure.PointCloud;
using Acme.Product.Infrastructure.PointCloud.Segmentation;
using FluentAssertions;
using Xunit;

namespace Acme.Product.Tests.PointCloud;

public sealed class RansacPlaneSegmentationTests
{
    [Fact]
    public void Segment_PlaneWithOutliers_ShouldRecoverNormal_AndKeepMostInliers()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 91);

        // Note: AddOutliers disposes the input cloud and returns a new one.
        var baseCloud = gen.GeneratePlane(
            center: Vector3.Zero,
            normal: Vector3.UnitZ,
            size: (1.0f, 1.0f),
            density: 6_000,
            noise: 0.0005f,
            includeColors: false,
            includeNormals: false,
            outlierRatio: 0.0f);

        var farBounds = new AxisAlignedBoundingBox
        {
            Min = new Vector3(-30, -30, -30),
            Max = new Vector3(30, 30, 30)
        };

        using var cloud = gen.AddOutliers(baseCloud, outlierRatio: 0.25f, bounds: farBounds);

        var ransac = new RansacPlaneSegmentation(seed: 123);
        var result = ransac.Segment(
            cloud,
            distanceThreshold: 0.0025f,
            maxIterations: 300,
            minInliers: 2_000);

        result.Inliers.Should().NotBeEmpty();
        result.InlierCount.Should().BeGreaterThanOrEqualTo(2_000);

        // Angle between normals should be small.
        var expected = Vector3.UnitZ;
        var dot = Math.Abs(Vector3.Dot(Vector3.Normalize(result.Normal), expected));
        dot.Should().BeGreaterThan(0.98f); // ~11 degrees

        // Inlier ratio should be high given most points are on the plane.
        var inlierRatio = (float)result.InlierCount / cloud.Count;
        inlierRatio.Should().BeGreaterThan(0.70f);
    }

    [Fact]
    public void Segment_Plane_ShouldProduceInliersWithinThreshold()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 93);
        using var cloud = gen.GeneratePlane(
            center: Vector3.Zero,
            normal: Vector3.UnitZ,
            size: (0.5f, 0.5f),
            density: 4_000,
            noise: 0.0002f,
            includeColors: false,
            includeNormals: false,
            outlierRatio: 0.0f);

        var ransac = new RansacPlaneSegmentation(seed: 321);
        var threshold = 0.0015f;
        var result = ransac.Segment(cloud, distanceThreshold: threshold, maxIterations: 200, minInliers: 500);

        result.InlierCount.Should().BeGreaterThanOrEqualTo(3_000);

        var idx = cloud.Points.GetGenericIndexer<float>();
        for (int t = 0; t < Math.Min(result.Inliers.Length, 200); t++)
        {
            var i = result.Inliers[t];
            var p = new Vector3(idx[i, 0], idx[i, 1], idx[i, 2]);
            var dist = MathF.Abs(Vector3.Dot(result.Normal, p) + result.D);
            dist.Should().BeLessThanOrEqualTo(threshold * 1.25f);
        }
    }
}

