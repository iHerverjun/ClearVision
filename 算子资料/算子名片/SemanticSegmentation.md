# 语义分割 / SemanticSegmentation

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `SemanticSegmentationOperator` |
| 枚举值 (Enum) | `OperatorType.SemanticSegmentation` |
| 分类 (Category) | AI检测 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：Runs an ONNX semantic segmentation model and returns class map, colored visualization, and per-class masks.。
> English: Runs an ONNX semantic segmentation model and returns class map, colored visualization, and per-class masks..

## 实现策略 / Implementation Strategy
> 中文：TODO：补充实现策略与方案对比。
> English: TODO: Add implementation strategy and alternatives comparison.

## 核心 API 调用链 / Core API Call Chain
- TODO：补充关键 API 调用链

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `ModelId` | `string` | "" | - | - |
| `ModelCatalogPath` | `file` | "" | - | - |
| `ModelPath` | `file` | "" | - | - |
| `InputSize` | `string` | 512,512 | - | Width,Height |
| `NumClasses` | `int` | 21 | [2, 4096] | - |
| `ClassNames` | `string` | "" | - | JSON array or comma-separated names |
| `ExecutionProvider` | `enum` | cpu | - | - |
| `ScaleToUnitRange` | `bool` | true | - | - |
| `ChannelOrder` | `enum` | RGB | - | - |
| `Mean` | `string` | 0,0,0 | - | - |
| `Std` | `string` | 1,1,1 | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | Image | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `SegmentationMap` | Segmentation Map | `Image` | - |
| `ColoredMap` | Colored Map | `Image` | - |
| `ClassMasks` | Class Masks | `Any` | - |
| `ClassCount` | Class Count | `Integer` | - |
| `PresentClasses` | Present Classes | `Any` | - |

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
