# 算子目录 / Operator Catalog

> 生成时间 / Generated At: `2026-03-17 14:30:51 +08:00`
> 算子总数 / Total Operators: **119**

## 分类统计 / Category Summary
| 分类 (Category) | 数量 (Count) | 占比 (Ratio) |
|------|------:|------:|
| AI检测 | 4 | 3.4% |
| 匹配定位 | 6 | 5.0% |
| 变量 | 4 | 3.4% |
| 图像处理 | 4 | 3.4% |
| 定位 | 7 | 5.9% |
| 拆分组合 | 2 | 1.7% |
| 数据处理 | 10 | 8.4% |
| 标定 | 6 | 5.0% |
| 检测 | 16 | 13.4% |
| 流程控制 | 6 | 5.0% |
| 特征提取 | 4 | 3.4% |
| 识别 | 2 | 1.7% |
| 辅助 | 3 | 2.5% |
| 输出 | 2 | 1.7% |
| 通信 | 8 | 6.7% |
| 通用 | 4 | 3.4% |
| 逻辑工具 | 5 | 4.2% |
| 采集 | 1 | 0.8% |
| 预处理 | 23 | 19.3% |
| 颜色处理 | 2 | 1.7% |

## 质量评分 / Quality Score
- 平均分 / Average: **88.9**
| 等级 (Level) | 数量 (Count) |
|------|------:|
| A | 80 |
| B | 33 |
| C | 6 |

## 分类索引 / Grouped Index

### AI检测 (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.DeepLearning` | 深度学习 | 1 | 6 | 9 | 100 (A) | `1.0.0` | 当前实现是一个基于 ONNX Runtime 的 YOLO 推理算子，支持： | [DeepLearning](./DeepLearning.md) |
| `OperatorType.DualModalVoting` | 双模态投票 | 2 | 3 | 6 | 90 (A) | `1.0.0` | 该算子结合学习型模型或规则判定完成识别、检测或缺陷筛查。 | [DualModalVoting](./DualModalVoting.md) |
| `OperatorType.EdgePairDefect` | 边缘对缺陷 | 3 | 4 | 4 | 94 (A) | `1.0.0` | 该算子基于固定阈值或自动阈值策略把图像分成前景和背景两类，可用于快速分割。 | [EdgePairDefect](./EdgePairDefect.md) |
| `OperatorType.SurfaceDefectDetection` | 表面缺陷检测 | 2 | 4 | 5 | 94 (A) | `1.0.0` | 该算子基于高斯卷积平滑图像，在抑制高频噪声的同时尽量保持整体结构稳定。 | [SurfaceDefectDetection](./SurfaceDefectDetection.md) |

### 匹配定位 (6)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.AkazeFeatureMatch` | AKAZE特征匹配 | 2 | 5 | 5 | 73 (B) | `1.0.0` | 该算子基于局部特征点匹配完成模板定位，核心流程是： | [AkazeFeatureMatch](./AkazeFeatureMatch.md) |
| `OperatorType.GradientShapeMatch` | 梯度形状匹配 | 2 | 5 | 6 | 83 (B) | `1.0.0` | 该算子不是直接在原始灰度图上做相关性匹配，而是使用自定义 GradientShape… | [GradientShapeMatch](./GradientShapeMatch.md) |
| `OperatorType.OrbFeatureMatch` | ORB特征匹配 | 2 | 5 | 7 | 73 (B) | `1.0.0` | 该算子与 AkazeFeatureMatchOperator 属于同一类局部特征匹配… | [OrbFeatureMatch](./OrbFeatureMatch.md) |
| `OperatorType.PyramidShapeMatch` | 金字塔形状匹配 | 2 | 5 | 15 | 83 (B) | `1.0.0` | 该算子围绕模板、特征或几何相似性执行定位匹配，用于判断目标是否存在以及位姿大致位置。 | [PyramidShapeMatch](./PyramidShapeMatch.md) |
| `OperatorType.ShapeMatching` | 旋转尺度模板匹配 | 2 | 2 | 10 | 100 (A) | `1.1.0` | 虽然名称叫“形状匹配”，但当前实现本质上仍是基于灰度模板匹配的旋转搜索，而不是基于轮… | [ShapeMatching](./ShapeMatching.md) |
| `OperatorType.TemplateMatching` | 模板匹配 | 2 | 6 | 3 | 96 (A) | `1.0.0` | 该算子基于经典模板匹配，在搜索图像上滑动模板窗口并计算每个位置的相似度响应图，再取全… | [TemplateMatching](./TemplateMatching.md) |

### 变量 (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.CycleCounter` | 循环计数器 | 0 | 5 | 2 | 66 (C) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [CycleCounter](./CycleCounter.md) |
| `OperatorType.VariableIncrement` | 变量递增 | 0 | 5 | 5 | 73 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [VariableIncrement](./VariableIncrement.md) |
| `OperatorType.VariableRead` | 变量读取 | 0 | 3 | 3 | 73 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [VariableRead](./VariableRead.md) |
| `OperatorType.VariableWrite` | 变量写入 | 1 | 3 | 4 | 73 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [VariableWrite](./VariableWrite.md) |

### 图像处理 (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.AffineTransform` | 仿射变换 | 1 | 2 | 9 | 100 (A) | `1.0.0` | 该算子基于仿射模型执行旋转、缩放或平移等二维几何变换。 | [AffineTransform](./AffineTransform.md) |
| `OperatorType.CopyMakeBorder` | 边界填充 | 1 | 1 | 6 | 94 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [CopyMakeBorder](./CopyMakeBorder.md) |
| `OperatorType.ImageStitching` | 图像拼接 | 2 | 2 | 3 | 94 (A) | `1.0.0` | 该算子基于固定阈值或自动阈值策略把图像分成前景和背景两类，可用于快速分割。 | [ImageStitching](./ImageStitching.md) |
| `OperatorType.PolarUnwrap` | 极坐标展开 | 2 | 1 | 8 | 100 (A) | `1.0.0` | 该算子把以某个中心为参考的环形区域，从笛卡尔坐标系展开到极坐标平面。 | [PolarUnwrap](./PolarUnwrap.md) |

### 定位 (7)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.BlobLabeling` | 连通域标注 | 2 | 3 | 3 | 100 (A) | `1.0.0` | 该算子基于固定阈值或自动阈值策略把图像分成前景和背景两类，可用于快速分割。 | [BlobLabeling](./BlobLabeling.md) |
| `OperatorType.CornerDetection` | 角点检测 | 1 | 3 | 5 | 94 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [CornerDetection](./CornerDetection.md) |
| `OperatorType.EdgeIntersection` | 边线交点 | 2 | 3 | 0 | 89 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [EdgeIntersection](./EdgeIntersection.md) |
| `OperatorType.ParallelLineFind` | 平行线查找 | 1 | 6 | 4 | 94 (A) | `1.0.0` | 该算子从边缘图中提取直线段候选，再基于几何关系输出线结构或测量结果。 | [ParallelLineFind](./ParallelLineFind.md) |
| `OperatorType.PositionCorrection` | 位置修正 | 4 | 5 | 3 | 94 (A) | `1.0.0` | 该算子围绕标定、坐标映射或几何重采样展开，目标是在不同空间之间建立稳定映射关系。 | [PositionCorrection](./PositionCorrection.md) |
| `OperatorType.QuadrilateralFind` | 四边形查找 | 1 | 5 | 4 | 94 (A) | `1.0.0` | 该算子从二值结果中提取轮廓点集，并以轮廓为单位继续做几何分析或筛选。 | [QuadrilateralFind](./QuadrilateralFind.md) |
| `OperatorType.RectangleDetection` | 矩形检测 | 1 | 7 | 4 | 94 (A) | `1.0.0` | 该算子从二值结果中提取轮廓点集，并以轮廓为单位继续做几何分析或筛选。 | [RectangleDetection](./RectangleDetection.md) |

### 拆分组合 (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.ImageCompose` | 图像组合 | 4 | 1 | 3 | 94 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [ImageCompose](./ImageCompose.md) |
| `OperatorType.ImageTiling` | 图像切片 | 1 | 3 | 4 | 94 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [ImageTiling](./ImageTiling.md) |

### 数据处理 (10)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.Aggregator` | 数据聚合 | 3 | 5 | 1 | 66 (C) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [Aggregator](./Aggregator.md) |
| `OperatorType.ArrayIndexer` | 数组索引器 | 1 | 3 | 2 | 79 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [ArrayIndexer](./ArrayIndexer.md) |
| `OperatorType.BoxFilter` | 候选框过滤 (Bounding Box) | 2 | 3 | 9 | 90 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [BoxFilter](./BoxFilter.md) |
| `OperatorType.BoxNms` | 候选框抑制 | 2 | 3 | 3 | 90 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [BoxNms](./BoxNms.md) |
| `OperatorType.DatabaseWrite` | 数据库写入 | 1 | 2 | 3 | 100 (A) | `1.0.0` | 该算子负责把流程结果写入文件、数据库或外部系统，或从外围资源获取输入。 | [DatabaseWrite](./DatabaseWrite.md) |
| `OperatorType.JsonExtractor` | JSON 提取器 | 1 | 2 | 1 | 83 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [JsonExtractor](./JsonExtractor.md) |
| `OperatorType.MathOperation` | 数值计算 | 2 | 2 | 1 | 83 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [MathOperation](./MathOperation.md) |
| `OperatorType.PointAlignment` | 点位对齐 | 2 | 3 | 2 | 94 (A) | `1.0.0` | 该算子围绕标定、坐标映射或几何重采样展开，目标是在不同空间之间建立稳定映射关系。 | [PointAlignment](./PointAlignment.md) |
| `OperatorType.PointCorrection` | 点位修正 | 4 | 4 | 3 | 94 (A) | `1.0.0` | 该算子围绕标定、坐标映射或几何重采样展开，目标是在不同空间之间建立稳定映射关系。 | [PointCorrection](./PointCorrection.md) |
| `OperatorType.UnitConvert` | 单位换算 | 2 | 2 | 4 | 96 (A) | `1.0.0` | 该算子围绕标定、坐标映射或几何重采样展开，目标是在不同空间之间建立稳定映射关系。 | [UnitConvert](./UnitConvert.md) |

### 标定 (6)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.CalibrationLoader` | 标定加载 | 0 | 4 | 2 | 100 (A) | `1.0.0` | 该算子围绕标定、坐标映射或几何重采样展开，目标是在不同空间之间建立稳定映射关系。 | [CalibrationLoader](./CalibrationLoader.md) |
| `OperatorType.CameraCalibration` | Camera Calibration | 1 | 2 | 7 | 100 (A) | `1.0.0` | 该算子基于标定板角点/圆点，估计相机内参矩阵 CameraMatrix 和畸变参数 … | [CameraCalibration](./CameraCalibration.md) |
| `OperatorType.CoordinateTransform` | 坐标转换 | 3 | 3 | 4 | 100 (A) | `1.0.0` | 该算子围绕标定、坐标映射或几何重采样展开，目标是在不同空间之间建立稳定映射关系。 | [CoordinateTransform](./CoordinateTransform.md) |
| `OperatorType.NPointCalibration` | N点标定 | 1 | 3 | 3 | 100 (A) | `1.0.0` | 该算子围绕标定、坐标映射或几何重采样展开，目标是在不同空间之间建立稳定映射关系。 | [NPointCalibration](./NPointCalibration.md) |
| `OperatorType.TranslationRotationCalibration` | 平移旋转标定 | 1 | 3 | 3 | 100 (A) | `1.0.0` | 该算子围绕标定、坐标映射或几何重采样展开，目标是在不同空间之间建立稳定映射关系。 | [TranslationRotationCalibration](./TranslationRotationCalibration.md) |
| `OperatorType.Undistort` | Undistort | 2 | 1 | 1 | 95 (A) | `1.0.0` | 该算子根据相机标定结果中的内参矩阵 CameraMatrix 和畸变系数 DistC… | [Undistort](./Undistort.md) |

### 检测 (16)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.AngleMeasurement` | 角度测量 | 1 | 2 | 7 | 94 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [AngleMeasurement](./AngleMeasurement.md) |
| `OperatorType.CaliperTool` | 卡尺工具 | 2 | 7 | 9 | 94 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [CaliperTool](./CaliperTool.md) |
| `OperatorType.CircleMeasurement` | 圆测量 | 1 | 7 | 7 | 100 (A) | `1.0.0` | 该算子基于高斯卷积平滑图像，在抑制高频噪声的同时尽量保持整体结构稳定。 | [CircleMeasurement](./CircleMeasurement.md) |
| `OperatorType.ContourMeasurement` | 轮廓测量 | 1 | 4 | 4 | 94 (A) | `1.0.0` | 该算子基于固定阈值或自动阈值策略把图像分成前景和背景两类，可用于快速分割。 | [ContourMeasurement](./ContourMeasurement.md) |
| `OperatorType.GapMeasurement` | 间隙测量 | 2 | 6 | 4 | 94 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [GapMeasurement](./GapMeasurement.md) |
| `OperatorType.GeoMeasurement` | 几何测量 | 2 | 5 | 2 | 94 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [GeoMeasurement](./GeoMeasurement.md) |
| `OperatorType.GeometricFitting` | Geometric Fitting | 1 | 2 | 8 | 100 (A) | `1.0.0` | 当前实现的几何拟合流程并不是直接接收点集输入，而是： | [GeometricFitting](./GeometricFitting.md) |
| `OperatorType.GeometricTolerance` | 几何公差 | 1 | 5 | 9 | 94 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [GeometricTolerance](./GeometricTolerance.md) |
| `OperatorType.HistogramAnalysis` | 直方图分析 | 1 | 7 | 6 | 94 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [HistogramAnalysis](./HistogramAnalysis.md) |
| `OperatorType.LineLineDistance` | 线线距离 | 2 | 5 | 1 | 96 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [LineLineDistance](./LineLineDistance.md) |
| `OperatorType.LineMeasurement` | 直线测量 | 1 | 5 | 4 | 94 (A) | `1.0.0` | 该算子从边缘图中提取直线段候选，再基于几何关系输出线结构或测量结果。 | [LineMeasurement](./LineMeasurement.md) |
| `OperatorType.Measurement` | 测量 | 3 | 2 | 5 | 94 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [Measurement](./Measurement.md) |
| `OperatorType.PixelStatistics` | 像素统计 | 2 | 6 | 5 | 94 (A) | `1.0.0` | 该算子基于固定阈值或自动阈值策略把图像分成前景和背景两类，可用于快速分割。 | [PixelStatistics](./PixelStatistics.md) |
| `OperatorType.PointLineDistance` | 点线距离 | 2 | 2 | 0 | 91 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [PointLineDistance](./PointLineDistance.md) |
| `OperatorType.SharpnessEvaluation` | 清晰度评估 | 1 | 3 | 6 | 94 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [SharpnessEvaluation](./SharpnessEvaluation.md) |
| `OperatorType.WidthMeasurement` | 宽度测量 | 3 | 4 | 4 | 96 (A) | `1.0.0` | 当前实现有两种工作模式： | [WidthMeasurement](./WidthMeasurement.md) |

### 流程控制 (6)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.Comparator` | 数值比较 | 2 | 2 | 5 | 61 (C) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [Comparator](./Comparator.md) |
| `OperatorType.ConditionalBranch` | 条件分支 | 1 | 2 | 3 | 90 (A) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [ConditionalBranch](./ConditionalBranch.md) |
| `OperatorType.Delay` | 延时 | 1 | 2 | 1 | 61 (C) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [Delay](./Delay.md) |
| `OperatorType.ForEach` | ForEach 循环 | 1 | 1 | 4 | 83 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [ForEach](./ForEach.md) |
| `OperatorType.ResultJudgment` | 结果判定 | 2 | 3 | 8 | 66 (C) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [ResultJudgment](./ResultJudgment.md) |
| `OperatorType.TryCatch` | 异常捕获 | 1 | 4 | 3 | 75 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [TryCatch](./TryCatch.md) |

### 特征提取 (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.BlobAnalysis` | Blob分析 | 2 | 4 | 17 | 100 (A) | `1.1.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [BlobAnalysis](./BlobAnalysis.md) |
| `OperatorType.ContourDetection` | 轮廓检测 | 1 | 3 | 8 | 94 (A) | `1.0.0` | 该算子基于固定阈值或自动阈值策略把图像分成前景和背景两类，可用于快速分割。 | [ContourDetection](./ContourDetection.md) |
| `OperatorType.EdgeDetection` | Edge Detection | 1 | 2 | 8 | 76 (B) | `1.0.0` | 该算子基于高斯卷积平滑图像，在抑制高频噪声的同时尽量保持整体结构稳定。 | [EdgeDetection](./EdgeDetection.md) |
| `OperatorType.SubpixelEdgeDetection` | Subpixel Edge Detection | 1 | 2 | 5 | 94 (A) | `1.0.0` | 该算子基于高斯卷积平滑图像，在抑制高频噪声的同时尽量保持整体结构稳定。 | [SubpixelEdgeDetection](./SubpixelEdgeDetection.md) |

### 识别 (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.CodeRecognition` | 条码识别 | 1 | 4 | 2 | 94 (A) | `1.0.0` | 该算子结合学习型模型或规则判定完成识别、检测或缺陷筛查。 | [CodeRecognition](./CodeRecognition.md) |
| `OperatorType.OcrRecognition` | OCR 识别 | 1 | 2 | 0 | 100 (A) | `1.0.0` | 该算子结合学习型模型或规则判定完成识别、检测或缺陷筛查。 | [OcrRecognition](./OcrRecognition.md) |

### 辅助 (3)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.Comment` | 注释 | 1 | 2 | 1 | 61 (C) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [Comment](./Comment.md) |
| `OperatorType.RoiManager` | ROI管理器 | 1 | 2 | 10 | 100 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [RoiManager](./RoiManager.md) |
| `OperatorType.RoiTransform` | ROI跟踪 | 2 | 1 | 1 | 86 (A) | `1.0.0` | - | [RoiTransform](./RoiTransform.md) |

### 输出 (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.ImageSave` | 图像保存 | 1 | 2 | 3 | 83 (B) | `1.0.0` | 该算子负责把流程结果写入文件、数据库或外部系统，或从外围资源获取输入。 | [ImageSave](./ImageSave.md) |
| `OperatorType.ResultOutput` | 结果输出 | 4 | 6 | 2 | 98 (A) | `1.0.1` | 该算子负责把流程结果写入文件、数据库或外部系统，或从外围资源获取输入。 | [ResultOutput](./ResultOutput.md) |

### 通信 (8)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.HttpRequest` | HTTP 请求 | 2 | 3 | 6 | 83 (B) | `1.0.0` | 该算子不执行图像算法，而是把流程参数映射为通信请求，与外部设备或服务完成读写和响应解… | [HttpRequest](./HttpRequest.md) |
| `OperatorType.MitsubishiMcCommunication` | 三菱MC通信 | 1 | 2 | 12 | 80 (B) | `1.0.0` | 该算子不执行图像算法，而是把流程参数映射为通信请求，与外部设备或服务完成读写和响应解… | [MitsubishiMcCommunication](./MitsubishiMcCommunication.md) |
| `OperatorType.ModbusCommunication` | Modbus通信 | 1 | 2 | 8 | 98 (A) | `1.0.0` | 该算子不执行图像算法，而是把流程参数映射为通信请求，与外部设备或服务完成读写和响应解… | [ModbusCommunication](./ModbusCommunication.md) |
| `OperatorType.MqttPublish` | MQTT 发布 | 2 | 1 | 6 | 100 (A) | `1.0.0` | 该算子不执行图像算法，而是把流程参数映射为通信请求，与外部设备或服务完成读写和响应解… | [MqttPublish](./MqttPublish.md) |
| `OperatorType.OmronFinsCommunication` | 欧姆龙FINS通信 | 1 | 2 | 12 | 80 (B) | `1.0.0` | 该算子不执行图像算法，而是把流程参数映射为通信请求，与外部设备或服务完成读写和响应解… | [OmronFinsCommunication](./OmronFinsCommunication.md) |
| `OperatorType.SerialCommunication` | 串口通信 | 1 | 1 | 8 | 83 (B) | `1.0.0` | 该算子不执行图像算法，而是把流程参数映射为通信请求，与外部设备或服务完成读写和响应解… | [SerialCommunication](./SerialCommunication.md) |
| `OperatorType.SiemensS7Communication` | 西门子S7通信 | 1 | 2 | 14 | 80 (B) | `1.0.0` | 该算子不执行图像算法，而是把流程参数映射为通信请求，与外部设备或服务完成读写和响应解… | [SiemensS7Communication](./SiemensS7Communication.md) |
| `OperatorType.TcpCommunication` | TCP通信 | 1 | 2 | 6 | 98 (A) | `1.0.0` | 该算子不执行图像算法，而是把流程参数映射为通信请求，与外部设备或服务完成读写和响应解… | [TcpCommunication](./TcpCommunication.md) |

### 通用 (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.LogicGate` | 逻辑门 | 2 | 1 | 1 | 73 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [LogicGate](./LogicGate.md) |
| `OperatorType.Statistics` | Statistics | 1 | 7 | 5 | 90 (A) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [Statistics](./Statistics.md) |
| `OperatorType.StringFormat` | 字符串格式化 | 2 | 1 | 1 | 76 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [StringFormat](./StringFormat.md) |
| `OperatorType.TypeConvert` | Type Convert | 1 | 6 | 2 | 83 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [TypeConvert](./TypeConvert.md) |

### 逻辑工具 (5)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.PointSetTool` | 点集工具 | 2 | 4 | 6 | 90 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [PointSetTool](./PointSetTool.md) |
| `OperatorType.ScriptOperator` | 脚本算子 | 4 | 2 | 3 | 100 (A) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [ScriptOperator](./ScriptOperator.md) |
| `OperatorType.TextSave` | Text Save | 2 | 2 | 5 | 100 (A) | `1.0.0` | 该算子负责把流程结果写入文件、数据库或外部系统，或从外围资源获取输入。 | [TextSave](./TextSave.md) |
| `OperatorType.TimerStatistics` | 计时统计 | 1 | 4 | 2 | 84 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [TimerStatistics](./TimerStatistics.md) |
| `OperatorType.TriggerModule` | 触发模块 | 1 | 3 | 3 | 84 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [TriggerModule](./TriggerModule.md) |

### 采集 (1)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.ImageAcquisition` | 图像采集 | 2 | 1 | 6 | 78 (B) | `1.0.0` | 该算子负责把流程结果写入文件、数据库或外部系统，或从外围资源获取输入。 | [ImageAcquisition](./ImageAcquisition.md) |

