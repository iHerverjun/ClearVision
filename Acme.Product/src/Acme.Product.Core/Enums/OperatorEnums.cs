using System.Text.Json.Serialization;

namespace Acme.Product.Core.Enums;

/// <summary>
/// 算子类型枚举 - 定义所有支持的图像处理算子类型
/// </summary>
public enum OperatorType
{
    /// <summary>
    /// 图像采集 - 从相机或文件获取图像
    /// </summary>
    ImageAcquisition = 0,

    /// <summary>
    /// 预处理 - 图像预处理操作
    /// </summary>
    Preprocessing = 1,

    /// <summary>
    /// 滤波 - 图像滤波降噪
    /// </summary>
    Filtering = 2,

    /// <summary>
    /// 边缘检测 - 检测图像边缘
    /// </summary>
    EdgeDetection = 3,

    /// <summary>
    /// 二值化 - 图像阈值分割
    /// </summary>
    Thresholding = 4,

    /// <summary>
    /// 形态学 - 形态学操作（腐蚀、膨胀等）
    /// </summary>
    Morphology = 5,

    /// <summary>
    /// Blob分析 - 连通区域分析
    /// </summary>
    BlobAnalysis = 6,

    /// <summary>
    /// 模板匹配 - 图像模板匹配
    /// </summary>
    TemplateMatching = 7,

    /// <summary>
    /// 测量 - 几何测量
    /// </summary>
    Measurement = 8,

    /// <summary>
    /// 条码识别 - 一维码/二维码识别
    /// </summary>
    CodeRecognition = 9,

    /// <summary>
    /// 深度学习 - AI缺陷检测
    /// </summary>
    DeepLearning = 10,

    /// <summary>
    /// 结果输出 - 输出检测结果
    /// </summary>
    ResultOutput = 11,

    /// <summary>
    /// 轮廓检测
    /// </summary>
    ContourDetection = 12,

    /// <summary>
    /// 中值滤波 - 图像去噪处理
    /// </summary>
    MedianBlur = 13,

    /// <summary>
    /// 双边滤波 - 边缘保留滤波
    /// </summary>
    BilateralFilter = 14,

    /// <summary>
    /// 图像缩放 - 调整图像尺寸
    /// </summary>
    ImageResize = 15,

    /// <summary>
    /// 图像裁剪 - ROI区域提取
    /// </summary>
    ImageCrop = 16,

    /// <summary>
    /// 图像旋转 - 任意角度旋转
    /// </summary>
    ImageRotate = 17,

    /// <summary>
    /// 透视变换 - 四边形变换
    /// </summary>
    PerspectiveTransform = 18,

    /// <summary>
    /// 圆测量 - 霍夫圆检测与测量
    /// </summary>
    CircleMeasurement = 19,

    /// <summary>
    /// 直线测量 - 霍夫直线检测与测量
    /// </summary>
    LineMeasurement = 20,

    /// <summary>
    /// 轮廓测量 - 轮廓分析与测量
    /// </summary>
    ContourMeasurement = 21,

    /// <summary>
    /// 角度测量 - 基于三点计算角度
    /// </summary>
    AngleMeasurement = 22,

    /// <summary>
    /// 几何公差 - 平行度/垂直度测量
    /// </summary>
    GeometricTolerance = 23,

    /// <summary>
    /// 相机标定 - 棋盘格/圆点标定
    /// </summary>
    CameraCalibration = 24,

    /// <summary>
    /// 畸变校正 - 基于标定数据校正畸变
    /// </summary>
    Undistort = 25,

    /// <summary>
    /// 坐标转换 - 像素到物理坐标转换
    /// </summary>
    CoordinateTransform = 26,

    /// <summary>
    /// Modbus通信 - 工业设备通信
    /// </summary>
    ModbusCommunication = 27,

    /// <summary>
    /// TCP通信 - TCP/IP网络通信
    /// </summary>
    TcpCommunication = 28,

    /// <summary>
    /// 数据库写入 - 检测结果存储
    /// </summary>
    DatabaseWrite = 29,

    /// <summary>
    /// 条件分支 - 流程控制
    /// </summary>
    ConditionalBranch = 30,

    /// <summary>
    /// 颜色空间转换 - BGR/GRAY/HSV/Lab/YUV等转换
    /// </summary>
    ColorConversion = 38,

    /// <summary>
    /// 自适应阈值 - Mean和Gaussian自适应阈值
    /// </summary>
    AdaptiveThreshold = 39,

    /// <summary>
    /// 直方图均衡化 - 全局均衡化和CLAHE自适应均衡化
    /// </summary>
    HistogramEqualization = 40,

    /// <summary>
    /// 几何拟合 - 直线/圆/椭圆拟合
    /// </summary>
    GeometricFitting = 41,

    /// <summary>
    /// ROI管理器 - 区域裁剪与掩膜
    /// </summary>
    RoiManager = 42,

    /// <summary>
    /// 形状匹配 - 旋转/缩放不变模板匹配
    /// </summary>
    ShapeMatching = 43,

    /// <summary>
    /// 亚像素边缘提取 - 高精度边缘定位
    /// </summary>
    SubpixelEdgeDetection = 44,

    /// <summary>
    /// 颜色检测 - HSV/Lab 空间颜色分析
    /// </summary>
    ColorDetection = 45,

    /// <summary>
    /// 串口通信 - RS-232/485 PLC 通信
    /// </summary>
    SerialCommunication = 46,

    /// <summary>
    /// 西门子S7通信 - S7-200/300/400/1200/1500
    /// </summary>
    SiemensS7Communication = 50,

    /// <summary>
    /// 三菱MC通信 - FX5U/Q/iQ-R系列
    /// </summary>
    MitsubishiMcCommunication = 51,

    /// <summary>
    /// 欧姆龙FINS通信 - CP/CJ/NJ/NX系列
    /// </summary>
    OmronFinsCommunication = 52,

    /// <summary>
    /// 结果判定算子 - 通用判定逻辑（数量/范围/阈值）
    /// </summary>
    ResultJudgment = 60,

    // ==================== 【第三优先级】新增算子 ====================

    /// <summary>
    /// Modbus RTU通信 - 串口Modbus RTU协议
    /// </summary>
    ModbusRtuCommunication = 70,

    /// <summary>
    /// CLAHE自适应直方图均衡化
    /// </summary>
    ClaheEnhancement = 71,

    /// <summary>
    /// 形态学操作 - 腐蚀/膨胀/开运算/闭运算
    /// </summary>
    MorphologicalOperation = 72,

    /// <summary>
    /// 高斯滤波 - 平滑降噪
    /// </summary>
    GaussianBlur = 73,

    /// <summary>
    /// 拉普拉斯锐化 - 边缘增强
    /// </summary>
    LaplacianSharpen = 74,

    /// <summary>
    /// ONNX模型推理 - 通用深度学习模型
    /// </summary>
    OnnxInference = 75,

    /// <summary>
    /// 图像加法 - 图像叠加
    /// </summary>
    ImageAdd = 76,

    /// <summary>
    /// 图像减法 - 差异检测
    /// </summary>
    ImageSubtract = 77,

    /// <summary>
    /// 图像融合 - 加权混合
    /// </summary>
    ImageBlend = 78,

    // ==================== 【第三优先级】变量和流程控制算子 ====================

    /// <summary>
    /// 变量读取 - 从全局变量表读取值
    /// </summary>
    VariableRead = 80,

    /// <summary>
    /// 变量写入 - 写入值到全局变量表
    /// </summary>
    VariableWrite = 81,

    /// <summary>
    /// 变量递增 - 计数器自增
    /// </summary>
    VariableIncrement = 82,

    /// <summary>
    /// 异常捕获 - Try-Catch 流程控制
    /// </summary>
    TryCatch = 83,

