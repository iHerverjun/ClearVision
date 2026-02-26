# 欧姆龙FINS通信 / OmronFinsCommunication

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `OmronFinsCommunicationOperator` |
| 枚举值 (Enum) | `OperatorType.OmronFinsCommunication` |
| 分类 (Category) | 通信 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：使用 FINS/TCP 协议完成欧姆龙 PLC 地址读写，并执行字节与业务类型之间的转换。
> English: Uses Omron FINS/TCP to read/write PLC addresses with byte-to-type conversion.

## 实现策略 / Implementation Strategy
> 中文：通过基类连接池复用连接，按 `Operation` 分流读写逻辑；写入值同样优先取上游输入键；输出统一走基类成功/失败结构。
> English: Reuses pooled connections from base class, routes by `Operation`, resolves dynamic write value from upstream keys, and returns base-format outputs.

## 核心 API 调用链 / Core API Call Chain
- `PlcClientFactory.CreateOmronFins(...)`
- `GetOrCreateConnectionAsync`
- `client.ReadAsync` / `client.WriteAsync`
- `ConvertBytesToValue` / `ConvertValueToBytes`
- `CreateSuccessOutput` / `CreateFailureOutput`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `IpAddress` | `string` | 192.168.250.1 | - | - |
| `Port` | `int` | 9600 | [1, 65535] | - |
| `Address` | `string` | DM100 | - | - |
| `Length` | `int` | 1 | [1, 999] | - |
| `DataType` | `enum` | Word | - | - |
| `Operation` | `enum` | Read | - | - |
| `WriteValue` | `string` | "" | - | - |
| `PollingMode` | `enum` | None | - | 读取时是否启用轮询等待 |
| `PollingCondition` | `enum` | Equal | - | 等待的条件类型 |
| `PollingValue` | `string` | 1 | - | 等待的目标值（如触发信号值） |
| `PollingTimeout` | `int` | 30000 | [100, 300000] | 最长等待时间（毫秒） |
| `PollingInterval` | `int` | 50 | [10, 5000] | 每次读取间隔（毫秒） |

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
| 时间复杂度 (Time Complexity) | 单次读写近似 `O(1)`（网络延迟主导） |
| 典型耗时 (Typical Latency) | 常见 `3-50 ms`（局域网与 PLC 周期相关） |
| 内存特征 (Memory Profile) | 连接池常驻 + 小型数据缓冲 |

## 适用场景 / Use Cases
- 适合 (Suitable)：欧姆龙 PLC 数据采集、配方参数写入、工位状态反馈。
- 不适合 (Not Suitable)：必须使用串口 FINS 或需批量复杂事务控制的场景。

## 已知限制 / Known Limitations
1. 文档中声明了轮询相关参数，但当前执行路径未实际使用轮询逻辑。
2. 地址格式错误或长度配置不当会直接导致读写失败。
3. 不包含应用层事务补偿与多步原子写保护机制。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |