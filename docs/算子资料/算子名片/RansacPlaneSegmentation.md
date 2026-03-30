# RANSAC平面分割 / RansacPlaneSegmentation

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `RansacPlaneSegmentationOperator` |
| 枚举值 (Enum) | `OperatorType.RansacPlaneSegmentation` |
| 分类 (Category) | 3D |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：RANSAC 平面分割：随机采样 3 点拟合平面，统计距离平面小于阈值的内点数，迭代若干次选取内点最多的模型；最终使用内点做一次 PCA 平面精修（协方差矩阵最小特征向量作为法向量），并重新计算内点集。
> English: RANSAC plane segmentation: sample 3 points to hypothesize a plane, score by inlier count under a distance threshold, keep the best model, then refine via PCA on inliers and recompute inliers.

## 实现策略 / Implementation Strategy
- 中文：
  - 当前实现增加了“两阶段候选评分”：先用子样本粗筛候选平面，再对少量高分候选做全量精评，显著降低百万点场景的验收延迟。
  - 使用 `OpenCvSharp.Cv2.Eigen` 对 3x3 协方差矩阵做特征分解完成 PCA 精修。
  - 输出平面系数按 `ax + by + cz + d = 0`，其中 `(a,b,c)` 为单位法向量。
- English:
  - Uses a two-stage candidate scoring path: coarse subsampled evaluation first, then full scoring on a small set of top planes.
  - Uses `OpenCvSharp.Cv2.Eigen` on a 3x3 covariance matrix for PCA refinement.
  - Outputs plane coefficients `ax + by + cz + d = 0` with unit normal.

## 核心 API 调用链 / Core API Call Chain
- `RansacPlaneSegmentationOperator.ExecuteCoreAsync`
- `OperatorBase.RunCpuBoundWork(...)`
- `RansacPlaneSegmentation.Segment(...)`
- `RansacPlaneSegmentation.ExtractInlierCloud(...)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `DistanceThreshold` | `double` | 0.01 | >= 1E-06 | 平面距离阈值（同点云单位）。越小越严格。 / Inlier distance threshold. |
| `MaxIterations` | `int` | 1000 | [1, 200000] | 最大迭代次数，越大越稳但更慢。 / Max RANSAC iterations. |
| `MinInliers` | `int` | 100 | [1, 10000000] | 最小内点数门槛，达不到视为失败。 / Minimum inliers required. |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `PointCloud` | Point Cloud | `Any` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `PlaneA` | Plane A | `Float` | - |
| `PlaneB` | Plane B | `Float` | - |
| `PlaneC` | Plane C | `Float` | - |
| `PlaneD` | Plane D | `Float` | - |
| `InlierCount` | Inlier Count | `Integer` | - |
| `InlierRatio` | Inlier Ratio | `Float` | - |
| `Inliers` | Inliers | `Any` | - |
| `InlierPointCloud` | Inlier Point Cloud | `Any` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | O(n * MaxIterations) |
| 典型耗时 (Typical Latency) | 阶段2专项验收（Release，100万点，阈值 1.5mm，144 次迭代）核心分割满足 `<300ms`；若算子同时输出 `InlierPointCloud`，总耗时会高于核心分割 |
| 内存特征 (Memory Profile) | O(n) scratch + 输出内点点云拷贝 / O(n) + inlier cloud copy |

## 适用场景 / Use Cases
- 适合 (Suitable)：从场景点云中提取主平面（桌面/地面/工装平面），作为后续聚类、配准、测量的基准。
- 不适合 (Not Suitable)：未下采样的超大点云（百万级）直接使用时会很慢；需要 KDTree 或基于网格的加速。

## 已知限制 / Known Limitations
1. 若必须同时物化 `InlierPointCloud`，算子总耗时会显著高于核心分割耗时；这是输出拷贝成本而非平面估计本身的瓶颈。
1. 只做单平面提取；多平面需要迭代剔除内点并重复调用。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.0 | 2026-03-17 | 自动生成文档骨架 / Generated skeleton |
