using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// AI API 调用客户端（支持 Anthropic Claude 和 OpenAI）
/// </summary>
public class AiApiClient
{
    private readonly HttpClient _httpClient;
    private readonly AiConfigStore _configStore;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public AiApiClient(HttpClient httpClient, AiConfigStore configStore)
    {
        _httpClient = httpClient;
        _configStore = configStore;
    }


    /// <summary>
    /// 调用 AI API 获取工作流 JSON（支持多轮对话和选项覆盖）
    /// </summary>
    public async Task<AiCompletionResult> CompleteAsync(
        string systemPrompt,
        List<ChatMessage> messages,
        AiGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var currentOptions = options ?? _configStore.Get();
        return currentOptions.Provider.ToLower().Replace(" ", "") switch
        {
            "anthropic" => await CallAnthropicAsync(systemPrompt, messages, currentOptions, cancellationToken),
            "openai" or "openaicompatible" => await CallOpenAiAsync(systemPrompt, messages, currentOptions, cancellationToken),
            _ => throw new InvalidOperationException($"不支持的 AI 提供商：{currentOptions.Provider}")
        };
    }

    private async Task<AiCompletionResult> CallAnthropicAsync(
        string systemPrompt,
        List<ChatMessage> messages,
        AiGenerationOptions options,
        CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model = options.Model,
            max_tokens = options.MaxTokens,
            temperature = options.Temperature,
            system = systemPrompt,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray()
        };

        var apiUrl = options.BaseUrl ?? "https://api.anthropic.com/v1/messages";
        var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        request.Headers.Add("x-api-key", options.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));

        using var response = await _httpClient.SendAsync(request, cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"AI API 调用失败 ({response.StatusCode}): {errorContent}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseJson);

        // 提取思维链（Anthropic thinking block）
        string? reasoning = null;
        string? content = null;
        var contentArray = doc.RootElement.GetProperty("content");
        foreach (var block in contentArray.EnumerateArray())
        {
            var blockType = block.GetProperty("type").GetString();
            if (blockType == "thinking" && block.TryGetProperty("thinking", out var thinkingEl))
            {
                reasoning = thinkingEl.GetString();
            }
            else if (blockType == "text" && block.TryGetProperty("text", out var textEl))
            {
                content = textEl.GetString();
            }
        }

        return new AiCompletionResult
        {
            Content = content ?? throw new InvalidOperationException("AI 返回了空响应"),
            Reasoning = reasoning
        };
    }

    private async Task<AiCompletionResult> CallOpenAiAsync(
        string systemPrompt,
        List<ChatMessage> messages,
        AiGenerationOptions options,
        CancellationToken cancellationToken)
    {
        // 转换消息格式：OpenAI 将 system prompt 作为消息的一部分
        var apiMessages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };
        apiMessages.AddRange(messages.Select(m => new { role = m.Role, content = m.Content }));

        // 判断是否为推理模型（如 deepseek-reasoner）
        var isReasonerModel = options.Model.Contains("reasoner", StringComparison.OrdinalIgnoreCase);

        // 推理模型不支持 response_format 和 temperature 参数
        // 推理模型的 max_tokens 包含推理 token，需要更多配额
        object requestBody;
        if (isReasonerModel)
        {
            var reasonerMaxTokens = Math.Max(options.MaxTokens, 16384);
            requestBody = new
            {
                model = options.Model,
                max_tokens = reasonerMaxTokens,
                messages = apiMessages
            };
        }
        else
        {
            requestBody = new
            {
                model = options.Model,
                max_tokens = options.MaxTokens,
                temperature = options.Temperature,
                messages = apiMessages,
                response_format = new { type = "json_object" }
            };
        }

        var apiUrl = "https://api.openai.com/v1/chat/completions";
        if (!string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            apiUrl = options.BaseUrl.Trim();
            // 如果用户填写的 URL 不包含完整的端点路径，自动补齐
            if (!apiUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                if (!apiUrl.EndsWith("/"))
                {
                    apiUrl += "/";
                }

                // 兼容有些用户只填到域名，有些填了 v1 的情况
                // 通常 OpenAI 兼容协议都支持 /v1/chat/completions，如果已经有 v1 则不加
                if (!apiUrl.Contains("/v1/", StringComparison.OrdinalIgnoreCase) &&
                    !apiUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                {
                    apiUrl += "v1/";
                }

                apiUrl += "chat/completions";
            }
        }

        var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        // 推理模型需要更长超时（思维链推理耗时较长）
        var timeoutSeconds = isReasonerModel
            ? Math.Max(options.TimeoutSeconds, 300)
            : options.TimeoutSeconds;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        using var response = await _httpClient.SendAsync(request, cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"AI API 调用失败 ({response.StatusCode}): {errorContent}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseJson);

        var message = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message");

        var content = message.GetProperty("content").GetString();

        // 提取思维链（DeepSeek reasoning_content）
        string? reasoning = null;
        if (message.TryGetProperty("reasoning_content", out var reasoningEl))
        {
            reasoning = reasoningEl.GetString();
        }

        return new AiCompletionResult
        {
            Content = content ?? throw new InvalidOperationException("AI 返回了空响应"),
            Reasoning = reasoning
        };
    }

    // 保留旧方法以兼容
    public async Task<AiCompletionResult> CompleteAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        return await CompleteAsync(
            systemPrompt,
            new List<ChatMessage> { new ChatMessage("user", userMessage) },
            null,
            cancellationToken);
    }
}

/// <summary>
/// 聊天消息
/// </summary>
public record ChatMessage(string Role, string Content);
