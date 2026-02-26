# 边缘对缺陷 / EdgePairDefect

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `EdgePairDefectOperator` |
| 枚举值 (Enum) | `OperatorType.EdgePairDefect` |
| 分类 (Category) | AI检测 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：沿基准边线采样，计算采样点到另一条边线的法向距离，与期望宽度比较，超出容差的采样点判为边缘对异常。
> English: Samples along one edge line, computes perpendicular distance to the paired line, and flags points whose deviation from expected width exceeds tolerance.

## 实现策略 / Implementation Strategy
> 中文：优先使用输入 `Line1/Line2`；若缺失则从图像自动检测（Canny/Sobel + HoughLinesP）并挑选近似平行且综合分值最高的线对，再执行宽度偏差统计。
> English: Uses provided lines first; otherwise auto-detects line pairs from image (Canny/Sobel + HoughLinesP), selects best near-parallel pair, then computes sampled deviations.

## 核心 API 调用链 / Core API Call Chain
- `TryResolveLines`（输入线优先，自动检测回退）
- 自动检测：`Cv2.Canny`/`Cv2.Sobel` + `Cv2.HoughLinesP`
- 几何计算：`DistancePointToLine` + `AngleDiff`
- 可视化：`Cv2.Line` / `Cv2.Circle` / `Cv2.PutText`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `ExpectedWidth` | `double` | 20 | [0, 100000] | - |
| `Tolerance` | `double` | 2 | [0, 100000] | - |
| `NumSamples` | `int` | 100 | [5, 5000] | - |
| `EdgeMethod` | `enum` | Canny | - | - |

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
| `DefectCount` | Defect Count | `Integer` | - |
| `MaxDeviation` | Max Deviation | `Float` | - |
| `Deviations` | Deviations | `Any` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 约 `O(W*H + L^2 + S)`（`L` 候选线数，`S` 采样点数） |
| 典型耗时 (Typical Latency) | 约 `1-12 ms`（取决于是否自动找线） |
| 内存特征 (Memory Profile) | 边缘图 + 候选线列表 + 偏差数组 |

## 适用场景 / Use Cases
- 适合 (Suitable)：槽宽/边距一致性检测、边缘对平行度与间距异常预警。
- 不适合 (Not Suitable)：目标边缘弯曲严重或不具备稳定线性结构的场景。

## 已知限制 / Known Limitations
1. 自动找线依赖边缘质量，低对比或噪声高场景易失败。
2. 当前宽度定义为点到线距离，不能直接反映曲线或非平行边对的真实间隔。
3. 仅输出统计与采样异常点，不提供缺陷类型分类。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |