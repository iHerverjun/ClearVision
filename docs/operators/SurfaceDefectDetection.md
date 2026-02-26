# 表面缺陷检测 / SurfaceDefectDetection

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `SurfaceDefectDetectionOperator` |
| 枚举值 (Enum) | `OperatorType.SurfaceDefectDetection` |
| 分类 (Category) | AI检测 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：通过响应图构建（梯度幅值 / 参考图差分 / 局部对比度）定位疑似缺陷，再经阈值分割、形态学清理和轮廓面积筛选输出缺陷区域。
> English: Builds a defect response map (gradient/reference-diff/local-contrast), then thresholds, morphologically cleans, and contour-filters by area.

## 实现策略 / Implementation Strategy
> 中文：统一转灰度后按 `Method` 分支处理；`MorphCleanSize` 自动修正奇数核执行开闭操作；最终输出缺陷掩膜、缺陷计数与总面积，并在原图标注外接框。
> English: Converts to grayscale, executes method-specific response generation, applies odd-kernel open/close cleanup, then outputs defect mask/count/area with image annotations.

## 核心 API 调用链 / Core API Call Chain
- `Cv2.CvtColor`（灰度化）
- 响应图：`Cv2.Sobel`/`Cv2.Magnitude` 或 `Cv2.Absdiff` 或 `Cv2.GaussianBlur`
- `Cv2.Threshold`（二值化）
- `Cv2.MorphologyEx(Open/Close)`
- `Cv2.FindContours` + `Cv2.ContourArea` + `Cv2.BoundingRect`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Method` | `enum` | GradientMagnitude | - | - |
| `Threshold` | `double` | 35 | [0, 255] | - |
| `MinArea` | `int` | 20 | [0, 10000000] | - |
| `MaxArea` | `int` | 1000000 | [0, 10000000] | - |
| `MorphCleanSize` | `int` | 3 | [1, 301] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | Image | `Image` | Yes | - |
| `Reference` | Reference | `Image` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | Image | `Image` | - |
| `DefectMask` | Defect Mask | `Image` | - |
| `DefectCount` | Defect Count | `Integer` | - |
| `DefectArea` | Defect Area | `Float` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 约 `O(W*H*K^2 + C)`（`C` 为轮廓数量） |
| 典型耗时 (Typical Latency) | 约 `2-18 ms`（1920x1080，受方法与核尺寸影响） |
| 内存特征 (Memory Profile) | 灰度图、响应图、二值图与掩膜图，多张中间 `Mat` |

## 适用场景 / Use Cases
- 适合 (Suitable)：划痕、污点、局部异常亮暗等传统视觉缺陷预筛。
- 不适合 (Not Suitable)：缺陷纹理与背景极度相似且需学习式语义判别的场景。

## 已知限制 / Known Limitations
1. `ReferenceDiff` 模式必须提供 `Reference` 输入，否则会失败。
2. 当前流程主要基于灰度响应，对纯色度差异缺陷不敏感。
3. 阈值与面积参数需按工件材质和光照条件重新标定。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |