using System.Buffers.Binary;

namespace Acme.PlcComm.Mitsubishi;

/// <summary>
/// 三菱MC协议3E帧构造器
/// 负责构建请求帧和解析响应帧
/// </summary>
public class McFrameBuilder
{
    // 副头部
    private const ushort RequestSubHeader = 0x0050;  // 请求副头部
    private const ushort ResponseSubHeader = 0x00D0; // 响应副头部

    // 网络号和PC号(默认值)
    private const byte NetworkNumber = 0x00;
    private const byte PcNumber = 0xFF;

    // 模块IO号(0x03FF表示本站)
    private const ushort ModuleIo = 0x03FF;

    // 请求站号
    private const byte StationNumber = 0x00;

    // 监视定时器(4秒 = 0x0010)
    private const ushort MonitorTimer = 0x0010;

    // 命令代码
    private const ushort BatchReadCommand = 0x0401;  // 批量读取
    private const ushort BatchWriteCommand = 0x1401; // 批量写入

    // 子命令
    private const ushort WordAccess = 0x0000; // 字访问
    private const ushort BitAccess = 0x0001;  // 位访问

    private byte _sequenceNumber = 0;

    /// <summary>
    /// 构建批量读取请求帧
    /// </summary>
    public byte[] BuildReadRequest(byte deviceCode, int startAddress, ushort length, bool isBitAccess)
    {
        // 计算数据长度(从监视定时器到末尾)
        const int dataLength = 12; // 监视定时器(2) + 命令(2) + 子命令(2) + 起始地址(3) + 软元件代码(1) + 点数(2)

        var frame = new byte[15 + dataLength]; // 副头部(2) + 网络号(1) + PC号(1) + 模块IO(2) + 站号(1) + 数据长度(2) + 数据(dataLength)
        var offset = 0;

        // 副头部
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(offset), RequestSubHeader);
        offset += 2;

        // 网络号
        frame[offset++] = NetworkNumber;

        // PC号
        frame[offset++] = PcNumber;

        // 模块IO号
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(offset), ModuleIo);
        offset += 2;

        // 请求站号
        frame[offset++] = StationNumber;

        // 数据长度
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(offset), (ushort)dataLength);
        offset += 2;

        // 监视定时器
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(offset), MonitorTimer);
        offset += 2;

        // 命令
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(offset), BatchReadCommand);
        offset += 2;

        // 子命令
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(offset), isBitAccess ? BitAccess : WordAccess);
        offset += 2;

        // 起始地址(3字节，小端序)
        frame[offset++] = (byte)(startAddress & 0xFF);
        frame[offset++] = (byte)((startAddress >> 8) & 0xFF);
        frame[offset++] = (byte)((startAddress >> 16) & 0xFF);

        // 软元件代码
        frame[offset++] = deviceCode;

        // 点数(2字节，小端序)
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(offset), length);

        return frame;
    }

    /// <summary>
    /// 构建批量写入请求帧
    /// </summary>
    public byte[] BuildWriteRequest(byte deviceCode, int startAddress, ushort length, byte[] data, bool isBitAccess)
    {
        // 数据长度 = 固定部分(12) + 写入数据
        var dataLength = 12 + data.Length;

        var frame = new byte[15 + dataLength];
        var offset = 0;

        // 副头部
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(offset), RequestSubHeader);
        offset += 2;

        // 网络号
        frame[offset++] = NetworkNumber;

        // PC号
        frame[offset++] = PcNumber;

        // 模块IO号
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(offset), ModuleIo);
        offset += 2;

        // 请求站号
        frame[offset++] = StationNumber;

        // 数据长度
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(offset), (ushort)dataLength);
        offset += 2;

        // 监视定时器
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(offset), MonitorTimer);
        offset += 2;

        // 命令
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(offset), BatchWriteCommand);
        offset += 2;

        // 子命令
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(offset), isBitAccess ? BitAccess : WordAccess);
        offset += 2;

        // 起始地址(3字节，小端序)
        frame[offset++] = (byte)(startAddress & 0xFF);
        frame[offset++] = (byte)((startAddress >> 8) & 0xFF);
        frame[offset++] = (byte)((startAddress >> 16) & 0xFF);

        // 软元件代码
        frame[offset++] = deviceCode;

        // 点数(2字节，小端序)
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(offset), length);
        offset += 2;

        // 写入数据
        Array.Copy(data, 0, frame, offset, data.Length);

        return frame;
    }

    /// <summary>
    /// 解析读取响应帧
    /// </summary>
    public (bool success, byte[]? data, string? error) ParseReadResponse(byte[] response)
    {
        if (response == null || response.Length < 11)
            return (false, null, "响应帧太短");

        var offset = 0;

        // 检查副头部
        var subHeader = BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(offset));
        offset += 2;
        if (subHeader != ResponseSubHeader)
            return (false, null, $"无效的响应副头部: 0x{subHeader:X4}");

        // 跳过网络号、PC号、模块IO号、站号
        offset += 5;

        // 数据长度
        var dataLength = BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(offset));
        offset += 2;

        // 结束代码
        var endCode = BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(offset));
        offset += 2;

        if (endCode != 0x0000)
            return (false, null, $"PLC返回错误码: 0x{endCode:X4} - {GetErrorMessage(endCode)}");

        // 读取数据
        var data = new byte[response.Length - offset];
        Array.Copy(response, offset, data, 0, data.Length);

        return (true, data, null);
    }

    /// <summary>
    /// 解析写入响应帧
    /// </summary>
    public (bool success, string? error) ParseWriteResponse(byte[] response)
    {
        if (response == null || response.Length < 11)
            return (false, "响应帧太短");

        var offset = 0;

        // 检查副头部
        var subHeader = BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(offset));
        offset += 2;
        if (subHeader != ResponseSubHeader)
            return (false, $"无效的响应副头部: 0x{subHeader:X4}");

        // 跳过网络号、PC号、模块IO号、站号
        offset += 5;

        // 数据长度
        var dataLength = BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(offset));
        offset += 2;

        // 结束代码
        var endCode = BinaryPrimitives.ReadUInt16LittleEndian(response.AsSpan(offset));
        offset += 2;

        if (endCode != 0x0000)
            return (false, $"PLC返回错误码: 0x{endCode:X4} - {GetErrorMessage(endCode)}");

        return (true, null);
    }

    /// <summary>
    /// 获取错误代码对应的错误信息
    /// </summary>
    private static string GetErrorMessage(ushort endCode)
    {
        return endCode switch
        {
            0x0000 => "正常完成",
            0xC050 => "软元件范围超出",
            0xC051 => "请求长度错误",
            0xC056 => "软元件代码错误",
            0xC059 => "命令不支持",
            0xC061 => "数据长度错误",
            0xC064 => "请求数据错误",
            0xC090 => "监视定时器超时",
            _ => $"未知错误: 0x{endCode:X4}"
        };
    }

    /// <summary>
    /// 获取下一个序列号(用于4E帧)
    /// </summary>
    public byte GetNextSequenceNumber()
    {
        return _sequenceNumber++;
    }
}
