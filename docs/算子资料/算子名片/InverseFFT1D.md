# Inverse FFT 1D / InverseFFT1D

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `InverseFFT1DOperator` |
| 枚举值 (Enum) | `OperatorType.InverseFFT1D` |
| 分类 (Category) | Frequency |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：Performs 1D Inverse Fast Fourier Transform to convert frequency spectrum back to time domain.。
> English: Performs 1D Inverse Fast Fourier Transform to convert frequency spectrum back to time domain..

## 实现策略 / Implementation Strategy
> 中文：对 1D 复频谱使用 OpenCV 逆 DFT，并在当前实现中显式启用 `DftFlags.Scale` 保证时域信号幅值回归到原始量级；若输入为双通道频谱图，则输出归一化后的灰度可视化。
> English: The current implementation performs inverse OpenCV DFT on 1D complex spectra and explicitly enables `DftFlags.Scale` so reconstructed amplitudes return to the original time-domain scale. For 2-channel spectrum images, it emits normalized grayscale visualization outputs.

## 核心 API 调用链 / Core API Call Chain
- `ExecuteCoreAsync -> IFFT(Complex[]) -> Cv2.Dft(..., DftFlags.Inverse | DftFlags.RealOutput | DftFlags.Scale)`
- `Signal -> CreateSignalVisualization(...)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| - | - | - | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Spectrum` | Input Frequency Spectrum | `Any` | Yes | - |
| `OutputSize` | Desired Output Size | `Integer` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Signal` | Reconstructed Signal | `Any` | - |
| `Real` | Real Part | `Any` | - |
| `Imaginary` | Imaginary Part | `Any` | - |
| `Image` | Visualization | `Image` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | `O(n log n)`（1D 频谱重建） |
| 典型耗时 (Typical Latency) | `<= 4 ms`（1024 点 1D 频谱，实验室基线，2026-04-15） |
| 内存特征 (Memory Profile) | 重建信号与可视化额外占用 `O(n)` |

## 适用场景 / Use Cases
- 适合 (Suitable)：FFT 频域链路回放、滤波后时域重建、实验室精度回归。
- 不适合 (Not Suitable)：需要复杂窗口补偿或全 2D 频谱复原的高精度场景。

## 已知限制 / Known Limitations
1. 图像频谱输入需要至少双通道（实部/虚部）；单通道幅度图不能直接做逆变换。
2. 当前信号可视化以调试为主，不建议直接作为生产判定输入。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.0 | 2026-03-18 | 自动生成文档骨架 / Generated skeleton |
