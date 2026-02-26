# 变量写入 / VariableWrite

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `VariableWriteOperator` |
| 枚举值 (Enum) | `OperatorType.VariableWrite` |
| 分类 (Category) | 变量 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蔺苇君 |

## 算法原理 / Algorithm Principle
> 中文：将输入值或静态配置值写入全局变量上下文，并按目标数据类型执行转换。
> English: Writes input or static value into global variable context with type conversion based on configured target data type.

## 实现策略 / Implementation Strategy
> 中文：值选择优先级为 `inputs["Value"]` -> `inputs[VariableName]` -> `StaticValue`（受 `UseInputValue` 控制）。选定值后按 `DataType` 转换为 `long/double/bool/string` 并调用 `SetValue` 写入，最终返回变量名、写入值与循环计数。
> English: Resolves value by precedence (`Value` input, named input, then static fallback), converts by `DataType`, writes through `SetValue`, and returns write telemetry.

## 核心 API 调用链 / Core API Call Chain
- 参数读取：`GetStringParam/GetBoolParam`（`VariableName/DataType/UseInputValue`）
- 值决策：`TryGetValue("Value")` -> `TryGetValue(variableName)` -> `GetStaticValue(...)`
- 类型转换：`Convert.ToInt64/ToDouble/ToBoolean` 或 `ToString()`
- 上下文写入：`_variableContext.SetValue(variableName, typedValue)`
- 输出返回：`OperatorExecutionOutput.Success({ VariableName, Value, CycleCount })`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `VariableName` | `string` | "" | - | 要写入的变量名，不能为空 |
| `DataType` | `enum` | String | - | 写入类型：`String/Int/Double/Bool` |
| `UseInputValue` | `bool` | true | - | 是否优先采用输入端口值 |
| `StaticValue` | `string` | 0 | - | 无输入值时的静态回退值 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Value` | 值 | `Any` | No | 可选输入值 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `VariableName` | 变量名 | `String` | 写入目标变量名 |
| `Value` | 写入的值 | `Any` | 本次写入使用的原始值 |
| `CycleCount` | 循环计数 | `Integer` | 当前上下文循环计数 |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | `O(1)` |
| 典型耗时 (Typical Latency) | 常见 `<0.1 ms` |
| 内存特征 (Memory Profile) | 常量级，主要为一次类型转换与输出字典分配 |

## 适用场景 / Use Cases
- 适合 (Suitable)：流程状态写入、结果标志位落地、跨算子共享变量更新。
- 不适合 (Not Suitable)：需要复杂事务语义、批量变量原子更新或强校验写入链路。

## 已知限制 / Known Limitations
1. 类型转换依赖 `Convert.*`，当输入值不可转换时会抛出异常并导致算子失败。
2. 输出 `Value` 为“写入前选定的原始值”，可能与上下文中的最终强制类型值不完全一致。
3. `StaticValue` 的数值/布尔解析失败会回退到 `0/0.0/false`，需注意配置正确性。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |
| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |
