# 算子版本变更记录 / Operator Version Changelog

> 生成时间 / Generated At: `2026-02-26 21:20:16 +08:00`
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
| `OperatorType.CalibrationLoader` | Calibration Loader | 标定 | `1.0.0` |
| `OperatorType.CameraCalibration` | Camera Calibration | 标定 | `1.0.0` |
| `OperatorType.CoordinateTransform` | Coordinate Transform | 标定 | `1.0.0` |
| `OperatorType.FisheyeCalibration` | Fisheye Calibration | 标定 | `1.0.0` |
| `OperatorType.FisheyeUndistort` | Fisheye Undistort | 标定 | `1.0.0` |
| `OperatorType.HandEyeCalibration` | Hand-Eye Calibration | 标定 | `1.0.0` |
| `OperatorType.HandEyeCalibrationValidator` | Hand-Eye Calibration Validator | 标定 | `1.0.1` |
| `OperatorType.NPointCalibration` | N Point Calibration | 标定 | `1.0.0` |
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
| `OperatorType.ColorMeasurement` | 颜色测量 | 颜色处理 | `2.0.0` |

## 历史变更 / Historical Changes

### OperatorType.AdaptiveThreshold / Adaptive Threshold
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `B975CE907035` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `B455F3119904` |

### OperatorType.AffineTransform / 仿射变换
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `E63763CA1D29` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `4AD3551216EE` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `D15105FFF7E6` |

### OperatorType.Aggregator / 数据聚合
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `E68F1AFAFC5F` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `FAD533F6A53E` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `9D1FADF66DA7` |

### OperatorType.AkazeFeatureMatch / AKAZE特征匹配
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `8E074C0E1329` |
| `1.0.0` | `2026-03-04T10:35:29.6469155+08:00` | `6F966F31BFBE` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `7826C15D141A` |

### OperatorType.AngleMeasurement / 角度测量
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `6AD54E78C9E8` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `75B22BE2483A` |

### OperatorType.AnomalyDetection / 异常检测
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `AAEA9F498173` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `C16C20BC74A9` |
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `0C14CFAF3486` |
| `1.0.0` | `2026-03-18T19:00:25.2910689+08:00` | `C16C20BC74A9` |

### OperatorType.ArcCaliper / Arc Caliper
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `BD6BC33A07AE` |
| `1.0.0` | `2026-03-18T19:00:25.2910689+08:00` | `A18C33786AE5` |

### OperatorType.ArrayIndexer / 数组索引器
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `14127778F4A0` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `A4ADD286E7D5` |

### OperatorType.BlobAnalysis / Blob分析
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.1.0` | `2026-04-12T12:53:52.9929473+08:00` | `35B00D18F075` |
| `1.1.0` | `2026-03-21T01:38:49.8374844+08:00` | `3BC2F4374BA8` |
| `1.1.0` | `2026-03-17T14:30:51.0566057+08:00` | `9C4A1922B234` |
| `1.0.0` | `2026-03-17T14:27:11.6128169+08:00` | `15C54747CFB6` |
| `1.0.0` | `2026-03-17T12:35:04.9178309+08:00` | `066BA62991EB` |
| `1.0.0` | `2026-03-16T23:16:26.6950446+08:00` | `E479498C6334` |
| `1.0.0` | `2026-03-16T01:00:41.9846479+08:00` | `DD3B35AC2885` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `AA3FBABF31F0` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `399F00274DCD` |

### OperatorType.BlobLabeling / 连通域标注
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `98361D064008` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `10BC8AC0A2DD` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `CB8C33825ABA` |

### OperatorType.BoxFilter / 候选框过滤 (Bounding Box)
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-27T15:14:57.0770992+08:00` | `809AD2B52541` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `E6CEA2515082` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `E733CF600B7F` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `2E238D831FF0` |

### OperatorType.BoxNms / 候选框抑制
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `E4CBB398C15C` |
| `1.0.0` | `2026-03-26T20:16:17.5776687+08:00` | `2B3620B159CC` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `426E8183E019` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `96FD137BF2A1` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `C0B7D3338B2D` |

### OperatorType.CalibrationLoader / Calibration Loader
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `54BFF1856F9A` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `78A181F5EBA5` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `4DE2162FEF55` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `1680B6462051` |

### OperatorType.CaliperTool / 卡尺工具
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T18:31:04.9508036+08:00` | `6CD663B6F1E2` |
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `CDB7057CA44D` |
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `8956BB4F439B` |
| `1.0.0` | `2026-03-17T12:35:04.9178309+08:00` | `78EBE4E17E09` |
| `1.0.0` | `2026-03-16T23:16:26.6950446+08:00` | `79BAEECDB051` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `BE0CC297B91B` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `18247D9E6FB3` |

### OperatorType.CameraCalibration / Camera Calibration
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `FE613E289E47` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `F16C96AD05AD` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `490769260740` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `C892CA5A86D9` |

### OperatorType.CircleMeasurement / 圆测量
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `715E73668BBF` |
| `1.0.0` | `2026-03-16T01:00:41.9846479+08:00` | `0F06DC09DFF2` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `F24C794D0D92` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `D89303A4578F` |

### OperatorType.ClaheEnhancement / CLAHE增强
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `5F3BBC85B0F4` |
| `1.0.0` | `2026-03-16T01:00:41.9846479+08:00` | `69E533C1A735` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `E6C58B4D7CFF` |

### OperatorType.CodeRecognition / 条码识别
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `E9EA23FB99BA` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `C49C5CAA3C71` |

### OperatorType.ColorConversion / 颜色空间转换
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-16T01:00:41.9846479+08:00` | `287716BE3467` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `04C479C51518` |

### OperatorType.ColorDetection / 颜色检测
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `2.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `D9F18714A4C1` |
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `424C14C47E4B` |
| `1.0.0` | `2026-03-17T12:35:04.9178309+08:00` | `F78B0BF0F340` |
| `1.0.0` | `2026-03-16T01:00:41.9846479+08:00` | `423D33D479AE` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `BBD0B5A93508` |
| `1.0.0` | `2026-03-04T10:35:29.6469155+08:00` | `46BDCCBEBC34` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `47E27D2BD6CF` |

### OperatorType.ColorMeasurement / 颜色测量
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `2.0.0` | `2026-04-12T18:31:04.9508036+08:00` | `20BC9FC908FB` |
| `1.0.2` | `2026-03-21T01:38:49.8374844+08:00` | `63EE99CDC008` |
| `1.0.2` | `2026-03-18T19:00:25.2910689+08:00` | `B6BCC41568EB` |
| `1.0.1` | `2026-03-17T17:33:01.9139128+08:00` | `13FF4D7C376D` |
| `1.0.0` | `2026-03-17T17:30:55.6121854+08:00` | `AB581D9F2445` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `31FC57E80C3C` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `970C6588B0A4` |

### OperatorType.Comment / 注释
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `3D88605226ED` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `D088230EC428` |

### OperatorType.Comparator / 数值比较
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `22FCE0FF7CB5` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `B6B5D8E5A137` |

### OperatorType.ConditionalBranch / 条件分支
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `E8EBC9095D13` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `29F6BE3DEEB2` |

### OperatorType.ContourDetection / 轮廓检测
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `D73488938EDD` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `62F866A1DD98` |

### OperatorType.ContourExtrema / Contour Extrema
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `89D3C64A1F06` |
| `1.0.0` | `2026-03-18T19:00:25.2910689+08:00` | `CF1814DC72D3` |

### OperatorType.ContourMeasurement / 轮廓测量
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `9E59A9BBDB9F` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `54B6127392FC` |

### OperatorType.CoordinateTransform / Coordinate Transform
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `0ACBA65146BA` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `9A444692FBD4` |

### OperatorType.CopyMakeBorder / 边界填充
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `D98F801C200D` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `EA1319B3DDD0` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `D52A1162A333` |

### OperatorType.CornerDetection / 角点检测
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `A309BED01F1A` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `8FC52A647500` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `8CB17BD12666` |

### OperatorType.CycleCounter / 循环计数器
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `59FED1DF2CFC` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `8938799BB943` |

### OperatorType.DatabaseWrite / 数据库写入
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `DB7098D9E78E` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `71703C60A331` |

### OperatorType.DeepLearning / 深度学习
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `2E48EADEADA1` |
| `1.0.0` | `2026-03-27T21:24:05.6159117+08:00` | `4D9170172AFC` |
| `1.0.0` | `2026-03-26T20:16:17.5776687+08:00` | `D88A39CAAF47` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `94CF298C8838` |
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `7A28ABBC8B98` |
| `1.0.0` | `2026-03-19T21:05:20.9090050+08:00` | `BA294CCA79B2` |
| `1.0.0` | `2026-03-16T19:59:19.7031372+08:00` | `1080D0ECD2E1` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `5EC82567EBEA` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `B029BB23CF34` |
| `1.0.0` | `2026-03-04T10:35:29.6469155+08:00` | `BED694E4F32C` |
| `1.0.0` | `2026-02-28T20:04:47.7041096+08:00` | `EE655353AE8F` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `CB4801A37985` |

### OperatorType.Delay / 延时
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `D68E60C72457` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `AE3DCF902E63` |

### OperatorType.DetectionSequenceJudge / Detection Sequence Judge
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `727A2E906BAF` |
| `1.0.0` | `2026-03-27T15:14:57.0770992+08:00` | `304650035DC9` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `BBDC8D920528` |
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `8A2065048793` |
| `1.0.0` | `2026-03-19T21:05:20.9090050+08:00` | `FBDBEFB1AF7F` |

### OperatorType.DistanceTransform / Distance Transform
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `F7E7D8FDA5AC` |
| `1.0.0` | `2026-03-18T19:00:25.2910689+08:00` | `067E9764FAD3` |

### OperatorType.DualModalVoting / 双模态投票
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `B50F970BACFE` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `F7DA5BDB66C7` |

### OperatorType.EdgeDetection / Edge Detection
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-16T01:00:41.9846479+08:00` | `2DFA437FCD6B` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `3635CC3533DC` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `D8DB44452BC2` |

### OperatorType.EdgeIntersection / 边线交点
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `F9B031E6BA5A` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `25C7003F1EBD` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `49D076E880F2` |

### OperatorType.EdgePairDefect / 边缘对缺陷
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `612FC012809E` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `40A7891AAD29` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `7FC4BA140142` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `81CCA70F0BDE` |

### OperatorType.EuclideanClusterExtraction / 欧氏聚类分割
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `24432AA1D437` |
| `1.0.0` | `2026-03-17T15:49:49.5980240+08:00` | `80F7E85D31B4` |

### OperatorType.Filtering / Gaussian Blur
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `D9C1A51FA92C` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `10556AD6D71E` |

### OperatorType.FisheyeCalibration / Fisheye Calibration
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `429A6C95885C` |
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `990EBD083DE8` |
| `1.0.0` | `2026-03-18T19:00:25.2910689+08:00` | `72B2E9BED29F` |

### OperatorType.FisheyeUndistort / Fisheye Undistort
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `A9155938C127` |
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `B0B56491C645` |
| `1.0.0` | `2026-03-18T19:00:25.2910689+08:00` | `7B86C470ECE8` |

### OperatorType.FrameAveraging / 帧平均
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `5D3973154CFA` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `986722B04F67` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `2DF9846480B8` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `66F873796264` |

### OperatorType.GapMeasurement / 间隙测量
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `CE50242C670F` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `B4480FC082D5` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `81FC48BD52D5` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `EC1A78180B57` |

### OperatorType.GeoMeasurement / 几何测量
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T18:31:04.9508036+08:00` | `B21D3ED23346` |
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `D3985346D77B` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `8E8DBCE518EA` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `20AC7D63887E` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `8F4ECDF2968D` |

### OperatorType.GeometricFitting / Geometric Fitting
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T18:31:04.9508036+08:00` | `B5568DC07CE4` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `8DF53D931DAF` |
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `8C22B86DA1AA` |
| `1.0.0` | `2026-03-16T19:59:19.7031372+08:00` | `8DF53D931DAF` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `8C22B86DA1AA` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `5F9717089B34` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `911B9BC4CEF9` |

### OperatorType.GeometricTolerance / 几何公差
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T18:31:04.9508036+08:00` | `1B73C69B7203` |
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `29267D6F030B` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `D68B1A1FA0EC` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `9BDF9252DECC` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `CC1399C0B8AD` |

### OperatorType.GlcmTexture / GLCM Texture Features
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `0AD9F465A363` |
| `1.0.0` | `2026-03-17T17:07:32.7286304+08:00` | `1D82B3EEEF43` |

### OperatorType.GradientShapeMatch / 梯度形状匹配
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `6E00E64E04AA` |
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `BE1DD761D410` |
| `1.0.0` | `2026-03-16T19:59:19.7031372+08:00` | `6E00E64E04AA` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `BE1DD761D410` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `C4917CE7BE0A` |

### OperatorType.HandEyeCalibration / Hand-Eye Calibration
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `37FB38F7A53F` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `0E8F8C1E43EC` |
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `2958261D0E81` |
| `1.0.0` | `2026-03-18T19:00:25.2910689+08:00` | `0E8F8C1E43EC` |

### OperatorType.HandEyeCalibrationValidator / Hand-Eye Calibration Validator
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.1` | `2026-04-12T13:38:35.2141516+08:00` | `E17BBC343ACF` |
| `1.0.0` | `2026-04-12T13:37:21.3936141+08:00` | `16D11BE65447` |
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `0C424A5EA7B2` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `67868CDD56AD` |
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `5B2B745ACB3E` |
| `1.0.0` | `2026-03-18T19:00:25.2910689+08:00` | `67868CDD56AD` |

### OperatorType.HistogramAnalysis / 直方图分析
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T18:31:04.9508036+08:00` | `C6D017A36ED7` |
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `01897F1C223A` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `8515841FC225` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `8B682154A78D` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `A33C328B07D8` |

### OperatorType.HistogramEqualization / 直方图均衡化
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `279F9C6A031C` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `4F533BE2BFD2` |

### OperatorType.HttpRequest / HTTP 请求
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-16T01:00:41.9846479+08:00` | `FEE9B353B7DD` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `5329BD29F5B3` |

### OperatorType.ImageAcquisition / 图像采集
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `94FF4EB8EBE5` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `ADB472FDFADC` |
| `1.0.0` | `2026-02-27T13:55:09.8962327+08:00` | `E826D45C253D` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `0B7EA0C62AEC` |

### OperatorType.ImageAdd / 图像加法
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-02-27T09:08:37.3873065+08:00` | `0CDCBE571A32` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `68648F5303AB` |

### OperatorType.ImageCompose / 图像组合
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `739E80EBA720` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `023726942CCB` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `D087BD94D51F` |

### OperatorType.ImageNormalize / 图像归一化
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `C13B4C8B12BF` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `EAC81104D863` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `4510C4122028` |

### OperatorType.ImageSave / 图像保存
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `5F4D6954858A` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `0AF22B8BF03D` |

### OperatorType.ImageStitching / 图像拼接
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `6BEF54EF7AED` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `6E49A6E1A982` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `648EE37D0E05` |

### OperatorType.ImageSubtract / Image Subtract
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `BB6FA7671D3D` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `C55253F2EBBF` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `01B4C5663412` |

### OperatorType.ImageTiling / 图像切片
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `2B7CC3AE5B97` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `98FA118539D1` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `E5252D9D290F` |

### OperatorType.InverseFFT1D / Inverse FFT 1D
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `6820133964C4` |
| `1.0.0` | `2026-03-18T19:00:25.2910689+08:00` | `C1A839CCFE84` |

### OperatorType.JsonExtractor / JSON 提取器
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `170544CA4FA5` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `F3C38D7AD62D` |

### OperatorType.LawsTextureFilter / Laws Texture Filter
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `40D5DCDD709C` |
| `1.0.0` | `2026-03-17T16:52:44.2376192+08:00` | `261438B3A43B` |

### OperatorType.LineLineDistance / 线线距离
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T18:31:04.9508036+08:00` | `FFC6ED93DD57` |
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `50EFBA259A3A` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `DB4FE7BE4F4D` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `6218C76C8208` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `C4197D651346` |

### OperatorType.LineMeasurement / 直线测量
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T18:31:04.9508036+08:00` | `482500224EB3` |
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `2FF0E868B0A4` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `FB7CB0898D13` |

### OperatorType.LocalDeformableMatching / Local Deformable Matching
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.4` | `2026-04-12T12:53:52.9929473+08:00` | `9AD0E95A8C88` |
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `95A32A24B967` |
| `1.0.0` | `2026-03-18T19:00:25.2910689+08:00` | `D31FBB0020ED` |

### OperatorType.MathOperation / 数值计算
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `EC1803C8E9A9` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `985B1E155628` |

### OperatorType.MeanFilter / 均值滤波
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `23168BEB9E7F` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `DD0BC13363D3` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `ED740E081606` |

### OperatorType.Measurement / 测量
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T18:31:04.9508036+08:00` | `1A22FD221339` |
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `91AB87431111` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `4016CC586C1F` |

### OperatorType.MinEnclosingGeometry / Min Enclosing Geometry
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `1575F5D91455` |
| `1.0.0` | `2026-03-18T19:00:25.2910689+08:00` | `9A00C29AC41A` |

### OperatorType.MitsubishiMcCommunication / 三菱MC通信
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `64EF0C1DC0E3` |
| `1.0.0` | `2026-02-27T21:39:45.2435118+08:00` | `6DA73E6E8D2B` |
| `1.0.0` | `2026-02-27T21:13:09.9008744+08:00` | `6FCA4034036C` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `5CBB32DC2CED` |

### OperatorType.ModbusCommunication / Modbus通信
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `2FB888EEAEA1` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `EAC52E7749F1` |

### OperatorType.MorphologicalOperation / Morphological Operation
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `9F96B5773EA8` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `F4895E5C1243` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `C4E6D9300B08` |

### OperatorType.Morphology / Morphology (Legacy)
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `DD34DE7B5726` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `D68EB476BBEF` |
| `1.0.0` | `2026-02-27T09:08:37.3873065+08:00` | `3161073CD194` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `7D8DED829483` |

### OperatorType.MqttPublish / MQTT 发布
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `0875F6C2B395` |
| `1.0.0` | `2026-03-16T19:59:19.7031372+08:00` | `EDF3937B18BD` |
| `1.0.0` | `2026-03-16T01:00:41.9846479+08:00` | `FFCB190578C7` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `7F475495E079` |

### OperatorType.NPointCalibration / N Point Calibration
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `7FBC8F939D41` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `170692A628FB` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `3DF5A204BEE4` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `0B9DBE2431AE` |

### OperatorType.OmronFinsCommunication / 欧姆龙FINS通信
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `F11484F49198` |
| `1.0.0` | `2026-02-27T21:39:45.2435118+08:00` | `6041B1ADB2F0` |
| `1.0.0` | `2026-02-27T21:13:09.9008744+08:00` | `63542A557B6C` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `DC5EBDDA2BD2` |

### OperatorType.OrbFeatureMatch / ORB特征匹配
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `AE7C4204010A` |
| `1.0.0` | `2026-03-04T10:35:29.6469155+08:00` | `024F1AD3150C` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `260BD860C2E1` |

### OperatorType.PPFEstimation / PPF点对特征
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `DA072308815D` |
| `1.0.0` | `2026-03-17T16:06:03.0736962+08:00` | `A311D862B07B` |

### OperatorType.PPFMatch / PPF表面匹配
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.4` | `2026-04-12T12:53:52.9929473+08:00` | `0C6B3AA3DE69` |
| `1.0.1` | `2026-03-21T01:38:49.8374844+08:00` | `0D0FDDC9C625` |
| `1.0.1` | `2026-03-18T19:00:25.2910689+08:00` | `E64DEFAE4B95` |
| `1.0.0` | `2026-03-17T16:34:20.9153387+08:00` | `3F62675CEB39` |

### OperatorType.ParallelLineFind / 平行线查找
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `F11A9F3E6855` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `D67465BA6D4D` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `26F9114FF5A8` |

### OperatorType.PerspectiveTransform / 透视变换
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `C56528ABD9AF` |
| `1.0.0` | `2026-02-27T09:08:37.3873065+08:00` | `683816ED05A1` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `386962DF7E0B` |

### OperatorType.PixelStatistics / 像素统计
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T18:31:04.9508036+08:00` | `86B857B8747F` |
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `77ED8F58B715` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `3E845B989BC1` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `82DC78427BAF` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `4EB06D674A3D` |

### OperatorType.PixelToWorldTransform / Pixel To World Transform
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `6B3BFC7109C5` |
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `863B5827D277` |
| `1.0.0` | `2026-03-18T19:00:25.2910689+08:00` | `DD71E7F3F1AA` |

### OperatorType.PlanarMatching / Planar Matching
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.1.1` | `2026-04-12T12:53:52.9929473+08:00` | `F97D9B2286D6` |
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `7DF103AFF77A` |
| `1.0.0` | `2026-03-18T19:00:25.2910689+08:00` | `2706B43F0B0E` |

### OperatorType.PointAlignment / 点位对齐
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.1` | `2026-04-12T12:53:52.9929473+08:00` | `87ED7F7672ED` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `7D3ED24D3273` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `6732C97572C2` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `E4A8E6F775A9` |

### OperatorType.PointCorrection / 点位修正
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.1` | `2026-04-12T12:53:52.9929473+08:00` | `6AA553FE68DD` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `C1DFFAC1D1D8` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `324DE476C870` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `815617758954` |

### OperatorType.PointLineDistance / 点线距离
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T18:31:04.9508036+08:00` | `AB9761C58FAA` |
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `84F85377B4C7` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `7C11CE8D231C` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `FCE5924A5358` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `EB16F20EF164` |

### OperatorType.PointSetTool / 点集工具
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `50908877001E` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `C86961A6A036` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `32673C56E0B9` |

### OperatorType.PolarUnwrap / 极坐标展开
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `8BEAF8DE5095` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `886EAF34FEA7` |
| `1.0.0` | `2026-02-27T09:08:37.3873065+08:00` | `AC99929E34C4` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `688C288F636E` |

### OperatorType.PositionCorrection / 位置修正
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.1` | `2026-04-12T12:53:52.9929473+08:00` | `53B133FF0C30` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `07D84C961991` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `8B093712CDBE` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `A33AE687F32A` |

### OperatorType.PyramidShapeMatch / 金字塔形状匹配
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-16T01:00:41.9846479+08:00` | `396248DA73D3` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `9A881861B316` |

### OperatorType.QuadrilateralFind / 四边形查找
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `11857A8CD15C` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `B4D7EBCD05FB` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `1CCDD43E1B39` |

### OperatorType.RansacPlaneSegmentation / RANSAC平面分割
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `E69D35252071` |
| `1.0.0` | `2026-03-17T15:37:22.1136239+08:00` | `D692426CF813` |

### OperatorType.RectangleDetection / 矩形检测
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `9A52976090F9` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `D1603F6CA019` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `FBC9E5DEC5C4` |

### OperatorType.RegionComplement / Region Complement
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `AE6829D33D5A` |
| `1.0.0` | `2026-03-18T19:00:25.2910689+08:00` | `8BD7259C4E0C` |

### OperatorType.RegionOpening / Region Opening
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `BFA8EED40F07` |
| `1.0.0` | `2026-03-18T19:00:25.2910689+08:00` | `B1DA89F6441D` |

### OperatorType.RegionSkeleton / Region Skeleton
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `618A97A0B08E` |
| `1.0.0` | `2026-03-18T19:00:25.2910689+08:00` | `0B331757C5AA` |

### OperatorType.ResultJudgment / 结果判定
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `D6D6EF86DA89` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `53985764ED7F` |

### OperatorType.ResultOutput / 结果输出
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.1` | `2026-03-26T18:46:50.6676488+08:00` | `296E7F76B69C` |
| `1.0.1` | `2026-03-21T01:38:49.8374844+08:00` | `8547990CCEBC` |
| `1.0.1` | `2026-03-17T14:30:51.0566057+08:00` | `8165811DA08A` |
| `1.0.0` | `2026-03-17T14:27:11.6128169+08:00` | `9272BB760587` |
| `1.0.0` | `2026-03-16T19:59:19.7031372+08:00` | `BECDD0398F2A` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `ED19595D838D` |
| `1.0.0` | `2026-03-04T11:07:12.6855371+08:00` | `F230E925DC3A` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `CD53E822B204` |

### OperatorType.RoiTransform / ROI跟踪
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `2BA2591DC83F` |
| `1.0.0` | `2026-03-17T14:27:11.6128169+08:00` | `72CC34B3C57B` |

### OperatorType.ScriptOperator / 脚本算子
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `3BF73F11F392` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `5FFF3B6EEC51` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `C36E4CDE7016` |

### OperatorType.SemanticSegmentation / 语义分割
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `F78A9BC39DE0` |
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `097A3205214C` |
| `1.0.0` | `2026-03-18T19:00:25.2910689+08:00` | `F78A9BC39DE0` |

### OperatorType.SerialCommunication / 串口通信
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `661DA5689903` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `0546620C9813` |

### OperatorType.ShadingCorrection / 光照校正
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `4B654D0F93E4` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `D979701EF91A` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `4A014AD67BA3` |

### OperatorType.ShapeMatching / 旋转尺度模板匹配
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.1.2` | `2026-04-12T12:53:52.9929473+08:00` | `51059543B1E4` |
| `1.1.0` | `2026-03-21T01:38:49.8374844+08:00` | `1A5E244A6349` |
| `1.1.0` | `2026-03-17T14:30:51.0566057+08:00` | `141086A053D4` |
| `1.0.0` | `2026-03-17T14:27:11.6128169+08:00` | `D405977E4DDC` |
| `1.0.0` | `2026-03-16T19:59:19.7031372+08:00` | `35D41D58EBB1` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `ADE360463FD2` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `C20A8A850891` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `16DA32089F01` |

### OperatorType.SharpnessEvaluation / 清晰度评估
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T18:31:04.9508036+08:00` | `52D038E87604` |
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `85F599843C91` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `88E5BE97F908` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `1FF3E760D01C` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `3362EBCD01BB` |

### OperatorType.SiemensS7Communication / 西门子S7通信
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `B6237819F8BE` |
| `1.0.0` | `2026-02-27T21:39:45.2435118+08:00` | `42E5C6F8C21C` |
| `1.0.0` | `2026-02-27T21:13:09.9008744+08:00` | `DD54339521A8` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `8AB237E58691` |

### OperatorType.StatisticalOutlierRemoval / 统计滤波
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `6824980A49D6` |
| `1.0.0` | `2026-03-17T15:25:05.6682201+08:00` | `39F96B06A2E6` |

### OperatorType.Statistics / Statistics
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `4C4452402E9A` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `4E2FDAE0D791` |
| `1.0.0` | `2026-02-27T09:08:37.3873065+08:00` | `284782E31077` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `332ECE5D2E91` |

### OperatorType.StereoCalibration / Stereo Calibration
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `810DBE56FF6D` |
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `8CC920E1EE3D` |
| `1.0.0` | `2026-03-18T19:00:25.2910689+08:00` | `EB7C28467E72` |

