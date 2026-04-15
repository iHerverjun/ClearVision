using System.Net;
using System.Text.Json;
using Acme.Product.Core.Cameras;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Interfaces;
using Acme.Product.Desktop.Endpoints;
using Acme.Product.Infrastructure.AI;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Acme.Product.Desktop.Tests;

public class CameraBindingsEndpointTests
{
    [Fact]
    public async Task GetCameraBindings_ShouldProjectRuntimeConnectionStatus()
    {
        var connectedCamera = Substitute.For<ICamera>();
        connectedCamera.IsConnected.Returns(true);

        var cameraManager = Substitute.For<ICameraManager>();
        cameraManager.GetBindings().Returns(new List<CameraBindingConfig>
        {
            new() { Id = "cam-connected", DisplayName = "Connected", SerialNumber = "SN-CONNECTED", IpAddress = "10.0.0.1", IsEnabled = true, TriggerMode = "Hardware" },
            new() { Id = "cam-online", DisplayName = "Online", SerialNumber = "SN-ONLINE", IpAddress = "10.0.0.2", IsEnabled = true },
            new() { Id = "cam-offline", DisplayName = "Offline", SerialNumber = "SN-OFFLINE", IpAddress = "10.0.0.3", IsEnabled = true },
            new() { Id = "cam-disabled", DisplayName = "Disabled", SerialNumber = "SN-DISABLED", IpAddress = "10.0.0.4", IsEnabled = false },
            new() { Id = "cam-unbound", DisplayName = "Unbound", SerialNumber = "", IpAddress = "", IsEnabled = true }
        });
        cameraManager.GetCamera("SN-CONNECTED").Returns(connectedCamera);
        cameraManager.GetCamera("SN-ONLINE").Returns((ICamera?)null);
        cameraManager.GetCamera("SN-OFFLINE").Returns((ICamera?)null);
        cameraManager.GetCamera("SN-DISABLED").Returns((ICamera?)null);
        cameraManager.EnumerateCamerasAsync().Returns(Task.FromResult<IEnumerable<CameraInfo>>(new[]
        {
            new CameraInfo { CameraId = "SN-ONLINE", Name = "Discovered Online", IsConnected = false }
        }));

        await using var host = await CameraBindingsTestHost.CreateAsync(cameraManager);
        using var response = await host.Client.GetAsync("/api/cameras/bindings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = document.RootElement.EnumerateArray().ToList();
        items.Should().HaveCount(5);

        GetProperty(FindById(items, "cam-connected"), "ConnectionStatus", "connectionStatus").GetString().Should().Be("Connected");
        GetProperty(FindById(items, "cam-connected"), "DeviceId", "deviceId").GetString().Should().Be("SN-CONNECTED");
        GetProperty(FindById(items, "cam-connected"), "IpAddress", "ipAddress").GetString().Should().Be("10.0.0.1");
        GetProperty(FindById(items, "cam-connected"), "TriggerMode", "triggerMode").GetString().Should().Be("External");
        GetProperty(FindById(items, "cam-connected"), "TargetFrameRateFps", "targetFrameRateFps").GetInt32().Should().Be(10);
        GetProperty(FindById(items, "cam-online"), "ConnectionStatus", "connectionStatus").GetString().Should().Be("Online");
        GetProperty(FindById(items, "cam-offline"), "ConnectionStatus", "connectionStatus").GetString().Should().Be("Offline");
        GetProperty(FindById(items, "cam-disabled"), "ConnectionStatus", "connectionStatus").GetString().Should().Be("Disabled");
        GetProperty(FindById(items, "cam-unbound"), "ConnectionStatus", "connectionStatus").GetString().Should().Be("Unbound");
    }

    private static JsonElement FindById(List<JsonElement> items, string id)
    {
        return items.Single(item => GetProperty(item, "Id", "id").GetString() == id);
    }

    private static JsonElement GetProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out var property))
            {
                return property;
            }
        }

        throw new KeyNotFoundException(string.Join(", ", propertyNames));
    }

    private sealed class CameraBindingsTestHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private CameraBindingsTestHost(WebApplication app)
        {
            _app = app;
            Client = app.GetTestClient();
        }

        public HttpClient Client { get; }

        public static async Task<CameraBindingsTestHost> CreateAsync(ICameraManager cameraManager)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });

            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton(cameraManager);
            builder.Services.AddSingleton(Substitute.For<ICameraFrameStreamCoordinator>());

            var configService = Substitute.For<IConfigurationService>();
            configService.LoadAsync().Returns(Task.FromResult(new AppConfig()));
            configService.GetCurrent().Returns(new AppConfig());
            configService.SaveAsync(Arg.Any<AppConfig>()).Returns(Task.CompletedTask);
            builder.Services.AddSingleton(configService);

            var aiConfigStore = new AiConfigStore(
                Options.Create(new AiGenerationOptions
                {
                    Provider = "openai",
                    Model = "gpt-4o-mini",
                    ApiKey = "test-key"
                }),
                NullLogger<AiConfigStore>.Instance);
            builder.Services.AddSingleton(aiConfigStore);
            builder.Services.AddSingleton(new AiApiClient(new HttpClient(), aiConfigStore));

            var app = builder.Build();
            app.MapSettingsEndpoints();
            await app.StartAsync();
            return new CameraBindingsTestHost(app);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
