# 三菱MC通信 / MitsubishiMcCommunication

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `MitsubishiMcCommunicationOperator` |
| 枚举值 (Enum) | `OperatorType.MitsubishiMcCommunication` |
| 分类 (Category) | 通信 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：基于三菱 MC 协议执行 PLC 地址读写，支持多数据类型转换，并可轮询等待目标值触发。
> English: Performs Mitsubishi MC protocol read/write with typed conversion and optional polling-until-target condition.

## 实现策略 / Implementation Strategy
> 中文：通过基类连接池复用 MC 连接；写入值优先取上游输入（`JudgmentValue/Value/Data`）；读取时可进入轮询循环，按条件判定结束。
> English: Reuses pooled MC connections from base class, resolves write value from prioritized upstream keys, and optionally enters conditional polling loop for reads.

## 核心 API 调用链 / Core API Call Chain
- `PlcClientFactory.CreateMitsubishiMc(...)`
- `GetOrCreateConnectionAsync`
- `client.ReadAsync` / `client.WriteAsync`
- `ExecuteReadWithPollingAsync` + `EvaluatePollingCondition`
- 类型转换：`ConvertBytesToValue` / `ConvertValueToBytes`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `IpAddress` | `string` | 192.168.3.1 | - | - |
| `Port` | `int` | 5002 | [1, 65535] | - |
| `Address` | `string` | D100 | - | - |
| `Length` | `int` | 1 | [1, 960] | - |
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
| 典型耗时 (Typical Latency) | 单次读写 `3-40 ms`；轮询由超时和目标条件决定 |
| 内存特征 (Memory Profile) | 连接池对象 + 小型字节数组转换缓冲 |

## 适用场景 / Use Cases
- 适合 (Suitable)：三菱 PLC 触发信号等待、状态同步、参数下载与回读验证。
- 不适合 (Not Suitable)：不支持 MC 协议或需要跨站点高并发广播写入的场景。

## 已知限制 / Known Limitations
1. 轮询机制为主动查询，配置不当会导致 PLC 访问频率过高。
2. 地址语法与数据类型必须严格匹配 PLC 侧定义，否则读写失败。
3. 异常处理以算子级失败返回为主，缺少细粒度错误码映射。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |