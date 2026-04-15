// HttpRequestOperator.cs
// HTTP 请求算子 - Sprint 3 Task 3.5a
// 调用 REST API，触发 MES/AGV 等外部服务
// 作者：蘅芜君

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// HTTP 请求算子 - 调用 REST API
/// 
/// 功能：
/// - 支持 GET/POST/PUT/DELETE
/// - 支持自定义 Headers
/// - 支持 JSON 请求体
/// - 支持超时配置
/// - 支持重试机制
/// 
/// 使用场景：
/// - 调用 MES API 上报检测结果
/// - 触发 AGV 搬运指令
/// - 查询外部系统数据
/// </summary>
[OperatorMeta(
    DisplayName = "HTTP 请求",
    Description = "调用外部 REST API",
    Category = "通信",
    IconName = "http"
)]
[InputPort("Body", "请求体", PortDataType.String, IsRequired = false)]
[InputPort("Headers", "请求头", PortDataType.Any, IsRequired = false)]
[OutputPort("Response", "响应内容", PortDataType.String)]
[OutputPort("StatusCode", "状态码", PortDataType.Integer)]
[OutputPort("IsSuccess", "是否成功", PortDataType.Boolean)]
[OperatorParam("Url", "API 地址", "string", DefaultValue = "http://localhost:5000/api")]
[OperatorParam("Method", "方法", "enum", DefaultValue = "POST", Options = new[] { "GET|GET", "POST|POST", "PUT|PUT", "DELETE|DELETE" })]
[OperatorParam("TimeoutMs", "超时(ms)", "int", DefaultValue = 10000, Min = 1000, Max = 60000)]
[OperatorParam("RetryCount", "最大重试", "int", DefaultValue = 0, Min = 0, Max = 5)]
[OperatorParam("ContentType", "内容类型", "string", DefaultValue = "application/json")]
[OperatorParam("RetryDelayMs", "重试延迟(ms)", "int", DefaultValue = 1000, Min = 0, Max = 10000)]
public class HttpRequestOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.HttpRequest;

    private static readonly HttpClient _httpClient = new();

    public HttpRequestOperator(ILogger<HttpRequestOperator> logger) : base(logger) { }

    protected override async Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        // 获取参数
        var url = GetStringParam(@operator, "Url", "");
        var method = GetStringParam(@operator, "Method", "POST");
        var contentType = GetStringParam(@operator, "ContentType", "application/json");
        var timeoutMs = GetIntParam(@operator, "TimeoutMs", 10000, 1000, 60000);
        var retryCount = GetIntParam(@operator, "RetryCount", 0, 0, 5);
        var retryDelayMs = GetIntParam(@operator, "RetryDelayMs", 1000, 0, 10000);

        if (string.IsNullOrWhiteSpace(url))
        {
            return OperatorExecutionOutput.Failure("Url 参数不能为空");
        }

        // 构建请求体
        string? body = null;
        if (inputs != null)
        {
            if (inputs.TryGetValue("Body", out var bodyObj) && bodyObj != null)
            {
                body = bodyObj.ToString();
            }
            else if (inputs.Count > 0)
            {
                // 将所有输入序列化为 JSON
                body = JsonSerializer.Serialize(inputs);
            }
        }

        // 构建 Headers
        var headers = new Dictionary<string, string>();
        if (inputs != null && inputs.TryGetValue("Headers", out var headersObj) && headersObj is Dictionary<string, object> headersDict)
        {
            foreach (var (key, value) in headersDict)
            {
                headers[key] = value?.ToString() ?? "";
            }
        }

        // 添加默认 Content-Type
        if (!headers.ContainsKey("Content-Type") && !headers.ContainsKey("content-type"))
        {
            headers["Content-Type"] = contentType;
        }

        // 执行请求（带重试）
        for (int attempt = 0; attempt <= retryCount; attempt++)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

                var result = await ExecuteRequestAsync(url, method, body, headers, linkedCts.Token);
                
                if (result.IsSuccess)
                {
                    return OperatorExecutionOutput.Success(result.OutputData);
                }

                // 如果不是最后一次尝试，等待后重试
                if (attempt < retryCount)
                {
                    Logger.LogWarning("[HttpRequest] 请求失败，{Delay}ms 后重试 ({Attempt}/{RetryCount})", 
                        retryDelayMs, attempt + 1, retryCount);
                    await Task.Delay(retryDelayMs, cancellationToken);
                }
                else
                {
                    return OperatorExecutionOutput.Failure(result.ErrorMessage ?? "HTTP 请求失败");
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // 超时
                if (attempt < retryCount)
                {
                    Logger.LogWarning("[HttpRequest] 请求超时，{Delay}ms 后重试 ({Attempt}/{RetryCount})", 
                        retryDelayMs, attempt + 1, retryCount);
                    await Task.Delay(retryDelayMs, cancellationToken);
                }
                else
                {
                    return OperatorExecutionOutput.Failure($"HTTP 请求超时 ({timeoutMs}ms)");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[HttpRequest] 请求异常");
                
                if (attempt < retryCount)
                {
                    await Task.Delay(retryDelayMs, cancellationToken);
                }
                else
                {
                    return OperatorExecutionOutput.Failure($"HTTP 请求异常: {ex.Message}");
                }
            }
        }

        return OperatorExecutionOutput.Failure("HTTP 请求失败（超出重试次数）");
    }

    private async Task<RequestResult> ExecuteRequestAsync(
        string url, 
        string method, 
        string? body,
        Dictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method.ToUpper()), url);

        // 添加 Headers
        foreach (var (key, value) in headers)
        {
            if (key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                request.Content = body != null 
                    ? new StringContent(body, Encoding.UTF8, value)
                    : null;
            }
            else
            {
                request.Headers.TryAddWithoutValidation(key, value);
            }
        }

        // 如果没有设置 Content，但有 Body
        if (request.Content == null && body != null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        var outputData = new Dictionary<string, object>
        {
            { "StatusCode", (int)response.StatusCode },
            { "IsSuccess", response.IsSuccessStatusCode },
            { "IsSuccessStatusCode", response.IsSuccessStatusCode },
            { "Response", responseBody },
            { "ResponseBody", responseBody }
        };

        if (response.IsSuccessStatusCode)
        {
            return new RequestResult { IsSuccess = true, OutputData = outputData };
        }
        else
        {
            return new RequestResult 
            { 
                IsSuccess = false, 
                ErrorMessage = $"HTTP {(int)response.StatusCode}: {responseBody}",
                OutputData = outputData
            };
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var url = GetStringParam(@operator, "Url", "");
        var method = GetStringParam(@operator, "Method", "POST");

        if (string.IsNullOrWhiteSpace(url))
        {
            return ValidationResult.Invalid("Url 不能为空");
        }

        var validMethods = new[] { "GET", "POST", "PUT", "DELETE", "PATCH" };
        if (!validMethods.Contains(method.ToUpper()))
        {
            return ValidationResult.Invalid($"Method 必须是以下之一: {string.Join(", ", validMethods)}");
        }

        return ValidationResult.Valid();
    }

    private class RequestResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object>? OutputData { get; set; }
    }
}
