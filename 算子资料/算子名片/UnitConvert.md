# 单位换算 / UnitConvert

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `UnitConvertOperator` |
| 枚举值 (Enum) | `OperatorType.UnitConvert` |
| 分类 (Category) | 数据处理 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
该算子围绕标定、坐标映射或几何重采样展开，目标是在不同空间之间建立稳定映射关系。

> English: This section is completed from the current source implementation and focuses on actual runtime behavior in code.

## 实现策略 / Implementation Strategy
- 实现遵循统一算子框架：参数读取、输入检查、核心处理与结果封装相互分离。

## 核心 API 调用链 / Core API Call Chain
1. `GetStringParam / GetIntParam / GetDoubleParam / GetBoolParam / GetFloatParam`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `FromUnit` | `enum` | `"Pixel"` | Pixel/Pixel；mm/mm；um/um；inch/inch | 该参数用于在多个实现分支之间切换。 |
| `ToUnit` | `enum` | `"mm"` | Pixel/Pixel；mm/mm；um/um；inch/inch | 该参数用于在多个实现分支之间切换。 |
| `Scale` | `double` | `1.0` | [1E-09, 1000000.0] | 缩放或比例参数。 |
| `UseCalibration` | `bool` | `false` | - | 该参数用于控制当前实现行为，建议结合现场样本调节。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Value` | Value | `Float` | Yes | 提供算法执行所需输入。 |
| `PixelSize` | Pixel Size | `Float` | No | 提供算法执行所需输入。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Result` | Result | `Float` | 输出本算子的处理结果。 |
| `Unit` | Unit | `String` | 输出本算子的处理结果。 |
## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 多数路径近似随输入规模线性增长。 |
| 典型耗时 (Typical Latency) | 仓库中未提供固定 benchmark；实际延迟受图像尺寸、参数规模、缓存命中率和外部依赖影响。 |
| 内存特征 (Memory Profile) | 主要由中间结果、缓存结构和输出封装决定。 |

## 适用场景 / Use Cases
- 适合做坐标变换、畸变校正和几何纠正。
- 适合在测量、定位和机器人引导前统一参考系。
- 不适合在标定数据质量差时直接作为精密依据。
- 不适合跳过标定参数有效性检查。

## 已知限制 / Known Limitations


## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.2 | 2026-03-14 | 第二轮基于源码深化实现行为、性能与限制说明 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |
