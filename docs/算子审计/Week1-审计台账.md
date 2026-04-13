---
title: "Week1 审计台账"
doc_type: "ledger"
status: "completed"
topic: "算子审计"
created: "2026-04-13"
updated: "2026-04-13"
---

# Week1 审计台账

## 1. 周一冻结结论

| 状态 | 结论 | 证据路径 | 复现方式 | 阻塞项 | 负责人 | 截止日期 |
| --- | --- | --- | --- | --- | --- | --- |
| [x] | Week1 首批 P0 审计包冻结为 `预处理(23)`、`定位(7)`、`特征提取(4)`；复核包冻结为 `匹配定位(8)`、`AI检测(6)`；补证包冻结为 `检测(20)`、`识别(2)`、`标定(12)`。 | `docs/算子审计/启动计划/Week1-长时启动计划.md`；`docs/算子审计/TODO.md`；`算子资料/算子名片/CATALOG.md:6-35` | 对照 `算子资料/算子目录.json` 中 `categories` 的计数，核对 `23/7/4/8/6/20/2/12` 八组数字一致。 | 无 | A/B/C 线负责人 | 2026-04-13 |
| [x] | 周一执行口径冻结为“模板定稿、范围锁定、责任归属、交付路径固定”，不把代码修复数量作为当日目标。 | `docs/算子审计/审计执行标准.md`；`docs/算子审计/启动计划/Week1-长时启动计划.md` | 检查 `Week1-审计台账.md`、`Week1-已升级证据链.md`、`Week2-修复优先级清单.md`、`Week1-审计周报.md` 4 个路径均已创建。 | 无 | A/B/C 线负责人 | 2026-04-13 |

## 2. 审计模板与引用规范

| 项目 | 冻结口径 |
| --- | --- |
| 状态字段 | 统一使用 `[x] / [-] / [ ] / [!]`。 |
| 风险分级 | 统一使用 `P0 / P1 / P2`。 |
| 证据路径 | 必须使用仓库相对路径，并精确到行号级，格式为 `path:line` 或 `path:line-line`。 |
| 复现方式 | 优先写成 `脚本 -> 测试类 -> 样本/手工步骤`；无统一脚本时允许写 `run-dotnet-test-serial.ps1 + FullyQualifiedName`。 |
| 样本维度 | 每个算子至少覆盖 `正常 / 边界 / 异常` 三类最小样本。 |
| 静态盘点项 | `实现 / 参数 / 异常路径 / 已知限制 / 输入依赖 / 错误收口`。 |
| 验收门禁 | 每条结论必须同时具备 `证据路径 / 可复现方式 / 责任归属 / 截止日期`。 |

| 模板字段 | 填写说明 |
| --- | --- |
| 结论 | 只写已核验证据结论，不写口头推断。 |
| 证据路径 | 至少 1 条文档、源码或测试证据；周内优先补到测试/脚本。 |
| 复现方式 | 写明脚本名、测试类名、样本路径或最小操作步骤。 |
| 阻塞项 | 统一归类到 `数据阻塞 / 模型阻塞 / 硬件阻塞 / 依赖阻塞`。 |
| 负责人 | 使用角色名回填：`A线负责人（算法审计）` / `B线负责人（链路稳定性）` / `C线负责人（证据与复核）`。 |
| 截止日期 | 周内默认按自然日回填；本周冻结默认日截点为 `18:00（UTC+8）`。 |

## 3. 责任与交付路径

| 线别 | 负责人角色 | 审计范围 | 日截点 | 今日必交付 | 交付路径 |
| --- | --- | --- | --- | --- | --- |
| A线 | A线负责人（算法审计） | `预处理(23)` | 2026-04-13 18:00（UTC+8） | 审计清单与优先级冻结 | `docs/算子审计/Week1-审计台账.md` |
| B线 | B线负责人（链路稳定性） | `定位(7) + 特征提取(4)` | 2026-04-13 18:00（UTC+8） | 链路分组与回归入口冻结 | `docs/算子审计/Week1-审计台账.md` |
| C线 | C线负责人（证据与复核） | `已升级证据链 + 升级中复核` | 2026-04-13 18:00（UTC+8） | 证据引用规范冻结 | `docs/算子审计/Week1-已升级证据链.md` |

## 4. A线范围冻结（预处理 23）

### 4.1 分组优先级

| 分组 | 优先级 | 算子数 | 审计目标 | 冻结算子 |
| --- | --- | ---: | --- | --- |
| A1 上游输入一致性核心组 | 高 | 8 | 优先核查输入一致性、光照/阈值敏感性、几何对齐与下游耦合强度。 | `AdaptiveThreshold`、`ColorConversion`、`Filtering`、`ImageNormalize`、`ImageResize`、`PerspectiveTransform`、`ShadingCorrection`、`Thresholding` |
| A2 去噪与对比度增强组 | 中 | 9 | 核查去噪、锐化、增强对检测稳定性和边界样本的影响。 | `BilateralFilter`、`ClaheEnhancement`、`FrameAveraging`、`HistogramEqualization`、`LaplacianSharpen`、`MeanFilter`、`MedianBlur`、`MorphologicalOperation`、`Morphology` |
| A3 几何/图像算术组 | 中 | 6 | 核查 ROI 裁剪、几何变换、图像算术对结果可信度与数据完整性的影响。 | `ImageAdd`、`ImageBlend`、`ImageCrop`、`ImageDiff`、`ImageRotate`、`ImageSubtract` |

### 4.2 冻结依据

- 计数与分类来源：`算子资料/算子名片/CATALOG.md:277-302`
- 启动优先级来源：`docs/算子审计/TODO.md:35-41`
- 周内动作来源：`docs/算子审计/启动计划/Week1-长时启动计划.md`

### 4.3 本线周内回填口径

| 日期 | 必填内容 | 当前状态 |
| --- | --- | --- |
| 周一 | 清单、分组、优先级、责任人、路径锁定 | [x] |
| 周二 | 逐算子实现/参数/异常路径/已知限制盘点 | [x] |
| 周三 | 每算子最小复现实验集（正常/边界/异常） | [x] |
| 周四 | 风险收敛、P0/P1 入池、阻塞项登记 | [x] |
| 周五 | Week2 优先修复 Top10 建议回填 | [x] |

## 5. B线范围冻结（定位 7 + 特征提取 4）

### 5.1 链路分组

| 分组 | 优先级 | 算子数 | 审计目标 | 冻结算子 |
| --- | --- | ---: | --- | --- |
| B1 特征前置链 | 高 | 4 | 核查边缘、轮廓、Blob 特征输出是否稳定供给定位/测量链。 | `BlobAnalysis`、`ContourDetection`、`EdgeDetection`、`SubpixelEdgeDetection` |
| B2 几何定位主链 | 高 | 5 | 核查几何定位精度、断点收口、异常输入下的重复性与回退行为。 | `CornerDetection`、`EdgeIntersection`、`ParallelLineFind`、`QuadrilateralFind`、`RectangleDetection` |
| B3 桥接/修正链 | 中 | 2 | 核查二值结果到结构输出、定位结果到修正坐标的联动稳定性。 | `BlobLabeling`、`PositionCorrection` |

### 5.2 联动链路冻结

| 链路 | 用途 | 审计重点 |
| --- | --- | --- |
| `AdaptiveThreshold/Thresholding -> BlobLabeling/ContourDetection -> RectangleDetection/QuadrilateralFind` | 二值结果驱动轮廓/矩形定位 | 关注阈值漂移、轮廓断裂、空结果收口。 |
| `Filtering/ShadingCorrection -> EdgeDetection/SubpixelEdgeDetection -> ParallelLineFind/EdgeIntersection` | 边缘主链驱动几何定位 | 关注光照变化、亚像素输出稳定性、直线/交点退化场景。 |
| `定位结果/标定输出 -> PositionCorrection` | 结果修正与坐标回写 | 关注前序定位缺失、标定数据不一致、异常回退行为。 |

### 5.3 冻结依据

- 计数与分类来源：`算子资料/算子名片/CATALOG.md:135-144`；`算子资料/算子名片/CATALOG.md:216-222`
- 启动优先级来源：`docs/算子审计/TODO.md:37-39`
- 周内动作来源：`docs/算子审计/启动计划/Week1-长时启动计划.md`

## 6. 周内回填进度

| 日期 | 计划动作 | 当前状态 | 备注 |
| --- | --- | --- | --- |
| 2026-04-13（周一） | 模板定稿、范围冻结、路径锁定 | [x] | 本文件与证据链文件已创建。 |
| 2026-04-14（周二） | A/B 静态盘点覆盖率达到 100% | [x] | 已于 2026-04-13 提前产出首版，待 2026-04-14 复核封版。 |
| 2026-04-15（周三） | 最小复现实验与联动回归设计补全 | [x] | 已补 A/B 组级实验设计、联动回归入口与直接单测缺口说明。 |
| 2026-04-16（周四） | P0/P1 缺陷池与阻塞清单 | [x] | 已输出到 `Week2-修复优先级清单.md`。 |
| 2026-04-17（周五） | 审计周报与 Week2 入场顺序确认 | [x] | 已输出到 `Week1-审计周报.md` 并同步启动计划状态板。 |

## 7. 周二静态审计首版结论（已于 2026-04-13 提前产出）

| 线别 | 覆盖率 | 首版结论 | 主要缺口 |
| --- | --- | --- | --- |
| A线 | 23 / 23 | 已完成“实现 / 参数 / 异常路径 / 已知限制”逐算子静态盘点首版。 | 直接单测缺口集中在 `Filtering`、`ImageBlend`、`ImageDiff`、`LaplacianSharpen`、`MorphologicalOperation`、`Thresholding`。 |
| B线 | 11 / 11 | 已完成“输入依赖 / 链路断点 / 错误收口”逐算子静态盘点首版。 | `EdgeDetection` 暂未发现直接单测；`EdgeIntersection`、`PositionCorrection` 的名片限制说明偏薄。 |
| C线 | 3 / 3 类 | 已完成统一回归脚本补齐，并绑定本周识别/标定/检测/匹配定位批次结果。 | `AI检测` 仍缺工业验收签收与模型/阈值一致性说明。 |

## 8. A线静态审计首版（预处理 23）

| 算子 | 实现 / 参数盘点 | 异常路径 / 已知限制 | 测试入口 | 证据路径 | 状态 |
| --- | --- | --- | --- | --- | --- |
| `AdaptiveThreshold` | 实现为 `Cv2.AdaptiveThreshold` 封装，前置灰度化、后置转回 BGR；重点参数为 `BlockSize / C / ThresholdType`。 | 偶数 `BlockSize` 会被自动修正；仅支持灰度单通道阈值，不含算子内去噪/形态学后处理。 | `Acme.Product/tests/Acme.Product.Tests/Operators/AdaptiveThresholdOperatorTests.cs:1-70` | `算子资料/算子名片/AdaptiveThreshold.md:1-83` | [x] |
| `BilateralFilter` | 实现为保边降噪双边滤波；参数集中在 `Diameter / SigmaColor / SigmaSpace`。 | 主要产物仍是图像主输出，参数稍偏就会在“保边”和“平滑”之间剧烈切换。 | `Acme.Product/tests/Acme.Product.Tests/Operators/BilateralFilterOperatorTests.cs:1-46` | `算子资料/算子名片/BilateralFilter.md:1-60` | [x] |
| `ClaheEnhancement` | 实现为局部直方图增强，重点关注 clip/tile 类参数。 | 会主动改变局部对比度分布，下游阈值链、边缘链需要重新校样。 | `Acme.Product/tests/Acme.Product.Tests/Operators/ClaheEnhancementOperatorTests.cs:1-93` | `算子资料/算子名片/ClaheEnhancement.md:1-69` | [x] |
| `ColorConversion` | 实现为颜色空间转换，参数核心是转换目标与通道布局。 | 输入通道数与目标空间不匹配是主要断点，下游图像语义会因颜色空间改变而漂移。 | `Acme.Product/tests/Acme.Product.Tests/Operators/ColorConversionOperatorTests.cs:1-46` | `算子资料/算子名片/ColorConversion.md:1-62` | [x] |
| `Filtering` | 当前实现为 Gaussian Blur 主路径；参数核心是核尺寸与平滑强度。 | 主要限制是输出仍以图像为主，平滑过度会直接吞掉细小边缘；当前未发现直接同名单测。 | 待补直接单测 | `算子资料/算子名片/Filtering.md:1-62` | [x] |
| `FrameAveraging` | 当前实现是带内部缓存队列的状态型算子，支持滚动平均/中值；参数核心是 `FrameCount` 与融合模式。 | `Median` 偶数帧取上中位数；没有“窗口满再输出”开关，也无时间戳同步机制。 | `Acme.Product/tests/Acme.Product.Tests/Operators/FrameAveragingOperatorTests.cs:1-146` | `算子资料/算子名片/FrameAveraging.md:1-89` | [x] |
| `HistogramEqualization` | 实现为全局直方图均衡化，参数少但会整体重排灰度分布。 | 对亮暗分布改动大，易放大噪声或让固定阈值链整体重标。 | `Acme.Product/tests/Acme.Product.Tests/Operators/HistogramEqualizationOperatorTests.cs:1-177` | `算子资料/算子名片/HistogramEqualization.md:1-69` | [x] |
| `ImageAdd` | 双输入图像算术叠加，参数核心在权重/饱和控制。 | 断点主要是双输入尺寸、通道、类型不一致。 | `Acme.Product/tests/Acme.Product.Tests/Operators/ImageAddOperatorTests.cs:1-67` | `算子资料/算子名片/ImageAdd.md:1-75` | [x] |
| `ImageBlend` | 双输入图像融合，参数核心在融合比例与模式。 | 断点主要是双输入尺寸/类型不匹配；当前未发现直接单测。 | 待补直接单测 | `算子资料/算子名片/ImageBlend.md:1-65` | [x] |
| `ImageCrop` | 实现为 ROI 裁剪，参数核心在裁剪区域与边界。 | 超界 ROI、负坐标或空裁剪是主要异常路径。 | `Acme.Product/tests/Acme.Product.Tests/Operators/ImageCropOperatorTests.cs:1-46` | `算子资料/算子名片/ImageCrop.md:1-60` | [x] |
| `ImageDiff` | 双输入图像差分，参数少，主要依赖输入一致性。 | 双输入尺寸/类型不一致时最易断链；当前未发现直接单测。 | 待补直接单测 | `算子资料/算子名片/ImageDiff.md:1-60` | [x] |
| `ImageNormalize` | 实现为图像归一化，重点参数是目标范围与归一化策略。 | 会改变数值动态范围，下游阈值/模型链需要重新校准。 | `Acme.Product/tests/Acme.Product.Tests/Operators/ImageNormalizeOperatorTests.cs:1-71` | `算子资料/算子名片/ImageNormalize.md:1-68` | [x] |
| `ImageResize` | 实现为图像缩放，重点参数是目标尺寸与插值方式。 | 缩放后几何比例和纹理细节都会变化，对定位/检测链敏感。 | `Acme.Product/tests/Acme.Product.Tests/Operators/ImageResizeOperatorTests.cs:1-46` | `算子资料/算子名片/ImageResize.md:1-62` | [x] |
| `ImageRotate` | 实现为仿射旋转，重点参数是角度、中心点、插值与边界。 | 主要风险是裁边、补边和旋转后 ROI 语义漂移。 | `Acme.Product/tests/Acme.Product.Tests/Operators/ImageRotateOperatorTests.cs:1-46` | `算子资料/算子名片/ImageRotate.md:1-65` | [x] |
| `ImageSubtract` | 双输入图像减法，重点关注输入对齐与溢出/截断行为。 | 双输入尺寸、通道和数据类型一致性是首要断点。 | `Acme.Product/tests/Acme.Product.Tests/Operators/ImageSubtractOperatorTests.cs:1-58` | `算子资料/算子名片/ImageSubtract.md:1-73` | [x] |
| `LaplacianSharpen` | 实现为拉普拉斯锐化，重点参数是核与增强强度。 | 会同步放大噪声和毛刺，当前未发现直接单测。 | 待补直接单测 | `算子资料/算子名片/LaplacianSharpen.md:1-67` | [x] |
| `MeanFilter` | 实现为均值滤波，参数核心是核尺寸。 | 会平滑细节和边缘，过大核会直接损伤定位前置链。 | `Acme.Product/tests/Acme.Product.Tests/Operators/MeanFilterOperatorTests.cs:1-62` | `算子资料/算子名片/MeanFilter.md:1-59` | [x] |
| `MedianBlur` | 实现为中值滤波，适合椒盐噪声抑制；参数核心是核尺寸。 | 对细线、小角点会有明显钝化效应。 | `Acme.Product/tests/Acme.Product.Tests/Operators/MedianBlurOperatorTests.cs:1-62` | `算子资料/算子名片/MedianBlur.md:1-58` | [x] |
| `MorphologicalOperation` | 实现为形态学算子集合，重点参数为操作类型、核大小与迭代次数。 | 强依赖前序二值质量与结构元素设置；当前未发现直接单测。 | 待补直接单测 | `算子资料/算子名片/MorphologicalOperation.md:1-67` | [x] |
| `Morphology` | 现有 Legacy 形态学路径，重点关注与 `MorphologicalOperation` 的职责区分。 | 容易与新版形态学能力重叠，需在周四风险评审时确认保留策略。 | `Acme.Product/tests/Acme.Product.Tests/Operators/MorphologyOperatorTests.cs:1-46` | `算子资料/算子名片/Morphology.md:1-69` | [x] |
| `PerspectiveTransform` | 实现为四点透视变换，参数核心在源/目标点与输出尺寸。 | 点位次序、点数不足、点集退化会直接导致几何失真。 | `Acme.Product/tests/Acme.Product.Tests/Operators/PerspectiveTransformOperatorTests.cs:1-62` | `算子资料/算子名片/PerspectiveTransform.md:1-82` | [x] |
| `ShadingCorrection` | 实现为光照校正，重点关注背景/除背景策略。 | 光照基准不稳会把亮度波动带进下游阈值链；附加字段依赖运行时输出。 | `Acme.Product/tests/Acme.Product.Tests/Operators/ShadingCorrectionOperatorTests.cs:1-83` | `算子资料/算子名片/ShadingCorrection.md:1-70` | [x] |
| `Thresholding` | 实现为固定阈值/Otsu 二值化，参数核心是阈值、类型和 `UseOtsu`。 | 主要限制是结果仍以图像主输出为主，阈值漂移对下游影响最大；当前未发现直接单测。 | 待补直接单测 | `算子资料/算子名片/Thresholding.md:1-64` | [x] |

## 9. B线静态审计首版（定位 7 + 特征提取 4）

| 算子 | 输入依赖 | 链路断点 | 错误收口 / 已知限制 | 测试入口 | 证据路径 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| `BlobLabeling` | `Image` 必填，`Blobs` 轮廓输入可选；依赖前序分割质量。 | 阈值不稳、前景粘连或空轮廓会导致 `Labels/Count` 失真。 | 结果主要通过 `Labels/Count` 和附加字段收口，没有独立错误端口。 | `Acme.Product/tests/Acme.Product.Tests/Operators/BlobLabelingOperatorTests.cs:1-65` | `算子资料/算子名片/BlobLabeling.md:1-79` | [x] |
| `CornerDetection` | 依赖单张图像输入，实质上依赖足够角点纹理和对比度。 | 低纹理、过平滑或 `MaxCorners/QualityLevel` 配置不当时会直接掉点。 | 通过 `Corners/Count` 收口；限制是结果仍以图像与附加字段共同表达。 | `Acme.Product/tests/Acme.Product.Tests/Operators/CornerDetectionOperatorTests.cs:1-63` | `算子资料/算子名片/CornerDetection.md:1-72` | [x] |
| `EdgeIntersection` | 强依赖 `Line1 + Line2` 两个 `LineData` 输入，通常来自平行线/直线链。 | 任一上游未产线、两线平行或几何退化时会失去交点。 | `HasIntersection` 是当前主要错误收口；名片中的实现与限制说明偏薄。 | `Acme.Product/tests/Acme.Product.Tests/Operators/EdgeIntersectionOperatorTests.cs:1-59` | `算子资料/算子名片/EdgeIntersection.md:1-48` | [x] |
| `ParallelLineFind` | 依赖边缘清晰的 `Image` 输入，实质依赖前序边缘链稳定。 | `Canny/Hough` 候选不足、角度容差不当或线距越界时会断链。 | 通过 `Line1/Line2/Distance/Angle/PairCount` 收口；仍无独立错误端口。 | `Acme.Product/tests/Acme.Product.Tests/Operators/ParallelLineFindOperatorTests.cs:1-70` | `算子资料/算子名片/ParallelLineFind.md:1-76` | [x] |
| `PositionCorrection` | `ReferencePoint`、`BasePoint` 必填，`RoiX/RoiY` 可选；依赖上游点位与角度语义一致。 | 缺点位、角度参考不一致或 ROI 基准漂移时会输出错误修正量。 | 仅以修正后的坐标和偏移量收口；名片的限制说明目前为空。 | `Acme.Product/tests/Acme.Product.Tests/Operators/PositionCorrectionOperatorTests.cs:1-88` | `算子资料/算子名片/PositionCorrection.md:1-56` | [x] |
| `QuadrilateralFind` | 依赖单图输入，实质依赖前序二值/边缘结果足以形成四边形轮廓。 | 非四边形、面积越界、凸性不满足或近似参数失配时会失去候选。 | 通过 `Vertices/Count/Area/Center` 收口；仍以图像主输出承载可视化。 | `Acme.Product/tests/Acme.Product.Tests/Operators/QuadrilateralFindOperatorTests.cs:1-72` | `算子资料/算子名片/QuadrilateralFind.md:1-78` | [x] |
| `RectangleDetection` | 依赖单图输入，实质依赖轮廓提取质量和矩形几何完整性。 | 面积、角度、近似参数不匹配时最易丢候选或误检。 | 通过 `Rectangles/Count/Center/Angle/Width/Height` 收口；仍无错误端口。 | `Acme.Product/tests/Acme.Product.Tests/Operators/RectangleDetectionOperatorTests.cs:1-72` | `算子资料/算子名片/RectangleDetection.md:1-81` | [x] |
| `BlobAnalysis` | 依赖图像输入，本质依赖前序分割把目标 Blob 从背景中稳定分离。 | 分割质量差时 `BlobCount/Area/Center` 全部失真；文档中 `Color` 参数声明但源码未见明显执行。 | 通过 `Blobs/BlobCount` 与附加字段收口。 | `Acme.Product/tests/Acme.Product.Tests/Operators/BlobDetectionOperatorTests.cs:1-101` | `算子资料/算子名片/BlobAnalysis.md:1-71` | [x] |
| `ContourDetection` | 依赖图像输入，本质依赖阈值/边缘链生成可闭合轮廓。 | 阈值漂移、轮廓断裂、噪声过多会使 `Contours/ContourCount` 直接退化。 | 通过 `Contours/ContourCount` 和附加统计字段收口。 | `Acme.Product/tests/Acme.Product.Tests/Operators/FindContoursOperatorTests.cs:1-46` | `算子资料/算子名片/ContourDetection.md:1-78` | [x] |
| `EdgeDetection` | 依赖图像输入，通常依赖上游光照校正/平滑链稳定供图。 | `Threshold1/Threshold2/AutoThreshold` 漂移和噪声会直接让后续定位链断裂。 | 通过 `Edges` 图像和 `Threshold1Used/Threshold2Used` 附加字段收口；当前未发现直接单测。 | 待补直接单测 | `算子资料/算子名片/EdgeDetection.md:1-74` | [x] |
| `SubpixelEdgeDetection` | 依赖图像输入，实质依赖足够边缘对比度与 ROI 稳定。 | 低对比、方法切换失配或阈值过窄时会导致 `EdgeCount/ContourCount` 下降。 | 通过 `Edges` 点集与 `EdgeCount/ContourCount` 收口；并行分支意味着后续还需关注性能一致性。 | `Acme.Product/tests/Acme.Product.Tests/Operators/SubpixelEdgeDetectionOperatorTests.cs:1-47` | `算子资料/算子名片/SubpixelEdgeDetection.md:1-80` | [x] |

## 10. 周三最小复现实验设计（A线）

| 分组 | 最小复现实验入口 | 正常样本 | 边界样本 | 异常样本 | 当前结论 | Week2 去向 |
| --- | --- | --- | --- | --- | --- | --- |
| A1 上游输入一致性核心组 | `scripts/run-dotnet-test-serial.ps1` + `AdaptiveThresholdOperatorTests,ColorConversionOperatorTests,PerspectiveTransformOperatorTests,ShadingCorrectionOperatorTests` | 标准灰度图/彩色图、规则四点透视、均匀光照输入 | 偶数 `BlockSize`、透视点近共线、光照强弱不均、缩放后通道变化 | 空图、通道不匹配、点数不足 | 已具备“输入一致性 -> 阈值敏感性 -> 几何对齐”的最小复现入口；`Filtering`、`Thresholding` 仍缺同名单测。 | P1 补直接单测 |
| A2 去噪与对比度增强组 | `scripts/run-dotnet-test-serial.ps1` + `BilateralFilterOperatorTests,ClaheEnhancementOperatorTests,FrameAveragingOperatorTests,HistogramEqualizationOperatorTests,MeanFilterOperatorTests,MedianBlurOperatorTests` | 常规噪声抑制、局部对比度增强、滚动平均输入 | 高噪声、低对比、偶数帧中值、核尺寸接近上限 | 空输入、核尺寸越界、帧缓存不足 | 已具备“去噪/增强 -> 稳定性变化”的最小复现入口；`LaplacianSharpen`、`MorphologicalOperation` 仍缺同名单测。 | P1 补直接单测 |
| A3 几何/图像算术组 | `scripts/run-dotnet-test-serial.ps1` + `ImageAddOperatorTests,ImageCropOperatorTests,ImageResizeOperatorTests,ImageRotateOperatorTests,ImageSubtractOperatorTests` | 尺寸一致双输入、标准 ROI、常规旋转/缩放 | 极小 ROI、旋转裁边、近零权重、尺寸临界变化 | 双输入尺寸/类型不一致、越界 ROI、空差分输入 | 已具备“图像算术 -> ROI/几何变换 -> 数据完整性”的最小复现入口；`ImageBlend`、`ImageDiff` 仍缺同名单测。 | P1 补直接单测 |

