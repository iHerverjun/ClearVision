using System.Numerics;
using Acme.Product.Infrastructure.Memory;
using Acme.Product.Infrastructure.PointCloud.Features;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.PointCloud.Matching;

public readonly record struct PPFMatchResult(
    bool IsMatched,
    Matrix4x4 TransformModelToScene,
    int InlierCount,
    int CorrespondenceCount,
    double RmsError,
    bool IsAmbiguous = false,
    double AmbiguityScore = 0.0,
    double StabilityScore = 0.0,
    double NormalConsistency = 0.0);

/// <summary>
/// Simplified PPF-based surface matching:
/// - Build a quantized PPF hash table from the model (within FeatureRadius).
/// - Sample reference points in the scene, generate candidate correspondences via hash lookup.
/// - Use RANSAC to estimate a rigid transform (model -&gt; scene) from correspondences and verify by inlier count.
/// </summary>
public sealed class PPFMatcher
{
    public const double MinimumRecommendedNormalConsistency = 0.70;
    public const double MinimumRecommendedStabilityScore = 0.12;

    private readonly MatPool _pool;
    private readonly Random _rng;

    public PPFMatcher(int? seed = null, MatPool? pool = null)
    {
        _pool = pool ?? MatPool.Shared;
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public PPFMatchResult Match(
        PointCloud model,
        PointCloud scene,
        float normalRadius = 0.03f,
        float featureRadius = 0.05f,
        float distanceStep = 0.01f,
        float angleStepRad = 0.05f,
        int numSamples = 100,
        int modelRefStride = 3,
        int maxPairsPerKey = 64,
        int maxCorrespondences = 5000,
        int ransacIterations = 800,
        float inlierThreshold = 0.005f,
        int minInliers = 80)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        if (scene == null) throw new ArgumentNullException(nameof(scene));
        if (model.Count == 0 || scene.Count == 0)
        {
            return new PPFMatchResult(false, Matrix4x4.Identity, 0, 0, double.PositiveInfinity);
        }

        if (normalRadius <= 0 || !float.IsFinite(normalRadius)) throw new ArgumentOutOfRangeException(nameof(normalRadius));
        if (featureRadius <= 0 || !float.IsFinite(featureRadius)) throw new ArgumentOutOfRangeException(nameof(featureRadius));
        if (distanceStep <= 0 || !float.IsFinite(distanceStep)) throw new ArgumentOutOfRangeException(nameof(distanceStep));
        if (angleStepRad <= 0 || !float.IsFinite(angleStepRad)) throw new ArgumentOutOfRangeException(nameof(angleStepRad));
        if (numSamples <= 0) throw new ArgumentOutOfRangeException(nameof(numSamples));
        if (modelRefStride <= 0) throw new ArgumentOutOfRangeException(nameof(modelRefStride));
        if (maxPairsPerKey <= 0) throw new ArgumentOutOfRangeException(nameof(maxPairsPerKey));
        if (maxCorrespondences <= 0) throw new ArgumentOutOfRangeException(nameof(maxCorrespondences));
        if (ransacIterations <= 0) throw new ArgumentOutOfRangeException(nameof(ransacIterations));
        if (inlierThreshold <= 0 || !float.IsFinite(inlierThreshold)) throw new ArgumentOutOfRangeException(nameof(inlierThreshold));
        if (minInliers <= 0) throw new ArgumentOutOfRangeException(nameof(minInliers));

        // Ensure both clouds have normals we can consume.
        using var modelWithNormals = EnsureNormals(model, normalRadius);
        using var sceneWithNormals = EnsureNormals(scene, normalRadius);

        var modelPoints = modelWithNormals.Points.GetGenericIndexer<float>();
        var modelNormals = modelWithNormals.Normals!.GetGenericIndexer<float>();
        var scenePoints = sceneWithNormals.Points.GetGenericIndexer<float>();
        var sceneNormals = sceneWithNormals.Normals!.GetGenericIndexer<float>();
        var symmetry = ComputeSymmetryDescriptor(modelPoints, modelWithNormals.Count, modelWithNormals.GetAABB());

        var modelHash = BuildModelHash(
            modelPoints,
            modelNormals,
            modelWithNormals.Count,
            featureRadius,
            distanceStep,
            angleStepRad,
            modelRefStride,
            maxPairsPerKey);

        var correspondences = BuildSceneCorrespondences(
            modelHash,
            scenePoints,
            sceneNormals,
            sceneWithNormals.Count,
            featureRadius,
            distanceStep,
            angleStepRad,
            numSamples,
            maxCorrespondences);

        if (correspondences.Count < Math.Max(3, minInliers))
        {
            return new PPFMatchResult(false, Matrix4x4.Identity, 0, correspondences.Count, double.PositiveInfinity);
        }

        // Geometric verification grid (nearest-neighbor within inlierThreshold).
        var nnGrid = SpatialHashGrid.Build(scenePoints, sceneWithNormals.Count, cellSize: inlierThreshold);
        var evalIndices = BuildEvaluationIndices(modelWithNormals.Count, targetCount: 1500);

        var (bestT, bestInliers, bestRms, bestSupport, secondaryT, secondarySupport, secondaryRms, hypothesisLandscape) = RansacRigidTransform(
            correspondences,
            modelPoints,
            scenePoints,
            nnGrid,
            evalIndices,
            ransacIterations,
            inlierThreshold,
            minInliers);

        if (bestInliers.Length < minInliers)
        {
            return new PPFMatchResult(false, Matrix4x4.Identity, bestInliers.Length, correspondences.Count, bestRms);
        }

        // Refine using a larger subset of model points with nearest-neighbor correspondences (ICP-like).
        // We do a coarse stage with a larger capture range, then a fine stage at inlierThreshold.
        var refineIndices = BuildEvaluationIndices(modelWithNormals.Count, targetCount: 7000);
        var coarseThreshold = MathF.Min(0.10f, MathF.Max(inlierThreshold * 4f, 0.03f));
        var coarseGrid = SpatialHashGrid.Build(scenePoints, sceneWithNormals.Count, cellSize: coarseThreshold);
        var (refined, refinedInliers, refinedRms, normalConsistency) = RefineHypothesis(
            modelPoints,
            modelNormals,
            scenePoints,
            sceneNormals,
            coarseGrid,
            nnGrid,
            refineIndices,
            bestT,
            coarseThreshold,
            inlierThreshold,
            minInliers);

        if (refinedInliers.Length < minInliers)
        {
            return new PPFMatchResult(false, Matrix4x4.Identity, refinedInliers.Length, correspondences.Count, refinedRms);
        }

        Matrix4x4 refinedSecondary = Matrix4x4.Identity;
        Correspondence[] refinedSecondaryInliers = Array.Empty<Correspondence>();
        var refinedSecondaryRms = double.PositiveInfinity;
        var secondaryNormalConsistency = 0.0;

        if (secondarySupport >= minInliers)
        {
            (refinedSecondary, refinedSecondaryInliers, refinedSecondaryRms, secondaryNormalConsistency) = RefineHypothesis(
                modelPoints,
                modelNormals,
                scenePoints,
                sceneNormals,
                coarseGrid,
                nnGrid,
                refineIndices,
                secondaryT,
                coarseThreshold,
                inlierThreshold,
                minInliers);
        }

        var ambiguityScore = ComputeAmbiguityScore(
            refinedInliers.Length,
            refinedRms,
            normalConsistency,
            refinedSecondaryInliers.Length,
            refinedSecondaryRms,
            secondaryNormalConsistency,
            symmetry,
            hypothesisLandscape);
        var stabilityScore = ComputeStabilityScore(
            refinedInliers.Length,
            refinedRms,
            normalConsistency,
            refinedSecondaryInliers.Length,
            refinedSecondaryRms,
            secondaryNormalConsistency,
            symmetry,
            hypothesisLandscape);
        var ambiguous = IsAmbiguousPose(
            refinedInliers.Length,
            refinedSecondaryInliers.Length,
            refined,
            refinedSecondary,
            inlierThreshold,
            ambiguityScore,
            symmetry,
            refinedRms,
            refinedSecondaryRms,
            normalConsistency,
            secondaryNormalConsistency,
            hypothesisLandscape);

        if (!ambiguous && ShouldForceSphericalAmbiguity(
                refinedInliers.Length,
                refinedSecondaryInliers.Length,
                normalConsistency,
                symmetry,
                hypothesisLandscape))
        {
            ambiguous = true;
            ambiguityScore = Math.Max(ambiguityScore, symmetry.SphericalScore);
            stabilityScore = Math.Min(stabilityScore, 1.0 - symmetry.SphericalScore);
        }

        var passedNormalConsistency = normalConsistency >= MinimumRecommendedNormalConsistency;
        var passedStability = stabilityScore >= MinimumRecommendedStabilityScore;
        var isMatched = !ambiguous && passedNormalConsistency && passedStability;
        return new PPFMatchResult(
            isMatched,
            refined,
            refinedInliers.Length,
            correspondences.Count,
            refinedRms,
            ambiguous,
            ambiguityScore,
            stabilityScore,
            normalConsistency);
    }

    private PointCloud EnsureNormals(PointCloud input, float normalRadius)
    {
        if (input.Normals != null)
        {
            // Deep copy to keep ownership simple.
            var p = _pool.Rent(width: 3, height: input.Count, type: MatType.CV_32FC1);
            input.Points.CopyTo(p);

            Mat? c = null;
            if (input.Colors != null)
            {
                c = _pool.Rent(width: 3, height: input.Count, type: MatType.CV_8UC1);
                input.Colors.CopyTo(c);
            }

            var n = _pool.Rent(width: 3, height: input.Count, type: MatType.CV_32FC1);
            input.Normals.CopyTo(n);
            NormalizeNormalsInPlace(n);
            OrientNormalsOutward(p, n);

            return new PointCloud(p, c, n, isOrganized: false, pool: _pool);
        }

        var estimator = new NormalEstimation(_pool);
        var normals = estimator.Estimate(input, normalRadius);
        NormalizeNormalsInPlace(normals);
        OrientNormalsOutward(input.Points, normals);

        var outPoints = _pool.Rent(width: 3, height: input.Count, type: MatType.CV_32FC1);
        input.Points.CopyTo(outPoints);

        Mat? outColors = null;
        if (input.Colors != null)
        {
            outColors = _pool.Rent(width: 3, height: input.Count, type: MatType.CV_8UC1);
            input.Colors.CopyTo(outColors);
        }

        return new PointCloud(outPoints, outColors, normals, isOrganized: false, pool: _pool);
    }

    private static void NormalizeNormalsInPlace(Mat normals)
    {
        var idx = normals.GetGenericIndexer<float>();
        for (int i = 0; i < normals.Rows; i++)
        {
            var v = new Vector3(idx[i, 0], idx[i, 1], idx[i, 2]);
            if (v.LengthSquared() <= 1e-20f)
            {
                idx[i, 0] = 0;
                idx[i, 1] = 0;
                idx[i, 2] = 1;
                continue;
            }

            v = Vector3.Normalize(v);
            idx[i, 0] = v.X;
            idx[i, 1] = v.Y;
            idx[i, 2] = v.Z;
        }
    }

    private static void OrientNormalsOutward(Mat points, Mat normals)
    {
        if (points.Rows == 0 || normals.Rows == 0)
        {
            return;
        }

        var pIdx = points.GetGenericIndexer<float>();
        var nIdx = normals.GetGenericIndexer<float>();

        double cx = 0, cy = 0, cz = 0;
        for (int i = 0; i < points.Rows; i++)
        {
            cx += pIdx[i, 0];
            cy += pIdx[i, 1];
            cz += pIdx[i, 2];
        }

        var inv = 1.0 / points.Rows;
        cx *= inv; cy *= inv; cz *= inv;
        var centroid = new Vector3((float)cx, (float)cy, (float)cz);

        for (int i = 0; i < points.Rows; i++)
        {
            var p = new Vector3(pIdx[i, 0], pIdx[i, 1], pIdx[i, 2]);
            var n = new Vector3(nIdx[i, 0], nIdx[i, 1], nIdx[i, 2]);
            if (Vector3.Dot(n, p - centroid) < 0)
            {
                nIdx[i, 0] = -nIdx[i, 0];
                nIdx[i, 1] = -nIdx[i, 1];
                nIdx[i, 2] = -nIdx[i, 2];
            }
        }
    }

    private sealed class ModelHash
    {
        public readonly Dictionary<int, List<ModelPair>> Table;
        public readonly float DistanceStep;
        public readonly float AngleStepRad;

        public ModelHash(Dictionary<int, List<ModelPair>> table, float distanceStep, float angleStepRad)
        {
            Table = table;
            DistanceStep = distanceStep;
            AngleStepRad = angleStepRad;
        }
    }

    private readonly record struct ModelPair(int RefIndex, int NeighborIndex);
    private readonly record struct PairCorrespondence(int ModelRefIndex, int ModelNeighborIndex, int SceneRefIndex, int SceneNeighborIndex);
    private readonly record struct Correspondence(int ModelIndex, int SceneIndex);
    private readonly record struct Hypothesis(Matrix4x4 Transform, int Support, double Rms, int VoteCount, double SupportSum);
    private readonly record struct HypothesisLandscape(
        double DominantVoteRatio,
        double CompetitiveSupportRatio,
        double CompetitiveVoteRatio,
        int CompetitiveClusterCount,
        double PoseSpreadScore);
    private readonly record struct SymmetryDescriptor(double SphericalScore, double AxialScore, double ExtentScore);

    private ModelHash BuildModelHash(
        MatIndexer<float> modelPoints,
        MatIndexer<float> modelNormals,
        int n,
        float featureRadius,
        float distanceStep,
        float angleStepRad,
        int refStride,
        int maxPairsPerKey)
    {
        var grid = SpatialHashGrid.Build(modelPoints, n, cellSize: featureRadius);
        var r2 = (double)featureRadius * featureRadius;
        var neighbors = new List<int>(capacity: 64);

        var table = new Dictionary<int, List<ModelPair>>(capacity: Math.Min(n * 2, 2_000_000));

        for (int i = 0; i < n; i += refStride)
        {
            neighbors.Clear();
            SpatialHashGrid.CollectRadiusNeighbors(modelPoints, i, grid, featureRadius, r2, neighbors);
            if (neighbors.Count == 0)
            {
                continue;
            }

            var p1 = new Vector3(modelPoints[i, 0], modelPoints[i, 1], modelPoints[i, 2]);
            var n1 = new Vector3(modelNormals[i, 0], modelNormals[i, 1], modelNormals[i, 2]);

            for (int t = 0; t < neighbors.Count; t++)
            {
                var j = neighbors[t];
                var p2 = new Vector3(modelPoints[j, 0], modelPoints[j, 1], modelPoints[j, 2]);
                var n2 = new Vector3(modelNormals[j, 0], modelNormals[j, 1], modelNormals[j, 2]);

                var f = PPFFeature.Compute(p1, n1, p2, n2);
                if (f.Distance <= 0)
                {
                    continue;
                }

                var key = QuantizeKey(f, distanceStep, angleStepRad);
                AddModelPair(table, key, new ModelPair(i, j), maxPairsPerKey);

                // Insert reverse direction too (helps ambiguity).
                var fr = PPFFeature.Compute(p2, n2, p1, n1);
                if (fr.Distance > 0)
                {
                    var kr = QuantizeKey(fr, distanceStep, angleStepRad);
                    AddModelPair(table, kr, new ModelPair(j, i), maxPairsPerKey);
                }
            }
        }

        return new ModelHash(table, distanceStep, angleStepRad);
    }

    private static void AddModelPair(Dictionary<int, List<ModelPair>> table, int key, ModelPair pair, int maxPairsPerKey)
    {
        if (!table.TryGetValue(key, out var list))
        {
            list = new List<ModelPair>(capacity: 8);
            table[key] = list;
        }

        if (list.Count >= maxPairsPerKey)
        {
            return;
        }

        list.Add(pair);
    }

    private List<PairCorrespondence> BuildScenePairCorrespondences(
        ModelHash modelHash,
        MatIndexer<float> scenePoints,
        MatIndexer<float> sceneNormals,
        int sceneCount,
        float featureRadius,
        float distanceStep,
        float angleStepRad,
        int numSamples,
        int maxCorrespondences)
    {
        var grid = SpatialHashGrid.Build(scenePoints, sceneCount, cellSize: featureRadius);
        var r2 = (double)featureRadius * featureRadius;
        var neighbors = new List<int>(capacity: 64);

        var correspondences = new List<PairCorrespondence>(capacity: Math.Min(maxCorrespondences, 8192));

        var sampleCount = Math.Min(numSamples, sceneCount);
        for (int s = 0; s < sampleCount; s++)
        {
            var i = _rng.Next(sceneCount);

            neighbors.Clear();
            SpatialHashGrid.CollectRadiusNeighbors(scenePoints, i, grid, featureRadius, r2, neighbors);
            if (neighbors.Count == 0)
            {
                continue;
            }

            var p1 = new Vector3(scenePoints[i, 0], scenePoints[i, 1], scenePoints[i, 2]);
            var n1 = new Vector3(sceneNormals[i, 0], sceneNormals[i, 1], sceneNormals[i, 2]);

            // Pair correspondences: each (scene ref, scene neighbor) PPF bin yields multiple (model ref, model neighbor) candidates.
            // We intentionally keep a bounded subset to keep RANSAC stable and fast.
            var takeNeighbors = Math.Min(neighbors.Count, 24);
            for (int t = 0; t < takeNeighbors; t++)
            {
                var j = neighbors[t];
                var p2 = new Vector3(scenePoints[j, 0], scenePoints[j, 1], scenePoints[j, 2]);
                var n2 = new Vector3(sceneNormals[j, 0], sceneNormals[j, 1], sceneNormals[j, 2]);

                var f = PPFFeature.Compute(p1, n1, p2, n2);
                if (f.Distance <= 0)
                {
                    continue;
                }

                var key = QuantizeKey(f, distanceStep, angleStepRad);
                if (!modelHash.Table.TryGetValue(key, out var candidates))
                {
                    continue;
                }

                // Add a few candidates per feature to keep correspondence pool bounded.
                var take = Math.Min(candidates.Count, 6);
                for (int c = 0; c < take; c++)
                {
                    var pair = candidates[c];
                    correspondences.Add(new PairCorrespondence(pair.RefIndex, pair.NeighborIndex, i, j));
                    if (correspondences.Count >= maxCorrespondences)
                    {
                        return correspondences;
                    }
                }
            }
        }

        return correspondences;
    }

    private List<Correspondence> BuildSceneCorrespondences(
        ModelHash modelHash,
        MatIndexer<float> scenePoints,
        MatIndexer<float> sceneNormals,
        int sceneCount,
        float featureRadius,
        float distanceStep,
        float angleStepRad,
        int numSamples,
        int maxCorrespondences)
    {
        var grid = SpatialHashGrid.Build(scenePoints, sceneCount, cellSize: featureRadius);
        var r2 = (double)featureRadius * featureRadius;
        var neighbors = new List<int>(capacity: 64);

        var correspondences = new List<Correspondence>(capacity: Math.Min(maxCorrespondences, 8192));

        var sampleCount = Math.Min(numSamples, sceneCount);
        for (int s = 0; s < sampleCount; s++)
        {
            var i = _rng.Next(sceneCount);

            neighbors.Clear();
            SpatialHashGrid.CollectRadiusNeighbors(scenePoints, i, grid, featureRadius, r2, neighbors);
            if (neighbors.Count == 0)
            {
                continue;
            }

            var p1 = new Vector3(scenePoints[i, 0], scenePoints[i, 1], scenePoints[i, 2]);
            var n1 = new Vector3(sceneNormals[i, 0], sceneNormals[i, 1], sceneNormals[i, 2]);

            // Voting: find the most likely model reference index for this scene reference point.
            // This reduces random cross-pair correspondences when the PPF bins are ambiguous.
            var votes = new Dictionary<int, int>(capacity: 64);
            for (int t = 0; t < neighbors.Count; t++)
            {
                var j = neighbors[t];
                var p2 = new Vector3(scenePoints[j, 0], scenePoints[j, 1], scenePoints[j, 2]);
                var n2 = new Vector3(sceneNormals[j, 0], sceneNormals[j, 1], sceneNormals[j, 2]);

                var f = PPFFeature.Compute(p1, n1, p2, n2);
                if (f.Distance <= 0)
                {
                    continue;
                }

                var key = QuantizeKey(f, distanceStep, angleStepRad);
                if (!modelHash.Table.TryGetValue(key, out var candidates))
                {
                    continue;
                }

                // Add a few candidates per feature to keep correspondence pool bounded.
                var take = Math.Min(candidates.Count, 8);
                for (int c = 0; c < take; c++)
                {
                    var pair = candidates[c];
                    votes.TryGetValue(pair.RefIndex, out var count);
                    votes[pair.RefIndex] = count + 1;
                }
            }

            if (votes.Count == 0)
            {
                continue;
            }

            // Take top-K voted model refs for this scene ref point.
            var top = votes
                .OrderByDescending(kv => kv.Value)
                .Take(3)
                .Select(kv => kv.Key);

            foreach (var modelRef in top)
            {
                correspondences.Add(new Correspondence(modelRef, i));
                if (correspondences.Count >= maxCorrespondences)
                {
                    return correspondences;
                }
            }

            if (correspondences.Count >= maxCorrespondences)
            {
                return correspondences;
            }
        }

        return correspondences;
    }

    private static int QuantizeKey(PPFFeature f, float distStep, float angleStep)
    {
        int qd = (int)MathF.Round(f.Distance / distStep);
        int qa1 = (int)MathF.Round(f.Angle1 / angleStep);
        int qa2 = (int)MathF.Round(f.Angle2 / angleStep);
        int qan = (int)MathF.Round(f.AngleNormals / angleStep);

        qd = Math.Clamp(qd, 0, 1023);
        qa1 = Math.Clamp(qa1, 0, 63);
        qa2 = Math.Clamp(qa2, 0, 63);
        qan = Math.Clamp(qan, 0, 63);

        return (qd) | (qa1 << 10) | (qa2 << 16) | (qan << 22);
    }

    private (Matrix4x4 Transform, Correspondence[] Inliers, double BestRms) RansacRigidTransformFromPairs(
        List<PairCorrespondence> pool,
        MatIndexer<float> modelPoints,
        MatIndexer<float> modelNormals,
        MatIndexer<float> scenePoints,
        MatIndexer<float> sceneNormals,
        SpatialHashGridIndex sceneGrid,
        int[] evalModelIndices,
        int iterations,
        float inlierThreshold,
        int minInliers)
    {
        var threshold2 = (double)inlierThreshold * inlierThreshold;

        Matrix4x4 bestT = Matrix4x4.Identity;
        int bestCount = 0;
        double bestRms = double.PositiveInfinity;

        // If we get a near-perfect fit early, stop.
        var earlyStop = Math.Max(minInliers, (int)(evalModelIndices.Length * 0.90));

        for (int it = 0; it < iterations; it++)
        {
            var pc = pool[_rng.Next(pool.Count)];

            if (!TryEstimateTransformFromPair(
                modelPoints,
                modelNormals,
                scenePoints,
                sceneNormals,
                pc,
                out var tform))
            {
                continue;
            }

            var (count, sum2) = ScoreTransform(modelPoints, scenePoints, sceneGrid, evalModelIndices, tform, threshold2);
            if (count == 0)
            {
                continue;
            }

            var rms = Math.Sqrt(sum2 / count);
            if (count > bestCount || (count == bestCount && rms < bestRms))
            {
                bestCount = count;
                bestRms = rms;
                bestT = tform;

                if (bestCount >= earlyStop)
                {
                    break;
                }
            }
        }

        if (bestCount < minInliers)
        {
            return (bestT, Array.Empty<Correspondence>(), bestRms);
        }

        // Build inlier correspondences for refinement (NN correspondences under bestT).
        var inliers = new List<Correspondence>(capacity: bestCount);
        for (int i = 0; i < evalModelIndices.Length; i++)
        {
            var mi = evalModelIndices[i];
            var mp = new Vector3(modelPoints[mi, 0], modelPoints[mi, 1], modelPoints[mi, 2]);
            var tp = Vector3.Transform(mp, bestT);

            if (TryFindNearest(scenePoints, sceneGrid, tp, threshold2, out var sj, out _))
            {
                inliers.Add(new Correspondence(mi, sj));
            }
        }

        return (bestT, inliers.ToArray(), bestRms);
    }

