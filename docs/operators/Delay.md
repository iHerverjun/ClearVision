# 延时 / Delay

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `DelayOperator` |
| 枚举值 (Enum) | `OperatorType.Delay` |
| 分类 (Category) | 流程控制 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蔺苇君 |

## 算法原理 / Algorithm Principle
> 中文：在流程中主动等待指定毫秒数后继续执行，并将输入值透传到输出，常用于节拍对齐与设备就绪等待。
> English: Waits for a configured duration, then continues execution with pass-through output, commonly used for pacing and device readiness delay.

## 实现策略 / Implementation Strategy
> 中文：通过 `Task.Delay(ms, cancellationToken)` 实现可取消等待；等待前后取 UTC 时间差计算真实耗时 `ElapsedMs`。若存在输入 `Input` 则透传，否则输出空字符串，便于下游链路保持统一端口。
> English: Uses cancellable `Task.Delay` and computes actual elapsed time via UTC timestamps. Passes through `Input` when provided, otherwise emits an empty string.

## 核心 API 调用链 / Core API Call Chain
- 参数读取：`GetIntParam("Milliseconds", 200, 0, 60000)`
- 计时起点：`DateTime.UtcNow`
- 延时执行：`Task.Delay(ms, cancellationToken)`
- 计时终点：`DateTime.UtcNow - start`
- 结果返回：`OperatorExecutionOutput.Success({ Output, ElapsedMs })`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Milliseconds` | `int` | 200 | [0, 60000] | 延时毫秒数，超出范围会被参数读取逻辑约束 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Input` | 透传输入 | `Any` | No | 可选透传数据 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Output` | 透传输出 | `Any` | 输入值透传（无输入时为空字符串） |
| `ElapsedMs` | 实际耗时(ms) | `Integer` | 实际等待时长（受调度抖动影响） |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 算子计算部分 `O(1)`，总时延约为配置等待时间 `T` |
| 典型耗时 (Typical Latency) | 约 `T` 到 `T+15 ms`（取决于线程调度与系统负载） |
| 内存特征 (Memory Profile) | 常量级，仅保存少量时间戳与输出字典 |

## 适用场景 / Use Cases
- 适合 (Suitable)：设备握手前等待、拍照触发节拍缓冲、串行流程节流。
- 不适合 (Not Suitable)：需要亚毫秒级精确定时或硬实时控制场景。

## 已知限制 / Known Limitations
1. 单次延时上限为 `60000 ms`（60 秒）。
2. 精度受操作系统调度影响，不保证硬实时。
3. 延时期间当前执行链路被占用，不适合作为高吞吐并行限流手段。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |
| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |
