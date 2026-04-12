using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.Calibration;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Undistort",
    Description = "Correct lens distortion using calibration data.",
    Category = "Calibration",
    IconName = "undistort",
    Keywords = new[] { "Undistort", "Distortion", "Calibration" }
)]
[InputPort("Image", "Input Image", PortDataType.Image, IsRequired = true)]
[InputPort("CalibrationData", "Calibration Data", PortDataType.String, IsRequired = false)]
[OutputPort("Image", "Undistorted Image", PortDataType.Image)]
public class UndistortOperator : OperatorBase
{
    private const int MaxCacheEntries = 16;
    private readonly Dictionary<string, (Mat map1, Mat map2)> _mapCache = new(StringComparer.Ordinal);
    private readonly Queue<string> _cacheOrder = new();
    private readonly object _cacheLock = new();

    public override OperatorType OperatorType => OperatorType.Undistort;

    public UndistortOperator(ILogger<UndistortOperator> logger) : base(logger)
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
                CalibrationKindV2.CameraIntrinsics,
                new[] { DistortionModelV2.BrownConrady, DistortionModelV2.None },
                out var runtime,
                out var parseError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"Invalid CalibrationBundleV2 for Undistort: {parseError}"));
        }

        using (runtime)
        {
            if (!IntrinsicsCalibrationRuntimeFactory.TryRequireExactImageSize(runtime, src.Size(), out var sizeError))
            {
                return Task.FromResult(OperatorExecutionOutput.Failure(sizeError));
            }

            var cacheKey = IntrinsicsCalibrationRuntimeFactory.BuildCacheKey(
                runtime,
                profile: "undistort-brown",
                outputSize: src.Size());

            var (map1, map2) = GetOrCreateRemap(cacheKey, runtime.CameraMatrix, runtime.DistCoeffs, src.Size());
            var dst = new Mat();
            Cv2.Remap(src, dst, map1, map2, InterpolationFlags.Linear, BorderTypes.Constant);

            var diagnostics = runtime.Bundle.Quality.Diagnostics?.Count > 0
                ? string.Join("; ", runtime.Bundle.Quality.Diagnostics)
                : "No diagnostics";

            var output = new Dictionary<string, object>
            {
                ["Applied"] = true,
                ["Accepted"] = runtime.Bundle.Quality.Accepted,
                ["CalibrationKind"] = runtime.Bundle.CalibrationKind.ToString(),
                ["DistortionModel"] = runtime.Bundle.Distortion?.Model.ToString() ?? DistortionModelV2.None.ToString(),
                ["Message"] = "Undistortion applied using CalibrationBundleV2.",
                ["Diagnostics"] = diagnostics
            };

            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst, output)));
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
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

    private (Mat map1, Mat map2) GetOrCreateRemap(string key, Mat cameraMatrix, Mat distCoeffs, Size imageSize)
    {
        lock (_cacheLock)
        {
            if (_mapCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var map1 = new Mat();
            var map2 = new Mat();
            Cv2.InitUndistortRectifyMap(
                cameraMatrix,
                distCoeffs,
                new Mat(),
                cameraMatrix,
                imageSize,
                MatType.CV_32FC1,
                map1,
                map2);

            _mapCache[key] = (map1, map2);
            _cacheOrder.Enqueue(key);
            TrimCacheIfNeeded();
            return (map1, map2);
        }
    }

    private void TrimCacheIfNeeded()
    {
        while (_cacheOrder.Count > MaxCacheEntries)
        {
            var oldestKey = _cacheOrder.Dequeue();
            if (!_mapCache.Remove(oldestKey, out var maps))
            {
                continue;
            }

            maps.map1.Dispose();
            maps.map2.Dispose();
        }
    }
}
