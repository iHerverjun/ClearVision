# 颜色检测 / ColorDetection

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `ColorDetectionOperator` |
| 枚举值 (Enum) | `OperatorType.ColorDetection` |
| 分类 (Category) | 颜色处理 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：按 `AnalysisMode` 分三种流程：`Average` 计算颜色空间均值；`Dominant` 先降采样后执行 K-Means 提取主色；`Range` 在 HSV/Lab 空间做阈值分割并统计命中占比。
> English: Three analysis paths are provided: `Average` computes channel means, `Dominant` extracts major colors via resized K-Means clustering, and `Range` performs HSV/Lab threshold segmentation with coverage statistics.

## 实现策略 / Implementation Strategy
> 中文：将平均色、主色和范围分割统一到同一算子，既可用于快速调参诊断，也可直接输出生产判定所需统计量。
> English: Average-color, dominant-color, and range segmentation are unified in one operator for both fast diagnostics and production-ready statistics.

## 核心 API 调用链 / Core API Call Chain
- `Cv2.CvtColor` + `Cv2.Mean`（Average）
- `Cv2.Resize` + `Cv2.Kmeans`（Dominant）
- `Cv2.InRange` + `Cv2.CountNonZero`（Range）
- `Cv2.BitwiseAnd` + `Cv2.AddWeighted` + `Cv2.PutText`（可视化叠加）

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `ColorSpace` | `enum` | HSV | - | - |
| `AnalysisMode` | `enum` | Average | - | - |
| `HueLow` | `int` | 0 | [0, 180] | - |
| `HueHigh` | `int` | 180 | [0, 180] | - |
| `SatLow` | `int` | 50 | [0, 255] | - |
| `SatHigh` | `int` | 255 | [0, 255] | - |
| `ValLow` | `int` | 50 | [0, 255] | - |
| `ValHigh` | `int` | 255 | [0, 255] | - |
| `DominantK` | `int` | 3 | [1, 10] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 输入图像 | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | - |
| `ColorInfo` | 颜色信息 | `Any` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | Average/Range: O(W×H)；Dominant: O(k·n·iter)，n=64×64 |
| 典型耗时 (Typical Latency) | Average/Range: ~1-6 ms；Dominant: ~3-12 ms |
| 内存特征 (Memory Profile) | 颜色转换图 + 掩膜/聚类缓冲，整体中低内存占用 |

## 适用场景 / Use Cases
- 适合 (Suitable)：色偏监控、主色占比分析、颜色范围合格率判定。
- 不适合 (Not Suitable)：光照剧烈波动且无白平衡约束的场景，或需光谱级颜色精度任务。

## 已知限制 / Known Limitations
1. `Range` 模式阈值依赖现场光照，建议配合标准光源与白平衡。
2. `Dominant` 采用缩小图聚类，可能忽略小面积关键颜色区域。
3. 当前只支持 HSV/Lab 两种空间，未内置自定义颜色模型。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |
| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P0 文档补全 / Completed Phase 2.3 P0 documentation enrichment |
