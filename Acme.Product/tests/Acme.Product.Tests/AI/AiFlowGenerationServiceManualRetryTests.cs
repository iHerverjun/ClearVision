using Acme.Product.Contracts.Messages;
using Acme.Product.Core.DTOs;
using Acme.Product.Core.Services;
using Acme.Product.Infrastructure.AI;
using Acme.Product.Infrastructure.AI.DryRun;
using Acme.Product.Infrastructure.AI.Runtime;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Acme.Product.Tests.AI;

public class AiFlowGenerationServiceManualRetryTests : IDisposable
{
    private readonly string _tempRoot;

    public AiFlowGenerationServiceManualRetryTests()
    {
        _tempRoot = Path.Combine(
            Path.GetTempPath(),
            "clearvision-ai-manual-retry-test-" + Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public async Task GenerateFlowAsync_InvalidJson_ShouldReturnManualRetryWithoutRetryingModel()
    {
        var connector = Substitute.For<IAiConnector>();
        connector.StreamCompleteAsync(
                Arg.Any<string>(),
                Arg.Any<List<ChatMessage>>(),
                Arg.Any<Action<AiStreamChunk>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AiCompletionResult
            {
                Content = "this is not json"
            }));

        var validator = Substitute.For<IAiFlowValidator>();
        var conversationService = new ConversationalFlowService(_tempRoot);
        var service = CreateService(connector, validator, conversationService);

        var result = await service.GenerateFlowAsync(new AiFlowGenerationRequest(
            "请生成一个基础检测流程",
            SessionId: "parse-manual-retry"));

        result.Success.Should().BeFalse();
        result.FailureType.Should().Be(AiFlowGenerationResult.FailureTypeManualRetryRequired);
        result.ManualRetry.Should().NotBeNull();
        result.ManualRetry!.Required.Should().BeTrue();
        result.ManualRetry.Stage.Should().Be("parse");
        result.ManualRetry.Draft.Should().Contain("请只返回一个完整且可解析的 JSON 对象");
        result.LastAttemptDiagnostics.Should().ContainSingle();
        result.LastAttemptDiagnostics[0].Stage.Should().Be("parse");

        await connector.Received(1).StreamCompleteAsync(
            Arg.Any<string>(),
            Arg.Any<List<ChatMessage>>(),
            Arg.Any<Action<AiStreamChunk>>(),
            Arg.Any<CancellationToken>());
        validator.DidNotReceive().Validate(Arg.Any<AiGeneratedFlowJson>());

        var session = conversationService.GetSession("parse-manual-retry");
        session.Should().NotBeNull();
        session!.History.Last().Payload.Should().NotBeNull();
        session.History.Last().Payload!.ManualRetry.Should().NotBeNull();
        session.History.Last().Payload!.ManualRetry!.Stage.Should().Be("parse");
    }

    [Fact]
    public async Task GenerateFlowAsync_InvalidStructure_ShouldReturnManualRetryWithoutRetryingModel()
    {
        var connector = Substitute.For<IAiConnector>();
        connector.StreamCompleteAsync(
                Arg.Any<string>(),
                Arg.Any<List<ChatMessage>>(),
                Arg.Any<Action<AiStreamChunk>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AiCompletionResult
            {
                Content = "{}"
            }));

        var validation = new AiValidationResult();
        validation.AddError(
            "缺少 ResultOutput 的必填输入参数",
            code: "missing_parameter",
            category: "validation",
            relatedFields: ["operators[0].parameters.Result"],
            repairHint: "请补齐 ResultOutput 的输入参数。");

        var validator = Substitute.For<IAiFlowValidator>();
        validator.Validate(Arg.Any<AiGeneratedFlowJson>()).Returns(validation);

        var conversationService = new ConversationalFlowService(_tempRoot);
        var service = CreateService(connector, validator, conversationService);

        var result = await service.GenerateFlowAsync(new AiFlowGenerationRequest(
            "请修正当前流程参数",
            SessionId: "validation-manual-retry"));

        result.Success.Should().BeFalse();
        result.FailureType.Should().Be(AiFlowGenerationResult.FailureTypeManualRetryRequired);
        result.ManualRetry.Should().NotBeNull();
        result.ManualRetry!.Stage.Should().Be("validation");
        result.LastAttemptDiagnostics.Should().ContainSingle();
        result.LastAttemptDiagnostics[0].Stage.Should().Be("validation");
        result.LastAttemptDiagnostics[0].Issues.Should().ContainSingle();
        result.LastAttemptDiagnostics[0].Issues[0].Code.Should().Be("missing_parameter");
        result.ManualRetry.Diagnostics.Should().ContainSingle();
        result.ManualRetry.RepairTarget.Should().Contain("ResultOutput");

        await connector.Received(1).StreamCompleteAsync(
            Arg.Any<string>(),
            Arg.Any<List<ChatMessage>>(),
            Arg.Any<Action<AiStreamChunk>>(),
            Arg.Any<CancellationToken>());
        validator.Received(1).Validate(Arg.Any<AiGeneratedFlowJson>());

        var session = conversationService.GetSession("validation-manual-retry");
        session.Should().NotBeNull();
        session!.History.Last().Payload.Should().NotBeNull();
        session.History.Last().Payload!.Failure.Should().NotBeNull();
        session.History.Last().Payload!.Failure!.Diagnostics.Should().ContainSingle();
        session.History.Last().Payload!.ManualRetry.Should().NotBeNull();
        session.History.Last().Payload!.ManualRetry!.Stage.Should().Be("validation");
    }

    private static AiFlowGenerationService CreateService(
        IAiConnector connector,
        IAiFlowValidator validator,
        IConversationalFlowService conversationService)
    {
        var modelSelector = Substitute.For<IAiModelSelector>();
        modelSelector.SelectGenerationModel().Returns(new AiModelConfig
        {
            Name = "Test Model",
            Provider = "OpenAI Compatible",
            Model = "test-model",
            TimeoutMs = 30_000
        });

        var connectorFactory = Substitute.For<IAiConnectorFactory>();
        connectorFactory.CreateConnector(Arg.Any<AiModelConfig>()).Returns(connector);

        var operatorFactory = Substitute.For<IOperatorFactory>();
        operatorFactory.GetAllMetadata().Returns(Array.Empty<OperatorMetadata>());

        var templateService = Substitute.For<IFlowTemplateService>();
        var flowExecutionService = Substitute.For<IFlowExecutionService>();
        var hostEnvironment = Substitute.For<IHostEnvironment>();
        hostEnvironment.EnvironmentName.Returns("Production");

        return new AiFlowGenerationService(
            new AiGenerationOrchestrator(modelSelector, connectorFactory),
            new PromptBuilder(operatorFactory),
            conversationService,
            validator,
            new AutoLayoutService(),
            operatorFactory,
            templateService,
            new DryRunService(flowExecutionService),
            hostEnvironment,
            Substitute.For<Microsoft.Extensions.Logging.ILogger<AiFlowGenerationService>>());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
