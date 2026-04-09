using System.Numerics;
using System.Reflection;
using Acme.Product.Infrastructure.PointCloud;
using Acme.Product.Infrastructure.PointCloud.Matching;
using FluentAssertions;
using OpenCvSharp;
using Xunit;
using MatPool = Acme.Product.Infrastructure.Memory.MatPool;
using PointCloudModel = Acme.Product.Infrastructure.PointCloud.PointCloud;

namespace Acme.Product.Tests.PointCloud;

public sealed class PPFMatcherTests
{
    [Fact]
    public void Match_ModelToScene_WithOcclusionAndOutliers_ShouldRecoverPose()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 201);

        using var model = BuildAsymmetricModel(gen);

        // Ground truth pose: model -> scene
        var rot = Matrix4x4.CreateFromYawPitchRoll(0.55f, -0.25f, 0.35f);
        var gt = rot * Matrix4x4.CreateTranslation(0.12f, -0.08f, 0.05f);

        using var sceneFull = model.Transform(gt);

        // Occlusion: keep ~70% points by simple AABB crop on +X side (in scene space).
        var aabb = sceneFull.GetAABB();
        var crop = new AxisAlignedBoundingBox
        {
            Min = new Vector3(aabb.Min.X + (aabb.Extent.X * 0.15f), aabb.Min.Y, aabb.Min.Z),
            Max = aabb.Max
        };
        using var sceneCropped = sceneFull.Crop(crop);

        // Add sparse outliers far away.
        var farBounds = new AxisAlignedBoundingBox
        {
            Min = new Vector3(-2, -2, -2),
            Max = new Vector3(2, 2, 2)
        };
        var sceneWithOutliers = gen.AddOutliers(sceneCropped, outlierRatio: 0.1f, bounds: farBounds);
        using var scene = sceneWithOutliers;

        var matcher = new PPFMatcher(seed: 123);
        var result = matcher.Match(
            model,
            scene,
            normalRadius: 0.06f,
            featureRadius: 0.12f,
            distanceStep: 0.01f,
            angleStepRad: 5f * (MathF.PI / 180f),
            numSamples: 200,
            modelRefStride: 2,
            maxPairsPerKey: 64,
            maxCorrespondences: 5000,
            ransacIterations: 1200,
            inlierThreshold: 0.01f,
            minInliers: 120);

        result.IsMatched.Should().BeTrue(
            $"ambiguous={result.IsAmbiguous}, ambiguityScore={result.AmbiguityScore:F3}, stability={result.StabilityScore:F3}, normal={result.NormalConsistency:F3}, inliers={result.InlierCount}, rms={result.RmsError:F4}");
        result.IsAmbiguous.Should().BeFalse();
        result.InlierCount.Should().BeGreaterThanOrEqualTo(120);
        result.RmsError.Should().BeLessThan(0.02); // 20mm RMS on inliers
        result.NormalConsistency.Should().BeGreaterThan(PPFMatcher.MinimumRecommendedNormalConsistency);
        result.StabilityScore.Should().BeGreaterThan(0.15);
        var (translationError, rotationErrorDeg) = ComputePoseErrors(gt, result.TransformModelToScene);
        translationError.Should().BeLessThan(0.03);
        rotationErrorDeg.Should().BeLessThan(8.0);
    }

    [Fact]
    public void Match_AsymmetricModelAcrossSeeds_ShouldRemainPoseStable()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 251);
        using var model = BuildAsymmetricModel(gen);

        var gt = Matrix4x4.CreateFromYawPitchRoll(0.45f, -0.22f, 0.28f) * Matrix4x4.CreateTranslation(0.10f, -0.05f, 0.04f);
        using var scene = model.Transform(gt);

        var translationErrors = new List<double>();
        var rotationErrors = new List<double>();

        foreach (var seed in Enumerable.Range(700, 8))
        {
            var matcher = new PPFMatcher(seed: seed);
            var result = matcher.Match(
                model,
                scene,
                normalRadius: 0.06f,
                featureRadius: 0.12f,
                distanceStep: 0.01f,
                angleStepRad: 5f * (MathF.PI / 180f),
                numSamples: 220,
                modelRefStride: 2,
                maxPairsPerKey: 64,
                maxCorrespondences: 5000,
                ransacIterations: 1200,
                inlierThreshold: 0.01f,
                minInliers: 120);

            result.IsMatched.Should().BeTrue($"seed {seed} should remain stable");
            result.IsAmbiguous.Should().BeFalse();
            result.NormalConsistency.Should().BeGreaterThan(PPFMatcher.MinimumRecommendedNormalConsistency);
            result.StabilityScore.Should().BeGreaterThan(0.15);
            var (translationError, rotationErrorDeg) = ComputePoseErrors(gt, result.TransformModelToScene);
            translationErrors.Add(translationError);
            rotationErrors.Add(rotationErrorDeg);
        }

        Percentile95(translationErrors).Should().BeLessThan(0.03);
        Percentile95(rotationErrors).Should().BeLessThan(8.0);
    }

    [Fact]
    public void Match_SymmetricSphere_ShouldReportAmbiguity()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 281);
        using var model = gen.GenerateSphere(Vector3.Zero, radius: 0.20f, numPoints: 2600, noise: 0.0004f, includeColors: false, includeNormals: true);
        var gt = Matrix4x4.CreateFromYawPitchRoll(0.52f, -0.18f, 0.41f) * Matrix4x4.CreateTranslation(0.06f, -0.04f, 0.03f);
        using var scene = model.Transform(gt);

        var matcher = new PPFMatcher(seed: 333);
        var result = matcher.Match(
            model,
            scene,
            normalRadius: 0.05f,
            featureRadius: 0.10f,
            distanceStep: 0.01f,
            angleStepRad: 5f * (MathF.PI / 180f),
            numSamples: 200,
            modelRefStride: 2,
            maxPairsPerKey: 64,
            maxCorrespondences: 5000,
            ransacIterations: 1400,
            inlierThreshold: 0.01f,
            minInliers: 140);

        result.IsAmbiguous.Should().BeTrue();
        result.IsMatched.Should().BeFalse();
        result.AmbiguityScore.Should().BeGreaterThan(0.9);
        result.StabilityScore.Should().BeLessThan(0.35);
    }

    [Fact]
    public void NearSphericalSymmetry_WithClearDominantLandscape_ShouldNotBeForcedAmbiguous()
    {
        var symmetry = CreatePrivateValue("SymmetryDescriptor", 0.992, 0.12, 0.975);
        var landscape = CreatePrivateValue("HypothesisLandscape", 0.74, 0.58, 0.32, 1, 0.18);
        var ambiguityScore = InvokePrivateStatic<double>(
            "ComputeAmbiguityScore",
            420,
            0.0045,
            0.94,
            180,
            0.0058,
            0.88,
            symmetry,
            landscape);
        var forcedAmbiguity = InvokePrivateStatic<bool>(
            "ShouldForceSphericalAmbiguity",
            420,
            180,
            0.94,
            symmetry,
            landscape);
        var ambiguous = InvokePrivateStatic<bool>(
            "IsAmbiguousPose",
            420,
            180,
            Matrix4x4.Identity,
            Matrix4x4.CreateFromYawPitchRoll(0.0f, 0.0f, 0.18f) * Matrix4x4.CreateTranslation(0.018f, 0.0f, 0.0f),
            0.01f,
            ambiguityScore,
            symmetry,
            0.0045,
            0.0058,
            0.94,
            0.88,
            landscape);

        forcedAmbiguity.Should().BeFalse();
        ambiguous.Should().BeFalse();
        ambiguityScore.Should().BeLessThan(0.86);
    }

    [Fact]
    public void ComputeIsotropicSymmetryPrior_ShouldIgnoreExtentIsotropyWithoutSphericalEvidence()
    {
        var dominantEvidence = 0.22;
        var nearCubicButNotSpherical = CreatePrivateValue("SymmetryDescriptor", 0.41, 0.08, 0.99);
        var anisotropicReference = CreatePrivateValue("SymmetryDescriptor", 0.41, 0.08, 0.18);

        var nearCubicPrior = InvokePrivateStatic<double>(
            "ComputeIsotropicSymmetryPrior",
            nearCubicButNotSpherical,
            dominantEvidence);
        var anisotropicPrior = InvokePrivateStatic<double>(
            "ComputeIsotropicSymmetryPrior",
            anisotropicReference,
            dominantEvidence);

        nearCubicPrior.Should().BeApproximately(anisotropicPrior, 1e-9);
        nearCubicPrior.Should().BeApproximately(0.41 * (1.0 - (dominantEvidence * 0.90)), 1e-9);
    }

    [Fact]
    public void Match_AxiallySymmetricCylinder_ShouldReportAmbiguity()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 292);
        using var model = gen.GenerateCylinder(
            center: Vector3.Zero,
            axis: Vector3.UnitZ,
            radius: 0.12f,
            height: 0.45f,
            numPoints: 2800,
            noise: 0.0004f,
            includeColors: false,
            includeNormals: true);
        var gt = Matrix4x4.CreateFromYawPitchRoll(0.35f, -0.24f, 1.10f) * Matrix4x4.CreateTranslation(0.05f, -0.03f, 0.02f);
        using var scene = model.Transform(gt);

        var matcher = new PPFMatcher(seed: 444);
        var result = matcher.Match(
            model,
            scene,
            normalRadius: 0.05f,
            featureRadius: 0.11f,
            distanceStep: 0.01f,
            angleStepRad: 5f * (MathF.PI / 180f),
            numSamples: 220,
            modelRefStride: 2,
            maxPairsPerKey: 64,
            maxCorrespondences: 5000,
            ransacIterations: 1400,
            inlierThreshold: 0.01f,
            minInliers: 150);

        result.IsAmbiguous.Should().BeTrue();
        result.IsMatched.Should().BeFalse();
        result.AmbiguityScore.Should().BeGreaterThan(0.85);
        result.NormalConsistency.Should().BeGreaterThan(PPFMatcher.MinimumRecommendedNormalConsistency);
    }

    [Fact]
    public void Match_NearSymmetricCylinderWithKeyFeature_ShouldRemainStableAndUnambiguous()
    {
        var gen = new SyntheticPointCloudGenerator(seed: 304);
        using var model = BuildNearSymmetricCylinderWithKey(gen);
        var gt = Matrix4x4.CreateFromYawPitchRoll(0.31f, -0.19f, 0.92f) * Matrix4x4.CreateTranslation(0.07f, -0.02f, 0.04f);
        using var scene = model.Transform(gt);

        var matcher = new PPFMatcher(seed: 512);
        var result = matcher.Match(
            model,
            scene,
            normalRadius: 0.05f,
            featureRadius: 0.11f,
            distanceStep: 0.01f,
            angleStepRad: 5f * (MathF.PI / 180f),
            numSamples: 220,
            modelRefStride: 2,
            maxPairsPerKey: 64,
            maxCorrespondences: 5000,
            ransacIterations: 1400,
            inlierThreshold: 0.01f,
            minInliers: 150);

        result.IsMatched.Should().BeTrue();
        result.IsAmbiguous.Should().BeFalse();
        result.StabilityScore.Should().BeGreaterThan(PPFMatcher.MinimumRecommendedStabilityScore);
        result.NormalConsistency.Should().BeGreaterThan(PPFMatcher.MinimumRecommendedNormalConsistency);
        var (translationError, rotationErrorDeg) = ComputePoseErrors(gt, result.TransformModelToScene);
        translationError.Should().BeLessThan(0.03);
        rotationErrorDeg.Should().BeLessThan(8.0);
    }

    private static PointCloudModel BuildAsymmetricModel(SyntheticPointCloudGenerator gen)
    {
        // Combine two shapes at different offsets to break symmetry.
        using var sphere = gen.GenerateSphere(
            center: new Vector3(0.0f, 0.0f, 0.0f),
            radius: 0.18f,
            numPoints: 2500,
            noise: 0.0004f,
            includeColors: true,
            includeNormals: false,
            outlierRatio: 0.0f);

        using var cube = gen.GenerateCube(
            center: new Vector3(0.35f, 0.12f, -0.05f),
            edgeLength: 0.22f,
            numPoints: 1800,
            noise: 0.0004f,
            includeColors: true,
            includeNormals: false,
            outlierRatio: 0.0f);

        return MergeTwo(sphere, cube);
    }

    private static PointCloudModel BuildNearSymmetricCylinderWithKey(SyntheticPointCloudGenerator gen)
    {
        using var cylinder = gen.GenerateCylinder(
            center: Vector3.Zero,
            axis: Vector3.UnitZ,
            radius: 0.12f,
            height: 0.45f,
            numPoints: 2600,
            noise: 0.0004f,
            includeColors: true,
            includeNormals: false);

        using var cube = gen.GenerateCube(
            center: new Vector3(0.13f, 0.01f, 0.10f),
            edgeLength: 0.08f,
            numPoints: 700,
            noise: 0.0004f,
            includeColors: true,
            includeNormals: false,
            outlierRatio: 0.0f);

        return MergeTwo(cylinder, cube);
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

    // Note: Exact pose error checks are intentionally omitted here.

    private static (double TranslationError, double RotationErrorDeg) ComputePoseErrors(Matrix4x4 expected, Matrix4x4 actual)
    {
        var translationError = Math.Sqrt(
            Math.Pow(expected.M41 - actual.M41, 2) +
            Math.Pow(expected.M42 - actual.M42, 2) +
            Math.Pow(expected.M43 - actual.M43, 2));

        Matrix4x4.Decompose(expected, out _, out var expectedRotation, out _).Should().BeTrue();
        Matrix4x4.Decompose(actual, out _, out var actualRotation, out _).Should().BeTrue();
        expectedRotation = Quaternion.Normalize(expectedRotation);
        actualRotation = Quaternion.Normalize(actualRotation);
        var delta = Quaternion.Normalize(actualRotation * Quaternion.Conjugate(expectedRotation));
        var rotationErrorDeg = 2.0 * Math.Acos(Math.Clamp(Math.Abs(delta.W), 0.0f, 1.0f)) * 180.0 / Math.PI;
        return (translationError, rotationErrorDeg);
    }

    private static double Percentile95(IReadOnlyCollection<double> values)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        var index = Math.Clamp((int)Math.Ceiling(ordered.Length * 0.95) - 1, 0, ordered.Length - 1);
        return ordered[index];
    }

    private static object CreatePrivateValue(string nestedTypeName, params object[] arguments)
    {
        var nestedType = typeof(PPFMatcher).GetNestedType(nestedTypeName, BindingFlags.NonPublic);
        nestedType.Should().NotBeNull();
        var value = Activator.CreateInstance(nestedType!, arguments);
        value.Should().NotBeNull();
        return value!;
    }

    private static T InvokePrivateStatic<T>(string methodName, params object[] arguments)
    {
        var method = typeof(PPFMatcher).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();
        var value = method!.Invoke(null, arguments);
        value.Should().NotBeNull();
        return (T)value!;
    }
}
