# 中值滤波 / MedianBlur

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `MedianBlurOperator` |
| 枚举值 (Enum) | `OperatorType.MedianBlur` |
| 分类 (Category) | 预处理 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：在局部窗口内取像素中值替代中心像素，能够有效抑制脉冲噪声并尽量保留边缘。
> English: Replaces each pixel by neighborhood median, effective for impulse noise while preserving edges better than linear smoothing.

## 实现策略 / Implementation Strategy
> 中文：执行前将偶数 `KernelSize` 自动调整为奇数；目标缓冲区通过 `MatPool.Shared.Rent` 申请以减少频繁分配；核心计算调用 OpenCV 中值滤波。
> English: Ensures odd kernel size, rents output buffers from `MatPool` to reduce allocations, then runs OpenCV median filtering.

## 核心 API 调用链 / Core API Call Chain
- `GetIntParam("KernelSize")` + 奇数核修正
- `MatPool.Shared.Rent(width, height, type)`（复用输出内存）
- `Cv2.MedianBlur(src, dst, kernelSize)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `KernelSize` | `int` | 5 | [1, 31] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 图像 | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 图像 | `Image` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 近似 `O(W*H*K^2)`（中值选择开销随核增大上升） |
| 典型耗时 (Typical Latency) | 约 `0.7-6 ms`（1920x1080，`K=3..11`） |
| 内存特征 (Memory Profile) | 一张输出图（来自 `MatPool`），降低重复分配成本 |

## 适用场景 / Use Cases
- 适合 (Suitable)：椒盐噪声清理、二值化前的孤立噪点抑制。
- 不适合 (Not Suitable)：需要高保真灰度梯度的任务，或超大核实时处理场景。

## 已知限制 / Known Limitations
1. 核尺寸越大，细线与小缺陷被吞噬的风险越高。
2. 仅支持方形窗口中值滤波，不支持自定义结构元素。
3. 对高斯噪声并非最优，可能不如高斯滤波稳定。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P1 文档补全 / Completed Phase 2.3 P1 documentation enrichment |