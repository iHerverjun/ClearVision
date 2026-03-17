using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public sealed class SemanticSegmentationOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeSemanticSegmentation()
    {
        var sut = new SemanticSegmentationOperator(Substitute.For<ILogger<SemanticSegmentationOperator>>());
        sut.OperatorType.Should().Be(OperatorType.SemanticSegmentation);
    }

    [Fact]
    public void ValidateParameters_WithInvalidInputSize_ShouldReturnInvalid()
    {
        var sut = new SemanticSegmentationOperator(Substitute.For<ILogger<SemanticSegmentationOperator>>());
        var op = CreateOperator(inputSize: "bad-size");

        var result = sut.ValidateParameters(op);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithIdentitySegmentationModel_ShouldReturnSegmentationOutputs()
    {
        var sut = new SemanticSegmentationOperator(Substitute.For<ILogger<SemanticSegmentationOperator>>());
        var op = CreateOperator();

        using var image = new ImageWrapper(Cv2.ImRead(ResolveTestDataPath(@"model_test_suite\identity_2x2\input.png"), ImreadModes.Color));
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData.Should().NotBeNull();

        var segmentationMap = result.OutputData!["SegmentationMap"].Should().BeOfType<ImageWrapper>().Subject;
        var coloredMap = result.OutputData["ColoredMap"].Should().BeOfType<ImageWrapper>().Subject;
        var classMasks = result.OutputData["ClassMasks"].Should().BeOfType<Dictionary<string, object>>().Subject;
        var presentClasses = result.OutputData["PresentClasses"].Should().BeOfType<string[]>().Subject;

        segmentationMap.MatReadOnly.Type().Should().Be(MatType.CV_8UC1);
        coloredMap.MatReadOnly.Type().Should().Be(MatType.CV_8UC3);
        result.OutputData["ClassCount"].Should().Be(3);
        presentClasses.Should().BeEquivalentTo(["red", "green", "blue"]);
        classMasks.Keys.Should().Contain(["red", "green", "blue"]);

        var indexer = segmentationMap.MatReadOnly.GetGenericIndexer<byte>();
        indexer[0, 0].Should().Be(0);
        indexer[0, 1].Should().Be(1);
        indexer[1, 0].Should().Be(2);
        indexer[1, 1].Should().Be(0);

        segmentationMap.Dispose();
        coloredMap.Dispose();
        foreach (var mask in classMasks.Values.OfType<ImageWrapper>())
        {
            mask.Dispose();
        }
    }

    private static Operator CreateOperator(string inputSize = "2,2")
    {
        var op = new Operator("segmentation", OperatorType.SemanticSegmentation, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("ModelPath", ResolveTestDataPath(@"model_test_suite\identity_2x2\identity_2x2.onnx"), "file"));
        op.AddParameter(TestHelpers.CreateParameter("InputSize", inputSize, "string"));
        op.AddParameter(TestHelpers.CreateParameter("NumClasses", 3, "int"));
        op.AddParameter(TestHelpers.CreateParameter("ClassNames", "[\"red\",\"green\",\"blue\"]", "string"));
        op.AddParameter(TestHelpers.CreateParameter("ExecutionProvider", "cpu", "string"));
        op.AddParameter(TestHelpers.CreateParameter("ScaleToUnitRange", true, "bool"));
        op.AddParameter(TestHelpers.CreateParameter("ChannelOrder", "RGB", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Mean", "0,0,0", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Std", "1,1,1", "string"));
        return op;
    }

    private static string ResolveTestDataPath(string relativePath)
    {
        var baseDir = AppContext.BaseDirectory;
        var candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "TestData", relativePath));
        if (File.Exists(candidate))
        {
            return candidate;
        }

        var dir = new DirectoryInfo(baseDir);
        while (dir != null && !dir.Name.Equals("Acme.Product", StringComparison.OrdinalIgnoreCase))
        {
            dir = dir.Parent;
        }

        if (dir != null)
        {
            candidate = Path.Combine(dir.FullName, "tests", "TestData", relativePath);
        }

        return candidate;
    }
}
