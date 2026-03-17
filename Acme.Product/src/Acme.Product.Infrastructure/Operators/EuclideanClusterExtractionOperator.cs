using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.PointCloud.Segmentation;
using Microsoft.Extensions.Logging;
using PointCloudModel = Acme.Product.Infrastructure.PointCloud.PointCloud;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "欧氏聚类分割",
    Description = "Euclidean Cluster Extraction for point clouds (3D connected components by distance).",
    Category = "3D",
    IconName = "cluster",
    Keywords = new[] { "PointCloud", "Cluster", "Segmentation", "3D" },
    Version = "1.0.0"
)]
[InputPort("PointCloud", "Point Cloud", PortDataType.Any, IsRequired = true)]
[OutputPort("Clusters", "Clusters", PortDataType.Any)]
[OutputPort("ClusterCount", "Cluster Count", PortDataType.Integer)]
[OutputPort("ClusterPointClouds", "Cluster Point Clouds", PortDataType.Any)]
[OperatorParam("ClusterTolerance", "Cluster Tolerance", "double", DefaultValue = 0.02, Min = 1e-6)]
[OperatorParam("MinClusterSize", "Min Cluster Size", "int", DefaultValue = 100, Min = 1, Max = 10000000)]
[OperatorParam("MaxClusterSize", "Max Cluster Size", "int", DefaultValue = 1000000, Min = 1, Max = 10000000)]
public sealed class EuclideanClusterExtractionOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.EuclideanClusterExtraction;

    public EuclideanClusterExtractionOperator(ILogger<EuclideanClusterExtractionOperator> logger) : base(logger)
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

        var tol = (float)GetDoubleParam(@operator, "ClusterTolerance", 0.02, min: 1e-6, max: 1000);
        var minSize = GetIntParam(@operator, "MinClusterSize", 100, min: 1, max: 10_000_000);
        var maxSize = GetIntParam(@operator, "MaxClusterSize", 1_000_000, min: 1, max: 10_000_000);

        if (minSize > maxSize)
        {
            return OperatorExecutionOutput.Failure("InvalidParameters: MinClusterSize must be <= MaxClusterSize.");
        }

        var extractor = new EuclideanClusterExtraction();
        List<int[]> clusters;
        List<PointCloudModel> clusterClouds;

        try
        {
            clusters = await RunCpuBoundWork(
                () => extractor.Extract(cloud, clusterTolerance: tol, minClusterSize: minSize, maxClusterSize: maxSize),
                cancellationToken);

            clusterClouds = await RunCpuBoundWork(
                () => extractor.ExtractPointClouds(cloud, clusterTolerance: tol, minClusterSize: minSize, maxClusterSize: maxSize),
                cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Euclidean cluster extraction failed.");
            return OperatorExecutionOutput.Failure($"Euclidean cluster extraction failed: {ex.Message}");
        }

        return OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            ["Clusters"] = clusters,
            ["ClusterCount"] = clusters.Count,
            ["ClusterPointClouds"] = clusterClouds
        });
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        _ = GetDoubleParam(@operator, "ClusterTolerance", 0.02, min: 1e-6, max: 1000);
        var minSize = GetIntParam(@operator, "MinClusterSize", 100, min: 1, max: 10_000_000);
        var maxSize = GetIntParam(@operator, "MaxClusterSize", 1_000_000, min: 1, max: 10_000_000);

        if (minSize > maxSize)
        {
            return ValidationResult.Invalid("MinClusterSize must be <= MaxClusterSize.");
        }

        return ValidationResult.Valid();
    }
}

