# 点位对齐 / PointAlignment

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `PointAlignmentOperator` |
| 枚举值 (Enum) | `OperatorType.PointAlignment` |
| 分类 (Category) | 数据处理 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：Computes offset and distance between current and reference points.。
> English: Computes offset and distance between current and reference points..

## 实现策略 / Implementation Strategy
> 中文：采用“输入规范化 -> 业务规则处理 -> 输出结构化”的实现策略，保持参数可配置并兼容现有流程上下文。
> English: Uses an input-normalization, rule-processing, and structured-output strategy with configurable parameters and compatibility with the existing workflow context.

## 核心 API 调用链 / Core API Call Chain
- 输入数据解析与类型规范化
- 规则计算/逻辑执行
- 输出结果封装与上下文回写

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `OutputUnit` | `enum` | Pixel | - | - |
| `PixelSize` | `double` | 1 | [1E-09, 1000000] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `CurrentPoint` | Current Point | `Point` | Yes | - |
| `ReferencePoint` | Reference Point | `Point` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `OffsetX` | Offset X | `Float` | - |
| `OffsetY` | Offset Y | `Float` | - |
| `Distance` | Distance | `Float` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 约 `O(N)`（N 为元素或记录条目数） |
| 典型耗时 (Typical Latency) | 约 `0.1-5 ms`（数据量增大时线性上升） |
| 内存特征 (Memory Profile) | 列表/字典转换与中间对象创建，额外开销约 `O(N)` |

## 适用场景 / Use Cases
- 适合 (Suitable)：结果清洗、结构转换、聚合统计、单位换算。
- 不适合 (Not Suitable)：超大规模离线数据计算或需要分布式计算的场景。

## 已知限制 / Known Limitations
1. 当前实现遵循统一输入/输出协议，输入类型、维度或关键字段不符合约定时会返回失败。
2. 输入结构多态时需严格字段校验，避免隐式类型转换误差。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.5 文档补全 / Completed Phase 2.5 documentation enrichment |
