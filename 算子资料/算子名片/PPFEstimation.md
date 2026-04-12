# PPF点对特征 / PPFEstimation

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `PPFEstimationOperator` |
| 枚举值 (Enum) | `OperatorType.PPFEstimation` |
| 分类 (Category) | 3D |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：PPF（Point Pair Feature）是点对的 4 维描述子：距离 `||p2-p1||`、法向与连线角度 `∠(n1,d)` / `∠(n2,d)`、法向夹角 `∠(n1,n2)`。本算子按参考点索引输出其与半径邻域内点的 PPF 列表（per-point map）。
> English: PPF (Point Pair Feature) is a 4D descriptor for point pairs. This operator builds a per-point map: for each reference point, compute PPF features against neighbors within `FeatureRadius`.

## 实现策略 / Implementation Strategy
- 中文：
  - 法向估计使用 PCA（邻域点协方差矩阵最小特征向量）实现 `EstimateNormals`。
  - 邻域搜索使用空间哈希网格（cell size = radius），每点只扫描 27 个相邻 cell，避免暴力 O(n^2)。
  - PPF 角度计算使用 `acos(dot)`（对 `dot` 做 [-1,1] clamp），保留符号信息；法向翻转（normal sign ambiguity）需要在匹配阶段通过一致的法向朝向或显式处理来消解。
- English:
  - Normals are estimated via PCA (smallest eigenvector of the local covariance matrix).
  - Neighbor search uses a spatial hash grid (scan 27 adjacent cells per point).
  - Uses `acos(dot)` (with dot clamped to [-1,1]) to retain signed angle information; normal-flip ambiguity should be handled in the matching stage.

## 核心 API 调用链 / Core API Call Chain
- `PPFEstimationOperator.ExecuteCoreAsync`
- `OperatorBase.RunCpuBoundWork(...)`
- `PPFEstimation.ComputePointCloudWithNormals(...)`
- `PPFEstimation.ComputeModel(...)`
- `NormalEstimation.Estimate(...)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `NormalRadius` | `double` | 0.03 | >= 1E-06 | 法向估计的邻域半径（同点云单位）。 / Neighborhood radius for normal estimation. |
| `FeatureRadius` | `double` | 0.05 | >= 1E-06 | PPF 计算的邻域半径（参考点到邻居点）。 / Neighborhood radius for PPF pairs. |
| `UseExistingNormals` | `bool` | true | - | 若输入点云已有 `Normals`，是否直接复用；否则会估计法向。 / Reuse input normals when available. |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `PointCloud` | Point Cloud | `Any` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `PPFMap` | PPF Map | `Any` | - |
| `PointCloudWithNormals` | Point Cloud With Normals | `Any` | - |
| `PointCount` | Point Count | `Integer` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | ~O(n * k) (k 为半径内平均邻居数) |
| 典型耗时 (Typical Latency) | 与点数与密度强相关；建议先体素下采样 / Depends heavily on point count and density |
| 内存特征 (Memory Profile) | 额外 normals + per-point features（可能较大） / Normals + feature lists (can be large) |

## 适用场景 / Use Cases
- 适合 (Suitable)：为后续 W8-2 的 PPF 匹配构建模型特征；或用于比较局部几何一致性。
- 不适合 (Not Suitable)：超大点云且 featureRadius 过大导致邻居数爆炸时，内存与耗时都会上升，需要采样或下采样。

## 已知限制 / Known Limitations
1. 当前输出为 “每点一份 List<PPFFeature>” 的 map，内存开销可能很大；匹配阶段通常会用离散化哈希表或采样降低规模。
1. 本算子只做特征计算，不做匹配投票与位姿估计（见 W8-2）。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.0 | 2026-03-17 | 自动生成文档骨架 / Generated skeleton |
