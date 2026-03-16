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
| `OperatorType.ShapeMatching` | 旋转尺度模板匹配 | 匹配定位 | `1.0.0` |
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

### OperatorType.AffineTransform / 仿射变换
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
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

### OperatorType.ArrayIndexer / 数组索引器
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `14127778F4A0` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `A4ADD286E7D5` |

### OperatorType.BlobAnalysis / Blob分析
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-16T01:00:41.9846479+08:00` | `DD3B35AC2885` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `AA3FBABF31F0` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `399F00274DCD` |

### OperatorType.BlobLabeling / 连通域标注
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `10BC8AC0A2DD` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `CB8C33825ABA` |

### OperatorType.BoxFilter / 候选框过滤 (Bounding Box)
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `E733CF600B7F` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `2E238D831FF0` |

### OperatorType.BoxNms / 候选框抑制
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `96FD137BF2A1` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `C0B7D3338B2D` |

### OperatorType.CalibrationLoader / 标定加载
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `4DE2162FEF55` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `1680B6462051` |

### OperatorType.CaliperTool / 卡尺工具
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `BE0CC297B91B` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `18247D9E6FB3` |

### OperatorType.CameraCalibration / Camera Calibration
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `490769260740` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `C892CA5A86D9` |

### OperatorType.CircleMeasurement / 圆测量
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-16T01:00:41.9846479+08:00` | `0F06DC09DFF2` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `F24C794D0D92` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `D89303A4578F` |

### OperatorType.ClaheEnhancement / CLAHE增强
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-16T01:00:41.9846479+08:00` | `69E533C1A735` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `E6C58B4D7CFF` |

### OperatorType.ColorConversion / 颜色空间转换
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-16T01:00:41.9846479+08:00` | `287716BE3467` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `04C479C51518` |

### OperatorType.ColorDetection / 颜色检测
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-16T01:00:41.9846479+08:00` | `423D33D479AE` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `BBD0B5A93508` |
| `1.0.0` | `2026-03-04T10:35:29.6469155+08:00` | `46BDCCBEBC34` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `47E27D2BD6CF` |

### OperatorType.ColorMeasurement / 颜色测量
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
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

### OperatorType.ContourDetection / 轮廓检测
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `D73488938EDD` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `62F866A1DD98` |

### OperatorType.CopyMakeBorder / 边界填充
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `EA1319B3DDD0` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `D52A1162A333` |

### OperatorType.CornerDetection / 角点检测
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `8FC52A647500` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `8CB17BD12666` |

### OperatorType.DeepLearning / 深度学习
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
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

### OperatorType.EdgeDetection / Edge Detection
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-16T01:00:41.9846479+08:00` | `2DFA437FCD6B` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `3635CC3533DC` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `D8DB44452BC2` |

### OperatorType.EdgeIntersection / 边线交点
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `25C7003F1EBD` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `49D076E880F2` |

### OperatorType.EdgePairDefect / 边缘对缺陷
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `7FC4BA140142` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `81CCA70F0BDE` |

### OperatorType.FrameAveraging / 帧平均
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `2DF9846480B8` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `66F873796264` |

### OperatorType.GapMeasurement / 间隙测量
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `81FC48BD52D5` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `EC1A78180B57` |

### OperatorType.GeoMeasurement / 几何测量
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `20AC7D63887E` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `8F4ECDF2968D` |

### OperatorType.GeometricFitting / Geometric Fitting
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-16T19:59:19.7031372+08:00` | `8DF53D931DAF` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `8C22B86DA1AA` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `5F9717089B34` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `911B9BC4CEF9` |

### OperatorType.GeometricTolerance / 几何公差
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `9BDF9252DECC` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `CC1399C0B8AD` |

### OperatorType.GradientShapeMatch / 梯度形状匹配
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-16T19:59:19.7031372+08:00` | `6E00E64E04AA` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `BE1DD761D410` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `C4917CE7BE0A` |

