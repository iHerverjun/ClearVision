# ClearVision 算子库算法深度审计计划

<!-- DOC_AUDIT_STATUS_START -->
## 文档审计状态（自动更新）
- 审计日期：2026-03-02
- 完成状态：未完成
- 任务统计：总计 0，已完成 0，未完成 0，待办关键词命中 1
- 判定依据：检测到待办关键词（TODO/待办/未完成/TBD/FIXME/WIP）
<!-- DOC_AUDIT_STATUS_END -->



基于 NotebookLM 笔记本中归纳的工业机器视觉算法原理与最佳实践，对现有 121 个算子的算法实现进行**科学性、高效性、工程规范性**三维审计。

---

## 审计执行结果（2026-02-26，源码复核版）

### 执行状态总览

| 批次 | 状态 | 报告 |
|------|------|------|
| 第 1 批：图像预处理与滤波 | ✅ 已完成 | `docs/AlgorithmAudit/Batch1_Preprocessing.md` |
| 第 2 批：边缘检测与亚像素处理 | ✅ 已完成 | `docs/AlgorithmAudit/Batch2_EdgeDetection.md` |
| 第 3 批：特征匹配与模板匹配 | ✅ 已完成 | `docs/AlgorithmAudit/Batch3_Matching.md` |
| 第 4 批：Blob/轮廓/几何测量 | ✅ 已完成 | `docs/AlgorithmAudit/Batch4_Measurement.md` |
| 第 5 批：标定与几何变换 | ✅ 已完成 | `docs/AlgorithmAudit/Batch5_Calibration.md` |
| 第 6 批：深度学习与缺陷检测 | ✅ 已完成 | `docs/AlgorithmAudit/Batch6_DeepLearning.md` |
| 第 7 批：颜色/算术/工具类算子 | ✅ 已完成 | `docs/AlgorithmAudit/Batch7_General.md` |
| 全局勘误与交叉核查 | ✅ 已完成 | `docs/AlgorithmAudit/Errata_CrossVerification.md` |

本次更新在既有 7 份批次报告基础上，对历史高优先级问题进行了源码级复核（以当前分支代码为准），避免将已修复问题继续作为待办项保留。

### 关键问题闭环情况（已确认）

| 项目 | 当前状态 | 代码证据 |
|------|----------|----------|
| `BoxFilterOperator` 概念混淆（候选框 vs 均值滤波） | ✅ 已闭环：候选框算子已更名为 `BoundingBoxFilterOperator`，且已新增图像级均值滤波 `MeanFilterOperator` | `Acme.Product/src/Acme.Product.Infrastructure/Operators/BoundingBoxFilterOperator.cs:18`, `Acme.Product/src/Acme.Product.Infrastructure/Operators/MeanFilterOperator.cs:30` |
| `FrameAveragingOperator` Median 性能瓶颈 | ✅ 已闭环：采用时间堆叠 + `Cv2.Sort` 取中值，不再使用逐像素 `Array.Sort` | `Acme.Product/src/Acme.Product.Infrastructure/Operators/FrameAveragingOperator.cs:165`, `Acme.Product/src/Acme.Product.Infrastructure/Operators/FrameAveragingOperator.cs:167` |
| `CannyEdgeOperator` 仅手动双阈值 | ✅ 已闭环：新增 `AutoThreshold` / `AutoThresholdSigma` 与中值自适应阈值计算 | `Acme.Product/src/Acme.Product.Infrastructure/Operators/CannyEdgeOperator.cs:22`, `Acme.Product/src/Acme.Product.Infrastructure/Operators/CannyEdgeOperator.cs:87` |
| `ShapeMatchingOperator` 的 `NumLevels` 参数未生效 | ✅ 已闭环：已实现金字塔构建与粗到细角度搜索 | `Acme.Product/src/Acme.Product.Infrastructure/Operators/ShapeMatchingOperator.cs:84`, `Acme.Product/src/Acme.Product.Infrastructure/Operators/ShapeMatchingOperator.cs:187` |
| `CameraCalibrationOperator` 单图模式不输出标定矩阵 | ✅ 已闭环：单图模式执行 `Cv2.CalibrateCamera` 并输出 `CameraMatrix` | `Acme.Product/src/Acme.Product.Infrastructure/Operators/CameraCalibrationOperator.cs:117`, `Acme.Product/src/Acme.Product.Infrastructure/Operators/CameraCalibrationOperator.cs:139` |
| `UndistortOperator` 标定矩阵契约不兼容 | ✅ 已闭环：解析逻辑同时支持 9 元素扁平矩阵与 3x3 嵌套矩阵 | `Acme.Product/src/Acme.Product.Infrastructure/Operators/UndistortOperator.cs:146`, `Acme.Product/src/Acme.Product.Infrastructure/Operators/UndistortOperator.cs:163` |
| `TypeConvertOperator` 输入端口名不匹配导致失效 | ✅ 已闭环：优先读取 `Input`，兼容回退 `Value` | `Acme.Product/src/Acme.Product.Infrastructure/Operators/TypeConvertOperator.cs:94`, `Acme.Product/src/Acme.Product.Infrastructure/Operators/TypeConvertOperator.cs:100` |
| `OcrRecognitionOperator` 并发调用风险 | ✅ 已闭环：`OcrEngineProvider` 对引擎访问已加锁 | `Acme.Product/src/Acme.Product.Infrastructure/Services/OcrEngineProvider.cs:37` |
| `ImageSubtractOperator` 彩色图 `MinMaxLoc` 崩溃风险 | ✅ 已闭环：统计前先转灰度再执行 `MinMaxLoc` | `Acme.Product/src/Acme.Product.Infrastructure/Operators/ImageSubtractOperator.cs:70`, `Acme.Product/src/Acme.Product.Infrastructure/Operators/ImageSubtractOperator.cs:77` |
| `TextSaveOperator` 并发写文件冲突 | ✅ 已闭环：按文件路径维护锁并串行写入 | `Acme.Product/src/Acme.Product.Infrastructure/Operators/TextSaveOperator.cs:30`, `Acme.Product/src/Acme.Product.Infrastructure/Operators/TextSaveOperator.cs:186` |

结论：历史报告中的核心 P1 缺陷在当前代码基线下已完成闭环，复核范围内未发现新增 P1。

### 优化项完成情况（2026-02-27）

| 原优先级 | 项目 | 完成状态 | 实现位置 |
|--------|------|----------|----------|
| P2 | 形态学双算子并存（兼容保留） | ✅ 已落地兼容下线信号：Legacy 算子增加 `Tags=Legacy/Deprecated` 与一次性运行告警，保留兼容不破坏旧流程。 | `Acme.Product/src/Acme.Product.Infrastructure/Operators/MorphologyOperator.cs`, `Acme.Product/src/Acme.Product.Infrastructure/Services/OperatorFactory.cs` |
| P2 | 透视变换参数建模繁琐（16 个点坐标参数） | ✅ 已支持点集输入：新增 `SrcPoints/DstPoints` 输入端口 + `SrcPointsJson/DstPointsJson` 参数；旧 `SrcX1..DstY4` 仍可回退兼容。 | `Acme.Product/src/Acme.Product.Infrastructure/Operators/PerspectiveTransformOperator.cs`, `Acme.Product/src/Acme.Product.Infrastructure/Services/OperatorFactory.cs` |
| P2 | 极坐标展开性能瓶颈 | ✅ 已引入 `WarpPolar` 加速路径，并保留 `Remap` 自动回退机制（异常或不适配场景自动降级）。 | `Acme.Product/src/Acme.Product.Infrastructure/Operators/PolarUnwrapOperator.cs`, `Acme.Product/src/Acme.Product.Infrastructure/Services/OperatorFactory.cs` |
| P3 | 图像加法尺寸不一致默认 Resize 失真 | ✅ 已新增策略化处理：`Resize/Fail/Crop/AnchorPaste` + `OffsetX/OffsetY`，默认保持兼容。 | `Acme.Product/src/Acme.Product.Infrastructure/Operators/ImageAddOperator.cs`, `Acme.Product/src/Acme.Product.Infrastructure/Services/OperatorFactory.cs` |
| P3 | 统计算子状态回收缺失 | ✅ 已新增显式回收策略：`StateTtlMinutes` 参数 + 周期性清理过期状态。 | `Acme.Product/src/Acme.Product.Infrastructure/Operators/StatisticsOperator.cs`, `Acme.Product/src/Acme.Product.Infrastructure/Services/OperatorFactory.cs` |

本批优化已完成代码落地与定向单测验证，当前“仍需持续优化项”清单清零。

### 清单差异说明（计划 vs 当前代码）

- 计划表中的 `BoxFilterOperator.cs` 已演进为 `BoundingBoxFilterOperator.cs`（检测框过滤语义更清晰）。
- 图像级均值滤波能力已由 `MeanFilterOperator.cs` 提供，补齐了 Box/Mean Filter 的图像处理场景。
- `MorphologyOperator.cs` 目前处于 Legacy 兼容态，`MorphologicalOperationOperator.cs` 为推荐主入口。

---

## 审计维度

每个算子将从以下 5 个维度评分（1-5 分）：

| 维度 | 说明 |
|------|------|
| **算法科学性** | 核心算法是否符合论文/教科书的最佳实践 |
| **性能效率** | 是否有不必要的内存拷贝、冗余计算、可并行化但未并行的瓶颈 |
| **参数合理性** | 默认参数是否工业合理，是否暴露了关键调参接口 |
| **鲁棒性** | 对噪声、光照变化、边界情况的处理是否充分 |
| **代码工程质量** | 资源释放（Mat/IDisposable）、异常处理、日志、可维护性 |

---

## 分批审计计划（共 7 批）

### 第 1 批：图像预处理与滤波（12 个算子）

> **审计重点**：滤波器选型是否科学、是否有 O(N) 优化、是否避免过度平滑破坏高频细节

| # | 算子文件 | NotebookLM 关键审计点 |
|---|---------|----------------------|
| 1 | `GaussianBlurOperator.cs` | 过度平滑是否会破坏边缘？是否支持自适应 sigma？ |
| 2 | `BilateralFilterOperator.cs` | 是否保持边缘结构？参数 sigmaColor/sigmaSpace 是否合理？ |
| 3 | `BoxFilterOperator.cs` | 是否采用直方图统计O(N)优化？是否易受异常值影响？ |
| 4 | `MedianBlurOperator.cs` | 是否优先用于椒盐噪声场景？核大小是否可配置？ |
| 5 | `AdaptiveThresholdOperator.cs` | 是否采用滑动窗口均值/Shannon熵？是否克服固定阈值缺陷？ |
| 6 | `ThresholdOperator.cs` | 是否集成 Otsu 自适应阈值？是否仍依赖手动设置？ |
| 7 | `ClaheEnhancementOperator.cs` | tileGridSize 和 clipLimit 是否可调？是否避免过曝？ |
| 8 | `HistogramEqualizationOperator.cs` | 是否有全局 HE 的过曝问题？是否应推荐 CLAHE 替代？ |
| 9 | `HistogramAnalysisOperator.cs` | 统计功能是否完备？是否支持多通道？ |
| 10 | `ImageNormalizeOperator.cs` | 归一化方式是否科学（MinMax vs Z-Score）？ |
| 11 | `ShadingCorrectionOperator.cs` | 光照不均校正算法是否有效？是否支持多种校正模型？ |
| 12 | `FrameAveragingOperator.cs` | 时域降噪是否高效？是否支持加权/指数移动平均？ |

