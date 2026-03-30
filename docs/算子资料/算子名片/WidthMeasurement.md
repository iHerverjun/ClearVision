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
当前实现有两种工作模式：

- **ManualLines**：直接使用输入的两条线 `Line1` 和 `Line2` 做宽度测量。
- **AutoEdge**：先从图像中自动检测两条近似平行线，再在它们之间测量宽度。

最终测量值不是“线段间最短距离”的一次性计算，而是：

1. 在 `Line1` 上均匀采样 `NumSamples` 个点；
2. 对每个采样点，计算其到 `Line2` 所在**无限直线**的垂直距离；
3. 统计这些距离的 `Mean / Min / Max` 作为最终宽度结果。

也就是说，当前输出：

- `Width` = 平均宽度
- `MinWidth` = 最小采样宽度
- `MaxWidth` = 最大采样宽度

因此它更接近“沿一条边对另一条边做多点投影测距”的统计型测量，而不是严格的双边对称测量。

> English: The operator samples points on one line, projects them perpendicularly to the other line, and reports the mean/min/max pixel distances.

## 实现策略 / Implementation Strategy
当前实现的工程逻辑比较清晰：

- **手动模式优先精确控制**：在 `ManualLines` 模式下，必须显式提供 `Line1` 和 `Line2`，否则直接失败。
- **自动模式优先可用性**：在 `AutoEdge` 模式下，先做 `Canny` 边缘，再用 `HoughLinesP` 找线段，从中挑选一对最可能的平行线。
- **候选线对评分**：自动模式不是随便取前两条线，而是对候选对按“长度 + 分离度 - 角度差惩罚”打分，取分数最高者。
- **统计而非单点**：宽度不是单值硬算，而是通过多个采样点做统计，能反映边缘轻微不平行或局部波动。
- **结果图可视化**：结果图中会绘制两条测量线，并每隔若干采样点画出红色投影线，最后叠加均值/最小值/最大值文字。

> English: The operator favors practical width estimation: robust line-pair selection in auto mode, multiple point-to-line samples, and visual overlays for debugging.

## 核心 API 调用链 / Core API Call Chain
1. `TryGetInputImage(inputs)`
2. `GetStringParam("MeasureMode")` / `GetIntParam("NumSamples")`
3. 分支一：`TryParseLine(...)`（`ManualLines`）
4. 分支二：`TryDetectParallelLines(src, out line1, out line2)`（`AutoEdge`）
   - `Cv2.CvtColor(...)`
   - `Cv2.Canny(...)`
   - `Cv2.HoughLinesP(...)`
5. `SampleWidths(line1, line2, numSamples)`
6. `DistancePointToLine(...)`
7. `DrawMeasurementOverlay(...)`
   - `Cv2.Line(...)`
   - `ProjectPointToLine(...)`
   - `Cv2.PutText(...)`
8. `CreateImageOutput(resultImage, additionalData)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `MeasureMode` | `enum` | `"AutoEdge"` | `AutoEdge` / `ManualLines` | 测量模式。`AutoEdge` 从图像中自动找两条近似平行线；`ManualLines` 使用输入端口给定的两条线。 |
| `NumSamples` | `int` | `24` | `[10, 100]` | 沿 `Line1` 采样的点数。采样点越多，统计更稳，但对局部毛刺和非平行误差也会更敏感。 |
| `Direction` | `enum` | `"Perpendicular"` | `Perpendicular` / `Custom` | 元数据中声明了方向参数，但**当前源码没有实际读取和使用它**。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | Image | `Image` | Yes | 待测量图像。自动模式需要从中检测边缘和线段。 |
| `Line1` | Line 1 | `LineData` | No | 手动模式下的第一条基准线。 |
| `Line2` | Line 2 | `LineData` | No | 手动模式下的第二条基准线。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | Image | `Image` | 测量结果图，会绘制两条线、若干投影采样线以及统计文字。 |
| `Width` | Width | `Float` | 平均宽度，单位为像素。 |
| `MinWidth` | Min Width | `Float` | 最小采样宽度，单位为像素。 |
| `MaxWidth` | Max Width | `Float` | 最大采样宽度，单位为像素。 |

### 运行时附加输出 / Runtime Additional Outputs
| 名称 (Name) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|
| `Height` | `Integer` | 输出图像高度。 |
| `Width` | `Double` | **注意：这里被源码覆盖为平均测量宽度，而不是图像宽度。** |
| `MinWidth` | `Double` | 最小采样宽度。 |
| `MaxWidth` | `Double` | 最大采样宽度。 |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 手动模式近似 `O(NumSamples)`；自动模式额外包含 `Canny + HoughLinesP`，总体与图像像素数和候选线段数量相关。 |
| 典型耗时 (Typical Latency) | 仓库中未提供固定 benchmark；自动模式明显重于手动模式，主要耗时在边缘和直线检测。 |
| 内存特征 (Memory Profile) | 自动模式会额外分配灰度图和边缘图；结果图还会复制原图用于叠加显示。 |

## 适用场景 / Use Cases
- **适合 (Suitable)**：两条边界近似平行、宽度主要在像素层面评估的场景。
- **适合 (Suitable)**：有明确手工基准线输入时的快速宽度测量。
- **适合 (Suitable)**：需要同时输出数值统计和可视化示意图的测量流程。
- **不适合 (Not Suitable)**：要求物理单位测量但未做标定换算的场景。
- **不适合 (Not Suitable)**：边缘极弱、噪声很强或线段严重断裂时的自动测量。
- **不适合 (Not Suitable)**：需要真正亚像素级、双边对称或法向严格约束的精密测量任务。

## 已知限制 / Known Limitations
1. `Direction` 参数当前未被源码读取，属于已声明但未生效参数。
2. `ManualLines` 模式不会额外检查两条线是否平行；即便存在夹角，也会按“点到线距离”继续计算。
3. 采样距离是从 `Line1` 上的点投影到 `Line2` 的**无限延长线**，不是点到线段的截断距离。
4. 输出字典中的 `Width` 键被平均测量宽度覆盖，因此无法再通过该键读取图像宽度。
5. 当前结果单位始终为像素，若需要毫米等物理单位，必须在下游结合标定或 `UnitConvert` 再处理。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.2 | 2026-03-14 | 第二轮基于源码补充自动/手动模式、采样统计逻辑、候选线评分与输出键覆盖说明 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |

