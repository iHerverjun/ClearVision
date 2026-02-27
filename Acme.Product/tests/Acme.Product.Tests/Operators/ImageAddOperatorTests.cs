using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class ImageAddOperatorTests
{
    private readonly ImageAddOperator _operator;

    public ImageAddOperatorTests()
    {
        _operator = new ImageAddOperator(Substitute.For<ILogger<ImageAddOperator>>());
    }

    [Fact]
    public async Task ExecuteAsync_WithSameSizeImages_ShouldReturnSuccess()
    {
        var op = new Operator("测试", OperatorType.ImageAdd, 0, 0);
        using var image1 = TestHelpers.CreateTestImage(100, 80);
        using var image2 = TestHelpers.CreateTestImage(100, 80);

        var inputs = new Dictionary<string, object>
        {
            { "Image1", image1 },
            { "Image2", image2 }
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Image");
        result.OutputData.Should().ContainKey("SizeMismatchPolicy");
    }

    [Fact]
    public async Task ExecuteAsync_WithMismatchedSizeAndFailPolicy_ShouldReturnFailure()
    {
        var op = new Operator("测试", OperatorType.ImageAdd, 0, 0);
        op.AddParameter(new Parameter(Guid.NewGuid(), "SizeMismatchPolicy", "SizeMismatchPolicy", "", "enum", "Fail"));

        using var image1 = TestHelpers.CreateTestImage(120, 100);
        using var image2 = TestHelpers.CreateTestImage(60, 50);

        var inputs = new Dictionary<string, object>
        {
            { "Image1", image1 },
            { "Image2", image2 }
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Image size mismatch");
    }

    [Fact]
    public async Task ExecuteAsync_WithAnchorPastePolicy_ShouldReturnSuccess()
    {
        var op = new Operator("测试", OperatorType.ImageAdd, 0, 0);
        op.AddParameter(new Parameter(Guid.NewGuid(), "SizeMismatchPolicy", "SizeMismatchPolicy", "", "enum", "AnchorPaste"));
        op.AddParameter(new Parameter(Guid.NewGuid(), "OffsetX", "OffsetX", "", "int", 10));
        op.AddParameter(new Parameter(Guid.NewGuid(), "OffsetY", "OffsetY", "", "int", 5));

        using var image1 = TestHelpers.CreateTestImage(160, 120);
        using var image2 = TestHelpers.CreateTestImage(60, 40);

        var inputs = new Dictionary<string, object>
        {
            { "Image1", image1 },
            { "Image2", image2 }
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("PolicyMessage");
        result.OutputData!["SizeMismatchPolicy"].Should().Be("AnchorPaste");
    }
}
