using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class MorphologicalOperationOperatorTests
{
    private readonly MorphologicalOperationOperator _operator =
        new(Substitute.For<ILogger<MorphologicalOperationOperator>>());

    [Fact]
    public void OperatorType_ShouldBeMorphologicalOperation()
    {
        _operator.OperatorType.Should().Be(OperatorType.MorphologicalOperation);
    }

    [Fact]
    public async Task ExecuteAsync_WithCloseOperation_ShouldReturnImageAndMetadata()
    {
        var op = CreateOperator();
        op.AddParameter(TestHelpers.CreateParameter("Operation", "Close", "enum"));

        using var image = CreateMorphologyImage();
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        using var outputImage = result.OutputData!["Image"].Should().BeOfType<ImageWrapper>().Subject;
        outputImage.Width.Should().Be(120);
        outputImage.Height.Should().Be(120);
        result.OutputData["Operation"].Should().Be("Close");
        result.OutputData["KernelSize"].Should().Be("3x3");
    }

    [Fact]
    public async Task ExecuteAsync_WithTopHatAndEllipseKernel_ShouldHighlightSmallFeatureAndSuppressLargeRegion()
    {
        var op = CreateOperator();
        op.AddParameter(TestHelpers.CreateParameter("Operation", "TopHat", "enum"));
        op.AddParameter(TestHelpers.CreateParameter("KernelShape", "Ellipse", "enum"));
        op.AddParameter(TestHelpers.CreateParameter("KernelWidth", 9, "int"));
        op.AddParameter(TestHelpers.CreateParameter("KernelHeight", 9, "int"));

        using var image = CreateTopHatSourceImage();
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        using var outputImage = result.OutputData!["Image"].Should().BeOfType<ImageWrapper>().Subject;
        outputImage.Width.Should().Be(41);
        outputImage.Height.Should().Be(41);
        outputImage.MatReadOnly.At<byte>(30, 30).Should().BeGreaterThan((byte)200);
        outputImage.MatReadOnly.At<byte>(12, 12).Should().Be(0);
        outputImage.MatReadOnly.At<byte>(0, 0).Should().Be(0);
        result.OutputData!["Operation"].Should().Be("TopHat");
        result.OutputData["KernelShape"].Should().Be("Ellipse");
    }

    [Fact]
    public void ValidateParameters_WithUnsupportedOperation_ShouldReturnInvalid()
    {
        var op = CreateOperator();
        op.AddParameter(TestHelpers.CreateParameter("Operation", "Skeletonize", "string"));

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    private static Operator CreateOperator()
    {
        return new Operator("MorphologicalOperation", OperatorType.MorphologicalOperation, 0, 0);
    }

    private static ImageWrapper CreateMorphologyImage()
    {
        var mat = new Mat(120, 120, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(20, 20, 80, 60), new Scalar(90, 90, 90), -1);
        Cv2.Circle(mat, new Point(60, 50), 8, Scalar.White, -1);
        Cv2.Circle(mat, new Point(90, 80), 6, Scalar.Black, -1);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateTopHatSourceImage()
    {
        var mat = new Mat(41, 41, MatType.CV_8UC1, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(4, 4, 16, 16), new Scalar(120), -1);
        Cv2.Circle(mat, new Point(30, 30), 2, new Scalar(255), -1);
        return new ImageWrapper(mat);
    }
}
