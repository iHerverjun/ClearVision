# 模板匹配 / TemplateMatching

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `TemplateMatchOperator` |
| 枚举值 (Enum) | `OperatorType.TemplateMatching` |
| 分类 (Category) | 匹配定位 |
| 成熟度 (Maturity) | 稳定 Stable |
| 当前版本 (Version) | `1.2.0` |

## 算法原理 / Algorithm Principle
该算子在搜索图像上滑动模板并生成响应图，然后从响应图中提取多个候选并做 IoU NMS。

当前版本把输出分成两层语义：

- `RawResponse`：OpenCV `MatchTemplate` 的原始响应值。
- `NormalizedScore`：canonical 的高分更好分数，面向新流程消费。
- `Score`：保留的兼容字段；当前仍等于算子用于阈值判定的分数。

对 `SqDiff` / `SqDiffNormed` 的处理已修正：

- `SqDiffNormed`
  - `RawResponse = rawSqDiffNormed`
  - `NormalizedScore = 1 - RawResponse`
  - `Score` 兼容保留，当前与 `NormalizedScore` 相同
- `SqDiff`
  - `RawResponse = rawSqDiff`
  - `NormalizedScore = inverted min-max normalization of current response map`
  - `Score` 兼容保留，当前与 `NormalizedScore` 相同

结论：
- 原始 `SqDiff` 响应不再被伪装成可直接阈值化的 0-1 分数。
- 新接入请优先读取 `NormalizedScore` 和 `RawResponse`。

## 实现策略 / Implementation Strategy
- 输入图和模板图统一走 `TryGetInputImage(...)`。
- 可选做 ROI 裁剪与搜索掩膜限制。
- `Gray / Edge / Gradient` 三种域都会先生成可匹配图，再调用 `Cv2.MatchTemplate(...)`。
- 候选提取不再只依赖单次 `MinMaxLoc`；会从响应图中持续取峰值并做局部抑制，然后再做 IoU NMS。
- `MaxMatches` 已实际生效，可返回多个离散匹配。

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 说明 (Description) |
|--------|------|--------|------|
| `Method` | `enum` | `CCoeffNormed` | 匹配方法，支持 `CCoeffNormed`、`SqDiff`、`SqDiffNormed`、`CCorr`、`CCorrNormed`、`CCoeff`。 |
| `Domain` | `enum` | `Gray` | 匹配域，可选 `Gray`、`Edge`、`Gradient`。 |
| `Threshold` | `double` | `0.8` | 候选阈值。对 `SqDiff` / `SqDiffNormed`，阈值比较的是修正后的高分更好分数。 |
| `MaxMatches` | `int` | `1` | 最多保留的匹配数量。 |
| `UseRoi` | `bool` | `false` | 是否启用 ROI 搜索。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|
| `Image` | `Image` | Yes | 搜索图像。 |
| `Template` | `Image` | Yes | 模板图像。 |
| `Mask` | `Image` | No | 搜索掩膜；非零区域允许搜索。 |

### 输出 / Outputs
| 名称 (Name) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|
| `Image` | `Image` | 结果图像。 |
| `Position` | `Point` | 最佳匹配中心点。 |
| `Score` | `Float` | legacy 兼容分数字段。 |
| `NormalizedScore` | `Float` | canonical 分数，新流程优先读这个字段。 |
| `RawResponse` | `Float` | 原始 OpenCV 响应值。 |
| `IsMatch` | `Boolean` | 是否存在满足阈值的候选。 |
| `Matches` | `Any` | 匹配列表，每项都包含 `Score`、`NormalizedScore`、`RawResponse`。 |
| `MatchCount` | `Integer` | 匹配数量。 |

## Legacy / Canonical 关系
- legacy：
  - `Score`
  - 直接把 `SqDiff` 原始响应当成阈值分数的旧理解
- canonical：
  - `NormalizedScore`
  - `RawResponse`

推荐读取方式：
- 需要统一阈值和排序时，读 `NormalizedScore`
- 需要和 OpenCV 原始响应对账、调参或排障时，读 `RawResponse`
- 老流程暂时可继续读 `Score`

## 已知限制 / Known Limitations
1. `CCorr` / `CCoeff` 的 `RawResponse` 仍保留 OpenCV 原始量纲，新流程应优先依赖 `NormalizedScore` 做统一展示或诊断。
2. 算子仍然是固定尺度模板匹配，不负责旋转/尺度搜索。
3. 重复纹理或强周期背景下，仍需要结合 ROI、Mask 或更强约束使用。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.2.0 | 2026-04-12 | 修正 `SqDiff` / `SqDiffNormed` 评分语义，新增 `NormalizedScore` 与 `RawResponse`，同步澄清 legacy / canonical 关系 |
| 1.0.2 | 2026-03-14 | 第二轮基于源码补充实际匹配模式、得分归一化、模板输入形态与参数限制说明 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |
