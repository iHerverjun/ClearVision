// ColorDetectionIntegrationTests.cs
// 颜色检测算子集成测试
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Integration;

/// <summary>
/// 颜色检测算子集成测试
/// </summary>
public class ColorDetectionIntegrationTests
{
    [Fact]
    public async Task ColorDetection_AverageMode_ShouldReturnColorValues()
    {
        var op = new Operator("颜色检测", OperatorType.ColorDetection, 0, 0);
        op.Parameters.Add(new OperatorParameter { Name = "ColorSpace", Value = "HSV" });
        op.Parameters.Add(new OperatorParameter { Name = "AnalysisMode", Value = "Average" });
        
        var executor = new ColorDetectionOperator(new Mock<ILogger<ColorDetectionOperator>>().Object);
        
        // 纯红色图像
        using var redImage = TestHelpers.CreateTestImage(color: new Scalar(0, 0, 255));
        var inputs = new Dictionary<string, object> { { "Image", redImage } };
        
        var result = await executor.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Image");
    }

    [Fact]
    public async Task ColorDetection_DominantMode_ShouldExtractMainColors()
    {
        var op = new Operator("颜色检测", OperatorType.ColorDetection, 0, 0);
        op.Parameters.Add(new OperatorParameter { Name = "ColorSpace", Value = "BGR" });
        op.Parameters.Add(new OperatorParameter { Name = "AnalysisMode", Value = "Dominant" });
        op.Parameters.Add(new OperatorParameter { Name = "DominantK", Value = 3 });
        
        var executor = new ColorDetectionOperator(new Mock<ILogger<ColorDetectionOperator>>().Object);
        
        using var testImage = TestHelpers.CreateShapeTestImage();
        var inputs = new Dictionary<string, object> { { "Image", testImage } };
        
        var result = await executor.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("DominantColors");
    }

    [Fact]
    public async Task ColorDetection_RangeMode_ShouldDetectColorRange()
    {
        var op = new Operator("颜色检测", OperatorType.ColorDetection, 0, 0);
        op.Parameters.Add(new OperatorParameter { Name = "ColorSpace", Value = "HSV" });
        op.Parameters.Add(new OperatorParameter { Name = "AnalysisMode", Value = "Range" });
        op.Parameters.Add(new OperatorParameter { Name = "HueLow", Value = 0 });
        op.Parameters.Add(new OperatorParameter { Name = "HueHigh", Value = 20 });
        op.Parameters.Add(new OperatorParameter { Name = "SatLow", Value = 50 });
        op.Parameters.Add(new OperatorParameter { Name = "ValLow", Value = 50 });
        
        var executor = new ColorDetectionOperator(new Mock<ILogger<ColorDetectionOperator>>().Object);
        
        using var testImage = TestHelpers.CreateTestImage(color: new Scalar(0, 0, 255));
        var inputs = new Dictionary<string, object> { { "Image", testImage } };
        
        var result = await executor.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Percentage");
    }
}
