# CLAHE增强 / ClaheEnhancement

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `ClaheEnhancementOperator` |
| 枚举值 (Enum) | `OperatorType.ClaheEnhancement` |
| 分类 (Category) | 预处理 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：CLAHE 将图像分块后进行限制对比度直方图均衡，通过裁剪阈值抑制噪声过放大并提升局部细节。
> English: CLAHE performs contrast-limited local histogram equalization on tiles to improve local details without over-amplifying noise.

## 实现策略 / Implementation Strategy
> 中文：根据 `ColorSpace` 选择处理路径：灰度直接处理、Lab 处理 L 通道、HSV 处理 V 通道、All 处理全部通道；统一由 `Cv2.CreateCLAHE` 创建算子并在通道拆分后应用。
> English: Branches by `ColorSpace`: Gray direct, Lab-L channel, HSV-V channel, or all channels; uses a single CLAHE object and split/merge workflow.

## 核心 API 调用链 / Core API Call Chain
- `Cv2.CreateCLAHE(clipLimit, tileSize)`
- `Cv2.CvtColor`（`BGR<->Lab` / `BGR<->HSV` / `BGR->GRAY`）
- `Cv2.Split` + `clahe.Apply` + `Cv2.Merge`
- 输出附带 `ClipLimit`、`TileSize`、`ColorSpace`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `ClipLimit` | `double` | 2 | [0, 40] | 对比度限制阈值，防止过度放大噪声 |
| `TileWidth` | `int` | 8 | [2, 64] | - |
| `TileHeight` | `int` | 8 | [2, 64] | - |
| `ColorSpace` | `enum` | Lab | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 图像 | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 增强图像 | `Image` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 约 `O(W*H + Tiles*Bins)` |
| 典型耗时 (Typical Latency) | 约 `1-8 ms`（1920x1080，`All` 通道模式更高） |
| 内存特征 (Memory Profile) | 多通道拆分和合并产生多个临时 `Mat` |

## 适用场景 / Use Cases
- 适合 (Suitable)：局部对比度不足、暗部细节提升、反光/阴影共存场景增强。
- 不适合 (Not Suitable)：噪声很重且未先去噪，或对颜色一致性有极严格约束的流程。

## 已知限制 / Known Limitations
1. 强化参数过大（高 `ClipLimit`、小网格）会显著放大纹理噪声。
2. 代码中读取了 `Channel` 参数但当前分支逻辑未使用该参数。
3. 仅支持单帧增强，不包含跨帧亮度一致性约束。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P1 文档补全 / Completed Phase 2.3 P1 documentation enrichment |