# 极坐标展开 / PolarUnwrap

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `PolarUnwrapOperator` |
| 枚举值 (Enum) | `OperatorType.PolarUnwrap` |
| 分类 (Category) | 图像处理 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
该算子把以某个中心为参考的环形区域，从笛卡尔坐标系展开到极坐标平面。

当前展开区域由以下参数定义：

- 中心点：`Center` 输入端口，或参数 `CenterX / CenterY`
- 半径范围：`InnerRadius ~ OuterRadius`
- 角度范围：`StartAngle ~ EndAngle`

展开后图像的几何语义是：

- **横轴**：角度方向
- **纵轴**：半径方向

当前实现的输出尺寸规则为：

- `height = OuterRadius - InnerRadius`
- `width = OutputWidth > 0 ? OutputWidth : round(2π × OuterRadius × angleSpan / 360)`

因此，如果你不显式指定 `OutputWidth`，算子会按外圈弧长近似来自动推导展开宽度。

> English: The operator unwraps an annular region around a center point into a rectangular image, where the horizontal axis represents angle and the vertical axis represents radius.

## 实现策略 / Implementation Strategy
当前实现实际上有两套展开路径：

- **优先路径：`WarpPolar`**
  - 若 `UseWarpPolar=true`，先尝试用 OpenCV 的 `Cv2.WarpPolar(...)` 做极坐标展开。
  - 随后按角度范围切行、必要时做环绕拼接，再转置成最终方向。
- **回退路径：`Remap`**
  - 如果 `WarpPolar` 失败，或 `UseWarpPolar=false`，则使用自定义 `mapX/mapY + Cv2.Remap(...)` 做逐像素极坐标映射。

源码中的 `WarpPolar` 路径不是直接输出最终结果，而是：

1. 先生成整圈展开结果；
2. 根据 `StartAngle` 与角度跨度切出所需行；
3. 若跨越 `0/360°` 边界，则通过 `SliceRowsWithWrap(...)` 做拼接；
4. 转置后再按目标宽高调整尺寸。

这也是它相对简单 `Remap` 方案更高效，但实现更复杂的原因。

> English: The operator prefers OpenCV `WarpPolar` for performance, but falls back to a custom remap implementation when needed or when `UseWarpPolar` is disabled.

## 核心 API 调用链 / Core API Call Chain
1. `TryGetInputImage(inputs)`
2. `ResolveCenter(...)`
3. `GetIntParam / GetDoubleParam / GetBoolParam`
4. 计算 `angleSpan`、自动输出宽度和输出高度
5. 分支一：`TryUnwrapByWarpPolar(...)`
   - `Cv2.WarpPolar(...)`
   - `SliceRowsWithWrap(...)`
   - `Cv2.Transpose(...)`
   - `Cv2.Resize(...)`
6. 分支二：`UnwrapByRemap(...)`
   - 构建 `mapX / mapY`
   - `Cv2.Remap(...)`
7. `CreateImageOutput(unwrapped, additionalData)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `CenterX` | `int` | `0` | - | 中心点 `X` 坐标。未提供 `Center` 输入端口时使用；默认会退化为图像中心。 |
| `CenterY` | `int` | `0` | - | 中心点 `Y` 坐标。未提供 `Center` 输入端口时使用；默认会退化为图像中心。 |
| `InnerRadius` | `int` | `0` | `[0, min(width,height)/2]` | 展开起始半径。 |
| `OuterRadius` | `int` | `min(width,height)/2` | `[1, min(width,height)/2]` | 展开终止半径，必须大于 `InnerRadius`。 |
| `StartAngle` | `double` | `0.0` | `[-3600.0, 3600.0]` | 起始角度，单位为度。 |
| `EndAngle` | `double` | `360.0` | `[-3600.0, 3600.0]` | 结束角度，单位为度。若 `EndAngle < StartAngle`，源码会通过加 `360` 处理角度跨度。 |
| `OutputWidth` | `int` | `0` | `[0, 20000]` | 输出图宽度。为 `0` 时自动按外圈弧长估算。 |
| `UseWarpPolar` | `bool` | `true` | `true` / `false` | 是否优先使用 `Cv2.WarpPolar(...)`。失败时会回退到 `Remap` 方案。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | Image | `Image` | Yes | 待展开图像。 |
| `Center` | Center | `Point` | No | 可选中心点输入。若提供，将优先于 `CenterX/CenterY`。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | Image | `Image` | 展开后的极坐标图像。 |

### 运行时附加输出 / Runtime Additional Outputs
| 名称 (Name) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|
| `Width` | `Integer` | 输出图像宽度。测试中 `OutputWidth=180` 时可直接得到 `Width=180`。 |
| `Height` | `Integer` | 输出图像高度，等于 `OuterRadius - InnerRadius`。 |
| `Method` | `String` | 本次执行实际采用的展开方法：`WarpPolar` 或 `Remap`。 |
| `UseWarpPolar` | `Boolean` | 当前请求是否优先使用 `WarpPolar`。 |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 两种路径整体都近似与输出像素数 `Width × Height` 成正相关；`Remap` 路径逐像素构图更直接，`WarpPolar` 路径额外包含切片、转置和可选缩放。 |
| 典型耗时 (Typical Latency) | 在大尺寸环形展开场景下，`WarpPolar` 通常更有优势；当退回 `Remap` 时，性能更依赖输出分辨率。 |
| 内存特征 (Memory Profile) | `WarpPolar` 路径会产生中间极坐标图、切片图和转置图；`Remap` 路径会分配两张映射表 `mapX / mapY`。 |

## 适用场景 / Use Cases
- **适合 (Suitable)**：圆环、瓶盖、轴承、卷材、标签环形区域等需要“拉直查看”的任务。
- **适合 (Suitable)**：后续在展开图上做字符检测、缺陷检测、条纹分析或模板匹配的流程。
- **适合 (Suitable)**：需要指定部分角度区间，而不是整圈展开的场景。
- **不适合 (Not Suitable)**：中心点估计不准确的情况，中心偏差会直接导致展开畸变。
- **不适合 (Not Suitable)**：期望自动识别环形中心、半径和角度范围的任务；当前实现需要外部提供这些参数。
- **不适合 (Not Suitable)**：把输出高度误认为 `OuterRadius` 本身；当前高度只等于径向厚度。

## 已知限制 / Known Limitations
1. 输出高度固定为 `OuterRadius - InnerRadius`，因此最外圈本身不是额外增加一行，而是按径向厚度离散采样。
2. 当 `UseWarpPolar=true` 时，如果 `WarpPolar` 失败，源码会静默回退到 `Remap`，成功输出中只通过 `Method` 字段体现本次实际方法。
3. 中心点优先级是输入端口 `Center` 高于参数 `CenterX/CenterY`；若流程里同时提供两者，参数值会被忽略。
4. 自动宽度估计依赖 `OuterRadius` 外圈弧长，因此不同 `OuterRadius` 会直接影响横向分辨率和后续检测尺度。
5. 当前实现只支持线性极坐标展开，不是对数极坐标展开，也未暴露插值方式或边界填充值配置。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.2 | 2026-03-14 | 第二轮基于源码补充 WarpPolar/Remap 双路径、输出尺寸规则和中心点优先级说明 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |

