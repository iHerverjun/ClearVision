using System.Numerics;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
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

public sealed class PPFMatchOperatorTests
{
    [Fact]
    public async Task ExecuteAsync_ModelToScene_ShouldReturnMatchedPose()
    {
        var sut = new PPFMatchOperator(Substitute.For<ILogger<PPFMatchOperator>>());

        var op = new Operator("ppf_match", OperatorType.PPFMatch, 0, 0);
        op.AddParameter(new Parameter(Guid.NewGuid(), "NormalRadius", "NormalRadius", string.Empty, "double", 0.06));
        op.AddParameter(new Parameter(Guid.NewGuid(), "FeatureRadius", "FeatureRadius", string.Empty, "double", 0.12));
        op.AddParameter(new Parameter(Guid.NewGuid(), "NumSamples", "NumSamples", string.Empty, "int", 180));
        op.AddParameter(new Parameter(Guid.NewGuid(), "ModelRefStride", "ModelRefStride", string.Empty, "int", 2));
        op.AddParameter(new Parameter(Guid.NewGuid(), "RansacIterations", "RansacIterations", string.Empty, "int", 900));
        op.AddParameter(new Parameter(Guid.NewGuid(), "InlierThreshold", "InlierThreshold", string.Empty, "double", 0.01));
        op.AddParameter(new Parameter(Guid.NewGuid(), "MinInliers", "MinInliers", string.Empty, "int", 100));
        op.AddParameter(new Parameter(Guid.NewGuid(), "DistanceStep", "DistanceStep", string.Empty, "double", 0.01));
        op.AddParameter(new Parameter(Guid.NewGuid(), "AngleStepDeg", "AngleStepDeg", string.Empty, "double", 5.0));

        var gen = new SyntheticPointCloudGenerator(seed: 301);
        using var model = BuildAsymmetricModel(gen);

        var gt = Matrix4x4.CreateFromYawPitchRoll(0.4f, -0.2f, 0.3f) * Matrix4x4.CreateTranslation(0.1f, -0.05f, 0.02f);
        using var scene = model.Transform(gt);

        var inputs = new Dictionary<string, object>
        {
            ["ModelPointCloud"] = model,
            ["ScenePointCloud"] = scene
        };

        var result = await sut.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData.Should().ContainKey("IsMatched");
        result.OutputData.Should().ContainKey("TransformMatrix");
        result.OutputData.Should().ContainKey("InlierCount");

        Convert.ToBoolean(result.OutputData!["IsMatched"]).Should().BeTrue();
        Convert.ToInt32(result.OutputData["InlierCount"]).Should().BeGreaterThan(50);
        result.OutputData["TransformMatrix"].Should().BeOfType<Matrix4x4>();
    }

    private static PointCloudModel BuildAsymmetricModel(SyntheticPointCloudGenerator gen)
    {
        using var sphere = gen.GenerateSphere(
            center: new Vector3(0.0f, 0.0f, 0.0f),
            radius: 0.18f,
            numPoints: 1400,
            noise: 0.0004f,
            includeColors: true,
            includeNormals: false,
            outlierRatio: 0.0f);

        using var cube = gen.GenerateCube(
            center: new Vector3(0.28f, 0.10f, -0.04f),
            edgeLength: 0.20f,
            numPoints: 900,
            noise: 0.0004f,
            includeColors: true,
            includeNormals: false,
            outlierRatio: 0.0f);

        return MergeTwo(sphere, cube);
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
