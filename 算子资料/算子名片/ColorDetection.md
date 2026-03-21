# 颜色检测 / ColorDetection

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `ColorDetectionOperator` |
| 枚举值 (Enum) | `OperatorType.ColorDetection` |
| 分类 (Category) | 颜色处理 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
该算子结合学习型模型或规则判定完成识别、检测或缺陷筛查。

> English: This section is completed from the current source implementation and focuses on actual runtime behavior in code.

## 实现策略 / Implementation Strategy
- 实现遵循统一算子框架：参数读取、输入检查、核心处理与结果封装相互分离。
- 先校验输入图像与参数，再进入核心处理，避免空输入或非法格式直接进入底层 API。
- 结果通过 `CreateImageOutput(...)` 封装，运行时通常附带 `Width` / `Height` 等基础字段。

## 核心 API 调用链 / Core API Call Chain
1. `TryGetInputImage(...)`
2. `GetStringParam / GetIntParam / GetDoubleParam / GetBoolParam / GetFloatParam`
3. `Cv2.CvtColor`
4. `Cv2.Mean`
5. `Cv2.PutText`
6. `Cv2.Resize`
7. `Cv2.Kmeans`
8. `Cv2.Rectangle`
9. `Cv2.InRange`
10. `Cv2.CountNonZero`
11. `CreateImageOutput(...)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `ColorSpace` | `enum` | `"HSV"` | HSV/HSV；Lab/Lab | 该参数用于在多个实现分支之间切换。 |
| `AnalysisMode` | `enum` | `"Average"` | Average/平均色；Dominant/主色提取；Range/颜色范围检测 | 工作模式选择。 |
| `HueLow` | `int` | `0` | [0, 180] | 控制“HueLow”这一实现参数，建议结合现场样本调节。 |
| `HueHigh` | `int` | `180` | [0, 180] | 控制“HueHigh”这一实现参数，建议结合现场样本调节。 |
| `SatLow` | `int` | `50` | [0, 255] | 控制“SatLow”这一实现参数，建议结合现场样本调节。 |
| `SatHigh` | `int` | `255` | [0, 255] | 控制“SatHigh”这一实现参数，建议结合现场样本调节。 |
| `ValLow` | `int` | `50` | [0, 255] | 控制“ValLow”这一实现参数，建议结合现场样本调节。 |
| `ValHigh` | `int` | `255` | [0, 255] | 控制“ValHigh”这一实现参数，建议结合现场样本调节。 |
| `DominantK` | `int` | `3` | [1, 10] | 最小数量或下限约束。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 输入图像 | `Image` | Yes | 输入待处理图像。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | 输出处理后的结果图像。 |
| `ColorInfo` | 颜色信息 | `Any` | 输出本算子的处理结果。 |
### 运行时附加输出 / Runtime Additional Outputs
| 名称 (Name) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|
| `Width` | `Integer` | 输出图像宽度。 |
| `Height` | `Integer` | 输出图像高度。 |
| `Hue` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |
| `Saturation` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |
| `Value` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |
| `L` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |
| `a` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |
| `b` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |
| `Channel1` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |
| `Channel2` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 主要由模型推理复杂度主导，预处理和后处理成本次之。 |
| 典型耗时 (Typical Latency) | 仓库中未提供固定 benchmark；实际延迟受图像尺寸、参数规模、缓存命中率和外部依赖影响。 |
| 内存特征 (Memory Profile) | 通常需要为中间图像、结果图和输出封装分配额外内存；峰值随图像尺寸和中间副本数量增长。 |

## 适用场景 / Use Cases
- 适合识别、检测、分类和缺陷筛查任务。
- 适合在规则算法不足时作为增强手段。
- 不适合在模型和标签配置不清晰时直接上线。
- 不适合把推理结果当作无条件真值。

## 已知限制 / Known Limitations
1. 当前实现通常以图像作为主要输出载体；若下游只关心数值，还需要同步读取附加字段。
2. 源码若在内部自动转换颜色空间，下游拿到的图像语义可能与原始输入不同。
3. 声明输出 `ColorInfo` 与当前运行时附加字段不完全一致，集成时应以实际输出字典为准。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.2 | 2026-03-14 | 第二轮基于源码深化实现行为、性能与限制说明 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |
