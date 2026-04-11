// DeepLearningOperator.cs
// 深度学习算子 - 使用 ONNX 模型进行 AI 缺陷检测
// 作者：蘅芜君

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Acme.Product.Infrastructure.AI.Runtime;
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
[OutputPort("OriginalImage", "原始图像", PortDataType.Image)]
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
[OperatorParam("LabelsPath", "标签文件路径", "file", Description = "自定义标签文件路径（每行一个标签），为空则优先使用模型 metadata names 或自动查找模型目录下的 labels.txt；仍不可用时执行失败", DefaultValue = "")]
[OperatorParam("EnableInternalNms", "启用内部NMS", "bool", Description = "关闭后输出置信度筛选后的候选框，由下游 BoxNms 负责唯一 NMS。", DefaultValue = true)]
[OperatorParam("DetectionMode", "检测模式", "enum", Description = "缺陷检测：检出目标视为缺陷(NG)；目标检测：检出目标视为正常(OK)", DefaultValue = "Defect", Options = new[] { "Defect|缺陷检测", "Object|目标检测" })]
public class DeepLearningOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.DeepLearning;

    public DeepLearningOperator(ILogger<DeepLearningOperator> logger) : base(logger) { }

    /// <summary>
    /// 模型缓存 - 避免重复加载
    /// </summary>
    private static readonly ConcurrentDictionary<string, CachedModelSession> _modelCache = new();

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
    private sealed class LabelSourceInfo
    {
        public required string[] Labels { get; init; }
        public required string Source { get; init; }
        public string Path { get; init; } = string.Empty;
        public bool IsFileBacked { get; init; }
    }

    private sealed class CachedModelSession
    {
        private int _leaseCount;
        private int _disposeRequested;
        private int _disposed;

        public CachedModelSession(InferenceSession session)
        {
            Session = session;
        }

        public InferenceSession Session { get; }

        public bool TryAcquire([NotNullWhen(true)] out ModelSessionLease? lease)
        {
            lease = null;

            if (Volatile.Read(ref _disposeRequested) != 0)
            {
                return false;
            }

            Interlocked.Increment(ref _leaseCount);

            if (Volatile.Read(ref _disposeRequested) != 0)
            {
                Release();
                return false;
            }

            lease = new ModelSessionLease(this);
            return true;
        }

        public void MarkForDisposal()
        {
            Interlocked.Exchange(ref _disposeRequested, 1);
            TryDispose();
        }

        private void Release()
        {
            var remainingLeases = Interlocked.Decrement(ref _leaseCount);
            if (remainingLeases < 0)
            {
                throw new InvalidOperationException("Model session lease count dropped below zero.");
            }

            if (remainingLeases == 0)
            {
                TryDispose();
            }
        }

        private void TryDispose()
        {
            if (Volatile.Read(ref _disposeRequested) == 0 || Volatile.Read(ref _leaseCount) != 0)
            {
                return;
            }

            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                Session.Dispose();
            }
        }

        public sealed class ModelSessionLease : IDisposable
        {
            private CachedModelSession? _owner;

            public ModelSessionLease(CachedModelSession owner)
            {
                _owner = owner;
            }

            public InferenceSession Session => _owner?.Session ?? throw new ObjectDisposedException(nameof(ModelSessionLease));

            public void Dispose()
            {
                Interlocked.Exchange(ref _owner, null)?.Release();
            }
        }
    }

    private sealed class LabelContract
    {
        public required string[] ResolvedLabels { get; init; }
        public required string[] MetadataLabels { get; init; }
        public required string[] ExternalLabels { get; init; }
        public required string ResolvedLabelSource { get; init; }
        public string ResolvedLabelPath { get; init; } = string.Empty;
        public string ValidationStatus { get; init; } = "Unknown";
        public string? ValidationMessage { get; init; }
        public bool IsValid => string.IsNullOrWhiteSpace(ValidationMessage);
    }

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
        var labels = Array.Empty<string>();
        var unresolvedTargetClasses = new List<string>();
        HashSet<int>? targetClasses = null;

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
        using var modelSessionLease = AcquireModelSessionWithVerifiedExecutionProvider(modelPath, useGpu, gpuDeviceId, cancellationToken);
        if (modelSessionLease == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("模型加载失败"));
        }

        var session = modelSessionLease.Session;

        // 6. 预处理图像
        var labelContract = ResolveLabelContract(session, labelsPath, modelPath, targetClassesStr);
        if (!labelContract.IsValid)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(labelContract.ValidationMessage!));
        }

        labels = labelContract.ResolvedLabels;
        unresolvedTargetClasses = FindUnresolvedTargetClasses(targetClassesStr, labels);
        if (unresolvedTargetClasses.Count > 0)
        {
            const string labelSource = "the active labels";
            return Task.FromResult(OperatorExecutionOutput.Failure(
                $"Failed to resolve TargetClasses [{string.Join(", ", unresolvedTargetClasses)}] against {labelSource}. Set LabelsPath or place labels.txt next to the model."));
        }

        targetClasses = ParseTargetClasses(targetClassesStr, labels);
        Logger.LogInformation(
            "[DeepLearning] Using {Count} labels. TargetClasses={TargetStr}, LabelSource={LabelSource}, ValidationStatus={ValidationStatus}",
            labels.Length,
            targetClassesStr,
            labelContract.ResolvedLabelSource,
            labelContract.ValidationStatus);
        Logger.LogInformation(
            "[DeepLearning] Label contract resolved. LabelContractSource={LabelContractSource}, LabelContractStatus={LabelContractStatus}",
            labelContract.ResolvedLabelSource,
            labelContract.ValidationStatus);

        var inputTensor = PreprocessImage(src, inputSize);
        Logger.LogDebug("[DeepLearning] 输入张量形状: [1, 3, {InputSize}, {InputSize}]", inputSize, inputSize);

        // 7. 执行推理
        var inferenceOutput = RunInference(session, inputTensor, labels.Length);
        var outputTensor = inferenceOutput.Tensor;
        Logger.LogInformation(
            "[DeepLearning] Output tensor selected. OutputTensorName={OutputTensorName}, OutputTensorShape={OutputTensorShape}, SelectionRule={SelectionRule}",
            inferenceOutput.OutputName,
            string.Join(", ", inferenceOutput.OutputShape),
            inferenceOutput.SelectionRule);

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
        var outputDetections = new List<Core.ValueObjects.DetectionResult>(detections.Count);
        foreach (var detection in detections)
        {
            outputDetections.Add(new Core.ValueObjects.DetectionResult
            {
                Label = GetClassName(detection.ClassId, labels),
                Confidence = detection.Confidence,
                X = detection.X,
                Y = detection.Y,
                Width = detection.Width,
                Height = detection.Height
            });
        }

        var detectionList = new DetectionList(outputDetections);

        // 输出原始图像（不带任何绘制），供下游节点重新绘制
        var originalImage = src.Clone();
        
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
            { "DefectCount", isObjectMode ? 0 : detections.Count },
            { "OriginalImage", new ImageWrapper(originalImage) },
            { "LabelSource", labelContract.ResolvedLabelSource },
            { "ResolvedLabels", labelContract.ResolvedLabels },
            { "ModelMetadataLabels", labelContract.MetadataLabels },
            { "LabelsPath", labelContract.ResolvedLabelPath },
            { "LabelValidationStatus", labelContract.ValidationStatus }
        };

        Logger.LogInformation("[DeepLearning] 执行完毕. 检测总数: {Count}, 过滤后输出: {DefectCount}", detections.Count, detections.Count);

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(outputImage, additionalData)));
    }

    private CachedModelSession.ModelSessionLease? AcquireModelSessionWithVerifiedExecutionProvider(
        string modelPath,
        bool useGpu = true,
        int gpuDeviceId = 0,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{modelPath}_gpu_{useGpu}_{gpuDeviceId}";
        if (_modelCache.TryGetValue(cacheKey, out var cachedSessionEntry))
        {
            if (cachedSessionEntry.TryAcquire(out var cachedLease))
            {
                TouchModelCacheKey(cacheKey);
                return cachedLease;
            }

            _modelCache.TryRemove(new KeyValuePair<string, CachedModelSession>(cacheKey, cachedSessionEntry));
        }

        var lockObj = _modelLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));

        var lockAcquired = false;
        try
        {
            lockObj.WaitAsync(cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
            lockAcquired = true;

            if (_modelCache.TryGetValue(cacheKey, out cachedSessionEntry))
            {
                if (cachedSessionEntry.TryAcquire(out var cachedLease))
                {
                    TouchModelCacheKey(cacheKey);
                    return cachedLease;
                }

                _modelCache.TryRemove(new KeyValuePair<string, CachedModelSession>(cacheKey, cachedSessionEntry));
            }

            var sessionOptions = new SessionOptions
            {
                InterOpNumThreads = 4,
                IntraOpNumThreads = 4,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
            };

            var activeExecutionProvider = "CPU";

            if (useGpu && GpuAvailabilityChecker.IsCudaAvailable)
            {
                if (TryAppendTensorRtExecutionProvider(sessionOptions, gpuDeviceId, out var tensorRtFailureReason))
                {
                    activeExecutionProvider = "TensorRT";
                    Logger.LogInformation("[DeepLearning] TensorRT execution provider enabled, device ID: {DeviceId}", gpuDeviceId);
                }
                else
                {
                    if (GpuAvailabilityChecker.IsTensorRtAvailable)
                    {
                        Logger.LogWarning(
                            "[DeepLearning] TensorRT detected but not enabled. Falling back to CUDA. Reason: {Reason}",
                            tensorRtFailureReason);
                    }

                    try
                    {
                        sessionOptions.AppendExecutionProvider_CUDA(gpuDeviceId);
                        activeExecutionProvider = "CUDA";
                        Logger.LogInformation("[DeepLearning] CUDA execution provider enabled, device ID: {DeviceId}", gpuDeviceId);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "[DeepLearning] GPU execution provider enable failed, falling back to CPU");
                    }
                }
            }
            else
            {
                Logger.LogInformation("[DeepLearning] Using CPU execution provider");
            }

            var session = new InferenceSession(modelPath, sessionOptions);
            var cacheEntry = new CachedModelSession(session);

            lock (_modelCacheEvictionLock)
            {
                EvictModelsIfNeeded();
                _modelCache[cacheKey] = cacheEntry;
                TouchModelCacheKey(cacheKey);
            }

            Logger.LogDebug("[DeepLearning] Model loaded successfully with execution provider: {ExecutionProvider}", activeExecutionProvider);
            if (!cacheEntry.TryAcquire(out var createdLease))
            {
                throw new InvalidOperationException("Newly created model session could not be acquired.");
            }

            return createdLease;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[DeepLearning] Failed to load model with verified execution provider");
            return null;
        }
        finally
        {
            if (lockAcquired)
            {
                lockObj.Release();
            }
        }
    }

    private bool TryAppendTensorRtExecutionProvider(SessionOptions sessionOptions, int gpuDeviceId, out string failureReason)
    {
        failureReason = string.Empty;

        if (!GpuAvailabilityChecker.IsTensorRtAvailable)
        {
            failureReason = "TensorRT was not detected on this machine.";
            return false;
        }

        try
        {
            var tensorRtMethod = typeof(SessionOptions)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(method =>
                    string.Equals(method.Name, "AppendExecutionProvider_TensorRT", StringComparison.Ordinal) &&
                    method.GetParameters().Length == 1);

            if (tensorRtMethod is null)
            {
                failureReason = "The current OnnxRuntime package does not expose TensorRT provider APIs.";
                return false;
            }

            var optionsParameterType = tensorRtMethod.GetParameters()[0].ParameterType;
            var providerOptions = Activator.CreateInstance(optionsParameterType);
            if (providerOptions is null)
            {
                failureReason = $"Unable to create TensorRT provider options of type '{optionsParameterType.FullName}'.";
                return false;
            }

            SetTensorRtDeviceId(providerOptions, gpuDeviceId);
            tensorRtMethod.Invoke(sessionOptions, new[] { providerOptions });
            return true;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            failureReason = ex.InnerException.Message;
            return false;
        }
        catch (Exception ex)
        {
            failureReason = ex.Message;
            return false;
        }
    }

    private static void SetTensorRtDeviceId(object providerOptions, int gpuDeviceId)
    {
        var optionsType = providerOptions.GetType();
        var candidateProperties = new[] { "DeviceId", "GpuDeviceId" };

        foreach (var propertyName in candidateProperties)
        {
            var property = optionsType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property is null || !property.CanWrite || property.PropertyType != typeof(int))
            {
                continue;
            }

            property.SetValue(providerOptions, gpuDeviceId);
            return;
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
                    session.MarkForDisposal();
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
                oldSession.MarkForDisposal();
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
        using var normalizedSrc = NormalizeToBgr8(src);

        // 1. 计算缩放比例（保持宽高比）
        var scale = Math.Min((float)inputSize / normalizedSrc.Width, (float)inputSize / normalizedSrc.Height);
        var newWidth = (int)(normalizedSrc.Width * scale);
        var newHeight = (int)(normalizedSrc.Height * scale);

        // 2. Resize
        using var resized = new Mat();
        Cv2.Resize(normalizedSrc, resized, new Size(newWidth, newHeight), 0, 0, InterpolationFlags.Linear);

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
        var tensorData = new float[tensorSize];
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
                tensorData[pixelIndex] = pixel.Item2;
                tensorData[channelSize + pixelIndex] = pixel.Item1;
                tensorData[(channelSize * 2) + pixelIndex] = pixel.Item0;
            }
        }

        return new DenseTensor<float>(tensorData, new[] { 1, 3, inputSize, inputSize });
    }

    private static Mat NormalizeToBgr8(Mat src)
    {
        if (src.Empty())
        {
            throw new ArgumentException("Source image must not be empty.", nameof(src));
        }

        using var normalizedDepth = new Mat();
        ConvertToByteDepth(src, normalizedDepth);

        if (normalizedDepth.Channels() == 3)
        {
            return normalizedDepth.Clone();
        }

        var converted = new Mat();
        switch (normalizedDepth.Channels())
        {
            case 1:
                Cv2.CvtColor(normalizedDepth, converted, ColorConversionCodes.GRAY2BGR);
                return converted;
            case 4:
                Cv2.CvtColor(normalizedDepth, converted, ColorConversionCodes.BGRA2BGR);
                return converted;
            default:
                throw new NotSupportedException($"Unsupported channel count for deep learning preprocessing: {normalizedDepth.Channels()}.");
        }
    }

    private static void ConvertToByteDepth(Mat src, Mat dst)
    {
        var targetType = MatType.MakeType(MatType.CV_8U, src.Channels());
        switch (src.Depth())
        {
            case MatType.CV_8U:
                src.CopyTo(dst);
                return;
            case MatType.CV_16U:
                src.ConvertTo(dst, targetType, 1.0 / 256.0);
                return;
            case MatType.CV_32F:
            case MatType.CV_64F:
                var (floatMin, floatMax) = GetGlobalMinMax(src);
                if (floatMin >= 0d && floatMax <= 1d)
                {
                    src.ConvertTo(dst, targetType, 255.0);
                    return;
                }

                if (floatMin >= 0d && floatMax <= 255d)
                {
                    src.ConvertTo(dst, targetType);
                    return;
                }

                ConvertToByteDepthWithRangeNormalization(src, dst, targetType, floatMin, floatMax);
                return;
            default:
                var (minValue, maxValue) = GetGlobalMinMax(src);
                ConvertToByteDepthWithRangeNormalization(src, dst, targetType, minValue, maxValue);
                return;
        }
    }

    private static void ConvertToByteDepthWithRangeNormalization(Mat src, Mat dst, MatType targetType, double minValue, double maxValue)
    {
        if (!double.IsFinite(minValue) || !double.IsFinite(maxValue))
        {
            throw new InvalidOperationException("Input image contains non-finite values and cannot be normalized.");
        }

        if (maxValue <= minValue)
        {
            src.ConvertTo(dst, targetType, 0.0, 0.0);
            return;
        }

        var scale = 255.0 / (maxValue - minValue);
        var shift = -minValue * scale;
        src.ConvertTo(dst, targetType, scale, shift);
    }

    private static (double Min, double Max) GetGlobalMinMax(Mat src)
    {
        if (src.Channels() == 1)
        {
            double minValue;
            double maxValue;
            Cv2.MinMaxLoc(src, out minValue, out maxValue);
            return (minValue, maxValue);
        }

        Cv2.Split(src, out var channels);
        try
        {
            var minValue = double.PositiveInfinity;
            var maxValue = double.NegativeInfinity;
            foreach (var channel in channels)
            {
                double channelMin;
                double channelMax;
                Cv2.MinMaxLoc(channel, out channelMin, out channelMax);
                minValue = Math.Min(minValue, channelMin);
                maxValue = Math.Max(maxValue, channelMax);
            }

            return (minValue, maxValue);
        }
        finally
        {
            foreach (var channel in channels)
            {
                channel.Dispose();
            }
        }
    }

    private readonly record struct InferenceTensorSelection(
        DenseTensor<float> Tensor,
        string OutputName,
        int[] OutputShape,
        string SelectionRule);

    /// <summary>
    /// 执行推理
    /// </summary>
    private InferenceTensorSelection RunInference(InferenceSession session, DenseTensor<float> inputTensor, int knownLabelCount)
    {
        var inputName = session.InputMetadata.Keys.First();
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
        };

        using var results = session.Run(inputs);
        var outputNames = new List<string>();
        var outputShapes = new List<int[]>();
        var outputTensors = new List<Tensor<float>>();

        foreach (var output in results)
        {
            try
            {
                var tensor = output.AsTensor<float>();
                outputNames.Add(output.Name);
                outputShapes.Add(tensor.Dimensions.ToArray());
                outputTensors.Add(tensor);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "[DeepLearning] Ignoring non-float output tensor: {OutputName}", output.Name);
            }
        }

        if (outputTensors.Count == 0)
        {
            throw new InvalidOperationException("No float output tensor was produced by the model.");
        }

        var selection = SelectDetectionOutputIndex(outputNames, outputShapes, knownLabelCount);
        var selectedShape = outputShapes[selection.SelectedIndex];
        var selectedTensor = outputTensors[selection.SelectedIndex];
        var selectedDenseTensor = new DenseTensor<float>(selectedTensor.ToArray(), selectedShape);

        return new InferenceTensorSelection(
            selectedDenseTensor,
            outputNames[selection.SelectedIndex],
            selectedShape,
            selection.SelectionRule);
    }

    private static (int SelectedIndex, string SelectionRule) SelectDetectionOutputIndex(
        IReadOnlyList<string> outputNames,
        IReadOnlyList<int[]> outputShapes,
        int knownLabelCount)
    {
        if (outputNames.Count == 0 || outputShapes.Count == 0 || outputNames.Count != outputShapes.Count)
        {
            throw new ArgumentException("Output tensor names and shapes must be non-empty and aligned.");
        }

        if (knownLabelCount > 0)
        {
            var bestIndex = -1;
            var bestAnchor = -1;
            var bestRule = string.Empty;

            for (var i = 0; i < outputShapes.Count; i++)
            {
                if (!TryMatchKnownLabelShape(outputShapes[i], knownLabelCount, out var anchorDim, out var rule))
                {
                    continue;
                }

                if (anchorDim > bestAnchor)
                {
                    bestAnchor = anchorDim;
                    bestIndex = i;
                    bestRule = rule;
                }
            }

            if (bestIndex >= 0)
            {
                return (bestIndex, bestRule);
            }
        }

        var heuristicIndex = -1;
        var heuristicScore = int.MinValue;
        for (var i = 0; i < outputShapes.Count; i++)
        {
            if (!TryGetRank3DetectionScore(outputShapes[i], out var score))
            {
                continue;
            }

            if (score > heuristicScore)
            {
                heuristicScore = score;
                heuristicIndex = i;
            }
        }

        if (heuristicIndex >= 0)
        {
            return (heuristicIndex, "Rank3Heuristic");
        }

        var firstRank3Index = outputShapes
            .Select((shape, index) => (shape, index))
            .FirstOrDefault(candidate => candidate.shape.Length == 3)
            .index;
        if (firstRank3Index > 0 || outputShapes[0].Length == 3)
        {
            return (firstRank3Index, "Rank3Fallback");
        }

        return (0, "FirstOutputFallback");
    }

    private static bool TryMatchKnownLabelShape(int[] shape, int knownLabelCount, out int anchorDim, out string rule)
    {
        anchorDim = 0;
        rule = string.Empty;

        if (shape.Length != 3)
        {
            return false;
        }

        if (TryMatchFeatureDimension(shape[1], shape[2], knownLabelCount, out rule))
        {
            anchorDim = shape[2];
            return true;
        }

        if (TryMatchFeatureDimension(shape[2], shape[1], knownLabelCount, out rule))
        {
            anchorDim = shape[1];
            return true;
        }

        return false;
    }

    private static bool TryMatchFeatureDimension(int featureDim, int anchorDim, int knownLabelCount, out string rule)
    {
        rule = string.Empty;
        if (anchorDim <= featureDim)
        {
            return false;
        }

        if (featureDim == knownLabelCount + 4)
        {
            rule = "KnownLabelFeature+4";
            return true;
        }

        if (featureDim == knownLabelCount + 5)
        {
            rule = "KnownLabelFeature+5";
            return true;
        }

        return false;
    }

    private static bool TryGetRank3DetectionScore(int[] shape, out int score)
    {
        score = int.MinValue;
        if (shape.Length != 3)
        {
            return false;
        }

        var dimA = shape[1];
        var dimB = shape[2];
        var anchorDim = Math.Max(dimA, dimB);
        var featureDim = Math.Min(dimA, dimB);
        if (anchorDim < 16 || featureDim < 4 || featureDim > 512)
        {
            return false;
        }

        score = (anchorDim * 1024) - featureDim;
        return true;
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
            var filteredDetections = new List<DetectionResult>(detections.Count);
            foreach (var detection in detections)
            {
                if (targetClasses.Contains(detection.ClassId))
                {
                    filteredDetections.Add(detection);
                }
            }

            detections = filteredDetections;
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

        var keep = new List<DetectionResult>(detections.Count);
        var indicesByClass = new Dictionary<int, List<int>>();
        for (var i = 0; i < detections.Count; i++)
        {
            var classId = detections[i].ClassId;
            if (!indicesByClass.TryGetValue(classId, out var indices))
            {
                indices = new List<int>();
                indicesByClass[classId] = indices;
            }

            indices.Add(i);
        }

        foreach (var indices in indicesByClass.Values)
        {
            indices.Sort((left, right) => detections[right].Confidence.CompareTo(detections[left].Confidence));
            var removed = new bool[indices.Count];
            for (int i = 0; i < indices.Count; i++)
            {
                if (removed[i])
                    continue;

                var current = detections[indices[i]];
                keep.Add(current);

                for (int j = i + 1; j < indices.Count; j++)
                {
                    if (removed[j])
                        continue;

                    if (CalculateIoU(current, detections[indices[j]]) > iouThreshold)
                    {
                        removed[j] = true;
                    }
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

    private string? TryResolveBundledLabelsPath(string targetClassesStr)
    {
        return DeepLearningLabelResolver.TryResolveBundledLabelsPath(targetClassesStr);
    }

    private LabelContract ResolveLabelContract(
        InferenceSession session,
        string configuredLabelsPath,
        string modelPath,
        string targetClassesStr)
    {
        var metadataLabels = DeepLearningLabelResolver.GetMetadataLabels(session);
        var externalLabels = LoadExternalLabels(configuredLabelsPath, modelPath, targetClassesStr);
        return BuildLabelContract(modelPath, metadataLabels, externalLabels);
    }

    private LabelContract BuildLabelContract(
        string modelPath,
        string[] metadataLabels,
        LabelSourceInfo externalLabels)
    {
        if (metadataLabels.Length > 0)
        {
            Logger.LogInformation("[DeepLearning] Loaded {Count} labels from ONNX metadata.", metadataLabels.Length);

            if (externalLabels.IsFileBacked && !LabelSequencesEqual(metadataLabels, externalLabels.Labels))
            {
                return new LabelContract
                {
                    ResolvedLabels = metadataLabels,
                    MetadataLabels = metadataLabels,
                    ExternalLabels = externalLabels.Labels,
                    ResolvedLabelSource = "ModelMetadata",
                    ResolvedLabelPath = externalLabels.Path,
                    ValidationStatus = "Mismatch",
                    ValidationMessage = BuildLabelContractMismatchMessage(modelPath, externalLabels, metadataLabels)
                };
            }

            return new LabelContract
            {
                ResolvedLabels = metadataLabels,
                MetadataLabels = metadataLabels,
                ExternalLabels = externalLabels.IsFileBacked ? externalLabels.Labels : Array.Empty<string>(),
                ResolvedLabelSource = "ModelMetadata",
                ResolvedLabelPath = externalLabels.Path,
                ValidationStatus = externalLabels.IsFileBacked
                    ? "MetadataValidatedWithExternalLabels"
                    : "MetadataOnly"
            };
        }

        if (externalLabels.Labels.Length == 0)
        {
            return new LabelContract
            {
                ResolvedLabels = Array.Empty<string>(),
                MetadataLabels = Array.Empty<string>(),
                ExternalLabels = Array.Empty<string>(),
                ResolvedLabelSource = externalLabels.Source,
                ResolvedLabelPath = externalLabels.Path,
                ValidationStatus = "MissingLabelContract",
                ValidationMessage = BuildMissingLabelContractMessage(modelPath)
            };
        }

        return new LabelContract
        {
            ResolvedLabels = externalLabels.Labels,
            MetadataLabels = Array.Empty<string>(),
            ExternalLabels = externalLabels.Labels,
            ResolvedLabelSource = externalLabels.Source,
            ResolvedLabelPath = externalLabels.Path,
            ValidationStatus = "ExternalLabelsOnly"
        };
    }

    private LabelSourceInfo LoadExternalLabels(string labelFile, string modelPath, string targetClassesStr)
    {
        if (!string.IsNullOrWhiteSpace(labelFile) && File.Exists(labelFile))
        {
            var labels = DeepLearningLabelResolver.ReadLabelsFromFile(labelFile);
            Logger.LogInformation("[DeepLearning] Loaded labels from explicit LabelsPath: {File}", labelFile);
            return new LabelSourceInfo
            {
                Labels = labels,
                Source = "ExplicitFile",
                Path = labelFile,
                IsFileBacked = true
            };
        }

        if (!string.IsNullOrWhiteSpace(modelPath))
        {
            var modelDir = Path.GetDirectoryName(modelPath);
            if (!string.IsNullOrWhiteSpace(modelDir))
            {
                var autoLabelFile = Path.Combine(modelDir, "labels.txt");
                if (File.Exists(autoLabelFile))
                {
                    var labels = DeepLearningLabelResolver.ReadLabelsFromFile(autoLabelFile);
                    Logger.LogInformation("[DeepLearning] Loaded labels from model directory: {File}", autoLabelFile);
                    return new LabelSourceInfo
                    {
                        Labels = labels,
                        Source = "ModelDirectoryFile",
                        Path = autoLabelFile,
                        IsFileBacked = true
                    };
                }
            }
        }

        var bundledLabelFile = TryResolveBundledLabelsPath(targetClassesStr);
        if (!string.IsNullOrEmpty(bundledLabelFile))
        {
            var labels = DeepLearningLabelResolver.ReadLabelsFromFile(bundledLabelFile);
            Logger.LogInformation("[DeepLearning] Loaded bundled labels: {File}", bundledLabelFile);
            return new LabelSourceInfo
            {
                Labels = labels,
                Source = "BundledFile",
                Path = bundledLabelFile,
                IsFileBacked = true
            };
        }

        return new LabelSourceInfo
        {
            Labels = Array.Empty<string>(),
            Source = "Unavailable",
            Path = string.Empty,
            IsFileBacked = false
        };
    }

    private static bool LabelSequencesEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string BuildLabelContractMismatchMessage(
        string modelPath,
        LabelSourceInfo externalLabels,
        IReadOnlyList<string> metadataLabels)
    {
        var externalLabelPath = string.IsNullOrWhiteSpace(externalLabels.Path)
            ? "<not provided>"
            : externalLabels.Path;

        return string.Join(
            Environment.NewLine,
            "Label contract mismatch: ONNX metadata names do not match the external labels file.",
            $"ModelPath: {modelPath}",
            $"LabelsPath: {externalLabelPath}",
            $"ModelMetadataLabels: {FormatLabelSequence(metadataLabels)}",
            $"ExternalLabels: {FormatLabelSequence(externalLabels.Labels)}",
            "Update labels.txt to match the model export order, or remove the mismatched external labels file.");
    }

    private static string BuildMissingLabelContractMessage(string modelPath)
    {
        return string.Join(
            Environment.NewLine,
            "Label contract missing: the model does not expose ONNX metadata names and no valid labels file was found.",
            $"ModelPath: {modelPath}",
            "Provide LabelsPath, place labels.txt next to the model, or export the ONNX model with metadata names.");
    }

    private static string FormatLabelSequence(IReadOnlyList<string> labels)
    {
        return labels.Count == 0
            ? "<empty>"
            : string.Join(", ", labels);
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
        var filtered = new List<DetectionResult>(detections.Count);
        foreach (var detection in detections)
        {
            if (detection.Confidence >= scoreFloor)
            {
                filtered.Add(detection);
            }
        }
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

        return -1;
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
