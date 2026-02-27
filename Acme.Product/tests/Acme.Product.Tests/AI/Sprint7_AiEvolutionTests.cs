using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Acme.Product.Core.DTOs;
using Acme.Product.Core.Entities;
using Acme.Product.Infrastructure.AI;
using Acme.Product.Infrastructure.Services;
using FluentAssertions;

namespace Acme.Product.Tests.AI;

[Trait("Category", "Sprint7_AiEvolution")]
public class Sprint7_AiEvolutionTests
{
    [Fact(DisplayName = "PromptBuilder - 动态裁剪应按需求选择高相关算子并保留 fallback 提示")]
    public void PromptBuilder_BuildSystemPrompt_ShouldPruneOperatorsByDescription()
    {
        // Arrange
        var factory = new OperatorFactory();
        var builder = new PromptBuilder(factory);

        // Act
        var measurementPrompt = builder.BuildSystemPrompt("测量两个孔之间的间距并输出毫米结果");
        var commPrompt = builder.BuildSystemPrompt("把检测结果通过Modbus发送到PLC");
        var measurementOperators = ExtractPrioritizedOperatorIds(measurementPrompt);
        var communicationOperators = ExtractPrioritizedOperatorIds(commPrompt);

        // Assert
        measurementOperators.Should().Contain(id =>
            id.Equals("GapMeasurement", StringComparison.OrdinalIgnoreCase) ||
            id.Equals("Measurement", StringComparison.OrdinalIgnoreCase) ||
            id.Equals("CoordinateTransform", StringComparison.OrdinalIgnoreCase));

        communicationOperators.Should().Contain(id =>
            id.Equals("ModbusCommunication", StringComparison.OrdinalIgnoreCase) ||
            id.Equals("SiemensS7Communication", StringComparison.OrdinalIgnoreCase) ||
            id.Equals("MitsubishiMcCommunication", StringComparison.OrdinalIgnoreCase) ||
            id.Equals("OmronFinsCommunication", StringComparison.OrdinalIgnoreCase));

        measurementPrompt.Should().Contain("如果需要的算子不在列表中，仍可使用其他已注册算子。");
        measurementOperators.Should().NotBeEquivalentTo(communicationOperators);
    }

    [Fact(DisplayName = "PromptBuilder - System Prompt 应包含参数推理指南")]
    public void PromptBuilder_BuildSystemPrompt_ShouldContainParameterInferenceGuide()
    {
        // Arrange
        var factory = new OperatorFactory();
        var builder = new PromptBuilder(factory);

        // Act
        var prompt = builder.BuildSystemPrompt("测量间距0.5mm，容差0.05mm");

        // Assert
        prompt.Should().Contain("参数推理指南");
        prompt.Should().Contain("数值提取规则");
        prompt.Should().Contain("mm/μm");
        prompt.Should().Contain("PixelSize");
    }

    [Fact(DisplayName = "AiFlowValidator - 应自动填充必填默认值并对越界参数执行 Clamp")]
    public void AiFlowValidator_Validate_ShouldApplyIntelligentDefaultsAndClamp()
    {
        // Arrange
        var factory = new OperatorFactory();
        var validator = new AiFlowValidator(factory);
        var generated = new AiGeneratedFlowJson
        {
            Operators = new List<AiGeneratedOperator>
            {
                new()
                {
                    TempId = "op_1",
                    OperatorType = "SharpnessEvaluation",
                    DisplayName = "清晰度评估",
                    Parameters = new Dictionary<string, string>
                    {
                        ["Threshold"] = "-5"
                    }
                }
            },
            Connections = new List<AiGeneratedConnection>()
        };

        // Act
        var result = validator.Validate(generated);

        // Assert
        result.IsValid.Should().BeTrue();
        generated.Operators[0].Parameters.Should().ContainKey("Method");
        generated.Operators[0].Parameters["Method"].Should().Be("Laplacian");
        double.Parse(generated.Operators[0].Parameters["Threshold"], CultureInfo.InvariantCulture)
            .Should().BeGreaterOrEqualTo(0);
    }

