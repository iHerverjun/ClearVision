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
> 中文：通用判定逻辑（数量/范围/阈值），输出OK/NG结果。
> English: 通用判定逻辑（数量/范围/阈值），输出OK/NG结果.

## 实现策略 / Implementation Strategy
> 中文：TODO：补充实现策略与方案对比。
> English: TODO: Add implementation strategy and alternatives comparison.

## 核心 API 调用链 / Core API Call Chain
- TODO：补充关键 API 调用链

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `FieldName` | `string` | Value | - | 要从上游输入中读取的字段名，如 DefectCount, Distance |
| `Condition` | `enum` | Equal | - | - |
| `ExpectValue` | `string` | 4 | - | 判定目标值，如 4（螺钉数）、0（缺陷数）、9.5（尺寸下限） |
| `ExpectValueMin` | `string` | "" | - | 用于Range条件，设置范围下限 |
| `ExpectValueMax` | `string` | "" | - | 用于Range条件，设置范围上限 |
| `MinConfidence` | `double` | 0 | [0, 1] | 置信度低于此值时判定为NG（0表示不检查置信度） |
| `OkOutputValue` | `string` | 1 | - | 判定为OK时输出的值（用于PLC写入） |
| `NgOutputValue` | `string` | 0 | - | 判定为NG时输出的值（用于PLC写入） |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Value` | 输入值 | `Any` | Yes | - |
| `Confidence` | 置信度 | `Float` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `IsOk` | 是否OK | `Boolean` | - |
| `JudgmentValue` | 判定值 | `String` | - |
| `Details` | 详细信息 | `String` | - |

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
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |
