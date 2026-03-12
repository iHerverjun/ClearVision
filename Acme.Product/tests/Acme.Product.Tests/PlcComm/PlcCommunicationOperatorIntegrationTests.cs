using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Acme.PlcComm.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acme.Product.Tests.PlcComm;

[Collection("PLC Operator Integration")]
public class PlcCommunicationOperatorIntegrationTests : IDisposable
{
    public PlcCommunicationOperatorIntegrationTests()
    {
        ResetPlcOperatorState();
    }

    public void Dispose()
    {
        ResetPlcOperatorState();
    }

    [Fact]
    public async Task MitsubishiMcCommunicationOperator_ReadAsync_ShouldReturnConvertedWordValue()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var serverTask = ServeMcReadAsync(listener, cts.Token, 0x12, 0x34);
        var sut = new MitsubishiMcCommunicationOperator(NullLogger<MitsubishiMcCommunicationOperator>.Instance);
        var @operator = CreateOperator(
            "MC Read",
            OperatorType.MitsubishiMcCommunication,
            ("IpAddress", IPAddress.Loopback.ToString(), "string"),
            ("Port", port, "int"),
            ("Address", "D100", "string"),
            ("Length", 1, "int"),
            ("DataType", "Word", "string"),
            ("Operation", "Read", "string"));

        var result = await sut.ExecuteAsync(@operator, cancellationToken: cts.Token);

        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.OutputData.Should().NotBeNull();
        result.OutputData!["Status"].Should().Be(true);
        result.OutputData["DataType"].Should().Be("Word");
        result.OutputData["Value"].Should().Be((ushort)0x3412);

        await serverTask;
    }

    [Fact]
    public async Task MitsubishiMcCommunicationOperator_WriteAsync_ShouldUseUpstreamInputAndLittleEndianPayload()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var serverTask = ServeMcWriteAndCaptureAsync(listener, cts.Token);
        var sut = new MitsubishiMcCommunicationOperator(NullLogger<MitsubishiMcCommunicationOperator>.Instance);
        var @operator = CreateOperator(
            "MC Write",
            OperatorType.MitsubishiMcCommunication,
            ("IpAddress", IPAddress.Loopback.ToString(), "string"),
            ("Port", port, "int"),
            ("Address", "D100", "string"),
            ("DataType", "Word", "string"),
            ("Operation", "Write", "string"),
            ("WriteValue", string.Empty, "string"));

        var result = await sut.ExecuteAsync(
            @operator,
            new Dictionary<string, object> { ["Value"] = 4660 },
            cts.Token);

        var requestFrame = await serverTask;

        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.OutputData.Should().NotBeNull();
        result.OutputData!["Status"].Should().Be(true);
        result.OutputData["Value"].Should().Be("4660");
        requestFrame[21].Should().Be(0x34);
        requestFrame[22].Should().Be(0x12);
    }

    [Fact]
    public async Task OmronFinsCommunicationOperator_ReadAsync_ShouldReturnConvertedWordValue()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var serverTask = ServeFinsReadAsync(listener, cts.Token, 0x12, 0x34);
        var sut = new OmronFinsCommunicationOperator(NullLogger<OmronFinsCommunicationOperator>.Instance);
        var @operator = CreateOperator(
            "FINS Read",
            OperatorType.OmronFinsCommunication,
            ("IpAddress", IPAddress.Loopback.ToString(), "string"),
            ("Port", port, "int"),
            ("Address", "DM100", "string"),
            ("Length", 1, "int"),
            ("DataType", "Word", "string"),
            ("Operation", "Read", "string"));

        var result = await sut.ExecuteAsync(@operator, cancellationToken: cts.Token);

        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.OutputData.Should().NotBeNull();
        result.OutputData!["Status"].Should().Be(true);
        result.OutputData["DataType"].Should().Be("Word");
        result.OutputData["Value"].Should().Be((ushort)0x1234);

        await serverTask;
    }

    [Fact]
    public async Task OmronFinsCommunicationOperator_WriteAsync_ShouldUseUpstreamInputAndBigEndianPayload()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var serverTask = ServeFinsWriteAndCaptureAsync(listener, cts.Token);
        var sut = new OmronFinsCommunicationOperator(NullLogger<OmronFinsCommunicationOperator>.Instance);
        var @operator = CreateOperator(
            "FINS Write",
            OperatorType.OmronFinsCommunication,
            ("IpAddress", IPAddress.Loopback.ToString(), "string"),
            ("Port", port, "int"),
            ("Address", "DM100", "string"),
            ("DataType", "Word", "string"),
            ("Operation", "Write", "string"),
            ("WriteValue", string.Empty, "string"));

        var result = await sut.ExecuteAsync(
            @operator,
            new Dictionary<string, object> { ["Data"] = 4660 },
            cts.Token);

        var requestFrame = await serverTask;

        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.OutputData.Should().NotBeNull();
        result.OutputData!["Status"].Should().Be(true);
        result.OutputData["Value"].Should().Be("4660");
        requestFrame[^2].Should().Be(0x12);
        requestFrame[^1].Should().Be(0x34);
    }

    private static Operator CreateOperator(
        string name,
        OperatorType operatorType,
        params (string Name, object Value, string DataType)[] parameters)
    {
        var @operator = new Operator(name, operatorType, 0, 0);
        foreach (var (parameterName, value, dataType) in parameters)
        {
            @operator.AddParameter(new Parameter(Guid.NewGuid(), parameterName, parameterName, string.Empty, dataType, value));
        }

        return @operator;
    }

    private static void ResetPlcOperatorState()
    {
        PlcCommunicationOperatorBase.StopHeartbeat();

        var connectionPoolField = typeof(PlcCommunicationOperatorBase).GetField("_connectionPool", BindingFlags.Static | BindingFlags.NonPublic);
        var lastKnownStateField = typeof(PlcCommunicationOperatorBase).GetField("_lastKnownState", BindingFlags.Static | BindingFlags.NonPublic);

        if (connectionPoolField?.GetValue(null) is Dictionary<string, IPlcClient> connectionPool)
        {
            foreach (var client in connectionPool.Values)
            {
                try
                {
                    client.Dispose();
                }
                catch
                {
                }
            }

            connectionPool.Clear();
        }

        if (lastKnownStateField?.GetValue(null) is Dictionary<string, bool> lastKnownState)
        {
            lastKnownState.Clear();
        }
    }

    private static async Task<byte[]> ServeMcWriteAndCaptureAsync(TcpListener listener, CancellationToken ct)
    {
        using var server = await listener.AcceptTcpClientAsync(ct);
        using var stream = server.GetStream();

        var request = await ReadMcFrameAsync(stream, ct);
        await WriteInChunksAsync(stream, BuildMcWriteResponse(), ct, 1, 2, 3);
        return request;
    }

    private static async Task ServeMcReadAsync(TcpListener listener, CancellationToken ct, params byte[] data)
    {
        using var server = await listener.AcceptTcpClientAsync(ct);
        using var stream = server.GetStream();

        _ = await ReadMcFrameAsync(stream, ct);
        await WriteInChunksAsync(stream, BuildMcReadResponse(data), ct, 2, 2, 3, 1);
    }

    private static async Task<byte[]> ServeFinsWriteAndCaptureAsync(TcpListener listener, CancellationToken ct)
    {
        using var server = await listener.AcceptTcpClientAsync(ct);
        using var stream = server.GetStream();

        var handshakeRequest = new byte[20];
        await ReadExactAsync(stream, handshakeRequest, ct);
        await WriteInChunksAsync(stream, BuildFinsNodeAddressResponse(clientNode: 0x22, serverNode: 0x11), ct, 3, 5, 4);

        var request = await ReadFinsWrappedFrameAsync(stream, ct);
        await WriteInChunksAsync(stream, BuildFinsWriteResponse(), ct, 2, 6, 4, 3);
        return request;
    }

    private static async Task ServeFinsReadAsync(TcpListener listener, CancellationToken ct, params byte[] data)
    {
        using var server = await listener.AcceptTcpClientAsync(ct);
        using var stream = server.GetStream();

        var handshakeRequest = new byte[20];
        await ReadExactAsync(stream, handshakeRequest, ct);
        await WriteInChunksAsync(stream, BuildFinsNodeAddressResponse(clientNode: 0x22, serverNode: 0x11), ct, 4, 4, 4);

        _ = await ReadFinsWrappedFrameAsync(stream, ct);
        await WriteInChunksAsync(stream, BuildFinsReadResponse(data), ct, 5, 5, 5, 5);
    }

    private static async Task<byte[]> ReadMcFrameAsync(NetworkStream stream, CancellationToken ct)
    {
        var header = new byte[9];
        await ReadExactAsync(stream, header, ct);
        var dataLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(7, 2));

        var trailingLength = 6 + dataLength;
        var frame = new byte[header.Length + trailingLength];
        Array.Copy(header, frame, header.Length);

        if (trailingLength > 0)
        {
            await ReadExactAsync(stream, frame.AsMemory(header.Length, trailingLength), ct);
        }

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
        {
            await ReadExactAsync(stream, frame.AsMemory(16, length), ct);
        }

        return frame;
    }

    private static async Task WriteInChunksAsync(NetworkStream stream, byte[] payload, CancellationToken ct, params int[] chunkSizes)
    {
        var offset = 0;
        foreach (var size in chunkSizes)
        {
            if (offset >= payload.Length)
            {
                break;
            }

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
            {
                throw new IOException("Connection closed before expected bytes were received.");
            }

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
        {
            data.CopyTo(response, 11);
        }

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

[CollectionDefinition("PLC Operator Integration", DisableParallelization = true)]
public sealed class PlcOperatorIntegrationCollectionDefinition
{
}



