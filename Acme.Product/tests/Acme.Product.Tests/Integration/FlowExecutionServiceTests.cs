// FlowExecutionServiceTests.cs
// FlowExecutionService 集成测试
// 作者：蘅芜君

using Acme.Product.Core.Cameras;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.Services;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Integration;

/// <summary>
/// FlowExecutionService 集成测试
/// Sprint 5: S5-002 实现
/// </summary>
public class FlowExecutionServiceIntegrationTests
{
    private readonly IFlowExecutionService _flowExecutionService;
    private readonly ILogger<FlowExecutionService> _logger;

    public FlowExecutionServiceIntegrationTests()
    {
        _logger = Substitute.For<ILogger<FlowExecutionService>>();

        // 创建所有算子执行器
        var executors = new List<IOperatorExecutor>
        {
            new ImageAcquisitionOperator(
                Substitute.For<ILogger<ImageAcquisitionOperator>>(),
                Substitute.For<ICameraManager>()),
            new GaussianBlurOperator(Substitute.For<ILogger<GaussianBlurOperator>>()),
            new CannyEdgeOperator(Substitute.For<ILogger<CannyEdgeOperator>>()),
            new ThresholdOperator(Substitute.For<ILogger<ThresholdOperator>>()),
            new MorphologyOperator(Substitute.For<ILogger<MorphologyOperator>>()),
            new RoiManagerOperator(Substitute.For<ILogger<RoiManagerOperator>>()),
            new BlobDetectionOperator(Substitute.For<ILogger<BlobDetectionOperator>>()),
            new FindContoursOperator(Substitute.For<ILogger<FindContoursOperator>>()),
            new ResultOutputOperator(Substitute.For<ILogger<ResultOutputOperator>>())
        };

        _flowExecutionService = new FlowExecutionService(
            executors,
            _logger,
            Substitute.For<IVariableContext>());
    }

    #region 3算子顺序执行测试

    [Fact]
    public async Task ExecuteFlowAsync_3OperatorLinearFlow_ShouldExecuteSuccessfully()
    {
        // Arrange - 创建 采集→高斯模糊→边缘检测 流程
        var (flow, acquisitionOp, blurOp, cannyOp) = Create3OperatorLinearFlow();
        var testImage = CreateTestImageBytes();
        var inputData = new Dictionary<string, object>
        {
            { "Image", testImage }
        };

        // Act
        var result = await _flowExecutionService.ExecuteFlowAsync(flow, inputData);

        // Assert
        if (!result.IsSuccess)
        {
            // 输出详细的错误信息以便调试
            var errorDetails = string.Join("\n", result.OperatorResults
                .Where(r => !r.IsSuccess)
                .Select(r => $"  - {r.OperatorName}: {r.ErrorMessage}"));
            Assert.Fail($"流程执行失败: {result.ErrorMessage}\n算子错误:\n{errorDetails}");
        }
        result.OperatorResults.Should().HaveCount(3);
        result.OperatorResults.All(r => r.IsSuccess).Should().BeTrue();
        result.ExecutionTimeMs.Should().BeGreaterOrEqualTo(0);

        // 验证每个算子都执行了
        result.OperatorResults[0].OperatorName.Should().Be("图像采集");
        result.OperatorResults[1].OperatorName.Should().Be("高斯模糊");
        result.OperatorResults[2].OperatorName.Should().Be("边缘检测");
    }

    [Fact]
    public async Task ExecuteFlowAsync_3OperatorFlow_ShouldPassDataBetweenOperators()
    {
        // Arrange
        var (flow, acquisitionOp, blurOp, cannyOp) = Create3OperatorLinearFlow();
        var testImage = CreateTestImageBytes();
        var inputData = new Dictionary<string, object>
        {
            { "Image", testImage }
        };

        // Act
        var result = await _flowExecutionService.ExecuteFlowAsync(flow, inputData);

        // Assert
        if (!result.IsSuccess)
        {
            // 输出详细的错误信息以便调试
            var errorDetails = string.Join("\n", result.OperatorResults
                .Where(r => !r.IsSuccess)
                .Select(r => $"  - {r.OperatorName}: {r.ErrorMessage}"));
            Assert.Fail($"流程执行失败: {result.ErrorMessage}\n算子错误:\n{errorDetails}");
        }
        result.IsSuccess.Should().BeTrue();

        // 最后一个算子应该有输出
        var lastResult = result.OperatorResults.Last();
        lastResult.OutputData.Should().NotBeNull();
        lastResult.OutputData.Should().ContainKey("Image");
    }

    #endregion

    #region 5算子复杂流程测试

    [Fact]
    public async Task ExecuteFlowAsync_5OperatorComplexFlow_ShouldExecuteSuccessfully()
    {
        // Arrange - 创建复杂流程：采集→高斯→阈值→形态学→轮廓查找
        var flow = Create5OperatorComplexFlow();
        var testImage = CreateTestImageBytes();
        var inputData = new Dictionary<string, object>
        {
            { "Image", testImage }
        };

        // Act
        var result = await _flowExecutionService.ExecuteFlowAsync(flow, inputData);

        // Assert
        if (!result.IsSuccess)
        {
            var errorDetails = string.Join("\n", result.OperatorResults
                .Where(r => !r.IsSuccess)
                .Select(r => $"  - {r.OperatorName}: {r.ErrorMessage}"));
            Assert.Fail($"流程执行失败: {result.ErrorMessage}\n算子错误:\n{errorDetails}");
        }
        result.OperatorResults.Should().HaveCount(5);
        result.OperatorResults.All(r => r.IsSuccess).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteFlowAsync_ComplexFlow_ShouldMaintainExecutionOrder()
    {
        // Arrange
        var flow = Create5OperatorComplexFlow();
        var testImage = CreateTestImageBytes();
        var inputData = new Dictionary<string, object> { { "Image", testImage } };

        // Act
        var result = await _flowExecutionService.ExecuteFlowAsync(flow, inputData);

        // Assert - 验证执行顺序
        if (!result.IsSuccess)
        {
            var errorDetails = string.Join("\n", result.OperatorResults
                .Where(r => !r.IsSuccess)
                .Select(r => $"  - {r.OperatorName}: {r.ErrorMessage}"));
            Assert.Fail($"流程执行失败: {result.ErrorMessage}\n算子错误:\n{errorDetails}");
        }
        var executionOrder = result.OperatorResults.Select(r => r.OperatorName).ToList();

        executionOrder[0].Should().Be("图像采集");
        executionOrder[1].Should().Be("高斯模糊");
        executionOrder[2].Should().Be("阈值二值化");
        executionOrder[3].Should().Be("形态学操作");
        executionOrder[4].Should().Be("轮廓查找");
    }

    #endregion

    #region 流程验证失败场景测试

    [Fact]
    public void ValidateFlow_MissingImageAcquisitionOperator_ShouldReturnWarning()
    {
        // Arrange - 创建没有图像采集算子的流程
        var flow = new OperatorFlow();
        var blurOp = CreateOperatorWithPorts("高斯模糊", OperatorType.Filtering, 100, 100);
        flow.AddOperator(blurOp);

        // Act
        var validation = _flowExecutionService.ValidateFlow(flow);

        // Assert
        validation.IsValid.Should().BeTrue(); // 没有错误，但有警告
        validation.Warnings.Should().Contain("流程缺少图像采集算子作为输入");
    }

    [Fact]
    public void ValidateFlow_InvalidOperatorParameter_ShouldReturnValidationError()
    {
        // Arrange - 创建带有无效参数的算子
        var flow = new OperatorFlow();
        var acquisitionOp = CreateOperatorWithPorts("图像采集", OperatorType.ImageAcquisition, 0, 0);
        var blurOp = CreateOperatorWithPorts("高斯模糊", OperatorType.Filtering, 100, 100);

        // 添加无效的核大小参数（超过最大值31）
        blurOp.AddParameter(new Parameter(
            Guid.NewGuid(), "KernelSize", "核大小", "", "int", 50, 1, 31, true));

        flow.AddOperator(acquisitionOp);
        flow.AddOperator(blurOp);
        flow.AddConnection(CreateConnection(acquisitionOp, blurOp));

        // Act
        var validation = _flowExecutionService.ValidateFlow(flow);

        // Assert
        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(e => e.Contains("核大小必须在 1-31 之间"));
    }

    [Fact]
    public async Task ExecuteFlowAsync_OperatorExecutionFailure_ShouldStopFlow()
    {
        // Arrange - 创建一个会导致执行失败的流程（无效图像数据）
        var (flow, acquisitionOp, blurOp, cannyOp) = Create3OperatorLinearFlow();
        var invalidImageData = new byte[] { 0x00, 0x01, 0x02 }; // 无效的图像数据
        var inputData = new Dictionary<string, object>
        {
            { "Image", invalidImageData }
        };

        // Act
        var result = await _flowExecutionService.ExecuteFlowAsync(flow, inputData);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        // 第一个算子失败后应该停止执行
        result.OperatorResults.Should().Contain(r => !r.IsSuccess);
    }

    [Fact]
    public void ValidateFlow_EmptyFlow_ShouldReturnValidationError()
    {
        // Arrange
        var flow = new OperatorFlow();

        // Act
        var validation = _flowExecutionService.ValidateFlow(flow);

        // Assert
        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain("流程中没有任何算子");
    }

    #endregion

    #region 算子参数边界值测试

    [Theory]
    [InlineData(1)]    // 最小值
    [InlineData(31)]   // 最大值
    [InlineData(15)]   // 中间值
    public async Task ExecuteFlowAsync_GaussianBlurWithValidKernelSize_ShouldSucceed(int kernelSize)
    {
        // Arrange
        var flow = new OperatorFlow();
        var acquisitionOp = CreateOperatorWithPorts("图像采集", OperatorType.ImageAcquisition, 0, 0);
        var blurOp = CreateOperatorWithPorts("高斯模糊", OperatorType.Filtering, 100, 100);

        blurOp.AddParameter(new Parameter(
            Guid.NewGuid(), "KernelSize", "核大小", "", "int", kernelSize, 1, 31, true));

        flow.AddOperator(acquisitionOp);
        flow.AddOperator(blurOp);
        flow.AddConnection(CreateConnection(acquisitionOp, blurOp));

        var testImage = CreateTestImageBytes();
        var inputData = new Dictionary<string, object> { { "Image", testImage } };

        // Act
        var result = await _flowExecutionService.ExecuteFlowAsync(flow, inputData);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]    // 低于最小值
    [InlineData(32)]   // 高于最大值
    [InlineData(-5)]   // 负数
    public void ValidateFlow_GaussianBlurWithInvalidKernelSize_ShouldFailValidation(int kernelSize)
    {
        // Arrange
        var flow = new OperatorFlow();
        var acquisitionOp = CreateOperatorWithPorts("图像采集", OperatorType.ImageAcquisition, 0, 0);
        var blurOp = CreateOperatorWithPorts("高斯模糊", OperatorType.Filtering, 100, 100);

        blurOp.AddParameter(new Parameter(
            Guid.NewGuid(), "KernelSize", "核大小", "", "int", kernelSize, 1, 31, true));

        flow.AddOperator(acquisitionOp);
        flow.AddOperator(blurOp);
        flow.AddConnection(CreateConnection(acquisitionOp, blurOp));

        // Act
        var validation = _flowExecutionService.ValidateFlow(flow);

        // Assert
        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(e => e.Contains("核大小必须在 1-31 之间"));
    }

    [Theory]
    [InlineData(0, 255)]    // 边界值
    [InlineData(50, 150)]   // 典型值
    [InlineData(100, 200)]  // 高阈值
    public async Task ExecuteFlowAsync_CannyEdgeWithValidThresholds_ShouldSucceed(double threshold1, double threshold2)
    {
        // Arrange
        var flow = new OperatorFlow();
        var acquisitionOp = CreateOperatorWithPorts("图像采集", OperatorType.ImageAcquisition, 0, 0);
        var cannyOp = CreateOperatorWithPorts("边缘检测", OperatorType.EdgeDetection, 100, 100);

        cannyOp.AddParameter(new Parameter(
            Guid.NewGuid(), "Threshold1", "低阈值", "", "double", threshold1, 0.0, 255.0, true));
        cannyOp.AddParameter(new Parameter(
            Guid.NewGuid(), "Threshold2", "高阈值", "", "double", threshold2, 0.0, 255.0, true));

        flow.AddOperator(acquisitionOp);
        flow.AddOperator(cannyOp);
        flow.AddConnection(CreateConnection(acquisitionOp, cannyOp));

        var testImage = CreateTestImageBytes();
        var inputData = new Dictionary<string, object> { { "Image", testImage } };

        // Act
        var result = await _flowExecutionService.ExecuteFlowAsync(flow, inputData);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1, 150)]   // 阈值1为负
    [InlineData(50, 256)]   // 阈值2超过255
    [InlineData(300, 150)]  // 阈值1超过255
    public void ValidateFlow_CannyEdgeWithInvalidThresholds_ShouldFailValidation(double threshold1, double threshold2)
    {
        // Arrange
        var flow = new OperatorFlow();
        var acquisitionOp = CreateOperatorWithPorts("图像采集", OperatorType.ImageAcquisition, 0, 0);
        var cannyOp = CreateOperatorWithPorts("边缘检测", OperatorType.EdgeDetection, 100, 100);

        cannyOp.AddParameter(new Parameter(
            Guid.NewGuid(), "Threshold1", "低阈值", "", "double", threshold1, 0.0, 255.0, true));
        cannyOp.AddParameter(new Parameter(
            Guid.NewGuid(), "Threshold2", "高阈值", "", "double", threshold2, 0.0, 255.0, true));

        flow.AddOperator(acquisitionOp);
        flow.AddOperator(cannyOp);
        flow.AddConnection(CreateConnection(acquisitionOp, cannyOp));

        // Act
        var validation = _flowExecutionService.ValidateFlow(flow);

        // Assert
        validation.IsValid.Should().BeFalse();
    }

    #endregion

    #region 执行状态跟踪测试

    [Fact]
    public async Task ExecuteFlowAsync_ShouldTrackExecutionStatus()
    {
        // Arrange
        var (flow, acquisitionOp, blurOp, cannyOp) = Create3OperatorLinearFlow();
        var testImage = CreateTestImageBytes();
        var inputData = new Dictionary<string, object> { { "Image", testImage } };

        // Act
        var executionTask = _flowExecutionService.ExecuteFlowAsync(flow, inputData);

        // 在执行过程中检查状态
        var status = _flowExecutionService.GetExecutionStatus(flow.Id);

        await executionTask;

        var finalStatus = _flowExecutionService.GetExecutionStatus(flow.Id);

        // Assert
        finalStatus.Should().NotBeNull();
        finalStatus!.IsExecuting.Should().BeFalse();
        finalStatus.ProgressPercentage.Should().Be(100);
    }

    [Fact]
    public async Task ExecuteFlowAsync_ShouldRecordExecutionTimeForEachOperator()
    {
        // Arrange
        var (flow, acquisitionOp, blurOp, cannyOp) = Create3OperatorLinearFlow();
        var testImage = CreateTestImageBytes();
        var inputData = new Dictionary<string, object> { { "Image", testImage } };

        // Act
        var result = await _flowExecutionService.ExecuteFlowAsync(flow, inputData);

        // Assert
        result.IsSuccess.Should().BeTrue();
        foreach (var opResult in result.OperatorResults)
        {
            opResult.ExecutionTimeMs.Should().BeGreaterThanOrEqualTo(0);
        }

        // 总执行时间应该大于等于各算子执行时间之和
        var totalOperatorTime = result.OperatorResults.Sum(r => r.ExecutionTimeMs);
        result.ExecutionTimeMs.Should().BeGreaterThanOrEqualTo(totalOperatorTime);
    }

    #endregion

    #region 生命周期回归测试

    [Fact]
    public async Task ExecuteFlowAsync_ThresholdToResultOutput_ShouldReturnImageBytes()
    {
        // Arrange - 采集 -> 二值化 -> 结果输出
        var flow = new OperatorFlow();
        var acquisitionOp = CreateOperatorWithPorts("图像采集", OperatorType.ImageAcquisition, 0, 0);
        var thresholdOp = CreateOperatorWithPorts("阈值二值化", OperatorType.Thresholding, 100, 100);
        var outputOp = CreateOperatorWithPorts("结果输出", OperatorType.ResultOutput, 200, 100);

        thresholdOp.AddParameter(new Parameter(
            Guid.NewGuid(), "Threshold", "阈值", "", "double", 127.0, 0.0, 255.0, true));

        flow.AddOperator(acquisitionOp);
        flow.AddOperator(thresholdOp);
        flow.AddOperator(outputOp);
        flow.AddConnection(CreateConnection(acquisitionOp, thresholdOp));
        flow.AddConnection(CreateConnection(thresholdOp, outputOp));

        var inputData = new Dictionary<string, object> { { "Image", CreateTestImageBytes() } };

        // Act
        var result = await _flowExecutionService.ExecuteFlowAsync(flow, inputData);

        // Assert
        result.IsSuccess.Should().BeTrue($"流程执行失败: {result.ErrorMessage}");
        result.OutputData.Should().NotBeNull();
        result.OutputData.Should().ContainKey("Image");
        result.OutputData!["Image"].Should().BeAssignableTo<byte[]>();
        ((byte[])result.OutputData!["Image"]).Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteFlowAsync_CannyToResultOutput_ShouldReturnSerializableEdges()
    {
        // Arrange - 采集 -> 边缘检测 -> 结果输出
        var flow = new OperatorFlow();
        var acquisitionOp = CreateOperatorWithPorts("图像采集", OperatorType.ImageAcquisition, 0, 0);
        var cannyOp = CreateOperatorWithPorts("边缘检测", OperatorType.EdgeDetection, 100, 100);
        var outputOp = CreateOperatorWithPorts("结果输出", OperatorType.ResultOutput, 200, 100);

        flow.AddOperator(acquisitionOp);
        flow.AddOperator(cannyOp);
        flow.AddOperator(outputOp);
        flow.AddConnection(CreateConnection(acquisitionOp, cannyOp));
        flow.AddConnection(CreateConnection(cannyOp, outputOp));

        var inputData = new Dictionary<string, object> { { "Image", CreateTestImageBytes() } };

        // Act
        var result = await _flowExecutionService.ExecuteFlowAsync(flow, inputData);

        // Assert
        result.IsSuccess.Should().BeTrue($"流程执行失败: {result.ErrorMessage}");
        result.OutputData.Should().NotBeNull();
        result.OutputData.Should().ContainKey("Edges");
        result.OutputData!["Edges"].Should().BeAssignableTo<byte[]>();
    }

    [Fact]
    public async Task ExecuteFlowAsync_RoiToResultOutput_ShouldReturnSerializableMask()
    {
        // Arrange - 采集 -> ROI管理 -> 结果输出
        var flow = new OperatorFlow();
        var acquisitionOp = CreateOperatorWithPorts("图像采集", OperatorType.ImageAcquisition, 0, 0);
        var roiOp = CreateOperatorWithPorts("ROI管理", OperatorType.RoiManager, 100, 100);
        var outputOp = CreateOperatorWithPorts("结果输出", OperatorType.ResultOutput, 200, 100);

        roiOp.AddParameter(new Parameter(Guid.NewGuid(), "Shape", "形状", "", "string", "Rectangle", null, null, true));
        roiOp.AddParameter(new Parameter(Guid.NewGuid(), "Operation", "操作", "", "string", "Crop", null, null, true));
        roiOp.AddParameter(new Parameter(Guid.NewGuid(), "X", "X", "", "int", 0, 0, 10000, true));
        roiOp.AddParameter(new Parameter(Guid.NewGuid(), "Y", "Y", "", "int", 0, 0, 10000, true));
        roiOp.AddParameter(new Parameter(Guid.NewGuid(), "Width", "宽度", "", "int", 1, 1, 10000, true));
        roiOp.AddParameter(new Parameter(Guid.NewGuid(), "Height", "高度", "", "int", 1, 1, 10000, true));

        flow.AddOperator(acquisitionOp);
        flow.AddOperator(roiOp);
        flow.AddOperator(outputOp);
        flow.AddConnection(CreateConnection(acquisitionOp, roiOp));
        flow.AddConnection(CreateConnection(roiOp, outputOp));

        var inputData = new Dictionary<string, object> { { "Image", CreateTestImageBytes() } };

        // Act
        var result = await _flowExecutionService.ExecuteFlowAsync(flow, inputData);

        // Assert
        result.IsSuccess.Should().BeTrue($"流程执行失败: {result.ErrorMessage}");
        result.OutputData.Should().NotBeNull();
        result.OutputData.Should().ContainKey("Mask");
        result.OutputData!["Mask"].Should().BeAssignableTo<byte[]>();
    }

    #endregion

    #region Helper Methods

    private static (OperatorFlow flow, Operator acquisitionOp, Operator blurOp, Operator cannyOp) Create3OperatorLinearFlow()
    {
        var flow = new OperatorFlow();

        var acquisitionOp = CreateOperatorWithPorts("图像采集", OperatorType.ImageAcquisition, 0, 0);
        var blurOp = CreateOperatorWithPorts("高斯模糊", OperatorType.Filtering, 100, 100);
        var cannyOp = CreateOperatorWithPorts("边缘检测", OperatorType.EdgeDetection, 200, 100);

        // 配置高斯模糊参数
        blurOp.AddParameter(new Parameter(
            Guid.NewGuid(), "KernelSize", "核大小", "", "int", 5, 1, 31, true));
        blurOp.AddParameter(new Parameter(
            Guid.NewGuid(), "SigmaX", "Sigma X", "", "double", 1.0, 0.1, 10.0, true));

        // 配置Canny参数
        cannyOp.AddParameter(new Parameter(
            Guid.NewGuid(), "Threshold1", "低阈值", "", "double", 50.0, 0.0, 255.0, true));
        cannyOp.AddParameter(new Parameter(
            Guid.NewGuid(), "Threshold2", "高阈值", "", "double", 150.0, 0.0, 255.0, true));

        flow.AddOperator(acquisitionOp);
        flow.AddOperator(blurOp);
        flow.AddOperator(cannyOp);

        flow.AddConnection(CreateConnection(acquisitionOp, blurOp));
        flow.AddConnection(CreateConnection(blurOp, cannyOp));

        return (flow, acquisitionOp, blurOp, cannyOp);
    }

    private static OperatorFlow Create5OperatorComplexFlow()
    {
        var flow = new OperatorFlow();

        var acquisitionOp = CreateOperatorWithPorts("图像采集", OperatorType.ImageAcquisition, 0, 0);
        var blurOp = CreateOperatorWithPorts("高斯模糊", OperatorType.Filtering, 100, 100);
        var thresholdOp = CreateOperatorWithPorts("阈值二值化", OperatorType.Thresholding, 200, 100);
        var morphologyOp = CreateOperatorWithPorts("形态学操作", OperatorType.Morphology, 300, 100);
        var contoursOp = CreateOperatorWithPorts("轮廓查找", OperatorType.ContourDetection, 400, 100);

        // 配置参数
        blurOp.AddParameter(new Parameter(
            Guid.NewGuid(), "KernelSize", "核大小", "", "int", 5, 1, 31, true));

        thresholdOp.AddParameter(new Parameter(
            Guid.NewGuid(), "Threshold", "阈值", "", "double", 127.0, 0.0, 255.0, true));

        morphologyOp.AddParameter(new Parameter(
            Guid.NewGuid(), "Operation", "操作类型", "", "string", "Close", null, null, true));
        morphologyOp.AddParameter(new Parameter(
            Guid.NewGuid(), "KernelSize", "核大小", "", "int", 3, 1, 21, true));

        // 配置Blob检测参数 - 使用正值避免OpenCV验证错误
        contoursOp.AddParameter(new Parameter(
            Guid.NewGuid(), "MinArea", "最小面积", "", "int", 100, 1, 1000000, true));
        contoursOp.AddParameter(new Parameter(
            Guid.NewGuid(), "MaxArea", "最大面积", "", "int", 100000, 1, 10000000, true));
        contoursOp.AddParameter(new Parameter(
            Guid.NewGuid(), "MinCircularity", "最小圆度", "", "double", 0.1, 0.0, 1.0, true));

        flow.AddOperator(acquisitionOp);
        flow.AddOperator(blurOp);
        flow.AddOperator(thresholdOp);
        flow.AddOperator(morphologyOp);
        flow.AddOperator(contoursOp);

        // 连接流程
        flow.AddConnection(CreateConnection(acquisitionOp, blurOp));
        flow.AddConnection(CreateConnection(blurOp, thresholdOp));
        flow.AddConnection(CreateConnection(thresholdOp, morphologyOp));
        flow.AddConnection(CreateConnection(morphologyOp, contoursOp));

        return flow;
    }

    private static Operator CreateOperatorWithPorts(string name, OperatorType type, int x, int y)
    {
        var op = new Operator(name, type, x, y);

        // 添加默认端口
        if (type != OperatorType.ImageAcquisition)
        {
            op.AddInputPort("Input", PortDataType.Image, true);
        }

        op.AddOutputPort("Output", PortDataType.Image);

        return op;
    }

    private static OperatorConnection CreateConnection(Operator source, Operator target)
    {
        var sourcePort = source.OutputPorts.First();
        Port? targetPort = target.InputPorts.FirstOrDefault();

        if (targetPort == null)
        {
            target.AddInputPort("Input", PortDataType.Image, true);
            targetPort = target.InputPorts.First();
        }

        return new OperatorConnection(
            source.Id,
            sourcePort.Id,
            target.Id,
            targetPort.Id);
    }

    private static byte[] CreateTestImageBytes()
    {
        // 创建一个简单的1x1 PNG图像的Base64编码
        var base64Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        return Convert.FromBase64String(base64Png);
    }

    #endregion

    #region Sprint 5 Enhanced Tests

    [Fact]
    public async Task ExecuteFlowAsync_ParallelMode_ShouldSucceed()
    {
        // Arrange - 创建可以并行执行的流程
        var flow = new OperatorFlow();

        var acquisitionOp = CreateOperatorWithPorts("图像采集", OperatorType.ImageAcquisition, 0, 0);
        var blurOp = CreateOperatorWithPorts("高斯模糊", OperatorType.Filtering, 100, 100);
        var thresholdOp = CreateOperatorWithPorts("阈值二值化", OperatorType.Thresholding, 100, 200);

        blurOp.AddParameter(new Parameter(Guid.NewGuid(), "KernelSize", "核大小", "", "int", 5, 1, 31, true));
        thresholdOp.AddParameter(new Parameter(Guid.NewGuid(), "Threshold", "阈值", "", "double", 127.0, 0.0, 255.0, true));

        flow.AddOperator(acquisitionOp);
        flow.AddOperator(blurOp);
        flow.AddOperator(thresholdOp);

        // 从采集分叉到两个并行算子
        flow.AddConnection(CreateConnection(acquisitionOp, blurOp));
        flow.AddConnection(CreateConnection(acquisitionOp, thresholdOp));

        var testImage = CreateTestImageBytes();
        var inputData = new Dictionary<string, object> { { "Image", testImage } };

        // Act - 启用并行执行
        var result = await _flowExecutionService.ExecuteFlowAsync(flow, inputData, enableParallel: true);

        // Assert
        if (!result.IsSuccess)
        {
            var errorDetails = string.Join("\n", result.OperatorResults
                .Where(r => !r.IsSuccess)
                .Select(r => $"  - {r.OperatorName}: {r.ErrorMessage}"));
            Assert.Fail($"并行流程执行失败: {result.ErrorMessage}\n算子错误:\n{errorDetails}");
        }
        result.OperatorResults.Should().HaveCount(3);
        result.OperatorResults.All(r => r.IsSuccess).Should().BeTrue();
    }

    [Fact]
    public void ExecuteFlowAsync_CircularDependency_ShouldBeDetected()
    {
        // Arrange - 创建算子 A, B, C
        var flow = new OperatorFlow();

        var opA = CreateOperatorWithPorts("算子A", OperatorType.Filtering, 0, 0);
        var opB = CreateOperatorWithPorts("算子B", OperatorType.Filtering, 100, 0);
        var opC = CreateOperatorWithPorts("算子C", OperatorType.Filtering, 200, 0);

        flow.AddOperator(opA);
        flow.AddOperator(opB);
        flow.AddOperator(opC);

        // Act & Assert - 添加前两个正常连接
        flow.AddConnection(CreateConnection(opA, opB));
        flow.AddConnection(CreateConnection(opB, opC));

        // 尝试添加循环连接 C → A 应该抛出异常
        var cyclicConnection = CreateConnection(opC, opA);
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            flow.AddConnection(cyclicConnection);
        });

        exception.Message.Should().Contain("循环依赖");
    }

    [Fact]
    public async Task ExecuteFlowAsync_MultipleFlows_Concurrent()
    {
        // Arrange
        var tasks = new List<Task<FlowExecutionResult>>();

        // Act - 同时启动10个流程执行
        for (int i = 0; i < 10; i++)
        {
            var flow = Create3OperatorLinearFlow().flow;
            var testImage = CreateTestImageBytes();
            var inputData = new Dictionary<string, object> { { "Image", testImage } };
            tasks.Add(_flowExecutionService.ExecuteFlowAsync(flow, inputData));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - 所有流程都应该成功完成
        results.Should().AllSatisfy(r =>
        {
            r.IsSuccess.Should().BeTrue($"流程执行失败: {r.ErrorMessage}");
            r.OperatorResults.Should().HaveCount(3);
        });
    }

    [Fact(Skip = "CancelExecutionAsync尚未完全实现")]
    public async Task CancelExecutionAsync_DuringExecution_ShouldStop()
    {
        // Arrange - 创建一个耗时较长的流程
        var flow = Create5OperatorComplexFlow();
        var testImage = CreateTestImageBytes();
        var inputData = new Dictionary<string, object> { { "Image", testImage } };

        // Act - 启动流程
        var executionTask = _flowExecutionService.ExecuteFlowAsync(flow, inputData);

        // 取消功能待实现 - 需要添加CancelExecutionAsync方法到IFlowExecutionService接口
        // 实现后取消下面这行的注释:
        // await _flowExecutionService.CancelExecutionAsync(flow.Id);

        // Assert - 暂时只验证流程能正常完成
        var result = await executionTask;
        result.Should().NotBeNull();
    }

    #endregion
}
