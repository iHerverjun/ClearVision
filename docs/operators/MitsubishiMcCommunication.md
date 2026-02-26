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
> 中文：三菱MC协议PLC读写通信（FX5U/Q/iQ-R/iQ-F）。
> English: 三菱MC协议PLC读写通信（FX5U/Q/iQ-R/iQ-F）.

## 实现策略 / Implementation Strategy
> 中文：TODO：补充实现策略与方案对比。
> English: TODO: Add implementation strategy and alternatives comparison.

## 核心 API 调用链 / Core API Call Chain
- TODO：补充关键 API 调用链

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
| 时间复杂度 (Time Complexity) | O(?) |
| 典型耗时 (Typical Latency) | ~?ms (1920x1080) |
| 内存特征 (Memory Profile) | ? |

## 适用场景 / Use Cases
- 适合 (Suitable)：TODO
- 不适合 (Not Suitable)：TODO

## 已知限制 / Known Limitations
1. TODO

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |
