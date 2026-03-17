# 欧氏聚类分割 / EuclideanClusterExtraction

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `EuclideanClusterExtractionOperator` |
| 枚举值 (Enum) | `OperatorType.EuclideanClusterExtraction` |
| 分类 (Category) | 3D |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：欧氏聚类分割：以 `ClusterTolerance` 为连通阈值，对点云做 3D 连通域（BFS/DFS）。两点距离小于阈值则视为连通，最终输出每个连通分量的点索引集合。
> English: Euclidean clustering: build 3D connected components via BFS/DFS using `ClusterTolerance` as the connectivity threshold, outputting point index sets per component.

## 实现策略 / Implementation Strategy
- 中文：
  - 邻域查询采用网格哈希（cell size = `ClusterTolerance`），每次只扫描 27 个相邻 cell，避免 O(n^2) 暴力找邻居。
  - 可配 `MinClusterSize/MaxClusterSize` 对结果做过滤。
  - 算子额外输出 `ClusterPointClouds`，便于后续对每个聚类单独处理。
- English:
  - Uses a spatial hash grid (cell size = `ClusterTolerance`) and checks only 27 neighboring cells per point.
  - Supports min/max cluster size filtering.
  - Operator also materializes `ClusterPointClouds` for downstream processing.

## 核心 API 调用链 / Core API Call Chain
- `EuclideanClusterExtractionOperator.ExecuteCoreAsync`
- `OperatorBase.RunCpuBoundWork(...)`
- `EuclideanClusterExtraction.Extract(...)`
- `EuclideanClusterExtraction.ExtractPointClouds(...)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `ClusterTolerance` | `double` | 0.02 | >= 1E-06 | 连通距离阈值（同点云单位）。 / Connectivity distance threshold. |
| `MinClusterSize` | `int` | 100 | [1, 10000000] | 最小聚类点数，小于该值的连通分量将被丢弃。 / Minimum cluster size. |
| `MaxClusterSize` | `int` | 1000000 | [1, 10000000] | 最大聚类点数，大于该值的连通分量将被丢弃。 / Maximum cluster size. |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `PointCloud` | Point Cloud | `Any` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Clusters` | Clusters | `Any` | - |
| `ClusterCount` | Cluster Count | `Integer` | - |
| `ClusterPointClouds` | Cluster Point Clouds | `Any` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | ~O(n) average (grid-hash neighbor queries) |
| 典型耗时 (Typical Latency) | 与点数与密度相关；建议先下采样 / Depends on point count and density |
| 内存特征 (Memory Profile) | O(n) visited + grid buckets + 输出聚类点云拷贝 / O(n) + output copies |

## 适用场景 / Use Cases
- 适合 (Suitable)：分割多个相互分离的物体点云，为后续匹配/测量提供候选区域。
- 不适合 (Not Suitable)：点云密度极不均匀或对象强连接时可能合并为一个大聚类；需要配合下采样、裁剪或更复杂的分割策略。

## 已知限制 / Known Limitations
1. 当前聚类按单阈值距离连通，无法处理“相交/接触”目标的分离（需要额外特征或模型约束）。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.0 | 2026-03-17 | 自动生成文档骨架 / Generated skeleton |
