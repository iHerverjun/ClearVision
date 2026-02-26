# 仿射变换 / AffineTransform

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `AffineTransformOperator` |
| 枚举值 (Enum) | `OperatorType.AffineTransform` |
| 分类 (Category) | 图像处理 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：执行二维仿射变换，支持三点求矩阵（`GetAffineTransform`）或“旋转+缩放+平移”模式（`GetRotationMatrix2D` 后叠加平移）。
> English: Performs 2D affine transform either from three point pairs or from rotate-scale-translate parameterization.

## 实现策略 / Implementation Strategy
> 中文：输出尺寸默认沿用输入图像尺寸；`ThreePoint` 模式解析 JSON 点数组并取前 3 点，另一模式以图像中心为旋转中心构建矩阵，再统一调用 `WarpAffine`。
> English: Output size falls back to source size; `ThreePoint` mode parses JSON points and uses first three pairs, while RST mode builds matrix around image center and then runs `WarpAffine`.

## 核心 API 调用链 / Core API Call Chain
- `JsonDocument.Parse` + `TryParsePointArray`（三点模式参数解析）
- `Cv2.GetAffineTransform`（三点矩阵）
- `Cv2.GetRotationMatrix2D` + `Mat.Set`（旋转缩放平移矩阵）
- `Cv2.WarpAffine`（图像重采样）

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Mode` | `enum` | RotateScaleTranslate | - | - |
| `SrcPoints` | `string` | [[0,0],[100,0],[0,100]] | - | - |
| `DstPoints` | `string` | [[0,0],[100,0],[0,100]] | - | - |
| `Angle` | `double` | 0 | [-3600, 3600] | - |
| `Scale` | `double` | 1 | [0.001, 1000] | - |
| `TranslateX` | `double` | 0 | [-100000, 100000] | - |
| `TranslateY` | `double` | 0 | [-100000, 100000] | - |
| `OutputWidth` | `int` | 0 | [0, 10000] | - |
| `OutputHeight` | `int` | 0 | [0, 10000] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | Image | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | Image | `Image` | - |
| `TransformMatrix` | Transform Matrix | `Any` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 矩阵构建 `O(1)`，图像变换 `O(W*H)` |
| 典型耗时 (Typical Latency) | 约 `1-6 ms`（1920x1080，线性插值，取决于输出尺寸） |
| 内存特征 (Memory Profile) | 一张输出图 + 一个 `2x3` 变换矩阵 |

## 适用场景 / Use Cases
- 适合 (Suitable)：图像旋转纠偏、尺度归一、平移对齐、通过 3 点完成小范围几何配准。
- 不适合 (Not Suitable)：存在明显透视畸变或需要亚像素级鲁棒配准（含外点）的场景。

## 已知限制 / Known Limitations
1. 三点模式仅使用前 3 组点，额外点不会用于最小二乘优化。
2. 边界模式固定为黑色常量填充，未暴露反射/复制等边界策略。
3. 输出尺寸不合理时会造成裁切或大量空白，需由流程侧显式配置。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P1 文档补全 / Completed Phase 2.3 P1 documentation enrichment |