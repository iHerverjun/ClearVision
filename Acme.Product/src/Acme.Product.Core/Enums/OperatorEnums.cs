// OperatorEnums.cs
// 杈撳嚭绔彛
// 浣滆€咃細铇呰姕鍚?

using System.Text.Json.Serialization;

namespace Acme.Product.Core.Enums;

/// <summary>
/// 绠楀瓙绫诲瀷鏋氫妇 - 瀹氫箟鎵€鏈夋敮鎸佺殑鍥惧儚澶勭悊绠楀瓙绫诲瀷
/// </summary>
public enum OperatorType
{
    /// <summary>
    /// 鍥惧儚閲囬泦 - 浠庣浉鏈烘垨鏂囦欢鑾峰彇鍥惧儚
    /// </summary>
    ImageAcquisition = 0,

    /// <summary>
    /// 棰勫鐞?- 鍥惧儚棰勫鐞嗘搷浣?
    /// </summary>
    Preprocessing = 1,

    /// <summary>
    /// 婊ゆ尝 - 鍥惧儚婊ゆ尝闄嶅櫔
    /// </summary>
    Filtering = 2,

    /// <summary>
    /// 杈圭紭妫€娴?- 妫€娴嬪浘鍍忚竟缂?
    /// </summary>
    EdgeDetection = 3,

    /// <summary>
    /// 浜屽€煎寲 - 鍥惧儚闃堝€煎垎鍓?
    /// </summary>
    Thresholding = 4,

    /// <summary>
    /// 褰㈡€佸 - 褰㈡€佸鎿嶄綔锛堣厫铓€銆佽啫鑳€绛夛級
    /// </summary>
    Morphology = 5,

    /// <summary>
    /// Blob鍒嗘瀽 - 杩為€氬尯鍩熷垎鏋?
    /// </summary>
    BlobAnalysis = 6,

    /// <summary>
    /// 妯℃澘鍖归厤 - 鍥惧儚妯℃澘鍖归厤
    /// </summary>
    TemplateMatching = 7,

    /// <summary>
    /// 娴嬮噺 - 鍑犱綍娴嬮噺
    /// </summary>
    Measurement = 8,

    /// <summary>
    /// 鏉＄爜璇嗗埆 - 涓€缁寸爜/浜岀淮鐮佽瘑鍒?
    /// </summary>
    CodeRecognition = 9,

    /// <summary>
    /// 娣卞害瀛︿範 - AI缂洪櫡妫€娴?
    /// </summary>
    DeepLearning = 10,

    /// <summary>
    /// 缁撴灉杈撳嚭 - 杈撳嚭妫€娴嬬粨鏋?
    /// </summary>
    ResultOutput = 11,

    /// <summary>
    /// 杞粨妫€娴?
    /// </summary>
    ContourDetection = 12,

    /// <summary>
    /// 涓€兼护娉?- 鍥惧儚鍘诲櫔澶勭悊
    /// </summary>
    MedianBlur = 13,

    /// <summary>
    /// 鍙岃竟婊ゆ尝 - 杈圭紭淇濈暀婊ゆ尝
    /// </summary>
    BilateralFilter = 14,

    /// <summary>
    /// 鍥惧儚缂╂斁 - 璋冩暣鍥惧儚灏哄
    /// </summary>
    ImageResize = 15,

    /// <summary>
    /// 鍥惧儚瑁佸壀 - ROI鍖哄煙鎻愬彇
    /// </summary>
    ImageCrop = 16,

    /// <summary>
    /// 鍥惧儚鏃嬭浆 - 浠绘剰瑙掑害鏃嬭浆
    /// </summary>
    ImageRotate = 17,

    /// <summary>
    /// 閫忚鍙樻崲 - 鍥涜竟褰㈠彉鎹?
    /// </summary>
    PerspectiveTransform = 18,

    /// <summary>
    /// 鍦嗘祴閲?- 闇嶅か鍦嗘娴嬩笌娴嬮噺
    /// </summary>
    CircleMeasurement = 19,

    /// <summary>
    /// 鐩寸嚎娴嬮噺 - 闇嶅か鐩寸嚎妫€娴嬩笌娴嬮噺
    /// </summary>
    LineMeasurement = 20,

    /// <summary>
    /// 杞粨娴嬮噺 - 杞粨鍒嗘瀽涓庢祴閲?
    /// </summary>
    ContourMeasurement = 21,

    /// <summary>
    /// 瑙掑害娴嬮噺 - 鍩轰簬涓夌偣璁＄畻瑙掑害
    /// </summary>
    AngleMeasurement = 22,

    /// <summary>
    /// 鍑犱綍鍏樊 - 骞宠搴?鍨傜洿搴︽祴閲?
    /// </summary>
    GeometricTolerance = 23,

    /// <summary>
    /// 鐩告満鏍囧畾 - 妫嬬洏鏍?鍦嗙偣鏍囧畾
    /// </summary>
    CameraCalibration = 24,

    /// <summary>
    /// 鐣稿彉鏍℃ - 鍩轰簬鏍囧畾鏁版嵁鏍℃鐣稿彉
    /// </summary>
    Undistort = 25,

    /// <summary>
    /// 鍧愭爣杞崲 - 鍍忕礌鍒扮墿鐞嗗潗鏍囪浆鎹?
    /// </summary>
    CoordinateTransform = 26,

    /// <summary>
    /// Modbus閫氫俊 - 宸ヤ笟璁惧閫氫俊
    /// </summary>
    ModbusCommunication = 27,

    /// <summary>
    /// TCP閫氫俊 - TCP/IP缃戠粶閫氫俊
    /// </summary>
    TcpCommunication = 28,

    /// <summary>
    /// 鏁版嵁搴撳啓鍏?- 妫€娴嬬粨鏋滃瓨鍌?
    /// </summary>
    DatabaseWrite = 29,

    /// <summary>
    /// 鏉′欢鍒嗘敮 - 娴佺▼鎺у埗
    /// </summary>
    ConditionalBranch = 30,

    /// <summary>
    /// 棰滆壊绌洪棿杞崲 - BGR/GRAY/HSV/Lab/YUV绛夎浆鎹?
    /// </summary>
    ColorConversion = 38,

    /// <summary>
    /// 鑷€傚簲闃堝€?- Mean鍜孏aussian鑷€傚簲闃堝€?
    /// </summary>
    AdaptiveThreshold = 39,

    /// <summary>
    /// 鐩存柟鍥惧潎琛″寲 - 鍏ㄥ眬鍧囪　鍖栧拰CLAHE鑷€傚簲鍧囪　鍖?
    /// </summary>
    HistogramEqualization = 40,

    /// <summary>
    /// 鍑犱綍鎷熷悎 - 鐩寸嚎/鍦?妞渾鎷熷悎
    /// </summary>
    GeometricFitting = 41,

    /// <summary>
    /// ROI绠＄悊鍣?- 鍖哄煙瑁佸壀涓庢帺鑶?
    /// </summary>
    RoiManager = 42,

    /// <summary>
    /// 褰㈢姸鍖归厤 - 鏃嬭浆/缂╂斁涓嶅彉妯℃澘鍖归厤
    /// </summary>
    ShapeMatching = 43,

    /// <summary>
    /// 浜氬儚绱犺竟缂樻彁鍙?- 楂樼簿搴﹁竟缂樺畾浣?
    /// </summary>
    SubpixelEdgeDetection = 44,

    /// <summary>
    /// 棰滆壊妫€娴?- HSV/Lab 绌洪棿棰滆壊鍒嗘瀽
    /// </summary>
    ColorDetection = 45,

    /// <summary>
    /// 涓插彛閫氫俊 - RS-232/485 PLC 閫氫俊
    /// </summary>
    SerialCommunication = 46,

    /// <summary>
    /// 瑗块棬瀛怱7閫氫俊 - S7-200/300/400/1200/1500
    /// </summary>
    SiemensS7Communication = 50,

    /// <summary>
    /// 涓夎彵MC閫氫俊 - FX5U/Q/iQ-R绯诲垪
    /// </summary>
    MitsubishiMcCommunication = 51,

    /// <summary>
    /// 娆у榫橣INS閫氫俊 - CP/CJ/NJ/NX绯诲垪
    /// </summary>
    OmronFinsCommunication = 52,

    /// <summary>
    /// 缁撴灉鍒ゅ畾绠楀瓙 - 閫氱敤鍒ゅ畾閫昏緫锛堟暟閲?鑼冨洿/闃堝€硷級
    /// </summary>
    ResultJudgment = 60,

    // ==================== 銆愮涓変紭鍏堢骇銆戞柊澧炵畻瀛?====================

    /// <summary>
    /// Modbus RTU閫氫俊 - 涓插彛Modbus RTU鍗忚
    /// </summary>
    ModbusRtuCommunication = 70,

    /// <summary>
    /// CLAHE鑷€傚簲鐩存柟鍥惧潎琛″寲
    /// </summary>
    ClaheEnhancement = 71,

    /// <summary>
    /// 褰㈡€佸鎿嶄綔 - 鑵愯殌/鑶ㄨ儉/寮€杩愮畻/闂繍绠?
    /// </summary>
    MorphologicalOperation = 72,

    /// <summary>
    /// 楂樻柉婊ゆ尝 - 骞虫粦闄嶅櫔
    /// </summary>
    GaussianBlur = 73,

    /// <summary>
    /// 鎷夋櫘鎷夋柉閿愬寲 - 杈圭紭澧炲己
    /// </summary>
    LaplacianSharpen = 74,

    /// <summary>
    /// ONNX妯″瀷鎺ㄧ悊 - 閫氱敤娣卞害瀛︿範妯″瀷
    /// </summary>
    OnnxInference = 75,

    /// <summary>
    /// 鍥惧儚鍔犳硶 - 鍥惧儚鍙犲姞
    /// </summary>
    ImageAdd = 76,

    /// <summary>
    /// 鍥惧儚鍑忔硶 - 宸紓妫€娴?
    /// </summary>
    ImageSubtract = 77,

    /// <summary>
    /// 鍥惧儚铻嶅悎 - 鍔犳潈娣峰悎
    /// </summary>
    ImageBlend = 78,

    // ==================== 銆愮涓変紭鍏堢骇銆戝彉閲忓拰娴佺▼鎺у埗绠楀瓙 ====================

    /// <summary>
    /// 鍙橀噺璇诲彇 - 浠庡叏灞€鍙橀噺琛ㄨ鍙栧€?
    /// </summary>
    VariableRead = 80,

    /// <summary>
    /// 鍙橀噺鍐欏叆 - 鍐欏叆鍊煎埌鍏ㄥ眬鍙橀噺琛?
    /// </summary>
    VariableWrite = 81,

    /// <summary>
    /// 鍙橀噺閫掑 - 璁℃暟鍣ㄨ嚜澧?
    /// </summary>
    VariableIncrement = 82,

    /// <summary>
    /// 寮傚父鎹曡幏 - Try-Catch 娴佺▼鎺у埗
    /// </summary>
    TryCatch = 83,

    /// <summary>
    /// 寰幆璁℃暟鍣?- 鑾峰彇褰撳墠寰幆娆℃暟
    /// </summary>
    CycleCounter = 84,

    // ==================== 娓呴湝V3杩佺Щ绠楀瓙 ====================

    /// <summary>
    /// AKAZE鐗瑰緛鍖归厤 - 鍩轰簬AKAZE鐗瑰緛鐨勯瞾妫掓ā鏉垮尮閰?
    /// </summary>
    AkazeFeatureMatch = 90,

    /// <summary>
    /// ORB鐗瑰緛鍖归厤 - 鍩轰簬ORB鐗瑰緛鐨勫揩閫熸ā鏉垮尮閰?
    /// </summary>
    OrbFeatureMatch = 91,

    /// <summary>
    /// 姊害褰㈢姸鍖归厤 - 鍩轰簬姊害鏂瑰悜鐨勫舰鐘跺尮閰?
    /// </summary>
    GradientShapeMatch = 92,

    /// <summary>
    /// 閲戝瓧濉斿舰鐘跺尮閰?- 澶氬昂搴﹂噾瀛楀褰㈢姸鍖归厤
    /// </summary>
    PyramidShapeMatch = 93,

    /// <summary>
    /// 鍙屾ā鎬佹姇绁?- 缁撳悎AI鍜屼紶缁熸娴嬬粨鏋?
    /// </summary>
    DualModalVoting = 94,

    // ==================== Sprint 3: 缂哄け琛ラ綈 ====================

    /// <summary>
    /// OCR璇嗗埆 - 鏂囧瓧璇嗗埆绠楀瓙
    /// </summary>
    OcrRecognition = 117,

    /// <summary>
    /// 鍥惧儚瀵规瘮 - 宸垎鍒嗘瀽
    /// </summary>
    ImageDiff = 118,

    /// <summary>
    /// 缁熻鍒嗘瀽 - CPK缁熻
    /// </summary>
    Statistics = 119,

    // ==================== Sprint 2: ForEach 涓庢暟鎹搷浣滅畻瀛?====================

    /// <summary>
    /// ForEach 寰幆 - 瀵归泦鍚堜腑鐨勬瘡涓厓绱犳墽琛屽瓙鍥撅紙鏀寔 IoMode 骞惰/涓茶妯″紡锛?
    /// </summary>
    ForEach = 100,

    /// <summary>
    /// 鏁扮粍绱㈠紩鍣?- 浠庡垪琛?鏁扮粍涓寜绱㈠紩鎻愬彇鍏冪礌
    /// </summary>
    ArrayIndexer = 101,

    /// <summary>
    /// JSON 鎻愬彇鍣?- 鎸?JSONPath 浠?JSON 瀛楃涓蹭腑鎻愬彇瀛楁
    /// </summary>
    JsonExtractor = 102,

    // ==================== Sprint 3: 绠楀瓙鍏ㄩ潰鎵╁厖 ====================

    /// <summary>
    /// 鏁板€艰绠?- Add/Subtract/Multiply/Divide/Abs/Min/Max/Power/Sqrt/Round/Modulo
    /// </summary>
    MathOperation = 110,

    /// <summary>
    /// 閫昏緫闂?- AND/OR/NOT/XOR/NAND/NOR
    /// </summary>
    LogicGate = 111,

    /// <summary>
    /// 绫诲瀷杞崲 - String/Float/Integer/Boolean
    /// </summary>
    TypeConvert = 112,

    /// <summary>
    /// HTTP 璇锋眰 - 璋冪敤 REST API
    /// </summary>
    HttpRequest = 113,

    /// <summary>
    /// MQTT 鍙戝竷 - 鍚戞秷鎭槦鍒楁帹閫佹暟鎹?
    /// </summary>
    MqttPublish = 114,

    /// <summary>
    /// 瀛楃涓叉牸寮忓寲 - 鎷兼帴瀛楃涓?
    /// </summary>
    StringFormat = 115,

    /// <summary>
    /// 鍥惧儚淇濆瓨 - NG 鍥惧儚瀛樻。
    /// </summary>
    ImageSave = 116,

    // ==================== 鑳舵按绠楀瓙 ====================

    /// <summary>
    /// 鑱氬悎鍣?- 鑱氬悎澶氳矾鏁版嵁
    /// </summary>
    Aggregator = 120,

    /// <summary>
    /// 娉ㄩ噴 - 娣诲姞璇存槑鏂囨湰
    /// </summary>
    Comment = 121,

    /// <summary>
    /// 姣旇緝鍣?- 姣旇緝鏁板€?
    /// </summary>
    Comparator = 122,

    /// <summary>
    /// 寤舵椂 - 绛夊緟涓€瀹氭椂闂?
    /// </summary>
        Delay = 123,

    // ==================== Phase 1 Operators ====================

    /// <summary>
    /// Caliper edge-pair measurement
    /// </summary>
    CaliperTool = 130,

    /// <summary>
    /// Width measurement
    /// </summary>
    WidthMeasurement = 131,

    /// <summary>
    /// Point-to-line distance
    /// </summary>
    PointLineDistance = 132,

    /// <summary>
    /// Line-to-line distance and angle
    /// </summary>
    LineLineDistance = 133,

    /// <summary>
    /// Box NMS
    /// </summary>
    BoxNms = 140,

    /// <summary>
    /// Box filtering
    /// </summary>
    BoxFilter = 141,

    /// <summary>
    /// Sharpness evaluation
    /// </summary>
    SharpnessEvaluation = 142,

    /// <summary>
    /// Position correction
    /// </summary>
    PositionCorrection = 143,

    /// <summary>
    /// N-point calibration
    /// </summary>
    NPointCalibration = 150,

    /// <summary>
    /// Calibration file loader
    /// </summary>
    CalibrationLoader = 151,

    /// <summary>
    /// Unit conversion
    /// </summary>
    UnitConvert = 152,
    /// <summary>
    /// Timer statistics
    /// </summary>
    TimerStatistics = 153,

    /// <summary>
    /// Script operator
    /// </summary>
    ScriptOperator = 160,

    /// <summary>
    /// Trigger module
    /// </summary>
    TriggerModule = 161,

    /// <summary>
    /// Point alignment
    /// </summary>
    PointAlignment = 162,

    /// <summary>
    /// Point correction
    /// </summary>
    PointCorrection = 163,

    /// <summary>
    /// Gap measurement
    /// </summary>
    GapMeasurement = 164,

    /// <summary>
    /// Polar unwrap
    /// </summary>
    PolarUnwrap = 170,

    /// <summary>
    /// Shading correction
    /// </summary>
    ShadingCorrection = 171,

    /// <summary>
    /// Frame averaging
    /// </summary>
    FrameAveraging = 172,

    /// <summary>
    /// Affine transform
    /// </summary>
    AffineTransform = 173,

    /// <summary>
    /// Color measurement
    /// </summary>
    ColorMeasurement = 174,

    /// <summary>
    /// Surface defect detection
    /// </summary>
    SurfaceDefectDetection = 180,

    /// <summary>
    /// Edge pair defect detection
    /// </summary>
    EdgePairDefect = 181,

    /// <summary>
    /// Rectangle detection
    /// </summary>
    RectangleDetection = 182,

    /// <summary>
    /// Translation and rotation calibration
    /// </summary>
    TranslationRotationCalibration = 183,

    /// <summary>
    /// Corner detection
    /// </summary>
    CornerDetection = 190,

    /// <summary>
    /// Edge intersection
    /// </summary>
    EdgeIntersection = 191,

    /// <summary>
    /// Parallel line find
    /// </summary>
    ParallelLineFind = 192,

    /// <summary>
    /// Quadrilateral find
    /// </summary>
    QuadrilateralFind = 193,

    /// <summary>
    /// General geometry measurement
    /// </summary>
    GeoMeasurement = 194,

    /// <summary>
    /// Image stitching
    /// </summary>
    ImageStitching = 200,

    /// <summary>
    /// Image tiling
    /// </summary>
    ImageTiling = 201,

    /// <summary>
    /// Image normalize
    /// </summary>
    ImageNormalize = 202,

    /// <summary>
    /// Image compose
    /// </summary>
    ImageCompose = 203,

    /// <summary>
    /// Copy make border
    /// </summary>
    CopyMakeBorder = 204,

    /// <summary>
    /// Text save
    /// </summary>
    TextSave = 210,

    /// <summary>
    /// Point set tool
    /// </summary>
    PointSetTool = 211,

    /// <summary>
    /// Blob labeling
    /// </summary>
    BlobLabeling = 212,

    /// <summary>
    /// Histogram analysis
    /// </summary>
    HistogramAnalysis = 213,

    /// <summary>
    /// Pixel statistics
    /// </summary>
    PixelStatistics = 214
}

/// <summary>
/// 绠楀瓙鎵ц鐘舵€?
/// </summary>
public enum OperatorExecutionStatus
{
    /// <summary>
    /// 鏈墽琛?
    /// </summary>
    NotExecuted = 0,

    /// <summary>
    /// 鎵ц涓?
    /// </summary>
    Executing = 1,

