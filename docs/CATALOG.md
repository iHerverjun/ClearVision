# 算子目录 / Operator Catalog

> 生成时间 / Generated At: `2026-03-18 19:00:34 +08:00`
> 算子总数 / Total Operators: **154**

## 分类统计 / Category Summary
| 分类 (Category) | 数量 (Count) | 占比 (Ratio) |
|------|------:|------:|
| 3D | 6 | 3.9% |
| AI检测 | 6 | 3.9% |
| Analysis | 1 | 0.6% |
| Frequency | 3 | 1.9% |
| Morphology | 5 | 3.2% |
| Region | 4 | 2.6% |
| Texture | 2 | 1.3% |
| 匹配定位 | 8 | 5.2% |
| 变量 | 4 | 2.6% |
| 图像处理 | 4 | 2.6% |
| 定位 | 7 | 4.5% |
| 拆分组合 | 2 | 1.3% |
| 数据处理 | 10 | 6.5% |
| 标定 | 12 | 7.8% |
| 检测 | 20 | 13.0% |
| 流程控制 | 6 | 3.9% |
| 特征提取 | 4 | 2.6% |
| 识别 | 2 | 1.3% |
| 辅助 | 3 | 1.9% |
| 输出 | 2 | 1.3% |
| 通信 | 8 | 5.2% |
| 通用 | 4 | 2.6% |
| 逻辑工具 | 5 | 3.2% |
| 采集 | 1 | 0.6% |
| 预处理 | 23 | 14.9% |
| 颜色处理 | 2 | 1.3% |

## 质量评分 / Quality Score
- 平均分 / Average: **86.4**
| 等级 (Level) | 数量 (Count) |
|------|------:|
| A | 99 |
| B | 34 |
| C | 21 |

## 分类索引 / Grouped Index

### 3D (6)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.EuclideanClusterExtraction` | 欧氏聚类分割 | 1 | 3 | 3 | 100 (A) | `1.0.0` | - | [EuclideanClusterExtraction](./operators/EuclideanClusterExtraction.md) |
| `OperatorType.PPFEstimation` | PPF点对特征 | 1 | 3 | 3 | 95 (A) | `1.0.0` | - | [PPFEstimation](./operators/PPFEstimation.md) |
| `OperatorType.PPFMatch` | PPF表面匹配 | 2 | 6 | 10 | 95 (A) | `1.0.1` | - | [PPFMatch](./operators/PPFMatch.md) |
| `OperatorType.RansacPlaneSegmentation` | RANSAC平面分割 | 1 | 8 | 3 | 95 (A) | `1.0.0` | - | [RansacPlaneSegmentation](./operators/RansacPlaneSegmentation.md) |
| `OperatorType.StatisticalOutlierRemoval` | 统计滤波 | 1 | 3 | 2 | 95 (A) | `1.0.0` | - | [StatisticalOutlierRemoval](./operators/StatisticalOutlierRemoval.md) |
| `OperatorType.VoxelDownsample` | 体素下采样 | 1 | 2 | 1 | 85 (A) | `1.0.0` | - | [VoxelDownsample](./operators/VoxelDownsample.md) |

### AI检测 (6)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.AnomalyDetection` | 异常检测 | 2 | 6 | 10 | 90 (A) | `1.0.0` | Simplified PatchCore | [AnomalyDetection](./operators/AnomalyDetection.md) |
| `OperatorType.DeepLearning` | 深度学习 | 1 | 6 | 9 | 100 (A) | `1.0.0` | 当前实现是一个基于 ONNX Runtime 的 YOLO 推理算子，支持： | [DeepLearning](./operators/DeepLearning.md) |
| `OperatorType.DualModalVoting` | 双模态投票 | 2 | 3 | 6 | 90 (A) | `1.0.0` | 该算子结合学习型模型或规则判定完成识别、检测或缺陷筛查。 | [DualModalVoting](./operators/DualModalVoting.md) |
| `OperatorType.EdgePairDefect` | 边缘对缺陷 | 3 | 4 | 4 | 94 (A) | `1.0.0` | 该算子基于固定阈值或自动阈值策略把图像分成前景和背景两类，可用于快速分割。 | [EdgePairDefect](./operators/EdgePairDefect.md) |
| `OperatorType.SemanticSegmentation` | 语义分割 | 1 | 5 | 11 | 90 (A) | `1.0.0` | - | [SemanticSegmentation](./operators/SemanticSegmentation.md) |
| `OperatorType.SurfaceDefectDetection` | 表面缺陷检测 | 2 | 4 | 5 | 94 (A) | `1.0.0` | 该算子基于高斯卷积平滑图像，在抑制高频噪声的同时尽量保持整体结构稳定。 | [SurfaceDefectDetection](./operators/SurfaceDefectDetection.md) |

### Analysis (1)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.DistanceTransform` | Distance Transform | 1 | 4 | 7 | 90 (A) | `1.0.0` | - | [DistanceTransform](./operators/DistanceTransform.md) |

### Frequency (3)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.FFT1D` | FFT 1D | 2 | 4 | 0 | 61 (C) | `1.0.0` | - | [FFT1D](./operators/FFT1D.md) |
| `OperatorType.FrequencyFilter` | Frequency Filter | 5 | 3 | 0 | 61 (C) | `1.0.0` | - | [FrequencyFilter](./operators/FrequencyFilter.md) |
| `OperatorType.InverseFFT1D` | Inverse FFT 1D | 2 | 4 | 0 | 61 (C) | `1.0.0` | - | [InverseFFT1D](./operators/InverseFFT1D.md) |

### Morphology (5)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.RegionClosing` | Region Closing | 2 | 3 | 3 | 63 (C) | `1.0.0` | - | [RegionClosing](./operators/RegionClosing.md) |
| `OperatorType.RegionDilation` | Region Dilation | 2 | 3 | 4 | 63 (C) | `1.0.0` | - | [RegionDilation](./operators/RegionDilation.md) |
| `OperatorType.RegionErosion` | Region Erosion | 2 | 3 | 4 | 63 (C) | `1.0.0` | - | [RegionErosion](./operators/RegionErosion.md) |
| `OperatorType.RegionOpening` | Region Opening | 2 | 3 | 3 | 63 (C) | `1.0.0` | - | [RegionOpening](./operators/RegionOpening.md) |
| `OperatorType.RegionSkeleton` | Region Skeleton | 2 | 5 | 2 | 63 (C) | `1.0.0` | - | [RegionSkeleton](./operators/RegionSkeleton.md) |

### Region (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.RegionComplement` | Region Complement | 4 | 3 | 0 | 58 (C) | `1.0.0` | - | [RegionComplement](./operators/RegionComplement.md) |
| `OperatorType.RegionDifference` | Region Difference | 2 | 3 | 0 | 61 (C) | `1.0.0` | - | [RegionDifference](./operators/RegionDifference.md) |
| `OperatorType.RegionIntersection` | Region Intersection | 2 | 3 | 0 | 61 (C) | `1.0.0` | - | [RegionIntersection](./operators/RegionIntersection.md) |
| `OperatorType.RegionUnion` | Region Union | 2 | 3 | 0 | 61 (C) | `1.0.0` | - | [RegionUnion](./operators/RegionUnion.md) |

### Texture (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.GlcmTexture` | GLCM Texture Features | 1 | 6 | 9 | 90 (A) | `1.0.0` | - | [GlcmTexture](./operators/GlcmTexture.md) |
| `OperatorType.LawsTextureFilter` | Laws Texture Filter | 1 | 3 | 5 | 90 (A) | `1.0.0` | - | [LawsTextureFilter](./operators/LawsTextureFilter.md) |

### 匹配定位 (8)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.AkazeFeatureMatch` | AKAZE特征匹配 | 2 | 5 | 5 | 73 (B) | `1.0.0` | 该算子基于局部特征点匹配完成模板定位，核心流程是： | [AkazeFeatureMatch](./operators/AkazeFeatureMatch.md) |
| `OperatorType.GradientShapeMatch` | 梯度形状匹配 | 2 | 5 | 6 | 83 (B) | `1.0.0` | 该算子不是直接在原始灰度图上做相关性匹配，而是使用自定义 GradientShape… | [GradientShapeMatch](./operators/GradientShapeMatch.md) |
| `OperatorType.LocalDeformableMatching` | Local Deformable Matching | 2 | 6 | 15 | 90 (A) | `1.0.0` | - | [LocalDeformableMatching](./operators/LocalDeformableMatching.md) |
| `OperatorType.OrbFeatureMatch` | ORB特征匹配 | 2 | 5 | 7 | 73 (B) | `1.0.0` | 该算子与 AkazeFeatureMatchOperator 属于同一类局部特征匹配… | [OrbFeatureMatch](./operators/OrbFeatureMatch.md) |
| `OperatorType.PlanarMatching` | Planar Matching | 2 | 4 | 18 | 90 (A) | `1.0.0` | - | [PlanarMatching](./operators/PlanarMatching.md) |
| `OperatorType.PyramidShapeMatch` | 金字塔形状匹配 | 2 | 5 | 15 | 83 (B) | `1.0.0` | 该算子围绕模板、特征或几何相似性执行定位匹配，用于判断目标是否存在以及位姿大致位置。 | [PyramidShapeMatch](./operators/PyramidShapeMatch.md) |
| `OperatorType.ShapeMatching` | 旋转尺度模板匹配 | 2 | 2 | 10 | 100 (A) | `1.1.0` | 虽然名称叫“形状匹配”，但当前实现本质上仍是基于灰度模板匹配的旋转搜索，而不是基于轮… | [ShapeMatching](./operators/ShapeMatching.md) |
| `OperatorType.TemplateMatching` | 模板匹配 | 2 | 6 | 3 | 96 (A) | `1.0.0` | 该算子基于经典模板匹配，在搜索图像上滑动模板窗口并计算每个位置的相似度响应图，再取全… | [TemplateMatching](./operators/TemplateMatching.md) |

### 变量 (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.CycleCounter` | 循环计数器 | 0 | 5 | 2 | 66 (C) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [CycleCounter](./operators/CycleCounter.md) |
| `OperatorType.VariableIncrement` | 变量递增 | 0 | 5 | 5 | 73 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [VariableIncrement](./operators/VariableIncrement.md) |
| `OperatorType.VariableRead` | 变量读取 | 0 | 3 | 3 | 73 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [VariableRead](./operators/VariableRead.md) |
| `OperatorType.VariableWrite` | 变量写入 | 1 | 3 | 4 | 73 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [VariableWrite](./operators/VariableWrite.md) |

### 图像处理 (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.AffineTransform` | 仿射变换 | 1 | 2 | 9 | 100 (A) | `1.0.0` | 该算子基于仿射模型执行旋转、缩放或平移等二维几何变换。 | [AffineTransform](./operators/AffineTransform.md) |
| `OperatorType.CopyMakeBorder` | 边界填充 | 1 | 1 | 6 | 94 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [CopyMakeBorder](./operators/CopyMakeBorder.md) |
| `OperatorType.ImageStitching` | 图像拼接 | 2 | 2 | 3 | 94 (A) | `1.0.0` | 该算子基于固定阈值或自动阈值策略把图像分成前景和背景两类，可用于快速分割。 | [ImageStitching](./operators/ImageStitching.md) |
| `OperatorType.PolarUnwrap` | 极坐标展开 | 2 | 1 | 8 | 100 (A) | `1.0.0` | 该算子把以某个中心为参考的环形区域，从笛卡尔坐标系展开到极坐标平面。 | [PolarUnwrap](./operators/PolarUnwrap.md) |

### 定位 (7)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.BlobLabeling` | 连通域标注 | 2 | 3 | 3 | 100 (A) | `1.0.0` | 该算子基于固定阈值或自动阈值策略把图像分成前景和背景两类，可用于快速分割。 | [BlobLabeling](./operators/BlobLabeling.md) |
| `OperatorType.CornerDetection` | 角点检测 | 1 | 3 | 5 | 94 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [CornerDetection](./operators/CornerDetection.md) |
| `OperatorType.EdgeIntersection` | 边线交点 | 2 | 3 | 0 | 89 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [EdgeIntersection](./operators/EdgeIntersection.md) |
| `OperatorType.ParallelLineFind` | 平行线查找 | 1 | 6 | 4 | 94 (A) | `1.0.0` | 该算子从边缘图中提取直线段候选，再基于几何关系输出线结构或测量结果。 | [ParallelLineFind](./operators/ParallelLineFind.md) |
| `OperatorType.PositionCorrection` | 位置修正 | 4 | 5 | 3 | 94 (A) | `1.0.0` | 该算子围绕标定、坐标映射或几何重采样展开，目标是在不同空间之间建立稳定映射关系。 | [PositionCorrection](./operators/PositionCorrection.md) |
| `OperatorType.QuadrilateralFind` | 四边形查找 | 1 | 5 | 4 | 94 (A) | `1.0.0` | 该算子从二值结果中提取轮廓点集，并以轮廓为单位继续做几何分析或筛选。 | [QuadrilateralFind](./operators/QuadrilateralFind.md) |
| `OperatorType.RectangleDetection` | 矩形检测 | 1 | 7 | 4 | 94 (A) | `1.0.0` | 该算子从二值结果中提取轮廓点集，并以轮廓为单位继续做几何分析或筛选。 | [RectangleDetection](./operators/RectangleDetection.md) |

### 拆分组合 (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.ImageCompose` | 图像组合 | 4 | 1 | 3 | 94 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [ImageCompose](./operators/ImageCompose.md) |
| `OperatorType.ImageTiling` | 图像切片 | 1 | 3 | 4 | 94 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [ImageTiling](./operators/ImageTiling.md) |

### 数据处理 (10)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.Aggregator` | 数据聚合 | 3 | 5 | 1 | 66 (C) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [Aggregator](./operators/Aggregator.md) |
| `OperatorType.ArrayIndexer` | 数组索引器 | 1 | 3 | 2 | 79 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [ArrayIndexer](./operators/ArrayIndexer.md) |
| `OperatorType.BoxFilter` | 候选框过滤 (Bounding Box) | 2 | 3 | 9 | 90 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [BoxFilter](./operators/BoxFilter.md) |
| `OperatorType.BoxNms` | 候选框抑制 | 2 | 3 | 3 | 90 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [BoxNms](./operators/BoxNms.md) |
| `OperatorType.DatabaseWrite` | 数据库写入 | 1 | 2 | 3 | 100 (A) | `1.0.0` | 该算子负责把流程结果写入文件、数据库或外部系统，或从外围资源获取输入。 | [DatabaseWrite](./operators/DatabaseWrite.md) |
| `OperatorType.JsonExtractor` | JSON 提取器 | 1 | 2 | 1 | 83 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [JsonExtractor](./operators/JsonExtractor.md) |
| `OperatorType.MathOperation` | 数值计算 | 2 | 2 | 1 | 83 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [MathOperation](./operators/MathOperation.md) |
| `OperatorType.PointAlignment` | 点位对齐 | 2 | 3 | 2 | 94 (A) | `1.0.0` | 该算子围绕标定、坐标映射或几何重采样展开，目标是在不同空间之间建立稳定映射关系。 | [PointAlignment](./operators/PointAlignment.md) |
| `OperatorType.PointCorrection` | 点位修正 | 4 | 4 | 3 | 94 (A) | `1.0.0` | 该算子围绕标定、坐标映射或几何重采样展开，目标是在不同空间之间建立稳定映射关系。 | [PointCorrection](./operators/PointCorrection.md) |
| `OperatorType.UnitConvert` | 单位换算 | 2 | 2 | 4 | 96 (A) | `1.0.0` | 该算子围绕标定、坐标映射或几何重采样展开，目标是在不同空间之间建立稳定映射关系。 | [UnitConvert](./operators/UnitConvert.md) |

### 标定 (12)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.CalibrationLoader` | 标定加载 | 0 | 4 | 2 | 100 (A) | `1.0.0` | 该算子围绕标定、坐标映射或几何重采样展开，目标是在不同空间之间建立稳定映射关系。 | [CalibrationLoader](./operators/CalibrationLoader.md) |
| `OperatorType.CameraCalibration` | Camera Calibration | 1 | 2 | 7 | 100 (A) | `1.0.0` | 该算子基于标定板角点/圆点，估计相机内参矩阵 CameraMatrix 和畸变参数 … | [CameraCalibration](./operators/CameraCalibration.md) |
| `OperatorType.CoordinateTransform` | 坐标转换 | 3 | 3 | 4 | 100 (A) | `1.0.0` | 该算子围绕标定、坐标映射或几何重采样展开，目标是在不同空间之间建立稳定映射关系。 | [CoordinateTransform](./operators/CoordinateTransform.md) |
| `OperatorType.FisheyeCalibration` | Fisheye Calibration | 1 | 4 | 9 | 90 (A) | `1.0.0` | - | [FisheyeCalibration](./operators/FisheyeCalibration.md) |
| `OperatorType.FisheyeUndistort` | Fisheye Undistort | 2 | 2 | 5 | 90 (A) | `1.0.0` | - | [FisheyeUndistort](./operators/FisheyeUndistort.md) |
| `OperatorType.HandEyeCalibration` | 手眼标定 | 2 | 8 | 4 | 90 (A) | `1.0.0` | OpenCV Hand-Eye Calibration | [HandEyeCalibration](./operators/HandEyeCalibration.md) |
| `OperatorType.HandEyeCalibrationValidator` | 手眼标定验证 | 3 | 7 | 1 | 73 (B) | `1.0.0` | Hand-Eye Consistency Validation | [HandEyeCalibrationValidator](./operators/HandEyeCalibrationValidator.md) |
| `OperatorType.NPointCalibration` | N点标定 | 1 | 3 | 3 | 100 (A) | `1.0.0` | 该算子围绕标定、坐标映射或几何重采样展开，目标是在不同空间之间建立稳定映射关系。 | [NPointCalibration](./operators/NPointCalibration.md) |
| `OperatorType.PixelToWorldTransform` | Pixel To World Transform | 3 | 3 | 9 | 90 (A) | `1.0.0` | - | [PixelToWorldTransform](./operators/PixelToWorldTransform.md) |
| `OperatorType.StereoCalibration` | Stereo Calibration | 2 | 6 | 11 | 90 (A) | `1.0.0` | - | [StereoCalibration](./operators/StereoCalibration.md) |
| `OperatorType.TranslationRotationCalibration` | 平移旋转标定 | 1 | 3 | 3 | 100 (A) | `1.0.0` | 该算子围绕标定、坐标映射或几何重采样展开，目标是在不同空间之间建立稳定映射关系。 | [TranslationRotationCalibration](./operators/TranslationRotationCalibration.md) |
| `OperatorType.Undistort` | Undistort | 2 | 1 | 1 | 95 (A) | `1.0.0` | 该算子根据相机标定结果中的内参矩阵 CameraMatrix 和畸变系数 DistC… | [Undistort](./operators/Undistort.md) |

### 检测 (20)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.AngleMeasurement` | 角度测量 | 1 | 2 | 7 | 94 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [AngleMeasurement](./operators/AngleMeasurement.md) |
| `OperatorType.ArcCaliper` | Arc Caliper | 7 | 2 | 0 | 58 (C) | `1.0.0` | - | [ArcCaliper](./operators/ArcCaliper.md) |
| `OperatorType.CaliperTool` | 卡尺工具 | 2 | 7 | 9 | 94 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [CaliperTool](./operators/CaliperTool.md) |
| `OperatorType.CircleMeasurement` | 圆测量 | 1 | 7 | 7 | 100 (A) | `1.0.0` | 该算子基于高斯卷积平滑图像，在抑制高频噪声的同时尽量保持整体结构稳定。 | [CircleMeasurement](./operators/CircleMeasurement.md) |
| `OperatorType.ContourExtrema` | Contour Extrema | 3 | 6 | 0 | 58 (C) | `1.0.0` | - | [ContourExtrema](./operators/ContourExtrema.md) |
| `OperatorType.ContourMeasurement` | 轮廓测量 | 1 | 4 | 4 | 94 (A) | `1.0.0` | 该算子基于固定阈值或自动阈值策略把图像分成前景和背景两类，可用于快速分割。 | [ContourMeasurement](./operators/ContourMeasurement.md) |
| `OperatorType.GapMeasurement` | 间隙测量 | 2 | 6 | 4 | 94 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [GapMeasurement](./operators/GapMeasurement.md) |
| `OperatorType.GeoMeasurement` | 几何测量 | 2 | 5 | 2 | 94 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [GeoMeasurement](./operators/GeoMeasurement.md) |
| `OperatorType.GeometricFitting` | Geometric Fitting | 1 | 2 | 8 | 100 (A) | `1.0.0` | 当前实现的几何拟合流程并不是直接接收点集输入，而是： | [GeometricFitting](./operators/GeometricFitting.md) |
| `OperatorType.GeometricTolerance` | 几何公差 | 1 | 5 | 9 | 94 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [GeometricTolerance](./operators/GeometricTolerance.md) |
| `OperatorType.HistogramAnalysis` | 直方图分析 | 1 | 7 | 6 | 94 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [HistogramAnalysis](./operators/HistogramAnalysis.md) |
| `OperatorType.LineLineDistance` | 线线距离 | 2 | 5 | 1 | 96 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [LineLineDistance](./operators/LineLineDistance.md) |
| `OperatorType.LineMeasurement` | 直线测量 | 1 | 5 | 4 | 94 (A) | `1.0.0` | 该算子从边缘图中提取直线段候选，再基于几何关系输出线结构或测量结果。 | [LineMeasurement](./operators/LineMeasurement.md) |
| `OperatorType.Measurement` | 测量 | 3 | 2 | 5 | 94 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [Measurement](./operators/Measurement.md) |
| `OperatorType.MinEnclosingGeometry` | Min Enclosing Geometry | 1 | 2 | 10 | 90 (A) | `1.0.0` | - | [MinEnclosingGeometry](./operators/MinEnclosingGeometry.md) |
| `OperatorType.PhaseClosure` | Phase Closure | 4 | 4 | 0 | 58 (C) | `1.0.0` | - | [PhaseClosure](./operators/PhaseClosure.md) |
| `OperatorType.PixelStatistics` | 像素统计 | 2 | 6 | 5 | 94 (A) | `1.0.0` | 该算子基于固定阈值或自动阈值策略把图像分成前景和背景两类，可用于快速分割。 | [PixelStatistics](./operators/PixelStatistics.md) |
| `OperatorType.PointLineDistance` | 点线距离 | 2 | 2 | 0 | 91 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [PointLineDistance](./operators/PointLineDistance.md) |
| `OperatorType.SharpnessEvaluation` | 清晰度评估 | 1 | 3 | 6 | 94 (A) | `1.0.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [SharpnessEvaluation](./operators/SharpnessEvaluation.md) |
| `OperatorType.WidthMeasurement` | 宽度测量 | 3 | 4 | 4 | 96 (A) | `1.0.0` | 当前实现有两种工作模式： | [WidthMeasurement](./operators/WidthMeasurement.md) |

### 流程控制 (6)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.Comparator` | 数值比较 | 2 | 2 | 5 | 61 (C) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [Comparator](./operators/Comparator.md) |
| `OperatorType.ConditionalBranch` | 条件分支 | 1 | 2 | 3 | 90 (A) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [ConditionalBranch](./operators/ConditionalBranch.md) |
| `OperatorType.Delay` | 延时 | 1 | 2 | 1 | 61 (C) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [Delay](./operators/Delay.md) |
| `OperatorType.ForEach` | ForEach 循环 | 1 | 1 | 4 | 83 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [ForEach](./operators/ForEach.md) |
| `OperatorType.ResultJudgment` | 结果判定 | 2 | 3 | 8 | 66 (C) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [ResultJudgment](./operators/ResultJudgment.md) |
| `OperatorType.TryCatch` | 异常捕获 | 1 | 4 | 3 | 75 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [TryCatch](./operators/TryCatch.md) |

### 特征提取 (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.BlobAnalysis` | Blob分析 | 2 | 4 | 17 | 100 (A) | `1.1.0` | 该算子围绕边缘、轮廓、点线关系或几何模型参数完成测量与定位。 | [BlobAnalysis](./operators/BlobAnalysis.md) |
| `OperatorType.ContourDetection` | 轮廓检测 | 1 | 3 | 8 | 94 (A) | `1.0.0` | 该算子基于固定阈值或自动阈值策略把图像分成前景和背景两类，可用于快速分割。 | [ContourDetection](./operators/ContourDetection.md) |
| `OperatorType.EdgeDetection` | Edge Detection | 1 | 2 | 8 | 76 (B) | `1.0.0` | 该算子基于高斯卷积平滑图像，在抑制高频噪声的同时尽量保持整体结构稳定。 | [EdgeDetection](./operators/EdgeDetection.md) |
| `OperatorType.SubpixelEdgeDetection` | Subpixel Edge Detection | 1 | 2 | 5 | 94 (A) | `1.0.0` | 该算子基于高斯卷积平滑图像，在抑制高频噪声的同时尽量保持整体结构稳定。 | [SubpixelEdgeDetection](./operators/SubpixelEdgeDetection.md) |

### 识别 (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.CodeRecognition` | 条码识别 | 1 | 4 | 2 | 94 (A) | `1.0.0` | 该算子结合学习型模型或规则判定完成识别、检测或缺陷筛查。 | [CodeRecognition](./operators/CodeRecognition.md) |
| `OperatorType.OcrRecognition` | OCR 识别 | 1 | 2 | 0 | 100 (A) | `1.0.0` | 该算子结合学习型模型或规则判定完成识别、检测或缺陷筛查。 | [OcrRecognition](./operators/OcrRecognition.md) |

### 辅助 (3)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.Comment` | 注释 | 1 | 2 | 1 | 61 (C) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [Comment](./operators/Comment.md) |
| `OperatorType.RoiManager` | ROI管理器 | 1 | 2 | 10 | 100 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [RoiManager](./operators/RoiManager.md) |
| `OperatorType.RoiTransform` | ROI跟踪 | 2 | 1 | 1 | 86 (A) | `1.0.0` | - | [RoiTransform](./operators/RoiTransform.md) |

### 输出 (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.ImageSave` | 图像保存 | 1 | 2 | 3 | 83 (B) | `1.0.0` | 该算子负责把流程结果写入文件、数据库或外部系统，或从外围资源获取输入。 | [ImageSave](./operators/ImageSave.md) |
| `OperatorType.ResultOutput` | 结果输出 | 4 | 6 | 2 | 98 (A) | `1.0.1` | 该算子负责把流程结果写入文件、数据库或外部系统，或从外围资源获取输入。 | [ResultOutput](./operators/ResultOutput.md) |

### 通信 (8)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.HttpRequest` | HTTP 请求 | 2 | 3 | 6 | 83 (B) | `1.0.0` | 该算子不执行图像算法，而是把流程参数映射为通信请求，与外部设备或服务完成读写和响应解… | [HttpRequest](./operators/HttpRequest.md) |
| `OperatorType.MitsubishiMcCommunication` | 三菱MC通信 | 1 | 2 | 12 | 80 (B) | `1.0.0` | 该算子不执行图像算法，而是把流程参数映射为通信请求，与外部设备或服务完成读写和响应解… | [MitsubishiMcCommunication](./operators/MitsubishiMcCommunication.md) |
| `OperatorType.ModbusCommunication` | Modbus通信 | 1 | 2 | 8 | 98 (A) | `1.0.0` | 该算子不执行图像算法，而是把流程参数映射为通信请求，与外部设备或服务完成读写和响应解… | [ModbusCommunication](./operators/ModbusCommunication.md) |
| `OperatorType.MqttPublish` | MQTT 发布 | 2 | 1 | 6 | 100 (A) | `1.0.0` | 该算子不执行图像算法，而是把流程参数映射为通信请求，与外部设备或服务完成读写和响应解… | [MqttPublish](./operators/MqttPublish.md) |
| `OperatorType.OmronFinsCommunication` | 欧姆龙FINS通信 | 1 | 2 | 12 | 80 (B) | `1.0.0` | 该算子不执行图像算法，而是把流程参数映射为通信请求，与外部设备或服务完成读写和响应解… | [OmronFinsCommunication](./operators/OmronFinsCommunication.md) |
| `OperatorType.SerialCommunication` | 串口通信 | 1 | 1 | 8 | 83 (B) | `1.0.0` | 该算子不执行图像算法，而是把流程参数映射为通信请求，与外部设备或服务完成读写和响应解… | [SerialCommunication](./operators/SerialCommunication.md) |
| `OperatorType.SiemensS7Communication` | 西门子S7通信 | 1 | 2 | 14 | 80 (B) | `1.0.0` | 该算子不执行图像算法，而是把流程参数映射为通信请求，与外部设备或服务完成读写和响应解… | [SiemensS7Communication](./operators/SiemensS7Communication.md) |
| `OperatorType.TcpCommunication` | TCP通信 | 1 | 2 | 6 | 98 (A) | `1.0.0` | 该算子不执行图像算法，而是把流程参数映射为通信请求，与外部设备或服务完成读写和响应解… | [TcpCommunication](./operators/TcpCommunication.md) |

### 通用 (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.LogicGate` | 逻辑门 | 2 | 1 | 1 | 73 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [LogicGate](./operators/LogicGate.md) |
| `OperatorType.Statistics` | Statistics | 1 | 7 | 5 | 90 (A) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [Statistics](./operators/Statistics.md) |
| `OperatorType.StringFormat` | 字符串格式化 | 2 | 1 | 1 | 76 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [StringFormat](./operators/StringFormat.md) |
| `OperatorType.TypeConvert` | Type Convert | 1 | 6 | 2 | 83 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [TypeConvert](./operators/TypeConvert.md) |

### 逻辑工具 (5)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.PointSetTool` | 点集工具 | 2 | 4 | 6 | 90 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [PointSetTool](./operators/PointSetTool.md) |
| `OperatorType.ScriptOperator` | 脚本算子 | 4 | 2 | 3 | 100 (A) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [ScriptOperator](./operators/ScriptOperator.md) |
| `OperatorType.TextSave` | Text Save | 2 | 2 | 5 | 100 (A) | `1.0.0` | 该算子负责把流程结果写入文件、数据库或外部系统，或从外围资源获取输入。 | [TextSave](./operators/TextSave.md) |
| `OperatorType.TimerStatistics` | 计时统计 | 1 | 4 | 2 | 84 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [TimerStatistics](./operators/TimerStatistics.md) |
| `OperatorType.TriggerModule` | 触发模块 | 1 | 3 | 3 | 84 (B) | `1.0.0` | 该算子主要执行流程控制、数据整理、变量处理或类型转换，用于把上下游节点连接得更稳定。 | [TriggerModule](./operators/TriggerModule.md) |

### 采集 (1)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.ImageAcquisition` | 图像采集 | 2 | 1 | 6 | 78 (B) | `1.0.0` | 该算子负责把流程结果写入文件、数据库或外部系统，或从外围资源获取输入。 | [ImageAcquisition](./operators/ImageAcquisition.md) |

### 预处理 (23)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.AdaptiveThreshold` | 自适应阈值 | 1 | 1 | 5 | 94 (A) | `1.0.0` | 自适应阈值不会对整幅图使用一个全局阈值，而是针对每个像素在其邻域窗口 W(x, y)… | [AdaptiveThreshold](./operators/AdaptiveThreshold.md) |
| `OperatorType.BilateralFilter` | 双边滤波 | 1 | 1 | 3 | 94 (A) | `1.0.0` | 该算子使用双边滤波同时考虑空间距离与像素差异，在保边前提下降低噪声。 | [BilateralFilter](./operators/BilateralFilter.md) |
| `OperatorType.ClaheEnhancement` | CLAHE增强 | 1 | 1 | 5 | 76 (B) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [ClaheEnhancement](./operators/ClaheEnhancement.md) |
| `OperatorType.ColorConversion` | 颜色空间转换 | 1 | 1 | 2 | 94 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [ColorConversion](./operators/ColorConversion.md) |
| `OperatorType.Filtering` | Gaussian Blur | 1 | 1 | 4 | 76 (B) | `1.0.0` | Gaussian Blur (OpenCV) | [Filtering](./operators/Filtering.md) |
| `OperatorType.FrameAveraging` | 帧平均 | 1 | 2 | 2 | 94 (A) | `1.0.0` | 该算子做的是时间域融合，不是空间域滤波。它会保留最近 N 帧图像，在时间轴上对同一像… | [FrameAveraging](./operators/FrameAveraging.md) |
| `OperatorType.HistogramEqualization` | 直方图均衡化 | 1 | 1 | 3 | 94 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [HistogramEqualization](./operators/HistogramEqualization.md) |
| `OperatorType.ImageAdd` | 图像加法 | 2 | 1 | 6 | 100 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [ImageAdd](./operators/ImageAdd.md) |
| `OperatorType.ImageBlend` | 图像融合 | 2 | 1 | 3 | 76 (B) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [ImageBlend](./operators/ImageBlend.md) |
| `OperatorType.ImageCrop` | 图像裁剪 | 1 | 1 | 4 | 94 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [ImageCrop](./operators/ImageCrop.md) |
| `OperatorType.ImageDiff` | 图像对比 | 2 | 2 | 0 | 71 (B) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [ImageDiff](./operators/ImageDiff.md) |
| `OperatorType.ImageNormalize` | 图像归一化 | 1 | 1 | 3 | 94 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [ImageNormalize](./operators/ImageNormalize.md) |
| `OperatorType.ImageResize` | 图像缩放 | 1 | 1 | 5 | 94 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [ImageResize](./operators/ImageResize.md) |
| `OperatorType.ImageRotate` | 图像旋转 | 1 | 1 | 5 | 94 (A) | `1.0.0` | 该算子基于仿射模型执行旋转、缩放或平移等二维几何变换。 | [ImageRotate](./operators/ImageRotate.md) |
| `OperatorType.ImageSubtract` | Image Subtract | 2 | 4 | 1 | 89 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [ImageSubtract](./operators/ImageSubtract.md) |
| `OperatorType.LaplacianSharpen` | 拉普拉斯锐化 | 1 | 1 | 3 | 76 (B) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [LaplacianSharpen](./operators/LaplacianSharpen.md) |
| `OperatorType.MeanFilter` | 均值滤波 | 1 | 1 | 2 | 94 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [MeanFilter](./operators/MeanFilter.md) |
| `OperatorType.MedianBlur` | 中值滤波 | 1 | 1 | 1 | 94 (A) | `1.0.0` | 该算子通过局部中值替换中心像素，特别适合抑制椒盐噪声和孤立异常点。 | [MedianBlur](./operators/MedianBlur.md) |
| `OperatorType.MorphologicalOperation` | Morphological Operation | 1 | 1 | 7 | 76 (B) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [MorphologicalOperation](./operators/MorphologicalOperation.md) |
| `OperatorType.Morphology` | Morphology (Legacy) | 1 | 1 | 6 | 94 (A) | `1.0.0` | 该算子主要做图像预处理、增强、分割、变换或格式调整，为后续节点提供更稳定输入。 | [Morphology](./operators/Morphology.md) |
| `OperatorType.PerspectiveTransform` | 透视变换 | 3 | 1 | 20 | 100 (A) | `1.0.0` | 该算子利用单应性矩阵对图像做透视变换，用于视角校正或几何对齐。 | [PerspectiveTransform](./operators/PerspectiveTransform.md) |
| `OperatorType.ShadingCorrection` | 光照校正 | 2 | 1 | 2 | 94 (A) | `1.0.0` | 该算子基于高斯卷积平滑图像，在抑制高频噪声的同时尽量保持整体结构稳定。 | [ShadingCorrection](./operators/ShadingCorrection.md) |
| `OperatorType.Thresholding` | 二值化 | 1 | 1 | 4 | 76 (B) | `1.0.0` | 该算子基于固定阈值或自动阈值策略把图像分成前景和背景两类，可用于快速分割。 | [Thresholding](./operators/Thresholding.md) |

### 颜色处理 (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.ColorDetection` | 颜色检测 | 1 | 4 | 9 | 76 (B) | `1.0.0` | 该算子结合学习型模型或规则判定完成识别、检测或缺陷筛查。 | [ColorDetection](./operators/ColorDetection.md) |
| `OperatorType.ColorMeasurement` | 颜色测量 | 2 | 8 | 9 | 94 (A) | `1.0.2` | - | [ColorMeasurement](./operators/ColorMeasurement.md) |
