# 极坐标展开 / PolarUnwrap

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `PolarUnwrapOperator` |
| 枚举值 (Enum) | `OperatorType.PolarUnwrap` |
| 分类 (Category) | 图像处理 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：Unwraps annular image regions into rectangular view.。
> English: Unwraps annular image regions into rectangular view..

## 实现策略 / Implementation Strategy
> 中文：TODO：补充实现策略与方案对比。
> English: TODO: Add implementation strategy and alternatives comparison.

## 核心 API 调用链 / Core API Call Chain
- TODO：补充关键 API 调用链

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `CenterX` | `int` | 0 | - | - |
| `CenterY` | `int` | 0 | - | - |
| `InnerRadius` | `int` | 0 | [0, 100000] | - |
| `OuterRadius` | `int` | 100 | [1, 100000] | - |
| `StartAngle` | `double` | 0 | [-3600, 3600] | - |
| `EndAngle` | `double` | 360 | [-3600, 3600] | - |
| `OutputWidth` | `int` | 0 | [0, 20000] | - |
| `UseWarpPolar` | `bool` | true | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | Image | `Image` | Yes | - |
| `Center` | Center | `Point` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | Image | `Image` | - |

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
