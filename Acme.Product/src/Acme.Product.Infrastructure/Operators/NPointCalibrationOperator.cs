using System.Collections;
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
    DisplayName = "N点标定",
    Description = "Builds affine or perspective calibration from user point pairs.",
    Category = "标定",
    IconName = "n-point",
    Keywords = new[] { "n-point", "affine", "perspective", "calibration" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = false)]
[OutputPort("TransformMatrix", "Transform Matrix", PortDataType.Any)]
[OutputPort("PixelSize", "Pixel Size", PortDataType.Float)]
[OutputPort("ReprojectionError", "Reprojection Error", PortDataType.Float)]
[OperatorParam("CalibrationMode", "Calibration Mode", "enum", DefaultValue = "Affine", Options = new[] { "Affine|Affine", "Perspective|Perspective" })]
[OperatorParam("PointPairs", "Point Pairs", "string", DefaultValue = "")]
[OperatorParam("SavePath", "Save Path", "file", DefaultValue = "")]
public class NPointCalibrationOperator : OperatorBase
{
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
            return Task.FromResult(OperatorExecutionOutput.Failure("PointPairs is required and must be valid JSON or list data"));
        }

        var requiredCount = mode.Equals("Perspective", StringComparison.OrdinalIgnoreCase) ? 4 : 3;
        if (pointPairs.Count < requiredCount)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"{mode} mode requires at least {requiredCount} point pairs"));
        }

        var srcPoints = pointPairs.Select(p => new Point2f((float)p.ImagePoint.X, (float)p.ImagePoint.Y)).ToArray();
        var dstPoints = pointPairs.Select(p => new Point2f((float)p.WorldPoint.X, (float)p.WorldPoint.Y)).ToArray();

        double[][] transformMatrix;
        double reprojectionError;

        if (mode.Equals("Perspective", StringComparison.OrdinalIgnoreCase))
        {
            using var perspectiveMatrix = Cv2.GetPerspectiveTransform(srcPoints.Take(4).ToArray(), dstPoints.Take(4).ToArray());
            transformMatrix = ToMatrixArray(perspectiveMatrix, 3, 3);
            reprojectionError = CalculatePerspectiveReprojectionError(pointPairs, perspectiveMatrix);
        }
        else
        {
            using var affineMatrix = Cv2.GetAffineTransform(srcPoints.Take(3).ToArray(), dstPoints.Take(3).ToArray());
            transformMatrix = ToMatrixArray(affineMatrix, 2, 3);
            reprojectionError = CalculateAffineReprojectionError(pointPairs, affineMatrix);
        }

        var pixelSize = EstimatePixelSize(pointPairs);

        var savePath = GetStringParam(@operator, "SavePath", string.Empty);
        if (!string.IsNullOrWhiteSpace(savePath))
        {
            TrySaveCalibration(savePath, mode, transformMatrix, pixelSize, reprojectionError, pointPairs);
        }

        var resultData = new Dictionary<string, object>
        {
            { "TransformMatrix", transformMatrix },
            { "PixelSize", pixelSize },
            { "ReprojectionError", reprojectionError }
        };

        if (TryGetInputImage(inputs, out var imageWrapper) && imageWrapper != null)
        {
            var src = imageWrapper.GetMat();
            if (!src.Empty())
            {
                var resultImage = src.Clone();
                DrawCalibrationPoints(resultImage, pointPairs);
                return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, resultData)));
            }
        }

        return Task.FromResult(OperatorExecutionOutput.Success(resultData));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var mode = GetStringParam(@operator, "CalibrationMode", "Affine");
        var validModes = new[] { "Affine", "Perspective" };
        if (!validModes.Contains(mode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("CalibrationMode must be Affine or Perspective");
        }

        var pointPairsRaw = ResolvePointPairsRaw(@operator, null);
        if (!string.IsNullOrWhiteSpace(pointPairsRaw) && !TryParsePointPairs(pointPairsRaw, out var parsedPairs))
        {
            return ValidationResult.Invalid("PointPairs is not valid JSON/list format");
        }

        var requiredCount = mode.Equals("Perspective", StringComparison.OrdinalIgnoreCase) ? 4 : 3;
        if (!string.IsNullOrWhiteSpace(pointPairsRaw) && TryParsePointPairs(pointPairsRaw, out parsedPairs) && parsedPairs.Count < requiredCount)
        {
            return ValidationResult.Invalid($"{mode} mode requires at least {requiredCount} point pairs");
        }

        return ValidationResult.Valid();
    }

    private static string ResolvePointPairsRaw(Operator @operator, Dictionary<string, object>? inputs)
    {
        if (inputs != null && inputs.TryGetValue("PointPairs", out var pairObj) && pairObj != null)
        {
            return pairObj.ToString() ?? string.Empty;
        }

        return @operator.Parameters.FirstOrDefault(p => p.Name.Equals("PointPairs", StringComparison.OrdinalIgnoreCase))?.Value?.ToString() ?? string.Empty;
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
                .Where(e => e.Key != null)
                .ToDictionary(e => e.Key!.ToString() ?? string.Empty, e => e.Value ?? 0.0, StringComparer.OrdinalIgnoreCase);
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

        if (raw is Position p)
        {
            point = p;
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
                .Where(e => e.Key != null)
                .ToDictionary(e => e.Key!.ToString() ?? string.Empty, e => e.Value ?? 0.0, StringComparer.OrdinalIgnoreCase);
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

    private static double CalculateAffineReprojectionError(IReadOnlyList<PointPair> pairs, Mat affineMatrix)
    {
        var sumSquared = 0.0;
        for (var i = 0; i < pairs.Count; i++)
        {
            var x = pairs[i].ImagePoint.X;
            var y = pairs[i].ImagePoint.Y;

            var predictedX = affineMatrix.At<double>(0, 0) * x + affineMatrix.At<double>(0, 1) * y + affineMatrix.At<double>(0, 2);
            var predictedY = affineMatrix.At<double>(1, 0) * x + affineMatrix.At<double>(1, 1) * y + affineMatrix.At<double>(1, 2);

            var dx = predictedX - pairs[i].WorldPoint.X;
            var dy = predictedY - pairs[i].WorldPoint.Y;
            sumSquared += dx * dx + dy * dy;
        }

        return Math.Sqrt(sumSquared / Math.Max(1, pairs.Count));
    }

    private static double CalculatePerspectiveReprojectionError(IReadOnlyList<PointPair> pairs, Mat perspectiveMatrix)
    {
        var sumSquared = 0.0;
        for (var i = 0; i < pairs.Count; i++)
        {
            var x = pairs[i].ImagePoint.X;
            var y = pairs[i].ImagePoint.Y;

            var w = perspectiveMatrix.At<double>(2, 0) * x + perspectiveMatrix.At<double>(2, 1) * y + perspectiveMatrix.At<double>(2, 2);
            if (Math.Abs(w) < 1e-9)
            {
                continue;
            }

            var predictedX = (perspectiveMatrix.At<double>(0, 0) * x + perspectiveMatrix.At<double>(0, 1) * y + perspectiveMatrix.At<double>(0, 2)) / w;
            var predictedY = (perspectiveMatrix.At<double>(1, 0) * x + perspectiveMatrix.At<double>(1, 1) * y + perspectiveMatrix.At<double>(1, 2)) / w;

            var dx = predictedX - pairs[i].WorldPoint.X;
            var dy = predictedY - pairs[i].WorldPoint.Y;
            sumSquared += dx * dx + dy * dy;
        }

        return Math.Sqrt(sumSquared / Math.Max(1, pairs.Count));
    }

    private static double EstimatePixelSize(IReadOnlyList<PointPair> pairs)
    {
        var ratios = new List<double>();

        for (var i = 0; i < pairs.Count; i++)
        {
            for (var j = i + 1; j < pairs.Count; j++)
            {
                var imageDist = Math.Sqrt(Math.Pow(pairs[i].ImagePoint.X - pairs[j].ImagePoint.X, 2) + Math.Pow(pairs[i].ImagePoint.Y - pairs[j].ImagePoint.Y, 2));
                var worldDist = Math.Sqrt(Math.Pow(pairs[i].WorldPoint.X - pairs[j].WorldPoint.X, 2) + Math.Pow(pairs[i].WorldPoint.Y - pairs[j].WorldPoint.Y, 2));

                if (imageDist > 1e-9 && worldDist > 1e-9)
                {
                    ratios.Add(worldDist / imageDist);
                }
            }
        }

        return ratios.Count > 0 ? ratios.Average() : 1.0;
    }

    private void TrySaveCalibration(
        string savePath,
        string mode,
        double[][] matrix,
        double pixelSize,
        double reprojectionError,
        IReadOnlyList<PointPair> pairs)
    {
        try
        {
            var directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var payload = new Dictionary<string, object>
            {
                { "CalibrationMode", mode },
                { "TransformMatrix", matrix },
                { "PixelSize", pixelSize },
                { "ReprojectionError", reprojectionError },
                { "PointPairs", pairs.Select(p => new
                    {
                        ImagePoint = new { p.ImagePoint.X, p.ImagePoint.Y },
                        WorldPoint = new { p.WorldPoint.X, p.WorldPoint.Y }
                    }).ToList()
                },
                { "Timestamp", DateTime.UtcNow }
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(savePath, json);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to save calibration data to {Path}", savePath);
        }
    }

    private static void DrawCalibrationPoints(Mat image, IReadOnlyList<PointPair> pointPairs)
    {
        for (var i = 0; i < pointPairs.Count; i++)
        {
            var x = (int)Math.Round(pointPairs[i].ImagePoint.X);
            var y = (int)Math.Round(pointPairs[i].ImagePoint.Y);
            Cv2.Circle(image, new Point(x, y), 4, new Scalar(0, 255, 0), -1);
            Cv2.PutText(image, (i + 1).ToString(), new Point(x + 6, y - 6), HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 255, 255), 1);
        }
    }

    private readonly record struct PointPair(Position ImagePoint, Position WorldPoint);
}
