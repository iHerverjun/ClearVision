using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.Calibration;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Camera Calibration",
    Description = "Calibrates camera intrinsics from chessboard or circle grid images.",
    Category = "Calibration",
    IconName = "calibration",
    Keywords = new[] { "Calibration", "Chessboard", "Intrinsic", "Distortion" }
)]
[InputPort("Image", "Input Image", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "Result Image", PortDataType.Image)]
[OutputPort("CalibrationData", "Calibration Data", PortDataType.String)]
[OperatorParam("PatternType", "Pattern Type", "enum", DefaultValue = "Chessboard", Options = new[] { "Chessboard|Chessboard", "CircleGrid|CircleGrid" })]
[OperatorParam("BoardWidth", "Board Width", "int", DefaultValue = 9, Min = 2, Max = 30)]
[OperatorParam("BoardHeight", "Board Height", "int", DefaultValue = 6, Min = 2, Max = 30)]
[OperatorParam("SquareSize", "Square Size(mm)", "double", DefaultValue = 25.0, Min = 0.1, Max = 1000.0)]
[OperatorParam("Mode", "Mode", "enum", DefaultValue = "SingleImage", Options = new[] { "SingleImage|SingleImage", "FolderCalibration|FolderCalibration" })]
[OperatorParam("ImageFolder", "Image Folder", "string", DefaultValue = "")]
[OperatorParam("CalibrationOutputPath", "Calibration Output Path", "string", DefaultValue = "calibration_result.json")]
public class CameraCalibrationOperator : OperatorBase
{
    private const int MinValidFolderSamples = 12;
    private const double MeanErrorAcceptanceThreshold = 0.35;
    private const double MaxErrorAcceptanceThreshold = 0.60;
    private const double OutlierThresholdScale = 1.8;

    public override OperatorType OperatorType => OperatorType.CameraCalibration;

    public CameraCalibrationOperator(ILogger<CameraCalibrationOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var patternType = GetStringParam(@operator, "PatternType", "Chessboard");
        var boardWidth = GetIntParam(@operator, "BoardWidth", 9, 2, 30);
        var boardHeight = GetIntParam(@operator, "BoardHeight", 6, 2, 30);
        var squareSize = GetDoubleParam(@operator, "SquareSize", 25.0, 0.1, 1000.0);
        var mode = GetStringParam(@operator, "Mode", "SingleImage");
        var imageFolder = GetStringParam(@operator, "ImageFolder", "");
        var calibrationOutputPath = GetStringParam(@operator, "CalibrationOutputPath", "calibration_result.json");
        var patternSize = new Size(boardWidth, boardHeight);

        if (mode.Equals("FolderCalibration", StringComparison.OrdinalIgnoreCase))
        {
            return ExecuteFolderCalibration(patternType, patternSize, squareSize, imageFolder, calibrationOutputPath, cancellationToken);
        }

        return ExecuteSingleImagePreview(inputs, patternType, patternSize);
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var boardWidth = GetIntParam(@operator, "BoardWidth", 9);
        var boardHeight = GetIntParam(@operator, "BoardHeight", 6);
        var squareSize = GetDoubleParam(@operator, "SquareSize", 25.0);
        var mode = GetStringParam(@operator, "Mode", "SingleImage");

        if (boardWidth < 2 || boardWidth > 30)
        {
            return ValidationResult.Invalid("BoardWidth must be between 2 and 30.");
        }

        if (boardHeight < 2 || boardHeight > 30)
        {
            return ValidationResult.Invalid("BoardHeight must be between 2 and 30.");
        }

        if (squareSize <= 0 || squareSize > 1000)
        {
            return ValidationResult.Invalid("SquareSize must be between 0 and 1000 mm.");
        }

        if (!mode.Equals("SingleImage", StringComparison.OrdinalIgnoreCase) &&
            !mode.Equals("FolderCalibration", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Mode must be SingleImage or FolderCalibration.");
        }

        return ValidationResult.Valid();
    }

    private Task<OperatorExecutionOutput> ExecuteSingleImagePreview(
        Dictionary<string, object>? inputs,
        string patternType,
        Size patternSize)
    {
        if (!TryGetInputImage(inputs, "Image", out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is required."));
        }

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid."));
        }

        var resultImage = src.Clone();
        using var gray = new Mat();
        if (src.Channels() == 1)
        {
            src.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }

        var diagnostics = new List<string>
        {
            "SingleImage mode is preview only. Use FolderCalibration to produce an accepted calibration bundle."
        };
        var found = TryFindCalibrationCorners(gray, patternType, patternSize, out var corners) && corners.Length > 0;
        if (found)
        {
            if (patternType.Equals("Chessboard", StringComparison.OrdinalIgnoreCase))
            {
                Cv2.CornerSubPix(
                    gray,
                    corners,
                    new Size(11, 11),
                    new Size(-1, -1),
                    new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 30, 0.1));
            }

            Cv2.DrawChessboardCorners(resultImage, patternSize, corners, true);
            diagnostics.Add($"Pattern detected with {corners.Length} corners.");
            Cv2.PutText(resultImage, $"Preview corners: {corners.Length}", new Point(10, 30), HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 255, 0), 2);
        }
        else
        {
            diagnostics.Add("Calibration pattern was not detected.");
            Cv2.PutText(resultImage, "Preview only: pattern not found", new Point(10, 30), HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 165, 255), 2);
        }

        var bundle = new CalibrationBundleV2
        {
            CalibrationKind = CalibrationKindV2.CameraIntrinsics,
            TransformModel = TransformModelV2.Preview,
            SourceFrame = "image_pixel",
            TargetFrame = "camera_normalized",
            Unit = "px",
            ImageSize = new CalibrationImageSizeV2
            {
                Width = src.Width,
                Height = src.Height
            },
            Distortion = new CalibrationDistortionV2
            {
                Model = DistortionModelV2.BrownConrady,
                Coefficients = Array.Empty<double>()
            },
            Quality = CalibrationBundleV2Helpers.CreatePreviewQuality(
                diagnostics: diagnostics,
                sampleCount: found ? corners.Length : 0),
            ProducerOperator = nameof(CameraCalibrationOperator)
        };

        var calibrationJson = CalibrationBundleV2Json.Serialize(bundle);
        var output = new Dictionary<string, object>
        {
            ["CalibrationData"] = calibrationJson,
            ["Found"] = found,
            ["Accepted"] = false,
            ["CornerCount"] = found ? corners.Length : 0,
            ["Message"] = "SingleImage mode generates preview diagnostics only."
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, output)));
    }

    private Task<OperatorExecutionOutput> ExecuteFolderCalibration(
        string patternType,
        Size patternSize,
        double squareSize,
        string imageFolder,
        string outputPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageFolder) || !Directory.Exists(imageFolder))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("ImageFolder does not exist."));
        }

        var imageFiles = EnumerateImageFiles(imageFolder);
        if (imageFiles.Length == 0)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("No calibration image was found in folder."));
        }

        var failedFiles = new List<string>();
        var samples = new List<CalibrationSample>();
        Size? imageSize = null;

        foreach (var file in imageFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var img = Cv2.ImRead(file, ImreadModes.Grayscale);
                if (img.Empty())
                {
                    failedFiles.Add($"{Path.GetFileName(file)}: unreadable");
                    continue;
                }

                if (imageSize == null)
                {
                    imageSize = img.Size();
                }
                else if (img.Size() != imageSize.Value)
                {
                    return Task.FromResult(OperatorExecutionOutput.Failure(
                        $"Mixed image resolution detected. Expected {imageSize.Value.Width}x{imageSize.Value.Height}, but {Path.GetFileName(file)} is {img.Width}x{img.Height}."));
                }

                if (!TryFindCalibrationCorners(img, patternType, patternSize, out var corners) || corners.Length == 0)
                {
                    failedFiles.Add($"{Path.GetFileName(file)}: pattern not found");
                    continue;
                }

                if (patternType.Equals("Chessboard", StringComparison.OrdinalIgnoreCase))
                {
                    Cv2.CornerSubPix(
                        img,
                        corners,
                        new Size(11, 11),
                        new Size(-1, -1),
                        new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 30, 0.001));
                }

                samples.Add(new CalibrationSample(
                    Path.GetFileName(file),
                    CreateObjectPoints(patternSize, squareSize),
                    corners));
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to process calibration image {File}.", file);
                failedFiles.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        if (imageSize == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("No readable image was found in folder."));
        }

        if (samples.Count < MinValidFolderSamples)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(
                $"Need at least {MinValidFolderSamples} valid images. Found {samples.Count}."));
        }

        if (!TrySolveCalibration(samples, imageSize.Value, out var solveResult, out var solveError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"Calibration failed: {solveError}"));
        }

        var outlierThreshold = Math.Max(MaxErrorAcceptanceThreshold, solveResult.MeanError * OutlierThresholdScale);
        var inlierSamples = new List<CalibrationSample>();
        var outlierFiles = new List<string>();
        for (var i = 0; i < samples.Count; i++)
        {
            if (solveResult.ViewErrors[i] <= outlierThreshold)
            {
                inlierSamples.Add(samples[i]);
            }
            else
            {
                outlierFiles.Add(samples[i].Source);
            }
        }

        var finalResult = solveResult;
        if (outlierFiles.Count > 0 && inlierSamples.Count >= MinValidFolderSamples)
        {
            if (TrySolveCalibration(inlierSamples, imageSize.Value, out var refinedResult, out var refinedError))
            {
                finalResult = refinedResult;
                samples = inlierSamples;
            }
            else
            {
                Logger.LogWarning("Refined calibration failed after outlier rejection: {Error}", refinedError);
            }
        }

        var accepted =
            samples.Count >= MinValidFolderSamples &&
            finalResult.MeanError <= MeanErrorAcceptanceThreshold &&
            finalResult.MaxError <= MaxErrorAcceptanceThreshold;

        var diagnostics = new List<string>
        {
            $"Total images: {imageFiles.Length}",
            $"Detected valid samples: {samples.Count}",
            $"Rejected during detection: {failedFiles.Count}",
            $"Rejected as outliers: {outlierFiles.Count}",
            $"Mean reprojection error: {finalResult.MeanError:F4} px",
            $"Max reprojection error: {finalResult.MaxError:F4} px"
        };

        if (failedFiles.Count > 0)
        {
            diagnostics.Add($"Bad samples: {string.Join(", ", failedFiles.Take(10))}");
        }

        if (outlierFiles.Count > 0)
        {
            diagnostics.Add($"Outlier samples: {string.Join(", ", outlierFiles.Take(10))}");
        }

        if (!accepted)
        {
            diagnostics.Add($"Quality gate failed. Thresholds: mean<={MeanErrorAcceptanceThreshold:F2}, max<={MaxErrorAcceptanceThreshold:F2}, minSamples>={MinValidFolderSamples}.");
        }
        else
        {
            diagnostics.Add("Quality gate passed.");
        }

        var bundle = new CalibrationBundleV2
        {
            CalibrationKind = CalibrationKindV2.CameraIntrinsics,
            TransformModel = TransformModelV2.Projection,
            SourceFrame = "image_pixel",
            TargetFrame = "camera_normalized",
            Unit = "px",
            ImageSize = new CalibrationImageSizeV2
            {
                Width = imageSize.Value.Width,
                Height = imageSize.Value.Height
            },
            Intrinsics = new CalibrationIntrinsicsV2
            {
                CameraMatrix = finalResult.CameraMatrix
            },
            Distortion = new CalibrationDistortionV2
            {
                Model = DistortionModelV2.BrownConrady,
                Coefficients = finalResult.DistCoeffs
            },
            Quality = new CalibrationQualityV2
            {
                Accepted = accepted,
                MeanError = finalResult.MeanError,
                MaxError = finalResult.MaxError,
                InlierCount = samples.Count,
                TotalSampleCount = imageFiles.Length,
                Diagnostics = diagnostics
            },
            ProducerOperator = nameof(CameraCalibrationOperator)
        };

        var calibrationJson = CalibrationBundleV2Json.Serialize(bundle);
        TryWriteOutputFile(outputPath, calibrationJson);

        using var previewBase = Cv2.ImRead(imageFiles[0], ImreadModes.Color);
        var resultImage = previewBase.Empty()
            ? new Mat(imageSize.Value.Height, imageSize.Value.Width, MatType.CV_8UC3, Scalar.Black)
            : previewBase.Clone();
        Cv2.PutText(
            resultImage,
            $"Accepted: {accepted}  Mean: {finalResult.MeanError:F3}px  Max: {finalResult.MaxError:F3}px",
            new Point(10, 30),
            HersheyFonts.HersheySimplex,
            0.65,
            accepted ? new Scalar(0, 255, 0) : new Scalar(0, 165, 255),
            2);

        var output = new Dictionary<string, object>
        {
            ["CalibrationData"] = calibrationJson,
            ["Accepted"] = accepted,
            ["ReprojectionError"] = finalResult.MeanError,
            ["MaxReprojectionError"] = finalResult.MaxError,
            ["PerViewErrors"] = finalResult.ViewErrors,
            ["ImageCount"] = samples.Count,
            ["TotalImages"] = imageFiles.Length,
            ["RejectedDetectionCount"] = failedFiles.Count,
            ["RejectedOutlierCount"] = outlierFiles.Count,
            ["Diagnostics"] = diagnostics,
            ["OutputPath"] = outputPath,
            ["Message"] = accepted
                ? $"Calibration accepted with {samples.Count}/{imageFiles.Length} samples."
                : "Calibration completed but did not pass quality acceptance."
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, output)));
    }

    private static string[] EnumerateImageFiles(string folder)
    {
        return Directory.GetFiles(folder, "*.*")
            .Where(path =>
            {
                var ext = Path.GetExtension(path);
                return ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                       ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                       ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                       ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
                       ext.Equals(".tif", StringComparison.OrdinalIgnoreCase) ||
                       ext.Equals(".tiff", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool TryFindCalibrationCorners(Mat gray, string patternType, Size patternSize, out Point2f[] corners)
    {
        corners = Array.Empty<Point2f>();
        if (patternType.Equals("Chessboard", StringComparison.OrdinalIgnoreCase))
        {
            if (Cv2.FindChessboardCorners(
                gray,
                patternSize,
                out corners,
                ChessboardFlags.AdaptiveThresh | ChessboardFlags.NormalizeImage | ChessboardFlags.FastCheck))
            {
                return true;
            }

            return Cv2.FindChessboardCorners(
                gray,
                patternSize,
                out corners,
                ChessboardFlags.AdaptiveThresh | ChessboardFlags.NormalizeImage);
        }

        if (patternType.Equals("CircleGrid", StringComparison.OrdinalIgnoreCase))
        {
            return Cv2.FindCirclesGrid(gray, patternSize, out corners, FindCirclesGridFlags.SymmetricGrid);
        }

        return false;
    }

    private static Point3f[] CreateObjectPoints(Size patternSize, double squareSize)
    {
        var points = new Point3f[patternSize.Width * patternSize.Height];
        var index = 0;
        for (var y = 0; y < patternSize.Height; y++)
        {
            for (var x = 0; x < patternSize.Width; x++)
            {
                points[index++] = new Point3f((float)(x * squareSize), (float)(y * squareSize), 0f);
            }
        }

        return points;
    }

    private static bool TrySolveCalibration(
        IReadOnlyList<CalibrationSample> samples,
        Size imageSize,
        out CalibrationSolveResult result,
        out string error)
    {
        result = new CalibrationSolveResult(Array.Empty<double[]>(), Array.Empty<double>(), Array.Empty<double>(), 0, 0);
        error = string.Empty;

        var objectPointMats = new List<Mat>(samples.Count);
        var imagePointMats = new List<Mat>(samples.Count);
        Mat[]? rvecs = null;
        Mat[]? tvecs = null;

        try
        {
            foreach (var sample in samples)
            {
                objectPointMats.Add(Mat.FromArray(sample.ObjectPoints));
                imagePointMats.Add(Mat.FromArray(sample.ImagePoints));
            }

            using var cameraMatrix = new Mat();
            using var distCoeffs = new Mat();
            var rms = Cv2.CalibrateCamera(
                objectPointMats,
                imagePointMats,
                imageSize,
                cameraMatrix,
                distCoeffs,
                out rvecs,
                out tvecs,
                CalibrationFlags.None,
                new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 80, 1e-7));

            var viewErrors = ComputePerViewErrors(samples, rvecs, tvecs, cameraMatrix, distCoeffs);
            var mean = viewErrors.Length == 0 ? 0 : viewErrors.Average();
            var max = viewErrors.Length == 0 ? 0 : viewErrors.Max();

            result = new CalibrationSolveResult(
                CalibrationBundleV2Helpers.ToJaggedMatrix(cameraMatrix),
                CalibrationBundleV2Helpers.ToFlatVector(distCoeffs),
                viewErrors,
                mean,
                max);

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            DisposeMatList(objectPointMats);
            DisposeMatList(imagePointMats);
            DisposeMatArray(rvecs);
            DisposeMatArray(tvecs);
        }
    }

    private static double[] ComputePerViewErrors(
        IReadOnlyList<CalibrationSample> samples,
        IReadOnlyList<Mat>? rvecs,
        IReadOnlyList<Mat>? tvecs,
        Mat cameraMatrix,
        Mat distCoeffs)
    {
        if (rvecs == null || tvecs == null || rvecs.Count != samples.Count || tvecs.Count != samples.Count)
        {
            return Enumerable.Repeat(double.PositiveInfinity, samples.Count).ToArray();
        }

        var errors = new double[samples.Count];
        for (var i = 0; i < samples.Count; i++)
        {
            using var projectedMat = new Mat();
            Cv2.ProjectPoints(
                InputArray.Create(samples[i].ObjectPoints),
                rvecs[i],
                tvecs[i],
                cameraMatrix,
                distCoeffs,
                projectedMat);

            var projectedPoints = ReadPoint2fVector(projectedMat);

            if (projectedPoints.Length != samples[i].ImagePoints.Length)
            {
                errors[i] = double.PositiveInfinity;
                continue;
            }

            double sumSq = 0;
            for (var p = 0; p < projectedPoints.Length; p++)
            {
                var dx = projectedPoints[p].X - samples[i].ImagePoints[p].X;
                var dy = projectedPoints[p].Y - samples[i].ImagePoints[p].Y;
                sumSq += dx * dx + dy * dy;
            }

            errors[i] = Math.Sqrt(sumSq / projectedPoints.Length);
        }

        return errors;
    }

    private static Point2f[] ReadPoint2fVector(Mat mat)
    {
        if (mat.Empty())
        {
            return Array.Empty<Point2f>();
        }

        var count = Math.Max(mat.Rows, mat.Cols);
        var points = new Point2f[count];
        for (var i = 0; i < count; i++)
        {
            points[i] = mat.Rows >= mat.Cols ? mat.At<Point2f>(i, 0) : mat.At<Point2f>(0, i);
        }

        return points;
    }

    private void TryWriteOutputFile(string outputPath, string json)
    {
        try
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputPath, json);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to save calibration file to {Path}.", outputPath);
        }
    }

    private static void DisposeMatList(IEnumerable<Mat> mats)
    {
        foreach (var mat in mats)
        {
            mat.Dispose();
        }
    }

    private static void DisposeMatArray(IEnumerable<Mat>? mats)
    {
        if (mats == null)
        {
            return;
        }

        foreach (var mat in mats)
        {
            mat.Dispose();
        }
    }

    private sealed record CalibrationSample(string Source, Point3f[] ObjectPoints, Point2f[] ImagePoints);

    private sealed record CalibrationSolveResult(
        double[][] CameraMatrix,
        double[] DistCoeffs,
        double[] ViewErrors,
        double MeanError,
        double MaxError);
}
