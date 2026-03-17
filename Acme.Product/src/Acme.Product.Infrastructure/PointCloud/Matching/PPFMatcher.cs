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
    double RmsError);

/// <summary>
/// Simplified PPF-based surface matching:
/// - Build a quantized PPF hash table from the model (within FeatureRadius).
/// - Sample reference points in the scene, generate candidate correspondences via hash lookup.
/// - Use RANSAC to estimate a rigid transform (model -&gt; scene) from correspondences and verify by inlier count.
/// </summary>
public sealed class PPFMatcher
{
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

        var (bestT, bestInliers, bestRms) = RansacRigidTransform(
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
        var (coarseT, coarseInliers, coarseRms) = RefineTransform(
            modelPoints,
            scenePoints,
            coarseGrid,
            refineIndices,
            bestT,
            threshold2: (double)coarseThreshold * coarseThreshold,
            iterations: 6,
            minInliers: minInliers);

        var (refined, refinedInliers, refinedRms) = RefineTransform(
            modelPoints,
            scenePoints,
            nnGrid,
            refineIndices,
            coarseT,
            threshold2: (double)inlierThreshold * inlierThreshold,
            iterations: 4,
            minInliers: minInliers);

        if (refinedInliers.Length < minInliers)
        {
            // Provide the best available refinement RMS for diagnostics.
            var rr = double.IsFinite(refinedRms) ? refinedRms : coarseRms;
            var cc = refinedInliers.Length > 0 ? refinedInliers.Length : coarseInliers.Length;
            return new PPFMatchResult(false, Matrix4x4.Identity, cc, correspondences.Count, rr);
        }

        return new PPFMatchResult(true, refined, refinedInliers.Length, correspondences.Count, refinedRms);
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

    private (Matrix4x4 Transform, Correspondence[] Inliers, double BestRms) RansacRigidTransform(
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

        Matrix4x4 bestT = Matrix4x4.Identity;
        int bestCount = 0;
        double bestRms = double.PositiveInfinity;
        Correspondence[] bestInliers = Array.Empty<Correspondence>();

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

        // Build inlier correspondences for refinement.
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
                (float)r[0, 0], (float)r[0, 1], (float)r[0, 2], 0,
                (float)r[1, 0], (float)r[1, 1], (float)r[1, 2], 0,
                (float)r[2, 0], (float)r[2, 1], (float)r[2, 2], 0,
                0, 0, 0, 1);

            var t = sCentroid - Vector3.Transform(mCentroid, rot);
            rot.M41 = t.X;
            rot.M42 = t.Y;
            rot.M43 = t.Z;
            return rot;
        }
    }
}
