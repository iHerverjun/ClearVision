# 宽度测量 / WidthMeasurement

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `WidthMeasurementOperator` |
| 枚举值 (Enum) | `OperatorType.WidthMeasurement` |
| 分类 (Category) | 检测 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：支持两种路径：`ManualLines` 直接对输入线对采样并计算点到线距离；`AutoEdge` 先做 `Canny + HoughLinesP` 自动提取候选线段，再按平行度、长度与分离度评分选出最佳线对，输出均值/最小/最大宽度。
> English: Two modes are supported: `ManualLines` samples distances directly between provided lines; `AutoEdge` detects candidates via `Canny + HoughLinesP`, selects the best parallel pair by score, and reports mean/min/max width.

## 实现策略 / Implementation Strategy
> 中文：优先兼容上游几何算子（手动线输入）以保证确定性；无法提供线输入时自动回退边缘检测，提升落地容错性。
> English: Deterministic manual-line input is preferred when upstream geometry is available; otherwise, automatic edge-based fallback improves deployment robustness.

## 核心 API 调用链 / Core API Call Chain
- `Cv2.CvtColor` + `Cv2.Canny`（边缘提取）
- `Cv2.HoughLinesP`（候选线段检测）
- `AngleDiffDeg` + `DistancePointToLine`（平行线对筛选评分）
- `SampleWidths`（多点采样宽度统计）
- `Cv2.Line` / `Cv2.PutText`（可视化与标注）

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `MeasureMode` | `enum` | AutoEdge | - | - |
| `NumSamples` | `int` | 24 | [10, 100] | - |
| `Direction` | `enum` | Perpendicular | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | Image | `Image` | Yes | - |
| `Line1` | Line 1 | `LineData` | No | - |
| `Line2` | Line 2 | `LineData` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | Image | `Image` | - |
| `Width` | Width | `Float` | - |
| `MinWidth` | Min Width | `Float` | - |
| `MaxWidth` | Max Width | `Float` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | AutoEdge: O(P + K² + S)，P 为像素量、K 为候选线数、S 为采样数 |
| 典型耗时 (Typical Latency) | ~2-12 ms（1920x1080，AutoEdge） |
| 内存特征 (Memory Profile) | 1 张边缘图 + 有界候选集合（K 默认截断），内存可控 |

## 适用场景 / Use Cases
- 适合 (Suitable)：条带宽度、平行边间距、双边缘厚度检测。
- 不适合 (Not Suitable)：目标边界明显非平行或高曲率、边缘断裂严重的场景。

## 已知限制 / Known Limitations
1. `AutoEdge` 依赖固定 Canny/Hough 参数，对极端噪声与低对比图像敏感。
2. 当前实现针对“两条主线”宽度，不直接支持多区段宽度模型。
3. 输出为像素单位，若需物理单位需结合标定链路。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |
| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P0 文档补全 / Completed Phase 2.3 P0 documentation enrichment |
