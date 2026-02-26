# 双边滤波 / BilateralFilter

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `BilateralFilterOperator` |
| 枚举值 (Enum) | `OperatorType.BilateralFilter` |
| 分类 (Category) | 预处理 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：双边滤波同时考虑空间距离与像素差异，达到平滑噪声且尽量保留边缘的效果。
> English: Bilateral filtering combines spatial and intensity-domain weights to smooth noise while preserving edges.

## 实现策略 / Implementation Strategy
> 中文：通过 `Diameter`、`SigmaColor`、`SigmaSpace` 控制邻域大小、颜色域权重与空间域权重，直接调用 OpenCV 双边滤波。
> English: Uses `Diameter`, `SigmaColor`, and `SigmaSpace` to control neighborhood and weighting, then executes OpenCV bilateral filtering.

## 核心 API 调用链 / Core API Call Chain
- 参数读取：`Diameter`、`SigmaColor`、`SigmaSpace`
- `Cv2.BilateralFilter(src, dst, diameter, sigmaColor, sigmaSpace)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Diameter` | `int` | 9 | [1, 25] | - |
| `SigmaColor` | `double` | 75 | [1, 255] | - |
| `SigmaSpace` | `double` | 75 | [1, 255] | - |

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
| 时间复杂度 (Time Complexity) | 近似 `O(W*H*D^2)`，`D` 为邻域直径 |
| 典型耗时 (Typical Latency) | 约 `4-30 ms`（1920x1080，参数敏感） |
| 内存特征 (Memory Profile) | 一张输出图，主要瓶颈为计算量而非内存 |

## 适用场景 / Use Cases
- 适合 (Suitable)：需要保边降噪的表面检测、纹理抑制前处理。
- 不适合 (Not Suitable)：严格实时高帧率链路、超高分辨率全图无 ROI 处理。

## 已知限制 / Known Limitations
1. 参数耦合强，`SigmaColor/SigmaSpace` 需要按材质和曝光单独调参。
2. 相比高斯/中值滤波计算代价显著更高。
3. 对大面积强噪声或低对比度细节场景仍可能产生细节损失。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P1 文档补全 / Completed Phase 2.3 P1 documentation enrichment |