# 算子目录 / Operator Catalog

> 生成时间 / Generated At: `2026-04-09 23:22:12 +08:00`
> 算子总数 / Total Operators: **155**

## 分类统计 / Category Summary
| 分类 (Category) | 数量 (Count) | 占比 (Ratio) |
|------|------:|------:|
| 3D | 6 | 3.9% |
| AI Inspection | 1 | 0.6% |
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
| 标定 | 12 | 7.7% |
| 检测 | 20 | 12.9% |
| 流程控制 | 6 | 3.9% |
| 特征提取 | 4 | 2.6% |
| 识别 | 2 | 1.3% |
| 辅助 | 3 | 1.9% |
| 输出 | 2 | 1.3% |
| 通信 | 8 | 5.2% |
| 通用 | 4 | 2.6% |
| 逻辑工具 | 5 | 3.2% |
| 采集 | 1 | 0.6% |
| 预处理 | 23 | 14.8% |
| 颜色处理 | 2 | 1.3% |

## 质量评分 / Quality Score
- 平均分 / Average: **78.6**
| 等级 (Level) | 数量 (Count) |
|------|------:|
| A | 50 |
| B | 66 |
| C | 36 |
| D | 3 |

## 分类索引 / Grouped Index

### 3D (6)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.EuclideanClusterExtraction` | 欧氏聚类分割 | 1 | 3 | 3 | 90 (A) | `1.0.0` | - | [EuclideanClusterExtraction](./EuclideanClusterExtraction.md) |
| `OperatorType.PPFEstimation` | PPF点对特征 | 1 | 3 | 3 | 85 (A) | `1.0.0` | - | [PPFEstimation](./PPFEstimation.md) |
| `OperatorType.PPFMatch` | PPF表面匹配 | 2 | 16 | 10 | 85 (A) | `1.0.4` | - | [PPFMatch](./PPFMatch.md) |
| `OperatorType.RansacPlaneSegmentation` | RANSAC平面分割 | 1 | 8 | 3 | 85 (A) | `1.0.0` | - | [RansacPlaneSegmentation](./RansacPlaneSegmentation.md) |
| `OperatorType.StatisticalOutlierRemoval` | 统计滤波 | 1 | 3 | 2 | 85 (A) | `1.0.0` | - | [StatisticalOutlierRemoval](./StatisticalOutlierRemoval.md) |
| `OperatorType.VoxelDownsample` | 体素下采样 | 1 | 2 | 1 | 85 (A) | `1.0.0` | - | [VoxelDownsample](./VoxelDownsample.md) |

### AI Inspection (1)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.DetectionSequenceJudge` | Detection Sequence Judge | 1 | 8 | 7 | 80 (B) | `1.0.0` | - | [DetectionSequenceJudge](./DetectionSequenceJudge.md) |

### AI检测 (6)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.AnomalyDetection` | 异常检测 | 2 | 6 | 10 | 90 (A) | `1.0.0` | Simplified PatchCore | [AnomalyDetection](./AnomalyDetection.md) |
| `OperatorType.DeepLearning` | 深度学习 | 1 | 7 | 10 | 90 (A) | `1.0.0` | - | [DeepLearning](./DeepLearning.md) |
| `OperatorType.DualModalVoting` | 双模态投票 | 2 | 3 | 6 | 80 (B) | `1.0.0` | - | [DualModalVoting](./DualModalVoting.md) |
| `OperatorType.EdgePairDefect` | 边缘对缺陷 | 3 | 4 | 4 | 84 (B) | `1.0.0` | - | [EdgePairDefect](./EdgePairDefect.md) |
| `OperatorType.SemanticSegmentation` | 语义分割 | 1 | 5 | 11 | 90 (A) | `1.0.0` | - | [SemanticSegmentation](./SemanticSegmentation.md) |
| `OperatorType.SurfaceDefectDetection` | 表面缺陷检测 | 2 | 4 | 5 | 84 (B) | `1.0.0` | - | [SurfaceDefectDetection](./SurfaceDefectDetection.md) |

### Analysis (1)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.DistanceTransform` | Distance Transform | 1 | 4 | 7 | 90 (A) | `1.0.0` | - | [DistanceTransform](./DistanceTransform.md) |

### Frequency (3)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.FFT1D` | FFT 1D | 2 | 4 | 0 | 61 (C) | `1.0.0` | - | [FFT1D](./FFT1D.md) |
| `OperatorType.FrequencyFilter` | Frequency Filter | 5 | 3 | 0 | 61 (C) | `1.0.0` | - | [FrequencyFilter](./FrequencyFilter.md) |
| `OperatorType.InverseFFT1D` | Inverse FFT 1D | 2 | 4 | 0 | 61 (C) | `1.0.0` | - | [InverseFFT1D](./InverseFFT1D.md) |

### Morphology (5)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.RegionClosing` | Region Closing | 2 | 3 | 3 | 63 (C) | `1.0.0` | - | [RegionClosing](./RegionClosing.md) |
| `OperatorType.RegionDilation` | Region Dilation | 2 | 3 | 4 | 63 (C) | `1.0.0` | - | [RegionDilation](./RegionDilation.md) |
| `OperatorType.RegionErosion` | Region Erosion | 2 | 3 | 4 | 63 (C) | `1.0.0` | - | [RegionErosion](./RegionErosion.md) |
| `OperatorType.RegionOpening` | Region Opening | 2 | 3 | 3 | 63 (C) | `1.0.0` | - | [RegionOpening](./RegionOpening.md) |
| `OperatorType.RegionSkeleton` | Region Skeleton | 2 | 5 | 2 | 63 (C) | `1.0.0` | - | [RegionSkeleton](./RegionSkeleton.md) |

### Region (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.RegionComplement` | Region Complement | 4 | 3 | 0 | 58 (C) | `1.0.0` | - | [RegionComplement](./RegionComplement.md) |
| `OperatorType.RegionDifference` | Region Difference | 2 | 3 | 0 | 61 (C) | `1.0.0` | - | [RegionDifference](./RegionDifference.md) |
| `OperatorType.RegionIntersection` | Region Intersection | 2 | 3 | 0 | 61 (C) | `1.0.0` | - | [RegionIntersection](./RegionIntersection.md) |
| `OperatorType.RegionUnion` | Region Union | 2 | 3 | 0 | 61 (C) | `1.0.0` | - | [RegionUnion](./RegionUnion.md) |

### Texture (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.GlcmTexture` | GLCM Texture Features | 1 | 6 | 9 | 90 (A) | `1.0.0` | - | [GlcmTexture](./GlcmTexture.md) |
| `OperatorType.LawsTextureFilter` | Laws Texture Filter | 1 | 3 | 5 | 90 (A) | `1.0.0` | - | [LawsTextureFilter](./LawsTextureFilter.md) |

### 匹配定位 (8)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.AkazeFeatureMatch` | AKAZE特征匹配 | 2 | 5 | 5 | 63 (C) | `1.0.0` | - | [AkazeFeatureMatch](./AkazeFeatureMatch.md) |
| `OperatorType.GradientShapeMatch` | 梯度形状匹配 | 2 | 5 | 6 | 73 (B) | `1.0.0` | - | [GradientShapeMatch](./GradientShapeMatch.md) |
| `OperatorType.LocalDeformableMatching` | Local Deformable Matching | 2 | 6 | 15 | 90 (A) | `1.0.4` | - | [LocalDeformableMatching](./LocalDeformableMatching.md) |
| `OperatorType.OrbFeatureMatch` | ORB特征匹配 | 2 | 5 | 7 | 63 (C) | `1.0.0` | - | [OrbFeatureMatch](./OrbFeatureMatch.md) |
| `OperatorType.PlanarMatching` | Planar Matching | 2 | 13 | 19 | 90 (A) | `1.1.1` | - | [PlanarMatching](./PlanarMatching.md) |
| `OperatorType.PyramidShapeMatch` | 金字塔形状匹配 | 2 | 5 | 15 | 73 (B) | `1.0.0` | - | [PyramidShapeMatch](./PyramidShapeMatch.md) |
| `OperatorType.ShapeMatching` | 旋转尺度模板匹配 | 2 | 2 | 10 | 90 (A) | `1.1.2` | - | [ShapeMatching](./ShapeMatching.md) |
| `OperatorType.TemplateMatching` | 模板匹配 | 3 | 6 | 9 | 86 (A) | `1.1.1` | - | [TemplateMatching](./TemplateMatching.md) |

### 变量 (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.CycleCounter` | 循环计数器 | 0 | 5 | 2 | 56 (C) | `1.0.0` | - | [CycleCounter](./CycleCounter.md) |
| `OperatorType.VariableIncrement` | 变量递增 | 0 | 5 | 5 | 63 (C) | `1.0.0` | - | [VariableIncrement](./VariableIncrement.md) |
| `OperatorType.VariableRead` | 变量读取 | 0 | 3 | 3 | 63 (C) | `1.0.0` | - | [VariableRead](./VariableRead.md) |
| `OperatorType.VariableWrite` | 变量写入 | 1 | 3 | 4 | 63 (C) | `1.0.0` | - | [VariableWrite](./VariableWrite.md) |

### 图像处理 (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.AffineTransform` | 仿射变换 | 1 | 2 | 9 | 90 (A) | `1.0.0` | - | [AffineTransform](./AffineTransform.md) |
| `OperatorType.CopyMakeBorder` | 边界填充 | 1 | 1 | 6 | 84 (B) | `1.0.0` | - | [CopyMakeBorder](./CopyMakeBorder.md) |
| `OperatorType.ImageStitching` | 图像拼接 | 2 | 2 | 3 | 84 (B) | `1.0.0` | - | [ImageStitching](./ImageStitching.md) |
| `OperatorType.PolarUnwrap` | 极坐标展开 | 2 | 1 | 8 | 90 (A) | `1.0.0` | - | [PolarUnwrap](./PolarUnwrap.md) |

### 定位 (7)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.BlobLabeling` | 连通域标注 | 2 | 3 | 3 | 90 (A) | `1.0.0` | - | [BlobLabeling](./BlobLabeling.md) |
| `OperatorType.CornerDetection` | 角点检测 | 1 | 3 | 5 | 84 (B) | `1.0.0` | - | [CornerDetection](./CornerDetection.md) |
| `OperatorType.EdgeIntersection` | 边线交点 | 2 | 3 | 0 | 79 (B) | `1.0.0` | - | [EdgeIntersection](./EdgeIntersection.md) |
| `OperatorType.ParallelLineFind` | 平行线查找 | 1 | 6 | 4 | 84 (B) | `1.0.0` | - | [ParallelLineFind](./ParallelLineFind.md) |
| `OperatorType.PositionCorrection` | 位置修正 | 4 | 5 | 3 | 84 (B) | `1.0.1` | - | [PositionCorrection](./PositionCorrection.md) |
| `OperatorType.QuadrilateralFind` | 四边形查找 | 1 | 5 | 4 | 84 (B) | `1.0.0` | - | [QuadrilateralFind](./QuadrilateralFind.md) |
| `OperatorType.RectangleDetection` | 矩形检测 | 1 | 7 | 4 | 84 (B) | `1.0.0` | - | [RectangleDetection](./RectangleDetection.md) |

### 拆分组合 (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.ImageCompose` | 图像组合 | 4 | 1 | 3 | 84 (B) | `1.0.0` | - | [ImageCompose](./ImageCompose.md) |
| `OperatorType.ImageTiling` | 图像切片 | 1 | 3 | 4 | 84 (B) | `1.0.0` | - | [ImageTiling](./ImageTiling.md) |

### 数据处理 (10)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.Aggregator` | 数据聚合 | 3 | 5 | 1 | 56 (C) | `1.0.0` | - | [Aggregator](./Aggregator.md) |
| `OperatorType.ArrayIndexer` | 数组索引器 | 1 | 3 | 2 | 69 (C) | `1.0.0` | - | [ArrayIndexer](./ArrayIndexer.md) |
| `OperatorType.BoxFilter` | 候选框过滤 (Bounding Box) | 2 | 3 | 9 | 80 (B) | `1.0.0` | - | [BoxFilter](./BoxFilter.md) |
| `OperatorType.BoxNms` | 候选框抑制 | 3 | 7 | 4 | 80 (B) | `1.0.0` | - | [BoxNms](./BoxNms.md) |
| `OperatorType.DatabaseWrite` | 数据库写入 | 1 | 2 | 3 | 90 (A) | `1.0.0` | - | [DatabaseWrite](./DatabaseWrite.md) |
| `OperatorType.JsonExtractor` | JSON 提取器 | 1 | 2 | 1 | 73 (B) | `1.0.0` | - | [JsonExtractor](./JsonExtractor.md) |
| `OperatorType.MathOperation` | 数值计算 | 2 | 2 | 1 | 73 (B) | `1.0.0` | - | [MathOperation](./MathOperation.md) |
| `OperatorType.PointAlignment` | 点位对齐 | 2 | 3 | 2 | 84 (B) | `1.0.1` | - | [PointAlignment](./PointAlignment.md) |
| `OperatorType.PointCorrection` | 点位修正 | 4 | 4 | 3 | 84 (B) | `1.0.1` | - | [PointCorrection](./PointCorrection.md) |
| `OperatorType.UnitConvert` | 单位换算 | 2 | 2 | 4 | 86 (A) | `1.0.0` | - | [UnitConvert](./UnitConvert.md) |

### 标定 (12)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.CalibrationLoader` | 标定加载 | 0 | 4 | 2 | 90 (A) | `1.0.0` | - | [CalibrationLoader](./CalibrationLoader.md) |
| `OperatorType.CameraCalibration` | Camera Calibration | 1 | 2 | 7 | 90 (A) | `1.0.0` | - | [CameraCalibration](./CameraCalibration.md) |
| `OperatorType.CoordinateTransform` | 坐标转换 | 3 | 3 | 4 | 90 (A) | `1.0.0` | - | [CoordinateTransform](./CoordinateTransform.md) |
| `OperatorType.FisheyeCalibration` | Fisheye Calibration | 1 | 4 | 9 | 90 (A) | `1.0.0` | - | [FisheyeCalibration](./FisheyeCalibration.md) |
| `OperatorType.FisheyeUndistort` | Fisheye Undistort | 2 | 2 | 5 | 90 (A) | `1.0.0` | - | [FisheyeUndistort](./FisheyeUndistort.md) |
| `OperatorType.HandEyeCalibration` | 手眼标定 | 2 | 8 | 4 | 90 (A) | `1.0.0` | OpenCV Hand-Eye Calibration | [HandEyeCalibration](./HandEyeCalibration.md) |
| `OperatorType.HandEyeCalibrationValidator` | 手眼标定验证 | 3 | 7 | 1 | 73 (B) | `1.0.0` | Hand-Eye Consistency Validation | [HandEyeCalibrationValidator](./HandEyeCalibrationValidator.md) |
| `OperatorType.NPointCalibration` | N点标定 | 1 | 3 | 3 | 90 (A) | `1.0.0` | - | [NPointCalibration](./NPointCalibration.md) |
| `OperatorType.PixelToWorldTransform` | Pixel To World Transform | 3 | 3 | 9 | 90 (A) | `1.0.0` | - | [PixelToWorldTransform](./PixelToWorldTransform.md) |
| `OperatorType.StereoCalibration` | Stereo Calibration | 2 | 6 | 11 | 90 (A) | `1.0.0` | - | [StereoCalibration](./StereoCalibration.md) |
| `OperatorType.TranslationRotationCalibration` | 平移旋转标定 | 1 | 3 | 3 | 90 (A) | `1.0.0` | - | [TranslationRotationCalibration](./TranslationRotationCalibration.md) |
| `OperatorType.Undistort` | Undistort | 2 | 1 | 1 | 85 (A) | `1.0.0` | - | [Undistort](./Undistort.md) |

### 检测 (20)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.AngleMeasurement` | 角度测量 | 1 | 2 | 7 | 84 (B) | `1.0.0` | - | [AngleMeasurement](./AngleMeasurement.md) |
| `OperatorType.ArcCaliper` | Arc Caliper | 7 | 2 | 0 | 58 (C) | `1.0.0` | - | [ArcCaliper](./ArcCaliper.md) |
| `OperatorType.CaliperTool` | 卡尺工具 | 2 | 7 | 9 | 84 (B) | `1.0.0` | - | [CaliperTool](./CaliperTool.md) |
| `OperatorType.CircleMeasurement` | 圆测量 | 1 | 7 | 7 | 90 (A) | `1.0.0` | - | [CircleMeasurement](./CircleMeasurement.md) |
| `OperatorType.ContourExtrema` | Contour Extrema | 3 | 6 | 0 | 58 (C) | `1.0.0` | - | [ContourExtrema](./ContourExtrema.md) |
| `OperatorType.ContourMeasurement` | 轮廓测量 | 1 | 4 | 4 | 84 (B) | `1.0.0` | - | [ContourMeasurement](./ContourMeasurement.md) |
| `OperatorType.GapMeasurement` | 间隙测量 | 2 | 6 | 4 | 84 (B) | `1.0.0` | - | [GapMeasurement](./GapMeasurement.md) |
| `OperatorType.GeoMeasurement` | 几何测量 | 2 | 5 | 2 | 84 (B) | `1.0.0` | - | [GeoMeasurement](./GeoMeasurement.md) |
| `OperatorType.GeometricFitting` | Geometric Fitting | 1 | 2 | 8 | 90 (A) | `1.0.0` | - | [GeometricFitting](./GeometricFitting.md) |
| `OperatorType.GeometricTolerance` | 几何公差 | 1 | 5 | 9 | 84 (B) | `1.0.0` | - | [GeometricTolerance](./GeometricTolerance.md) |
| `OperatorType.HistogramAnalysis` | 直方图分析 | 1 | 7 | 6 | 84 (B) | `1.0.0` | - | [HistogramAnalysis](./HistogramAnalysis.md) |
| `OperatorType.LineLineDistance` | 线线距离 | 2 | 5 | 1 | 86 (A) | `1.0.0` | - | [LineLineDistance](./LineLineDistance.md) |
| `OperatorType.LineMeasurement` | 直线测量 | 1 | 5 | 4 | 84 (B) | `1.0.0` | - | [LineMeasurement](./LineMeasurement.md) |
| `OperatorType.Measurement` | 测量 | 3 | 2 | 5 | 84 (B) | `1.0.0` | - | [Measurement](./Measurement.md) |
| `OperatorType.MinEnclosingGeometry` | Min Enclosing Geometry | 1 | 2 | 10 | 90 (A) | `1.0.0` | - | [MinEnclosingGeometry](./MinEnclosingGeometry.md) |
| `OperatorType.PhaseClosure` | Phase Closure | 4 | 4 | 0 | 58 (C) | `1.0.0` | - | [PhaseClosure](./PhaseClosure.md) |
| `OperatorType.PixelStatistics` | 像素统计 | 2 | 6 | 5 | 84 (B) | `1.0.0` | - | [PixelStatistics](./PixelStatistics.md) |
| `OperatorType.PointLineDistance` | 点线距离 | 2 | 2 | 0 | 81 (B) | `1.0.0` | - | [PointLineDistance](./PointLineDistance.md) |
| `OperatorType.SharpnessEvaluation` | 清晰度评估 | 1 | 3 | 6 | 84 (B) | `1.0.0` | - | [SharpnessEvaluation](./SharpnessEvaluation.md) |
| `OperatorType.WidthMeasurement` | 宽度测量 | 3 | 4 | 4 | 86 (A) | `1.0.0` | - | [WidthMeasurement](./WidthMeasurement.md) |

### 流程控制 (6)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.Comparator` | 数值比较 | 2 | 2 | 5 | 51 (D) | `1.0.0` | - | [Comparator](./Comparator.md) |
| `OperatorType.ConditionalBranch` | 条件分支 | 1 | 2 | 3 | 80 (B) | `1.0.0` | - | [ConditionalBranch](./ConditionalBranch.md) |
| `OperatorType.Delay` | 延时 | 1 | 2 | 1 | 51 (D) | `1.0.0` | - | [Delay](./Delay.md) |
| `OperatorType.ForEach` | ForEach 循环 | 1 | 1 | 4 | 73 (B) | `1.0.0` | - | [ForEach](./ForEach.md) |
| `OperatorType.ResultJudgment` | 结果判定 | 2 | 3 | 8 | 56 (C) | `1.0.0` | - | [ResultJudgment](./ResultJudgment.md) |
| `OperatorType.TryCatch` | 异常捕获 | 1 | 4 | 3 | 65 (C) | `1.0.0` | - | [TryCatch](./TryCatch.md) |

### 特征提取 (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.BlobAnalysis` | Blob分析 | 2 | 4 | 17 | 90 (A) | `1.1.0` | - | [BlobAnalysis](./BlobAnalysis.md) |
| `OperatorType.ContourDetection` | 轮廓检测 | 1 | 3 | 8 | 84 (B) | `1.0.0` | - | [ContourDetection](./ContourDetection.md) |
| `OperatorType.EdgeDetection` | Edge Detection | 1 | 2 | 8 | 66 (C) | `1.0.0` | - | [EdgeDetection](./EdgeDetection.md) |
| `OperatorType.SubpixelEdgeDetection` | Subpixel Edge Detection | 1 | 2 | 5 | 84 (B) | `1.0.0` | - | [SubpixelEdgeDetection](./SubpixelEdgeDetection.md) |

### 识别 (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.CodeRecognition` | 条码识别 | 1 | 4 | 2 | 90 (A) | `1.0.0` | - | [CodeRecognition](./CodeRecognition.md) |
| `OperatorType.OcrRecognition` | OCR 识别 | 1 | 2 | 0 | 90 (A) | `1.0.0` | - | [OcrRecognition](./OcrRecognition.md) |

### 辅助 (3)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.Comment` | 注释 | 1 | 2 | 1 | 51 (D) | `1.0.0` | - | [Comment](./Comment.md) |
| `OperatorType.RoiManager` | ROI管理器 | 1 | 2 | 10 | 90 (A) | `1.0.0` | - | [RoiManager](./RoiManager.md) |
| `OperatorType.RoiTransform` | ROI跟踪 | 2 | 1 | 1 | 86 (A) | `1.0.0` | - | [RoiTransform](./RoiTransform.md) |

### 输出 (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.ImageSave` | 图像保存 | 1 | 2 | 3 | 73 (B) | `1.0.0` | - | [ImageSave](./ImageSave.md) |
| `OperatorType.ResultOutput` | 结果输出 | 4 | 6 | 2 | 88 (A) | `1.0.1` | - | [ResultOutput](./ResultOutput.md) |

### 通信 (8)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.HttpRequest` | HTTP 请求 | 2 | 3 | 6 | 73 (B) | `1.0.0` | - | [HttpRequest](./HttpRequest.md) |
| `OperatorType.MitsubishiMcCommunication` | 三菱MC通信 | 1 | 2 | 12 | 70 (B) | `1.0.0` | - | [MitsubishiMcCommunication](./MitsubishiMcCommunication.md) |
| `OperatorType.ModbusCommunication` | Modbus通信 | 1 | 2 | 8 | 88 (A) | `1.0.0` | - | [ModbusCommunication](./ModbusCommunication.md) |
| `OperatorType.MqttPublish` | MQTT 发布 | 2 | 1 | 6 | 90 (A) | `1.0.0` | - | [MqttPublish](./MqttPublish.md) |
| `OperatorType.OmronFinsCommunication` | 欧姆龙FINS通信 | 1 | 2 | 12 | 70 (B) | `1.0.0` | - | [OmronFinsCommunication](./OmronFinsCommunication.md) |
| `OperatorType.SerialCommunication` | 串口通信 | 1 | 1 | 8 | 73 (B) | `1.0.0` | - | [SerialCommunication](./SerialCommunication.md) |
| `OperatorType.SiemensS7Communication` | 西门子S7通信 | 1 | 2 | 14 | 70 (B) | `1.0.0` | - | [SiemensS7Communication](./SiemensS7Communication.md) |
| `OperatorType.TcpCommunication` | TCP通信 | 1 | 2 | 6 | 88 (A) | `1.0.0` | - | [TcpCommunication](./TcpCommunication.md) |

### 通用 (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.LogicGate` | 逻辑门 | 2 | 1 | 1 | 63 (C) | `1.0.0` | - | [LogicGate](./LogicGate.md) |
| `OperatorType.Statistics` | Statistics | 1 | 7 | 5 | 80 (B) | `1.0.0` | - | [Statistics](./Statistics.md) |
| `OperatorType.StringFormat` | 字符串格式化 | 2 | 1 | 1 | 66 (C) | `1.0.0` | - | [StringFormat](./StringFormat.md) |
| `OperatorType.TypeConvert` | Type Convert | 1 | 6 | 2 | 73 (B) | `1.0.0` | - | [TypeConvert](./TypeConvert.md) |

### 逻辑工具 (5)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.PointSetTool` | 点集工具 | 2 | 4 | 6 | 80 (B) | `1.0.0` | - | [PointSetTool](./PointSetTool.md) |
| `OperatorType.ScriptOperator` | 脚本算子 | 4 | 2 | 3 | 90 (A) | `1.0.0` | - | [ScriptOperator](./ScriptOperator.md) |
| `OperatorType.TextSave` | Text Save | 2 | 2 | 5 | 90 (A) | `1.0.0` | - | [TextSave](./TextSave.md) |
| `OperatorType.TimerStatistics` | 计时统计 | 1 | 4 | 2 | 74 (B) | `1.0.0` | - | [TimerStatistics](./TimerStatistics.md) |
| `OperatorType.TriggerModule` | 触发模块 | 1 | 3 | 3 | 74 (B) | `1.0.0` | - | [TriggerModule](./TriggerModule.md) |

### 采集 (1)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.ImageAcquisition` | 图像采集 | 2 | 1 | 6 | 68 (C) | `1.0.0` | - | [ImageAcquisition](./ImageAcquisition.md) |

### 预处理 (23)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.AdaptiveThreshold` | Adaptive Threshold | 1 | 1 | 5 | 84 (B) | `1.0.0` | - | [AdaptiveThreshold](./AdaptiveThreshold.md) |
| `OperatorType.BilateralFilter` | 双边滤波 | 1 | 1 | 3 | 84 (B) | `1.0.0` | - | [BilateralFilter](./BilateralFilter.md) |
| `OperatorType.ClaheEnhancement` | CLAHE增强 | 1 | 1 | 5 | 84 (B) | `1.0.0` | - | [ClaheEnhancement](./ClaheEnhancement.md) |
| `OperatorType.ColorConversion` | 颜色空间转换 | 1 | 1 | 2 | 84 (B) | `1.0.0` | - | [ColorConversion](./ColorConversion.md) |
| `OperatorType.Filtering` | Gaussian Blur | 1 | 1 | 4 | 66 (C) | `1.0.0` | Gaussian Blur (OpenCV) | [Filtering](./Filtering.md) |
| `OperatorType.FrameAveraging` | 帧平均 | 1 | 2 | 2 | 84 (B) | `1.0.0` | - | [FrameAveraging](./FrameAveraging.md) |
| `OperatorType.HistogramEqualization` | 直方图均衡化 | 1 | 1 | 4 | 84 (B) | `1.0.0` | - | [HistogramEqualization](./HistogramEqualization.md) |
| `OperatorType.ImageAdd` | 图像加法 | 2 | 1 | 6 | 90 (A) | `1.0.0` | - | [ImageAdd](./ImageAdd.md) |
| `OperatorType.ImageBlend` | 图像融合 | 2 | 1 | 3 | 66 (C) | `1.0.0` | - | [ImageBlend](./ImageBlend.md) |
| `OperatorType.ImageCrop` | 图像裁剪 | 1 | 1 | 4 | 84 (B) | `1.0.0` | - | [ImageCrop](./ImageCrop.md) |
| `OperatorType.ImageDiff` | 图像对比 | 2 | 2 | 0 | 61 (C) | `1.0.0` | - | [ImageDiff](./ImageDiff.md) |
| `OperatorType.ImageNormalize` | 图像归一化 | 1 | 1 | 3 | 84 (B) | `1.0.0` | - | [ImageNormalize](./ImageNormalize.md) |
| `OperatorType.ImageResize` | 图像缩放 | 1 | 1 | 5 | 84 (B) | `1.0.0` | - | [ImageResize](./ImageResize.md) |
| `OperatorType.ImageRotate` | 图像旋转 | 1 | 1 | 5 | 84 (B) | `1.0.0` | - | [ImageRotate](./ImageRotate.md) |
| `OperatorType.ImageSubtract` | Image Subtract | 2 | 4 | 1 | 79 (B) | `1.0.0` | - | [ImageSubtract](./ImageSubtract.md) |
| `OperatorType.LaplacianSharpen` | 拉普拉斯锐化 | 1 | 1 | 3 | 66 (C) | `1.0.0` | - | [LaplacianSharpen](./LaplacianSharpen.md) |
| `OperatorType.MeanFilter` | 均值滤波 | 1 | 1 | 2 | 84 (B) | `1.0.0` | - | [MeanFilter](./MeanFilter.md) |
| `OperatorType.MedianBlur` | 中值滤波 | 1 | 1 | 1 | 84 (B) | `1.0.0` | - | [MedianBlur](./MedianBlur.md) |
| `OperatorType.MorphologicalOperation` | Morphological Operation | 1 | 1 | 7 | 66 (C) | `1.0.0` | - | [MorphologicalOperation](./MorphologicalOperation.md) |
| `OperatorType.Morphology` | Morphology (Legacy) | 1 | 1 | 6 | 84 (B) | `1.0.0` | - | [Morphology](./Morphology.md) |
| `OperatorType.PerspectiveTransform` | 透视变换 | 3 | 1 | 20 | 90 (A) | `1.0.0` | - | [PerspectiveTransform](./PerspectiveTransform.md) |
| `OperatorType.ShadingCorrection` | 光照校正 | 2 | 1 | 2 | 84 (B) | `1.0.0` | - | [ShadingCorrection](./ShadingCorrection.md) |
| `OperatorType.Thresholding` | Threshold | 1 | 1 | 4 | 66 (C) | `1.0.0` | - | [Thresholding](./Thresholding.md) |

### 颜色处理 (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.ColorDetection` | 颜色检测 | 1 | 4 | 9 | 66 (C) | `1.0.0` | - | [ColorDetection](./ColorDetection.md) |
| `OperatorType.ColorMeasurement` | 颜色测量 | 2 | 8 | 9 | 84 (B) | `1.0.2` | - | [ColorMeasurement](./ColorMeasurement.md) |
