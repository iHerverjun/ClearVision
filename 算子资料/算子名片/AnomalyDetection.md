# 异常检测 / AnomalyDetection

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `AnomalyDetectionOperator` |
| 枚举值 (Enum) | `OperatorType.AnomalyDetection` |
| 分类 (Category) | AI检测 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：Runs a simplified PatchCore-style anomaly detector with train/inference modes and feature-bank persistence.。
> English: Runs a simplified PatchCore-style anomaly detector with train/inference modes and feature-bank persistence..

## 实现策略 / Implementation Strategy
> 中文：TODO：补充实现策略与方案对比。
> English: TODO: Add implementation strategy and alternatives comparison.

## 核心 API 调用链 / Core API Call Chain
- `OpenCvSharp + memory-bank nearest-neighbor`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Mode` | `enum` | inference | - | - |
| `FeatureBankPath` | `file` | "" | - | - |
| `SaveFeatureBankPath` | `file` | "" | - | - |
| `ModelId` | `string` | "" | - | - |
| `ModelCatalogPath` | `file` | "" | - | - |
| `Backbone` | `string` | simple_patchcore | - | - |
| `PatchSize` | `int` | 32 | [4, 256] | - |
| `PatchStride` | `int` | 16 | [1, 256] | - |
| `CoresetRatio` | `double` | 0.2 | [0.01, 1] | - |
| `Threshold` | `double` | 0.35 | [0, 1] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | Image | `Image` | No | - |
| `NormalImages` | Normal Images | `Any` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `AnomalyScore` | Anomaly Score | `Float` | - |
| `IsAnomaly` | Is Anomaly | `Boolean` | - |
| `AnomalyMap` | Anomaly Map | `Image` | - |
| `AnomalyMask` | Anomaly Mask | `Image` | - |
| `FeatureBankPath` | Feature Bank Path | `String` | - |
| `PatchCount` | Patch Count | `Integer` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | O(P * B) |
| 典型耗时 (Typical Latency) | ~?ms (1920x1080) |
| 内存特征 (Memory Profile) | O(B) |

## 适用场景 / Use Cases
- 适合 (Suitable)：TODO
- 不适合 (Not Suitable)：TODO

## 已知限制 / Known Limitations
1. TODO

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.0 | 2026-04-07 | 自动生成文档骨架 / Generated skeleton |
