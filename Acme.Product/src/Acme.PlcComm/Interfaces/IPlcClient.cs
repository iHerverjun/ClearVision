// IPlcClient.cs
// 错误发生事件
// 作者：蘅芜君

using Acme.PlcComm.Core;

namespace Acme.PlcComm.Interfaces;

/// <summary>
/// 统一PLC客户端接口
/// </summary>
public interface IPlcClient : IDisposable
{
    // ─── 连接属性 ───────────────────────────────────────────
    /// <summary>
    /// IP地址
    /// </summary>
    string IpAddress { get; }

    /// <summary>
    /// 端口号
    /// </summary>
    int Port { get; }

    /// <summary>
    /// 是否已连接
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 连接超时(毫秒)
    /// </summary>
    int ConnectTimeout { get; set; }

    /// <summary>
    /// 读取超时(毫秒)
    /// </summary>
    int ReadTimeout { get; set; }

    /// <summary>
    /// 写入超时(毫秒)
    /// </summary>
    int WriteTimeout { get; set; }

    /// <summary>
    /// 重连策略
    /// </summary>
    ReconnectPolicy ReconnectPolicy { get; set; }

    /// <summary>
    /// 字节序转换器（协议感知）
    /// </summary>
    IByteTransform ByteTransform { get; }

    // ─── 生命周期 ───────────────────────────────────────────
    /// <summary>
    /// 异步连接
    /// </summary>
    Task<bool> ConnectAsync(CancellationToken ct = default);

    /// <summary>
    /// 异步断开连接
    /// </summary>
    Task DisconnectAsync();

    // ─── 原始字节读写 ──────────────────────────────────────
    /// <summary>
    /// 读取原始字节数据
    /// </summary>
    Task<OperateResult<byte[]>> ReadAsync(string address, ushort length, CancellationToken ct = default);

    /// <summary>
    /// 写入原始字节数据
    /// </summary>
    Task<OperateResult> WriteAsync(string address, byte[] value, CancellationToken ct = default);

    // ─── 泛型类型化读写（核心便捷 API）─────────────────────
    /// <summary>
    /// 读取指定类型的数据
    /// </summary>
    Task<OperateResult<T>> ReadAsync<T>(string address, CancellationToken ct = default) where T : struct;

    /// <summary>
    /// 写入指定类型的数据
    /// </summary>
    Task<OperateResult> WriteAsync<T>(string address, T value, CancellationToken ct = default) where T : struct;

    // ─── 批量读写（多个不连续地址）─────────────────────────
    /// <summary>
    /// 批量读取多个地址的数据
    /// </summary>
    Task<OperateResult<Dictionary<string, byte[]>>> ReadBatchAsync(string[] addresses, ushort[] lengths, CancellationToken ct = default);

    // ─── 字符串读写 ────────────────────────────────────────
    /// <summary>
    /// 读取字符串
    /// </summary>
    Task<OperateResult<string>> ReadStringAsync(string address, ushort length, CancellationToken ct = default);

    /// <summary>
    /// 写入字符串
    /// </summary>
    Task<OperateResult> WriteStringAsync(string address, string value, CancellationToken ct = default);

    // ─── 连接状态检测 ──────────────────────────────────────
    /// <summary>
    /// 检测连接是否存活
    /// </summary>
    Task<bool> PingAsync(CancellationToken ct = default);

    // ─── 事件通知 ──────────────────────────────────────────
    /// <summary>
    /// 连接成功事件
    /// </summary>
    event EventHandler<ConnectionEventArgs>? Connected;

    /// <summary>
    /// 断开连接事件
    /// </summary>
    event EventHandler<DisconnectionEventArgs>? Disconnected;

    /// <summary>
    /// 错误发生事件
    /// </summary>
    event EventHandler<PlcErrorEventArgs>? ErrorOccurred;
}