### 预处理 (23)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.AdaptiveThreshold` | 自适应阈值 | 1 | 1 | 5 | 94 (A) | `1.0.0` | 自适应阈值不会对整幅图使用一个全局阈值，而是针对每个像素在其邻域窗口 W(x, y)… | [AdaptiveThreshold](./AdaptiveThreshold.md) |
| `OperatorType.BilateralFilter` | 双边滤波 | 1 | 1 | 3 | 94 (A) | `1.0.0` | 该算子使用双边滤波同时考虑空间距离与像素差异，在保边前提下降低噪声。 | [BilateralFilter](./BilateralFilter.md) |
| `OperatorType.ClaheEnhancement` | CLAHE增强 | 1 | 1 | 5 | 76 (B) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [ClaheEnhancement](./ClaheEnhancement.md) |
| `OperatorType.ColorConversion` | 颜色空间转换 | 1 | 1 | 2 | 94 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [ColorConversion](./ColorConversion.md) |
| `OperatorType.Filtering` | Gaussian Blur | 1 | 1 | 4 | 76 (B) | `1.0.0` | Gaussian Blur (OpenCV) | [Filtering](./Filtering.md) |
| `OperatorType.FrameAveraging` | 帧平均 | 1 | 2 | 2 | 94 (A) | `1.0.0` | 该算子做的是时间域融合，不是空间域滤波。它会保留最近 N 帧图像，在时间轴上对同一像… | [FrameAveraging](./FrameAveraging.md) |
| `OperatorType.HistogramEqualization` | 直方图均衡化 | 1 | 1 | 3 | 94 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [HistogramEqualization](./HistogramEqualization.md) |
| `OperatorType.ImageAdd` | 图像加法 | 2 | 1 | 6 | 100 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [ImageAdd](./ImageAdd.md) |
| `OperatorType.ImageBlend` | 图像融合 | 2 | 1 | 3 | 76 (B) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [ImageBlend](./ImageBlend.md) |
| `OperatorType.ImageCrop` | 图像裁剪 | 1 | 1 | 4 | 94 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [ImageCrop](./ImageCrop.md) |
| `OperatorType.ImageDiff` | 图像对比 | 2 | 2 | 0 | 71 (B) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [ImageDiff](./ImageDiff.md) |
| `OperatorType.ImageNormalize` | 图像归一化 | 1 | 1 | 3 | 94 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [ImageNormalize](./ImageNormalize.md) |
| `OperatorType.ImageResize` | 图像缩放 | 1 | 1 | 5 | 94 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [ImageResize](./ImageResize.md) |
| `OperatorType.ImageRotate` | 图像旋转 | 1 | 1 | 5 | 94 (A) | `1.0.0` | 该算子基于仿射模型执行旋转、缩放或平移等二维几何变换。 | [ImageRotate](./ImageRotate.md) |
| `OperatorType.ImageSubtract` | Image Subtract | 2 | 4 | 1 | 89 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [ImageSubtract](./ImageSubtract.md) |
| `OperatorType.LaplacianSharpen` | 拉普拉斯锐化 | 1 | 1 | 3 | 76 (B) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [LaplacianSharpen](./LaplacianSharpen.md) |
| `OperatorType.MeanFilter` | 均值滤波 | 1 | 1 | 2 | 94 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [MeanFilter](./MeanFilter.md) |
| `OperatorType.MedianBlur` | 中值滤波 | 1 | 1 | 1 | 94 (A) | `1.0.0` | 该算子通过局部中值替换中心像素，特别适合抑制椒盐噪声和孤立异常点。 | [MedianBlur](./MedianBlur.md) |
| `OperatorType.MorphologicalOperation` | Morphological Operation | 1 | 1 | 7 | 76 (B) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [MorphologicalOperation](./MorphologicalOperation.md) |
| `OperatorType.Morphology` | Morphology (Legacy) | 1 | 1 | 6 | 94 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [Morphology](./Morphology.md) |
| `OperatorType.PerspectiveTransform` | 透视变换 | 3 | 1 | 20 | 100 (A) | `1.0.0` | 该算子利用单应性矩阵对图像做透视变换，用于视角校正或几何对齐。 | [PerspectiveTransform](./PerspectiveTransform.md) |
| `OperatorType.ShadingCorrection` | 光照校正 | 2 | 1 | 2 | 94 (A) | `1.0.0` | 该算子基于高斯卷积平滑图像，在抑制高频噪声的同时尽量保持整体结构稳定。 | [ShadingCorrection](./ShadingCorrection.md) |
| `OperatorType.Thresholding` | 二值化 | 1 | 1 | 4 | 76 (B) | `1.0.0` | 该算子基于固定阈值或自动阈值策略把图像分成前景和背景两类，可用于快速分割。 | [Thresholding](./Thresholding.md) |

### 颜色处理 (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.ColorDetection` | 颜色检测 | 1 | 4 | 9 | 76 (B) | `1.0.0` | 该算子结合学习型模型或规则判定完成识别、检测或缺陷筛查。 | [ColorDetection](./ColorDetection.md) |
| `OperatorType.ColorMeasurement` | 颜色测量 | 2 | 8 | 8 | 94 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [ColorMeasurement](./ColorMeasurement.md) |
