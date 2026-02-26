# 候选框抑制 / BoxNms

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `BoxNmsOperator` |
| 枚举值 (Enum) | `OperatorType.BoxNms` |
| 分类 (Category) | 数据处理 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：按置信度排序后进行贪心式非极大值抑制（NMS），以 IoU 阈值去除同类重叠框，保留代表性检测结果。
> English: Applies greedy NMS on confidence-sorted detections, suppressing overlapping boxes of the same class by IoU threshold.

## 实现策略 / Implementation Strategy
> 中文：先按 `ScoreThreshold` 预过滤，再按 `Label` 分组做类内 NMS；支持 `DetectionList`、字典列表等输入格式；可选在图像上同时可视化保留框与被抑制框。
> English: Pre-filters by score, performs class-wise NMS by label grouping, supports multiple detection input formats, and can visualize kept/suppressed boxes on image output.

## 核心 API 调用链 / Core API Call Chain
- `TryParseDetectionList`（兼容 `DetectionList`/`IEnumerable`/字典）
- `OrderByDescending(Confidence)` + `GroupBy(Label)`
- `IoU(a,b)` 计算 + `removed[]` 抑制标记
- `DrawDetections`（保留框绿色、抑制框红色）

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `IouThreshold` | `double` | 0.45 | [0.1, 1] | - |
| `ScoreThreshold` | `double` | 0.25 | [0, 1] | - |
| `MaxDetections` | `int` | 100 | [1, 1000] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Detections` | Detections | `DetectionList` | Yes | - |
| `Image` | Image | `Image` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Detections` | Detections | `DetectionList` | - |
| `Image` | Image | `Image` | - |
| `Count` | Count | `Integer` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 最坏约 `O(N^2)`（分组后为各组平方和） |
| 典型耗时 (Typical Latency) | 约 `0.1-4 ms`（`N=50..1000`，不含网络/推理） |
| 内存特征 (Memory Profile) | 候选框列表 + 抑制标记数组，额外开销约 `O(N)` |

## 适用场景 / Use Cases
- 适合 (Suitable)：目标检测后处理、去重冗余框、下游统计与跟踪前的框精简。
- 不适合 (Not Suitable)：需要跨类别联合抑制或 Soft-NMS 的场景。

## 已知限制 / Known Limitations
1. 当前按 `Label` 分组做 NMS，不同标签之间不会互相抑制。
2. 输入框几何质量差时（宽高为负或异常）会被裁剪或丢弃，需上游保证数据质量。
3. 若标签缺失会落入同一分组，可能造成过抑制。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |