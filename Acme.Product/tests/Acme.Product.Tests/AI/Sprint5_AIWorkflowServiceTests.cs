// Sprint5_AIWorkflowServiceTests.cs
// AI 编排服务单元测试 - Sprint 5
// 作者：蘅芜君

using Xunit;
using FluentAssertions;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.AI;
using Acme.Product.Infrastructure.Services;

namespace Acme.Product.Tests.AI;

/// <summary>
/// Sprint 5: AI 编排服务单元测试
/// </summary>
public class Sprint5_AIWorkflowServiceTests
{
    private readonly FlowLinter _linter;

    public Sprint5_AIWorkflowServiceTests()
    {
        _linter = new FlowLinter();
    }

    #region AIPromptBuilder Tests

    [Fact(DisplayName = "AIPromptBuilder - 构建完整提示词包含系统提示")]
    public void AIPromptBuilder_WithSystemPrompt_ShouldContainHeader()
    {
        // Arrange
        var builder = new AIPromptBuilder();

        // Act
        var prompt = builder
            .WithSystemPrompt()
            .Build();

        // Assert
        prompt.Should().Contain("ClearVision");
        prompt.Should().Contain("工业视觉检测");
    }

    [Fact(DisplayName = "AIPromptBuilder - 构建的提示词包含算子库")]
    public void AIPromptBuilder_WithOperatorLibrary_ShouldContainOperators()
    {
        // Arrange
        var builder = new AIPromptBuilder();

        // Act
        var prompt = builder
            .WithSystemPrompt()
            .WithOperatorLibrary()
            .Build();

        // Assert
        prompt.Should().Contain("图像采集");
        prompt.Should().Contain("深度学习检测");
        prompt.Should().Contain("边缘检测");
        prompt.Should().Contain("ForEach");
    }

    [Fact(DisplayName = "AIPromptBuilder - 构建的提示词包含设计规则")]
    public void AIPromptBuilder_WithDesignRules_ShouldContainRules()
    {
        // Arrange
        var builder = new AIPromptBuilder();

        // Act
        var prompt = builder
            .WithSystemPrompt()
            .WithDesignRules()
            .Build();

        // Assert
        prompt.Should().Contain("DAG");
        prompt.Should().Contain("通信算子");
        prompt.Should().Contain("ForEach");
    }

    [Fact(DisplayName = "AIPromptBuilder - CreateFullPrompt 方法应生成完整提示词")]
    public void AIPromptBuilder_CreateFullPrompt_ShouldGenerateCompletePrompt()
    {
        // Arrange & Act
        var prompt = AIPromptBuilder.CreateFullPrompt("采集图像并进行缺陷检测");

        // Assert
        prompt.Should().Contain("ClearVision");
        prompt.Should().Contain("采集图像并进行缺陷检测");
        prompt.Should().Contain("JSON");
    }

    #endregion

    #region AIGeneratedFlowParser Tests

    [Fact(DisplayName = "FlowParser - 应能解析有效的 AI 生成 JSON")]
    public void AIGeneratedFlowParser_Parse_ValidJson_ShouldReturnSuccess()
    {
        // Arrange
        var parser = new AIGeneratedFlowParser(_linter);
        var validJson = @"{
            ""flowName"": ""测试流程"",
            ""operators"": [
                { ""id"": ""00000000-0000-0000-0000-000000000001"", ""name"": ""图像采集"", ""type"": ""ImageAcquisition"", ""outputPorts"": [{ ""id"": ""00000000-0000-0000-0000-000000000011"", ""name"": ""Image"", ""dataType"": ""Image"" }] }
            ],
            ""connections"": []
        }";

        // Act
        var result = parser.Parse(validJson);

