# 算子实现收敛计划 / Operator Implementation Reconciliation Plan

## 1. 文档目的

本文档用于统一整理当前算子库中三类问题：

1. 契约不一致：元数据、UI 参数、输入输出端口、运行时行为不一致。
2. 实现欠账：隐藏参数、未生效参数、隐藏输入、输出键漂移、失败语义不统一。
3. 算法边界：当前实现可用，但能力边界与用户预期存在偏差，导致效果不稳或调参困难。

本文档的目标不是直接修改实现，而是形成一份可评审、可排期、可执行的统一整改计划。

## 2. 排查范围

- 代码范围：`Acme.Product/src/Acme.Product.Infrastructure/Operators/**/*.cs`
- 文档范围：`docs/operators/*.md`
- 交叉参考：`docs/AlgorithmAudit/*.md`
- 目录索引参考：`docs/OPERATOR_CATALOG.md`、`docs/operators/CATALOG.md`

覆盖算子总数：**118**

## 3. 排查方法

本轮排查采用两层方法：

### 3.1 静态对比扫描

对全部 `*Operator.cs` 做以下对比：

- `OperatorParam` 声明参数 vs `GetStringParam / GetIntParam / GetDoubleParam / GetBoolParam / GetFloatParam` 实际读取参数
- `InputPort` 声明输入 vs `TryGetInputImage / TryGetInputValue / inputs.TryGetValue` 实际读取输入
- `OutputPort` 声明输出 vs `CreateImageOutput(...)` / 输出字典中的运行时键

### 3.2 人工深挖复核

对高价值、复杂、用户体感敏感的算子做源码级复核，重点包括：

- `TemplateMatching`
- `WidthMeasurement`
- `AkazeFeatureMatch`
- `OrbFeatureMatch`
- `GradientShapeMatch`
- `ShapeMatching`
- `GeometricFitting`
- `DeepLearning`
- `CameraCalibration`
- `Undistort`
- `PolarUnwrap`

本轮还额外参考了独立交叉审查结果，用于补漏和修正文档中的过度推断，但最终只合并了能被当前源码直接支撑的高置信度问题。

### 3.3 置信度说明

静态扫描中的「运行时输出键」提取会把部分普通字符串字面量误认为输出键，因此：

- 「隐藏参数 / 未使用参数 / 隐藏输入」结论整体置信度较高。
- 「额外输出键」结论需要人工复核后再纳入实施计划。
- 对使用 `GetOptional*Param` 等非常规读取路径的算子，静态扫描可能把「已读取参数」误判为「未读取参数」，因此批量清单仍需人工复核后才能作为实施依据。

## 4. 总体结论

当前算子库不是「整体不可用」，而是存在明显的 **工程契约漂移**。

| 算子 | 问题类型 | 高置信度问题 | 建议动作 | 状态 |
|------|------|------|------|------|
| `TemplateMatching` | 契约 + 实现 | `MaxMatches` 已声明但未生效；`Template` 运行时按 `byte[]` 解码；`Method` 元数据只暴露 `NCC` / `SQDiff`；`CCoeffNormed` 默认值差异 | **已完成**：统一 `Method` 枚举；实现多目标循环匹配；标准化 `Position` 为中心点。 | ✅ |
| `WidthMeasurement` | 契约 + 算法边界 | `Direction` 未生效；`CreateImageOutput` 注入 `Width` 语义冲突；手动/自动模式差异 | **已完成**：实现 `Direction` 与 `CustomAngle`；增加亚像素检测骨架（由 `SubpixelEdgeDetection` 支持）。 | ✅ |
| `GradientShapeMatch` | 契约 + 实现 | `_matcherCache` 键碰撞风险；`Position` 缺失；缓存缺乏 LRU 限制 | **已完成**：修复缓存键；显式输出 `Position`；LRU 暂待后续清理。 | ✅ |
| `OrbFeatureMatch` | 契约 + 实现 | `EnableSymmetryTest` / `MinMatchCount` 隐藏；`Position` 缺失；失败语义不统一 | **已完成**：暴露全部隐藏参数；统一输出 `Position` 结构；对齐失败语义。 | ✅ |
| `AkazeFeatureMatch` | 契约 | `Position` 缺失；`Score` 定义冲突；与 ORB 类似的失败语义问题 | **已完成**：补全 `Position` 输出；稳定 `Score` 为相似度定义；对齐失败语义。 | ✅ |
| `DeepLearning` | 契约 + 实现 | `UseGpu` / `GpuDeviceId` 隐藏；`DetectionList` 端口未声明 | **已完成**：暴露 GPU 关键参数；显式声明 `DetectionList` 端口。 | ✅ |
| `ImageAcquisition` | 契约 + 实现 | 相机核心参数 `exposureTime`、`gain`、`triggerMode` 参数预览与元数据不同步 | **已完成**：在元数据中稳定声明采集参数；保留新旧参数名兼容映射。 | ✅ |
| `TypeConvert` | 契约 | 声明输入 `Input` 与源码 `Value` 不一致；输出端口漂移 | **已完成**：统一输入键为 `Input`，移除 `Value` 读取；补全 `AsString/AsFloat/AsInteger/AsBoolean/OriginalType` 输出端口。 | ✅ |
| `TriggerModule` | 契约 | 未声明 `Signal` 端口，隐藏路径读取 `Trigger` | **已完成**：正式声明 `Signal` 输入端口；移除不必要的 `Trigger` 隐藏逻辑。 | ✅ |

## 7. 收敛原则（决策 checklist）

1. 已声明端口必须真实存在：声明了 `Position` 就真正输出 `Position`。
2. 隐藏参数必须二选一：要么正式暴露，要么删除运行时读取。
3. 已声明参数必须三选一：要么实现、要么废弃、要么从元数据移除。
4. 输出端口必须名实一致：声明了 `Position` 就真正输出 `Position`。
5. 业务失败语义统一：明确哪些算子使用「执行成功但业务 NG」，哪些算子使用框架级 `Failure`。
6. 算法升级与契约收敛分批进行，先修可解释性，再修性能和精度增强。

## 8. P0 优先级清单

P0 定义：直接影响调参、运行结果理解、下游集成，或容易导致用户以为「算子效果差/算子坏了」。

| 算子 | 问题类型 | 高置信度问题 | 建议动作 | 验证方式 |
|------|------|------|------|------|
| `TemplateMatching` | 契约 + 实现 | `MaxMatches` 已声明但未生效；`Template` 运行时按 `byte[]` 解码；`Method` 元数据只暴露 `NCC` / `SQDiff`，但运行时默认值是 `CCoeffNormed`，且内部 `switch` 还接受 `CCorr` / `CCoeff` 系列；`Position` 实际是左上角 | 明确单目标/多目标策略；要么实现 `MaxMatches`，要么移除；统一 `Template` 输入契约；统一 `Method` 枚举、默认值与内部 `switch`；明确位置语义 | 单元测试 + UI 参数回归 + 多目标样本验证 |
| `WidthMeasurement` | 契约 + 算法边界 | `Direction` 未生效；`CreateImageOutput` 默认注入 `Width` 为图像宽度后，算子又手动把同名键改写为测量宽度；手动模式与自动模式语义差异较大 | 要么实现 `Direction`，要么删除；拆分图像尺寸键与测量结果键；在端口和文档中明确测量几何定义 | 手动线样本测试 + 自动边缘样本测试 |
| `GradientShapeMatch` | 契约 + 实现 | `_matcherCache` 键未包含输入模板内容；当模板来自输入端口而 `templatePath` 为空时，相同参数的不同模板会共享同一缓存键；声明 `Position` 但运行时输出 `X/Y`；固定大小显示框与真实模板尺寸无关 | 修缓存键；在端口流模板模式下避免使用仅依赖 `templatePath` 的缓存；正式输出 `Position`；区分「显示框尺寸」和「模板真实尺寸」；必要时支持关闭缓存 | 多模板切换测试 + 缓存命中测试 |
| `OrbFeatureMatch` | 契约 + 实现 | `EnableSymmetryTest` / `MinMatchCount` 运行时生效但未声明；`Position` 缺失；`X/Y` 不是几何中心；特征不足和匹配 NG 既可能走 `Failure`，也可能走 `Success(CreateFailedOutput(...))`，失败语义不统一 | 暴露隐藏参数；输出 `Position`；明确 `X/Y` 是代表点还是中心；统一「执行失败」和「业务 NG」语义；考虑输出 `Center` | 特征匹配契约测试 + UI 参数可见性测试 |
| `AkazeFeatureMatch` | 契约 | `Position` 声明但未实际输出；`X/Y` 取首个匹配点；`Score` 是内点比例而非相似度；与 ORB 一样存在「业务 NG 但整体返回 Success」的混合语义 | 输出 `Position`；补 `Center` 或 `MatchPoint` 区分语义；在结果模型里固定 `Score` 定义；统一失败语义 | 匹配输出契约测试 |
| `DeepLearning` | 契约 + 实现 | `UseGpu` / `GpuDeviceId` 实际生效但未声明；`DetectionList` 是稳定输出但未声明为端口；`DetectionMode` 决定其余输出字段集合；目录和 UI 很难看出这一点 | 暴露 GPU 参数；明确 `Defects/Objects` 与 `DetectionList` 的输出契约；统一模式切换文案；决定是否显式声明 `DetectionList` | 参数面板测试 + 推理模式回归测试 |
| `ImageAcquisition` | 契约 + 实现 | 相机核心参数 `exposureTime`、`gain`、`triggerMode` 在运行时读取，但未通过元数据稳定声明，且与已声明参数命名风格不统一 | 统一采集参数命名与元数据；区分哪些参数由 `cameraBinding` 承接，哪些保留为算子参数；补兼容映射 | 采集参数回归测试 + 工程 JSON 兼容测试 |
| `TypeConvert` | 契约 | 声明输入为 `Input`，源码还读取 `Value`；运行时输出远多于声明 `Output` | 统一输入键；决定是否保留多种附加输出；若保留则显式声明 | 输入兼容测试 + 下游转换回归测试 |
| `TriggerModule` | 契约 | 未声明输入端口，但运行时读取 `Signal` / `Trigger` | 正式声明输入端口或移除隐藏路径；统一触发输入模型 | 触发行为测试 |

## 9. P1 优先级清单

P1 定义：短期不会让算子完全不可用，但会持续制造维护成本、结果理解成本或效果不稳。

| 算子 | 问题类型 | 高置信度问题 | 建议动作 |
|------|------|------|------|
| `GeometricFitting` | 算法边界 + 输出契约 | 所有有效轮廓点会被合并后统一拟合；椭圆不足 RANSAC；`FitResult` 字段结构依赖 `FitType` | 增加「最大轮廓/单轮廓/全部轮廓」选择；明确椭圆鲁棒性边界；稳定结果结构 |
| `ShapeMatching` | 算法边界 | 名称是「形状匹配」，实际更接近「旋转模板匹配」；没有尺度搜索 | 统一命名或补尺度搜索；文档和参数面板显式约束能力边界 |
| `CameraCalibration` | 算法边界 + 契约 | 单图模式可产出矩阵，但不应被误解为最终稳定标定；文件夹模式输入端口要求与实际依赖不完全一致 | 补模式说明；梳理单图模式输出用途；文件夹模式输入要求与 UI 同步 |
| `Undistort` | 契约 + 校验 | 运行时附加输出未声明；校验过于宽松；不校验图像尺寸与标定尺寸一致性 | 加强参数/输入校验；补尺寸兼容策略；补输出声明 |
| `PolarUnwrap` | 契约 + 算法边界 | `Method` / `UseWarpPolar` 为附加输出但未声明；自动宽度估计和高度语义容易被误解 | 明确输出几何语义；补输出端口或结果模型 |
| `CircleMeasurement` | 契约 | 声明输出 `Center` / `Circle`，运行时主要输出 `CenterX/CenterY/...` | 统一圆结果模型和输出端口 |
| `ColorDetection` | 契约漂移 | `ColorInfo` 声明与大量运行时附加键不一致 | 设计稳定的 `ColorInfo` 结果结构，减少散落键 |
| `Statistics` | 条件输出契约 | `USL` / `LSL`、`Cpk`、`IsCapable` 实际已实现，但只有在提供规格限、样本数足够且标准差大于 0 时才输出，当前文档和下游约束未强调这种条件性输出 | 明确条件输出规则；必要时增加 `HasCapabilityMetrics` 或在无规格限时输出 `null` / 默认值 |
| `Aggregator` | 契约 + 实现 | `Mode` 已声明但未读取；当前实现会稳定同时输出 `MergedList/MaxValue/MinValue/Average`，与「按模式输出」的 UI 认知不一致 | 明确聚合模式是否保留；若保留则让 `Mode` 真正生效，否则从元数据中移除 |
| `ContourDetection` | 契约 + 实现 | 运行时隐藏读取 `DrawContours`、`MaxValue`、`ThresholdType`，但元数据未声明，导致二值化和绘制行为不可调 | 暴露轮廓提取前处理关键参数，统一阈值与绘制行为契约 |
| `BlobAnalysis` | 契约 + 算法边界 | `MinCircularity`、`MinConvexity`、`MinInertiaRatio` 运行时生效但未声明；`Color` 已声明但当前实现未参与 Blob 过滤 | 补齐 `SimpleBlobDetector` 相关参数元数据，并验证参数到 OpenCV 行为映射；明确 `Color` 参数是否保留 |
| `ArrayIndexer` | 契约 | `List` 与 `Items` 输入存在双键语义；`Item` 输出声明与运行时结果键不一致 | **已完成**：输入统一为 `List`（向后兼容 `Items`）；输出统一为 `Item`（原 `Result`）；补充 `Found/Index` 端口声明。 | ✅ |
| `Comparator` | 契约 | 声明 `Result` / `Difference`，运行时不稳定 | 输出模型收敛 |
| `ConditionalBranch` | 契约 | 声明 `True/False` 分支输出，但运行时主要输出结果字段 | 明确分支行为是否输出端口化 |
| `Comment` / `Delay` / `ResultOutput` / `TryCatch` | 契约 | 声明输出与运行时返回不完全一致 | 做一轮流程控制类算子的统一收敛 |

