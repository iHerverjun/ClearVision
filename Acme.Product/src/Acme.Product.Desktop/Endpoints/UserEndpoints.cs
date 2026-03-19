// UserEndpoints.cs
// 重置密码请求
// 作者：蘅芜君

using Acme.Product.Application.DTOs;
using Acme.Product.Application.Services;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Acme.Product.Desktop.Endpoints;

/// <summary>
/// 用户管理 API 端点 - 仅 Admin 可访问
/// </summary>
public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        // 获取所有用户 - Admin
        app.MapGet("/api/users", async (UserManagementService userService, HttpContext context) =>
        {
            if (!await IsAdminAsync(context))
            {
                return Results.Forbid();
            }

            var users = await userService.GetAllUsersAsync();
            return Results.Ok(users);
        });

        // 获取单个用户 - Admin
        app.MapGet("/api/users/{id}", async (string id, UserManagementService userService, HttpContext context) =>
        {
            if (!await IsAdminAsync(context))
            {
                return Results.Forbid();
            }

            var user = await userService.GetUserByIdAsync(id);
            if (user == null)
            {
                return Results.NotFound(new { Error = $"用户 {id} 不存在" });
            }

            return Results.Ok(user);
        });

        // 创建用户 - Admin
        app.MapPost("/api/users", async (CreateUserRequest request, UserManagementService userService, HttpContext context, IConfigurationService configService) =>
        {
            if (!await IsAdminAsync(context))
            {
                return Results.Forbid();
            }

            var minPasswordLength = Math.Max(6, configService.GetCurrent()?.Security?.PasswordMinLength ?? 6);
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Trim().Length < minPasswordLength)
            {
                return Results.BadRequest(new { Error = $"初始密码长度不能少于 {minPasswordLength} 位" });
            }

            var result = await userService.CreateUserAsync(request);
            
            if (!result.Success)
            {
                return Results.BadRequest(new { Error = result.ErrorMessage });
            }

            return Results.Created($"/api/users/{result.User!.Id}", result.User);
        });

        // 更新用户 - Admin
        app.MapPut("/api/users/{id}", async (
            string id, 
            UpdateUserRequest request, 
            UserManagementService userService, 
            HttpContext context) =>
        {
            if (!await IsAdminAsync(context))
            {
                return Results.Forbid();
            }

            var result = await userService.UpdateUserAsync(id, request);
            
            if (!result.Success)
            {
                if (result.ErrorMessage?.Contains("不存在") == true)
                {
                    return Results.NotFound(new { Error = result.ErrorMessage });
                }
                return Results.BadRequest(new { Error = result.ErrorMessage });
            }

            return Results.Ok(result.User);
        });

        // 删除用户 - Admin
        app.MapDelete("/api/users/{id}", async (string id, UserManagementService userService, HttpContext context) =>
        {
            if (!await IsAdminAsync(context))
            {
                return Results.Forbid();
            }

            var result = await userService.DeleteUserAsync(id);
            
            if (!result.Success)
            {
                if (result.ErrorMessage?.Contains("不存在") == true)
                {
                    return Results.NotFound(new { Error = result.ErrorMessage });
                }
                return Results.BadRequest(new { Error = result.ErrorMessage });
            }

            return Results.NoContent();
        });

        // 重置密码 - Admin
        app.MapPost("/api/users/{id}/reset-password", async (
            string id, 
            ResetPasswordRequest request, 
            UserManagementService userService, 
            HttpContext context,
            IConfigurationService configService) =>
        {
            if (!await IsAdminAsync(context))
            {
                return Results.Forbid();
            }

            var minPasswordLength = Math.Max(6, configService.GetCurrent()?.Security?.PasswordMinLength ?? 6);
            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Trim().Length < minPasswordLength)
            {
                return Results.BadRequest(new { Error = $"重置密码长度不能少于 {minPasswordLength} 位" });
            }

            var result = await userService.ResetPasswordAsync(id, request.NewPassword);
            
            if (!result.Success)
            {
                if (result.ErrorMessage?.Contains("不存在") == true)
                {
                    return Results.NotFound(new { Error = result.ErrorMessage });
                }
                return Results.BadRequest(new { Error = result.ErrorMessage });
            }

            return Results.Ok(new { Message = "密码重置成功" });
        });

        return app;
    }

    /// <summary>
    /// 检查当前用户是否为 Admin
    /// </summary>
    private static async Task<bool> IsAdminAsync(HttpContext context)
    {
        // 从 Items 中获取当前用户信息（由 AuthMiddleware 注入）
        if (context.Items.TryGetValue("CurrentUser", out var userObj) && userObj is UserSession user)
        {
            return user.Role == UserRole.Admin.ToString();
        }
        return false;
    }
}

/// <summary>
/// 重置密码请求
/// </summary>
public class ResetPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}
