# 逻辑门 / LogicGate

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `LogicGateOperator` |
| 枚举值 (Enum) | `OperatorType.LogicGate` |
| 分类 (Category) | 通用 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：对布尔输入执行标准逻辑门运算（AND/OR/NOT/XOR/NAND/NOR）并输出布尔结果。
> English: Applies standard boolean gate operations (AND/OR/NOT/XOR/NAND/NOR) and outputs a boolean result.

## 实现策略 / Implementation Strategy
> 中文：输入支持多类型自动转布尔（字符串、数字、布尔等）；根据 `Operation` 分支执行逻辑表达式；`NOT` 仅使用 `InputA`。
> English: Converts heterogeneous input types to boolean, dispatches operation logic by `Operation`, and uses only `InputA` for `NOT`.

## 核心 API 调用链 / Core API Call Chain
- `ConvertToBool`（多类型归一为布尔）
- `operation switch`（AND/OR/NOT/XOR/NAND/NOR）
- 输出 `Result` 与调试字段 `InputA/InputB/Operation`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Operation` | `enum` | AND | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `InputA` | 输入 A | `Boolean` | Yes | - |
| `InputB` | 输入 B | `Boolean` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Result` | 输出 | `Boolean` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | `O(1)` |
| 典型耗时 (Typical Latency) | 约 `<0.05 ms` |
| 内存特征 (Memory Profile) | 常数级，只有少量字典输出 |

## 适用场景 / Use Cases
- 适合 (Suitable)：多检测结果组合判定、流程开关控制、联锁条件合成。
- 不适合 (Not Suitable)：需要三值逻辑、模糊逻辑或复杂规则表达式的场景。

## 已知限制 / Known Limitations
1. 非布尔输入会被宽松转换，可能导致隐式真值误判。
2. 双输入逻辑在 `InputB` 缺失时默认 `false`，需上游显式连接。
3. 不支持短路副作用控制，仅返回纯结果值。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |