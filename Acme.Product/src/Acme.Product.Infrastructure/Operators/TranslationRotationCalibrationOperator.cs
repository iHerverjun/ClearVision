using System.Globalization;
using System.Text.Json;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Calibration;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "平移旋转标定",
    Description = "Fits robust 2D rigid or similarity transform from image-to-robot point pairs.",
    Category = "标定",
    IconName = "calibration",
    Keywords = new[] { "calibration", "translation", "rotation", "svd", "similarity" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = false)]
[OutputPort("CalibrationData", "Calibration Data", PortDataType.String)]
[OutputPort("CalibrationError", "Calibration Error", PortDataType.Float)]
[OutputPort("MaxCalibrationError", "Max Calibration Error", PortDataType.Float)]
[OperatorParam("CalibrationPoints", "Calibration Points", "string", DefaultValue = "[]")]
[OperatorParam("Method", "Method", "enum", DefaultValue = "LeastSquares", Options = new[] { "LeastSquares|LeastSquares", "SVD|SVD" })]
[OperatorParam("SavePath", "Save Path", "file", DefaultValue = "")]
public class TranslationRotationCalibrationOperator : OperatorBase
{
    private const double DegenerateThreshold = 1e-9;

    public override OperatorType OperatorType => OperatorType.TranslationRotationCalibration;

    public TranslationRotationCalibrationOperator(ILogger<TranslationRotationCalibrationOperator> logger)
        : base(logger)
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
            return Task.FromResult(OperatorExecutionOutput.Failure("CalibrationPoints must contain at least 3 valid points."));
        }

        if (!TryValidatePointGeometry(points, out var geometryError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(geometryError));
        }

        var solveMethod = method.Equals("SVD", StringComparison.OrdinalIgnoreCase)
            ? SolveMethod.RigidSvd
            : SolveMethod.SimilarityLeastSquares;

        if (!TrySolveTransform(points, solveMethod, out var transformMatrix, out var solvedScale, out var solveError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(solveError));
        }

        var errorStats = ComputeErrorStats(points, transformMatrix);

        var diagnostics = new List<string>
        {
            $"method={solveMethod}",
            $"sample_count={points.Count}",
            $"estimated_scale={solvedScale.ToString("G17", CultureInfo.InvariantCulture)}"
        };
        if (points.Any(p => Math.Abs(p.Angle) > 1e-9))
        {
            diagnostics.Add("input_angle_present=true");
        }

        var transformModel = solveMethod == SolveMethod.RigidSvd
            ? TransformModelV2.Rigid
            : TransformModelV2.Similarity;
        var accepted = errorStats.RmsError <= 0.15 && errorStats.MaxError <= 0.30;
        diagnostics.Add($"accepted={accepted}");

        var bundle = new CalibrationBundleV2
        {
            CalibrationKind = CalibrationKindV2.RigidTransform2D,
            TransformModel = transformModel,
            SourceFrame = "image",
            TargetFrame = "robot",
            Unit = "mm",
            Transform2D = new CalibrationTransform2DV2
            {
                Model = transformModel,
                Matrix = transformMatrix,
                PixelSizeX = solvedScale,
                PixelSizeY = solvedScale
            },
            Quality = new CalibrationQualityV2
            {
                Accepted = accepted,
                MeanError = errorStats.RmsError,
                MaxError = errorStats.MaxError,
                InlierCount = points.Count,
                TotalSampleCount = points.Count,
                Diagnostics = diagnostics
            },
            ProducerOperator = nameof(TranslationRotationCalibrationOperator)
        };

        var calibrationData = CalibrationBundleV2Json.Serialize(bundle);
        if (!string.IsNullOrWhiteSpace(savePath))
        {
            TrySaveCalibrationBundle(savePath, calibrationData);
        }

        var output = new Dictionary<string, object>
        {
            ["CalibrationData"] = calibrationData,
            ["Accepted"] = accepted,
            ["TransformModel"] = transformModel.ToString(),
            ["CalibrationError"] = errorStats.RmsError,
            ["MaxCalibrationError"] = errorStats.MaxError
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
            return ValidationResult.Invalid("Method must be LeastSquares or SVD.");
        }

        var pointsJson = GetStringParam(@operator, "CalibrationPoints", string.Empty);
        if (string.IsNullOrWhiteSpace(pointsJson))
        {
            return ValidationResult.Invalid("CalibrationPoints cannot be empty.");
        }

        if (!TryParseCalibrationPoints(pointsJson, out var points) || points.Count < 3)
        {
            return ValidationResult.Invalid("CalibrationPoints must contain at least 3 valid points.");
        }

        if (!TryValidatePointGeometry(points, out var geometryError))
        {
            return ValidationResult.Invalid(geometryError);
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

                if (!IsFinite(imageX) || !IsFinite(imageY) || !IsFinite(robotX) || !IsFinite(robotY))
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

    private static bool TryValidatePointGeometry(IReadOnlyList<CalibrationPoint> points, out string error)
    {
        error = string.Empty;
        if (points.Count < 3)
        {
            error = "At least 3 point pairs are required.";
            return false;
        }

        var uniqueSrc = points
            .Select(p => $"{p.ImageX:F12}|{p.ImageY:F12}")
            .Distinct(StringComparer.Ordinal)
            .Count();
        var uniqueDst = points
            .Select(p => $"{p.RobotX:F12}|{p.RobotY:F12}")
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (uniqueSrc < 2 || uniqueDst < 2)
        {
            error = "Point set is degenerate: at least two unique source and destination points are required.";
            return false;
        }

        var srcCx = points.Average(p => p.ImageX);
        var srcCy = points.Average(p => p.ImageY);
        var srcVar = points.Sum(p =>
        {
            var dx = p.ImageX - srcCx;
            var dy = p.ImageY - srcCy;
            return dx * dx + dy * dy;
        }) / points.Count;
        if (srcVar <= DegenerateThreshold)
        {
            error = "Point set is degenerate: source points have near-zero variance.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TrySolveTransform(
        IReadOnlyList<CalibrationPoint> points,
        SolveMethod method,
        out double[][] matrix,
        out double solvedScale,
        out string error)
    {
        matrix = Array.Empty<double[]>();
        solvedScale = 1.0;
        error = string.Empty;

        var srcCx = points.Average(p => p.ImageX);
        var srcCy = points.Average(p => p.ImageY);
        var dstCx = points.Average(p => p.RobotX);
        var dstCy = points.Average(p => p.RobotY);

        var h00 = 0.0;
        var h01 = 0.0;
        var h10 = 0.0;
        var h11 = 0.0;
        var srcVar = 0.0;

        foreach (var p in points)
        {
            var sx = p.ImageX - srcCx;
            var sy = p.ImageY - srcCy;
            var dx = p.RobotX - dstCx;
            var dy = p.RobotY - dstCy;

            h00 += sx * dx;
            h01 += sx * dy;
            h10 += sy * dx;
            h11 += sy * dy;
            srcVar += sx * sx + sy * sy;
        }

        srcVar /= points.Count;
        if (srcVar <= DegenerateThreshold)
        {
            error = "Cannot solve transform from degenerate source points.";
            return false;
        }

        using var h = new Mat(2, 2, MatType.CV_64FC1);
        h.Set(0, 0, h00);
        h.Set(0, 1, h01);
        h.Set(1, 0, h10);
        h.Set(1, 1, h11);

        using var w = new Mat();
        using var u = new Mat();
        using var vt = new Mat();
        Cv2.SVDecomp(h, w, u, vt);

        if (w.Empty() || w.Rows * w.Cols < 2)
        {
            error = "SVD decomposition failed.";
            return false;
        }

        var singular0 = w.At<double>(0, 0);
        var singular1 = w.At<double>(1, 0);
        if (singular0 <= DegenerateThreshold || singular1 <= DegenerateThreshold)
        {
            error = "Point set is singular or near-collinear; cannot derive a stable transform.";
            return false;
        }

        using var v = new Mat();
        using var ut = new Mat();
        Cv2.Transpose(vt, v);
        Cv2.Transpose(u, ut);
        using var r = new Mat();
        using var empty = new Mat();
        Cv2.Gemm(v, ut, 1.0, empty, 0.0, r);

        if (Cv2.Determinant(r) < 0)
        {
            for (var row = 0; row < 2; row++)
            {
                v.Set(row, 1, -v.At<double>(row, 1));
            }

            using var adjusted = new Mat();
            Cv2.Gemm(v, ut, 1.0, empty, 0.0, adjusted);
            adjusted.CopyTo(r);
        }

        var r00 = r.At<double>(0, 0);
        var r01 = r.At<double>(0, 1);
        var r10 = r.At<double>(1, 0);
        var r11 = r.At<double>(1, 1);

        solvedScale = 1.0;
        if (method == SolveMethod.SimilarityLeastSquares)
        {
            var det = Cv2.Determinant(r) >= 0 ? 1.0 : -1.0;
            var trace = singular0 + det * singular1;
            solvedScale = trace / srcVar;
            if (!IsFinite(solvedScale) || solvedScale <= DegenerateThreshold)
            {
                error = "Solved scale is invalid; check calibration point geometry.";
                return false;
            }
        }

        var tx = dstCx - solvedScale * (r00 * srcCx + r01 * srcCy);
        var ty = dstCy - solvedScale * (r10 * srcCx + r11 * srcCy);

        matrix = new[]
        {
            new[] { solvedScale * r00, solvedScale * r01, tx },
            new[] { solvedScale * r10, solvedScale * r11, ty }
        };
        error = string.Empty;
        return true;
    }

    private static CalibrationErrorStats ComputeErrorStats(IReadOnlyList<CalibrationPoint> points, double[][] matrix)
    {
        var sumSquared = 0.0;
        var maxError = 0.0;

        foreach (var p in points)
        {
            var x = matrix[0][0] * p.ImageX + matrix[0][1] * p.ImageY + matrix[0][2];
            var y = matrix[1][0] * p.ImageX + matrix[1][1] * p.ImageY + matrix[1][2];
            var dx = x - p.RobotX;
            var dy = y - p.RobotY;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            sumSquared += distance * distance;
            if (distance > maxError)
            {
                maxError = distance;
            }
        }

        return new CalibrationErrorStats
        {
            RmsError = Math.Sqrt(sumSquared / Math.Max(1, points.Count)),
            MaxError = maxError
        };
    }

    private void TrySaveCalibrationBundle(string savePath, string calibrationData)
    {
        try
        {
            var dir = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(savePath, calibrationData);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to save calibration bundle to {Path}", savePath);
        }
    }

    private static void DrawPoints(Mat image, IReadOnlyList<CalibrationPoint> points)
    {
        for (var i = 0; i < points.Count; i++)
        {
            var x = (int)Math.Round(points[i].ImageX);
            var y = (int)Math.Round(points[i].ImageY);
            Cv2.Circle(image, new Point(x, y), 4, new Scalar(0, 255, 0), -1);
            Cv2.PutText(image, (i + 1).ToString(CultureInfo.InvariantCulture), new Point(x + 5, y - 5), HersheyFonts.HersheySimplex, 0.45, new Scalar(0, 255, 255), 1);
        }
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private enum SolveMethod
    {
        SimilarityLeastSquares = 0,
        RigidSvd = 1
    }

    private sealed record CalibrationPoint(double ImageX, double ImageY, double RobotX, double RobotY, double Angle);

    private sealed class CalibrationErrorStats
    {
        public double RmsError { get; init; }

        public double MaxError { get; init; }
    }
}
