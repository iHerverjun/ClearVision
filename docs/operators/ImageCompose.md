# 图像组合 / ImageCompose

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `ImageComposeOperator` |
| 枚举值 (Enum) | `OperatorType.ImageCompose` |
| 分类 (Category) | 拆分组合 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：Composes multiple images by concat/grid/channel merge.。
> English: Composes multiple images by concat/grid/channel merge..

## 实现策略 / Implementation Strategy
> 中文：采用“输入规范化 -> 业务规则处理 -> 输出结构化”的实现策略，保持参数可配置并兼容现有流程上下文。
> English: Uses an input-normalization, rule-processing, and structured-output strategy with configurable parameters and compatibility with the existing workflow context.

## 核心 API 调用链 / Core API Call Chain
- `Cv2.Merge`
- `Cv2.CvtColor`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Mode` | `enum` | Horizontal | - | - |
| `Padding` | `int` | 0 | [0, 1000] | - |
| `BackgroundColor` | `string` | #000000 | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image1` | Image 1 | `Image` | Yes | - |
| `Image2` | Image 2 | `Image` | Yes | - |
| `Image3` | Image 3 | `Image` | No | - |
| `Image4` | Image 4 | `Image` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | Image | `Image` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 约 `O(N)` |
| 典型耗时 (Typical Latency) | 通常 `<5 ms`（不含外部 I/O） |
| 内存特征 (Memory Profile) | 额外缓存随输入规模线性增长 |

## 适用场景 / Use Cases
- 适合 (Suitable)：通用视觉流程中的基础节点与数据编排。
- 不适合 (Not Suitable)：超大规模离线计算或对吞吐有极限要求的场景。

## 已知限制 / Known Limitations
1. 当前实现遵循统一输入/输出协议，输入类型、维度或关键字段不符合约定时会返回失败。
2. 参数与输入分布变化会影响稳定性，需结合现场数据持续校准。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.5 文档补全 / Completed Phase 2.5 documentation enrichment |
