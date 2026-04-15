using System.Net;
using System.Net.Http.Json;
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

public class ContinuousPreviewEndpointTests
{
    private static readonly byte[] ValidPngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    [Fact]
    public async Task ContinuousPreviewEndpoints_ShouldStartFetchAndStopSession()
    {
        var cameraManager = Substitute.For<ICameraManager>();
        cameraManager.GetBindings().Returns(new List<CameraBindingConfig>
        {
            new()
            {
                Id = "binding-1",
                SerialNumber = "SN-001",
                TriggerMode = "Continuous",
                TargetFrameRateFps = 12
            }
        });

        var streamCoordinator = Substitute.For<ICameraFrameStreamCoordinator>();
        streamCoordinator.StartPreviewSessionAsync("binding-1", Arg.Any<CancellationToken>())
            .Returns(new CameraPreviewSession("session-1", "binding-1", CameraTriggerMode.Continuous, 12));
        streamCoordinator.WaitForPreviewFrameAsync("session-1", Arg.Any<CancellationToken>())
            .Returns(new CameraStreamFrame("binding-1", ValidPngBytes, "image/png", 1, 1, 7, DateTime.UtcNow));

        await using var host = await PreviewTestHost.CreateAsync(cameraManager, streamCoordinator);

        var startResponse = await host.Client.PostAsJsonAsync("/api/cameras/continuous-preview/start", new { cameraBindingId = "binding-1" });
        startResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var startPayload = await startResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        startPayload.Should().NotBeNull();

        var frameResponse = await host.Client.GetAsync("/api/cameras/continuous-preview/frame/session-1");
        frameResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        frameResponse.Content.Headers.ContentType?.MediaType.Should().Be("image/png");
        frameResponse.Headers.GetValues("X-Image-Width").Single().Should().Be("1");
        frameResponse.Headers.GetValues("X-Image-Height").Single().Should().Be("1");
        frameResponse.Headers.GetValues("X-Camera-Id").Single().Should().Be("binding-1");
        frameResponse.Headers.GetValues("X-Frame-Sequence").Single().Should().Be("7");
        (await frameResponse.Content.ReadAsByteArrayAsync()).Should().Equal(ValidPngBytes);

        var stopResponse = await host.Client.PostAsJsonAsync("/api/cameras/continuous-preview/stop", new { sessionId = "session-1" });
        stopResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        await streamCoordinator.Received(1).StopPreviewSessionAsync("session-1");
    }

    private sealed class PreviewTestHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private PreviewTestHost(WebApplication app)
        {
            _app = app;
            Client = app.GetTestClient();
        }

        public HttpClient Client { get; }

        public static async Task<PreviewTestHost> CreateAsync(ICameraManager cameraManager, ICameraFrameStreamCoordinator streamCoordinator)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });

            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton(cameraManager);
            builder.Services.AddSingleton(streamCoordinator);

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
            return new PreviewTestHost(app);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
