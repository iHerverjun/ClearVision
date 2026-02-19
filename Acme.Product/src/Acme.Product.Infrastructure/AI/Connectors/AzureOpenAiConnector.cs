// AzureOpenAiConnector.cs
// Azure OpenAI LLM 连接器 - Sprint 6 Task 6.2
// 支持 Azure OpenAI Service API 调用
// 作者：蘅芜君

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// Azure OpenAI LLM 连接器
/// 支持 Azure OpenAI Service，提供 API Key 和 Entra ID 认证
/// </summary>
public class AzureOpenAiConnector : ILLMConnector, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly AzureOpenAiConfig _config;
    private readonly IRetryPolicy _retryPolicy;

    public AzureOpenAiConnector(AzureOpenAiConfig config, HttpClient? httpClient = null, IRetryPolicy? retryPolicy = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClient ?? new HttpClient();
        _retryPolicy = retryPolicy ?? new ExponentialBackoffRetryPolicy(3, TimeSpan.FromSeconds(1));
        
        // 配置 HttpClient
        var baseUrl = $"{_config.Endpoint}/openai/deployments/{_config.DeploymentName}";
        _httpClient.BaseAddress = new Uri(baseUrl);
        
        // 根据认证方式设置请求头
        if (!string.IsNullOrEmpty(_config.ApiKey))
        {
            // API Key 认证
            _httpClient.DefaultRequestHeaders.Add("api-key", _config.ApiKey);
        }
        else if (!string.IsNullOrEmpty(_config.AccessToken))
        {
            // Entra ID (AAD) 认证
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.AccessToken}");
        }
        
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
            var json = JsonSerializer.Serialize(request, AzureOpenAiJsonContext.Default.AzureChatCompletionRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"/chat/completions?api-version={_config.ApiVersion}";
            var response = await _httpClient.PostAsync(url, content, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                throw new LLMException(
                    $"Azure OpenAI API 错误: {response.StatusCode}",
                    error,
                    (int)response.StatusCode
                );
            }

            var result = await response.Content.ReadFromJsonAsync(
                AzureOpenAiJsonContext.Default.AzureChatCompletionResponse,
                ct
            );

            if (result?.Choices == null || result.Choices.Count == 0)
            {
                throw new LLMException("Azure OpenAI 返回空响应");
            }

            var choice = result.Choices[0];
            
            return new LLMResponse
            {
                Content = choice.Message?.Content ?? string.Empty,
                TokenUsage = result.Usage?.TotalTokens ?? 0,
                Model = result.Model ?? _config.DeploymentName,
                Provider = "AzureOpenAI",
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
        var json = JsonSerializer.Serialize(request, AzureOpenAiJsonContext.Default.AzureChatCompletionRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var url = $"/chat/completions?api-version={_config.ApiVersion}";
        using var response = await _httpClient.PostAsync(url, content, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new LLMException(
                $"Azure OpenAI API 错误: {response.StatusCode}",
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

            // 解析 JSON
            AzureChatCompletionChunk? chunk = null;
            try
            {
                chunk = JsonSerializer.Deserialize(
                    data, 
                    AzureOpenAiJsonContext.Default.AzureChatCompletionChunk
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
            var request = new AzureChatCompletionRequest
            {
                Messages = new List<AzureChatMessage>
                {
                    new() { Role = "user", Content = "Hi" }
                },
                MaxTokens = 5
            };

            var json = JsonSerializer.Serialize(request, AzureOpenAiJsonContext.Default.AzureChatCompletionRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = $"/chat/completions?api-version={_config.ApiVersion}";

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 刷新访问令牌（用于 Entra ID 认证）
    /// </summary>
    public void RefreshAccessToken(string newToken)
    {
        _httpClient.DefaultRequestHeaders.Remove("Authorization");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {newToken}");
    }

    /// <summary>
    /// 创建请求对象
    /// </summary>
    private AzureChatCompletionRequest CreateRequest(string prompt, bool stream = false)
    {
        return new AzureChatCompletionRequest
        {
            Messages = new List<AzureChatMessage>
            {
                new() { Role = "system", Content = GetSystemPrompt() },
                new() { Role = "user", Content = prompt }
            },
            Temperature = _config.Temperature,
            MaxTokens = _config.MaxTokens,
            ResponseFormat = new AzureResponseFormat { Type = "json_object" },
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
}

/// <summary>
/// Azure OpenAI 配置
/// </summary>
public class AzureOpenAiConfig
{
    /// <summary>服务端点 (必需, 如 https://{resource}.openai.azure.com)</summary>
    public string Endpoint { get; set; } = string.Empty;
    
    /// <summary>部署名称 (必需)</summary>
    public string DeploymentName { get; set; } = string.Empty;
    
    /// <summary>API Key (与 AccessToken 二选一)</summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>Entra ID 访问令牌 (与 ApiKey 二选一)</summary>
    public string AccessToken { get; set; } = string.Empty;
    
    /// <summary>API 版本 (默认 2024-02-15-preview)</summary>
    public string ApiVersion { get; set; } = "2024-02-15-preview";
    
    /// <summary>温度参数 (默认 0.1)</summary>
    public float Temperature { get; set; } = 0.1f;
    
    /// <summary>最大 Token 数 (默认 4000)</summary>
    public int MaxTokens { get; set; } = 4000;
    
    /// <summary>超时时间 (默认 60 秒)</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);
}

/// <summary>
/// Azure 聊天完成请求
/// </summary>
public class AzureChatCompletionRequest
{
    [JsonPropertyName("messages")]
    public List<AzureChatMessage> Messages { get; set; } = new();

    [JsonPropertyName("temperature")]
    public float Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }

    [JsonPropertyName("response_format")]
    public AzureResponseFormat? ResponseFormat { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
}

/// <summary>
/// Azure 响应格式
/// </summary>
public class AzureResponseFormat
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "json_object";
}

/// <summary>
/// Azure 聊天消息
/// </summary>
public class AzureChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Azure 聊天完成响应
/// </summary>
public class AzureChatCompletionResponse
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
    public List<AzureChoice>? Choices { get; set; }

    [JsonPropertyName("usage")]
    public AzureUsage? Usage { get; set; }
}

/// <summary>
/// Azure 流式响应块
/// </summary>
public class AzureChatCompletionChunk
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
    public List<AzureChunkChoice>? Choices { get; set; }
}

/// <summary>
/// Azure 选择项
/// </summary>
public class AzureChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public AzureChatMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

/// <summary>
/// Azure 流式选择项
/// </summary>
public class AzureChunkChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("delta")]
    public AzureChatMessage? Delta { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

/// <summary>
/// Azure Token 使用量
/// </summary>
public class AzureUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

/// <summary>
/// JSON 序列化上下文
/// </summary>
[JsonSerializable(typeof(AzureChatCompletionRequest))]
[JsonSerializable(typeof(AzureChatCompletionResponse))]
[JsonSerializable(typeof(AzureChatCompletionChunk))]
internal partial class AzureOpenAiJsonContext : JsonSerializerContext
{
}
