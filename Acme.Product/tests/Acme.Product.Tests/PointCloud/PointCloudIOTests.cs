using System.Numerics;
using Acme.Product.Infrastructure.PointCloud;
using FluentAssertions;
using Xunit;

namespace Acme.Product.Tests.PointCloud;

public sealed class PointCloudIOTests
{
    [Fact]
    public void SaveLoadPCD_ShouldRoundTripPointCountAndOptionalChannels()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 21);
        using var cloud = gen.GenerateSphere(
            center: new Vector3(0, 0, 0),
            radius: 0.05f,
            numPoints: 1200,
            noise: 0.0001f,
            includeColors: true,
            includeNormals: true);

        var dir = Path.Combine(Path.GetTempPath(), "ClearVision_PointCloudIOTests");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"sphere_{Guid.NewGuid():N}.pcd");

        PointCloudIO.SavePCD(path, cloud);
        using var loaded = PointCloudIO.LoadPCD(path);

        loaded.Count.Should().Be(cloud.Count);
        loaded.Colors.Should().NotBeNull();
        loaded.Normals.Should().NotBeNull();
    }

    [Fact]
    public void SaveLoadPLY_ShouldRoundTripPointCountAndOptionalChannels()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 23);
        using var cloud = gen.GenerateCube(
            center: new Vector3(0.02f, -0.03f, 0.01f),
            edgeLength: 0.2f,
            numPoints: 900,
            noise: 0.0001f,
            includeColors: true,
            includeNormals: true);

        var dir = Path.Combine(Path.GetTempPath(), "ClearVision_PointCloudIOTests");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"cube_{Guid.NewGuid():N}.ply");

        PointCloudIO.SavePLY(path, cloud);
        using var loaded = PointCloudIO.LoadPLY(path);

        loaded.Count.Should().Be(cloud.Count);
        loaded.Colors.Should().NotBeNull();
        loaded.Normals.Should().NotBeNull();
    }

    [Fact]
    public void Load_ShouldDispatchByExtension()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 25);
        using var cloud = gen.GeneratePlane(
            center: Vector3.Zero,
            normal: Vector3.UnitZ,
            size: (1.0f, 1.0f),
            density: 300,
            noise: 0.0f,
            includeColors: false,
            includeNormals: true);

        var dir = Path.Combine(Path.GetTempPath(), "ClearVision_PointCloudIOTests");
        Directory.CreateDirectory(dir);
        var pcdPath = Path.Combine(dir, $"plane_{Guid.NewGuid():N}.pcd");
        var plyPath = Path.Combine(dir, $"plane_{Guid.NewGuid():N}.ply");

        PointCloudIO.SavePCD(pcdPath, cloud);
        PointCloudIO.SavePLY(plyPath, cloud);

        using var pcd = PointCloudIO.Load(pcdPath);
        using var ply = PointCloudIO.Load(plyPath);

        pcd.Count.Should().Be(cloud.Count);
        ply.Count.Should().Be(cloud.Count);
    }
}