---

### 第 2 批：边缘检测与亚像素处理（10 个算子）

> **审计重点**：Canny 是否集成 Otsu 自适应阈值、亚像素是否采用 Zernike 正交矩、是否有粗-精分级策略

| # | 算子文件 | NotebookLM 关键审计点 |
|---|---------|----------------------|
| 1 | `CannyEdgeOperator.cs` | 双阈值是否支持 Otsu 自动计算？是否避免手动调参？ |
| 2 | `SubpixelEdgeDetectionOperator.cs` | 是否采用 Zernike 矩（7×7/9×9 掩模）？是否有粗-精策略？ |
| 3 | `LaplacianSharpenOperator.cs` | 锐化是否引入噪声放大？是否有预滤波？ |
| 4 | `CornerDetectionOperator.cs` | Harris/Shi-Tomasi 参数是否合理？角点精度如何？ |
| 5 | `EdgeIntersectionOperator.cs` | 交点计算精度？是否支持亚像素？ |
| 6 | `EdgePairDefectOperator.cs` | 缺陷检测逻辑是否科学？边缘配对算法？ |
| 7 | `CaliperToolOperator.cs` | 卡尺工具的投影边缘搜索策略？亚像素插值？ |
| 8 | `ParallelLineFindOperator.cs` | 平行线检测精度？是否利用形态学梯度？ |
| 9 | `LineMeasurementOperator.cs` | 直线拟合方法（最小二乘 vs RANSAC）？ |
| 10 | `MorphologicalOperationOperator.cs` + `MorphologyOperator.cs` | 结构元素分解优化？形态学梯度用于边缘估计？ |

---

### 第 3 批：特征匹配与模板匹配（7 个算子）

> **审计重点**：是否支持旋转/缩放不变性、是否使用 NCC 优化、是否采用对数极坐标变换

| # | 算子文件 | NotebookLM 关键审计点 |
|---|---------|----------------------|
| 1 | `TemplateMatchOperator.cs` | 是否使用 NCC？是否有多级分区加速？Cauchy-Schwarz 剪枝？ |
| 2 | `ShapeMatchingOperator.cs` | 是否支持旋转/缩放不变？是否使用对数极坐标变换？ |
| 3 | `Features/OrbFeatureMatchOperator.cs` | ORB 配优于 SIFT/SURF？是否用于边缘设备？ |
| 4 | `Features/AkazeFeatureMatchOperator.cs` | AKAZE vs ORB 的选型合理性？ |
| 5 | `Features/GradientShapeMatchOperator.cs` | 梯度匹配是否高效？是否有金字塔加速？ |
| 6 | `Features/PyramidShapeMatchOperator.cs` | 金字塔搜索策略是否科学？ |
| 7 | `Features/FeatureMatchOperatorBase.cs` | 基类设计是否合理？公共逻辑复用？ |

---

### 第 4 批：Blob 分析、轮廓与几何测量（14 个算子）

> **审计重点**：连通域标记效率、轮廓提取前的形态学清洗、几何拟合精度

