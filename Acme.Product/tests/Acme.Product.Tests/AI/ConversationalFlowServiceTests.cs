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

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
