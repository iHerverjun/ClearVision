# 形状匹配 / ShapeMatching

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `ShapeMatchingOperator` |
| 枚举值 (Enum) | `OperatorType.ShapeMatching` |
| 分类 (Category) | Matching |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
虽然名称叫“形状匹配”，但当前实现本质上仍是**基于灰度模板匹配的旋转搜索**，而不是基于轮廓描述子或 `matchShapes` 的轮廓相似度匹配。

核心流程是：

1. 将搜索图和模板图统一转为灰度。
2. 构建搜索图与模板图的金字塔。
3. 在最高层上用更大的角度步长做粗搜索。
4. 逐层向下时，围绕上一层高分角度建立更细的角度候选集。
5. 对每个角度将模板旋转后执行 `Cv2.MatchTemplate(..., CCoeffNormed)`。
6. 仅保留 `Score >= MinScore` 的候选。
7. 通过 IoU 非极大值抑制去掉重叠候选，最终输出前 `MaxMatches` 个结果。

角度步长在金字塔高层会被放大：

`levelStep = clamp(baseStep × 2^level, baseStep, 90)`

因此该算子是一个**粗到细（coarse-to-fine）角度搜索模板匹配器**。

> English: The current implementation is a coarse-to-fine rotation search over grayscale template matching, not a contour-descriptor-based shape matcher.

## 实现策略 / Implementation Strategy
当前实现相比简单的旋转穷举有几处关键优化：

- **模板来源双通道**：优先使用输入端口 `Template`，否则再尝试 `TemplatePath`。
- **统一灰度处理**：无论输入是彩色还是灰度，都会先转成灰度图以稳定匹配过程。
- **金字塔粗到细**：通过 `PyrDown` 逐层下采样，在粗层快速筛出候选角度，再回到细层精化。
- **并行角度评估**：对每个候选角度使用 `Parallel.ForEach` 并行执行旋转模板匹配。
- **两阶段 NMS**：角度搜索阶段先用 `IoU=0.4` 过滤候选；最终输出前再用 `IoU=0.5` 再筛一轮。

这使得当前版本相比纯原图全角度穷举更实用，但它仍然主要解决“旋转变化”，而不是“尺度变化”。

> English: The operator combines grayscale conversion, image pyramids, parallel angle search, and NMS to make rotation-robust matching practical on larger images.

## 核心 API 调用链 / Core API Call Chain
1. `TryGetInputImage(inputs, "Image")`
2. `TryGetInputImage(inputs, "Template")` 或 `Cv2.ImRead(templatePath)`
3. `ToGray(src)` / `ToGray(template)`
4. `BuildPyramids(srcGray, tmplGray, numLevels)`
5. `BuildAngleRange(...)` + `ComputeLevelAngleStep(...)`
6. `MatchByAngles(...)`
   - `RotateImage(...)`
   - `Cv2.WarpAffine(...)`
   - `Cv2.MatchTemplate(..., TemplateMatchModes.CCoeffNormed)`
   - `Cv2.MinMaxLoc(...)`
7. `BuildRefinedAngles(...)`
8. `NonMaximumSuppression(...)`
9. `DrawMatchResult(...)`
10. `CreateImageOutput(resultImage, additionalData)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `TemplatePath` | `file` | `""` | 文件路径 | 当未从输入端口提供模板时，从文件读取模板图。 |
| `MinScore` | `double` | `0.7` | `[0.1, 1.0]` | 最小匹配分数。分数越高，候选越少、越保守。 |
| `MaxMatches` | `int` | `1` | `[1, 50]` | 最终最多输出的匹配数量。内部候选上限会扩成 `max(MaxMatches × 5, 20)`。 |
| `AngleStart` | `double` | `-30.0` | `[-180.0, 180.0]` | 搜索起始角度。 |
| `AngleExtent` | `double` | `60.0` | `[0.0, 360.0]` | 搜索角度跨度，最终搜索区间是 `[AngleStart, AngleStart + AngleExtent]`。 |
| `AngleStep` | `double` | `1.0` | `[0.1, 10.0]` | 基础角度步长。高层金字塔会自动放大步长做粗搜索。 |
| `NumLevels` | `int` | `3` | `[1, 6]` | 请求的金字塔层数。实际使用层数还受图像尺寸和模板尺寸约束。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | Search Image | `Image` | Yes | 搜索图像。 |
| `Template` | Template Image | `Image` | No | 模板图像。若不提供，可改用 `TemplatePath`。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | Result Image | `Image` | 结果图，会为每个最终匹配候选绘制矩形框、中心点和分数/角度标签。 |
| `Matches` | Matches | `Any` | 匹配结果列表。每项包含 `X`、`Y`、`Angle`、`Score`、`CenterX`、`CenterY`、`Width`、`Height`。 |

### 运行时附加输出 / Runtime Additional Outputs
| 名称 (Name) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|
| `Width` | `Integer` | 输出图像宽度。 |
| `Height` | `Integer` | 输出图像高度。 |
| `Matches` | `List<Dictionary>` | 最终保留下来的匹配列表。 |
| `MatchCount` | `Integer` | 最终输出的匹配数量。 |
| `NumLevelsUsed` | `Integer` | 实际构建并参与搜索的金字塔层数。 |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 约为 `O(Levels × Angles × MatchTemplateCost)`；主成本来自每个角度的模板旋转与 `MatchTemplate` 计算。 |
| 典型耗时 (Typical Latency) | 仓库中未提供固定 benchmark；主要受图像尺寸、模板尺寸、角度范围、角度步长和实际层数影响。 |
| 内存特征 (Memory Profile) | 需要维护搜索图与模板图的金字塔副本、旋转模板的临时矩阵，以及匹配候选列表。 |

## 适用场景 / Use Cases
- **适合 (Suitable)**：目标尺度基本稳定，但存在一定旋转变化的定位任务。
- **适合 (Suitable)**：对简单模板匹配不够稳，但又不需要 AKAZE/ORB 特征匹配的场景。
- **适合 (Suitable)**：希望用较少参数实现“旋转鲁棒模板定位”的流程。
- **不适合 (Not Suitable)**：目标尺度变化明显的场景，因为当前实现并未搜索尺度。
- **不适合 (Not Suitable)**：模板外观会发生非刚体形变、遮挡或严重光照变化的任务。
- **不适合 (Not Suitable)**：需要真正基于轮廓形状描述子做匹配评分的场景。

## 已知限制 / Known Limitations
1. 尽管名称为“ShapeMatching”，当前实现并不是轮廓描述子匹配，而是旋转模板匹配。
2. 当前实现没有尺度搜索，鲁棒性主要来自角度搜索和金字塔粗到细流程。
3. 匹配核心固定使用 `TemplateMatchModes.CCoeffNormed`，没有暴露不同匹配模式供用户切换。
4. `TemplatePath` 路径加载当前没有像 AKAZE/ORB 那样的缓存机制；频繁重复加载模板会有额外 I/O 成本。
5. 输出 `Matches` 中的坐标基于矩形左上角和尺寸推导，不是亚像素级定位结果。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.2 | 2026-03-14 | 第二轮基于源码补充粗到细角度搜索、金字塔规则、NMS 与实际输出结构说明 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |

