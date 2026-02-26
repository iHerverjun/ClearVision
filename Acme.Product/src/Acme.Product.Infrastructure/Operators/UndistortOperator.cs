// UndistortOperator.cs
// 畸变校正算子 - 基于标定数据校正图像畸变
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System.Text.Json;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 畸变校正算子 - 基于标定数据校正图像畸变
/// </summary>
[OperatorMeta(
    DisplayName = "畸变校正",
    Description = "基于标定数据校正图像畸变",
    Category = "标定",
    IconName = "undistort",
    Keywords = new[] { "畸变", "校正", "矫正", "去畸变", "Undistort", "Distortion", "Correct" }
)]
[InputPort("Image", "输入图像", PortDataType.Image, IsRequired = true)]
[InputPort("CalibrationData", "标定数据", PortDataType.String, IsRequired = false)]
[OutputPort("Image", "校正图像", PortDataType.Image)]
[OperatorParam("CalibrationFile", "标定文件路径", "file", DefaultValue = "")]
public class UndistortOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.Undistort;

    public UndistortOperator(ILogger<UndistortOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供输入图像"));
        }

        // 获取标定数据
        string calibrationData = "";
        if (inputs != null && inputs.TryGetValue("CalibrationData", out var calObj) && calObj is string calStr)
        {
            calibrationData = calStr;
        }

        // 如果没有从输入端口获取，尝试从参数获取文件路径
        var calibrationFile = GetStringParam(@operator, "CalibrationFile", "");
        if (string.IsNullOrEmpty(calibrationData) && !string.IsNullOrEmpty(calibrationFile) && File.Exists(calibrationFile))
        {
            calibrationData = File.ReadAllText(calibrationFile);
        }

        if (string.IsNullOrEmpty(calibrationData))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供标定数据"));
        }

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        // 解析标定数据
        CalibrationInfo? calInfo = null;
        try
        {
            calInfo = JsonSerializer.Deserialize<CalibrationInfo>(calibrationData);
        }
        catch { }

        if (calInfo == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("标定数据格式无效"));
        }

        // 创建相机矩阵和畸变系数（简化处理，实际应从完整标定数据获取）
        var cameraMatrix = new double[,]
        {
            { src.Width * 0.8, 0, src.Width / 2.0 },
            { 0, src.Width * 0.8, src.Height / 2.0 },
            { 0, 0, 1 }
        };

        var distCoeffs = new double[] { 0, 0, 0, 0, 0 }; // 无畸变系数时保持不变

        // 如果标定数据包含相机矩阵和畸变系数，使用它们
        if (calInfo.CameraMatrix != null && calInfo.CameraMatrix.Length == 9)
        {
            cameraMatrix = new double[,]
            {
                { calInfo.CameraMatrix[0], calInfo.CameraMatrix[1], calInfo.CameraMatrix[2] },
                { calInfo.CameraMatrix[3], calInfo.CameraMatrix[4], calInfo.CameraMatrix[5] },
                { calInfo.CameraMatrix[6], calInfo.CameraMatrix[7], calInfo.CameraMatrix[8] }
            };
        }

        if (calInfo.DistCoeffs != null)
        {
            distCoeffs = calInfo.DistCoeffs;
        }

        // 执行畸变校正
        var dst = new Mat();
        using var cameraMat = new Mat(3, 3, MatType.CV_64FC1, cameraMatrix);
        using var distMat = new Mat(distCoeffs.Length, 1, MatType.CV_64FC1, distCoeffs);

        Cv2.Undistort(src, dst, cameraMat, distMat);

        // P0: 使用ImageWrapper实现零拷贝输出
        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst, new Dictionary<string, object>
        {
            { "Applied", true },
            { "Message", "畸变校正已应用" }
        })));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        return ValidationResult.Valid();
    }

    private class CalibrationInfo
    {
        public double[]? CameraMatrix { get; set; }
        public double[]? DistCoeffs { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
    }
}
