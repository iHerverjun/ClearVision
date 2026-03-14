# 梯度形状匹配 / GradientShapeMatch

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `GradientShapeMatchOperator` |
| 枚举值 (Enum) | `OperatorType.GradientShapeMatch` |
| 分类 (Category) | 匹配定位 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
该算子不是直接在原始灰度图上做相关性匹配，而是使用自定义 `GradientShapeMatcher` 基于**梯度方向特征**进行模板训练和匹配。

核心思想是：

1. 从模板图中提取梯度幅值足够大的边缘特征点；
2. 把每个特征点的梯度方向量化为 `8` 个方向桶；
3. 针对 `-AngleRange ~ +AngleRange` 按 `AngleStep` 预生成一组旋转模板；
4. 对场景图同样计算梯度方向图；
5. 在候选位置比较模板方向与场景方向是否一致；
6. 以“方向一致的特征点数 / 模板特征点总数”作为匹配分数，再乘 `100` 输出百分比得分。

源码中方向匹配不是严格同一方向，而是允许相邻方向桶匹配：

- `diff <= 1` 视为方向匹配成立

因此这是一种对边缘方向有一定容差的离散梯度模板匹配。

> English: The operator trains a bank of rotated gradient templates, quantizes edge directions into 8 bins, and scores scene positions by directional agreement ratio.

## 实现策略 / Implementation Strategy
当前实现有几项很重要的源码行为：

- **训练-匹配分离**：匹配前会先训练模板，不是每次简单地对原图旋转模板直接做相关性匹配。
- **旋转模板缓存**：算子内部维护 `_matcherCache`，缓存键由 `templatePath + angleRange + angleStep + magnitudeThreshold` 组成。
- **模板来源有优先级**：优先使用输入端口 `Template`，否则读取 `TemplatePath`。
- **结果图固定绘制框大小**：匹配成功后绘制的是以 `Position` 为中心、边长约 `80` 的固定框，并不是模板真实包围框。
- **只输出最佳匹配**：`GradientShapeMatcher.Match(...)` 最终只返回最佳结果，而不是候选列表。

> English: The implementation is a cached train-once / match-many gradient-template matcher with built-in rotation support, but it only returns the single best match and uses a fixed visualization box.

## 核心 API 调用链 / Core API Call Chain
1. `TryGetInputImage(inputs)`
2. `GetStringParam / GetIntParam / GetDoubleParam`
3. 获取或创建 `GradientShapeMatcher`
4. `matcher.Train(template, angleRange)`
   - `EnsureGray(...)`
   - `ExtractFeatures(...)`
   - `CreateRotatedTemplate(...)`
5. `matcher.Match(srcImage, minScore)`
   - `ComputeSceneGradients(...)`
   - `ComputeMatchScore(...)`
   - 选择最佳 `ShapeMatchResult`
6. `Cv2.Rectangle(...)` / `Cv2.DrawMarker(...)` / `Cv2.PutText(...)`
7. `CreateImageOutput(resultImage, additionalData)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `TemplatePath` | `file` | `""` | 文件路径 | 未提供模板输入端口时，从此路径加载模板。 |
| `MinScore` | `double` | `80.0` | `[0.0, 100.0]` | 最小匹配分数，单位为百分比。得分来自“方向匹配特征比例 × 100”。 |
| `AngleRange` | `int` | `180` | `[0, 180]` | 训练旋转模板时的角度范围，表示从 `-AngleRange` 到 `+AngleRange`。 |
| `AngleStep` | `int` | `1` | `[1, 10]` | 训练旋转模板的步长。步长越小，旋转鲁棒性更高，但训练成本和模板数量也更大。 |
| `MagnitudeThreshold` | `int` | `30` | `[0, 255]` | 梯度幅值阈值。只有强于该阈值的边缘点才会进入模板特征集。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 搜索图像 | `Image` | Yes | 搜索图像。 |
| `Template` | 模板图像 | `Image` | No | 可选模板输入。若提供，会优先于 `TemplatePath`。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | 匹配结果图。成功时会绘制固定尺寸矩形框、中心十字和角度文字。 |
| `Position` | 匹配位置 | `Point` | 元数据声明为点位输出，但当前运行时实际输出 `X` / `Y` 字段。 |
| `Angle` | 旋转角度 | `Float` | 最佳匹配角度。 |
| `IsMatch` | 是否匹配 | `Boolean` | 是否满足最小分数阈值。 |
| `Score` | 匹配分数 | `Float` | 百分比得分，范围通常在 `0~100`。 |

### 运行时附加输出 / Runtime Additional Outputs
| 名称 (Name) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|
| `Width` | `Integer` | 输出图像宽度。 |
| `Height` | `Integer` | 输出图像高度。 |
| `IsMatch` | `Boolean` | 匹配是否通过。 |
| `Score` | `Double` | 最佳匹配百分比分数。 |
| `X` | `Integer` | 最佳匹配位置 `X`。 |
| `Y` | `Integer` | 最佳匹配位置 `Y`。 |
| `Angle` | `Double` | 最佳匹配角度。 |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 训练阶段与模板特征点数和旋转模板数量相关；匹配阶段与场景尺寸、模板特征数和候选位置数相关。 |
| 典型耗时 (Typical Latency) | 首次训练模板时开销明显高于复用缓存的重复调用；`AngleRange` 与 `AngleStep` 对性能影响很大。 |
| 内存特征 (Memory Profile) | 旋转模板集会缓存在 `_matcherCache` 中，缓存体积会随角度范围和模板复杂度增加。 |

## 适用场景 / Use Cases
- **适合 (Suitable)**：目标主要由边缘和轮廓定义、光照变化较大但边缘结构相对稳定的场景。
- **适合 (Suitable)**：目标存在旋转变化，但尺度变化不大的定位任务。
- **适合 (Suitable)**：希望避免直接使用灰度相关性、转而使用边缘方向一致性做匹配的场景。
- **不适合 (Not Suitable)**：模板来自输入端口且会频繁切换的多模板流程，若沿用同一算子实例需要特别注意缓存行为。
- **不适合 (Not Suitable)**：依赖真实模板包围框尺寸的后续几何处理，因为当前结果框大小是固定的。
- **不适合 (Not Suitable)**：需要返回多个候选匹配位置的任务。

## 已知限制 / Known Limitations
1. `_matcherCache` 的缓存键只使用 `templatePath + angleRange + angleStep + magnitudeThreshold`；当模板来自输入端口且 `templatePath` 为空时，不同模板可能命中同一缓存键，存在复用错误模板匹配器的风险。
2. `Position` 端口已声明，但当前运行时主要输出的是 `X` / `Y`，没有直接写出 `Position` 对象。
3. 可视化矩形框的大小固定为约 `80×80`，不代表模板真实尺寸或匹配区域真实包围框。
4. 当前匹配器只返回最佳匹配结果，不支持候选列表输出，也没有 NMS 或多目标输出逻辑。
5. 模板训练要求至少 `10` 个有效特征点，否则会抛出异常并导致算子失败。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.2 | 2026-03-14 | 第二轮基于源码补充梯度方向模板训练、缓存键风险、分数语义与运行时输出说明 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |

