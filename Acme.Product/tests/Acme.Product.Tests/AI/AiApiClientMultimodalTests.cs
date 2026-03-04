using System.Net;
using System.Text;
using System.Text.Json;
using Acme.Product.Contracts.Messages;
using Acme.Product.Infrastructure.AI;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Acme.Product.Tests.AI;

public class AiApiClientMultimodalTests
{
    [Fact(DisplayName = "AiApiClient should send OpenAI multimodal message content with text and image")]
    public async Task CompleteAsync_OpenAi_ShouldSerializeTextAndImageParts()
    {
        var imagePath = CreateTinyPngFile();
        string? capturedRequestJson = null;

        try
        {
            var handler = new CaptureHandler(async (request, cancellationToken) =>
            {
                capturedRequestJson = await request.Content!.ReadAsStringAsync(cancellationToken);
                var responseJson = """
                {
                  "choices": [
                    {
                      "message": {
                        "content": "{\"ok\":true}"
                      }
                    }
                  ]
                }
                """;

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                };
            });

            using var httpClient = new HttpClient(handler);
            var configStore = new AiConfigStore(
                Options.Create(new AiGenerationOptions()),
                Substitute.For<Microsoft.Extensions.Logging.ILogger<AiConfigStore>>());
            var apiClient = new AiApiClient(httpClient, configStore);

            var messages = new List<ChatMessage>
            {
                new("user", new List<ChatMessageContentPart>
                {
                    ChatMessageContentPart.TextPart("Please compare these images."),
                    ChatMessageContentPart.ImageFile(imagePath, "high")
                })
            };

            var result = await apiClient.CompleteAsync(
                systemPrompt: "You are a visual QA assistant.",
                messages: messages,
                options: new AiGenerationOptions
                {
                    Provider = "OpenAI",
                    ApiKey = "test-key",
                    Model = "gpt-4o-mini",
                    MaxTokens = 512,
                    TimeoutSeconds = 10
                });

            result.Content.Should().Be("{\"ok\":true}");
            capturedRequestJson.Should().NotBeNullOrWhiteSpace();

            using var doc = JsonDocument.Parse(capturedRequestJson!);
            var requestMessages = doc.RootElement.GetProperty("messages");
            requestMessages.GetArrayLength().Should().Be(2);

            var userMessage = requestMessages[1];
            userMessage.GetProperty("role").GetString().Should().Be("user");
            var content = userMessage.GetProperty("content");
            content.ValueKind.Should().Be(JsonValueKind.Array);
            content.GetArrayLength().Should().Be(2);

            content[0].GetProperty("type").GetString().Should().Be("text");
            content[0].GetProperty("text").GetString().Should().Be("Please compare these images.");

            content[1].GetProperty("type").GetString().Should().Be("image_url");
            content[1].GetProperty("image_url").GetProperty("detail").GetString().Should().Be("high");
            content[1].GetProperty("image_url").GetProperty("url").GetString()
                .Should().StartWith("data:image/png;base64,");
        }
        finally
        {
            if (File.Exists(imagePath))
            {
                File.Delete(imagePath);
            }
        }
    }

    [Fact(DisplayName = "AiApiClient stream should parse non-SSE OpenAI compatible JSON payload")]
    public async Task StreamCompleteAsync_OpenAi_ShouldHandleNonSseJsonPayload()
    {
        var responseJson = """
        {
          "choices": [
            {
              "message": {
                "content": "{\"ok\":true}"
              }
            }
          ]
        }
        """;

        var handler = new CaptureHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        }));

        using var httpClient = new HttpClient(handler);
        var apiClient = CreateApiClient(httpClient);
        var chunks = new List<AiStreamChunk>();

        var result = await apiClient.StreamCompleteAsync(
            systemPrompt: "system",
            messages: new List<ChatMessage> { new("user", "test") },
            onChunk: chunks.Add,
            options: new AiGenerationOptions
            {
                Provider = "OpenAI",
                ApiKey = "test-key",
                Model = "gpt-4o-mini",
                MaxTokens = 256,
                TimeoutSeconds = 10
            });

        result.Content.Should().Be("{\"ok\":true}");
        chunks.Count(c => c.ChunkType == AiStreamChunkType.Done).Should().Be(1);
        chunks.Where(c => c.ChunkType == AiStreamChunkType.Content).Select(c => c.Content).Should().Contain("{\"ok\":true}");
    }

    [Fact(DisplayName = "AiApiClient stream should parse response.output_text.delta events")]
    public async Task StreamCompleteAsync_OpenAi_ShouldHandleResponseOutputTextDelta()
    {
        var ssePayload = string.Join("\n",
        [
            "data: {\"type\":\"response.output_text.delta\",\"delta\":\"{\\\"ok\\\":\"}",
            "",
            "data: {\"type\":\"response.output_text.delta\",\"delta\":\"true}\"}",
            "",
            "data: [DONE]",
            ""
        ]);

        var handler = new CaptureHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ssePayload, Encoding.UTF8, "text/event-stream")
        }));

        using var httpClient = new HttpClient(handler);
        var apiClient = CreateApiClient(httpClient);
        var chunks = new List<AiStreamChunk>();

        var result = await apiClient.StreamCompleteAsync(
            systemPrompt: "system",
            messages: new List<ChatMessage> { new("user", "test") },
            onChunk: chunks.Add,
            options: new AiGenerationOptions
            {
                Provider = "OpenAI",
                ApiKey = "test-key",
                Model = "gpt-4o-mini",
                MaxTokens = 256,
                TimeoutSeconds = 10
            });

        result.Content.Should().Be("{\"ok\":true}");
        chunks.Count(c => c.ChunkType == AiStreamChunkType.Content).Should().BeGreaterThan(0);
        chunks.Count(c => c.ChunkType == AiStreamChunkType.Done).Should().Be(1);
    }

    [Fact(DisplayName = "AiApiClient stream should recover JSON from reasoning when content chunk is missing")]
    public async Task StreamCompleteAsync_OpenAi_ShouldRecoverJsonFromReasoning()
    {
        var ssePayload = string.Join("\n",
        [
            "data: {\"choices\":[{\"delta\":{\"reasoning_content\":\"分析完成，输出 JSON: {\\\"ok\\\":true}\"}}]}",
            "",
            "data: [DONE]",
            ""
        ]);

        var handler = new CaptureHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ssePayload, Encoding.UTF8, "text/event-stream")
        }));

        using var httpClient = new HttpClient(handler);
        var apiClient = CreateApiClient(httpClient);
        var chunks = new List<AiStreamChunk>();

        var result = await apiClient.StreamCompleteAsync(
            systemPrompt: "system",
            messages: new List<ChatMessage> { new("user", "test") },
            onChunk: chunks.Add,
            options: new AiGenerationOptions
            {
                Provider = "OpenAI",
                ApiKey = "test-key",
                Model = "gpt-4o-mini",
                MaxTokens = 256,
                TimeoutSeconds = 10
            });

        result.Content.Should().Be("{\"ok\":true}");
        result.Reasoning.Should().Contain("输出 JSON");
        chunks.Count(c => c.ChunkType == AiStreamChunkType.Done).Should().Be(1);
    }

    [Fact(DisplayName = "AiApiClient stream should fallback to non-stream completion when stream has no content")]
    public async Task StreamCompleteAsync_OpenAi_ShouldFallbackToNonStreamCall()
    {
        var callCount = 0;
        var handler = new CaptureHandler((_, _) =>
        {
            callCount++;
            if (callCount == 1)
            {
                var emptySse = string.Join("\n",
                [
                    "data: {\"choices\":[{\"delta\":{}}]}",
                    "",
                    "data: [DONE]",
                    ""
                ]);

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(emptySse, Encoding.UTF8, "text/event-stream")
                });
            }

            var responseJson = """
            {
              "choices": [
                {
                  "message": {
                    "content": "{\"ok\":true}"
                  }
                }
              ]
            }
            """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        });

        using var httpClient = new HttpClient(handler);
        var apiClient = CreateApiClient(httpClient);

        var result = await apiClient.StreamCompleteAsync(
            systemPrompt: "system",
            messages: new List<ChatMessage> { new("user", "test") },
            onChunk: _ => { },
            options: new AiGenerationOptions
            {
                Provider = "OpenAI",
                ApiKey = "test-key",
                Model = "gpt-4o-mini",
                MaxTokens = 256,
                TimeoutSeconds = 10
            });

        callCount.Should().Be(2);
        result.Content.Should().Be("{\"ok\":true}");
    }

    private static AiApiClient CreateApiClient(HttpClient httpClient)
    {
        var configStore = new AiConfigStore(
            Options.Create(new AiGenerationOptions()),
            Substitute.For<Microsoft.Extensions.Logging.ILogger<AiConfigStore>>());
        return new AiApiClient(httpClient, configStore);
    }

    private static string CreateTinyPngFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cv-ai-mm-{Guid.NewGuid():N}.png");
        var pngBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/aYQAAAAASUVORK5CYII=");
        File.WriteAllBytes(path, pngBytes);
        return path;
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
}
