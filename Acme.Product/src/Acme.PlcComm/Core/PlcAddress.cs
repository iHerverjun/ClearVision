namespace Acme.PlcComm.Core;

/// <summary>
/// PLC数据类型枚举
/// </summary>
public enum PlcDataType
{
    Bit,
    Byte,
    Word,
    DWord,
    LWord,
    Int16,
    Int32,
    Float,
    Double,
    String
}

/// <summary>
/// 统一PLC地址模型
/// </summary>
public class PlcAddress
{
    /// <summary>
    /// 区域类型 (DB/M/I/Q/D/CIO/DM等)
    /// </summary>
    public string AreaType { get; set; } = string.Empty;

    /// <summary>
    /// DB块号（仅S7需要）
    /// </summary>
    public int DbNumber { get; set; }

    /// <summary>
    /// 起始地址偏移（字节或字）
    /// </summary>
    public int StartAddress { get; set; }

    /// <summary>
    /// 位偏移（位访问时使用，0-15）
    /// -1表示非位访问
    /// </summary>
    public int BitOffset { get; set; } = -1;

    /// <summary>
    /// 数据类型标识
    /// </summary>
    public PlcDataType DataType { get; set; } = PlcDataType.Word;

    /// <summary>
    /// 协议特定的软元件代码（如MC的0xA8）
    /// </summary>
    public byte DeviceCode { get; set; }

    /// <summary>
    /// 获取地址字符串表示
    /// </summary>
    public override string ToString()
    {
        if (AreaType.Equals("DB", StringComparison.OrdinalIgnoreCase))
        {
            var suffix = DataType switch
            {
                PlcDataType.Bit => "DBX",
                PlcDataType.Byte => "DBB",
                PlcDataType.Word or PlcDataType.Int16 => "DBW",
                PlcDataType.DWord or PlcDataType.Int32 or PlcDataType.Float => "DBD",
                _ => "DBW"
            };
            
            if (BitOffset >= 0)
                return $"DB{DbNumber}.{suffix}{StartAddress}.{BitOffset}";
            return $"DB{DbNumber}.{suffix}{StartAddress}";
        }
        
        if (BitOffset >= 0)
            return $"{AreaType}{StartAddress}.{BitOffset}";
        return $"{AreaType}{StartAddress}";
    }
}

/// <summary>
/// 重连策略配置
/// </summary>
public class ReconnectPolicy
{
    /// <summary>
    /// 是否启用自动重连
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// 重试间隔
    /// </summary>
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// 最大重试间隔
    /// </summary>
    public TimeSpan MaxRetryInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 是否启用指数退避
    /// </summary>
    public bool ExponentialBackoff { get; set; } = true;
}

/// <summary>
/// 连接事件参数
/// </summary>
public class ConnectionEventArgs : EventArgs
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string? ProtocolVersion { get; set; }
    public string? PlcModel { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; }
}

/// <summary>
/// 断开连接事件参数
/// </summary>
public class DisconnectionEventArgs : EventArgs
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public DisconnectionReason Reason { get; set; }
    public DateTime? LastSuccessfulCommunication { get; set; }
    public string? RecommendedRecovery { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 断开连接原因
/// </summary>
public enum DisconnectionReason
{
    UserInitiated,
    NetworkError,
    ProtocolError,
    HeartbeatTimeout,
    Unknown
}

/// <summary>
/// PLC错误事件参数
/// </summary>
public class PlcErrorEventArgs : EventArgs
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public int ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
}
