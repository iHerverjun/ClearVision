// OllamaConnector.cs
// Ollama 本地模型 LLM 连接器 - Sprint 6 Task 6.3
// 支持本地部署的大语言模型（Llama2、CodeLlama、Mistral 等）
// 作者：蘅芜君

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// Ollama LLM 连接器
/// 支持本地部署的开源大语言模型，无需 API Key
/// </summary>
public class OllamaConnector : ILLMConnector, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly OllamaConfig _config;
    private readonly IRetryPolicy _retryPolicy;

    public OllamaConnector(OllamaConfig config, HttpClient? httpClient = null, IRetryPolicy? retryPolicy = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClient ?? new HttpClient();
        _retryPolicy = retryPolicy ?? new ExponentialBackoffRetryPolicy(3, TimeSpan.FromSeconds(1));
        
        // 配置 HttpClient
        _httpClient.BaseAddress = new Uri(EnsureTrailingSlash(_config.BaseUrl));
        _httpClient.Timeout = _config.Timeout;
    }

    /// <summary>
    /// 生成响应
    /// </summary>
    public async Task<LLMResponse> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        return await _retryPolicy.ExecuteAsync(async ct =>
        {
            var request = new OllamaGenerateRequest
            {
                Model = _config.Model,
                Prompt = FormatPrompt(prompt),
                Stream = false,
                Options = new OllamaOptions
                {
                    Temperature = _config.Temperature,
                    NumContext = _config.ContextWindow
                }
            };

            var json = JsonSerializer.Serialize(request, OllamaJsonContext.Default.OllamaGenerateRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("api/generate", content, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                throw new LLMException(
                    $"Ollama API 错误: {response.StatusCode}",
                    error,
                    (int)response.StatusCode
                );
            }

            var result = await response.Content.ReadFromJsonAsync(
                OllamaJsonContext.Default.OllamaGenerateResponse,
                ct
            );

            if (result == null)
            {
                throw new LLMException("Ollama 返回空响应");
            }

            // 估算 Token 使用量 (Ollama 不直接返回)
            var estimatedTokens = EstimateTokenCount(request.Prompt) + EstimateTokenCount(result.Response);

            return new LLMResponse
            {
                Content = result.Response,
                TokenUsage = estimatedTokens,
                Model = result.Model,
                Provider = "Ollama",
                FinishReason = result.Done ? "stop" : "unknown"
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
        var request = new OllamaGenerateRequest
        {
            Model = _config.Model,
            Prompt = FormatPrompt(prompt),
            Stream = true,
            Options = new OllamaOptions
            {
                Temperature = _config.Temperature,
                NumContext = _config.ContextWindow
            }
        };

        var json = JsonSerializer.Serialize(request, OllamaJsonContext.Default.OllamaGenerateRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("api/generate", content, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new LLMException(
                $"Ollama API 错误: {response.StatusCode}",
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
            
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // 解析 JSON
            OllamaGenerateResponse? chunk = null;
            try
            {
                chunk = JsonSerializer.Deserialize(
                    line, 
                    OllamaJsonContext.Default.OllamaGenerateResponse
                );
            }
            catch (JsonException)
            {
                // 忽略解析错误的行
            }

            if (chunk != null)
            {
                if (!string.IsNullOrEmpty(chunk.Response))
                {
                    yield return new LLMStreamChunk
                    {
                        Content = chunk.Response,
                        IsComplete = false
                    };
                }

                if (chunk.Done)
                {
                    isComplete = true;
                    yield return new LLMStreamChunk { Content = string.Empty, IsComplete = true };
                    yield break;
                }
            }
        }

        if (!isComplete)
        {
            yield return new LLMStreamChunk { Content = string.Empty, IsComplete = true };
        }
    }

    /// <summary>
    /// 验证配置是否有效（检查服务是否可访问）
    /// </summary>
    public async Task<bool> ValidateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/tags", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取本地可用模型列表
    /// </summary>
    public async Task<List<OllamaModelInfo>> GetLocalModelsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/tags", cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new LLMException($"获取模型列表失败: {response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync(
            OllamaJsonContext.Default.OllamaModelListResponse,
            cancellationToken
        );

        return result?.Models ?? new List<OllamaModelInfo>();
    }

    /// <summary>
    /// 拉取模型
    /// </summary>
    public async Task PullModelAsync(string modelName, CancellationToken cancellationToken = default)
    {
        var request = new OllamaPullRequest
        {
            Name = modelName,
            Stream = false
        };

        var json = JsonSerializer.Serialize(request, OllamaJsonContext.Default.OllamaPullRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("api/pull", content, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new LLMException($"拉取模型失败: {error}");
        }
    }

    /// <summary>
    /// 格式化提示词
    /// </summary>
    private string FormatPrompt(string userPrompt)
    {
        var systemPrompt = @"You are a professional industrial vision inspection flow generation assistant for ClearVision platform.
Your task is to convert natural language requirements into structured JSON flow definitions.
Always respond with valid JSON that matches the ClearVision flow schema.";

        return $"{systemPrompt}\n\n{userPrompt}";
    }

    /// <summary>
    /// 估算 Token 数量（简化算法：大约每 4 个字符 1 个 Token）
    /// </summary>
    private int EstimateTokenCount(string text)
    {
        return text.Length / 4;
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
/// Ollama 配置
/// </summary>
public class OllamaConfig
{
    /// <summary>基础 URL (默认 http://localhost:11434)</summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";
    
    /// <summary>模型名称 (必需, 如 "llama2", "codellama", "mistral")</summary>
    public string Model { get; set; } = string.Empty;
    
    /// <summary>温度参数 (默认 0.1)</summary>
    public float Temperature { get; set; } = 0.1f;
    
    /// <summary>上下文窗口大小 (默认 4096)</summary>
    public int ContextWindow { get; set; } = 4096;
    
    /// <summary>超时时间 (默认 120 秒)</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(120);
}

/// <summary>
/// Ollama 生成请求
/// </summary>
public class OllamaGenerateRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("options")]
    public OllamaOptions? Options { get; set; }
}

/// <summary>
/// Ollama 生成响应
/// </summary>
public class OllamaGenerateResponse
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("response")]
    public string Response { get; set; } = string.Empty;

    [JsonPropertyName("done")]
    public bool Done { get; set; }

    [JsonPropertyName("context")]
    public List<int>? Context { get; set; }

    [JsonPropertyName("total_duration")]
    public long? TotalDuration { get; set; }

    [JsonPropertyName("load_duration")]
    public long? LoadDuration { get; set; }

    [JsonPropertyName("prompt_eval_count")]
    public int? PromptEvalCount { get; set; }

    [JsonPropertyName("eval_count")]
    public int? EvalCount { get; set; }
}

/// <summary>
/// Ollama 选项
/// </summary>
public class OllamaOptions
{
    [JsonPropertyName("temperature")]
    public float Temperature { get; set; }

    [JsonPropertyName("num_ctx")]
    public int NumContext { get; set; }
}

/// <summary>
/// Ollama 模型信息
/// </summary>
public class OllamaModelInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("modified_at")]
    public string ModifiedAt { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("digest")]
    public string Digest { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public OllamaModelDetails? Details { get; set; }
}

/// <summary>
/// Ollama 模型详情
/// </summary>
public class OllamaModelDetails
{
    [JsonPropertyName("format")]
    public string Format { get; set; } = string.Empty;

    [JsonPropertyName("family")]
    public string Family { get; set; } = string.Empty;

    [JsonPropertyName("families")]
    public List<string>? Families { get; set; }

    [JsonPropertyName("parameter_size")]
    public string ParameterSize { get; set; } = string.Empty;

    [JsonPropertyName("quantization_level")]
    public string QuantizationLevel { get; set; } = string.Empty;
}

/// <summary>
/// Ollama 模型列表响应
/// </summary>
public class OllamaModelListResponse
{
    [JsonPropertyName("models")]
    public List<OllamaModelInfo> Models { get; set; } = new();
}

/// <summary>
/// Ollama 拉取请求
/// </summary>
public class OllamaPullRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
}

/// <summary>
/// JSON 序列化上下文
/// </summary>
[JsonSerializable(typeof(OllamaGenerateRequest))]
[JsonSerializable(typeof(OllamaGenerateResponse))]
[JsonSerializable(typeof(OllamaModelListResponse))]
[JsonSerializable(typeof(OllamaPullRequest))]
internal partial class OllamaJsonContext : JsonSerializerContext
{
}