## 10. P2 优先级清单

P2 定义：更多属于系统性元数据收敛和工程清洁度问题，建议批量机械修复。

### 10.1 隐藏参数批量收敛

静态扫描命中的高优先级清单：

- `ArrayIndexer`: `LabelFilter` ✅ 已暴露
- `BlobAnalysis`: `MinCircularity`、`MinConvexity`、`MinInertiaRatio`
  - 补充说明：这组参数已实际传入 `SimpleBlobDetector.Params`，但当前元数据未暴露，且应额外验证与 OpenCvSharp 底层行为的一致性。
- `EdgeDetection`: `L2Gradient`
- `ClaheEnhancement`: `Channel`
- `ColorConversion`: `SourceChannels`
- `DeepLearning`: `UseGpu`、`GpuDeviceId`
- `OrbFeatureMatch`: `EnableSymmetryTest`、`MinMatchCount`
- `PyramidShapeMatch`: 隐藏参数待与实现一起核对
- `ContourDetection`: `DrawContours`、`MaxValue`、`ThresholdType`
- `ForEach`: 隐藏参数待核对
- `Filtering`: 隐藏参数待核对
- `HistogramEqualization`: 隐藏参数待核对
- `HttpRequest`: 隐藏参数待核对
- `ImageAcquisition`: `exposureTime`、`gain`、`triggerMode`
- `ImageSave`: 隐藏参数待核对
- `JsonExtractor`: 隐藏参数待核对
- `LaplacianSharpen`: 隐藏参数待核对
- `MeanFilter`: 隐藏参数待核对
- `MqttPublish`: 隐藏参数待核对
- `StringFormat`: `DateFormat`、`Mode`、`Separator`

建议动作：

1. 逐个确认是否应正式暴露。
2. 若不应暴露，则移除运行时读取并回归默认行为。
3. 补 UI 元数据测试，防止隐藏参数再次出现。

### 10.2 已声明但未读取参数批量收敛

高优先级清单：

- `Aggregator`: `Mode` ✅ 已实现
- `CoordinateTransform`: `PixelX`、`PixelY`
- `HistogramAnalysis`: 部分参数待复核
- `HistogramEqualization`: 部分参数待复核
- `ImageSave`: 部分参数待复核
- `ImageTiling`: 部分参数待复核
- `NPointCalibration`: 部分参数待复核
- `PixelStatistics`: `RoiX`、`RoiY`、`RoiW`、`RoiH`
- `PositionCorrection`: `CurrentAngle`
- `ResultOutput`: `Format`、`SaveToFile`
- `SharpnessEvaluation`: `RoiX`、`RoiY`、`RoiW`、`RoiH`
- `TemplateMatching`: `MaxMatches` ✅ 已实现
- `WidthMeasurement`: `Direction` ✅ 已实现

