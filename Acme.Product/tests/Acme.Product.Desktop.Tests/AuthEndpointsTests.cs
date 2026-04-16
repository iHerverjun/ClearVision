using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Acme.Product.Application.DTOs;
using Acme.Product.Application.Services;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Interfaces;
using Acme.Product.Desktop.Endpoints;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Acme.Product.Desktop.Tests;

public class AuthEndpointsTests
{
    [Fact]
    public async Task SetupStatus_ShouldReturnCurrentInitialAdminRequirements()
    {
        await using var host = await AuthEndpointsTestHost.CreateAsync();
        host.AuthService.GetInitialAdminSetupStatusAsync().Returns(new InitialAdminSetupStatusResponse
        {
            RequiresInitialAdminSetup = true,
            UsernameMinLength = 3,
            PasswordMinLength = 8,
            RequiresUppercase = false,
            RequiresLowercase = false,
            RequiresDigit = false
        });

        using var response = await host.Client.GetAsync("/api/auth/setup-status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        GetProperty(document.RootElement, "requiresInitialAdminSetup").GetBoolean().Should().BeTrue();
        GetProperty(document.RootElement, "passwordMinLength").GetInt32().Should().Be(8);
        GetProperty(document.RootElement, "requiresUppercase").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task SetupAdmin_ShouldReturnTokenAndUser_WhenInitializationSucceeds()
    {
        await using var host = await AuthEndpointsTestHost.CreateAsync();
        host.AuthService.SetupInitialAdminAsync(Arg.Any<InitialAdminSetupRequest>())
            .Returns(AuthResult.Ok("setup-token", new UserDto
            {
                Id = Guid.NewGuid().ToString(),
                Username = "factory-admin",
                DisplayName = "factory-admin",
                Role = Acme.Product.Core.Enums.UserRole.Admin,
                IsActive = true
            }));

        using var response = await host.Client.PostAsJsonAsync("/api/auth/setup-admin", new InitialAdminSetupRequest
        {
            Username = "factory-admin",
            Password = "password1",
            ConfirmPassword = "password1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        GetProperty(document.RootElement, "Token").GetString().Should().Be("setup-token");
        GetProperty(GetProperty(document.RootElement, "User"), "Username").GetString().Should().Be("factory-admin");
    }

    [Fact]
    public async Task SetupAdmin_ShouldReturnConflict_WhenInitializationAlreadyCompleted()
    {
        await using var host = await AuthEndpointsTestHost.CreateAsync();
        host.AuthService.SetupInitialAdminAsync(Arg.Any<InitialAdminSetupRequest>())
            .Returns(AuthResult.Fail(AuthService.InitialAdminSetupAlreadyCompletedMessage));

        using var response = await host.Client.PostAsJsonAsync("/api/auth/setup-admin", new InitialAdminSetupRequest
        {
            Username = "factory-admin",
            Password = "password1",
            ConfirmPassword = "password1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        GetProperty(document.RootElement, "Error").GetString().Should().Be(AuthService.InitialAdminSetupAlreadyCompletedMessage);
    }

    [Fact]
    public async Task Logout_ShouldCallAuthServiceAndReturnAuditPayload()
    {
        await using var host = await AuthEndpointsTestHost.CreateAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token-123");

        using var response = await host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await host.AuthService.Received(1).LogoutAsync("token-123");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        GetProperty(document.RootElement, "Audit").GetString().Should().Be("server-session-cleared");
    }

    [Fact]
    public async Task ChangePassword_ShouldReturnWeakPasswordErrorCode_WhenServiceRejectsPolicy()
    {
        await using var host = await AuthEndpointsTestHost.CreateAsync();
        host.AuthService.GetSessionAsync("token-123").Returns(new UserSession
        {
            UserId = Guid.NewGuid().ToString(),
            Username = "tester",
            Role = "Engineer"
        });
        host.AuthService.ChangePasswordAsync(Arg.Any<string>(), "OldPwd1", "short1")
            .Returns(AuthResult.Fail("新密码长度不能少于 8 位"));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token-123");
        request.Content = JsonContent.Create(new ChangePasswordRequest
        {
            OldPassword = "OldPwd1",
            NewPassword = "short1"
        });

        using var response = await host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        GetProperty(document.RootElement, "ErrorCode").GetString().Should().Be("WEAK_PASSWORD");
    }

    [Fact]
    public async Task ChangePassword_ShouldReturnPasswordReuseErrorCode_WhenServiceRejectsReuse()
    {
        await using var host = await AuthEndpointsTestHost.CreateAsync();
        host.AuthService.GetSessionAsync("token-123").Returns(new UserSession
        {
            UserId = Guid.NewGuid().ToString(),
            Username = "tester",
            Role = "Engineer"
        });
        host.AuthService.ChangePasswordAsync(Arg.Any<string>(), "CurrentPwd1", "CurrentPwd1")
            .Returns(AuthResult.Fail("新密码不能与当前密码相同"));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token-123");
        request.Content = JsonContent.Create(new ChangePasswordRequest
        {
            OldPassword = "CurrentPwd1",
            NewPassword = "CurrentPwd1"
        });

        using var response = await host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        GetProperty(document.RootElement, "ErrorCode").GetString().Should().Be("PASSWORD_REUSE");
    }

    private static JsonElement GetProperty(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals(propertyName) || property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        throw new KeyNotFoundException(propertyName);
    }

    private sealed class AuthEndpointsTestHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private AuthEndpointsTestHost(WebApplication app, IAuthService authService)
        {
            _app = app;
            AuthService = authService;
            Client = app.GetTestClient();
        }

        public HttpClient Client { get; }

        public IAuthService AuthService { get; }

        public static async Task<AuthEndpointsTestHost> CreateAsync()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });

            builder.WebHost.UseTestServer();

            var authService = Substitute.For<IAuthService>();
            var configService = Substitute.For<IConfigurationService>();
            configService.GetCurrent().Returns(new AppConfig
            {
                Security = new SecurityConfig
                {
                    PasswordMinLength = 8
                }
            });

            builder.Services.AddSingleton(authService);
            builder.Services.AddSingleton(configService);

            var app = builder.Build();
            app.MapAuthEndpoints();
            await app.StartAsync();
            return new AuthEndpointsTestHost(app, authService);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
