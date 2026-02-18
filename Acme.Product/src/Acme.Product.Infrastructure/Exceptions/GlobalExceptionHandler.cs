// GlobalExceptionHandler.cs
// 错误响应
// 作者：蘅芜君

using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Exceptions;

/// <summary>
/// 全局异常处理程序
/// Sprint 5: S5-009 简化实现（基于日志）
/// </summary>
public static class GlobalExceptionHandler
{
    /// <summary>
    /// 处理异常并记录日志
    /// </summary>
    public static void HandleException(Exception exception, ILogger logger, string? context = null)
    {
        var traceId = Guid.NewGuid().ToString("N")[..8];
        var contextInfo = string.IsNullOrEmpty(context) ? "" : $"[{context}] ";
        
        var (category, message) = exception switch
        {
            ArgumentException _ => ("BadRequest", "请求参数无效"),
            InvalidOperationException _ => ("BadRequest", "操作无效"),
            KeyNotFoundException _ => ("NotFound", "资源不存在"),
            UnauthorizedAccessException _ => ("Unauthorized", "未授权访问"),
            TimeoutException _ => ("Timeout", "请求超时"),
            _ => ("InternalError", "服务器内部错误")
        };

        logger.LogError(exception, 
            "{Context}[TraceId: {TraceId}] [{Category}] {Message}", 
            contextInfo, traceId, category, exception.Message);
    }

    /// <summary>
    /// 创建标准化的错误响应
    /// </summary>
    public static ErrorResponse CreateErrorResponse(Exception exception)
    {
        var traceId = Guid.NewGuid().ToString("N")[..8];
        
        var (statusCode, message) = exception switch
        {
            ArgumentException _ => (400, "请求参数无效"),
            InvalidOperationException _ => (400, "操作无效"),
            KeyNotFoundException _ => (404, "资源不存在"),
            UnauthorizedAccessException _ => (401, "未授权访问"),
            TimeoutException _ => (408, "请求超时"),
            _ => (500, "服务器内部错误")
        };

        return new ErrorResponse
        {
            StatusCode = statusCode,
            Message = message,
            TraceId = traceId,
            Timestamp = DateTime.UtcNow,
            Detail = exception.Message
        };
    }
}

/// <summary>
/// 错误响应
/// </summary>
public class ErrorResponse
{
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Detail { get; set; } = string.Empty;
}
