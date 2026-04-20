using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace Acme.Product.Tests.Operators;

public class TcpCommunicationOperatorTests
{
    private readonly TcpCommunicationOperator _operator;

    public TcpCommunicationOperatorTests()
    {
        _operator = new TcpCommunicationOperator(Substitute.For<ILogger<TcpCommunicationOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeTcpCommunication()
    {
        _operator.OperatorType.Should().Be(OperatorType.TcpCommunication);
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("test", OperatorType.TcpCommunication, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_WithInvalidPort_ShouldReturnInvalid()
    {
        var op = new Operator("test", OperatorType.TcpCommunication, 0, 0);
        op.AddParameter(new(Guid.NewGuid(), "Port", "Port", "", "int", 70000, 0, 65535, true));
        var result = _operator.ValidateParameters(op);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateParameters_WithEmptyHost_ShouldReturnInvalid()
    {
        var op = new Operator("test", OperatorType.TcpCommunication, 0, 0);
        op.AddParameter(new(Guid.NewGuid(), "IpAddress", "Host", "", "string", "", "", "", true));
        var result = _operator.ValidateParameters(op);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithUnsupportedServerMode_ShouldReturnFailure()
    {
        var op = new Operator("test", OperatorType.TcpCommunication, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Mode", "Server", "string"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>());

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteAsync_WithConcurrentClientCalls_ShouldKeepRequestResponseConsistent()
    {
        const int requestCount = 8;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var serverTask = RunDelayedEchoServerAsync(listener, requestCount, cts.Token);

        var payloads = Enumerable.Range(1, requestCount).Select(i => $"R{i:000}").ToArray();

        try
        {
            var tasks = payloads.Select(p => ExecuteClientAsync(port, p, 2500, cts.Token)).ToArray();
            var results = await Task.WhenAll(tasks);

            results.Should().OnlyContain(r => r.IsSuccess);
            for (var i = 0; i < payloads.Length; i++)
            {
                GetResponse(results[i]).Should().Be(ToResponse(payloads[i]));
            }
        }
        finally
        {
            cts.Cancel();
            listener.Stop();
            await IgnoreServerTerminationAsync(serverTask);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenConnectionClosedByPeer_ShouldReconnectOnNextCall()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var serverTask = RunDropFirstThenEchoAsync(listener, cts.Token);

        try
        {
            var first = await ExecuteClientAsync(port, "R001", 1200, cts.Token);
            first.IsSuccess.Should().BeFalse();

            var second = await ExecuteClientAsync(port, "R002", 2500, cts.Token);
            second.IsSuccess.Should().BeTrue();
            GetResponse(second).Should().Be("S002");
        }
        finally
        {
            cts.Cancel();
            listener.Stop();
            await IgnoreServerTerminationAsync(serverTask);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithFailedUniqueEndpoints_ShouldNotRetainPerKeyLocks()
    {
        ResetStaticTcpState();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var tasks = Enumerable.Range(0, 24)
                .Select(i => ExecuteClientAsync(
                    port: 45000 + i,
                    sendData: $"R{i:000}",
                    timeoutMs: 200,
                    cancellationToken: cts.Token))
                .ToArray();

            var results = await Task.WhenAll(tasks);
            results.Should().OnlyContain(result => !result.IsSuccess);

            GetStaticDictionaryCount("_connectionLocks").Should().Be(0);
            GetStaticDictionaryCount("_requestResponseLocks").Should().Be(0);
        }
        finally
        {
            ResetStaticTcpState();
        }
    }

    private async Task<OperatorExecutionOutput> ExecuteClientAsync(
        int port,
        string sendData,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        var op = new Operator("tcp-client", OperatorType.TcpCommunication, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Mode", "Client", "string"));
        op.AddParameter(TestHelpers.CreateParameter("IpAddress", "127.0.0.1", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Port", port, "int"));
        op.AddParameter(TestHelpers.CreateParameter("SendData", sendData, "string"));
        op.AddParameter(TestHelpers.CreateParameter("Timeout", timeoutMs, "int"));
        op.AddParameter(TestHelpers.CreateParameter("Encoding", "UTF8", "string"));

        return await _operator.ExecuteAsync(op, cancellationToken: cancellationToken);
    }

    private static string GetResponse(OperatorExecutionOutput result)
    {
        result.OutputData.Should().NotBeNull();
        result.OutputData!.Should().ContainKey("Response");
        return result.OutputData["Response"].Should().BeOfType<string>().Subject;
    }

    private static string ToResponse(string request)
    {
        return $"S{request[1..]}";
    }

    private static async Task RunDelayedEchoServerAsync(TcpListener listener, int expectedRequests, CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        using var stream = client.GetStream();

        for (var i = 0; i < expectedRequests; i++)
        {
            var requestBytes = await ReadExactAsync(stream, 4, cancellationToken);
            var request = Encoding.UTF8.GetString(requestBytes);
            var requestId = int.Parse(request[1..]);
            var delay = requestId % 2 == 0 ? 140 : 20;
            await Task.Delay(delay, cancellationToken);

            var response = Encoding.UTF8.GetBytes(ToResponse(request));
            await stream.WriteAsync(response, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
    }

    private static async Task RunDropFirstThenEchoAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        using (var firstClient = await listener.AcceptTcpClientAsync(cancellationToken))
        using (var firstStream = firstClient.GetStream())
        {
            _ = await ReadExactAsync(firstStream, 4, cancellationToken);
            firstClient.Client.Shutdown(SocketShutdown.Both);
            firstClient.Close();
        }

        using var secondClient = await listener.AcceptTcpClientAsync(cancellationToken);
        using var secondStream = secondClient.GetStream();

        var requestBytes = await ReadExactAsync(secondStream, 4, cancellationToken);
        var request = Encoding.UTF8.GetString(requestBytes);
        var response = Encoding.UTF8.GetBytes(ToResponse(request));

        await secondStream.WriteAsync(response, cancellationToken);
        await secondStream.FlushAsync(cancellationToken);
    }

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var offset = 0;

        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);
            if (read == 0)
            {
                throw new IOException("Peer closed connection before all request bytes were received.");
            }

            offset += read;
        }

        return buffer;
    }

    private static async Task IgnoreServerTerminationAsync(Task serverTask)
    {
        try
        {
            await serverTask;
        }
        catch (OperationCanceledException)
        {
            // Expected on test cleanup.
        }
        catch (SocketException)
        {
            // Listener stop during cleanup can interrupt Accept.
        }
        catch (ObjectDisposedException)
        {
            // Listener/stream may already be disposed during cleanup.
        }
    }

    private static int GetStaticDictionaryCount(string fieldName)
    {
        var field = typeof(TcpCommunicationOperator).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
        field.Should().NotBeNull();

        var dictionary = field!.GetValue(null) as IDictionary;
        dictionary.Should().NotBeNull();
        return dictionary!.Count;
    }

    private static void ResetStaticTcpState()
    {
        ResetStaticDictionary("_connectionPool");
        ResetStaticDictionary("_streamPool");
        ResetStaticDictionary("_connectionLocks");
        ResetStaticDictionary("_requestResponseLocks");
    }

    private static void ResetStaticDictionary(string fieldName)
    {
        var field = typeof(TcpCommunicationOperator).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
        field.Should().NotBeNull();

        if (field!.GetValue(null) is not IDictionary dictionary)
        {
            return;
        }

        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Value is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        dictionary.Clear();
    }
}
