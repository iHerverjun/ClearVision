// OperatorEnums.cs


using System.Text.Json.Serialization;

namespace Acme.Product.Core.Enums;

public enum OperatorType
{
    ImageAcquisition = 0,

    Preprocessing = 1,

    Filtering = 2,

    EdgeDetection = 3,

    Thresholding = 4,

    Morphology = 5,

    BlobAnalysis = 6,

    TemplateMatching = 7,

    Measurement = 8,

    CodeRecognition = 9,

    DeepLearning = 10,

    ResultOutput = 11,

    ContourDetection = 12,

    MedianBlur = 13,

    BilateralFilter = 14,

    ImageResize = 15,

    ImageCrop = 16,

    ImageRotate = 17,

    PerspectiveTransform = 18,

    CircleMeasurement = 19,

    LineMeasurement = 20,

    ContourMeasurement = 21,

    AngleMeasurement = 22,

    GeometricTolerance = 23,

    CameraCalibration = 24,

    Undistort = 25,

    CoordinateTransform = 26,

    ModbusCommunication = 27,

    TcpCommunication = 28,

    DatabaseWrite = 29,

    ConditionalBranch = 30,

    ColorConversion = 38,

    AdaptiveThreshold = 39,

    HistogramEqualization = 40,

    GeometricFitting = 41,

    RoiManager = 42,

    RoiTransform = 216,

    ShapeMatching = 43,

    SubpixelEdgeDetection = 44,

    ColorDetection = 45,

    SerialCommunication = 46,

    SiemensS7Communication = 50,

    MitsubishiMcCommunication = 51,

    OmronFinsCommunication = 52,

    ResultJudgment = 60,


    ModbusRtuCommunication = 70,

    ClaheEnhancement = 71,

    MorphologicalOperation = 72,

    GaussianBlur = 73,

    LaplacianSharpen = 74,

    OnnxInference = 75,

    ImageAdd = 76,

    ImageSubtract = 77,

    ImageBlend = 78,


    VariableRead = 80,

    VariableWrite = 81,

    VariableIncrement = 82,

    TryCatch = 83,

    CycleCounter = 84,


    AkazeFeatureMatch = 90,

    OrbFeatureMatch = 91,

    GradientShapeMatch = 92,

    PyramidShapeMatch = 93,

    DualModalVoting = 94,


    OcrRecognition = 117,

    ImageDiff = 118,

    Statistics = 119,


    ForEach = 100,

    ArrayIndexer = 101,

    JsonExtractor = 102,


    MathOperation = 110,

    LogicGate = 111,

    TypeConvert = 112,

    HttpRequest = 113,

    MqttPublish = 114,

    StringFormat = 115,

    ImageSave = 116,


    Aggregator = 120,

    Comment = 121,

    Comparator = 122,

        Delay = 123,


    CaliperTool = 130,

    WidthMeasurement = 131,

    PointLineDistance = 132,

    LineLineDistance = 133,

    BoxNms = 140,

    BoxFilter = 141,

    SharpnessEvaluation = 142,

    PositionCorrection = 143,

    NPointCalibration = 150,

    CalibrationLoader = 151,

    UnitConvert = 152,
    TimerStatistics = 153,

    ScriptOperator = 160,

    TriggerModule = 161,

    PointAlignment = 162,

    PointCorrection = 163,

    GapMeasurement = 164,

    PolarUnwrap = 170,

    ShadingCorrection = 171,

    FrameAveraging = 172,

    AffineTransform = 173,

    ColorMeasurement = 174,

    SurfaceDefectDetection = 180,

    EdgePairDefect = 181,

    RectangleDetection = 182,

    TranslationRotationCalibration = 183,

    CornerDetection = 190,

    EdgeIntersection = 191,

    ParallelLineFind = 192,

    QuadrilateralFind = 193,

    GeoMeasurement = 194,

    ImageStitching = 200,

    ImageTiling = 201,

    ImageNormalize = 202,

    ImageCompose = 203,

    CopyMakeBorder = 204,

    TextSave = 210,

    PointSetTool = 211,

    BlobLabeling = 212,

    HistogramAnalysis = 213,

    PixelStatistics = 214,

    MeanFilter = 215,

    VoxelDownsample = 217,

    StatisticalOutlierRemoval = 218,

    RansacPlaneSegmentation = 219,

    EuclideanClusterExtraction = 220,

    PPFEstimation = 221,

    PPFMatch = 222
}

public enum OperatorExecutionStatus
{
    NotExecuted = 0,

    Executing = 1,

    Success = 2,

    Failed = 3,

    Skipped = 4
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InspectionStatus
{
    NotInspected = 0,

    Inspecting = 1,

    OK = 2,

    NG = 3,

    Error = 4
}

public enum DefectType
{
    Scratch = 0,

    Stain = 1,

    ForeignObject = 2,

    Missing = 3,

    Deformation = 4,

    DimensionalDeviation = 5,

    ColorAbnormality = 6,

    Other = 99
}

public enum PortDataType
{
    Image = 0,

    Integer = 1,

    Float = 2,

    Boolean = 3,

    String = 4,

    Point = 5,

    Rectangle = 6,

    Contour = 7,

    PointList = 8,

    DetectionResult = 9,

    DetectionList = 10,

    CircleData = 11,

    LineData = 12,

    Any = 99
}

public enum PortDirection
{
    Input = 0,

    Output = 1
}




