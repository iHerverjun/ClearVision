# 候选框筛选 / BoxFilter

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `BoundingBoxFilterOperator` |
| 枚举值 (Enum) | `OperatorType.BoxFilter` |
| 分类 (Category) | 数据处理 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：对检测框按面积、类别、区域或分数规则进行筛选，输出满足条件的子集。
> English: Filters detections by area, class, region, or confidence score to produce a constrained subset.

## 实现策略 / Implementation Strategy
> 中文：通过 `FilterMode` 切换谓词逻辑；区域模式使用框中心点落区判定；当 `MinScore>0` 且非分数模式时仍会追加一次通用分数过滤。
> English: Switches filtering predicates via `FilterMode`; region mode checks center-in-ROI; non-score modes still apply an optional global `MinScore` post-filter.

## 核心 API 调用链 / Core API Call Chain
- `TryParseDetectionList`（多格式检测框解析）
- `mode switch`：`Area/Class/Region/Score`
- 区域过滤：`region.Contains(center)`
- 可视化：`Cv2.Rectangle` + `Cv2.PutText`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `FilterMode` | `enum` | Area | - | - |
| `MinArea` | `int` | 0 | >= 0 | - |
| `MaxArea` | `int` | 9999999 | >= 0 | - |
| `TargetClasses` | `string` | "" | - | - |
| `MinScore` | `double` | 0 | [0, 1] | - |
| `RegionX` | `int` | 0 | - | - |
| `RegionY` | `int` | 0 | - | - |
| `RegionW` | `int` | 0 | - | - |
| `RegionH` | `int` | 0 | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Detections` | Detections | `DetectionList` | Yes | - |
| `Image` | Image | `Image` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Detections` | Detections | `DetectionList` | - |
| `Image` | Image | `Image` | - |
| `Count` | Count | `Integer` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | `O(N)` |
| 典型耗时 (Typical Latency) | 约 `0.05-1 ms`（纯数据过滤） |
| 内存特征 (Memory Profile) | 结果列表复制，额外开销约 `O(N)` |

## 适用场景 / Use Cases
- 适合 (Suitable)：检测后按业务规则清洗结果（尺寸阈值、白名单类别、ROI 限制）。
- 不适合 (Not Suitable)：需要复杂几何关系或时序约束的高级筛选场景。

## 已知限制 / Known Limitations
1. 区域模式仅用中心点判定，不考虑框与区域 IoU/覆盖比例。
2. 类别过滤依赖字符串标签匹配，标签规范不一致会影响结果。
3. 参数名以实现为准（如 `RegionW/RegionH`），需与配置端保持一致。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |