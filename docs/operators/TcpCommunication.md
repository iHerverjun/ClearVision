# TCP通信 / TcpCommunication

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `TcpCommunicationOperator` |
| 枚举值 (Enum) | `OperatorType.TcpCommunication` |
| 分类 (Category) | 通信 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：通过 TCP Socket 与远端设备进行请求-响应通信，支持字符编码转换与连接复用。
> English: Implements TCP request-response communication with encoding support and pooled client connections.

## 实现策略 / Implementation Strategy
> 中文：客户端模式下按 `ip:port` 维护连接池并做活性检查；发送后同步读取响应；服务器模式在当前版本仅返回说明信息，不执行监听。
> English: Client mode reuses pooled connections keyed by `ip:port` with liveness checks, writes then reads response; server mode is currently a non-executable placeholder.

## 核心 API 调用链 / Core API Call Chain
- `TcpClient.ConnectAsync` + `GetStream`
- `NetworkStream.WriteAsync/FlushAsync/ReadAsync`
- 连接池：`ConcurrentDictionary` + `SemaphoreSlim`
- 编码：`UTF8/ASCII/GBK`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Mode` | `enum` | Client | - | - |
| `IpAddress` | `string` | 127.0.0.1 | - | - |
| `Port` | `int` | 8080 | [1, 65535] | - |
| `SendData` | `string` | "" | - | - |
| `Timeout` | `int` | 5000 | [100, 30000] | - |
| `Encoding` | `enum` | UTF8 | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Data` | 数据 | `Any` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Response` | 响应 | `String` | - |
| `Status` | 状态 | `Boolean` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 网络 I/O 主导，约 `O(P)`（`P` 为收发字节数） |
| 典型耗时 (Typical Latency) | 局域网常见 `1-30 ms`，取决于设备响应与超时配置 |
| 内存特征 (Memory Profile) | 连接池对象 + 单次读写缓冲（默认 4096 字节） |

## 适用场景 / Use Cases
- 适合 (Suitable)：与治具/控制器进行文本协议通信、轻量指令交互。
- 不适合 (Not Suitable)：需要完整 TCP 服务器监听与多客户端会话管理的场景。

## 已知限制 / Known Limitations
1. `Server` 模式尚未实现真实监听处理流程。
2. 读取逻辑单次 `ReadAsync`，大报文或分包场景需上层协议补齐。
3. 参数定义与验证存在 `IpAddress/Host` 命名差异，集成时需统一。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |