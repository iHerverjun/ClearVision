using System.Numerics;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Calibration;

public enum RobotHandEyeCalibrationType
{
    EyeInHand = 0,
    EyeToHand = 1
}

public sealed class RobotHandEyeCalibrationResult
{
    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }

    public RobotHandEyeCalibrationType CalibrationType { get; init; }

    public string Method { get; init; } = string.Empty;

    public Matrix4x4 HandEyeMatrix { get; init; } = Matrix4x4.Identity;

    public Matrix4x4 InverseHandEyeMatrix { get; init; } = Matrix4x4.Identity;

    public string MatrixConvention { get; init; } = string.Empty;

    public HandEyeValidationReport Validation { get; init; } = HandEyeValidationReport.Empty;
}

public static class HandEyeCalibrationSolver
{
    public static RobotHandEyeCalibrationResult Solve(
        IReadOnlyList<Matrix4x4> baseToToolPoses,
        IReadOnlyList<Matrix4x4> cameraToTargetPoses,
        RobotHandEyeCalibrationType calibrationType,
        HandEyeCalibrationMethod method)
    {
        if (baseToToolPoses == null || cameraToTargetPoses == null)
        {
            return new RobotHandEyeCalibrationResult
            {
                Success = false,
                ErrorMessage = "Pose inputs are required.",
                CalibrationType = calibrationType,
                Method = method.ToString()
            };
        }

        if (baseToToolPoses.Count != cameraToTargetPoses.Count)
        {
            return new RobotHandEyeCalibrationResult
            {
                Success = false,
                ErrorMessage = "Robot pose count must match calibration board pose count.",
                CalibrationType = calibrationType,
                Method = method.ToString()
            };
        }

        if (baseToToolPoses.Count < 3)
        {
            return new RobotHandEyeCalibrationResult
            {
                Success = false,
                ErrorMessage = "At least 3 pose pairs are required for hand-eye calibration.",
                CalibrationType = calibrationType,
                Method = method.ToString()
            };
        }

        try
        {
            return calibrationType switch
            {
                RobotHandEyeCalibrationType.EyeInHand => SolveEyeInHand(baseToToolPoses, cameraToTargetPoses, method),
                RobotHandEyeCalibrationType.EyeToHand => SolveEyeToHand(baseToToolPoses, cameraToTargetPoses, method),
                _ => new RobotHandEyeCalibrationResult
                {
                    Success = false,
                    ErrorMessage = $"Unsupported calibration type: {calibrationType}",
                    CalibrationType = calibrationType,
                    Method = method.ToString()
                }
            };
        }
        catch (Exception ex)
        {
            return new RobotHandEyeCalibrationResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                CalibrationType = calibrationType,
                Method = method.ToString()
            };
        }
    }

    private static RobotHandEyeCalibrationResult SolveEyeInHand(
        IReadOnlyList<Matrix4x4> baseToToolPoses,
        IReadOnlyList<Matrix4x4> cameraToTargetPoses,
        HandEyeCalibrationMethod method)
    {
        var rotationsGripperToBase = new List<Mat>(baseToToolPoses.Count);
        var translationsGripperToBase = new List<Mat>(baseToToolPoses.Count);
        var rotationsTargetToCamera = new List<Mat>(cameraToTargetPoses.Count);
        var translationsTargetToCamera = new List<Mat>(cameraToTargetPoses.Count);

        try
        {
            for (var i = 0; i < baseToToolPoses.Count; i++)
            {
                var gripperToBase = Invert(baseToToolPoses[i]);
                var targetToCamera = Invert(cameraToTargetPoses[i]);

                rotationsGripperToBase.Add(ToRotationMat(gripperToBase));
                translationsGripperToBase.Add(ToTranslationMat(gripperToBase));
                rotationsTargetToCamera.Add(ToRotationMat(targetToCamera));
                translationsTargetToCamera.Add(ToTranslationMat(targetToCamera));
            }

            using var rotationCameraToTool = new Mat();
            using var translationCameraToTool = new Mat();
            Cv2.CalibrateHandEye(
                rotationsGripperToBase,
                translationsGripperToBase,
                rotationsTargetToCamera,
                translationsTargetToCamera,
                rotationCameraToTool,
                translationCameraToTool,
                method);

            var handEyeMatrix = FromRotationTranslation(rotationCameraToTool, translationCameraToTool);
            var validation = HandEyeCalibrationValidator.Validate(
                baseToToolPoses,
                cameraToTargetPoses,
                handEyeMatrix,
                RobotHandEyeCalibrationType.EyeInHand);

            return new RobotHandEyeCalibrationResult
            {
                Success = true,
                CalibrationType = RobotHandEyeCalibrationType.EyeInHand,
                Method = method.ToString(),
                HandEyeMatrix = handEyeMatrix,
                InverseHandEyeMatrix = Invert(handEyeMatrix),
                MatrixConvention = "CameraToToolMatrix",
                Validation = validation
            };
        }
        finally
        {
            DisposeAll(rotationsGripperToBase);
            DisposeAll(translationsGripperToBase);
            DisposeAll(rotationsTargetToCamera);
            DisposeAll(translationsTargetToCamera);
        }
    }

    private static RobotHandEyeCalibrationResult SolveEyeToHand(
        IReadOnlyList<Matrix4x4> baseToToolPoses,
        IReadOnlyList<Matrix4x4> cameraToTargetPoses,
        HandEyeCalibrationMethod method)
    {
        var rotationsBaseToTool = new List<Mat>(baseToToolPoses.Count);
        var translationsBaseToTool = new List<Mat>(baseToToolPoses.Count);
        var rotationsTargetToCamera = new List<Mat>(cameraToTargetPoses.Count);
        var translationsTargetToCamera = new List<Mat>(cameraToTargetPoses.Count);

        try
        {
            for (var i = 0; i < baseToToolPoses.Count; i++)
            {
                var targetToCamera = Invert(cameraToTargetPoses[i]);

                rotationsBaseToTool.Add(ToRotationMat(baseToToolPoses[i]));
                translationsBaseToTool.Add(ToTranslationMat(baseToToolPoses[i]));
                rotationsTargetToCamera.Add(ToRotationMat(targetToCamera));
                translationsTargetToCamera.Add(ToTranslationMat(targetToCamera));
            }

            using var rotationCameraToBase = new Mat();
            using var translationCameraToBase = new Mat();
            Cv2.CalibrateHandEye(
                rotationsBaseToTool,
                translationsBaseToTool,
                rotationsTargetToCamera,
                translationsTargetToCamera,
                rotationCameraToBase,
                translationCameraToBase,
                method);

            var handEyeMatrix = FromRotationTranslation(rotationCameraToBase, translationCameraToBase);
            var validation = HandEyeCalibrationValidator.Validate(
                baseToToolPoses,
                cameraToTargetPoses,
                handEyeMatrix,
                RobotHandEyeCalibrationType.EyeToHand);

            return new RobotHandEyeCalibrationResult
            {
                Success = true,
                CalibrationType = RobotHandEyeCalibrationType.EyeToHand,
                Method = method.ToString(),
                HandEyeMatrix = handEyeMatrix,
                InverseHandEyeMatrix = Invert(handEyeMatrix),
                MatrixConvention = "CameraToBaseMatrix",
                Validation = validation
            };
        }
        finally
        {
            DisposeAll(rotationsBaseToTool);
            DisposeAll(translationsBaseToTool);
            DisposeAll(rotationsTargetToCamera);
            DisposeAll(translationsTargetToCamera);
        }
    }

    internal static Matrix4x4 Invert(Matrix4x4 matrix)
    {
        if (!Matrix4x4.Invert(matrix, out var inverted))
        {
            throw new InvalidOperationException("Pose matrix is not invertible.");
        }

        return inverted;
    }

    internal static Matrix4x4 AverageTransforms(IReadOnlyList<Matrix4x4> transforms)
    {
        if (transforms.Count == 0)
        {
            return Matrix4x4.Identity;
        }

        var translation = Vector3.Zero;
        foreach (var transform in transforms)
        {
            translation += new Vector3(transform.M41, transform.M42, transform.M43);
        }

        translation /= transforms.Count;
        var rotation = AverageQuaternion(transforms.Select(Quaternion.CreateFromRotationMatrix));

        var averaged = Matrix4x4.CreateFromQuaternion(rotation);
        averaged.M41 = translation.X;
        averaged.M42 = translation.Y;
        averaged.M43 = translation.Z;
        averaged.M44 = 1f;
        return averaged;
    }

    internal static double RotationErrorDegrees(Matrix4x4 left, Matrix4x4 right)
    {
        var q1 = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(left));
        var q2 = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(right));
        var dot = Math.Abs((double)Quaternion.Dot(q1, q2));
        dot = Math.Clamp(dot, 0d, 1d);
        return 2d * Math.Acos(dot) * (180d / Math.PI);
    }

    private static Quaternion AverageQuaternion(IEnumerable<Quaternion> quaternions)
    {
        var normalized = quaternions.Select(Quaternion.Normalize).ToList();
        if (normalized.Count == 0)
        {
            return Quaternion.Identity;
        }

        var reference = normalized[0];
        var sum = Vector4.Zero;
        foreach (var quaternion in normalized)
        {
            var q = Quaternion.Dot(reference, quaternion) < 0f
                ? new Quaternion(-quaternion.X, -quaternion.Y, -quaternion.Z, -quaternion.W)
                : quaternion;

            sum += new Vector4(q.X, q.Y, q.Z, q.W);
        }

        var averaged = new Quaternion(sum.X, sum.Y, sum.Z, sum.W);
        return Quaternion.Normalize(averaged);
    }

    private static Mat ToRotationMat(Matrix4x4 matrix)
    {
        var mat = new Mat(3, 3, MatType.CV_64FC1);
        mat.Set(0, 0, (double)matrix.M11);
        mat.Set(0, 1, (double)matrix.M21);
        mat.Set(0, 2, (double)matrix.M31);
        mat.Set(1, 0, (double)matrix.M12);
        mat.Set(1, 1, (double)matrix.M22);
        mat.Set(1, 2, (double)matrix.M32);
        mat.Set(2, 0, (double)matrix.M13);
        mat.Set(2, 1, (double)matrix.M23);
        mat.Set(2, 2, (double)matrix.M33);
        return mat;
    }

    private static Mat ToTranslationMat(Matrix4x4 matrix)
    {
        var mat = new Mat(3, 1, MatType.CV_64FC1);
        mat.Set(0, 0, (double)matrix.M41);
        mat.Set(1, 0, (double)matrix.M42);
        mat.Set(2, 0, (double)matrix.M43);
        return mat;
    }

    private static Matrix4x4 FromRotationTranslation(Mat rotation, Mat translation)
    {
        return new Matrix4x4(
            (float)rotation.At<double>(0, 0), (float)rotation.At<double>(1, 0), (float)rotation.At<double>(2, 0), 0f,
            (float)rotation.At<double>(0, 1), (float)rotation.At<double>(1, 1), (float)rotation.At<double>(2, 1), 0f,
            (float)rotation.At<double>(0, 2), (float)rotation.At<double>(1, 2), (float)rotation.At<double>(2, 2), 0f,
            (float)translation.At<double>(0, 0), (float)translation.At<double>(1, 0), (float)translation.At<double>(2, 0), 1f);
    }

    private static void DisposeAll(IEnumerable<Mat> mats)
    {
        foreach (var mat in mats)
        {
            mat.Dispose();
        }
    }
}
