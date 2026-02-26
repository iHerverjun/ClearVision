# Gaussian Blur / GaussianBlur

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `GaussianBlurOperator` |
| 枚举值 (Enum) | `OperatorType.Filtering` |
| 分类 (Category) | 预处理 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：使用高斯核进行加权卷积平滑，抑制高频噪声并保留主要低频结构。
> English: Applies Gaussian convolution to suppress high-frequency noise while preserving coarse structures.

## 实现策略 / Implementation Strategy
> 中文：`KernelSize` 若为偶数会自动增 1 保证奇数核；`SigmaY=0` 时交给 OpenCV 自动按 `SigmaX` 推导；边界处理使用可配置 `BorderType`。
> English: Even kernel sizes are normalized to odd values; `SigmaY=0` lets OpenCV infer from `SigmaX`; border handling follows configurable `BorderType`.

## 核心 API 调用链 / Core API Call Chain
- `GetIntParam` / `GetDoubleParam`（读取核大小与 sigma）
- 奇数核修正（`KernelSize % 2 == 0` 时 +1）
- `Cv2.GaussianBlur(src, dst, Size(k,k), sigmaX, sigmaY, borderMode)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `KernelSize` | `int` | 5 | [1, 31] | - |
| `SigmaX` | `double` | 1 | [0.1, 10] | - |
| `SigmaY` | `double` | 0 | [0, 10] | - |
| `BorderType` | `enum` | 4 | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | Image | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | Image | `Image` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | `O(W*H*K^2)` |
| 典型耗时 (Typical Latency) | 约 `0.4-3 ms`（1920x1080，`K=3..11`） |
| 内存特征 (Memory Profile) | 一张输出图，额外中间开销较小 |

## 适用场景 / Use Cases
- 适合 (Suitable)：边缘检测前降噪、轻度模糊去纹理、对随机高斯噪声的预平滑。
- 不适合 (Not Suitable)：椒盐噪声主导或需要严格保边细节的任务（可优先中值/双边）。

## 已知限制 / Known Limitations
1. 单一核尺寸参数无法分别控制 X/Y 方向核大小。
2. 核尺寸过大时会明显损失边缘与细小缺陷信息。
3. 算子未内置 ROI 或分块机制，高分辨率全图处理需关注吞吐。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P1 文档补全 / Completed Phase 2.3 P1 documentation enrichment |