# 条件分支 / ConditionalBranch

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `ConditionalBranchOperator` |
| 枚举值 (Enum) | `OperatorType.ConditionalBranch` |
| 分类 (Category) | 流程控制 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：根据输入值与比较值的关系判断条件是否成立，并将原始数据路由到 `True` 或 `False` 分支。
> English: Evaluates a condition between input and compare value, then routes payload to `True` or `False` output branch.

## 实现策略 / Implementation Strategy
> 中文：支持从字典输入中按 `FieldName` 抽取字段；优先按数值比较，无法数值化时退化为字符串比较；分支输出会保留输入对象（图像会增加引用计数）。
> English: Optionally extracts a field from dictionary input, prefers numeric comparison, falls back to string comparison, and preserves payload references when routing.

## 核心 API 调用链 / Core API Call Chain
- 输入解析：`Value` + 可选 `FieldName`
- 条件计算：`EvaluateCondition`（`GreaterThan/LessThan/Equal/...`）
- 输出路由：`True` / `False`
- 图像透传：`ImageWrapper.AddRef()`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Condition` | `enum` | GreaterThan | - | - |
| `CompareValue` | `string` | 0 | - | - |
| `FieldName` | `string` | "" | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Value` | 判断值 | `Any` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `True` | True分支 | `Any` | - |
| `False` | False分支 | `Any` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | `O(1)`（字符串条件如 `Contains` 为 `O(L)`） |
| 典型耗时 (Typical Latency) | 约 `<0.1 ms` |
| 内存特征 (Memory Profile) | 仅构建少量输出字典，内存开销低 |

## 适用场景 / Use Cases
- 适合 (Suitable)：OK/NG 分流、阈值路由、基于字段值的流程分支控制。
- 不适合 (Not Suitable)：需要复杂表达式组合或多条件树优化的高级规则引擎场景。

## 已知限制 / Known Limitations
1. 当前仅支持简单单条件判断，不支持条件组合与括号优先级。
2. 参数选项展示与实际支持条件（如 `StartsWith/EndsWith`）存在不完全一致。
3. 真实分支执行依赖流程引擎路由能力，算子本身只负责判定与标记输出。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |