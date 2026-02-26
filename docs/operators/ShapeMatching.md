# 形状匹配 / ShapeMatching

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `ShapeMatchingOperator` |
| 枚举值 (Enum) | `OperatorType.ShapeMatching` |
| 分类 (Category) | 匹配定位 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：将搜索图与模板图转灰度并构建图像金字塔，在粗层以较大角步长做旋转模板匹配，逐层细化角度范围；每层使用并行角度搜索 + `MatchTemplate`，最终经 IoU 非极大值抑制输出前 N 个结果。
> English: Convert both images to grayscale, build pyramids, perform coarse-to-fine rotational template matching with parallel angle search per level, then apply IoU-based NMS and return top-N matches.

## 实现策略 / Implementation Strategy
> 中文：通过“金字塔降采样 + 角度分层细化”降低全角度穷举成本，同时保留旋转鲁棒性；NMS 负责去重，提升最终结果可用性。
> English: Pyramid downsampling and hierarchical angle refinement reduce brute-force cost while preserving rotation robustness. NMS removes overlaps for cleaner outputs.

## 核心 API 调用链 / Core API Call Chain
- `Cv2.PyrDown`（多层金字塔）
- `Cv2.GetRotationMatrix2D` + `Cv2.WarpAffine`（模板旋转）
- `Cv2.MatchTemplate` + `Cv2.MinMaxLoc`（并行角度搜索）
- `NonMaximumSuppression` + `CalculateIoU`（结果去重）
- `Cv2.Rectangle` / `Cv2.Circle` / `Cv2.PutText`（结果绘制）

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `TemplatePath` | `file` | "" | - | - |
| `MinScore` | `double` | 0.7 | [0.1, 1] | - |
| `MaxMatches` | `int` | 1 | [1, 50] | - |
| `AngleStart` | `double` | -30 | [-180, 180] | - |
| `AngleExtent` | `double` | 60 | [0, 360] | - |
| `AngleStep` | `double` | 1 | [0.1, 10] | - |
| `NumLevels` | `int` | 3 | [1, 6] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 搜索图像 | `Image` | Yes | - |
| `Template` | 模板图像 | `Image` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | - |
| `Matches` | 匹配结果 | `Any` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | O(Σₗ Aₗ·Mₗ)，Aₗ 为该层角度数，Mₗ 为匹配代价 |
| 典型耗时 (Typical Latency) | ~20-150 ms（与角步长、层数、模板尺寸相关） |
| 内存特征 (Memory Profile) | 双金字塔 + 旋转模板临时 Mat，内存中等偏高 |

## 适用场景 / Use Cases
- 适合 (Suitable)：存在旋转变化的模板定位、姿态估计前置匹配。
- 不适合 (Not Suitable)：大比例缩放、非刚体形变、强重复纹理场景。

## 已知限制 / Known Limitations
1. 缩放鲁棒性受金字塔层级限制，非连续尺度变化仍可能漏检。
2. 计算开销随角度范围和步长显著上升，需结合节拍约束调参。
3. 当前匹配度量固定为 `CCoeffNormed`，未提供多指标融合。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |
| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P0 文档补全 / Completed Phase 2.3 P0 documentation enrichment |
