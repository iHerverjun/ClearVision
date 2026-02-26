# 条码识别 / CodeRecognition

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `CodeRecognitionOperator` |
| 枚举值 (Enum) | `OperatorType.CodeRecognition` |
| 分类 (Category) | 识别 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：一维码/二维码识别，支持 QR、Code128、DataMatrix 等多种码制。
> English: 一维码/二维码识别，支持 QR、Code128、DataMatrix 等多种码制.

## 实现策略 / Implementation Strategy
> 中文：采用“输入校验 -> 参数解析 -> 核心算法执行 -> 结果封装”的统一链路，优先复用 OpenCV 与现有算子基类能力，确保与主项目运行时行为一致。
> English: Uses a consistent pipeline of input validation, parameter parsing, core algorithm execution, and result packaging, reusing OpenCV and existing operator infrastructure for runtime consistency.

## 核心 API 调用链 / Core API Call Chain
- `Cv2.CvtColor`
- `Cv2.Line`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `CodeType` | `enum` | All | - | - |
| `MaxResults` | `int` | 10 | [1, 100] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 输入图像 | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | - |
| `Text` | 识别内容 | `String` | - |
| `CodeCount` | 识别数量 | `Integer` | - |
| `CodeType` | 条码类型 | `String` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 约 `O(W*H)` 到 `O(W*H + N)` |
| 典型耗时 (Typical Latency) | 约 `5-40 ms`（与解码器/识别模型相关） |
| 内存特征 (Memory Profile) | 图像编码缓存与识别结果结构体，开销中等 |

## 适用场景 / Use Cases
- 适合 (Suitable)：条码、二维码、OCR 文本等编码信息提取。
- 不适合 (Not Suitable)：图像严重模糊、强反光或字符遮挡场景。

## 已知限制 / Known Limitations
1. 当前实现遵循统一输入/输出协议，输入类型、维度或关键字段不符合约定时会返回失败。
2. 识别精度受成像质量显著影响，建议先做增强与校正。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.5 文档补全 / Completed Phase 2.5 documentation enrichment |
