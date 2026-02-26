# 图像保存 / ImageSave

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `ImageSaveOperator` |
| 枚举值 (Enum) | `OperatorType.ImageSave` |
| 分类 (Category) | 输出 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：保存检测图像到本地硬盘。
> English: 保存检测图像到本地硬盘.

## 实现策略 / Implementation Strategy
> 中文：采用“配置解析 -> 连接/资源准备 -> 数据编解码 -> 结果状态输出”的实现方式，重点保证超时、异常和重试路径可观测且可控。
> English: Implements a configuration-driven flow of setup, resource/connection handling, payload encode/decode, and status output, with explicit timeout/error/retry handling.

## 核心 API 调用链 / Core API Call Chain
- `Cv2.ImWrite`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Directory` | `string` | C:\ClearVision\NG_Images | - | - |
| `FileNameTemplate` | `string` | NG_{yyyyMMdd_HHmmss}_{Guid}.jpg | - | - |
| `Quality` | `int` | 90 | [1, 100] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 图像 | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `FilePath` | 保存路径 | `String` | - |
| `IsSuccess` | 是否成功 | `Boolean` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 约 `O(N)` 或 `O(1) + I/O` |
| 典型耗时 (Typical Latency) | 本地输出通常 `<3 ms`，外部介质取决于 I/O |
| 内存特征 (Memory Profile) | 以序列化缓冲和输出对象为主，开销较小 |

## 适用场景 / Use Cases
- 适合 (Suitable)：结果落盘、可视化导出、流程终端输出。
- 不适合 (Not Suitable)：高频海量数据无队列控制的直接同步输出场景。

## 已知限制 / Known Limitations
1. 当前实现遵循统一输入/输出协议，输入类型、维度或关键字段不符合约定时会返回失败。
2. 外部存储或设备不可用时会增加失败率，需要重试与降级策略。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.5 文档补全 / Completed Phase 2.5 documentation enrichment |
