# FFT 1D / FFT1D

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `FFT1DOperator` |
| 枚举值 (Enum) | `OperatorType.FFT1D` |
| 分类 (Category) | Frequency |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：Performs 1D Fast Fourier Transform on input signal or image rows/columns.。
> English: Performs 1D Fast Fourier Transform on input signal or image rows/columns..

## 实现策略 / Implementation Strategy
> 中文：当前实现对 1D 数组直接做 OpenCV DFT；若输入为图像，则按指定轴逐行或逐列做 1D FFT，并额外输出幅度/相位可视化，优先服务实验室内的频域链路验证。
> English: The current implementation runs OpenCV DFT on 1D arrays directly. For image inputs, it performs row-wise or column-wise 1D FFT and emits magnitude/phase visualizations for lab-side frequency-chain verification.

## 核心 API 调用链 / Core API Call Chain
- `ExecuteCoreAsync -> FFT(double[]) -> Cv2.Dft(..., DftFlags.ComplexOutput)`
- `Spectrum -> Magnitude/Phase -> CreateSpectrumVisualization(...)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| - | - | - | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Input` | Input Signal or Image | `Any` | Yes | - |
| `Axis` | Transform Axis (0=row, 1=col) | `Integer` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Spectrum` | Frequency Spectrum | `Any` | - |
| `Magnitude` | Magnitude Spectrum | `Any` | - |
| `Phase` | Phase Spectrum | `Any` | - |
| `Image` | Visualization | `Image` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | `O(n log n)`（1D 信号） |
| 典型耗时 (Typical Latency) | `<= 4 ms`（1024 点 1D 信号，实验室基线，2026-04-15） |
| 内存特征 (Memory Profile) | 频谱与可视化输出额外占用 `O(n)` |

## 适用场景 / Use Cases
- 适合 (Suitable)：周期信号频谱分析、频域滤波链路前置、实验室内 1D 信号快速验算。
- 不适合 (Not Suitable)：需要完整 2D FFT 频域图像处理、需要现场级实时硬约束的长链路。

## 已知限制 / Known Limitations
1. 图像输入当前只支持“按行 / 按列”的 1D FFT，不等同于完整 2D FFT。
2. 幅度可视化主要用于调试和审计，不应直接当作业务输出参与判定。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.0 | 2026-03-18 | 自动生成文档骨架 / Generated skeleton |
