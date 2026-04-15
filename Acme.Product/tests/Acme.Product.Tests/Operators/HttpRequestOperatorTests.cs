using System.Net;
using System.Net.Sockets;
using System.Text;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class HttpRequestOperatorTests
{
    private readonly HttpRequestOperator _operator;

    public HttpRequestOperatorTests()
    {
        _operator = new HttpRequestOperator(Substitute.For<ILogger<HttpRequestOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeHttpRequest()
    {
        _operator.OperatorType.Should().Be(OperatorType.HttpRequest);
    }

    [Fact]
    public void ValidateParameters_WithInvalidMethod_ShouldReturnInvalid()
    {
        var op = new Operator("test", OperatorType.HttpRequest, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Url", "http://127.0.0.1:8080/api", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Method", "TRACE", "string"));

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithLocalServer_ShouldExposeResponseCompatibilityKeys()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        const string responseBody = "{\"ok\":true}";
        var serverTask = ServeOnceAsync(listener, responseBody);

        var op = new Operator("test", OperatorType.HttpRequest, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Url", $"http://127.0.0.1:{port}/ingest", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Method", "POST", "string"));
        op.AddParameter(TestHelpers.CreateParameter("TimeoutMs", 3000, "int"));
        op.AddParameter(TestHelpers.CreateParameter("RetryCount", 0, "int"));
        op.AddParameter(TestHelpers.CreateParameter("ContentType", "application/json", "string"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Body"] = "{\"job\":\"demo\"}",
            ["Headers"] = new Dictionary<string, object>
            {
                ["X-Correlation-Id"] = "abc-123"
            }
        });

        var request = await serverTask.WaitAsync(TimeSpan.FromSeconds(5));

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().NotBeNull();
        result.OutputData!["StatusCode"].Should().Be(200);
        result.OutputData["IsSuccess"].Should().Be(true);
        result.OutputData["IsSuccessStatusCode"].Should().Be(true);
        result.OutputData["Response"].Should().Be(responseBody);
        result.OutputData["ResponseBody"].Should().Be(responseBody);

        request.Method.Should().Be("POST");
        request.Path.Should().Be("/ingest");
        request.Body.Should().Be("{\"job\":\"demo\"}");
        request.Headers["X-Correlation-Id"].Should().Be("abc-123");
    }

    private static async Task<CapturedRequest> ServeOnceAsync(TcpListener listener, string responseBody)
    {
        using var client = await listener.AcceptTcpClientAsync();
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        var requestLine = await reader.ReadLineAsync();
        requestLine.Should().NotBeNullOrWhiteSpace();

        var requestLineParts = requestLine!.Split(' ');
        var method = requestLineParts[0];
        var path = requestLineParts[1];

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int contentLength = 0;

        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(line))
            {
                break;
            }

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            headers[key] = value;

            if (key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                contentLength = int.Parse(value);
            }
        }

        string body = string.Empty;
        if (contentLength > 0)
        {
            var buffer = new char[contentLength];
            var read = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
            body = new string(buffer, 0, read);
        }

        var responseBytes = Encoding.UTF8.GetBytes(responseBody);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
        {
            NewLine = "\r\n"
        };

        await writer.WriteLineAsync("HTTP/1.1 200 OK");
        await writer.WriteLineAsync("Content-Type: application/json; charset=utf-8");
        await writer.WriteLineAsync($"Content-Length: {responseBytes.Length}");
        await writer.WriteLineAsync("Connection: close");
        await writer.WriteLineAsync();
        await writer.WriteAsync(responseBody);
        await writer.FlushAsync();

        return new CapturedRequest(method, path, body, headers);
    }

    private sealed record CapturedRequest(
        string Method,
        string Path,
        string Body,
        Dictionary<string, string> Headers);
}
