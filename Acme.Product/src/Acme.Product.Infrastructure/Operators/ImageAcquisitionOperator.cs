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

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 图像采集算子 - 支持相机和文件采集
/// </summary>
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
        string? sourceType = null;
        string? filePath = null;

        if (inputs != null && inputs.TryGetValue("sourceType", out var stObj))
            sourceType = stObj?.ToString();
        if (sourceType == null)
        {
            sourceType = GetStringParam(@operator, "sourceType", "");
        }

        if (inputs != null && inputs.TryGetValue("filePath", out var fpObj))
            filePath = fpObj?.ToString();
        // 注意：旧代码使用的是 ImagePath，这里统一为 filePath 以对齐元数据和前端
        if (string.IsNullOrEmpty(filePath))
        {
            filePath = GetStringParam(@operator, "filePath", "");
        }

        // 如果连线输入中有名为 Image 的字节数组，则直接使用（透传模式）
        if (inputs != null && inputs.TryGetValue("Image", out var imgObj) && imgObj is byte[] rawData)
        {
            // 【优化】直接从PNG头部解析尺寸，避免完整解码
            var dimensions = ImageWrapper.TryParsePngDimensions(rawData);
            var width = dimensions?.width ?? 0;
            var height = dimensions?.height ?? 0;
            var channels = dimensions?.channels ?? 3;

            // 如果PNG解析失败（非PNG格式），回退到ImageWrapper的延迟属性
            if (width == 0 || height == 0)
            {
                var wrapper = ImageWrapper.FromBytes(rawData);
                width = wrapper.Width;
                height = wrapper.Height;
                channels = wrapper.Channels;
            }

            return OperatorExecutionOutput.Success(new Dictionary<string, object>
            {
                { "Image", rawData },
                { "Width", width },
                { "Height", height },
                { "Channels", channels }
            });
        }

        // 如果是相机模式
        if (sourceType?.ToLower() == "camera")
        {
            var cameraId = GetStringParam(@operator, "cameraId", "");
            if (string.IsNullOrEmpty(cameraId))
            {
                throw new InvalidOperationException("未选择相机");
            }

            try
            {
                // 获取并配置相机
                var camera = await _cameraManager.GetOrCreateByBindingAsync(cameraId);

                // 应用运行时参数
                var exposureTime = GetDoubleParam(@operator, "exposureTime", 5000);
                var gain = GetDoubleParam(@operator, "gain", 1.0);
                await camera.SetExposureTimeAsync(exposureTime);
                await camera.SetGainAsync(gain);

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
        if (sourceType?.ToLower() == "file" || !string.IsNullOrEmpty(filePath))
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return OperatorExecutionOutput.Failure("未指定文件路径");
            }

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

        return OperatorExecutionOutput.Failure("未提供图像数据或有效的采集设置");
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        return ValidationResult.Valid();
    }
}
