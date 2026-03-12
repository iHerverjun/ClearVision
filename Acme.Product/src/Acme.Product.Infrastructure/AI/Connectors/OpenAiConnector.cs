// OpenAiConnector.cs
// OpenAI LLM 连接器 - Sprint 6 Task 6.1
// 支持 GPT-4/GPT-3.5 API 调用
// 作者：蘅芜君

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// OpenAI LLM 连接器
/// 支持 GPT-4/GPT-3.5 API，提供流式响应和重试机制
/// </summary>
public class OpenAiConnector : ILLMConnector, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiConfig _config;
    private readonly IRetryPolicy _retryPolicy;

    public OpenAiConnector(OpenAiConfig config, HttpClient? httpClient = null, IRetryPolicy? retryPolicy = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClient ?? new HttpClient();
        _retryPolicy = retryPolicy ?? new ExponentialBackoffRetryPolicy(3, TimeSpan.FromSeconds(1));
        
        // 配置 HttpClient
        _httpClient.BaseAddress = new Uri(EnsureTrailingSlash(_config.BaseUrl));
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
        _httpClient.Timeout = _config.Timeout;
    }

    /// <summary>
    /// 生成响应
    /// </summary>
    public async Task<LLMResponse> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        return await _retryPolicy.ExecuteAsync(async ct =>
        {
            var request = CreateRequest(prompt);
            var json = JsonSerializer.Serialize(request, OpenAiJsonContext.Default.OpenAIChatCompletionRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("chat/completions", content, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                throw new LLMException(
                    $"OpenAI API 错误: {response.StatusCode}",
                    error,
                    (int)response.StatusCode
                );
            }

            var result = await response.Content.ReadFromJsonAsync(
                OpenAiJsonContext.Default.OpenAIChatCompletionResponse,
                ct
            );

            if (result?.Choices == null || result.Choices.Count == 0)
            {
                throw new LLMException("OpenAI 返回空响应");
            }

            var choice = result.Choices[0];
            
            return new LLMResponse
            {
                Content = choice.Message?.Content ?? string.Empty,
                TokenUsage = result.Usage?.TotalTokens ?? 0,
                Model = result.Model ?? _config.Model,
                Provider = "OpenAI",
                FinishReason = choice.FinishReason ?? "unknown"
            };
        }, cancellationToken);
    }

    /// <summary>
    /// 流式生成响应
    /// </summary>
    public async IAsyncEnumerable<LLMStreamChunk> GenerateStreamAsync(
        string prompt, 
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(prompt, stream: true);
        var json = JsonSerializer.Serialize(request, OpenAiJsonContext.Default.OpenAIChatCompletionRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync("chat/completions", content, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new LLMException(
                $"OpenAI API 错误: {response.StatusCode}",
                error,
                (int)response.StatusCode
            );
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var isComplete = false;

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested && !isComplete)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                continue;

            var data = line.Substring(6); // 去掉 "data: "
            
            if (data == "[DONE]")
            {
                isComplete = true;
                yield return new LLMStreamChunk { Content = string.Empty, IsComplete = true };
                yield break;
            }

            // 解析 JSON，失败时跳过
            OpenAIChatCompletionChunk? chunk = null;
            try
            {
                chunk = JsonSerializer.Deserialize(
                    data, 
                    OpenAiJsonContext.Default.OpenAIChatCompletionChunk
                );
            }
            catch (JsonException)
            {
                // 忽略解析错误的行
            }

            if (chunk?.Choices?.Count > 0)
            {
                var delta = chunk.Choices[0].Delta?.Content;
                if (!string.IsNullOrEmpty(delta))
                {
                    yield return new LLMStreamChunk
                    {
                        Content = delta,
                        IsComplete = false
                    };
                }
            }
        }

        if (!isComplete)
        {
            yield return new LLMStreamChunk { Content = string.Empty, IsComplete = true };
        }
    }

    /// <summary>
    /// 验证配置是否有效
    /// </summary>
    public async Task<bool> ValidateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 使用简单请求验证 API Key
            var request = new OpenAIChatCompletionRequest
            {
                Model = _config.Model,
                Messages = new List<OpenAIChatMessage>
                {
                    new() { Role = "user", Content = "Hi" }
                },
                MaxTokens = 5
            };

            var json = JsonSerializer.Serialize(request, OpenAiJsonContext.Default.OpenAIChatCompletionRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("chat/completions", content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取可用模型列表
    /// </summary>
    public async Task<List<string>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("models", cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new LLMException($"获取模型列表失败: {response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync(
            OpenAiJsonContext.Default.OpenAIModelsResponse,
            cancellationToken
        );

        return result?.Data?.Select(m => m.Id).ToList() ?? new List<string>();
    }

    /// <summary>
    /// 创建请求对象
    /// </summary>
    private OpenAIChatCompletionRequest CreateRequest(string prompt, bool stream = false)
    {
        return new OpenAIChatCompletionRequest
        {
            Model = _config.Model,
            Messages = new List<OpenAIChatMessage>
            {
                new() { Role = "system", Content = GetSystemPrompt() },
                new() { Role = "user", Content = prompt }
            },
            Temperature = _config.Temperature,
            MaxTokens = _config.MaxTokens,
            ResponseFormat = new OpenAIResponseFormat { Type = "json_object" },
            Stream = stream
        };
    }

    /// <summary>
    /// 获取系统提示词
    /// </summary>
    private string GetSystemPrompt()
    {
        return @"You are a professional industrial vision inspection flow generation assistant for ClearVision platform.
Your task is to convert natural language requirements into structured JSON flow definitions.
Always respond with valid JSON that matches the ClearVision flow schema.";
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    private static string EnsureTrailingSlash(string baseUrl)
    {
        return baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : $"{baseUrl}/";
    }
}

/// <summary>
/// OpenAI 配置
/// </summary>
public class OpenAiConfig
{
    /// <summary>API Key (必需)</summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>模型名称 (默认 gpt-4)</summary>
    public string Model { get; set; } = "gpt-4";
    
    /// <summary>基础 URL (默认 https://api.openai.com/v1)</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    
    /// <summary>温度参数 (默认 0.1)</summary>
    public float Temperature { get; set; } = 0.1f;
    
    /// <summary>最大 Token 数 (默认 4000)</summary>
    public int MaxTokens { get; set; } = 4000;
    
    /// <summary>超时时间 (默认 60 秒)</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);
}

/// <summary>
/// 聊天完成请求
/// </summary>
public class OpenAIChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<OpenAIChatMessage> Messages { get; set; } = new();

    [JsonPropertyName("temperature")]
    public float Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }

    [JsonPropertyName("response_format")]
    public OpenAIResponseFormat? ResponseFormat { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
}

/// <summary>
/// 响应格式
/// </summary>
public class OpenAIResponseFormat
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "json_object";
}

/// <summary>
/// 聊天消息
/// </summary>
public class OpenAIChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 聊天完成响应
/// </summary>
public class OpenAIChatCompletionResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("choices")]
    public List<OpenAIChoice>? Choices { get; set; }

    [JsonPropertyName("usage")]
    public OpenAIUsage? Usage { get; set; }
}

/// <summary>
/// 流式响应块
/// </summary>
public class OpenAIChatCompletionChunk
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("choices")]
    public List<OpenAIChunkChoice>? Choices { get; set; }
}

/// <summary>
/// 选择项
/// </summary>
public class OpenAIChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public OpenAIChatMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

/// <summary>
/// 流式选择项
/// </summary>
public class OpenAIChunkChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("delta")]
    public OpenAIChatMessage? Delta { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

/// <summary>
/// Token 使用量
/// </summary>
public class OpenAIUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

/// <summary>
/// 模型响应
/// </summary>
public class OpenAIModelsResponse
{
    [JsonPropertyName("data")]
    public List<OpenAIModelInfo>? Data { get; set; }
}

/// <summary>
/// 模型信息
/// </summary>
public class OpenAIModelInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;
}

/// <summary>
/// JSON 序列化上下文
/// </summary>
[JsonSerializable(typeof(OpenAIChatCompletionRequest))]
[JsonSerializable(typeof(OpenAIChatCompletionResponse))]
[JsonSerializable(typeof(OpenAIChatCompletionChunk))]
[JsonSerializable(typeof(OpenAIModelsResponse))]
internal partial class OpenAiJsonContext : JsonSerializerContext
{
}

/// <summary>
/// 流式响应块
/// </summary>
public class LLMStreamChunk
{
    /// <summary>内容片段</summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>是否完成</summary>
    public bool IsComplete { get; set; }
}

/// <summary>
/// LLM 异常
/// </summary>
public class LLMException : Exception
{
    public string? ErrorDetails { get; }
    public int StatusCode { get; }

    public LLMException(string message, string? errorDetails = null, int statusCode = 0) 
        : base(message)
    {
        ErrorDetails = errorDetails;
        StatusCode = statusCode;
    }
}

/// <summary>
/// 重试策略接口
/// </summary>
public interface IRetryPolicy
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
}

/// <summary>
/// 指数退避重试策略
/// </summary>
public class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _initialDelay;

    public ExponentialBackoffRetryPolicy(int maxRetries, TimeSpan initialDelay)
    {
        _maxRetries = maxRetries;
        _initialDelay = initialDelay;
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt < _maxRetries)
        {
            try
            {
                return await action(cancellationToken);
            }
            catch (Exception ex) when (IsRetryable(ex) && attempt < _maxRetries - 1)
            {
                lastException = ex;
                attempt++;
                
                var delay = _initialDelay * Math.Pow(2, attempt - 1);
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw lastException ?? new InvalidOperationException("重试失败");
    }

    private bool IsRetryable(Exception ex)
    {
        // 网络错误、超时、5xx 错误可重试
        if (ex is HttpRequestException) return true;
        if (ex is TaskCanceledException) return true;
        if (ex is LLMException llmEx && llmEx.StatusCode >= 500) return true;
        return false;
    }
}
