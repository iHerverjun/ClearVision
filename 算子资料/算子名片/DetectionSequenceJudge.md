# Detection Sequence Judge / DetectionSequenceJudge

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `DetectionSequenceJudgeOperator` |
| 枚举值 (Enum) | `OperatorType.DetectionSequenceJudge` |
| 分类 (Category) | AI Inspection |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：Sorts detections and compares the resulting label order against an expected sequence.。
> English: Sorts detections and compares the resulting label order against an expected sequence..

## 实现策略 / Implementation Strategy
> 中文：TODO：补充实现策略与方案对比。
> English: TODO: Add implementation strategy and alternatives comparison.

## 核心 API 调用链 / Core API Call Chain
- TODO：补充关键 API 调用链

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `ExpectedLabels` | `string` | "" | - | Comma-separated expected labels in order. |
| `SortBy` | `enum` | CenterX | - | Field used to sort detections before judging the sequence. |
| `Direction` | `enum` | Ascending | - | Ordering direction after sorting. |
| `ExpectedCount` | `int` | 0 | [0, 256] | Expected detection count. Use 0 to derive from ExpectedLabels. |
| `MinConfidence` | `double` | 0 | [0, 1] | Ignore detections below this confidence before sequence judgment. |
| `AllowMissing` | `bool` | false | - | Whether missing expected labels should still be treated as a match. |
| `AllowDuplicate` | `bool` | false | - | Whether duplicate labels should still be treated as a match. |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Detections` | Detections | `DetectionList` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `IsMatch` | Is Match | `Boolean` | - |
| `ActualOrder` | Actual Order | `Any` | - |
| `Count` | Count | `Integer` | - |
| `MissingLabels` | Missing Labels | `Any` | - |
| `DuplicateLabels` | Duplicate Labels | `Any` | - |
| `SortedDetections` | Sorted Detections | `DetectionList` | - |
| `Diagnostics` | Diagnostics | `Any` | - |
| `Message` | Message | `String` | - |

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
