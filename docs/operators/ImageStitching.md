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
该算子基于固定阈值或自动阈值策略把图像分成前景和背景两类，可用于快速分割。

> English: This section is completed from the current source implementation and focuses on actual runtime behavior in code.

## 实现策略 / Implementation Strategy
- 实现遵循统一算子框架：参数读取、输入检查、核心处理与结果封装相互分离。
- 先校验输入图像与参数，再进入核心处理，避免空输入或非法格式直接进入底层 API。
- 结果通过 `CreateImageOutput(...)` 封装，运行时通常附带 `Width` / `Height` 等基础字段。

## 核心 API 调用链 / Core API Call Chain
1. `TryGetInputImage(...)`
2. `GetStringParam / GetIntParam / GetDoubleParam / GetBoolParam / GetFloatParam`
3. `Cv2.CvtColor`
4. `Cv2.FindHomography`
5. `Cv2.WarpPerspective`
6. `Cv2.Threshold`
7. `Cv2.CountNonZero`
8. `ORB.Create`
9. `BFMatcher`
10. `CreateImageOutput(...)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Method` | `enum` | `"FeatureBased"` | FeatureBased/FeatureBased；Manual/Manual | 算法方法选择。 |
| `OverlapPercent` | `double` | `20.0` | [0.0, 90.0] | 控制“OverlapPercent”这一实现参数，建议结合现场样本调节。 |
| `BlendMode` | `enum` | `"Linear"` | Linear/Linear；MultiBand/MultiBand | 工作模式选择。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image1` | Image 1 | `Image` | Yes | 输入待处理图像。 |
| `Image2` | Image 2 | `Image` | Yes | 输入待处理图像。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | Image | `Image` | 输出处理后的结果图像。 |
| `OverlapRatio` | Overlap Ratio | `Float` | 输出本算子的处理结果。 |
### 运行时附加输出 / Runtime Additional Outputs
| 名称 (Name) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|
| `Width` | `Integer` | 输出图像宽度。 |
| `Height` | `Integer` | 输出图像高度。 |
| `stitch` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |
| `OverlapRatio` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |
| `FeatureBased` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 特征提取随像素数增长，匹配阶段与特征点数量乘积相关。 |
| 典型耗时 (Typical Latency) | 仓库中未提供固定 benchmark；实际延迟受图像尺寸、参数规模、缓存命中率和外部依赖影响。 |
| 内存特征 (Memory Profile) | 通常需要为中间图像、结果图和输出封装分配额外内存；峰值随图像尺寸和中间副本数量增长。 |

## 适用场景 / Use Cases
- 适合模板定位、特征匹配和参考图比对。
- 适合目标存在一定位姿变化但模板仍具可比性的任务。
- 不适合弱纹理或重复纹理很强的目标。
- 不适合直接替代精密测量工具。

## 已知限制 / Known Limitations
1. 当前实现通常以图像作为主要输出载体；若下游只关心数值，还需要同步读取附加字段。
2. 源码若在内部自动转换颜色空间，下游拿到的图像语义可能与原始输入不同。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.2 | 2026-03-14 | 第二轮基于源码深化实现行为、性能与限制说明 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |
