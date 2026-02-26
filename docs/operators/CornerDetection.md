# 角点检测 / CornerDetection

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `CornerDetectionOperator` |
| 枚举值 (Enum) | `OperatorType.CornerDetection` |
| 分类 (Category) | 定位 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：Detects corner points using Harris or Shi-Tomasi.。
> English: Detects corner points using Harris or Shi-Tomasi..

## 实现策略 / Implementation Strategy
> 中文：采用“输入校验 -> 参数解析 -> 核心算法执行 -> 结果封装”的统一链路，优先复用 OpenCV 与现有算子基类能力，确保与主项目运行时行为一致。
> English: Uses a consistent pipeline of input validation, parameter parsing, core algorithm execution, and result packaging, reusing OpenCV and existing operator infrastructure for runtime consistency.

## 核心 API 调用链 / Core API Call Chain
- `Cv2.CvtColor`
- `Cv2.GoodFeaturesToTrack`
- `Cv2.CornerSubPix`
- `Cv2.DrawMarker`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Method` | `enum` | ShiTomasi | - | - |
| `MaxCorners` | `int` | 100 | [1, 5000] | - |
| `QualityLevel` | `double` | 0.01 | [1E-06, 1] | - |
| `MinDistance` | `double` | 10 | [0, 10000] | - |
| `BlockSize` | `int` | 3 | [2, 31] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | Image | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | Image | `Image` | - |
| `Corners` | Corners | `PointList` | - |
| `Count` | Count | `Integer` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 约 `O(W*H + N)` |
| 典型耗时 (Typical Latency) | 约 `1-15 ms`（1920x1080） |
| 内存特征 (Memory Profile) | 轮廓/角点/几何中间结果缓存，额外开销约 `O(W*H)` |

## 适用场景 / Use Cases
- 适合 (Suitable)：基准点定位、姿态修正、几何目标搜索与对位。
- 不适合 (Not Suitable)：纹理极弱且缺少先验 ROI 约束的全图盲搜场景。

## 已知限制 / Known Limitations
1. 当前实现遵循统一输入/输出协议，输入类型、维度或关键字段不符合约定时会返回失败。
2. 对初始 ROI 与阈值依赖较强，初始化不稳定会导致定位漂移。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.5 文档补全 / Completed Phase 2.5 documentation enrichment |
