// StereoCalibrationOperator.cs
// 双目标定与立体校正算子
// 对标 Halcon: binocular_calibration / gen_binocular_rectification_map
// 作者：AI Assistant

using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.Calibration;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 双目标定与立体校正算子
/// 对标 Halcon binocular_calibration / gen_binocular_rectification_map
/// </summary>
[OperatorMeta(
    DisplayName = "Stereo Calibration",
    Description = "Calibrates stereo camera pair and generates rectification maps for epipolar alignment.",
    Category = "Calibration",
    IconName = "stereo-calibration",
    Keywords = new[] { "Stereo", "Binocular", "Calibration", "Rectification", "Epipolar" }
)]
[InputPort("LeftImage", "Left Input Image", PortDataType.Image, IsRequired = true)]
[InputPort("RightImage", "Right Input Image", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "Result Visualization", PortDataType.Image)]
[OutputPort("CalibrationData", "Stereo Calibration Data", PortDataType.String)]
[OutputPort("LeftMap1", "Left Rectification Map1", PortDataType.Any)]
[OutputPort("LeftMap2", "Left Rectification Map2", PortDataType.Any)]
[OutputPort("RightMap1", "Right Rectification Map1", PortDataType.Any)]
[OutputPort("RightMap2", "Right Rectification Map2", PortDataType.Any)]
[OperatorParam("PatternType", "Pattern Type", "enum", DefaultValue = "Chessboard", Options = new[] { "Chessboard|Chessboard", "CircleGrid|CircleGrid" })]
[OperatorParam("BoardWidth", "Board Width", "int", DefaultValue = 9, Min = 2, Max = 30)]
[OperatorParam("BoardHeight", "Board Height", "int", DefaultValue = 6, Min = 2, Max = 30)]
[OperatorParam("SquareSize", "Square Size(mm)", "double", DefaultValue = 25.0, Min = 0.1, Max = 1000.0)]
[OperatorParam("Mode", "Mode", "enum", DefaultValue = "SinglePair", Options = new[] { "SinglePair|Single Pair", "FolderCalibration|Folder Calibration" })]
[OperatorParam("LeftImageFolder", "Left Image Folder", "string", DefaultValue = "")]
[OperatorParam("RightImageFolder", "Right Image Folder", "string", DefaultValue = "")]
[OperatorParam("CalibrationOutputPath", "Calibration Output Path", "string", DefaultValue = "stereo_calibration_result.json")]
[OperatorParam("MinValidPairs", "Minimum Valid Pairs", "int", DefaultValue = 12, Min = 3, Max = 100)]
[OperatorParam("ZeroDisparity", "Zero Disparity", "bool", DefaultValue = false)]
[OperatorParam("Alpha", "Alpha (0=Crop, 1=Preserve)", "double", DefaultValue = 0.0, Min = -1.0, Max = 1.0)]
public class StereoCalibrationOperator : OperatorBase
{
    private const double StereoMeanErrorAcceptanceThreshold = 0.35;
    private const double CameraMeanErrorAcceptanceThreshold = 0.35;
    private const double PerViewErrorAcceptanceThreshold = 0.60;

    public override OperatorType OperatorType => OperatorType.StereoCalibration;

    public StereoCalibrationOperator(ILogger<StereoCalibrationOperator> logger) : base(logger)
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
        var mode = GetStringParam(@operator, "Mode", "SinglePair");
        var leftFolder = GetStringParam(@operator, "LeftImageFolder", "");
        var rightFolder = GetStringParam(@operator, "RightImageFolder", "");
        var outputPath = GetStringParam(@operator, "CalibrationOutputPath", "stereo_calibration_result.json");
        var minValidPairs = GetIntParam(@operator, "MinValidPairs", 12, 3, 100);
        var zeroDisparity = GetBoolParam(@operator, "ZeroDisparity", false);
        var alpha = GetDoubleParam(@operator, "Alpha", 0.0, -1.0, 1.0);

        var patternSize = new Size(boardWidth, boardHeight);

        if (mode.Equals("FolderCalibration", StringComparison.OrdinalIgnoreCase))
        {
            return ExecuteFolderCalibration(
                patternType, patternSize, squareSize,
                leftFolder, rightFolder, outputPath,
                minValidPairs, zeroDisparity, alpha, cancellationToken);
        }

