# 算子目录 / Operator Catalog

> 生成时间 / Generated At: `2026-02-26 16:43:42 +08:00`
> 算子总数 / Total Operators: **118**

## 分类统计 / Category Summary
| 分类 (Category) | 数量 (Count) | 占比 (Ratio) |
|------|------:|------:|
| AI检测 | 4 | 3.4% |
| Calibration | 2 | 1.7% |
| Feature Extraction | 2 | 1.7% |
| Filtering | 1 | 0.8% |
| General | 2 | 1.7% |
| Logic Tools | 1 | 0.8% |
| Matching | 1 | 0.8% |
| Measurement | 1 | 0.8% |
| Preprocessing | 3 | 2.5% |
| 匹配定位 | 5 | 4.2% |
| 变量 | 4 | 3.4% |
| 图像处理 | 4 | 3.4% |
| 定位 | 7 | 5.9% |
| 拆分组合 | 2 | 1.7% |
| 控制 | 1 | 0.8% |
| 数据 | 1 | 0.8% |
| 数据处理 | 9 | 7.6% |
| 标定 | 4 | 3.4% |
| 检测 | 15 | 12.7% |
| 流程控制 | 4 | 3.4% |
| 特征提取 | 2 | 1.7% |
| 识别 | 2 | 1.7% |
| 辅助 | 2 | 1.7% |
| 输出 | 2 | 1.7% |
| 通信 | 8 | 6.8% |
| 通用 | 2 | 1.7% |
| 逻辑工具 | 4 | 3.4% |
| 逻辑控制 | 1 | 0.8% |
| 采集 | 1 | 0.8% |
| 预处理 | 19 | 16.1% |
| 颜色处理 | 2 | 1.7% |

## 分类索引 / Grouped Index

### AI检测 (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.DeepLearning` | 深度学习 | 1 | 3 | 6 | - | [DeepLearning](./DeepLearning.md) |
| `OperatorType.DualModalVoting` | 双模态投票 | 2 | 3 | 6 | - | [DualModalVoting](./DualModalVoting.md) |
| `OperatorType.EdgePairDefect` | 边缘对缺陷 | 3 | 4 | 4 | - | [EdgePairDefect](./EdgePairDefect.md) |
| `OperatorType.SurfaceDefectDetection` | 表面缺陷检测 | 2 | 4 | 5 | - | [SurfaceDefectDetection](./SurfaceDefectDetection.md) |

### Calibration (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.CameraCalibration` | Camera Calibration | 1 | 2 | 7 | - | [CameraCalibration](./CameraCalibration.md) |
| `OperatorType.Undistort` | Undistort | 2 | 1 | 1 | - | [Undistort](./Undistort.md) |

### Feature Extraction (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.EdgeDetection` | Edge Detection | 1 | 2 | 7 | - | [EdgeDetection](./EdgeDetection.md) |
| `OperatorType.SubpixelEdgeDetection` | Subpixel Edge Detection | 1 | 2 | 5 | - | [SubpixelEdgeDetection](./SubpixelEdgeDetection.md) |

### Filtering (1)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.Filtering` | Gaussian Blur | 1 | 1 | 4 | Gaussian Blur (OpenCV) | [Filtering](./Filtering.md) |

### General (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.Statistics` | Statistics | 1 | 7 | 4 | - | [Statistics](./Statistics.md) |
| `OperatorType.TypeConvert` | Type Convert | 1 | 1 | 2 | - | [TypeConvert](./TypeConvert.md) |

### Logic Tools (1)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.TextSave` | Text Save | 2 | 2 | 5 | - | [TextSave](./TextSave.md) |

### Matching (1)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.ShapeMatching` | Shape Matching | 2 | 2 | 7 | - | [ShapeMatching](./ShapeMatching.md) |

### Measurement (1)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.GeometricFitting` | Geometric Fitting | 1 | 2 | 7 | - | [GeometricFitting](./GeometricFitting.md) |

### Preprocessing (3)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.ImageSubtract` | Image Subtract | 2 | 4 | 1 | - | [ImageSubtract](./ImageSubtract.md) |
| `OperatorType.MorphologicalOperation` | Morphological Operation | 1 | 1 | 7 | - | [MorphologicalOperation](./MorphologicalOperation.md) |
| `OperatorType.Morphology` | Morphology (Legacy) | 1 | 1 | 6 | - | [Morphology](./Morphology.md) |

### 匹配定位 (5)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.AkazeFeatureMatch` | AKAZE特征匹配 | 2 | 4 | 5 | - | [AkazeFeatureMatch](./AkazeFeatureMatch.md) |
| `OperatorType.GradientShapeMatch` | 梯度形状匹配 | 2 | 5 | 5 | - | [GradientShapeMatch](./GradientShapeMatch.md) |
| `OperatorType.OrbFeatureMatch` | ORB特征匹配 | 2 | 4 | 5 | - | [OrbFeatureMatch](./OrbFeatureMatch.md) |
| `OperatorType.PyramidShapeMatch` | 金字塔形状匹配 | 2 | 5 | 5 | - | [PyramidShapeMatch](./PyramidShapeMatch.md) |
| `OperatorType.TemplateMatching` | 模板匹配 | 2 | 4 | 3 | - | [TemplateMatching](./TemplateMatching.md) |

### 变量 (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.CycleCounter` | 循环计数器 | 0 | 5 | 2 | - | [CycleCounter](./CycleCounter.md) |
| `OperatorType.VariableIncrement` | 变量递增 | 0 | 5 | 5 | - | [VariableIncrement](./VariableIncrement.md) |
| `OperatorType.VariableRead` | 变量读取 | 0 | 3 | 3 | - | [VariableRead](./VariableRead.md) |
| `OperatorType.VariableWrite` | 变量写入 | 1 | 3 | 4 | - | [VariableWrite](./VariableWrite.md) |

### 图像处理 (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.AffineTransform` | 仿射变换 | 1 | 2 | 9 | - | [AffineTransform](./AffineTransform.md) |
| `OperatorType.CopyMakeBorder` | 边界填充 | 1 | 1 | 6 | - | [CopyMakeBorder](./CopyMakeBorder.md) |
| `OperatorType.ImageStitching` | 图像拼接 | 2 | 2 | 3 | - | [ImageStitching](./ImageStitching.md) |
| `OperatorType.PolarUnwrap` | 极坐标展开 | 2 | 1 | 7 | - | [PolarUnwrap](./PolarUnwrap.md) |

### 定位 (7)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.BlobLabeling` | 连通域标注 | 2 | 3 | 3 | - | [BlobLabeling](./BlobLabeling.md) |
| `OperatorType.CornerDetection` | 角点检测 | 1 | 3 | 5 | - | [CornerDetection](./CornerDetection.md) |
| `OperatorType.EdgeIntersection` | 边线交点 | 2 | 3 | 0 | - | [EdgeIntersection](./EdgeIntersection.md) |
| `OperatorType.ParallelLineFind` | 平行线查找 | 1 | 6 | 4 | - | [ParallelLineFind](./ParallelLineFind.md) |
| `OperatorType.PositionCorrection` | 位置修正 | 4 | 5 | 3 | - | [PositionCorrection](./PositionCorrection.md) |
| `OperatorType.QuadrilateralFind` | 四边形查找 | 1 | 5 | 4 | - | [QuadrilateralFind](./QuadrilateralFind.md) |
| `OperatorType.RectangleDetection` | 矩形检测 | 1 | 7 | 4 | - | [RectangleDetection](./RectangleDetection.md) |

### 拆分组合 (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.ImageCompose` | 图像组合 | 4 | 1 | 3 | - | [ImageCompose](./ImageCompose.md) |
| `OperatorType.ImageTiling` | 图像切片 | 1 | 3 | 4 | - | [ImageTiling](./ImageTiling.md) |

### 控制 (1)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.ConditionalBranch` | 条件分支 | 1 | 2 | 3 | - | [ConditionalBranch](./ConditionalBranch.md) |

### 数据 (1)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.DatabaseWrite` | 数据库写入 | 1 | 2 | 3 | - | [DatabaseWrite](./DatabaseWrite.md) |

### 数据处理 (9)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.Aggregator` | 数据聚合 | 3 | 4 | 1 | - | [Aggregator](./Aggregator.md) |
| `OperatorType.ArrayIndexer` | 数组索引器 | 1 | 1 | 2 | - | [ArrayIndexer](./ArrayIndexer.md) |
| `OperatorType.BoxFilter` | 候选框过滤 (Bounding Box) | 2 | 3 | 9 | - | [BoxFilter](./BoxFilter.md) |
| `OperatorType.BoxNms` | 候选框抑制 | 2 | 3 | 3 | - | [BoxNms](./BoxNms.md) |
| `OperatorType.JsonExtractor` | JSON 提取器 | 1 | 2 | 1 | - | [JsonExtractor](./JsonExtractor.md) |
| `OperatorType.MathOperation` | 数值计算 | 2 | 2 | 1 | - | [MathOperation](./MathOperation.md) |
| `OperatorType.PointAlignment` | 点位对齐 | 2 | 3 | 2 | - | [PointAlignment](./PointAlignment.md) |
| `OperatorType.PointCorrection` | 点位修正 | 4 | 4 | 3 | - | [PointCorrection](./PointCorrection.md) |
| `OperatorType.UnitConvert` | 单位换算 | 2 | 2 | 4 | - | [UnitConvert](./UnitConvert.md) |

### 标定 (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.CalibrationLoader` | 标定加载 | 0 | 4 | 2 | - | [CalibrationLoader](./CalibrationLoader.md) |
| `OperatorType.CoordinateTransform` | 坐标转换 | 3 | 3 | 4 | - | [CoordinateTransform](./CoordinateTransform.md) |
| `OperatorType.NPointCalibration` | N点标定 | 1 | 3 | 3 | - | [NPointCalibration](./NPointCalibration.md) |
| `OperatorType.TranslationRotationCalibration` | 平移旋转标定 | 1 | 3 | 3 | - | [TranslationRotationCalibration](./TranslationRotationCalibration.md) |

### 检测 (15)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.AngleMeasurement` | 角度测量 | 1 | 2 | 7 | - | [AngleMeasurement](./AngleMeasurement.md) |
| `OperatorType.CaliperTool` | 卡尺工具 | 2 | 4 | 6 | - | [CaliperTool](./CaliperTool.md) |
| `OperatorType.CircleMeasurement` | 圆测量 | 1 | 5 | 7 | - | [CircleMeasurement](./CircleMeasurement.md) |
| `OperatorType.ContourMeasurement` | 轮廓测量 | 1 | 4 | 4 | - | [ContourMeasurement](./ContourMeasurement.md) |
| `OperatorType.GapMeasurement` | 间隙测量 | 2 | 6 | 4 | - | [GapMeasurement](./GapMeasurement.md) |
| `OperatorType.GeoMeasurement` | 几何测量 | 2 | 5 | 2 | - | [GeoMeasurement](./GeoMeasurement.md) |
| `OperatorType.GeometricTolerance` | 几何公差 | 1 | 5 | 9 | - | [GeometricTolerance](./GeometricTolerance.md) |
| `OperatorType.HistogramAnalysis` | 直方图分析 | 1 | 7 | 6 | - | [HistogramAnalysis](./HistogramAnalysis.md) |
| `OperatorType.LineLineDistance` | 线线距离 | 2 | 5 | 1 | - | [LineLineDistance](./LineLineDistance.md) |
| `OperatorType.LineMeasurement` | 直线测量 | 1 | 5 | 4 | - | [LineMeasurement](./LineMeasurement.md) |
| `OperatorType.Measurement` | 测量 | 3 | 2 | 5 | - | [Measurement](./Measurement.md) |
| `OperatorType.PixelStatistics` | 像素统计 | 2 | 6 | 5 | - | [PixelStatistics](./PixelStatistics.md) |
| `OperatorType.PointLineDistance` | 点线距离 | 2 | 2 | 0 | - | [PointLineDistance](./PointLineDistance.md) |
| `OperatorType.SharpnessEvaluation` | 清晰度评估 | 1 | 3 | 6 | - | [SharpnessEvaluation](./SharpnessEvaluation.md) |
| `OperatorType.WidthMeasurement` | 宽度测量 | 3 | 4 | 3 | - | [WidthMeasurement](./WidthMeasurement.md) |

### 流程控制 (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.Delay` | 延时 | 1 | 2 | 1 | - | [Delay](./Delay.md) |
| `OperatorType.ForEach` | ForEach 循环 | 1 | 1 | 4 | - | [ForEach](./ForEach.md) |
| `OperatorType.ResultJudgment` | 结果判定 | 2 | 3 | 8 | - | [ResultJudgment](./ResultJudgment.md) |
| `OperatorType.TryCatch` | 异常捕获 | 1 | 4 | 3 | - | [TryCatch](./TryCatch.md) |

### 特征提取 (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.BlobAnalysis` | Blob分析 | 1 | 3 | 3 | - | [BlobAnalysis](./BlobAnalysis.md) |
| `OperatorType.ContourDetection` | 轮廓检测 | 1 | 3 | 5 | - | [ContourDetection](./ContourDetection.md) |

### 识别 (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.CodeRecognition` | 条码识别 | 1 | 4 | 2 | - | [CodeRecognition](./CodeRecognition.md) |
| `OperatorType.OcrRecognition` | OCR 识别 | 1 | 2 | 0 | - | [OcrRecognition](./OcrRecognition.md) |

### 辅助 (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.Comment` | 注释 | 1 | 2 | 1 | - | [Comment](./Comment.md) |
| `OperatorType.RoiManager` | ROI管理器 | 1 | 2 | 10 | - | [RoiManager](./RoiManager.md) |

### 输出 (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.ImageSave` | 图像保存 | 1 | 2 | 3 | - | [ImageSave](./ImageSave.md) |
| `OperatorType.ResultOutput` | 结果输出 | 4 | 1 | 2 | - | [ResultOutput](./ResultOutput.md) |

### 通信 (8)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.HttpRequest` | HTTP 请求 | 1 | 3 | 4 | - | [HttpRequest](./HttpRequest.md) |
| `OperatorType.MitsubishiMcCommunication` | 三菱MC通信 | 1 | 2 | 12 | - | [MitsubishiMcCommunication](./MitsubishiMcCommunication.md) |
| `OperatorType.ModbusCommunication` | Modbus通信 | 1 | 2 | 8 | - | [ModbusCommunication](./ModbusCommunication.md) |
| `OperatorType.MqttPublish` | MQTT 发布 | 1 | 1 | 4 | - | [MqttPublish](./MqttPublish.md) |
| `OperatorType.OmronFinsCommunication` | 欧姆龙FINS通信 | 1 | 2 | 12 | - | [OmronFinsCommunication](./OmronFinsCommunication.md) |
| `OperatorType.SerialCommunication` | 串口通信 | 1 | 1 | 8 | - | [SerialCommunication](./SerialCommunication.md) |
| `OperatorType.SiemensS7Communication` | 西门子S7通信 | 1 | 2 | 14 | - | [SiemensS7Communication](./SiemensS7Communication.md) |
| `OperatorType.TcpCommunication` | TCP通信 | 1 | 2 | 6 | - | [TcpCommunication](./TcpCommunication.md) |

### 通用 (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.LogicGate` | 逻辑门 | 2 | 1 | 1 | - | [LogicGate](./LogicGate.md) |
| `OperatorType.StringFormat` | 字符串格式化 | 2 | 1 | 1 | - | [StringFormat](./StringFormat.md) |

### 逻辑工具 (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.PointSetTool` | 点集工具 | 2 | 4 | 6 | - | [PointSetTool](./PointSetTool.md) |
| `OperatorType.ScriptOperator` | 脚本算子 | 4 | 2 | 3 | - | [ScriptOperator](./ScriptOperator.md) |
| `OperatorType.TimerStatistics` | 计时统计 | 1 | 4 | 2 | - | [TimerStatistics](./TimerStatistics.md) |
| `OperatorType.TriggerModule` | 触发模块 | 0 | 3 | 3 | - | [TriggerModule](./TriggerModule.md) |

### 逻辑控制 (1)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.Comparator` | 数值比较 | 2 | 2 | 5 | - | [Comparator](./Comparator.md) |

### 采集 (1)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.ImageAcquisition` | 图像采集 | 0 | 1 | 6 | - | [ImageAcquisition](./ImageAcquisition.md) |

### 预处理 (19)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.AdaptiveThreshold` | 自适应阈值 | 1 | 1 | 5 | - | [AdaptiveThreshold](./AdaptiveThreshold.md) |
| `OperatorType.BilateralFilter` | 双边滤波 | 1 | 1 | 3 | - | [BilateralFilter](./BilateralFilter.md) |
| `OperatorType.ClaheEnhancement` | CLAHE增强 | 1 | 1 | 4 | - | [ClaheEnhancement](./ClaheEnhancement.md) |
| `OperatorType.ColorConversion` | 颜色空间转换 | 1 | 1 | 1 | - | [ColorConversion](./ColorConversion.md) |
| `OperatorType.FrameAveraging` | 帧平均 | 1 | 2 | 2 | - | [FrameAveraging](./FrameAveraging.md) |
| `OperatorType.HistogramEqualization` | 直方图均衡化 | 1 | 1 | 3 | - | [HistogramEqualization](./HistogramEqualization.md) |
| `OperatorType.ImageAdd` | 图像加法 | 2 | 1 | 3 | - | [ImageAdd](./ImageAdd.md) |
| `OperatorType.ImageBlend` | 图像融合 | 2 | 1 | 3 | - | [ImageBlend](./ImageBlend.md) |
| `OperatorType.ImageCrop` | 图像裁剪 | 1 | 1 | 4 | - | [ImageCrop](./ImageCrop.md) |
| `OperatorType.ImageDiff` | 图像对比 | 2 | 2 | 0 | - | [ImageDiff](./ImageDiff.md) |
| `OperatorType.ImageNormalize` | 图像归一化 | 1 | 1 | 3 | - | [ImageNormalize](./ImageNormalize.md) |
| `OperatorType.ImageResize` | 图像缩放 | 1 | 1 | 5 | - | [ImageResize](./ImageResize.md) |
| `OperatorType.ImageRotate` | 图像旋转 | 1 | 1 | 5 | - | [ImageRotate](./ImageRotate.md) |
| `OperatorType.LaplacianSharpen` | 拉普拉斯锐化 | 1 | 1 | 3 | - | [LaplacianSharpen](./LaplacianSharpen.md) |
| `OperatorType.MeanFilter` | 均值滤波 | 1 | 1 | 2 | - | [MeanFilter](./MeanFilter.md) |
| `OperatorType.MedianBlur` | 中值滤波 | 1 | 1 | 1 | - | [MedianBlur](./MedianBlur.md) |
| `OperatorType.PerspectiveTransform` | 透视变换 | 1 | 1 | 18 | - | [PerspectiveTransform](./PerspectiveTransform.md) |
| `OperatorType.ShadingCorrection` | 光照校正 | 2 | 1 | 2 | - | [ShadingCorrection](./ShadingCorrection.md) |
| `OperatorType.Thresholding` | 二值化 | 1 | 1 | 4 | - | [Thresholding](./Thresholding.md) |

### 颜色处理 (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|
| `OperatorType.ColorDetection` | 颜色检测 | 1 | 2 | 9 | - | [ColorDetection](./ColorDetection.md) |
| `OperatorType.ColorMeasurement` | 颜色测量 | 2 | 8 | 8 | - | [ColorMeasurement](./ColorMeasurement.md) |
