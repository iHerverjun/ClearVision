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
- 中文：
  - 采用“简化 PatchCore”思路：先从正常样本提取局部 patch 特征，再用特征库最近邻距离做异常评分。
  - 训练模式从 `NormalImages` 构建特征库，并可保存到 `FeatureBankPath/SaveFeatureBankPath`。
  - 推理模式支持直接读取特征库，也支持通过 `ModelId + ModelCatalogPath` 从模型仓库解析特征库路径。
  - 输出热力图与二值掩码，便于与阈值判定、结果存档和前端叠加显示联动。
- English:
  - Uses a simplified PatchCore pipeline: extract local patch features from normal samples, then score by nearest-neighbor distance against a feature bank.
  - Training mode builds and optionally persists a feature bank from `NormalImages`.
  - Inference mode supports both direct bank path loading and repository-driven lookup through `ModelId + ModelCatalogPath`.
  - Produces both heatmap and binary mask for downstream visualization and logic gates.

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
- 适合 (Suitable)：缺陷样本稀缺、但正常样本容易采集的工业表面检测、异物检测、区域异常提示。
- 不适合 (Not Suitable)：需要明确缺陷类别、多类别监督分类或强几何先验的任务。

## 已知限制 / Known Limitations
1. 当前为轻量级 C# 记忆库实现，未接入真实 ResNet/ViT backbone 与大规模 MVTec 资产基准。
1. 推理复杂度与特征库规模相关，若特征库过大应通过 `CoresetRatio` 控制采样规模。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.0 | 2026-03-17 | 自动生成文档骨架 / Generated skeleton |
