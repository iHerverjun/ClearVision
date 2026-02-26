# 自适应阈值 / AdaptiveThreshold

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `AdaptiveThresholdOperator` |
| 枚举值 (Enum) | `OperatorType.AdaptiveThreshold` |
| 分类 (Category) | 预处理 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：按局部窗口统计值（Mean/Gaussian）计算像素级阈值，并通过常数 `C` 调整阈值偏移，实现非均匀光照下分割。
> English: Computes local thresholds per pixel using mean or Gaussian-weighted neighborhoods with offset `C`.

## 实现策略 / Implementation Strategy
> 中文：统一先转灰度，`BlockSize` 自动修正为奇数；`AdaptiveMethod` 映射到 `MeanC/GaussianC`，`ThresholdType` 映射到 `Binary/BinaryInv`，执行后转 BGR 便于显示。
> English: Converts to grayscale, normalizes odd `BlockSize`, maps method/type strings to OpenCV enums, runs adaptive thresholding, and converts result to BGR for display.

## 核心 API 调用链 / Core API Call Chain
- `Cv2.CvtColor(..., BGR2GRAY)`
- 字符串到枚举映射：`AdaptiveThresholdTypes` / `ThresholdTypes`
- `Cv2.AdaptiveThreshold(gray, binary, maxValue, method, type, blockSize, C)`
- `Cv2.CvtColor(binary, dst, GRAY2BGR)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `MaxValue` | `double` | 255 | [0, 255] | - |
| `AdaptiveMethod` | `enum` | Gaussian | - | - |
| `ThresholdType` | `enum` | Binary | - | - |
| `BlockSize` | `int` | 11 | [3, 99] | - |
| `C` | `double` | 2 | [-100, 100] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 输入图像 | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 输出图像 | `Image` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 近似 `O(W*H*B^2)`，`B` 为局部块尺寸 |
| 典型耗时 (Typical Latency) | 约 `1-8 ms`（1920x1080，`BlockSize=11..31`） |
| 内存特征 (Memory Profile) | 灰度图 + 二值图 + BGR 输出图 |

## 适用场景 / Use Cases
- 适合 (Suitable)：光照不均、局部阴影明显、纸面/标签类文本与边缘提取。
- 不适合 (Not Suitable)：图像噪声很强且未先做滤波、或目标灰度与背景几乎无差异场景。

## 已知限制 / Known Limitations
1. 对 `BlockSize` 与 `C` 较敏感，需要按镜头和光源重新标定。
2. 仅支持 `Binary/BinaryInv`，不支持 `Trunc/ToZero` 等类型。
3. 输出同样固定转 BGR，若需单通道二值需下游显式转换。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P1 文档补全 / Completed Phase 2.3 P1 documentation enrichment |