### OperatorType.StringFormat / 字符串格式化
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `4CCDDE88F48F` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `2A4462CE9D5A` |

### OperatorType.SubpixelEdgeDetection / Subpixel Edge Detection
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `482F884A9448` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `3AB12925E1E0` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `A3FB6B396DF1` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `8A3328983212` |

### OperatorType.SurfaceDefectDetection / 表面缺陷检测
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `2.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `3A7A4EC68BD2` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `90D11B72CA9C` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `77EFD328EF95` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `02BD406438C7` |

### OperatorType.TemplateMatching / 模板匹配
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.1.1` | `2026-04-12T12:53:52.9929473+08:00` | `A856B21665F3` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `2FD9DB94E474` |
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `30BE4FBE1B26` |
| `1.0.0` | `2026-03-16T19:59:19.7031372+08:00` | `2FD9DB94E474` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `30BE4FBE1B26` |
| `1.0.0` | `2026-03-04T10:35:29.6469155+08:00` | `9D6ABB27BF04` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `19F60BB66DE8` |

### OperatorType.TextSave / Text Save
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `D27F4CA5947C` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `BD1CB4079308` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `23E51E9AEF58` |

### OperatorType.Thresholding / Threshold
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `6EF90355BA3A` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `F59A1E561D20` |

### OperatorType.TimerStatistics / 计时统计
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `A2DAA30341B0` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `5AE68CD325A0` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `606B1D386D2B` |

### OperatorType.TranslationRotationCalibration / 平移旋转标定
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `288090A14B98` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `108D0A0FCC40` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `994AF95A3442` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `62D1609B1CCB` |

### OperatorType.TriggerModule / 触发模块
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `A2BBEE56F7C4` |
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `757078C7423F` |
| `1.0.0` | `2026-03-16T19:59:19.7031372+08:00` | `A2BBEE56F7C4` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `757078C7423F` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `4325D6D6D8A0` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `8EBA52E49F78` |

### OperatorType.TryCatch / 异常捕获
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `1C3ACCA39267` |
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `4063A03C1DD0` |
| `1.0.0` | `2026-03-16T19:59:19.7031372+08:00` | `1C3ACCA39267` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `4063A03C1DD0` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `92086F4126B2` |

### OperatorType.TypeConvert / Type Convert
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `102ADEA5B2B2` |
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `7B2399167D77` |
| `1.0.0` | `2026-03-16T19:59:19.7031372+08:00` | `102ADEA5B2B2` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `7B2399167D77` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `8BCC83E53180` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `A92AC259F970` |

### OperatorType.Undistort / Undistort
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `DC5C039E75FD` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `AA6855AFF929` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `B1CB22CD65A0` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `397F69EB0E6E` |

### OperatorType.UnitConvert / 单位换算
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `0283FE80FDDC` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `F2D5A61A11C9` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `FE7B780A4358` |

### OperatorType.VariableIncrement / 变量递增
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `BF2BB11DC5D1` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `7F3E98C46BB3` |

### OperatorType.VariableRead / 变量读取
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `4FE6D6F64722` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `BBAB0B899754` |

### OperatorType.VariableWrite / 变量写入
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `42DA20C2270A` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `34636F7341BC` |

### OperatorType.VoxelDownsample / 体素下采样
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `37C219635A89` |
| `1.0.0` | `2026-03-17T15:02:44.2227737+08:00` | `1FDE2F2206BC` |

### OperatorType.WidthMeasurement / 宽度测量
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-12T18:31:04.9508036+08:00` | `1BD1D7AEA8FE` |
| `1.0.0` | `2026-04-12T12:53:52.9929473+08:00` | `0A455BD495CA` |
| `1.0.0` | `2026-03-26T18:46:50.6676488+08:00` | `7D994B459340` |
| `1.0.0` | `2026-03-21T01:38:49.8374844+08:00` | `175A335805B8` |
| `1.0.0` | `2026-03-16T19:59:19.7031372+08:00` | `7D994B459340` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `175A335805B8` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `BBDEE390601A` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `801058F2953A` |
