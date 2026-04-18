using System.Collections;
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
    DisplayName = "N Point Calibration",
    Description = "Builds robust affine or homography calibration from all point pairs.",
    Category = "Calibration",
    IconName = "n-point",
    Keywords = new[] { "n-point", "affine", "homography", "calibration", "ransac" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = false)]
[OutputPort("CalibrationData", "Calibration Data", PortDataType.String)]
[OutputPort("ReprojectionError", "Reprojection Error", PortDataType.Float)]
[OperatorParam("CalibrationMode", "Calibration Mode", "enum", DefaultValue = "Affine", Options = new[] { "Affine|Affine", "Perspective|Perspective" })]
[OperatorParam("PointPairs", "Point Pairs", "string", DefaultValue = "")]
[OperatorParam("SavePath", "Save Path", "file", DefaultValue = "")]
public class NPointCalibrationOperator : OperatorBase
{
    private const double MinPointDistance = 1e-6;
    private const double MaxAcceptedReprojectionError = 3.0;

    public override OperatorType OperatorType => OperatorType.NPointCalibration;

    public NPointCalibrationOperator(ILogger<NPointCalibrationOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var mode = GetStringParam(@operator, "CalibrationMode", "Affine");
        var pointPairsRaw = ResolvePointPairsRaw(@operator, inputs);

        if (!TryParsePointPairs(pointPairsRaw, out var pointPairs) || pointPairs.Count == 0)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("PointPairs is required and must be valid JSON or list data."));
        }

        var isPerspective = mode.Equals("Perspective", StringComparison.OrdinalIgnoreCase);
        var requiredCount = isPerspective ? 4 : 3;
        if (pointPairs.Count < requiredCount)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"{mode} mode requires at least {requiredCount} point pairs."));
        }

        var srcPoints = pointPairs.Select(p => new Point2d(p.ImagePoint.X, p.ImagePoint.Y)).ToArray();
        var dstPoints = pointPairs.Select(p => new Point2d(p.WorldPoint.X, p.WorldPoint.Y)).ToArray();

        if (!TryValidatePointSet(srcPoints, requiredCount, "ImagePoint", out var sourceValidationError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(sourceValidationError ?? "ImagePoint set is invalid."));
        }

        if (!TryValidatePointSet(dstPoints, requiredCount, "WorldPoint", out var targetValidationError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(targetValidationError ?? "WorldPoint set is invalid."));
        }

        if (isPerspective)
        {
            return Task.FromResult(ExecutePerspectiveCalibration(@operator, inputs, pointPairs, srcPoints, dstPoints));
        }

        return Task.FromResult(ExecuteAffineCalibration(@operator, inputs, pointPairs, srcPoints, dstPoints));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var mode = GetStringParam(@operator, "CalibrationMode", "Affine");
        if (!mode.Equals("Affine", StringComparison.OrdinalIgnoreCase) &&
            !mode.Equals("Perspective", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("CalibrationMode must be Affine or Perspective.");
        }

        var pointPairsRaw = ResolvePointPairsRaw(@operator, null);
        if (string.IsNullOrWhiteSpace(pointPairsRaw))
        {
            return ValidationResult.Valid();
        }

        if (!TryParsePointPairs(pointPairsRaw, out var pointPairs))
        {
            return ValidationResult.Invalid("PointPairs is not valid JSON/list format.");
        }

        var requiredCount = mode.Equals("Perspective", StringComparison.OrdinalIgnoreCase) ? 4 : 3;
        if (pointPairs.Count < requiredCount)
        {
            return ValidationResult.Invalid($"{mode} mode requires at least {requiredCount} point pairs.");
        }

        return ValidationResult.Valid();
    }

    private OperatorExecutionOutput ExecuteAffineCalibration(
        Operator @operator,
        Dictionary<string, object>? inputs,
        IReadOnlyList<PointPair> pointPairs,
        IReadOnlyList<Point2d> srcPoints,
        IReadOnlyList<Point2d> dstPoints)
    {
        using var srcMat = InputArray.Create(srcPoints.ToArray());
        using var dstMat = InputArray.Create(dstPoints.ToArray());
        using var inlierMask = new Mat();
        using var affineMatrix = Cv2.EstimateAffine2D(
            srcMat,
            dstMat,
            inlierMask,
            RobustEstimationAlgorithms.RANSAC,
            3.0,
            3000,
            0.995,
            20);

        if (affineMatrix is null || affineMatrix.Empty() || affineMatrix.Rows != 2 || affineMatrix.Cols != 3)
        {
            return OperatorExecutionOutput.Failure("Failed to estimate a valid affine transform.");
        }

        var transform = ToMatrixArray(affineMatrix, 2, 3);
        if (!CalibrationBundleV2Helpers.IsFiniteMatrix(transform))
        {
            return OperatorExecutionOutput.Failure("Estimated affine transform contains invalid values.");
        }

        if (!TryGetInlierFlags(inlierMask, pointPairs.Count, out var inlierFlags))
        {
            return OperatorExecutionOutput.Failure("Failed to parse affine inlier mask.");
        }

        var errorStats = CalculateAffineReprojectionErrors(pointPairs, transform, inlierFlags);
        if (errorStats.InlierCount < 3)
        {
            return OperatorExecutionOutput.Failure("Affine estimation failed because inliers are insufficient.");
        }

        var pixelSizeX = Math.Sqrt(transform[0][0] * transform[0][0] + transform[1][0] * transform[1][0]);
        var pixelSizeY = Math.Sqrt(transform[0][1] * transform[0][1] + transform[1][1] * transform[1][1]);
        double? pixelSize = null;
        if (pixelSizeX > 0 && pixelSizeY > 0)
        {
            var anisotropy = Math.Abs(pixelSizeX - pixelSizeY) / Math.Max(pixelSizeX, pixelSizeY);
            if (anisotropy <= 0.02)
            {
                pixelSize = (pixelSizeX + pixelSizeY) * 0.5;
            }
        }

        var accepted = errorStats.MeanError <= MaxAcceptedReprojectionError &&
                       errorStats.InlierCount >= 3 &&
                       errorStats.InlierRatio >= 0.5;

        var diagnostics = new List<string>
        {
            "Affine transform estimated with all points via RANSAC.",
            $"InlierRatio={errorStats.InlierRatio:F3}",
            $"InlierCount={errorStats.InlierCount}/{pointPairs.Count}"
        };
        if (!accepted)
        {
            diagnostics.Add($"Mean reprojection error {errorStats.MeanError:F4} exceeds acceptance threshold {MaxAcceptedReprojectionError:F4}.");
        }

        var bundle = CreateBundle(
            TransformModelV2.Affine,
            transform,
            pixelSizeX,
            pixelSizeY,
            accepted,
            diagnostics,
            errorStats,
            pointPairs.Count);

        return BuildSuccessOutput(@operator, inputs, pointPairs, bundle, transform, pixelSize, pixelSizeX, pixelSizeY, errorStats);
    }

    private OperatorExecutionOutput ExecutePerspectiveCalibration(
        Operator @operator,
        Dictionary<string, object>? inputs,
        IReadOnlyList<PointPair> pointPairs,
        IReadOnlyList<Point2d> srcPoints,
        IReadOnlyList<Point2d> dstPoints)
    {
        using var srcMat = InputArray.Create(srcPoints);
        using var dstMat = InputArray.Create(dstPoints);
        using var inlierMask = new Mat();
        using var homography = Cv2.FindHomography(
            srcMat,
            dstMat,
            HomographyMethods.Ransac,
            3.0,
            inlierMask,
            3000,
            0.995);

        if (homography is null || homography.Empty() || homography.Rows != 3 || homography.Cols != 3)
        {
            return OperatorExecutionOutput.Failure("Failed to estimate a valid homography.");
        }

        var transform = ToMatrixArray(homography, 3, 3);
        if (!CalibrationBundleV2Helpers.IsFiniteMatrix(transform))
        {
            return OperatorExecutionOutput.Failure("Estimated homography contains invalid values.");
        }

        var det = Cv2.Determinant(homography);
        if (!double.IsFinite(det) || Math.Abs(det) <= 1e-12)
        {
            return OperatorExecutionOutput.Failure("Estimated homography is singular.");
        }

        if (!TryGetInlierFlags(inlierMask, pointPairs.Count, out var inlierFlags))
        {
            return OperatorExecutionOutput.Failure("Failed to parse homography inlier mask.");
        }

        var errorStats = CalculateHomographyReprojectionErrors(pointPairs, transform, inlierFlags);
        if (errorStats.InlierCount < 4)
        {
            return OperatorExecutionOutput.Failure("Homography estimation failed because inliers are insufficient.");
        }

        var accepted = errorStats.MeanError <= MaxAcceptedReprojectionError &&
                       errorStats.InlierCount >= 4 &&
                       errorStats.InlierRatio >= 0.5;

        var diagnostics = new List<string>
        {
            "Perspective transform estimated with all points via FindHomography(RANSAC).",
            $"InlierRatio={errorStats.InlierRatio:F3}",
            $"InlierCount={errorStats.InlierCount}/{pointPairs.Count}",
            "PixelSize is intentionally not reported for homography model."
        };
        if (!accepted)
        {
            diagnostics.Add($"Mean reprojection error {errorStats.MeanError:F4} exceeds acceptance threshold {MaxAcceptedReprojectionError:F4}.");
        }

        var bundle = CreateBundle(
            TransformModelV2.Homography,
            transform,
            pixelSizeX: null,
            pixelSizeY: null,
            accepted,
            diagnostics,
            errorStats,
            pointPairs.Count);

        return BuildSuccessOutput(@operator, inputs, pointPairs, bundle, transform, pixelSize: null, pixelSizeX: null, pixelSizeY: null, errorStats);
    }

    private OperatorExecutionOutput BuildSuccessOutput(
        Operator @operator,
        Dictionary<string, object>? inputs,
        IReadOnlyList<PointPair> pointPairs,
        CalibrationBundleV2 bundle,
        double[][] transform,
        double? pixelSize,
        double? pixelSizeX,
        double? pixelSizeY,
        ReprojectionErrorStats errorStats)
    {
        var calibrationJson = CalibrationBundleV2Json.Serialize(bundle);
        var savePath = GetStringParam(@operator, "SavePath", string.Empty);
        if (!string.IsNullOrWhiteSpace(savePath))
        {
            TrySaveCalibration(savePath, calibrationJson);
        }

        var resultData = new Dictionary<string, object>
        {
            ["CalibrationData"] = calibrationJson,
            ["CalibrationBundle"] = bundle,
            ["ReprojectionError"] = errorStats.MeanError,
            ["MaxReprojectionError"] = errorStats.MaxError,
            ["InlierCount"] = errorStats.InlierCount,
            ["TotalSampleCount"] = pointPairs.Count,
            ["InlierRatio"] = errorStats.InlierRatio,
            ["Accepted"] = bundle.Quality.Accepted
        };


        if (TryGetInputImage(inputs, out var imageWrapper) && imageWrapper != null)
        {
            var src = imageWrapper.GetMat();
            if (!src.Empty())
            {
                var resultImage = src.Clone();
                DrawCalibrationPoints(resultImage, pointPairs);
                return OperatorExecutionOutput.Success(CreateImageOutput(resultImage, resultData));
            }
        }

        return OperatorExecutionOutput.Success(resultData);
    }

    private static CalibrationBundleV2 CreateBundle(
        TransformModelV2 model,
        double[][] transformMatrix,
        double? pixelSizeX,
        double? pixelSizeY,
        bool accepted,
        IReadOnlyList<string> diagnostics,
        ReprojectionErrorStats errorStats,
        int sampleCount)
    {
        return new CalibrationBundleV2
        {
            SchemaVersion = 2,
            CalibrationKind = CalibrationKindV2.PlanarTransform2D,
            TransformModel = model,
            SourceFrame = "image",
            TargetFrame = "world",
            Unit = "mm",
            Transform2D = new CalibrationTransform2DV2
            {
                Model = model,
                Matrix = transformMatrix,
                PixelSizeX = pixelSizeX,
                PixelSizeY = pixelSizeY
            },
            Quality = new CalibrationQualityV2
            {
                Accepted = accepted,
                MeanError = errorStats.MeanError,
                MaxError = errorStats.MaxError,
                InlierCount = errorStats.InlierCount,
                TotalSampleCount = sampleCount,
                Diagnostics = diagnostics.ToList()
            },
            GeneratedAtUtc = DateTime.UtcNow,
            ProducerOperator = nameof(NPointCalibrationOperator)
        };
    }

    private void TrySaveCalibration(string savePath, string calibrationJson)
    {
        try
        {
            var directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(savePath, calibrationJson);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to save calibration data to {Path}.", savePath);
        }
    }

    private static bool TryValidatePointSet(
        IReadOnlyList<Point2d> points,
        int requiredCount,
        string pointName,
        out string? error)
    {
        error = null;
        if (points.Count < requiredCount)
        {
            error = $"{pointName} requires at least {requiredCount} points.";
            return false;
        }

        for (var i = 0; i < points.Count; i++)
        {
            if (!double.IsFinite(points[i].X) || !double.IsFinite(points[i].Y))
            {
                error = $"{pointName} contains non-finite values.";
                return false;
            }
        }

        for (var i = 0; i < points.Count; i++)
        {
            for (var j = i + 1; j < points.Count; j++)
            {
                var dx = points[i].X - points[j].X;
                var dy = points[i].Y - points[j].Y;
                if (dx * dx + dy * dy <= MinPointDistance * MinPointDistance)
                {
                    error = $"{pointName} contains duplicate or near-duplicate points.";
                    return false;
                }
            }
        }

        var maxTriangleArea = GetMaxTriangleArea(points);
        var minX = points.Min(p => p.X);
        var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxY = points.Max(p => p.Y);
        var scale = Math.Max(maxX - minX, maxY - minY);
        var minArea = Math.Max(1e-8, scale * scale * 1e-4);
        if (maxTriangleArea < minArea)
        {
            error = $"{pointName} is geometrically degenerate (nearly collinear).";
            return false;
        }

        return true;
    }

    private static bool TryGetInlierFlags(Mat inlierMask, int pointCount, out bool[] inlierFlags)
    {
        inlierFlags = Enumerable.Repeat(true, pointCount).ToArray();
        if (inlierMask.Empty())
        {
            return true;
        }

        try
        {
            if (inlierMask.Rows == pointCount && inlierMask.Cols >= 1)
            {
                for (var i = 0; i < pointCount; i++)
                {
                    inlierFlags[i] = inlierMask.At<byte>(i, 0) != 0;
                }

                return true;
            }

            if (inlierMask.Cols == pointCount && inlierMask.Rows >= 1)
            {
                for (var i = 0; i < pointCount; i++)
                {
                    inlierFlags[i] = inlierMask.At<byte>(0, i) != 0;
                }

                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static ReprojectionErrorStats CalculateAffineReprojectionErrors(
        IReadOnlyList<PointPair> pairs,
        IReadOnlyList<double[]> matrix,
        IReadOnlyList<bool> inliers)
    {
        var allErrors = new List<double>(pairs.Count);
        var inlierErrors = new List<double>(pairs.Count);
        var inlierCount = 0;

        for (var i = 0; i < pairs.Count; i++)
        {
            var x = pairs[i].ImagePoint.X;
            var y = pairs[i].ImagePoint.Y;
            var px = matrix[0][0] * x + matrix[0][1] * y + matrix[0][2];
            var py = matrix[1][0] * x + matrix[1][1] * y + matrix[1][2];
            var dx = px - pairs[i].WorldPoint.X;
            var dy = py - pairs[i].WorldPoint.Y;
            var error = Math.Sqrt(dx * dx + dy * dy);
            allErrors.Add(error);

            if (inliers[i])
            {
                inlierCount++;
                inlierErrors.Add(error);
            }
        }

        var selected = inlierErrors.Count > 0 ? inlierErrors : allErrors;
        return new ReprojectionErrorStats(
            selected.Average(),
            selected.Max(),
            inlierCount,
            pairs.Count == 0 ? 0 : inlierCount / (double)pairs.Count);
    }

    private static ReprojectionErrorStats CalculateHomographyReprojectionErrors(
        IReadOnlyList<PointPair> pairs,
        IReadOnlyList<double[]> matrix,
        IReadOnlyList<bool> inliers)
    {
        var allErrors = new List<double>(pairs.Count);
        var inlierErrors = new List<double>(pairs.Count);
        var inlierCount = 0;

        for (var i = 0; i < pairs.Count; i++)
        {
            var x = pairs[i].ImagePoint.X;
            var y = pairs[i].ImagePoint.Y;

            var w = matrix[2][0] * x + matrix[2][1] * y + matrix[2][2];
            if (Math.Abs(w) <= 1e-12)
            {
                allErrors.Add(double.MaxValue / 4);
                continue;
            }

            var px = (matrix[0][0] * x + matrix[0][1] * y + matrix[0][2]) / w;
            var py = (matrix[1][0] * x + matrix[1][1] * y + matrix[1][2]) / w;
            var dx = px - pairs[i].WorldPoint.X;
            var dy = py - pairs[i].WorldPoint.Y;
            var error = Math.Sqrt(dx * dx + dy * dy);
            allErrors.Add(error);

            if (inliers[i])
            {
                inlierCount++;
                inlierErrors.Add(error);
            }
        }

        var selected = inlierErrors.Count > 0 ? inlierErrors : allErrors;
        return new ReprojectionErrorStats(
            selected.Average(),
            selected.Max(),
            inlierCount,
            pairs.Count == 0 ? 0 : inlierCount / (double)pairs.Count);
    }

    private static string ResolvePointPairsRaw(Operator @operator, Dictionary<string, object>? inputs)
    {
        if (inputs != null && inputs.TryGetValue("PointPairs", out var pairObj) && pairObj != null)
        {
            return pairObj.ToString() ?? string.Empty;
        }

        return @operator.Parameters.FirstOrDefault(p =>
                   p.Name.Equals("PointPairs", StringComparison.OrdinalIgnoreCase))
               ?.Value?.ToString()
               ?? string.Empty;
    }

    private static bool TryParsePointPairs(object? raw, out List<PointPair> pointPairs)
    {
        pointPairs = new List<PointPair>();
        if (raw == null)
        {
            return false;
        }

        if (raw is string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            try
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (TryParsePointPair(item, out var pair))
                    {
                        pointPairs.Add(pair);
                    }
                }

                return pointPairs.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        if (raw is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (TryParsePointPair(item, out var pair))
                {
                    pointPairs.Add(pair);
                }
            }

            return pointPairs.Count > 0;
        }

        return false;
    }

    private static bool TryParsePointPair(object? raw, out PointPair pair)
    {
        pair = default;
        if (raw == null)
        {
            return false;
        }

        if (raw is JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (TryGetNumberProperty(element, "ImageX", out var imageX) &&
                TryGetNumberProperty(element, "ImageY", out var imageY) &&
                TryGetNumberProperty(element, "WorldX", out var worldX) &&
                TryGetNumberProperty(element, "WorldY", out var worldY))
            {
                pair = new PointPair(new Position(imageX, imageY), new Position(worldX, worldY));
                return true;
            }

            if (TryGetNestedPoint(element, "ImagePoint", out var imagePoint) &&
                TryGetNestedPoint(element, "WorldPoint", out var worldPoint))
            {
                pair = new PointPair(imagePoint, worldPoint);
                return true;
            }

            if (TryGetNumberProperty(element, "PixelX", out imageX) &&
                TryGetNumberProperty(element, "PixelY", out imageY) &&
                TryGetNumberProperty(element, "PhysicalX", out worldX) &&
                TryGetNumberProperty(element, "PhysicalY", out worldY))
            {
                pair = new PointPair(new Position(imageX, imageY), new Position(worldX, worldY));
                return true;
            }

            return false;
        }

        if (raw is IDictionary<string, object> dict)
        {
            if (TryGetDouble(dict, "ImageX", out var imageX) &&
                TryGetDouble(dict, "ImageY", out var imageY) &&
                TryGetDouble(dict, "WorldX", out var worldX) &&
                TryGetDouble(dict, "WorldY", out var worldY))
            {
                pair = new PointPair(new Position(imageX, imageY), new Position(worldX, worldY));
                return true;
            }

            if (dict.TryGetValue("ImagePoint", out var imagePointObj) &&
                dict.TryGetValue("WorldPoint", out var worldPointObj) &&
                TryParsePoint(imagePointObj, out var imagePoint) &&
                TryParsePoint(worldPointObj, out var worldPoint))
            {
                pair = new PointPair(imagePoint, worldPoint);
                return true;
            }

            return false;
        }

        if (raw is IDictionary legacy)
        {
            var normalized = legacy.Cast<DictionaryEntry>()
                .Where(entry => entry.Key != null)
                .ToDictionary(
                    entry => entry.Key!.ToString() ?? string.Empty,
                    entry => entry.Value ?? 0.0,
                    StringComparer.OrdinalIgnoreCase);
            return TryParsePointPair(normalized, out pair);
        }

        return false;
    }

    private static bool TryGetNumberProperty(JsonElement obj, string propertyName, out double value)
    {
        value = 0;
        foreach (var property in obj.EnumerateObject())
        {
            if (!property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
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

    private static bool TryGetNestedPoint(JsonElement parent, string name, out Position point)
    {
        point = new Position(0, 0);
        foreach (var property in parent.EnumerateObject())
        {
            if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return TryParsePoint(property.Value, out point);
        }

        return false;
    }

    private static bool TryParsePoint(object? raw, out Position point)
    {
        point = new Position(0, 0);
        if (raw == null)
        {
            return false;
        }

        if (raw is Position position)
        {
            point = position;
            return true;
        }

        if (raw is JsonElement element)
        {
            if (TryGetNumberProperty(element, "X", out var x) && TryGetNumberProperty(element, "Y", out var y))
            {
                point = new Position(x, y);
                return true;
            }

            return false;
        }

        if (raw is IDictionary<string, object> dict &&
            TryGetDouble(dict, "X", out var dx) &&
            TryGetDouble(dict, "Y", out var dy))
        {
            point = new Position(dx, dy);
            return true;
        }

        if (raw is IDictionary legacy)
        {
            var normalized = legacy.Cast<DictionaryEntry>()
                .Where(entry => entry.Key != null)
                .ToDictionary(
                    entry => entry.Key!.ToString() ?? string.Empty,
                    entry => entry.Value ?? 0.0,
                    StringComparer.OrdinalIgnoreCase);
            return TryParsePoint(normalized, out point);
        }

        return false;
    }

    private static bool TryGetDouble(IDictionary<string, object> dict, string key, out double value)
    {
        value = 0;
        if (!dict.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        return raw switch
        {
            double d => (value = d) == d,
            float f => (value = f) == f,
            int i => (value = i) == i,
            long l => (value = l) == l,
            _ => double.TryParse(raw.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value)
        };
    }

    private static double[][] ToMatrixArray(Mat matrix, int rows, int cols)
    {
        var result = new double[rows][];
        for (var r = 0; r < rows; r++)
        {
            result[r] = new double[cols];
            for (var c = 0; c < cols; c++)
            {
                result[r][c] = matrix.At<double>(r, c);
            }
        }

        return result;
    }

    private static double GetMaxTriangleArea(IReadOnlyList<Point2d> points)
    {
        var maxArea = 0.0;
        for (var i = 0; i < points.Count - 2; i++)
        {
            for (var j = i + 1; j < points.Count - 1; j++)
            {
                for (var k = j + 1; k < points.Count; k++)
                {
                    var area = Math.Abs(
                        points[i].X * (points[j].Y - points[k].Y) +
                        points[j].X * (points[k].Y - points[i].Y) +
                        points[k].X * (points[i].Y - points[j].Y)) * 0.5;
                    if (area > maxArea)
                    {
                        maxArea = area;
                    }
                }
            }
        }

        return maxArea;
    }

    private static void DrawCalibrationPoints(Mat image, IReadOnlyList<PointPair> pointPairs)
    {
        for (var i = 0; i < pointPairs.Count; i++)
        {
            var x = (int)Math.Round(pointPairs[i].ImagePoint.X);
            var y = (int)Math.Round(pointPairs[i].ImagePoint.Y);
            Cv2.Circle(image, new Point(x, y), 4, new Scalar(0, 255, 0), -1);
            Cv2.PutText(
                image,
                (i + 1).ToString(CultureInfo.InvariantCulture),
                new Point(x + 6, y - 6),
                HersheyFonts.HersheySimplex,
                0.5,
                new Scalar(0, 255, 255),
                1);
        }
    }

    private readonly record struct PointPair(Position ImagePoint, Position WorldPoint);

    private readonly record struct ReprojectionErrorStats(double MeanError, double MaxError, int InlierCount, double InlierRatio);
}
