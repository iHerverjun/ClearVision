// WebMessage.cs
// 响应时间戳。
// 作者：蘅芜君

namespace Acme.Product.Contracts.Messages;

/// <summary>
/// WebView2 与宿主之间的基础消息类型。
/// 使用 C# 12 record 语法实现不可变消息。
/// </summary>
public sealed record WebMessage
{
    /// <summary>
    /// 消息类型，用于路由分发。
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// 消息唯一标识，用于请求-响应匹配。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 消息负载（JSON 序列化后的数据）。
    /// </summary>
    public string? Payload { get; init; }

    /// <summary>
    /// 消息时间戳。
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 响应消息类型。
/// </summary>
public sealed record WebMessageResponse
{
    /// <summary>
    /// 对应请求的消息 ID。
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>
    /// 是否操作成功。
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// 响应数据（成功时）。
    /// </summary>
    public string? Data { get; init; }

    /// <summary>
    /// 错误信息（失败时）。
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// 响应时间戳。
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
