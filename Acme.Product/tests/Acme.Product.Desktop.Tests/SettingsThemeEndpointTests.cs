using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

public class SettingsThemeEndpointTests
{
    [Fact]
    public async Task UpdateTheme_ShouldNormalizeThemeWithoutOverwritingOtherSettings()
    {
        var initialConfig = new AppConfig
        {
            General = new GeneralConfig
            {
                SoftwareTitle = "ClearVision",
                Theme = GeneralConfig.ThemeDark,
                AutoStart = true
            },
            Communication = new CommunicationConfig
            {
                ActiveProtocol = CommunicationConfig.ProtocolMc,
                HeartbeatIntervalMs = 4321,
                Mc = new PlcCommunicationProfile
                {
                    IpAddress = "10.0.0.8",
                    Port = 5002,
                    Mappings = new List<PlcAddressMapping>()
                }
            },
            Storage = new StorageConfig
            {
                ImageSavePath = @"D:\VisionData\Images",
                RetentionDays = 45,
                MinFreeSpaceGb = 9
            },
            Runtime = new RuntimeConfig
            {
                MissingMaterialTimeoutSeconds = 18
            },
            Security = new SecurityConfig
            {
                PasswordMinLength = 10
            },
            ActiveCameraId = "camera-001"
        };
        initialConfig.Normalize();

        await using var host = await SettingsThemeTestHost.CreateAsync(initialConfig);

        using var response = await host.Client.PutAsJsonAsync("/api/settings/theme", new ThemeUpdateRequest
        {
            Theme = " LIGHT "
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await host.ConfigurationService.Received(1).SaveAsync(Arg.Is<AppConfig>(config =>
            config.General != null &&
            config.General.Theme == GeneralConfig.ThemeLight &&
            config.General.SoftwareTitle == initialConfig.General.SoftwareTitle &&
            config.General.AutoStart == initialConfig.General.AutoStart &&
            config.Communication != null &&
            config.Communication.ActiveProtocol == initialConfig.Communication.ActiveProtocol &&
            config.Communication.HeartbeatIntervalMs == initialConfig.Communication.HeartbeatIntervalMs &&
            config.Storage != null &&
            config.Storage.RetentionDays == initialConfig.Storage.RetentionDays &&
            config.Storage.MinFreeSpaceGb == initialConfig.Storage.MinFreeSpaceGb &&
            config.Runtime != null &&
            config.Runtime.MissingMaterialTimeoutSeconds == initialConfig.Runtime.MissingMaterialTimeoutSeconds &&
            config.Security != null &&
            config.Security.PasswordMinLength == initialConfig.Security.PasswordMinLength &&
            config.ActiveCameraId == initialConfig.ActiveCameraId));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("theme").GetString().Should().Be(GeneralConfig.ThemeLight);
    }

    private sealed class SettingsThemeTestHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private SettingsThemeTestHost(WebApplication app, IConfigurationService configurationService)
        {
            _app = app;
            ConfigurationService = configurationService;
            Client = app.GetTestClient();
        }

        public HttpClient Client { get; }

        public IConfigurationService ConfigurationService { get; }

        public static async Task<SettingsThemeTestHost> CreateAsync(AppConfig initialConfig)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });

            builder.WebHost.UseTestServer();

            var configService = Substitute.For<IConfigurationService>();
            configService.LoadAsync().Returns(_ => Task.FromResult(CloneConfig(initialConfig)));
            configService.GetCurrent().Returns(CloneConfig(initialConfig));
            configService.SaveAsync(Arg.Any<AppConfig>()).Returns(Task.CompletedTask);
            builder.Services.AddSingleton(configService);
            builder.Services.AddSingleton(Substitute.For<Acme.Product.Core.Cameras.ICameraManager>());

            var aiConfigStore = new AiConfigStore(
                Options.Create(new AiGenerationOptions
                {
                    Provider = "openai",
                    Model = "gpt-4o-mini",
                    ApiKey = "default-key",
                    BaseUrl = "https://api.openai.com/v1",
                    TimeoutSeconds = 90
                }),
                NullLogger<AiConfigStore>.Instance,
                Path.Combine(Path.GetTempPath(), $"cv-settings-theme-{Guid.NewGuid():N}"));
            builder.Services.AddSingleton(aiConfigStore);
            builder.Services.AddSingleton(new AiApiClient(new HttpClient(), aiConfigStore));

            var app = builder.Build();
            app.MapSettingsEndpoints();
            await app.StartAsync();
            return new SettingsThemeTestHost(app, configService);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        private static AppConfig CloneConfig(AppConfig source)
        {
            return JsonSerializer.Deserialize<AppConfig>(JsonSerializer.Serialize(source))
                ?? new AppConfig();
        }
    }
}
