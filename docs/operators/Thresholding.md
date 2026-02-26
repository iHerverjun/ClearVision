# 二值化 / Threshold

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `ThresholdOperator` |
| 枚举值 (Enum) | `OperatorType.Thresholding` |
| 分类 (Category) | 预处理 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：将灰度图按阈值规则映射到前景/背景，可选 Otsu 自动阈值以适配双峰直方图。
> English: Converts grayscale intensities into binary/thresholded output, with optional Otsu automatic thresholding.

## 实现策略 / Implementation Strategy
> 中文：先保证输入为单通道灰度，再依据 `Type` 与 `UseOtsu` 组合阈值类型执行 `Cv2.Threshold`；输出阶段将单通道结果转为 BGR 以提升前端显示兼容性。
> English: Converts input to grayscale, combines `Type` with `UseOtsu`, runs `Cv2.Threshold`, and converts the single-channel result to BGR for viewer compatibility.

## 核心 API 调用链 / Core API Call Chain
- `Cv2.CvtColor(..., BGR2GRAY)`（必要时）
- `Cv2.Threshold(gray, binary, thresh, maxVal, thresholdType)`
- `Cv2.CvtColor(binary, dst, GRAY2BGR)`
- 输出附带 `ActualThreshold` / `OtsuThreshold`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Threshold` | `double` | 127 | [0, 255] | - |
| `MaxValue` | `double` | 255 | [0, 255] | - |
| `Type` | `enum` | 0 | - | - |
| `UseOtsu` | `bool` | false | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 图像 | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 图像 | `Image` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | `O(W*H)` |
| 典型耗时 (Typical Latency) | 约 `0.2-1.2 ms`（1920x1080） |
| 内存特征 (Memory Profile) | 灰度图 + 二值图 + BGR 输出图 |

## 适用场景 / Use Cases
- 适合 (Suitable)：前景提取、缺陷区域初筛、轮廓检测前的快速二值化。
- 不适合 (Not Suitable)：光照强不均且局部对比度变化大的场景（优先自适应阈值）。

## 已知限制 / Known Limitations
1. Otsu 仅在直方图双峰较明显时效果稳定。
2. 输出固定转为三通道 BGR，若下游期望单通道需自行转换。
3. 不包含噪声抑制步骤，通常需要与滤波或形态学组合使用。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P1 文档补全 / Completed Phase 2.3 P1 documentation enrichment |