| # | 算子文件 | NotebookLM 关键审计点 |
|---|---------|----------------------|
| 1 | `BlobDetectionOperator.cs` | 是否直接从面积计算几何属性而非 Hough？ |
| 2 | `BlobLabelingOperator.cs` | 连通域标记是否高效？是否有并行优化空间？ |
| 3 | `FindContoursOperator.cs` | 提取前是否做形态学腐蚀膨胀清洗？ |
| 4 | `ContourMeasurementOperator.cs` | 轮廓测量精度？ApproxPoly 参数？ |
| 5 | `CircleMeasurementOperator.cs` | 圆拟合方法？最小二乘 vs RANSAC？ |
| 6 | `AngleMeasurementOperator.cs` | 角度计算精度与鲁棒性？ |
| 7 | `GapMeasurementOperator.cs` | 间隙测量的边缘搜索策略？ |
| 8 | `WidthMeasurementOperator.cs` | 宽度测量精度？亚像素支持？ |
| 9 | `MeasureDistanceOperator.cs` | 距离计算是否支持校准后的物理单位？ |
| 10 | `GeometricFittingOperator.cs` | 拟合算法选择？异常值剔除策略？ |
| 11 | `GeometricToleranceOperator.cs` | 公差判定逻辑是否符合工业标准？ |
| 12 | `GeoMeasurementOperator.cs` | 综合几何测量的完备性？ |
| 13 | `PointSetToolOperator.cs` | 点集操作的效率？ |
| 14 | `RectangleDetectionOperator.cs` + `QuadrilateralFindOperator.cs` | 矩形/四边形检测策略？ |

---

### 第 5 批：标定、坐标变换与图像几何变换（10 个算子）

> **审计重点**：张正友标定法 + 重投影误差 < 0.5px、Hand-Eye 是否使用同步求解器、畸变矫正

| # | 算子文件 | NotebookLM 关键审计点 |
|---|---------|----------------------|
| 1 | `CameraCalibrationOperator.cs` | 是否用张正友法？重投影误差是否 <0.5px？残差分布？ |
| 2 | `CalibrationLoaderOperator.cs` | 标定数据加载/序列化是否可靠？ |
| 3 | `NPointCalibrationOperator.cs` | N点标定精度？优化器选择（LM）？ |
| 4 | `TranslationRotationCalibrationOperator.cs` | 是否避免分离式求解？是否用双四元数？ |
| 5 | `CoordinateTransformOperator.cs` | 坐标变换精度与鲁棒性？ |
| 6 | `UndistortOperator.cs` | 畸变矫正是否完善？是否支持鱼眼？ |
| 7 | `AffineTransformOperator.cs` | 仿射变换实现是否高效？ |
| 8 | `PerspectiveTransformOperator.cs` | 透视变换精度？ |
| 9 | `PolarUnwrapOperator.cs` | 对数极坐标展开是否正确？是否用于圆柱形零件？ |
| 10 | `PointCorrectionOperator.cs` + `PositionCorrectionOperator.cs` + `PointAlignmentOperator.cs` | 位置校正逻辑？ |

---

### 第 6 批：深度学习推理与缺陷检测（7 个算子）

> **审计重点**：NMS 是否有 Fast-NMS/Cluster-NMS 并行优化、低光照预处理、INT8/FP16 量化、ONNX 部署效率

| # | 算子文件 | NotebookLM 关键审计点 |
|---|---------|----------------------|
| 1 | `DeepLearningOperator.cs`（34KB 大文件） | ONNX 推理效率？NMS 瓶颈？FP16/INT8 量化支持？ |
| 2 | `BoxNmsOperator.cs` | NMS 是否采用 Fast-NMS 或 Cluster-NMS 并行化？ |
| 3 | `SurfaceDefectDetectionOperator.cs` | 表面缺陷检测是否有背景减除预处理？ |
| 4 | `DualModalVotingOperator.cs` | 双模态投票融合逻辑是否科学？ |
| 5 | `OcrRecognitionOperator.cs` | OCR 识别引擎选型？预处理链？ |
| 6 | `CodeRecognitionOperator.cs` | 条码/二维码识别效率与鲁棒性？ |
| 7 | `ImageDiffOperator.cs` | 图像差分是否用于背景减除？是否支持频谱分析？ |

---

### 第 7 批：颜色处理、图像算术与工具类算子（15+ 个算子）

> **审计重点**：RGB→HSV/CIELAB 分离亮度与色度、颜色检测鲁棒性、图像算术运算溢出处理

| # | 算子文件 | NotebookLM 关键审计点 |
|---|---------|----------------------|
| 1 | `ColorConversionOperator.cs` | 是否脱离 RGB 进行分析？是否支持 HSV/CIELAB？ |
| 2 | `ColorDetectionOperator.cs` | 颜色检测是否在 HSV 空间？是否抗光照变化？ |
| 3 | `ColorMeasurementOperator.cs` | 色彩测量精度？Delta-E 标准？ |
| 4 | `ImageAddOperator.cs` | 加法运算溢出处理？饱和运算？ |
| 5 | `ImageSubtractOperator.cs` | 减法运算下溢处理？ |
| 6 | `ImageBlendOperator.cs` | 混合权重策略？ |
| 7 | `PixelStatisticsOperator.cs` | 像素统计完备性？ |
| 8 | `SharpnessEvaluationOperator.cs` | 清晰度评估是否在亮度通道？是否用 Laplacian 方差？ |
| 9 | `ImageCropOperator.cs` | 裁剪边界检查？ |
| 10 | `ImageResizeOperator.cs` | 插值方法选择（双线性 vs 双三次）？ |
| 11 | `ImageRotateOperator.cs` | 旋转是否保持图像完整？ |
| 12 | `ImageStitchingOperator.cs` | 拼接算法选型？特征匹配 vs 直接拼接？ |
| 13 | `ImageComposeOperator.cs` | 合成逻辑？ |
| 14 | `CopyMakeBorderOperator.cs` | 边界填充策略（REFLECT vs CONSTANT）？ |
| 15 | `ImageTilingOperator.cs` | 切片策略？重叠区域处理？ |

---

> [!NOTE]
> 以下 **非视觉算法类算子**（通信、流程控制、IO）不在本次算法深度审计范围内，但会记录其工程质量：
> `CommentOperator`, `ComparatorOperator`, `ConditionalBranchOperator`, `CycleCounterOperator`, `DatabaseWriteOperator`,
> `DelayOperator`, `ForEachOperator`, `HttpRequestOperator`, `JsonExtractorOperator`, `LogicGateOperator`,
> `MathOperationOperator`, `MqttPublishOperator`, `ModbusCommunicationOperator`, `MitsubishiMcCommunicationOperator`,
> `OmronFinsCommunicationOperator`, `SiemensS7CommunicationOperator`, `SerialCommunicationOperator`,
> `TcpCommunicationOperator`, `ScriptOperator`, `StringFormatOperator`, `TriggerModuleOperator`, `TryCatchOperator`,
> `TypeConvertOperator`, `UnitConvertOperator`, `VariableReadOperator`, `VariableWriteOperator`, `VariableIncrementOperator`,
> `ResultJudgmentOperator`, `ResultOutputOperator`, `StatisticsOperator`, `TimerStatisticsOperator`,
> `TextSaveOperator`, `ImageSaveOperator`, `ImageAcquisitionOperator`, `RoiManagerOperator`,
> `AggregatorOperator`, `ArrayIndexerOperator`, `LineLineDistanceOperator`, `PointLineDistanceOperator`,
> `PlcCommunicationOperatorBase`, `OperatorBase`, `ImageWrapper`

---

## 审计执行方式

每批审计将按如下流程执行：

```mermaid
graph LR
    A[阅读算子源码] --> B[对照 NotebookLM 原理]
    B --> C[逐项评分 5 维度]
    C --> D[记录问题与改进建议]
    D --> E[输出审计报告]
```

1. **阅读源码**：逐个打开算子 `.cs` 文件，分析核心算法实现
2. **对照 NotebookLM**：根据笔记本中的论文原理和最佳实践进行对照
3. **逐项评分**：按 5 个维度打分（1-5）
4. **记录改进建议**：具体的代码级改进方案
5. **输出批次报告**：每批完成后输出一份结构化的审计报告

## 验证方式

- 审计报告以 Markdown 文档输出到 `docs/` 目录
- 每批次审计前会先由用户确认是否开始
- 关键改进建议后续可作为独立开发任务跟踪
