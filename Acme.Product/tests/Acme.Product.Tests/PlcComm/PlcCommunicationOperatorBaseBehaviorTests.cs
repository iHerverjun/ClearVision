using System.Text;
using System.Reflection;
using Acme.PlcComm.Common;
using Acme.PlcComm.Core;
using Acme.PlcComm.Interfaces;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acme.Product.Tests.PlcComm;

public class PlcCommunicationOperatorBaseBehaviorTests
{
    [Fact]
    public void GetReadElementCount_ShouldAlwaysReturnSingleElement()
    {
        var sut = new TestPlcOperator();

        sut.GetReadElementCountPublic("BOOL").Should().Be(1);
        sut.GetReadElementCountPublic("WORD").Should().Be(1);
        sut.GetReadElementCountPublic("DWORD").Should().Be(1);
        sut.GetReadElementCountPublic("FLOAT").Should().Be(1);
    }

    [Fact]
    public void ConvertBytesToValue_BigEndianClient_ShouldDecodeBoolWordDwordAndFloat()
    {
        var sut = new TestPlcOperator();
        var client = new TestPlcClient(BigEndianTransform.Instance);

        sut.ConvertBytesToValuePublic(client, new byte[] { 0x01 }, "BOOL").Should().Be(true);
        sut.ConvertBytesToValuePublic(client, new byte[] { 0x12, 0x34 }, "WORD").Should().Be((ushort)0x1234);
        sut.ConvertBytesToValuePublic(client, new byte[] { 0x12, 0x34, 0x56, 0x78 }, "DWORD").Should().Be(0x12345678u);
        ((float)sut.ConvertBytesToValuePublic(client, BigEndianTransform.Instance.GetBytes(12.5f), "FLOAT")).Should().BeApproximately(12.5f, 0.001f);
    }

    [Fact]
    public void ConvertBytesToValue_LittleEndianClient_ShouldDecodeBoolWordDwordAndFloat()
    {
        var sut = new TestPlcOperator();
        var client = new TestPlcClient(LittleEndianTransform.Instance);

        sut.ConvertBytesToValuePublic(client, new byte[] { 0x01 }, "BOOL").Should().Be(true);
        sut.ConvertBytesToValuePublic(client, new byte[] { 0x34, 0x12 }, "WORD").Should().Be((ushort)0x1234);
        sut.ConvertBytesToValuePublic(client, new byte[] { 0x78, 0x56, 0x34, 0x12 }, "DWORD").Should().Be(0x12345678u);
        ((float)sut.ConvertBytesToValuePublic(client, LittleEndianTransform.Instance.GetBytes(12.5f), "FLOAT")).Should().BeApproximately(12.5f, 0.001f);
    }

    [Fact]
    public void ConvertValueToBytes_BigEndianClient_ShouldEncodeWordDwordAndFloatUsingProtocolTransform()
    {
        var sut = new TestPlcOperator();
        var client = new TestPlcClient(BigEndianTransform.Instance);

        sut.ConvertValueToBytesPublic(client, true, "BOOL").Should().Equal(0x01);
        sut.ConvertValueToBytesPublic(client, (ushort)0x1234, "WORD").Should().Equal(0x12, 0x34);
        sut.ConvertValueToBytesPublic(client, 0x12345678u, "DWORD").Should().Equal(0x12, 0x34, 0x56, 0x78);
        sut.ConvertValueToBytesPublic(client, 12.5f, "FLOAT").Should().Equal(BigEndianTransform.Instance.GetBytes(12.5f));
    }

    [Fact]
    public void ConvertValueToBytes_LittleEndianClient_ShouldEncodeWordDwordAndFloatUsingProtocolTransform()
    {
        var sut = new TestPlcOperator();
        var client = new TestPlcClient(LittleEndianTransform.Instance);

        sut.ConvertValueToBytesPublic(client, true, "BOOL").Should().Equal(0x01);
        sut.ConvertValueToBytesPublic(client, (ushort)0x1234, "WORD").Should().Equal(0x34, 0x12);
        sut.ConvertValueToBytesPublic(client, 0x12345678u, "DWORD").Should().Equal(0x78, 0x56, 0x34, 0x12);
        sut.ConvertValueToBytesPublic(client, 12.5f, "FLOAT").Should().Equal(LittleEndianTransform.Instance.GetBytes(12.5f));
    }

