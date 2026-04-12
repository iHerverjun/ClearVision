# 卡尺工具 / CaliperTool

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `CaliperToolOperator` |
| 枚举值 (Enum) | `OperatorType.CaliperTool` |
| 分类 (Category) | 检测 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。

> English: This section is completed from the current source implementation and focuses on actual runtime behavior in code.

## 实现策略 / Implementation Strategy
- 实现遵循统一算子框架：参数读取、输入检查、核心处理与结果封装相互分离。
- 先校验输入图像与参数，再进入核心处理，避免空输入或非法格式直接进入底层 API。
- 结果通过 `CreateImageOutput(...)` 封装，运行时通常附带 `Width` / `Height` 等基础字段。

## 核心 API 调用链 / Core API Call Chain
1. `TryGetInputImage(...)`
2. `GetStringParam / GetIntParam / GetDoubleParam / GetBoolParam / GetFloatParam`
3. `Cv2.CvtColor`
4. `Cv2.Line`
5. `Cv2.Circle`
6. `Cv2.PutText`
7. `CreateImageOutput(...)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Direction` | `enum` | `"Horizontal"` | Horizontal/Horizontal；Vertical/Vertical；Custom/Custom | 该参数用于在多个实现分支之间切换。 |
| `Angle` | `double` | `0.0` | [-180.0, 180.0] | 角度参数。 |
| `Polarity` | `enum` | `"Both"` | DarkToLight/DarkToLight；LightToDark/LightToDark；Both/Both | 该参数用于在多个实现分支之间切换。 |
| `EdgeThreshold` | `double` | `18.0` | [1.0, 255.0] | 用于判定、分割或筛选的阈值。 |
| `ExpectedCount` | `int` | `1` | [1, 100] | 控制“ExpectedCount”这一实现参数，建议结合现场样本调节。 |
| `SubpixelAccuracy` | `bool` | `false` | - | 控制“SubpixelAccuracy”这一实现参数，建议结合现场样本调节。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | Image | `Image` | Yes | 输入待处理图像。 |
| `SearchRegion` | Search Region | `Rectangle` | No | 提供算法执行所需输入。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | Image | `Image` | 输出处理后的结果图像。 |
| `Width` | Width | `Float` | 输出本算子的处理结果。 |
| `EdgePairs` | Edge Pairs | `PointList` | 输出本算子的处理结果。 |
| `PairCount` | Pair Count | `Integer` | 输出本算子的处理结果。 |
### 运行时附加输出 / Runtime Additional Outputs
| 名称 (Name) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|
| `Width` | `Integer` | 输出图像宽度。 |
| `Height` | `Integer` | 输出图像高度。 |
| `caliper` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |
| `EdgePairs` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |
| `PairCount` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |
| `Horizontal` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |
| `DarkToLight` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 多数路径近似随输入规模线性增长。 |
| 典型耗时 (Typical Latency) | 仓库中未提供固定 benchmark；实际延迟受图像尺寸、参数规模、缓存命中率和外部依赖影响。 |
| 内存特征 (Memory Profile) | 通常需要为中间图像、结果图和输出封装分配额外内存；峰值随图像尺寸和中间副本数量增长。 |

## 适用场景 / Use Cases
- 适合尺寸、角度、间距和几何位置测量。
- 适合同时输出数值结果和可视化结果图。
- 不适合在边缘模糊或对比度不足时直接追求高精度。
- 不适合忽略标定比例和亚像素能力对精度的影响。

## 已知限制 / Known Limitations
1. 当前实现通常以图像作为主要输出载体；若下游只关心数值，还需要同步读取附加字段。
2. 源码若在内部自动转换颜色空间，下游拿到的图像语义可能与原始输入不同。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.2 | 2026-03-14 | 第二轮基于源码深化实现行为、性能与限制说明 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |
