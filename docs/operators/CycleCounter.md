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
> 中文：采用“输入规范化 -> 业务规则处理 -> 输出结构化”的实现策略，保持参数可配置并兼容现有流程上下文。
> English: Uses an input-normalization, rule-processing, and structured-output strategy with configurable parameters and compatibility with the existing workflow context.

## 核心 API 调用链 / Core API Call Chain
- 输入数据解析与类型规范化
- 规则计算/逻辑执行
- 输出结果封装与上下文回写

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
| 时间复杂度 (Time Complexity) | 约 `O(1)` |
| 典型耗时 (Typical Latency) | 通常 `<1 ms` |
| 内存特征 (Memory Profile) | 仅维护少量键值与状态缓存，额外开销很小 |

## 适用场景 / Use Cases
- 适合 (Suitable)：流程中间状态存取、计数、增量控制。
- 不适合 (Not Suitable)：大规模数据缓存或需要持久化事务能力的需求。

## 已知限制 / Known Limitations
1. 当前实现遵循统一输入/输出协议，输入类型、维度或关键字段不符合约定时会返回失败。
2. 变量作用域与生命周期依赖流程上下文，不当复用会产生脏状态。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.5 文档补全 / Completed Phase 2.5 documentation enrichment |
