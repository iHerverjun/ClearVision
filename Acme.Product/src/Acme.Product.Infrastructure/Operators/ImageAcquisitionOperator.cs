// ImageAcquisitionOperator.cs
// 图像采集算子 - 支持相机和文件采集
// 作者：蘅芜君

using Acme.Product.Core.Cameras;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 图像采集算子 - 支持相机和文件采集
/// </summary>
[OperatorMeta(
    DisplayName = "图像采集",
    Description = "从文件或相机采集图像",
    Category = "采集",
    IconName = "camera",
    Keywords = new[] { "采集", "相机", "拍照", "取图", "摄像头", "图像输入", "Acquire", "Camera", "Capture" }
)]
[InputPort("Image", "输入图像", PortDataType.Image, IsRequired = false)]
[InputPort("FilePath", "文件路径输入", PortDataType.String, IsRequired = false)]
[OutputPort("Image", "图像", PortDataType.Image)]
[OperatorParam("SourceType", "采集源", "enum", DefaultValue = "File", Options = new[] { "File|文件", "Camera|相机" })]
[OperatorParam("FilePath", "文件路径", "file", DefaultValue = "")]
[OperatorParam("CameraId", "相机", "cameraBinding", DefaultValue = "")]
[OperatorParam("ExposureTime", "曝光时间(us)", "double", DefaultValue = 5000.0, Min = 1.0)]
[OperatorParam("Gain", "增益(dB)", "double", DefaultValue = 1.0, Min = 0.0)]
[OperatorParam("TriggerMode", "触发模式", "enum", DefaultValue = "Software", Options = new[] { "Software|软件触发", "External|外部触发" })]
public class ImageAcquisitionOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ImageAcquisition;
    private readonly ICameraManager _cameraManager;

    public ImageAcquisitionOperator(ILogger<ImageAcquisitionOperator> logger, ICameraManager cameraManager) : base(logger)
    {
        _cameraManager = cameraManager;
    }

    protected override async Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        // 优先获取 sourceType 和 filePath 参数
        // 1. 尝试从连线输入获取
        // 2. 如果没有连线输入，从算子自身的参数列表中获取 (Metadata-driven)
        var sourceType = TryGetStringInput(inputs, "SourceType")
            ?? TryGetStringInput(inputs, "sourceType")
            ?? GetStringParam(@operator, "SourceType", GetStringParam(@operator, "sourceType", string.Empty));

        var filePath = TryGetStringInput(inputs, "FilePath")
            ?? TryGetStringInput(inputs, "filePath")
            ?? GetStringParam(@operator, "FilePath", GetStringParam(@operator, "filePath", string.Empty));

        // 如果连线输入中有名为 Image 的数据，则直接使用（透传模式）
        if (inputs != null && inputs.TryGetValue("Image", out var imgObj) && imgObj != null)
        {
            if (ImageWrapper.TryGetFromObject(imgObj, out var wrapper) && wrapper != null)
            {
                // P0: 透传 ImageWrapper 时必须增加引用计数，因为当前算子结束后会 Release 输入
                return OperatorExecutionOutput.Success(new Dictionary<string, object>
                {
                    { "Image", wrapper.AddRef() },
                    { "Width", wrapper.Width },
                    { "Height", wrapper.Height },
                    { "Channels", wrapper.Channels }
                });
            }

            if (imgObj is byte[] rawData)
            {
                // 【优化】直接从PNG头部解析尺寸，避免完整解码
                var dimensions = ImageWrapper.TryParsePngDimensions(rawData);
                var width = dimensions?.width ?? 0;
                var height = dimensions?.height ?? 0;
                var channels = dimensions?.channels ?? 3;

                // 如果PNG解析失败（非PNG格式），回退到ImageWrapper的延迟属性
                if (width == 0 || height == 0)
                {
                    var w = ImageWrapper.FromBytes(rawData);
                    width = w.Width;
                    height = w.Height;
                    channels = w.Channels;
                }

                return OperatorExecutionOutput.Success(new Dictionary<string, object>
                {
                    { "Image", rawData },
                    { "Width", width },
                    { "Height", height },
                    { "Channels", channels }
                });
            }
        }

        var hasExplicitFilePath = !string.IsNullOrEmpty(filePath);

        // 如果显式配置了文件路径，文件模式优先于采集源枚举。
        if (hasExplicitFilePath)
        {
            if (!File.Exists(filePath))
            {
                return OperatorExecutionOutput.Failure($"图像文件不存在: {filePath}");
            }

            var mat = Cv2.ImRead(filePath, ImreadModes.Color);
            if (mat.Empty())
            {
                return OperatorExecutionOutput.Failure("无法加载图像文件，格式可能不受支持");
            }

            return OperatorExecutionOutput.Success(CreateImageOutput(mat, new Dictionary<string, object>
            {
                { "Channels", mat.Channels() }
            }));
        }

        // 如果是相机模式
        if (sourceType?.Equals("Camera", StringComparison.OrdinalIgnoreCase) == true)
        {
            var cameraId = GetStringParam(@operator, "CameraId", GetStringParam(@operator, "cameraId", string.Empty));
            if (string.IsNullOrEmpty(cameraId))
            {
                throw new InvalidOperationException("未选择相机");
            }

            try
            {
                // 获取并配置相机
                var camera = await _cameraManager.GetOrCreateByBindingAsync(cameraId);
                var bindingConfig = _cameraManager.GetBindings()
                    .FirstOrDefault(b => b.Id.Equals(cameraId, StringComparison.OrdinalIgnoreCase));

                // 相机参数优先来自“系统设置 -> 相机管理”，保留旧算子参数作为向后兼容 fallback。
                var exposureTime = bindingConfig?.ExposureTimeUs
                    ?? GetDoubleParam(@operator, "ExposureTime", GetDoubleParam(@operator, "exposureTime", 5000));
                var gain = bindingConfig?.GainDb
                    ?? GetDoubleParam(@operator, "Gain", GetDoubleParam(@operator, "gain", 1.0));
                await camera.SetExposureTimeAsync(exposureTime);
                await camera.SetGainAsync(gain);

                if (camera is IIndustrialCamera industrialCamera)
                {
                    var triggerMode = bindingConfig?.TriggerMode
                        ?? GetStringParam(@operator, "TriggerMode", GetStringParam(@operator, "triggerMode", "Software"));
                    if (triggerMode.Equals("Software", StringComparison.OrdinalIgnoreCase))
                    {
                        // Current adapter maps false -> software trigger mode in provider SDKs.
                        await industrialCamera.SetTriggerModeAsync(false);
                        await industrialCamera.ExecuteSoftwareTriggerAsync();
                    }
                    else
                    {
                        await industrialCamera.SetTriggerModeAsync(true);
                    }
                }

                // 采集图像
                var imageData = await camera.AcquireSingleFrameAsync();

                // 解码图像以获取尺寸信息
                var mat = Cv2.ImDecode(imageData, ImreadModes.Color);
                if (mat.Empty())
                {
                    return OperatorExecutionOutput.Failure("相机返回的图像数据无效");
                }

                return OperatorExecutionOutput.Success(CreateImageOutput(mat, new Dictionary<string, object>
                {
                    { "Channels", mat.Channels() },
                    { "Source", "camera" },
                    { "CameraId", cameraId }
                }));
            }
            catch (Exception ex)
            {
                return OperatorExecutionOutput.Failure($"相机采集失败: {ex.Message}");
            }
        }

        // 如果是文件模式
        if (sourceType?.Equals("File", StringComparison.OrdinalIgnoreCase) == true)
        {
            return OperatorExecutionOutput.Failure("未指定文件路径");
        }

        return OperatorExecutionOutput.Failure("未提供图像数据或有效的采集设置");
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        return ValidationResult.Valid();
    }

    private static string? TryGetStringInput(Dictionary<string, object>? inputs, string key)
    {
        if (inputs != null && inputs.TryGetValue(key, out var value))
        {
            return value?.ToString();
        }

        return null;
    }
}
