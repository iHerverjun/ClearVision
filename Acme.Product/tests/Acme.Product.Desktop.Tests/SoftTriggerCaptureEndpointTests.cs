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

public class SoftTriggerCaptureEndpointTests
{
    private static readonly byte[] ValidPngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    [Fact]
    public async Task SoftTriggerCapture_WithEmptyCameraBindingId_ShouldReturnBadRequest()
    {
        var cameraManager = Substitute.For<ICameraManager>();
        cameraManager.GetBindings().Returns(new List<CameraBindingConfig>());

        await using var host = await SoftTriggerTestHost.CreateAsync(cameraManager);
        var response = await host.Client.PostAsJsonAsync("/api/cameras/soft-trigger-capture", new { cameraBindingId = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("CameraBindingId is required.");
    }

    [Fact]
    public async Task SoftTriggerCapture_WithUnknownBinding_ShouldReturnNotFound()
    {
        var cameraManager = Substitute.For<ICameraManager>();
        cameraManager.GetBindings().Returns(new List<CameraBindingConfig>
        {
            new()
            {
                Id = "known-binding",
                SerialNumber = "SN-001",
                ExposureTimeUs = 4000,
                GainDb = 1.2
            }
        });

        await using var host = await SoftTriggerTestHost.CreateAsync(cameraManager);
        var response = await host.Client.PostAsJsonAsync("/api/cameras/soft-trigger-capture", new { cameraBindingId = "missing-binding" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Camera binding not found");
        await cameraManager.DidNotReceive().GetOrCreateByBindingAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task SoftTriggerCapture_WithValidRequest_ShouldReturnPngAndHeaders()
    {
        const string bindingId = "cam-bind-1";
        var binding = new CameraBindingConfig
        {
            Id = bindingId,
            SerialNumber = "SN-001",
            ExposureTimeUs = 5500.5,
            GainDb = 3.2
        };

        var camera = Substitute.For<ICamera>();
        camera.SetExposureTimeAsync(Arg.Any<double>()).Returns(Task.CompletedTask);
        camera.SetGainAsync(Arg.Any<double>()).Returns(Task.CompletedTask);
        camera.AcquireSingleFrameAsync().Returns(Task.FromResult(ValidPngBytes));

        var cameraManager = Substitute.For<ICameraManager>();
        cameraManager.GetBindings().Returns(new List<CameraBindingConfig> { binding });
        cameraManager.GetOrCreateByBindingAsync(bindingId).Returns(Task.FromResult(camera));

        await using var host = await SoftTriggerTestHost.CreateAsync(cameraManager);
        var response = await host.Client.PostAsJsonAsync("/api/cameras/soft-trigger-capture", new { cameraBindingId = bindingId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("image/png");
        response.Headers.GetValues("X-Image-Width").Single().Should().Be("1");
        response.Headers.GetValues("X-Image-Height").Single().Should().Be("1");
        response.Headers.GetValues("X-Camera-Id").Single().Should().Be(bindingId);
        response.Headers.GetValues("X-Trigger-Mode").Single().Should().Be("Software");

        var bodyBytes = await response.Content.ReadAsByteArrayAsync();
        bodyBytes.Should().Equal(ValidPngBytes);

        await cameraManager.Received(1).GetOrCreateByBindingAsync(bindingId);
        await camera.Received(1).SetExposureTimeAsync(binding.ExposureTimeUs);
        await camera.Received(1).SetGainAsync(binding.GainDb);
        await camera.Received(1).AcquireSingleFrameAsync();
    }

    [Fact]
    public async Task SoftTriggerCapture_WithInvalidPngBytes_ShouldReturnBadRequest()
    {
        const string bindingId = "cam-bind-2";
        var binding = new CameraBindingConfig
        {
            Id = bindingId,
            SerialNumber = "SN-002",
            ExposureTimeUs = 1000,
            GainDb = 2.0
        };

        var camera = Substitute.For<ICamera>();
        camera.SetExposureTimeAsync(Arg.Any<double>()).Returns(Task.CompletedTask);
        camera.SetGainAsync(Arg.Any<double>()).Returns(Task.CompletedTask);
        camera.AcquireSingleFrameAsync().Returns(Task.FromResult(new byte[] { 1, 2, 3, 4, 5 }));

        var cameraManager = Substitute.For<ICameraManager>();
        cameraManager.GetBindings().Returns(new List<CameraBindingConfig> { binding });
        cameraManager.GetOrCreateByBindingAsync(bindingId).Returns(Task.FromResult(camera));

        await using var host = await SoftTriggerTestHost.CreateAsync(cameraManager);
        var response = await host.Client.PostAsJsonAsync("/api/cameras/soft-trigger-capture", new { cameraBindingId = bindingId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Camera frame metadata parse failed.");
    }

    [Fact]
    public async Task SoftTriggerCapture_WhenCameraThrows_ShouldReturnBadRequest()
    {
        const string bindingId = "cam-bind-3";
        var binding = new CameraBindingConfig
        {
            Id = bindingId,
            SerialNumber = "SN-003",
            ExposureTimeUs = 7000,
            GainDb = 4.4
        };

        var camera = Substitute.For<ICamera>();
        camera.SetExposureTimeAsync(Arg.Any<double>()).Returns(Task.CompletedTask);
        camera.SetGainAsync(Arg.Any<double>()).Returns(Task.CompletedTask);
        camera.AcquireSingleFrameAsync().Returns<Task<byte[]>>(_ => throw new TimeoutException("相机超时"));

        var cameraManager = Substitute.For<ICameraManager>();
        cameraManager.GetBindings().Returns(new List<CameraBindingConfig> { binding });
        cameraManager.GetOrCreateByBindingAsync(bindingId).Returns(Task.FromResult(camera));

        await using var host = await SoftTriggerTestHost.CreateAsync(cameraManager);
        var response = await host.Client.PostAsJsonAsync("/api/cameras/soft-trigger-capture", new { cameraBindingId = bindingId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("相机超时");
    }

    private sealed class SoftTriggerTestHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private SoftTriggerTestHost(WebApplication app)
        {
            _app = app;
            Client = app.GetTestClient();
        }

        public HttpClient Client { get; }

        public static async Task<SoftTriggerTestHost> CreateAsync(ICameraManager cameraManager)
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
            return new SoftTriggerTestHost(app);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
