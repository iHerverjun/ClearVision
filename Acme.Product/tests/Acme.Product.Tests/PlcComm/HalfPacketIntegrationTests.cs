using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Acme.PlcComm.Mitsubishi;
using Acme.PlcComm.Omron;
using FluentAssertions;

namespace Acme.Product.Tests.PlcComm;

public class HalfPacketIntegrationTests
{
    [Fact]
    public async Task MitsubishiMcClient_WriteAsync_WithFragmentedResponse_ShouldSucceed()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var serverTask = ServeMcWriteAsync(listener, cts.Token);

        using var client = new MitsubishiMcClient(IPAddress.Loopback.ToString())
        {
            Port = port,
            ConnectTimeout = 2000,
            ReadTimeout = 2000,
            WriteTimeout = 2000
        };

        var connected = await client.ConnectAsync(cts.Token);
        connected.Should().BeTrue();

        var writeResult = await client.WriteAsync("D100", new byte[] { 0x12, 0x34 }, cts.Token);
        writeResult.IsSuccess.Should().BeTrue(writeResult.Message);

        await serverTask;
    }

    [Fact]
    public async Task OmronFinsClient_WriteAsync_WithFragmentedHandshakeAndResponse_ShouldSucceed()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var serverTask = ServeFinsWriteAsync(listener, cts.Token);

        using var client = new OmronFinsClient(IPAddress.Loopback.ToString())
        {
            Port = port,
            ConnectTimeout = 2000,
            ReadTimeout = 2000,
            WriteTimeout = 2000
        };

        var connected = await client.ConnectAsync(cts.Token);
        connected.Should().BeTrue();

        var writeResult = await client.WriteAsync("DM100", new byte[] { 0x00, 0x01 }, cts.Token);
        writeResult.IsSuccess.Should().BeTrue(writeResult.Message);

        await serverTask;
    }

    [Fact]
    public async Task OmronFinsClient_ReadAsync_WithFragmentedPayload_ShouldSucceed()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var serverTask = ServeFinsReadAsync(listener, cts.Token);

        using var client = new OmronFinsClient(IPAddress.Loopback.ToString())
        {
            Port = port,
            ConnectTimeout = 2000,
            ReadTimeout = 2000,
            WriteTimeout = 2000
        };

        var connected = await client.ConnectAsync(cts.Token);
        connected.Should().BeTrue();

        var readResult = await client.ReadAsync("DM100", 1, cts.Token);
        readResult.IsSuccess.Should().BeTrue(readResult.Message);
        readResult.Content.Should().NotBeNull();
        readResult.Content.Should().Equal(0x12, 0x34);

        await serverTask;
    }

    private static async Task ServeMcWriteAsync(TcpListener listener, CancellationToken ct)
    {
        using var server = await listener.AcceptTcpClientAsync(ct);
        using var stream = server.GetStream();

        _ = await ReadMcFrameAsync(stream, ct);
        var response = BuildMcWriteResponse();
        await WriteInChunksAsync(stream, response, ct, 1, 2, 3);
    }

    private static async Task ServeFinsWriteAsync(TcpListener listener, CancellationToken ct)
    {
        using var server = await listener.AcceptTcpClientAsync(ct);
        using var stream = server.GetStream();

        var handshakeRequest = new byte[20];
        await ReadExactAsync(stream, handshakeRequest, ct);
        var handshakeResponse = BuildFinsNodeAddressResponse(clientNode: 0x22, serverNode: 0x11);
        await WriteInChunksAsync(stream, handshakeResponse, ct, 3, 5, 4);

        _ = await ReadFinsWrappedFrameAsync(stream, ct); // write request
        var writeResponse = BuildFinsWriteResponse();
        await WriteInChunksAsync(stream, writeResponse, ct, 2, 6, 4, 3);
    }

    private static async Task ServeFinsReadAsync(TcpListener listener, CancellationToken ct)
    {
        using var server = await listener.AcceptTcpClientAsync(ct);
        using var stream = server.GetStream();

        var handshakeRequest = new byte[20];
        await ReadExactAsync(stream, handshakeRequest, ct);
        var handshakeResponse = BuildFinsNodeAddressResponse(clientNode: 0x22, serverNode: 0x11);
        await WriteInChunksAsync(stream, handshakeResponse, ct, 4, 4, 4);

        _ = await ReadFinsWrappedFrameAsync(stream, ct); // read request
        var readResponse = BuildFinsReadResponse(new byte[] { 0x12, 0x34 });
        await WriteInChunksAsync(stream, readResponse, ct, 5, 5, 5, 5);
    }

    private static async Task<byte[]> ReadMcFrameAsync(NetworkStream stream, CancellationToken ct)
    {
        var header = new byte[9];
        await ReadExactAsync(stream, header, ct);
        var dataLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(7, 2));

        var frame = new byte[9 + dataLength];
        Array.Copy(header, frame, header.Length);

        if (dataLength > 0)
            await ReadExactAsync(stream, frame.AsMemory(9, dataLength), ct);

        return frame;
    }

    private static async Task<byte[]> ReadFinsWrappedFrameAsync(NetworkStream stream, CancellationToken ct)
    {
        var header = new byte[16];
        await ReadExactAsync(stream, header, ct);
        var length = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(4, 4));
        length.Should().BeGreaterOrEqualTo(0);

        var frame = new byte[16 + length];
        Array.Copy(header, frame, header.Length);

        if (length > 0)
            await ReadExactAsync(stream, frame.AsMemory(16, length), ct);

        return frame;
    }

    private static async Task WriteInChunksAsync(NetworkStream stream, byte[] payload, CancellationToken ct, params int[] chunkSizes)
    {
        var offset = 0;

        foreach (var size in chunkSizes)
        {
            if (offset >= payload.Length)
                break;

            var toWrite = Math.Min(size, payload.Length - offset);
            await stream.WriteAsync(payload.AsMemory(offset, toWrite), ct);
            await stream.FlushAsync(ct);
            offset += toWrite;
            await Task.Delay(10, ct);
        }

        if (offset < payload.Length)
        {
            await stream.WriteAsync(payload.AsMemory(offset), ct);
            await stream.FlushAsync(ct);
        }
    }

    private static async Task ReadExactAsync(NetworkStream stream, Memory<byte> buffer, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.Slice(totalRead), ct);
            if (read == 0)
                throw new IOException("Connection closed before expected bytes were received.");

            totalRead += read;
        }
    }

    private static byte[] BuildMcWriteResponse()
    {
        var response = new byte[11];
        response[0] = 0xD0;
        response[1] = 0x00;
        response[2] = 0x00;
        response[3] = 0xFF;
        response[4] = 0xFF;
        response[5] = 0x03;
        response[6] = 0x00;
        BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(7, 2), 2);
        response[9] = 0x00;
        response[10] = 0x00;
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

    private static byte[] BuildFinsWriteResponse()
    {
        var finsFrame = new byte[14];
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
        finsFrame[11] = 0x02;
        finsFrame[12] = 0x00;
        finsFrame[13] = 0x00;
        return WrapFinsTcpFrame(finsFrame);
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
