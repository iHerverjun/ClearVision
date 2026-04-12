using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Calibration;

public sealed class IntrinsicsCalibrationRuntime : IDisposable
{
    public IntrinsicsCalibrationRuntime(
        CalibrationBundleV2 bundle,
        Mat cameraMatrix,
        Mat distCoeffs,
        Size calibrationImageSize,
        string fingerprint)
    {
        Bundle = bundle;
        CameraMatrix = cameraMatrix;
        DistCoeffs = distCoeffs;
        CalibrationImageSize = calibrationImageSize;
        Fingerprint = fingerprint;
    }

    public CalibrationBundleV2 Bundle { get; }

    public Mat CameraMatrix { get; }

    public Mat DistCoeffs { get; }

    public Size CalibrationImageSize { get; }

    public string Fingerprint { get; }

    public void Dispose()
    {
        CameraMatrix.Dispose();
        DistCoeffs.Dispose();
    }
}

public static class IntrinsicsCalibrationRuntimeFactory
{
    private static readonly HashSet<int> BrownConradyLengths = new() { 0, 4, 5, 8, 12, 14 };

    public static bool TryCreate(
        string calibrationData,
        CalibrationKindV2 expectedKind,
        DistortionModelV2[] allowedDistortionModels,
        out IntrinsicsCalibrationRuntime runtime,
        out string error)
    {
        runtime = new IntrinsicsCalibrationRuntime(
            new CalibrationBundleV2(),
            new Mat(),
            new Mat(),
            default,
            string.Empty);
        error = string.Empty;

        if (!CalibrationBundleV2Json.TryDeserialize(calibrationData, out var bundle, out error))
        {
            return false;
        }

        if (bundle.CalibrationKind != expectedKind)
        {
            error = $"CalibrationKind mismatch. Expected {expectedKind}, actual {bundle.CalibrationKind}.";
            return false;
        }

        if (!CalibrationBundleV2Json.TryRequireAccepted(bundle, out error))
        {
            return false;
        }

        if (bundle.ImageSize == null || bundle.ImageSize.Width <= 0 || bundle.ImageSize.Height <= 0)
        {
            error = "ImageSize is required and must be positive.";
            return false;
        }

        if (!CalibrationBundleV2Json.TryRequireIntrinsics(bundle, allowedDistortionModels, out var intrinsics, out var distortion, out error))
        {
            return false;
        }

        if (!ValidateCameraMatrix(intrinsics.CameraMatrix, out error))
        {
            return false;
        }

        if (!ValidateDistortion(distortion, out error))
        {
            return false;
        }

        var cameraMatrix = CalibrationBundleV2Helpers.ToMat(intrinsics.CameraMatrix);
        var distCoeffs = CreateDistortionMat(distortion.Coefficients);
        var imageSize = new Size(bundle.ImageSize.Width, bundle.ImageSize.Height);
        var fingerprint = ComputeFingerprint(calibrationData);

        runtime = new IntrinsicsCalibrationRuntime(bundle, cameraMatrix, distCoeffs, imageSize, fingerprint);
        return true;
    }

    public static bool TryRequireExactImageSize(IntrinsicsCalibrationRuntime runtime, Size runtimeImageSize, out string error)
    {
        if (runtimeImageSize != runtime.CalibrationImageSize)
        {
            error = $"Runtime image size {runtimeImageSize.Width}x{runtimeImageSize.Height} does not match calibration image size {runtime.CalibrationImageSize.Width}x{runtime.CalibrationImageSize.Height}.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static string BuildCacheKey(
        IntrinsicsCalibrationRuntime runtime,
        string profile,
        Size outputSize,
        double balance = 0,
        double sizeFactor = 1.0)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{profile}|{runtime.Fingerprint}|{runtime.CalibrationImageSize.Width}x{runtime.CalibrationImageSize.Height}|{outputSize.Width}x{outputSize.Height}|b={balance:F4}|s={sizeFactor:F4}");
    }

    private static bool ValidateCameraMatrix(double[][] cameraMatrix, out string error)
    {
        if (!CalibrationBundleV2Json.HasMatrix(cameraMatrix, 3, 3))
        {
            error = "CameraMatrix must be 3x3.";
            return false;
        }

        if (!CalibrationBundleV2Helpers.IsFiniteMatrix(cameraMatrix))
        {
            error = "CameraMatrix contains non-finite values.";
            return false;
        }

        var fx = cameraMatrix[0][0];
        var fy = cameraMatrix[1][1];
        if (fx <= 0 || fy <= 0)
        {
            error = "CameraMatrix focal lengths fx/fy must be positive.";
            return false;
        }

        if (!NearZero(cameraMatrix[2][0]) || !NearZero(cameraMatrix[2][1]) || !NearOne(cameraMatrix[2][2]))
        {
            error = "CameraMatrix last row must be [0, 0, 1].";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateDistortion(CalibrationDistortionV2 distortion, out string error)
    {
        if (!CalibrationBundleV2Helpers.IsFiniteVector(distortion.Coefficients))
        {
            error = "Distortion coefficients contain non-finite values.";
            return false;
        }

        switch (distortion.Model)
        {
            case DistortionModelV2.None:
                if (distortion.Coefficients.Length != 0)
                {
                    error = "Distortion model None requires empty coefficients.";
                    return false;
                }

                break;
            case DistortionModelV2.BrownConrady:
                if (!BrownConradyLengths.Contains(distortion.Coefficients.Length))
                {
                    error = $"BrownConrady supports coefficient lengths: {string.Join(", ", BrownConradyLengths.OrderBy(v => v))}.";
                    return false;
                }

                break;
            case DistortionModelV2.KannalaBrandt:
                if (distortion.Coefficients.Length != 4)
                {
                    error = "KannalaBrandt requires exactly 4 distortion coefficients.";
                    return false;
                }

                break;
            default:
                error = $"Unsupported distortion model: {distortion.Model}.";
                return false;
        }

        error = string.Empty;
        return true;
    }

    private static Mat CreateDistortionMat(double[] coefficients)
    {
        if (coefficients.Length == 0)
        {
            return new Mat();
        }

        var mat = new Mat(coefficients.Length, 1, MatType.CV_64FC1);
        for (var i = 0; i < coefficients.Length; i++)
        {
            mat.Set(i, 0, coefficients[i]);
        }

        return mat;
    }

    private static string ComputeFingerprint(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }

    private static bool NearZero(double value)
    {
        return Math.Abs(value) < 1e-9;
    }

    private static bool NearOne(double value)
    {
        return Math.Abs(value - 1.0) < 1e-9;
    }
}
