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
> 中文：角度偏差测量（仅角度模型，非完整GD&T公差带）。
> English: 角度偏差测量（仅角度模型，非完整GD&T公差带）.

## 实现策略 / Implementation Strategy
> 中文：TODO：补充实现策略与方案对比。
> English: TODO: Add implementation strategy and alternatives comparison.

## 核心 API 调用链 / Core API Call Chain
- TODO：补充关键 API 调用链

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `MeasureType` | `enum` | Parallelism | - | - |
| `Line1_X1` | `int` | 0 | - | - |
| `Line1_Y1` | `int` | 0 | - | - |
| `Line1_X2` | `int` | 100 | - | - |
| `Line1_Y2` | `int` | 100 | - | - |
| `Line2_X1` | `int` | 0 | - | - |
| `Line2_Y1` | `int` | 200 | - | - |
| `Line2_X2` | `int` | 100 | - | - |
| `Line2_Y2` | `int` | 200 | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 输入图像 | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | - |
| `Tolerance` | 角度偏差 | `Float` | - |
| `AngularDeviationDeg` | 角度偏差(度) | `Float` | - |
| `LinearBand` | 线性跳动带(像素) | `Float` | - |
| `MeasurementModel` | 测量模型 | `String` | - |

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
| 1.0.0 | 2026-04-09 | 自动生成文档骨架 / Generated skeleton |
