# AKAZE特征匹配 / AkazeFeatureMatch

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `AkazeFeatureMatchOperator` |
| 枚举值 (Enum) | `OperatorType.AkazeFeatureMatch` |
| 分类 (Category) | 匹配定位 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：基于AKAZE特征的鲁棒模板匹配，对光照/旋转/缩放变化具有强鲁棒性。
> English: 基于AKAZE特征的鲁棒模板匹配，对光照/旋转/缩放变化具有强鲁棒性.

## 实现策略 / Implementation Strategy
> 中文：采用“输入校验 -> 参数解析 -> 核心算法执行 -> 结果封装”的统一链路，优先复用 OpenCV 与现有算子基类能力，确保与主项目运行时行为一致。
> English: Uses a consistent pipeline of input validation, parameter parsing, core algorithm execution, and result packaging, reusing OpenCV and existing operator infrastructure for runtime consistency.

## 核心 API 调用链 / Core API Call Chain
- 输入图像与参数校验
- 核心视觉处理链路执行
- 结果图像/结构化结果输出

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `TemplatePath` | `file` | "" | - | - |
| `Threshold` | `double` | 0.001 | [0.0001, 0.1] | - |
| `MinMatchCount` | `int` | 10 | [3, 100] | - |
| `EnableSymmetryTest` | `bool` | true | - | - |
| `MaxFeatures` | `int` | 500 | [100, 2000] | - |

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
| `IsMatch` | 是否匹配 | `Boolean` | - |
| `Score` | 匹配分数 | `Float` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 约 `O(W*H*L)`（与金字塔层数、角度步进有关） |
| 典型耗时 (Typical Latency) | 约 `3-40 ms`（模板大小、搜索范围越大越高） |
| 内存特征 (Memory Profile) | 模板金字塔与相似度图缓存，额外开销约 `O(W*H)` |

## 适用场景 / Use Cases
- 适合 (Suitable)：工件模板匹配、重复定位、姿态估计前置步骤。
- 不适合 (Not Suitable)：目标外观变化剧烈且缺少在线模板更新策略的场景。

## 已知限制 / Known Limitations
1. 当前实现遵循统一输入/输出协议，输入类型、维度或关键字段不符合约定时会返回失败。
2. 大角度/尺度变化下需提高搜索步进或层级，计算成本明显增加。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.5 文档补全 / Completed Phase 2.5 documentation enrichment |
