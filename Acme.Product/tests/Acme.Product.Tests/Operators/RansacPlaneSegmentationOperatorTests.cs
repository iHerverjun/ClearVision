using System.Numerics;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Infrastructure.PointCloud;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;
using Xunit;
using MatPool = Acme.Product.Infrastructure.Memory.MatPool;
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

    [Fact]
    public async Task ExecuteAsync_DefaultSeedMode_ShouldBeStableAcrossRepeatedRuns()
    {
        var sut = new RansacPlaneSegmentationOperator(Substitute.For<ILogger<RansacPlaneSegmentationOperator>>());
        var op = CreateOperator(distanceThreshold: 0.003, maxIterations: 420, minInliers: 1500, randomSeed: null);
        using var cloud = BuildCompetingPlanesCloud(seed: 713);

        var snapshots = new List<SegmentationSnapshot>();
        for (int i = 0; i < 5; i++)
        {
            var result = await sut.ExecuteAsync(op, new Dictionary<string, object> { ["PointCloud"] = cloud });
            result.IsSuccess.Should().BeTrue(result.ErrorMessage);
            snapshots.Add(Capture(result));
        }

        var baseline = snapshots[0];
        snapshots.Should().OnlyContain(s => s.Equals(baseline));
    }

    [Fact]
    public async Task ExecuteAsync_DifferentSeeds_ShouldYieldExplainableDifferences_OnBoundaryScene()
    {
        var sut = new RansacPlaneSegmentationOperator(Substitute.For<ILogger<RansacPlaneSegmentationOperator>>());
        using var cloud = BuildCompetingPlanesCloud(seed: 719);

        var seededA = CreateOperator(distanceThreshold: 0.003, maxIterations: 420, minInliers: 1500, randomSeed: 11);
        var seededB = CreateOperator(distanceThreshold: 0.003, maxIterations: 420, minInliers: 1500, randomSeed: 987654);

        var resultA1 = await sut.ExecuteAsync(seededA, new Dictionary<string, object> { ["PointCloud"] = cloud });
        var resultA2 = await sut.ExecuteAsync(seededA, new Dictionary<string, object> { ["PointCloud"] = cloud });
        var resultB1 = await sut.ExecuteAsync(seededB, new Dictionary<string, object> { ["PointCloud"] = cloud });
        var resultB2 = await sut.ExecuteAsync(seededB, new Dictionary<string, object> { ["PointCloud"] = cloud });

        resultA1.IsSuccess.Should().BeTrue(resultA1.ErrorMessage);
        resultA2.IsSuccess.Should().BeTrue(resultA2.ErrorMessage);
        resultB1.IsSuccess.Should().BeTrue(resultB1.ErrorMessage);
        resultB2.IsSuccess.Should().BeTrue(resultB2.ErrorMessage);

        var a1 = Capture(resultA1);
        var a2 = Capture(resultA2);
        var b1 = Capture(resultB1);
        var b2 = Capture(resultB2);

        a2.Should().Be(a1);
        b2.Should().Be(b1);

        var hasExplainableDifference =
            a1.InlierCount != b1.InlierCount ||
            a1.InlierHash != b1.InlierHash ||
            Math.Abs(a1.PlaneD - b1.PlaneD) > 1e-6 ||
            Math.Abs(a1.PlaneA - b1.PlaneA) > 1e-6 ||
            Math.Abs(a1.PlaneB - b1.PlaneB) > 1e-6 ||
            Math.Abs(a1.PlaneC - b1.PlaneC) > 1e-6;

        hasExplainableDifference.Should().BeTrue("different seeds should allow different but deterministic hypotheses on boundary data");
    }

    private static Operator CreateOperator(double distanceThreshold, int maxIterations, int minInliers, int? randomSeed)
    {
        var op = new Operator("ransac_plane", OperatorType.RansacPlaneSegmentation, 0, 0);
        op.AddParameter(new Parameter(Guid.NewGuid(), "DistanceThreshold", "DistanceThreshold", string.Empty, "double", distanceThreshold));
        op.AddParameter(new Parameter(Guid.NewGuid(), "MaxIterations", "MaxIterations", string.Empty, "int", maxIterations));
        op.AddParameter(new Parameter(Guid.NewGuid(), "MinInliers", "MinInliers", string.Empty, "int", minInliers));
        if (randomSeed.HasValue)
        {
            op.AddParameter(new Parameter(Guid.NewGuid(), "RandomSeed", "RandomSeed", string.Empty, "int", randomSeed.Value));
        }

        return op;
    }

    private static PointCloudModel BuildCompetingPlanesCloud(int seed)
    {
        var gen = new SyntheticPointCloudGenerator(seed: seed);

        using var planeA = gen.GeneratePlane(
            center: Vector3.Zero,
            normal: Vector3.UnitZ,
            size: (1.2f, 1.0f),
            density: 3_200,
            noise: 0.0008f,
            includeColors: false,
            includeNormals: false,
            outlierRatio: 0.0f);

        using var planeB = gen.GeneratePlane(
            center: new Vector3(0.02f, -0.01f, 0.012f),
            normal: Vector3.Normalize(new Vector3(0.08f, 0.04f, 0.996f)),
            size: (1.1f, 1.0f),
            density: 3_000,
            noise: 0.0008f,
            includeColors: false,
            includeNormals: false,
            outlierRatio: 0.0f);

        return MergeTwo(planeA, planeB);
    }

    private static PointCloudModel MergeTwo(PointCloudModel a, PointCloudModel b)
    {
        var pool = MatPool.Shared;
        var total = a.Count + b.Count;

        var points = pool.Rent(width: 3, height: total, type: MatType.CV_32FC1);
        a.Points.CopyTo(points.RowRange(0, a.Count));
        b.Points.CopyTo(points.RowRange(a.Count, total));

        return new PointCloudModel(points, colors: null, normals: null, isOrganized: false, pool: pool);
    }

    private static SegmentationSnapshot Capture(OperatorExecutionOutput output)
    {
        var data = output.OutputData!;
        var inliers = data["Inliers"].Should().BeOfType<int[]>().Subject;

        int hash = 17;
        int take = Math.Min(inliers.Length, 64);
        for (int i = 0; i < take; i++)
        {
            hash = unchecked((hash * 31) + inliers[i]);
        }

        return new SegmentationSnapshot(
            PlaneA: Convert.ToDouble(data["PlaneA"]),
            PlaneB: Convert.ToDouble(data["PlaneB"]),
            PlaneC: Convert.ToDouble(data["PlaneC"]),
            PlaneD: Convert.ToDouble(data["PlaneD"]),
            InlierCount: Convert.ToInt32(data["InlierCount"]),
            InlierHash: hash);
    }

    private readonly record struct SegmentationSnapshot(
        double PlaneA,
        double PlaneB,
        double PlaneC,
        double PlaneD,
        int InlierCount,
        int InlierHash);
}

