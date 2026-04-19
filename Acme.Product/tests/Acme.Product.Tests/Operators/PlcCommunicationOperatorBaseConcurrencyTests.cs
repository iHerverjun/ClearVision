using System.Collections;
using System.Diagnostics;
using System.Reflection;
using Acme.PlcComm.Common;
using Acme.PlcComm.Core;
using Acme.PlcComm.Interfaces;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acme.Product.Tests.Operators;

public class PlcCommunicationOperatorBaseConcurrencyTests
{
    [Fact]
    public async Task GetOrCreateConnectionAsync_DifferentKeys_ShouldNotBeBlockedBySlowConnectUnderGlobalPoolLock()
    {
        ResetStaticConnectionState();
        var sut = new TestPlcOperator();

        var slowConnectStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slowClient = new DelayedConnectPlcClient(
            connectDelay: TimeSpan.FromMilliseconds(700),
            onConnectStart: () => slowConnectStarted.TrySetResult());
        var fastClient = new DelayedConnectPlcClient(connectDelay: TimeSpan.FromMilliseconds(80));

        var slowTask = sut.GetOrCreateConnectionPublicAsync("S7:192.168.0.1:102", () => slowClient);
        await slowConnectStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var sw = Stopwatch.StartNew();
        var fastResult = await sut.GetOrCreateConnectionPublicAsync("S7:192.168.0.2:102", () => fastClient)
            .WaitAsync(TimeSpan.FromSeconds(2));
        sw.Stop();

        fastResult.isNewConnection.Should().BeTrue();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(350));

        _ = await slowTask.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task GetOrCreateConnectionAsync_SameKeyConcurrentCalls_ShouldCreateOnceAndReuse()
    {
        ResetStaticConnectionState();
        var sut = new TestPlcOperator();

        var factoryCalls = 0;
        var connectCalls = 0;
        Func<IPlcClient> factory = () =>
        {
            Interlocked.Increment(ref factoryCalls);
            return new DelayedConnectPlcClient(
                connectDelay: TimeSpan.FromMilliseconds(150),
                onConnectStart: () => Interlocked.Increment(ref connectCalls));
        };

        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(async () =>
            {
                await startGate.Task;
                return await sut.GetOrCreateConnectionPublicAsync("MC:192.168.1.10:5002", factory);
            }))
            .ToArray();

        startGate.TrySetResult();
        var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5));

        factoryCalls.Should().Be(1);
        connectCalls.Should().Be(1);
        results.Count(static r => r.isNewConnection).Should().Be(1);

        var firstClient = results[0].client;
        results.Should().OnlyContain(r => ReferenceEquals(r.client, firstClient));
    }

    [Fact]
    public async Task GetOrCreateConnectionAsync_ManyDistinctKeys_ShouldNotRetainKeyLocks()
    {
        ResetStaticConnectionState();
        var sut = new TestPlcOperator();

        try
        {
            for (var i = 0; i < 64; i++)
            {
                var key = $"S7:192.168.10.{i}:102";
                _ = await sut.GetOrCreateConnectionPublicAsync(
                    key,
                    () => new DelayedConnectPlcClient(TimeSpan.FromMilliseconds(1)));
            }

            GetConnectionKeyLockCount().Should().Be(0);
        }
        finally
        {
            ResetStaticConnectionState();
        }
    }

    private static void ResetStaticConnectionState()
    {
        var poolField = typeof(PlcCommunicationOperatorBase).GetField("_connectionPool", BindingFlags.Static | BindingFlags.NonPublic);
        var stateField = typeof(PlcCommunicationOperatorBase).GetField("_lastKnownState", BindingFlags.Static | BindingFlags.NonPublic);
        var keyLocksField = typeof(PlcCommunicationOperatorBase).GetField("_connectionKeyLocks", BindingFlags.Static | BindingFlags.NonPublic);

        if (poolField?.GetValue(null) is IDictionary pool)
        {
            foreach (DictionaryEntry entry in pool)
            {
                if (entry.Value is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            pool.Clear();
        }

        if (stateField?.GetValue(null) is IDictionary state)
        {
            state.Clear();
        }

        if (keyLocksField?.GetValue(null) is IDictionary keyLocks)
        {
            foreach (DictionaryEntry entry in keyLocks)
            {
                if (entry.Value is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            keyLocks.Clear();
        }
    }

    private static int GetConnectionKeyLockCount()
    {
        var keyLocksField = typeof(PlcCommunicationOperatorBase).GetField("_connectionKeyLocks", BindingFlags.Static | BindingFlags.NonPublic);
        keyLocksField.Should().NotBeNull();

        var keyLocks = keyLocksField!.GetValue(null) as IDictionary;
        keyLocks.Should().NotBeNull();
        return keyLocks!.Count;
    }

    private sealed class TestPlcOperator : PlcCommunicationOperatorBase
    {
        public TestPlcOperator() : base(NullLogger.Instance)
        {
        }

        public override OperatorType OperatorType => OperatorType.SiemensS7Communication;

        public Task<(IPlcClient client, bool isNewConnection)> GetOrCreateConnectionPublicAsync(
            string connectionKey,
            Func<IPlcClient> factory)
        {
            return GetOrCreateConnectionAsync(connectionKey, factory);
        }

        protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
            Operator @operator,
            Dictionary<string, object>? inputs,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public override ValidationResult ValidateParameters(Operator @operator)
        {
            return ValidationResult.Valid();
        }
    }

    private sealed class DelayedConnectPlcClient : IPlcClient
    {
        private readonly TimeSpan _connectDelay;
        private readonly Action? _onConnectStart;

        public DelayedConnectPlcClient(TimeSpan connectDelay, Action? onConnectStart = null)
        {
            _connectDelay = connectDelay;
            _onConnectStart = onConnectStart;
        }

        public string IpAddress => "127.0.0.1";
        public int Port => 0;
        public bool IsConnected { get; private set; }
        public int ConnectTimeout { get; set; }
        public int ReadTimeout { get; set; }
        public int WriteTimeout { get; set; }
        public ReconnectPolicy ReconnectPolicy { get; set; } = new();
        public IByteTransform ByteTransform { get; } = BigEndianTransform.Instance;

        public event EventHandler<ConnectionEventArgs>? Connected;
        public event EventHandler<DisconnectionEventArgs>? Disconnected;
        public event EventHandler<PlcErrorEventArgs>? ErrorOccurred;

        public async Task<bool> ConnectAsync(CancellationToken ct = default)
        {
            _onConnectStart?.Invoke();
            await Task.Delay(_connectDelay, ct);
            IsConnected = true;
            return true;
        }

        public Task DisconnectAsync()
        {
            IsConnected = false;
            return Task.CompletedTask;
        }

        public Task<OperateResult<byte[]>> ReadAsync(string address, ushort length, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OperateResult> WriteAsync(string address, byte[] value, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OperateResult<T>> ReadAsync<T>(string address, CancellationToken ct = default) where T : struct => throw new NotSupportedException();
        public Task<OperateResult> WriteAsync<T>(string address, T value, CancellationToken ct = default) where T : struct => throw new NotSupportedException();
        public Task<OperateResult<Dictionary<string, byte[]>>> ReadBatchAsync(string[] addresses, ushort[] lengths, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OperateResult<string>> ReadStringAsync(string address, ushort length, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OperateResult> WriteStringAsync(string address, string value, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> PingAsync(CancellationToken ct = default) => Task.FromResult(IsConnected);

        public void Dispose()
        {
        }
    }
}
