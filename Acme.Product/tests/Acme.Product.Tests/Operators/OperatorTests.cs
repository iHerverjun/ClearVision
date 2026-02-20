// OperatorTests.cs
// 阈值二值化算子测试
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Cameras;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

/// <summary>
/// 图像采集算子测试
/// </summary>
public class ImageAcquisitionOperatorTests
{
    private readonly ImageAcquisitionOperator _operator;

    public ImageAcquisitionOperatorTests()
    {
        _operator = new ImageAcquisitionOperator(
            Substitute.For<ILogger<ImageAcquisitionOperator>>(),
            Substitute.For<ICameraManager>());
    }

    [Fact]
    public void OperatorType_ShouldBeImageAcquisition()
    {
        // Assert
        _operator.OperatorType.Should().Be(OperatorType.ImageAcquisition);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        // Arrange
        var op = CreateTestOperator();

        // Act
        var result = await _operator.ExecuteAsync(op, null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("未提供图像数据或有效的采集设置");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyInputs_ShouldReturnFailure()
    {
        // Arrange
        var op = CreateTestOperator();
        var inputs = new Dictionary<string, object>();

        // Act
        var result = await _operator.ExecuteAsync(op, inputs);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("未提供图像数据或有效的采集设置");
    }

    [Fact]
    public void ValidateParameters_WithValidOperator_ShouldReturnValid()
    {
        // Arrange
        var op = CreateTestOperator();

        // Act
        var result = _operator.ValidateParameters(op);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    private static Operator CreateTestOperator()
    {
        return new Operator("测试算子", OperatorType.ImageAcquisition, 0, 0);
    }
}

/// <summary>
/// 高斯模糊算子测试
/// </summary>
public class GaussianBlurOperatorTests
{
    private readonly GaussianBlurOperator _operator;

    public GaussianBlurOperatorTests()
    {
        _operator = new GaussianBlurOperator(Substitute.For<ILogger<GaussianBlurOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeFiltering()
    {
        // Assert
        _operator.OperatorType.Should().Be(OperatorType.Filtering);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutImageInput_ShouldReturnFailure()
    {
        // Arrange
        var op = CreateTestOperator();
        var inputs = new Dictionary<string, object>();

        // Act
        var result = await _operator.ExecuteAsync(op, inputs);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("未提供输入图像");
    }

    [Fact]
    public void ValidateParameters_WithValidKernelSize_ShouldReturnValid()
    {
        // Arrange
        var op = CreateTestOperator();
        op.AddParameter(TestHelpers.CreateParameter(
            "KernelSize", "核大小", "int", 5, 1, 31, true));

        // Act
        var result = _operator.ValidateParameters(op);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_WithInvalidKernelSize_ShouldReturnInvalid()
    {
        // Arrange
        var op = CreateTestOperator();
        op.AddParameter(TestHelpers.CreateParameter(
            "KernelSize", "核大小", "int", 50, 1, 31, true));

        // Act
        var result = _operator.ValidateParameters(op);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("核大小必须在 1-31 之间");
    }

    private static Operator CreateTestOperator()
    {
        return new Operator("高斯模糊", OperatorType.Filtering, 0, 0);
    }
}

/// <summary>
/// Canny边缘检测算子测试
/// </summary>
public class CannyEdgeOperatorTests
{
    private readonly CannyEdgeOperator _operator;

    public CannyEdgeOperatorTests()
    {
        _operator = new CannyEdgeOperator(Substitute.For<ILogger<CannyEdgeOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeEdgeDetection()
    {
        // Assert
        _operator.OperatorType.Should().Be(OperatorType.EdgeDetection);
    }

    [Fact]
    public void ValidateParameters_WithValidThresholds_ShouldReturnValid()
    {
        // Arrange
        var op = CreateTestOperator();
        op.AddParameter(TestHelpers.CreateParameter(
            "Threshold1", "低阈值", "double", 50.0, 0.0, 255.0, true));
        op.AddParameter(TestHelpers.CreateParameter(
            "Threshold2", "高阈值", "double", 150.0, 0.0, 255.0, true));

        // Act
        var result = _operator.ValidateParameters(op);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_WithInvalidThreshold1_ShouldReturnInvalid()
    {
        // Arrange
        var op = CreateTestOperator();
        op.AddParameter(TestHelpers.CreateParameter(
            "Threshold1", "低阈值", "double", 300.0, 0.0, 255.0, true));

        // Act
        var result = _operator.ValidateParameters(op);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("阈值1必须在 0-255 之间");
    }

    private static Operator CreateTestOperator()
    {
        return new Operator("Canny边缘检测", OperatorType.EdgeDetection, 0, 0);
    }
}

/// <summary>
/// 阈值二值化算子测试
/// </summary>
public class ThresholdOperatorTests
{
    private readonly ThresholdOperator _operator;

    public ThresholdOperatorTests()
    {
        _operator = new ThresholdOperator(Substitute.For<ILogger<ThresholdOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeThresholding()
    {
        // Assert
        _operator.OperatorType.Should().Be(OperatorType.Thresholding);
    }

    [Fact]
    public void ValidateParameters_WithValidThreshold_ShouldReturnValid()
    {
        // Arrange
        var op = CreateTestOperator();
        op.AddParameter(TestHelpers.CreateParameter(
            "Threshold", "阈值", "double", 127.0, 0.0, 255.0, true));

        // Act
        var result = _operator.ValidateParameters(op);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_WithNegativeThreshold_ShouldReturnInvalid()
    {
        // Arrange
        var op = CreateTestOperator();
        op.AddParameter(TestHelpers.CreateParameter(
            "Threshold", "阈值", "double", -10.0, 0.0, 255.0, true));

        // Act
        var result = _operator.ValidateParameters(op);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("阈值必须在 0-255 之间");
    }

    private static Operator CreateTestOperator()
    {
        return new Operator("阈值二值化", OperatorType.Thresholding, 0, 0);
    }
}
