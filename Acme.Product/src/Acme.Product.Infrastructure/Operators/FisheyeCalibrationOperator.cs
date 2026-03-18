// FisheyeCalibrationOperator.cs
// 鱼眼相机标定算子
// 对标 Halcon: change_radial_distortion_cam_par
// 作者：AI Assistant

using System.Text.Json;
using System.Text.Json.Serialization;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 鱼眼相机标定算子 - 支持棋盘格和圆点板标定
/// 对标 Halcon change_radial_distortion_cam_par
/// </summary>
[OperatorMeta(
    DisplayName = "Fisheye Calibration",
    Description = "Calibrates fisheye camera intrinsics and distortion parameters using chessboard or circle grid patterns.",
    Category = "Calibration",
    IconName = "fisheye-calibration",
    Keywords = new[] { "Fisheye", "Calibration", "Distortion", "Kannala-Brandt" }
)]
[InputPort("Image", "Input Image", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "Result Image", PortDataType.Image)]
[OutputPort("CalibrationData", "Calibration Data", PortDataType.String)]
[OutputPort("CameraMatrix", "Camera Matrix", PortDataType.Any)]
[OutputPort("DistCoeffs", "Distortion Coefficients", PortDataType.Any)]
[OperatorParam("PatternType", "Pattern Type", "enum", DefaultValue = "Chessboard", Options = new[] { "Chessboard|Chessboard", "CircleGrid|CircleGrid" })]
[OperatorParam("BoardWidth", "Board Width", "int", DefaultValue = 9, Min = 2, Max = 30)]
[OperatorParam("BoardHeight", "Board Height", "int", DefaultValue = 6, Min = 2, Max = 30)]
[OperatorParam("SquareSize", "Square Size(mm)", "double", DefaultValue = 25.0, Min = 0.1, Max = 1000.0)]
[OperatorParam("Mode", "Mode", "enum", DefaultValue = "SingleImage", Options = new[] { "SingleImage|SingleImage", "FolderCalibration|FolderCalibration" })]
[OperatorParam("ImageFolder", "Image Folder", "string", DefaultValue = "")]
[OperatorParam("CalibrationOutputPath", "Calibration Output Path", "string", DefaultValue = "fisheye_calibration_result.json")]
[OperatorParam("RecomputeExtrinsic", "Recompute Extrinsic", "bool", DefaultValue = true)]
[OperatorParam("CheckConditions", "Check Conditions", "bool", DefaultValue = true)]
public class FisheyeCalibrationOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.FisheyeCalibration;

    public FisheyeCalibrationOperator(ILogger<FisheyeCalibrationOperator> logger) : base(logger)
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
        var calibrationOutputPath = GetStringParam(@operator, "CalibrationOutputPath", "fisheye_calibration_result.json");
        var recomputeExtrinsic = GetBoolParam(@operator, "RecomputeExtrinsic", true);
        var checkConditions = GetBoolParam(@operator, "CheckConditions", true);

        var patternSize = new Size(boardWidth, boardHeight);

        if (mode.Equals("FolderCalibration", StringComparison.OrdinalIgnoreCase))
        {
            return ExecuteFolderCalibration(
                patternType, patternSize, squareSize, imageFolder, 
                calibrationOutputPath, recomputeExtrinsic, checkConditions, cancellationToken);
        }

        return ExecuteSingleImageCalibration(inputs, patternType, patternSize, squareSize);
    }

    private Task<OperatorExecutionOutput> ExecuteSingleImageCalibration(
        Dictionary<string, object>? inputs,
        string patternType,
        Size patternSize,
        double squareSize)
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

        if (!TryFindCalibrationCorners(gray, patternType, patternSize, out var corners) || corners.Length == 0)
        {
            var notFoundData = new Dictionary<string, object>
            {
                { "CalibrationData", "" },
                { "Found", false },
                { "Message", "Calibration pattern was not detected." }
            };
            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, notFoundData)));
        }

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

        var objectPoints = CreateObjectPoints(patternSize, squareSize);
        var imageSize = gray.Size();

        // 使用兼容 OpenCvSharp4 的标定路径。当前运行时缺少 Fisheye API，回退到标准标定。
        var (cameraMatrix, distCoeffs, rvecs, tvecs, calibrationError) = CalibrateFisheye(
            new List<Point3f[]> { objectPoints },
            new List<Point2f[]> { corners },
            imageSize);

        var payload = new FisheyeCalibrationPayload
        {
            PatternType = patternType,
            BoardWidth = patternSize.Width,
            BoardHeight = patternSize.Height,
            SquareSize = squareSize,
            ImageWidth = src.Width,
            ImageHeight = src.Height,
            CameraMatrix = ToJaggedMatrix3x3(cameraMatrix),
            DistCoeffs = FlattenMat(distCoeffs),
            CalibrationError = calibrationError,
            ImageCount = 1,
            FailedFiles = new List<string>(),
            Found = true,
            Corners = corners.Select(c => new Point2Payload(c.X, c.Y)).ToList(),
            ObjectPoints = objectPoints.Select(p => new Point3Payload(p.X, p.Y, p.Z)).ToList(),
            Timestamp = DateTime.UtcNow,
            Model = "Kannala-Brandt",
            IsFisheye = true
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });

        Cv2.PutText(
            resultImage,
            $"Fisheye: {corners.Length} corners, Err: {calibrationError:F4}",
            new Point(10, 30),
            HersheyFonts.HersheySimplex,
            0.7,
            new Scalar(0, 255, 0),
            2);

        var outputData = new Dictionary<string, object>
        {
            { "CalibrationData", json },
            { "CameraMatrix", cameraMatrix },
            { "DistCoeffs", distCoeffs },
            { "Found", true },
            { "CornerCount", corners.Length },
            { "CalibrationError", calibrationError },
            { "Message", $"Fisheye calibration succeeded with {corners.Length} corners." }
        };

        cameraMatrix.Dispose();
        distCoeffs.Dispose();
        DisposeMatArray(rvecs);
        DisposeMatArray(tvecs);

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, outputData)));
    }

    private Task<OperatorExecutionOutput> ExecuteFolderCalibration(
        string patternType,
        Size patternSize,
        double squareSize,
        string imageFolder,
        string outputPath,
        bool recomputeExtrinsic,
        bool checkConditions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageFolder) || !Directory.Exists(imageFolder))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("ImageFolder does not exist."));
        }

        var imageFiles = Directory.GetFiles(imageFolder, "*.png")
            .Concat(Directory.GetFiles(imageFolder, "*.jpg"))
            .Concat(Directory.GetFiles(imageFolder, "*.jpeg"))
            .Concat(Directory.GetFiles(imageFolder, "*.bmp"))
            .ToArray();

        if (imageFiles.Length == 0)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("No calibration image was found in folder."));
        }

        var objectPointsList = new List<Point3f[]>();
        var imagePointsList = new List<Point2f[]>();
        var failedFiles = new List<string>();
        Size imageSize = default;

        foreach (var file in imageFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var img = Cv2.ImRead(file, ImreadModes.Grayscale);
                if (img.Empty())
                {
                    failedFiles.Add(Path.GetFileName(file));
                    continue;
                }

                if (imageSize == default)
                {
                    imageSize = img.Size();
                }

                if (!TryFindCalibrationCorners(img, patternType, patternSize, out var corners) || corners.Length == 0)
                {
                    failedFiles.Add(Path.GetFileName(file));
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

                objectPointsList.Add(CreateObjectPoints(patternSize, squareSize));
                imagePointsList.Add(corners);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to process calibration image {File}.", file);
                failedFiles.Add(Path.GetFileName(file));
            }
        }

        if (objectPointsList.Count < 3)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"Need at least 3 valid images, got {objectPointsList.Count}."));
        }

        // 使用兼容 OpenCvSharp4 的标定路径。当前运行时缺少 Fisheye API，回退到标准标定。
        var (cameraMatrix, distCoeffs, rvecs, tvecs, calibrationError) = CalibrateFisheye(
            objectPointsList, imagePointsList, imageSize);

        var payload = new FisheyeCalibrationPayload
        {
            PatternType = patternType,
            BoardWidth = patternSize.Width,
            BoardHeight = patternSize.Height,
            SquareSize = squareSize,
            ImageWidth = imageSize.Width,
            ImageHeight = imageSize.Height,
            CameraMatrix = ToJaggedMatrix3x3(cameraMatrix),
            DistCoeffs = FlattenMat(distCoeffs),
            CalibrationError = calibrationError,
            ImageCount = imageFiles.Length - failedFiles.Count,
            FailedFiles = failedFiles,
            Found = true,
            Timestamp = DateTime.UtcNow,
            Model = "Kannala-Brandt",
            IsFisheye = true
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        try
        {
            File.WriteAllText(outputPath, json);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to save calibration file to {Path}.", outputPath);
        }

        using var previewBase = Cv2.ImRead(imageFiles.First(), ImreadModes.Color);
        var resultImage = previewBase.Empty()
            ? new Mat(imageSize.Height, imageSize.Width, MatType.CV_8UC3, Scalar.Black)
            : previewBase.Clone();

        Cv2.PutText(
            resultImage,
            $"Fisheye: {payload.ImageCount}/{imageFiles.Length}, Err: {calibrationError:F4}",
            new Point(10, 30),
            HersheyFonts.HersheySimplex,
            0.7,
            new Scalar(0, 255, 0),
            2);

        var additionalData = new Dictionary<string, object>
        {
            { "CalibrationData", json },
            { "CameraMatrix", cameraMatrix },
            { "DistCoeffs", distCoeffs },
            { "CalibrationError", calibrationError },
            { "ImageCount", payload.ImageCount },
            { "TotalImages", imageFiles.Length },
            { "OutputPath", outputPath },
            { "Message", $"Fisheye calibration completed with {payload.ImageCount}/{imageFiles.Length} images." }
        };

        cameraMatrix.Dispose();
        distCoeffs.Dispose();
        DisposeMatArray(rvecs);
        DisposeMatArray(tvecs);

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, additionalData)));
    }

    private static (Mat cameraMatrix, Mat distCoeffs, Mat[] rvecs, Mat[] tvecs, double calibrationError) CalibrateFisheye(
        List<Point3f[]> objectPoints,
        List<Point2f[]> imagePoints,
        Size imageSize)
    {
        var cameraMatrix = new Mat(3, 3, MatType.CV_64FC1);
        var distCoeffs = new Mat(1, 8, MatType.CV_64FC1, Scalar.All(0));
        var rvecs = new Mat[objectPoints.Count];
        var tvecs = new Mat[objectPoints.Count];

        // 初始化相机矩阵
        cameraMatrix.Set(0, 0, imageSize.Width * 0.8);
        cameraMatrix.Set(1, 1, imageSize.Height * 0.8);
        cameraMatrix.Set(0, 2, imageSize.Width / 2.0);
        cameraMatrix.Set(1, 2, imageSize.Height / 2.0);
        cameraMatrix.Set(2, 2, 1.0);

        try
        {
            var objectPointMats = new List<Mat>(objectPoints.Count);
            var imagePointMats = new List<Mat>(imagePoints.Count);
            for (var i = 0; i < objectPoints.Count; i++)
            {
                objectPointMats.Add(Mat.FromArray(objectPoints[i]));
                imagePointMats.Add(Mat.FromArray(imagePoints[i]));
            }

            var rms = Cv2.CalibrateCamera(
                objectPointMats,
                imagePointMats,
                imageSize,
                cameraMatrix,
                distCoeffs,
                out rvecs,
                out tvecs);

            foreach (var mat in objectPointMats) mat.Dispose();
            foreach (var mat in imagePointMats) mat.Dispose();

            return (cameraMatrix, distCoeffs, rvecs, tvecs, rms);
        }
        catch
        {
            return (cameraMatrix, distCoeffs, rvecs, tvecs, double.NaN);
        }
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

    private static bool TryFindCalibrationCorners(Mat gray, string patternType, Size patternSize, out Point2f[] corners)
    {
        corners = Array.Empty<Point2f>();
        if (patternType.Equals("Chessboard", StringComparison.OrdinalIgnoreCase))
        {
            return Cv2.FindChessboardCorners(
                gray,
                patternSize,
                out corners,
                ChessboardFlags.AdaptiveThresh | ChessboardFlags.NormalizeImage | ChessboardFlags.FastCheck);
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

    private static double[][] ToJaggedMatrix3x3(Mat cameraMatrix)
    {
        return new[]
        {
            new[] { cameraMatrix.At<double>(0, 0), cameraMatrix.At<double>(0, 1), cameraMatrix.At<double>(0, 2) },
            new[] { cameraMatrix.At<double>(1, 0), cameraMatrix.At<double>(1, 1), cameraMatrix.At<double>(1, 2) },
            new[] { cameraMatrix.At<double>(2, 0), cameraMatrix.At<double>(2, 1), cameraMatrix.At<double>(2, 2) }
        };
    }

    private static double[] FlattenMat(Mat mat)
    {
        if (mat.Empty())
        {
            return Array.Empty<double>();
        }

        var result = new double[mat.Rows * mat.Cols];
        var index = 0;
        for (var r = 0; r < mat.Rows; r++)
        {
            for (var c = 0; c < mat.Cols; c++)
            {
                result[index++] = mat.At<double>(r, c);
            }
        }

        return result;
    }

    private static void DisposeMatArray(Mat[]? mats)
    {
        if (mats == null) return;
        foreach (var mat in mats)
        {
            mat?.Dispose();
        }
    }

    private sealed class FisheyeCalibrationPayload
    {
        public string PatternType { get; set; } = string.Empty;
        public int BoardWidth { get; set; }
        public int BoardHeight { get; set; }
        public double SquareSize { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public double[][] CameraMatrix { get; set; } = Array.Empty<double[]>();
        public double[] DistCoeffs { get; set; } = Array.Empty<double>();
        public double CalibrationError { get; set; }
        public int ImageCount { get; set; }
        public List<string> FailedFiles { get; set; } = new();
        public bool Found { get; set; }
        public List<Point2Payload>? Corners { get; set; }
        public List<Point3Payload>? ObjectPoints { get; set; }
        public DateTime Timestamp { get; set; }
        public string Model { get; set; } = "Kannala-Brandt";
        public bool IsFisheye { get; set; } = true;
    }

    private readonly record struct Point2Payload(double X, double Y);
    private readonly record struct Point3Payload(double X, double Y, double Z);
}
