# 边缘检测 / CannyEdge

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `CannyEdgeOperator` |
| 枚举值 (Enum) | `OperatorType.EdgeDetection` |
| 分类 (Category) | 特征提取 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：采用 Canny 边缘检测流程（梯度、非极大值抑制、双阈值滞后连接），并提供基于中位灰度的自动阈值估计。
> English: Uses the Canny pipeline (gradient, NMS, hysteresis) with optional median-based auto-threshold estimation.

## 实现策略 / Implementation Strategy
> 中文：先灰度化，可选高斯预滤波；若启用自动阈值，则由直方图中位数和 `AutoThresholdSigma` 推导上下阈值，再执行 `Cv2.Canny`，并额外输出 `Edges` PNG 字节。
> English: Converts to grayscale, optionally blurs, derives thresholds from median intensity when auto mode is on, runs Canny, and returns edge PNG bytes in `Edges` output.

## 核心 API 调用链 / Core API Call Chain
- `Cv2.CvtColor`（灰度化）
- `Cv2.GaussianBlur`（可选预平滑）
- `Cv2.CalcHist` + `ComputeMedianIntensity`（自动阈值统计）
- `Cv2.Canny(processedSrc, dst, threshold1, threshold2, apertureSize, l2Gradient)`
- `dst.ToBytes(".png")`（输出 `Edges`）

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Threshold1` | `double` | 50 | [0, 255] | - |
| `Threshold2` | `double` | 150 | [0, 255] | - |
| `EnableGaussianBlur` | `bool` | true | - | - |
| `GaussianKernelSize` | `int` | 5 | [3, 15] | - |
| `ApertureSize` | `enum` | 3 | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 图像 | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 图像 | `Image` | - |
| `Edges` | 边缘 | `Image` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | `O(W*H)`（含直方图统计仍为线性量级） |
| 典型耗时 (Typical Latency) | 约 `1-6 ms`（1920x1080，取决于是否预滤波） |
| 内存特征 (Memory Profile) | 灰度图、预处理图、边缘图 + 可选 PNG 序列化缓冲 |

## 适用场景 / Use Cases
- 适合 (Suitable)：轮廓提取、缺陷边界定位、后续霍夫/测量算法的前置步骤。
- 不适合 (Not Suitable)：纹理噪声极强且无稳定对比度、或需要亚像素边缘模型拟合的场景。

## 已知限制 / Known Limitations
1. 自动阈值采用中位数启发式，对极端亮暗分布可能不稳定。
2. `L2Gradient` 未在属性参数中暴露，默认由运行参数读取（通常为 false）。
3. 边缘结果为二值细线，断边场景仍需后续形态学连接。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P1 文档补全 / Completed Phase 2.3 P1 documentation enrichment |