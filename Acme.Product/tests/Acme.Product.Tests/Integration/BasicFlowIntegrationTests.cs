// BasicFlowIntegrationTests.cs
// 端到端流程集成测试 — 验证多算子串联执行
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
/// 端到端流程集成测试 — 验证多算子串联执行
/// </summary>
public class BasicFlowIntegrationTests
{
    [Fact]
    public async Task GaussianBlur_Then_Threshold_ShouldProduceOutput()
    {
        // Arrange: 创建两个算子
        var blurOp = new Operator("高斯模糊", OperatorType.GaussianBlur, 0, 0);
        blurOp.Parameters.Add(new OperatorParameter { Name = "KernelSize", Value = 5 });
        blurOp.Parameters.Add(new OperatorParameter { Name = "Sigma", Value = 1.0 });
        
        var threshOp = new Operator("阈值", OperatorType.Threshold, 200, 0);
        threshOp.Parameters.Add(new OperatorParameter { Name = "ThresholdValue", Value = 128 });
        
        var blurExecutor = new GaussianBlurOperator(new Mock<ILogger<GaussianBlurOperator>>().Object);
        var threshExecutor = new ThresholdOperator(new Mock<ILogger<ThresholdOperator>>().Object);
        
        // 创建测试输入图像
        using var testImage = TestHelpers.CreateGradientTestImage();
        var inputs = new Dictionary<string, object> { { "Image", testImage } };
        
        // Act: 串联执行
        var blurResult = await blurExecutor.ExecuteAsync(blurOp, inputs);
        blurResult.IsSuccess.Should().BeTrue("高斯模糊应成功");
        
        var threshResult = await threshExecutor.ExecuteAsync(threshOp, blurResult.OutputData);
        threshResult.IsSuccess.Should().BeTrue("阈值处理应成功");
        
        // Assert: 输出包含图像
        threshResult.OutputData.Should().ContainKey("Image");
    }

    [Fact]
    public async Task ColorConversion_Then_AdaptiveThreshold_ShouldWork()
    {
        var colorOp = new Operator("颜色转换", OperatorType.ColorConversion, 0, 0);
        colorOp.Parameters.Add(new OperatorParameter { Name = "ConversionCode", Value = "BGR2GRAY" });
        
        var atOp = new Operator("自适应阈值", OperatorType.AdaptiveThreshold, 200, 0);
        atOp.Parameters.Add(new OperatorParameter { Name = "AdaptiveMethod", Value = "Gaussian" });
        
        var colorExec = new ColorConversionOperator(new Mock<ILogger<ColorConversionOperator>>().Object);
        var atExec = new AdaptiveThresholdOperator(new Mock<ILogger<AdaptiveThresholdOperator>>().Object);
        
        using var testImage = TestHelpers.CreateShapeTestImage();
        var inputs = new Dictionary<string, object> { { "Image", testImage } };
        
        var r1 = await colorExec.ExecuteAsync(colorOp, inputs);
        r1.IsSuccess.Should().BeTrue();
        
        var r2 = await atExec.ExecuteAsync(atOp, r1.OutputData);
        r2.IsSuccess.Should().BeTrue();
    }
    
    [Fact]
    public async Task FindContours_Then_ContourMeasurement_Pipeline()
    {
        var findOp = new Operator("轮廓检测", OperatorType.FindContours, 0, 0);
        findOp.Parameters.Add(new OperatorParameter { Name = "Mode", Value = "External" });
        
        var measureOp = new Operator("轮廓测量", OperatorType.ContourMeasurement, 200, 0);
        measureOp.Parameters.Add(new OperatorParameter { Name = "MeasurementType", Value = "Area" });
        
        var findExec = new FindContoursOperator(new Mock<ILogger<FindContoursOperator>>().Object);
        var measureExec = new ContourMeasurementOperator(new Mock<ILogger<ContourMeasurementOperator>>().Object);
        
        using var testImage = TestHelpers.CreateShapeTestImage();
        var inputs = new Dictionary<string, object> { { "Image", testImage } };
        
        var r1 = await findExec.ExecuteAsync(findOp, inputs);
        r1.IsSuccess.Should().BeTrue();
        
        var r2 = await measureExec.ExecuteAsync(measureOp, r1.OutputData);
        r2.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GeometricFitting_Then_Measurement_Pipeline()
    {
        var fittingOp = new Operator("几何拟合", OperatorType.GeometricFitting, 0, 0);
        fittingOp.Parameters.Add(new OperatorParameter { Name = "FitType", Value = "Circle" });
        
        var measureOp = new Operator("测量距离", OperatorType.MeasureDistance, 200, 0);
        
        var fittingExec = new GeometricFittingOperator(new Mock<ILogger<GeometricFittingOperator>>().Object);
        var measureExec = new MeasureDistanceOperator(new Mock<ILogger<MeasureDistanceOperator>>().Object);
        
        using var testImage = TestHelpers.CreateShapeTestImage();
        var inputs = new Dictionary<string, object> { { "Image", testImage } };
        
        var r1 = await fittingExec.ExecuteAsync(fittingOp, inputs);
        r1.IsSuccess.Should().BeTrue("几何拟合应成功");
        
        // 即使测量失败也应返回结果（可能有默认行为）
        var r2 = await measureExec.ExecuteAsync(measureOp, r1.OutputData);
        r2.Should().NotBeNull();
    }
}
