# 西门子S7通信 / SiemensS7Communication

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `SiemensS7CommunicationOperator` |
| 枚举值 (Enum) | `OperatorType.SiemensS7Communication` |
| 分类 (Category) | 通信 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：通过 S7 协议与西门子 PLC 进行地址读写，支持类型转换与轮询等待条件（用于触发信号同步）。
> English: Communicates with Siemens PLC via S7 protocol, supports typed read/write and polling-until-condition workflows.

## 实现策略 / Implementation Strategy
> 中文：继承 `PlcCommunicationOperatorBase` 复用连接池与心跳巡检；写入值优先读取上游输入；读取模式可启用 `WaitForValue` 轮询直到条件满足或超时。
> English: Reuses base-class connection pooling and heartbeat checks; write values are resolved from upstream inputs first; read mode can poll until condition matches or timeout.

## 核心 API 调用链 / Core API Call Chain
- `PlcClientFactory.CreateSiemensS7(...)`
- `GetOrCreateConnectionAsync`（连接复用）
- `client.ReadAsync` / `client.WriteAsync`
- `ConvertBytesToValue` / `ConvertValueToBytes`
- 轮询：`EvaluatePollingCondition` + `Task.Delay(interval)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `IpAddress` | `string` | 192.168.0.1 | - | - |
| `Port` | `int` | 102 | [1, 65535] | - |
| `CpuType` | `enum` | S71200 | - | - |
| `Rack` | `int` | 0 | [0, 15] | - |
| `Slot` | `int` | 1 | [0, 15] | - |
| `Address` | `string` | DB1.DBW100 | - | - |
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
| 时间复杂度 (Time Complexity) | 单次读写近似 `O(1)`；轮询模式约 `O(T/Interval)` |
| 典型耗时 (Typical Latency) | 单次读写 `3-40 ms`；轮询取决于超时与匹配时刻 |
| 内存特征 (Memory Profile) | 连接池常驻 + 少量类型转换缓冲 |

## 适用场景 / Use Cases
- 适合 (Suitable)：产线触发位握手、配方参数读写、S7 系列 PLC 状态采集。
- 不适合 (Not Suitable)：对毫秒级硬实时有严格确定性要求的闭环控制。

## 已知限制 / Known Limitations
1. 轮询模式仅在 `Read` 分支启用，写入不支持等待反馈条件。
2. 轮询期间频繁读 PLC 可能增加站点负载，需合理设置 `PollingInterval`。
3. 文档端口命名与运行输出键（`Response/Status` vs `Value/DataType`）存在语义差异。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |