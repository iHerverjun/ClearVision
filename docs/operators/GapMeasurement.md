# 间隙测量 / GapMeasurement

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `GapMeasurementOperator` |
| 枚举值 (Enum) | `OperatorType.GapMeasurement` |
| 分类 (Category) | 检测 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：支持点集与图像两种输入。点集模式按方向排序后计算相邻差值；图像模式先做灰度投影（行/列平均），再通过平滑 + 中位数/MAD 鲁棒阈值 + 峰值筛选提取特征位置，最后计算相邻间隙并按阈值过滤。
> English: Supports both point-list and image modes. Point mode sorts points and computes adjacent spacing; image mode uses 1D projection, smoothing, median/MAD robust thresholding, and peak picking to derive feature positions and gaps.

## 实现策略 / Implementation Strategy
> 中文：在“上游已有点位”与“仅有原图”两类工况间做统一封装，图像模式引入鲁棒统计阈值以降低光照与对比度波动影响。
> English: Unifies two deployment scenarios: upstream point inputs and raw-image-only inputs. Robust statistics (median/MAD) are used to reduce sensitivity to illumination/contrast drift.

## 核心 API 调用链 / Core API Call Chain
- `Cv2.CvtColor`（灰度化）
- `Cv2.Reduce`（行/列投影）
- `SmoothProfile` + `ComputeMedian` + `ComputeRobustThreshold`（鲁棒峰值门限）
- `FindFeaturePositions`（局部峰值与峰距约束）
- `DrawProjectionFeatures` / `DrawPointGaps`（结果可视化）

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Direction` | `enum` | Auto | - | - |
| `MinGap` | `double` | 0 | [0, 1000000] | - |
| `MaxGap` | `double` | 0 | [0, 1000000] | - |
| `ExpectedCount` | `int` | 0 | [0, 10000] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | Image | `Image` | No | - |
| `Points` | Points | `PointList` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | Image | `Image` | - |
| `Gaps` | Gaps | `Any` | - |
| `MeanGap` | Mean Gap | `Float` | - |
| `MinGap` | Min Gap | `Float` | - |
| `MaxGap` | Max Gap | `Float` | - |
| `Count` | Count | `Integer` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | O(W×H + N)，N 为投影长度 |
| 典型耗时 (Typical Latency) | ~3-15 ms（1920x1080） |
| 内存特征 (Memory Profile) | 灰度图 + 1D 投影数组，空间复杂度 O(W+H) |

## 适用场景 / Use Cases
- 适合 (Suitable)：等间距结构（齿距、针脚间距、阵列间隙）统计与一致性检测。
- 不适合 (Not Suitable)：非周期纹理、方向不明确且干扰强的复杂场景。

## 已知限制 / Known Limitations
1. 图像模式本质为 1D 投影，复杂二维拓扑可能丢失结构信息。
2. 峰值最小间距和显著性参数对密集细小结构有合并风险。
3. `Auto` 方向基于方差判断，极端图案下可能选错主方向。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |
| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P0 文档补全 / Completed Phase 2.3 P0 documentation enrichment |
