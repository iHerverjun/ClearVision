using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using Acme.Product.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acme.Product.Tests.Services;

public sealed class ConnectionPoolManagerTests
{
    [Fact]
    public async Task GetOrCreateTcpConnectionAsync_ReusesSingleUnderlyingConnection()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var manager = new ConnectionPoolManager(NullLogger<ConnectionPoolManager>.Instance);
        using var lease1 = await manager.GetOrCreateTcpConnectionAsync(IPAddress.Loopback.ToString(), port);
        using var serverClient = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(2));

        using var lease2 = await manager.GetOrCreateTcpConnectionAsync(IPAddress.Loopback.ToString(), port);

        await Task.Delay(100);
        lease2.Stream.Should().BeSameAs(lease1.Stream);
        listener.Pending().Should().BeFalse("reusing the lease should not open a second TCP client");
        serverClient.Connected.Should().BeTrue();
    }

    [Fact]
    public async Task ReleaseConnection_DoesNotDisposeBorrowedTcpStreamUntilLeaseDisposed()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var manager = new ConnectionPoolManager(NullLogger<ConnectionPoolManager>.Instance);
        using var lease = await manager.GetOrCreateTcpConnectionAsync(IPAddress.Loopback.ToString(), port);
        using var serverClient = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(2));
        using var serverStream = serverClient.GetStream();

        manager.ReleaseConnection($"tcp:{IPAddress.Loopback}:{port}");

        await lease.Stream.WriteAsync(new byte[] { 0x42 });
        var buffer = new byte[1];
        var read = await serverStream.ReadAsync(buffer).AsTask().WaitAsync(TimeSpan.FromSeconds(2));

        read.Should().Be(1);
        buffer[0].Should().Be(0x42);
    }

    [Fact]
    public async Task Dispose_ClosesBorrowedTcpStreamEvenBeforeLeaseIsReleased()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var manager = new ConnectionPoolManager(NullLogger<ConnectionPoolManager>.Instance);
        var lease = await manager.GetOrCreateTcpConnectionAsync(IPAddress.Loopback.ToString(), port);
        using var serverClient = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(2));
        using var serverStream = serverClient.GetStream();

        manager.Dispose();

        var buffer = new byte[1];
        var read = await serverStream.ReadAsync(buffer).AsTask().WaitAsync(TimeSpan.FromSeconds(2));

        read.Should().Be(0, "disposing the pool should close checked-out TCP clients immediately");

        lease.Dispose();
    }

    [Fact]
    public async Task GetOrCreateTcpConnectionAsync_DoesNotBlockOtherKeysWhileSlowConnectIsPending()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        const string slowAddress = "slow.test";
        using var manager = new TestConnectionPoolManager(
            NullLogger<ConnectionPoolManager>.Instance,
            slowAddress,
            TimeSpan.FromMilliseconds(500));

        var slowTask = manager.GetOrCreateTcpConnectionAsync(slowAddress, 65000);
        await manager.SlowConnectStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var stopwatch = Stopwatch.StartNew();
        using var fastLease = await manager
            .GetOrCreateTcpConnectionAsync(IPAddress.Loopback.ToString(), port)
            .WaitAsync(TimeSpan.FromSeconds(2));
        using var serverClient = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(2));
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2),
            "a slow connect on another key should not hold the pool-wide gate");
        fastLease.Stream.Should().NotBeNull();
        serverClient.Connected.Should().BeTrue();

        try
        {
            await slowTask;
        }
        catch
        {
        }
    }

    private sealed class TestConnectionPoolManager : ConnectionPoolManager
    {
        private readonly string _slowAddress;
        private readonly TimeSpan _slowDelay;

        public TestConnectionPoolManager(
            ILogger<ConnectionPoolManager> logger,
            string slowAddress,
            TimeSpan slowDelay)
            : base(logger)
        {
            _slowAddress = slowAddress;
            _slowDelay = slowDelay;
        }

        public TaskCompletionSource<bool> SlowConnectStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<TcpClient> CreateConnectedTcpClientAsync(
            string ipAddress,
            int port,
            CancellationToken cancellationToken)
        {
            if (string.Equals(ipAddress, _slowAddress, StringComparison.Ordinal))
            {
                SlowConnectStarted.TrySetResult(true);
                await Task.Delay(_slowDelay, cancellationToken);
                throw new SocketException((int)SocketError.TimedOut);
            }

            return await base.CreateConnectedTcpClientAsync(ipAddress, port, cancellationToken);
        }
    }
}
