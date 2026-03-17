using System.Numerics;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.PointCloud.Matching;
using Microsoft.Extensions.Logging;
using PointCloudModel = Acme.Product.Infrastructure.PointCloud.PointCloud;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "PPF表面匹配",
    Description = "Simplified PPF-based 3D surface matching (model -> scene pose).",
    Category = "3D",
    IconName = "match3d",
    Keywords = new[] { "PointCloud", "PPF", "Match", "Pose", "3D" },
    Version = "1.0.0"
)]
[InputPort("ModelPointCloud", "Model Point Cloud", PortDataType.Any, IsRequired = true)]
[InputPort("ScenePointCloud", "Scene Point Cloud", PortDataType.Any, IsRequired = true)]
[OutputPort("IsMatched", "Is Matched", PortDataType.Boolean)]
[OutputPort("TransformMatrix", "Transform Matrix", PortDataType.Any)]
[OutputPort("InlierCount", "Inlier Count", PortDataType.Integer)]
[OutputPort("InlierRatio", "Inlier Ratio", PortDataType.Float)]
[OutputPort("CorrespondenceCount", "Correspondence Count", PortDataType.Integer)]
[OutputPort("RmsError", "RMS Error", PortDataType.Float)]
[OperatorParam("NormalRadius", "Normal Radius", "double", DefaultValue = 0.03, Min = 1e-6)]
[OperatorParam("FeatureRadius", "Feature Radius", "double", DefaultValue = 0.08, Min = 1e-6)]
[OperatorParam("NumSamples", "Num Samples", "int", DefaultValue = 120, Min = 10, Max = 5000)]
[OperatorParam("ModelRefStride", "Model Ref Stride", "int", DefaultValue = 3, Min = 1, Max = 50)]
[OperatorParam("RansacIterations", "RANSAC Iterations", "int", DefaultValue = 800, Min = 50, Max = 100000)]
[OperatorParam("InlierThreshold", "Inlier Threshold", "double", DefaultValue = 0.005, Min = 1e-6)]
[OperatorParam("MinInliers", "Min Inliers", "int", DefaultValue = 80, Min = 3, Max = 1000000)]
[OperatorParam("DistanceStep", "Distance Step", "double", DefaultValue = 0.01, Min = 1e-6)]
[OperatorParam("AngleStepDeg", "Angle Step (deg)", "double", DefaultValue = 5.0, Min = 0.1, Max = 90.0)]
public sealed class PPFMatchOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.PPFMatch;

    public PPFMatchOperator(ILogger<PPFMatchOperator> logger) : base(logger)
    {
    }

    protected override async Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null ||
            !inputs.TryGetValue("ModelPointCloud", out var modelObj) ||
            !inputs.TryGetValue("ScenePointCloud", out var sceneObj) ||
            modelObj is null || sceneObj is null)
        {
            return OperatorExecutionOutput.Failure("ModelPointCloud and ScenePointCloud inputs are required.");
        }

        if (modelObj is not PointCloudModel model)
        {
            return OperatorExecutionOutput.Failure($"ModelPointCloud must be {nameof(PointCloudModel)}.");
        }

        if (sceneObj is not PointCloudModel scene)
        {
            return OperatorExecutionOutput.Failure($"ScenePointCloud must be {nameof(PointCloudModel)}.");
        }

        if (model.Count == 0 || scene.Count == 0)
        {
            return OperatorExecutionOutput.Failure("EmptyPointCloud: model or scene point cloud is empty.");
        }

        var normalRadius = (float)GetDoubleParam(@operator, "NormalRadius", 0.03, min: 1e-6, max: 1000);
        var featureRadius = (float)GetDoubleParam(@operator, "FeatureRadius", 0.08, min: 1e-6, max: 1000);
        var numSamples = GetIntParam(@operator, "NumSamples", 120, min: 10, max: 5000);
        var modelRefStride = GetIntParam(@operator, "ModelRefStride", 3, min: 1, max: 50);
        var ransacIterations = GetIntParam(@operator, "RansacIterations", 800, min: 50, max: 100000);
        var inlierThreshold = (float)GetDoubleParam(@operator, "InlierThreshold", 0.005, min: 1e-6, max: 1000);
        var minInliers = GetIntParam(@operator, "MinInliers", 80, min: 3, max: 1_000_000);
        var distanceStep = (float)GetDoubleParam(@operator, "DistanceStep", 0.01, min: 1e-6, max: 1000);
        var angleStepDeg = (float)GetDoubleParam(@operator, "AngleStepDeg", 5.0, min: 0.1, max: 90.0);
        var angleStepRad = angleStepDeg * (MathF.PI / 180f);

        var matcher = new PPFMatcher();
        PPFMatchResult result;

        try
        {
            result = await RunCpuBoundWork(
                () => matcher.Match(
                    model,
                    scene,
                    normalRadius: normalRadius,
                    featureRadius: featureRadius,
                    distanceStep: distanceStep,
                    angleStepRad: angleStepRad,
                    numSamples: numSamples,
                    modelRefStride: modelRefStride,
                    ransacIterations: ransacIterations,
                    inlierThreshold: inlierThreshold,
                    minInliers: minInliers),
                cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "PPF match failed.");
            return OperatorExecutionOutput.Failure($"PPF match failed: {ex.Message}");
        }

        var ratio = (double)result.InlierCount / Math.Max(1, model.Count);

        return OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            ["IsMatched"] = result.IsMatched,
            ["TransformMatrix"] = result.TransformModelToScene,
            ["InlierCount"] = result.InlierCount,
            ["InlierRatio"] = ratio,
            ["CorrespondenceCount"] = result.CorrespondenceCount,
            ["RmsError"] = result.RmsError,
            ["NormalRadius"] = normalRadius,
            ["FeatureRadius"] = featureRadius
        });
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        _ = GetDoubleParam(@operator, "NormalRadius", 0.03, min: 1e-6, max: 1000);
        _ = GetDoubleParam(@operator, "FeatureRadius", 0.08, min: 1e-6, max: 1000);
        _ = GetIntParam(@operator, "NumSamples", 120, min: 10, max: 5000);
        _ = GetIntParam(@operator, "ModelRefStride", 3, min: 1, max: 50);
        _ = GetIntParam(@operator, "RansacIterations", 800, min: 50, max: 100000);
        _ = GetDoubleParam(@operator, "InlierThreshold", 0.005, min: 1e-6, max: 1000);
        _ = GetIntParam(@operator, "MinInliers", 80, min: 3, max: 1_000_000);
        _ = GetDoubleParam(@operator, "DistanceStep", 0.01, min: 1e-6, max: 1000);
        _ = GetDoubleParam(@operator, "AngleStepDeg", 5.0, min: 0.1, max: 90.0);
        return ValidationResult.Valid();
    }
}

