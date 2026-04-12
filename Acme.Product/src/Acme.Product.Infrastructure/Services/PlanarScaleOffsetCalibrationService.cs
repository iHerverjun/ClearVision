using System.Text.Json;
using Acme.Product.Infrastructure.Calibration;

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// 二维平面比例/偏移标定点。
/// </summary>
public class PlanarScaleOffsetCalibrationPoint
{
    public double PixelX { get; set; }

    public double PixelY { get; set; }

    public double PhysicalX { get; set; }

    public double PhysicalY { get; set; }
}

/// <summary>
/// 二维平面比例/偏移标定结果。
/// </summary>
public class PlanarScaleOffsetCalibrationResult
{
    private const double AcceptedRmseThreshold = 0.15;
    private const double AcceptedAxisErrorThreshold = 0.10;

    public bool Success { get; set; }

    public bool Accepted { get; set; }

    public string Message { get; set; } = string.Empty;

    public double OriginX { get; set; }

    public double OriginY { get; set; }

    public double ScaleX { get; set; }

    public double ScaleY { get; set; }

    public double MeanErrorX { get; set; }

    public double MeanErrorY { get; set; }

    public double Rmse { get; set; }

    public int PointCount { get; set; }

    public bool MeetsAcceptanceCriteria()
    {
        return Success &&
               PointCount >= 3 &&
               Rmse <= AcceptedRmseThreshold &&
               MeanErrorX <= AcceptedAxisErrorThreshold &&
               MeanErrorY <= AcceptedAxisErrorThreshold;
    }
}

public interface IPlanarScaleOffsetCalibrationService
{
    Task<PlanarScaleOffsetCalibrationResult> SolveAsync(List<PlanarScaleOffsetCalibrationPoint> points);

    Task<bool> SaveCalibrationAsync(PlanarScaleOffsetCalibrationResult result, string fileName);
}

/// <summary>
/// 二维平面比例/偏移标定服务。
/// </summary>
public class PlanarScaleOffsetCalibrationService : IPlanarScaleOffsetCalibrationService
{
    public Task<PlanarScaleOffsetCalibrationResult> SolveAsync(List<PlanarScaleOffsetCalibrationPoint> points)
    {
        if (points == null || points.Count < 2)
        {
            return Task.FromResult(new PlanarScaleOffsetCalibrationResult
            {
                Success = false,
                Message = "Point count is insufficient. At least 2 non-overlapping points are required."
            });
        }

        var count = points.Count;

        var sumPx = points.Sum(p => p.PixelX);
        var sumPy = points.Sum(p => p.PixelY);
        var sumPhysicalX = points.Sum(p => p.PhysicalX);
        var sumPhysicalY = points.Sum(p => p.PhysicalY);
        var sumPxPhysicalX = points.Sum(p => p.PixelX * p.PhysicalX);
        var sumPyPhysicalY = points.Sum(p => p.PixelY * p.PhysicalY);
        var sumPxSquared = points.Sum(p => p.PixelX * p.PixelX);
        var sumPySquared = points.Sum(p => p.PixelY * p.PixelY);

        var denominatorX = count * sumPxSquared - sumPx * sumPx;
        var denominatorY = count * sumPySquared - sumPy * sumPy;

        if (Math.Abs(denominatorX) < 1e-10 || Math.Abs(denominatorY) < 1e-10)
        {
            return Task.FromResult(new PlanarScaleOffsetCalibrationResult
            {
                Success = false,
                Message = "Input points are degenerate. Use more spatially distributed points."
            });
        }

        var scaleX = (count * sumPxPhysicalX - sumPx * sumPhysicalX) / denominatorX;
        var originX = (sumPhysicalX - scaleX * sumPx) / count;
        var scaleY = (count * sumPyPhysicalY - sumPy * sumPhysicalY) / denominatorY;
        var originY = (sumPhysicalY - scaleY * sumPy) / count;

        var errorSquaredSum = 0.0;
        var meanErrorXSum = 0.0;
        var meanErrorYSum = 0.0;

        foreach (var point in points)
        {
            var fittedPhysicalX = scaleX * point.PixelX + originX;
            var fittedPhysicalY = scaleY * point.PixelY + originY;
            var errorX = Math.Abs(fittedPhysicalX - point.PhysicalX);
            var errorY = Math.Abs(fittedPhysicalY - point.PhysicalY);

            meanErrorXSum += errorX;
            meanErrorYSum += errorY;
            errorSquaredSum += errorX * errorX + errorY * errorY;
        }

        var rawResult = new PlanarScaleOffsetCalibrationResult
        {
            Success = true,
            OriginX = originX,
            OriginY = originY,
            ScaleX = scaleX,
            ScaleY = scaleY,
            MeanErrorX = meanErrorXSum / count,
            MeanErrorY = meanErrorYSum / count,
            Rmse = Math.Sqrt(errorSquaredSum / count),
            PointCount = count
        };

        return Task.FromResult(new PlanarScaleOffsetCalibrationResult
        {
            Success = rawResult.Success,
            Accepted = rawResult.MeetsAcceptanceCriteria(),
            Message = rawResult.MeetsAcceptanceCriteria()
                ? "Solve succeeded and passed planar scale-offset acceptance."
                : "Solve succeeded but did not pass planar scale-offset acceptance.",
            OriginX = rawResult.OriginX,
            OriginY = rawResult.OriginY,
            ScaleX = rawResult.ScaleX,
            ScaleY = rawResult.ScaleY,
            MeanErrorX = rawResult.MeanErrorX,
            MeanErrorY = rawResult.MeanErrorY,
            Rmse = rawResult.Rmse,
            PointCount = rawResult.PointCount
        });
    }

    public async Task<bool> SaveCalibrationAsync(PlanarScaleOffsetCalibrationResult result, string fileName)
    {
        var accepted = result.MeetsAcceptanceCriteria();
        if (!result.Success || !accepted)
        {
            return false;
        }

        try
        {
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClearVision");
            Directory.CreateDirectory(appData);

            if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".json";
            }

            var fullPath = Path.IsPathRooted(fileName) ? fileName : Path.Combine(appData, fileName);
            var directoryPath = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var bundle = new CalibrationBundleV2
            {
                CalibrationKind = CalibrationKindV2.PlanarTransform2D,
                TransformModel = TransformModelV2.ScaleOffset,
                SourceFrame = "image",
                TargetFrame = "world",
                Unit = "mm",
                Transform2D = new CalibrationTransform2DV2
                {
                    Model = TransformModelV2.ScaleOffset,
                    Matrix = new[]
                    {
                        new[] { result.ScaleX, 0d, result.OriginX },
                        new[] { 0d, result.ScaleY, result.OriginY }
                    },
                    PixelSizeX = Math.Abs(result.ScaleX),
                    PixelSizeY = Math.Abs(result.ScaleY)
                },
                Quality = new CalibrationQualityV2
                {
                    Accepted = accepted,
                    MeanError = result.Rmse,
                    MaxError = Math.Max(result.MeanErrorX, result.MeanErrorY),
                    InlierCount = result.PointCount,
                    TotalSampleCount = result.PointCount,
                    Diagnostics = new List<string>
                    {
                        "Desktop planar scale-offset save path produced a CalibrationBundleV2 payload.",
                        "This bundle is intended for CoordinateTransform and PixelToWorldTransform planar consumption."
                    }
                },
                ProducerOperator = nameof(PlanarScaleOffsetCalibrationService)
            };

            var json = CalibrationBundleV2Json.Serialize(bundle);
            await File.WriteAllTextAsync(fullPath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
