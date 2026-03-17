using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Core.Attributes;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Acme.Product.Tests.Operators;

public class RoiTransformOperatorTests
{
    private readonly RoiTransformOperator _roiOperator;
    private readonly CaliperToolOperator _caliper;

    public RoiTransformOperatorTests()
    {
        _roiOperator = new RoiTransformOperator(Substitute.For<ILogger<RoiTransformOperator>>());
        _caliper = new CaliperToolOperator(Substitute.For<ILogger<CaliperToolOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeRoiTransform()
    {
        Assert.Equal(OperatorType.RoiTransform, _roiOperator.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldTrackRoi_And_EnableCaliperPairs_WithSubpixelAccuracy()
    {
        // Base "reference" block at integer coordinates.
        var baseBlock = new Rect(90, 60, 40, 100);
        var baseRoi = new Rect(60, 80, 100, 60); // Center = (110,110), crosses the block's vertical edges.

        const int width = 300;
        const int height = 220;

        using var baseImage = new Mat(height, width, MatType.CV_8UC1, Scalar.Black);
        Cv2.Rectangle(baseImage, baseBlock, Scalar.White, -1);

        // Apply subpixel translation to simulate the "current frame" where the part moved.
        const double dx = 55.25;
        const double dy = -18.75;
        var matchCenterX = (baseRoi.X + baseRoi.Width / 2.0) + dx;
        var matchCenterY = (baseRoi.Y + baseRoi.Height / 2.0) + dy;

        using var moved = new Mat();
        using (var m = new Mat(2, 3, MatType.CV_64FC1))
        {
            m.Set(0, 0, 1.0); m.Set(0, 1, 0.0); m.Set(0, 2, dx);
            m.Set(1, 0, 0.0); m.Set(1, 1, 1.0); m.Set(1, 2, dy);
            Cv2.WarpAffine(baseImage, moved, m, new Size(width, height), InterpolationFlags.Linear, BorderTypes.Constant, Scalar.Black);
        }

        var roiOp = CreateRoiOperator(new Dictionary<string, object>
        {
            { "MatchIndex", 0 }
        });

        var roiInputs = new Dictionary<string, object>
        {
            ["BaseRoi"] = new Dictionary<string, object>
            {
                ["X"] = baseRoi.X,
                ["Y"] = baseRoi.Y,
                ["Width"] = baseRoi.Width,
                ["Height"] = baseRoi.Height
            },
            ["Matches"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["CenterX"] = matchCenterX,
                    ["CenterY"] = matchCenterY,
                    ["Angle"] = 0.0,
                    ["Scale"] = 1.0,
                    ["Score"] = 0.99
                }
            }
        };

        var roiResult = await _roiOperator.ExecuteAsync(roiOp, roiInputs);
        Assert.True(roiResult.IsSuccess);
        Assert.NotNull(roiResult.OutputData);
        Assert.True(roiResult.OutputData!.ContainsKey("SearchRegion"));

        var searchRegion = Assert.IsType<Dictionary<string, object>>(roiResult.OutputData["SearchRegion"]);

        var trackedX = Convert.ToInt32(searchRegion["X"]);
        var trackedY = Convert.ToInt32(searchRegion["Y"]);
        var trackedW = Convert.ToInt32(searchRegion["Width"]);
        var trackedH = Convert.ToInt32(searchRegion["Height"]);

        var trackedCenterX = trackedX + trackedW / 2.0;
        var trackedCenterY = trackedY + trackedH / 2.0;

        // "ROI跟随无延迟": tracked ROI center should follow match center (within rounding tolerance).
        Assert.InRange(trackedCenterX, matchCenterX - 1.0, matchCenterX + 1.0);
        Assert.InRange(trackedCenterY, matchCenterY - 1.0, matchCenterY + 1.0);

        var caliperOp = CreateCaliperOperator(new Dictionary<string, object>
        {
            { "Direction", "Horizontal" },
            { "Polarity", "Both" },
            { "EdgeThreshold", 10.0 },
            { "ExpectedCount", 1 },
            { "MeasureMode", "edge_pairs" },
            { "PairDirection", "any" },
            { "SubpixelAccuracy", true },
            { "SubPixelMode", "gradient_centroid" }
        });

        var caliperInputs = new Dictionary<string, object>
        {
            ["Image"] = new ImageWrapper(moved.Clone()),
            ["SearchRegion"] = searchRegion
        };

        var caliperResult = await _caliper.ExecuteAsync(caliperOp, caliperInputs);
        Assert.True(caliperResult.IsSuccess);
        Assert.NotNull(caliperResult.OutputData);

        // "标准量块 宽度误差<0.1px": true width is 40px. Assert subpixel result within 0.1px.
        var measured = Convert.ToDouble(caliperResult.OutputData!["Width"]);
        Assert.InRange(measured, 39.9, 40.1);

        var pairCount = Convert.ToInt32(caliperResult.OutputData["PairCount"]);
        Assert.Equal(1, pairCount);
    }

    [Fact]
    public void MetadataScan_ShouldResolve_UniqueRoiTransformOperator()
    {
        var assembly = typeof(OperatorBase).Assembly;
        var candidates = assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => typeof(OperatorBase).IsAssignableFrom(t))
            .Where(t => t.GetCustomAttribute<OperatorMetaAttribute>(inherit: false) != null)
            .ToList();

        var resolved = new List<Type>();
        foreach (var t in candidates)
        {
            var type = ResolveOperatorTypeForScan(t);
            if (type == OperatorType.RoiTransform)
            {
                resolved.Add(t);
            }
        }

        Assert.Single(resolved);
        Assert.Equal(typeof(RoiTransformOperator), resolved[0]);
    }

    private static OperatorType ResolveOperatorTypeForScan(Type operatorClrType)
    {
        var property = operatorClrType.GetProperty(nameof(OperatorBase.OperatorType), BindingFlags.Public | BindingFlags.Instance);
        if (property?.PropertyType == typeof(OperatorType) && property.GetMethod != null)
        {
            try
            {
                var uninitialized = RuntimeHelpers.GetUninitializedObject(operatorClrType);
                var value = property.GetValue(uninitialized);
                if (value is OperatorType resolvedType)
                {
                    return resolvedType;
                }
            }
            catch
            {
                // Fall back to class-name parsing.
            }
        }

        const string suffix = "Operator";
        var className = operatorClrType.Name;
        if (className.EndsWith(suffix, StringComparison.Ordinal))
        {
            className = className[..^suffix.Length];
        }

        return Enum.TryParse(className, out OperatorType parsed) ? parsed : default;
    }

    private static Operator CreateRoiOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("roi", OperatorType.RoiTransform, 0, 0);

        if (parameters != null)
        {
            foreach (var (name, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, "string", value));
            }
        }

        return op;
    }

    private static Operator CreateCaliperOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("caliper", OperatorType.CaliperTool, 0, 0);

        if (parameters != null)
        {
            foreach (var (name, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, "string", value));
            }
        }

        return op;
    }
}
