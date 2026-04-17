using System.Numerics;
using Acme.Product.Infrastructure.PointCloud;
using Acme.Product.Infrastructure.PointCloud.Features;
using FluentAssertions;
using Xunit;

namespace Acme.Product.Tests.PointCloud;

public sealed class PPFEstimationTests
{
    [Fact]
    public void PPFFeature_Compute_ShouldMatchSimpleGeometry()
    {
        var p1 = Vector3.Zero;
        var p2 = new Vector3(1, 0, 0);

        var nAligned = new Vector3(1, 0, 0);
        var nPerp = new Vector3(0, 1, 0);

        var f1 = PPFFeature.Compute(p1, nAligned, p2, nAligned);
        f1.Distance.Should().BeApproximately(1.0f, 1e-6f);
        f1.Angle1.Should().BeApproximately(0.0f, 1e-6f);
        f1.Angle2.Should().BeApproximately(0.0f, 1e-6f);
        f1.AngleNormals.Should().BeApproximately(0.0f, 1e-6f);

        var f2 = PPFFeature.Compute(p1, nPerp, p2, nPerp);
        f2.Distance.Should().BeApproximately(1.0f, 1e-6f);
        f2.Angle1.Should().BeApproximately(MathF.PI / 2f, 1e-5f);
        f2.Angle2.Should().BeApproximately(MathF.PI / 2f, 1e-5f);
        f2.AngleNormals.Should().BeApproximately(0.0f, 1e-6f);
    }

    [Fact]
    public void ComputeModel_RotationAndTranslation_ShouldBeInvariant_ForSamePointOrder()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 131);
        using var cloud = gen.GenerateSphere(
            center: Vector3.Zero,
            radius: 0.25f,
            numPoints: 2000,
            noise: 0.0002f,
            includeColors: false,
            includeNormals: true,
            outlierRatio: 0.0f);

        var estimator = new PPFEstimation();
        var map1 = estimator.ComputeModel(cloud, normalRadius: 0.03f, featureRadius: 0.06f, useExistingNormals: true);

        var rot = Matrix4x4.CreateFromYawPitchRoll(0.7f, -0.35f, 0.4f);
        var xform = rot * Matrix4x4.CreateTranslation(0.12f, -0.07f, 0.03f);
        using var transformed = cloud.Transform(xform);

        var map2 = estimator.ComputeModel(transformed, normalRadius: 0.03f, featureRadius: 0.06f, useExistingNormals: true);

        // Compare a few reference points using quantized histograms to avoid ordering sensitivity.
        foreach (var i in new[] { 0, 100, 500, 999, 1500 })
        {
            map1.Should().ContainKey(i);
            map2.Should().ContainKey(i);

            var h1 = Histogram(map1[i], distStep: 0.005f, angleStep: 0.05f);
            var h2 = Histogram(map2[i], distStep: 0.005f, angleStep: 0.05f);

            h1.Count.Should().Be(h2.Count);
            foreach (var (key, count) in h1)
            {
                h2.TryGetValue(key, out var c2).Should().BeTrue();
                c2.Should().Be(count);
            }
        }
    }

    [Fact]
    public void ComputePointCloudWithNormals_ShouldRecoverConsistentOrientation_FromGloballyFlippedExistingNormals()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 181);
        using var cloud = gen.GenerateSphere(
            center: new Vector3(0.04f, -0.03f, 0.02f),
            radius: 0.25f,
            numPoints: 1800,
            noise: 0.0002f,
            includeColors: false,
            includeNormals: true,
            outlierRatio: 0.0f);
        using var flipped = FlipNormals(cloud);

        var estimator = new PPFEstimation();
        using var oriented = estimator.ComputePointCloudWithNormals(flipped, normalRadius: 0.03f, useExistingNormals: true);

        oriented.Normals.Should().NotBeNull();
        var sourceNormals = cloud.Normals!.GetGenericIndexer<float>();
        var orientedNormals = oriented.Normals!.GetGenericIndexer<float>();

        double sumDot = 0;
        var stronglyAligned = 0;
        for (int i = 0; i < cloud.Count; i++)
        {
            var expected = new Vector3(sourceNormals[i, 0], sourceNormals[i, 1], sourceNormals[i, 2]);
            var actual = new Vector3(orientedNormals[i, 0], orientedNormals[i, 1], orientedNormals[i, 2]);
            var dot = Vector3.Dot(expected, actual);
            sumDot += dot;
            if (dot >= 0.95f)
            {
                stronglyAligned++;
            }
        }

        (sumDot / cloud.Count).Should().BeGreaterThan(0.98);
        stronglyAligned.Should().BeGreaterThan((int)(cloud.Count * 0.95));
    }

    [Fact]
    public void ComputeModel_ShouldRemainInvariant_WhenExistingNormalsAreGloballyFlipped()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 191);
        using var cloud = gen.GenerateSphere(
            center: new Vector3(-0.03f, 0.01f, 0.05f),
            radius: 0.22f,
            numPoints: 2200,
            noise: 0.0002f,
            includeColors: false,
            includeNormals: true,
            outlierRatio: 0.0f);
        using var flipped = FlipNormals(cloud);

        var estimator = new PPFEstimation();
        var originalMap = estimator.ComputeModel(cloud, normalRadius: 0.03f, featureRadius: 0.06f, useExistingNormals: true);
        var flippedMap = estimator.ComputeModel(flipped, normalRadius: 0.03f, featureRadius: 0.06f, useExistingNormals: true);

        foreach (var i in new[] { 0, 100, 500, 999, 1500 })
        {
            var h1 = Histogram(originalMap[i], distStep: 0.005f, angleStep: 0.05f);
            var h2 = Histogram(flippedMap[i], distStep: 0.005f, angleStep: 0.05f);

            h1.Count.Should().Be(h2.Count);
            foreach (var (key, count) in h1)
            {
                h2.TryGetValue(key, out var c2).Should().BeTrue();
                c2.Should().Be(count);
            }
        }
    }

    private static Dictionary<int, int> Histogram(List<PPFFeature> features, float distStep, float angleStep)
    {
        var dict = new Dictionary<int, int>(capacity: Math.Max(16, features.Count));

        for (int i = 0; i < features.Count; i++)
        {
            var f = features[i];

            int qd = (int)MathF.Round(f.Distance / distStep);
            int qa1 = (int)MathF.Round(f.Angle1 / angleStep);
            int qa2 = (int)MathF.Round(f.Angle2 / angleStep);
            int qan = (int)MathF.Round(f.AngleNormals / angleStep);

            qd = Math.Clamp(qd, 0, 1023);
            qa1 = Math.Clamp(qa1, 0, 63);
            qa2 = Math.Clamp(qa2, 0, 63);
            qan = Math.Clamp(qan, 0, 63);

            int key = (qd) | (qa1 << 10) | (qa2 << 16) | (qan << 22);
            dict.TryGetValue(key, out var count);
            dict[key] = count + 1;
        }

        return dict;
    }

    private static Acme.Product.Infrastructure.PointCloud.PointCloud FlipNormals(Acme.Product.Infrastructure.PointCloud.PointCloud source)
    {
        var flipped = source.Transform(Matrix4x4.Identity);
        var normals = flipped.Normals!.GetGenericIndexer<float>();
        for (int i = 0; i < flipped.Count; i++)
        {
            normals[i, 0] = -normals[i, 0];
            normals[i, 1] = -normals[i, 1];
            normals[i, 2] = -normals[i, 2];
        }

        return flipped;
    }
}

