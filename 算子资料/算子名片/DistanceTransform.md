# Distance Transform / DistanceTransform

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `DistanceTransformOperator` |
| 枚举值 (Enum) | `OperatorType.DistanceTransform` |
| 分类 (Category) | Analysis |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：Computes the distance from each pixel to the nearest zero pixel. Supports multiple distance metrics and signed distances.。
> English: Computes the distance from each pixel to the nearest zero pixel. Supports multiple distance metrics and signed distances..

## 实现策略 / Implementation Strategy
> 中文：TODO：补充实现策略与方案对比。
> English: TODO: Add implementation strategy and alternatives comparison.

## 核心 API 调用链 / Core API Call Chain
- TODO：补充关键 API 调用链

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `DistanceType` | `enum` | Euclidean | - | - |
| `MaskSize` | `int` | 5 | [3, 7] | - |
| `Signed` | `bool` | false | - | - |
| `Threshold` | `double` | 127 | [0, 255] | - |
| `Invert` | `bool` | false | - | - |
| `Normalize` | `bool` | false | - | - |
| `MaxDistanceLimit` | `double` | 0 | [0, 10000] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | Input Image (Binary or Grayscale) | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | Distance Transform Result | `Image` | - |
| `DistanceMap` | Distance Map (Float) | `Any` | - |
| `MaxDistance` | Maximum Distance | `Float` | - |
| `MaxLocation` | Maximum Distance Location | `Point` | - |

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
| 1.0.0 | 2026-04-07 | 自动生成文档骨架 / Generated skeleton |
