# 亚像素边缘 / SubpixelEdgeDetection

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `SubpixelEdgeDetectionOperator` |
| 枚举值 (Enum) | `OperatorType.SubpixelEdgeDetection` |
| 分类 (Category) | 颜色处理 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：提供 `Steger` 与传统插值两条路径。传统路径为 `GaussianBlur + Canny + 轮廓提取 + Sobel 梯度`，再沿梯度方向采样并用二次插值估计亚像素偏移；`GaussianFit` 在偏移上增加高斯权重修正。
> English: Provides two paths: `Steger` and interpolation-based traditional mode. Traditional mode applies `GaussianBlur + Canny + contour extraction + Sobel gradients`, then estimates subpixel offset along gradient direction, with optional Gaussian-weight correction.

## 实现策略 / Implementation Strategy
> 中文：在高精度需求下可选 `Steger`，在通用场景下使用插值法以平衡速度与实现复杂度；统一输出边缘点、法向和强度信息。
> English: `Steger` targets high-precision metrology, while interpolation mode balances speed and complexity. Both expose edge points, normals, and strengths in a unified output schema.

## 核心 API 调用链 / Core API Call Chain
- `StegerSubpixelEdgeDetector.DetectEdges`（Steger 分支）
- `Cv2.CvtColor` -> `Cv2.GaussianBlur` -> `Cv2.Canny` -> `Cv2.FindContours`
- `Cv2.Sobel`（梯度计算）
- `Cv2.Remap`（沿法向采样）+ 亚像素偏移求解
- `Cv2.Circle` / `Cv2.Line` / `Cv2.PutText`（结果可视化）

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `LowThreshold` | `double` | 50 | [0, 255] | - |
| `HighThreshold` | `double` | 150 | [0, 255] | - |
| `Sigma` | `double` | 1 | [0.1, 10] | - |
| `Method` | `enum` | GradientInterp | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 输入图像 | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | - |
| `Edges` | 边缘点集 | `Any` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | O(W×H + E)，E 为候选边缘点数 |
| 典型耗时 (Typical Latency) | ~8-40 ms（1920x1080，方法相关） |
| 内存特征 (Memory Profile) | 多张中间 Mat（灰度、平滑、边缘、Sobel），内存中等 |

## 适用场景 / Use Cases
- 适合 (Suitable)：高精度边缘定位、尺寸测量前置点提取、亚像素轮廓拟合。
- 不适合 (Not Suitable)：极低对比、纹理噪声强且边缘断裂严重的场景。

## 已知限制 / Known Limitations
1. `LowThreshold/HighThreshold/Sigma` 对结果敏感，需工况标定。
2. `EdgeThreshold` 仅在 `Steger` 分支生效。
3. 传统分支依赖 Canny 轮廓，弱边缘可能被提前抑制。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |
| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P0 文档补全 / Completed Phase 2.3 P0 documentation enrichment |
