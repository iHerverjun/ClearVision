# ORB特征匹配 / OrbFeatureMatch

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `OrbFeatureMatchOperator` |
| 枚举值 (Enum) | `OperatorType.OrbFeatureMatch` |
| 分类 (Category) | 匹配定位 / 特征匹配 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
该算子与 `AkazeFeatureMatchOperator` 属于同一类局部特征匹配流程，但特征提取器从 AKAZE 换成了 ORB，因此更偏向实时性和较低计算成本。

核心流程如下：

1. 对搜索图和模板图统一转为灰度图；
2. 使用 `ORB.Create(maxFeatures, scaleFactor, nLevels, edgeThreshold)` 提取关键点和二进制描述符；
3. 使用 `BFMatcher + Hamming` 做近邻匹配；
4. 选择性启用对称测试，只保留正反一致的匹配；
5. 基于有效匹配对做 `RANSAC` 单应性估计；
6. 以 `Inliers >= MinMatchCount` 且 `InlierRatio >= 0.25` 作为最终通过条件。

与 AKAZE 版本一样，当前 `Score` 的真实语义是：

`Score = Inliers / TotalMatches`

即**内点比例**，不是特征距离、不是相似度相关系数，也不是“越小越好”的误差值。

> English: This operator uses ORB keypoints and binary descriptors for faster feature matching, then validates the match by homography inliers and inlier ratio.

## 实现策略 / Implementation Strategy
当前实现延续了特征匹配基类的缓存与匹配框架，但 ORB 侧有几项值得注意的工程设计：

- **模板来源双通道**：优先使用输入端口 `Template`，否则读取 `TemplatePath`。
- **路径模板缓存**：当模板来自 `TemplatePath` 时，会复用基类 `FeatureMatchOperatorBase` 中的静态缓存，避免反复提特征。
- **模板特征限流**：`FilterFeatures(...)` 只对模板端做裁剪，按 `Response` 保留更强的特征点。
- **失败返回 NG 图而不是执行失败**：当模板或场景特征不足、或几何验证失败时，算子仍返回成功执行结果，但 `IsMatch=false` 且结果图上会画出 `NG` 文本。
- **输出中心点并非几何中心**：当前 `X` / `Y` 取的是第一个有效匹配点对应的场景特征坐标，而不是单应性框中心。

> English: The implementation focuses on practical pipeline behavior: cached templates, ORB-specific tuning parameters, and NG-result visualization instead of hard execution failure for most business-level mismatches.

## 核心 API 调用链 / Core API Call Chain
1. `TryGetInputImage(inputs, "Image")`
2. `TryGetInputImage(inputs, "Template")` 或 `GetOrLoadTemplate(templatePath, ...)`
3. `Cv2.CvtColor(..., BGR2GRAY)`
4. `ORB.Create(maxFeatures, scaleFactor, nLevels, edgeThreshold)`
5. `DetectAndCompute(...)`
6. `FilterFeatures(...)`
7. `MatchWithSymmetryTest(...)` 或 `BFMatcher.KnnMatch(..., k: 2)` + ratio test
8. `ComputeHomography(...)`
9. `DrawPerspectiveBox(...)` / `Cv2.DrawMarker(...)` / `Cv2.PutText(...)`
10. `CreateImageOutput(resultImage, additionalData)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `TemplatePath` | `file` | `""` | 文件路径 | 当未从输入端口传入模板图像时，从文件路径加载模板。 |
| `MaxFeatures` | `int` | `500` | `[100, 2000]` | ORB 最大特征点数，同时也作为模板端的特征筛选上限。 |
| `ScaleFactor` | `double` | `1.2` | `[1.0, 2.0]` | ORB 金字塔尺度因子。值越大，层间尺度变化越大。 |
| `NLevels` | `int` | `8` | `[1, 12]` | ORB 金字塔层数。层数越多，尺度鲁棒性通常更强，但提特征成本也会上升。 |
| `EdgeThreshold` | `int` | `31` | `[3, 100]` | ORB 的边缘阈值，影响边界附近可检测区域。 |

### 源码隐含参数 / Runtime-Used But Undeclared Parameters
以下参数在源码中被实际读取和校验，但**没有通过 `OperatorParam` 元数据对外声明**：

| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `EnableSymmetryTest` | `bool` | `true` | `true` / `false` | 是否启用双向匹配一致性筛选。 |
| `MinMatchCount` | `int` | `10` | `[3, 100]` | 最小内点数量阈值，用于最终通过判定。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 搜索图像 | `Image` | Yes | 搜索图像。 |
| `Template` | 模板图像 | `Image` | No | 可选模板输入；若提供，将覆盖 `TemplatePath`。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | 结果图，成功时绘制透视框和十字标记，失败时绘制 NG 文本。 |
| `Position` | 匹配位置 | `Point` | 元数据声明为点位输出，但当前运行时并未直接构造 `Position` 对象。 |
| `IsMatch` | 是否匹配 | `Boolean` | 是否通过最终几何判定。 |
| `Score` | 匹配分数 | `Float` | 当前实现中等于 `Inliers / TotalMatches` 的内点比例。 |

### 运行时附加输出 / Runtime Additional Outputs
| 名称 (Name) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|
| `Width` | `Integer` | 结果图像宽度。 |
| `Height` | `Integer` | 结果图像高度。 |
| `Inliers` | `Integer` | `RANSAC` 内点数。 |
| `TotalMatches` | `Integer` | 通过筛选的匹配总数。 |
| `X` | `Integer` | 当前实现中选取的代表性匹配点 `X` 坐标。 |
| `Y` | `Integer` | 当前实现中选取的代表性匹配点 `Y` 坐标。 |
| `Message` | `String` | NG 路径中的失败原因。 |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 特征提取阶段随像素数增长，匹配阶段与模板/场景特征点数量乘积相关。 |
| 典型耗时 (Typical Latency) | 通常低于 AKAZE 版本；实际仍取决于图像尺寸、纹理复杂度和模板特征数。 |
| 内存特征 (Memory Profile) | 除场景和模板描述符外，模板路径模式下会占用静态缓存。 |

## 适用场景 / Use Cases
- **适合 (Suitable)**：对实时性较敏感、模板纹理较丰富的匹配定位任务。
- **适合 (Suitable)**：目标存在一定平移、旋转和轻微尺度变化的场景。
- **适合 (Suitable)**：需要沿用特征匹配思路，但希望比 AKAZE 更轻量的流程。
- **不适合 (Not Suitable)**：纹理弱、重复纹理强或目标局部信息不足的模板。
- **不适合 (Not Suitable)**：把 `X` / `Y` 当成几何中心、装配中心或机器人抓取中心直接使用。
- **不适合 (Not Suitable)**：需要亚像素级或严格几何中心输出的测量任务。

## 已知限制 / Known Limitations
1. `EnableSymmetryTest` 和 `MinMatchCount` 在源码中实际生效，但未通过元数据声明，UI 或文档若只看属性会遗漏这两个行为开关。
2. `Position` 端口已声明，但当前运行时输出的主要坐标字段是 `X` / `Y`，且来源于首个有效匹配点而非透视框中心。
3. 最终判定除了内点数量外，还写死了 `InlierRatio >= 0.25` 的硬编码条件，未暴露为参数。
4. 模板特征点数量会被限制，但场景特征点数量不会同步限制；高纹理场景下匹配成本仍可能较高。
5. 与 AKAZE 版本类似，业务失败路径多数返回 NG 图像的“成功执行结果”，流程应以 `IsMatch` 而不是仅以执行状态做业务判定。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.2 | 2026-03-14 | 第二轮基于源码补充 ORB 参数、隐含参数、缓存行为与运行时输出说明 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |

