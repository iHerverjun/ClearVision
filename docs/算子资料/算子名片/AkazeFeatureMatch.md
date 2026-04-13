# AKAZE特征匹配 / AkazeFeatureMatch

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `AkazeFeatureMatchOperator` |
| 枚举值 (Enum) | `OperatorType.AkazeFeatureMatch` |
| 分类 (Category) | 匹配定位 / 特征匹配 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
该算子基于局部特征点匹配完成模板定位，核心流程是：

1. 在搜索图像和模板图像上分别提取 `AKAZE` 特征点与描述符。
2. 使用 `BFMatcher + Hamming` 做近邻匹配。
3. 通过 **Lowe ratio test**（当前阈值固定为 `0.75`）过滤明显歧义匹配。
4. 若启用 `EnableSymmetryTest`，再做一次反向匹配，只保留正反一致的匹配对，进一步抑制误匹配。
5. 根据保留下来的匹配点估计单应性矩阵 `H`：
   `p_scene ≈ H × p_template`
6. 用 `RANSAC` 统计内点数 `Inliers`，再依据内点数量与内点比例判断是否匹配成功。

当前实现的最终判定逻辑不是“匹配数够多就算通过”，而是同时满足：

- `Inliers >= MinMatchCount`
- `Score = Inliers / GoodMatches >= 0.25`

因此，这里的 `Score` 代表的是**内点比例**，不是模板相似度、不是相关系数、也不是“最佳匹配距离”。

> English: The operator detects AKAZE features on the scene and template, filters matches by ratio test (and optionally symmetry test), estimates a homography with RANSAC, and decides success using both inlier count and inlier ratio.

## 实现策略 / Implementation Strategy
从工程实现上看，这个算子有几项非常关键的设计：

- **模板来源有优先级**：如果输入端口 `Template` 提供了模板图像，会优先使用它；否则才尝试读取参数 `TemplatePath`。
- **路径模板带缓存**：当使用 `TemplatePath` 时，基类会把模板图、关键点和描述符缓存在静态 `ConcurrentDictionary` 中，后续同路径调用可复用，减少磁盘读取和重复特征提取开销。
- **模板特征限流**：`MaxFeatures` 只对模板特征点生效，会按 `Response` 从高到低保留最强的一批特征点，以控制匹配开销。
- **场景特征不过滤**：当前实现不会对搜索图像的特征数量做同样的上限裁剪，因此搜索图像复杂时，匹配成本仍可能较高。
- **失败也返回成功结果对象**：当模板特征不足、场景特征不足或匹配失败时，当前实现不会让执行器返回框架级 `Failure`，而是返回一张带 `NG` 标注的结果图，并设置 `IsMatch = false`。
- **可视化优先于纯数值输出**：匹配成功时会画透视框和十字标记，匹配失败时会在图上叠加失败原因，便于在调试界面快速定位问题。

> English: The operator prioritizes practical pipeline behavior: optional template input, cached template loading by path, template feature limiting, and always returning an annotated result image even for NG cases.

## 核心 API 调用链 / Core API Call Chain
1. `TryGetInputImage(inputs, "Image")`
2. `TryGetInputImage(inputs, "Template")` 或读取 `TemplatePath`
3. `Cv2.CvtColor(..., BGR2GRAY)`
4. `AKAZE.Create(threshold)`
5. `DetectAndCompute(...)` 提取场景与模板特征
6. `FilterFeatures(templateKeyPoints, templateDescriptors, maxFeatures)`
7. 匹配分支：
   - `MatchWithSymmetryTest(templateDesc, sceneDesc)`
   - 或 `BFMatcher.KnnMatch(..., k: 2)` + ratio test (`0.75`)
8. `ComputeHomography(...)`
   - `Cv2.FindHomography(..., HomographyMethods.Ransac, 5.0, mask)`
9. `DrawPerspectiveBox(...)`
   - `Cv2.PerspectiveTransform(...)`
   - `Cv2.Line(...)`
10. `Cv2.DrawMarker(...)` / `Cv2.PutText(...)`
11. `CreateImageOutput(resultImage, additionalData)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `TemplatePath` | `file` | `""` | 文件路径 | 当未从输入端口传入 `Template` 时，算子尝试从此路径加载模板，并使用静态缓存复用特征。 |
| `Threshold` | `double` | `0.001` | `[0.0001, 0.1]` | AKAZE 检测阈值。值越小，通常会检测到更多特征点，也更容易把弱纹理和噪声当作特征；值越大，特征点更少但更保守。 |
| `MinMatchCount` | `int` | `10` | `[3, 100]` | 匹配成功所需的**最小内点数**，注意不是原始匹配数，也不是总特征数。 |
| `EnableSymmetryTest` | `bool` | `true` | `true` / `false` | 是否启用双向一致性校验。开启后误匹配通常更少，但匹配开销更高。 |
| `MaxFeatures` | `int` | `500` | `[100, 2000]` | 保留的模板特征点上限，按 `Response` 从高到低筛选。**当前只作用于模板，不作用于搜索图像。** |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 搜索图像 | `Image` | Yes | 待搜索目标的场景图像。 |
| `Template` | 模板图像 | `Image` | No | 可选的模板输入。若提供，会覆盖 `TemplatePath` 的读取逻辑。 |

### 输出 / Declared Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | 叠加了匹配框、十字标记和文字说明的可视化结果图。 |
| `Position` | 匹配位置 | `Point` | 元数据中声明为点位输出，但**当前实现并未直接构造 `Position` 对象**。详见下方运行时附加输出说明。 |
| `IsMatch` | 是否匹配 | `Boolean` | 是否通过最终判定。 |
| `Score` | 匹配分数 | `Float` | 当前实现中等于 `Inliers / TotalMatches` 的内点比例。 |

### 运行时附加输出 / Runtime Additional Outputs
当前实际运行时输出除上述声明项外，还会额外带出以下字段：

| 名称 (Name) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|
| `Width` | `Integer` | 结果图像宽度。 |
| `Height` | `Integer` | 结果图像高度。 |
| `Inliers` | `Integer` | `RANSAC` 判断为内点的匹配数量。 |
| `TotalMatches` | `Integer` | 通过 ratio test / symmetry test 后的有效匹配数。 |
| `X` | `Integer` | 当前实现输出的匹配 `X` 坐标。来源于 `goodMatches[0]` 对应的场景特征点。 |
| `Y` | `Integer` | 当前实现输出的匹配 `Y` 坐标。来源于 `goodMatches[0]` 对应的场景特征点。 |
| `Message` | `String` | 仅在 `NG` 路径中附带失败原因，例如“场景特征点不足”或“模板特征点不足”。 |

**重要说明：**

- `X` / `Y` 不是透视框中心，也不是模板几何中心，而是当前代码选取的“首个通过筛选的匹配点”的场景坐标。
- 如果你在流程里需要真正的目标中心点，建议不要直接把 `X` / `Y` 当作几何中心使用。

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 特征提取阶段近似随图像大小增长；匹配阶段使用暴力匹配，近似与模板/场景特征数乘积相关。启用 `EnableSymmetryTest` 后会进行正反两次匹配。 |
| 典型耗时 (Typical Latency) | 取决于图像尺寸、纹理复杂度、模板特征数和是否命中模板缓存；同一路径模板重复调用时通常会明显快于每次重新读取模板。 |
| 内存特征 (Memory Profile) | 除场景与模板描述符外，`TemplatePath` 模式下还会在静态缓存中保留模板图、关键点和描述符副本。 |

## 适用场景 / Use Cases
- **适合 (Suitable)**：模板有丰富纹理、角点、边缘细节，且存在旋转、轻微尺度变化、视角变化或光照变化的定位任务。
- **适合 (Suitable)**：Logo、字符块、标签、PCB 局部纹理、零件表面标记等“局部特征明显”的目标搜索。
- **适合 (Suitable)**：需要在匹配结果图上直接显示定位框、匹配状态和失败原因的可视化调试流程。
- **不适合 (Not Suitable)**：纯色块、弱纹理、重复纹理强、周期性图案明显的目标，误匹配风险较高。
- **不适合 (Not Suitable)**：要求亚像素级几何定位或精密测量的场景，因为该算子输出的是特征匹配结果，不是精密边缘测量结果。
- **不适合 (Not Suitable)**：严重模糊、遮挡过多、模板与现场外观差异过大的情况，内点比例往往不足。

## 已知限制 / Known Limitations
1. 元数据声明了 `Position` 输出端口，但当前实现实际输出的是 `X` / `Y` 两个整数值，而不是一个 `Point` 对象。
2. `X` / `Y` 取自 `goodMatches[0]` 对应的场景特征点，不是透视框中心，也不是基于单应性反算出的目标中心。
3. 当前成功判定除了 `MinMatchCount` 之外，还写死了 `inlierRatio >= 0.25` 的阈值，这个比例门限并没有暴露为可配置参数。
4. 当前 ratio test 阈值 `0.75`、`RANSAC` 重投影阈值 `5.0` 都是硬编码，不能在界面上直接调参。
5. `MaxFeatures` 只限制模板特征点数量，不限制场景特征点数量；场景纹理非常丰富时，匹配成本仍可能较高。
6. `TemplatePath` 缓存没有淘汰策略，也没有基于文件修改时间做失效判断；若同一路径的模板文件内容被替换，进程内可能继续使用旧缓存。
7. 当模板不存在、路径错误或模板特征过少时，当前实现会统一落到“模板特征点不足”的 `NG` 路径，而不是输出更细粒度的文件错误信息。
8. 当前失败路径通常返回带 `NG` 标注图像的“成功执行结果”，而不是框架级执行失败；流程编排时应以 `IsMatch` 判定业务通过与否，而不是只看执行状态。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.1 | 2026-03-14 | 基于源码补充 AKAZE 匹配链路、判定规则、模板缓存、运行时实际输出与限制说明 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |

## 2026-04-12 Compatibility Update / 兼容性更新
- 模板缓存键已包含模板内容指纹与 detector 配置，避免同路径模板热替换后静默命中旧缓存。
- 缓存命中返回克隆结果，不再共享底层 Mat 对象。

