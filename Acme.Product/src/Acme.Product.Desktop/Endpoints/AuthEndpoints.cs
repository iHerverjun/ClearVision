// AuthEndpoints.cs
// 从请求头获取 Token
// 作者：蘅芜君

using Acme.Product.Application.DTOs;
using Acme.Product.Application.Services;
using Acme.Product.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Acme.Product.Desktop.Endpoints;

/// <summary>
/// 认证 API 端点
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // 登录 - 公开
        app.MapPost("/api/auth/login", async (LoginRequest request, IAuthService authService) =>
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.BadRequest(new { Error = "用户名和密码不能为空" });
            }

            var result = await authService.LoginAsync(request.Username, request.Password);
            
            if (!result.Success)
            {
                return Results.Json(
                    new { Error = result.ErrorMessage ?? "用户名或密码错误" },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            return Results.Ok(new 
            { 
                Token = result.Token, 
                User = result.User 
            });
        })
        .AllowAnonymous();

        // 登出 - 需要认证
        app.MapPost("/api/auth/logout", async (HttpContext context, IAuthService authService) =>
        {
            var token = GetTokenFromHeader(context);
            if (!string.IsNullOrEmpty(token))
            {
                await authService.LogoutAsync(token);
            }
            return Results.Ok(new { Message = "已登出", Audit = "server-session-cleared" });
        });

        // 获取当前用户 - 需要认证
        app.MapGet("/api/auth/me", async (HttpContext context, IAuthService authService) =>
        {
            var token = GetTokenFromHeader(context);
            if (string.IsNullOrEmpty(token))
            {
                return Results.Unauthorized();
            }

            var session = await authService.GetSessionAsync(token);
            if (session == null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new 
            { 
                session.UserId, 
                session.Username, 
                session.Role 
            });
        });

        // 修改密码 - 需要认证
        app.MapPost("/api/auth/change-password", async (
            HttpContext context, 
            ChangePasswordRequest request, 
            IAuthService authService,
            IConfigurationService configService) =>
        {
            var token = GetTokenFromHeader(context);
            if (string.IsNullOrEmpty(token))
            {
                return Results.Unauthorized();
            }

            var session = await authService.GetSessionAsync(token);
            if (session == null)
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(request.OldPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return Results.BadRequest(new { ErrorCode = "EMPTY_PASSWORD", Error = "密码不能为空" });
            }

            var minPasswordLength = Math.Max(6, configService.GetCurrent()?.Security?.PasswordMinLength ?? 6);
            if (request.NewPassword.Trim().Length < minPasswordLength)
            {
                return Results.BadRequest(new { ErrorCode = "WEAK_PASSWORD", Error = $"新密码长度不能少于 {minPasswordLength} 位" });
            }

            var result = await authService.ChangePasswordAsync(
                session.UserId, 
                request.OldPassword, 
                request.NewPassword);

            if (!result.Success)
            {
                return Results.BadRequest(new
                {
                    ErrorCode = ResolveChangePasswordErrorCode(result.ErrorMessage),
                    Error = result.ErrorMessage
                });
            }

            return Results.Ok(new { Message = "密码修改成功" });
        });

        return app;
    }

    /// <summary>
    /// 从请求头获取 Token
    /// </summary>
    private static string? GetTokenFromHeader(HttpContext context)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader.Substring("Bearer ".Length).Trim();
        }
        return null;
    }

    private static string ResolveChangePasswordErrorCode(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return "CHANGE_PASSWORD_FAILED";
        }

        if (errorMessage.Contains("当前密码错误", StringComparison.Ordinal))
        {
            return "INVALID_OLD_PASSWORD";
        }

        if (errorMessage.Contains("不能与当前密码相同", StringComparison.Ordinal))
        {
            return "PASSWORD_REUSE";
        }

        if (errorMessage.Contains("长度不能少于", StringComparison.Ordinal) ||
            errorMessage.Contains("必须同时包含大写字母、小写字母和数字", StringComparison.Ordinal))
        {
            return "WEAK_PASSWORD";
        }

        if (errorMessage.Contains("用户不存在", StringComparison.Ordinal))
        {
            return "USER_NOT_FOUND";
        }

        if (errorMessage.Contains("密码不能为空", StringComparison.Ordinal))
        {
            return "EMPTY_PASSWORD";
        }

        return "CHANGE_PASSWORD_FAILED";
    }
}
