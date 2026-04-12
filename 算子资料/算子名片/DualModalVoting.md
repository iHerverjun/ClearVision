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
该算子结合学习型模型或规则判定完成识别、检测或缺陷筛查。

> English: This section is completed from the current source implementation and focuses on actual runtime behavior in code.

## 实现策略 / Implementation Strategy
- 实现遵循统一算子框架：参数读取、输入检查、核心处理与结果封装相互分离。

## 核心 API 调用链 / Core API Call Chain
1. `GetStringParam / GetIntParam / GetDoubleParam / GetBoolParam / GetFloatParam`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `VotingStrategy` | `enum` | `"WeightedAverage"` | WeightedAverage/加权平均；Unanimous/一致同意；Majority/多数表决；PrioritizeDeepLearning/优先深度学习；PrioritizeTraditional/优先传统算法 | 该参数用于在多个实现分支之间切换。 |
| `DLWeight` | `double` | `0.6` | [0.0, 1.0] | 控制“DLWeight”这一实现参数，建议结合现场样本调节。 |
| `TraditionalWeight` | `double` | `0.4` | [0.0, 1.0] | 控制“TraditionalWeight”这一实现参数，建议结合现场样本调节。 |
| `ConfidenceThreshold` | `double` | `0.5` | [0.0, 1.0] | 用于判定、分割或筛选的阈值。 |
| `OkOutputValue` | `string` | `"1"` | - | 控制“OkOutputValue”这一实现参数，建议结合现场样本调节。 |
| `NgOutputValue` | `string` | `"0"` | - | 控制“NgOutputValue”这一实现参数，建议结合现场样本调节。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `DLResult` | 深度学习结果 | `Any` | Yes | 提供算法执行所需输入。 |
| `TraditionalResult` | 传统算法结果 | `Any` | Yes | 提供算法执行所需输入。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `IsOk` | 是否OK | `Boolean` | 输出本算子的处理结果。 |
| `Confidence` | 综合置信度 | `Float` | 输出本算子的处理结果。 |
| `JudgmentValue` | 判定值 | `String` | 输出本算子的处理结果。 |
## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 主要由模型推理复杂度主导，预处理和后处理成本次之。 |
| 典型耗时 (Typical Latency) | 仓库中未提供固定 benchmark；实际延迟受图像尺寸、参数规模、缓存命中率和外部依赖影响。 |
| 内存特征 (Memory Profile) | 主要由中间结果、缓存结构和输出封装决定。 |

## 适用场景 / Use Cases
- 适合识别、检测、分类和缺陷筛查任务。
- 适合在规则算法不足时作为增强手段。
- 不适合在模型和标签配置不清晰时直接上线。
- 不适合把推理结果当作无条件真值。

## 已知限制 / Known Limitations


## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.2 | 2026-03-14 | 第二轮基于源码深化实现行为、性能与限制说明 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |
