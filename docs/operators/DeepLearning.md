# 深度学习 / DeepLearning

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `DeepLearningOperator` |
| 枚举值 (Enum) | `OperatorType.DeepLearning` |
| 分类 (Category) | AI检测 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：AI 深度学习推理，支持 YOLOv5/v6/v8/v11 等模型，用于缺陷检测和目标分类。
> English: AI 深度学习推理，支持 YOLOv5/v6/v8/v11 等模型，用于缺陷检测和目标分类.

## 实现策略 / Implementation Strategy
> 中文：TODO：补充实现策略与方案对比。
> English: TODO: Add implementation strategy and alternatives comparison.

## 核心 API 调用链 / Core API Call Chain
- TODO：补充关键 API 调用链

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `ModelPath` | `file` | "" | - | - |
| `Confidence` | `double` | 0.5 | [0, 1] | - |
| `ModelVersion` | `enum` | Auto | - | - |
| `InputSize` | `int` | 640 | [320, 1280] | - |
| `TargetClasses` | `string` | "" | - | 检测目标类别（逗号分隔，如 person,car），为空则检测所有类别 |
| `LabelFile` | `file` | "" | - | 自定义标签文件路径（每行一个标签），为空则使用COCO 80类或自动查找模型目录下的labels.txt |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 输入图像 | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | - |
| `Defects` | 缺陷列表 | `DetectionList` | - |
| `DefectCount` | 缺陷数量 | `Integer` | - |

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
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |
