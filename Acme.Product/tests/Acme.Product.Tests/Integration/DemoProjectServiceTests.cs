// DemoProjectServiceTests.cs
// DemoProjectService 集成测试
// 作者：蘅芜君

using Acme.Product.Application.Services;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Interfaces;
using FluentAssertions;
using NSubstitute;

namespace Acme.Product.Tests.Integration;

/// <summary>
/// DemoProjectService 集成测试
/// </summary>
public class DemoProjectServiceIntegrationTests
{
    private readonly IProjectRepository _projectRepository;
    private readonly DemoProjectService _demoService;

    public DemoProjectServiceIntegrationTests()
    {
        _projectRepository = Substitute.For<IProjectRepository>();
        _demoService = new DemoProjectService(_projectRepository);
    }

    [Fact]
    public async Task CreateDemoProjectAsync_ShouldCreateCompletePCBInspectionFlow()
    {
        // Arrange
        Project? capturedProject = null;
        _projectRepository.AddAsync(Arg.Do<Project>(p => capturedProject = p)).Returns(p => Task.FromResult(p.Arg<Project>()));

        // Act
        var result = await _demoService.CreateDemoProjectAsync();

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("PCB缺陷检测演示");
        result.Description.Should().Be("用于演示工业视觉检测的示例工程");
        result.Flow.Should().NotBeNull();
        result.Flow!.Operators.Should().HaveCount(6);

        // 验证算子链
        var operators = result.Flow.Operators.ToList();
        operators[0].Name.Should().Be("图像采集");
        operators[0].Type.Should().Be(OperatorType.ImageAcquisition);
        operators[1].Name.Should().Be("高斯滤波");
        operators[1].Type.Should().Be(OperatorType.Preprocessing);
        operators[2].Name.Should().Be("边缘检测");
        operators[2].Type.Should().Be(OperatorType.EdgeDetection);
        operators[3].Name.Should().Be("二值化");
        operators[3].Type.Should().Be(OperatorType.Thresholding);
        operators[4].Name.Should().Be("轮廓查找");
        operators[4].Type.Should().Be(OperatorType.ContourDetection);
        operators[5].Name.Should().Be("结果输出");
        operators[5].Type.Should().Be(OperatorType.ResultOutput);

        // 验证连接
        result.Flow.Connections.Should().HaveCount(5);

        // 验证保存到仓库
        await _projectRepository.Received(1).AddAsync(Arg.Any<Project>());
        capturedProject.Should().NotBeNull();
        capturedProject!.Name.Should().Be("PCB缺陷检测演示");
    }

    [Fact]
    public async Task CreateDemoProjectAsync_ShouldSetGlobalSettings()
    {
        // Arrange
        _projectRepository.AddAsync(Arg.Any<Project>()).Returns(p => Task.FromResult(p.Arg<Project>()));

        // Act
        var result = await _demoService.CreateDemoProjectAsync();

        // Assert
        result.GlobalSettings.Should().ContainKey("InspectionMode");
        result.GlobalSettings["InspectionMode"].Should().Be("Single");
        result.GlobalSettings.Should().ContainKey("SaveResults");
        result.GlobalSettings["SaveResults"].Should().Be("true");
        result.GlobalSettings.Should().ContainKey("DefectThreshold");
        result.GlobalSettings["DefectThreshold"].Should().Be("0.75");
    }

    [Fact]
    public async Task CreateSimpleDemoProjectAsync_ShouldCreateMinimalFlow()
    {
        // Arrange
        _projectRepository.AddAsync(Arg.Any<Project>()).Returns(p => Task.FromResult(p.Arg<Project>()));

        // Act
        var result = await _demoService.CreateSimpleDemoProjectAsync();

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("简单检测演示");
        result.Flow.Should().NotBeNull();
        result.Flow!.Operators.Should().HaveCount(2);

        var operators = result.Flow.Operators.ToList();
        operators[0].Name.Should().Be("图像采集");
        operators[1].Name.Should().Be("结果输出");

        // 简单流程只有1个连接
        result.Flow.Connections.Should().HaveCount(1);

        await _projectRepository.Received(1).AddAsync(Arg.Any<Project>());
    }

    [Fact]
    public void GetDemoGuide_ShouldReturnCompleteGuide()
    {
        // Act
        var guide = _demoService.GetDemoGuide();

        // Assert
        guide.Should().NotBeNull();
        guide.Title.Should().Be("ClearVision 演示指南");
        guide.Description.Should().Contain("PCB缺陷检测");

        // 验证步骤
        guide.Steps.Should().HaveCount(4);
        guide.Steps[0].Step.Should().Be(1);
        guide.Steps[0].Title.Should().Be("选择图像");
        guide.Steps[1].Title.Should().Be("配置算子");
        guide.Steps[2].Title.Should().Be("运行检测");
        guide.Steps[3].Title.Should().Be("查看结果");

        // 验证提示
        guide.Tips.Should().NotBeEmpty();
        guide.Tips.Should().Contain(t => t.Contains("拖拽算子"));
        guide.Tips.Should().Contain(t => t.Contains("检测结果"));
    }

    [Fact]
    public async Task CreateDemoProjectAsync_ShouldConfigureOperatorParameters()
    {
        // Arrange
        _projectRepository.AddAsync(Arg.Any<Project>()).Returns(p => Task.FromResult(p.Arg<Project>()));

        // Act
        var result = await _demoService.CreateDemoProjectAsync();

        // Assert
        var blurOp = result.Flow!.Operators.First(o => o.Name == "高斯滤波");
        blurOp.Parameters.Should().Contain(p => p.Name == "KernelSize");
        blurOp.Parameters.Should().Contain(p => p.Name == "SigmaX");
        blurOp.Parameters.Should().Contain(p => p.Name == "SigmaY");

        var cannyOp = result.Flow.Operators.First(o => o.Name == "边缘检测");
        cannyOp.Parameters.Should().Contain(p => p.Name == "Threshold1");
        cannyOp.Parameters.Should().Contain(p => p.Name == "Threshold2");
    }
}
