# 几何公差 / GeometricTolerance

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `GeometricToleranceOperator` |
| 枚举值 (Enum) | `OperatorType.GeometricTolerance` |
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
3. `Cv2.PutText`
4. `Cv2.Line`
5. `Cv2.Circle`
6. `CreateImageOutput(...)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `MeasureType` | `enum` | `"Parallelism"` | Parallelism/平行度；Perpendicularity/垂直度 | 操作类型或转换类型。 |
| `Line1_X1` | `int` | `0` | - | 控制“Line1_X1”这一实现参数，建议结合现场样本调节。 |
| `Line1_Y1` | `int` | `0` | - | 控制“Line1_Y1”这一实现参数，建议结合现场样本调节。 |
| `Line1_X2` | `int` | `100` | - | 控制“Line1_X2”这一实现参数，建议结合现场样本调节。 |
| `Line1_Y2` | `int` | `100` | - | 控制“Line1_Y2”这一实现参数，建议结合现场样本调节。 |
| `Line2_X1` | `int` | `0` | - | 控制“Line2_X1”这一实现参数，建议结合现场样本调节。 |
| `Line2_Y1` | `int` | `200` | - | 控制“Line2_Y1”这一实现参数，建议结合现场样本调节。 |
| `Line2_X2` | `int` | `100` | - | 控制“Line2_X2”这一实现参数，建议结合现场样本调节。 |
| `Line2_Y2` | `int` | `200` | - | 控制“Line2_Y2”这一实现参数，建议结合现场样本调节。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 输入图像 | `Image` | Yes | 输入待处理图像。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | 输出处理后的结果图像。 |
| `Tolerance` | 角度偏差 | `Float` | 输出本算子的处理结果。 |
| `AngularDeviationDeg` | 角度偏差(度) | `Float` | 输出本算子的处理结果。 |
| `LinearBand` | 线性跳动带(像素) | `Float` | 输出线几何结果。 |
| `MeasurementModel` | 测量模型 | `String` | 输出本算子的处理结果。 |
### 运行时附加输出 / Runtime Additional Outputs
| 名称 (Name) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|
| `Width` | `Integer` | 输出图像宽度。 |
| `Height` | `Integer` | 输出图像高度。 |
| `Tolerance` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |
| `AngularDeviationDeg` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |
| `LinearBand` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |
| `MeasureType` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |
| `MeasurementModel` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |
| `Result` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |

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

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.2 | 2026-03-14 | 第二轮基于源码深化实现行为、性能与限制说明 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |
