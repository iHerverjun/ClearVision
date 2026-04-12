# 数据聚合 / Aggregator

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `AggregatorOperator` |
| 枚举值 (Enum) | `OperatorType.Aggregator` |
| 分类 (Category) | 数据处理 |
| 成熟度 (Maturity) | 稳定 Stable |

## 算法原理 / Algorithm Principle
该算子把多路输入聚合成列表，并在可解析为有限数值的输入上计算统计结果。

## 实现策略 / Implementation Strategy
- `Merge` 模式保留原始输入值并输出聚合列表。
- `Max / Min / Average` 仅对可解析为有限数值的输入生效。
- 当统计模式下 `NumericCount == 0` 时直接失败，避免把异常输入伪装成 `0`。

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Mode` | `enum` | `"Merge"` | `Merge` / `Max` / `Min` / `Average` | 聚合模式。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Value1` | 值1 | `Any` | No | 第一路输入。 |
| `Value2` | 值2 | `Any` | No | 第二路输入。 |
| `Value3` | 值3 | `Any` | No | 第三路输入。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Result` | 结果 | `Any` | 当前模式下的主输出。 |
| `MergedList` | 合并列表 | `Any` | 原始输入聚合后的列表。 |
| `MaxValue` | 最大值 | `Float` | 有限数值输入中的最大值。 |
| `MinValue` | 最小值 | `Float` | 有限数值输入中的最小值。 |
| `Average` | 均值 | `Float` | 有限数值输入中的平均值。 |
| `NumericCount` | 数值数量 | `Integer` | 成功参与统计的有限数值个数。 |

## 已知限制 / Known Limitations
1. 仅内置 3 路输入；更长序列应由上游先整理成集合后再处理。
2. 统计模式只认有限数值，无法解析的输入会被忽略而不会自动转换。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.0 | 2026-04-12 | 新增 `NumericCount` 输出，并收口统计模式下无有效数值时直接失败。 |
