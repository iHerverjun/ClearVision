using System.Net;
using System.Text;
using Acme.Product.Infrastructure.AI;
using Acme.Product.Infrastructure.AI.Connectors;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acme.Product.Tests.AI;

public class LLMConnectorSmokeTests
{
    [Fact]
    public async Task OpenAiConnector_GenerateAsync_ShouldHandleSuccessAndUseV1Path()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new CaptureHandler((request, _) =>
        {
            capturedRequest = request;
            return Task.FromResult(JsonResponse("""
            {
              "id": "chatcmpl-1",
              "object": "chat.completion",
              "created": 1710000000,
              "model": "gpt-4o-mini",
              "choices": [
                {
                  "index": 0,
                  "message": { "role": "assistant", "content": "{\"ok\":true}" },
                  "finish_reason": "stop"
                }
              ],
              "usage": {
                "prompt_tokens": 10,
                "completion_tokens": 5,
                "total_tokens": 15
              }
            }
            """));
        });

        using var httpClient = new HttpClient(handler);
        using var connector = new OpenAiConnector(
            new OpenAiConfig
            {
                ApiKey = "test-key",
                BaseUrl = "https://api.openai.com/v1",
                Model = "gpt-4o-mini"
            },
            httpClient,
            NoRetryPolicy.Instance);

        var result = await connector.GenerateAsync("hello");

        result.Content.Should().Be("{\"ok\":true}");
        result.TokenUsage.Should().Be(15);
        result.Provider.Should().Be("OpenAI");
        result.Model.Should().Be("gpt-4o-mini");
        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.ToString().Should().Be("https://api.openai.com/v1/chat/completions");
        capturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        capturedRequest.Headers.Authorization.Parameter.Should().Be("test-key");
    }

    [Fact]
    public async Task OpenAiConnector_GenerateAsync_ShouldThrowOnFailure()
    {
        var handler = new CaptureHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("{\"error\":\"boom\"}", Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler);
        using var connector = new OpenAiConnector(
            new OpenAiConfig { ApiKey = "test-key", BaseUrl = "https://api.openai.com/v1" },
            httpClient,
            NoRetryPolicy.Instance);

        var act = () => connector.GenerateAsync("hello");

        await act.Should().ThrowAsync<LLMException>();
    }

    [Fact]
    public async Task AzureOpenAiConnector_GenerateAsync_ShouldHandleApiKeySuccessPath()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new CaptureHandler((request, _) =>
        {
            capturedRequest = request;
            return Task.FromResult(JsonResponse("""
            {
              "id": "chatcmpl-2",
              "object": "chat.completion",
              "created": 1710000000,
              "model": "gpt-4.1-mini",
              "choices": [
                {
                  "index": 0,
                  "message": { "role": "assistant", "content": "azure-ok" },
                  "finish_reason": "stop"
                }
              ],
              "usage": {
                "prompt_tokens": 8,
                "completion_tokens": 4,
                "total_tokens": 12
              }
            }
            """));
        });

        using var httpClient = new HttpClient(handler);
        using var connector = new AzureOpenAiConnector(
            new AzureOpenAiConfig
            {
                Endpoint = "https://example-resource.openai.azure.com",
                DeploymentName = "gpt-4.1-mini",
                ApiKey = "azure-key",
                ApiVersion = "2024-02-15-preview"
            },
            httpClient,
            NoRetryPolicy.Instance);

        var result = await connector.GenerateAsync("hello");

        result.Content.Should().Be("azure-ok");
        result.TokenUsage.Should().Be(12);
        result.Provider.Should().Be("AzureOpenAI");
        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.ToString().Should()
            .Be("https://example-resource.openai.azure.com/openai/deployments/gpt-4.1-mini/chat/completions?api-version=2024-02-15-preview");
        capturedRequest.Headers.Should().Contain(header => header.Key == "api-key");
    }

    [Fact]
    public async Task AzureOpenAiConnector_GenerateAsync_ShouldUseBearerTokenWhenConfigured()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new CaptureHandler((request, _) =>
        {
            capturedRequest = request;
            return Task.FromResult(JsonResponse("""
            {
              "id": "chatcmpl-3",
              "object": "chat.completion",
              "created": 1710000000,
              "model": "gpt-4.1-mini",
              "choices": [
                {
                  "index": 0,
                  "message": { "role": "assistant", "content": "entra-ok" },
                  "finish_reason": "stop"
                }
              ],
              "usage": {
                "prompt_tokens": 7,
                "completion_tokens": 3,
                "total_tokens": 10
              }
            }
            """));
        });

        using var httpClient = new HttpClient(handler);
        using var connector = new AzureOpenAiConnector(
            new AzureOpenAiConfig
            {
                Endpoint = "https://example-resource.openai.azure.com",
                DeploymentName = "gpt-4.1-mini",
                AccessToken = "entra-token",
                ApiVersion = "2024-02-15-preview"
            },
            httpClient,
            NoRetryPolicy.Instance);

        var result = await connector.GenerateAsync("hello");

        result.Content.Should().Be("entra-ok");
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        capturedRequest.Headers.Authorization.Parameter.Should().Be("entra-token");
    }

    [Fact]
    public async Task AzureOpenAiConnector_GenerateAsync_ShouldThrowOnFailure()
    {
        var handler = new CaptureHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("gateway error", Encoding.UTF8, "text/plain")
            }));

        using var httpClient = new HttpClient(handler);
        using var connector = new AzureOpenAiConnector(
            new AzureOpenAiConfig
            {
                Endpoint = "https://example-resource.openai.azure.com",
                DeploymentName = "gpt-4.1-mini",
                ApiKey = "azure-key"
            },
            httpClient,
            NoRetryPolicy.Instance);

        var act = () => connector.GenerateAsync("hello");

        await act.Should().ThrowAsync<LLMException>();
    }

    [Fact]
    public async Task OllamaConnector_GenerateAsync_ShouldHandleSuccessPath()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new CaptureHandler((request, _) =>
        {
            capturedRequest = request;
            return Task.FromResult(JsonResponse("""
            {
              "model": "llama3",
              "created_at": "2026-03-06T10:00:00Z",
              "response": "ollama-ok",
              "done": true,
              "prompt_eval_count": 5,
              "eval_count": 3
            }
            """));
        });

        using var httpClient = new HttpClient(handler);
        using var connector = new OllamaConnector(
            new OllamaConfig { BaseUrl = "http://localhost:11434", Model = "llama3" },
            httpClient,
            NoRetryPolicy.Instance);

        var result = await connector.GenerateAsync("hello");

        result.Content.Should().Be("ollama-ok");
        result.Provider.Should().Be("Ollama");
        result.FinishReason.Should().Be("stop");
        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.ToString().Should().Be("http://localhost:11434/api/generate");
    }

    [Fact]
    public async Task OllamaConnector_GenerateAsync_ShouldThrowOnFailure()
    {
        var handler = new CaptureHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("unavailable", Encoding.UTF8, "text/plain")
            }));

        using var httpClient = new HttpClient(handler);
        using var connector = new OllamaConnector(
            new OllamaConfig { BaseUrl = "http://localhost:11434", Model = "llama3" },
            httpClient,
            NoRetryPolicy.Instance);

        var act = () => connector.GenerateAsync("hello");

        await act.Should().ThrowAsync<LLMException>();
    }

    [Fact]
    public void LlmConnectorFactory_Create_ShouldReturnExpectedConnectorTypes()
    {
        var services = new ServiceCollection();
        services.AddHttpClient("LLM");
        using var provider = services.BuildServiceProvider();
        var factory = new LLMConnectorFactory(provider, NullLoggerFactory.Instance);

        var openAi = factory.Create(LLMProviderType.OpenAI, new Dictionary<string, string>
        {
            ["ApiKey"] = "openai-key"
        });
        var azure = factory.Create(LLMProviderType.AzureOpenAI, new Dictionary<string, string>
        {
            ["Endpoint"] = "https://example-resource.openai.azure.com",
            ["DeploymentName"] = "gpt-4.1-mini",
            ["ApiKey"] = "azure-key"
        });
        var ollama = factory.Create(LLMProviderType.Ollama, new Dictionary<string, string>
        {
            ["BaseUrl"] = "http://localhost:11434",
            ["Model"] = "llama3"
        });

        openAi.Should().BeOfType<OpenAiConnector>();
        azure.Should().BeOfType<AzureOpenAiConnector>();
        ollama.Should().BeOfType<OllamaConnector>();
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync;

        public CaptureHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        {
            _sendAsync = sendAsync;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _sendAsync(request, cancellationToken);
        }
    }

    private sealed class NoRetryPolicy : IRetryPolicy
    {
        public static NoRetryPolicy Instance { get; } = new();

        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
        {
            return action(cancellationToken);
        }
    }
}
