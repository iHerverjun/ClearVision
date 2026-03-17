# GLCM Texture Features / GlcmTexture

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `GlcmTextureOperator` |
| 枚举值 (Enum) | `OperatorType.GlcmTexture` |
| 分类 (Category) | Texture |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：Compute Gray-Level Co-occurrence Matrix (GLCM) texture features.。
> English: Compute Gray-Level Co-occurrence Matrix (GLCM) texture features..

## 实现策略 / Implementation Strategy
> 中文：TODO：补充实现策略与方案对比。
> English: TODO: Add implementation strategy and alternatives comparison.

## 核心 API 调用链 / Core API Call Chain
- TODO：补充关键 API 调用链

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Levels` | `int` | 16 | [2, 256] | - |
| `Distance` | `int` | 1 | [1, 64] | - |
| `DirectionsDeg` | `string` | 0,45,90,135 | - | - |
| `Symmetric` | `bool` | true | - | - |
| `Normalize` | `bool` | true | - | - |
| `RoiX` | `int` | 0 | >= 0 | - |
| `RoiY` | `int` | 0 | >= 0 | - |
| `RoiW` | `int` | 0 | >= 0 | - |
| `RoiH` | `int` | 0 | >= 0 | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | Image | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Contrast` | Contrast | `Float` | - |
| `Correlation` | Correlation | `Float` | - |
| `Energy` | Energy | `Float` | - |
| `Homogeneity` | Homogeneity | `Float` | - |
| `Entropy` | Entropy | `Float` | - |
| `PerDirection` | Per Direction Features | `Any` | - |

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
| 1.0.0 | 2026-03-17 | 自动生成文档骨架 / Generated skeleton |