        // Assert
        result.IsSuccessful.Should().BeTrue();
        result.Flow.Should().NotBeNull();
        result.Flow!.Name.Should().Be("测试流程");
    }

    [Fact(DisplayName = "FlowParser - 解析无效 JSON 应返回失败")]
    public void AIGeneratedFlowParser_Parse_InvalidJson_ShouldReturnFailure()
    {
        // Arrange
        var parser = new AIGeneratedFlowParser(_linter);
        var invalidJson = "not valid json";

        // Act
        var result = parser.Parse(invalidJson);

        // Assert
        result.IsSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Contain("JSON");
    }

    [Fact(DisplayName = "FlowParser - 解析包含未知算子类型的 JSON 应返回失败")]
    public void AIGeneratedFlowParser_Parse_UnknownOperatorType_ShouldReturnFailure()
    {
        // Arrange
        var parser = new AIGeneratedFlowParser(_linter);
        var jsonWithUnknownOp = @"{
            ""flowName"": ""测试流程"",
            ""operators"": [
                { ""id"": ""00000000-0000-0000-0000-000000000001"", ""name"": ""未知算子"", ""type"": ""UnknownOperator"", ""outputPorts"": [] }
            ],
            ""connections"": []
        }";

        // Act
        var result = parser.Parse(jsonWithUnknownOp);

        // Assert
        result.IsSuccessful.Should().BeFalse();
    }

    [Fact(DisplayName = "FlowParser - 解析完整流程应正确映射端口类型")]
    public void AIGeneratedFlowParser_Parse_FullFlow_ShouldMapPortTypes()
    {
        // Arrange
        var parser = new AIGeneratedFlowParser(_linter);
        var fullJson = @"{
            ""flowName"": ""检测流程"",
            ""operators"": [
                { 
                    ""id"": ""00000000-0000-0000-0000-000000000001"", 
                    ""name"": ""图像采集"", 
                    ""type"": ""ImageAcquisition"",
                    ""outputPorts"": [{ ""id"": ""00000000-0000-0000-0000-000000000011"", ""name"": ""Image"", ""dataType"": ""Image"" }]
                },
                { 
                    ""id"": ""00000000-0000-0000-0000-000000000002"", 
                    ""name"": ""YOLO检测"", 
                    ""type"": ""DeepLearning"",
                    ""parameters"": [{ ""name"": ""Confidence"", ""value"": ""0.5"" }],
                    ""inputPorts"": [{ ""id"": ""00000000-0000-0000-0000-000000000012"", ""name"": ""Image"", ""dataType"": ""Image"" }],
                    ""outputPorts"": [{ ""id"": ""00000000-0000-0000-0000-000000000013"", ""name"": ""Results"", ""dataType"": ""DetectionList"" }]
                }
            ],
            ""connections"": [
                { 
                    ""sourceOperatorId"": ""00000000-0000-0000-0000-000000000001"", 
                    ""sourcePortId"": ""00000000-0000-0000-0000-000000000011"",
                    ""targetOperatorId"": ""00000000-0000-0000-0000-000000000002"",
                    ""targetPortId"": ""00000000-0000-0000-0000-000000000012""
                }
            ]
        }";

        // Act
        var result = parser.Parse(fullJson);

        // Assert
        Assert.True(result.IsSuccessful, $"解析失败: {result.ErrorMessage}");
        result.Flow.Should().NotBeNull();
        result.Flow!.Operators.Should().HaveCount(2);
        result.Flow.Connections.Should().HaveCount(1);
    }

    #endregion

    #region StubRegistryBuilder Tests

    [Fact(DisplayName = "StubRegistryBuilder - 应包含所有算子的 Stub 定义")]
    public void StubRegistryBuilder_ShouldContainStubsForAllOperators()
    {
        // Arrange
        var registry = new Acme.Product.Infrastructure.AI.DryRun.DryRunStubRegistry();
        var builder = new Acme.Product.Infrastructure.AI.DryRun.StubRegistryBuilder(registry);

        // Act & Assert
        builder.HasStub(OperatorType.ImageAcquisition).Should().BeTrue();
        builder.HasStub(OperatorType.DeepLearning).Should().BeTrue();
        builder.HasStub(OperatorType.ForEach).Should().BeTrue();
        builder.HasStub(OperatorType.MathOperation).Should().BeTrue();
        builder.HasStub(OperatorType.HttpRequest).Should().BeTrue();
    }

    [Fact(DisplayName = "StubRegistryBuilder - 执行 MathOperation Stub 应返回计算结果")]
    public void StubRegistryBuilder_ExecuteMathOperationStub_ShouldReturnCalculatedValue()
    {
        // Arrange
        var registry = new Acme.Product.Infrastructure.AI.DryRun.DryRunStubRegistry();
        var builder = new Acme.Product.Infrastructure.AI.DryRun.StubRegistryBuilder(registry);
        var inputs = new Dictionary<string, string>
        {
            ["Operation"] = "Add",
            ["InputA"] = "10",
            ["InputB"] = "5"
        };

        // Act
        var result = builder.ExecuteStub(OperatorType.MathOperation, inputs);

        // Assert
        result.SimulatedValue.Should().Be("15.00");
    }

    [Fact(DisplayName = "StubRegistryBuilder - 执行 LogicGate Stub 应返回布尔结果")]
    public void StubRegistryBuilder_ExecuteLogicGateStub_ShouldReturnBooleanValue()
    {
        // Arrange
        var registry = new Acme.Product.Infrastructure.AI.DryRun.DryRunStubRegistry();
        var builder = new Acme.Product.Infrastructure.AI.DryRun.StubRegistryBuilder(registry);
        var inputs = new Dictionary<string, string>
        {
            ["Operation"] = "AND",
            ["InputA"] = "true",
            ["InputB"] = "false"
        };

        // Act
        var result = builder.ExecuteStub(OperatorType.LogicGate, inputs);

        // Assert
        result.SimulatedValue.Should().Be("False");
    }

    [Fact(DisplayName = "StubRegistryBuilder - BuildForFlow 应返回配置的注册表")]
    public void StubRegistryBuilder_BuildForFlow_ShouldReturnConfiguredRegistry()
    {
        // Arrange
        var registry = new Acme.Product.Infrastructure.AI.DryRun.DryRunStubRegistry();
        var builder = new Acme.Product.Infrastructure.AI.DryRun.StubRegistryBuilder(registry);
        var operatorTypes = new List<OperatorType>
        {
            OperatorType.ImageAcquisition,
            OperatorType.DeepLearning
        };

        // Act
        var flowRegistry = builder.BuildForFlow(operatorTypes);

        // Assert
        flowRegistry.Should().NotBeNull();
    }

    #endregion

    #region AIWorkflowService Integration Tests

    [Fact(DisplayName = "AIWorkflowService - 验证简单流程应成功")]
    public void AIWorkflowService_ValidateSimpleFlow_ShouldSucceed()
    {
        // Arrange
        var flow = new OperatorFlow("简单流程");
        var op1 = new Operator(Guid.NewGuid(), "图像采集", OperatorType.ImageAcquisition, 100, 100);
        op1.LoadOutputPort(Guid.NewGuid(), "Image", PortDataType.Image);
        flow.AddOperator(op1);

        // 创建 service（需要更多依赖）
        // 由于需要大量依赖，这里只验证 Linter 部分
        var lintResult = _linter.Lint(flow);

        // Assert
        lintResult.HasErrors.Should().BeFalse();
    }

    [Fact(DisplayName = "AIWorkflowService - 验证包含错误的流程应报告错误")]
    public void AIWorkflowService_ValidateFlowWithErrors_ShouldReportErrors()
    {
        // Arrange - 创建一个包含循环的流程
        var flow = new OperatorFlow("循环流程");
        var op1 = new Operator(Guid.NewGuid(), "算子1", OperatorType.ImageAcquisition, 100, 100);
        var op2 = new Operator(Guid.NewGuid(), "算子2", OperatorType.GaussianBlur, 300, 100);

        var port1 = Guid.NewGuid();
        var port2 = Guid.NewGuid();
        var port3 = Guid.NewGuid();
        var port4 = Guid.NewGuid();

        op1.LoadOutputPort(port1, "Output", PortDataType.Image);
        op1.LoadInputPort(port4, "Input", PortDataType.Image, true);
        op2.LoadInputPort(port2, "Input", PortDataType.Image, true);
        op2.LoadOutputPort(port3, "Output", PortDataType.Image);

        flow.AddOperator(op1);
        flow.AddOperator(op2);

        // 创建循环连接: op1 -> op2 -> op1
        flow.AddConnection(new Acme.Product.Core.ValueObjects.OperatorConnection(op1.Id, port1, op2.Id, port2));

        // 第二条连接应抛出异常，因为形成了循环
        Assert.Throws<InvalidOperationException>(() =>
            flow.AddConnection(new Acme.Product.Core.ValueObjects.OperatorConnection(op2.Id, port3, op1.Id, port4)));
    }

    #endregion
}
