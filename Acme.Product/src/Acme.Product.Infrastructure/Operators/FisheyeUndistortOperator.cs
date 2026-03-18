// FisheyeUndistortOperator.cs
// 鱼眼去畸变算子 - 支持LUT加速
// 对标 Halcon: change_radial_distortion_cam_par
// 作者：AI Assistant

using System.Text.Json;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 鱼眼去畸变算子 - 支持LUT加速
/// 对标 Halcon change_radial_distortion_cam_par
/// </summary>
[OperatorMeta(
    DisplayName = "Fisheye Undistort",
    Description = "Correct fisheye lens distortion using calibration data with LUT acceleration.",
    Category = "Calibration",
    IconName = "fisheye-undistort",
    Keywords = new[] { "Fisheye", "Undistort", "Distortion", "LUT", "Kannala-Brandt" }
)]
[InputPort("Image", "Input Image", PortDataType.Image, IsRequired = true)]
[InputPort("CalibrationData", "Calibration Data", PortDataType.String, IsRequired = false)]
[OutputPort("Image", "Undistorted Image", PortDataType.Image)]
[OutputPort("UndistortedImage", "Undistorted Image", PortDataType.Image)]
[OperatorParam("CalibrationFile", "Calibration File", "file", DefaultValue = "")]
[OperatorParam("UseLut", "Use LUT Acceleration", "bool", DefaultValue = true)]
[OperatorParam("LutCacheSize", "LUT Cache Size", "int", DefaultValue = 10, Min = 1, Max = 100)]
[OperatorParam("Balance", "Balance", "double", DefaultValue = 0.0, Min = 0.0, Max = 1.0)]
[OperatorParam("NewImageSizeFactor", "New Image Size Factor", "double", DefaultValue = 1.0, Min = 0.1, Max = 2.0)]
public class FisheyeUndistortOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.FisheyeUndistort;

    // LUT缓存 - 线程安全考虑：每个算子实例有自己的缓存
    private readonly Dictionary<string, (Mat map1, Mat map2, Size imageSize)> _lutCache = new();
    private readonly object _cacheLock = new();

    public FisheyeUndistortOperator(ILogger<FisheyeUndistortOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, "Image", out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is required."));
        }

        if (!TryResolveCalibrationData(@operator, inputs, out var calibrationData))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Calibration data is required."));
        }

        if (!TryParseCalibrationData(calibrationData!, out var cameraMatrix, out var distCoeffs, out var isFisheye, out var parseError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"Invalid calibration data: {parseError}"));
        }

        var useLut = GetBoolParam(@operator, "UseLut", true);
        var balance = GetDoubleParam(@operator, "Balance", 0.0, 0.0, 1.0);
        var newImageSizeFactor = GetDoubleParam(@operator, "NewImageSizeFactor", 1.0, 0.1, 2.0);

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid."));
        }

        var dst = new Mat();
        var imageSize = src.Size();
        var newImageSize = new Size(
            (int)(imageSize.Width * newImageSizeFactor),
            (int)(imageSize.Height * newImageSizeFactor));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        if (isFisheye)
        {
            // 使用鱼眼去畸变模型 (Kannala-Brandt)
            ExecuteFisheyeUndistort(src, dst, cameraMatrix, distCoeffs, newImageSize, balance, useLut, calibrationData!);
        }
        else
        {
            // 使用普通去畸变模型 (Brown-Conrady)
            ExecuteStandardUndistort(src, dst, cameraMatrix, distCoeffs, newImageSize, useLut, calibrationData!);
        }

        stopwatch.Stop();
        var processingTime = stopwatch.ElapsedMilliseconds;

        // 生成校正报告信息
        var correctionReport = new Dictionary<string, object>
        {
            { "Applied", true },
            { "Model", isFisheye ? "Kannala-Brandt (Fisheye)" : "Brown-Conrady (Standard)" },
            { "ProcessingTimeMs", processingTime },
            { "UseLutAcceleration", useLut },
            { "OriginalSize", new { Width = imageSize.Width, Height = imageSize.Height } },
            { "OutputSize", new { Width = newImageSize.Width, Height = newImageSize.Height } },
            { "Balance", balance },
            { "Message", $"Undistortion applied using {(isFisheye ? "fisheye" : "standard")} model. LUT: {useLut}" }
        };

        // 清理矩阵资源
        cameraMatrix.Dispose();
        distCoeffs.Dispose();

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst, correctionReport)));
    }

    private void ExecuteFisheyeUndistort(
        Mat src, Mat dst, Mat cameraMatrix, Mat distCoeffs, 
        Size newImageSize, double balance, bool useLut, string calibrationDataKey)
    {
        using var adjustedCameraMatrix = BuildAdjustedCameraMatrix(cameraMatrix, src.Size(), newImageSize, balance);
        if (useLut)
        {
            var cacheKey = $"{calibrationDataKey.GetHashCode()}_{newImageSize.Width}_{newImageSize.Height}_{balance:F2}";
            
            if (!TryGetLutFromCache(cacheKey, newImageSize, out var map1, out var map2))
            {
                // 生成LUT
                map1 = new Mat();
                map2 = new Mat();
                
                Cv2.InitUndistortRectifyMap(
                    cameraMatrix, distCoeffs, new Mat(), adjustedCameraMatrix,
                    newImageSize, MatType.CV_32FC1, map1, map2);
                
                AddLutToCache(cacheKey, map1, map2, newImageSize);
            }

            // 使用LUT进行重映射
            Cv2.Remap(src, dst, map1, map2, InterpolationFlags.Linear, BorderTypes.Constant, Scalar.Black);
        }
        else
        {
            Cv2.Undistort(src, dst, cameraMatrix, distCoeffs, adjustedCameraMatrix);
        }
    }

    private void ExecuteStandardUndistort(
        Mat src, Mat dst, Mat cameraMatrix, Mat distCoeffs,
        Size newImageSize, bool useLut, string calibrationDataKey)
    {
        using var adjustedCameraMatrix = BuildAdjustedCameraMatrix(cameraMatrix, src.Size(), newImageSize, 0);
        if (useLut)
        {
            var cacheKey = $"std_{calibrationDataKey.GetHashCode()}_{newImageSize.Width}_{newImageSize.Height}";
            
            if (!TryGetLutFromCache(cacheKey, newImageSize, out var map1, out var map2))
            {
                // 生成LUT
                map1 = new Mat();
                map2 = new Mat();
                
                Cv2.InitUndistortRectifyMap(
                    cameraMatrix, distCoeffs, new Mat(), adjustedCameraMatrix,
                    newImageSize, MatType.CV_32FC1, map1, map2);
                
                AddLutToCache(cacheKey, map1, map2, newImageSize);
            }

            // 使用LUT进行重映射
            Cv2.Remap(src, dst, map1, map2, InterpolationFlags.Linear, BorderTypes.Constant, Scalar.Black);
        }
        else
        {
            // 直接计算
            Cv2.Undistort(src, dst, cameraMatrix, distCoeffs, adjustedCameraMatrix);
        }
    }

    private static Mat BuildAdjustedCameraMatrix(Mat cameraMatrix, Size originalSize, Size newImageSize, double balance)
    {
        var adjusted = cameraMatrix.Clone();
        if (adjusted.Rows < 3 || adjusted.Cols < 3)
        {
            return adjusted;
        }

        var scaleX = originalSize.Width > 0 ? (double)newImageSize.Width / originalSize.Width : 1.0;
        var scaleY = originalSize.Height > 0 ? (double)newImageSize.Height / originalSize.Height : 1.0;
        var balanceScale = 1.0 + balance * 0.15;

        adjusted.Set(0, 0, cameraMatrix.At<double>(0, 0) * scaleX / balanceScale);
        adjusted.Set(1, 1, cameraMatrix.At<double>(1, 1) * scaleY / balanceScale);
        adjusted.Set(0, 2, newImageSize.Width / 2.0);
        adjusted.Set(1, 2, newImageSize.Height / 2.0);
        adjusted.Set(2, 2, 1.0);

        return adjusted;
    }

    private bool TryGetLutFromCache(string cacheKey, Size expectedSize, out Mat map1, out Mat map2)
    {
        map1 = new Mat();
        map2 = new Mat();

        lock (_cacheLock)
        {
            if (_lutCache.TryGetValue(cacheKey, out var cached))
            {
                if (cached.imageSize == expectedSize)
                {
                    cached.map1.CopyTo(map1);
                    cached.map2.CopyTo(map2);
                    Logger.LogDebug("LUT cache hit for key: {CacheKey}", cacheKey);
                    return true;
                }
            }
        }

        return false;
    }

    private void AddLutToCache(string cacheKey, Mat map1, Mat map2, Size imageSize)
    {
        lock (_cacheLock)
        {
            // 限制缓存大小
            var maxCacheSize = 10;
            while (_lutCache.Count >= maxCacheSize)
            {
                var oldestKey = _lutCache.Keys.First();
                if (_lutCache.TryGetValue(oldestKey, out var oldest))
                {
                    oldest.map1.Dispose();
                    oldest.map2.Dispose();
                }
                _lutCache.Remove(oldestKey);
            }

            var map1Copy = new Mat();
            var map2Copy = new Mat();
            map1.CopyTo(map1Copy);
            map2.CopyTo(map2Copy);

            _lutCache[cacheKey] = (map1Copy, map2Copy, imageSize);
            Logger.LogDebug("LUT cached for key: {CacheKey}", cacheKey);
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var balance = GetDoubleParam(@operator, "Balance", 0.0);
        if (balance < 0.0 || balance > 1.0)
        {
            return ValidationResult.Invalid("Balance must be between 0.0 and 1.0.");
        }

        var newImageSizeFactor = GetDoubleParam(@operator, "NewImageSizeFactor", 1.0);
        if (newImageSizeFactor < 0.1 || newImageSizeFactor > 2.0)
        {
            return ValidationResult.Invalid("NewImageSizeFactor must be between 0.1 and 2.0.");
        }

        return ValidationResult.Valid();
    }

    private bool TryResolveCalibrationData(
        Operator @operator,
        Dictionary<string, object>? inputs,
        out string? calibrationData)
    {
        calibrationData = null;
        if (inputs != null &&
            inputs.TryGetValue("CalibrationData", out var calibrationObj) &&
            calibrationObj is string calibrationText &&
            !string.IsNullOrWhiteSpace(calibrationText))
        {
            calibrationData = calibrationText;
            return true;
        }

        var calibrationFile = GetStringParam(@operator, "CalibrationFile", "");
        if (!string.IsNullOrWhiteSpace(calibrationFile) && File.Exists(calibrationFile))
        {
            calibrationData = File.ReadAllText(calibrationFile);
            return true;
        }

        return false;
    }

    private static bool TryParseCalibrationData(
        string calibrationData,
        out Mat cameraMatrix,
        out Mat distCoeffs,
        out bool isFisheye,
        out string? error)
    {
        cameraMatrix = new Mat(3, 3, MatType.CV_64FC1);
        distCoeffs = new Mat();
        isFisheye = false;
        error = null;

        try
        {
            using var doc = JsonDocument.Parse(calibrationData);
            var root = doc.RootElement;

            // 检查是否是鱼眼标定数据
            if (root.TryGetProperty("IsFisheye", out var isFisheyeElement))
            {
                isFisheye = isFisheyeElement.GetBoolean();
            }
            else if (root.TryGetProperty("Model", out var modelElement))
            {
                isFisheye = modelElement.GetString()?.Contains("Fisheye", StringComparison.OrdinalIgnoreCase) ?? false;
            }

            if (!TryParseCameraMatrix(root, out var cameraMatrixData))
            {
                error = "CameraMatrix is missing or has unsupported format.";
                return false;
            }

            // 填充相机矩阵
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    cameraMatrix.Set(i, j, cameraMatrixData[i][j]);
                }
            }

            // 解析畸变系数
            var distCoeffsData = TryParseDistCoeffs(root);
            if (isFisheye)
            {
                // 鱼眼模型使用4个系数 (k1, k2, k3, k4)
                distCoeffs = new Mat(1, Math.Max(4, distCoeffsData.Length), MatType.CV_64FC1);
                for (int i = 0; i < distCoeffsData.Length && i < 4; i++)
                {
                    distCoeffs.Set(0, i, distCoeffsData[i]);
                }
            }
            else
            {
                // 标准模型使用最多8个系数 (k1, k2, p1, p2, k3, k4, k5, k6)
                distCoeffs = new Mat(1, distCoeffsData.Length, MatType.CV_64FC1);
                for (int i = 0; i < distCoeffsData.Length; i++)
                {
                    distCoeffs.Set(0, i, distCoeffsData[i]);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryParseCameraMatrix(JsonElement root, out double[][] cameraMatrix)
    {
        cameraMatrix = new double[3][];
        for (int i = 0; i < 3; i++)
        {
            cameraMatrix[i] = new double[3];
        }

        if (!root.TryGetProperty("CameraMatrix", out var matrixElement))
        {
            return false;
        }

        if (matrixElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var length = matrixElement.GetArrayLength();
        if (length == 9)
        {
            var index = 0;
            foreach (var value in matrixElement.EnumerateArray())
            {
                if (!TryReadNumber(value, out var number))
                {
                    return false;
                }

                cameraMatrix[index / 3][index % 3] = number;
                index++;
            }

            return true;
        }

        if (length == 3)
        {
            var rowIndex = 0;
            foreach (var row in matrixElement.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() != 3)
                {
                    return false;
                }

                var colIndex = 0;
                foreach (var value in row.EnumerateArray())
                {
                    if (!TryReadNumber(value, out var number))
                    {
                        return false;
                    }

                    cameraMatrix[rowIndex][colIndex++] = number;
                }

                rowIndex++;
            }

            return true;
        }

        return false;
    }

    private static double[] TryParseDistCoeffs(JsonElement root)
    {
        if (!root.TryGetProperty("DistCoeffs", out var distElement) || distElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<double>();
        }

        var values = new List<double>();
        foreach (var item in distElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Array)
            {
                foreach (var nested in item.EnumerateArray())
                {
                    if (TryReadNumber(nested, out var nestedValue))
                    {
                        values.Add(nestedValue);
                    }
                }
            }
            else if (TryReadNumber(item, out var value))
            {
                values.Add(value);
            }
        }

        return values.ToArray();
    }

    private static bool TryReadNumber(JsonElement element, out double value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                value = element.GetDouble();
                return true;
            case JsonValueKind.String:
                return double.TryParse(element.GetString(), out value);
            default:
                value = 0;
                return false;
        }
    }
}
