// TranslationRotationCalibrationOperator.cs
// 平移旋转标定算子
// 求解平移与旋转参数用于坐标系标定
// 作者：蘅芜君
using System.Globalization;
using System.Text.Json;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "平移旋转标定",
    Description = "Fits image-to-robot transform from calibration point pairs.",
    Category = "标定",
    IconName = "calibration",
    Keywords = new[] { "calibration", "hand-eye", "translation", "rotation" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = false)]
[OutputPort("TransformMatrix", "Transform Matrix", PortDataType.Any)]
[OutputPort("RotationCenter", "Rotation Center", PortDataType.Point)]
[OutputPort("CalibrationError", "Calibration Error", PortDataType.Float)]
[OperatorParam("CalibrationPoints", "Calibration Points", "string", DefaultValue = "[]")]
[OperatorParam("Method", "Method", "enum", DefaultValue = "LeastSquares", Options = new[] { "LeastSquares|LeastSquares", "SVD|SVD" })]
[OperatorParam("SavePath", "Save Path", "file", DefaultValue = "")]
public class TranslationRotationCalibrationOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.TranslationRotationCalibration;

    public TranslationRotationCalibrationOperator(ILogger<TranslationRotationCalibrationOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var pointsJson = GetStringParam(@operator, "CalibrationPoints", string.Empty);
        var method = GetStringParam(@operator, "Method", "LeastSquares");
        var savePath = GetStringParam(@operator, "SavePath", string.Empty);

        if (!TryParseCalibrationPoints(pointsJson, out var points) || points.Count < 3)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("CalibrationPoints must contain at least 3 valid points"));
        }

        double[][] matrix;
        if (method.Equals("SVD", StringComparison.OrdinalIgnoreCase))
        {
            matrix = SolveRigidTransform(points);
        }
        else
        {
            matrix = SolveAffineLeastSquares(points);
        }

        var error = ComputeRmsError(points, matrix);
        var rotationCenter = new Position(points.Average(p => p.ImageX), points.Average(p => p.ImageY));

        if (!string.IsNullOrWhiteSpace(savePath))
        {
            TrySaveCalibration(savePath, method, matrix, error, points);
        }

        var output = new Dictionary<string, object>
        {
            { "TransformMatrix", matrix },
            { "RotationCenter", rotationCenter },
            { "CalibrationError", error }
        };

        if (TryGetInputImage(inputs, out var imageWrapper) && imageWrapper != null)
        {
            var src = imageWrapper.GetMat();
            if (!src.Empty())
            {
                var result = src.Clone();
                DrawPoints(result, points);
                return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(result, output)));
            }
        }

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var method = GetStringParam(@operator, "Method", "LeastSquares");
        var validMethods = new[] { "LeastSquares", "SVD" };
        if (!validMethods.Contains(method, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Method must be LeastSquares or SVD");
        }

        var pointsJson = GetStringParam(@operator, "CalibrationPoints", string.Empty);
        if (string.IsNullOrWhiteSpace(pointsJson))
        {
            return ValidationResult.Invalid("CalibrationPoints cannot be empty");
        }

        if (!TryParseCalibrationPoints(pointsJson, out var points) || points.Count < 3)
        {
            return ValidationResult.Invalid("CalibrationPoints must contain at least 3 valid points");
        }

        return ValidationResult.Valid();
    }

    private static bool TryParseCalibrationPoints(string json, out List<CalibrationPoint> points)
    {
        points = new List<CalibrationPoint>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!TryGetNumber(item, "imageX", out var imageX) ||
                    !TryGetNumber(item, "imageY", out var imageY) ||
                    !TryGetNumber(item, "robotX", out var robotX) ||
                    !TryGetNumber(item, "robotY", out var robotY))
                {
                    continue;
                }

                TryGetNumber(item, "angle", out var angle);
                points.Add(new CalibrationPoint(imageX, imageY, robotX, robotY, angle));
            }

            return points.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetNumber(JsonElement obj, string name, out double value)
    {
        value = 0;
        foreach (var property in obj.EnumerateObject())
        {
            if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Number)
            {
                return property.Value.TryGetDouble(out value);
            }

            if (property.Value.ValueKind == JsonValueKind.String &&
                double.TryParse(property.Value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            return false;
        }

        return false;
    }

    private static double[][] SolveAffineLeastSquares(IReadOnlyList<CalibrationPoint> points)
    {
        var ata = new double[3, 3];
        var atx = new double[3];
        var aty = new double[3];

        foreach (var p in points)
        {
            var row = new[] { p.ImageX, p.ImageY, 1.0 };
            for (var i = 0; i < 3; i++)
            {
                for (var j = 0; j < 3; j++)
                {
                    ata[i, j] += row[i] * row[j];
                }

                atx[i] += row[i] * p.RobotX;
                aty[i] += row[i] * p.RobotY;
            }
        }

        var px = Solve3x3(ata, atx);
        var py = Solve3x3(ata, aty);

        return new[]
        {
            new[] { px[0], px[1], px[2] },
            new[] { py[0], py[1], py[2] }
        };
    }

    private static double[][] SolveRigidTransform(IReadOnlyList<CalibrationPoint> points)
    {
        var cx = points.Average(p => p.ImageX);
        var cy = points.Average(p => p.ImageY);
        var rx = points.Average(p => p.RobotX);
        var ry = points.Average(p => p.RobotY);

        var sxx = 0.0;
        var sxy = 0.0;

        foreach (var p in points)
        {
            var px = p.ImageX - cx;
            var py = p.ImageY - cy;
            var qx = p.RobotX - rx;
            var qy = p.RobotY - ry;

            sxx += px * qx + py * qy;
            sxy += px * qy - py * qx;
        }

        var theta = Math.Atan2(sxy, sxx);
        var cos = Math.Cos(theta);
        var sin = Math.Sin(theta);

        var tx = rx - (cos * cx - sin * cy);
        var ty = ry - (sin * cx + cos * cy);

        return new[]
        {
            new[] { cos, -sin, tx },
            new[] { sin, cos, ty }
        };
    }

    private static double ComputeRmsError(IReadOnlyList<CalibrationPoint> points, double[][] matrix)
    {
        var sum = 0.0;

        foreach (var p in points)
        {
            var x = matrix[0][0] * p.ImageX + matrix[0][1] * p.ImageY + matrix[0][2];
            var y = matrix[1][0] * p.ImageX + matrix[1][1] * p.ImageY + matrix[1][2];

            var dx = x - p.RobotX;
            var dy = y - p.RobotY;
            sum += dx * dx + dy * dy;
        }

        return Math.Sqrt(sum / Math.Max(1, points.Count));
    }

    private void TrySaveCalibration(string savePath, string method, double[][] matrix, double error, IReadOnlyList<CalibrationPoint> points)
    {
        try
        {
            var dir = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var payload = new
            {
                Method = method,
                TransformMatrix = matrix,
                CalibrationError = error,
                Points = points,
                Timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(savePath, json);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to save calibration to {Path}", savePath);
        }
    }

    private static void DrawPoints(Mat image, IReadOnlyList<CalibrationPoint> points)
    {
        for (var i = 0; i < points.Count; i++)
        {
            var x = (int)Math.Round(points[i].ImageX);
            var y = (int)Math.Round(points[i].ImageY);
            Cv2.Circle(image, new Point(x, y), 4, new Scalar(0, 255, 0), -1);
            Cv2.PutText(image, (i + 1).ToString(), new Point(x + 5, y - 5), HersheyFonts.HersheySimplex, 0.45, new Scalar(0, 255, 255), 1);
        }
    }

    private static double[] Solve3x3(double[,] a, double[] b)
    {
        var m = new double[3, 4];
        for (var r = 0; r < 3; r++)
        {
            for (var c = 0; c < 3; c++)
            {
                m[r, c] = a[r, c];
            }

            m[r, 3] = b[r];
        }

        for (var pivot = 0; pivot < 3; pivot++)
        {
            var best = pivot;
            for (var r = pivot + 1; r < 3; r++)
            {
                if (Math.Abs(m[r, pivot]) > Math.Abs(m[best, pivot]))
                {
                    best = r;
                }
            }

            if (Math.Abs(m[best, pivot]) < 1e-12)
            {
                return new[] { 1.0, 0.0, 0.0 };
            }

            if (best != pivot)
            {
                for (var c = pivot; c < 4; c++)
                {
                    (m[pivot, c], m[best, c]) = (m[best, c], m[pivot, c]);
                }
            }

            var div = m[pivot, pivot];
            for (var c = pivot; c < 4; c++)
            {
                m[pivot, c] /= div;
            }

            for (var r = 0; r < 3; r++)
            {
                if (r == pivot)
                {
                    continue;
                }

                var factor = m[r, pivot];
                for (var c = pivot; c < 4; c++)
                {
                    m[r, c] -= factor * m[pivot, c];
                }
            }
        }

        return new[] { m[0, 3], m[1, 3], m[2, 3] };
    }

    private sealed record CalibrationPoint(double ImageX, double ImageY, double RobotX, double RobotY, double Angle);
}

