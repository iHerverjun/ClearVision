namespace Acme.Product.Infrastructure.Calibration;

/// <summary>
/// Runtime helper for applying CalibrationBundleV2 Transform2D in a consistent way.
/// </summary>
public sealed class CalibrationPlanarTransformRuntime
{
    private const double Epsilon = 1e-12;

    private CalibrationPlanarTransformRuntime(
        TransformModelV2 model,
        double[][] forwardMatrix3x3,
        double[][] inverseMatrix3x3)
    {
        Model = model;
        ForwardMatrix3x3 = forwardMatrix3x3;
        InverseMatrix3x3 = inverseMatrix3x3;
    }

    public TransformModelV2 Model { get; }

    public double[][] ForwardMatrix3x3 { get; }

    public double[][] InverseMatrix3x3 { get; }

    public static bool TryCreate(
        CalibrationBundleV2 bundle,
        TransformModelV2[] allowedModels,
        out CalibrationPlanarTransformRuntime runtime,
        out string error)
    {
        runtime = new CalibrationPlanarTransformRuntime(
            TransformModelV2.None,
            CreateIdentity3x3(),
            CreateIdentity3x3());
        error = string.Empty;

        if (!CalibrationBundleV2Json.TryRequireTransform2D(bundle, allowedModels, out var transform, out error))
        {
            return false;
        }

        if (transform.Model != TransformModelV2.ScaleOffset &&
            transform.Model != TransformModelV2.Similarity &&
            transform.Model != TransformModelV2.Affine &&
            transform.Model != TransformModelV2.Homography)
        {
            error = $"Transform2D model {transform.Model} is not supported by planar runtime.";
            return false;
        }

        if (!TryBuildForward3x3(transform, out var forward, out error))
        {
            return false;
        }

        if (!TryInvert3x3(forward, out var inverse, out error))
        {
            return false;
        }

        runtime = new CalibrationPlanarTransformRuntime(transform.Model, forward, inverse);
        return true;
    }

    public bool TryApplyForward(double x, double y, out double tx, out double ty, out string error)
    {
        return TryApply(ForwardMatrix3x3, x, y, out tx, out ty, out error);
    }

    public bool TryApplyInverse(double x, double y, out double tx, out double ty, out string error)
    {
        return TryApply(InverseMatrix3x3, x, y, out tx, out ty, out error);
    }

    private static bool TryBuildForward3x3(
        CalibrationTransform2DV2 transform,
        out double[][] matrix,
        out string error)
    {
        matrix = CreateIdentity3x3();
        error = string.Empty;

        if (transform.Model == TransformModelV2.Homography)
        {
            if (!CalibrationBundleV2Json.HasMatrix(transform.Matrix, 3, 3))
            {
                error = "Homography requires a 3x3 matrix.";
                return false;
            }

            matrix = CloneMatrix(transform.Matrix);
        }
        else
        {
            if (!CalibrationBundleV2Json.HasMatrix(transform.Matrix, 2, 3))
            {
                error = $"{transform.Model} requires a 2x3 matrix.";
                return false;
            }

            matrix[0][0] = transform.Matrix[0][0];
            matrix[0][1] = transform.Matrix[0][1];
            matrix[0][2] = transform.Matrix[0][2];
            matrix[1][0] = transform.Matrix[1][0];
            matrix[1][1] = transform.Matrix[1][1];
            matrix[1][2] = transform.Matrix[1][2];
            matrix[2][0] = 0;
            matrix[2][1] = 0;
            matrix[2][2] = 1;
        }

        if (!CalibrationBundleV2Helpers.IsFiniteMatrix(matrix))
        {
            error = "Transform2D matrix contains NaN or Infinity.";
            return false;
        }

        return true;
    }

    private static bool TryInvert3x3(double[][] matrix, out double[][] inverse, out string error)
    {
        inverse = CreateIdentity3x3();
        error = string.Empty;

        var a = matrix[0][0];
        var b = matrix[0][1];
        var c = matrix[0][2];
        var d = matrix[1][0];
        var e = matrix[1][1];
        var f = matrix[1][2];
        var g = matrix[2][0];
        var h = matrix[2][1];
        var i = matrix[2][2];

        var det = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
        if (Math.Abs(det) <= Epsilon)
        {
            error = "Transform2D matrix is singular and cannot be inverted.";
            return false;
        }

        var invDet = 1.0 / det;
        inverse = new[]
        {
            new[]
            {
                (e * i - f * h) * invDet,
                (c * h - b * i) * invDet,
                (b * f - c * e) * invDet
            },
            new[]
            {
                (f * g - d * i) * invDet,
                (a * i - c * g) * invDet,
                (c * d - a * f) * invDet
            },
            new[]
            {
                (d * h - e * g) * invDet,
                (b * g - a * h) * invDet,
                (a * e - b * d) * invDet
            }
        };

        if (!CalibrationBundleV2Helpers.IsFiniteMatrix(inverse))
        {
            error = "Inverse matrix contains NaN or Infinity.";
            return false;
        }

        return true;
    }

    private static bool TryApply(
        double[][] matrix,
        double x,
        double y,
        out double tx,
        out double ty,
        out string error)
    {
        tx = 0;
        ty = 0;
        error = string.Empty;

        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            error = "Input coordinate is not finite.";
            return false;
        }

        var w = matrix[2][0] * x + matrix[2][1] * y + matrix[2][2];
        if (Math.Abs(w) <= Epsilon)
        {
            error = "Homogeneous denominator is too close to zero.";
            return false;
        }

        tx = (matrix[0][0] * x + matrix[0][1] * y + matrix[0][2]) / w;
        ty = (matrix[1][0] * x + matrix[1][1] * y + matrix[1][2]) / w;

        if (!double.IsFinite(tx) || !double.IsFinite(ty))
        {
            error = "Transformed coordinate is not finite.";
            return false;
        }

        return true;
    }

    private static double[][] CloneMatrix(double[][] source)
    {
        var clone = new double[source.Length][];
        for (var i = 0; i < source.Length; i++)
        {
            clone[i] = source[i].ToArray();
        }

        return clone;
    }

    private static double[][] CreateIdentity3x3()
    {
        return
        [
            [1, 0, 0],
            [0, 1, 0],
            [0, 0, 1]
        ];
    }
}
