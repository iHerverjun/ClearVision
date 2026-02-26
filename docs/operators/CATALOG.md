# 算子目录 / Operator Catalog

> 生成时间 / Generated At: `2026-02-26 21:20:16 +08:00`
> 算子总数 / Total Operators: **118**

## 分类统计 / Category Summary
| 分类 (Category) | 数量 (Count) | 占比 (Ratio) |
|------|------:|------:|
| AI检测 | 4 | 3.4% |
| 匹配定位 | 6 | 5.1% |
| 变量 | 4 | 3.4% |
| 图像处理 | 4 | 3.4% |
| 定位 | 7 | 5.9% |
| 拆分组合 | 2 | 1.7% |
| 数据处理 | 10 | 8.5% |
| 标定 | 6 | 5.1% |
| 检测 | 16 | 13.6% |
| 流程控制 | 6 | 5.1% |
| 特征提取 | 4 | 3.4% |
| 识别 | 2 | 1.7% |
| 辅助 | 2 | 1.7% |
| 输出 | 2 | 1.7% |
| 通信 | 8 | 6.8% |
| 通用 | 4 | 3.4% |
| 逻辑工具 | 5 | 4.2% |
| 采集 | 1 | 0.8% |
| 预处理 | 23 | 19.5% |
| 颜色处理 | 2 | 1.7% |

## 质量评分 / Quality Score
- 平均分 / Average: **88.2**
| 等级 (Level) | 数量 (Count) |
|------|------:|
| A | 76 |
| B | 35 |
| C | 7 |

## 分类索引 / Grouped Index

### AI检测 (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.DeepLearning` | 深度学习 | 1 | 3 | 6 | 100 (A) | `1.0.0` | - | [DeepLearning](./DeepLearning.md) |
| `OperatorType.DualModalVoting` | 双模态投票 | 2 | 3 | 6 | 90 (A) | `1.0.0` | - | [DualModalVoting](./DualModalVoting.md) |
| `OperatorType.EdgePairDefect` | 边缘对缺陷 | 3 | 4 | 4 | 94 (A) | `1.0.0` | - | [EdgePairDefect](./EdgePairDefect.md) |
| `OperatorType.SurfaceDefectDetection` | 表面缺陷检测 | 2 | 4 | 5 | 94 (A) | `1.0.0` | - | [SurfaceDefectDetection](./SurfaceDefectDetection.md) |

### 匹配定位 (6)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.AkazeFeatureMatch` | AKAZE特征匹配 | 2 | 4 | 5 | 73 (B) | `1.0.0` | - | [AkazeFeatureMatch](./AkazeFeatureMatch.md) |
| `OperatorType.GradientShapeMatch` | 梯度形状匹配 | 2 | 5 | 5 | 83 (B) | `1.0.0` | - | [GradientShapeMatch](./GradientShapeMatch.md) |
| `OperatorType.OrbFeatureMatch` | ORB特征匹配 | 2 | 4 | 5 | 73 (B) | `1.0.0` | - | [OrbFeatureMatch](./OrbFeatureMatch.md) |
| `OperatorType.PyramidShapeMatch` | 金字塔形状匹配 | 2 | 5 | 5 | 83 (B) | `1.0.0` | - | [PyramidShapeMatch](./PyramidShapeMatch.md) |
| `OperatorType.ShapeMatching` | Shape Matching | 2 | 2 | 7 | 100 (A) | `1.0.0` | - | [ShapeMatching](./ShapeMatching.md) |
| `OperatorType.TemplateMatching` | 模板匹配 | 2 | 4 | 3 | 96 (A) | `1.0.0` | - | [TemplateMatching](./TemplateMatching.md) |

### 变量 (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.CycleCounter` | 循环计数器 | 0 | 5 | 2 | 66 (C) | `1.0.0` | - | [CycleCounter](./CycleCounter.md) |
| `OperatorType.VariableIncrement` | 变量递增 | 0 | 5 | 5 | 73 (B) | `1.0.0` | - | [VariableIncrement](./VariableIncrement.md) |
| `OperatorType.VariableRead` | 变量读取 | 0 | 3 | 3 | 73 (B) | `1.0.0` | - | [VariableRead](./VariableRead.md) |
| `OperatorType.VariableWrite` | 变量写入 | 1 | 3 | 4 | 63 (C) | `1.0.0` | - | [VariableWrite](./VariableWrite.md) |

### 图像处理 (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.AffineTransform` | 仿射变换 | 1 | 2 | 9 | 100 (A) | `1.0.0` | - | [AffineTransform](./AffineTransform.md) |
| `OperatorType.CopyMakeBorder` | 边界填充 | 1 | 1 | 6 | 94 (A) | `1.0.0` | - | [CopyMakeBorder](./CopyMakeBorder.md) |
| `OperatorType.ImageStitching` | 图像拼接 | 2 | 2 | 3 | 94 (A) | `1.0.0` | - | [ImageStitching](./ImageStitching.md) |
| `OperatorType.PolarUnwrap` | 极坐标展开 | 2 | 1 | 7 | 96 (A) | `1.0.0` | - | [PolarUnwrap](./PolarUnwrap.md) |

### 定位 (7)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.BlobLabeling` | 连通域标注 | 2 | 3 | 3 | 100 (A) | `1.0.0` | - | [BlobLabeling](./BlobLabeling.md) |
| `OperatorType.CornerDetection` | 角点检测 | 1 | 3 | 5 | 94 (A) | `1.0.0` | - | [CornerDetection](./CornerDetection.md) |
| `OperatorType.EdgeIntersection` | 边线交点 | 2 | 3 | 0 | 89 (A) | `1.0.0` | - | [EdgeIntersection](./EdgeIntersection.md) |
| `OperatorType.ParallelLineFind` | 平行线查找 | 1 | 6 | 4 | 94 (A) | `1.0.0` | - | [ParallelLineFind](./ParallelLineFind.md) |
| `OperatorType.PositionCorrection` | 位置修正 | 4 | 5 | 3 | 94 (A) | `1.0.0` | - | [PositionCorrection](./PositionCorrection.md) |
| `OperatorType.QuadrilateralFind` | 四边形查找 | 1 | 5 | 4 | 94 (A) | `1.0.0` | - | [QuadrilateralFind](./QuadrilateralFind.md) |
| `OperatorType.RectangleDetection` | 矩形检测 | 1 | 7 | 4 | 94 (A) | `1.0.0` | - | [RectangleDetection](./RectangleDetection.md) |

### 拆分组合 (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.ImageCompose` | 图像组合 | 4 | 1 | 3 | 94 (A) | `1.0.0` | - | [ImageCompose](./ImageCompose.md) |
| `OperatorType.ImageTiling` | 图像切片 | 1 | 3 | 4 | 94 (A) | `1.0.0` | - | [ImageTiling](./ImageTiling.md) |

### 数据处理 (10)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.Aggregator` | 数据聚合 | 3 | 4 | 1 | 61 (C) | `1.0.0` | - | [Aggregator](./Aggregator.md) |
| `OperatorType.ArrayIndexer` | 数组索引器 | 1 | 1 | 2 | 79 (B) | `1.0.0` | - | [ArrayIndexer](./ArrayIndexer.md) |
| `OperatorType.BoxFilter` | 候选框过滤 (Bounding Box) | 2 | 3 | 9 | 90 (A) | `1.0.0` | - | [BoxFilter](./BoxFilter.md) |
| `OperatorType.BoxNms` | 候选框抑制 | 2 | 3 | 3 | 90 (A) | `1.0.0` | - | [BoxNms](./BoxNms.md) |
| `OperatorType.DatabaseWrite` | 数据库写入 | 1 | 2 | 3 | 100 (A) | `1.0.0` | - | [DatabaseWrite](./DatabaseWrite.md) |
| `OperatorType.JsonExtractor` | JSON 提取器 | 1 | 2 | 1 | 83 (B) | `1.0.0` | - | [JsonExtractor](./JsonExtractor.md) |
| `OperatorType.MathOperation` | 数值计算 | 2 | 2 | 1 | 83 (B) | `1.0.0` | - | [MathOperation](./MathOperation.md) |
| `OperatorType.PointAlignment` | 点位对齐 | 2 | 3 | 2 | 94 (A) | `1.0.0` | - | [PointAlignment](./PointAlignment.md) |
| `OperatorType.PointCorrection` | 点位修正 | 4 | 4 | 3 | 94 (A) | `1.0.0` | - | [PointCorrection](./PointCorrection.md) |
| `OperatorType.UnitConvert` | 单位换算 | 2 | 2 | 4 | 96 (A) | `1.0.0` | - | [UnitConvert](./UnitConvert.md) |

### 标定 (6)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.CalibrationLoader` | 标定加载 | 0 | 4 | 2 | 100 (A) | `1.0.0` | - | [CalibrationLoader](./CalibrationLoader.md) |
| `OperatorType.CameraCalibration` | Camera Calibration | 1 | 2 | 7 | 100 (A) | `1.0.0` | - | [CameraCalibration](./CameraCalibration.md) |
| `OperatorType.CoordinateTransform` | 坐标转换 | 3 | 3 | 4 | 100 (A) | `1.0.0` | - | [CoordinateTransform](./CoordinateTransform.md) |
| `OperatorType.NPointCalibration` | N点标定 | 1 | 3 | 3 | 100 (A) | `1.0.0` | - | [NPointCalibration](./NPointCalibration.md) |
| `OperatorType.TranslationRotationCalibration` | 平移旋转标定 | 1 | 3 | 3 | 100 (A) | `1.0.0` | - | [TranslationRotationCalibration](./TranslationRotationCalibration.md) |
| `OperatorType.Undistort` | Undistort | 2 | 1 | 1 | 95 (A) | `1.0.0` | - | [Undistort](./Undistort.md) |

### 检测 (16)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.AngleMeasurement` | 角度测量 | 1 | 2 | 7 | 94 (A) | `1.0.0` | - | [AngleMeasurement](./AngleMeasurement.md) |
| `OperatorType.CaliperTool` | 卡尺工具 | 2 | 4 | 6 | 94 (A) | `1.0.0` | - | [CaliperTool](./CaliperTool.md) |
| `OperatorType.CircleMeasurement` | 圆测量 | 1 | 5 | 7 | 100 (A) | `1.0.0` | - | [CircleMeasurement](./CircleMeasurement.md) |
| `OperatorType.ContourMeasurement` | 轮廓测量 | 1 | 4 | 4 | 94 (A) | `1.0.0` | - | [ContourMeasurement](./ContourMeasurement.md) |
| `OperatorType.GapMeasurement` | 间隙测量 | 2 | 6 | 4 | 94 (A) | `1.0.0` | - | [GapMeasurement](./GapMeasurement.md) |
| `OperatorType.GeoMeasurement` | 几何测量 | 2 | 5 | 2 | 94 (A) | `1.0.0` | - | [GeoMeasurement](./GeoMeasurement.md) |
| `OperatorType.GeometricFitting` | Geometric Fitting | 1 | 2 | 7 | 100 (A) | `1.0.0` | - | [GeometricFitting](./GeometricFitting.md) |
| `OperatorType.GeometricTolerance` | 几何公差 | 1 | 5 | 9 | 94 (A) | `1.0.0` | - | [GeometricTolerance](./GeometricTolerance.md) |
| `OperatorType.HistogramAnalysis` | 直方图分析 | 1 | 7 | 6 | 94 (A) | `1.0.0` | - | [HistogramAnalysis](./HistogramAnalysis.md) |
| `OperatorType.LineLineDistance` | 线线距离 | 2 | 5 | 1 | 96 (A) | `1.0.0` | - | [LineLineDistance](./LineLineDistance.md) |
| `OperatorType.LineMeasurement` | 直线测量 | 1 | 5 | 4 | 94 (A) | `1.0.0` | - | [LineMeasurement](./LineMeasurement.md) |
| `OperatorType.Measurement` | 测量 | 3 | 2 | 5 | 94 (A) | `1.0.0` | - | [Measurement](./Measurement.md) |
| `OperatorType.PixelStatistics` | 像素统计 | 2 | 6 | 5 | 94 (A) | `1.0.0` | - | [PixelStatistics](./PixelStatistics.md) |
| `OperatorType.PointLineDistance` | 点线距离 | 2 | 2 | 0 | 91 (A) | `1.0.0` | - | [PointLineDistance](./PointLineDistance.md) |
| `OperatorType.SharpnessEvaluation` | 清晰度评估 | 1 | 3 | 6 | 94 (A) | `1.0.0` | - | [SharpnessEvaluation](./SharpnessEvaluation.md) |
| `OperatorType.WidthMeasurement` | 宽度测量 | 3 | 4 | 3 | 96 (A) | `1.0.0` | - | [WidthMeasurement](./WidthMeasurement.md) |