    private static bool TryEstimateTransformFromPair(
        MatIndexer<float> modelPoints,
        MatIndexer<float> modelNormals,
        MatIndexer<float> scenePoints,
        MatIndexer<float> sceneNormals,
        PairCorrespondence pc,
        out Matrix4x4 transformModelToScene)
    {
        var pm = new Vector3(modelPoints[pc.ModelRefIndex, 0], modelPoints[pc.ModelRefIndex, 1], modelPoints[pc.ModelRefIndex, 2]);
        var qm = new Vector3(modelPoints[pc.ModelNeighborIndex, 0], modelPoints[pc.ModelNeighborIndex, 1], modelPoints[pc.ModelNeighborIndex, 2]);
        var nm = new Vector3(modelNormals[pc.ModelRefIndex, 0], modelNormals[pc.ModelRefIndex, 1], modelNormals[pc.ModelRefIndex, 2]);

        var ps = new Vector3(scenePoints[pc.SceneRefIndex, 0], scenePoints[pc.SceneRefIndex, 1], scenePoints[pc.SceneRefIndex, 2]);
        var qs = new Vector3(scenePoints[pc.SceneNeighborIndex, 0], scenePoints[pc.SceneNeighborIndex, 1], scenePoints[pc.SceneNeighborIndex, 2]);
        var ns = new Vector3(sceneNormals[pc.SceneRefIndex, 0], sceneNormals[pc.SceneRefIndex, 1], sceneNormals[pc.SceneRefIndex, 2]);

        if (!TryNormalize(nm, out nm) || !TryNormalize(ns, out ns))
        {
            transformModelToScene = Matrix4x4.Identity;
            return false;
        }

        var dm = qm - pm;
        var ds = qs - ps;
        if (!TryNormalize(dm, out dm) || !TryNormalize(ds, out ds))
        {
            transformModelToScene = Matrix4x4.Identity;
            return false;
        }

        // 1) Align reference normals.
        var r1 = RotationFromTo(nm, ns);

        // 2) Resolve remaining rotation around the scene normal by aligning neighbor directions.
        var dm1 = Vector3.TransformNormal(dm, r1);

        // Project directions onto plane orthogonal to ns.
        var u = dm1 - (ns * Vector3.Dot(dm1, ns));
        var v = ds - (ns * Vector3.Dot(ds, ns));

        if (!TryNormalize(u, out u) || !TryNormalize(v, out v))
        {
            transformModelToScene = Matrix4x4.Identity;
            return false;
        }

        var sin = Vector3.Dot(ns, Vector3.Cross(u, v));
        var cos = Vector3.Dot(u, v);
        var angle = MathF.Atan2(sin, cos);
        var r2 = Matrix4x4.CreateFromAxisAngle(ns, angle);

        var r = r2 * r1;
        var t = ps - Vector3.Transform(pm, r);
        r.M41 = t.X;
        r.M42 = t.Y;
        r.M43 = t.Z;

        transformModelToScene = r;
        return true;
    }

    private static Matrix4x4 RotationFromTo(Vector3 from, Vector3 to)
    {
        // Assumes both vectors are normalized.
        var v = Vector3.Cross(from, to);
        var c = Vector3.Dot(from, to);

        if (v.LengthSquared() <= 1e-12f)
        {
            // Parallel or anti-parallel.
            if (c > 0.9999f)
            {
                return Matrix4x4.Identity;
            }

            // 180-degree rotation around an arbitrary axis orthogonal to from.
            var axis = Vector3.Cross(from, MathF.Abs(from.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY);
            _ = TryNormalize(axis, out axis);
            return Matrix4x4.CreateFromAxisAngle(axis, MathF.PI);
        }

        var s = MathF.Sqrt(v.LengthSquared());
        var axisN = v / s;
        var angle = MathF.Atan2(s, c);
        return Matrix4x4.CreateFromAxisAngle(axisN, angle);
    }

    private static bool TryNormalize(Vector3 v, out Vector3 normalized)
    {
        var len2 = v.LengthSquared();
        if (!float.IsFinite(len2) || len2 <= 1e-20f)
        {
            normalized = default;
            return false;
        }

        normalized = v / MathF.Sqrt(len2);
        return true;
    }

    private (Matrix4x4 Transform, Correspondence[] Inliers, double BestRms, int BestSupport, Matrix4x4 SecondaryTransform, int SecondarySupport, double SecondaryRms, HypothesisLandscape Landscape) RansacRigidTransform(
        List<Correspondence> pool,
        MatIndexer<float> modelPoints,
        MatIndexer<float> scenePoints,
        SpatialHashGridIndex sceneGrid,
        int[] evalModelIndices,
        int iterations,
        float inlierThreshold,
        int minInliers)
    {
        var threshold2 = (double)inlierThreshold * inlierThreshold;
        var translationMergeTolerance = Math.Max(inlierThreshold * 4f, 0.006f);
        const double rotationMergeToleranceDeg = 4.0;
        var hypotheses = new List<Hypothesis>(capacity: 6);

        var earlyStop = Math.Max(minInliers, (int)(evalModelIndices.Length * 0.85));
        Span<Correspondence> sample = stackalloc Correspondence[3];

        for (int it = 0; it < iterations; it++)
        {
            if (!TrySample3(pool, out var c1, out var c2, out var c3))
            {
                continue;
            }

            if (!IsNonDegenerateTriangle(modelPoints, c1.ModelIndex, c2.ModelIndex, c3.ModelIndex) ||
                !IsNonDegenerateTriangle(scenePoints, c1.SceneIndex, c2.SceneIndex, c3.SceneIndex))
            {
                continue;
            }

            sample[0] = c1;
            sample[1] = c2;
            sample[2] = c3;

            var tform = RigidTransformEstimator.Estimate(modelPoints, scenePoints, sample);

            // Score geometrically against the scene (nearest neighbor within threshold).
            var (count, sum2) = ScoreTransform(modelPoints, scenePoints, sceneGrid, evalModelIndices, tform, threshold2);
            if (count == 0)
            {
                continue;
            }

            var rms = Math.Sqrt(sum2 / count);
            UpsertHypothesis(hypotheses, new Hypothesis(tform, count, rms, 1, count), translationMergeTolerance, rotationMergeToleranceDeg);
            if (hypotheses.Count > 0 && hypotheses[0].Support >= earlyStop)
            {
                break;
            }
        }

        hypotheses.Sort(CompareHypotheses);
        if (hypotheses.Count == 0)
        {
            return (Matrix4x4.Identity, Array.Empty<Correspondence>(), double.PositiveInfinity, 0, Matrix4x4.Identity, 0, double.PositiveInfinity, new HypothesisLandscape(0.0, 0.0, 0.0, 0, 0.0));
        }

        var best = hypotheses[0];
        var landscape = AnalyzeHypothesisLandscape(hypotheses, best, inlierThreshold);
        var secondary = hypotheses.Count > 1
            ? hypotheses[1]
            : new Hypothesis(Matrix4x4.Identity, 0, double.PositiveInfinity, 0, 0.0);

        var bestT = best.Transform;
        var bestCount = best.Support;
        var bestRms = best.Rms;
        var secondT = secondary.Transform;
        var secondCount = secondary.Support;
        var secondRms = secondary.Rms;

        if (bestCount < minInliers)
        {
            return (bestT, Array.Empty<Correspondence>(), bestRms, bestCount, secondT, secondCount, secondRms, landscape);
        }

        var inliers = CollectInliers(modelPoints, scenePoints, sceneGrid, evalModelIndices, bestT, threshold2);
        return (bestT, inliers, bestRms, bestCount, secondT, secondCount, secondRms, landscape);
    }

    private static void UpsertHypothesis(List<Hypothesis> hypotheses, Hypothesis candidate, double translationTolerance, double rotationToleranceDeg)
    {
        for (int i = 0; i < hypotheses.Count; i++)
        {
            if (!AreTransformsSimilar(hypotheses[i].Transform, candidate.Transform, translationTolerance, rotationToleranceDeg))
            {
                continue;
            }

            var existing = hypotheses[i];
            var representative = CompareHypotheses(candidate, existing) < 0 ? candidate : existing;
            hypotheses[i] = representative with
            {
                VoteCount = existing.VoteCount + candidate.VoteCount,
                SupportSum = existing.SupportSum + candidate.SupportSum
            };

            hypotheses.Sort(CompareHypotheses);
            return;
        }

        hypotheses.Add(candidate);
        hypotheses.Sort(CompareHypotheses);
        if (hypotheses.Count > 6)
        {
            hypotheses.RemoveAt(hypotheses.Count - 1);
        }
    }

    private static int CompareHypotheses(Hypothesis x, Hypothesis y)
    {
        var supportCompare = y.Support.CompareTo(x.Support);
        if (supportCompare != 0)
        {
            return supportCompare;
        }

        var voteCompare = y.VoteCount.CompareTo(x.VoteCount);
        return voteCompare != 0 ? voteCompare : x.Rms.CompareTo(y.Rms);
    }

    private static HypothesisLandscape AnalyzeHypothesisLandscape(IReadOnlyList<Hypothesis> hypotheses, Hypothesis best, float inlierThreshold)
    {
        if (hypotheses.Count == 0 || best.Support <= 0)
        {
            return new HypothesisLandscape(0.0, 0.0, 0.0, 0, 0.0);
        }

        var totalVotes = Math.Max(1, hypotheses.Sum(h => h.VoteCount));
        var bestVotes = Math.Max(1, best.VoteCount);
        var bestSupport = Math.Max(1, best.Support);
        var poseTranslationScale = Math.Max(inlierThreshold * 6f, 0.01f);
        const double poseRotationScaleDeg = 15.0;

        double maxSupportRatio = 0.0;
        double maxVoteRatio = 0.0;
        double maxPoseSpread = 0.0;
        var competitiveClusters = 0;

        for (int i = 1; i < hypotheses.Count; i++)
        {
            var alt = hypotheses[i];
            var supportRatio = Math.Clamp(alt.Support / (double)bestSupport, 0.0, 1.0);
            var voteRatio = Math.Clamp(alt.VoteCount / (double)bestVotes, 0.0, 1.0);
            if (supportRatio < 0.70 && voteRatio < 0.45)
            {
                continue;
            }

            competitiveClusters++;
            maxSupportRatio = Math.Max(maxSupportRatio, supportRatio);
            maxVoteRatio = Math.Max(maxVoteRatio, voteRatio);

            var translationSpread = TranslationDistance(best.Transform, alt.Transform) / poseTranslationScale;
            var rotationSpread = RotationDifferenceDegrees(best.Transform, alt.Transform) / poseRotationScaleDeg;
            maxPoseSpread = Math.Max(maxPoseSpread, Math.Clamp(Math.Max(translationSpread, rotationSpread), 0.0, 1.0));
        }

        return new HypothesisLandscape(
            DominantVoteRatio: Math.Clamp(bestVotes / (double)totalVotes, 0.0, 1.0),
            CompetitiveSupportRatio: maxSupportRatio,
            CompetitiveVoteRatio: maxVoteRatio,
            CompetitiveClusterCount: competitiveClusters,
            PoseSpreadScore: maxPoseSpread);
    }

    private static double ComputeDominantEvidenceScore(
        int bestSupport,
        int secondarySupport,
        double bestNormalConsistency,
        HypothesisLandscape landscape)
    {
        if (bestSupport <= 0)
        {
            return 0;
        }

        var directSupportMargin = secondarySupport <= 0
            ? 1.0
            : Math.Clamp(1.0 - (secondarySupport / (double)bestSupport), 0.0, 1.0);
        var voteDominance = Math.Clamp((landscape.DominantVoteRatio - 0.45) / 0.30, 0.0, 1.0);
        var voteSeparation = 1.0 - Math.Clamp(landscape.CompetitiveVoteRatio, 0.0, 1.0);
        var supportSeparation = 1.0 - Math.Clamp(landscape.CompetitiveSupportRatio, 0.0, 1.0);
        var clusterIsolation = 1.0 - Math.Clamp(landscape.CompetitiveClusterCount / 2.0, 0.0, 1.0);
        var poseConcentration = 1.0 - Math.Clamp(landscape.PoseSpreadScore, 0.0, 1.0);
        var normalConfidence = Math.Clamp(
            (bestNormalConsistency - MinimumRecommendedNormalConsistency) / 0.18,
            0.0,
            1.0);

        return Math.Clamp(
            (voteDominance * 0.28) +
            (voteSeparation * 0.20) +
            (supportSeparation * 0.16) +
            (directSupportMargin * 0.14) +
            (clusterIsolation * 0.10) +
            (poseConcentration * 0.06) +
            (normalConfidence * 0.06),
            0.0,
            1.0);
    }

    private static double ComputeIsotropicSymmetryPrior(SymmetryDescriptor symmetry, double dominantEvidenceScore)
    {
        return Math.Clamp(symmetry.SphericalScore * (1.0 - (dominantEvidenceScore * 0.90)), 0.0, 1.0);
    }

    private static bool ShouldForceSphericalAmbiguity(
        int bestSupport,
        int secondarySupport,
        double bestNormalConsistency,
        SymmetryDescriptor symmetry,
        HypothesisLandscape landscape)
    {
        if (symmetry.SphericalScore < 0.985)
        {
            return false;
        }

        var dominantEvidence = ComputeDominantEvidenceScore(bestSupport, secondarySupport, bestNormalConsistency, landscape);
        var hasClearDominantMode =
            landscape.DominantVoteRatio >= 0.50 &&
            landscape.CompetitiveVoteRatio <= 0.50 &&
            landscape.CompetitiveSupportRatio <= 0.88 &&
            landscape.CompetitiveClusterCount <= 2;
        var highConfidenceDominantMode =
            bestNormalConsistency >= 0.90 &&
            landscape.DominantVoteRatio >= 0.48 &&
            landscape.CompetitiveVoteRatio <= 0.58 &&
            landscape.CompetitiveSupportRatio <= 0.90 &&
            landscape.PoseSpreadScore <= 0.40;
        if (dominantEvidence >= 0.50 || hasClearDominantMode)
        {
            return false;
        }

        if (highConfidenceDominantMode)
        {
            return false;
        }

        return (landscape.CompetitiveClusterCount >= 1 && landscape.CompetitiveVoteRatio >= 0.45) ||
               (landscape.CompetitiveClusterCount >= 1 && landscape.CompetitiveSupportRatio >= 0.80 && landscape.PoseSpreadScore >= 0.25) ||
               landscape.CompetitiveClusterCount >= 2;
    }

    private static double ComputeAmbiguityScore(
        int bestSupport,
        double bestRms,
        double bestNormalConsistency,
        int secondarySupport,
        double secondaryRms,
        double secondaryNormalConsistency,
        SymmetryDescriptor symmetry,
        HypothesisLandscape landscape)
    {
        if (bestSupport <= 0)
        {
            return 0;
        }

        var supportCompetition = secondarySupport <= 0
            ? 0.0
            : Math.Clamp(secondarySupport / (double)bestSupport, 0.0, 1.0);
        supportCompetition = Math.Max(supportCompetition, landscape.CompetitiveSupportRatio);
        var rmsCompetition = (!double.IsFinite(bestRms) || !double.IsFinite(secondaryRms) || secondarySupport <= 0)
            ? 0.0
            : Math.Clamp(1.0 - ((secondaryRms - bestRms) / Math.Max(bestRms, 1e-6)), 0.0, 1.0);
        var normalCompetition = secondarySupport <= 0
            ? 0.0
            : Math.Clamp(1.0 - Math.Max(0.0, bestNormalConsistency - secondaryNormalConsistency), 0.0, 1.0);
        var clusterCompetition = Math.Clamp(landscape.CompetitiveClusterCount / 3.0, 0.0, 1.0);
        var dominantEvidence = ComputeDominantEvidenceScore(bestSupport, secondarySupport, bestNormalConsistency, landscape);
        var symmetryPrior = Math.Max(
            ComputeIsotropicSymmetryPrior(symmetry, dominantEvidence),
            symmetry.AxialScore * 0.95);

        return Math.Clamp(
            (supportCompetition * 0.28) +
            (rmsCompetition * 0.16) +
            (normalCompetition * 0.10) +
            (landscape.CompetitiveVoteRatio * 0.14) +
            (clusterCompetition * 0.10) +
            (landscape.PoseSpreadScore * 0.07) +
            (symmetryPrior * 0.15) -
            (dominantEvidence * 0.22),
            0.0,
            1.0);
    }

    private static double ComputeStabilityScore(
        int bestSupport,
        double bestRms,
        double bestNormalConsistency,
        int secondarySupport,
        double secondaryRms,
        double secondaryNormalConsistency,
        SymmetryDescriptor symmetry,
        HypothesisLandscape landscape)
    {
        if (bestSupport <= 0)
        {
            return 0;
        }

        var ambiguityScore = ComputeAmbiguityScore(
            bestSupport,
            bestRms,
            bestNormalConsistency,
            secondarySupport,
            secondaryRms,
            secondaryNormalConsistency,
            symmetry,
            landscape);
        var supportMargin = secondarySupport <= 0
            ? 1.0
            : Math.Clamp(1.0 - (secondarySupport / (double)bestSupport), 0.0, 1.0);
        supportMargin = Math.Min(supportMargin, 1.0 - landscape.CompetitiveSupportRatio);
        var clusterPenalty = Math.Clamp(landscape.CompetitiveClusterCount / 3.0, 0.0, 1.0);
        var dominantEvidence = ComputeDominantEvidenceScore(bestSupport, secondarySupport, bestNormalConsistency, landscape);
        var symmetryPenalty = Math.Max(
            ComputeIsotropicSymmetryPrior(symmetry, dominantEvidence),
            symmetry.AxialScore * 0.60);

        return Math.Clamp(
            ((1.0 - ambiguityScore) * 0.42) +
            (Math.Clamp(bestNormalConsistency, 0.0, 1.0) * 0.20) +
            (Math.Clamp(landscape.DominantVoteRatio, 0.0, 1.0) * 0.18) +
            (supportMargin * 0.15) -
            (symmetryPenalty * 0.08) -
            (clusterPenalty * 0.05) +
            (dominantEvidence * 0.12),
            0.0,
            1.0);
    }

    private static bool IsAmbiguousPose(
        int bestSupport,
        int secondarySupport,
        Matrix4x4 bestTransform,
        Matrix4x4 secondaryTransform,
        float inlierThreshold,
        double ambiguityScore,
        SymmetryDescriptor symmetry,
        double bestRms,
        double secondaryRms,
        double bestNormalConsistency,
        double secondaryNormalConsistency,
        HypothesisLandscape landscape)
    {
        var dominantEvidence = ComputeDominantEvidenceScore(bestSupport, secondarySupport, bestNormalConsistency, landscape);

        if (bestSupport <= 0 || secondarySupport <= 0)
        {
            return ShouldForceSphericalAmbiguity(bestSupport, secondarySupport, bestNormalConsistency, symmetry, landscape) ||
                   (symmetry.AxialScore >= 0.90 &&
                    landscape.CompetitiveClusterCount >= 1 &&
                    landscape.CompetitiveVoteRatio >= 0.60 &&
                    landscape.PoseSpreadScore >= 0.45);
        }

        var translationDelta = TranslationDistance(bestTransform, secondaryTransform);
        var rotationDeltaDeg = RotationDifferenceDegrees(bestTransform, secondaryTransform);
        var translationTolerance = Math.Max(inlierThreshold * 6f, 0.01f);
        var supportRatio = secondarySupport / (double)bestSupport;
        var rmsComparable = double.IsFinite(bestRms) &&
                            double.IsFinite(secondaryRms) &&
                            secondaryRms <= (bestRms + Math.Max(inlierThreshold * 1.5f, bestRms * 0.25));
        var normalComparable = secondaryNormalConsistency >= Math.Max(0.55, bestNormalConsistency - 0.12);
        var distinctPose = translationDelta >= translationTolerance || rotationDeltaDeg >= 8.0;
        var symmetrySensitiveSupportThreshold = symmetry.AxialScore >= 0.75 ? 0.90 : 0.93;

        var primaryAmbiguity = ambiguityScore >= 0.86 &&
                               supportRatio >= symmetrySensitiveSupportThreshold &&
                               rmsComparable &&
                               normalComparable &&
                               distinctPose;
        var symmetryDominatedAmbiguity =
            symmetry.SphericalScore >= 0.97 &&
            dominantEvidence < 0.45 &&
            landscape.CompetitiveClusterCount >= 2 &&
            landscape.CompetitiveVoteRatio >= 0.50 &&
            landscape.PoseSpreadScore >= 0.30;
        var axialCompetitionAmbiguity =
            symmetry.AxialScore >= 0.85 &&
            landscape.CompetitiveClusterCount >= 2 &&
            landscape.CompetitiveSupportRatio >= 0.82 &&
            landscape.PoseSpreadScore >= 0.45;

        return primaryAmbiguity || symmetryDominatedAmbiguity || axialCompetitionAmbiguity;
    }

    private static SymmetryDescriptor ComputeSymmetryDescriptor(MatIndexer<float> points, int count, AxisAlignedBoundingBox box)
    {
        if (count < 3)
        {
            return new SymmetryDescriptor(0.0, 0.0, 0.0);
        }

        double cx = 0;
        double cy = 0;
        double cz = 0;
        for (int i = 0; i < count; i++)
        {
            cx += points[i, 0];
            cy += points[i, 1];
            cz += points[i, 2];
        }

        var inv = 1.0 / count;
        cx *= inv;
        cy *= inv;
        cz *= inv;

        double c00 = 0, c01 = 0, c02 = 0;
        double c11 = 0, c12 = 0, c22 = 0;
        for (int i = 0; i < count; i++)
        {
            var x = points[i, 0] - cx;
            var y = points[i, 1] - cy;
            var z = points[i, 2] - cz;
            c00 += x * x;
            c01 += x * y;
            c02 += x * z;
            c11 += y * y;
            c12 += y * z;
            c22 += z * z;
        }

        using var covariance = new Mat(3, 3, MatType.CV_64FC1);
        covariance.Set(0, 0, c00 * inv); covariance.Set(0, 1, c01 * inv); covariance.Set(0, 2, c02 * inv);
        covariance.Set(1, 0, c01 * inv); covariance.Set(1, 1, c11 * inv); covariance.Set(1, 2, c12 * inv);
        covariance.Set(2, 0, c02 * inv); covariance.Set(2, 1, c12 * inv); covariance.Set(2, 2, c22 * inv);

        using var eigenValues = new Mat();
        using var eigenVectors = new Mat();
        if (!Cv2.Eigen(covariance, eigenValues, eigenVectors))
        {
            return new SymmetryDescriptor(0.0, 0.0, ComputeExtentSymmetryScore(box));
        }

        var eig = eigenValues.GetGenericIndexer<double>();
        var lambda0 = Math.Max(eig[0], 1e-12);
        var lambda1 = Math.Max(eig[1], 1e-12);
        var lambda2 = Math.Max(eig[2], 1e-12);

        var spherical = Math.Clamp(lambda2 / lambda0, 0.0, 1.0);
        var radialPair = Math.Clamp(lambda2 / lambda1, 0.0, 1.0);
        var axisAnisotropy = 1.0 - Math.Clamp(lambda1 / lambda0, 0.0, 1.0);
        var axial = Math.Clamp(radialPair * axisAnisotropy, 0.0, 1.0);
        return new SymmetryDescriptor(spherical, axial, ComputeExtentSymmetryScore(box));
    }

    private static double ComputeExtentSymmetryScore(AxisAlignedBoundingBox box)
    {
        var extents = new[] { Math.Abs(box.Extent.X), Math.Abs(box.Extent.Y), Math.Abs(box.Extent.Z) }
            .Where(value => value > 1e-6f)
            .OrderBy(value => value)
            .ToArray();

        if (extents.Length < 2)
        {
            return 0;
        }

        var min = extents.First();
        var max = extents.Last();
        return max <= 1e-6 ? 0 : Math.Clamp(min / max, 0, 1);
    }

    private static double TranslationDistance(Matrix4x4 a, Matrix4x4 b)
    {
        var dx = a.M41 - b.M41;
        var dy = a.M42 - b.M42;
        var dz = a.M43 - b.M43;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    private static double RotationDifferenceDegrees(Matrix4x4 a, Matrix4x4 b)
    {
        if (!Matrix4x4.Decompose(a, out _, out var rotA, out _) ||
            !Matrix4x4.Decompose(b, out _, out var rotB, out _))
        {
            return 0;
        }

        rotA = Quaternion.Normalize(rotA);
        rotB = Quaternion.Normalize(rotB);
        var delta = Quaternion.Normalize(rotB * Quaternion.Conjugate(rotA));
        var clampedW = Math.Clamp(Math.Abs(delta.W), -1.0f, 1.0f);
        return 2.0 * Math.Acos(clampedW) * 180.0 / Math.PI;
    }

    private static bool AreTransformsSimilar(Matrix4x4 a, Matrix4x4 b, double translationTolerance, double rotationToleranceDeg)
    {
        return TranslationDistance(a, b) <= translationTolerance &&
               RotationDifferenceDegrees(a, b) <= rotationToleranceDeg;
    }

    private static (Matrix4x4 Transform, Correspondence[] Inliers, double Rms, double NormalConsistency) RefineHypothesis(
        MatIndexer<float> modelPoints,
        MatIndexer<float> modelNormals,
        MatIndexer<float> scenePoints,
        MatIndexer<float> sceneNormals,
        SpatialHashGridIndex coarseGrid,
        SpatialHashGridIndex fineGrid,
        int[] refineIndices,
        Matrix4x4 initialTransform,
        float coarseThreshold,
        float fineThreshold,
        int minInliers)
    {
        var (coarseTransform, coarseInliers, coarseRms) = RefineTransform(
            modelPoints,
            scenePoints,
            coarseGrid,
            refineIndices,
            initialTransform,
            threshold2: (double)coarseThreshold * coarseThreshold,
            iterations: 6,
            minInliers: minInliers);

        var (refinedTransform, refinedInliers, refinedRms) = RefineTransform(
            modelPoints,
            scenePoints,
            fineGrid,
            refineIndices,
            coarseTransform,
            threshold2: (double)fineThreshold * fineThreshold,
            iterations: 4,
            minInliers: minInliers);

        if (refinedInliers.Length >= minInliers)
        {
            return (refinedTransform, refinedInliers, refinedRms, ComputeNormalConsistency(modelNormals, sceneNormals, refinedInliers, refinedTransform));
        }

        if (coarseInliers.Length >= minInliers)
        {
            return (coarseTransform, coarseInliers, coarseRms, ComputeNormalConsistency(modelNormals, sceneNormals, coarseInliers, coarseTransform));
        }

        return (Matrix4x4.Identity, Array.Empty<Correspondence>(), double.IsFinite(refinedRms) ? refinedRms : coarseRms, 0.0);
    }

    private static double ComputeNormalConsistency(
        MatIndexer<float> modelNormals,
        MatIndexer<float> sceneNormals,
        Correspondence[] inliers,
        Matrix4x4 transform)
    {
        if (inliers.Length == 0)
        {
            return 0;
        }

        double sum = 0;
        var counted = 0;
        for (int i = 0; i < inliers.Length; i++)
        {
            var c = inliers[i];
            var modelNormal = new Vector3(modelNormals[c.ModelIndex, 0], modelNormals[c.ModelIndex, 1], modelNormals[c.ModelIndex, 2]);
            var sceneNormal = new Vector3(sceneNormals[c.SceneIndex, 0], sceneNormals[c.SceneIndex, 1], sceneNormals[c.SceneIndex, 2]);
            modelNormal = Vector3.TransformNormal(modelNormal, transform);

            if (!TryNormalize(modelNormal, out modelNormal) || !TryNormalize(sceneNormal, out sceneNormal))
            {
                continue;
            }

            sum += Math.Max(0.0f, Vector3.Dot(modelNormal, sceneNormal));
            counted++;
        }

        return counted == 0 ? 0.0 : Math.Clamp(sum / counted, 0.0, 1.0);
    }

    private static double ComputeRms(MatIndexer<float> modelPoints, MatIndexer<float> scenePoints, Correspondence[] inliers, Matrix4x4 t)
    {
        if (inliers.Length == 0)
        {
            return double.PositiveInfinity;
        }

        double sum2 = 0;
        for (int i = 0; i < inliers.Length; i++)
        {
            var c = inliers[i];
            var mp = new Vector3(modelPoints[c.ModelIndex, 0], modelPoints[c.ModelIndex, 1], modelPoints[c.ModelIndex, 2]);
            var sp = new Vector3(scenePoints[c.SceneIndex, 0], scenePoints[c.SceneIndex, 1], scenePoints[c.SceneIndex, 2]);

            var tp = Vector3.Transform(mp, t);
            var dx = (double)tp.X - sp.X;
            var dy = (double)tp.Y - sp.Y;
            var dz = (double)tp.Z - sp.Z;
            sum2 += (dx * dx) + (dy * dy) + (dz * dz);
        }

        return Math.Sqrt(sum2 / inliers.Length);
    }

    private static (int Count, double SumSquared) ScoreTransform(
        MatIndexer<float> modelPoints,
        MatIndexer<float> scenePoints,
        SpatialHashGridIndex sceneGrid,
        int[] evalModelIndices,
        Matrix4x4 t,
        double threshold2)
    {
        int count = 0;
        double sum2 = 0;

        for (int i = 0; i < evalModelIndices.Length; i++)
        {
            var mi = evalModelIndices[i];
            var mp = new Vector3(modelPoints[mi, 0], modelPoints[mi, 1], modelPoints[mi, 2]);
            var tp = Vector3.Transform(mp, t);

            if (TryFindNearest(scenePoints, sceneGrid, tp, threshold2, out _, out var d2))
            {
                count++;
                sum2 += d2;
            }
        }

        return (count, sum2);
    }

    private static bool TryFindNearest(
        MatIndexer<float> scenePoints,
        SpatialHashGridIndex grid,
        Vector3 query,
        double threshold2,
        out int nearestIndex,
        out double nearestDist2)
    {
        var inv = grid.InvCellSize;
        var cx = (int)MathF.Floor(query.X * inv);
        var cy = (int)MathF.Floor(query.Y * inv);
        var cz = (int)MathF.Floor(query.Z * inv);

        nearestIndex = -1;
        nearestDist2 = threshold2;

        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var key = new SpatialCellKey(cx + dx, cy + dy, cz + dz);
                    if (!grid.Cells.TryGetValue(key, out var candidates))
                    {
                        continue;
                    }

                    for (int c = 0; c < candidates.Count; c++)
                    {
                        var j = candidates[c];
                        var ddx = (double)scenePoints[j, 0] - query.X;
                        var ddy = (double)scenePoints[j, 1] - query.Y;
                        var ddz = (double)scenePoints[j, 2] - query.Z;
                        var d2 = (ddx * ddx) + (ddy * ddy) + (ddz * ddz);
                        if (d2 <= nearestDist2)
                        {
                            nearestDist2 = d2;
                            nearestIndex = j;
                        }
                    }
                }
            }
        }

        return nearestIndex >= 0;
    }

    private static int[] BuildEvaluationIndices(int count, int targetCount)
    {
        if (count <= 0)
        {
            return Array.Empty<int>();
        }

        var stride = Math.Max(1, count / Math.Max(1, targetCount));
        var indices = new List<int>(capacity: Math.Min(count, targetCount + 16));
        for (int i = 0; i < count; i += stride)
        {
            indices.Add(i);
        }

        return indices.ToArray();
    }

    private static (Matrix4x4 Transform, Correspondence[] Inliers, double Rms) RefineTransform(
        MatIndexer<float> modelPoints,
        MatIndexer<float> scenePoints,
        SpatialHashGridIndex sceneGrid,
        int[] modelIndices,
        Matrix4x4 initial,
        double threshold2,
        int iterations,
        int minInliers)
    {
        var t = initial;
        Correspondence[] inliers = Array.Empty<Correspondence>();
        double rms = double.PositiveInfinity;

        for (int it = 0; it < iterations; it++)
        {
            var inliers0 = CollectInliers(modelPoints, scenePoints, sceneGrid, modelIndices, t, threshold2);
            if (inliers0.Length < minInliers)
            {
                rms = double.PositiveInfinity;
                break;
            }

            var tNew = RigidTransformEstimator.Estimate(modelPoints, scenePoints, inliers0);

            // Re-collect inliers under the updated transform so reported RMS reflects the final transform,
            // not the pre-update correspondence set.
            var inliers1 = CollectInliers(modelPoints, scenePoints, sceneGrid, modelIndices, tNew, threshold2);
            if (inliers1.Length < minInliers)
            {
                rms = double.PositiveInfinity;
                break;
            }

            t = tNew;
            inliers = inliers1;
            rms = ComputeRms(modelPoints, scenePoints, inliers, t);
        }

        return (t, inliers, rms);
    }

    private static Correspondence[] CollectInliers(
        MatIndexer<float> modelPoints,
        MatIndexer<float> scenePoints,
        SpatialHashGridIndex sceneGrid,
        int[] modelIndices,
        Matrix4x4 t,
        double threshold2)
    {
        var list = new List<Correspondence>(capacity: modelIndices.Length);
        for (int i = 0; i < modelIndices.Length; i++)
        {
            var mi = modelIndices[i];
            var mp = new Vector3(modelPoints[mi, 0], modelPoints[mi, 1], modelPoints[mi, 2]);
            var tp = Vector3.Transform(mp, t);

            if (TryFindNearest(scenePoints, sceneGrid, tp, threshold2, out var sj, out _))
            {
                list.Add(new Correspondence(mi, sj));
            }
        }

        return list.ToArray();
    }

    private bool TrySample3(List<Correspondence> pool, out Correspondence c1, out Correspondence c2, out Correspondence c3)
    {
        if (pool.Count < 3)
        {
            c1 = c2 = c3 = default;
            return false;
        }

        for (int attempt = 0; attempt < 16; attempt++)
        {
            var i1 = _rng.Next(pool.Count);
            var i2 = _rng.Next(pool.Count);
            var i3 = _rng.Next(pool.Count);
            if (i1 == i2 || i1 == i3 || i2 == i3)
            {
                continue;
            }

            c1 = pool[i1];
            c2 = pool[i2];
            c3 = pool[i3];

            if (c1.ModelIndex == c2.ModelIndex || c1.ModelIndex == c3.ModelIndex || c2.ModelIndex == c3.ModelIndex)
            {
                continue;
            }
            if (c1.SceneIndex == c2.SceneIndex || c1.SceneIndex == c3.SceneIndex || c2.SceneIndex == c3.SceneIndex)
            {
                continue;
            }

            return true;
        }

        c1 = c2 = c3 = default;
        return false;
    }

    private static bool IsNonDegenerateTriangle(MatIndexer<float> points, int a, int b, int c)
    {
        var p0 = new Vector3(points[a, 0], points[a, 1], points[a, 2]);
        var p1 = new Vector3(points[b, 0], points[b, 1], points[b, 2]);
        var p2 = new Vector3(points[c, 0], points[c, 1], points[c, 2]);

        var v1 = p1 - p0;
        var v2 = p2 - p0;
        var cross = Vector3.Cross(v1, v2);
        return cross.LengthSquared() > 1e-10f;
    }

    private static class RigidTransformEstimator
    {
        public static Matrix4x4 Estimate(MatIndexer<float> modelPoints, MatIndexer<float> scenePoints, ReadOnlySpan<Correspondence> correspondences)
        {
            // Kabsch via SVD on 3x3 covariance.
            // Note: correspondences are model->scene point matches.
            double mx = 0, my = 0, mz = 0;
            double sx = 0, sy = 0, sz = 0;

            for (int i = 0; i < correspondences.Length; i++)
            {
                var c = correspondences[i];
                mx += modelPoints[c.ModelIndex, 0];
                my += modelPoints[c.ModelIndex, 1];
                mz += modelPoints[c.ModelIndex, 2];
                sx += scenePoints[c.SceneIndex, 0];
                sy += scenePoints[c.SceneIndex, 1];
                sz += scenePoints[c.SceneIndex, 2];
            }

            var inv = 1.0 / correspondences.Length;
            mx *= inv; my *= inv; mz *= inv;
            sx *= inv; sy *= inv; sz *= inv;

            double h00 = 0, h01 = 0, h02 = 0;
            double h10 = 0, h11 = 0, h12 = 0;
            double h20 = 0, h21 = 0, h22 = 0;

            for (int i = 0; i < correspondences.Length; i++)
            {
                var c = correspondences[i];
                var x = modelPoints[c.ModelIndex, 0] - mx;
                var y = modelPoints[c.ModelIndex, 1] - my;
                var z = modelPoints[c.ModelIndex, 2] - mz;

                var X = scenePoints[c.SceneIndex, 0] - sx;
                var Y = scenePoints[c.SceneIndex, 1] - sy;
                var Z = scenePoints[c.SceneIndex, 2] - sz;

                h00 += x * X; h01 += x * Y; h02 += x * Z;
                h10 += y * X; h11 += y * Y; h12 += y * Z;
                h20 += z * X; h21 += z * Y; h22 += z * Z;
            }

            using var H = new Mat(3, 3, MatType.CV_64FC1);
            H.Set(0, 0, h00); H.Set(0, 1, h01); H.Set(0, 2, h02);
            H.Set(1, 0, h10); H.Set(1, 1, h11); H.Set(1, 2, h12);
            H.Set(2, 0, h20); H.Set(2, 1, h21); H.Set(2, 2, h22);

            using var w = new Mat();
            using var u = new Mat();
            using var vt = new Mat();
            Cv2.SVDecomp(H, w, u, vt);

            // R = V * U^T
            using var v = new Mat();
            using var ut = new Mat();
            Cv2.Transpose(vt, v);
            Cv2.Transpose(u, ut);

            using var Rm = new Mat();
            Cv2.Gemm(v, ut, 1.0, new Mat(), 0.0, Rm);

            // Fix reflection if det(R) < 0
            var det = Cv2.Determinant(Rm);
            if (det < 0)
            {
                // Flip last column of V and recompute.
                var vidx = v.GetGenericIndexer<double>();
                vidx[0, 2] = -vidx[0, 2];
                vidx[1, 2] = -vidx[1, 2];
                vidx[2, 2] = -vidx[2, 2];

                using var Rm2 = new Mat();
                Cv2.Gemm(v, ut, 1.0, new Mat(), 0.0, Rm2);
                return ComposeTransform(Rm2, mx, my, mz, sx, sy, sz);
            }

            return ComposeTransform(Rm, mx, my, mz, sx, sy, sz);
        }

        private static Matrix4x4 ComposeTransform(Mat R, double mx, double my, double mz, double sx, double sy, double sz)
        {
            var r = R.GetGenericIndexer<double>();

            var mCentroid = new Vector3((float)mx, (float)my, (float)mz);
            var sCentroid = new Vector3((float)sx, (float)sy, (float)sz);

            var rot = new Matrix4x4(
                (float)r[0, 0], (float)r[1, 0], (float)r[2, 0], 0,
                (float)r[0, 1], (float)r[1, 1], (float)r[2, 1], 0,
                (float)r[0, 2], (float)r[1, 2], (float)r[2, 2], 0,
                0, 0, 0, 1);

            var t = sCentroid - Vector3.Transform(mCentroid, rot);
            rot.M41 = t.X;
            rot.M42 = t.Y;
            rot.M43 = t.Z;
            return rot;
        }
    }
}