        return ExecuteSinglePairCalibration(
            inputs, patternType, patternSize, squareSize,
            zeroDisparity, alpha);
    }

    private Task<OperatorExecutionOutput> ExecuteSinglePairCalibration(
        Dictionary<string, object>? inputs,
        string patternType,
        Size patternSize,
        double squareSize,
        bool zeroDisparity,
        double alpha)
    {
        if (!TryGetInputImage(inputs, "LeftImage", out var leftWrapper) || leftWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Left image is required."));
        }

        if (!TryGetInputImage(inputs, "RightImage", out var rightWrapper) || rightWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Right image is required."));
        }

        var leftMat = leftWrapper.GetMat();
        var rightMat = rightWrapper.GetMat();

        if (leftMat.Empty() || rightMat.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input images are invalid."));
        }

        if (leftMat.Size() != rightMat.Size())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Left and right images must have the same size."));
        }

        using var leftGray = new Mat();
        using var rightGray = new Mat();

        if (leftMat.Channels() == 1)
            leftMat.CopyTo(leftGray);
        else
            Cv2.CvtColor(leftMat, leftGray, ColorConversionCodes.BGR2GRAY);

        if (rightMat.Channels() == 1)
            rightMat.CopyTo(rightGray);
        else
            Cv2.CvtColor(rightMat, rightGray, ColorConversionCodes.BGR2GRAY);

        // 检测标定板角点
        if (!TryFindCalibrationCorners(leftGray, patternType, patternSize, out var leftCorners) || leftCorners.Length == 0)
        {
            return Task.FromResult(CreateFailureOutput(leftMat, "Calibration pattern not found in left image."));
        }

        if (!TryFindCalibrationCorners(rightGray, patternType, patternSize, out var rightCorners) || rightCorners.Length == 0)
        {
            return Task.FromResult(CreateFailureOutput(leftMat, "Calibration pattern not found in right image."));
        }

        // 亚像素优化
        if (patternType.Equals("Chessboard", StringComparison.OrdinalIgnoreCase))
        {
            Cv2.CornerSubPix(leftGray, leftCorners, new Size(11, 11), new Size(-1, -1),
                new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 30, 0.1));
            Cv2.CornerSubPix(rightGray, rightCorners, new Size(11, 11), new Size(-1, -1),
                new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 30, 0.1));
        }

        var imageSize = leftGray.Size();

        // 单个图像对无法进行完整双目标定，只能验证角点检测
        var resultImage = CreateStereoVisualization(leftMat, rightMat, leftCorners, rightCorners, patternSize);

        var bundle = new CalibrationBundleV2
        {
            CalibrationKind = CalibrationKindV2.StereoRig,
            TransformModel = TransformModelV2.Preview,
            SourceFrame = "left_camera",
            TargetFrame = "right_camera",
            Unit = "mm",
            ImageSize = new CalibrationImageSizeV2
            {
                Width = imageSize.Width,
                Height = imageSize.Height
            },
            Quality = CalibrationBundleV2Helpers.CreatePreviewQuality(
                new[]
                {
                    "SinglePair mode is preview-only.",
                    $"PatternType={patternType}",
                    $"CornersLeft={leftCorners.Length}",
                    $"CornersRight={rightCorners.Length}"
                },
                sampleCount: 1),
            ProducerOperator = nameof(StereoCalibrationOperator)
        };

        var json = CalibrationBundleV2Json.Serialize(bundle);

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, new Dictionary<string, object>
        {
            { "CalibrationData", json },
            { "Found", true },
            { "LeftCornerCount", leftCorners.Length },
            { "RightCornerCount", rightCorners.Length },
            { "Accepted", false },
            { "Message", "Single pair mode: preview only. Use FolderCalibration for accepted stereo bundle." }
        })));
    }

    private Task<OperatorExecutionOutput> ExecuteFolderCalibration(
        string patternType,
        Size patternSize,
        double squareSize,
        string leftFolder,
        string rightFolder,
        string outputPath,
        int minValidPairs,
        bool zeroDisparity,
        double alpha,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(leftFolder) || !Directory.Exists(leftFolder))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("LeftImageFolder does not exist."));
        }

        if (string.IsNullOrWhiteSpace(rightFolder) || !Directory.Exists(rightFolder))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("RightImageFolder does not exist."));
        }

        var leftFiles = GetImageFiles(leftFolder)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var rightFiles = GetImageFiles(rightFolder)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (leftFiles.Length == 0 || rightFiles.Length == 0)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("No calibration images found in folders."));
        }

        if (!TryCreateStereoFilePairs(leftFiles, rightFiles, out var filePairs, out var pairingError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(pairingError));
        }

        var objectPointsList = new List<Point3f[]>();
        var leftImagePointsList = new List<Point2f[]>();
        var rightImagePointsList = new List<Point2f[]>();
        var validPairKeys = new List<string>();
        var failedPairKeys = new List<string>();
        var successfulPairs = new List<StereoImagePair>();
        Size imageSize = default;

        for (int i = 0; i < filePairs.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pair = filePairs[i];

            try
            {
                using var leftImg = Cv2.ImRead(pair.LeftFile, ImreadModes.Grayscale);
                using var rightImg = Cv2.ImRead(pair.RightFile, ImreadModes.Grayscale);

                if (leftImg.Empty() || rightImg.Empty())
                {
                    failedPairKeys.Add(pair.StableKey);
                    Logger.LogWarning("Failed to load image pair {Index}: {Left}, {Right}", i, pair.LeftFile, pair.RightFile);
                    continue;
                }

                if (leftImg.Size() != rightImg.Size())
                {
                    failedPairKeys.Add(pair.StableKey);
                    Logger.LogWarning("Image sizes do not match for stereo pair {Key}: {LeftSize} vs {RightSize}", pair.StableKey, leftImg.Size(), rightImg.Size());
                    continue;
                }

                if (imageSize == default)
                {
                    imageSize = leftImg.Size();
                }
                else if (leftImg.Size() != imageSize)
                {
                    failedPairKeys.Add(pair.StableKey);
                    Logger.LogWarning("Image size for stereo pair {Key} differs from calibration set size {Expected}: {Actual}", pair.StableKey, imageSize, leftImg.Size());
                    continue;
                }

                if (!TryFindCalibrationCorners(leftImg, patternType, patternSize, out var leftCorners) || leftCorners.Length == 0)
                {
                    failedPairKeys.Add(pair.StableKey);
                    Logger.LogWarning("Pattern not found in left image: {File}", pair.LeftFile);
                    continue;
                }

                if (!TryFindCalibrationCorners(rightImg, patternType, patternSize, out var rightCorners) || rightCorners.Length == 0)
                {
                    failedPairKeys.Add(pair.StableKey);
                    Logger.LogWarning("Pattern not found in right image: {File}", pair.RightFile);
                    continue;
                }

                // 亚像素优化
                if (patternType.Equals("Chessboard", StringComparison.OrdinalIgnoreCase))
                {
                    Cv2.CornerSubPix(leftImg, leftCorners, new Size(11, 11), new Size(-1, -1),
                        new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 30, 0.001));
                    Cv2.CornerSubPix(rightImg, rightCorners, new Size(11, 11), new Size(-1, -1),
                        new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 30, 0.001));
                }

                objectPointsList.Add(CreateObjectPoints(patternSize, squareSize));
                leftImagePointsList.Add(leftCorners);
                rightImagePointsList.Add(rightCorners);
                validPairKeys.Add(pair.StableKey);
                successfulPairs.Add(pair);

                Logger.LogDebug("Successfully processed pair {Index}/{Total} ({Key})", i + 1, filePairs.Length, pair.StableKey);
            }
            catch (Exception ex)
            {
                failedPairKeys.Add(pair.StableKey);
                Logger.LogWarning(ex, "Failed to process image pair {Index} ({Key})", i, pair.StableKey);
            }
        }

        if (objectPointsList.Count < minValidPairs)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(
                $"Need at least {minValidPairs} valid image pairs, got {objectPointsList.Count}. Failed pairs: {failedPairKeys.Count}. Keys: {JoinKeys(failedPairKeys)}"));
        }

        // 执行双目标定
        var calibrationResult = PerformStereoCalibration(
            objectPointsList, leftImagePointsList, rightImagePointsList, imageSize);

        if (!calibrationResult.Success)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"Stereo calibration failed: {calibrationResult.ErrorMessage}"));
        }

        // 执行立体校正
        var rectificationResult = PerformStereoRectification(
            calibrationResult, imageSize, zeroDisparity, alpha);
        if (!rectificationResult.Success)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Stereo rectification failed."));
        }

        // 保存结果
        var quality = EvaluateStereoCalibrationQuality(
            patternType,
            patternSize,
            squareSize,
            objectPointsList.Count,
            filePairs.Length,
            minValidPairs,
            calibrationResult.ReprojectionErrorStereo,
            calibrationResult.ReprojectionErrorLeft,
            calibrationResult.ReprojectionErrorRight,
            calibrationResult.LeftPerViewErrors,
            calibrationResult.RightPerViewErrors,
            validPairKeys,
            failedPairKeys);

        var bundle = CreateCalibrationBundleV2(
            imageSize,
            calibrationResult,
            rectificationResult,
            quality);

        var json = CalibrationBundleV2Json.Serialize(bundle);
        try
        {
            File.WriteAllText(outputPath, json);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to save calibration file to {Path}", outputPath);
        }

        // 创建可视化结果
        var resultImage = CreateCalibrationResultVisualization(
            successfulPairs[0].LeftFile, successfulPairs[0].RightFile, calibrationResult, rectificationResult);

        // 输出校正映射
        var outputData = new Dictionary<string, object>
        {
            { "CalibrationData", json },
            { "Accepted", quality.Accepted },
            { "CameraMatrixLeft", calibrationResult.CameraMatrixLeft },
            { "DistCoeffsLeft", calibrationResult.DistCoeffsLeft },
            { "CameraMatrixRight", calibrationResult.CameraMatrixRight },
            { "DistCoeffsRight", calibrationResult.DistCoeffsRight },
            { "RotationMatrix", calibrationResult.RotationMatrix },
            { "TranslationVector", calibrationResult.TranslationVector },
            { "EssentialMatrix", calibrationResult.EssentialMatrix },
            { "FundamentalMatrix", calibrationResult.FundamentalMatrix },
            { "LeftMap1", rectificationResult.LeftMap1 },
            { "LeftMap2", rectificationResult.LeftMap2 },
            { "RightMap1", rectificationResult.RightMap1 },
            { "RightMap2", rectificationResult.RightMap2 },
            { "LeftRectifiedRoi", rectificationResult.LeftValidRoi },
            { "RightRectifiedRoi", rectificationResult.RightValidRoi },
            { "ReprojectionErrorLeft", calibrationResult.ReprojectionErrorLeft },
            { "ReprojectionErrorRight", calibrationResult.ReprojectionErrorRight },
            { "ReprojectionErrorStereo", calibrationResult.ReprojectionErrorStereo },
            { "MaxPerViewErrorLeft", calibrationResult.MaxPerViewErrorLeft },
            { "MaxPerViewErrorRight", calibrationResult.MaxPerViewErrorRight },
            { "MaxPerViewError", quality.MaxError },
            { "EpipolarError", calibrationResult.EpipolarError },
            { "ValidPairs", objectPointsList.Count },
            { "TotalPairs", filePairs.Length },
            { "FailedPairs", failedPairKeys.Count },
            { "OutputPath", outputPath },
            { "Message", quality.Accepted
                ? $"Stereo calibration accepted. Valid pairs: {objectPointsList.Count}/{filePairs.Length}, Stereo RMS: {calibrationResult.ReprojectionErrorStereo:F4}"
                : $"Stereo calibration completed but did not pass quality acceptance. Valid pairs: {objectPointsList.Count}/{filePairs.Length}, Stereo RMS: {calibrationResult.ReprojectionErrorStereo:F4}, Max per-view: {quality.MaxError:F4}" }
        };

        // 清理资源（输出字典中保留的Mat除外）
        calibrationResult.CameraMatrixLeft = new Mat();
        calibrationResult.DistCoeffsLeft = new Mat();
        calibrationResult.CameraMatrixRight = new Mat();
        calibrationResult.DistCoeffsRight = new Mat();

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, outputData)));
    }

    private StereoCalibrationResult PerformStereoCalibration(
        List<Point3f[]> objectPoints,
        List<Point2f[]> leftImagePoints,
        List<Point2f[]> rightImagePoints,
        Size imageSize)
    {
        var result = new StereoCalibrationResult();

        try
        {
            // 转换为OpenCV格式
            var objectPointInputs = new InputArray[objectPoints.Count];
            var leftImagePointInputs = new InputArray[leftImagePoints.Count];
            var rightImagePointInputs = new InputArray[rightImagePoints.Count];
            for (var i = 0; i < objectPoints.Count; i++)
            {
                objectPointInputs[i] = InputArray.Create(objectPoints[i]);
                leftImagePointInputs[i] = InputArray.Create(leftImagePoints[i]);
                rightImagePointInputs[i] = InputArray.Create(rightImagePoints[i]);
            }

            result.CameraMatrixLeft = new Mat(3, 3, MatType.CV_64FC1);
            result.DistCoeffsLeft = new Mat();
            result.CameraMatrixRight = new Mat(3, 3, MatType.CV_64FC1);
            result.DistCoeffsRight = new Mat();
            result.RotationMatrix = new Mat(3, 3, MatType.CV_64FC1);
            result.TranslationVector = new Mat(3, 1, MatType.CV_64FC1);
            result.EssentialMatrix = new Mat(3, 3, MatType.CV_64FC1);
            result.FundamentalMatrix = new Mat(3, 3, MatType.CV_64FC1);

            // 执行双目标定
            var stereoRms = Cv2.StereoCalibrate(
                objectPointInputs,
                leftImagePointInputs,
                rightImagePointInputs,
                result.CameraMatrixLeft,
                result.DistCoeffsLeft,
                result.CameraMatrixRight,
                result.DistCoeffsRight,
                imageSize,
                result.RotationMatrix,
                result.TranslationVector,
                result.EssentialMatrix,
                result.FundamentalMatrix,
                CalibrationFlags.None,
                new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 100, 1e-5));

            result.ReprojectionErrorStereo = stereoRms;
            result.Success = true;

            // 计算单独的重投影误差
            var leftStats = CalculateReprojectionStats(
                objectPoints, leftImagePoints, result.CameraMatrixLeft, result.DistCoeffsLeft);
            var rightStats = CalculateReprojectionStats(
                objectPoints, rightImagePoints, result.CameraMatrixRight, result.DistCoeffsRight);

            result.ReprojectionErrorLeft = leftStats.MeanError;
            result.ReprojectionErrorRight = rightStats.MeanError;
            result.MaxPerViewErrorLeft = leftStats.MaxError;
            result.MaxPerViewErrorRight = rightStats.MaxError;
            result.LeftPerViewErrors = leftStats.PerViewErrors;
            result.RightPerViewErrors = rightStats.PerViewErrors;

            // 计算极线误差
            result.EpipolarError = CalculateEpipolarError(
                leftImagePoints, rightImagePoints, result.FundamentalMatrix);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            Logger.LogError(ex, "Stereo calibration failed");
        }

        return result;
    }

    private StereoRectificationResult PerformStereoRectification(
        StereoCalibrationResult calibration,
        Size imageSize,
        bool zeroDisparity,
        double alpha)
    {
        var result = new StereoRectificationResult();

        try
        {
            result.LeftRotation = new Mat(3, 3, MatType.CV_64FC1);
            result.RightRotation = new Mat(3, 3, MatType.CV_64FC1);
            result.LeftProjection = new Mat(3, 4, MatType.CV_64FC1);
            result.RightProjection = new Mat(3, 4, MatType.CV_64FC1);
            result.DisparityToDepth = new Mat(4, 4, MatType.CV_64FC1);

            // 执行立体校正
            var rectificationFlags = zeroDisparity ? StereoRectificationFlags.ZeroDisparity : (StereoRectificationFlags)0;
            Cv2.StereoRectify(
                calibration.CameraMatrixLeft,
                calibration.DistCoeffsLeft,
                calibration.CameraMatrixRight,
                calibration.DistCoeffsRight,
                imageSize,
                calibration.RotationMatrix,
                calibration.TranslationVector,
                result.LeftRotation,
                result.RightRotation,
                result.LeftProjection,
                result.RightProjection,
                result.DisparityToDepth,
                rectificationFlags,
                alpha,
                imageSize,
                out var leftValidRoi,
                out var rightValidRoi);

            result.LeftValidRoi = leftValidRoi;
            result.RightValidRoi = rightValidRoi;

            // 生成校正映射
            result.LeftMap1 = new Mat();
            result.LeftMap2 = new Mat();
            result.RightMap1 = new Mat();
            result.RightMap2 = new Mat();

            Cv2.InitUndistortRectifyMap(
                calibration.CameraMatrixLeft,
                calibration.DistCoeffsLeft,
                result.LeftRotation,
                result.LeftProjection,
                imageSize,
                MatType.CV_32FC1,
                result.LeftMap1,
                result.LeftMap2);

            Cv2.InitUndistortRectifyMap(
                calibration.CameraMatrixRight,
                calibration.DistCoeffsRight,
                result.RightRotation,
                result.RightProjection,
                imageSize,
                MatType.CV_32FC1,
                result.RightMap1,
                result.RightMap2);

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            Logger.LogError(ex, "Stereo rectification failed");
        }

        return result;
    }

    private ReprojectionErrorStats CalculateReprojectionStats(
        List<Point3f[]> objectPoints,
        List<Point2f[]> imagePoints,
        Mat cameraMatrix,
        Mat distCoeffs)
    {
        try
        {
            var totalError = 0.0;
            var totalPoints = 0;
            var perViewErrors = new List<double>(objectPoints.Count);

            for (int i = 0; i < objectPoints.Count; i++)
            {
                using var rvec = new Mat();
                using var tvec = new Mat();

                Cv2.SolvePnP(
                    InputArray.Create(objectPoints[i]),
                    InputArray.Create(imagePoints[i]),
                    cameraMatrix,
                    distCoeffs,
                    rvec,
                    tvec,
                    flags: SolvePnPFlags.Iterative);

                using var projectedPoints = new Mat();
                Cv2.ProjectPoints(
                    InputArray.Create(objectPoints[i]),
                    rvec,
                    tvec,
                    cameraMatrix,
                    distCoeffs,
                    projectedPoints);

                var projected = ReadPoint2fVector(projectedPoints);
                var original = imagePoints[i];
                var viewError = 0.0;

                for (int j = 0; j < projected.Length; j++)
                {
                    var error = Math.Sqrt(
                        Math.Pow(projected[j].X - original[j].X, 2) +
                        Math.Pow(projected[j].Y - original[j].Y, 2));
                    totalError += error;
                    viewError += error;
                    totalPoints++;
                }

                perViewErrors.Add(projected.Length > 0 ? viewError / projected.Length : 0.0);
            }

            var meanError = totalPoints > 0 ? totalError / totalPoints : 0.0;
            var maxError = perViewErrors.Count > 0 ? perViewErrors.Max() : 0.0;
            return new ReprojectionErrorStats(meanError, maxError, perViewErrors);
        }
        catch
        {
            return new ReprojectionErrorStats(-1.0, double.PositiveInfinity, Array.Empty<double>());
        }
    }

    private double CalculateEpipolarError(List<Point2f[]> leftPoints, List<Point2f[]> rightPoints, Mat fundamentalMatrix)
    {
        try
        {
            var totalError = 0.0;
            var totalPoints = 0;

            for (int i = 0; i < leftPoints.Count; i++)
            {
                var left = leftPoints[i];
                var right = rightPoints[i];

                for (int j = 0; j < left.Length && j < right.Length; j++)
                {
                    // 计算极线: l = F * p_left
                    var pl = new double[] { left[j].X, left[j].Y, 1 };
                    var line = new double[3];

                    for (int r = 0; r < 3; r++)
                    {
                        line[r] = 0;
                        for (int c = 0; c < 3; c++)
                        {
                            line[r] += fundamentalMatrix.At<double>(r, c) * pl[c];
                        }
                    }

                    // 点到极线的距离
                    var distance = Math.Abs(line[0] * right[j].X + line[1] * right[j].Y + line[2]) /
                                   Math.Sqrt(line[0] * line[0] + line[1] * line[1]);

                    totalError += distance;
                    totalPoints++;
                }
            }

            return totalPoints > 0 ? totalError / totalPoints : 0.0;
        }
        catch
        {
            return -1.0;
        }
    }

    private Mat CreateStereoVisualization(
        Mat leftMat, Mat rightMat,
        Point2f[] leftCorners, Point2f[] rightCorners,
        Size patternSize)
    {
        // 创建并排显示
        var result = new Mat(Math.Max(leftMat.Height, rightMat.Height),
            leftMat.Width + rightMat.Width, MatType.CV_8UC3);

        using var leftColor = leftMat.Channels() == 1
            ? new Mat()
            : leftMat.Clone();
        if (leftMat.Channels() == 1)
            Cv2.CvtColor(leftMat, leftColor, ColorConversionCodes.GRAY2BGR);

        using var rightColor = rightMat.Channels() == 1
            ? new Mat()
            : rightMat.Clone();
        if (rightMat.Channels() == 1)
            Cv2.CvtColor(rightMat, rightColor, ColorConversionCodes.GRAY2BGR);

        leftColor.CopyTo(result[new Rect(0, 0, leftMat.Width, leftMat.Height)]);
        rightColor.CopyTo(result[new Rect(leftMat.Width, 0, rightMat.Width, rightMat.Height)]);

        // 绘制角点
        foreach (var corner in leftCorners)
        {
            Cv2.Circle(result, (Point)corner, 3, new Scalar(0, 0, 255), -1);
        }

        foreach (var corner in rightCorners)
        {
            Cv2.Circle(result, new Point((int)corner.X + leftMat.Width, (int)corner.Y), 3, new Scalar(0, 0, 255), -1);
        }

        // 绘制连接线（验证极线对齐前的匹配）
        for (int i = 0; i < Math.Min(leftCorners.Length, rightCorners.Length); i += 5)
        {
            var pt1 = new Point((int)leftCorners[i].X, (int)leftCorners[i].Y);
            var pt2 = new Point((int)rightCorners[i].X + leftMat.Width, (int)rightCorners[i].Y);
            Cv2.Line(result, pt1, pt2, new Scalar(0, 255, 0), 1);
        }

        return result;
    }

    private Mat CreateCalibrationResultVisualization(
        string leftFirstFile, string rightFirstFile,
        StereoCalibrationResult calib, StereoRectificationResult rect)
    {
        try
        {
            using var leftImg = Cv2.ImRead(leftFirstFile, ImreadModes.Color);
            using var rightImg = Cv2.ImRead(rightFirstFile, ImreadModes.Color);

            if (leftImg.Empty() || rightImg.Empty())
            {
                return new Mat(480, 640, MatType.CV_8UC3, Scalar.Black);
            }

            // 应用校正映射
            using var leftRectified = new Mat();
            using var rightRectified = new Mat();

            Cv2.Remap(leftImg, leftRectified, rect.LeftMap1, rect.LeftMap2, InterpolationFlags.Linear);
            Cv2.Remap(rightImg, rightRectified, rect.RightMap1, rect.RightMap2, InterpolationFlags.Linear);

            // 创建并排显示
            var result = new Mat(leftRectified.Height, leftRectified.Width * 2, MatType.CV_8UC3);
            leftRectified.CopyTo(result[new Rect(0, 0, leftRectified.Width, leftRectified.Height)]);
            rightRectified.CopyTo(result[new Rect(leftRectified.Width, 0, rightRectified.Width, rightRectified.Height)]);

            // 绘制水平线验证极线对齐
            for (int y = 0; y < result.Height; y += 30)
            {
                Cv2.Line(result, new Point(0, y), new Point(result.Width, y), new Scalar(0, 255, 255), 1);
            }

            // 添加信息文本
            Cv2.PutText(result, $"Stereo RMS: {calib.ReprojectionErrorStereo:F3}",
                new Point(10, 30), HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 255, 0), 2);
            Cv2.PutText(result, $"Epi Error: {calib.EpipolarError:F3}px",
                new Point(10, 60), HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 255, 0), 2);

            return result;
        }
        catch
        {
            return new Mat(480, 640, MatType.CV_8UC3, Scalar.Black);
        }
    }

    private CalibrationBundleV2 CreateCalibrationBundleV2(
        Size imageSize,
        StereoCalibrationResult calib, StereoRectificationResult rect,
        CalibrationQualityV2 quality)
    {
        var leftDistCoeffs = FlattenMat(calib.DistCoeffsLeft);
        var rightDistCoeffs = FlattenMat(calib.DistCoeffsRight);

        return new CalibrationBundleV2
        {
            CalibrationKind = CalibrationKindV2.StereoRig,
            TransformModel = TransformModelV2.StereoRig,
            SourceFrame = "left_camera",
            TargetFrame = "right_camera",
            Unit = "mm",
            ImageSize = new CalibrationImageSizeV2
            {
                Width = imageSize.Width,
                Height = imageSize.Height
            },
            Stereo = new StereoCalibrationDataV2
            {
                LeftIntrinsics = new CalibrationIntrinsicsV2
                {
                    CameraMatrix = ToJaggedMatrix(calib.CameraMatrixLeft)
                },
                RightIntrinsics = new CalibrationIntrinsicsV2
                {
                    CameraMatrix = ToJaggedMatrix(calib.CameraMatrixRight)
                },
                LeftDistortion = new CalibrationDistortionV2
                {
                    Model = DistortionModelV2.BrownConrady,
                    Coefficients = leftDistCoeffs
                },
                RightDistortion = new CalibrationDistortionV2
                {
                    Model = DistortionModelV2.BrownConrady,
                    Coefficients = rightDistCoeffs
                },
                Rotation = ToJaggedMatrix(calib.RotationMatrix),
                Translation = FlattenMat(calib.TranslationVector),
                Essential = ToJaggedMatrix(calib.EssentialMatrix),
                Fundamental = ToJaggedMatrix(calib.FundamentalMatrix),
                Q = ToJaggedMatrix(rect.DisparityToDepth)
            },
            Quality = quality,
            ProducerOperator = nameof(StereoCalibrationOperator)
        };
    }

    private OperatorExecutionOutput CreateFailureOutput(Mat input, string message)
    {
        var output = input.Clone();
        Cv2.PutText(output, $"NG: {message}", new Point(10, 30),
            HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 0, 255), 2);

        return OperatorExecutionOutput.Success(CreateImageOutput(output, new Dictionary<string, object>
        {
            { "Found", false },
            { "Message", message }
        }));
    }

    private static string[] GetImageFiles(string folder)
    {
        return Directory.GetFiles(folder, "*.png")
            .Concat(Directory.GetFiles(folder, "*.jpg"))
            .Concat(Directory.GetFiles(folder, "*.jpeg"))
            .Concat(Directory.GetFiles(folder, "*.bmp"))
            .ToArray();
    }

    private static bool TryCreateStereoFilePairs(
        IReadOnlyList<string> leftFiles,
        IReadOnlyList<string> rightFiles,
        out StereoImagePair[] pairs,
        out string error)
    {
        pairs = Array.Empty<StereoImagePair>();
        error = string.Empty;

        if (!TryIndexStereoFiles(leftFiles, "Left", out var leftIndex, out error) ||
            !TryIndexStereoFiles(rightFiles, "Right", out var rightIndex, out error))
        {
            return false;
        }

        var missingOnRight = leftIndex.Keys.Except(rightIndex.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var missingOnLeft = rightIndex.Keys.Except(leftIndex.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (missingOnRight.Length > 0 || missingOnLeft.Length > 0)
        {
            error = $"Stereo pair mismatch detected. Missing on right: {JoinKeys(missingOnRight)}. Missing on left: {JoinKeys(missingOnLeft)}.";
            return false;
        }

        pairs = leftIndex.Keys
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .Select(key => new StereoImagePair(key, leftIndex[key], rightIndex[key]))
            .ToArray();

        return true;
    }

    private static bool TryIndexStereoFiles(
        IReadOnlyList<string> files,
        string side,
        out Dictionary<string, string> index,
        out string error)
    {
        index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        error = string.Empty;

        foreach (var file in files)
        {
            var stableKey = CreateStablePairKey(Path.GetFileNameWithoutExtension(file));
            if (index.TryGetValue(stableKey, out var existing))
            {
                error = $"{side} folder contains duplicate stereo key '{stableKey}' from '{Path.GetFileName(existing)}' and '{Path.GetFileName(file)}'.";
                return false;
            }

            index[stableKey] = file;
        }

        return true;
    }

    private static string CreateStablePairKey(string? baseName)
    {
        if (string.IsNullOrWhiteSpace(baseName))
        {
            return string.Empty;
        }

        var tokens = baseName
            .Split(new[] { '_', '-', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => !IsStereoSideToken(token))
            .Select(token => token.ToLowerInvariant())
            .ToArray();

        return tokens.Length == 0
            ? baseName.Trim().ToLowerInvariant()
            : string.Join("_", tokens);
    }

    private static bool IsStereoSideToken(string token)
    {
        return token.Equals("left", StringComparison.OrdinalIgnoreCase) ||
               token.Equals("right", StringComparison.OrdinalIgnoreCase) ||
               token.Equals("l", StringComparison.OrdinalIgnoreCase) ||
               token.Equals("r", StringComparison.OrdinalIgnoreCase) ||
               token.Equals("cam0", StringComparison.OrdinalIgnoreCase) ||
               token.Equals("cam1", StringComparison.OrdinalIgnoreCase) ||
               token.Equals("camera0", StringComparison.OrdinalIgnoreCase) ||
               token.Equals("camera1", StringComparison.OrdinalIgnoreCase);
    }

    private static string JoinKeys(IReadOnlyList<string> keys)
    {
        return keys.Count == 0 ? "none" : string.Join(", ", keys.Take(10));
    }

    private static CalibrationQualityV2 EvaluateStereoCalibrationQuality(
        string patternType,
        Size patternSize,
        double squareSize,
        int validPairs,
        int totalPairs,
        int minValidPairs,
        double stereoMeanError,
        double leftMeanError,
        double rightMeanError,
        IReadOnlyList<double> leftPerViewErrors,
        IReadOnlyList<double> rightPerViewErrors,
        IReadOnlyList<string> pairKeys,
        IReadOnlyList<string>? failedPairKeys)
    {
        var diagnostics = new List<string>
        {
            $"PatternType={patternType}",
            $"Board={patternSize.Width}x{patternSize.Height}",
            $"SquareSize={squareSize.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)}",
            $"ValidPairs={validPairs}",
            $"TotalPairs={totalPairs}",
            $"FailedPairs={failedPairKeys?.Count ?? 0}",
            $"StereoRms={stereoMeanError:F4}",
            $"LeftMeanError={leftMeanError:F4}",
            $"RightMeanError={rightMeanError:F4}"
        };

        if (failedPairKeys is { Count: > 0 })
        {
            diagnostics.Add($"Rejected samples: {JoinKeys(failedPairKeys)}");
        }

        var maxPerViewErrorLeft = leftPerViewErrors.Count > 0 ? leftPerViewErrors.Max() : 0.0;
        var maxPerViewErrorRight = rightPerViewErrors.Count > 0 ? rightPerViewErrors.Max() : 0.0;
        var maxPerViewError = Math.Max(maxPerViewErrorLeft, maxPerViewErrorRight);
        diagnostics.Add($"MaxPerViewError={maxPerViewError:F4}");

        var perViewOutliers = pairKeys
            .Select((key, index) => new
            {
                Key = key,
                Error = Math.Max(
                    index < leftPerViewErrors.Count ? leftPerViewErrors[index] : double.PositiveInfinity,
                    index < rightPerViewErrors.Count ? rightPerViewErrors[index] : double.PositiveInfinity)
            })
            .Where(item => !double.IsFinite(item.Error) || item.Error > PerViewErrorAcceptanceThreshold)
            .OrderByDescending(item => item.Error)
            .Take(10)
            .Select(item => $"{item.Key}={item.Error:F4}px")
            .ToArray();

        if (perViewOutliers.Length > 0)
        {
            diagnostics.Add($"Per-view outliers: {string.Join(", ", perViewOutliers)}");
        }

        var metricsAreFinite =
            double.IsFinite(stereoMeanError) &&
            double.IsFinite(leftMeanError) &&
            double.IsFinite(rightMeanError) &&
            double.IsFinite(maxPerViewError);

        var accepted =
            validPairs >= minValidPairs &&
            metricsAreFinite &&
            stereoMeanError <= StereoMeanErrorAcceptanceThreshold &&
            Math.Max(leftMeanError, rightMeanError) <= CameraMeanErrorAcceptanceThreshold &&
            maxPerViewError <= PerViewErrorAcceptanceThreshold;

        diagnostics.Add(accepted
            ? "Quality gate passed."
            : $"Quality gate failed. Thresholds: stereoMean<={StereoMeanErrorAcceptanceThreshold:F2}, cameraMean<={CameraMeanErrorAcceptanceThreshold:F2}, perView<={PerViewErrorAcceptanceThreshold:F2}, minPairs>={minValidPairs}.");

        return new CalibrationQualityV2
        {
            Accepted = accepted,
            MeanError = stereoMeanError,
            MaxError = maxPerViewError,
            InlierCount = validPairs,
            TotalSampleCount = totalPairs,
            Diagnostics = diagnostics
        };
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

    private static double[][] ToJaggedMatrix(Mat mat)
    {
        if (mat.Empty()) return Array.Empty<double[]>();

        var rows = mat.Rows;
        var cols = mat.Cols;
        var result = new double[rows][];

        for (int i = 0; i < rows; i++)
        {
            result[i] = new double[cols];
            for (int j = 0; j < cols; j++)
            {
                result[i][j] = mat.At<double>(i, j);
            }
        }

        return result;
    }

    private static double[] FlattenMat(Mat mat)
    {
        if (mat.Empty()) return Array.Empty<double>();

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

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var boardWidth = GetIntParam(@operator, "BoardWidth", 9);
        var boardHeight = GetIntParam(@operator, "BoardHeight", 6);
        var squareSize = GetDoubleParam(@operator, "SquareSize", 25.0);
        var mode = GetStringParam(@operator, "Mode", "SinglePair");
        var minValidPairs = GetIntParam(@operator, "MinValidPairs", 12);

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

        if (!mode.Equals("SinglePair", StringComparison.OrdinalIgnoreCase) &&
            !mode.Equals("FolderCalibration", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Mode must be SinglePair or FolderCalibration.");
        }

        if (minValidPairs < 3)
        {
            return ValidationResult.Invalid("MinValidPairs must be at least 3.");
        }

        return ValidationResult.Valid();
    }

    private class StereoCalibrationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Mat CameraMatrixLeft { get; set; } = new();
        public Mat DistCoeffsLeft { get; set; } = new();
        public Mat CameraMatrixRight { get; set; } = new();
        public Mat DistCoeffsRight { get; set; } = new();
        public Mat RotationMatrix { get; set; } = new();
        public Mat TranslationVector { get; set; } = new();
        public Mat EssentialMatrix { get; set; } = new();
        public Mat FundamentalMatrix { get; set; } = new();
        public double ReprojectionErrorLeft { get; set; }
        public double ReprojectionErrorRight { get; set; }
        public double ReprojectionErrorStereo { get; set; }
        public double MaxPerViewErrorLeft { get; set; }
        public double MaxPerViewErrorRight { get; set; }
        public IReadOnlyList<double> LeftPerViewErrors { get; set; } = Array.Empty<double>();
        public IReadOnlyList<double> RightPerViewErrors { get; set; } = Array.Empty<double>();
        public double EpipolarError { get; set; }
    }

    private class StereoRectificationResult
    {
        public bool Success { get; set; }
        public Mat LeftRotation { get; set; } = new();
        public Mat RightRotation { get; set; } = new();
        public Mat LeftProjection { get; set; } = new();
        public Mat RightProjection { get; set; } = new();
        public Mat DisparityToDepth { get; set; } = new();
        public Mat LeftMap1 { get; set; } = new();
        public Mat LeftMap2 { get; set; } = new();
        public Mat RightMap1 { get; set; } = new();
        public Mat RightMap2 { get; set; } = new();
        public Rect LeftValidRoi { get; set; }
        public Rect RightValidRoi { get; set; }
    }

    private sealed class StereoCalibrationPayload
    {
        public string PatternType { get; set; } = string.Empty;
        public int BoardWidth { get; set; }
        public int BoardHeight { get; set; }
        public double SquareSize { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public int ImageCount { get; set; }
        public int TotalPairs { get; set; }
        public List<int> FailedPairIndices { get; set; } = new();
        public bool Found { get; set; }
        public DateTime Timestamp { get; set; }
        public List<Point2Payload>? LeftCorners { get; set; }
        public List<Point2Payload>? RightCorners { get; set; }
        public double[][] CameraMatrixLeft { get; set; } = Array.Empty<double[]>();
        public double[] DistCoeffsLeft { get; set; } = Array.Empty<double>();
        public double[][] CameraMatrixRight { get; set; } = Array.Empty<double[]>();
        public double[] DistCoeffsRight { get; set; } = Array.Empty<double>();
        public double[][] RotationMatrix { get; set; } = Array.Empty<double[]>();
        public double[] TranslationVector { get; set; } = Array.Empty<double>();
        public double[][] EssentialMatrix { get; set; } = Array.Empty<double[]>();
        public double[][] FundamentalMatrix { get; set; } = Array.Empty<double[]>();
        public double ReprojectionErrorLeft { get; set; }
        public double ReprojectionErrorRight { get; set; }
        public double ReprojectionErrorStereo { get; set; }
        public double EpipolarError { get; set; }
        public int[] LeftValidRoi { get; set; } = Array.Empty<int>();
        public int[] RightValidRoi { get; set; } = Array.Empty<int>();
        public string Message { get; set; } = string.Empty;
    }

    private readonly record struct Point2Payload(double X, double Y);
    private readonly record struct StereoImagePair(string StableKey, string LeftFile, string RightFile);
    private readonly record struct ReprojectionErrorStats(double MeanError, double MaxError, IReadOnlyList<double> PerViewErrors);

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
}
