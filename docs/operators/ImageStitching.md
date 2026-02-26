# 图像拼接 / ImageStitching

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `ImageStitchingOperator` |
| 枚举值 (Enum) | `OperatorType.ImageStitching` |
| 分类 (Category) | 图像处理 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：Stitches two images into a larger panorama-like output.。
> English: Stitches two images into a larger panorama-like output..

## 实现策略 / Implementation Strategy
> 中文：采用“输入校验 -> 参数解析 -> 核心算法执行 -> 结果封装”的统一链路，优先复用 OpenCV 与现有算子基类能力，确保与主项目运行时行为一致。
> English: Uses a consistent pipeline of input validation, parameter parsing, core algorithm execution, and result packaging, reusing OpenCV and existing operator infrastructure for runtime consistency.

## 核心 API 调用链 / Core API Call Chain
- `Cv2.CvtColor`
- `Cv2.FindHomography`
- `Cv2.WarpPerspective`
- `Cv2.Threshold`
- `Cv2.CountNonZero`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Method` | `enum` | FeatureBased | - | - |
| `OverlapPercent` | `double` | 20 | [0, 90] | - |
| `BlendMode` | `enum` | Linear | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image1` | Image 1 | `Image` | Yes | - |
| `Image2` | Image 2 | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | Image | `Image` | - |
| `OverlapRatio` | Overlap Ratio | `Float` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 约 `O(W*H)` |
| 典型耗时 (Typical Latency) | 约 `0.8-12 ms`（1920x1080） |
| 内存特征 (Memory Profile) | 几何变换与重采样会创建中间图，额外开销约 `O(W*H)` |

## 适用场景 / Use Cases
- 适合 (Suitable)：坐标变换、图像拼接、边界扩展等图像重构流程。
- 不适合 (Not Suitable)：对亚像素几何误差极敏感且缺少标定约束的场景。

## 已知限制 / Known Limitations
1. 当前实现遵循统一输入/输出协议，输入类型、维度或关键字段不符合约定时会返回失败。
2. 插值与边界策略会引入轻微失真，需按任务选择插值方式。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.5 文档补全 / Completed Phase 2.5 documentation enrichment |
