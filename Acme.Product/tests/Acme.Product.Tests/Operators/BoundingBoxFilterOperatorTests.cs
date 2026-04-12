using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;
using System.Collections;
using System.Reflection;
using Xunit;

namespace Acme.Product.Tests.Operators;

public class BoundingBoxFilterOperatorTests
{
    private readonly BoundingBoxFilterOperator _operator;

    public BoundingBoxFilterOperatorTests()
    {
        _operator = new BoundingBoxFilterOperator(Substitute.For<ILogger<BoundingBoxFilterOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeBoxFilter()
    {
        Assert.Equal(OperatorType.BoxFilter, _operator.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithClassFilter_ShouldReturnTargetClassOnly()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "FilterMode", "Class" },
            { "TargetClasses", "defect" }
        });

        var detections = new DetectionList(new[]
        {
            new DetectionResult("defect", 0.9f, 10, 10, 20, 20),
            new DetectionResult("ok", 0.8f, 40, 40, 20, 20)
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Detections", detections } });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.Equal(1, (int)result.OutputData!["Count"]);
    }

    [Fact]
    public void ValidateParameters_WithInvalidMode_ShouldReturnInvalid()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "FilterMode", "Invalid" } });

        var validation = _operator.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidMode_ShouldFailAtRuntime()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "FilterMode", "Invalid" } });
        var detections = new DetectionList(new[]
        {
            new DetectionResult("defect", 0.9f, 10, 10, 20, 20)
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Detections", detections } });

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("FilterMode");
    }

    [Fact]
    public void ValidateParameters_WithMinAreaGreaterThanMaxArea_ShouldReturnInvalid()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "MinArea", 100 },
            { "MaxArea", 10 }
        });

        var validation = _operator.ValidateParameters(op);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(error => error.Contains("MinArea", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_WithRegionFilter_ShouldUseDoubleCenterAndRightBottomExclusiveBoundary()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "FilterMode", "Region" },
            { "RegionX", 10 },
            { "RegionY", 10 },
            { "RegionW", 20 },
            { "RegionH", 20 }
        });

        var detections = new DetectionList(new[]
        {
            new DetectionResult("in-left-top", 0.9f, 9.6f, 9.6f, 1f, 1f),   // center (10.1, 10.1), include
            new DetectionResult("in-near-right", 0.9f, 28.6f, 10f, 1f, 1f), // center (29.1, 10.5), include
            new DetectionResult("out-right", 0.9f, 29.5f, 10f, 1f, 1f),      // center (30.0, 10.5), exclude
            new DetectionResult("out-bottom", 0.9f, 10f, 29.5f, 1f, 1f)      // center (10.5, 30.0), exclude
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Detections", detections } });

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().NotBeNull();
        result.OutputData!["Count"].Should().Be(2);
        var kept = result.OutputData["Detections"].Should().BeOfType<DetectionList>().Subject;
        kept.Detections.Select(d => d.Label).Should().Contain(new[] { "in-left-top", "in-near-right" });
        kept.Detections.Select(d => d.Label).Should().NotContain(new[] { "out-right", "out-bottom" });
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyNonGenericCollection_ShouldReturnSuccessAndEmptyResult()
    {
        var op = CreateOperator(new Dictionary<string, object>());
        var detections = new ArrayList();

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Detections", detections } });

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().NotBeNull();
        result.OutputData!["Count"].Should().Be(0);
        result.OutputData["Detections"].Should().BeOfType<DetectionList>().Subject.Count.Should().Be(0);
    }

    [Fact]
    public void BuildVisualizationDetections_ShouldApplyPreviewNmsForImageOutput()
    {
        var method = typeof(BoundingBoxFilterOperator).GetMethod(
            "BuildVisualizationDetections",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        var detections = new List<DetectionResult>
        {
            new("wire", 0.95f, 10, 10, 40, 40),
            new("wire", 0.85f, 12, 12, 40, 40),
            new("wire", 0.24f, 60, 60, 20, 20),
            new("wire", 0.90f, 100, 100, 20, 20)
        };

        var result = method!.Invoke(null, new object?[] { detections, 0.0d });

        result.Should().BeAssignableTo<IEnumerable<DetectionResult>>();
        result.As<IEnumerable<DetectionResult>>().Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_WithImageAndNoKeptBoxes_ShouldStillRenderIncomingPreviewDetections()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "FilterMode", "Score" },
            { "MinScore", 0.99 }
        });

        var detections = new DetectionList(new[]
        {
            new DetectionResult("wire", 0.9f, 10, 10, 20, 20)
        });

        using var sourceMat = new Mat(80, 80, MatType.CV_8UC3, Scalar.All(0));
        var inputImage = new ImageWrapper(sourceMat.Clone());
        var sourceBytes = inputImage.GetBytes();

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            { "Detections", detections },
            { "Image", inputImage }
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().NotBeNull();
        result.OutputData!["Count"].Should().Be(0);
        result.OutputData["ReceivedCount"].Should().Be(1);
        result.OutputData["ReceivedVisualizationCount"].Should().Be(1);
        result.OutputData["Image"].Should().BeOfType<ImageWrapper>();

        var previewImage = (ImageWrapper)result.OutputData["Image"];
        previewImage.GetBytes().Should().NotEqual(sourceBytes);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutImage_ShouldNotEmitVisualizationMetadata()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "FilterMode", "Score" },
            { "MinScore", 0.5 }
        });

        var detections = new DetectionList(new[]
        {
            new DetectionResult("wire", 0.9f, 10, 10, 20, 20)
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            { "Detections", detections }
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().NotBeNull();
        result.OutputData!.ContainsKey("ReceivedVisualizationCount").Should().BeFalse();
        result.OutputData.ContainsKey("VisualizationCount").Should().BeFalse();
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("BoundingBoxFilter", OperatorType.BoxFilter, 0, 0);

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
