# 线序判定 / DetectionSequenceJudge

## 基本信息 / Basic Info
| 项目 | 值 |
|------|------|
| 类名 | `DetectionSequenceJudgeOperator` |
| 枚举值 | `OperatorType.DetectionSequenceJudge` |
| 分类 | `AI Inspection` |
| 版本 | `1.0.0` |
| 成熟度 | `Experimental` |
| 标签 | `experimental`, `industrial-remediation`, `sequence-judge` |

## 算法说明 / Algorithm
该算子用于把检测框转换成“有顺序的线序结果”。当前实现有 3 条路径：

1. `SingleRow`
   保持旧逻辑，按坐标排序后直接比对标签序列。
2. `RowCluster`
   先按 Y 方向聚成多行，再按每行内部顺序展开，适合双排或多排端子。
3. `SlotAssignment`
   根据期望槽位点，把检测结果分配到最近槽位，再按槽位顺序判定，适合规则槽位布局。

另外支持可选透视校正：若提供透视源点和目标点，会先把检测中心映射到校正平面，再进入排序或槽位分配。

## 参数 / Parameters
| 名称 | 类型 | 默认值 | 说明 |
|------|------|------|------|
| `ExpectedLabels` | `string` | `""` | 逗号分隔的期望标签顺序 |
| `SortBy` | `enum` | `CenterX` | 旧排序依据 |
| `Direction` | `enum` | `Ascending` | 排序方向 |
| `ExpectedCount` | `int` | `0` | 期望检测数量 |
| `MinConfidence` | `double` | `0.0` | 最低置信度 |
| `AllowMissing` | `bool` | `false` | 是否允许缺失标签 |
| `AllowDuplicate` | `bool` | `false` | 是否允许重复标签 |
| `GroupingMode` | `enum` | `SingleRow` | 分组模式，支持 `SingleRow` / `RowCluster` / `SlotAssignment` / `Auto` |
| `ExpectedSlots` | `string` | `""` | JSON 数组或 `x:y;x:y` 槽位列表 |
| `RowTolerance` | `double` | `0.0` | 行聚类容差，`0` 表示自动 |
| `SlotTolerance` | `double` | `0.0` | 槽位分配最大距离，`0` 表示自动 |
| `PerspectiveSrcPointsJson` | `string` | `""` | 透视源点 JSON |
| `PerspectiveDstPointsJson` | `string` | `""` | 透视目标点 JSON |

## 输入 / 输出 / Inputs & Outputs
### 输入 / Inputs
| 名称 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `Detections` | `DetectionList` | 是 | 上游检测结果 |
| `SlotPoints` | `PointList` | 否 | 槽位点输入，优先于 `ExpectedSlots` |
| `PerspectiveSrcPoints` | `PointList` | 否 | 透视源点 |
| `PerspectiveDstPoints` | `PointList` | 否 | 透视目标点 |

### 输出 / Outputs
| 名称 | 类型 | 说明 |
|------|------|------|
| `IsMatch` | `Boolean` | 是否判定匹配 |
| `ActualOrder` | `Any` | 实际排序标签序列 |
| `Count` | `Integer` | 有效检测数量 |
| `MissingLabels` | `Any` | 缺失标签 |
| `DuplicateLabels` | `Any` | 重复标签 |
| `SortedDetections` | `DetectionList` | 排序后的检测结果 |
| `Assignment` | `Any` | 槽位分配详情 |
| `UnassignedDetections` | `DetectionList` | 未分配检测 |
| `SlotDistances` | `Any` | 槽位距离数组 |
| `RowCount` | `Integer` | 识别出的行数 |
| `PerspectiveApplied` | `Boolean` | 是否应用了透视校正 |
| `Diagnostics` | `Any` | 分组模式、容差、槽位数等诊断信息 |
| `Message` | `String` | 最终判定说明 |

## 适用场景 / Use Cases
- 线束、端子、连接器的顺序判定
- 存在单排、多排、规则槽位的工装布局
- 需要把检测结果转成更稳定的业务顺序输出

## 已知限制 / Known Limitations
1. 透视校正当前只做点位映射，不会自动估计透视模型。
2. `SlotAssignment` 依赖较稳定的槽位配置，槽位点错误会直接带来错序。
3. 对严重遮挡、弯折或标签错误的场景，最终效果仍然受上游检测质量约束。
