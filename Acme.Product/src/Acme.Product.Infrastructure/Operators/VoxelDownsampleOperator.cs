using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using PointCloudModel = Acme.Product.Infrastructure.PointCloud.PointCloud;
using Acme.Product.Infrastructure.PointCloud.Filters;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "体素下采样",
    Description = "Voxel grid downsampling for point clouds (centroid per voxel).",
    Category = "3D",
    IconName = "voxel",
    Keywords = new[] { "PointCloud", "Voxel", "Downsample", "3D" },
    Version = "1.0.0"
)]
[InputPort("PointCloud", "Point Cloud", PortDataType.Any, IsRequired = true)]
[OutputPort("PointCloud", "Point Cloud", PortDataType.Any)]
[OutputPort("PointCount", "Point Count", PortDataType.Integer)]
[OperatorParam("LeafSize", "Leaf Size", "double", DefaultValue = 0.01, Min = 1e-6)]
public sealed class VoxelDownsampleOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.VoxelDownsample;

    public VoxelDownsampleOperator(ILogger<VoxelDownsampleOperator> logger) : base(logger)
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

        var leafSize = (float)GetDoubleParam(@operator, "LeafSize", 0.01, min: 1e-6, max: 10_000);

        var filter = new VoxelGridFilter();
        PointCloudModel downsampled;

        try
        {
            downsampled = await RunCpuBoundWork(
                () => filter.Downsample(cloud, leafSize),
                cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Voxel downsample failed.");
            return OperatorExecutionOutput.Failure($"Voxel downsample failed: {ex.Message}");
        }

        return OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            ["PointCloud"] = downsampled,
            ["PointCount"] = downsampled.Count,
            ["LeafSize"] = leafSize
        });
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        _ = GetDoubleParam(@operator, "LeafSize", 0.01, min: 1e-6, max: 10_000);
        return ValidationResult.Valid();
    }
}
