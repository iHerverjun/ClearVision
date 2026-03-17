using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.PointCloud.Features;
using Microsoft.Extensions.Logging;
using PointCloudModel = Acme.Product.Infrastructure.PointCloud.PointCloud;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "PPF点对特征",
    Description = "Compute Point Pair Features (PPF) for a point cloud and build a per-point feature map.",
    Category = "3D",
    IconName = "ppf",
    Keywords = new[] { "PointCloud", "PPF", "Feature", "3D" },
    Version = "1.0.0"
)]
[InputPort("PointCloud", "Point Cloud", PortDataType.Any, IsRequired = true)]
[OutputPort("PPFMap", "PPF Map", PortDataType.Any)]
[OutputPort("PointCloudWithNormals", "Point Cloud With Normals", PortDataType.Any)]
[OutputPort("PointCount", "Point Count", PortDataType.Integer)]
[OperatorParam("NormalRadius", "Normal Radius", "double", DefaultValue = 0.03, Min = 1e-6)]
[OperatorParam("FeatureRadius", "Feature Radius", "double", DefaultValue = 0.05, Min = 1e-6)]
[OperatorParam("UseExistingNormals", "Use Existing Normals", "bool", DefaultValue = true)]
public sealed class PPFEstimationOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.PPFEstimation;

    public PPFEstimationOperator(ILogger<PPFEstimationOperator> logger) : base(logger)
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

        var normalRadius = (float)GetDoubleParam(@operator, "NormalRadius", 0.03, min: 1e-6, max: 1000);
        var featureRadius = (float)GetDoubleParam(@operator, "FeatureRadius", 0.05, min: 1e-6, max: 1000);
        var useExistingNormals = GetBoolParam(@operator, "UseExistingNormals", defaultValue: true);

        var estimator = new PPFEstimation();
        Dictionary<int, List<PPFFeature>> map;
        PointCloudModel withNormals;

        try
        {
            withNormals = await RunCpuBoundWork(
                () => estimator.ComputePointCloudWithNormals(cloud, normalRadius, useExistingNormals),
                cancellationToken);

            map = await RunCpuBoundWork(
                () => estimator.ComputeModel(withNormals, normalRadius, featureRadius, useExistingNormals: true),
                cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "PPF estimation failed.");
            return OperatorExecutionOutput.Failure($"PPF estimation failed: {ex.Message}");
        }

        return OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            ["PPFMap"] = map,
            ["PointCloudWithNormals"] = withNormals,
            ["PointCount"] = withNormals.Count,
            ["NormalRadius"] = normalRadius,
            ["FeatureRadius"] = featureRadius
        });
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        _ = GetDoubleParam(@operator, "NormalRadius", 0.03, min: 1e-6, max: 1000);
        _ = GetDoubleParam(@operator, "FeatureRadius", 0.05, min: 1e-6, max: 1000);
        _ = GetBoolParam(@operator, "UseExistingNormals", defaultValue: true);
        return ValidationResult.Valid();
    }
}

