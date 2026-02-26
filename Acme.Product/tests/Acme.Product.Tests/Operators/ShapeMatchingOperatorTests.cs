using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class ShapeMatchingOperatorTests
{
    private readonly ShapeMatchingOperator _operator;

    public ShapeMatchingOperatorTests()
    {
        _operator = new ShapeMatchingOperator(Substitute.For<ILogger<ShapeMatchingOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeShapeMatching()
    {
        _operator.OperatorType.Should().Be(OperatorType.ShapeMatching);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("Shape", OperatorType.ShapeMatching, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyInputs_ShouldReturnFailure()
    {
        var op = new Operator("Shape", OperatorType.ShapeMatching, 0, 0);
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>());
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithNumLevels_ShouldExposeLevelsUsed()
    {
        var op = new Operator("Shape", OperatorType.ShapeMatching, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MinScore", "MinScore", "double", 0.6, 0.1, 1.0, true));
        op.AddParameter(TestHelpers.CreateParameter("AngleStart", "AngleStart", "double", 0.0, -180.0, 180.0, true));
        op.AddParameter(TestHelpers.CreateParameter("AngleExtent", "AngleExtent", "double", 0.0, 0.0, 360.0, true));
        op.AddParameter(TestHelpers.CreateParameter("AngleStep", "AngleStep", "double", 1.0, 0.1, 10.0, true));
        op.AddParameter(TestHelpers.CreateParameter("NumLevels", "NumLevels", "int", 4, 1, 6, true));

        using var template = CreateTemplateImage();
        using var scene = CreateSceneImage(template.MatReadOnly);
        var inputs = new Dictionary<string, object>
        {
            { "Image", scene },
            { "Template", template }
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("NumLevelsUsed");
        result.OutputData!["NumLevelsUsed"].Should().BeOfType<int>();
        ((int)result.OutputData["NumLevelsUsed"]).Should().BeGreaterThan(1);
    }

    private static ImageWrapper CreateTemplateImage()
    {
        var mat = new Mat(40, 40, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(8, 8, 24, 24), Scalar.White, -1);
        Cv2.Circle(mat, new Point(20, 20), 6, Scalar.Black, -1);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateSceneImage(Mat template)
    {
        var mat = new Mat(220, 220, MatType.CV_8UC3, Scalar.Black);
        using (var roi = new Mat(mat, new Rect(90, 70, template.Width, template.Height)))
        {
            template.CopyTo(roi);
        }

        return new ImageWrapper(mat);
    }
}
