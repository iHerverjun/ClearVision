# 圆测量 / CircleMeasurement

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `CircleMeasurementOperator` |
| 枚举值 (Enum) | `OperatorType.CircleMeasurement` |
| 分类 (Category) | 检测 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：先将图像灰度化并高斯平滑，使用 `HoughCircles` 进行圆检测；对每个候选圆在 ROI 内做 `Canny + FindContours`，以 `4πA/P²` 估计圆度，用于质量评估。
> English: The image is converted to grayscale and smoothed, then circles are detected via `HoughCircles`. Circularity is estimated in ROI using `Canny + FindContours` and `4πA/P²` for quality evaluation.

## 实现策略 / Implementation Strategy
> 中文：主检测使用霍夫梯度法保证召回，圆度评估使用轮廓几何指标补充质量信息，兼顾检测与测量可解释性。
> English: Hough-gradient detection provides robust recall, while contour-based circularity adds interpretable quality metrics for metrology.

## 核心 API 调用链 / Core API Call Chain
- `Cv2.CvtColor` + `Cv2.GaussianBlur`
- `Cv2.HoughCircles`（圆候选检测）
- `CalculateCircularity`: `Cv2.Canny` -> `Cv2.FindContours` -> `Cv2.ContourArea` / `Cv2.ArcLength`
- `Cv2.Circle`（圆轮廓与圆心绘制）

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Method` | `enum` | HoughCircle | - | - |
| `MinRadius` | `int` | 10 | >= 0 | - |
| `MaxRadius` | `int` | 200 | >= 0 | - |
| `Dp` | `double` | 1 | [0.5, 4] | - |
| `MinDist` | `double` | 50 | >= 1 | - |
| `Param1` | `double` | 100 | [0, 255] | - |
| `Param2` | `double` | 30 | [0, 255] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 输入图像 | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | - |
| `Radius` | 半径 | `Float` | - |
| `Center` | 圆心 | `Point` | - |
| `Circle` | 圆数据 | `CircleData` | - |
| `CircleCount` | 圆数量 | `Integer` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 近似 O(W×H×R)，R 与半径搜索空间相关 |
| 典型耗时 (Typical Latency) | ~5-30 ms（1920x1080，取决于半径范围） |
| 内存特征 (Memory Profile) | 灰度/模糊图 + 局部 ROI 边缘图，内存中等 |

## 适用场景 / Use Cases
- 适合 (Suitable)：孔径检测、圆心定位、圆形零件同心度前置分析。
- 不适合 (Not Suitable)：强遮挡、严重椭圆形变或仅弧段可见的场景。

## 已知限制 / Known Limitations
1. 当前执行路径以 `HoughCircle` 为主，`Method=FitEllipse` 尚未形成独立分支。
2. 对 `MinRadius/MaxRadius/Param2` 参数敏感，需按产品规格标定。
3. 输出默认为像素尺度，物理量需结合标定转换。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |
| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P0 文档补全 / Completed Phase 2.3 P0 documentation enrichment |