### 流程控制 (6)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.Comparator` | 数值比较 | 2 | 2 | 5 | 61 (C) | `1.0.0` | - | [Comparator](./Comparator.md) |
| `OperatorType.ConditionalBranch` | 条件分支 | 1 | 2 | 3 | 90 (A) | `1.0.0` | - | [ConditionalBranch](./ConditionalBranch.md) |
| `OperatorType.Delay` | 延时 | 1 | 2 | 1 | 61 (C) | `1.0.0` | - | [Delay](./Delay.md) |
| `OperatorType.ForEach` | ForEach 循环 | 1 | 1 | 4 | 83 (B) | `1.0.0` | - | [ForEach](./ForEach.md) |
| `OperatorType.ResultJudgment` | 结果判定 | 2 | 3 | 8 | 66 (C) | `1.0.0` | - | [ResultJudgment](./ResultJudgment.md) |
| `OperatorType.TryCatch` | 异常捕获 | 1 | 4 | 3 | 75 (B) | `1.0.0` | - | [TryCatch](./TryCatch.md) |

### 特征提取 (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.BlobAnalysis` | Blob分析 | 1 | 3 | 3 | 94 (A) | `1.0.0` | - | [BlobAnalysis](./BlobAnalysis.md) |
| `OperatorType.ContourDetection` | 轮廓检测 | 1 | 3 | 5 | 94 (A) | `1.0.0` | - | [ContourDetection](./ContourDetection.md) |
| `OperatorType.EdgeDetection` | Edge Detection | 1 | 2 | 7 | 76 (B) | `1.0.0` | - | [EdgeDetection](./EdgeDetection.md) |
| `OperatorType.SubpixelEdgeDetection` | Subpixel Edge Detection | 1 | 2 | 5 | 94 (A) | `1.0.0` | - | [SubpixelEdgeDetection](./SubpixelEdgeDetection.md) |

### 识别 (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.CodeRecognition` | 条码识别 | 1 | 4 | 2 | 94 (A) | `1.0.0` | - | [CodeRecognition](./CodeRecognition.md) |
| `OperatorType.OcrRecognition` | OCR 识别 | 1 | 2 | 0 | 100 (A) | `1.0.0` | - | [OcrRecognition](./OcrRecognition.md) |

### 辅助 (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.Comment` | 注释 | 1 | 2 | 1 | 61 (C) | `1.0.0` | - | [Comment](./Comment.md) |
| `OperatorType.RoiManager` | ROI管理器 | 1 | 2 | 10 | 100 (A) | `1.0.0` | - | [RoiManager](./RoiManager.md) |

### 输出 (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.ImageSave` | 图像保存 | 1 | 2 | 3 | 83 (B) | `1.0.0` | - | [ImageSave](./ImageSave.md) |
| `OperatorType.ResultOutput` | 结果输出 | 4 | 1 | 2 | 79 (B) | `1.0.0` | - | [ResultOutput](./ResultOutput.md) |