建议动作：

1. 标记为「待实现」或「废弃」。
2. 优先处理会误导用户决策的参数。
3. 批量加回归测试，确保参数真正影响输出。

### 10.3 隐藏输入批量收敛

静态扫描命中：

- `ArrayIndexer`: `Items` ✅ 已收敛（优先 `List`，兼容 `Items`）
- `HttpRequest`: 隐藏输入键待核对
- `ImageAcquisition`: 隐藏输入键待核对
- `MqttPublish`: 隐藏输入键待核对
- `NPointCalibration`: 隐藏输入键待核对
- `PositionCorrection`: `BaseAngle`
- `TriggerModule`: `Signal`、`Trigger` ✅ 已收敛（正式声明 `Signal`）
- `TypeConvert`: `Value` ✅ 已收敛（移除 `Value`，统一为 `Input`）

建议动作：

1. 所有隐藏输入都必须显式端口化，或彻底删除。
2. 下游流程编辑器只应暴露一种输入契约。

## 11. 算法边界重点说明

以下问题不一定是 bug，但会真实影响用户对「效果」的判断。

### 11.1 模板匹配类

- `TemplateMatching`：适合单目标、尺度固定、旋转变化小的场景，不适合多目标和尺度旋转变化大的任务。
- `ShapeMatching`：当前更像旋转模板匹配，不是完整 shape descriptor 匹配。
- `GradientShapeMatch`：适合边缘结构稳定、尺度变化不大的目标；不支持多候选输出。
- `AkazeFeatureMatch` / `OrbFeatureMatch`：适合纹理型目标，不适合弱纹理、重复纹理、几何中心严格需求场景。

### 11.2 测量类

- `WidthMeasurement`：当前是统计型点到线距离测量，不是严格法向双边测量。
- `GeometricFitting`：当前是「图像 -> 二值 -> 轮廓 -> 合并点集 -> 拟合」，不适合作为高精度点集拟合器的替代品。
- `CircleMeasurement` / `LineMeasurement` 等：应区分「显示结果」和「结构化测量结果」。

### 11.3 标定与几何变换类

- `CameraCalibration`：单图模式应定义为「调参/验证模式」，不要作为高质量生产标定默认路径。
- `Undistort`：当前只做标准 pinhole 模型去畸变，不是高级重映射平台。
- `PolarUnwrap`：中心点与半径参数对效果高度敏感，当前不自动估计中心。

### 11.4 AI 类

- `DeepLearning`：整体工程化程度较高，但应把 GPU、模式切换、标签优先级这些运行时关键行为显式端口化/参数化。

## 12. 建议实施顺序

### Batch A：契约止血（先做）

目标：先解决「UI 看起来是 A，运行时是 B」的问题。

建议覆盖：

- `TemplateMatching`
- `WidthMeasurement`
- `OrbFeatureMatch`
- `AkazeFeatureMatch`
- `GradientShapeMatch`
- `ImageAcquisition`
- `ContourDetection`
- `TypeConvert`
- `TriggerModule`
- `Aggregator`

### Batch B：输出结构统一

目标：统一 `Position / X / Y / CenterX / CenterY / FitResult / ColorInfo / DetectionList` 等结果模型。

建议覆盖：

- `CircleMeasurement`
- `ColorDetection`
- `GeometricFitting`
- `DeepLearning`
- `Comment` / `Delay` / `ResultOutput` / `TryCatch`

### Batch C：算法能力补齐

目标：修真正会影响「效果」的能力缺口。

建议覆盖：

- `TemplateMatching` 的多匹配支持
- `WidthMeasurement` 的方向/法向策略与亚像素选项
- `ShapeMatching` 的尺度搜索或能力重命名
- `GeometricFitting` 的单轮廓/最大轮廓选择
- `GradientShapeMatch` 的缓存模型与多模板安全性
- `BlobAnalysis` 的参数映射核验与 `Color` 行为收敛

