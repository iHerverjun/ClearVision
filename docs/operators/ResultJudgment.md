# 结果判定 / ResultJudgment

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `ResultJudgmentOperator` |
| 枚举值 (Enum) | `OperatorType.ResultJudgment` |
| 分类 (Category) | 流程控制 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蔺苇君 |

## 算法原理 / Algorithm Principle
> 中文：对输入字段执行规则判定（相等、大小比较、范围、字符串匹配等），并输出 `IsOk` 与映射后的 `JudgmentValue`（OK/NG）。
> English: Applies configurable rule-based judgment (equality, numeric compare, range, string matching), then outputs `IsOk` and mapped `JudgmentValue`.

## 实现策略 / Implementation Strategy
> 中文：优先按 `FieldName` 从输入字典取值，找不到时回退 `Value`；支持可选置信度门限（`Confidence < MinConfidence` 直接判 NG）。判定函数优先做数值比较，失败后做字符串比较；最终统一返回成功输出对象（`IsOk=true/false`），而非异常中断流程。
> English: Resolves actual value by `FieldName` with fallback to `Value`, optionally gates by confidence threshold, evaluates numeric-first then string logic, and always returns a structured success output with `IsOk` status.

## 核心 API 调用链 / Core API Call Chain
- 参数读取：`GetStringParam/GetDoubleParam`（`Condition/ExpectValue/MinConfidence/...`）
- 输入解析：`inputs[fieldName]` -> fallback `inputs["Value"]`
- 置信度门限：读取 `inputs["Confidence"]`（仅 `double` 类型）
- 条件判定：`EvaluateCondition(...)`
- NG 构建：`CreateNgOutput(...)`
- 输出返回：`OperatorExecutionOutput.Success({ IsOk, JudgmentValue, Details, ... })`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `FieldName` | `string` | Value | - | 要判定的输入字段名；找不到时回退使用 `Value` |
| `Condition` | `enum` | Equal | - | 判定条件：`Equal/GreaterThan/LessThan/GreaterOrEqual/LessOrEqual/Range/Contains/StartsWith/EndsWith` |
| `ExpectValue` | `string` | 4 | - | 判定目标值 |
| `ExpectValueMin` | `string` | "" | - | `Range` 下限 |
| `ExpectValueMax` | `string` | "" | - | `Range` 上限 |
| `MinConfidence` | `double` | 0 | [0, 1] | 最小置信度阈值，低于该值直接判 NG |
| `OkOutputValue` | `string` | 1 | - | 判定为 OK 时输出值 |
| `NgOutputValue` | `string` | 0 | - | 判定为 NG 时输出值 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Value` | 输入值 | `Any` | Yes | 判定输入主值 |
| `Confidence` | 置信度 | `Float` | No | 可选置信度输入 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `IsOk` | 是否OK | `Boolean` | 判定布尔结果 |
| `JudgmentValue` | 判定值 | `String` | OK/NG 映射值（用于下游写 PLC/流程分支） |
| `Details` | 详细信息 | `String` | 判定解释文本 |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 数值比较约 `O(1)`；字符串匹配（`Contains/StartsWith/EndsWith`）约 `O(L)` |
| 典型耗时 (Typical Latency) | 常见 `<0.2 ms`（不含上游推理耗时） |
| 内存特征 (Memory Profile) | 常量级，主要为少量字符串与输出字典分配 |

## 适用场景 / Use Cases
- 适合 (Suitable)：缺陷数量阈值判断、尺寸上下限判定、结果转 PLC OK/NG 信号。
- 不适合 (Not Suitable)：复杂多条件布尔表达式引擎、统计学判别或模型级推断。

## 已知限制 / Known Limitations
1. 置信度门限仅在 `Confidence` 输入为 `double` 时生效，`float/string` 不会触发该分支。
2. 字符串比较默认区分大小写，未提供文化/大小写无关选项。
3. 判定为 NG 时仍返回 `Success`（通过 `IsOk=false` 表示失败语义），调用方需按业务语义处理。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |
| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |
