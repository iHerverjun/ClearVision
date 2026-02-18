// FinsFrameBuilder.cs
// 获取错误信息
// 作者：蘅芜君

using System.Buffers.Binary;

namespace Acme.PlcComm.Omron;

/// <summary>
/// FINS/TCP帧构造器
/// 负责构建FINS请求帧和解析响应
/// </summary>
public class FinsFrameBuilder
{
    // FINS/TCP魔数
    private static readonly byte[] FinsMagic = { 0x46, 0x49, 0x4E, 0x53 }; // "FINS"

    // 命令类型
    private const uint NodeAddressRequest = 0;    // 节点地址请求
    private const uint NodeAddressResponse = 1;   // 节点地址响应
    private const uint FinsFrameSend = 2;         // FINS帧发送

    // FINS命令
    private const byte MrcMemoryAreaRead = 0x01;  // 内存区读取
    private const byte SrcMemoryAreaRead = 0x01;  // 读取子命令
    private const byte SrcMemoryAreaWrite = 0x02; // 写入子命令

    // FINS帧固定字段
    private const byte IcfCommand = 0x80;  // 命令帧
    private const byte IcfResponse = 0xC0; // 响应帧
    private const byte Rsv = 0x00;         // 保留
    private const byte Gct = 0x02;         // 网关计数
    private const byte Dna = 0x00;         // 目标网络号(本地)
    private const byte Sna = 0x00;         // 源网络号
    private const byte Da2 = 0x00;         // 目标单元号(CPU)
    private const byte Sa2 = 0x00;         // 源单元号

    private byte _sid = 0; // 服务ID，递增

    /// <summary>
    /// 构建节点地址请求帧(FINS/TCP握手第一步)
    /// </summary>
    public byte[] BuildNodeAddressRequest()
    {
        var frame = new byte[20];
        var offset = 0;

        // FINS魔数
        Array.Copy(FinsMagic, 0, frame, offset, 4);
        offset += 4;

        // 长度(后续数据长度=12)
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(offset), 12);
        offset += 4;

        // 命令(节点地址请求)
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(offset), NodeAddressRequest);
        offset += 4;

        // 错误码(0)
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(offset), 0);
        offset += 4;

        // 客户端节点号(0表示请求自动分配)
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(offset), 0);

        return frame;
    }

    /// <summary>
    /// 解析节点地址响应帧
    /// </summary>
    public (bool success, byte clientNode, byte serverNode, string? error) ParseNodeAddressResponse(byte[] response)
    {
        if (response == null || response.Length < 24)
            return (false, 0, 0, "响应帧太短");

        var offset = 0;

        // 检查魔数
        if (!response.AsSpan(offset, 4).SequenceEqual(FinsMagic))
            return (false, 0, 0, "无效的FINS魔数");
        offset += 4;

        // 长度
        var length = BinaryPrimitives.ReadUInt32BigEndian(response.AsSpan(offset));
        offset += 4;

        // 命令
        var command = BinaryPrimitives.ReadUInt32BigEndian(response.AsSpan(offset));
        offset += 4;
        if (command != NodeAddressResponse)
            return (false, 0, 0, $"无效的响应命令: {command}");

        // 错误码
        var errorCode = BinaryPrimitives.ReadUInt32BigEndian(response.AsSpan(offset));
        offset += 4;
        if (errorCode != 0)
            return (false, 0, 0, $"握手错误码: 0x{errorCode:X8}");

        // 服务器节点号
        var serverNode = (byte)BinaryPrimitives.ReadUInt32BigEndian(response.AsSpan(offset));
        offset += 4;

        // 客户端节点号
        var clientNode = (byte)BinaryPrimitives.ReadUInt32BigEndian(response.AsSpan(offset));

        return (true, clientNode, serverNode, null);
    }

    /// <summary>
    /// 构建FINS读取请求帧
    /// </summary>
    public byte[] BuildReadRequest(byte areaCode, ushort startAddress, byte bitAddress, ushort length, 
        byte clientNode, byte serverNode)
    {
        // 构建FINS命令帧
        var finsFrame = new byte[12];
        var offset = 0;

        // FINS头部(10字节)
        finsFrame[offset++] = IcfCommand;  // ICF
        finsFrame[offset++] = Rsv;         // RSV
        finsFrame[offset++] = Gct;         // GCT
        finsFrame[offset++] = Dna;         // DNA
        finsFrame[offset++] = serverNode;  // DA1(目标节点号)
        finsFrame[offset++] = Da2;         // DA2
        finsFrame[offset++] = Sna;         // SNA
        finsFrame[offset++] = clientNode;  // SA1(源节点号)
        finsFrame[offset++] = Sa2;         // SA2
        finsFrame[offset++] = GetNextSid(); // SID

        // FINS命令(内存区读取)
        finsFrame[offset++] = MrcMemoryAreaRead; // MRC
        finsFrame[offset++] = SrcMemoryAreaRead; // SRC

        // 注意：实际FINS命令参数需要单独构建，这里简化处理
        // 完整的请求需要包含：内存区代码 + 起始地址 + 位地址 + 读取长度

        // 构建FINS/TCP封装
        return WrapFinsFrame(finsFrame);
    }

    /// <summary>
    /// 构建内存区读取请求(简化版，不包含在基本FINS帧中)
    /// </summary>
    public byte[] BuildMemoryReadRequest(byte areaCode, ushort startAddress, byte bitAddress, ushort length,
        byte clientNode, byte serverNode)
    {
        // FINS命令部分
        var commandData = new byte[8];
        var offset = 0;

        // FINS头部(10字节)
        var header = new byte[]
        {
            IcfCommand, Rsv, Gct, Dna, serverNode, Da2, Sna, clientNode, Sa2, GetNextSid()
        };

        // 命令
        commandData[offset++] = MrcMemoryAreaRead;
        commandData[offset++] = SrcMemoryAreaRead;

        // 内存区代码
        commandData[offset++] = areaCode;

        // 起始地址(2字节，大端序)
        commandData[offset++] = (byte)(startAddress >> 8);
        commandData[offset++] = (byte)(startAddress & 0xFF);

        // 位地址
        commandData[offset++] = bitAddress;

        // 读取长度(2字节，大端序)
        commandData[offset++] = (byte)(length >> 8);
        commandData[offset++] = (byte)(length & 0xFF);

        // 组合完整FINS帧
        var finsFrame = new byte[header.Length + commandData.Length];
        Array.Copy(header, finsFrame, header.Length);
        Array.Copy(commandData, 0, finsFrame, header.Length, commandData.Length);

        return WrapFinsFrame(finsFrame);
    }

    /// <summary>
    /// 构建内存区写入请求
    /// </summary>
    public byte[] BuildMemoryWriteRequest(byte areaCode, ushort startAddress, byte bitAddress, byte[] data,
        byte clientNode, byte serverNode)
    {
        var length = (ushort)(data.Length / 2); // 字数

        // FINS头部
        var header = new byte[]
        {
            IcfCommand, Rsv, Gct, Dna, serverNode, Da2, Sna, clientNode, Sa2, GetNextSid()
        };

        // 命令部分
        var commandData = new byte[8 + data.Length];
        var offset = 0;

        commandData[offset++] = MrcMemoryAreaRead;
        commandData[offset++] = SrcMemoryAreaWrite;
        commandData[offset++] = areaCode;
        commandData[offset++] = (byte)(startAddress >> 8);
        commandData[offset++] = (byte)(startAddress & 0xFF);
        commandData[offset++] = bitAddress;
        commandData[offset++] = (byte)(length >> 8);
        commandData[offset++] = (byte)(length & 0xFF);

        // 写入数据
        Array.Copy(data, 0, commandData, offset, data.Length);

        // 组合完整FINS帧
        var finsFrame = new byte[header.Length + commandData.Length];
        Array.Copy(header, finsFrame, header.Length);
        Array.Copy(commandData, 0, finsFrame, header.Length, commandData.Length);

        return WrapFinsFrame(finsFrame);
    }

    /// <summary>
    /// 解析FINS响应帧
    /// </summary>
    public (bool success, byte[]? data, string? error) ParseResponse(byte[] response)
    {
        if (response == null || response.Length < 16)
            return (false, null, "响应帧太短");

        var offset = 0;

        // 检查魔数
        if (!response.AsSpan(offset, 4).SequenceEqual(FinsMagic))
            return (false, null, "无效的FINS魔数");
        offset += 4;

        // 长度
        var length = BinaryPrimitives.ReadUInt32BigEndian(response.AsSpan(offset));
        offset += 4;

        // 命令
        var command = BinaryPrimitives.ReadUInt32BigEndian(response.AsSpan(offset));
        offset += 4;
        if (command != FinsFrameSend)
            return (false, null, $"无效的命令类型: {command}");

        // 错误码
        var errorCode = BinaryPrimitives.ReadUInt32BigEndian(response.AsSpan(offset));
        offset += 4;
        if (errorCode != 0)
            return (false, null, $"FINS/TCP错误码: 0x{errorCode:X8}");

        // FINS响应帧数据
        if (response.Length < offset + 14)
            return (false, null, "FINS响应帧太短");

        // 检查FINS响应头部
        var icf = response[offset];
        if (icf != IcfResponse)
            return (false, null, $"无效的ICF: 0x{icf:X2}");

        // 跳过FINS头部(10字节)读取MRC/SRC
        offset += 10;
        var mrc = response[offset++];
        var src = response[offset++];

        // 检查结束码
        var endCode = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(offset));
        offset += 2;

        if (endCode != 0x0000)
            return (false, null, $"FINS结束码错误: 0x{endCode:X4} - {GetErrorMessage(mrc, src, endCode)}");

        // 提取数据
        var dataLength = response.Length - offset;
        var data = new byte[dataLength];
        Array.Copy(response, offset, data, 0, dataLength);

        return (true, data, null);
    }

    /// <summary>
    /// 将FINS帧封装到FINS/TCP帧中
    /// </summary>
    private byte[] WrapFinsFrame(byte[] finsFrame)
    {
        var result = new byte[16 + finsFrame.Length];
        var offset = 0;

        // FINS魔数
        Array.Copy(FinsMagic, 0, result, offset, 4);
        offset += 4;

        // 长度
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(offset), (uint)finsFrame.Length);
        offset += 4;

        // 命令
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(offset), FinsFrameSend);
        offset += 4;

        // 错误码
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(offset), 0);
        offset += 4;

        // FINS帧数据
        Array.Copy(finsFrame, 0, result, offset, finsFrame.Length);

        return result;
    }

    /// <summary>
    /// 获取下一个SID(服务ID)
    /// </summary>
    private byte GetNextSid()
    {
        return _sid++;
    }

    /// <summary>
    /// 获取错误信息
    /// </summary>
    private static string GetErrorMessage(byte mrc, byte src, ushort endCode)
    {
        return endCode switch
        {
            0x0000 => "正常完成",
            0x0001 => "服务取消",
            0x0101 => "本地节点不在网络",
            0x0102 => "令牌超时",
            0x0103 => "重复接收",
            0x0104 => "发送忙",
            0x0105 => "响应超时",
            0x0201 => "目标节点不在网络",
            0x0202 => "目标节点不存在",
            0x0203 => "目标节点忙",
            0x0301 => "通信控制器错误",
            0x0401 => "地址范围超出",
            0x1001 => "命令格式错误",
            0x1002 => "不支持命令",
            0x1101 => "区域类型错误",
            0x1103 => "地址范围超出",
            0x2201 => "属性错误",
            0x2202 => "条件错误",
            _ => $"未知错误: MRC=0x{mrc:X2}, SRC=0x{src:X2}, EndCode=0x{endCode:X4}"
        };
    }
}
