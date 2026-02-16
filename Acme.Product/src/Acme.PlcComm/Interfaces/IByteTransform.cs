using System.Text;
using Acme.PlcComm.Core;

namespace Acme.PlcComm.Interfaces;

/// <summary>
/// 字节序转换接口
/// </summary>
public interface IByteTransform
{
    // 基础类型转换 (byte[] → 值类型)
    short ToInt16(byte[] buffer, int index);
    ushort ToUInt16(byte[] buffer, int index);
    int ToInt32(byte[] buffer, int index);
    uint ToUInt32(byte[] buffer, int index);
    long ToInt64(byte[] buffer, int index);
    ulong ToUInt64(byte[] buffer, int index);
    float ToFloat(byte[] buffer, int index);
    double ToDouble(byte[] buffer, int index);
    bool ToBool(byte[] buffer, int index);
    string ToString(byte[] buffer, int index, int length, Encoding encoding);

    // 反向: 值类型 → byte[]
    byte[] GetBytes(short value);
    byte[] GetBytes(ushort value);
    byte[] GetBytes(int value);
    byte[] GetBytes(uint value);
    byte[] GetBytes(long value);
    byte[] GetBytes(ulong value);
    byte[] GetBytes(float value);
    byte[] GetBytes(double value);
    byte[] GetBytes(bool value);
    byte[] GetBytes(string value, int length, Encoding encoding);
}

/// <summary>
/// 地址解析器接口
/// </summary>
public interface IAddressParser
{
    /// <summary>
    /// 解析地址字符串为统一结构
    /// </summary>
    OperateResult<PlcAddress> Parse(string address);

    /// <summary>
    /// 尝试解析（不抛异常）
    /// </summary>
    bool TryParse(string address, out PlcAddress result);

    /// <summary>
    /// 将统一结构转回字符串
    /// </summary>
    string ToAddressString(PlcAddress address);

    /// <summary>
    /// 验证地址格式是否有效
    /// </summary>
    bool IsValidAddress(string address);
}

/// <summary>
/// 协议帧处理接口
/// </summary>
public interface IPlcProtocol
{
    /// <summary>
    /// 构造连接握手请求帧
    /// </summary>
    byte[] BuildConnectRequest(PlcConnectionConfig config);

    /// <summary>
    /// 解析连接握手响应
    /// </summary>
    OperateResult ParseConnectResponse(byte[] response);

    /// <summary>
    /// 构造数据读取请求帧
    /// </summary>
    byte[] BuildReadRequest(PlcAddress address, ushort length);

    /// <summary>
    /// 解析数据读取响应帧
    /// </summary>
    OperateResult<byte[]> ParseReadResponse(byte[] response);

    /// <summary>
    /// 构造数据写入请求帧
    /// </summary>
    byte[] BuildWriteRequest(PlcAddress address, byte[] data);

    /// <summary>
    /// 解析数据写入响应帧
    /// </summary>
    OperateResult ParseWriteResponse(byte[] response);

    /// <summary>
    /// 构造心跳检测帧
    /// </summary>
    byte[] BuildHeartbeatRequest();

    /// <summary>
    /// 解析心跳响应
    /// </summary>
    bool ParseHeartbeatResponse(byte[] response);
}

/// <summary>
/// PLC连接配置
/// </summary>
public class PlcConnectionConfig
{
    public string IpAddress { get; set; } = "192.168.0.1";
    public int Port { get; set; }
    public int ConnectTimeout { get; set; } = 10000;
    public int ReadTimeout { get; set; } = 5000;
    public int WriteTimeout { get; set; } = 5000;
    
    // S7专用
    public int Rack { get; set; } = 0;
    public int Slot { get; set; } = 1;
    public SiemensCpuType CpuType { get; set; } = SiemensCpuType.S71200;
    
    // MC专用
    public McFrameType FrameType { get; set; } = McFrameType.Frame3E;
    public byte NetworkNumber { get; set; } = 0;
    public byte PcNumber { get; set; } = 0xFF;
    
    // FINS专用
    public byte LocalNode { get; set; } = 0;
    public byte RemoteNode { get; set; } = 0;
}

/// <summary>
/// 西门子CPU类型
/// </summary>
public enum SiemensCpuType
{
    S7200 = 0,
    S7200Smart,
    S7300,
    S7400,
    S71200,
    S71500
}

/// <summary>
/// 三菱MC帧类型
/// </summary>
public enum McFrameType
{
    Frame3E = 0,
    Frame4E
}
