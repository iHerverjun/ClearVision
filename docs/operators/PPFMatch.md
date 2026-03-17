# PPF表面匹配 / PPFMatch

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `PPFMatchOperator` |
| 枚举值 (Enum) | `OperatorType.PPFMatch` |
| 分类 (Category) | 3D |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：Simplified PPF-based 3D surface matching (model -> scene pose).。
> English: Simplified PPF-based 3D surface matching (model -> scene pose)..

## 实现策略 / Implementation Strategy
- 中文：
  - 先对模型点云构建量化后的 PPF 哈希表，再在场景中采样参考点生成候选对应。
  - 候选对应进入 RANSAC 刚体变换估计，并通过内点数和 RMS 误差进行筛选。
  - 已修正 OpenCV/SVD 到 `Matrix4x4` 的旋转矩阵约定，Week11 最终验收已收紧到 `<5mm` 平移误差。
- English:
  - Builds a quantized PPF hash table on the model, then samples scene reference points to generate candidate correspondences.
  - Candidate correspondences are verified by RANSAC rigid transform estimation and inlier/RMS scoring.
  - Rotation-matrix convention between OpenCV SVD output and `Matrix4x4` has been aligned, enabling final `<5mm` translation acceptance.

## 核心 API 调用链 / Core API Call Chain
- `PPFMatchOperator.ExecuteCoreAsync`
- `PPFMatcher.Match(...)`
- `EnsureNormals(...)`
- `BuildModelHash(...)`
- `BuildSceneCorrespondences(...)`
- `RansacRigidTransform(...) / RefineTransform(...)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `NormalRadius` | `double` | 0.03 | >= 1E-06 | - |
| `FeatureRadius` | `double` | 0.08 | >= 1E-06 | - |
| `NumSamples` | `int` | 120 | [10, 5000] | - |
| `ModelRefStride` | `int` | 3 | [1, 50] | - |
| `RansacIterations` | `int` | 800 | [50, 100000] | - |
| `InlierThreshold` | `double` | 0.005 | >= 1E-06 | - |
| `MinInliers` | `int` | 80 | [3, 1000000] | - |
| `DistanceStep` | `double` | 0.01 | >= 1E-06 | - |
| `AngleStepDeg` | `double` | 5 | [0.1, 90] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `ModelPointCloud` | Model Point Cloud | `Any` | Yes | - |
| `ScenePointCloud` | Scene Point Cloud | `Any` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `IsMatched` | Is Matched | `Boolean` | - |
| `TransformMatrix` | Transform Matrix | `Any` | - |
| `InlierCount` | Inlier Count | `Integer` | - |
| `InlierRatio` | Inlier Ratio | `Float` | - |
| `CorrespondenceCount` | Correspondence Count | `Integer` | - |
| `RmsError` | RMS Error | `Float` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 主要由模型哈希构建、场景采样和 RANSAC 迭代组成，近似随候选对应数线性增长 |
| 典型耗时 (Typical Latency) | 阶段2专项验收（Release，4500 点模型 + 4500 点场景）`P50=1786.35ms` |
| 内存特征 (Memory Profile) | 主要来自 PPF 哈希表、法向量估计与候选对应缓存，受 `NumSamples` / `ModelRefStride` 影响明显 |

## 适用场景 / Use Cases
- 适合 (Suitable)：刚体 3D 目标配准、点云姿态恢复、工件粗定位与后续精配准前置。
- 不适合 (Not Suitable)：大面积对称体、严重遮挡或需要非刚体/可变形匹配的场景。

## 已知限制 / Known Limitations
1. 当前实现强调“足够工业可用的轻量 PPF”，仍未覆盖大规模遮挡、强对称体和复杂噪声场景的完整投票优化。
1. 性能和稳定性高度依赖 `NormalRadius`、`FeatureRadius`、`NumSamples`、`ModelRefStride` 等参数组合，建议在目标工件上做一次标定式调参。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.0 | 2026-03-17 | 自动生成文档骨架 / Generated skeleton |
