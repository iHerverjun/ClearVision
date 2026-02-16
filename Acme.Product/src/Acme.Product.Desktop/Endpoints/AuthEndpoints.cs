using Acme.Product.Application.DTOs;
using Acme.Product.Application.Services;
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
                return Results.Unauthorized();
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
            return Results.Ok(new { Message = "已登出" });
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
            IAuthService authService) =>
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
                return Results.BadRequest(new { Error = "密码不能为空" });
            }

            var result = await authService.ChangePasswordAsync(
                session.UserId, 
                request.OldPassword, 
                request.NewPassword);

            if (!result.Success)
            {
                return Results.BadRequest(new { Error = result.ErrorMessage });
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
}