### Batch D：低风险机械收敛

目标：清理隐藏参数、未用参数、隐藏输入、输出声明噪声。

建议方式：

1. 一次只做一种问题类型。
2. 每批附带自动化扫描和契约测试。
3. 不与算法升级混在同一个 PR 中。

## 13. 建议测试策略

每类整改都建议配套新增测试：

### 13.1 契约测试

- 参数面板存在的参数，运行时必须能被读取并影响结果。
- 声明端口必须能在 `OutputData` 中稳定找到对应键或对应结构。
- 模式切换不能 silently 改变输出字段集合，除非文档和契约明确说明。

### 13.2 兼容性测试

- 老工程 JSON 在参数增删后能否平稳兼容。
- 目录生成器是否正确反映新的参数/输出契约。

### 13.3 算法回归测试

- 模板匹配类：旋转、尺度、遮挡、重复纹理样本
- 测量类：噪声、边缘模糊、ROI 变化样本
- AI 类：模式切换、标签文件切换、GPU/CPU 运行模式

## 14. 评审建议

在进入实现前，建议你先拍板以下决策：

1. 是否优先兼容旧工程，还是允许一次性收紧输出契约？
2. 对于 `Position` / `X` / `Y` / `CenterX` / `CenterY`，是否统一采用结构化输出对象？
3. 对于已声明但未生效参数，是优先实现，还是先标记 deprecated？
4. 对于「执行成功但业务 NG」的语义，是否统一形成框架约定？
5. 对于 `ShapeMatching` 这类名称和实现不完全一致的算子，是改名还是补能力？

## 15. 实施记录

### 2026-03-15 完成 Batch A 契约止血

本次更新完成了计划文档中 **Batch A：契约止血** 的全部算子修复：

| 算子 | 修复内容 | 状态 |
|------|---------|------|
| `TemplateMatching` | 统一 `Method` 枚举（4 种方法全支持）；实现 `MaxMatches` 多目标匹配；`Position` 标准化为中心点。 | ✅ |
| `WidthMeasurement` | 实现 `Direction`/`CustomAngle`；亚像素检测；`ImageWidth/ImageHeight` 与 `Width` 分离 | ✅ |
| `GradientShapeMatch` | 修复缓存键（含模板哈希）；显式输出 `Position`；LRU缓存（最多4条） | ✅ |
| `OrbFeatureMatch` | 暴露 `EnableSymmetryTest`/`MinMatchCount`；统一 `Position` 输出；对齐失败语义 | ✅ |
| `AkazeFeatureMatch` | 补全 `Position` 输出；`Score` 定义为内点比例；统一失败语义 | ✅ |
| `DeepLearning` | 暴露 `UseGpu`/`GpuDeviceId`；显式声明 `DetectionList` 端口 | ✅ |
| `ImageAcquisition` | 元数据声明 `ExposureTime`/`Gain`/`TriggerMode`；保留参数名兼容映射 | ✅ |
| `TypeConvert` | 统一输入键为 `Input`；补全 `AsString/AsFloat/AsInteger/AsBoolean/OriginalType` 输出端口 | ✅ |
| `TriggerModule` | 正式声明 `Signal` 输入端口；移除 `Trigger` 隐藏逻辑 | ✅ |
| `Aggregator` | 实现 `Mode` 参数；稳定输出 `MergedList/MaxValue/MinValue/Average` | ✅ |
| `ContourDetection` | 暴露 `DrawContours`/`MaxValue`/`ThresholdType` 参数 | ✅ |
| `BlobAnalysis` | 暴露 `MinCircularity`/`MinConvexity`/`MinInertiaRatio` 参数 | ✅ |
| `ArrayIndexer` | 输入统一为 `List`（向后兼容 `Items`）；输出统一为 `Item`；补全 `Found/Index` 端口 | ✅ |

**配套测试**：
- `Sprint2_ArrayIndexerTests.cs`：10个测试用例，覆盖索引/最大置信度/最大面积/最小面积/标签过滤/空列表/越界/向后兼容/契约一致性。
- `OperatorContractReconciliationTests.cs`：15个契约回归测试，覆盖元数据声明与运行时行为一致性验证。

## 16. 结论
