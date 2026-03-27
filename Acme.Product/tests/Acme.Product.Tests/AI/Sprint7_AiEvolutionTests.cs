using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Acme.Product.Core.DTOs;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Services;
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
        result.Diagnostics.Should().Contain(item =>
            item.Code == "default_parameter_applied" &&
            item.ParameterName == "Method" &&
            item.Severity == AiValidationSeverity.Warning);
        result.Diagnostics.Should().Contain(item =>
            item.Code == "parameter_clamped" &&
            item.ParameterName == "Threshold" &&
            item.Severity == AiValidationSeverity.Warning);
    }

    [Fact(DisplayName = "AiFlowValidator - 非法算子类型应产出结构化错误诊断")]
    public void AiFlowValidator_Validate_InvalidOperatorType_ShouldEmitStructuredDiagnostic()
    {
        // Arrange
        var factory = new OperatorFactory();
        var validator = new AiFlowValidator(factory);
        var generated = new AiGeneratedFlowJson
        {
            Operators =
            [
                new AiGeneratedOperator
                {
                    TempId = "op_1",
                    OperatorType = "UnknownOperator",
                    DisplayName = "未知算子"
                }
            ],
            Connections = []
        };

        // Act
        var result = validator.Validate(generated);

        // Assert
        result.IsValid.Should().BeFalse();
        result.PrimaryError.Should().NotBeNull();
        result.PrimaryError!.Code.Should().Be("unknown_operator_type");
        result.PrimaryError.Category.Should().Be("operator");
        result.PrimaryError.OperatorId.Should().Be("op_1");
        result.PrimaryError.RelatedFields.Should().Contain("operators[0].operatorType");
        result.PrimaryError.RepairHint.Should().NotBeNullOrWhiteSpace();
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
            templates.Should().HaveCount(1);
            templates.Select(t => t.Name).Should().ContainSingle().Which.Should().Be("端子线序检测");
            File.Exists(Path.Combine(tempRoot, "templates", "flow_templates.json")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact(DisplayName = "FlowTemplateService - 已有模板库缺失新增内置模板时应自动补齐")]
    public async Task FlowTemplateService_GetTemplatesAsync_ShouldMergeMissingBuiltInTemplatesIntoExistingStore()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "clearvision-template-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var templateDirectory = Path.Combine(tempRoot, "templates");
            Directory.CreateDirectory(templateDirectory);

            var existingTemplates = new List<FlowTemplate>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "传统缺陷检测",
                    Description = "旧模板库中的内置模板副本",
                    Industry = "3C电子",
                    Tags = new List<string> { "缺陷检测" },
                    FlowJson = "{}",
                    CreatedAt = DateTime.UtcNow.AddDays(-7)
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "我的自定义模板",
                    Description = "需要保留的用户模板",
                    Industry = "通用制造",
                    Tags = new List<string> { "自定义" },
                    FlowJson = "{\"operators\":[]}",
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                }
            };

            var templateFilePath = Path.Combine(templateDirectory, "flow_templates.json");
            await File.WriteAllTextAsync(
                templateFilePath,
                JsonSerializer.Serialize(existingTemplates, new JsonSerializerOptions { WriteIndented = true }));

            var service = new FlowTemplateService(tempRoot);

            var templates = await service.GetTemplatesAsync();

            templates.Should().HaveCount(2);
            templates.Select(item => item.Name).Should().Contain("我的自定义模板");
            templates.Select(item => item.Name).Should().Contain("端子线序检测");
            templates.Select(item => item.Name).Should().NotContain("传统缺陷检测");
            templates.Count(item => item.Name == "端子线序检测").Should().Be(1);

            var persisted = JsonSerializer.Deserialize<List<FlowTemplate>>(
                await File.ReadAllTextAsync(templateFilePath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            persisted.Should().NotBeNull();
            persisted!.Select(item => item.Name).Should().Contain("端子线序检测");
            persisted.Select(item => item.Name).Should().Contain("我的自定义模板");
            persisted.Select(item => item.Name).Should().NotContain("传统缺陷检测");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact(DisplayName = "FlowTemplateService - 已有旧版线序模板时应自动升级到最新骨架")]
    public async Task FlowTemplateService_GetTemplatesAsync_ShouldUpgradeOutdatedWireSequenceTemplate()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "clearvision-template-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var templateDirectory = Path.Combine(tempRoot, "templates");
            Directory.CreateDirectory(templateDirectory);

            var oldWireTemplate = new FlowTemplate
            {
                Id = Guid.NewGuid(),
                Name = "端子线序检测",
                Description = "旧模板",
                Industry = "线束装配",
                Tags = new List<string> { "线序" },
                TemplateVersion = "1.1.0",
                ScenarioKey = "wire-sequence-terminal",
                ScenarioPackage = new ScenarioPackageBinding
                {
                    PackageKey = "wire-sequence-terminal",
                    PackageVersion = "1.1.0",
                    AssetVersionIds = new List<string> { "template:terminal-wire-sequence-template@1.1.0" },
                    RequiredResources = new List<string> { "DeepLearning.ModelPath" }
                },
                FlowJson = "{\"operators\":[{\"tempId\":\"op_4\",\"operatorType\":\"DeepLearning\",\"displayName\":\"线根检测\",\"parameters\":{\"Confidence\":\"0.5\"}}]}",
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            };

            var templateFilePath = Path.Combine(templateDirectory, "flow_templates.json");
            await File.WriteAllTextAsync(
                templateFilePath,
                JsonSerializer.Serialize(new[] { oldWireTemplate }, new JsonSerializerOptions { WriteIndented = true }));

            var service = new FlowTemplateService(tempRoot);
            var templates = await service.GetTemplatesAsync();
            var upgraded = templates.Single(item => item.ScenarioKey == "wire-sequence-terminal");

            upgraded.TemplateVersion.Should().Be("1.4.2");
            upgraded.ScenarioPackage.Should().NotBeNull();
            upgraded.ScenarioPackage!.PackageVersion.Should().Be("1.4.0");

            using var document = JsonDocument.Parse(upgraded.FlowJson);
            var deepLearningParams = document.RootElement.GetProperty("operators").EnumerateArray()
                .Single(item => item.GetProperty("tempId").GetString() == "op_2")
                .GetProperty("parameters");
            deepLearningParams.GetProperty("EnableInternalNms").GetString().Should().Be("false");
            deepLearningParams.GetProperty("Confidence").GetString().Should().Be("0.05");
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

    [Fact(DisplayName = "FlowTemplateService - 端子线序模板应对齐固定 ROI + BoxNms 主链")]
    public async Task FlowTemplateService_WireSequenceTemplate_ShouldUseAlignedSkeleton()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "clearvision-template-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new FlowTemplateService(tempRoot);

            var template = (await service.GetTemplatesAsync())
                .Single(item => item.Name == "端子线序检测");

            template.TemplateVersion.Should().Be("1.4.2");
            template.ScenarioPackage.Should().NotBeNull();
            template.ScenarioPackage!.PackageVersion.Should().Be("1.4.0");
            template.ScenarioPackage.RequiredResources.Should().Equal("DeepLearning.ModelPath");
            template.ScenarioPackage.AssetVersionIds.Should().Contain("template:terminal-wire-sequence-template@1.4.0");
            template.ScenarioPackage.AssetVersionIds.Should().Contain("model:wire-seq-yolo@1.2.0");
            template.ScenarioPackage.AssetVersionIds.Should().Contain("rule:wire-sequence-rule@1.4.0");

            using var document = JsonDocument.Parse(template.FlowJson);
            var root = document.RootElement;
            root.GetProperty("expectedSequence").EnumerateArray().Select(item => item.GetString())
                .Should().Equal("Wire_Black", "Wire_Blue");
            root.GetProperty("requiredResources").EnumerateArray().Select(item => item.GetString())
                .Should().Equal("DeepLearning.ModelPath");
            root.GetProperty("tunableParameters").EnumerateArray().Select(item => item.GetString())
                .Should().Equal("BoxNms.ScoreThreshold", "BoxNms.IouThreshold");

            var operatorTypes = root.GetProperty("operators").EnumerateArray()
                .Select(item => item.GetProperty("operatorType").GetString())
                .ToList();
            operatorTypes.Should().Equal(
                "ImageAcquisition",
                "DeepLearning",
                "BoxFilter",
                "BoxNms",
                "DetectionSequenceJudge",
                "ResultOutput");
            operatorTypes.Should().NotContain("ConditionalBranch");
            operatorTypes.Should().NotContain("ModbusCommunication");

            var connections = root.GetProperty("connections").EnumerateArray().ToList();
            connections.Should().Contain(item =>
                item.GetProperty("sourceTempId").GetString() == "op_2" &&
                item.GetProperty("sourcePortName").GetString() == "Objects" &&
                item.GetProperty("targetTempId").GetString() == "op_3" &&
                item.GetProperty("targetPortName").GetString() == "Detections");
            connections.Should().Contain(item =>
                item.GetProperty("sourceTempId").GetString() == "op_3" &&
                item.GetProperty("sourcePortName").GetString() == "Detections" &&
                item.GetProperty("targetTempId").GetString() == "op_4" &&
                item.GetProperty("targetPortName").GetString() == "Detections");
            connections.Should().Contain(item =>
                item.GetProperty("sourceTempId").GetString() == "op_2" &&
                item.GetProperty("sourcePortName").GetString() == "OriginalImage" &&
                item.GetProperty("targetTempId").GetString() == "op_4" &&
                item.GetProperty("targetPortName").GetString() == "SourceImage");
            connections.Should().Contain(item =>
                item.GetProperty("sourceTempId").GetString() == "op_4" &&
                item.GetProperty("sourcePortName").GetString() == "Diagnostics" &&
                item.GetProperty("targetTempId").GetString() == "op_6" &&
                item.GetProperty("targetPortName").GetString() == "Data");
            connections.Should().Contain(item =>
                item.GetProperty("sourceTempId").GetString() == "op_5" &&
                item.GetProperty("sourcePortName").GetString() == "Diagnostics" &&
                item.GetProperty("targetTempId").GetString() == "op_6" &&
                item.GetProperty("targetPortName").GetString() == "Result");
            connections.Should().Contain(item =>
                item.GetProperty("sourceTempId").GetString() == "op_5" &&
                item.GetProperty("sourcePortName").GetString() == "Message" &&
                item.GetProperty("targetTempId").GetString() == "op_6" &&
                item.GetProperty("targetPortName").GetString() == "Text");

            var boxFilterParams = root.GetProperty("operators").EnumerateArray()
                .Single(item => item.GetProperty("tempId").GetString() == "op_3")
                .GetProperty("parameters");
            boxFilterParams.GetProperty("FilterMode").GetString().Should().Be("Region");
            boxFilterParams.GetProperty("RegionW").GetString().Should().Be("999999");
            boxFilterParams.GetProperty("RegionH").GetString().Should().Be("999999");

            var boxNmsParams = root.GetProperty("operators").EnumerateArray()
                .Single(item => item.GetProperty("tempId").GetString() == "op_4")
                .GetProperty("parameters");
            boxNmsParams.GetProperty("ShowSuppressed").GetString().Should().Be("false");

            var judgeParams = root.GetProperty("operators").EnumerateArray()
                .Single(item => item.GetProperty("tempId").GetString() == "op_5")
                .GetProperty("parameters");
            judgeParams.GetProperty("ExpectedLabels").GetString().Should().Be("Wire_Black,Wire_Blue");
            judgeParams.GetProperty("SortBy").GetString().Should().Be("CenterY");
            judgeParams.GetProperty("Direction").GetString().Should().Be("TopToBottom");

            var deepLearningParams = root.GetProperty("operators").EnumerateArray()
                .Single(item => item.GetProperty("tempId").GetString() == "op_2")
                .GetProperty("parameters");
            deepLearningParams.GetProperty("EnableInternalNms").GetString().Should().Be("false");
            deepLearningParams.GetProperty("Confidence").GetString().Should().Be("0.05");
            deepLearningParams.GetProperty("LabelsPath").GetString().Should().BeEmpty();

            judgeParams.GetProperty("MinConfidence").GetString().Should().Be("0.0");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact(DisplayName = "FlowTemplateService - 应支持显式创建与更新模板")]
    public async Task FlowTemplateService_CreateAndUpdateTemplateAsync_ShouldWork()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "clearvision-template-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new FlowTemplateService(tempRoot);
            var created = await service.CreateTemplateAsync(new FlowTemplate
            {
                Name = "模板A",
                Description = "初始描述",
                Industry = "通用制造",
                Tags = new List<string> { "线序" },
                FlowJson = "{}"
            });

            created.Id.Should().NotBe(Guid.Empty);

            var updated = await service.UpdateTemplateAsync(created.Id, new FlowTemplate
            {
                Name = "模板A-更新",
                Description = "更新描述",
                Industry = "连接器",
                Tags = new List<string> { "线序", "端子" },
                FlowJson = """{"operators":[],"connections":[]}"""
            });

            updated.Should().NotBeNull();
            updated!.Name.Should().Be("模板A-更新");

            var loaded = await service.GetTemplateAsync(created.Id);
            loaded.Should().NotBeNull();
            loaded!.Description.Should().Be("更新描述");
            loaded.Tags.Should().Contain("端子");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact(DisplayName = "FlowTemplateService - 读取损坏模板文件时应保留损坏副本并恢复默认模板")]
    public async Task FlowTemplateService_LoadCorruptedFile_ShouldBackupAndRecover()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "clearvision-template-test-" + Guid.NewGuid().ToString("N"));
        var templateDir = Path.Combine(tempRoot, "templates");
        var templateFile = Path.Combine(templateDir, "flow_templates.json");
        const string corruptedContent = "{not-valid-json";

        try
        {
            Directory.CreateDirectory(templateDir);
            File.WriteAllText(templateFile, corruptedContent);

            var service = new FlowTemplateService(tempRoot);
            var templates = await service.GetTemplatesAsync();

            templates.Should().NotBeEmpty();
            var corruptedCopies = Directory.GetFiles(templateDir, "flow_templates.corrupted.*.json");
            corruptedCopies.Should().ContainSingle();
            File.ReadAllText(corruptedCopies[0]).Should().Be(corruptedContent);

            var repairedJson = File.ReadAllText(templateFile);
            Action parse = () => JsonDocument.Parse(repairedJson);
            parse.Should().NotThrow();
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact(DisplayName = "FlowTemplateService - 保存模板后不应残留临时文件且主文件保持可解析")]
    public async Task FlowTemplateService_SaveTemplateAsync_ShouldCleanupTempFilesAndKeepValidJson()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "clearvision-template-test-" + Guid.NewGuid().ToString("N"));
        var templateDir = Path.Combine(tempRoot, "templates");
        var templateFile = Path.Combine(templateDir, "flow_templates.json");

        try
        {
            var service = new FlowTemplateService(tempRoot);
            await service.SaveTemplateAsync(new FlowTemplate
            {
                Name = "原子写测试",
                Description = "测试临时文件清理",
                Industry = "通用制造",
                Tags = new List<string> { "测试" },
                FlowJson = """{"operators":[],"connections":[]}"""
            });

            File.Exists(templateFile).Should().BeTrue();
            var tempFiles = Directory.GetFiles(templateDir, "*.tmp");
            tempFiles.Should().BeEmpty();

            var json = File.ReadAllText(templateFile);
            Action parse = () => JsonDocument.Parse(json);
            parse.Should().NotThrow();
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
