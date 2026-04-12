using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acme.Product.Infrastructure.Calibration;

public static class CalibrationBundleV2Json
{
    public static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    public static string Serialize(CalibrationBundleV2 bundle)
    {
        return JsonSerializer.Serialize(bundle, DefaultOptions);
    }

    public static bool TryDeserialize(string json, out CalibrationBundleV2 bundle, out string error)
    {
        bundle = new CalibrationBundleV2();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "CalibrationData is empty.";
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<CalibrationBundleV2>(json, DefaultOptions);
            if (parsed == null)
            {
                error = "CalibrationData could not be deserialized.";
                return false;
            }

            if (!TryValidateBase(parsed, out error))
            {
                return false;
            }

            bundle = parsed;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryValidateBase(CalibrationBundleV2 bundle, out string error)
    {
        if (bundle.SchemaVersion != 2)
        {
            error = $"Unsupported SchemaVersion: {bundle.SchemaVersion}.";
            return false;
        }

        if (bundle.CalibrationKind == CalibrationKindV2.Unknown)
        {
            error = "CalibrationKind is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(bundle.SourceFrame))
        {
            error = "SourceFrame is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(bundle.TargetFrame))
        {
            error = "TargetFrame is required.";
            return false;
        }

        if (bundle.Quality == null)
        {
            error = "Quality is required.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryRequireAccepted(CalibrationBundleV2 bundle, out string error)
    {
        if (!bundle.Quality.Accepted)
        {
            error = "Calibration bundle is diagnostic only and not accepted for production use.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryRequireIntrinsics(
        CalibrationBundleV2 bundle,
        DistortionModelV2[] allowedDistortionModels,
        out CalibrationIntrinsicsV2 intrinsics,
        out CalibrationDistortionV2 distortion,
        out string error)
    {
        intrinsics = new CalibrationIntrinsicsV2();
        distortion = new CalibrationDistortionV2();

        if (bundle.Intrinsics == null || !HasMatrix(bundle.Intrinsics.CameraMatrix, 3, 3))
        {
            error = "Intrinsics.CameraMatrix must be a 3x3 matrix.";
            return false;
        }

        if (bundle.Distortion == null)
        {
            error = "Distortion is required.";
            return false;
        }

        if (allowedDistortionModels.Length > 0 && !allowedDistortionModels.Contains(bundle.Distortion.Model))
        {
            error = $"Distortion model {bundle.Distortion.Model} is not supported by this operator.";
            return false;
        }

        intrinsics = bundle.Intrinsics;
        distortion = bundle.Distortion;
        error = string.Empty;
        return true;
    }

    public static bool TryRequireTransform2D(
        CalibrationBundleV2 bundle,
        TransformModelV2[] allowedModels,
        out CalibrationTransform2DV2 transform,
        out string error)
    {
        transform = new CalibrationTransform2DV2();

        if (bundle.Transform2D == null)
        {
            error = "Transform2D is required.";
            return false;
        }

        if (allowedModels.Length > 0 && !allowedModels.Contains(bundle.Transform2D.Model))
        {
            error = $"Transform2D model {bundle.Transform2D.Model} is not supported by this operator.";
            return false;
        }

        var rows = bundle.Transform2D.Model == TransformModelV2.Homography ? 3 : 2;
        var cols = 3;
        if (!HasMatrix(bundle.Transform2D.Matrix, rows, cols) &&
            !(bundle.Transform2D.Model == TransformModelV2.Homography && HasMatrix(bundle.Transform2D.Matrix, 3, 3)))
        {
            error = "Transform2D matrix shape does not match the declared model.";
            return false;
        }

        transform = bundle.Transform2D;
        error = string.Empty;
        return true;
    }

    public static bool TryRequireTransform3D(CalibrationBundleV2 bundle, out CalibrationTransform3DV2 transform, out string error)
    {
        transform = new CalibrationTransform3DV2();

        if (bundle.Transform3D == null || !HasMatrix(bundle.Transform3D.Matrix, 4, 4))
        {
            error = "Transform3D.Matrix must be a 4x4 matrix.";
            return false;
        }

        transform = bundle.Transform3D;
        error = string.Empty;
        return true;
    }

    public static bool HasMatrix(double[][] matrix, int rows, int cols)
    {
        if (matrix.Length != rows)
        {
            return false;
        }

        for (var r = 0; r < rows; r++)
        {
            if (matrix[r] == null || matrix[r].Length != cols)
            {
                return false;
            }
        }

        return true;
    }
}
