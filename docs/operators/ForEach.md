# ForEach 循环 / ForEach

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `ForEachOperator` |
| 枚举值 (Enum) | `OperatorType.ForEach` |
| 分类 (Category) | 流程控制 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：将输入集合逐项喂给子图执行，并聚合每项结果，支持并行与串行两种执行策略。
> English: Iterates over input items, executes a sub-flow per item, and aggregates outputs with parallel or sequential scheduling.

## 实现策略 / Implementation Strategy
> 中文：`Parallel` 模式使用 `Parallel.ForEachAsync` 提升吞吐；`Sequential` 模式用于含 I/O 子图保证顺序与资源安全；支持 `FailFast` 失败即停和超时控制。
> English: Uses `Parallel.ForEachAsync` in parallel mode for throughput, sequential mode for I/O safety, with optional fail-fast cancellation and timeout control.

## 核心 API 调用链 / Core API Call Chain
- `ParseItems`（将 `Items` 解析为可迭代对象）
- `GetSubGraph`（属性或参数反序列化）
- `ExecuteParallelAsync` / `ExecuteSequentialAsync`
- `IFlowExecutionService.ExecuteFlowAsync`（执行子图）
- `BuildAggregateResult`（统计结果、通过数、成功数）

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `IoMode` | `enum` | Parallel | - | - |
| `MaxParallelism` | `int` | 8 | [1, 64] | - |
| `Timeout` | `int` | 30000 | - | - |
| `FailFast` | `bool` | true | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Items` | 集合 | `Any` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Results` | 结果列表 | `Any` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 约 `O(N * C_subgraph)`；并行模式理论可降至 `O(N/P)` |
| 典型耗时 (Typical Latency) | 与子图复杂度强相关；并行模式吞吐提升明显但受资源上限约束 |
| 内存特征 (Memory Profile) | 结果集合 + 并发任务上下文，约 `O(N)` |

## 适用场景 / Use Cases
- 适合 (Suitable)：批量检测结果逐项处理、逐缺陷上报、列表数据并行变换。
- 不适合 (Not Suitable)：子图存在强共享状态且不可并发，或未配置子图定义的场景。

## 已知限制 / Known Limitations
1. 必须提供有效 `SubGraph`，否则算子直接失败。
2. 聚合逻辑默认读取子图输出键 `Result`，子图输出契约不一致会影响统计准确性。
3. 代码中使用 `TimeoutMs/OrderResults` 等键，而参数元数据主要暴露 `Timeout`，配置需对齐实现。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |