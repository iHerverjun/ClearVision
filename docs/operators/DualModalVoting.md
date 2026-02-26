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
> 中文：采用“输入校验 -> 参数解析 -> 核心算法执行 -> 结果封装”的统一链路，优先复用 OpenCV 与现有算子基类能力，确保与主项目运行时行为一致。
> English: Uses a consistent pipeline of input validation, parameter parsing, core algorithm execution, and result packaging, reusing OpenCV and existing operator infrastructure for runtime consistency.

## 核心 API 调用链 / Core API Call Chain
- 输入图像与参数校验
- 核心视觉处理链路执行
- 结果图像/结构化结果输出

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
| 时间复杂度 (Time Complexity) | 前后处理约 `O(W*H + N)`；端到端取决于模型推理复杂度 |
| 典型耗时 (Typical Latency) | 前后处理约 `1-10 ms`；总耗时取决于模型与硬件 |
| 内存特征 (Memory Profile) | 包含输入张量、候选框列表与可视化缓存，峰值随模型而变 |

## 适用场景 / Use Cases
- 适合 (Suitable)：缺陷检测、目标检测、复杂外观识别等语义任务。
- 不适合 (Not Suitable)：无模型/无标注数据且要求可解释几何规则的纯规则任务。

## 已知限制 / Known Limitations
1. 当前实现遵循统一输入/输出协议，输入类型、维度或关键字段不符合约定时会返回失败。
2. 模型泛化能力受训练数据分布影响，换线体通常需要再标定。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.5 文档补全 / Completed Phase 2.5 documentation enrichment |
