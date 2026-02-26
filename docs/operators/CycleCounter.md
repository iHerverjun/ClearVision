# 循环计数器 / CycleCounter

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `CycleCounterOperator` |
| 枚举值 (Enum) | `OperatorType.CycleCounter` |
| 分类 (Category) | 变量 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：获取当前循环次数和统计信息。
> English: 获取当前循环次数和统计信息.

## 实现策略 / Implementation Strategy
> 中文：TODO：补充实现策略与方案对比。
> English: TODO: Add implementation strategy and alternatives comparison.

## 核心 API 调用链 / Core API Call Chain
- TODO：补充关键 API 调用链

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Action` | `enum` | Read | - | 读取/重置/递增 |
| `MaxCycles` | `int` | 0 | - | 0表示无限制 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| - | - | - | - | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `CycleCount` | 当前次数 | `Integer` | - |
| `MaxCycles` | 最大次数 | `Integer` | - |
| `IsLimitReached` | 是否达到限制 | `Boolean` | - |
| `RemainingCycles` | 剩余次数 | `Integer` | - |
| `Progress` | 进度(%) | `Float` | - |

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
