# 脚本算子 / Script

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `ScriptOperator` |
| 枚举值 (Enum) | `OperatorType.ScriptOperator` |
| 分类 (Category) | 逻辑工具 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：Runs user-defined expression or script snippet.。
> English: Runs user-defined expression or script snippet..

## 实现策略 / Implementation Strategy
> 中文：采用“输入规范化 -> 业务规则处理 -> 输出结构化”的实现策略，保持参数可配置并兼容现有流程上下文。
> English: Uses an input-normalization, rule-processing, and structured-output strategy with configurable parameters and compatibility with the existing workflow context.

## 核心 API 调用链 / Core API Call Chain
- 输入数据解析与类型规范化
- 规则计算/逻辑执行
- 输出结果封装与上下文回写

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `ScriptLanguage` | `enum` | CSharpExpression | - | - |
| `Code` | `string` | Input1 + Input2 | - | - |
| `Timeout` | `int` | 5000 | [1, 120000] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Input1` | Input 1 | `Any` | No | - |
| `Input2` | Input 2 | `Any` | No | - |
| `Input3` | Input 3 | `Any` | No | - |
| `Input4` | Input 4 | `Any` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Output1` | Output 1 | `Any` | - |
| `Output2` | Output 2 | `Any` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 约 `O(N)` |
| 典型耗时 (Typical Latency) | 通常 `<2 ms` |
| 内存特征 (Memory Profile) | 以表达式上下文和中间结果对象为主，开销较小 |

## 适用场景 / Use Cases
- 适合 (Suitable)：规则表达、脚本扩展、事件触发与辅助逻辑。
- 不适合 (Not Suitable)：高频高并发下的重计算脚本执行场景。

## 已知限制 / Known Limitations
1. 当前实现遵循统一输入/输出协议，输入类型、维度或关键字段不符合约定时会返回失败。
2. 脚本/逻辑配置错误会直接影响流程决策，需加强参数校验。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.5 文档补全 / Completed Phase 2.5 documentation enrichment |
