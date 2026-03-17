using System.Numerics;
using Acme.Product.Infrastructure.PointCloud;
using Acme.Product.Infrastructure.PointCloud.Filters;
using FluentAssertions;
using Xunit;

namespace Acme.Product.Tests.PointCloud;

public sealed class VoxelGridFilterTests
{
    [Fact]
    public void Downsample_ShouldReducePoints_WhenLeafIsLarge()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 31);
        using var cloud = gen.GenerateCube(
            center: Vector3.Zero,
            edgeLength: 0.5f,
            numPoints: 100_000,
            noise: 0.0005f,
            includeColors: true,
            includeNormals: true);

        var filter = new VoxelGridFilter();
        using var down = filter.Downsample(cloud, leafSize: 0.05f);

        down.Count.Should().BeLessThan(cloud.Count);
        down.Count.Should().BeGreaterThan(10);
        down.Colors.Should().NotBeNull();
        down.Normals.Should().NotBeNull();
    }

    [Fact]
    public void Downsample_Plane_ShouldStayCloseToOriginalPlane()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 33);
        using var cloud = gen.GeneratePlane(
            center: Vector3.Zero,
            normal: Vector3.UnitZ,
            size: (1.0f, 1.0f),
            density: 50_000,
            noise: 0.0003f,
            includeColors: false,
            includeNormals: true);

        var filter = new VoxelGridFilter();
        using var down = filter.Downsample(cloud, leafSize: 0.02f);

        var idx = down.Points.GetGenericIndexer<float>();
        float maxAbsZ = 0;
        for (int i = 0; i < down.Count; i++)
        {
            maxAbsZ = Math.Max(maxAbsZ, Math.Abs(idx[i, 2]));
        }

        maxAbsZ.Should().BeLessThan(0.005f);
    }
}