    /// <summary>
    /// 循环计数器 - 获取当前循环次数
    /// </summary>
    CycleCounter = 84,

    // ==================== 清霜V3迁移算子 ====================
    
    /// <summary>
    /// AKAZE特征匹配 - 基于AKAZE特征的鲁棒模板匹配
    /// </summary>
    AkazeFeatureMatch = 90,

    /// <summary>
    /// ORB特征匹配 - 基于ORB特征的快速模板匹配
    /// </summary>
    OrbFeatureMatch = 91,

    /// <summary>
    /// 梯度形状匹配 - 基于梯度方向的形状匹配
    /// </summary>
    GradientShapeMatch = 92,

    /// <summary>
    /// 金字塔形状匹配 - 多尺度金字塔形状匹配
    /// </summary>
    PyramidShapeMatch = 93,

    /// <summary>
    /// 双模态投票 - 结合深度学习和传统算法结果进行投票决策
    /// </summary>
    DualModalVoting = 94
}

/// <summary>
/// 算子执行状态
/// </summary>
public enum OperatorExecutionStatus
{
    /// <summary>
    /// 未执行
    /// </summary>
    NotExecuted = 0,

    /// <summary>
    /// 执行中
    /// </summary>
    Executing = 1,

    /// <summary>
    /// 执行成功
    /// </summary>
    Success = 2,

    /// <summary>
    /// 执行失败
    /// </summary>
    Failed = 3,

    /// <summary>
    /// 已跳过
    /// </summary>
    Skipped = 4
}

/// <summary>
/// 检测结果状态
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InspectionStatus
{
    /// <summary>
    /// 未检测
    /// </summary>
    NotInspected = 0,

    /// <summary>
    /// 检测中
    /// </summary>
    Inspecting = 1,

    /// <summary>
    /// 合格（OK）
    /// </summary>
    OK = 2,

    /// <summary>
    /// 不合格（NG）
    /// </summary>
    NG = 3,

    /// <summary>
    /// 检测错误
    /// </summary>
    Error = 4
}

/// <summary>
/// 缺陷类型
/// </summary>
public enum DefectType
{
    /// <summary>
    /// 划痕
    /// </summary>
    Scratch = 0,

    /// <summary>
    /// 污渍
    /// </summary>
    Stain = 1,

    /// <summary>
    /// 异物
    /// </summary>
    ForeignObject = 2,

    /// <summary>
    /// 缺失
    /// </summary>
    Missing = 3,

    /// <summary>
    /// 变形
    /// </summary>
    Deformation = 4,

    /// <summary>
    /// 尺寸偏差
    /// </summary>
    DimensionalDeviation = 5,

    /// <summary>
    /// 颜色异常
    /// </summary>
    ColorAbnormality = 6,

    /// <summary>
    /// 其他
    /// </summary>
    Other = 99
}

/// <summary>
/// 端口数据类型
/// </summary>
public enum PortDataType
{
    /// <summary>
    /// 图像
    /// </summary>
    Image = 0,

    /// <summary>
    /// 整数
    /// </summary>
    Integer = 1,

    /// <summary>
    /// 浮点数
    /// </summary>
    Float = 2,

    /// <summary>
    /// 布尔值
    /// </summary>
    Boolean = 3,

    /// <summary>
    /// 字符串
    /// </summary>
    String = 4,

    /// <summary>
    /// 点坐标
    /// </summary>
    Point = 5,

    /// <summary>
    /// 矩形
    /// </summary>
    Rectangle = 6,

    /// <summary>
    /// 轮廓
    /// </summary>
    Contour = 7,

    /// <summary>
    /// 任意类型
    /// </summary>
    Any = 99
}

/// <summary>
/// 端口方向
/// </summary>
public enum PortDirection
{
    /// <summary>
    /// 输入端口
    /// </summary>
    Input = 0,

    /// <summary>
    /// 输出端口
    /// </summary>
    Output = 1
}
