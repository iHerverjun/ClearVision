using System.Buffers.Binary;
using Acme.PlcComm.Mitsubishi;
using Acme.PlcComm.Omron;
using FluentAssertions;

namespace Acme.Product.Tests.PlcComm;

public class FrameCodecTests
{
    [Fact]
    public void McFrameBuilder_BuildReadRequest_ShouldEncodeCoreFields()
    {
        var builder = new McFrameBuilder();

        var frame = builder.BuildReadRequest(0xA8, 100, 2, isBitAccess: false);

        frame.Should().NotBeNull();
        frame.Length.Should().Be(27);
        BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(0, 2)).Should().Be(0x0050);
        BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(7, 2)).Should().Be(12);
        BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(11, 2)).Should().Be(0x0401);
        BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(13, 2)).Should().Be(0x0000);
        frame[18].Should().Be(0xA8);
        BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(19, 2)).Should().Be(2);
    }

    [Fact]
    public void McFrameBuilder_ParseReadResponse_ShouldDecodePayload()
    {
        var builder = new McFrameBuilder();
        var response = BuildMcReadResponse(0x12, 0x34);

        var (success, data, error) = builder.ParseReadResponse(response);

        success.Should().BeTrue(error);
        data.Should().NotBeNull();
        data.Should().Equal(0x12, 0x34);
    }

    [Fact]
    public void FinsFrameBuilder_BuildMemoryWriteRequest_WordAccess_ShouldUseWordLength()
    {
        var builder = new FinsFrameBuilder();
        var data = new byte[] { 0x12, 0x34, 0x56, 0x78 };

        var frame = builder.BuildMemoryWriteRequest(0x82, 100, 0, data, isBitAccess: false, clientNode: 0x22, serverNode: 0x11);

        var finsPayload = frame.AsSpan(16);
        var encodedLength = BinaryPrimitives.ReadUInt16BigEndian(finsPayload.Slice(16, 2));

        encodedLength.Should().Be(2);
    }

    [Fact]
    public void FinsFrameBuilder_BuildMemoryWriteRequest_BitAccess_ShouldUseBitLength()
    {
        var builder = new FinsFrameBuilder();
        var data = new byte[] { 0x01, 0x00, 0x01 };

        var frame = builder.BuildMemoryWriteRequest(0x02, 200, 3, data, isBitAccess: true, clientNode: 0x22, serverNode: 0x11);

        var finsPayload = frame.AsSpan(16);
        var encodedLength = BinaryPrimitives.ReadUInt16BigEndian(finsPayload.Slice(16, 2));

        encodedLength.Should().Be(3);
    }

    [Fact]
    public void FinsFrameBuilder_BuildMemoryWriteRequest_WordAccessWithOddBytes_ShouldThrow()
    {
        var builder = new FinsFrameBuilder();
        var data = new byte[] { 0x01, 0x02, 0x03 };

        var act = () => builder.BuildMemoryWriteRequest(0x82, 100, 0, data, isBitAccess: false, clientNode: 0x22, serverNode: 0x11);

        act.Should().Throw<ArgumentException>().WithMessage("*2 的倍数*");
    }

    [Fact]
    public void FinsFrameBuilder_ParseNodeAddressResponse_ShouldReturnNodes()
    {
        var builder = new FinsFrameBuilder();
        var response = BuildFinsNodeAddressResponse(clientNode: 0x22, serverNode: 0x11);

        var (success, clientNode, serverNode, error) = builder.ParseNodeAddressResponse(response);

        success.Should().BeTrue(error);
        clientNode.Should().Be(0x22);
        serverNode.Should().Be(0x11);
    }

    [Fact]
    public void FinsFrameBuilder_ParseResponse_ShouldDecodeDataPayload()
    {
        var builder = new FinsFrameBuilder();
        var response = BuildFinsReadResponse(new byte[] { 0x12, 0x34 });

        var (success, data, error) = builder.ParseResponse(response);

        success.Should().BeTrue(error);
        data.Should().NotBeNull();
        data.Should().Equal(0x12, 0x34);
    }

    private static byte[] BuildMcReadResponse(params byte[] data)
    {
        var response = new byte[11 + data.Length];
        response[0] = 0xD0;
        response[1] = 0x00;
        response[2] = 0x00;
        response[3] = 0xFF;
        response[4] = 0xFF;
        response[5] = 0x03;
        response[6] = 0x00;
        BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(7, 2), (ushort)(2 + data.Length));
        response[9] = 0x00;
        response[10] = 0x00;

        if (data.Length > 0)
            data.CopyTo(response, 11);

        return response;
    }

    private static byte[] BuildFinsNodeAddressResponse(byte clientNode, byte serverNode)
    {
        var response = new byte[24];
        response[0] = 0x46;
        response[1] = 0x49;
        response[2] = 0x4E;
        response[3] = 0x53;
        BinaryPrimitives.WriteUInt32BigEndian(response.AsSpan(4, 4), 16u);
        BinaryPrimitives.WriteUInt32BigEndian(response.AsSpan(8, 4), 1u);
        BinaryPrimitives.WriteUInt32BigEndian(response.AsSpan(12, 4), 0u);
        BinaryPrimitives.WriteUInt32BigEndian(response.AsSpan(16, 4), serverNode);
        BinaryPrimitives.WriteUInt32BigEndian(response.AsSpan(20, 4), clientNode);
        return response;
    }

    private static byte[] BuildFinsReadResponse(byte[] data)
    {
        var finsFrame = new byte[14 + data.Length];
        finsFrame[0] = 0xC0;
        finsFrame[1] = 0x00;
        finsFrame[2] = 0x02;
        finsFrame[3] = 0x00;
        finsFrame[4] = 0x22;
        finsFrame[5] = 0x00;
        finsFrame[6] = 0x00;
        finsFrame[7] = 0x11;
        finsFrame[8] = 0x00;
        finsFrame[9] = 0x01;
        finsFrame[10] = 0x01;
        finsFrame[11] = 0x01;
        finsFrame[12] = 0x00;
        finsFrame[13] = 0x00;
        data.CopyTo(finsFrame, 14);

        return WrapFinsTcpFrame(finsFrame);
    }

    private static byte[] WrapFinsTcpFrame(byte[] finsFrame)
    {
        var response = new byte[16 + finsFrame.Length];
        response[0] = 0x46;
        response[1] = 0x49;
        response[2] = 0x4E;
        response[3] = 0x53;
        BinaryPrimitives.WriteUInt32BigEndian(response.AsSpan(4, 4), (uint)finsFrame.Length);
        BinaryPrimitives.WriteUInt32BigEndian(response.AsSpan(8, 4), 2u);
        BinaryPrimitives.WriteUInt32BigEndian(response.AsSpan(12, 4), 0u);
        finsFrame.CopyTo(response, 16);
        return response;
    }
}
