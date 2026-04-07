# 双模态投票 / DualModalVoting

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `DualModalVotingOperator` |
| 枚举值 (Enum) | `OperatorType.DualModalVoting` |
| 分类 (Category) | AI检测 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：结合深度学习和传统算法结果进行投票决策。
> English: 结合深度学习和传统算法结果进行投票决策.

## 实现策略 / Implementation Strategy
> 中文：TODO：补充实现策略与方案对比。
> English: TODO: Add implementation strategy and alternatives comparison.

## 核心 API 调用链 / Core API Call Chain
- TODO：补充关键 API 调用链

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `VotingStrategy` | `enum` | WeightedAverage | - | - |
| `DLWeight` | `double` | 0.6 | [0, 1] | - |
| `TraditionalWeight` | `double` | 0.4 | [0, 1] | - |
| `ConfidenceThreshold` | `double` | 0.5 | [0, 1] | - |
| `OkOutputValue` | `string` | 1 | - | - |
| `NgOutputValue` | `string` | 0 | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `DLResult` | 深度学习结果 | `Any` | Yes | - |
| `TraditionalResult` | 传统算法结果 | `Any` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `IsOk` | 是否OK | `Boolean` | - |
| `Confidence` | 综合置信度 | `Float` | - |
| `JudgmentValue` | 判定值 | `String` | - |

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
| 1.0.0 | 2026-04-07 | 自动生成文档骨架 / Generated skeleton |
