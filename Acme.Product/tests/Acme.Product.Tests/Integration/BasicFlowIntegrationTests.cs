using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Tests.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Integration;

public class BasicFlowIntegrationTests
{
    [Fact]
    public async Task Process_SimpleBlurAndThreshold_ShouldSucceed()
    {
        // 1. Arrange - Operators
        var blurOp = new Operator("高斯模糊", OperatorType.GaussianBlur, 0, 0);
        blurOp.AddParameter(TestHelpers.CreateParameter("KernelSize", 5, "int"));
        blurOp.AddParameter(TestHelpers.CreateParameter("Sigma", 1.0, "double"));

        var threshOp = new Operator("阈值", OperatorType.Thresholding, 200, 0);
        threshOp.AddParameter(TestHelpers.CreateParameter("Threshold", 128.0, "double"));

        // 2. Arrange - Executors
        var blurExecutor = new GaussianBlurOperator(Substitute.For<ILogger<GaussianBlurOperator>>());
        var threshExecutor = new ThresholdOperator(Substitute.For<ILogger<ThresholdOperator>>());

        // 3. Arrange - Input
        var inputImage = TestHelpers.CreateTestImage();
        var inputs = new Dictionary<string, object> { { "Image", inputImage } };

        // 4. Act - Blur
        var blurResult = await blurExecutor.ExecuteAsync(blurOp, inputs, CancellationToken.None);

        // 5. Assert - Blur
        blurResult.IsSuccess.Should().BeTrue();
        blurResult.OutputData.Should().ContainKey("Image");

        // 6. Act - Threshold
        var threshInputs = new Dictionary<string, object> { { "Image", blurResult.OutputData["Image"] } };
        var threshResult = await threshExecutor.ExecuteAsync(threshOp, threshInputs, CancellationToken.None);

        // 7. Assert - Threshold
        threshResult.IsSuccess.Should().BeTrue();
        threshResult.OutputData.Should().ContainKey("Image");
    }
}
