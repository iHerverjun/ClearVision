// DeepLearningOperator.cs
// 深度学习算子 - 使用 ONNX 模型进行 AI 缺陷检测
// 作者：蘅芜君

using System.Buffers;
using System.Collections.Concurrent;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Services;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.Extensions.Logging;
using OpenCvSharp;


using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// YOLO 模型版本
/// </summary>
public enum YoloVersion
{
    /// <summary>
    /// 自动检测
    /// </summary>
    Auto = 0,

    /// <summary>
    /// YOLOv5
    /// </summary>
    YOLOv5 = 5,

    /// <summary>
    /// YOLOv6
    /// </summary>
    YOLOv6 = 6,

    /// <summary>
    /// YOLOv8
    /// </summary>
    YOLOv8 = 8,

    /// <summary>
    /// YOLOv11
    /// </summary>
    YOLOv11 = 11
}

/// <summary>
/// 深度学习算子 - 使用 ONNX 模型进行 AI 缺陷检测
/// 支持 YOLOv5, YOLOv6, YOLOv8, YOLOv11 等多种模型格式
/// </summary>
[OperatorMeta(
    DisplayName = "深度学习",
    Description = "AI 深度学习推理，支持 YOLOv5/v6/v8/v11 等模型，用于缺陷检测和目标分类",
    Category = "AI检测",
    IconName = "ai",
    Keywords = new[] { "深度学习", "AI", "模型", "推理", "缺陷识别", "目标检测", "YOLO", "判断瑕疵", "Deep learning" }
)]
[InputPort("Image", "输入图像", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "结果图像", PortDataType.Image)]
[OutputPort("DetectionList", "检测列表", PortDataType.DetectionList)]
[OutputPort("Defects", "缺陷列表", PortDataType.DetectionList)]
[OutputPort("DefectCount", "缺陷数量", PortDataType.Integer)]
[OutputPort("Objects", "目标列表", PortDataType.DetectionList)]
[OutputPort("ObjectCount", "目标数量", PortDataType.Integer)]
[OperatorParam("ModelPath", "模型路径", "file", DefaultValue = "")]
[OperatorParam("Confidence", "置信度阈值", "double", DefaultValue = 0.5, Min = 0.0, Max = 1.0)]
[OperatorParam("ModelVersion", "YOLO版本", "enum", DefaultValue = "Auto", Options = new[] { "Auto|自动检测", "YOLOv5|YOLOv5", "YOLOv6|YOLOv6", "YOLOv8|YOLOv8", "YOLOv11|YOLOv11" })]
[OperatorParam("InputSize", "输入尺寸", "int", DefaultValue = 640, Min = 320, Max = 1280)]
[OperatorParam("UseGpu", "使用GPU", "bool", DefaultValue = true)]
[OperatorParam("GpuDeviceId", "GPU设备ID", "int", DefaultValue = 0, Min = 0, Max = 15)]
[OperatorParam("TargetClasses", "目标类别", "string", Description = "检测目标类别（逗号分隔，如 person,car），为空则检测所有类别", DefaultValue = "")]
[OperatorParam("LabelsPath", "标签文件路径", "file", Description = "自定义标签文件路径（每行一个标签），为空则使用COCO 80类或自动查找模型目录下的labels.txt", DefaultValue = "")]
[OperatorParam("EnableInternalNms", "启用内部NMS", "bool", Description = "关闭后输出置信度筛选后的候选框，由下游 BoxNms 负责唯一 NMS。", DefaultValue = true)]
[OperatorParam("DetectionMode", "检测模式", "enum", Description = "缺陷检测：检出目标视为缺陷(NG)；目标检测：检出目标视为正常(OK)", DefaultValue = "Defect", Options = new[] { "Defect|缺陷检测", "Object|目标检测" })]
public class DeepLearningOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.DeepLearning;

    public DeepLearningOperator(ILogger<DeepLearningOperator> logger) : base(logger) { }

    /// <summary>
    /// 模型缓存 - 避免重复加载
    /// </summary>
    private static readonly ConcurrentDictionary<string, InferenceSession> _modelCache = new();

    /// <summary>
    /// 线程锁 - 用于并发加载模型
    /// </summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _modelLocks = new();
    private const int MaxCachedModels = 3;
    private static readonly LinkedList<string> _modelAccessOrder = new();
    private static readonly Dictionary<string, LinkedListNode<string>> _modelAccessNodes = new();
    private static readonly object _modelCacheEvictionLock = new();

    /// <summary>
    /// 默认输入尺寸（YOLO 模型常用 640x640）
    /// </summary>
    private const int DefaultInputSize = 640;

    /// <summary>
    /// 类别颜色映射
    /// </summary>
    private static readonly Scalar[] ClassColors = new[]
    {
        new Scalar(0, 255, 0),     // 绿色
        new Scalar(255, 0, 0),     // 蓝色
        new Scalar(0, 0, 255),     // 红色
        new Scalar(255, 255, 0),   // 青色
        new Scalar(255, 0, 255),   // 紫色
        new Scalar(0, 255, 255),   // 黄色
        new Scalar(128, 128, 255), // 粉色
        new Scalar(128, 255, 128)  // 浅绿
    };

    /// <summary>
    /// COCO 80类标签名映射
    /// </summary>
    private static readonly string[] CocoClassNames = new[]
    {
        "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat", "traffic light",
        "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat", "dog", "horse", "sheep", "cow",
        "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee",
        "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket", "bottle",
        "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange",
        "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "couch", "potted plant", "bed",
        "dining table", "toilet", "tv", "laptop", "mouse", "remote", "keyboard", "cell phone", "microwave", "oven",
        "toaster", "sink", "refrigerator", "book", "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush"
    };

    /// <summary>
    /// 执行算子核心逻辑
    /// </summary>
    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        // 1. 获取输入图像
        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供输入图像"));
        }

        // 2. 获取参数
        var modelPath = GetStringParam(@operator, "ModelPath", string.Empty);
        var confidenceThreshold = GetFloatParam(@operator, "Confidence", 0.5f, 0.0f, 1.0f);
        var inputSize = GetIntParam(@operator, "InputSize", DefaultInputSize);
        var yoloVersionStr = GetStringParam(@operator, "ModelVersion", "Auto");
        var yoloVersion = ParseYoloVersion(yoloVersionStr);
        var targetClassesStr = GetStringParam(@operator, "TargetClasses", string.Empty);
        var labelsPath = ResolveLabelsPath(@operator);
        var enableInternalNms = GetBoolParam(@operator, "EnableInternalNms", true);

        // 2.1 加载自定义标签
        var labels = LoadLabels(labelsPath, modelPath, targetClassesStr);
        var unresolvedTargetClasses = FindUnresolvedTargetClasses(targetClassesStr, labels);
        if (unresolvedTargetClasses.Count > 0)
        {
            var labelSource = ReferenceEquals(labels, CocoClassNames)
                ? "the default COCO labels"
                : "the active labels";
            return Task.FromResult(OperatorExecutionOutput.Failure(
                $"Failed to resolve TargetClasses [{string.Join(", ", unresolvedTargetClasses)}] against {labelSource}. Set LabelsPath or place labels.txt next to the model."));
        }

        var targetClasses = ParseTargetClasses(targetClassesStr, labels);
        Logger.LogInformation("[DeepLearning] 当前使用标签数量: {Count}, 目标参数: {TargetStr}", labels.Length, targetClassesStr);
        if (labels.Length > 0 && !ReferenceEquals(labels, CocoClassNames))
        {
            Logger.LogInformation("[DeepLearning] 使用自定义标签");
        }

        // 3. 验证模型路径
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未指定模型路径"));
        }

        if (!File.Exists(modelPath))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"模型文件不存在: {modelPath}"));
        }

        // 4. 解码图像
        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        var originalWidth = src.Width;
        var originalHeight = src.Height;

        // 5. 加载模型（支持GPU加速 - P3-O3.1）
        var useGpu = GetBoolParam(@operator, "UseGpu", true);
        var gpuDeviceId = GetIntParam(@operator, "GpuDeviceId", 0, 0, 15);
        var session = LoadModel(modelPath, useGpu, gpuDeviceId);
        if (session == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("模型加载失败"));
        }

        // 6. 预处理图像
        var inputTensor = PreprocessImage(src, inputSize);
        Logger.LogDebug("[DeepLearning] 输入张量形状: [1, 3, {InputSize}, {InputSize}]", inputSize, inputSize);

        // 7. 执行推理
        var outputTensor = RunInference(session, inputTensor);
        Logger.LogDebug("[DeepLearning] 输出张量形状: [{Dimensions}]", string.Join(", ", outputTensor.Dimensions.ToArray()));

        // 8. 自动检测 YOLO 版本
        Logger.LogDebug("[DeepLearning] 参数ModelVersion: '{YoloVersionStr}', 解析为: {YoloVersion}", yoloVersionStr, yoloVersion);

        if (yoloVersion == YoloVersion.Auto)
        {
            yoloVersion = DetectYoloVersion(outputTensor, labels.Length);
        }
        Logger.LogInformation("[DeepLearning] 最终使用YOLO版本: {YoloVersion}, 置信度阈值: {Confidence}", yoloVersion, confidenceThreshold);

        // 9. 后处理
        var detections = PostprocessResults(outputTensor, confidenceThreshold, originalWidth, originalHeight, inputSize, yoloVersion, targetClasses, enableInternalNms);
        Logger.LogInformation("[DeepLearning] 检测到目标数量: {DetectionCount}", detections.Count);

        var detectionMode = GetStringParam(@operator, "DetectionMode", "Defect");
        var isObjectMode = detectionMode.Equals("Object", StringComparison.OrdinalIgnoreCase);

        // 10. 绘制结果
        var visualizationDetections = BuildVisualizationDetections(detections, confidenceThreshold, enableInternalNms);
        var outputImage = DrawResults(src, visualizationDetections, labels, detectionMode);

        // 11. 构建输出 - Sprint 1 Task 1.2: 使用 DetectionList 类型
        var detectionList = new DetectionList(
            detections.Select((d, index) => new Core.ValueObjects.DetectionResult
            {
                Label = GetClassName(d.ClassId, labels),
                Confidence = d.Confidence,
                X = d.X,
                Y = d.Y,
                Width = d.Width,
                Height = d.Height
            })
        );

        var additionalData = new Dictionary<string, object>
        {
            { "DetectionMode", detectionMode },
            { "InternalNmsEnabled", enableInternalNms },
            { "RawCandidateCount", detections.Count },
            { "VisualizationDetectionCount", visualizationDetections.Count },
            { "DetectionList", detectionList },
            { "Objects", isObjectMode ? detectionList : new DetectionList() },
            { "ObjectCount", isObjectMode ? detections.Count : 0 },
            { "Defects", isObjectMode ? new DetectionList() : detectionList },
            { "DefectCount", isObjectMode ? 0 : detections.Count }
        };

        Logger.LogInformation("[DeepLearning] 执行完毕. 检测总数: {Count}, 过滤后输出: {DefectCount}", detections.Count, detections.Count);

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(outputImage, additionalData)));
    }

    /// <summary>
    /// 加载模型（带缓存，支持GPU加速 - P3-O3.1）
    /// </summary>
    private InferenceSession? LoadModel(string modelPath, bool useGpu = true, int gpuDeviceId = 0)
    {
        var cacheKey = $"{modelPath}_gpu_{useGpu}_{gpuDeviceId}";
        if (_modelCache.TryGetValue(cacheKey, out var cachedSession))
        {
            TouchModelCacheKey(cacheKey);
            return cachedSession;
        }

        var lockObj = _modelLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));

        lockObj.Wait();
        try
        {
            if (_modelCache.TryGetValue(cacheKey, out cachedSession))
            {
                TouchModelCacheKey(cacheKey);
                return cachedSession;
            }

            var sessionOptions = new SessionOptions
            {
                InterOpNumThreads = 4,
                IntraOpNumThreads = 4,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
            };

            if (useGpu && GpuAvailabilityChecker.IsCudaAvailable)
            {
                try
                {
                    if (GpuAvailabilityChecker.IsTensorRtAvailable)
                    {
                        Logger.LogInformation("[DeepLearning] TensorRT加速已启用，设备ID: {DeviceId}", gpuDeviceId);
                    }
                    else
                    {
                        sessionOptions.AppendExecutionProvider_CUDA(gpuDeviceId);
                        Logger.LogInformation("[DeepLearning] CUDA GPU加速已启用，设备ID: {DeviceId}", gpuDeviceId);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "[DeepLearning] GPU加速启用失败，回退到CPU模式");
                }
            }
            else
            {
                Logger.LogInformation("[DeepLearning] 使用CPU模式运行");
            }

            var session = new InferenceSession(modelPath, sessionOptions);

            lock (_modelCacheEvictionLock)
            {
                EvictModelsIfNeeded();
                _modelCache[cacheKey] = session;
                TouchModelCacheKey(cacheKey);
            }

            Logger.LogDebug("[DeepLearning] 模型加载成功，GPU加速: {GpuEnabled}", useGpu && GpuAvailabilityChecker.IsCudaAvailable);
            return session;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[DeepLearning] 模型加载失败");
            return null;
        }
        finally
        {
            lockObj.Release();
        }
    }

    public static void UnloadModel(string modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
            return;

        var keyPrefix = $"{modelPath}_gpu_";
        var keysToRemove = _modelCache.Keys
            .Where(k => k.StartsWith(keyPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        lock (_modelCacheEvictionLock)
        {
            foreach (var key in keysToRemove)
            {
                if (_modelCache.TryRemove(key, out var session))
                {
                    session.Dispose();
                }

                _modelLocks.TryRemove(key, out _);
                RemoveModelAccessNode(key);
            }
        }
    }

    private void TouchModelCacheKey(string cacheKey)
    {
        lock (_modelCacheEvictionLock)
        {
            RemoveModelAccessNode(cacheKey);
            _modelAccessNodes[cacheKey] = _modelAccessOrder.AddLast(cacheKey);
        }
    }

    private void EvictModelsIfNeeded()
    {
        while (_modelCache.Count >= MaxCachedModels && _modelAccessOrder.Count > 0)
        {
            var oldestKey = _modelAccessOrder.First!.Value;
            RemoveModelAccessNode(oldestKey);

            if (_modelCache.TryRemove(oldestKey, out var oldSession))
            {
                oldSession.Dispose();
                Logger.LogInformation("[DeepLearning] 驱逐模型缓存: {Key}", oldestKey);
            }

            _modelLocks.TryRemove(oldestKey, out _);
        }
    }

    private static void RemoveModelAccessNode(string cacheKey)
    {
        if (_modelAccessNodes.TryGetValue(cacheKey, out var node))
        {
            _modelAccessOrder.Remove(node);
            _modelAccessNodes.Remove(cacheKey);
        }
    }

    /// <summary>
    /// 预处理图像（P3-O3.2: 使用ArrayPool优化内存分配）
    /// </summary>
    private DenseTensor<float> PreprocessImage(Mat src, int inputSize)
    {
        // 1. 计算缩放比例（保持宽高比）
        var scale = Math.Min((float)inputSize / src.Width, (float)inputSize / src.Height);
        var newWidth = (int)(src.Width * scale);
        var newHeight = (int)(src.Height * scale);

        // 2. Resize
        using var resized = new Mat();
        Cv2.Resize(src, resized, new Size(newWidth, newHeight), 0, 0, InterpolationFlags.Linear);

        // 3. 创建填充画布（640x640）
        using var padded = new Mat(inputSize, inputSize, MatType.CV_8UC3, new Scalar(114, 114, 114));
        var xOffset = (inputSize - newWidth) / 2;
        var yOffset = (inputSize - newHeight) / 2;

        // 将 resized 图像复制到画布中央
        var roi = new Rect(xOffset, yOffset, newWidth, newHeight);
        using var targetRoi = new Mat(padded, roi);
        resized.CopyTo(targetRoi);

        // 4. 转换为 float 并归一化（除以 255）
        using var floatMat = new Mat();
        padded.ConvertTo(floatMat, MatType.CV_32FC3, 1.0 / 255.0);

        // 5. 提取数据并转换为 CHW 格式（P3-O3.2: 使用ArrayPool）
        // YOLO 模型期望 RGB 顺序，OpenCV 使用 BGR 顺序
        var tensorSize = 1 * 3 * inputSize * inputSize;
        var tensorData = ArrayPool<float>.Shared.Rent(tensorSize);

        try
        {
            var matData = floatMat.GetGenericIndexer<Vec3f>();
            var channelSize = inputSize * inputSize;

            for (int h = 0; h < inputSize; h++)
            {
                for (int w = 0; w < inputSize; w++)
                {
                    var pixel = matData[h, w];
                    var pixelIndex = h * inputSize + w;
                    // CHW 格式: [batch, channel, height, width]
                    // OpenCV BGR -> 模型 RGB: Item2=R, Item1=G, Item0=B
                    tensorData[0 * channelSize + pixelIndex] = pixel.Item2; // R 通道
                    tensorData[1 * channelSize + pixelIndex] = pixel.Item1; // G 通道
                    tensorData[2 * channelSize + pixelIndex] = pixel.Item0; // B 通道
                }
            }

            // 创建DenseTensor（复制数据）
            return new DenseTensor<float>(tensorData[..tensorSize].ToArray(), new[] { 1, 3, inputSize, inputSize });
        }
        finally
        {
            // 归还数组到池（清理数据确保安全）
            ArrayPool<float>.Shared.Return(tensorData, clearArray: true);
        }
    }

    /// <summary>
    /// 执行推理
    /// </summary>
    private DenseTensor<float> RunInference(InferenceSession session, DenseTensor<float> inputTensor)
    {
        // 获取输入名称
        var inputName = session.InputMetadata.Keys.First();

        // 创建输入
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
        };

        // 执行推理
        using var results = session.Run(inputs);

        // 获取输出
        var outputName = session.OutputMetadata.Keys.First();
        var outputValue = results.First(r => r.Name == outputName);
        var outputTensor = outputValue.AsTensor<float>();

        return outputTensor as DenseTensor<float> ?? new DenseTensor<float>(outputTensor.ToArray(), outputTensor.Dimensions.ToArray());
    }

    /// <summary>
    /// 验证参数
    /// </summary>
    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var modelPath = GetStringParam(@operator, "ModelPath", string.Empty);
        var confidence = GetFloatParam(@operator, "Confidence", 0.5f);

        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return ValidationResult.Invalid("必须指定模型路径");
        }

        if (confidence < 0 || confidence > 1)
        {
            return ValidationResult.Invalid("置信度阈值必须在 0-1 之间");
        }

        return ValidationResult.Valid();
    }

    /// <summary>
    /// 后处理结果 - 支持多种 YOLO 版本
    /// </summary>
    private List<DetectionResult> PostprocessResults(
        DenseTensor<float> outputTensor,
        float confidenceThreshold,
        int originalWidth,
        int originalHeight,
        int inputSize,
        YoloVersion yoloVersion,
        HashSet<int>? targetClasses,
        bool enableInternalNms)
    {
        // 根据 YOLO 版本选择处理方式
        var detections = yoloVersion switch
        {
            YoloVersion.YOLOv5 => PostprocessYoloV5V6(outputTensor, confidenceThreshold, originalWidth, originalHeight, inputSize, enableInternalNms),
            YoloVersion.YOLOv6 => PostprocessYoloV5V6(outputTensor, confidenceThreshold, originalWidth, originalHeight, inputSize, enableInternalNms),
            YoloVersion.YOLOv8 => PostprocessYoloV8V11(outputTensor, confidenceThreshold, originalWidth, originalHeight, inputSize, enableInternalNms),
            YoloVersion.YOLOv11 => PostprocessYoloV8V11(outputTensor, confidenceThreshold, originalWidth, originalHeight, inputSize, enableInternalNms),
            _ => PostprocessYoloV8V11(outputTensor, confidenceThreshold, originalWidth, originalHeight, inputSize, enableInternalNms)
        };

        // 如果指定了目标类别，进行过滤
        if (targetClasses != null && targetClasses.Count > 0)
        {
            var beforeFilter = detections.Count;
            detections = detections.Where(d => targetClasses.Contains(d.ClassId)).ToList();
            Logger.LogDebug("[DeepLearning] 类别过滤: {BeforeFilter} -> {AfterFilter} (目标类别: {TargetClasses})",
                beforeFilter, detections.Count, string.Join(",", targetClasses));
        }

        return detections;
    }


    /// <summary>
    /// 后处理 YOLOv8/v11 格式：[1, 84, 8400] 或 [1, 8400, 84] (Transposed)
    /// </summary>
    private List<DetectionResult> PostprocessYoloV8V11(
        DenseTensor<float> outputTensor,
        float confidenceThreshold,
        int originalWidth,
        int originalHeight,
        int inputSize,
        bool enableInternalNms)
    {
        var detections = new List<DetectionResult>();
        var shape = outputTensor.Dimensions.ToArray();

        if (shape.Length != 3)
            return detections;

        // Determine orientation
        // Standard: [1, Features, Anchors] e.g. [1, 84, 8400]
        // Transposed: [1, Anchors, Features] e.g. [1, 8400, 84]

        int numAnchors, numFeatures;
        bool isTransposed = false;

        if (shape[1] > shape[2])
        {
            // Likely transposed: [1, 8400, 84]
            numAnchors = shape[1];
            numFeatures = shape[2];
            isTransposed = true;
            Logger.LogDebug("[DeepLearning] YOLOv8/v11 处理模式: Transposed [1, {NumAnchors}, {NumFeatures}]", numAnchors, numFeatures);
        }
        else
        {
            // Likely standard: [1, 84, 8400]
            numFeatures = shape[1];
            numAnchors = shape[2];
            Logger.LogDebug("[DeepLearning] YOLOv8/v11 处理模式: Standard [1, {NumFeatures}, {NumAnchors}]", numFeatures, numAnchors);
        }

        int numClasses = numFeatures - 4; // 84 - 4 = 80
        float globalMaxConf = 0f;

        var scale = Math.Min((float)inputSize / originalWidth, (float)inputSize / originalHeight);
        var xPad = (inputSize - originalWidth * scale) / 2;
        var yPad = (inputSize - originalHeight * scale) / 2;

        for (int i = 0; i < numAnchors; i++)
        {
            float x, y, w, h;

            if (isTransposed)
            {
                // [1, 8400, 84] -> [0, i, 0..3]
                x = outputTensor[0, i, 0];
                y = outputTensor[0, i, 1];
                w = outputTensor[0, i, 2];
                h = outputTensor[0, i, 3];
            }
            else
            {
                // [1, 84, 8400] -> [0, 0..3, i]
                x = outputTensor[0, 0, i];
                y = outputTensor[0, 1, i];
                w = outputTensor[0, 2, i];
                h = outputTensor[0, 3, i];
            }

            float maxClassProb = 0;
            int maxClassId = 0;

            for (int c = 0; c < numClasses; c++)
            {
                float prob;
                if (isTransposed)
                {
                    prob = outputTensor[0, i, 4 + c];
                }
                else
                {
                    prob = outputTensor[0, 4 + c, i];
                }

                if (prob > maxClassProb)
                {
                    maxClassProb = prob;
                    maxClassId = c;
                }
            }

            if (maxClassProb > globalMaxConf)
            {
                globalMaxConf = maxClassProb;
            }

            if (maxClassProb < confidenceThreshold)
            {
                continue;
            }

            float x1 = (x - w / 2 - xPad) / scale;
            float y1 = (y - h / 2 - yPad) / scale;
            float x2 = (x + w / 2 - xPad) / scale;
            float y2 = (y + h / 2 - yPad) / scale;

            x1 = Math.Max(0, Math.Min(x1, originalWidth));
            y1 = Math.Max(0, Math.Min(y1, originalHeight));
            x2 = Math.Max(0, Math.Min(x2, originalWidth));
            y2 = Math.Max(0, Math.Min(y2, originalHeight));

            detections.Add(new DetectionResult
            {
                X = x1,
                Y = y1,
                Width = x2 - x1,
                Height = y2 - y1,
                Confidence = maxClassProb,
                ClassId = maxClassId
            });
        }

        Logger.LogDebug("[DeepLearning] V8/V11后处理: 最大置信度={GlobalMaxConf:F4}, 阈值={ConfidenceThreshold}, 阈值前检测数={DetectionCount}",
            globalMaxConf, confidenceThreshold, detections.Count);
        if (!enableInternalNms)
        {
            Logger.LogDebug("[DeepLearning] 已禁用内部NMS，输出候选框数: {CandidateCount}", detections.Count);
            return detections;
        }

        var nmsResult = ApplyNMS(detections, 0.45f);
        Logger.LogDebug("[DeepLearning] NMS后检测数: {NmsCount}", nmsResult.Count);
        return nmsResult;
    }

    /// <summary>
    /// 后处理 YOLOv5/v6 格式：[1, 25200, 85]
    /// </summary>
    private List<DetectionResult> PostprocessYoloV5V6(
        DenseTensor<float> outputTensor,
        float confidenceThreshold,
        int originalWidth,
        int originalHeight,
        int inputSize,
        bool enableInternalNms)
    {
        var detections = new List<DetectionResult>();
        var shape = outputTensor.Dimensions.ToArray();

        if (shape.Length != 3)
            return detections;
        var isTransposed = shape[1] < shape[2];
        int numAnchors = isTransposed ? shape[2] : shape[1];
        int numFeatures = isTransposed ? shape[1] : shape[2];
        int numClasses = numFeatures - 5;
        float globalMaxConf = 0f;
        var scale = Math.Min((float)inputSize / originalWidth, (float)inputSize / originalHeight);
        var xPad = (inputSize - originalWidth * scale) / 2;
        var yPad = (inputSize - originalHeight * scale) / 2;

        for (int i = 0; i < numAnchors; i++)
        {
            float objConf = isTransposed
                ? outputTensor[0, 4, i]
                : outputTensor[0, i, 4];
            if (objConf < confidenceThreshold)
                continue;

            float x = isTransposed ? outputTensor[0, 0, i] : outputTensor[0, i, 0];
            float y = isTransposed ? outputTensor[0, 1, i] : outputTensor[0, i, 1];
            float w = isTransposed ? outputTensor[0, 2, i] : outputTensor[0, i, 2];
            float h = isTransposed ? outputTensor[0, 3, i] : outputTensor[0, i, 3];

            float maxClassProb = 0;
            int maxClassId = 0;

            for (int c = 0; c < numClasses; c++)
            {
                float prob = isTransposed
                    ? outputTensor[0, 5 + c, i]
                    : outputTensor[0, i, 5 + c];
                if (prob > maxClassProb)
                { maxClassProb = prob; maxClassId = c; }
            }

            float finalConf = objConf * maxClassProb;
            if (finalConf > globalMaxConf)
                globalMaxConf = finalConf;
            if (finalConf < confidenceThreshold)
                continue;

            float x1 = (x - w / 2 - xPad) / scale;
            float y1 = (y - h / 2 - yPad) / scale;
            float x2 = (x + w / 2 - xPad) / scale;
            float y2 = (y + h / 2 - yPad) / scale;

            x1 = Math.Max(0, Math.Min(x1, originalWidth));
            y1 = Math.Max(0, Math.Min(y1, originalHeight));
            x2 = Math.Max(0, Math.Min(x2, originalWidth));
            y2 = Math.Max(0, Math.Min(y2, originalHeight));

            detections.Add(new DetectionResult { X = x1, Y = y1, Width = x2 - x1, Height = y2 - y1, Confidence = finalConf, ClassId = maxClassId });
        }

        Logger.LogDebug("[DeepLearning] V5/V6后处理: 最大置信度={GlobalMaxConf:F4}, 阈值前检测数={DetectionCount}",
            globalMaxConf, detections.Count);
        if (!enableInternalNms)
        {
            Logger.LogDebug("[DeepLearning] 已禁用内部NMS，输出候选框数: {CandidateCount}", detections.Count);
            return detections;
        }

        var nmsResult = ApplyNMS(detections, 0.45f);
        Logger.LogDebug("[DeepLearning] NMS后检测数: {NmsCount}", nmsResult.Count);
        return nmsResult;
    }

    /// <summary>
    /// 自动检测 YOLO 版本
    /// </summary>
    private YoloVersion DetectYoloVersion(DenseTensor<float> outputTensor, int knownLabelCount = 0)
    {
        var shape = outputTensor.Dimensions.ToArray();

        if (shape.Length != 3)
        {
            Logger.LogDebug("[DeepLearning] 非标准3维张量 (维度数={ShapeLength})，默认使用YOLOv8", shape.Length);
            return YoloVersion.YOLOv8;
        }

        int dim1 = shape[1];
        int dim2 = shape[2];

        // dim1=8400, dim2=84 -> Transposed V8/V11
        // dim1=84, dim2=8400 -> Standard V8/V11
        // dim1=25200, dim2=85 -> Transposed V5/V6 (standard output)

        if (knownLabelCount > 0)
        {
            if (dim1 > dim2)
            {
                if (dim2 == knownLabelCount + 5)
                {
                    Logger.LogDebug("[DeepLearning] 自动检测: YOLOv5/v6自定义类别格式 (anchors={Dim1}, features={Dim2}, labels={KnownLabelCount})", dim1, dim2, knownLabelCount);
                    return YoloVersion.YOLOv5;
                }

                if (dim2 == knownLabelCount + 4)
                {
                    Logger.LogDebug("[DeepLearning] 自动检测: YOLOv8/v11自定义类别格式 (anchors={Dim1}, features={Dim2}, labels={KnownLabelCount})", dim1, dim2, knownLabelCount);
                    return YoloVersion.YOLOv8;
                }
            }
            else
            {
                if (dim1 == knownLabelCount + 5)
                {
                    Logger.LogDebug("[DeepLearning] 自动检测: YOLOv5/v6转置格式 (features={Dim1}, anchors={Dim2}, labels={KnownLabelCount})", dim1, dim2, knownLabelCount);
                    return YoloVersion.YOLOv5;
                }

                if (dim1 == knownLabelCount + 4)
                {
                    Logger.LogDebug("[DeepLearning] 自动检测: YOLOv8/v11标准格式 (features={Dim1}, anchors={Dim2}, labels={KnownLabelCount})", dim1, dim2, knownLabelCount);
                    return YoloVersion.YOLOv8;
                }
            }
        }

        if (dim1 == 85 && dim2 > dim1)
        {
            Logger.LogDebug("[DeepLearning] 自动检测: YOLOv5/v6转置格式 (features={Dim1}, anchors={Dim2})", dim1, dim2);
            return YoloVersion.YOLOv5;
        }

        if (dim1 > dim2)
        {
            // [1, Many, Few]
            // Check feature count (dim2)
            if (dim2 == 85) // 4 box + 1 obj + 80 cls
            {
                Logger.LogDebug("[DeepLearning] 自动检测: YOLOv5/v6格式 (anchors={Dim1}, features={Dim2})", dim1, dim2);
                return YoloVersion.YOLOv5;
            }
            else // e.g. 84 (4 box + 80 cls)
            {
                Logger.LogDebug("[DeepLearning] 自动检测: YOLOv8/v11格式 (Transposed) (anchors={Dim1}, features={Dim2})", dim1, dim2);
                return YoloVersion.YOLOv8; // V8 logic handles V11 too
            }
        }
        else
        {
            // [1, Few, Many]
            // Typically V8/V11: [1, 84, 8400]
            Logger.LogDebug("[DeepLearning] 自动检测: YOLOv8/v11格式 (features={Dim1}, anchors={Dim2})", dim1, dim2);
            return YoloVersion.YOLOv8;
        }
    }

    private YoloVersion ParseYoloVersion(string version)
    {
        return version?.ToLower() switch
        {
            "auto" => YoloVersion.Auto,
            "v5" or "yolov5" or "5" => YoloVersion.YOLOv5,
            "v6" or "yolov6" or "6" => YoloVersion.YOLOv6,
            "v8" or "yolov8" or "8" => YoloVersion.YOLOv8,
            "v11" or "yolov11" or "11" => YoloVersion.YOLOv11,
            _ => YoloVersion.Auto
        };
    }

    /// <summary>
    /// 非极大值抑制 (NMS)
    /// </summary>
    private List<DetectionResult> ApplyNMS(List<DetectionResult> detections, float iouThreshold)
    {
        if (detections.Count == 0)
            return detections;

        // 按置信度排序
        var sorted = detections.OrderByDescending(d => d.Confidence).ToList();
        var keep = new List<DetectionResult>();
        var removed = new HashSet<int>();

        for (int i = 0; i < sorted.Count; i++)
        {
            if (removed.Contains(i))
                continue;

            keep.Add(sorted[i]);

            for (int j = i + 1; j < sorted.Count; j++)
            {
                if (removed.Contains(j))
                    continue;

                // 只比较相同类别的框
                if (sorted[i].ClassId != sorted[j].ClassId)
                    continue;

                // 计算 IoU
                float iou = CalculateIoU(sorted[i], sorted[j]);
                if (iou > iouThreshold)
                {
                    removed.Add(j);
                }
            }
        }

        return keep;
    }

    /// <summary>
    /// 计算 IoU
    /// </summary>
    private float CalculateIoU(DetectionResult a, DetectionResult b)
    {
        float x1 = Math.Max(a.X, b.X);
        float y1 = Math.Max(a.Y, b.Y);
        float x2 = Math.Min(a.X + a.Width, b.X + b.Width);
        float y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

        if (x2 < x1 || y2 < y1)
            return 0;

        float intersection = (x2 - x1) * (y2 - y1);
        float areaA = a.Width * a.Height;
        float areaB = b.Width * b.Height;
        float union = areaA + areaB - intersection;

        return union > 0 ? intersection / union : 0;
    }

    /// <summary>
    /// 解析目标类别字符串
    /// </summary>
    private HashSet<int>? ParseTargetClasses(string targetClassesStr, IReadOnlyList<string>? labels)
    {
        if (string.IsNullOrWhiteSpace(targetClassesStr))
            return null;

        var result = new HashSet<int>();
        var parts = targetClassesStr.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            // 尝试作为类别ID解析
            if (int.TryParse(trimmed, out var classId))
            {
                result.Add(classId);
            }
            else
            {
                // 尝试作为类别名称解析，查找对应的classId
                var index = FindClassIndex(labels, trimmed);
                if (index >= 0)
                {
                    result.Add(index);
                }
            }
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// 加载标签 - 支持自定义标签文件或自动查找
    /// </summary>
    /// <summary>
    /// Validates that named target classes exist in the active label set.
    /// </summary>
    private List<string> FindUnresolvedTargetClasses(string targetClassesStr, IReadOnlyList<string>? labels)
    {
        if (string.IsNullOrWhiteSpace(targetClassesStr))
        {
            return new List<string>();
        }

        var unresolved = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in targetClassesStr.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = part.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || int.TryParse(trimmed, out _))
            {
                continue;
            }

            if (FindClassIndex(labels, trimmed) >= 0 || !seen.Add(trimmed))
            {
                continue;
            }

            unresolved.Add(trimmed);
        }

        return unresolved;
    }

    /// <summary>
    /// 鍔犺浇鏍囩 - 鏀寔鑷畾涔夋爣绛炬枃浠舵垨鑷姩鏌ユ壘
    /// </summary>
    private string[] LoadLabels(string labelFile, string modelPath, string targetClassesStr = "")
    {
        string[] labels = Array.Empty<string>();

        // 1. 尝试加载用户指定的标签文件
        if (!string.IsNullOrEmpty(labelFile) && File.Exists(labelFile))
        {
            labels = File.ReadAllLines(labelFile)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();
            Logger.LogInformation("[DeepLearning] 加载自定义标签文件: {File}, 共 {Count} 个标签", labelFile, labels.Length);
            return labels;
        }

        // 2. 自动查找模型目录下的 labels.txt
        if (!string.IsNullOrEmpty(modelPath))
        {
            var modelDir = Path.GetDirectoryName(modelPath);
            if (!string.IsNullOrEmpty(modelDir))
            {
                var autoLabelFile = Path.Combine(modelDir, "labels.txt");
                if (File.Exists(autoLabelFile))
                {
                    labels = File.ReadAllLines(autoLabelFile)
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .ToArray();
                    Logger.LogInformation("[DeepLearning] 自动发现标签文件: {File}, 共 {Count} 个标签", autoLabelFile, labels.Length);
                    return labels;
                }
            }
        }

        // 3. 回退到 COCO 80 类默认标签
        var bundledLabelFile = TryResolveBundledLabelsPath(targetClassesStr);
        if (!string.IsNullOrEmpty(bundledLabelFile))
        {
            labels = File.ReadAllLines(bundledLabelFile)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();
            Logger.LogInformation("[DeepLearning] 使用内置标签文件: {File}, 共 {Count} 个标签", bundledLabelFile, labels.Length);
            return labels;
        }

        return CocoClassNames;
    }

    private string? TryResolveBundledLabelsPath(string targetClassesStr)
    {
        return DeepLearningLabelResolver.TryResolveBundledLabelsPath(targetClassesStr);
    }

    private static string ResolveLabelsPath(Operator @operator)
    {
        var labelsPath = @operator.Parameters
            .FirstOrDefault(parameter => parameter.Name.Equals("LabelsPath", StringComparison.OrdinalIgnoreCase))
            ?.Value
            ?.ToString();

        if (!string.IsNullOrWhiteSpace(labelsPath))
        {
            return labelsPath;
        }

        // Backward compatibility for older flows that still persisted LabelFile.
        return @operator.Parameters
            .FirstOrDefault(parameter => parameter.Name.Equals("LabelFile", StringComparison.OrdinalIgnoreCase))
            ?.Value
            ?.ToString()
            ?? string.Empty;
    }

    // 存储当前使用的标签数组
    /// <summary>
    /// 获取类别名称
    /// </summary>
    private string GetClassName(int classId, IReadOnlyList<string>? labels)
    {
        if (labels != null && classId >= 0 && classId < labels.Count)
            return labels[classId];
        if (classId >= 0 && classId < CocoClassNames.Length)
            return CocoClassNames[classId];
        return $"class_{classId}";
    }

    /// <summary>
    /// 绘制检测结果 - 返回Mat实现零拷贝 (P0优先级)
    /// </summary>
    private List<DetectionResult> BuildVisualizationDetections(
        List<DetectionResult> detections,
        float confidenceThreshold,
        bool enableInternalNms)
    {
        if (detections.Count == 0)
        {
            return detections;
        }

        if (enableInternalNms)
        {
            return detections;
        }

        // For preview readability we apply a visual-only NMS pass when the node is
        // configured to emit raw candidates to downstream BoxNms.
        var scoreFloor = Math.Max(confidenceThreshold, 0.25f);
        var filtered = detections
            .Where(detection => detection.Confidence >= scoreFloor)
            .ToList();
        if (filtered.Count == 0)
        {
            filtered = detections;
        }

        return ApplyNMS(filtered, 0.45f);
    }

    private static string BuildStatisticsLabel(int count, string detectionMode)
    {
        var isObjectMode = detectionMode.Equals("Object", StringComparison.OrdinalIgnoreCase);
        return isObjectMode
            ? $"Objects: {count}"
            : $"Defects: {count}";
    }

    private Mat DrawResults(Mat src, List<DetectionResult> detections, IReadOnlyList<string>? labels, string detectionMode)
    {
        var result = src.Clone();

        for (int i = 0; i < detections.Count; i++)
        {
            var det = detections[i];
            var color = ClassColors[det.ClassId % ClassColors.Length];

            // 绘制矩形框
            var rect = new Rect((int)det.X, (int)det.Y, (int)det.Width, (int)det.Height);
            Cv2.Rectangle(result, rect, color, 2);

            // 准备标签 - 使用真实类别名称
            var className = GetClassName(det.ClassId, labels);
            var label = $"{className}: {det.Confidence:P0}";

            // 计算标签大小
            var textSize = Cv2.GetTextSize(label, HersheyFonts.HersheySimplex, 0.5, 1, out var baseline);

            // 绘制标签背景
            var labelRect = new Rect(
                (int)det.X,
                (int)Math.Max(det.Y - textSize.Height - 5, 0),
                textSize.Width + 10,
                textSize.Height + 10
            );
            Cv2.Rectangle(result, labelRect, color, -1);

            // 绘制标签文字
            Cv2.PutText(
                result,
                label,
                new Point(det.X + 5, Math.Max(det.Y - 5, textSize.Height)),
                HersheyFonts.HersheySimplex,
                0.5,
                new Scalar(255, 255, 255),
                1,
                LineTypes.AntiAlias
            );
        }

        // 添加统计信息
        var stats = BuildStatisticsLabel(detections.Count, detectionMode);
        Cv2.PutText(
            result,
            stats,
            new Point(10, 30),
            HersheyFonts.HersheySimplex,
            0.7,
            new Scalar(0, 255, 0),
            2,
            LineTypes.AntiAlias
        );

        return result;
    }

    private static int FindClassIndex(IReadOnlyList<string>? labels, string className)
    {
        if (labels != null)
        {
            for (var i = 0; i < labels.Count; i++)
            {
                if (string.Equals(labels[i], className, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }

        return Array.FindIndex(CocoClassNames, name =>
            string.Equals(name, className, StringComparison.OrdinalIgnoreCase));
    }



    /// <summary>
    /// 检测结果结构
    /// </summary>
    private class DetectionResult
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public float Confidence { get; set; }
        public int ClassId { get; set; }
    }
}
