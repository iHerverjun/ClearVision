# 测量 / MeasureDistance

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `MeasureDistanceOperator` |
| 枚举值 (Enum) | `OperatorType.Measurement` |
| 分类 (Category) | 检测 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：两点/水平/垂直距离测量，支持图像坐标和 Point 输入两种模式，用于尺寸检测。
> English: 两点/水平/垂直距离测量，支持图像坐标和 Point 输入两种模式，用于尺寸检测.

## 实现策略 / Implementation Strategy
> 中文：TODO：补充实现策略与方案对比。
> English: TODO: Add implementation strategy and alternatives comparison.

## 核心 API 调用链 / Core API Call Chain
- TODO：补充关键 API 调用链

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `X1` | `int` | 0 | - | - |
| `Y1` | `int` | 0 | - | - |
| `X2` | `int` | 100 | - | - |
| `Y2` | `int` | 100 | - | - |
| `MeasureType` | `enum` | PointToPoint | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 输入图像 | `Image` | No | - |
| `PointA` | 起点 | `Point` | No | - |
| `PointB` | 终点 | `Point` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | - |
| `Distance` | 测量距离 | `Float` | - |

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
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |
