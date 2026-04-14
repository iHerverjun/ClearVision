using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class TryCatchOperatorTests
{
    private readonly TryCatchOperator _operator;

    public TryCatchOperatorTests()
    {
        _operator = new TryCatchOperator(Substitute.For<ILogger<TryCatchOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeTryCatch()
    {
        _operator.OperatorType.Should().Be(OperatorType.TryCatch);
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("test", OperatorType.TryCatch, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithTextInput_ShouldPassThroughTryBranch()
    {
        var op = new Operator("test", OperatorType.TryCatch, 0, 0);
        var inputs = new Dictionary<string, object>
        {
            ["Input"] = "payload"
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().NotBeNull();
        result.OutputData!["Try"].Should().Be("payload");
        result.OutputData["HasError"].Should().Be(false);
        result.OutputData["Error"].Should().Be(string.Empty);
    }

    [Fact]
    public async Task ExecuteAsync_WithImageInput_ShouldKeepTryOutputUsable()
    {
        var op = new Operator("test", OperatorType.TryCatch, 0, 0);
        var image = TestHelpers.CreateTestImage();

        try
        {
            var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
            {
                ["Input"] = image
            });

            result.IsSuccess.Should().BeTrue();
            var outputImage = result.OutputData!["Try"].Should().BeOfType<ImageWrapper>().Subject;
            outputImage.GetBytes().Length.Should().BeGreaterThan(0);
        }
        finally
        {
            if (image.RefCount > 0)
            {
                image.Release();
            }
        }
    }
}
