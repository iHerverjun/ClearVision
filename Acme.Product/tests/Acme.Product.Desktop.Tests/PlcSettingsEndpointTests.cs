using System.Net;
using System.Text;
using System.Text.Json;
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

public class PlcSettingsEndpointTests
{
    [Fact]
    public async Task GetPlcSettings_ShouldNormalizeLegacyFlatCommunicationConfig()
    {
        var legacyConfig = new AppConfig
        {
            Communication = new CommunicationConfig
            {
                Protocol = "MC",
                PlcIpAddress = "192.168.3.9",
                PlcPort = 5002,
                Mappings = new List<PlcAddressMapping>
                {
                    new() { Name = "Trigger", Address = "D100", DataType = "Word", CanWrite = false }
                }
            }
        };

        await using var host = await PlcSettingsTestHost.CreateAsync(legacyConfig);
        using var response = await host.Client.GetAsync("/api/plc/settings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        var settings = document.RootElement.GetProperty("settings");
        settings.GetProperty("activeProtocol").GetString().Should().Be("MC");
        settings.GetProperty("mc").GetProperty("ipAddress").GetString().Should().Be("192.168.3.9");
        settings.GetProperty("mc").GetProperty("port").GetInt32().Should().Be(5002);
        settings.GetProperty("mc").GetProperty("mappings").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task PutPlcSettings_ShouldRejectInvalidMappingsWithoutPersisting()
    {
        await using var host = await PlcSettingsTestHost.CreateAsync(new AppConfig());
        var payload = new
        {
            activeProtocol = "S7",
            heartbeatIntervalMs = 1000,
            s7 = new
            {
                ipAddress = "192.168.0.10",
                port = 102,
                cpuType = "S7-1200",
                rack = 0,
                slot = 1,
                mappings = new[]
                {
                    new { name = "Start", address = "BAD", dataType = "Bool", description = "", canWrite = false },
                    new { name = "Start", address = "DB1.DBX0.0", dataType = "Int16", description = "", canWrite = false }
                }
            },
            mc = new
            {
                ipAddress = "192.168.3.1",
                port = 5002,
                mappings = Array.Empty<object>()
            },
            fins = new
            {
                ipAddress = "192.168.250.1",
                port = 9600,
                mappings = Array.Empty<object>()
            }
        };

        using var response = await host.Client.PutAsync(
            "/api/plc/settings",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        document.RootElement.GetProperty("errors").GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
        await host.ConfigurationService.DidNotReceive().SaveAsync(Arg.Any<AppConfig>());
    }

    [Fact]
    public async Task PutPlcSettings_ShouldPersistNormalizedCommunicationProfile()
    {
        await using var host = await PlcSettingsTestHost.CreateAsync(new AppConfig());
        var payload = new
        {
            activeProtocol = "FINS",
            heartbeatIntervalMs = 1200,
            s7 = new
            {
                ipAddress = "192.168.0.1",
                port = 102,
                cpuType = "S7-1200",
                rack = 0,
                slot = 1,
                mappings = Array.Empty<object>()
            },
            mc = new
            {
                ipAddress = "192.168.3.1",
                port = 5002,
                mappings = Array.Empty<object>()
            },
            fins = new
            {
                ipAddress = "192.168.250.99",
                port = 9600,
                mappings = new[]
                {
                    new { name = "Ready", address = "DM100", dataType = "Word", description = "ready", canWrite = false }
                }
            }
        };

        using var response = await host.Client.PutAsync(
            "/api/plc/settings",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("settings").GetProperty("activeProtocol").GetString().Should().Be("FINS");
        document.RootElement.GetProperty("settings").GetProperty("fins").GetProperty("ipAddress").GetString().Should().Be("192.168.250.99");

        await host.ConfigurationService.Received(1).SaveAsync(Arg.Is<AppConfig>(config =>
            config.Communication.ActiveProtocol == "FINS"
            && config.Communication.Fins.IpAddress == "192.168.250.99"
            && config.Communication.Fins.Mappings.Count == 1
            && config.Communication.Fins.Mappings[0].Address == "DM100"));
    }

    [Theory]
    [InlineData("S7", "S7-1200", 0, 1)]
    [InlineData("MC", null, null, null)]
    [InlineData("FINS", null, null, null)]
    public async Task PostTestConnection_ShouldDispatchSupportedProtocols(
        string protocol,
        string? cpuType,
        int? rack,
        int? slot)
    {
        await using var host = await PlcSettingsTestHost.CreateAsync(new AppConfig());
        var payload = new
        {
            protocol,
            ipAddress = "127.0.0.1",
            port = 65500,
            cpuType,
            rack,
            slot
        };

        using var response = await host.Client.PostAsync(
            "/api/plc/test-connection",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        document.RootElement.GetProperty("protocol").GetString().Should().Be(protocol);
    }

    private sealed class PlcSettingsTestHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private PlcSettingsTestHost(WebApplication app, IConfigurationService configurationService)
        {
            _app = app;
            ConfigurationService = configurationService;
            Client = app.GetTestClient();
        }

        public HttpClient Client { get; }

        public IConfigurationService ConfigurationService { get; }

        public static async Task<PlcSettingsTestHost> CreateAsync(AppConfig initialConfig)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });

            builder.WebHost.UseTestServer();

            var configService = Substitute.For<IConfigurationService>();
            configService.LoadAsync().Returns(Task.FromResult(initialConfig));
            configService.GetCurrent().Returns(initialConfig);
            configService.SaveAsync(Arg.Any<AppConfig>()).Returns(Task.CompletedTask);
            builder.Services.AddSingleton(configService);

            var app = builder.Build();
            app.MapPlcEndpoints();
            await app.StartAsync();
            return new PlcSettingsTestHost(app, configService);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
