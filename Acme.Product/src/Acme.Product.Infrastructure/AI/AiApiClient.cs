// AiApiClient.cs
// AI API 客户端
// 负责调用外部 AI 服务并处理响应结果
// 作者：蘅芜君
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using Acme.Product.Contracts.Messages;

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

    internal const int MaxImageBytes = 20 * 1024 * 1024;

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
        var providerKey = currentOptions.Provider.ToLowerInvariant();
        if (providerKey.Contains("anthropic"))
        {
            return await CallAnthropicAsync(systemPrompt, messages, currentOptions, cancellationToken);
        }

        if (providerKey.Contains("openai"))
        {
            return await CallOpenAiAsync(systemPrompt, messages, currentOptions, cancellationToken);
        }

        throw new InvalidOperationException($"不支持的 AI 提供商：{currentOptions.Provider}");
    }

    /// <summary>
    /// 流式调用 AI API 获取结果（支持思维链和正文的逐块推送）
    /// </summary>
    public async Task<AiCompletionResult> StreamCompleteAsync(
        string systemPrompt,
        List<ChatMessage> messages,
        Action<AiStreamChunk> onChunk,
        AiGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var currentOptions = options ?? _configStore.Get();
        var providerKey = currentOptions.Provider.ToLowerInvariant();
        if (providerKey.Contains("anthropic"))
        {
            return await StreamAnthropicAsync(systemPrompt, messages, currentOptions, onChunk, cancellationToken);
        }

        if (providerKey.Contains("openai"))
        {
            return await StreamOpenAiAsync(systemPrompt, messages, currentOptions, onChunk, cancellationToken);
        }

        throw new InvalidOperationException($"不支持的 AI 提供商：{currentOptions.Provider}");
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
            messages = messages.Select(BuildAnthropicMessage).ToArray()
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

    private async Task<AiCompletionResult> StreamAnthropicAsync(
        string systemPrompt,
        List<ChatMessage> messages,
        AiGenerationOptions options,
        Action<AiStreamChunk> onChunk,
        CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model = options.Model,
            max_tokens = options.MaxTokens,
            temperature = options.Temperature,
            system = systemPrompt,
            messages = messages.Select(BuildAnthropicMessage).ToArray(),
            stream = true
        };

        var apiUrl = options.BaseUrl ?? "https://api.anthropic.com/v1/messages";
        var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        request.Headers.Add("x-api-key", options.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody, _jsonOptions), Encoding.UTF8, "application/json");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        await EnsureSuccessStatusCodeWithDetailsAsync(response, cts.Token);

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var fullContent = new StringBuilder();
        var fullReasoning = new StringBuilder();

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                continue;

            var dataStr = line["data: ".Length..].Trim();
            if (dataStr == "[DONE]")
                break;

            try
            {
                using var doc = JsonDocument.Parse(dataStr);
                var root = doc.RootElement;
                if (!root.TryGetProperty("type", out var typeEl))
                    continue;

                var type = typeEl.GetString();
                if (type == "content_block_delta" && root.TryGetProperty("delta", out var deltaEl))
                {
                    var deltaType = deltaEl.GetProperty("type").GetString();
                    if (deltaType == "thinking_delta" && deltaEl.TryGetProperty("thinking", out var thinkingEl))
                    {
                        var chunk = thinkingEl.GetString() ?? "";
                        fullReasoning.Append(chunk);
                        onChunk(new AiStreamChunk(AiStreamChunkType.Thinking, chunk));
                    }
                    else if (deltaType == "text_delta" && deltaEl.TryGetProperty("text", out var textEl))
                    {
                        var chunk = textEl.GetString() ?? "";
                        fullContent.Append(chunk);
                        onChunk(new AiStreamChunk(AiStreamChunkType.Content, chunk));
                    }
                }
            }
            catch (JsonException) { }
        }

        onChunk(new AiStreamChunk(AiStreamChunkType.Done, string.Empty));
        return new AiCompletionResult { Content = fullContent.ToString(), Reasoning = fullReasoning.ToString() };
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
        apiMessages.AddRange(messages.Select(BuildOpenAiMessage));

        // 判断是否为推理模型（如 deepseek-reasoner）
        var isReasonerModel = options.Model.Contains("reasoner", StringComparison.OrdinalIgnoreCase);

        // 推理模型不支持 response_format 和 temperature 参数
        // 推理模型的 max_tokens 包含推理 token，需要更多配额
        object requestBody;
        if (isReasonerModel)
        {
            var reasonerMaxTokens = Math.Max(options.MaxTokens, 8192);
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

        var content = ExtractOpenAiMessageContent(message);

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

    private async Task<AiCompletionResult> StreamOpenAiAsync(
        string systemPrompt,
        List<ChatMessage> messages,
        AiGenerationOptions options,
        Action<AiStreamChunk> onChunk,
        CancellationToken cancellationToken)
    {
        var apiMessages = new List<object> { new { role = "system", content = systemPrompt } };
        apiMessages.AddRange(messages.Select(BuildOpenAiMessage));

        var isReasonerModel = options.Model.Contains("reasoner", StringComparison.OrdinalIgnoreCase);
        object requestBody;
        if (isReasonerModel)
        {
            var reasonerMaxTokens = Math.Max(options.MaxTokens, 8192);
            requestBody = new { model = options.Model, max_tokens = reasonerMaxTokens, messages = apiMessages, stream = true };
        }
        else
        {
            requestBody = new { model = options.Model, max_tokens = options.MaxTokens, temperature = options.Temperature, messages = apiMessages, response_format = new { type = "json_object" }, stream = true };
        }

        var apiUrl = "https://api.openai.com/v1/chat/completions";
        if (!string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            apiUrl = options.BaseUrl.Trim();
            if (!apiUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                if (!apiUrl.EndsWith("/"))
                    apiUrl += "/";
                if (!apiUrl.Contains("/v1/", StringComparison.OrdinalIgnoreCase) && !apiUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                    apiUrl += "v1/";
                apiUrl += "chat/completions";
            }
        }

        var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody, _jsonOptions), Encoding.UTF8, "application/json");

        var timeoutSeconds = isReasonerModel ? Math.Max(options.TimeoutSeconds, 300) : options.TimeoutSeconds;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        await EnsureSuccessStatusCodeWithDetailsAsync(response, cts.Token);

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var fullContent = new StringBuilder();
        var fullReasoning = new StringBuilder();
        var nonSseBuffer = new StringBuilder();
        var sawSsePayload = false;

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!line.StartsWith("data: ", StringComparison.Ordinal))
            {
                // Some OpenAI-compatible gateways return plain JSON even when stream=true.
                if (!sawSsePayload)
                {
                    nonSseBuffer.AppendLine(line);
                }
                continue;
            }

            sawSsePayload = true;
            var dataStr = line["data: ".Length..].Trim();
            if (dataStr == "[DONE]")
                break;

            try
            {
                using var doc = JsonDocument.Parse(dataStr);
                if (TryExtractReasoningChunk(doc.RootElement, out var reasoningChunk) &&
                    !string.IsNullOrEmpty(reasoningChunk))
                {
                    fullReasoning.Append(reasoningChunk);
                    onChunk(new AiStreamChunk(AiStreamChunkType.Thinking, reasoningChunk));
                }

                if (TryProcessOpenAiStreamPayload(doc.RootElement, out var contentChunk) &&
                    !string.IsNullOrEmpty(contentChunk))
                {
                    fullContent.Append(contentChunk);
                    onChunk(new AiStreamChunk(AiStreamChunkType.Content, contentChunk));
                }
            }
            catch (JsonException) { }
        }

        if (fullContent.Length == 0)
        {
            if (TryExtractJsonObject(fullReasoning.ToString(), out var recoveredJson))
            {
                fullContent.Append(recoveredJson);
                onChunk(new AiStreamChunk(AiStreamChunkType.Content, recoveredJson));
            }
            else if (!sawSsePayload &&
                TryParseOpenAiNonStreamingPayload(nonSseBuffer.ToString(), out var payloadContent, out var payloadReasoning))
            {
                if (!string.IsNullOrWhiteSpace(payloadContent))
                {
                    fullContent.Append(payloadContent);
                    onChunk(new AiStreamChunk(AiStreamChunkType.Content, payloadContent));
                }

                if (fullReasoning.Length == 0 && !string.IsNullOrWhiteSpace(payloadReasoning))
                {
                    fullReasoning.Append(payloadReasoning);
                }
            }
            else
            {
                // Last resort: if stream returned no content chunk at all, fallback to non-stream call once.
                try
                {
                    var fallback = await CallOpenAiAsync(systemPrompt, messages, options, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(fallback.Content))
                    {
                        fullContent.Append(fallback.Content);
                        onChunk(new AiStreamChunk(AiStreamChunkType.Content, fallback.Content));
                    }

                    if (fullReasoning.Length == 0 && !string.IsNullOrWhiteSpace(fallback.Reasoning))
                    {
                        fullReasoning.Append(fallback.Reasoning);
                    }
                }
                catch
                {
                    // Keep legacy behavior if fallback request also fails: return empty content and let caller retry.
                }
            }
        }

        onChunk(new AiStreamChunk(AiStreamChunkType.Done, string.Empty));
        return new AiCompletionResult { Content = fullContent.ToString(), Reasoning = fullReasoning.ToString() };
    }

    // 保留旧方法以兼容
    private static object BuildAnthropicMessage(ChatMessage message)
    {
        if (!message.HasRichContent)
        {
            return new { role = message.Role, content = message.Content };
        }

        var contentParts = BuildAnthropicContentParts(message);
        if (contentParts.Count == 0)
        {
            return new { role = message.Role, content = message.Content };
        }

        return new { role = message.Role, content = contentParts };
    }

    private static List<object> BuildAnthropicContentParts(ChatMessage message)
    {
        var parts = new List<object>();

        if (!string.IsNullOrWhiteSpace(message.Content))
        {
            parts.Add(new { type = "text", text = message.Content });
        }

        if (message.Parts == null)
        {
            return parts;
        }

        foreach (var part in message.Parts)
        {
            if (part.Type == ChatContentPartType.Text)
            {
                if (!string.IsNullOrWhiteSpace(part.Text))
                {
                    parts.Add(new { type = "text", text = part.Text });
                }
                continue;
            }

            if (part.Type == ChatContentPartType.Image &&
                TryReadImageData(part.ImagePath, out var imageBase64, out var mediaType))
            {
                parts.Add(new
                {
                    type = "image",
                    source = new
                    {
                        type = "base64",
                        media_type = mediaType,
                        data = imageBase64
                    }
                });
            }
        }

        return parts;
    }

    private static object BuildOpenAiMessage(ChatMessage message)
    {
        if (!message.HasRichContent)
        {
            return new { role = message.Role, content = message.Content };
        }

        var contentParts = BuildOpenAiContentParts(message);
        if (contentParts.Count == 0)
        {
            return new { role = message.Role, content = message.Content };
        }

        return new { role = message.Role, content = contentParts };
    }

    private static List<object> BuildOpenAiContentParts(ChatMessage message)
    {
        var parts = new List<object>();

        if (!string.IsNullOrWhiteSpace(message.Content))
        {
            parts.Add(new { type = "text", text = message.Content });
        }

        if (message.Parts == null)
        {
            return parts;
        }

        foreach (var part in message.Parts)
        {
            if (part.Type == ChatContentPartType.Text)
            {
                if (!string.IsNullOrWhiteSpace(part.Text))
                {
                    parts.Add(new { type = "text", text = part.Text });
                }
                continue;
            }

            if (part.Type == ChatContentPartType.Image &&
                TryReadImageData(part.ImagePath, out var imageBase64, out var mediaType))
            {
                parts.Add(new
                {
                    type = "image_url",
                    image_url = new
                    {
                        url = $"data:{mediaType};base64,{imageBase64}",
                        detail = part.Detail
                    }
                });
            }
        }

        return parts;
    }

    private static bool TryReadImageData(
        string? imagePath,
        out string imageBase64,
        out string mediaType)
    {
        imageBase64 = string.Empty;
        mediaType = string.Empty;

        if (string.IsNullOrWhiteSpace(imagePath))
            return false;

        var normalizedPath = imagePath.Trim();
        if (!File.Exists(normalizedPath))
            return false;

        var extension = Path.GetExtension(normalizedPath);
        mediaType = GetImageMediaType(extension);
        if (string.IsNullOrWhiteSpace(mediaType))
            return false;

        var fileInfo = new FileInfo(normalizedPath);
        if (fileInfo.Length <= 0 || fileInfo.Length > MaxImageBytes)
            return false;

        var bytes = File.ReadAllBytes(normalizedPath);
        imageBase64 = Convert.ToBase64String(bytes);
        return true;
    }

    private static string GetImageMediaType(string? extension) =>
        extension?.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            ".tif" => "image/tiff",
            ".tiff" => "image/tiff",
            _ => string.Empty
        };

    internal static bool IsSupportedImageExtension(string? extension) =>
        !string.IsNullOrWhiteSpace(GetImageMediaType(extension));

    private static string? ExtractOpenAiMessageContent(JsonElement message)
    {
        if (!message.TryGetProperty("content", out var contentElement))
            return null;

        if (contentElement.ValueKind == JsonValueKind.String)
            return contentElement.GetString();

        if (contentElement.ValueKind != JsonValueKind.Array)
            return null;

        var sb = new StringBuilder();
        foreach (var part in contentElement.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.String)
            {
                sb.Append(part.GetString());
                continue;
            }

            if (part.ValueKind != JsonValueKind.Object)
                continue;

            if (part.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
            {
                sb.Append(textEl.GetString());
                continue;
            }

            if (part.TryGetProperty("type", out var typeEl) &&
                typeEl.ValueKind == JsonValueKind.String &&
                typeEl.GetString() == "text" &&
                part.TryGetProperty("content", out var contentEl) &&
                contentEl.ValueKind == JsonValueKind.String)
            {
                sb.Append(contentEl.GetString());
            }
        }

        return sb.Length > 0 ? sb.ToString() : null;
    }

    private static bool TryExtractDeltaContent(JsonElement delta, out string chunk)
    {
        chunk = string.Empty;

        if (TryExtractTextProperty(delta, "content", out chunk))
            return !string.IsNullOrEmpty(chunk);

        if (TryExtractTextProperty(delta, "text", out chunk))
            return !string.IsNullOrEmpty(chunk);

        if (TryExtractTextProperty(delta, "output_text", out chunk))
            return !string.IsNullOrEmpty(chunk);

        return false;
    }

    private static bool TryProcessOpenAiStreamPayload(JsonElement root, out string contentChunk)
    {
        contentChunk = string.Empty;

        if (root.TryGetProperty("choices", out var choices) &&
            choices.ValueKind == JsonValueKind.Array &&
            choices.GetArrayLength() > 0)
        {
            var choice = choices[0];

            if (choice.TryGetProperty("delta", out var delta) &&
                delta.ValueKind == JsonValueKind.Object &&
                TryExtractDeltaContent(delta, out contentChunk))
            {
                return !string.IsNullOrEmpty(contentChunk);
            }

            if (TryExtractChoiceContent(choice, out contentChunk))
            {
                return !string.IsNullOrEmpty(contentChunk);
            }
        }

        if (root.TryGetProperty("type", out var typeEl) &&
            typeEl.ValueKind == JsonValueKind.String)
        {
            var eventType = typeEl.GetString() ?? string.Empty;

            if (eventType.Equals("response.output_text.delta", StringComparison.OrdinalIgnoreCase) &&
                TryExtractTextProperty(root, "delta", out contentChunk))
            {
                return !string.IsNullOrEmpty(contentChunk);
            }
        }

        if (TryExtractTextProperty(root, "output_text", out contentChunk))
            return !string.IsNullOrEmpty(contentChunk);

        return TryExtractTextProperty(root, "text", out contentChunk) && !string.IsNullOrEmpty(contentChunk);
    }

    private static bool TryExtractReasoningChunk(JsonElement root, out string reasoningChunk)
    {
        reasoningChunk = string.Empty;

        if (root.TryGetProperty("choices", out var choices) &&
            choices.ValueKind == JsonValueKind.Array &&
            choices.GetArrayLength() > 0)
        {
            var choice = choices[0];
            if (choice.TryGetProperty("delta", out var delta) &&
                delta.ValueKind == JsonValueKind.Object)
            {
                if (TryExtractTextProperty(delta, "reasoning_content", out reasoningChunk))
                    return !string.IsNullOrEmpty(reasoningChunk);

                if (TryExtractTextProperty(delta, "reasoning", out reasoningChunk))
                    return !string.IsNullOrEmpty(reasoningChunk);

                if (TryExtractTextProperty(delta, "thinking", out reasoningChunk))
                    return !string.IsNullOrEmpty(reasoningChunk);
            }
        }

        if (root.TryGetProperty("type", out var typeEl) &&
            typeEl.ValueKind == JsonValueKind.String)
        {
            var eventType = typeEl.GetString() ?? string.Empty;
            if (eventType.Contains("reasoning", StringComparison.OrdinalIgnoreCase) ||
                eventType.Contains("thinking", StringComparison.OrdinalIgnoreCase))
            {
                if (TryExtractTextProperty(root, "delta", out reasoningChunk))
                    return !string.IsNullOrEmpty(reasoningChunk);
            }
        }

        if (TryExtractTextProperty(root, "reasoning_content", out reasoningChunk))
            return !string.IsNullOrEmpty(reasoningChunk);

        if (TryExtractTextProperty(root, "reasoning", out reasoningChunk))
            return !string.IsNullOrEmpty(reasoningChunk);

        return TryExtractTextProperty(root, "thinking", out reasoningChunk) && !string.IsNullOrEmpty(reasoningChunk);
    }

    private static bool TryExtractChoiceContent(JsonElement choice, out string contentChunk)
    {
        contentChunk = string.Empty;

        if (TryExtractTextProperty(choice, "text", out contentChunk))
            return !string.IsNullOrEmpty(contentChunk);

        if (choice.TryGetProperty("message", out var message) &&
            message.ValueKind == JsonValueKind.Object &&
            ExtractOpenAiMessageContent(message) is { } messageContent &&
            !string.IsNullOrWhiteSpace(messageContent))
        {
            contentChunk = messageContent;
            return true;
        }

        return false;
    }

    private static bool TryExtractTextProperty(JsonElement element, string propertyName, out string text)
    {
        text = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property))
            return false;

        return TryExtractTextValue(property, out text);
    }

    private static bool TryExtractTextValue(JsonElement element, out string text)
    {
        text = string.Empty;

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                text = element.GetString() ?? string.Empty;
                return !string.IsNullOrEmpty(text);
            case JsonValueKind.Array:
            {
                var sb = new StringBuilder();
                foreach (var item in element.EnumerateArray())
                {
                    if (TryExtractTextValue(item, out var itemText) && !string.IsNullOrEmpty(itemText))
                    {
                        sb.Append(itemText);
                    }
                }

                text = sb.ToString();
                return !string.IsNullOrEmpty(text);
            }
            case JsonValueKind.Object:
            {
                if (TryExtractTextProperty(element, "text", out text) && !string.IsNullOrEmpty(text))
                    return true;
                if (TryExtractTextProperty(element, "content", out text) && !string.IsNullOrEmpty(text))
                    return true;
                if (TryExtractTextProperty(element, "value", out text) && !string.IsNullOrEmpty(text))
                    return true;
                if (TryExtractTextProperty(element, "delta", out text) && !string.IsNullOrEmpty(text))
                    return true;
                if (TryExtractTextProperty(element, "output_text", out text) && !string.IsNullOrEmpty(text))
                    return true;
                return false;
            }
            default:
                return false;
        }
    }

    private static bool TryExtractJsonObject(string text, out string jsonObject)
    {
        jsonObject = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var firstBrace = text.IndexOf('{');
        if (firstBrace < 0)
            return false;

        var depth = 0;
        var inString = false;
        var escaped = false;
        for (var i = firstBrace; i < text.Length; i++)
        {
            var ch = text[i];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (ch == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }

            if (ch == '{')
            {
                depth++;
            }
            else if (ch == '}')
            {
                depth--;
                if (depth == 0)
                {
                    jsonObject = text[firstBrace..(i + 1)].Trim();
                    return !string.IsNullOrWhiteSpace(jsonObject);
                }
            }
        }

        return false;
    }

    private static bool TryParseOpenAiNonStreamingPayload(string payload, out string content, out string reasoning)
    {
        content = string.Empty;
        reasoning = string.Empty;

        if (string.IsNullOrWhiteSpace(payload))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (!doc.RootElement.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
            {
                return false;
            }

            var choice = choices[0];
            if (choice.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.Object)
            {
                content = ExtractOpenAiMessageContent(message) ?? string.Empty;
                if (TryExtractTextProperty(message, "reasoning_content", out var reasoningFromMessage))
                {
                    reasoning = reasoningFromMessage;
                }
            }
            else
            {
                TryExtractChoiceContent(choice, out content);
            }

            return !string.IsNullOrWhiteSpace(content) || !string.IsNullOrWhiteSpace(reasoning);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static async Task EnsureSuccessStatusCodeWithDetailsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        string errorContent = string.Empty;
        try
        {
            errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            // ignore body read failure
        }

        if (!string.IsNullOrWhiteSpace(errorContent))
        {
            errorContent = errorContent.Trim();
            if (errorContent.Length > 600)
                errorContent = errorContent[..600] + "...";
        }

        var reason = response.ReasonPhrase ?? "Request Failed";
        var message = string.IsNullOrWhiteSpace(errorContent)
            ? $"Response status code does not indicate success: {(int)response.StatusCode} ({reason})."
            : $"AI API 调用失败 ({(int)response.StatusCode} {reason}): {errorContent}";

        throw new HttpRequestException(message, null, response.StatusCode);
    }

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
public sealed class ChatMessage
{
    public string Role { get; }
    public string Content { get; }
    public IReadOnlyList<ChatMessageContentPart>? Parts { get; }

    public bool HasRichContent => Parts != null && Parts.Count > 0;

    public ChatMessage(string role, string content)
    {
        Role = role;
        Content = content;
        Parts = null;
    }

    public ChatMessage(string role, IReadOnlyList<ChatMessageContentPart> parts)
    {
        Role = role;
        Content = string.Empty;
        Parts = parts;
    }
}

public static class ChatContentPartType
{
    public const string Text = "text";
    public const string Image = "image";
}

public sealed class ChatMessageContentPart
{
    public string Type { get; private set; } = ChatContentPartType.Text;
    public string? Text { get; private set; }
    public string? ImagePath { get; private set; }
    public string Detail { get; private set; } = "high";

    private ChatMessageContentPart() { }

    public static ChatMessageContentPart TextPart(string text) =>
        new()
        {
            Type = ChatContentPartType.Text,
            Text = text
        };

    public static ChatMessageContentPart ImageFile(string imagePath, string detail = "high") =>
        new()
        {
            Type = ChatContentPartType.Image,
            ImagePath = imagePath,
            Detail = string.IsNullOrWhiteSpace(detail) ? "high" : detail
        };
}