    [Fact]
    public void ResolveConnectionSettings_ShouldReadLegacyGlobalConfigAfterNormalization()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        var originalConfig = File.Exists(configPath) ? File.ReadAllText(configPath, Encoding.UTF8) : null;

        try
        {
            const string legacyConfigJson = """
            {
              "communication": {
                "protocol": "MC",
                "plcIpAddress": "192.168.3.5",
                "plcPort": 5002
              }
            }
            """;

            File.WriteAllText(configPath, legacyConfigJson, Encoding.UTF8);
            ResetCachedCommunicationConfig();

            var sut = new TestPlcOperator();

            var (ipAddress, port, protocol) = sut.ResolveConnectionSettingsPublic(null, null, "MC");

            ipAddress.Should().Be("192.168.3.5");
            port.Should().Be(5002);
            protocol.Should().Be("MC");
        }
        finally
        {
            if (originalConfig == null)
            {
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
            }
            else
            {
                File.WriteAllText(configPath, originalConfig, Encoding.UTF8);
            }

            ResetCachedCommunicationConfig();
        }
    }

    private static void ResetCachedCommunicationConfig()
    {
        var cachedConfigField = typeof(PlcCommunicationOperatorBase).GetField("_cachedCommunicationConfig", BindingFlags.Static | BindingFlags.NonPublic);
        var cachedAtField = typeof(PlcCommunicationOperatorBase).GetField("_cachedCommunicationConfigAtUtc", BindingFlags.Static | BindingFlags.NonPublic);

        cachedConfigField?.SetValue(null, new CommunicationConfig());
        cachedAtField?.SetValue(null, DateTime.MinValue);
    }

    private sealed class TestPlcOperator : PlcCommunicationOperatorBase
    {
        public TestPlcOperator()
            : base(NullLogger.Instance)
        {
        }

        public override OperatorType OperatorType => OperatorType.SiemensS7Communication;

        public ushort GetReadElementCountPublic(string dataType)
        {
            return GetReadElementCount(dataType);
        }

        public object ConvertBytesToValuePublic(IPlcClient client, byte[] data, string dataType)
        {
            return ConvertBytesToValue(client, data, dataType);
        }

        public byte[] ConvertValueToBytesPublic(IPlcClient client, object value, string dataType)
        {
            return ConvertValueToBytes(client, value, dataType);
        }

        public (string ipAddress, int port, string protocol) ResolveConnectionSettingsPublic(
            string? ipAddress,
            int? port,
            string fallbackProtocol)
        {
            return ResolveConnectionSettings(ipAddress, port, fallbackProtocol);
        }

        protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(Operator @operator, Dictionary<string, object>? inputs, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public override ValidationResult ValidateParameters(Operator @operator)
        {
            return ValidationResult.Valid();
        }
    }

    private sealed class TestPlcClient : IPlcClient
    {
        public TestPlcClient(IByteTransform byteTransform)
        {
            ByteTransform = byteTransform;
        }

        public string IpAddress => "127.0.0.1";
        public int Port => 0;
        public bool IsConnected => true;
        public int ConnectTimeout { get; set; }
        public int ReadTimeout { get; set; }
        public int WriteTimeout { get; set; }
        public ReconnectPolicy ReconnectPolicy { get; set; } = new();
        public IByteTransform ByteTransform { get; }
        public event EventHandler<ConnectionEventArgs>? Connected;
        public event EventHandler<DisconnectionEventArgs>? Disconnected;
        public event EventHandler<PlcErrorEventArgs>? ErrorOccurred;

        public Task<bool> ConnectAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task<OperateResult<byte[]>> ReadAsync(string address, ushort length, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OperateResult> WriteAsync(string address, byte[] value, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OperateResult<T>> ReadAsync<T>(string address, CancellationToken ct = default) where T : struct => throw new NotSupportedException();
        public Task<OperateResult> WriteAsync<T>(string address, T value, CancellationToken ct = default) where T : struct => throw new NotSupportedException();
        public Task<OperateResult<Dictionary<string, byte[]>>> ReadBatchAsync(string[] addresses, ushort[] lengths, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OperateResult<string>> ReadStringAsync(string address, ushort length, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OperateResult> WriteStringAsync(string address, string value, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> PingAsync(CancellationToken ct = default) => Task.FromResult(true);
        public void Dispose() { }
    }
}
