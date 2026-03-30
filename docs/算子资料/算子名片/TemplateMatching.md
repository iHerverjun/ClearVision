# 模板匹配 / TemplateMatch

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `TemplateMatchOperator` |
| 枚举值 (Enum) | `OperatorType.TemplateMatching` |
| 分类 (Category) | 匹配定位 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
该算子基于经典模板匹配，在搜索图像上滑动模板窗口并计算每个位置的相似度响应图，再取全局最优位置作为匹配结果。

当前实现的核心判定逻辑是：

- 对 `CCoeff/CCorr` 系列方法，最佳匹配取响应图中的 **最大值**。
- 对 `SqDiff` 系列方法，最佳匹配取响应图中的 **最小值**。
- 为了统一下游阈值判定，源码把 `SqDiff` 的原始最优值做了反向归一化：
  `normalizedScore = 1 - minVal`
- 最终以 `normalizedScore >= Threshold` 判定是否找到目标。

因此，输出 `Score` 总是被归一化为“越大越好”的语义，而不是 OpenCV 原始响应值的直接回传。

> English: The operator performs classical template matching, converts different OpenCV matching modes into a unified “higher score is better” score, and decides `IsMatch` by comparing the normalized score against `Threshold`.

## 实现策略 / Implementation Strategy
当前实现是典型的“单最佳匹配”流程，而不是多目标模板检测流程：

- 搜索图像通过 `TryGetInputImage(...)` 获取，模板图像则直接通过 `TryGetInputValue<byte[]>(..., "Template")` 获取并 `ImDecode`。
- 模板若大于源图，会直接失败，不会自动缩放模板或裁剪搜索区域。
- 算子只取 `MinMaxLoc` 返回的**单个最优位置**，并以此输出 `Position`、`X`、`Y` 和 `Score`。
- 若分数超过阈值，才会在结果图上绘制绿色框与得分文字；若未超过阈值，结果图仅返回原图副本。
- 最终结果通过 `CreateImageOutput(...)` 封装，并附带若干运行时字段供下游节点使用。

这意味着当前实现更适合“找一个模板是否存在、位置在哪里”的任务，而不是“找出全部满足条件的位置”。

> English: The implementation returns only the single best match from `MinMaxLoc`, so it is best understood as a single-instance locator rather than a multi-instance detector.

## 核心 API 调用链 / Core API Call Chain
1. `TryGetInputImage(inputs, "Image")`
2. `TryGetInputValue<byte[]>(inputs, "Template")`
3. `Cv2.ImDecode(templateData, ImreadModes.Color)`
4. 选择 `TemplateMatchModes`（`SqDiff` / `CCorr` / `CCoeff` 及其 `Normed` 版本）
5. `Cv2.MatchTemplate(src, template, result, matchMethod)`
6. `Cv2.MinMaxLoc(result, out minVal, out maxVal, out minLoc, out maxLoc)`
7. 归一化 `Score` 并做阈值判定
8. `Cv2.Rectangle(...)` / `Cv2.PutText(...)`（仅匹配成功时绘制）
9. `CreateImageOutput(resultImg, additionalData)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Method` | `enum` | `"NCC"` | 元数据声明：`NCC` / `SQDiff`；源码实际支持：`sqdiff`、`sqdiffnormed`、`ccorr`、`ccorrnormed`、`ccoeff`、`ccoeffnormed` | 匹配方法。**注意：当前元数据、默认值和校验逻辑并不完全一致**；源码默认回退到 `CCoeffNormed`。 |
| `Threshold` | `double` | `0.8` | `[0.1, 1.0]`（执行时读取允许 `[0,1]`） | 匹配通过阈值。对所有模式都统一以归一化后的 `Score` 判定，值越高越严格。 |
| `MaxMatches` | `int` | `1` | `[1, 100]` | 元数据中声明为最大匹配数量，但**当前实现并未实际使用该参数**，始终只返回单个最佳匹配。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 输入图像 | `Image` | Yes | 搜索图像。运行时通过通用图像输入解析获取。 |
| `Template` | 模板图像 | `Image` | Yes | 模板图像。**当前源码直接按 `byte[]` 读取并解码**，因此运行时需要提供可被 `Cv2.ImDecode` 解码的图像字节。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | 结果图。匹配成功时会绘制绿色框和得分文字；匹配失败时通常返回原图副本。 |
| `Position` | 匹配位置 | `Point` | 最佳匹配位置。**当前语义是模板左上角坐标，不是模板中心点。** |
| `Score` | 匹配分数 | `Float` | 归一化后的匹配得分，统一为“越大越好”。 |
| `IsMatch` | 是否匹配 | `Boolean` | `Score >= Threshold` 时为 `true`。 |

### 运行时附加输出 / Runtime Additional Outputs
| 名称 (Name) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|
| `Width` | `Integer` | 输出图像宽度。 |
| `Height` | `Integer` | 输出图像高度。 |
| `Found` | `Boolean` | 与 `IsMatch` 同义的兼容字段。 |
| `IsMatch` | `Boolean` | 是否通过阈值判定。 |
| `Score` | `Double` | 归一化得分。 |
| `Position` | `Point` | 最佳匹配左上角位置。 |
| `X` | `Integer` | 最佳匹配左上角 `X`。 |
| `Y` | `Integer` | 最佳匹配左上角 `Y`。 |
| `TemplateWidth` | `Integer` | 模板宽度。 |
| `TemplateHeight` | `Integer` | 模板高度。 |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 近似 `O((W-w+1) × (H-h+1) × w × h)`，与模板尺寸和搜索区域成正相关。 |
| 典型耗时 (Typical Latency) | 仓库中未提供固定 benchmark；主要取决于源图尺寸、模板尺寸和所选匹配模式。 |
| 内存特征 (Memory Profile) | 需要分配响应图 `result` 和结果图副本；响应图尺寸约为 `(W-w+1) × (H-h+1)`。 |

## 适用场景 / Use Cases
- **适合 (Suitable)**：目标外观稳定、尺度固定、旋转变化很小的定位任务。
- **适合 (Suitable)**：缺件检测、贴标定位、固定治具上的零件存在性判断。
- **适合 (Suitable)**：对实现简单、参数少、运行逻辑直观的定位流程。
- **不适合 (Not Suitable)**：目标存在明显旋转、尺度变化、透视变化或局部遮挡的场景。
- **不适合 (Not Suitable)**：需要一次检测多个目标实例的任务。
- **不适合 (Not Suitable)**：重复纹理强、模板不唯一、背景中存在大量相似区域的图像。

## 已知限制 / Known Limitations
1. `MaxMatches` 参数当前未被实际使用，源码始终只返回单个最佳匹配结果。
2. `Method` 的元数据选项、默认值和 `ValidateParameters(...)` 的合法值集合并不完全一致；例如 UI 侧的 `NCC` 与源码内部的 `CCoeffNormed` 存在命名偏差。
3. `Position` / `X` / `Y` 表示的是模板左上角位置，不是模板中心点，也不是亚像素位置。
4. 模板输入当前通过 `byte[]` 解码，而不是走统一的 `TryGetInputImage("Template")` 路径；这会让端口声明与实际运行时输入形态出现差异。
5. 当前实现不会自动做多尺度搜索、旋转搜索或非极大值抑制，因此对形变和多目标场景支持有限。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.2 | 2026-03-14 | 第二轮基于源码补充实际匹配模式、得分归一化、模板输入形态与参数限制说明 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |

