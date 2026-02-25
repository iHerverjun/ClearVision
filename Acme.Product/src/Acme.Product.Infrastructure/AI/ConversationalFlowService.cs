using System.Collections.Concurrent;
using System.Text;
using Acme.Product.Core.DTOs;

namespace Acme.Product.Infrastructure.AI;

public enum ConversationIntent
{
    New,
    Modify,
    Explain
}

public sealed class ConversationTurn
{
    public string Role { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}

public sealed class ConversationSession
{
    public string SessionId { get; init; } = string.Empty;
    public string? CurrentFlowJson { get; set; }
    public List<ConversationTurn> History { get; } = new();
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ConversationContext
{
    public required string SessionId { get; init; }
    public required ConversationIntent Intent { get; init; }
    public string? ExistingFlowJson { get; init; }
    public string PromptContext { get; init; } = string.Empty;
}

public interface IConversationalFlowService
{
    ConversationSession GetOrCreateSession(string? sessionId);
    ConversationIntent DetectIntent(string userDescription, bool hasExistingFlow);
    ConversationContext PrepareContext(AiFlowGenerationRequest request);
    void RecordAssistantResponse(string sessionId, string assistantMessage, string? latestFlowJson);
}

public class ConversationalFlowService : IConversationalFlowService
{
    private static readonly string[] _modifyKeywords =
    [
        "改", "修改", "调整", "优化", "加", "增加", "新增", "补充", "删除", "删掉", "替换", "调大", "调小",
        "change", "update", "adjust", "add", "remove", "replace", "refine"
    ];

    private static readonly string[] _explainKeywords =
    [
        "解释", "为什么", "什么意思", "含义", "讲解", "说明", "原理", "思路",
        "explain", "why", "reason", "meaning"
    ];

    private static readonly string[] _newKeywords =
    [
        "新建", "重新", "从头", "重做", "另一个", "新的流程", "新流程",
        "new flow", "start over", "from scratch", "rebuild"
    ];

    private readonly ConcurrentDictionary<string, ConversationSession> _sessions =
        new(StringComparer.OrdinalIgnoreCase);

    public ConversationSession GetOrCreateSession(string? sessionId)
    {
        var normalizedSessionId = string.IsNullOrWhiteSpace(sessionId)
            ? Guid.NewGuid().ToString("N")
            : sessionId.Trim();

        return _sessions.GetOrAdd(normalizedSessionId, id => new ConversationSession
        {
            SessionId = id
        });
    }

    public ConversationIntent DetectIntent(string userDescription, bool hasExistingFlow)
    {
        if (!hasExistingFlow)
            return ConversationIntent.New;

        if (ContainsAny(userDescription, _newKeywords))
            return ConversationIntent.New;

        if (ContainsAny(userDescription, _explainKeywords))
            return ConversationIntent.Explain;

        if (ContainsAny(userDescription, _modifyKeywords))
            return ConversationIntent.Modify;

        // 存在上下文但未出现明确动词时，默认按增量修改处理
        return ConversationIntent.Modify;
    }

    public ConversationContext PrepareContext(AiFlowGenerationRequest request)
    {
        var session = GetOrCreateSession(request.SessionId);

        lock (session)
        {
            if (!string.IsNullOrWhiteSpace(request.ExistingFlowJson))
                session.CurrentFlowJson = request.ExistingFlowJson;

            session.History.Add(new ConversationTurn
            {
                Role = "user",
                Message = request.Description
            });

            TrimHistory(session);
            session.UpdatedAtUtc = DateTime.UtcNow;
        }

        var hasExistingFlow = !string.IsNullOrWhiteSpace(session.CurrentFlowJson);
        var intent = DetectIntent(request.Description, hasExistingFlow);

        if ((intent == ConversationIntent.Modify || intent == ConversationIntent.Explain) && !hasExistingFlow)
            intent = ConversationIntent.New;

        var context = BuildPromptContext(session, intent);
        return new ConversationContext
        {
            SessionId = session.SessionId,
            Intent = intent,
            ExistingFlowJson = session.CurrentFlowJson,
            PromptContext = context
        };
    }

    public void RecordAssistantResponse(string sessionId, string assistantMessage, string? latestFlowJson)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return;

        lock (session)
        {
            if (!string.IsNullOrWhiteSpace(latestFlowJson))
                session.CurrentFlowJson = latestFlowJson;

            session.History.Add(new ConversationTurn
            {
                Role = "assistant",
                Message = assistantMessage
            });

            TrimHistory(session);
            session.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    private static string BuildPromptContext(ConversationSession session, ConversationIntent intent)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"会话意图：{ToIntentLabel(intent)}");

        switch (intent)
        {
            case ConversationIntent.New:
                sb.AppendLine("请创建一个新的完整工作流。");
                break;
            case ConversationIntent.Modify:
                sb.AppendLine("请在当前工作流基础上做增量修改，优先保留未被明确要求修改的节点和连线。");
                break;
            case ConversationIntent.Explain:
                sb.AppendLine("用户希望理解当前工作流，请在不改变算子结构的前提下给出清晰 explanation。");
                break;
        }

        var historyToInject = session.History
            .OrderByDescending(turn => turn.TimestampUtc)
            .Take(6)
            .OrderBy(turn => turn.TimestampUtc)
            .ToList();

        if (historyToInject.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("最近对话历史：");
            foreach (var turn in historyToInject)
            {
                sb.AppendLine($"- {turn.Role}: {turn.Message}");
            }
        }

        return sb.ToString();
    }

    private static string ToIntentLabel(ConversationIntent intent) => intent switch
    {
        ConversationIntent.New => "NEW",
        ConversationIntent.Modify => "MODIFY",
        ConversationIntent.Explain => "EXPLAIN",
        _ => "NEW"
    };

    private static bool ContainsAny(string source, IEnumerable<string> keywords)
    {
        return keywords.Any(keyword => source.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static void TrimHistory(ConversationSession session)
    {
        const int maxHistory = 20;
        if (session.History.Count <= maxHistory)
            return;

        session.History.RemoveRange(0, session.History.Count - maxHistory);
    }
}