### OperatorType.HistogramAnalysis / 直方图分析
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `8B682154A78D` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `A33C328B07D8` |

### OperatorType.HttpRequest / HTTP 请求
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-16T01:00:41.9846479+08:00` | `FEE9B353B7DD` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `5329BD29F5B3` |

### OperatorType.ImageAcquisition / 图像采集
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
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
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `023726942CCB` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `D087BD94D51F` |

### OperatorType.ImageNormalize / 图像归一化
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `EAC81104D863` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `4510C4122028` |

### OperatorType.ImageStitching / 图像拼接
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `6E49A6E1A982` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `648EE37D0E05` |

### OperatorType.ImageSubtract / Image Subtract
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `C55253F2EBBF` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `01B4C5663412` |

### OperatorType.ImageTiling / 图像切片
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `98FA118539D1` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `E5252D9D290F` |

### OperatorType.LineLineDistance / 线线距离
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `6218C76C8208` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `C4197D651346` |

### OperatorType.MeanFilter / 均值滤波
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `DD0BC13363D3` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `ED740E081606` |

### OperatorType.MitsubishiMcCommunication / 三菱MC通信
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-02-27T21:39:45.2435118+08:00` | `6DA73E6E8D2B` |
| `1.0.0` | `2026-02-27T21:13:09.9008744+08:00` | `6FCA4034036C` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `5CBB32DC2CED` |

### OperatorType.MorphologicalOperation / Morphological Operation
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `F4895E5C1243` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `C4E6D9300B08` |

### OperatorType.Morphology / Morphology (Legacy)
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `D68EB476BBEF` |
| `1.0.0` | `2026-02-27T09:08:37.3873065+08:00` | `3161073CD194` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `7D8DED829483` |

### OperatorType.MqttPublish / MQTT 发布
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-16T19:59:19.7031372+08:00` | `EDF3937B18BD` |
| `1.0.0` | `2026-03-16T01:00:41.9846479+08:00` | `FFCB190578C7` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `7F475495E079` |

### OperatorType.NPointCalibration / N点标定
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `3DF5A204BEE4` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `0B9DBE2431AE` |

### OperatorType.OmronFinsCommunication / 欧姆龙FINS通信
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-02-27T21:39:45.2435118+08:00` | `6041B1ADB2F0` |
| `1.0.0` | `2026-02-27T21:13:09.9008744+08:00` | `63542A557B6C` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `DC5EBDDA2BD2` |

### OperatorType.OrbFeatureMatch / ORB特征匹配
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `AE7C4204010A` |
| `1.0.0` | `2026-03-04T10:35:29.6469155+08:00` | `024F1AD3150C` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `260BD860C2E1` |

### OperatorType.ParallelLineFind / 平行线查找
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `D67465BA6D4D` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `26F9114FF5A8` |

### OperatorType.PerspectiveTransform / 透视变换
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-02-27T09:08:37.3873065+08:00` | `683816ED05A1` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `386962DF7E0B` |

### OperatorType.PixelStatistics / 像素统计
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `82DC78427BAF` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `4EB06D674A3D` |

### OperatorType.PointAlignment / 点位对齐
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `6732C97572C2` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `E4A8E6F775A9` |

### OperatorType.PointCorrection / 点位修正
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `324DE476C870` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `815617758954` |

### OperatorType.PointLineDistance / 点线距离
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `FCE5924A5358` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `EB16F20EF164` |

### OperatorType.PointSetTool / 点集工具
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `C86961A6A036` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `32673C56E0B9` |

### OperatorType.PolarUnwrap / 极坐标展开
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `886EAF34FEA7` |
| `1.0.0` | `2026-02-27T09:08:37.3873065+08:00` | `AC99929E34C4` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `688C288F636E` |

### OperatorType.PositionCorrection / 位置修正
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
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
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `B4D7EBCD05FB` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `1CCDD43E1B39` |

### OperatorType.RectangleDetection / 矩形检测
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `D1603F6CA019` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `FBC9E5DEC5C4` |

### OperatorType.ResultOutput / 结果输出
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-16T19:59:19.7031372+08:00` | `BECDD0398F2A` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `ED19595D838D` |
| `1.0.0` | `2026-03-04T11:07:12.6855371+08:00` | `F230E925DC3A` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `CD53E822B204` |

### OperatorType.ScriptOperator / 脚本算子
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `5FFF3B6EEC51` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `C36E4CDE7016` |

### OperatorType.ShadingCorrection / 光照校正
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `D979701EF91A` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `4A014AD67BA3` |

### OperatorType.ShapeMatching / 旋转尺度模板匹配
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-16T19:59:19.7031372+08:00` | `35D41D58EBB1` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `ADE360463FD2` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `C20A8A850891` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `16DA32089F01` |

### OperatorType.SharpnessEvaluation / 清晰度评估
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `1FF3E760D01C` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `3362EBCD01BB` |

### OperatorType.SiemensS7Communication / 西门子S7通信
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-02-27T21:39:45.2435118+08:00` | `42E5C6F8C21C` |
| `1.0.0` | `2026-02-27T21:13:09.9008744+08:00` | `DD54339521A8` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `8AB237E58691` |

### OperatorType.Statistics / Statistics
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `4E2FDAE0D791` |
| `1.0.0` | `2026-02-27T09:08:37.3873065+08:00` | `284782E31077` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `332ECE5D2E91` |

### OperatorType.SubpixelEdgeDetection / Subpixel Edge Detection
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `A3FB6B396DF1` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `8A3328983212` |

### OperatorType.SurfaceDefectDetection / 表面缺陷检测
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `77EFD328EF95` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `02BD406438C7` |

### OperatorType.TemplateMatching / 模板匹配
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-16T19:59:19.7031372+08:00` | `2FD9DB94E474` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `30BE4FBE1B26` |
| `1.0.0` | `2026-03-04T10:35:29.6469155+08:00` | `9D6ABB27BF04` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `19F60BB66DE8` |

### OperatorType.TextSave / Text Save
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `BD1CB4079308` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `23E51E9AEF58` |

### OperatorType.TimerStatistics / 计时统计
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `5AE68CD325A0` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `606B1D386D2B` |

### OperatorType.TranslationRotationCalibration / 平移旋转标定
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `994AF95A3442` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `62D1609B1CCB` |

### OperatorType.TriggerModule / 触发模块
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-16T19:59:19.7031372+08:00` | `A2BBEE56F7C4` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `757078C7423F` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `4325D6D6D8A0` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `8EBA52E49F78` |

### OperatorType.TryCatch / 异常捕获
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-16T19:59:19.7031372+08:00` | `1C3ACCA39267` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `4063A03C1DD0` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `92086F4126B2` |

### OperatorType.TypeConvert / Type Convert
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-16T19:59:19.7031372+08:00` | `102ADEA5B2B2` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `7B2399167D77` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `8BCC83E53180` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `A92AC259F970` |

### OperatorType.Undistort / Undistort
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `B1CB22CD65A0` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `397F69EB0E6E` |

### OperatorType.UnitConvert / 单位换算
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `F2D5A61A11C9` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `FE7B780A4358` |

### OperatorType.WidthMeasurement / 宽度测量
| 版本 (Version) | 记录时间 (Recorded At) | 源码摘要 (Source Hash) |
|------|------|------|
| `1.0.0` | `2026-03-16T19:59:19.7031372+08:00` | `7D994B459340` |
| `1.0.0` | `2026-03-15T14:24:43.1972535+08:00` | `175A335805B8` |
| `1.0.0` | `2026-03-04T19:17:03.2031512+08:00` | `BBDEE390601A` |
| `1.0.0` | `2026-02-26T21:18:02.8071504+08:00` | `801058F2953A` |
