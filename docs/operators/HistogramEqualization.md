# 直方图均衡化 / HistogramEqualization

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `HistogramEqualizationOperator` |
| 枚举值 (Enum) | `OperatorType.HistogramEqualization` |
| 分类 (Category) | 预处理 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：提升图像对比度，支持全局直方图均衡与 CLAHE 局部均衡两种方法。
> English: Enhances contrast using either global histogram equalization or CLAHE local equalization.

## 实现策略 / Implementation Strategy
> 中文：`Global` 模式使用 `EqualizeHist`，`CLAHE` 模式使用 `CreateCLAHE`；彩色图默认在亮度通道处理（YUV/Lab）以抑制色偏，也可选择逐通道处理。
> English: Global mode uses `EqualizeHist`, CLAHE mode uses `CreateCLAHE`; color images are processed on luminance channels by default to reduce hue shift, with optional per-channel processing.

## 核心 API 调用链 / Core API Call Chain
- `Cv2.CreateCLAHE` / `Cv2.EqualizeHist`
- `Cv2.CvtColor`（`BGR<->Lab` 或 `BGR<->YUV`）
- `Cv2.Split` / `Cv2.Merge`（按通道处理）
- 输出附带 `Method` 与通道信息

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Method` | `enum` | Global | - | - |
| `ClipLimit` | `double` | 2 | [0, 100] | - |
| `TileGridSize` | `int` | 8 | [1, 64] | - |

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
| 时间复杂度 (Time Complexity) | Global 约 `O(W*H)`；CLAHE 约 `O(W*H + Tiles*Bins)` |
| 典型耗时 (Typical Latency) | 约 `0.6-5 ms`（1920x1080，CLAHE 与逐通道模式更高） |
| 内存特征 (Memory Profile) | 依赖多通道拆分/合并，存在多个临时 `Mat` |

## 适用场景 / Use Cases
- 适合 (Suitable)：低对比度图像增强、照度波动下细节提取前处理。
- 不适合 (Not Suitable)：噪声占比高且无降噪前处理的图像，或色彩保真要求极高场景。

## 已知限制 / Known Limitations
1. 算子实现中读取参数名为 `TileSize`，文档参数表显示为 `TileGridSize`，使用时需按实现参数名配置。
2. 逐通道均衡可能放大色彩噪声并引入色偏。
3. 强增强会放大背景纹理，可能干扰后续二值化与检测阈值稳定性。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P1 文档补全 / Completed Phase 2.3 P1 documentation enrichment |