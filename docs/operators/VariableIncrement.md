# 变量递增 / VariableIncrement

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `VariableIncrementOperator` |
| 枚举值 (Enum) | `OperatorType.VariableIncrement` |
| 分类 (Category) | 变量 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蔺苇君 |

## 算法原理 / Algorithm Principle
> 中文：从全局变量上下文读取计数值，按 `Delta` 做递增/递减，并可在满足条件时执行重置路径。
> English: Reads a global variable counter, applies increment/decrement by `Delta`, and supports conditional reset logic.

## 实现策略 / Implementation Strategy
> 中文：先读取当前值 `currentValue`，再根据 `ResetCondition + ResetThreshold` 判定是否走重置分支。正常分支调用 `IVariableContext.Increment` 原子更新；重置分支计算 `newValue = ResetValue + Delta` 并返回结果字段，附带 `CycleCount` 便于流程观测。
> English: Fetches current value, evaluates reset condition against threshold, uses `IVariableContext.Increment` on normal path, and computes `newValue = ResetValue + Delta` on reset path while returning detailed telemetry.

## 核心 API 调用链 / Core API Call Chain
- 参数读取：`GetStringParam/GetIntParam`（`VariableName/Delta/ResetCondition/...`）
- 当前值读取：`_variableContext.GetValue<long>(variableName, 0L)`
- 条件判定：`switch (resetCondition.ToLower())`
- 正常更新：`_variableContext.Increment(variableName, delta)`
- 结果输出：`OperatorExecutionOutput.Success({ PreviousValue, NewValue, WasReset, CycleCount })`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `VariableName` | `string` | counter | - | 计数变量名，不能为空 |
| `Delta` | `int` | 1 | - | 每次增量（可为负数实现递减） |
| `ResetCondition` | `enum` | None | - | 重置条件：`None/GreaterThan/LessThan/Equal` |
| `ResetThreshold` | `int` | 100 | - | 重置比较阈值 |
| `ResetValue` | `int` | 0 | - | 重置基准值 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| - | - | - | - | 无输入端口 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `VariableName` | 变量名 | `String` | 变量标识 |
| `PreviousValue` | 前值 | `Integer` | 更新前值 |
| `NewValue` | 新值 | `Integer` | 更新后计算值 |
| `Delta` | 增量 | `Integer` | 本次增量 |
| `WasReset` | 是否已重置 | `Boolean` | 是否命中重置条件 |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | `O(1)` |
| 典型耗时 (Typical Latency) | 常见 `<0.1 ms`（不含外部上下文同步开销） |
| 内存特征 (Memory Profile) | 常量级，仅读写少量标量与输出字典 |

## 适用场景 / Use Cases
- 适合 (Suitable)：循环计数、批次序号生成、按阈值回卷的节拍计数。
- 不适合 (Not Suitable)：需要事务级一致性计数或多变量联动计算的复杂状态机。

## 已知限制 / Known Limitations
1. 当前重置分支仅计算并返回 `NewValue`，未显式写回 `_variableContext.SetValue`，可能导致上下文值与输出不一致。
2. 变量读写类型固定按 `long` 处理，不支持小数累加。
3. 重置判定基于“更新前值”进行比较，业务若期望“更新后判定”需额外处理。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |
| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |
