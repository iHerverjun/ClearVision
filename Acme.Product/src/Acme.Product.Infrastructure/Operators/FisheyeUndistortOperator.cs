using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.Calibration;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

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
[OperatorParam("UseLut", "Use LUT Acceleration", "bool", DefaultValue = true)]
[OperatorParam("LutCacheSize", "LUT Cache Size", "int", DefaultValue = 10, Min = 1, Max = 100)]
[OperatorParam("Balance", "Balance", "double", DefaultValue = 0.0, Min = 0.0, Max = 1.0)]
[OperatorParam("NewImageSizeFactor", "New Image Size Factor", "double", DefaultValue = 1.0, Min = 0.1, Max = 2.0)]
public class FisheyeUndistortOperator : OperatorBase
{
    private readonly Dictionary<string, (Mat map1, Mat map2)> _mapCache = new(StringComparer.Ordinal);
    private readonly Queue<string> _cacheOrder = new();
    private readonly object _cacheLock = new();

    public override OperatorType OperatorType => OperatorType.FisheyeUndistort;

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

        if (!TryResolveCalibrationData(@operator, inputs, out var calibrationData, out var resolveError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(resolveError));
        }

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid."));
        }

        if (!IntrinsicsCalibrationRuntimeFactory.TryCreate(
                calibrationData!,
                CalibrationKindV2.FisheyeIntrinsics,
                new[] { DistortionModelV2.KannalaBrandt },
                out var runtime,
                out var parseError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"Invalid fisheye CalibrationBundleV2: {parseError}"));
        }

        using (runtime)
        {
            if (!IntrinsicsCalibrationRuntimeFactory.TryRequireExactImageSize(runtime, src.Size(), out var sizeError))
            {
                return Task.FromResult(OperatorExecutionOutput.Failure(sizeError));
            }

            var useLut = GetBoolParam(@operator, "UseLut", true);
            var lutCacheSize = GetIntParam(@operator, "LutCacheSize", 10, 1, 100);
            var balance = GetDoubleParam(@operator, "Balance", 0.0, 0.0, 1.0);
            var newImageSizeFactor = GetDoubleParam(@operator, "NewImageSizeFactor", 1.0, 0.1, 2.0);
            var outputSize = new Size(
                Math.Max(1, (int)Math.Round(src.Width * newImageSizeFactor)),
                Math.Max(1, (int)Math.Round(src.Height * newImageSizeFactor)));

            var cacheKey = IntrinsicsCalibrationRuntimeFactory.BuildCacheKey(
                runtime,
                profile: "undistort-fisheye",
                outputSize: outputSize,
                balance: balance,
                sizeFactor: newImageSizeFactor);

            Mat map1;
            Mat map2;
            if (useLut && TryGetCachedMap(cacheKey, out map1, out map2))
            {
                Logger.LogDebug("Fisheye undistort LUT cache hit for key: {CacheKey}", cacheKey);
            }
            else
            {
                (map1, map2) = CreateFisheyeMaps(runtime, outputSize, balance);
                if (useLut)
                {
                    CacheMaps(cacheKey, map1, map2, lutCacheSize);
                }
            }

            var dst = new Mat();
            Cv2.Remap(src, dst, map1, map2, InterpolationFlags.Linear, BorderTypes.Constant, Scalar.Black);

            if (!useLut)
            {
                map1.Dispose();
                map2.Dispose();
            }

            var output = new Dictionary<string, object>
            {
                ["Applied"] = true,
                ["Accepted"] = runtime.Bundle.Quality.Accepted,
                ["CalibrationKind"] = runtime.Bundle.CalibrationKind.ToString(),
                ["DistortionModel"] = runtime.Bundle.Distortion?.Model.ToString() ?? DistortionModelV2.None.ToString(),
                ["UseLutAcceleration"] = useLut,
                ["OriginalSize"] = new { Width = src.Width, Height = src.Height },
                ["OutputSize"] = new { Width = outputSize.Width, Height = outputSize.Height },
                ["Balance"] = balance,
                ["Message"] = "Fisheye undistortion applied using CalibrationBundleV2."
            };

            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst, output)));
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

        var lutCacheSize = GetIntParam(@operator, "LutCacheSize", 10);
        if (lutCacheSize < 1 || lutCacheSize > 100)
        {
            return ValidationResult.Invalid("LutCacheSize must be between 1 and 100.");
        }

        return ValidationResult.Valid();
    }

    private bool TryResolveCalibrationData(
        Operator @operator,
        Dictionary<string, object>? inputs,
        out string? calibrationData,
        out string error)
    {
        calibrationData = null;
        error = "Calibration data is required.";

        if (inputs != null &&
            inputs.TryGetValue("CalibrationData", out var calibrationObj) &&
            calibrationObj is string calibrationText &&
            !string.IsNullOrWhiteSpace(calibrationText))
        {
            calibrationData = calibrationText;
            error = string.Empty;
            return true;
        }

        var inlineParameterData = GetStringParam(@operator, "CalibrationData", "");
        if (string.IsNullOrWhiteSpace(inlineParameterData))
        {
            return false;
        }

        calibrationData = inlineParameterData;
        error = string.Empty;
        return true;
    }

    private static (Mat map1, Mat map2) CreateFisheyeMaps(
        IntrinsicsCalibrationRuntime runtime,
        Size outputSize,
        double balance)
    {
        using var rotation = Mat.Eye(3, 3, MatType.CV_64FC1);
        using var newCameraMatrix = new Mat();
        Cv2.FishEye.EstimateNewCameraMatrixForUndistortRectify(
            runtime.CameraMatrix,
            runtime.DistCoeffs,
            runtime.CalibrationImageSize,
            rotation,
            newCameraMatrix,
            balance,
            outputSize,
            1.0);

        var map1 = new Mat();
        var map2 = new Mat();
        Cv2.FishEye.InitUndistortRectifyMap(
            runtime.CameraMatrix,
            runtime.DistCoeffs,
            rotation,
            newCameraMatrix,
            outputSize,
            MatType.CV_32FC1,
            map1,
            map2);

        return (map1, map2);
    }

    private bool TryGetCachedMap(string cacheKey, out Mat map1, out Mat map2)
    {
        lock (_cacheLock)
        {
            if (_mapCache.TryGetValue(cacheKey, out var cached))
            {
                map1 = cached.map1;
                map2 = cached.map2;
                return true;
            }
        }

        map1 = new Mat();
        map2 = new Mat();
        return false;
    }

    private void CacheMaps(string cacheKey, Mat map1, Mat map2, int cacheCapacity)
    {
        lock (_cacheLock)
        {
            if (_mapCache.TryGetValue(cacheKey, out var existing))
            {
                existing.map1.Dispose();
                existing.map2.Dispose();
                _mapCache.Remove(cacheKey);
            }

            _mapCache[cacheKey] = (map1, map2);
            _cacheOrder.Enqueue(cacheKey);

            while (_cacheOrder.Count > cacheCapacity)
            {
                var oldestKey = _cacheOrder.Dequeue();
                if (!_mapCache.Remove(oldestKey, out var oldestMaps))
                {
                    continue;
                }

                oldestMaps.map1.Dispose();
                oldestMaps.map2.Dispose();
            }
        }
    }
}
