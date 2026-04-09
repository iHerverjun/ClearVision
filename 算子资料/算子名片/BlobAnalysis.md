# Blob分析 / BlobDetection

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `BlobDetectionOperator` |
| 枚举值 (Enum) | `OperatorType.BlobAnalysis` |
| 分类 (Category) | 特征提取 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：连通区域分析。
> English: 连通区域分析.

## 实现策略 / Implementation Strategy
> 中文：TODO：补充实现策略与方案对比。
> English: TODO: Add implementation strategy and alternatives comparison.

## 核心 API 调用链 / Core API Call Chain
- TODO：补充关键 API 调用链

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `MinArea` | `int` | 100 | >= 0 | - |
| `MaxArea` | `int` | 100000 | >= 0 | - |
| `Color` | `enum` | White | - | - |
| `MinCircularity` | `double` | 0 | [0, 1] | - |
| `MinConvexity` | `double` | 0 | [0, 1] | - |
| `MinInertiaRatio` | `double` | 0 | [0, 1] | - |
| `MinRectangularity` | `double` | 0 | [0, 1] | - |
| `MinEccentricity` | `double` | 0 | [0, 1] | - |
| `OutputDetailedFeatures` | `bool` | false | - | - |
| `FeatureFilter` | `string` | "" | - | - |
| `EnableColorFilter` | `bool` | false | - | 启用HSV颜色范围预过滤 |
| `HueLow` | `int` | 0 | [0, 180] | - |
| `HueHigh` | `int` | 180 | [0, 180] | - |
| `SatLow` | `int` | 50 | [0, 255] | - |
| `SatHigh` | `int` | 255 | [0, 255] | - |
| `ValLow` | `int` | 50 | [0, 255] | - |
| `ValHigh` | `int` | 255 | [0, 255] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 图像 | `Image` | Yes | - |
| `SourceImage` | Source Image | `Image` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 标记图像 | `Image` | - |
| `Blobs` | Blob数据 | `Contour` | - |
| `BlobFeatures` | Blob特征 | `Any` | - |
| `BlobCount` | Blob数量 | `Integer` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | O(?) |
| 典型耗时 (Typical Latency) | ~?ms (1920x1080) |
| 内存特征 (Memory Profile) | ? |

## 适用场景 / Use Cases
- 适合 (Suitable)：TODO
- 不适合 (Not Suitable)：TODO

## 已知限制 / Known Limitations
1. TODO

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.1.0 | 2026-04-09 | 自动生成文档骨架 / Generated skeleton |