    [Fact(DisplayName = "ConversationalFlowService - 有上下文且出现修改动词时应识别为 MODIFY")]
    public void ConversationalFlowService_PrepareContext_WithFlowAndModifyIntent_ShouldDetectModify()
    {
        // Arrange
        var service = new ConversationalFlowService(Path.Combine(Path.GetTempPath(), "clearvision-conversation-test-" + Guid.NewGuid().ToString("N")));
        var request = new AiFlowGenerationRequest(
            "把阈值改成100",
            SessionId: "session-1",
            ExistingFlowJson: """{"operators":[{"tempId":"op_1","operatorType":"Thresholding"}]}""");

        // Act
        var context = service.PrepareContext(request);

        // Assert
        context.Intent.Should().Be(ConversationIntent.Modify);
        context.PromptContext.Should().Contain("会话意图：MODIFY");
        context.ExistingFlowJson.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "ConversationalFlowService - 无上下文时默认 NEW")]
    public void ConversationalFlowService_PrepareContext_WithoutFlow_ShouldDefaultToNew()
    {
        // Arrange
        var service = new ConversationalFlowService(Path.Combine(Path.GetTempPath(), "clearvision-conversation-test-" + Guid.NewGuid().ToString("N")));
        var request = new AiFlowGenerationRequest(
            "把阈值改成100",
            SessionId: "session-2");

        // Act
        var context = service.PrepareContext(request);

        // Assert
        context.Intent.Should().Be(ConversationIntent.New);
        context.PromptContext.Should().Contain("会话意图：NEW");
    }

    [Fact(DisplayName = "ConversationalFlowService - 解释请求应识别为 EXPLAIN")]
    public void ConversationalFlowService_PrepareContext_WithExplainIntent_ShouldDetectExplain()
    {
        // Arrange
        var service = new ConversationalFlowService(Path.Combine(Path.GetTempPath(), "clearvision-conversation-test-" + Guid.NewGuid().ToString("N")));
        var request = new AiFlowGenerationRequest(
            "解释一下这个流程为什么这样设计",
            SessionId: "session-3",
            ExistingFlowJson: """{"operators":[{"tempId":"op_1","operatorType":"ImageAcquisition"}]}""");

        // Act
        var context = service.PrepareContext(request);

        // Assert
        context.Intent.Should().Be(ConversationIntent.Explain);
        context.PromptContext.Should().Contain("会话意图：EXPLAIN");
    }

    [Fact(DisplayName = "ConversationalFlowService - 重启后应恢复会话上下文")]
    public void ConversationalFlowService_Restart_ShouldRestoreSessionContext()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "clearvision-conversation-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var firstService = new ConversationalFlowService(tempRoot);
            var firstContext = firstService.PrepareContext(
                new AiFlowGenerationRequest(
                    "先创建一个检测流程",
                    SessionId: "persist-session",
                    ExistingFlowJson: """{"operators":[{"tempId":"op_1","operatorType":"Thresholding"}]}"""));

            firstService.RecordAssistantResponse(
                firstContext.SessionId,
                "已生成初版流程",
                firstContext.ExistingFlowJson);

            File.Exists(Path.Combine(tempRoot, "conversation_sessions.json")).Should().BeTrue();

            var secondService = new ConversationalFlowService(tempRoot);
            var restoredContext = secondService.PrepareContext(
                new AiFlowGenerationRequest(
                    "把阈值改成100",
                    SessionId: firstContext.SessionId));

            restoredContext.Intent.Should().Be(ConversationIntent.Modify);
            restoredContext.ExistingFlowJson.Should().NotBeNullOrWhiteSpace();
            restoredContext.PromptContext.Should().Contain("assistant: 已生成初版流程");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact(DisplayName = "FlowTemplateService - 应初始化内置模板并支持读取")]
    public async Task FlowTemplateService_GetTemplatesAsync_ShouldLoadBuiltInTemplates()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), "clearvision-template-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new FlowTemplateService(tempRoot);

            // Act
            var templates = await service.GetTemplatesAsync();

            // Assert
            templates.Should().HaveCountGreaterOrEqualTo(8);
            templates.Select(t => t.Name).Should().Contain("传统缺陷检测");
            File.Exists(Path.Combine(tempRoot, "templates", "flow_templates.json")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact(DisplayName = "FlowTemplateService - 应支持保存并回读自定义模板")]
    public async Task FlowTemplateService_SaveTemplateAsync_ShouldPersistTemplate()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), "clearvision-template-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new FlowTemplateService(tempRoot);
            var template = new FlowTemplate
            {
                Name = "自定义测试模板",
                Description = "用于测试保存",
                Industry = "通用制造",
                Tags = new List<string> { "测试" },
                FlowJson = "{}"
            };

            // Act
            var saved = await service.SaveTemplateAsync(template);
            var loaded = await service.GetTemplateAsync(saved.Id);

            // Assert
            loaded.Should().NotBeNull();
            loaded!.Name.Should().Be("自定义测试模板");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static IReadOnlyList<string> ExtractPrioritizedOperatorIds(string prompt)
    {
        var match = Regex.Match(
            prompt,
            @"# 优先算子目录（根据当前需求动态裁剪）[\s\S]*?```json\s*(\[[\s\S]*?\])\s*```",
            RegexOptions.Singleline);

        match.Success.Should().BeTrue("Prompt should contain prioritized operator catalog JSON block.");
        var json = match.Groups[1].Value;

        using var doc = JsonDocument.Parse(json);
        var ids = doc.RootElement
            .EnumerateArray()
            .Select(item => item.GetProperty("operator_id").GetString() ?? string.Empty)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList();

        return ids;
    }
}
