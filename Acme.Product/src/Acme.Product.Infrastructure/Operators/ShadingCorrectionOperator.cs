using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

public class ShadingCorrectionOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ShadingCorrection;

    public ShadingCorrectionOperator(ILogger<ShadingCorrectionOperator> logger) : base(logger)
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

        var method = GetStringParam(@operator, "Method", "GaussianModel");
        var kernelSize = ToOdd(GetIntParam(@operator, "KernelSize", 51, 3, 501));

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid"));
        }

        using var gray = new Mat();
        if (src.Channels() == 1)
        {
            src.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }

        var correctedGray = method.ToLowerInvariant() switch
        {
            "dividebybackground" => CorrectByBackground(gray, inputs),
            "gaussianmodel" => CorrectByGaussianModel(gray, kernelSize),
            "morphologicaltophat" => CorrectByTopHat(gray, kernelSize),
            _ => throw new InvalidOperationException("Unsupported shading correction method")
        };

        if (correctedGray.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Failed to perform shading correction"));
        }

        Mat result;
        if (src.Channels() == 1)
        {
            result = correctedGray;
        }
        else
        {
            result = new Mat();
            Cv2.CvtColor(correctedGray, result, ColorConversionCodes.GRAY2BGR);
            correctedGray.Dispose();
        }

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(result)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var method = GetStringParam(@operator, "Method", "GaussianModel");
        var validMethods = new[] { "DivideByBackground", "GaussianModel", "MorphologicalTopHat" };
        if (!validMethods.Contains(method, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Method must be DivideByBackground, GaussianModel or MorphologicalTopHat");
        }

        var kernel = GetIntParam(@operator, "KernelSize", 51);
        if (kernel < 3)
        {
            return ValidationResult.Invalid("KernelSize must be >= 3");
        }

        return ValidationResult.Valid();
    }

    private Mat CorrectByBackground(Mat gray, Dictionary<string, object>? inputs)
    {
        if (!TryGetInputImage(inputs, "Background", out var backgroundWrapper) || backgroundWrapper == null)
        {
            throw new InvalidOperationException("Background input is required for DivideByBackground mode");
        }

        var background = backgroundWrapper.GetMat();
        if (background.Empty())
        {
            throw new InvalidOperationException("Background image is invalid");
        }

        using var bgGray = new Mat();
        if (background.Channels() == 1)
        {
            background.CopyTo(bgGray);
        }
        else
        {
            Cv2.CvtColor(background, bgGray, ColorConversionCodes.BGR2GRAY);
        }

        using var resizedBg = new Mat();
        if (bgGray.Size() != gray.Size())
        {
            Cv2.Resize(bgGray, resizedBg, gray.Size());
        }
        else
        {
            bgGray.CopyTo(resizedBg);
        }

        using var src32 = new Mat();
        using var bg32 = new Mat();
        gray.ConvertTo(src32, MatType.CV_32FC1);
        resizedBg.ConvertTo(bg32, MatType.CV_32FC1);

        using var eps = new Mat(bg32.Size(), bg32.Type(), new Scalar(1.0));
        using var denom = new Mat();
        Cv2.Add(bg32, eps, denom);

        using var corrected32 = new Mat();
        Cv2.Divide(src32, denom, corrected32, 255.0);
        Cv2.Normalize(corrected32, corrected32, 0, 255, NormTypes.MinMax);

        var result = new Mat();
        corrected32.ConvertTo(result, MatType.CV_8UC1);
        return result;
    }

    private static Mat CorrectByGaussianModel(Mat gray, int kernelSize)
    {
        using var background = new Mat();
        Cv2.GaussianBlur(gray, background, new Size(kernelSize, kernelSize), 0);

        using var src32 = new Mat();
        using var bg32 = new Mat();
        gray.ConvertTo(src32, MatType.CV_32FC1);
        background.ConvertTo(bg32, MatType.CV_32FC1);

        using var eps = new Mat(bg32.Size(), bg32.Type(), new Scalar(1.0));
        using var denom = new Mat();
        Cv2.Add(bg32, eps, denom);

        using var corrected32 = new Mat();
        Cv2.Divide(src32, denom, corrected32, 128.0);
        Cv2.Normalize(corrected32, corrected32, 0, 255, NormTypes.MinMax);

        var result = new Mat();
        corrected32.ConvertTo(result, MatType.CV_8UC1);
        return result;
    }

    private static Mat CorrectByTopHat(Mat gray, int kernelSize)
    {
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(kernelSize, kernelSize));
        var result = new Mat();
        Cv2.MorphologyEx(gray, result, MorphTypes.TopHat, kernel);
        return result;
    }

    private static int ToOdd(int value)
    {
        return value % 2 == 0 ? value + 1 : value;
    }
}
