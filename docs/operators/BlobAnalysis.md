# Blob分析 / BlobDetection

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `BlobDetectionOperator` |
| 枚举值 (Enum) | `OperatorType.BlobAnalysis` |
| 分类 (Category) | 特征提取 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：基于 OpenCV `SimpleBlobDetector` 对图像中的斑点/连通区域进行关键点检测，按面积（及可选形状约束）过滤，输出中心位置、尺寸和估算面积。
> English: Uses OpenCV `SimpleBlobDetector` to detect blob keypoints, filter by area (and optional shape constraints), and output center, size, and estimated area.

## 实现策略 / Implementation Strategy
> 中文：采用检测器参数化方案，避免手写连通域标记流程，开发和维护成本低，适合快速上线斑点类任务。
> English: A detector-parameterized approach avoids manual connected-component pipelines and is efficient for rapid deployment of spot/blob tasks.

## 核心 API 调用链 / Core API Call Chain
- `SimpleBlobDetector.Params`（面积/形状过滤参数）
- `SimpleBlobDetector.Create(...).Detect(...)`
- `Cv2.CvtColor`（灰度输入转彩色可视化）
- `Cv2.Circle`（绘制 blob 外接圆与中心）

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `MinArea` | `int` | 100 | >= 0 | - |
| `MaxArea` | `int` | 100000 | >= 0 | - |
| `Color` | `enum` | White | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 图像 | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 标记图像 | `Image` | - |
| `Blobs` | Blob数据 | `Contour` | - |
| `BlobCount` | Blob数量 | `Integer` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 近似 O(W×H)（内部多阈值扫描） |
| 典型耗时 (Typical Latency) | ~2-15 ms（1920x1080） |
| 内存特征 (Memory Profile) | 低到中等，主要为检测器中间缓冲与结果列表 |

## 适用场景 / Use Cases
- 适合 (Suitable)：颗粒/气泡/污点计数、亮斑定位、粗粒度缺陷筛查。
- 不适合 (Not Suitable)：相互粘连目标的精细轮廓分割与高精度边界测量。

## 已知限制 / Known Limitations
1. 文档参数仅暴露 `MinArea/MaxArea/Color`，而实现还兼容部分隐式形状参数，后续需统一参数面。
2. `Color` 参数在当前执行路径未显式参与极性分离。
3. 输出为 keypoint 近似面积，不等同于精确像素轮廓面积。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |
| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P0 文档补全 / Completed Phase 2.3 P0 documentation enrichment |
