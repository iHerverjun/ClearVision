using System.Numerics;

namespace Acme.Product.Infrastructure.Calibration;

public static class CalibrationBundleV2PoseHelpers
{
    public static double[][] ToJaggedMatrix4x4(Matrix4x4 matrix)
    {
        return new[]
        {
            new[] { (double)matrix.M11, matrix.M21, matrix.M31, matrix.M41 },
            new[] { (double)matrix.M12, matrix.M22, matrix.M32, matrix.M42 },
            new[] { (double)matrix.M13, matrix.M23, matrix.M33, matrix.M43 },
            new[] { (double)matrix.M14, matrix.M24, matrix.M34, matrix.M44 }
        };
    }

    public static bool TryToMatrix4x4(double[][] values, out Matrix4x4 matrix, out string error)
    {
        matrix = Matrix4x4.Identity;
        error = string.Empty;

        if (!CalibrationBundleV2Json.HasMatrix(values, 4, 4))
        {
            error = "Transform3D matrix must be 4x4.";
            return false;
        }

        if (!CalibrationBundleV2Helpers.IsFiniteMatrix(values))
        {
            error = "Transform3D matrix contains non-finite values.";
            return false;
        }

        matrix = new Matrix4x4(
            (float)values[0][0], (float)values[1][0], (float)values[2][0], (float)values[3][0],
            (float)values[0][1], (float)values[1][1], (float)values[2][1], (float)values[3][1],
            (float)values[0][2], (float)values[1][2], (float)values[2][2], (float)values[3][2],
            (float)values[0][3], (float)values[1][3], (float)values[2][3], (float)values[3][3]);
        return true;
    }
}
