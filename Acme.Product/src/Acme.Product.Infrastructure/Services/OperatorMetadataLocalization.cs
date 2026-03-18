// OperatorMetadataLocalization.cs
// 算子元数据本地化
// 提供算子元数据的本地化映射与文本转换
// 作者：蘅芜君
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// Restores legacy UI-facing display names/categories while runtime metadata is sourced from attributes.
/// </summary>
internal static class OperatorMetadataLocalization
{
    private static readonly IReadOnlyDictionary<OperatorType, LocalizedMetadata> LegacyDisplayMap =
        new Dictionary<OperatorType, LocalizedMetadata>
        {
            [OperatorType.AdaptiveThreshold] = new("自适应阈值", "预处理"),
            [OperatorType.AffineTransform] = new("仿射变换", "图像处理"),
            [OperatorType.Aggregator] = new("数据聚合", "数据处理"),
            [OperatorType.AkazeFeatureMatch] = new("AKAZE特征匹配", "匹配定位"),
            [OperatorType.AngleMeasurement] = new("角度测量", "检测"),
            [OperatorType.ArrayIndexer] = new("数组索引器", "数据处理"),
            [OperatorType.BilateralFilter] = new("双边滤波", "预处理"),
            [OperatorType.BlobAnalysis] = new("Blob分析", "特征提取"),
            [OperatorType.BlobLabeling] = new("连通域标注", "定位"),
            [OperatorType.BoxFilter] = new("候选框筛选", "数据处理"),
            [OperatorType.BoxNms] = new("候选框抑制", "数据处理"),
            [OperatorType.CalibrationLoader] = new("标定加载", "标定"),
            [OperatorType.CaliperTool] = new("卡尺工具", "检测"),
            [OperatorType.CameraCalibration] = new("相机标定", "标定"),
            [OperatorType.CircleMeasurement] = new("圆测量", "检测"),
            [OperatorType.ClaheEnhancement] = new("CLAHE增强", "预处理"),
            [OperatorType.CodeRecognition] = new("条码识别", "识别"),
            [OperatorType.ColorConversion] = new("颜色空间转换", "预处理"),
            [OperatorType.ColorDetection] = new("颜色检测", "颜色处理"),
            [OperatorType.ColorMeasurement] = new("颜色测量", "颜色处理"),
            [OperatorType.Comment] = new("注释", "辅助"),
            [OperatorType.Comparator] = new("数值比较", "逻辑控制"),
            [OperatorType.ConditionalBranch] = new("条件分支", "控制"),
            [OperatorType.ContourDetection] = new("轮廓检测", "特征提取"),
            [OperatorType.ContourMeasurement] = new("轮廓测量", "检测"),
            [OperatorType.CoordinateTransform] = new("坐标转换", "标定"),
            [OperatorType.CopyMakeBorder] = new("边界填充", "图像处理"),
            [OperatorType.CornerDetection] = new("角点检测", "定位"),
            [OperatorType.CycleCounter] = new("循环计数器", "变量"),
            [OperatorType.DatabaseWrite] = new("数据库写入", "数据"),
            [OperatorType.DeepLearning] = new("深度学习", "AI检测"),
            [OperatorType.Delay] = new("延时", "流程控制"),
            [OperatorType.DualModalVoting] = new("双模态投票", "AI检测"),
            [OperatorType.EdgeDetection] = new("边缘检测", "特征提取"),
            [OperatorType.EdgeIntersection] = new("边线交点", "定位"),
            [OperatorType.EdgePairDefect] = new("边缘对缺陷", "AI检测"),
            [OperatorType.Filtering] = new("滤波", "预处理"),
            [OperatorType.FisheyeCalibration] = new("鱼眼标定", "标定"),
            [OperatorType.FisheyeUndistort] = new("鱼眼去畸变", "标定"),
            [OperatorType.ForEach] = new("ForEach 循环", "流程控制"),
            [OperatorType.FrameAveraging] = new("帧平均", "预处理"),
            [OperatorType.FrequencyFilter] = new("频域滤波", "频域"),
            [OperatorType.GapMeasurement] = new("间隙测量", "检测"),
            [OperatorType.GeoMeasurement] = new("几何测量", "检测"),
            [OperatorType.GeometricFitting] = new("几何拟合", "测量"),
            [OperatorType.GeometricTolerance] = new("几何公差", "检测"),
            [OperatorType.GradientShapeMatch] = new("梯度形状匹配", "匹配定位"),
            [OperatorType.HistogramAnalysis] = new("直方图分析", "检测"),
            [OperatorType.HistogramEqualization] = new("直方图均衡化", "预处理"),
            [OperatorType.HttpRequest] = new("HTTP 请求", "通信"),
            [OperatorType.ImageAcquisition] = new("图像采集", "采集"),
            [OperatorType.ImageAdd] = new("图像加法", "预处理"),
            [OperatorType.ImageBlend] = new("图像融合", "预处理"),
            [OperatorType.ImageCompose] = new("图像组合", "拆分组合"),
            [OperatorType.ImageCrop] = new("图像裁剪", "预处理"),
            [OperatorType.ImageDiff] = new("图像对比", "预处理"),
            [OperatorType.ImageNormalize] = new("图像归一化", "预处理"),
            [OperatorType.ImageResize] = new("图像缩放", "预处理"),
            [OperatorType.ImageRotate] = new("图像旋转", "预处理"),
            [OperatorType.ImageSave] = new("图像保存", "输出"),
            [OperatorType.ImageStitching] = new("图像拼接", "图像处理"),
            [OperatorType.ImageSubtract] = new("图像减法", "预处理"),
            [OperatorType.ImageTiling] = new("图像切片", "拆分组合"),
            [OperatorType.InverseFFT1D] = new("一维逆FFT", "频域"),
            [OperatorType.JsonExtractor] = new("JSON 提取器", "数据处理"),
            [OperatorType.LaplacianSharpen] = new("拉普拉斯锐化", "预处理"),
            [OperatorType.LineLineDistance] = new("线线距离", "检测"),
            [OperatorType.LineMeasurement] = new("直线测量", "检测"),
            [OperatorType.LocalDeformableMatching] = new("局部可变形匹配", "匹配定位"),
            [OperatorType.LogicGate] = new("逻辑门", "通用"),
            [OperatorType.MathOperation] = new("数值计算", "数据处理"),
            [OperatorType.MeanFilter] = new("均值滤波", "预处理"),
            [OperatorType.Measurement] = new("测量", "检测"),
            [OperatorType.MedianBlur] = new("中值滤波", "预处理"),
            [OperatorType.MitsubishiMcCommunication] = new("三菱MC通信", "通信"),
            [OperatorType.MinEnclosingGeometry] = new("最小外接几何体", "测量"),
            [OperatorType.ModbusCommunication] = new("Modbus通信", "通信"),
            [OperatorType.MorphologicalOperation] = new("形态学操作", "预处理"),
            [OperatorType.Morphology] = new("Morphology (Legacy)", "预处理"),
            [OperatorType.MqttPublish] = new("MQTT 发布", "通信"),
            [OperatorType.NPointCalibration] = new("N点标定", "标定"),
            [OperatorType.OcrRecognition] = new("OCR 识别", "识别"),
            [OperatorType.OmronFinsCommunication] = new("欧姆龙FINS通信", "通信"),
            [OperatorType.OrbFeatureMatch] = new("ORB特征匹配", "匹配定位"),
            [OperatorType.ParallelLineFind] = new("平行线查找", "定位"),
            [OperatorType.PhaseClosure] = new("相位解缠绕", "测量"),
            [OperatorType.PerspectiveTransform] = new("透视变换", "预处理"),
            [OperatorType.PixelStatistics] = new("像素统计", "检测"),
            [OperatorType.PixelToWorldTransform] = new("像素世界映射", "标定"),
            [OperatorType.PointAlignment] = new("点位对齐", "数据处理"),
            [OperatorType.PointCorrection] = new("点位修正", "数据处理"),
            [OperatorType.PointLineDistance] = new("点线距离", "检测"),
            [OperatorType.PointSetTool] = new("点集工具", "逻辑工具"),
            [OperatorType.PolarUnwrap] = new("极坐标展开", "图像处理"),
            [OperatorType.PlanarMatching] = new("透视匹配", "匹配定位"),
            [OperatorType.PositionCorrection] = new("位置修正", "定位"),
            [OperatorType.PyramidShapeMatch] = new("金字塔形状匹配", "匹配定位"),
            [OperatorType.QuadrilateralFind] = new("四边形查找", "定位"),
            [OperatorType.RectangleDetection] = new("矩形检测", "定位"),
            [OperatorType.RegionClosing] = new("区域闭运算", "区域处理"),
            [OperatorType.RegionComplement] = new("区域补集", "区域处理"),
            [OperatorType.RegionDifference] = new("区域差集", "区域处理"),
            [OperatorType.RegionDilation] = new("区域膨胀", "区域处理"),
            [OperatorType.RegionErosion] = new("区域腐蚀", "区域处理"),
            [OperatorType.RegionIntersection] = new("区域交集", "区域处理"),
            [OperatorType.RegionOpening] = new("区域开运算", "区域处理"),
            [OperatorType.RegionSkeleton] = new("区域骨架化", "区域处理"),
            [OperatorType.RegionUnion] = new("区域并集", "区域处理"),
            [OperatorType.ResultJudgment] = new("结果判定", "流程控制"),
            [OperatorType.ResultOutput] = new("结果输出", "输出"),
            [OperatorType.RoiManager] = new("ROI管理器", "辅助"),
            [OperatorType.ScriptOperator] = new("脚本算子", "逻辑工具"),
            [OperatorType.SerialCommunication] = new("串口通信", "通信"),
            [OperatorType.ShadingCorrection] = new("光照校正", "预处理"),
            [OperatorType.ShapeMatching] = new("旋转尺度模板匹配", "匹配定位"),
            [OperatorType.SharpnessEvaluation] = new("清晰度评估", "检测"),
            [OperatorType.SiemensS7Communication] = new("西门子S7通信", "通信"),
            [OperatorType.StereoCalibration] = new("双目标定", "标定"),
            [OperatorType.Statistics] = new("统计分析", "通用"),
            [OperatorType.StringFormat] = new("字符串格式化", "通用"),
            [OperatorType.SubpixelEdgeDetection] = new("亚像素边缘", "颜色处理"),
            [OperatorType.SurfaceDefectDetection] = new("表面缺陷检测", "AI检测"),
            [OperatorType.TcpCommunication] = new("TCP通信", "通信"),
            [OperatorType.TemplateMatching] = new("模板匹配", "匹配定位"),
            [OperatorType.TextSave] = new("文本保存", "逻辑工具"),
            [OperatorType.Thresholding] = new("二值化", "预处理"),
            [OperatorType.TimerStatistics] = new("计时统计", "逻辑工具"),
            [OperatorType.TranslationRotationCalibration] = new("平移旋转标定", "标定"),
            [OperatorType.TriggerModule] = new("触发模块", "逻辑工具"),
            [OperatorType.TryCatch] = new("异常捕获", "流程控制"),
            [OperatorType.TypeConvert] = new("类型转换", "通用"),
            [OperatorType.Undistort] = new("畸变校正", "标定"),
            [OperatorType.UnitConvert] = new("单位换算", "数据处理"),
            [OperatorType.VariableIncrement] = new("变量递增", "变量"),
            [OperatorType.VariableRead] = new("变量读取", "变量"),
            [OperatorType.VariableWrite] = new("变量写入", "变量"),
            [OperatorType.WidthMeasurement] = new("宽度测量", "检测"),
            [OperatorType.ArcCaliper] = new("圆弧卡尺", "检测"),
            [OperatorType.ContourExtrema] = new("轮廓极值", "检测"),
            [OperatorType.DistanceTransform] = new("距离变换", "图像处理"),
            [OperatorType.FFT1D] = new("一维FFT", "频域"),
        };

    public static void Apply(IEnumerable<OperatorMetadata> metadataItems)
    {
        foreach (var metadata in metadataItems)
        {
            if (!LegacyDisplayMap.TryGetValue(metadata.Type, out var localized))
            {
                continue;
            }

            metadata.DisplayName = localized.DisplayName;
            metadata.Category = localized.Category;
        }
    }

    private readonly record struct LocalizedMetadata(string DisplayName, string Category);
}
