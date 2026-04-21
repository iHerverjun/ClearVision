using Acme.Product.Core.DTOs;
using Acme.Product.Infrastructure.AI;
using FluentAssertions;

namespace Acme.Product.Tests.AI;

public class ConversationalFlowServiceTests : IDisposable
{
    private readonly string _tempRoot;

    public ConversationalFlowServiceTests()
    {
        _tempRoot = Path.Combine(
            Path.GetTempPath(),
            "clearvision-conversation-history-test-" + Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public void ListSessions_ReturnsOrderedSummaries()
    {
        var service = new ConversationalFlowService(_tempRoot);

        var firstContext = service.PrepareContext(new AiFlowGenerationRequest(
            "创建第一条流程",
            SessionId: "session-a"));
        service.RecordAssistantResponse(firstContext.SessionId, "assistant first", "{\"explanation\":\"first\"}");

        Thread.Sleep(25);

        var secondContext = service.PrepareContext(new AiFlowGenerationRequest(
            "创建第二条流程",
            SessionId: "session-b"));
        service.RecordAssistantResponse(secondContext.SessionId, "assistant second", "{\"explanation\":\"second\"}");

        var sessions = service.ListSessions();

        sessions.Should().HaveCount(2);
        sessions[0].SessionId.Should().Be("session-b");
        sessions[1].SessionId.Should().Be("session-a");
        sessions[0].LastMessage.Should().Contain("assistant second");
        sessions[0].TurnCount.Should().Be(2);
        sessions[1].TurnCount.Should().Be(2);
    }

    [Fact]
    public void GetSession_ValidId_ReturnsFullData()
    {
        var service = new ConversationalFlowService(_tempRoot);

        var context = service.PrepareContext(new AiFlowGenerationRequest(
            "生成流程",
            SessionId: "session-detail"));

        const string latestFlowJson = "{\"explanation\":\"restored explanation\",\"operators\":[{\"displayName\":\"Threshold\"}] }";
        const string latestCanvasFlowJson = "{\"operators\":[{\"id\":\"op-1\",\"type\":\"Thresholding\",\"name\":\"Threshold\",\"inputPorts\":[],\"outputPorts\":[]}],\"connections\":[]}";
        service.RecordAssistantResponse(context.SessionId, "assistant detail", latestFlowJson, latestCanvasFlowJson);

        var session = service.GetSession("session-detail");

        session.Should().NotBeNull();
        session!.SessionId.Should().Be("session-detail");
        session.History.Should().HaveCount(2);
        session.CurrentFlowJson.Should().Be(latestFlowJson);
        session.CurrentCanvasFlowJson.Should().Be(latestCanvasFlowJson);
    }

    [Fact]
    public void RecordAssistantResponse_WhenLatestFlowIsCanvasJson_ShouldPopulateCanvasSnapshot()
    {
        var service = new ConversationalFlowService(_tempRoot);

        var context = service.PrepareContext(new AiFlowGenerationRequest(
            "创建流程",
            SessionId: "session-canvas-only"));

        const string canvasJson = "{\"operators\":[{\"id\":\"op-1\",\"type\":\"ResultOutput\"}],\"connections\":[]}";
        service.RecordAssistantResponse(context.SessionId, "assistant canvas", canvasJson);

        var session = service.GetSession(context.SessionId);
        session.Should().NotBeNull();
        session!.CurrentFlowJson.Should().Be(canvasJson);
        session.CurrentCanvasFlowJson.Should().Be(canvasJson);
    }

    [Fact]
    public void GetSession_InvalidId_ReturnsNull()
    {
        var service = new ConversationalFlowService(_tempRoot);

        var session = service.GetSession("not-exists");

        session.Should().BeNull();
    }

    [Fact]
    public void DeleteSession_RemovesAndPersists()
    {
        var service = new ConversationalFlowService(_tempRoot);

        var context = service.PrepareContext(new AiFlowGenerationRequest(
            "创建待删除流程",
            SessionId: "session-delete"));
        service.RecordAssistantResponse(context.SessionId, "assistant delete", "{\"explanation\":\"delete\"}");

        service.DeleteSession("session-delete").Should().BeTrue();
        service.ListSessions().Should().NotContain(summary => summary.SessionId == "session-delete");

        var reloadedService = new ConversationalFlowService(_tempRoot);
        reloadedService.ListSessions().Should().NotContain(summary => summary.SessionId == "session-delete");
    }

    [Fact]
    public void TryBackfillCanvasFlowJson_ShouldPersistCanvasSnapshotForLegacySession()
    {
        var service = new ConversationalFlowService(_tempRoot);

        var context = service.PrepareContext(new AiFlowGenerationRequest(
            "恢复历史会话",
            SessionId: "session-legacy"));

        const string legacyAiRawJson = "{\"Explanation\":\"legacy\",\"Operators\":[{\"TempId\":\"op_1\",\"OperatorType\":\"ImageAcquisition\",\"DisplayName\":\"采集\",\"Parameters\":{}}],\"Connections\":[]}";
        service.RecordAssistantResponse(context.SessionId, "assistant legacy", legacyAiRawJson);

        const string canvasJson = "{\"operators\":[{\"id\":\"op-1\",\"type\":\"ImageAcquisition\",\"name\":\"采集\",\"inputPorts\":[],\"outputPorts\":[]}],\"connections\":[]}";
        service.TryBackfillCanvasFlowJson(context.SessionId, canvasJson).Should().BeTrue();

        var session = service.GetSession(context.SessionId);
        session.Should().NotBeNull();
        session!.CurrentCanvasFlowJson.Should().Be(canvasJson);

        var reloadedService = new ConversationalFlowService(_tempRoot);
        var reloaded = reloadedService.GetSession(context.SessionId);
        reloaded.Should().NotBeNull();
        reloaded!.CurrentCanvasFlowJson.Should().Be(canvasJson);
    }

    [Fact]
    public void PrepareContext_WithExplicitMode_ShouldPreferRequestedMode()
    {
        var service = new ConversationalFlowService(_tempRoot);

        var context = service.PrepareContext(new AiFlowGenerationRequest(
            "解释一下当前流程",
            SessionId: "session-explicit",
            Mode: GenerateFlowMode.Explain));

        context.Mode.Should().Be(GenerateFlowMode.Explain);
        context.Intent.Should().Be(ConversationIntent.Explain);
    }

    [Fact]
    public void PrepareContext_WithEmptyFlowPayload_ShouldTreatAsNewAndClearExistingFlow()
    {
        var service = new ConversationalFlowService(_tempRoot);

        service.PrepareContext(new AiFlowGenerationRequest(
            "先生成一个流程",
            SessionId: "session-empty-flow",
            ExistingFlowJson: """{"operators":[{"id":"op-1","type":"Thresholding"}],"connections":[]}"""));

        var context = service.PrepareContext(new AiFlowGenerationRequest(
            "修改一下",
            SessionId: "session-empty-flow",
            ExistingFlowJson: """{"operators":[],"connections":[]}""",
            Mode: GenerateFlowMode.Auto));

        context.Mode.Should().Be(GenerateFlowMode.New);
        context.Intent.Should().Be(ConversationIntent.New);
        context.ExistingFlowJson.Should().BeNull();
    }

    [Fact]
    public void PrepareContext_ShouldBuildSessionSummaryWithoutWorkflowJson()
    {
        var service = new ConversationalFlowService(_tempRoot);

        var first = service.PrepareContext(new AiFlowGenerationRequest(
            "创建流程",
            SessionId: "session-summary"));
        service.RecordAssistantResponse(
            first.SessionId,
            "已生成草案。\n```json\n{\"operators\":[1]}\n```",
            "{\"operators\":[{\"tempId\":\"op_1\"}],\"connections\":[]}");

        var second = service.PrepareContext(new AiFlowGenerationRequest(
            "继续优化参数",
            SessionId: "session-summary"));

        second.SessionSummary.Should().Contain("- user: 创建流程");
        second.SessionSummary.Should().Contain("- assistant: 已生成草案。");
        second.SessionSummary.Should().NotContain("tempId");
        second.SessionSummary.Should().NotContain("```");
    }

    [Fact]
    public void RecordAssistantResponse_ShouldPersistRichPayload()
    {
        var service = new ConversationalFlowService(_tempRoot);

        var context = service.PrepareContext(new AiFlowGenerationRequest(
            "修复 JSON 输出",
            SessionId: "session-rich-payload"));

        service.RecordAssistantResponse(
            context.SessionId,
            "本轮生成未通过结构校验，已生成纠错草稿，请确认后手动发送。",
            null,
            payload: new ConversationTurnPayload
            {
                Kind = "assistant_failure",
                Status = AiFlowGenerationResult.FailureTypeManualRetryRequired,
                Reply = "请确认后手动发送纠错草稿。",
                Reasoning = "模型输出缺少 ResultOutput 参数。",
                Progress = ["正在分析需求", "正在校验生成结果"],
                Failure = new ConversationTurnFailurePayload
                {
                    Summary = "缺少关键参数",
                    FailureSummary = new AiFailureSummary
                    {
                        Category = "validation",
                        Code = "missing_parameter",
                        Message = "缺少关键参数",
                        RepairTarget = "补齐 ResultOutput 的输入参数",
                        LastOutputSummary = "最近一次输出缺少 ResultOutput 参数"
                    },
                    Diagnostics =
                    [
                        new AiAttemptDiagnostic
                        {
                            AttemptNumber = 1,
                            Stage = "validation",
                            Summary = "缺少关键参数"
                        }
                    ]
                },
                ManualRetry = new AiManualRetryInfo
                {
                    Required = true,
                    Stage = "validation",
                    Draft = "请仅补齐缺失参数后返回 JSON。",
                    Summary = "缺少关键参数",
                    RepairTarget = "补齐 ResultOutput 的输入参数",
                    LastOutputSummary = "最近一次输出缺少 ResultOutput 参数"
                }
            });

        var reloadedService = new ConversationalFlowService(_tempRoot);
        var session = reloadedService.GetSession(context.SessionId);

        session.Should().NotBeNull();
        var assistantTurn = session!.History.Last();
        assistantTurn.Payload.Should().NotBeNull();
        assistantTurn.Payload!.Kind.Should().Be("assistant_failure");
        assistantTurn.Payload.Status.Should().Be(AiFlowGenerationResult.FailureTypeManualRetryRequired);
        assistantTurn.Payload.Progress.Should().ContainInOrder("正在分析需求", "正在校验生成结果");
        assistantTurn.Payload.Failure.Should().NotBeNull();
        assistantTurn.Payload.Failure!.FailureSummary!.Code.Should().Be("missing_parameter");
        assistantTurn.Payload.ManualRetry.Should().NotBeNull();
        assistantTurn.Payload.ManualRetry!.Required.Should().BeTrue();
        assistantTurn.Payload.ManualRetry.Stage.Should().Be("validation");
        assistantTurn.Payload.ManualRetry.Draft.Should().Be("请仅补齐缺失参数后返回 JSON。");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
