using Acme.PlcComm.Core;
using Acme.PlcComm.Siemens;
using FluentAssertions;

namespace Acme.Product.Tests.PlcComm;

public class SiemensS7ClientBehaviorTests
{
    [Fact]
    public async Task ReadCoreAsync_WordAddress_ShouldRequestTwoBytesForSingleElement()
    {
        var sut = new TestableSiemensS7Client();
        sut.EnqueueReadResponse(0x12, 0x34);

        var result = await sut.ReadCorePublicAsync("MW0", 1);

        result.IsSuccess.Should().BeTrue();
        result.Content.Should().Equal(0x12, 0x34);
        sut.LastReadByteCount.Should().Be(2);
        sut.LastReadAddress.Should().NotBeNull();
        sut.LastReadAddress!.AreaType.Should().Be("M");
        sut.LastReadAddress.StartAddress.Should().Be(0);
    }

    [Fact]
    public async Task ReadCoreAsync_FloatAddress_ShouldRequestFourBytesPerElement()
    {
        var sut = new TestableSiemensS7Client();
        sut.EnqueueReadResponse(0x41, 0x48, 0x00, 0x00);

        var result = await sut.ReadCorePublicAsync("DB1.DBR0", 1);

        result.IsSuccess.Should().BeTrue();
        sut.LastReadByteCount.Should().Be(4);
        sut.LastReadAddress.Should().NotBeNull();
        sut.LastReadAddress!.AreaType.Should().Be("DB");
        sut.LastReadAddress.DbNumber.Should().Be(1);
    }

    [Fact]
    public async Task ReadCoreAsync_BitAddress_ShouldExtractSingleBitValue()
    {
        var sut = new TestableSiemensS7Client();
        sut.EnqueueReadResponse(0b_0000_1000);

        var result = await sut.ReadCorePublicAsync("DB1.DBX10.3", 1);

        result.IsSuccess.Should().BeTrue();
        result.Content.Should().Equal(0x01);
        sut.LastReadByteCount.Should().Be(1);
    }

    [Fact]
    public async Task ReadCoreAsync_BitAddressWithMultiLength_ShouldFailFast()
    {
        var sut = new TestableSiemensS7Client();

        var result = await sut.ReadCorePublicAsync("M10.3", 2);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("仅支持单点读取");
    }

    [Fact]
    public async Task WriteCoreAsync_BitAddress_ShouldPreserveOtherBitsWhenSettingTargetBit()
    {
        var sut = new TestableSiemensS7Client();
        sut.EnqueueReadResponse(0b_1010_0000);

        var result = await sut.WriteCorePublicAsync("M10.3", new byte[] { 0x01 });

        result.IsSuccess.Should().BeTrue();
        sut.LastWriteAddress.Should().NotBeNull();
        sut.LastWriteAddress!.AreaType.Should().Be("M");
        sut.LastWriteBytes.Should().Equal(0b_1010_1000);
    }

    [Fact]
    public async Task WriteCoreAsync_BitAddress_ShouldPreserveOtherBitsWhenClearingTargetBit()
    {
        var sut = new TestableSiemensS7Client();
        sut.EnqueueReadResponse(0b_1010_1000);

        var result = await sut.WriteCorePublicAsync("M10.3", new byte[] { 0x00 });

        result.IsSuccess.Should().BeTrue();
        sut.LastWriteBytes.Should().Equal(0b_1010_0000);
    }

    [Fact]
    public async Task PingCoreAsync_ShouldUseMerkerHeartbeatAddress()
    {
        var sut = new TestableSiemensS7Client();
        sut.EnqueueReadResponse(0x12, 0x34);

        var result = await sut.PingCorePublicAsync();

        result.Should().BeTrue();
        sut.LastReadAddress.Should().NotBeNull();
        sut.LastReadAddress!.AreaType.Should().Be("M");
        sut.LastReadAddress.StartAddress.Should().Be(0);
        sut.LastReadByteCount.Should().Be(2);
    }

    private sealed class TestableSiemensS7Client : SiemensS7Client
    {
        private readonly Queue<byte[]?> _readResponses = new();

        public TestableSiemensS7Client()
            : base("127.0.0.1")
        {
        }

        public override bool IsConnected => true;
        public PlcAddress? LastReadAddress { get; private set; }
        public int LastReadByteCount { get; private set; }
        public PlcAddress? LastWriteAddress { get; private set; }
        public byte[]? LastWriteBytes { get; private set; }

        public void EnqueueReadResponse(params byte[] data)
        {
            _readResponses.Enqueue(data);
        }

        public Task<OperateResult<byte[]>> ReadCorePublicAsync(string address, ushort length)
        {
            return ReadCoreAsync(address, length, CancellationToken.None);
        }

        public Task<OperateResult> WriteCorePublicAsync(string address, byte[] value)
        {
            return WriteCoreAsync(address, value, CancellationToken.None);
        }

        public Task<bool> PingCorePublicAsync()
        {
            return PingCoreAsync(CancellationToken.None);
        }

        protected override bool IsProtocolConnected()
        {
            return true;
        }

        protected override Task<byte[]?> ReadProtocolBytesAsync(PlcAddress plcAddress, int byteCount, CancellationToken ct)
        {
            LastReadAddress = plcAddress;
            LastReadByteCount = byteCount;
            var payload = _readResponses.Count > 0 ? _readResponses.Dequeue() : Array.Empty<byte>();
            return Task.FromResult<byte[]?>(payload);
        }

        protected override Task WriteProtocolBytesAsync(PlcAddress plcAddress, byte[] value, CancellationToken ct)
        {
            LastWriteAddress = plcAddress;
            LastWriteBytes = value.ToArray();
            return Task.CompletedTask;
        }
    }
}