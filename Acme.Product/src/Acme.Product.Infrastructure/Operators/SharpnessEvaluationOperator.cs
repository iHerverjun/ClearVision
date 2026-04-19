using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "清晰度评估",
    Description = "Evaluates focus quality of an image.",
    Category = "检测",
    IconName = "focus",
    Keywords = new[] { "sharpness", "focus", "blur", "laplacian", "tenengrad" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[OutputPort("Score", "Score", PortDataType.Float)]
[OutputPort("IsSharp", "Is Sharp", PortDataType.Boolean)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OperatorParam("Method", "Method", "enum", DefaultValue = "Laplacian", Options = new[] { "Laplacian|Laplacian", "Brenner|Brenner", "Tenengrad|Tenengrad", "SMD|SMD" })]
[OperatorParam("ThresholdMode", "Threshold Mode", "enum", DefaultValue = "PerMethodDefault", Options = new[] { "PerMethodDefault|PerMethodDefault", "Manual|Manual" })]
[OperatorParam("Threshold", "Threshold", "double", DefaultValue = 100.0, Min = 0.0)]
[OperatorParam("RoiX", "ROI X", "int", DefaultValue = 0)]
[OperatorParam("RoiY", "ROI Y", "int", DefaultValue = 0)]
[OperatorParam("RoiW", "ROI Width", "int", DefaultValue = 0)]
[OperatorParam("RoiH", "ROI Height", "int", DefaultValue = 0)]
public class SharpnessEvaluationOperator : OperatorBase
{
    private static readonly IReadOnlyDictionary<string, double> DefaultThresholds =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["Laplacian"] = 100.0,
            ["Brenner"] = 30.0,
            ["Tenengrad"] = 800.0,
            ["SMD"] = 10.0
        };

    public override OperatorType OperatorType => OperatorType.SharpnessEvaluation;

    public SharpnessEvaluationOperator(ILogger<SharpnessEvaluationOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is required"));
        }

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid"));
        }

        var method = ResolveMethod(GetStringParam(@operator, "Method", "Laplacian"));
        if (method == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Method must be Laplacian/Brenner/Tenengrad/SMD"));
        }

        var thresholdMode = GetStringParam(@operator, "ThresholdMode", "PerMethodDefault");
        var thresholdUsed = ResolveThreshold(@operator, method, thresholdMode);
        if (double.IsNaN(thresholdUsed))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("ThresholdMode must be PerMethodDefault or Manual"));
        }

        var roi = MeasurementRoiHelper.ResolveRoi(@operator, src.Width, src.Height);
        using var gray = new Mat();
        if (src.Channels() == 1)
        {
            src.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }

        using var roiGray = new Mat(gray, roi);
        var score = method switch
        {
            "Laplacian" => ComputeLaplacianVariance(roiGray),
            "Brenner" => ComputeBrenner(roiGray),
            "Tenengrad" => ComputeTenengrad(roiGray),
            "SMD" => ComputeSmd(roiGray),
            _ => ComputeLaplacianVariance(roiGray)
        };
        var tileScores = ComputeTileScores(roiGray, method);
        var tileStdDev = tileScores.Count > 0
            ? MeasurementStatisticsHelper.ComputePopulationStdDev(tileScores, tileScores.Average())
            : 0.0;
        var tileStdError = MeasurementStatisticsHelper.ComputeStandardError(tileStdDev, tileScores.Count);

        var decisionReady = DefaultThresholds.ContainsKey(method);
        var isSharp = score >= thresholdUsed;
        var marginToThreshold = score - thresholdUsed;
        var normalizedScore = thresholdUsed > 1e-9 ? score / thresholdUsed : double.NaN;

        var resultImage = src.Clone();
        Cv2.Rectangle(resultImage, roi, new Scalar(0, 255, 255), 1);
        Cv2.PutText(resultImage, $"Sharpness[{method}]={score:F2}", new Point(8, 24), HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 255, 255), 2);
        Cv2.PutText(resultImage, isSharp ? "Sharp" : "Blur", new Point(8, 48), HersheyFonts.HersheySimplex, 0.7, isSharp ? new Scalar(0, 255, 0) : new Scalar(0, 0, 255), 2);

        var output = CreateImageOutput(resultImage, new Dictionary<string, object>
        {
            { "Score", score },
            { "IsSharp", isSharp },
            { "Method", method },
            { "ThresholdMode", thresholdMode },
            { "ThresholdUsed", thresholdUsed },
            { "DecisionReady", decisionReady },
            { "NormalizedScore", normalizedScore },
            { "MarginToThreshold", marginToThreshold },
            { "TileCount", tileScores.Count },
            { "ScoreStdDev", tileStdDev },
            { "ScoreStdError", tileStdError },
            { "StatusCode", "OK" },
            { "StatusMessage", "Success" },
            { "Confidence", MeasurementStatisticsHelper.ComputeConfidenceFromUncertainty(tileStdError) },
            { "UncertaintyPx", tileStdError }
        });

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var method = ResolveMethod(GetStringParam(@operator, "Method", "Laplacian"));
        if (method == null)
        {
            return ValidationResult.Invalid("Method must be Laplacian/Brenner/Tenengrad/SMD");
        }

        var thresholdMode = GetStringParam(@operator, "ThresholdMode", "PerMethodDefault");
        if (!thresholdMode.Equals("PerMethodDefault", StringComparison.OrdinalIgnoreCase) &&
            !thresholdMode.Equals("Manual", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("ThresholdMode must be PerMethodDefault or Manual");
        }

        var threshold = GetDoubleParam(@operator, "Threshold", 100);
        if (threshold < 0)
        {
            return ValidationResult.Invalid("Threshold must be >= 0");
        }

        return ValidationResult.Valid();
    }

    private static string? ResolveMethod(string raw)
    {
        return raw switch
        {
            "Laplacian" => "Laplacian",
            "Brenner" => "Brenner",
            "Tenengrad" => "Tenengrad",
            "SMD" => "SMD",
            _ => null
        };
    }

    private double ResolveThreshold(Operator @operator, string method, string thresholdMode)
    {
        if (thresholdMode.Equals("Manual", StringComparison.OrdinalIgnoreCase))
        {
            return GetDoubleParam(@operator, "Threshold", 100.0, 0);
        }

        if (thresholdMode.Equals("PerMethodDefault", StringComparison.OrdinalIgnoreCase) &&
            DefaultThresholds.TryGetValue(method, out var defaultThreshold))
        {
            return defaultThreshold;
        }

        return double.NaN;
    }

    private static double ComputeLaplacianVariance(Mat gray)
    {
        using var lap = new Mat();
        Cv2.Laplacian(gray, lap, MatType.CV_64F);
        Cv2.MeanStdDev(lap, out _, out var stddev);
        return stddev[0] * stddev[0];
    }

    private static double ComputeBrenner(Mat gray)
    {
        var sum = 0.0;
        var idx = gray.GetGenericIndexer<byte>();
        for (var y = 0; y < gray.Rows; y++)
        {
            for (var x = 0; x < gray.Cols - 2; x++)
            {
                var diff = idx[y, x + 2] - idx[y, x];
                sum += diff * diff;
            }
        }

        return sum / Math.Max(1, gray.Rows * gray.Cols);
    }

    private static double ComputeTenengrad(Mat gray)
    {
        using var gradX = new Mat();
        using var gradY = new Mat();
        Cv2.Sobel(gray, gradX, MatType.CV_64F, 1, 0, 3);
        Cv2.Sobel(gray, gradY, MatType.CV_64F, 0, 1, 3);

        var idxX = gradX.GetGenericIndexer<double>();
        var idxY = gradY.GetGenericIndexer<double>();
        var sum = 0.0;

        for (var y = 0; y < gray.Rows; y++)
        {
            for (var x = 0; x < gray.Cols; x++)
            {
                var gx = idxX[y, x];
                var gy = idxY[y, x];
                sum += (gx * gx) + (gy * gy);
            }
        }

        return sum / Math.Max(1, gray.Rows * gray.Cols);
    }

    private static double ComputeSmd(Mat gray)
    {
        var idx = gray.GetGenericIndexer<byte>();
        var sum = 0.0;
        for (var y = 0; y < gray.Rows - 1; y++)
        {
            for (var x = 0; x < gray.Cols - 1; x++)
            {
                sum += Math.Abs(idx[y, x] - idx[y, x + 1]);
                sum += Math.Abs(idx[y, x] - idx[y + 1, x]);
            }
        }

        return sum / Math.Max(1, gray.Rows * gray.Cols);
    }

    private static List<double> ComputeTileScores(Mat gray, string method)
    {
        var tilesX = Math.Clamp(gray.Cols / 32, 1, 4);
        var tilesY = Math.Clamp(gray.Rows / 32, 1, 4);
        var tileScores = new List<double>(tilesX * tilesY);

        for (var tileY = 0; tileY < tilesY; tileY++)
        {
            var y0 = tileY * gray.Rows / tilesY;
            var y1 = (tileY + 1) * gray.Rows / tilesY;
            for (var tileX = 0; tileX < tilesX; tileX++)
            {
                var x0 = tileX * gray.Cols / tilesX;
                var x1 = (tileX + 1) * gray.Cols / tilesX;
                var rect = new Rect(x0, y0, Math.Max(1, x1 - x0), Math.Max(1, y1 - y0));

                using var tile = new Mat(gray, rect);
                var score = method switch
                {
                    "Laplacian" => ComputeLaplacianVariance(tile),
                    "Brenner" => ComputeBrenner(tile),
                    "Tenengrad" => ComputeTenengrad(tile),
                    "SMD" => ComputeSmd(tile),
                    _ => ComputeLaplacianVariance(tile)
                };

                if (double.IsFinite(score))
                {
                    tileScores.Add(score);
                }
            }
        }

        return tileScores;
    }
}