### 通信 (8)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.HttpRequest` | HTTP 请求 | 1 | 3 | 4 | 83 (B) | `1.0.0` | - | [HttpRequest](./HttpRequest.md) |
| `OperatorType.MitsubishiMcCommunication` | 三菱MC通信 | 1 | 2 | 12 | 80 (B) | `1.0.0` | - | [MitsubishiMcCommunication](./MitsubishiMcCommunication.md) |
| `OperatorType.ModbusCommunication` | Modbus通信 | 1 | 2 | 8 | 98 (A) | `1.0.0` | - | [ModbusCommunication](./ModbusCommunication.md) |
| `OperatorType.MqttPublish` | MQTT 发布 | 1 | 1 | 4 | 83 (B) | `1.0.0` | - | [MqttPublish](./MqttPublish.md) |
| `OperatorType.OmronFinsCommunication` | 欧姆龙FINS通信 | 1 | 2 | 12 | 80 (B) | `1.0.0` | - | [OmronFinsCommunication](./OmronFinsCommunication.md) |
| `OperatorType.SerialCommunication` | 串口通信 | 1 | 1 | 8 | 83 (B) | `1.0.0` | - | [SerialCommunication](./SerialCommunication.md) |
| `OperatorType.SiemensS7Communication` | 西门子S7通信 | 1 | 2 | 14 | 80 (B) | `1.0.0` | - | [SiemensS7Communication](./SiemensS7Communication.md) |
| `OperatorType.TcpCommunication` | TCP通信 | 1 | 2 | 6 | 98 (A) | `1.0.0` | - | [TcpCommunication](./TcpCommunication.md) |

### 通用 (4)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.LogicGate` | 逻辑门 | 2 | 1 | 1 | 73 (B) | `1.0.0` | - | [LogicGate](./LogicGate.md) |
| `OperatorType.Statistics` | Statistics | 1 | 7 | 4 | 90 (A) | `1.0.0` | - | [Statistics](./Statistics.md) |
| `OperatorType.StringFormat` | 字符串格式化 | 2 | 1 | 1 | 76 (B) | `1.0.0` | - | [StringFormat](./StringFormat.md) |
| `OperatorType.TypeConvert` | Type Convert | 1 | 1 | 2 | 83 (B) | `1.0.0` | - | [TypeConvert](./TypeConvert.md) |

### 逻辑工具 (5)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.PointSetTool` | 点集工具 | 2 | 4 | 6 | 90 (A) | `1.0.0` | - | [PointSetTool](./PointSetTool.md) |
| `OperatorType.ScriptOperator` | 脚本算子 | 4 | 2 | 3 | 100 (A) | `1.0.0` | - | [ScriptOperator](./ScriptOperator.md) |
| `OperatorType.TextSave` | Text Save | 2 | 2 | 5 | 100 (A) | `1.0.0` | - | [TextSave](./TextSave.md) |
| `OperatorType.TimerStatistics` | 计时统计 | 1 | 4 | 2 | 84 (B) | `1.0.0` | - | [TimerStatistics](./TimerStatistics.md) |
| `OperatorType.TriggerModule` | 触发模块 | 0 | 3 | 3 | 84 (B) | `1.0.0` | - | [TriggerModule](./TriggerModule.md) |

### 采集 (1)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.ImageAcquisition` | 图像采集 | 0 | 1 | 6 | 78 (B) | `1.0.0` | - | [ImageAcquisition](./ImageAcquisition.md) |

### 预处理 (23)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.AdaptiveThreshold` | 自适应阈值 | 1 | 1 | 5 | 94 (A) | `1.0.0` | - | [AdaptiveThreshold](./AdaptiveThreshold.md) |
| `OperatorType.BilateralFilter` | 双边滤波 | 1 | 1 | 3 | 94 (A) | `1.0.0` | - | [BilateralFilter](./BilateralFilter.md) |
| `OperatorType.ClaheEnhancement` | CLAHE增强 | 1 | 1 | 4 | 76 (B) | `1.0.0` | - | [ClaheEnhancement](./ClaheEnhancement.md) |
| `OperatorType.ColorConversion` | 颜色空间转换 | 1 | 1 | 1 | 94 (A) | `1.0.0` | - | [ColorConversion](./ColorConversion.md) |
| `OperatorType.Filtering` | Gaussian Blur | 1 | 1 | 4 | 76 (B) | `1.0.0` | Gaussian Blur (OpenCV) | [Filtering](./Filtering.md) |
| `OperatorType.FrameAveraging` | 帧平均 | 1 | 2 | 2 | 94 (A) | `1.0.0` | - | [FrameAveraging](./FrameAveraging.md) |
| `OperatorType.HistogramEqualization` | 直方图均衡化 | 1 | 1 | 3 | 94 (A) | `1.0.0` | - | [HistogramEqualization](./HistogramEqualization.md) |
| `OperatorType.ImageAdd` | 图像加法 | 2 | 1 | 3 | 76 (B) | `1.0.0` | - | [ImageAdd](./ImageAdd.md) |
| `OperatorType.ImageBlend` | 图像融合 | 2 | 1 | 3 | 76 (B) | `1.0.0` | - | [ImageBlend](./ImageBlend.md) |
| `OperatorType.ImageCrop` | 图像裁剪 | 1 | 1 | 4 | 94 (A) | `1.0.0` | - | [ImageCrop](./ImageCrop.md) |
| `OperatorType.ImageDiff` | 图像对比 | 2 | 2 | 0 | 71 (B) | `1.0.0` | - | [ImageDiff](./ImageDiff.md) |
| `OperatorType.ImageNormalize` | 图像归一化 | 1 | 1 | 3 | 94 (A) | `1.0.0` | - | [ImageNormalize](./ImageNormalize.md) |
| `OperatorType.ImageResize` | 图像缩放 | 1 | 1 | 5 | 94 (A) | `1.0.0` | - | [ImageResize](./ImageResize.md) |
| `OperatorType.ImageRotate` | 图像旋转 | 1 | 1 | 5 | 94 (A) | `1.0.0` | - | [ImageRotate](./ImageRotate.md) |
| `OperatorType.ImageSubtract` | Image Subtract | 2 | 4 | 1 | 89 (A) | `1.0.0` | - | [ImageSubtract](./ImageSubtract.md) |
| `OperatorType.LaplacianSharpen` | 拉普拉斯锐化 | 1 | 1 | 3 | 76 (B) | `1.0.0` | - | [LaplacianSharpen](./LaplacianSharpen.md) |
| `OperatorType.MeanFilter` | 均值滤波 | 1 | 1 | 2 | 94 (A) | `1.0.0` | - | [MeanFilter](./MeanFilter.md) |
| `OperatorType.MedianBlur` | 中值滤波 | 1 | 1 | 1 | 94 (A) | `1.0.0` | - | [MedianBlur](./MedianBlur.md) |
| `OperatorType.MorphologicalOperation` | Morphological Operation | 1 | 1 | 7 | 76 (B) | `1.0.0` | - | [MorphologicalOperation](./MorphologicalOperation.md) |
| `OperatorType.Morphology` | Morphology (Legacy) | 1 | 1 | 6 | 94 (A) | `1.0.0` | - | [Morphology](./Morphology.md) |
| `OperatorType.PerspectiveTransform` | 透视变换 | 1 | 1 | 18 | 94 (A) | `1.0.0` | - | [PerspectiveTransform](./PerspectiveTransform.md) |
| `OperatorType.ShadingCorrection` | 光照校正 | 2 | 1 | 2 | 94 (A) | `1.0.0` | - | [ShadingCorrection](./ShadingCorrection.md) |
| `OperatorType.Thresholding` | 二值化 | 1 | 1 | 4 | 76 (B) | `1.0.0` | - | [Thresholding](./Thresholding.md) |

### 颜色处理 (2)
| 枚举 (Enum) | 显示名 (DisplayName) | 输入 | 输出 | 参数 | 质量 (Q) | 版本 (Version) | 算法 (Algorithm) | 文档 |
|------|------|------:|------:|------:|------|------|------|------|
| `OperatorType.ColorDetection` | 颜色检测 | 1 | 2 | 9 | 76 (B) | `1.0.0` | - | [ColorDetection](./ColorDetection.md) |
| `OperatorType.ColorMeasurement` | 颜色测量 | 2 | 8 | 8 | 94 (A) | `1.0.0` | - | [ColorMeasurement](./ColorMeasurement.md) |
