# 像素统计 / PixelStatistics

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `PixelStatisticsOperator` |
| 枚举值 (Enum) | `OperatorType.PixelStatistics` |
| 分类 (Category) | 检测 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
该算子基于固定阈值或自动阈值策略把图像分成前景和背景两类，可用于快速分割。

> English: This section is completed from the current source implementation and focuses on actual runtime behavior in code.

## 实现策略 / Implementation Strategy
- 实现遵循统一算子框架：参数读取、输入检查、核心处理与结果封装相互分离。
- 先校验输入图像与参数，再进入核心处理，避免空输入或非法格式直接进入底层 API。

## 核心 API 调用链 / Core API Call Chain
1. `TryGetInputImage(...)`
2. `GetStringParam / GetIntParam / GetDoubleParam / GetBoolParam / GetFloatParam`
3. `Cv2.MeanStdDev`
4. `Cv2.MinMaxLoc`
5. `Cv2.BitwiseAnd`
6. `Cv2.CountNonZero`
7. `Cv2.CvtColor`
8. `Cv2.Resize`
9. `Cv2.Threshold`
10. `Cv2.CalcHist`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `RoiX` | `int` | `0` | [0, +∞] | 限定处理区域。 |
| `RoiY` | `int` | `0` | [0, +∞] | 限定处理区域。 |
| `RoiW` | `int` | `0` | [0, +∞] | 限定处理区域。 |
| `RoiH` | `int` | `0` | [0, +∞] | 限定处理区域。 |
| `Channel` | `enum` | `"Gray"` | Gray/Gray；R/R；G/G；B/B；All/All | 该参数用于在多个实现分支之间切换。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | Image | `Image` | Yes | 输入待处理图像。 |
| `Mask` | Mask | `Image` | No | 输入待处理图像。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Mean` | Mean | `Float` | 输出流程数据结果。 |
| `StdDev` | StdDev | `Float` | 输出流程数据结果。 |
| `Min` | Min | `Integer` | 输出流程数据结果。 |
| `Max` | Max | `Integer` | 输出流程数据结果。 |
| `Median` | Median | `Integer` | 输出流程数据结果。 |
| `NonZeroCount` | NonZero Count | `Integer` | 输出流程数据结果。 |
## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 通常为 `O(1)` 或与输入集合长度线性相关。 |
| 典型耗时 (Typical Latency) | 仓库中未提供固定 benchmark；实际延迟受图像尺寸、参数规模、缓存命中率和外部依赖影响。 |
| 内存特征 (Memory Profile) | 主要由中间结果、缓存结构和输出封装决定。 |

## 适用场景 / Use Cases
- 适合做变量整理、条件判断、结果汇总和类型转换。
- 适合把上游复杂输出整理为下游更容易消费的结构。
- 不适合作为图像算法替代品。
- 不适合承载大量高频大对象搬运。

## 已知限制 / Known Limitations
1. 源码若在内部自动转换颜色空间，下游拿到的图像语义可能与原始输入不同。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.2 | 2026-03-14 | 第二轮基于源码深化实现行为、性能与限制说明 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |
