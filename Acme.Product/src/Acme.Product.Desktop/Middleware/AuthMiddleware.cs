using Acme.Product.Application.Services;
using Acme.Product.Core.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Desktop.Middleware;

/// <summary>
/// 认证中间件 - Token 验证和角色注入
/// </summary>
public class AuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthMiddleware> _logger;

    // 白名单路径 - 不需要认证
    private static readonly string[] _whitelist = new[]
    {
        "/api/auth/login",
        "/health",
        "/",
        "/index.html",
        "/login.html"
    };

    // 静态文件路径前缀 - 不需要认证
    private static readonly string[] _staticFilePrefixes = new[]
    {
        "/src/",
        "/assets/",
        "/css/",
        "/js/",
        "/images/",
        "/fonts/"
    };

    public AuthMiddleware(RequestDelegate next, ILogger<AuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuthService authService)
    {
        var path = context.Request.Path.Value ?? "";

        // 检查是否是白名单路径
        if (IsWhitelisted(path))
        {
            await _next(context);
            return;
        }

        // 提取 Token
        var token = ExtractToken(context);
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("请求缺少认证令牌: {Path}", path);
            await WriteUnauthorizedResponse(context);
            return;
        }

        // 验证 Token
        var session = await authService.GetSessionAsync(token);
        if (session == null)
        {
            _logger.LogWarning("无效的认证令牌: {Path}", path);
            await WriteUnauthorizedResponse(context);
            return;
        }

        // 将用户信息注入 HttpContext.Items
        context.Items["CurrentUser"] = session;

        _logger.LogDebug("用户 {Username} ({Role}) 已认证 - {Path}", 
            session.Username, session.Role, path);

        await _next(context);
    }

    /// <summary>
    /// 检查路径是否在白名单中
    /// </summary>
    private static bool IsWhitelisted(string path)
    {
        // 完全匹配白名单
        if (_whitelist.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        // 静态文件前缀匹配
        foreach (var prefix in _staticFilePrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // API 文档和其他公开端点
        if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 从请求头或查询字符串提取 Token
    /// </summary>
    private static string? ExtractToken(HttpContext context)
    {
        // 1. 从 Authorization 头提取
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && 
            authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader.Substring("Bearer ".Length).Trim();
        }

        // 2. 从查询字符串提取（用于WebSocket等场景）
        if (context.Request.Query.TryGetValue("token", out var tokenValue))
        {
            return tokenValue.FirstOrDefault();
        }

        // 3. 从自定义头 X-Auth-Token 提取（备选方案）
        if (context.Request.Headers.TryGetValue("X-Auth-Token", out var customToken))
        {
            return customToken.FirstOrDefault();
        }

        return null;
    }

    /// <summary>
    /// 写入 401 未授权响应
    /// </summary>
    private static async Task WriteUnauthorizedResponse(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new 
        { 
            Error = "Unauthorized",
            Message = "请先登录" 
        });
    }
}

/// <summary>
/// 用户会话信息（简化版）
/// </summary>
public class UserSession
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
