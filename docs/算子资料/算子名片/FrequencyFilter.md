# Frequency Filter / FrequencyFilter

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `FrequencyFilterOperator` |
| 枚举值 (Enum) | `OperatorType.FrequencyFilter` |
| 分类 (Category) | Frequency |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：Applies frequency domain filters (low-pass, high-pass, band-pass, band-stop) to spectrum.。
> English: Applies frequency domain filters (low-pass, high-pass, band-pass, band-stop) to spectrum..

## 实现策略 / Implementation Strategy
> 中文：对 1D 频谱使用掩码逐点乘法完成低通/高通/带通/带阻滤波；对 2D 频谱使用归一化半径生成滤波掩码，优先满足实验室内主链验证与频域效果观察。
> English: The implementation applies point-wise masks for 1D spectra and radius-normalized masks for 2D spectra, primarily to support lab-side chain validation and frequency-response inspection.

## 核心 API 调用链 / Core API Call Chain
- `ExecuteCoreAsync -> CreateFilterMask1D / CreateFilterMask2D`
- `FilteredSpectrum = Spectrum * Mask -> CreateFilterVisualization1D(...)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| - | - | - | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Spectrum` | Input Frequency Spectrum | `Any` | Yes | - |
| `FilterType` | Filter Type | `String` | Yes | - |
| `CutoffLow` | Low Cutoff Frequency | `Float` | No | - |
| `CutoffHigh` | High Cutoff Frequency | `Float` | No | - |
| `Order` | Filter Order | `Integer` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `FilteredSpectrum` | Filtered Spectrum | `Any` | - |
| `FilterMask` | Filter Mask | `Any` | - |
| `Image` | Visualization | `Image` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | `O(n)`（1D 频谱掩码乘法） |
| 典型耗时 (Typical Latency) | `<= 1 ms`（1024 点频谱，实验室基线，2026-04-15） |
| 内存特征 (Memory Profile) | 额外创建与频谱等长的滤波掩码 |

## 适用场景 / Use Cases
- 适合 (Suitable)：低通去高频噪声、带通保留目标频段、实验室内主链滤波验证。
- 不适合 (Not Suitable)：需要严格数学窗函数设计、需要现场级自适应频谱建模的复杂场景。

## 已知限制 / Known Limitations
1. 当前 1D 滤波器以经验型掩码为主，适合实验室验证，不等同于完整数字信号处理库。
2. 2D 频谱分支更偏可视化与调试，不作为本轮闭环的核心证据链。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.0 | 2026-03-18 | 自动生成文档骨架 / Generated skeleton |
