# 变量读取 / VariableRead

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `VariableReadOperator` |
| 枚举值 (Enum) | `OperatorType.VariableRead` |
| 分类 (Category) | 变量 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蔺苇君 |

## 算法原理 / Algorithm Principle
> 中文：从全局变量上下文读取指定变量，并按声明数据类型进行类型化返回，同时输出变量是否存在与当前循环计数。
> English: Reads a named variable from global context, returns a type-casted value by configured data type, and exposes existence/cycle metadata.

## 实现策略 / Implementation Strategy
> 中文：先检查变量是否存在（`Contains`），再根据 `DataType` 选择对应泛型读取：`long/double/bool/string`。若变量缺失则使用 `DefaultValue`（按目标类型解析后的值）作为回退，保证下游总能得到可用输出。
> English: Checks variable existence first, then performs typed retrieval by `DataType` using generic `GetValue<T>`. Falls back to parsed `DefaultValue` when missing.

## 核心 API 调用链 / Core API Call Chain
- 参数读取：`GetStringParam("VariableName"|"DefaultValue"|"DataType")`
- 存在性检查：`_variableContext.Contains(variableName)`
- 类型化读取：`_variableContext.GetValue<long|double|bool|string>(...)`
- 输出构建：`OperatorExecutionOutput.Success({ Value, VariableName, Exists, CycleCount })`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `VariableName` | `string` | "" | - | 要读取的变量名，不能为空 |
| `DefaultValue` | `string` | 0 | - | 变量不存在时的默认值（会按 `DataType` 解析） |
| `DataType` | `enum` | String | - | 读取类型：`String/Int/Double/Bool` |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| - | - | - | - | 无输入端口 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Value` | 值 | `Any` | 按 `DataType` 解析后的变量值 |
| `Exists` | 是否存在 | `Boolean` | 变量是否存在于上下文 |
| `CycleCount` | 循环计数 | `Integer` | 当前上下文循环计数 |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | `O(1)` |
| 典型耗时 (Typical Latency) | 常见 `<0.1 ms` |
| 内存特征 (Memory Profile) | 常量级，读取后封装少量输出字段 |

## 适用场景 / Use Cases
- 适合 (Suitable)：读取流程状态位、获取计数器当前值、将全局变量回传到判定链路。
- 不适合 (Not Suitable)：需要批量遍历变量集合或强类型 schema 校验的场景。

## 已知限制 / Known Limitations
1. `DefaultValue` 的数值解析失败会静默回退到 `0/0.0/false`，可能掩盖配置错误。
2. `Double` 解析依赖运行时文化设置，跨区域部署需关注小数点格式。
3. 读取结果类型完全由 `DataType` 驱动，若与实际写入类型不一致，可能得到默认值而非原始值。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |
| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |
