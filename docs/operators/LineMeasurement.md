# 直线测量 / LineMeasurement

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `LineMeasurementOperator` |
| 枚举值 (Enum) | `OperatorType.LineMeasurement` |
| 分类 (Category) | 检测 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：灰度化后执行 Canny 边缘检测；`HoughLine` 模式基于极坐标直线检测输出角度，另一模式使用 `HoughLinesP` 输出线段长度与角度。
> English: Convert to grayscale, run Canny edge detection, then detect lines by `HoughLine` (polar form) or `HoughLinesP` (segment form) and output angle/length statistics.

## 实现策略 / Implementation Strategy
> 中文：保留两种霍夫路径以兼容“全局主方向检测”和“显式线段测量”两类需求，参数简洁、调试成本低。
> English: Two Hough paths are kept to cover both dominant-line orientation and explicit segment measurement with simple parameter tuning.

## 核心 API 调用链 / Core API Call Chain
- `Cv2.CvtColor` + `Cv2.Canny`
- `Cv2.HoughLines`（极坐标直线）或 `Cv2.HoughLinesP`（概率霍夫线段）
- 角度与长度计算（`Math.Atan2` / 欧氏距离）
- `Cv2.Line`（绘制检测线）

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Method` | `enum` | HoughLine | - | - |
| `Threshold` | `int` | 100 | >= 1 | - |
| `MinLength` | `double` | 50 | >= 0 | - |
| `MaxGap` | `double` | 10 | >= 0 | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 输入图像 | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | - |
| `Angle` | 角度 | `Float` | - |
| `Length` | 长度 | `Float` | - |
| `Line` | 直线数据 | `LineData` | - |
| `LineCount` | 直线数量 | `Integer` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 近似 O(W×H + V)，V 为霍夫累加投票量 |
| 典型耗时 (Typical Latency) | ~3-20 ms（1920x1080） |
| 内存特征 (Memory Profile) | 1 张边缘图 + 有界线结果集合 |

## 适用场景 / Use Cases
- 适合 (Suitable)：边缘方向监控、线段长度统计、装配角度快速检查。
- 不适合 (Not Suitable)：弯曲轮廓、弱边缘或强纹理噪声导致的误检场景。

## 已知限制 / Known Limitations
1. `FitLine` 选项当前实际走 `HoughLinesP` 分支，不是最小二乘直线拟合。
2. Canny 阈值固定在代码中（50/150），不同工况需前置增强。
3. 可能产生重复/近似平行线，需后续去重策略。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |
| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P0 文档补全 / Completed Phase 2.3 P0 documentation enrichment |
