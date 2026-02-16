# HSL PLC 通信自研实施规范书 (Master Implementation Plan)

**版本**: v3.0 (Full Integrated)  
**日期**: 2026-02-15  
**状态**: 实施规划  
**来源整合**: opencode 架构设计 + Kimi 深度技术文档 + 架构师评审反馈

---

## 1. 核心目标与执行摘要

本项目旨在构建一套独立的、高性能的工业 PLC 通信框架，彻底替代外部商业库（如 HSLCommunication），实现对**西门子 S7**、**三菱 MC**、**欧姆龙 FINS** 三大主流协议的原生支持。

### 1.1 核心价值
- **自主可控**: 摆脱商业授权限制，掌握核心通信源码
- **架构统一**: 建立统一的 `PlcBaseClient` 抽象层，屏蔽底层协议差异
- **性能优化**: 针对工业场景优化连接池、批量读取和长连接保活机制
- **扩展性强**: 插件式协议架构，便于未来扩展 Modbus、EtherNet/IP、OPC UA 等

### 1.2 设计哲学

通信库的 API 设计遵循"**最小惊讶原则**"——方法的行为符合开发者的直觉预期。HslCommunication 的成功很大程度上源于其应用层 API 的易用性——几行代码即可完成连接和数据访问，这种设计理念值得借鉴。同时，我们需要在底层实现上做到**完全透明可调试**，每一个协议帧都有日志可追踪。

---

## 2. 现状评估与架构重构

### 2.1 现有系统诊断 (Current State Correction)

> [!WARNING]
> 以下现状分析基于项目源码实际走读，纠正了先前评审中的部分误判。

经过代码走读发现，现有 `Acme.Product` 项目的通信模块存在以下痛点：

| 组件 | 现状分析 | 改进方案 |
|------|----------|----------|
| **连接管理** | 分散管理：`ModbusCommunicationOperator` 和 `TcpCommunicationOperator` 各自用 `static ConcurrentDictionary` 维护私有连接池；`SerialCommunicationOperator` 无连接池（每次 `using new SerialPort`）；`ConnectionPoolManager` 类虽存在但**未被任何算子实际使用**。 | **强制统一**：废弃各算子的私有连接池，所有通信必须通过增强版的 `ConnectionPoolManager` 获取连接。 |
| **基类设计** | `OperatorBase` 仅提供 `GetStringParam`/`GetIntParam` 等参数获取和 `ExecuteCoreAsync` 抽象方法，缺乏通信基础设施（无重连、无互斥、无超时分级）。 | 引入 `PlcOperatorBase` 继承 `OperatorBase`，封装重连、超时、互斥锁等通用逻辑。 |
| **协议实现** | 仅支持 Modbus TCP（通过 NModbus 库）和原生 Socket（TcpClient）。 | 新增 S7、MC、FINS 三大协议栈实现。 |
| **地址解析** | 无统一解析器，Modbus 用 `RegisterAddress` 整数，TCP 直接收发字符串。 | 引入 `IAddressParser` 接口，统一解析 `DB1.DBW100` / `D100` / `CIO100.0` 等格式。 |
| **结果封装** | 各算子输出均为 `OperatorExecutionOutput`（`Dictionary<string, object>`），无内部通信结果类型。 | 新增 `OperateResult<T>` 用于协议层内部，在算子层转换为现有的 `OperatorExecutionOutput`。 |

### 2.2 总体架构设计

采用四层分离架构，确保核心逻辑与协议细节解耦。分层架构的优势在于各层职责明确、耦合度低——需要支持新 PLC 品牌时，只需在协议层添加新实现；网络技术演进时（如 TCP 向 TLS 加密升级），仅需改造传输层。

```
┌─────────────────────────────────────────────────────────────────┐
│                    应用层 (Application Layer)                     │
│  ┌──────────────────┐ ┌──────────────────┐ ┌────────────────┐   │
│  │  SiemensS7Comm   │ │ MitsubishiMcComm │ │ OmronFinsComm  │   │
│  │  Operator (算子)  │ │ Operator (算子)   │ │ Operator (算子) │   │
│  └────────┬─────────┘ └────────┬─────────┘ └───────┬────────┘   │
│           │ 继承 PlcOperatorBase                     │            │
└───────────┼──────────────────────────────────────────┼────────────┘
            │                                          │
┌───────────┼──────────────────────────────────────────┼────────────┐
│           ▼                                          ▼            │
│  ┌──────────────────────────────────────────────────────────┐    │
│  │              核心抽象层 (Core Abstraction)                │    │
│  │                                                          │    │
│  │  IPlcClient          PlcBaseClient        OperateResult<T>│    │
│  │  IPlcProtocol        IAddressParser       PlcAddress      │    │
│  │  IByteTransform      ReconnectPolicy      ConnectionPool  │    │
│  └──────────────────────────────────────────────────────────┘    │
└──────────────────────────┬───────────────────────────────────────┘
                           │
┌──────────────────────────┼───────────────────────────────────────┐
│                          ▼                                       │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │              协议实现层 (Protocol Layer)                    │  │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────────┐  │  │
│  │  │S7Protocol│ │McProtocol│ │FinsProto │ │ModbusProtocol│  │  │
│  │  │TPKT/COTP │ │3E/4E帧   │ │FINS/TCP  │ │(NModbus 已有)│  │  │
│  │  │S7Comm    │ │SLMP      │ │节点握手  │ │              │  │  │
│  │  └──────────┘ └──────────┘ └──────────┘ └──────────────┘  │  │
│  └────────────────────────────────────────────────────────────┘  │
└──────────────────────────┬───────────────────────────────────────┘
                           │
┌──────────────────────────┼───────────────────────────────────────┐
│                          ▼                                       │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │              传输层 (Transport Layer)                       │  │
│  │     TcpClient / NetworkStream  (异步 async/await)          │  │
│  │     SerialPort (RS-232/485)                                │  │
│  │     ConnectionPoolManager (统一连接池管理)                 │  │
│  └────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
```

### 2.3 核心类型定义

#### 2.3.1 OperateResult\<T\> — 统一操作结果

> [!IMPORTANT]
> `OperateResult<T>` 仅用于 **PLC 客户端内部**（协议层通信结果）。算子层对外仍使用现有的 `OperatorExecutionOutput`，在算子中做映射转换。

```csharp
/// <summary>
/// 统一的 PLC 操作结果封装，借鉴 HSL 的 Result 模式
/// </summary>
public class OperateResult
{
    public bool IsSuccess { get; set; }
    public int ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;

    public static OperateResult Success() => new() { IsSuccess = true };
    public static OperateResult Failure(int code, string msg) => 
        new() { IsSuccess = false, ErrorCode = code, Message = msg };
}

public class OperateResult<T> : OperateResult
{
    public T? Content { get; set; }

    public static OperateResult<T> Success(T content) => 
        new() { IsSuccess = true, Content = content };
    public static new OperateResult<T> Failure(int code, string msg) => 
        new() { IsSuccess = false, ErrorCode = code, Message = msg };
}
```

**算子层转换示例**:
```csharp
// 在 SiemensS7CommunicationOperator.ExecuteCoreAsync 中
var result = await _s7Client.ReadAsync<short>("DB1.DBW100");
if (result.IsSuccess)
    return OperatorExecutionOutput.Success(new Dictionary<string, object>
    {
        { "Value", result.Content },
        { "Status", true }
    });
else
    return OperatorExecutionOutput.Failure(result.Message);
```

#### 2.3.2 IPlcClient — 统一客户端接口

```csharp
public interface IPlcClient : IDisposable
{
    // ─── 连接属性 ───────────────────────────────────────────
    string IpAddress { get; }
    int Port { get; }
    bool IsConnected { get; }
    
    // 三级超时控制（单位：毫秒）
    int ConnectTimeout { get; set; }   // 默认 10000 (TCP 握手 + 协议握手)
    int ReadTimeout { get; set; }      // 默认 5000  (高频采集建议 1000)
    int WriteTimeout { get; set; }     // 默认 5000
    
    // 重连策略
    ReconnectPolicy ReconnectPolicy { get; set; }

    // ─── 生命周期 ───────────────────────────────────────────
    Task<bool> ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();
    
    // ─── 原始字节读写 ──────────────────────────────────────
    Task<OperateResult<byte[]>> ReadAsync(string address, ushort length, 
        CancellationToken ct = default);
    Task<OperateResult> WriteAsync(string address, byte[] value, 
        CancellationToken ct = default);
    
    // ─── 泛型类型化读写（核心便捷 API）─────────────────────
    Task<OperateResult<T>> ReadAsync<T>(string address, 
        CancellationToken ct = default) where T : struct;
    Task<OperateResult> WriteAsync<T>(string address, T value, 
        CancellationToken ct = default) where T : struct;
    
    // ─── 批量读写（多个不连续地址）─────────────────────────
    Task<OperateResult<byte[]>> ReadAsync(string[] addresses, ushort[] lengths, 
        CancellationToken ct = default);
    
    // ─── 字符串读写 ────────────────────────────────────────
    Task<OperateResult<string>> ReadStringAsync(string address, ushort length, 
        CancellationToken ct = default);
    Task<OperateResult> WriteStringAsync(string address, string value, 
        CancellationToken ct = default);
    
    // ─── 连接状态检测 ──────────────────────────────────────
    Task<bool> PingAsync(CancellationToken ct = default);

    // ─── 事件通知 ──────────────────────────────────────────
    event EventHandler<ConnectionEventArgs>? Connected;
    event EventHandler<DisconnectionEventArgs>? Disconnected;
    event EventHandler<PlcErrorEventArgs>? ErrorOccurred;
}
```

#### 2.3.3 ReconnectPolicy — 重连策略

```csharp
public class ReconnectPolicy
{
    public bool Enabled { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(2);
    public TimeSpan MaxRetryInterval { get; set; } = TimeSpan.FromSeconds(30);
    public bool ExponentialBackoff { get; set; } = true;  // 指数退避
}
```

### 2.4 PlcBaseClient 基类详细设计

抽象基类 `PlcBaseClient` 的设计质量直接决定整个通信库的易用性和扩展性。

#### 2.4.1 连接属性体系

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `IpAddress` | `string` | `"192.168.0.1"` | 支持 IPv4，连接后只读 |
| `Port` | `int` | `0` (品牌默认) | 0 表示自动选择：S7=102, MC=5002, FINS=9600 |
| `ConnectTimeout` | `int` | `10000` | TCP 握手 + 协议握手的总超时 |
| `ReadTimeout` | `int` | `5000` | 单次读操作超时，高频采集建议 1000ms |
| `WriteTimeout` | `int` | `5000` | 单次写操作超时 |
| `IsConnected` | `bool` | 计算属性 | 综合 TCP 状态 + 最后通信时间戳 + 心跳结果 |

> [!NOTE]
> `Port` 设为 0 时 `ConnectAsync()` 根据实现类自动选择默认端口。用户显式设置非零值可覆盖默认行为，满足非标准部署场景。

#### 2.4.2 核心方法体系

| 方法类型 | 同步签名 | 异步签名 | 说明 |
|---------|---------|---------|------|
| 连接管理 | `bool Connect()` | `Task<bool> ConnectAsync()` | 完整协议连接（TCP + 协议握手） |
| 断开连接 | `void Disconnect()` | `Task DisconnectAsync()` | 幂等操作，多次调用不异常 |
| 字节读取 | `OperateResult<byte[]> Read(addr, len)` | `Task<OperateResult<byte[]>> ReadAsync(...)` | 原始字节 |
| 类型化读取 | `OperateResult<T> Read<T>(addr)` | `Task<OperateResult<T>> ReadAsync<T>(...)` | 泛型自动转换 |
| 批量读取 | `OperateResult<T[]> Read<T>(addr, count)` | `Task<OperateResult<T[]>> ReadBatchAsync<T>(...)` | 连续地址优化 |
| 写入操作 | `OperateResult Write<T>(addr, value)` | `Task<OperateResult> WriteAsync<T>(...)` | 对称设计 |

#### 2.4.3 基类必须实现的基础设施

```csharp
public abstract class PlcBaseClient : IPlcClient
{
    // ─── 线程安全：单连接互斥锁 ────────────────────────────
    // PLC 协议通常为半双工，必须保证 Request-Response 串行
    private readonly SemaphoreSlim _communicationLock = new(1, 1);
    
    // ─── 字节序转换器（子类注入） ──────────────────────────
    protected IByteTransform ByteTransform { get; set; }
    // S7 → BigEndianTransform, MC → LittleEndianTransform
    
    // ─── 自动重连模板方法 ──────────────────────────────────
    protected async Task<OperateResult<T>> ExecuteWithReconnectAsync<T>(
        Func<Task<OperateResult<T>>> operation,
        CancellationToken ct)
    {
        for (int retry = 0; retry <= ReconnectPolicy.MaxRetries; retry++)
        {
            if (!IsConnected)
                await ConnectAsync(ct);
            
            try
            {
                await _communicationLock.WaitAsync(ct);
                try { return await operation(); }
                finally { _communicationLock.Release(); }
            }
            catch (IOException) when (retry < ReconnectPolicy.MaxRetries)
            {
                await DisconnectAsync();
                var delay = ReconnectPolicy.ExponentialBackoff
                    ? TimeSpan.FromSeconds(Math.Pow(2, retry))
                    : ReconnectPolicy.RetryInterval;
                await Task.Delay(delay, ct);
            }
        }
        return OperateResult<T>.Failure(-1, "超过最大重试次数");
    }
    
    // ─── 报文日志（现场调试核心工具）────────────────────────
    protected void LogFrame(string direction, byte[] data)
    {
        Logger.LogDebug("[{Dir}] {Hex}", direction, 
            BitConverter.ToString(data).Replace("-", " "));
    }
    
    // ─── 子类必须实现的抽象方法 ────────────────────────────
    protected abstract Task<bool> ConnectCoreAsync(CancellationToken ct);
    protected abstract Task DisconnectCoreAsync();
    protected abstract Task<OperateResult<byte[]>> ReadCoreAsync(
        string address, ushort length, CancellationToken ct);
    protected abstract Task<OperateResult> WriteCoreAsync(
        string address, byte[] value, CancellationToken ct);
}
```

#### 2.4.4 事件通知机制

| 事件 | 触发时机 | 参数内容 |
|------|---------|---------|
| `Connected` | 协议握手完成 | 时间戳、协议版本、PLC 型号（如可获取） |
| `Disconnected` | 连接断开 | 断开原因（用户主动 / 网络异常 / 协议错误 / 心跳超时）、最后成功通信时间、建议恢复策略 |
| `ErrorOccurred` | 非致命错误 | 错误码、描述、操作类型 |

> [!NOTE]
> 网络回调通常在后台线程，而 UI 更新需要 UI 线程。事件参数的构造必须在线程安全环境下完成。

### 2.5 协议层接口定义

#### 2.5.1 IPlcProtocol — 协议帧处理

```csharp
public interface IPlcProtocol
{
    /// <summary>构造连接握手请求帧</summary>
    byte[] BuildConnectRequest(PlcConnectionConfig config);
    
    /// <summary>解析连接握手响应</summary>
    OperateResult ParseConnectResponse(byte[] response);
    
    /// <summary>构造数据读取请求帧</summary>
    byte[] BuildReadRequest(PlcAddress address, ushort length);
    
    /// <summary>解析数据读取响应帧</summary>
    OperateResult<byte[]> ParseReadResponse(byte[] response);
    
    /// <summary>构造数据写入请求帧</summary>
    byte[] BuildWriteRequest(PlcAddress address, byte[] data);
    
    /// <summary>解析数据写入响应帧</summary>
    OperateResult ParseWriteResponse(byte[] response);
}
```

#### 2.5.2 IAddressParser — 地址解析器

```csharp
public interface IAddressParser
{
    /// <summary>解析地址字符串为统一结构</summary>
    OperateResult<PlcAddress> Parse(string address);
    
    /// <summary>尝试解析（不抛异常）</summary>
    bool TryParse(string address, out PlcAddress result);
    
    /// <summary>将统一结构转回字符串</summary>
    string ToString(PlcAddress address);
}
```

#### 2.5.3 PlcAddress — 统一地址模型

```csharp
public class PlcAddress
{
    /// <summary>区域类型 (DB/M/I/Q/D/CIO/DM 等)</summary>
    public string AreaType { get; set; } = string.Empty;
    
    /// <summary>DB块号（仅 S7 需要）</summary>
    public int DbNumber { get; set; }
    
    /// <summary>起始地址偏移（字节或字）</summary>
    public int StartAddress { get; set; }
    
    /// <summary>位偏移（位访问时使用，0-15）</summary>
    public int BitOffset { get; set; } = -1;  // -1 表示非位访问
    
    /// <summary>数据类型标识</summary>
    public PlcDataType DataType { get; set; } = PlcDataType.Word;
    
    /// <summary>协议特定的软元件代码（如 MC 的 0xA8）</summary>
    public byte DeviceCode { get; set; }
}

public enum PlcDataType
{
    Bit, Byte, Word, DWord, LWord,
    Int16, Int32, Float, Double, String
}
```

### 2.6 品牌专属实现类继承体系

| 实现类 | 目标系列 | 核心特性 | 字节序 |
|-------|---------|---------|--------|
| `SiemensS7Client` | S7-200/300/400/1200/1500 | CPU 类型枚举、Rack/Slot、PDU 协商、优化块访问 | **Big-Endian** |
| `MitsubishiMcClient` | FX5U, Q, iQ-R, iQ-F | 3E/4E 帧选择、软元件代码映射、监视定时器、X/Y 八进制 | **Little-Endian** |
| `OmronFinsClient` | CP1H, CJ2M, NJ, NX | FINS 握手、节点号交换、内存区代码、路由表 | **Big-Endian** |

### 2.7 扩展性设计

#### 协议插件机制
新品牌（如罗克韦尔、台达、基恩士）实现 `IPlcProtocol` + `IAddressParser` 接口后，通过 DI 注册即可接入。无需修改核心架构。

#### PlcClientFactory 工厂模式
```csharp
// 配置驱动：用户代码与具体品牌解耦
var client = PlcClientFactory.Create("S7://192.168.0.1:102?cpu=S7-1200&rack=0&slot=1");
var client = PlcClientFactory.Create("MC://192.168.3.1:5002?frame=3E");
var client = PlcClientFactory.Create("FINS://192.168.250.1:9600?node=1");
```

#### 版本自动适配
连接建立阶段通过协议握手信息识别 PLC 型号，自动选择适当的通信参数。自动检测失败时回退到用户显式配置的 `CpuType` 枚举值。

---

## 3. 协议详细设计与技术规范

本节整合了 Kimi 深度技术文档和架构师评审反馈，作为各协议实施的具体参考手册。

---

### 3.1 西门子 S7 协议 (Siemens S7)

#### 3.1.1 协议栈总览

S7 通信基于 ISO-on-TCP (RFC 1006)，由三层协议嵌套组成：

```
┌────────────────────────────────────────────────┐
│  TPKT Header  (4 bytes)                        │
│  Version(1) + Reserved(1) + Length(2)          │
├────────────────────────────────────────────────┤
│  COTP Header  (variable)                       │
│  连接管理层 (CR/CC/DT)                         │
├────────────────────────────────────────────────┤
│  S7Comm PDU  (variable)                        │
│  应用数据层 (Job/Ack_Data/UserData)            │
└────────────────────────────────────────────────┘
```

**TPKT Header 固定格式** (4 字节):
| 字节 | 含义 | 固定值 |
|------|------|--------|
| Byte 0 | Version | `0x03` |
| Byte 1 | Reserved | `0x00` |
| Byte 2-3 | Total Length (Big-Endian) | 包含 TPKT 自身的总长度 |

#### 3.1.2 连接握手三步流程

**Step 1: TCP Connect** → 目标端口 `102`

**Step 2: COTP Connection Request (CR) / Confirm (CC)**

CR 报文模板 (22 字节):
```
03 00 00 16        ← TPKT: version=3, length=22
11                 ← COTP: PDU长度=17
E0                 ← COTP类型: CR (Connection Request)
00 00              ← Destination Reference
00 00              ← Source Reference
00                 ← Class/Options
C0 01 0A           ← Parameter: TPDU Size = 1024
C1 02 01 00        ← Parameter: Source TSAP = 0x0100
C2 02 01 02        ← Parameter: Destination TSAP = 0x0102
```

> [!IMPORTANT]
> **TSAP 计算规则** (决定能否连上 PLC 的关键):
> - S7-1200/1500: Source TSAP=`0x0100`, Dest TSAP=`0x0102` (固定)
> - S7-300/400: Dest TSAP 需动态计算 = `0x0100 | (Rack << 5) | Slot`
>   - 例: Rack=0, Slot=2 → `0x0100 | 0x00 | 0x02` = `0x0102`
>   - 例: Rack=0, Slot=3 → `0x0103`

**Step 3: S7 Setup Communication (PDU 协商)**

请求帧 (25 字节):
```
03 00 00 19        ← TPKT
02 F0 80           ← COTP DT (Data Transfer)
32                 ← S7Comm Protocol ID
01                 ← ROSCTR: Job (请求)
00 00              ← Redundancy
00 00              ← PDU Reference
00 08              ← Parameter Length = 8
00 00              ← Data Length = 0
F0                 ← Function: Setup Communication
00                 ← Reserved
00 01              ← Max AmQ Calling (并发请求数)
00 01              ← Max AmQ Called
03 C0              ← PDU Size = 960 (请求值)
```

> [!WARNING]
> **PDU Size 协商结果必须缓存！** PLC 可能返回比请求更小的 PDU Size（如 S7-200 仅支持 240）。后续所有读写请求的数据分包必须以**协商后的 PDU Size**为上限，否则 PLC 会拒绝请求。

#### 3.1.3 CPU 类型枚举与差异

```csharp
public enum SiemensCpuType
{
    S7200 = 0,    // PDU=240, 老旧协议，不推荐
    S7200Smart,   // PDU=240, 增强版
    S7300,        // PDU=480, TSAP需Rack/Slot计算
    S7400,        // PDU=480, 多机架
    S71200,       // PDU=240, 需TIA Portal开启PUT/GET
    S71500        // PDU=960, 需TIA Portal开启PUT/GET
}
```

| CPU 类型 | 默认 PDU | TSAP 规则 | 特殊要求 |
|---------|---------|----------|---------|
| S7-200/Smart | 240 | 固定 | PPI 协议特殊处理 |
| S7-300/400 | 480 | Rack/Slot 计算 | 无额外限制 |
| S7-1200 | 240 | 固定 0x0102 | **必须在 TIA Portal 开启 PUT/GET** |
| S7-1500 | 960 | 固定 0x0102 | **必须在 TIA Portal 开启 PUT/GET + 关闭优化块访问** |

#### 3.1.4 PLC 端配置要求 (TIA Portal)

> [!CAUTION]
> 以下配置缺一不可，否则 S7-1200/1500 会拒绝第三方通信：
> 1. **Protection & Security** → Access level = "Full access (no protection)"
> 2. **Protection & Security** → "Permit access with PUT/GET" = ✅ 勾选
> 3. **DB 块属性** → "Optimized block access" = ❌ 取消勾选 (仅 S7-1500)

#### 3.1.5 S7 地址映射表

| 地址格式 | 区域代码 | DB号 | 偏移计算 | 示例 |
|---------|---------|------|---------|------|
| `DB{n}.DBB{offset}` | 0x84 | n | offset × 8 (位) | DB1.DBB100 = DB1, Byte 100 |
| `DB{n}.DBW{offset}` | 0x84 | n | offset × 8 | DB1.DBW100 = DB1, Word 100 |
| `DB{n}.DBD{offset}` | 0x84 | n | offset × 8 | DB1.DBD100 = DB1, DWord 100 |
| `DB{n}.DBX{byte}.{bit}` | 0x84 | n | byte×8 + bit | DB1.DBX10.3 = DB1, Bit 10.3 |
| `M{byte}` / `MB{byte}` | 0x83 | 0 | byte × 8 | M100 = Merker Byte 100 |
| `M{byte}.{bit}` | 0x83 | 0 | byte×8 + bit | M100.2 = Merker Bit |
| `I{byte}.{bit}` / `E` | 0x81 | 0 | byte×8 + bit | I0.0 = Input |
| `Q{byte}.{bit}` / `A` | 0x82 | 0 | byte×8 + bit | Q0.0 = Output |

#### 3.1.6 S7 读取请求帧 (Read Var)

```
S7Comm Header:
  32              ← Protocol ID
  01              ← ROSCTR: Job
  00 00           ← Redundancy
  xx xx           ← PDU Reference (递增)
  00 0E           ← Parameter Length
  00 00           ← Data Length

Parameter:
  04              ← Function: Read Var
  01              ← Item Count
  12              ← Variable Spec
  0A              ← Length of address spec
  10              ← Syntax ID: S7ANY
  02              ← Transport Size: BYTE
  00 01           ← Data Length (字数)
  00 01           ← DB Number
  84              ← Area: DB
  00 00 00        ← Byte Offset (3字节, 位地址)
```

#### 3.1.7 推荐实现方案

> [!TIP]
> 鉴于 S7 协议的复杂性（PDU 分组、握手细节、CPU 差异），**强烈推荐**基于以下开源库二次封装：
> - **S7NetPlus** (MIT): C# 原生, NuGet 可用, 支持 async, 社区活跃
> - **Sharp7** (LGPL): C 移植, 性能极高, 但 API 较底层
>
> 推荐选择 **S7NetPlus**，其 API 风格与我们的 `IPlcClient` 设计最为契合。

---

### 3.2 三菱 MC 协议 (Mitsubishi MC)

#### 3.2.1 帧格式选型

| 帧类型 | 协议名称 | 适用系列 | 推荐度 |
|-------|---------|---------|--------|
| **3E 帧** | Qna-3E / SLMP | FX5U, Q, iQ-R, iQ-F | ⭐⭐⭐ 首选 |
| 4E 帧 | Qna-4E | Q, iQ-R (带序列号) | 按需 |
| 4C 帧 | A-compatible | A/FX3 系列 (老旧) | 不推荐 |

**决策: 优先实现 3E 帧二进制模式**，覆盖主流型号。

#### 3.2.2 3E 帧完整结构 (二进制模式)

**请求帧结构**:
```
┌──────────┬──────────────────────────────────────────────────┐
│ 副头部    │ 50 00                                           │
├──────────┼──────────────────────────────────────────────────┤
│ 网络号    │ 00                                               │
│ PC号      │ FF                                               │
│ 请求目标  │ FF 03  (模块IO: 0x03FF)                         │
│ 请求站号  │ 00                                               │
├──────────┼──────────────────────────────────────────────────┤
│ 数据长度  │ xx xx  (从监视定时器到末尾的字节数, Little-Endian)│
├──────────┼──────────────────────────────────────────────────┤
│ 监视定时器│ 10 00  (0x0010 = 16 × 250ms = 4秒)              │
├──────────┼──────────────────────────────────────────────────┤
│ 命令      │ 01 04  (批量读取)  /  01 14  (批量写入)         │
│ 子命令    │ 00 00  (字访问)    /  01 00  (位访问)           │
├──────────┼──────────────────────────────────────────────────┤
│ 起始软元件│ xx xx xx  (3字节, Little-Endian)                 │
│ 软元件代码│ xx       (1字节)                                 │
│ 软元件点数│ xx xx    (2字节, Little-Endian)                  │
├──────────┼──────────────────────────────────────────────────┤
│ 写入数据  │ (仅写入命令时有此段)                             │
└──────────┴──────────────────────────────────────────────────┘
```

**响应帧结构**:
```
D0 00              ← 副头部 (响应标识)
00                 ← 网络号
FF                 ← PC号
FF 03              ← 模块IO
00                 ← 站号
xx xx              ← 数据长度
00 00              ← 结束代码 (0x0000 = 成功)
[数据...]          ← 读取的数据
```

#### 3.2.3 软元件代码映射表

| 软元件 | 代码 (Hex) | 类型 | 点数单位 | 地址进制 | 说明 |
|-------|-----------|------|---------|---------|------|
| **D** (数据寄存器) | `0xA8` | 字 (16位) | 字 | 十进制 | 最常用 |
| **W** (链接寄存器) | `0xB4` | 字 (16位) | 字 | 十六进制 | 网络通信 |
| **R** (文件寄存器) | `0xAF` | 字 (16位) | 字 | 十进制 | 大容量存储 |
| **M** (辅助继电器) | `0x90` | 位 | 位 | 十进制 | 内部标志 |
| **L** (锁存继电器) | `0x92` | 位 | 位 | 十进制 | 断电保持 |
| **B** (链接继电器) | `0xA0` | 位 | 位 | 十六进制 | 网络通信 |
| **X** (输入继电器) | `0x9C` | 位 | 位 | **八进制** ⚠️ | 物理输入 |
| **Y** (输出继电器) | `0x9D` | 位 | 位 | **八进制** ⚠️ | 物理输出 |
| **T** (定时器) | `0xC1` | 位+字 | 混合 | 十进制 | 当前值/触点 |
| **C** (计数器) | `0xC5` | 位+字 | 混合 | 十进制 | 当前值/触点 |

> [!WARNING]
> **X/Y 地址八进制陷阱**: `X10` 不是十进制 10，而是八进制 10 = 十进制 8。地址解析器**必须**实现：
> ```csharp
> // X/Y 地址八进制转十进制
> int ParseOctalAddress(string address) => Convert.ToInt32(address, 8);
> // X10 → ParseOctalAddress("10") → 8
> // X17 → ParseOctalAddress("17") → 15
> // X20 → ParseOctalAddress("20") → 16
> ```

#### 3.2.4 批量读取请求帧示例

读取 D100 起始的 10 个字 (构造完整帧):
```
50 00              ← 副头部
00                 ← 网络号
FF                 ← PC号
FF 03              ← 模块IO
00                 ← 站号
0C 00              ← 数据长度=12 (从监视定时器开始)
10 00              ← 监视定时器
01 04              ← 命令: 批量读取
00 00              ← 子命令: 字访问
64 00 00           ← 起始地址: 100 (0x000064, LE)
A8                 ← 软元件代码: D寄存器
0A 00              ← 点数: 10 (0x000A, LE)
```

#### 3.2.5 批量限制与分包策略

| 帧类型 | 字读取上限 | 位读取上限 | 分包策略 |
|-------|-----------|-----------|---------|
| 3E 帧 | **960 字** (1920字节) | 7168 点 | 超限自动拆分为多帧，合并响应 |
| 4E 帧 | 960 字 | 7168 点 | 同上，附加序列号校验 |

#### 3.2.6 MC 错误代码参考

| 结束代码 | 含义 | 常见原因 |
|---------|------|---------|
| `0x0000` | 成功 | - |
| `0xC050` | 软元件范围超出 | 地址超出 PLC 容量 |
| `0xC051` | 请求长度错误 | 批量读取点数过大 |
| `0xC056` | 软元件代码错误 | 使用了该CPU不支持的软元件 |
| `0xC059` | 命令不支持 | CPU 系列不支持该命令 |
| `0xC061` | 数据长度错误 | 帧构造错误 |

#### 3.2.7 实现方案

**自研原生实现**，因为 MC 协议结构扁平，帧构造逻辑清晰。实现重点：
1. **地址解析器**: 处理 X/Y 八进制、W/B 十六进制、D/M 十进制三种进制
2. **自动分包**: 超过 960 字自动拆分为多帧请求，合并响应数据
3. **帧构造器**: `McFrameBuilder` 类封装 3E 帧的构建与解析

---

### 3.3 欧姆龙 FINS 协议 (Omron FINS)

#### 3.3.1 FINS/TCP 架构

FINS 协议有两种传输方式，本项目实现 **FINS/TCP**：

| 传输方式 | 端口 | 特点 | 选择 |
|---------|------|------|------|
| FINS/TCP | 9600 | 需要握手交换节点号 | ✅ 推荐 |
| FINS/UDP | 9600 | 无需握手，但无重传保证 | ❌ 不选 |

#### 3.3.2 FINS/TCP 握手流程 (连接建立后必做)

**Step 1: 客户端发送节点请求帧** (20 字节):
```
46 49 4E 53        ← Magic: "FINS" (ASCII)
00 00 00 0C        ← Length: 12 (后续数据长度)
00 00 00 00        ← Command: 0 (节点地址请求)
00 00 00 00        ← Error Code: 0
00 00 00 00        ← Client Node: 0 (请求自动分配)
```

**Step 2: PLC 返回节点分配响应** (24 字节):
```
46 49 4E 53        ← Magic: "FINS"
00 00 00 10        ← Length: 16
00 00 00 01        ← Command: 1 (节点地址响应)
00 00 00 00        ← Error Code: 0 (成功)
00 00 00 xx        ← Server Node (PLC 节点号) → 保存为 DA1
00 00 00 yy        ← Client Node (分配给客户端) → 保存为 SA1
```

> [!IMPORTANT]
> 握手返回的 `Server Node` 和 `Client Node` **必须缓存**，后续所有 FINS 帧的 DA1/SA1 字段都要填入这两个值。如果填错，PLC 会返回 "路由错误"。

#### 3.3.3 FINS 帧完整结构

**FINS/TCP 数据帧**:
```
46 49 4E 53        ← Magic: "FINS"
00 00 00 xx        ← Length (后续数据长度)
00 00 00 02        ← Command: 2 (FINS Frame Send)
00 00 00 00        ← Error Code: 0
┌── FINS Header (10 bytes) ──────────────────────┐
│ ICF: 80           ← 命令帧 (80=请求, C0=响应)  │
│ RSV: 00           ← 保留                        │
│ GCT: 02           ← 网关计数                    │
│ DNA: 00           ← 目标网络号 (0=本地)         │
│ DA1: xx           ← 目标节点号 (握手获取)       │
│ DA2: 00           ← 目标单元号 (0=CPU)          │
│ SNA: 00           ← 源网络号                     │
│ SA1: yy           ← 源节点号 (握手获取)         │
│ SA2: 00           ← 源单元号                     │
│ SID: zz           ← 服务ID (递增, 用于匹配响应) │
└────────────────────────────────────────────────┘
┌── FINS Command ────────────────────────────────┐
│ MRC: 01           ← 主命令代码                   │
│ SRC: 01           ← 副命令代码 (01=读, 02=写)  │
│ [命令参数...]                                    │
└────────────────────────────────────────────────┘
```

#### 3.3.4 内存区代码表

| 内存区 | 区代码 (字访问) | 区代码 (位访问) | 说明 | 常用范围 |
|-------|----------------|----------------|------|---------|
| **CIO** (通道IO) | `0xB0` | `0x30` | 输入输出、内部继电器 | CIO 0~6143 |
| **WR** (Work) | `0xB1` | `0x31` | 工作区 | W0~511 |
| **HR** (Holding) | `0xB2` | `0x32` | 保持区 (断电保持) | H0~511 |
| **AR** (Auxiliary) | `0xB3` | `0x33` | 辅助区 (系统信息) | A0~959 |
| **DM** (Data Memory) | `0x82` | `0x02` | 数据存储区 | D0~32767 |
| **TIM** (Timer) | `0x89` | `0x09` | 定时器 (PV/状态) | T0~4095 |
| **CNT** (Counter) | `0x89` | `0x09` | 计数器 (PV/状态) | C0~4095 |
| **EM** (Extended) | `0xA0`~`0xAF` | `0x20`~`0x2F` | 扩展数据存储 | 按Bank |

#### 3.3.5 读取内存命令 (MRC=01, SRC=01)

请求参数:
```
xx              ← 内存区代码
xx xx           ← 起始地址 (Big-Endian, 字地址)
xx              ← 位地址 (字访问时为 0x00)
xx xx           ← 读取长度 (字数, Big-Endian)
```

示例: 读取 DM100 起始的 10 个字:
```
82              ← 内存区: DM (字访问)
00 64           ← 起始地址: 100 (Big-Endian)
00              ← 位地址: 0
00 0A           ← 长度: 10 字
```

#### 3.3.6 写入内存命令 (MRC=01, SRC=02)

请求参数:
```
xx              ← 内存区代码
xx xx           ← 起始地址 (Big-Endian)
xx              ← 位地址
xx xx           ← 写入长度 (字数)
[数据...]       ← 写入数据 (Big-Endian, 每字 2 字节)
```

#### 3.3.7 FINS 错误码参考

| Main/Sub Code | 含义 | 处理建议 |
|--------------|------|---------|
| `00 00` | 正常完成 | - |
| `00 01` | 服务取消 | 检查PLC运行状态 |
| `01 01` | 本地节点不在网络 | 检查网络连线 |
| `02 01` | 令牌超时 | 网络拥塞，增加重试间隔 |
| `02 03` | 重复令牌 | 节点号冲突，重新握手 |
| `04 01` | 地址范围超出 | 检查内存区容量 |
| `05 01` | 程序号超出 | 检查PLC程序配置 |
| `10 01` | 命令格式错误 | 检查帧构造 |
| `21 01` | CPU 运行中不可操作 | 需切换到 PROGRAM 模式 |

#### 3.3.8 实现方案

**自研原生实现**，重点关注：
1. **握手管理**: 必须正确缓存 DA1/SA1，断线重连后需重新握手
2. **SID 管理**: 服务 ID 递增 (0~FF 循环)，用于匹配异步响应
3. **大端序**: FINS 所有多字节字段均为 Big-Endian（与 MC 相反）
4. **超时机制**: FINS 响应通常较慢（100-500ms），ReadTimeout 建议设为 3000ms

---

## 4. 项目结构与目录规划

### 4.1 独立类库方案 (Solution 内项目引用)

```
ClearVision.sln
│
├── Acme.Product.Core/                    ← 现有核心层 (不动)
├── Acme.Product.Infrastructure/          ← 现有基础设施层
│   └── Operators/
│       ├── ModbusCommunicationOperator.cs    ← 重构: 改用 ConnectionPoolManager
│       ├── TcpCommunicationOperator.cs       ← 重构: 改用 ConnectionPoolManager
│       ├── SerialCommunicationOperator.cs    ← 重构: 加入连接池
│       ├── SiemensS7CommunicationOperator.cs ← 【新建】S7 算子
│       ├── MitsubishiMcCommunicationOperator.cs ← 【新建】MC 算子
│       └── OmronFinsCommunicationOperator.cs ← 【新建】FINS 算子
├── Acme.Product.Desktop/                 ← 现有桌面端
│
├── Acme.PlcComm/                         ← 【新建】独立 PLC 通信类库
│   ├── Acme.PlcComm.csproj
│   ├── Core/                             ← 核心抽象
│   │   ├── IPlcClient.cs
│   │   ├── PlcBaseClient.cs
│   │   ├── OperateResult.cs
│   │   ├── PlcAddress.cs
│   │   ├── ReconnectPolicy.cs
│   │   └── PlcConnectionConfig.cs
│   ├── Interfaces/                       ← 接口定义
│   │   ├── IPlcProtocol.cs
│   │   ├── IAddressParser.cs
│   │   └── IByteTransform.cs
│   ├── Common/                           ← 通用功能模块
│   │   ├── BigEndianTransform.cs
│   │   ├── LittleEndianTransform.cs
│   │   ├── PlcConnectionPool.cs
│   │   ├── HeartbeatManager.cs
│   │   └── FrameLogger.cs
│   ├── Siemens/                          ← 西门子 S7 实现
│   │   ├── SiemensS7Client.cs
│   │   ├── S7AddressParser.cs
│   │   ├── S7Protocol.cs
│   │   └── SiemensCpuType.cs
│   ├── Mitsubishi/                       ← 三菱 MC 实现
│   │   ├── MitsubishiMcClient.cs
│   │   ├── McAddressParser.cs
│   │   ├── McProtocol.cs
│   │   └── McFrameBuilder.cs
│   └── Omron/                            ← 欧姆龙 FINS 实现
│       ├── OmronFinsClient.cs
│       ├── FinsAddressParser.cs
│       ├── FinsProtocol.cs
│       └── FinsHandshake.cs
│
└── Acme.PlcComm.Tests/                   ← 【新建】通信库单元测试
    ├── Acme.PlcComm.Tests.csproj
    ├── Core/
    │   └── OperateResultTests.cs
    ├── AddressParsers/
    │   ├── S7AddressParserTests.cs
    │   ├── McAddressParserTests.cs
    │   └── FinsAddressParserTests.cs
    ├── Protocols/
    │   ├── McFrameBuilderTests.cs
    │   └── FinsHandshakeTests.cs
    └── Integration/
        ├── S7IntegrationTests.cs
        ├── McIntegrationTests.cs
        └── FinsIntegrationTests.cs
```

### 4.2 项目引用关系

```
Acme.Product.Infrastructure  ──引用──→  Acme.PlcComm
Acme.PlcComm.Tests           ──引用──→  Acme.PlcComm
```

> [!NOTE]
> `Acme.PlcComm` 是纯类库，**不依赖** `Acme.Product.Core` 或任何业务层。它只依赖 `Microsoft.Extensions.Logging.Abstractions` 和协议库 (如 S7NetPlus)。未来可独立打包为 NuGet。

---

## 5. 通用功能模块设计

### 5.1 IByteTransform — 字节序转换

```csharp
public interface IByteTransform
{
    // 基础类型转换 (byte[] ↔ 值类型)
    short ToInt16(byte[] buffer, int index);
    ushort ToUInt16(byte[] buffer, int index);
    int ToInt32(byte[] buffer, int index);
    float ToFloat(byte[] buffer, int index);
    double ToDouble(byte[] buffer, int index);
    string ToString(byte[] buffer, int index, int length, Encoding encoding);

    // 反向: 值类型 → byte[]
    byte[] GetBytes(short value);
    byte[] GetBytes(int value);
    byte[] GetBytes(float value);
    byte[] GetBytes(string value, int length, Encoding encoding);
}
```

| 实现类 | 适用协议 | 字节序规则 |
|-------|---------|-----------|
| `BigEndianTransform` | S7, FINS | 高位在前: `0x0064` → `[0x00, 0x64]` |
| `LittleEndianTransform` | MC | 低位在前: `0x0064` → `[0x64, 0x00]` |

### 5.2 数据类型映射表

| C# 类型 | PLC 表示 | 字节数 | S7 地址后缀 | MC 软元件 | FINS 读取 |
|---------|---------|--------|------------|----------|----------|
| `bool` | Bit | 1 bit | `DBX` | M/X/Y (位) | 位访问区代码 |
| `byte` | Byte | 1 | `DBB` | - | 1字节 |
| `short` / `ushort` | Word (16-bit) | 2 | `DBW` | D (1字) | 1字 |
| `int` / `uint` | DWord (32-bit) | 4 | `DBD` | D (2字) | 2字 |
| `float` | Real (32-bit IEEE754) | 4 | `DBD` | D (2字) | 2字 |
| `double` | LReal (64-bit IEEE754) | 8 | `DBD`×2 | D (4字) | 4字 |
| `string` | 变长字符串 | N+2 (S7) | 特殊 | D (N字) | N字 |

> [!WARNING]
> **S7 字符串特殊格式**: S7 的 String 类型前两个字节分别是`最大长度`和`实际长度`，实际字符从第3字节开始。读写字符串时必须处理这个头部。

### 5.3 连接池增强设计 (PlcConnectionPool)

```csharp
public class PlcConnectionPool
{
    private readonly ConcurrentDictionary<string, PooledConnection> _pool = new();
    private readonly Timer _healthCheckTimer;

    // 获取或创建连接 (线程安全)
    public async Task<IPlcClient> GetConnectionAsync(
        string key, Func<IPlcClient> factory, CancellationToken ct);

    // 归还连接
    public void ReturnConnection(string key, IPlcClient client);

    // 健康检查 (定时器驱动)
    private async Task HealthCheckAsync()
    {
        foreach (var (key, conn) in _pool)
        {
            if (!await conn.Client.PingAsync())
            {
                conn.Client.Dispose();
                _pool.TryRemove(key, out _);
                Logger.LogWarning("连接 {Key} 健康检查失败，已移除", key);
            }
        }
    }
}
```

### 5.4 心跳保活机制

| 协议 | 心跳方式 | 默认间隔 | 实现 |
|------|---------|---------|------|
| S7 | 读取 CPU 状态 (SZL) | 30秒 | `ReadCpuStatus()` |
| MC | 读取 D0 (1字) | 30秒 | `ReadAsync("D0", 1)` |
| FINS | 读取 DM0 (1字) | 30秒 | `ReadAsync("DM0", 1)` |

### 5.5 批量读取优化策略

工业场景常需同时读取多个不连续地址（如 D100, D200, D500），朴素实现是发 3 个独立请求。优化策略：

1. **地址合并**: 如果地址间距 ≤ 阈值（如 100 字），合并为一次大范围读取，丢弃中间无用数据
2. **分组排序**: 将地址按区域分组（同 DB 块 / 同软元件类型），每组内排序后合并
3. **PDU 填充**: 将多个小请求打包到一个 PDU 内（S7 支持 Multi-Read）

```csharp
// 使用示例
var addresses = new[] { "D100", "D105", "D110", "D500" };
// 优化器自动合并: D100~D110 一次读取, D500 单独一次
var optimized = AddressOptimizer.Optimize(addresses, maxGap: 50);
```

---

## 6. 实施路线图 (Phased Roadmap)

总工期 **12-16 周**，按品牌逐个击破。

### Phase 1: 基础设施搭建 (Weeks 1-3)

| 任务 | 交付物 | 验收标准 |
|------|--------|---------|
| 创建 `Acme.PlcComm` 类库项目 | `.csproj` + 项目引用配置 | 编译通过 |
| 实现核心抽象 | `IPlcClient`, `PlcBaseClient`, `OperateResult<T>` | 单元测试覆盖 |
| 实现通用模块 | `IByteTransform` (Big/Little), `PlcConnectionPool` | 字节序转换测试通过 |
| 实现地址模型 | `PlcAddress`, `IAddressParser` 接口 | 接口定义完成 |
| 重构现有算子 | `ModbusCommunicationOperator` 改用 `PlcConnectionPool` | 回归测试通过 |
| 搭建测试环境 | PLCSIM Advanced / GX Simulator / CX-Simulator | 模拟器可连接 |

### Phase 2A: 西门子 S7 (Weeks 4-6)

| 任务 | 交付物 | 验收标准 |
|------|--------|---------|
| 引入 S7NetPlus | NuGet 引用 + 封装层 | 连接 PLCSIM 成功 |
| `SiemensS7Client` | 完整实现 `IPlcClient` | DB 读写、PDU 分包正常 |
| `S7AddressParser` | 解析 DB/M/I/Q 地址 | 50+ 用例单元测试通过 |
| `SiemensS7CommunicationOperator` | 算子 + 前端 UI | 在 ClearVision 中端到端读写 PLC |

### Phase 2B: 三菱 MC (Weeks 7-9)

| 任务 | 交付物 | 验收标准 |
|------|--------|---------|
| `McProtocol` + `McFrameBuilder` | 3E 帧构造与解析 | 帧二进制与抓包一致 |
| `MitsubishiMcClient` | 完整实现 `IPlcClient` | D/M/X/Y 区读写正常 |
| `McAddressParser` | 八进制/十六进制/十进制转换 | X17=15, W1F=31 等用例通过 |
| `MitsubishiMcCommunicationOperator` | 算子 + 前端 UI | 端到端验证 |

### Phase 2C: 欧姆龙 FINS (Weeks 10-12)

| 任务 | 交付物 | 验收标准 |
|------|--------|---------|
| `FinsProtocol` + `FinsHandshake` | FINS/TCP 握手 + 帧构造 | 节点号交换成功 |
| `OmronFinsClient` | 完整实现 `IPlcClient` | DM/CIO 区读写正常 |
| `FinsAddressParser` | 解析 DM/CIO/W/H 地址 | 30+ 用例单元测试通过 |
| `OmronFinsCommunicationOperator` | 算子 + 前端 UI | 端到端验证 |

### Phase 3: 集成测试与优化 (Weeks 13-16)

| 任务 | 交付物 | 验收标准 |
|------|--------|---------|
| 压力测试 | 多线程并发读写报告 | 100 线程 × 1000 次无异常 |
| 断线恢复测试 | 物理断网 → 自动重连日志 | 60 秒内自动恢复 |
| 批量优化 | `AddressOptimizer` 实现 | 合并率 ≥ 50% |
| 文档 | 各品牌 PLC 连接指南 + 常见错误排查 | 覆盖 S7/MC/FINS |
| 发布 | 合并至 `main` 分支 | CI 编译通过 |

---

## 7. 前端算子集成方案

### 7.1 OperatorEnums 新增

```csharp
public enum OperatorType
{
    // ... 现有值 ...
    ModbusCommunication = 27,
    TcpCommunication = 28,
    SerialCommunication = 46,

    // 【新增】PLC 通信算子
    SiemensS7Communication = 50,
    MitsubishiMcCommunication = 51,
    OmronFinsCommunication = 52,
}
```

