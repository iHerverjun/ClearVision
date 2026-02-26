# 卡尺工具 / CaliperTool

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `CaliperToolOperator` |
| 枚举值 (Enum) | `OperatorType.CaliperTool` |
| 分类 (Category) | 检测 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：在 ROI 内构建单条扫描线并做 1D 灰度采样，基于相邻采样点梯度阈值检测边缘（支持极性），再按边缘成对距离计算宽度。启用 `SubpixelAccuracy` 时，使用局部梯度二次插值细化边缘位置。
> English: Build a single scan line in ROI, detect edges by thresholding 1D intensity gradients with polarity control, pair edges, and compute pair-wise distances. Optional subpixel refinement uses local quadratic interpolation on gradients.

## 实现策略 / Implementation Strategy
> 中文：采用“单线卡尺”策略而非全图轮廓分析，计算量低、时延稳定，适合在线节拍检测。通过 `Direction/Angle` 兼容水平、垂直和自定义方向，便于在同一算子中覆盖常见工位。
> English: A single-line caliper strategy is used instead of full contour analysis for low and predictable latency. `Direction/Angle` supports horizontal, vertical, and custom scan directions for broader station reuse.

## 核心 API 调用链 / Core API Call Chain
- `Cv2.CvtColor`（输入转灰度）
- `SampleIntensity`（沿扫描线采样灰度）
- `DetectEdges`（一阶差分梯度阈值与极性过滤）
- `RefineSubpixel`（可选亚像素细化）
- `Cv2.Line` / `Cv2.Circle` / `Cv2.PutText`（结果可视化）

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Direction` | `enum` | Horizontal | - | - |
| `Angle` | `double` | 0 | [-180, 180] | - |
| `Polarity` | `enum` | Both | - | - |
| `EdgeThreshold` | `double` | 18 | [1, 255] | - |
| `ExpectedCount` | `int` | 1 | [1, 100] | - |
| `SubpixelAccuracy` | `bool` | false | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | Image | `Image` | Yes | - |
| `SearchRegion` | Search Region | `Rectangle` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | Image | `Image` | - |
| `Width` | Width | `Float` | - |
| `EdgePairs` | Edge Pairs | `PointList` | - |
| `PairCount` | Pair Count | `Integer` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | O(L)，L 为扫描采样点数 |
| 典型耗时 (Typical Latency) | ~0.2-2 ms（单 ROI，1920x1080） |
| 内存特征 (Memory Profile) | 1 张灰度图 + O(L) 采样数组，内存开销低 |

## 适用场景 / Use Cases
- 适合 (Suitable)：边缘清晰且近似平行的宽度/厚度测量（如槽宽、边距、胶线宽度）。
- 不适合 (Not Suitable)：弯曲边界、纹理强干扰或低对比度场景；多目标交叉边缘且无有效 ROI 约束。

## 已知限制 / Known Limitations
1. 宽度由“边缘配对顺序”决定，复杂边缘场景需结合 ROI 与极性约束。
2. 对 `EdgeThreshold` 与光照变化敏感，阈值需按产线样本标定。
3. 单扫描线只能反映局部宽度，无法直接表征全轮廓非均匀形变。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |
| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P0 文档补全 / Completed Phase 2.3 P0 documentation enrichment |
