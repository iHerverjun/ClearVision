# 梯度形状匹配 / GradientShapeMatch

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `GradientShapeMatchOperator` |
| 枚举值 (Enum) | `OperatorType.GradientShapeMatch` |
| 分类 (Category) | 匹配定位 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：基于梯度方向的形状匹配，支持旋转不变性。
> English: 基于梯度方向的形状匹配，支持旋转不变性.

## 实现策略 / Implementation Strategy
> 中文：TODO：补充实现策略与方案对比。
> English: TODO: Add implementation strategy and alternatives comparison.

## 核心 API 调用链 / Core API Call Chain
- TODO：补充关键 API 调用链

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `TemplatePath` | `file` | "" | - | - |
| `MinScore` | `double` | 80 | [0, 100] | - |
| `AngleRange` | `int` | 180 | [0, 180] | - |
| `AngleStep` | `int` | 1 | [1, 10] | - |
| `MagnitudeThreshold` | `int` | 30 | [0, 255] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 搜索图像 | `Image` | Yes | - |
| `Template` | 模板图像 | `Image` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | - |
| `Position` | 匹配位置 | `Point` | - |
| `Angle` | 旋转角度 | `Float` | - |
| `IsMatch` | 是否匹配 | `Boolean` | - |
| `Score` | 匹配分数 | `Float` | - |

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
