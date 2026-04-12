# 边线交点 / EdgeIntersection

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `EdgeIntersectionOperator` |
| 枚举值 (Enum) | `OperatorType.EdgeIntersection` |
| 分类 (Category) | 定位 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。

> English: This section is completed from the current source implementation and focuses on actual runtime behavior in code.

## 实现策略 / Implementation Strategy
- 实现遵循统一算子框架：参数读取、输入检查、核心处理与结果封装相互分离。

## 核心 API 调用链 / Core API Call Chain


## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|


## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Line1` | Line 1 | `LineData` | Yes | 提供线几何输入。 |
| `Line2` | Line 2 | `LineData` | Yes | 提供线几何输入。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Point` | Point | `Point` | 输出点位结果。 |
| `Angle` | Angle | `Float` | 输出本算子的处理结果。 |
| `HasIntersection` | Has Intersection | `Boolean` | 输出本算子的处理结果。 |
## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 多数路径近似随输入规模线性增长。 |
| 典型耗时 (Typical Latency) | 仓库中未提供固定 benchmark；实际延迟受图像尺寸、参数规模、缓存命中率和外部依赖影响。 |
| 内存特征 (Memory Profile) | 主要由中间结果、缓存结构和输出封装决定。 |

## 适用场景 / Use Cases
- 适合尺寸、角度、间距和几何位置测量。
- 适合同时输出数值结果和可视化结果图。
- 不适合在边缘模糊或对比度不足时直接追求高精度。
- 不适合忽略标定比例和亚像素能力对精度的影响。

## 已知限制 / Known Limitations


## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.2 | 2026-03-14 | 第二轮基于源码深化实现行为、性能与限制说明 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |
