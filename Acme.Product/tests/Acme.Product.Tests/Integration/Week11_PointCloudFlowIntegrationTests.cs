using System.Collections;
using System.Diagnostics;
using System.Numerics;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Memory;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Infrastructure.PointCloud;
using Acme.Product.Tests.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;
using Xunit;
using PointCloudModel = Acme.Product.Infrastructure.PointCloud.PointCloud;

namespace Acme.Product.Tests.Integration;

public sealed class Week11_PointCloudFlowIntegrationTests
{
    [Fact]
    public async Task Flow_Downsample_Sor_RansacPlane_ShouldFindPlaneWithHighInlierRatio()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 701);

        using var plane = gen.GeneratePlane(
            center: Vector3.Zero,
            normal: Vector3.UnitZ,
            size: (1.0f, 1.0f),
            density: 6000,
            noise: 0.0010f,
            includeColors: false,
            includeNormals: false,
            outlierRatio: 0);

        using var cloud = gen.AddOutliers(plane, outlierRatio: 0.2f, bounds: new AxisAlignedBoundingBox
        {
            Min = new Vector3(-2, -2, -2),
            Max = new Vector3(2, 2, 2)
        });

        var voxelExec = new VoxelDownsampleOperator(NullLogger<VoxelDownsampleOperator>.Instance);
        var sorExec = new StatisticalOutlierRemovalOperator(NullLogger<StatisticalOutlierRemovalOperator>.Instance);
        var ransacExec = new RansacPlaneSegmentationOperator(NullLogger<RansacPlaneSegmentationOperator>.Instance);

        var voxelOp = new Operator("voxel", OperatorType.VoxelDownsample, 0, 0);
        voxelOp.AddParameter(TestHelpers.CreateParameter("LeafSize", 0.01, "double"));

        var sorOp = new Operator("sor", OperatorType.StatisticalOutlierRemoval, 0, 0);
        sorOp.AddParameter(TestHelpers.CreateParameter("MeanK", 40, "int"));
        sorOp.AddParameter(TestHelpers.CreateParameter("StddevMul", 1.0, "double"));

        var ransacOp = new Operator("ransac", OperatorType.RansacPlaneSegmentation, 0, 0);
        ransacOp.AddParameter(TestHelpers.CreateParameter("DistanceThreshold", 0.01, "double"));
        ransacOp.AddParameter(TestHelpers.CreateParameter("MaxIterations", 1500, "int"));
        ransacOp.AddParameter(TestHelpers.CreateParameter("MinInliers", 2000, "int"));

        var voxelResult = await voxelExec.ExecuteAsync(voxelOp, new Dictionary<string, object> { ["PointCloud"] = cloud });
        voxelResult.IsSuccess.Should().BeTrue(voxelResult.ErrorMessage);
        var downsampled = voxelResult.OutputData!["PointCloud"].Should().BeOfType<PointCloudModel>().Subject;

        var sorResult = await sorExec.ExecuteAsync(sorOp, new Dictionary<string, object> { ["PointCloud"] = downsampled });
        sorResult.IsSuccess.Should().BeTrue(sorResult.ErrorMessage);
        var filtered = sorResult.OutputData!["PointCloud"].Should().BeOfType<PointCloudModel>().Subject;

        var ransacResult = await ransacExec.ExecuteAsync(ransacOp, new Dictionary<string, object> { ["PointCloud"] = filtered });
        ransacResult.IsSuccess.Should().BeTrue(ransacResult.ErrorMessage);

        var ratio = Convert.ToDouble(ransacResult.OutputData!["InlierRatio"]);
        ratio.Should().BeGreaterThan(0.80);

        var n = new Vector3(
            (float)Convert.ToDouble(ransacResult.OutputData["PlaneA"]),
            (float)Convert.ToDouble(ransacResult.OutputData["PlaneB"]),
            (float)Convert.ToDouble(ransacResult.OutputData["PlaneC"]));
        n = Vector3.Normalize(n);
        MathF.Abs(Vector3.Dot(n, Vector3.UnitZ)).Should().BeGreaterThan(0.95f);

        // Cleanup point clouds produced by operators.
        downsampled.Dispose();
        filtered.Dispose();

        if (ransacResult.OutputData.TryGetValue("InlierPointCloud", out var inlierCloudObj) && inlierCloudObj is PointCloudModel inlierCloud)
        {
            inlierCloud.Dispose();
        }
    }

    [Fact]
    public async Task Flow_Clustering_ShouldExtractTwoObjects()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 703);

        using var cubeA = gen.GenerateCube(
            center: new Vector3(-0.25f, 0.0f, 0.0f),
            edgeLength: 0.18f,
            numPoints: 2000,
            noise: 0.0008f,
            includeColors: false,
            includeNormals: false,
            outlierRatio: 0.0f);

        using var cubeB = gen.GenerateCube(
            center: new Vector3(+0.25f, 0.0f, 0.0f),
            edgeLength: 0.18f,
            numPoints: 2200,
            noise: 0.0008f,
            includeColors: false,
            includeNormals: false,
            outlierRatio: 0.0f);

        using var merged = MergeTwo(cubeA, cubeB);

        var voxelExec = new VoxelDownsampleOperator(NullLogger<VoxelDownsampleOperator>.Instance);
        var clusterExec = new EuclideanClusterExtractionOperator(NullLogger<EuclideanClusterExtractionOperator>.Instance);

        var voxelOp = new Operator("voxel", OperatorType.VoxelDownsample, 0, 0);
        voxelOp.AddParameter(TestHelpers.CreateParameter("LeafSize", 0.01, "double"));

        var clusterOp = new Operator("cluster", OperatorType.EuclideanClusterExtraction, 0, 0);
        clusterOp.AddParameter(TestHelpers.CreateParameter("ClusterTolerance", 0.05, "double"));
        clusterOp.AddParameter(TestHelpers.CreateParameter("MinClusterSize", 150, "int"));
        clusterOp.AddParameter(TestHelpers.CreateParameter("MaxClusterSize", 5000000, "int"));

        var voxelResult = await voxelExec.ExecuteAsync(voxelOp, new Dictionary<string, object> { ["PointCloud"] = merged });
        voxelResult.IsSuccess.Should().BeTrue(voxelResult.ErrorMessage);
        var downsampled = voxelResult.OutputData!["PointCloud"].Should().BeOfType<PointCloudModel>().Subject;

        var clusterResult = await clusterExec.ExecuteAsync(clusterOp, new Dictionary<string, object> { ["PointCloud"] = downsampled });
        clusterResult.IsSuccess.Should().BeTrue(clusterResult.ErrorMessage);

        Convert.ToInt32(clusterResult.OutputData!["ClusterCount"]).Should().Be(2);

        var clusterClouds = clusterResult.OutputData["ClusterPointClouds"].Should().BeOfType<List<PointCloudModel>>().Subject;
        clusterClouds.Count.Should().Be(2);
        clusterClouds.All(c => c.Count > 200).Should().BeTrue();

        downsampled.Dispose();
        foreach (var c in clusterClouds)
        {
            c.Dispose();
        }
    }

    [Fact]
    public async Task Flow_ModelMatching_ShouldMatchModelInScene()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 707);
        using var model = BuildAsymmetricModel(gen);

        // Ground truth pose: model -> scene
        var rot = Matrix4x4.CreateFromYawPitchRoll(0.4f, -0.2f, 0.3f);
        var gt = rot * Matrix4x4.CreateTranslation(0.10f, -0.05f, 0.02f);

        using var sceneFull = model.Transform(gt);

        // Keep this integration scenario easy and deterministic: no occlusion/outliers.
        // Robustness under occlusion/outliers is covered by the dedicated matcher unit tests.
        using var scene = sceneFull;

        var matchExec = new PPFMatchOperator(NullLogger<PPFMatchOperator>.Instance);

        var matchOp = new Operator("ppf_match", OperatorType.PPFMatch, 0, 0);
        matchOp.AddParameter(TestHelpers.CreateParameter("NormalRadius", 0.06, "double"));
        matchOp.AddParameter(TestHelpers.CreateParameter("FeatureRadius", 0.12, "double"));
        matchOp.AddParameter(TestHelpers.CreateParameter("NumSamples", 200, "int"));
        matchOp.AddParameter(TestHelpers.CreateParameter("ModelRefStride", 2, "int"));
        matchOp.AddParameter(TestHelpers.CreateParameter("Seed", 123, "int"));
        matchOp.AddParameter(TestHelpers.CreateParameter("RansacIterations", 1200, "int"));
        matchOp.AddParameter(TestHelpers.CreateParameter("InlierThreshold", 0.01, "double"));
        matchOp.AddParameter(TestHelpers.CreateParameter("MinInliers", 100, "int"));
        matchOp.AddParameter(TestHelpers.CreateParameter("DistanceStep", 0.01, "double"));
        matchOp.AddParameter(TestHelpers.CreateParameter("AngleStepDeg", 5.0, "double"));

        var matchInputs = new Dictionary<string, object>
        {
            ["ModelPointCloud"] = model,
            ["ScenePointCloud"] = scene
        };

        var stopwatch = Stopwatch.StartNew();
        var matchResult = await matchExec.ExecuteAsync(matchOp, matchInputs);
        stopwatch.Stop();
        matchResult.IsSuccess.Should().BeTrue(matchResult.ErrorMessage);

        Convert.ToBoolean(matchResult.OutputData!["IsMatched"]).Should().BeTrue();
        var inliers = Convert.ToInt32(matchResult.OutputData["InlierCount"]);
        inliers.Should().BeGreaterThanOrEqualTo(100);

        var rms = Convert.ToDouble(matchResult.OutputData["RmsError"]);
        var estimated = (Matrix4x4)matchResult.OutputData["TransformMatrix"];
        var tErr = TranslationError(estimated, gt);
        var rErrDeg = RotationErrorDegrees(estimated, gt);

        rms.Should().BeLessThan(0.02,
            $"RMS={rms:0.0000}, Inliers={inliers}, TErr={tErr * 1000:0.0}mm, RErr={rErrDeg:0.0}deg");
        tErr.Should().BeLessThan(0.005,
            $"PPF translation error should satisfy <5mm acceptance, actual={tErr * 1000:0.00}mm");
        stopwatch.Elapsed.TotalMilliseconds.Should().BeLessThan(3000,
            $"PPF matching should satisfy <3s acceptance, actual={stopwatch.Elapsed.TotalMilliseconds:0.0}ms");
    }

    private static double TranslationError(Matrix4x4 estimated, Matrix4x4 gt)
    {
        var te = new Vector3(estimated.M41, estimated.M42, estimated.M43);
        var tg = new Vector3(gt.M41, gt.M42, gt.M43);
        return (te - tg).Length();
    }

    private static double RotationErrorDegrees(Matrix4x4 estimated, Matrix4x4 gt)
    {
        var qe = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(estimated));
        var qg = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(gt));
        var dot = Math.Abs((double)Quaternion.Dot(qe, qg));
        dot = Math.Min(1.0, Math.Max(0.0, dot));
        var angleRad = 2.0 * Math.Acos(dot);
        return angleRad * (180.0 / Math.PI);
    }

    private static PointCloudModel BuildAsymmetricModel(SyntheticPointCloudGenerator gen)
    {
        // Use a strongly non-symmetric model to make pose recovery unambiguous.
        using var cubeA = gen.GenerateCube(
            center: new Vector3(0.00f, 0.00f, 0.00f),
            edgeLength: 0.24f,
            numPoints: 2200,
            noise: 0.0004f,
            includeColors: true,
            includeNormals: false,
            outlierRatio: 0.0f);

        using var cubeB = gen.GenerateCube(
            center: new Vector3(0.38f, 0.10f, -0.06f),
            edgeLength: 0.18f,
            numPoints: 1400,
            noise: 0.0004f,
            includeColors: true,
            includeNormals: false,
            outlierRatio: 0.0f);

        using var cubeC = gen.GenerateCube(
            center: new Vector3(-0.20f, 0.26f, 0.14f),
            edgeLength: 0.12f,
            numPoints: 900,
            noise: 0.0004f,
            includeColors: true,
            includeNormals: false,
            outlierRatio: 0.0f);

        using var ab = MergeTwo(cubeA, cubeB);
        return MergeTwo(ab, cubeC);
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

        return new PointCloudModel(points, colors, normals: null, isOrganized: false, pool: pool);
    }
}