## 11. 周三联动回归设计（B线）

| 链路 | 联动回归入口 | 正常样本 | 退化/边界 | 异常收口 | 当前结论 | Week2 去向 |
| --- | --- | --- | --- | --- | --- | --- |
| B1 二值/轮廓/矩形主链 | `scripts/run-dotnet-test-serial.ps1` + `AdaptiveThresholdOperatorTests,BlobLabelingOperatorTests,FindContoursOperatorTests,RectangleDetectionOperatorTests,QuadrilateralFindOperatorTests` | 清晰前景、闭合轮廓、规则矩形/四边形 | 阈值临界、轮廓破碎、面积接近下限 | 空前景、非四边形、凸性不满足 | 已具备“阈值 -> 轮廓 -> 几何候选”联动入口。 | 无 |
| B2 光照/边缘/几何定位链 | `scripts/run-dotnet-test-serial.ps1` + `ShadingCorrectionOperatorTests,SubpixelEdgeDetectionOperatorTests,ParallelLineFindOperatorTests,EdgeIntersectionOperatorTests` | 高对比直线、稳定交点、均匀照明 | 光照漂移、弱边缘、近平行线、角度容差收窄 | 上游无候选、几何退化、交点缺失 | 已具备“光照 -> 边缘 -> 直线/交点”联动入口；`EdgeDetection` 仍缺同名单测。 | P1 补直接单测 |
| B3 标定/修正坐标链 | `scripts/run-dotnet-test-serial.ps1` + `CalibrationLoaderOperatorTests,PixelToWorldTransformOperatorTests,PositionCorrectionOperatorTests` | 标准标定矩阵、正常参考点、稳定 ROI | 标定尺度轻微漂移、ROI 偏移、基准点扰动 | 缺基准点、角度参考不一致、坐标回写缺失 | 已具备“标定输出 -> 坐标修正 -> 收口字段”联动入口。 | 无 |