### 7.2 各算子参数定义

**SiemensS7CommunicationOperator**:
| 参数名 | 类型 | 必填 | 说明 |
|--------|------|------|------|
| `IpAddress` | string | ✅ | PLC IP 地址 |
| `Port` | int | ❌ | 默认 102 |
| `CpuType` | enum | ✅ | S7200/S7300/S71200/S71500 |
| `Rack` | int | ❌ | 默认 0 |
| `Slot` | int | ❌ | 默认 1 |
| `Address` | string | ✅ | 如 `DB1.DBW100` |
| `DataType` | enum | ✅ | Bit/Byte/Word/DWord/Float/String |
| `Operation` | enum | ✅ | Read/Write |
| `WriteValue` | string | 条件 | 写入时的值 |

**MitsubishiMcCommunicationOperator**:
| 参数名 | 类型 | 必填 | 说明 |
|--------|------|------|------|
| `IpAddress` | string | ✅ | PLC IP 地址 |
| `Port` | int | ❌ | 默认 5002 |
| `FrameType` | enum | ❌ | 3E(默认)/4E |
| `Address` | string | ✅ | 如 `D100`, `M200`, `X10` |
| `Length` | int | ❌ | 批量读取点数，默认 1 |
| `Operation` | enum | ✅ | Read/Write |
| `WriteValue` | string | 条件 | 写入时的值 |

**OmronFinsCommunicationOperator**:
| 参数名 | 类型 | 必填 | 说明 |
|--------|------|------|------|
| `IpAddress` | string | ✅ | PLC IP 地址 |
| `Port` | int | ❌ | 默认 9600 |
| `Address` | string | ✅ | 如 `DM100`, `CIO0.01` |
| `Length` | int | ❌ | 批量读取字数，默认 1 |
| `Operation` | enum | ✅ | Read/Write |
| `WriteValue` | string | 条件 | 写入时的值 |

---

## 8. 风险管理

| 风险点 | 等级 | 概率 | 影响 | 应对策略 |
|--------|------|------|------|----------|
| **S7 协议复杂性** | 🔴 高 | 高 | 高 | 基于 S7NetPlus 封装，不从零实现 |
| **无实体 PLC 设备** | 🔴 高 | 中 | 高 | 申请采购/租借 S7-1200 + FX5U + CP1H；优先用模拟器验证逻辑 |
| **X/Y 八进制解析错误** | 🟡 中 | 高 | 中 | 编写 50+ 种地址变体的单元测试，包含边界值 (X7→7, X10→8, X17→15) |
| **并发连接冲突** | 🟡 中 | 中 | 高 | `SemaphoreSlim(1,1)` 严格串行化；连接池隔离不同 PLC 实例 |
| **PDU 分包边界** | 🟡 中 | 中 | 中 | 缓存协商 PDU Size，分包逻辑覆盖测试 (读 1 字 / 读 960 字 / 读 961 字) |
| **FINS 握手节点号丢失** | 🟢 低 | 低 | 高 | 断线重连时强制重新握手；DA1/SA1 存储在 Client 实例中 |
| **字节序混用** | 🟢 低 | 中 | 中 | 基类强制注入 `IByteTransform`，禁止直接使用 `BitConverter` |

---

## 9. 开源资源参考

| 资源 | 用途 | 许可证 | 链接 |
|------|------|--------|------|
| **S7NetPlus** | S7 协议核心 | MIT | github.com/S7NetPlus/s7netplus |
| **Sharp7** | S7 高性能替代 | LGPL | github.com/fbarresi/Sharp7 |
| **NModbus** | Modbus 参考 (已在用) | MIT | github.com/NModbus/NModbus |
| **McProtocol** | MC 协议参考实现 | MIT | github.com/SecondShiftEngineer/McProtocol |
| **FINS.NET** | FINS 协议参考 | MIT | 搜索 NuGet "OmronFins" |
| **Wireshark** | 协议抓包调试 | GPL | wireshark.org (含 S7Comm 解析插件) |

---

## 附录 A: 完整地址格式速查

### S7 (西门子)
| 格式 | 示例 | 含义 |
|------|------|------|
| `DB{n}.DBB{offset}` | `DB1.DBB100` | DB1 第 100 字节 (Byte) |
| `DB{n}.DBW{offset}` | `DB1.DBW100` | DB1 第 100 字 (Word/UInt16) |
| `DB{n}.DBD{offset}` | `DB1.DBD100` | DB1 第 100 双字 (DWord/Float) |
| `DB{n}.DBX{b}.{bit}` | `DB1.DBX10.3` | DB1 第 10 字节第 3 位 (Bool) |
| `M{byte}` | `M100` | Merker 第 100 字节 |
| `M{byte}.{bit}` | `M100.2` | Merker 第 100 字节第 2 位 |
| `I{byte}.{bit}` | `I0.0` | 输入 第 0 字节第 0 位 |
| `Q{byte}.{bit}` | `Q0.0` | 输出 第 0 字节第 0 位 |

### MC (三菱)
| 格式 | 示例 | 含义 | 注意 |
|------|------|------|------|
| `D{n}` | `D100` | 数据寄存器 100 | 十进制 |
| `M{n}` | `M200` | 辅助继电器 200 | 十进制 |
| `X{n}` | `X10` | 输入继电器 8 | ⚠️ **八进制** |
| `Y{n}` | `Y17` | 输出继电器 15 | ⚠️ **八进制** |
| `W{n}` | `W1F` | 链接寄存器 | 十六进制 |
| `B{n}` | `B0A` | 链接继电器 | 十六进制 |

### FINS (欧姆龙)
| 格式 | 示例 | 含义 |
|------|------|------|
| `DM{n}` 或 `D{n}` | `DM100` | 数据存储区 100 (字) |
| `CIO{n}` | `CIO0` | 通道IO 0 (字) |
| `CIO{n}.{bit}` | `CIO0.01` | 通道IO 0 第 1 位 (Bool) |
| `W{n}` | `W10` | Work 区 10 (字) |
| `H{n}` | `H0` | Holding 区 0 (字) |
| `A{n}` | `A448` | Auxiliary 区 448 (系统信息) |

---

## 附录 B: 关键配置检查清单

### 西门子 TIA Portal
- [ ] CPU 属性 → Protection & Security → Access level = "Full access (no protection)"
- [ ] CPU 属性 → Protection & Security → Permit access with PUT/GET = ✅
- [ ] DB 块属性 → Optimized block access = ❌ (S7-1500 必须关闭)
- [ ] 网络设置 → 确认 PLC IP 地址和子网掩码

### 三菱 GX Works
- [ ] 参数 → PLC 参数 → 内置以太网设定 → 确认 IP 和端口
- [ ] 参数 → PLC 参数 → 通讯 → SLMP/MC 协议允许 = ✅
- [ ] 在线 → 写入 PLC → 远程操作设置 → 允许远程读写

### 欧姆龙 CX-Programmer
- [ ] IO 表 → 内置 EtherNet/IP 端口 → 确认 IP
- [ ] 设定 → FINS/TCP 设定 → 端口号 (默认 9600)
- [ ] 设定 → 节点号 → 确认 PLC 节点号

---
