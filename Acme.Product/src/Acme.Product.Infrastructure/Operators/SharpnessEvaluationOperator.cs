// SharpnessEvaluationOperator.cs
// 清晰度评估算子
// 使用多种指标评估图像清晰度质量
// 作者：蘅芜君
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// Evaluates image sharpness by multiple focus measures.
/// </summary>
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
[OperatorParam("Threshold", "Threshold", "double", DefaultValue = 100.0, Min = 0.0)]
[OperatorParam("RoiX", "ROI X", "int", DefaultValue = 0)]
[OperatorParam("RoiY", "ROI Y", "int", DefaultValue = 0)]
[OperatorParam("RoiW", "ROI Width", "int", DefaultValue = 0)]
[OperatorParam("RoiH", "ROI Height", "int", DefaultValue = 0)]
public class SharpnessEvaluationOperator : OperatorBase
{
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

        var method = GetStringParam(@operator, "Method", "Laplacian");
        var threshold = GetDoubleParam(@operator, "Threshold", 100.0, 0);
        var roi = BuildRoi(@operator, src.Width, src.Height);

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
        var score = method.ToLowerInvariant() switch
        {
            "laplacian" => ComputeLaplacianVariance(roiGray),
            "brenner" => ComputeBrenner(roiGray),
            "tenengrad" => ComputeTenengrad(roiGray),
            "smd" => ComputeSmd(roiGray),
            _ => ComputeLaplacianVariance(roiGray)
        };

        var isSharp = score >= threshold;

        var resultImage = src.Clone();
        Cv2.Rectangle(resultImage, roi, new Scalar(0, 255, 255), 1);
        Cv2.PutText(resultImage, $"Sharpness[{method}]={score:F2}", new Point(8, 24), HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 255, 255), 2);
        Cv2.PutText(resultImage, isSharp ? "Sharp" : "Blur", new Point(8, 48), HersheyFonts.HersheySimplex, 0.7, isSharp ? new Scalar(0, 255, 0) : new Scalar(0, 0, 255), 2);

        var output = CreateImageOutput(resultImage, new Dictionary<string, object>
        {
            { "Score", score },
            { "IsSharp", isSharp },
            { "Method", method },
            { "StatusCode", "OK" },
            { "StatusMessage", "Success" },
            { "Confidence", 1.0 },
            { "UncertaintyPx", 0.0 }
        });

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var method = GetStringParam(@operator, "Method", "Laplacian");
        var valid = new[] { "Laplacian", "Brenner", "Tenengrad", "SMD" };
        if (!valid.Contains(method, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Method must be Laplacian/Brenner/Tenengrad/SMD");
        }

        var threshold = GetDoubleParam(@operator, "Threshold", 100);
        if (threshold < 0)
        {
            return ValidationResult.Invalid("Threshold must be >= 0");
        }

        return ValidationResult.Valid();
    }

    private static Rect BuildRoi(Operator @operator, int width, int height)
    {
        var x = Math.Clamp(GetParamAsInt(@operator, "RoiX", 0), 0, Math.Max(0, width - 1));
        var y = Math.Clamp(GetParamAsInt(@operator, "RoiY", 0), 0, Math.Max(0, height - 1));
        var w = GetParamAsInt(@operator, "RoiW", 0);
        var h = GetParamAsInt(@operator, "RoiH", 0);

        if (w <= 0 || h <= 0)
        {
            return new Rect(0, 0, width, height);
        }

        w = Math.Clamp(w, 1, width - x);
        h = Math.Clamp(h, 1, height - y);
        return new Rect(x, y, w, h);
    }

    private static int GetParamAsInt(Operator op, string name, int fallback)
    {
        var p = op.Parameters.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (p?.Value == null)
        {
            return fallback;
        }

        return int.TryParse(p.Value.ToString(), out var value) ? value : fallback;
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
                sum += gx * gx + gy * gy;
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
}
