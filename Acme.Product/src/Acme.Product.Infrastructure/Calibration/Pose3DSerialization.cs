using System.Numerics;
using System.Text.Json;

namespace Acme.Product.Infrastructure.Calibration;

public static class Pose3DSerialization
{
    public static bool TryParsePoseList(object? raw, out List<Matrix4x4> poses, out string error)
    {
        poses = [];
        error = string.Empty;

        if (raw == null)
        {
            error = "Pose list input is required.";
            return false;
        }

        if (raw is IEnumerable<Matrix4x4> typedMatrices)
        {
            poses = typedMatrices.ToList();
            if (poses.Count == 0)
            {
                error = "Pose list is empty.";
                return false;
            }

            return true;
        }

        if (raw is JsonElement jsonElement)
        {
            return TryParsePoseList(jsonElement, out poses, out error);
        }

        if (raw is string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                return TryParsePoseList(document.RootElement, out poses, out error);
            }
            catch (Exception ex)
            {
                error = $"Failed to parse pose list JSON: {ex.Message}";
                return false;
            }
        }

        if (raw is IEnumerable<object> values)
        {
            var index = 0;
            foreach (var value in values)
            {
                if (!TryParsePose(value, out var pose, out error))
                {
                    error = $"Failed to parse pose at index {index}: {error}";
                    return false;
                }

                poses.Add(pose);
                index++;
            }

            if (poses.Count == 0)
            {
                error = "Pose list is empty.";
                return false;
            }

            return true;
        }

        error = $"Unsupported pose list input type: {raw.GetType().Name}";
        return false;
    }

    public static bool TryParsePose(object? raw, out Matrix4x4 pose, out string error)
    {
        pose = Matrix4x4.Identity;
        error = string.Empty;

        if (raw == null)
        {
            error = "Pose input is null.";
            return false;
        }

        if (raw is Matrix4x4 matrix)
        {
            pose = matrix;
            return true;
        }

        if (raw is JsonElement jsonElement)
        {
            return TryParsePose(jsonElement, out pose, out error);
        }

        if (raw is string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                return TryParsePose(document.RootElement, out pose, out error);
            }
            catch (Exception ex)
            {
                error = $"Failed to parse pose JSON: {ex.Message}";
                return false;
            }
        }

        if (raw is IEnumerable<float> floatValues && TryCreateMatrix(floatValues.Select(x => (double)x).ToArray(), out pose))
        {
            return true;
        }

        if (raw is IEnumerable<double> doubleValues && TryCreateMatrix(doubleValues.ToArray(), out pose))
        {
            return true;
        }

        if (raw is IEnumerable<object> objectValues)
        {
            var values = objectValues.ToList();
            if (values.Count == 16 && TryCreateMatrix(values.Select(ToDouble).ToArray(), out pose))
            {
                return true;
            }
        }

        error = $"Unsupported pose input type: {raw.GetType().Name}";
        return false;
    }

    public static string ToJson(Matrix4x4 matrix)
    {
        var rows = new[]
        {
            new[] { matrix.M11, matrix.M12, matrix.M13, matrix.M14 },
            new[] { matrix.M21, matrix.M22, matrix.M23, matrix.M24 },
            new[] { matrix.M31, matrix.M32, matrix.M33, matrix.M34 },
            new[] { matrix.M41, matrix.M42, matrix.M43, matrix.M44 }
        };

        return JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
    }

    private static bool TryParsePoseList(JsonElement root, out List<Matrix4x4> poses, out string error)
    {
        poses = [];
        error = string.Empty;

        if (root.ValueKind != JsonValueKind.Array)
        {
            error = "Pose list JSON must be an array.";
            return false;
        }

        var index = 0;
        foreach (var item in root.EnumerateArray())
        {
            if (!TryParsePose(item, out var pose, out error))
            {
                error = $"Failed to parse pose at index {index}: {error}";
                return false;
            }

            poses.Add(pose);
            index++;
        }

        if (poses.Count == 0)
        {
            error = "Pose list is empty.";
            return false;
        }

        return true;
    }

    private static bool TryParsePose(JsonElement element, out Matrix4x4 pose, out string error)
    {
        pose = Matrix4x4.Identity;
        error = string.Empty;

        if (element.ValueKind == JsonValueKind.Array)
        {
            var flat = TryReadFlatMatrix(element, out pose);
            if (!flat)
            {
                error = "Pose array must be a flat 16-number array or a 4x4 nested array.";
            }

            return flat;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            error = "Pose JSON must be an object or array.";
            return false;
        }

        if (element.TryGetProperty("matrix", out var matrixElement))
        {
            if (TryParsePose(matrixElement, out pose, out error))
            {
                return true;
            }
        }

        if (element.TryGetProperty("rotation", out var rotationElement) &&
            element.TryGetProperty("translation", out var translationElement) &&
            TryReadRotation(rotationElement, out var rotation) &&
            TryReadTranslation(translationElement, out var translation))
        {
            pose = rotation;
            pose.M41 = translation.X;
            pose.M42 = translation.Y;
            pose.M43 = translation.Z;
            pose.M44 = 1f;
            return true;
        }

        error = "Pose object must contain either 'matrix' or ('rotation' + 'translation').";
        return false;
    }

    private static bool TryReadFlatMatrix(JsonElement element, out Matrix4x4 pose)
    {
        pose = Matrix4x4.Identity;

        var numbers = new List<double>();
        if (element.GetArrayLength() == 4 && element.EnumerateArray().All(x => x.ValueKind == JsonValueKind.Array))
        {
            foreach (var row in element.EnumerateArray())
            {
                foreach (var value in row.EnumerateArray())
                {
                    numbers.Add(value.GetDouble());
                }
            }
        }
        else
        {
            foreach (var value in element.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.Number)
                {
                    return false;
                }

                numbers.Add(value.GetDouble());
            }
        }

        return TryCreateMatrix(numbers.ToArray(), out pose);
    }

    private static bool TryCreateMatrix(IReadOnlyList<double> values, out Matrix4x4 matrix)
    {
        matrix = Matrix4x4.Identity;
        if (values.Count != 16)
        {
            return false;
        }

        matrix = new Matrix4x4(
            (float)values[0], (float)values[1], (float)values[2], (float)values[3],
            (float)values[4], (float)values[5], (float)values[6], (float)values[7],
            (float)values[8], (float)values[9], (float)values[10], (float)values[11],
            (float)values[12], (float)values[13], (float)values[14], (float)values[15]);
        return true;
    }

    private static bool TryReadRotation(JsonElement element, out Matrix4x4 rotation)
    {
        rotation = Matrix4x4.Identity;

        if (element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var values = new List<double>();
        if (element.GetArrayLength() == 3 && element.EnumerateArray().All(x => x.ValueKind == JsonValueKind.Array))
        {
            foreach (var row in element.EnumerateArray())
            {
                foreach (var value in row.EnumerateArray())
                {
                    values.Add(value.GetDouble());
                }
            }
        }
        else
        {
            values.AddRange(element.EnumerateArray().Select(x => x.GetDouble()));
        }

        if (values.Count != 9)
        {
            return false;
        }

        rotation = new Matrix4x4(
            (float)values[0], (float)values[1], (float)values[2], 0f,
            (float)values[3], (float)values[4], (float)values[5], 0f,
            (float)values[6], (float)values[7], (float)values[8], 0f,
            0f, 0f, 0f, 1f);
        return true;
    }

    private static bool TryReadTranslation(JsonElement element, out Vector3 translation)
    {
        translation = Vector3.Zero;
        if (element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var values = element.EnumerateArray().Select(x => x.GetDouble()).ToArray();
        if (values.Length != 3)
        {
            return false;
        }

        translation = new Vector3((float)values[0], (float)values[1], (float)values[2]);
        return true;
    }

    private static double ToDouble(object? value)
    {
        return value switch
        {
            null => 0d,
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Number => jsonElement.GetDouble(),
            IConvertible convertible => convertible.ToDouble(System.Globalization.CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"Unsupported numeric value type: {value.GetType().Name}")
        };
    }
}
