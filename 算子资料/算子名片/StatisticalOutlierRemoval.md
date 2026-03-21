# 统计滤波 / StatisticalOutlierRemoval

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `StatisticalOutlierRemovalOperator` |
| 枚举值 (Enum) | `OperatorType.StatisticalOutlierRemoval` |
| 分类 (Category) | 3D |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：统计离群点移除（SOR）：对每个点计算其 K 近邻的平均距离 `mean_dist`，统计所有点的全局均值 `global_mean` 与标准差 `global_std`，移除 `mean_dist > global_mean + StddevMul * global_std` 的点。
> English: Statistical Outlier Removal (SOR): compute each point's mean distance to its K nearest neighbors, then remove points whose mean distance is above `global_mean + StddevMul * global_std`.

## 实现策略 / Implementation Strategy
- 中文：
  - 当前实现为暴力 KNN（适合点数不大，例如 <= 100k 的场景），后续可替换为 KDTree/网格加速。
  - 过滤后会保留 `Colors`/`Normals`（如果输入存在），并返回非 organized 点云。
  - 对非有限数（NaN/Inf）距离会跳过统计，相关点会被自然剔除（避免污染全局均值/方差）。
- English:
  - Brute-force KNN for simplicity (best for modest point counts). Can be optimized with KDTree later.
  - Preserves optional `Colors` and `Normals`, returns an unorganized point cloud.
  - Non-finite distances are ignored in global statistics; affected points are dropped.

## 核心 API 调用链 / Core API Call Chain
- `StatisticalOutlierRemovalOperator.ExecuteCoreAsync`
- `OperatorBase.RunCpuBoundWork(...)`
- `StatisticalOutlierRemoval.Filter(...)`
- Brute-force KNN with a fixed-size max-heap to keep the K smallest distances per point

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `MeanK` | `int` | 50 | [1, 500] | 每个点参与统计的近邻数量（K 值）。K 越大越稳定，但计算更慢。 / Number of neighbors per point. |
| `StddevMul` | `double` | 1 | [0, 10] | 标准差倍数阈值。值越大越“宽松”，保留点更多。 / Threshold multiplier. Larger keeps more points. |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `PointCloud` | Point Cloud | `Any` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `PointCloud` | Point Cloud | `Any` | - |
| `PointCount` | Point Count | `Integer` | - |
| `RemovedCount` | Removed Count | `Integer` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | O(n^2 log K) (brute-force KNN) |
| 典型耗时 (Typical Latency) | 与点数强相关；建议先体素下采样再做统计滤波 / Strongly depends on point count |
| 内存特征 (Memory Profile) | 额外 O(n) 的 mean_dist 数组 + 输出点云拷贝 / O(n) + output copy |

## 适用场景 / Use Cases
- 适合 (Suitable)：点云含少量随机离群点（散点噪声），用于分割/匹配/配准前的预清洗。
- 不适合 (Not Suitable)：超大点云（例如百万级）且未做下采样，当前暴力实现会非常慢；建议先 `VoxelDownsample` 或后续引入 KDTree。

## 已知限制 / Known Limitations
1. 当前为暴力 KNN 版本，性能主要受 n 影响，点数过大时不建议直接使用。
1. SOR 是统计阈值，可能会剔除少量边界点或稀疏区域点（属于算法特性）。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.0 | 2026-03-17 | 自动生成文档骨架 / Generated skeleton |
