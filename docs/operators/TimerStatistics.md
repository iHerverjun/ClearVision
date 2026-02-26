# 计时统计 / TimerStatistics

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `TimerStatisticsOperator` |
| 枚举值 (Enum) | `OperatorType.TimerStatistics` |
| 分类 (Category) | 逻辑工具 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蔺苇君 |

## 算法原理 / Algorithm Principle
> 中文：记录相邻触发间隔并输出统计量，可在单次模式输出本次间隔，或在累计模式输出总时长、平均时长与计数。
> English: Measures interval timing between triggers and reports either per-shot elapsed time or cumulative total/average/count statistics.

## 实现策略 / Implementation Strategy
> 中文：内部使用 `Stopwatch` 保存跨调用状态，并通过 `lock` 保证并发安全。首次触发仅启动计时器并返回 `ElapsedMs=0`；后续触发读取间隔后重启计时器。`Cumulative` 模式累计 `_totalMs/_count`，可在达到 `ResetInterval` 后自动清零重新计时。
> English: Maintains state with `Stopwatch` and synchronized lock. The first call starts timing with `ElapsedMs=0`; subsequent calls read elapsed interval and restart. In cumulative mode, total/average/count are accumulated and optionally reset by `ResetInterval`.

## 核心 API 调用链 / Core API Call Chain
- 参数读取：`GetStringParam("Mode")` + `GetIntParam("ResetInterval")`
- 并发保护：`lock (_syncRoot)`
- 时间测量：`_intervalStopwatch.Start()/Elapsed/Restart()`
- 累计统计：更新 `_count`、`_totalMs`、`averageMs`
- 自动清零：`if (resetInterval > 0 && _count >= resetInterval)`
- 输出返回：`OperatorExecutionOutput.Success({ ElapsedMs, TotalMs, AverageMs, Count, Trigger? })`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Mode` | `enum` | SingleShot | - | 统计模式：`SingleShot` 或 `Cumulative` |
| `ResetInterval` | `int` | 0 | [0, 1000000] | 累计计数达到该值后自动清零；`0` 表示不自动重置 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Trigger` | Trigger | `Any` | No | 可选触发输入，存在时会透传到输出 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `ElapsedMs` | Elapsed (ms) | `Float` | 相邻触发间隔毫秒 |
| `TotalMs` | Total (ms) | `Float` | 累计总时长（单次模式等于 `ElapsedMs`） |
| `AverageMs` | Average (ms) | `Float` | 平均时长（单次模式等于 `ElapsedMs`） |
| `Count` | Count | `Integer` | 计数（累计模式下递增） |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | `O(1)` |
| 典型耗时 (Typical Latency) | 常见 `<0.1 ms`（仅本算子统计开销） |
| 内存特征 (Memory Profile) | 常量级，持有 `Stopwatch` 与少量累积状态 |

## 适用场景 / Use Cases
- 适合 (Suitable)：统计工位节拍、监控循环周期、调试流程触发抖动。
- 不适合 (Not Suitable)：需要跨进程持久化统计或硬实时高精度时钟同步场景。

## 已知限制 / Known Limitations
1. 首次触发固定返回 `ElapsedMs=0`，不代表真实业务耗时。
2. 统计状态保存在算子实例内存中，流程重启后会清零。
3. 在累计模式下触发自动重置时，当前次输出仍基于重置前计数与总和计算。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |
| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |
