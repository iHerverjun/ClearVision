# 轮廓测量 / ContourMeasurement

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `ContourMeasurementOperator` |
| 枚举值 (Enum) | `OperatorType.ContourMeasurement` |
| 分类 (Category) | 检测 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：灰度化后进行全局阈值二值化，提取轮廓并按面积过滤；计算面积、周长、中心矩、边界框、圆度与矩形度，并按面积或周长排序输出。
> English: Convert to grayscale, apply global thresholding, extract contours with area filtering, compute geometric descriptors (area/perimeter/moments/bounding box/circularity/extent), and sort by area or perimeter.

## 实现策略 / Implementation Strategy
> 中文：采用“二值分割 + 几何描述子”通用流程，适合绝大多数规则工件轮廓测量任务，结果字段可直接用于后续判定。
> English: Uses a generic “binary segmentation + geometric descriptors” pipeline suitable for common contour metrology and downstream rule-based decisions.

## 核心 API 调用链 / Core API Call Chain
- `Cv2.CvtColor`（灰度）
- `Cv2.Threshold`（全局二值化）
- `Cv2.FindContours`（轮廓提取）
- `Cv2.ContourArea` / `Cv2.ArcLength` / `Cv2.Moments` / `Cv2.BoundingRect`
- `Cv2.DrawContours` / `Cv2.Circle` / `Cv2.Rectangle`（可视化）

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Threshold` | `double` | 127 | [0, 255] | - |
| `MinArea` | `int` | 100 | >= 0 | - |
| `MaxArea` | `int` | 100000 | >= 0 | - |
| `SortBy` | `enum` | Area | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 输入图像 | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | - |
| `Area` | 面积 | `Float` | - |
| `Perimeter` | 周长 | `Float` | - |
| `ContourCount` | 轮廓数量 | `Integer` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | O(W×H + C)，C 为轮廓点总数 |
| 典型耗时 (Typical Latency) | ~2-10 ms（1920x1080） |
| 内存特征 (Memory Profile) | 1 张二值图 + 轮廓向量集合 |

## 适用场景 / Use Cases
- 适合 (Suitable)：目标面积/周长阈值检测、轮廓统计、形状一致性分析。
- 不适合 (Not Suitable)：灰度分布不稳定且难以使用单阈值分割的场景。

## 已知限制 / Known Limitations
1. 当前使用全局阈值，光照不均时可能导致轮廓断裂或粘连。
2. 未内置形态学去噪，复杂背景建议前置滤波或 ROI。
3. `Moments.M00` 极小场景可能引入中心点数值不稳定。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |
| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P0 文档补全 / Completed Phase 2.3 P0 documentation enrichment |
