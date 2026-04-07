using System.Net;
using System.Text;
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

public class AiModelEndpointsTests
{
    [Fact]
    public async Task GetAiModels_ShouldReturnReasoningAndSupportMetadata()
    {
        await using var host = await AiModelEndpointTestHost.CreateAsync();
        host.AiConfigStore.Add(new AiModelConfig
        {
            Id = "gpt5",
            Name = "GPT 5.4",
            Provider = "OpenAI Compatible",
            Model = "gpt-5.4",
            Reasoning = new AiReasoningSettings
            {
                Mode = AiReasoningModes.On,
                Effort = AiReasoningEfforts.High
            }
        });

        using var response = await host.Client.GetAsync("/api/ai/models");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var models = document.RootElement;
        models.ValueKind.Should().Be(JsonValueKind.Array);
        var gpt5 = models.EnumerateArray().First(x => x.GetProperty("id").GetString() == "gpt5");
        gpt5.GetProperty("reasoning").GetProperty("mode").GetString().Should().Be("on");
        gpt5.GetProperty("reasoning").GetProperty("effort").GetString().Should().Be("high");
        gpt5.GetProperty("reasoningSupport").GetProperty("familyId").GetString().Should().Be("openai_gpt5");
        gpt5.GetProperty("reasoningSupport").GetProperty("supportsExplicitMode").GetBoolean().Should().BeTrue();
        gpt5.GetProperty("reasoningSupport").GetProperty("supportsEffort").GetBoolean().Should().BeTrue();
        gpt5.GetProperty("reasoningSupport").GetProperty("allowedModes").EnumerateArray().Select(x => x.GetString())
            .Should().BeEquivalentTo(["auto", "on"]);
        gpt5.GetProperty("reasoningSupport").GetProperty("allowedEfforts").EnumerateArray().Select(x => x.GetString())
            .Should().BeEquivalentTo(["low", "medium", "high"]);
    }

    [Fact]
    public async Task UpdateAiModel_ShouldRejectTurningOffLockedReasonerModel()
    {
        await using var host = await AiModelEndpointTestHost.CreateAsync();
        host.AiConfigStore.Add(new AiModelConfig
        {
            Id = "deepseek-reasoner",
            Name = "DeepSeek Reasoner",
            Provider = "OpenAI Compatible",
            Model = "deepseek-reasoner"
        });

        var payload = JsonSerializer.Serialize(new
        {
            name = "DeepSeek Reasoner",
            provider = "OpenAI Compatible",
            model = "deepseek-reasoner",
            baseUrl = "https://api.deepseek.com",
            apiKey = "test-key",
            timeoutMs = 120000,
            reasoning = new
            {
                mode = "off",
                effort = "medium"
            }
        });

        using var response = await host.Client.PutAsync(
            "/api/ai/models/deepseek-reasoner",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("不支持关闭 reasoning / thinking");

        using var modelsResponse = await host.Client.GetAsync("/api/ai/models");
        modelsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await modelsResponse.Content.ReadAsStringAsync());
        var model = document.RootElement.EnumerateArray().First(x => x.GetProperty("id").GetString() == "deepseek-reasoner");
        model.GetProperty("reasoning").GetProperty("mode").GetString().Should().Be("auto");
    }

    [Fact]
    public async Task PreviewReasoningSupport_ShouldReturnServerResolvedAllowedModesAndEfforts()
    {
        await using var host = await AiModelEndpointTestHost.CreateAsync();
        var payload = JsonSerializer.Serialize(new
        {
            provider = "OpenAI Compatible",
            model = "gpt-5.1-mini",
            baseUrl = "https://api.openai.com/v1",
            protocol = (string?)null
        });

        using var response = await host.Client.PostAsync(
            "/api/ai/reasoning-support",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("familyId").GetString().Should().Be("openai_gpt5");
        document.RootElement.GetProperty("allowedModes").EnumerateArray().Select(x => x.GetString())
            .Should().BeEquivalentTo(["auto", "off", "on"]);
        document.RootElement.GetProperty("allowedEfforts").EnumerateArray().Select(x => x.GetString())
            .Should().BeEquivalentTo(["low", "medium", "high"]);
    }

    private sealed class AiModelEndpointTestHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private AiModelEndpointTestHost(WebApplication app, AiConfigStore aiConfigStore)
        {
            _app = app;
            AiConfigStore = aiConfigStore;
            Client = app.GetTestClient();
        }

        public HttpClient Client { get; }

        public AiConfigStore AiConfigStore { get; }

        public static async Task<AiModelEndpointTestHost> CreateAsync()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });
            builder.WebHost.UseTestServer();

            var configService = Substitute.For<IConfigurationService>();
            configService.LoadAsync().Returns(Task.FromResult(new AppConfig()));
            configService.GetCurrent().Returns(new AppConfig());
            configService.SaveAsync(Arg.Any<AppConfig>()).Returns(Task.CompletedTask);
            builder.Services.AddSingleton(configService);
            builder.Services.AddSingleton(Substitute.For<Acme.Product.Core.Cameras.ICameraManager>());

            var aiConfigStore = new AiConfigStore(
                Options.Create(new AiGenerationOptions
                {
                    Provider = "OpenAI Compatible",
                    Model = "gpt-4o-mini",
                    ApiKey = "default-key",
                    BaseUrl = "https://api.openai.com/v1",
                    TimeoutSeconds = 90
                }),
                NullLogger<AiConfigStore>.Instance,
                Path.Combine(Path.GetTempPath(), $"cv-ai-model-endpoints-{Guid.NewGuid():N}"));
            builder.Services.AddSingleton(aiConfigStore);
            builder.Services.AddSingleton(new AiApiClient(new HttpClient(), aiConfigStore));

            var app = builder.Build();
            app.MapSettingsEndpoints();
            await app.StartAsync();
            return new AiModelEndpointTestHost(app, aiConfigStore);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
