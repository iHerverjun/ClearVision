# 模板匹配 / TemplateMatch

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `TemplateMatchOperator` |
| 枚举值 (Enum) | `OperatorType.TemplateMatching` |
| 分类 (Category) | 匹配定位 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：Classic grayscale template matching for fixed-scale, low-rotation scenes. Multi-match outputs are filtered by IoU-based NMS.。
> English: Classic grayscale template matching for fixed-scale, low-rotation scenes. Multi-match outputs are filtered by IoU-based NMS..

## 实现策略 / Implementation Strategy
> 中文：TODO：补充实现策略与方案对比。
> English: TODO: Add implementation strategy and alternatives comparison.

## 核心 API 调用链 / Core API Call Chain
- TODO：补充关键 API 调用链

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Method` | `enum` | CCoeffNormed | - | - |
| `Domain` | `enum` | Gray | - | - |
| `Threshold` | `double` | 0.8 | [0, 1] | - |
| `MaxMatches` | `int` | 1 | [1, 100] | - |
| `UseRoi` | `bool` | false | - | - |
| `RoiX` | `int` | 0 | >= 0 | - |
| `RoiY` | `int` | 0 | >= 0 | - |
| `RoiWidth` | `int` | 0 | >= 0 | - |
| `RoiHeight` | `int` | 0 | >= 0 | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 杈撳叆鍥惧儚 | `Image` | Yes | - |
| `Template` | 妯℃澘鍥惧儚 | `Image` | Yes | - |
| `Mask` | 搜索掩膜 | `Image` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 缁撴灉鍥惧儚 | `Image` | - |
| `Position` | 鍖归厤浣嶇疆 | `Point` | - |
| `Score` | 鍖归厤鍒嗘暟 | `Float` | - |
| `IsMatch` | 鏄惁鍖归厤 | `Boolean` | - |
| `Matches` | 鍖归厤鍒楄〃 | `Any` | - |
| `MatchCount` | 鍖归厤鏁伴噺 | `Integer` | - |

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
| 1.1.1 | 2026-04-09 | 自动生成文档骨架 / Generated skeleton |
