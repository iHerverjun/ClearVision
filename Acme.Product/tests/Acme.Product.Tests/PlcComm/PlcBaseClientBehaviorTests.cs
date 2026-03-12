using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Acme.PlcComm.Core;
using FluentAssertions;

namespace Acme.Product.Tests.PlcComm;

public class PlcBaseClientBehaviorTests
{

    [Fact]
    public void GetRetryDelay_WhenExponentialBackoffExceedsMax_ShouldClampToMaxRetryInterval()
    {
        var sut = new TestPlcClient(
            readCore: (_, _, _) => Task.FromResult(OperateResult<byte[]>.Success(new byte[] { 0x01 })),
            writeCore: (_, _, _) => Task.FromResult(OperateResult.Success()));

        sut.ReconnectPolicy = new ReconnectPolicy
        {
            Enabled = true,
            ExponentialBackoff = true,
            RetryInterval = TimeSpan.FromSeconds(1),
            MaxRetryInterval = TimeSpan.FromSeconds(3)
        };

        InvokeGetRetryDelay(sut, 0).Should().Be(TimeSpan.FromSeconds(1));
        InvokeGetRetryDelay(sut, 1).Should().Be(TimeSpan.FromSeconds(2));
        InvokeGetRetryDelay(sut, 2).Should().Be(TimeSpan.FromSeconds(3));
        InvokeGetRetryDelay(sut, 5).Should().Be(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void GetRetryDelay_WhenFixedRetryIntervalExceedsMax_ShouldClampToMaxRetryInterval()
    {
        var sut = new TestPlcClient(
            readCore: (_, _, _) => Task.FromResult(OperateResult<byte[]>.Success(new byte[] { 0x01 })),
            writeCore: (_, _, _) => Task.FromResult(OperateResult.Success()));

        sut.ReconnectPolicy = new ReconnectPolicy
        {
            Enabled = true,
            ExponentialBackoff = false,
            RetryInterval = TimeSpan.FromSeconds(5),
            MaxRetryInterval = TimeSpan.FromSeconds(2)
        };

        InvokeGetRetryDelay(sut, 0).Should().Be(TimeSpan.FromSeconds(2));
        InvokeGetRetryDelay(sut, 3).Should().Be(TimeSpan.FromSeconds(2));
    }    [Fact]
    public async Task ReadAsync_WhenReadCoreThrowsIOException_ShouldNotRetryOperation()
    {
        var sut = new TestPlcClient(
            readCore: (_, _, _) => throw new IOException("half packet"),
            writeCore: (_, _, _) => Task.FromResult(OperateResult.Success()));

        sut.ReconnectPolicy = new ReconnectPolicy
        {
            Enabled = true,
            MaxRetries = 3,
            RetryInterval = TimeSpan.Zero,
            MaxRetryInterval = TimeSpan.Zero
        };

        var result = await sut.ReadAsync("D0", 1);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("通信IO异常");
        sut.ReadCoreCallCount.Should().Be(1);
        sut.DisconnectCoreCallCount.Should().Be(1);
    }

    [Fact]
    public async Task WriteAsync_WhenWriteCoreThrowsIOException_ShouldNotRetryOperation()
    {
        var sut = new TestPlcClient(
            readCore: (_, _, _) => Task.FromResult(OperateResult<byte[]>.Success(new byte[] { 0x01 })),
            writeCore: (_, _, _) => throw new IOException("write lost"));

        sut.ReconnectPolicy = new ReconnectPolicy
        {
            Enabled = true,
            MaxRetries = 3,
            RetryInterval = TimeSpan.Zero,
            MaxRetryInterval = TimeSpan.Zero
        };

        var result = await sut.WriteAsync("D0", new byte[] { 0x01 });

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("通信IO异常");
        sut.WriteCoreCallCount.Should().Be(1);
        sut.DisconnectCoreCallCount.Should().Be(1);
    }

    [Fact]
    public async Task ReadAsync_WhenReadCoreReturnsFailure_ShouldRaiseErrorOccurred()
    {
        var sut = new TestPlcClient(
            readCore: (_, _, _) => Task.FromResult(OperateResult<byte[]>.Failure(42, "read failed")),
            writeCore: (_, _, _) => Task.FromResult(OperateResult.Success()));

        PlcErrorEventArgs? captured = null;
        sut.ErrorOccurred += (_, args) => captured = args;

        var result = await sut.ReadAsync("D0", 1);

        result.IsSuccess.Should().BeFalse();
        captured.Should().NotBeNull();
        captured!.ErrorCode.Should().Be(42);
        captured.Message.Should().Be("read failed");
        captured.OperationType.Should().Be("Read");
    }

    [Fact]
    public async Task WriteAsync_WhenWriteCoreReturnsFailure_ShouldRaiseErrorOccurred()
    {
        var sut = new TestPlcClient(
            readCore: (_, _, _) => Task.FromResult(OperateResult<byte[]>.Success(new byte[] { 0x01 })),
            writeCore: (_, _, _) => Task.FromResult(OperateResult.Failure(24, "write failed")));

        PlcErrorEventArgs? captured = null;
        sut.ErrorOccurred += (_, args) => captured = args;

        var result = await sut.WriteAsync("D0", new byte[] { 0x01 });

        result.IsSuccess.Should().BeFalse();
        captured.Should().NotBeNull();
        captured!.ErrorCode.Should().Be(24);
        captured.Message.Should().Be("write failed");
        captured.OperationType.Should().Be("Write");
    }

    [Fact]
    public async Task ReadAsync_WhenClientNotConnected_ShouldRaiseErrorOccurred()
    {
        var sut = new TestPlcClient(
            readCore: (_, _, _) => Task.FromResult(OperateResult<byte[]>.Success(new byte[] { 0x01 })),
            writeCore: (_, _, _) => Task.FromResult(OperateResult.Success()));
        sut.SetConnected(false);
        sut.ReconnectPolicy = new ReconnectPolicy { Enabled = false };

        PlcErrorEventArgs? captured = null;
        sut.ErrorOccurred += (_, args) => captured = args;

        var result = await sut.ReadAsync("D0", 1);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("PLC未连接");
        captured.Should().NotBeNull();
        captured!.Message.Should().Be("PLC未连接");
        captured.OperationType.Should().Be("Read");
        sut.ReadCoreCallCount.Should().Be(0);
    }

    [Fact]
    public async Task WriteAsync_WhenClientNotConnected_ShouldRaiseErrorOccurred()
    {
        var sut = new TestPlcClient(
            readCore: (_, _, _) => Task.FromResult(OperateResult<byte[]>.Success(new byte[] { 0x01 })),
            writeCore: (_, _, _) => Task.FromResult(OperateResult.Success()));
        sut.SetConnected(false);
        sut.ReconnectPolicy = new ReconnectPolicy { Enabled = false };

        PlcErrorEventArgs? captured = null;
        sut.ErrorOccurred += (_, args) => captured = args;

        var result = await sut.WriteAsync("D0", new byte[] { 0x01 });

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("PLC未连接");
        captured.Should().NotBeNull();
        captured!.Message.Should().Be("PLC未连接");
        captured.OperationType.Should().Be("Write");
        sut.WriteCoreCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Dispose_WhenTransportAllocated_ShouldReleaseSocketAndInvokeDisconnectCore()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var acceptTask = listener.AcceptTcpClientAsync();

        var clientSocket = new TcpClient();
        await clientSocket.ConnectAsync(IPAddress.Loopback, endpoint.Port);
        using var serverSocket = await acceptTask;

        var sut = new LifecycleTestPlcClient();
        sut.AttachConnectedTransport(clientSocket);

        DisconnectionEventArgs? disconnected = null;
        sut.Disconnected += (_, args) => disconnected = args;

        sut.Dispose();

        sut.DisconnectCoreCallCount.Should().Be(1);
        sut.HasTcpClient.Should().BeFalse();
        sut.HasNetworkStream.Should().BeFalse();
        sut.IsConnected.Should().BeFalse();
        disconnected.Should().NotBeNull();
        disconnected!.Reason.Should().Be(DisconnectionReason.UserInitiated);
    }

    [Fact]
    public async Task Dispose_WhenAlreadyDisconnected_ShouldNotInvokeDisconnectCoreAgain()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var acceptTask = listener.AcceptTcpClientAsync();

        var clientSocket = new TcpClient();
        await clientSocket.ConnectAsync(IPAddress.Loopback, endpoint.Port);
        using var serverSocket = await acceptTask;

        var sut = new LifecycleTestPlcClient();
        sut.AttachConnectedTransport(clientSocket);

        await sut.DisconnectAsync();
        sut.Dispose();

        sut.DisconnectCoreCallCount.Should().Be(1);
        sut.HasTcpClient.Should().BeFalse();
        sut.HasNetworkStream.Should().BeFalse();
    }


    private static TimeSpan InvokeGetRetryDelay(PlcBaseClient client, int retry)
    {
        var method = typeof(PlcBaseClient).GetMethod("GetRetryDelay", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        return (TimeSpan)method!.Invoke(client, new object[] { retry })!;
    }    private sealed class TestPlcClient : PlcBaseClient
    {
        private readonly Func<string, ushort, CancellationToken, Task<OperateResult<byte[]>>> _readCore;
        private readonly Func<string, byte[], CancellationToken, Task<OperateResult>> _writeCore;
        private bool _hasActiveConnection = true;
        private bool _isConnected = true;

        public TestPlcClient(
            Func<string, ushort, CancellationToken, Task<OperateResult<byte[]>>> readCore,
            Func<string, byte[], CancellationToken, Task<OperateResult>> writeCore)
        {
            _readCore = readCore;
            _writeCore = writeCore;
        }

        public int ReadCoreCallCount { get; private set; }
        public int WriteCoreCallCount { get; private set; }
        public int DisconnectCoreCallCount { get; private set; }

        public override int DefaultPort => 0;
        public override bool IsConnected => _isConnected;

        public void SetConnected(bool isConnected)
        {
            _isConnected = isConnected;
            _hasActiveConnection = isConnected;
        }

        protected override Task<bool> ConnectCoreAsync(CancellationToken ct)
        {
            _isConnected = true;
            _hasActiveConnection = true;
            return Task.FromResult(true);
        }

        protected override Task DisconnectCoreAsync()
        {
            _isConnected = false;
            _hasActiveConnection = false;
            DisconnectCoreCallCount++;
            return Task.CompletedTask;
        }

        protected override bool HasActiveConnectionResources()
        {
            return _hasActiveConnection;
        }

        protected override async Task<OperateResult<byte[]>> ReadCoreAsync(string address, ushort length, CancellationToken ct)
        {
            ReadCoreCallCount++;
            return await _readCore(address, length, ct);
        }

        protected override async Task<OperateResult> WriteCoreAsync(string address, byte[] value, CancellationToken ct)
        {
            WriteCoreCallCount++;
            return await _writeCore(address, value, ct);
        }
    }

    private sealed class LifecycleTestPlcClient : PlcBaseClient
    {
        public override int DefaultPort => 0;
        public int DisconnectCoreCallCount { get; private set; }
        public bool HasTcpClient => _tcpClient != null;
        public bool HasNetworkStream => _networkStream != null;

        public void AttachConnectedTransport(TcpClient clientSocket)
        {
            _tcpClient = clientSocket;
            _networkStream = clientSocket.GetStream();
            _lastCommunicationTime = DateTime.Now;
        }

        protected override Task<bool> ConnectCoreAsync(CancellationToken ct)
        {
            return Task.FromResult(true);
        }

        protected override Task DisconnectCoreAsync()
        {
            DisconnectCoreCallCount++;
            return Task.CompletedTask;
        }

        protected override Task<OperateResult<byte[]>> ReadCoreAsync(string address, ushort length, CancellationToken ct)
        {
            return Task.FromResult(OperateResult<byte[]>.Success(Array.Empty<byte>()));
        }

        protected override Task<OperateResult> WriteCoreAsync(string address, byte[] value, CancellationToken ct)
        {
            return Task.FromResult(OperateResult.Success());
        }
    }
}