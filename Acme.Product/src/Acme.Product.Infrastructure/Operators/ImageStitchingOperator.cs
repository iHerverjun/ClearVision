using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

public class ImageStitchingOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ImageStitching;

    public ImageStitchingOperator(ILogger<ImageStitchingOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, "Image1", out var image1) || image1 == null ||
            !TryGetInputImage(inputs, "Image2", out var image2) || image2 == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Image1 and Image2 are required"));
        }

        var src1 = image1.GetMat();
        var src2 = image2.GetMat();
        if (src1.Empty() || src2.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input images are invalid"));
        }

        var method = GetStringParam(@operator, "Method", "FeatureBased");
        var overlapPercent = GetDoubleParam(@operator, "OverlapPercent", 20.0, 0.0, 90.0);
        var blendMode = GetStringParam(@operator, "BlendMode", "Linear");

        Mat stitched;
        double overlapRatio;

        if (method.Equals("FeatureBased", StringComparison.OrdinalIgnoreCase) &&
            TryFeatureBasedStitch(src1, src2, out stitched, out overlapRatio))
        {
            // stitched by feature matches
        }
        else
        {
            stitched = ManualHorizontalStitch(src1, src2, overlapPercent, blendMode, out overlapRatio);
        }

        var output = new Dictionary<string, object>
        {
            { "OverlapRatio", overlapRatio }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(stitched, output)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var method = GetStringParam(@operator, "Method", "FeatureBased");
        var validMethods = new[] { "FeatureBased", "Manual" };
        if (!validMethods.Contains(method, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Method must be FeatureBased or Manual");
        }

        var overlap = GetDoubleParam(@operator, "OverlapPercent", 20.0);
        if (overlap < 0 || overlap >= 100)
        {
            return ValidationResult.Invalid("OverlapPercent must be in [0, 100)");
        }

        return ValidationResult.Valid();
    }

    private static bool TryFeatureBasedStitch(Mat src1, Mat src2, out Mat stitched, out double overlapRatio)
    {
        stitched = new Mat();
        overlapRatio = 0.0;

        using var gray1 = new Mat();
        using var gray2 = new Mat();
        if (src1.Channels() == 1)
        {
            src1.CopyTo(gray1);
        }
        else
        {
            Cv2.CvtColor(src1, gray1, ColorConversionCodes.BGR2GRAY);
        }

        if (src2.Channels() == 1)
        {
            src2.CopyTo(gray2);
        }
        else
        {
            Cv2.CvtColor(src2, gray2, ColorConversionCodes.BGR2GRAY);
        }

        using var orb = ORB.Create(1200);
        using var desc1 = new Mat();
        using var desc2 = new Mat();
        orb.DetectAndCompute(gray1, null, out var kp1, desc1);
        orb.DetectAndCompute(gray2, null, out var kp2, desc2);

        if (desc1.Empty() || desc2.Empty() || kp1.Length < 8 || kp2.Length < 8)
        {
            return false;
        }

        using var matcher = new BFMatcher(NormTypes.Hamming, false);
        var knn = matcher.KnnMatch(desc2, desc1, 2);

        var good = new List<DMatch>();
        foreach (var pair in knn)
        {
            if (pair.Length < 2)
            {
                continue;
            }

            if (pair[0].Distance < pair[1].Distance * 0.75f)
            {
                good.Add(pair[0]);
            }
        }

        if (good.Count < 8)
        {
            return false;
        }

        var srcPts = good.Select(m => kp2[m.QueryIdx].Pt).ToArray();
        var dstPts = good.Select(m => kp1[m.TrainIdx].Pt).ToArray();

        using var mask = new Mat();
        using var homography = Cv2.FindHomography(InputArray.Create(srcPts), InputArray.Create(dstPts), HomographyMethods.Ransac, 3, mask);
        if (homography.Empty())
        {
            return false;
        }

        var width = src1.Width + src2.Width;
        var height = Math.Max(src1.Height, src2.Height);

        stitched = new Mat(height, width, src1.Type(), Scalar.Black);
        using (var roi = new Mat(stitched, new Rect(0, 0, src1.Width, src1.Height)))
        {
            src1.CopyTo(roi);
        }

        using var warped = new Mat();
        Cv2.WarpPerspective(src2, warped, homography, new Size(width, height));

        using var grayWarped = new Mat();
        if (warped.Channels() == 1)
        {
            warped.CopyTo(grayWarped);
        }
        else
        {
            Cv2.CvtColor(warped, grayWarped, ColorConversionCodes.BGR2GRAY);
        }

        using var maskWarped = new Mat();
        Cv2.Threshold(grayWarped, maskWarped, 1, 255, ThresholdTypes.Binary);
        warped.CopyTo(stitched, maskWarped);

        var overlapPixels = Cv2.CountNonZero(maskWarped);
        overlapRatio = overlapPixels / (double)Math.Max(1, src1.Width * src1.Height);
        overlapRatio = Math.Clamp(overlapRatio, 0.0, 1.0);
        return true;
    }

    private static Mat ManualHorizontalStitch(Mat src1, Mat src2, double overlapPercent, string blendMode, out double overlapRatio)
    {
        var overlap = (int)Math.Round(Math.Min(src1.Width, src2.Width) * overlapPercent / 100.0);
        overlap = Math.Clamp(overlap, 0, Math.Min(src1.Width, src2.Width) - 1);

        var width = src1.Width + src2.Width - overlap;
        var height = Math.Max(src1.Height, src2.Height);
        var stitched = new Mat(height, width, src1.Type(), Scalar.Black);

        using (var roi1 = new Mat(stitched, new Rect(0, 0, src1.Width, src1.Height)))
        {
            src1.CopyTo(roi1);
        }

        var startX = src1.Width - overlap;
        using (var roi2 = new Mat(stitched, new Rect(startX, 0, src2.Width, src2.Height)))
        {
            src2.CopyTo(roi2);
        }

        if (overlap > 0 && blendMode.Equals("Linear", StringComparison.OrdinalIgnoreCase))
        {
            for (var y = 0; y < Math.Min(src1.Height, src2.Height); y++)
            {
                for (var x = 0; x < overlap; x++)
                {
                    var alpha = x / (double)Math.Max(1, overlap - 1);
                    var p1 = src1.At<Vec3b>(y, src1.Width - overlap + x);
                    var p2 = src2.At<Vec3b>(y, x);
                    var blended = new Vec3b(
                        (byte)Math.Clamp((1 - alpha) * p1.Item0 + alpha * p2.Item0, 0, 255),
                        (byte)Math.Clamp((1 - alpha) * p1.Item1 + alpha * p2.Item1, 0, 255),
                        (byte)Math.Clamp((1 - alpha) * p1.Item2 + alpha * p2.Item2, 0, 255));
                    stitched.Set(y, src1.Width - overlap + x, blended);
                }
            }
        }

        overlapRatio = overlap / (double)Math.Max(1, Math.Min(src1.Width, src2.Width));
        return stitched;
    }
}
