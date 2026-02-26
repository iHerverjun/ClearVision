# 数值比较 / Comparator

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `ComparatorOperator` |
| 枚举值 (Enum) | `OperatorType.Comparator` |
| 分类 (Category) | 流程控制 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：比较两个数值的大小关系，输出布尔判定结果与差值。
> English: 比较两个数值的大小关系，输出布尔判定结果与差值.

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
| `Condition` | `enum` | GreaterThan | - | - |
| `CompareValue` | `double` | 0 | - | 当 ValueB 未连线时使用此值 |
| `Tolerance` | `double` | 0.0001 | >= 0 | 等于/不等于判断的容差 |
| `RangeMin` | `double` | 0 | - | InRange 模式的下限 |
| `RangeMax` | `double` | 1 | - | InRange 模式的上限 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `ValueA` | 数值 A | `Float` | Yes | - |
| `ValueB` | 数值 B | `Float` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Result` | 判定结果 | `Boolean` | - |
| `Difference` | 差值 | `Float` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 约 `O(N)`（N 为分支/集合元素数量） |
| 典型耗时 (Typical Latency) | 通常 `<1 ms`（不含下游算子执行） |
| 内存特征 (Memory Profile) | 主要为临时上下文与分支状态缓存，额外开销较小 |

## 适用场景 / Use Cases
- 适合 (Suitable)：流程编排、异常兜底、条件路由与批处理控制。
- 不适合 (Not Suitable)：需要复杂事务一致性或跨进程编排的场景。

## 已知限制 / Known Limitations
1. 当前实现遵循统一输入/输出协议，输入类型、维度或关键字段不符合约定时会返回失败。
2. 流程逻辑复杂时可读性下降，建议配合注释与命名规范。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.5 文档补全 / Completed Phase 2.5 documentation enrichment |
