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
| `OperatorType.SurfaceDefectDetection` | 表面缺陷检测 | AI检测 | `1.0.0` |
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
| `OperatorType.ColorDetection` | 颜色检测 | 颜色处理 | `1.0.0` |
| `OperatorType.ColorMeasurement` | 颜色测量 | 颜色处理 | `1.0.2` |

## 历史变更 / Historical Changes

### OperatorType.AdaptiveThreshold / Adaptive Threshold
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-08T19:13:32.2020980+08:00` | `B975CE907035` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `B455F3119904` |

### OperatorType.ClaheEnhancement / CLAHE增强
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-08T22:02:18.9641594+08:00` | `5F3BBC85B0F4` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `69E533C1A735` |

### OperatorType.FrameAveraging / 帧平均
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-08T22:02:18.9641594+08:00` | `5D3973154CFA` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `986722B04F67` |

### OperatorType.HistogramEqualization / 直方图均衡化
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-08T22:02:18.9641594+08:00` | `279F9C6A031C` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `4F533BE2BFD2` |

### OperatorType.LocalDeformableMatching / Local Deformable Matching
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.4` | `2026-04-09T23:22:12.6079040+08:00` | `9AD0E95A8C88` |
| `1.0.4` | `2026-04-09T18:54:31.7739645+08:00` | `7E12C10CAF47` |
| `1.0.3` | `2026-04-09T18:53:22.7806483+08:00` | `7748260D0601` |
| `1.0.3` | `2026-04-09T17:46:47.2039512+08:00` | `7238472CD61B` |
| `1.0.2` | `2026-04-09T17:45:07.6200243+08:00` | `5A57242A3FCC` |
| `1.0.2` | `2026-04-09T14:05:13.7916582+08:00` | `B24AF44A3D5E` |
| `1.0.1` | `2026-04-09T14:03:26.8095351+08:00` | `056A4288A3B9` |
| `1.0.1` | `2026-04-09T13:47:03.4893006+08:00` | `1696D2394889` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `95A32A24B967` |

### OperatorType.PPFMatch / PPF表面匹配
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.4` | `2026-04-09T17:46:47.2039512+08:00` | `0C6B3AA3DE69` |
| `1.0.3` | `2026-04-09T17:45:07.6200243+08:00` | `241B24FE4AC9` |
| `1.0.3` | `2026-04-09T15:38:44.1006780+08:00` | `EAAD9ABD96A0` |
| `1.0.2` | `2026-04-09T15:36:44.7095984+08:00` | `611C4A2A3333` |
| `1.0.2` | `2026-04-09T13:47:03.4893006+08:00` | `92BD59E42861` |
| `1.0.1` | `2026-04-07T18:58:05.9332569+08:00` | `0D0FDDC9C625` |

### OperatorType.PlanarMatching / Planar Matching
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.1.1` | `2026-04-09T23:22:12.6079040+08:00` | `F97D9B2286D6` |
| `1.1.1` | `2026-04-09T18:54:31.7739645+08:00` | `2DEA7AF84152` |
| `1.1.0` | `2026-04-09T18:53:22.7806483+08:00` | `68C4C6AB0440` |
| `1.1.0` | `2026-04-09T13:47:03.4893006+08:00` | `4C751066C40E` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `7DF103AFF77A` |

### OperatorType.PointAlignment / 点位对齐
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.1` | `2026-04-09T13:47:03.4893006+08:00` | `87ED7F7672ED` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `7D3ED24D3273` |

### OperatorType.PointCorrection / 点位修正
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.1` | `2026-04-09T13:47:03.4893006+08:00` | `6AA553FE68DD` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `C1DFFAC1D1D8` |

### OperatorType.PositionCorrection / 位置修正
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.1` | `2026-04-09T13:47:03.4893006+08:00` | `53B133FF0C30` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `07D84C961991` |

### OperatorType.RegionSkeleton / Region Skeleton
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-08T17:27:35.2941737+08:00` | `618A97A0B08E` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `0B331757C5AA` |

### OperatorType.ShapeMatching / 旋转尺度模板匹配
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.1.2` | `2026-04-09T23:22:12.6079040+08:00` | `51059543B1E4` |
| `1.1.2` | `2026-04-09T18:54:31.7739645+08:00` | `E204B802CA70` |
| `1.1.1` | `2026-04-09T18:53:22.7806483+08:00` | `FB1ED97E88A9` |
| `1.1.1` | `2026-04-09T13:47:03.4893006+08:00` | `5FDA78DADC35` |
| `1.1.0` | `2026-04-07T18:58:05.9332569+08:00` | `1A5E244A6349` |

### OperatorType.TemplateMatching / 模板匹配
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.1.1` | `2026-04-09T23:22:12.6079040+08:00` | `A856B21665F3` |
| `1.1.1` | `2026-04-09T18:54:31.7739645+08:00` | `F891628E8DAA` |
| `1.1.0` | `2026-04-09T18:53:22.7806483+08:00` | `3EFD9333DC8B` |
| `1.1.0` | `2026-04-09T15:08:06.1214871+08:00` | `BDE1CF87E2E3` |
| `1.0.1` | `2026-04-09T14:03:26.8095351+08:00` | `4483A2E3DB3C` |
| `1.0.0` | `2026-04-09T13:47:03.4893006+08:00` | `F5D734D936CA` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `2FD9DB94E474` |

### OperatorType.Thresholding / Threshold
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-04-08T19:13:32.2020980+08:00` | `6EF90355BA3A` |
| `1.0.0` | `2026-04-07T18:58:05.9332569+08:00` | `F59A1E561D20` |
