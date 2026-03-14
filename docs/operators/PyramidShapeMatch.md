# 金字塔形状匹配 / PyramidShapeMatch

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `PyramidShapeMatchOperator` |
| 枚举值 (Enum) | `OperatorType.PyramidShapeMatch` |
| 分类 (Category) | 匹配定位 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
该算子围绕模板、特征或几何相似性执行定位匹配，用于判断目标是否存在以及位姿大致位置。

> English: This section is completed from the current source implementation and focuses on actual runtime behavior in code.

## 实现策略 / Implementation Strategy
- 实现遵循统一算子框架：参数读取、输入检查、核心处理与结果封装相互分离。
- 先校验输入图像与参数，再进入核心处理，避免空输入或非法格式直接进入底层 API。
- 结果通过 `CreateImageOutput(...)` 封装，运行时通常附带 `Width` / `Height` 等基础字段。
- 实现中存在状态缓存或共享资源，使用时需要关注实例生命周期、缓存一致性与并发访问。
- 源码显式引入并行或后台 CPU 工作分支，以降低主线程阻塞或提升大批量搜索性能。

## 核心 API 调用链 / Core API Call Chain
1. `TryGetInputImage(...)`
2. `GetStringParam / GetIntParam / GetDoubleParam / GetBoolParam / GetFloatParam`
3. `Cv2.ImRead`
4. `Cv2.Rectangle`
5. `Cv2.DrawMarker`
6. `Cv2.PutText`
7. `File.Exists`
8. `CreateImageOutput(...)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `TemplatePath` | `file` | `""` | - | 文件或资源路径。 |
| `MinScore` | `double` | `80.0` | [0.0, 100.0] | 最小数量或下限约束。 |
| `AngleRange` | `int` | `180` | [0, 180] | 角度参数。 |
| `PyramidLevels` | `int` | `3` | [1, 5] | 控制“PyramidLevels”这一实现参数，建议结合现场样本调节。 |
| `MagnitudeThreshold` | `int` | `30` | [0, 255] | 用于判定、分割或筛选的阈值。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 搜索图像 | `Image` | Yes | 输入待处理图像。 |
| `Template` | 模板图像 | `Image` | No | 输入待处理图像。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | 输出处理后的结果图像。 |
| `Position` | 匹配位置 | `Point` | 输出点位结果。 |
| `Angle` | 旋转角度 | `Float` | 输出本算子的处理结果。 |
| `IsMatch` | 是否匹配 | `Boolean` | 输出本算子的处理结果。 |
| `Score` | 匹配分数 | `Float` | 输出本算子的处理结果。 |
### 运行时附加输出 / Runtime Additional Outputs
| 名称 (Name) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|
| `Width` | `Integer` | 输出图像宽度。 |
| `Height` | `Integer` | 输出图像高度。 |
| `IsMatch` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |
| `Score` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |
| `X` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |
| `Y` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |
| `Angle` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |
| `MatchCount` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |
| `PyramidLevels` | `Auto` | 当前实现中的运行时附加字段，具体语义以源码输出逻辑为准。 |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 多数路径近似随输入规模线性增长。 |
| 典型耗时 (Typical Latency) | 仓库中未提供固定 benchmark；实际延迟受图像尺寸、参数规模、缓存命中率和外部依赖影响。 |
| 内存特征 (Memory Profile) | 通常需要为中间图像、结果图和输出封装分配额外内存；峰值随图像尺寸和中间副本数量增长。 |

## 适用场景 / Use Cases
- 适合模板定位、特征匹配和参考图比对。
- 适合目标存在一定位姿变化但模板仍具可比性的任务。
- 不适合弱纹理或重复纹理很强的目标。
- 不适合直接替代精密测量工具。

## 已知限制 / Known Limitations
1. 当前实现通常以图像作为主要输出载体；若下游只关心数值，还需要同步读取附加字段。
2. 实现包含缓存或内部状态时，需要关注实例共享、并发访问和生命周期管理。
3. 参数 `MagnitudeThreshold` 已在元数据中声明，但从源码看当前没有明显被执行逻辑实际使用。
4. 声明输出 `Position` 与当前运行时附加字段不完全一致，集成时应以实际输出字典为准。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.2 | 2026-03-14 | 第二轮基于源码深化实现行为、性能与限制说明 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |
