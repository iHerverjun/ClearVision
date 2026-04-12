# 结果判定 / ResultJudgment

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `ResultJudgmentOperator` |
| 枚举值 (Enum) | `OperatorType.ResultJudgment` |
| 分类 (Category) | 流程控制 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。

> English: This section is completed from the current source implementation and focuses on actual runtime behavior in code.

## 实现策略 / Implementation Strategy
- 实现遵循统一算子框架：参数读取、输入检查、核心处理与结果封装相互分离。

## 核心 API 调用链 / Core API Call Chain
1. `GetStringParam / GetIntParam / GetDoubleParam / GetBoolParam / GetFloatParam`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `FieldName` | `string` | `"Value"` | - | 要从上游输入中读取的字段名，如 DefectCount, Distance |
| `Condition` | `enum` | `"Equal"` | Equal/等于 (Equal)；GreaterThan/大于 (GreaterThan)；LessThan/小于 (LessThan)；GreaterOrEqual/大于等于 (GreaterOrEqual)；LessOrEqual/小于等于 (LessOrEqual)；Range/范围内 (Range) | 该参数用于在多个实现分支之间切换。 |
| `ExpectValue` | `string` | `"4"` | - | 判定目标值，如 4（螺钉数）、0（缺陷数）、9.5（尺寸下限） |
| `ExpectValueMin` | `string` | `""` | - | 用于Range条件，设置范围下限 |
| `ExpectValueMax` | `string` | `""` | - | 用于Range条件，设置范围上限 |
| `MinConfidence` | `double` | `0.0` | [0.0, 1.0] | 置信度低于此值时判定为NG（0表示不检查置信度） |
| `OkOutputValue` | `string` | `"1"` | - | 判定为OK时输出的值（用于PLC写入） |
| `NgOutputValue` | `string` | `"0"` | - | 判定为NG时输出的值（用于PLC写入） |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Value` | 输入值 | `Any` | Yes | 提供流程数据输入。 |
| `Confidence` | 置信度 | `Float` | No | 提供流程数据输入。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `IsOk` | 是否OK | `Boolean` | 输出流程数据结果。 |
| `JudgmentValue` | 判定值 | `String` | 输出流程数据结果。 |
| `Details` | 详细信息 | `String` | 输出流程数据结果。 |
## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 通常为 `O(1)` 或与输入集合长度线性相关。 |
| 典型耗时 (Typical Latency) | 仓库中未提供固定 benchmark；实际延迟受图像尺寸、参数规模、缓存命中率和外部依赖影响。 |
| 内存特征 (Memory Profile) | 主要由中间结果、缓存结构和输出封装决定。 |

## 适用场景 / Use Cases
- 适合做变量整理、条件判断、结果汇总和类型转换。
- 适合把上游复杂输出整理为下游更容易消费的结构。
- 不适合作为图像算法替代品。
- 不适合承载大量高频大对象搬运。

## 已知限制 / Known Limitations


## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.2 | 2026-03-14 | 第二轮基于源码深化实现行为、性能与限制说明 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |
