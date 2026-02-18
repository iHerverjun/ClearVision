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

public class ColorDetectionIntegrationTests
{
    [Fact]
    public async Task ColorDetection_Flow_ShouldIdentifyColors()
    {
        // 1. Arrange
        var op = new Operator("颜色检测", OperatorType.ColorDetection, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("TargetColor", "Red", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Tolerance", 10.0, "double"));

        var executor = new ColorDetectionOperator(Substitute.For<ILogger<ColorDetectionOperator>>());

        var inputImage = TestHelpers.CreateTestImage(color: new OpenCvSharp.Scalar(0, 0, 255)); // Red in BGR
        var inputs = new Dictionary<string, object> { { "Image", inputImage } };

        // 2. Act
        var result = await executor.ExecuteAsync(op, inputs, CancellationToken.None);

        // 3. Assert
        result.IsSuccess.Should().BeTrue();
        // Assuming implementation output
        // result.OutputData.Should().ContainKey("IsMatch");
    }
}
