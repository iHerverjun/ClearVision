using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.PointCloud.Segmentation;
using Microsoft.Extensions.Logging;
using PointCloudModel = Acme.Product.Infrastructure.PointCloud.PointCloud;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "RANSAC平面分割",
    Description = "RANSAC plane segmentation for point clouds. Outputs plane coefficients and inliers.",
    Category = "3D",
    IconName = "plane",
    Keywords = new[] { "PointCloud", "RANSAC", "Plane", "Segmentation", "3D" },
    Version = "1.0.0"
)]
[InputPort("PointCloud", "Point Cloud", PortDataType.Any, IsRequired = true)]
[OutputPort("PlaneA", "Plane A", PortDataType.Float)]
[OutputPort("PlaneB", "Plane B", PortDataType.Float)]
[OutputPort("PlaneC", "Plane C", PortDataType.Float)]
[OutputPort("PlaneD", "Plane D", PortDataType.Float)]
[OutputPort("InlierCount", "Inlier Count", PortDataType.Integer)]
[OutputPort("InlierRatio", "Inlier Ratio", PortDataType.Float)]
[OutputPort("Inliers", "Inliers", PortDataType.Any)]
[OutputPort("InlierPointCloud", "Inlier Point Cloud", PortDataType.Any)]
[OperatorParam("DistanceThreshold", "Distance Threshold", "double", DefaultValue = 0.01, Min = 1e-6)]
[OperatorParam("MaxIterations", "Max Iterations", "int", DefaultValue = 1000, Min = 1, Max = 200000)]
[OperatorParam("MinInliers", "Min Inliers", "int", DefaultValue = 100, Min = 1, Max = 10000000)]
[OperatorParam("RandomSeed", "Random Seed (0=DeterministicFromInput)", "int", DefaultValue = 0, Min = 0, Max = 2147483647)]
public sealed class RansacPlaneSegmentationOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.RansacPlaneSegmentation;

    public RansacPlaneSegmentationOperator(ILogger<RansacPlaneSegmentationOperator> logger) : base(logger)
    {
    }

    protected override async Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null || !inputs.TryGetValue("PointCloud", out var cloudObj) || cloudObj is null)
        {
            return OperatorExecutionOutput.Failure("PointCloud input is required.");
        }

        if (cloudObj is not PointCloudModel cloud)
        {
            return OperatorExecutionOutput.Failure($"PointCloud input must be {nameof(PointCloudModel)}.");
        }

        if (cloud.Count == 0)
        {
            return OperatorExecutionOutput.Failure("EmptyPointCloud: point cloud is empty.");
        }

        var distanceThreshold = (float)GetDoubleParam(@operator, "DistanceThreshold", 0.01, min: 1e-6, max: 1000);
        var maxIterations = GetIntParam(@operator, "MaxIterations", 1000, min: 1, max: 200000);
        var minInliers = GetIntParam(@operator, "MinInliers", 100, min: 1, max: 10_000_000);
        var randomSeed = GetIntParam(@operator, "RandomSeed", 0, min: 0, max: int.MaxValue);
        int? resolvedSeed = randomSeed == 0 ? null : randomSeed;

        var segmenter = new RansacPlaneSegmentation(seed: resolvedSeed);
        RansacPlaneResult result;
        PointCloudModel inlierCloud;

        try
        {
            result = await RunCpuBoundWork(
                () => segmenter.Segment(cloud, distanceThreshold, maxIterations, minInliers),
                cancellationToken);

            if (result.InlierCount < minInliers)
            {
                return OperatorExecutionOutput.Failure("NoPlaneFound: RANSAC failed to find a plane with enough inliers.");
            }

            inlierCloud = await RunCpuBoundWork(
                () => segmenter.ExtractInlierCloud(cloud, result.Inliers),
                cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "RANSAC plane segmentation failed.");
            return OperatorExecutionOutput.Failure($"RANSAC plane segmentation failed: {ex.Message}");
        }

        var ratio = (double)result.InlierCount / Math.Max(1, cloud.Count);

        return OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            ["PlaneA"] = (double)result.Normal.X,
            ["PlaneB"] = (double)result.Normal.Y,
            ["PlaneC"] = (double)result.Normal.Z,
            ["PlaneD"] = (double)result.D,
            ["InlierCount"] = result.InlierCount,
            ["InlierRatio"] = ratio,
            ["Inliers"] = result.Inliers,
            ["InlierPointCloud"] = inlierCloud
        });
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        _ = GetDoubleParam(@operator, "DistanceThreshold", 0.01, min: 1e-6, max: 1000);
        _ = GetIntParam(@operator, "MaxIterations", 1000, min: 1, max: 200000);
        _ = GetIntParam(@operator, "MinInliers", 100, min: 1, max: 10_000_000);
        _ = GetIntParam(@operator, "RandomSeed", 0, min: 0, max: int.MaxValue);
        return ValidationResult.Valid();
    }
}

