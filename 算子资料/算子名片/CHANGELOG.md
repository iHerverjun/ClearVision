# 算子版本变更记录 / Operator Version Changelog

> 生成时间 / Generated At: `2026-04-07 18:58:05 +08:00`
> 算子总数 / Total Operators: **155**

## 当前版本快照 / Current Snapshot
| 枚举 (Enum) | 显示名 (DisplayName) | 分类 (Category) | 版本 (Version) |
|------|------|------|------|
| `OperatorType.EuclideanClusterExtraction` | 欧氏聚类分割 | 3D | `1.0.0` |
| `OperatorType.PPFEstimation` | PPF点对特征 | 3D | `1.0.0` |
| `OperatorType.PPFMatch` | PPF表面匹配 | 3D | `1.0.4` |
| `OperatorType.RansacPlaneSegmentation` | RANSAC平面分割 | 3D | `1.0.0` |
| `OperatorType.StatisticalOutlierRemoval` | 统计滤波 | 3D | `1.0.0` |
| `OperatorType.VoxelDownsample` | 体素下采样 | 3D | `1.0.0` |
| `OperatorType.DetectionSequenceJudge` | Detection Sequence Judge | AI Inspection | `1.0.0` |
| `OperatorType.AnomalyDetection` | 异常检测 | AI检测 | `1.0.0` |
| `OperatorType.DeepLearning` | 深度学习 | AI检测 | `1.0.0` |
| `OperatorType.DualModalVoting` | 双模态投票 | AI检测 | `1.0.0` |
| `OperatorType.EdgePairDefect` | 边缘对缺陷 | AI检测 | `1.0.0` |
| `OperatorType.SemanticSegmentation` | 语义分割 | AI检测 | `1.0.0` |
| `OperatorType.SurfaceDefectDetection` | 表面缺陷检测 | AI检测 | `2.0.0` |
| `OperatorType.DistanceTransform` | Distance Transform | Analysis | `1.0.0` |
| `OperatorType.FFT1D` | FFT 1D | Frequency | `1.0.0` |
| `OperatorType.FrequencyFilter` | Frequency Filter | Frequency | `1.0.0` |
| `OperatorType.InverseFFT1D` | Inverse FFT 1D | Frequency | `1.0.0` |
| `OperatorType.RegionClosing` | Region Closing | Morphology | `1.0.0` |
| `OperatorType.RegionDilation` | Region Dilation | Morphology | `1.0.0` |
| `OperatorType.RegionErosion` | Region Erosion | Morphology | `1.0.0` |
| `OperatorType.RegionOpening` | Region Opening | Morphology | `1.0.0` |
| `OperatorType.RegionSkeleton` | Region Skeleton | Morphology | `1.0.0` |
| `OperatorType.RegionComplement` | Region Complement | Region | `1.0.0` |
| `OperatorType.RegionDifference` | Region Difference | Region | `1.0.0` |
| `OperatorType.RegionIntersection` | Region Intersection | Region | `1.0.0` |
| `OperatorType.RegionUnion` | Region Union | Region | `1.0.0` |
| `OperatorType.GlcmTexture` | GLCM Texture Features | Texture | `1.0.0` |
| `OperatorType.LawsTextureFilter` | Laws Texture Filter | Texture | `1.0.0` |
| `OperatorType.AkazeFeatureMatch` | AKAZE特征匹配 | 匹配定位 | `1.0.0` |
| `OperatorType.GradientShapeMatch` | 梯度形状匹配 | 匹配定位 | `1.0.0` |
| `OperatorType.LocalDeformableMatching` | Local Deformable Matching | 匹配定位 | `1.0.4` |
| `OperatorType.OrbFeatureMatch` | ORB特征匹配 | 匹配定位 | `1.0.0` |
| `OperatorType.PlanarMatching` | Planar Matching | 匹配定位 | `1.1.1` |
| `OperatorType.PyramidShapeMatch` | 金字塔形状匹配 | 匹配定位 | `1.0.0` |
| `OperatorType.ShapeMatching` | 旋转尺度模板匹配 | 匹配定位 | `1.1.2` |
| `OperatorType.TemplateMatching` | 模板匹配 | 匹配定位 | `1.1.1` |
| `OperatorType.CycleCounter` | 循环计数器 | 变量 | `1.0.0` |
| `OperatorType.VariableIncrement` | 变量递增 | 变量 | `1.0.0` |
| `OperatorType.VariableRead` | 变量读取 | 变量 | `1.0.0` |
| `OperatorType.VariableWrite` | 变量写入 | 变量 | `1.0.0` |
| `OperatorType.AffineTransform` | 仿射变换 | 图像处理 | `1.0.0` |
| `OperatorType.CopyMakeBorder` | 边界填充 | 图像处理 | `1.0.0` |
| `OperatorType.ImageStitching` | 图像拼接 | 图像处理 | `1.0.0` |
| `OperatorType.PolarUnwrap` | 极坐标展开 | 图像处理 | `1.0.0` |
| `OperatorType.BlobLabeling` | 连通域标注 | 定位 | `1.0.0` |
| `OperatorType.CornerDetection` | 角点检测 | 定位 | `1.0.0` |
| `OperatorType.EdgeIntersection` | 边线交点 | 定位 | `1.0.0` |
| `OperatorType.ParallelLineFind` | 平行线查找 | 定位 | `1.0.0` |
| `OperatorType.PositionCorrection` | 位置修正 | 定位 | `1.0.1` |
| `OperatorType.QuadrilateralFind` | 四边形查找 | 定位 | `1.0.0` |
| `OperatorType.RectangleDetection` | 矩形检测 | 定位 | `1.0.0` |
| `OperatorType.ImageCompose` | 图像组合 | 拆分组合 | `1.0.0` |
| `OperatorType.ImageTiling` | 图像切片 | 拆分组合 | `1.0.0` |
| `OperatorType.Aggregator` | 数据聚合 | 数据处理 | `1.0.0` |
| `OperatorType.ArrayIndexer` | 数组索引器 | 数据处理 | `1.0.0` |
| `OperatorType.BoxFilter` | 候选框过滤 (Bounding Box) | 数据处理 | `1.0.0` |
| `OperatorType.BoxNms` | 候选框抑制 | 数据处理 | `1.0.0` |
| `OperatorType.DatabaseWrite` | 数据库写入 | 数据处理 | `1.0.0` |
| `OperatorType.JsonExtractor` | JSON 提取器 | 数据处理 | `1.0.0` |
| `OperatorType.MathOperation` | 数值计算 | 数据处理 | `1.0.0` |
| `OperatorType.PointAlignment` | 点位对齐 | 数据处理 | `1.0.1` |
| `OperatorType.PointCorrection` | 点位修正 | 数据处理 | `1.0.1` |
| `OperatorType.UnitConvert` | 单位换算 | 数据处理 | `1.0.0` |
| `OperatorType.CalibrationLoader` | 标定加载 | 标定 | `1.0.0` |
| `OperatorType.CameraCalibration` | Camera Calibration | 标定 | `1.0.0` |
| `OperatorType.CoordinateTransform` | 坐标转换 | 标定 | `1.0.0` |
| `OperatorType.FisheyeCalibration` | Fisheye Calibration | 标定 | `1.0.0` |
| `OperatorType.FisheyeUndistort` | Fisheye Undistort | 标定 | `1.0.0` |
| `OperatorType.HandEyeCalibration` | 手眼标定 | 标定 | `1.0.0` |
| `OperatorType.HandEyeCalibrationValidator` | 手眼标定验证 | 标定 | `1.0.0` |
| `OperatorType.NPointCalibration` | N点标定 | 标定 | `1.0.0` |
| `OperatorType.PixelToWorldTransform` | Pixel To World Transform | 标定 | `1.0.0` |
| `OperatorType.StereoCalibration` | Stereo Calibration | 标定 | `1.0.0` |
| `OperatorType.TranslationRotationCalibration` | 平移旋转标定 | 标定 | `1.0.0` |
| `OperatorType.Undistort` | Undistort | 标定 | `1.0.0` |
| `OperatorType.AngleMeasurement` | 角度测量 | 检测 | `1.0.0` |
| `OperatorType.ArcCaliper` | Arc Caliper | 检测 | `1.0.0` |
| `OperatorType.CaliperTool` | 卡尺工具 | 检测 | `1.0.0` |
| `OperatorType.CircleMeasurement` | 圆测量 | 检测 | `1.0.0` |
| `OperatorType.ContourExtrema` | Contour Extrema | 检测 | `1.0.0` |
| `OperatorType.ContourMeasurement` | 轮廓测量 | 检测 | `1.0.0` |
| `OperatorType.GapMeasurement` | 间隙测量 | 检测 | `1.0.0` |
| `OperatorType.GeoMeasurement` | 几何测量 | 检测 | `1.0.0` |
| `OperatorType.GeometricFitting` | Geometric Fitting | 检测 | `1.0.0` |
| `OperatorType.GeometricTolerance` | 几何公差 | 检测 | `1.0.0` |
| `OperatorType.HistogramAnalysis` | 直方图分析 | 检测 | `1.0.0` |
| `OperatorType.LineLineDistance` | 线线距离 | 检测 | `1.0.0` |
| `OperatorType.LineMeasurement` | 直线测量 | 检测 | `1.0.0` |
| `OperatorType.Measurement` | 测量 | 检测 | `1.0.0` |
| `OperatorType.MinEnclosingGeometry` | Min Enclosing Geometry | 检测 | `1.0.0` |
| `OperatorType.PhaseClosure` | Phase Closure | 检测 | `1.0.0` |
| `OperatorType.PixelStatistics` | 像素统计 | 检测 | `1.0.0` |
| `OperatorType.PointLineDistance` | 点线距离 | 检测 | `1.0.0` |
| `OperatorType.SharpnessEvaluation` | 清晰度评估 | 检测 | `1.0.0` |
| `OperatorType.WidthMeasurement` | 宽度测量 | 检测 | `1.0.0` |
| `OperatorType.Comparator` | 数值比较 | 流程控制 | `1.0.0` |
| `OperatorType.ConditionalBranch` | 条件分支 | 流程控制 | `1.0.0` |
| `OperatorType.Delay` | 延时 | 流程控制 | `1.0.0` |
| `OperatorType.ForEach` | ForEach 循环 | 流程控制 | `1.0.0` |
| `OperatorType.ResultJudgment` | 结果判定 | 流程控制 | `1.0.0` |
| `OperatorType.TryCatch` | 异常捕获 | 流程控制 | `1.0.0` |
| `OperatorType.BlobAnalysis` | Blob分析 | 特征提取 | `1.1.0` |
| `OperatorType.ContourDetection` | 轮廓检测 | 特征提取 | `1.0.0` |
| `OperatorType.EdgeDetection` | Edge Detection | 特征提取 | `1.0.0` |
| `OperatorType.SubpixelEdgeDetection` | Subpixel Edge Detection | 特征提取 | `1.0.0` |
| `OperatorType.CodeRecognition` | 条码识别 | 识别 | `1.0.0` |
| `OperatorType.OcrRecognition` | OCR 识别 | 识别 | `1.0.0` |
| `OperatorType.Comment` | 注释 | 辅助 | `1.0.0` |
| `OperatorType.RoiManager` | ROI管理器 | 辅助 | `1.0.0` |
| `OperatorType.RoiTransform` | ROI跟踪 | 辅助 | `1.0.0` |
| `OperatorType.ImageSave` | 图像保存 | 输出 | `1.0.0` |
| `OperatorType.ResultOutput` | 结果输出 | 输出 | `1.0.1` |
| `OperatorType.HttpRequest` | HTTP 请求 | 通信 | `1.0.0` |
| `OperatorType.MitsubishiMcCommunication` | 三菱MC通信 | 通信 | `1.0.0` |
| `OperatorType.ModbusCommunication` | Modbus通信 | 通信 | `1.0.0` |
| `OperatorType.MqttPublish` | MQTT 发布 | 通信 | `1.0.0` |
| `OperatorType.OmronFinsCommunication` | 欧姆龙FINS通信 | 通信 | `1.0.0` |
| `OperatorType.SerialCommunication` | 串口通信 | 通信 | `1.0.0` |
| `OperatorType.SiemensS7Communication` | 西门子S7通信 | 通信 | `1.0.0` |
| `OperatorType.TcpCommunication` | TCP通信 | 通信 | `1.0.0` |
| `OperatorType.LogicGate` | 逻辑门 | 通用 | `1.0.0` |
| `OperatorType.Statistics` | Statistics | 通用 | `1.0.0` |
| `OperatorType.StringFormat` | 字符串格式化 | 通用 | `1.0.0` |
| `OperatorType.TypeConvert` | Type Convert | 通用 | `1.0.0` |
| `OperatorType.PointSetTool` | 点集工具 | 逻辑工具 | `1.0.0` |
| `OperatorType.ScriptOperator` | 脚本算子 | 逻辑工具 | `1.0.0` |
| `OperatorType.TextSave` | Text Save | 逻辑工具 | `1.0.0` |
| `OperatorType.TimerStatistics` | 计时统计 | 逻辑工具 | `1.0.0` |
| `OperatorType.TriggerModule` | 触发模块 | 逻辑工具 | `1.0.0` |
| `OperatorType.ImageAcquisition` | 图像采集 | 采集 | `1.0.0` |
| `OperatorType.AdaptiveThreshold` | Adaptive Threshold | 预处理 | `1.0.0` |
| `OperatorType.BilateralFilter` | 双边滤波 | 预处理 | `1.0.0` |
| `OperatorType.ClaheEnhancement` | CLAHE增强 | 预处理 | `1.0.0` |
| `OperatorType.ColorConversion` | 颜色空间转换 | 预处理 | `1.0.0` |
| `OperatorType.Filtering` | Gaussian Blur | 预处理 | `1.0.0` |
| `OperatorType.FrameAveraging` | 帧平均 | 预处理 | `1.0.0` |
| `OperatorType.HistogramEqualization` | 直方图均衡化 | 预处理 | `1.0.0` |
| `OperatorType.ImageAdd` | 图像加法 | 预处理 | `1.0.0` |
| `OperatorType.ImageBlend` | 图像融合 | 预处理 | `1.0.0` |
| `OperatorType.ImageCrop` | 图像裁剪 | 预处理 | `1.0.0` |
| `OperatorType.ImageDiff` | 图像对比 | 预处理 | `1.0.0` |
| `OperatorType.ImageNormalize` | 图像归一化 | 预处理 | `1.0.0` |
| `OperatorType.ImageResize` | 图像缩放 | 预处理 | `1.0.0` |
| `OperatorType.ImageRotate` | 图像旋转 | 预处理 | `1.0.0` |
| `OperatorType.ImageSubtract` | Image Subtract | 预处理 | `1.0.0` |
| `OperatorType.LaplacianSharpen` | 拉普拉斯锐化 | 预处理 | `1.0.0` |
| `OperatorType.MeanFilter` | 均值滤波 | 预处理 | `1.0.0` |
| `OperatorType.MedianBlur` | 中值滤波 | 预处理 | `1.0.0` |
| `OperatorType.MorphologicalOperation` | Morphological Operation | 预处理 | `1.0.0` |
| `OperatorType.Morphology` | Morphology (Legacy) | 预处理 | `1.0.0` |
| `OperatorType.PerspectiveTransform` | 透视变换 | 预处理 | `1.0.0` |
| `OperatorType.ShadingCorrection` | 光照校正 | 预处理 | `1.0.0` |
| `OperatorType.Thresholding` | Threshold | 预处理 | `1.0.0` |
| `OperatorType.ColorDetection` | 颜色检测 | 颜色处理 | `2.0.0` |
| `OperatorType.ColorMeasurement` | 颜色测量 | 颜色处理 | `1.0.2` |

## 历史变更 / Historical Changes

### OperatorType.AdaptiveThreshold / Adaptive Threshold
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `0B6B9A1EA1B3` |
| `1.0.0` | `2026-04-08T19:13:32.2020980+08:00` | `B975CE907035` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `B455F3119904` |

### OperatorType.AffineTransform / 仿射变换
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `4AD3551216EE` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `E63763CA1D29` |

### OperatorType.AnomalyDetection / 异常检测
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `FFA0A783ACCD` |
| `1.0.0` | `2026-04-10T17:42:49.0767787+08:00` | `AAEA9F498173` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `C16C20BC74A9` |

### OperatorType.BlobLabeling / 连通域标注
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `10BC8AC0A2DD` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `98361D064008` |

### OperatorType.BoxFilter / 候选框过滤 (Bounding Box)
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `D4ADD51F8EEF` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `809AD2B52541` |

### OperatorType.CalibrationLoader / 标定加载
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `4DE2162FEF55` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `78A181F5EBA5` |

### OperatorType.CameraCalibration / Camera Calibration
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `490769260740` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `F16C96AD05AD` |

### OperatorType.ClaheEnhancement / CLAHE增强
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `43109B43EBCF` |
| `1.0.0` | `2026-04-08T22:02:18.9641594+08:00` | `5F3BBC85B0F4` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `69E533C1A735` |

### OperatorType.CodeRecognition / 条码识别
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `4911A54BEC5B` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `E9EA23FB99BA` |

### OperatorType.ColorDetection / 颜色检测
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `2.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `45742E8E06C1` |
| `2.0.0` | `2026-04-10T17:42:49.0767787+08:00` | `7D2667DA625B` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `424C14C47E4B` |

### OperatorType.ConditionalBranch / 条件分支
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `29F6BE3DEEB2` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `E8EBC9095D13` |

### OperatorType.CopyMakeBorder / 边界填充
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `EA1319B3DDD0` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `D98F801C200D` |

### OperatorType.CornerDetection / 角点检测
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `8FC52A647500` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `A309BED01F1A` |

### OperatorType.CycleCounter / 循环计数器
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `8938799BB943` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `59FED1DF2CFC` |

### OperatorType.DatabaseWrite / 数据库写入
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `B78C50662070` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `DB7098D9E78E` |

### OperatorType.DeepLearning / 深度学习
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `0AE09DC9C6F5` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `4D9170172AFC` |

### OperatorType.DetectionSequenceJudge / Detection Sequence Judge
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `B0480FA5762D` |
| `1.0.0` | `2026-04-10T17:42:49.0767787+08:00` | `CFD9D2384786` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `304650035DC9` |

### OperatorType.DualModalVoting / 双模态投票
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `F7DA5BDB66C7` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `B50F970BACFE` |

### OperatorType.EdgeIntersection / 边线交点
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `25C7003F1EBD` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `F9B031E6BA5A` |

### OperatorType.EdgePairDefect / 边缘对缺陷
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `7FC4BA140142` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `40A7891AAD29` |

### OperatorType.Filtering / Gaussian Blur
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `10556AD6D71E` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `D9C1A51FA92C` |

### OperatorType.FrameAveraging / 帧平均
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `5D3A3372B991` |
| `1.0.0` | `2026-04-08T22:02:18.9641594+08:00` | `5D3973154CFA` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `986722B04F67` |

### OperatorType.GapMeasurement / 间隙测量
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `81FC48BD52D5` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `B4480FC082D5` |

### OperatorType.GeoMeasurement / 几何测量
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `20AC7D63887E` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `8E8DBCE518EA` |

### OperatorType.GeometricFitting / Geometric Fitting
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `8C22B86DA1AA` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `8DF53D931DAF` |

### OperatorType.GeometricTolerance / 几何公差
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `9BDF9252DECC` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `D68B1A1FA0EC` |

### OperatorType.GradientShapeMatch / 梯度形状匹配
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `BE1DD761D410` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `6E00E64E04AA` |

### OperatorType.HandEyeCalibration / 手眼标定
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `2958261D0E81` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `0E8F8C1E43EC` |

### OperatorType.HandEyeCalibrationValidator / 手眼标定验证
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `5B2B745ACB3E` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `67868CDD56AD` |

### OperatorType.HistogramAnalysis / 直方图分析
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `8B682154A78D` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `8515841FC225` |

### OperatorType.HistogramEqualization / 直方图均衡化
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `DEF0F4F6E9C5` |
| `1.0.0` | `2026-04-08T22:02:18.9641594+08:00` | `279F9C6A031C` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `4F533BE2BFD2` |

### OperatorType.ImageCompose / 图像组合
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `023726942CCB` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `739E80EBA720` |

### OperatorType.ImageNormalize / 图像归一化
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `EAC81104D863` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `C13B4C8B12BF` |

### OperatorType.ImageSave / 图像保存
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `0AF22B8BF03D` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `5F4D6954858A` |

### OperatorType.ImageStitching / 图像拼接
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `6E49A6E1A982` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `6BEF54EF7AED` |

### OperatorType.ImageSubtract / Image Subtract
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `C55253F2EBBF` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `BB6FA7671D3D` |

### OperatorType.ImageTiling / 图像切片
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `98FA118539D1` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `2B7CC3AE5B97` |

### OperatorType.JsonExtractor / JSON 提取器
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `F3C38D7AD62D` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `170544CA4FA5` |

### OperatorType.LineLineDistance / 线线距离
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `6218C76C8208` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `DB4FE7BE4F4D` |

### OperatorType.LocalDeformableMatching / Local Deformable Matching
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.4` | `2026-04-11T16:55:26.5329457+08:00` | `8333DAA86430` |
| `1.0.4` | `2026-04-09T23:22:12.6079040+08:00` | `9AD0E95A8C88` |
| `1.0.4` | `2026-04-09T18:54:31.7739645+08:00` | `7E12C10CAF47` |
| `1.0.3` | `2026-04-09T18:53:22.7806483+08:00` | `7748260D0601` |
| `1.0.3` | `2026-04-09T17:46:47.2039512+08:00` | `7238472CD61B` |
| `1.0.2` | `2026-04-09T17:45:07.6200243+08:00` | `5A57242A3FCC` |
| `1.0.2` | `2026-04-09T14:05:13.7916582+08:00` | `B24AF44A3D5E` |
| `1.0.1` | `2026-04-09T14:03:26.8095351+08:00` | `056A4288A3B9` |
| `1.0.1` | `2026-04-09T13:47:03.4893006+08:00` | `1696D2394889` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `95A32A24B967` |

### OperatorType.MathOperation / 数值计算
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `985B1E155628` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `EC1803C8E9A9` |

### OperatorType.MeanFilter / 均值滤波
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `DD0BC13363D3` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `23168BEB9E7F` |

### OperatorType.MitsubishiMcCommunication / 三菱MC通信
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `6DA73E6E8D2B` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `64EF0C1DC0E3` |

### OperatorType.ModbusCommunication / Modbus通信
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `EAC52E7749F1` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `2FB888EEAEA1` |

### OperatorType.MorphologicalOperation / Morphological Operation
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `F4895E5C1243` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `9F96B5773EA8` |

### OperatorType.Morphology / Morphology (Legacy)
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `D68EB476BBEF` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `DD34DE7B5726` |

### OperatorType.NPointCalibration / N点标定
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `3DF5A204BEE4` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `170692A628FB` |

### OperatorType.OmronFinsCommunication / 欧姆龙FINS通信
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `6041B1ADB2F0` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `F11484F49198` |

### OperatorType.PPFMatch / PPF表面匹配
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.4` | `2026-04-11T16:55:26.5329457+08:00` | `A849550406CB` |
| `1.0.4` | `2026-04-09T17:46:47.2039512+08:00` | `0C6B3AA3DE69` |
| `1.0.3` | `2026-04-09T17:45:07.6200243+08:00` | `241B24FE4AC9` |
| `1.0.3` | `2026-04-09T15:38:44.1006780+08:00` | `EAAD9ABD96A0` |
| `1.0.2` | `2026-04-09T15:36:44.7095984+08:00` | `611C4A2A3333` |
| `1.0.2` | `2026-04-09T13:47:03.4893006+08:00` | `92BD59E42861` |
| `1.0.1` | `2026-04-07T18:58:05.9332569+08:00` | `0D0FDDC9C625` |

### OperatorType.ParallelLineFind / 平行线查找
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `D67465BA6D4D` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `F11A9F3E6855` |

### OperatorType.PerspectiveTransform / 透视变换
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `683816ED05A1` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `C56528ABD9AF` |

### OperatorType.PixelStatistics / 像素统计
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `82DC78427BAF` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `3E845B989BC1` |

### OperatorType.PlanarMatching / Planar Matching
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.1.1` | `2026-04-11T16:55:26.5329457+08:00` | `CD1D6DEF0F5F` |
| `1.1.1` | `2026-04-09T23:22:12.6079040+08:00` | `F97D9B2286D6` |
| `1.1.1` | `2026-04-09T18:54:31.7739645+08:00` | `2DEA7AF84152` |
| `1.1.0` | `2026-04-09T18:53:22.7806483+08:00` | `68C4C6AB0440` |
| `1.1.0` | `2026-04-09T13:47:03.4893006+08:00` | `4C751066C40E` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `7DF103AFF77A` |

### OperatorType.PointAlignment / 点位对齐
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.1` | `2026-04-11T16:55:26.5329457+08:00` | `3BF1F5B21D71` |
| `1.0.1` | `2026-04-09T13:47:03.4893006+08:00` | `87ED7F7672ED` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `7D3ED24D3273` |

### OperatorType.PointCorrection / 点位修正
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.1` | `2026-04-11T16:55:26.5329457+08:00` | `FA5BFCC8E525` |
| `1.0.1` | `2026-04-09T13:47:03.4893006+08:00` | `6AA553FE68DD` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `C1DFFAC1D1D8` |

### OperatorType.PointLineDistance / 点线距离
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `FCE5924A5358` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `7C11CE8D231C` |

### OperatorType.PointSetTool / 点集工具
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `C86961A6A036` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `50908877001E` |

### OperatorType.PolarUnwrap / 极坐标展开
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `886EAF34FEA7` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `8BEAF8DE5095` |

### OperatorType.PositionCorrection / 位置修正
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.1` | `2026-04-11T16:55:26.5329457+08:00` | `00E035FDDE0C` |
| `1.0.1` | `2026-04-09T13:47:03.4893006+08:00` | `53B133FF0C30` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `07D84C961991` |

### OperatorType.QuadrilateralFind / 四边形查找
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `B4D7EBCD05FB` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `11857A8CD15C` |

### OperatorType.RectangleDetection / 矩形检测
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `D1603F6CA019` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `9A52976090F9` |

### OperatorType.RegionSkeleton / Region Skeleton
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `D9F748831F97` |
| `1.0.0` | `2026-04-08T17:27:35.2941737+08:00` | `618A97A0B08E` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `0B331757C5AA` |

### OperatorType.ResultJudgment / 结果判定
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `53985764ED7F` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `D6D6EF86DA89` |

### OperatorType.ScriptOperator / 脚本算子
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `5FFF3B6EEC51` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `3BF73F11F392` |

### OperatorType.SemanticSegmentation / 语义分割
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `097A3205214C` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `F78A9BC39DE0` |

### OperatorType.SerialCommunication / 串口通信
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `0546620C9813` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `661DA5689903` |

### OperatorType.ShadingCorrection / 光照校正
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `D979701EF91A` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `4B654D0F93E4` |

### OperatorType.ShapeMatching / 旋转尺度模板匹配
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.1.2` | `2026-04-11T16:55:26.5329457+08:00` | `8D0AEBDCF2AB` |
| `1.1.2` | `2026-04-09T23:22:12.6079040+08:00` | `51059543B1E4` |
| `1.1.2` | `2026-04-09T18:54:31.7739645+08:00` | `E204B802CA70` |
| `1.1.1` | `2026-04-09T18:53:22.7806483+08:00` | `FB1ED97E88A9` |
| `1.1.1` | `2026-04-09T13:47:03.4893006+08:00` | `5FDA78DADC35` |
| `1.1.0` | `2026-04-07T18:58:05.9332569+08:00` | `1A5E244A6349` |

### OperatorType.SharpnessEvaluation / 清晰度评估
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `1FF3E760D01C` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `88E5BE97F908` |

### OperatorType.SiemensS7Communication / 西门子S7通信
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `42E5C6F8C21C` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `B6237819F8BE` |

### OperatorType.Statistics / Statistics
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `4E2FDAE0D791` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `4C4452402E9A` |

### OperatorType.StringFormat / 字符串格式化
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `2A4462CE9D5A` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `4CCDDE88F48F` |

### OperatorType.SubpixelEdgeDetection / Subpixel Edge Detection
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `652B8AC1E38A` |
| `1.0.0` | `2026-04-10T17:42:49.0767787+08:00` | `482F884A9448` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `3AB12925E1E0` |

### OperatorType.SurfaceDefectDetection / 表面缺陷检测
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `2.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `478933331F47` |
| `2.0.0` | `2026-04-10T17:42:49.0767787+08:00` | `3A7A4EC68BD2` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `90D11B72CA9C` |

### OperatorType.TemplateMatching / 模板匹配
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.1.1` | `2026-04-11T16:55:26.5329457+08:00` | `B5B076B4DFAF` |
| `1.1.1` | `2026-04-09T23:22:12.6079040+08:00` | `A856B21665F3` |
| `1.1.1` | `2026-04-09T18:54:31.7739645+08:00` | `F891628E8DAA` |
| `1.1.0` | `2026-04-09T18:53:22.7806483+08:00` | `3EFD9333DC8B` |
| `1.1.0` | `2026-04-09T15:08:06.1214871+08:00` | `BDE1CF87E2E3` |
| `1.0.1` | `2026-04-09T14:03:26.8095351+08:00` | `4483A2E3DB3C` |
| `1.0.0` | `2026-04-09T13:47:03.4893006+08:00` | `F5D734D936CA` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `2FD9DB94E474` |

### OperatorType.TextSave / Text Save
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `BD1CB4079308` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `D27F4CA5947C` |

### OperatorType.Thresholding / Threshold
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `20281AD75880` |
| `1.0.0` | `2026-04-08T19:13:32.2020980+08:00` | `6EF90355BA3A` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `F59A1E561D20` |

### OperatorType.TimerStatistics / 计时统计
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `5AE68CD325A0` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `A2DAA30341B0` |

### OperatorType.TranslationRotationCalibration / 平移旋转标定
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `994AF95A3442` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `108D0A0FCC40` |

### OperatorType.TriggerModule / 触发模块
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `757078C7423F` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `A2BBEE56F7C4` |

### OperatorType.TryCatch / 异常捕获
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `4063A03C1DD0` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `1C3ACCA39267` |

### OperatorType.TypeConvert / Type Convert
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `7B2399167D77` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `102ADEA5B2B2` |

### OperatorType.Undistort / Undistort
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `B1CB22CD65A0` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `AA6855AFF929` |

### OperatorType.UnitConvert / 单位换算
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `F2D5A61A11C9` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `0283FE80FDDC` |

### OperatorType.VariableIncrement / 变量递增
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `7F3E98C46BB3` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `BF2BB11DC5D1` |

### OperatorType.VariableRead / 变量读取
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `BBAB0B899754` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `4FE6D6F64722` |

### OperatorType.VariableWrite / 变量写入
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `34636F7341BC` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `42DA20C2270A` |

### OperatorType.WidthMeasurement / 宽度测量
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-11T16:55:26.5329457+08:00` | `175A335805B8` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `7D994B459340` |
