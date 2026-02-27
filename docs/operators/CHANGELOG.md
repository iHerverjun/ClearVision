# 算子版本变更记录 / Operator Version Changelog

> 生成时间 / Generated At: `2026-02-26 21:20:16 +08:00`
> 算子总数 / Total Operators: **118**

## 当前版本快照 / Current Snapshot
| 枚举 (Enum) | 显示名 (DisplayName) | 分类 (Category) | 版本 (Version) |
|------|------|------|------|
| `OperatorType.DeepLearning` | 深度学习 | AI检测 | `1.0.0` |
| `OperatorType.DualModalVoting` | 双模态投票 | AI检测 | `1.0.0` |
| `OperatorType.EdgePairDefect` | 边缘对缺陷 | AI检测 | `1.0.0` |
| `OperatorType.SurfaceDefectDetection` | 表面缺陷检测 | AI检测 | `1.0.0` |
| `OperatorType.AkazeFeatureMatch` | AKAZE特征匹配 | 匹配定位 | `1.0.0` |
| `OperatorType.GradientShapeMatch` | 梯度形状匹配 | 匹配定位 | `1.0.0` |
| `OperatorType.OrbFeatureMatch` | ORB特征匹配 | 匹配定位 | `1.0.0` |
| `OperatorType.PyramidShapeMatch` | 金字塔形状匹配 | 匹配定位 | `1.0.0` |
| `OperatorType.ShapeMatching` | Shape Matching | 匹配定位 | `1.0.0` |
| `OperatorType.TemplateMatching` | 模板匹配 | 匹配定位 | `1.0.0` |
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
| `OperatorType.PositionCorrection` | 位置修正 | 定位 | `1.0.0` |
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
| `OperatorType.PointAlignment` | 点位对齐 | 数据处理 | `1.0.0` |
| `OperatorType.PointCorrection` | 点位修正 | 数据处理 | `1.0.0` |
| `OperatorType.UnitConvert` | 单位换算 | 数据处理 | `1.0.0` |
| `OperatorType.CalibrationLoader` | 标定加载 | 标定 | `1.0.0` |
| `OperatorType.CameraCalibration` | Camera Calibration | 标定 | `1.0.0` |
| `OperatorType.CoordinateTransform` | 坐标转换 | 标定 | `1.0.0` |
| `OperatorType.NPointCalibration` | N点标定 | 标定 | `1.0.0` |
| `OperatorType.TranslationRotationCalibration` | 平移旋转标定 | 标定 | `1.0.0` |
| `OperatorType.Undistort` | Undistort | 标定 | `1.0.0` |
| `OperatorType.AngleMeasurement` | 角度测量 | 检测 | `1.0.0` |
| `OperatorType.CaliperTool` | 卡尺工具 | 检测 | `1.0.0` |
| `OperatorType.CircleMeasurement` | 圆测量 | 检测 | `1.0.0` |
| `OperatorType.ContourMeasurement` | 轮廓测量 | 检测 | `1.0.0` |
| `OperatorType.GapMeasurement` | 间隙测量 | 检测 | `1.0.0` |
| `OperatorType.GeoMeasurement` | 几何测量 | 检测 | `1.0.0` |
| `OperatorType.GeometricFitting` | Geometric Fitting | 检测 | `1.0.0` |
| `OperatorType.GeometricTolerance` | 几何公差 | 检测 | `1.0.0` |
| `OperatorType.HistogramAnalysis` | 直方图分析 | 检测 | `1.0.0` |
| `OperatorType.LineLineDistance` | 线线距离 | 检测 | `1.0.0` |
| `OperatorType.LineMeasurement` | 直线测量 | 检测 | `1.0.0` |
| `OperatorType.Measurement` | 测量 | 检测 | `1.0.0` |
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
| `OperatorType.BlobAnalysis` | Blob分析 | 特征提取 | `1.0.0` |
| `OperatorType.ContourDetection` | 轮廓检测 | 特征提取 | `1.0.0` |
| `OperatorType.EdgeDetection` | Edge Detection | 特征提取 | `1.0.0` |
| `OperatorType.SubpixelEdgeDetection` | Subpixel Edge Detection | 特征提取 | `1.0.0` |
| `OperatorType.CodeRecognition` | 条码识别 | 识别 | `1.0.0` |
| `OperatorType.OcrRecognition` | OCR 识别 | 识别 | `1.0.0` |
| `OperatorType.Comment` | 注释 | 辅助 | `1.0.0` |
| `OperatorType.RoiManager` | ROI管理器 | 辅助 | `1.0.0` |
| `OperatorType.ImageSave` | 图像保存 | 输出 | `1.0.0` |
| `OperatorType.ResultOutput` | 结果输出 | 输出 | `1.0.0` |
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
| `OperatorType.AdaptiveThreshold` | 自适应阈值 | 预处理 | `1.0.0` |
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
| `OperatorType.Thresholding` | 二值化 | 预处理 | `1.0.0` |
| `OperatorType.ColorDetection` | 颜色检测 | 颜色处理 | `1.0.0` |
| `OperatorType.ColorMeasurement` | 颜色测量 | 颜色处理 | `1.0.0` |

## 历史变更 / Historical Changes

### OperatorType.ImageAcquisition / 图像采集
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-02-27T13:55:09.8962327+08:00` | `E826D45C253D` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `0B7EA0C62AEC` |

### OperatorType.ImageAdd / 图像加法
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-02-27T09:08:37.3873065+08:00` | `0CDCBE571A32` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `68648F5303AB` |

### OperatorType.MitsubishiMcCommunication / 三菱MC通信
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-02-27T21:39:45.2435118+08:00` | `6DA73E6E8D2B` |
| `1.0.0` | `2026-02-27T21:13:09.9008744+08:00` | `6FCA4034036C` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `5CBB32DC2CED` |

### OperatorType.Morphology / Morphology (Legacy)
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-02-27T09:08:37.3873065+08:00` | `3161073CD194` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `7D8DED829483` |

### OperatorType.OmronFinsCommunication / 欧姆龙FINS通信
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-02-27T21:39:45.2435118+08:00` | `6041B1ADB2F0` |
| `1.0.0` | `2026-02-27T21:13:09.9008744+08:00` | `63542A557B6C` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `DC5EBDDA2BD2` |

### OperatorType.PerspectiveTransform / 透视变换
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-02-27T09:08:37.3873065+08:00` | `683816ED05A1` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `386962DF7E0B` |

### OperatorType.PolarUnwrap / 极坐标展开
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-02-27T09:08:37.3873065+08:00` | `AC99929E34C4` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `688C288F636E` |

### OperatorType.SiemensS7Communication / 西门子S7通信
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-02-27T21:39:45.2435118+08:00` | `42E5C6F8C21C` |
| `1.0.0` | `2026-02-27T21:13:09.9008744+08:00` | `DD54339521A8` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `8AB237E58691` |

### OperatorType.Statistics / Statistics
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-02-27T09:08:37.3873065+08:00` | `284782E31077` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `332ECE5D2E91` |
