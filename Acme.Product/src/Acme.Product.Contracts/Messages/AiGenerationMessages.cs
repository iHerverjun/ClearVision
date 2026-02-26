namespace Acme.Product.Contracts.Messages;

/// <summary>
/// 前端 → 后端：请求 AI 生成工作流
/// </summary>
public record GenerateFlowRequest
{
    public string Type => "GenerateFlow";
    public GenerateFlowRequestPayload Payload { get; init; } = new();
}

public record GenerateFlowRequestPayload
{
    /// <summary>
    /// 用户输入的自然语言描述
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 可选：用户选择的模板/场景类型提示
    /// </summary>
    public string? Hint { get; init; }

    /// <summary>
    /// 可选：会话ID，用于多轮对话上下文
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// 可选：当前流程 JSON（用于增量修改/解释）
    /// </summary>
    public string? ExistingFlowJson { get; init; }
}

/// <summary>
/// 后端 → 前端：AI 生成结果
/// </summary>
public record GenerateFlowResponse
{
    public string Type => "GenerateFlowResult";
    public bool Success { get; init; }
    public object? Flow { get; init; }   // OperatorFlowDto 序列化后的对象
    public string? ErrorMessage { get; init; }
    public string? AiExplanation { get; init; }
    public string? Reasoning { get; init; }
    public Dictionary<string, List<string>>? ParametersNeedingReview { get; init; }
    public string? SessionId { get; init; }
    public string? DetectedIntent { get; init; }
    public object? DryRunResult { get; init; }
}


/// <summary>
/// 后端 → 前端：AI 生成进度更新（流式反馈）
/// </summary>
public record GenerateFlowProgress
{
    public string Type => "GenerateFlowProgress";
    public string Stage { get; init; } = string.Empty;  // "calling_ai" | "validating" | "layouting"
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// 后端 → 前端：AI 实时流式分块输出数据
/// </summary>
public record GenerateFlowStreamChunk
{
    public string Type => "GenerateFlowStreamChunk";

    /// <summary>
    /// 数据块类型: "thinking" | "content" | "done"
    /// </summary>
    public string ChunkType { get; init; } = string.Empty;

    /// <summary>
    /// 数据块文本内容
    /// </summary>
    public string Content { get; init; } = string.Empty;
}

/// <summary>
/// 内部流式块传递模型
/// </summary>
public record AiStreamChunk(string ChunkType, string Content);

public static class AiStreamChunkType
{
    public const string Thinking = "thinking";
    public const string Content = "content";
    public const string Done = "done";
}
