using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
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
    public string Role { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ConversationSession
{
    public string SessionId { get; set; } = string.Empty;
    public string? CurrentFlowJson { get; set; }
    public List<ConversationTurn> History { get; set; } = new();
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ConversationSessionSummary
{
    public string SessionId { get; set; } = string.Empty;
    public string LastMessage { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
    public int TurnCount { get; set; }
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
    IReadOnlyList<ConversationSessionSummary> ListSessions();
    ConversationSession? GetSession(string sessionId);
    bool DeleteSession(string sessionId);
}

internal sealed class ConversationStore
{
    public List<ConversationSession> Sessions { get; set; } = new();
}

public class ConversationalFlowService : IConversationalFlowService
{
    private const int MaxHistory = 20;
    private const int MaxPromptHistory = 6;
    private const int MaxPersistedSessions = 200;
    private const int MaxLastMessagePreviewLength = 80;
    private static readonly TimeSpan SessionRetention = TimeSpan.FromDays(30);

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly string[] _modifyKeywords =
    [
        "改", "修改", "调整", "优化", "调优", "增加", "新增", "补充", "删除", "删掉", "替换", "调大", "调小",
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

    private readonly object _persistLock = new();
    private readonly string _storagePath;

    public ConversationalFlowService(string? storageRootPath = null)
    {
        var rootPath = string.IsNullOrWhiteSpace(storageRootPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClearVision")
            : storageRootPath;

        Directory.CreateDirectory(rootPath);
        _storagePath = Path.Combine(rootPath, "conversation_sessions.json");
        LoadSessionsFromStore();
    }

    public ConversationSession GetOrCreateSession(string? sessionId)
    {
        var normalizedSessionId = string.IsNullOrWhiteSpace(sessionId)
            ? Guid.NewGuid().ToString("N")
            : sessionId.Trim();

        return _sessions.GetOrAdd(normalizedSessionId, id => new ConversationSession
        {
            SessionId = id,
            UpdatedAtUtc = DateTime.UtcNow
        });
    }

    public ConversationIntent DetectIntent(string userDescription, bool hasExistingFlow)
    {
        var content = userDescription ?? string.Empty;

        if (!hasExistingFlow)
            return ConversationIntent.New;

        if (ContainsAny(content, _newKeywords))
            return ConversationIntent.New;

        if (ContainsAny(content, _explainKeywords))
            return ConversationIntent.Explain;

        if (ContainsAny(content, _modifyKeywords))
            return ConversationIntent.Modify;

        // 有上下文但未出现明确动词时，按增量修改处理。
        return ConversationIntent.Modify;
    }

    public ConversationContext PrepareContext(AiFlowGenerationRequest request)
    {
        var session = GetOrCreateSession(request.SessionId);
        ConversationIntent intent;
        string? existingFlowJson;
        string promptContext;

        lock (session)
        {
            if (!string.IsNullOrWhiteSpace(request.ExistingFlowJson))
                session.CurrentFlowJson = request.ExistingFlowJson;

            session.History.Add(new ConversationTurn
            {
                Role = "user",
                Message = request.Description,
                TimestampUtc = DateTime.UtcNow
            });

            TrimHistory(session);
            session.UpdatedAtUtc = DateTime.UtcNow;

            var hasExistingFlow = !string.IsNullOrWhiteSpace(session.CurrentFlowJson);
            intent = DetectIntent(request.Description, hasExistingFlow);
            if ((intent == ConversationIntent.Modify || intent == ConversationIntent.Explain) && !hasExistingFlow)
                intent = ConversationIntent.New;

            existingFlowJson = session.CurrentFlowJson;
            promptContext = BuildPromptContext(session, intent);
        }

        PersistSessions();
        return new ConversationContext
        {
            SessionId = session.SessionId,
            Intent = intent,
            ExistingFlowJson = existingFlowJson,
            PromptContext = promptContext
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
                Message = assistantMessage,
                TimestampUtc = DateTime.UtcNow
            });

            TrimHistory(session);
            session.UpdatedAtUtc = DateTime.UtcNow;
        }

        PersistSessions();
    }

    public IReadOnlyList<ConversationSessionSummary> ListSessions()
    {
        return _sessions.Values
            .Select(BuildSessionSummary)
            .OrderByDescending(summary => summary.UpdatedAtUtc)
            .ToList();
    }

    public ConversationSession? GetSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return null;

        var normalizedSessionId = sessionId.Trim();
        if (!_sessions.TryGetValue(normalizedSessionId, out var session))
            return null;

        return CloneSession(session);
    }

    public bool DeleteSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return false;

        var normalizedSessionId = sessionId.Trim();
        if (!_sessions.TryRemove(normalizedSessionId, out _))
            return false;

        PersistSessions();
        return true;
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
            .Take(MaxPromptHistory)
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
        if (session.History.Count <= MaxHistory)
            return;

        session.History.RemoveRange(0, session.History.Count - MaxHistory);
    }

    private static ConversationSessionSummary BuildSessionSummary(ConversationSession session)
    {
        lock (session)
        {
            var latestTurn = session.History
                .OrderByDescending(turn => turn.TimestampUtc)
                .FirstOrDefault();
            var latestMessage = latestTurn?.Message ?? string.Empty;
            if (latestMessage.Length > MaxLastMessagePreviewLength)
                latestMessage = latestMessage[..MaxLastMessagePreviewLength] + "...";

            return new ConversationSessionSummary
            {
                SessionId = session.SessionId,
                LastMessage = latestMessage,
                UpdatedAtUtc = session.UpdatedAtUtc,
                TurnCount = session.History.Count
            };
        }
    }

    private void LoadSessionsFromStore()
    {
        if (!File.Exists(_storagePath))
            return;

        try
        {
            var json = File.ReadAllText(_storagePath);
            var store = JsonSerializer.Deserialize<ConversationStore>(json, _jsonOptions);
            if (store?.Sessions == null || store.Sessions.Count == 0)
                return;

            var cutoff = DateTime.UtcNow - SessionRetention;
            foreach (var session in store.Sessions)
            {
                if (session == null || string.IsNullOrWhiteSpace(session.SessionId))
                    continue;

                NormalizeSession(session);
                if (session.UpdatedAtUtc < cutoff)
                    continue;

                _sessions[session.SessionId.Trim()] = session;
            }

            PruneInMemorySessions();
        }
        catch
        {
            // Ignore corrupted persistence file and start with empty in-memory sessions.
        }
    }

    private void PersistSessions()
    {
        lock (_persistLock)
        {
            try
            {
                PruneInMemorySessions();

                var snapshot = _sessions.Values
                    .Select(CloneSession)
                    .OrderByDescending(session => session.UpdatedAtUtc)
                    .Take(MaxPersistedSessions)
                    .ToList();

                var json = JsonSerializer.Serialize(new ConversationStore
                {
                    Sessions = snapshot
                }, _jsonOptions);

                File.WriteAllText(_storagePath, json);
            }
            catch
            {
                // Swallow persistence failure to avoid interrupting request flow.
            }
        }
    }

    private static ConversationSession CloneSession(ConversationSession session)
    {
        lock (session)
        {
            return new ConversationSession
            {
                SessionId = session.SessionId,
                CurrentFlowJson = session.CurrentFlowJson,
                UpdatedAtUtc = session.UpdatedAtUtc,
                History = session.History
                    .Select(turn => new ConversationTurn
                    {
                        Role = turn.Role,
                        Message = turn.Message,
                        TimestampUtc = turn.TimestampUtc
                    })
                    .ToList()
            };
        }
    }

    private static void NormalizeSession(ConversationSession session)
    {
        session.History ??= new List<ConversationTurn>();
        session.History = session.History
            .Where(turn => turn != null && !string.IsNullOrWhiteSpace(turn.Role))
            .OrderBy(turn => turn.TimestampUtc)
            .TakeLast(MaxHistory)
            .Select(turn => new ConversationTurn
            {
                Role = turn.Role,
                Message = turn.Message ?? string.Empty,
                TimestampUtc = turn.TimestampUtc == default ? DateTime.UtcNow : turn.TimestampUtc
            })
            .ToList();

        session.UpdatedAtUtc = session.UpdatedAtUtc == default ? DateTime.UtcNow : session.UpdatedAtUtc;
        session.SessionId = session.SessionId.Trim();
    }

    private void PruneInMemorySessions()
    {
        var cutoff = DateTime.UtcNow - SessionRetention;
        foreach (var kvp in _sessions.ToArray())
        {
            if (kvp.Value.UpdatedAtUtc < cutoff)
                _sessions.TryRemove(kvp.Key, out _);
        }

        if (_sessions.Count <= MaxPersistedSessions)
            return;

        var keepSessionIds = _sessions.Values
            .OrderByDescending(session => session.UpdatedAtUtc)
            .Take(MaxPersistedSessions)
            .Select(session => session.SessionId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var key in _sessions.Keys)
        {
            if (!keepSessionIds.Contains(key))
                _sessions.TryRemove(key, out _);
        }
    }
}