    /// <summary>
    /// 鎵ц鎴愬姛
    /// </summary>
    Success = 2,

    /// <summary>
    /// 鎵ц澶辫触
    /// </summary>
    Failed = 3,

    /// <summary>
    /// 宸茶烦杩?
    /// </summary>
    Skipped = 4
}

/// <summary>
/// 妫€娴嬬粨鏋滅姸鎬?
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InspectionStatus
{
    /// <summary>
    /// 鏈娴?
    /// </summary>
    NotInspected = 0,

    /// <summary>
    /// 妫€娴嬩腑
    /// </summary>
    Inspecting = 1,

    /// <summary>
    /// 鍚堟牸锛圤K锛?
    /// </summary>
    OK = 2,

    /// <summary>
    /// 涓嶅悎鏍硷紙NG锛?
    /// </summary>
    NG = 3,

    /// <summary>
    /// 妫€娴嬮敊璇?
    /// </summary>
    Error = 4
}

/// <summary>
/// 缂洪櫡绫诲瀷
/// </summary>
public enum DefectType
{
    /// <summary>
    /// 鍒掔棔
    /// </summary>
    Scratch = 0,

    /// <summary>
    /// 姹℃笉
    /// </summary>
    Stain = 1,

    /// <summary>
    /// 寮傜墿
    /// </summary>
    ForeignObject = 2,

    /// <summary>
    /// 缂哄け
    /// </summary>
    Missing = 3,

    /// <summary>
    /// 鍙樺舰
    /// </summary>
    Deformation = 4,

    /// <summary>
    /// 灏哄鍋忓樊
    /// </summary>
    DimensionalDeviation = 5,

    /// <summary>
    /// 棰滆壊寮傚父
    /// </summary>
    ColorAbnormality = 6,

    /// <summary>
    /// 鍏朵粬
    /// </summary>
    Other = 99
}

/// <summary>
/// 绔彛鏁版嵁绫诲瀷
/// </summary>
public enum PortDataType
{
    /// <summary>
    /// 鍥惧儚
    /// </summary>
    Image = 0,

    /// <summary>
    /// 鏁存暟
    /// </summary>
    Integer = 1,

    /// <summary>
    /// 娴偣鏁?
    /// </summary>
    Float = 2,

    /// <summary>
    /// 甯冨皵鍊?
    /// </summary>
    Boolean = 3,

    /// <summary>
    /// 瀛楃涓?
    /// </summary>
    String = 4,

    /// <summary>
    /// 鐐瑰潗鏍?
    /// </summary>
    Point = 5,

    /// <summary>
    /// 鐭╁舰
    /// </summary>
    Rectangle = 6,

    /// <summary>
    /// 杞粨
    /// </summary>
    Contour = 7,

    /// <summary>
    /// 鐐瑰垪琛?- Sprint 1 Task 1.2
    /// </summary>
    PointList = 8,

    /// <summary>
    /// 鍗曚釜妫€娴嬬粨鏋滐紙鍚被鍒€佺疆淇″害銆佽竟鐣屾绛夛級- Sprint 1 Task 1.2
    /// </summary>
    DetectionResult = 9,

    /// <summary>
    /// 妫€娴嬬粨鏋滃垪琛?- Sprint 1 Task 1.2
    /// </summary>
    DetectionList = 10,

    /// <summary>
    /// 鍦嗘暟鎹紙鍦嗗績銆佸崐寰勶級- Sprint 1 Task 1.2
    /// </summary>
    CircleData = 11,

    /// <summary>
    /// 鐩寸嚎鏁版嵁锛堣捣鐐广€佺粓鐐癸級- Sprint 1 Task 1.2
    /// </summary>
    LineData = 12,

    /// <summary>
    /// 浠绘剰绫诲瀷
    /// </summary>
    Any = 99
}

/// <summary>
/// 绔彛鏂瑰悜
/// </summary>
public enum PortDirection
{
    /// <summary>
    /// 杈撳叆绔彛
    /// </summary>
    Input = 0,

    /// <summary>
    /// 杈撳嚭绔彛
    /// </summary>
    Output = 1
}




