# 光照校正 / ShadingCorrection

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `ShadingCorrectionOperator` |
| 枚举值 (Enum) | `OperatorType.ShadingCorrection` |
| 分类 (Category) | 预处理 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：针对不均匀照明提供三种校正方式：背景相除、Gaussian 背景建模相除、形态学 TopHat 去背景。
> English: Corrects uneven illumination using divide-by-background, Gaussian-model division, or morphological TopHat.

## 实现策略 / Implementation Strategy
> 中文：输入先转灰度，再按 `Method` 分支处理；前两种方法在浮点域做除法并归一化，TopHat 直接做形态学变换。彩色输入最终会将灰度结果回写为 BGR 三通道。
> English: Converts to grayscale first, processes by method branch, uses float-domain division + normalization for division modes, and converts corrected grayscale back to BGR for color inputs.

## 核心 API 调用链 / Core API Call Chain
- `Cv2.CvtColor`（灰度化/回写 BGR）
- `CorrectByBackground`：`Cv2.Resize` + `Cv2.Divide` + `Cv2.Normalize`
- `CorrectByGaussianModel`：`Cv2.GaussianBlur` + `Cv2.Divide` + `Cv2.Normalize`
- `CorrectByTopHat`：`Cv2.GetStructuringElement` + `Cv2.MorphologyEx(TopHat)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Method` | `enum` | GaussianModel | - | - |
| `KernelSize` | `int` | 51 | [3, 501] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | Image | `Image` | Yes | - |
| `Background` | Background | `Image` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | Image | `Image` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 背景相除约 `O(W*H)`；Gaussian/TopHat 约 `O(W*H*K^2)` |
| 典型耗时 (Typical Latency) | 约 `1-20 ms`（1920x1080，取决于方法与 `KernelSize`） |
| 内存特征 (Memory Profile) | 需多个浮点中间 `Mat`（除法与归一化链路） |

## 适用场景 / Use Cases
- 适合 (Suitable)：光照渐变明显、平场不均导致阈值波动的检测场景。
- 不适合 (Not Suitable)：强彩色信息必须保真的场景（当前主要基于灰度校正）。

## 已知限制 / Known Limitations
1. `DivideByBackground` 模式必须提供 `Background` 输入，否则执行失败。
2. 彩色图像在校正时转为灰度处理，色度信息不会被独立校正。
3. 大核参数会显著增加耗时，并可能过度削弱真实低频结构。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P1 文档补全 / Completed Phase 2.3 P1 documentation enrichment |