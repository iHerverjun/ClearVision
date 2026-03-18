// StereoCalibrationOperator.cs
// 双目标定与立体校正算子
// 对标 Halcon: binocular_calibration / gen_binocular_rectification_map
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

        var objectPoints = CreateObjectPoints(patternSize, squareSize);
        var imageSize = leftGray.Size();

        // 单个图像对无法进行完整双目标定，只能验证角点检测
        var resultImage = CreateStereoVisualization(leftMat, rightMat, leftCorners, rightCorners, patternSize);

        var payload = new StereoCalibrationPayload
        {
            PatternType = patternType,
            BoardWidth = patternSize.Width,
            BoardHeight = patternSize.Height,
            SquareSize = squareSize,
            ImageWidth = imageSize.Width,
            ImageHeight = imageSize.Height,
            ImageCount = 1,
            Found = true,
            Message = "Single pair mode: corner detection successful. Use FolderCalibration for full stereo calibration.",
            LeftCorners = leftCorners.Select(c => new Point2Payload(c.X, c.Y)).ToList(),
            RightCorners = rightCorners.Select(c => new Point2Payload(c.X, c.Y)).ToList(),
            Timestamp = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, new Dictionary<string, object>
        {
            { "CalibrationData", json },
            { "Found", true },
            { "LeftCornerCount", leftCorners.Length },
            { "RightCornerCount", rightCorners.Length },
            { "Message", payload.Message }
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

        var leftFiles = GetImageFiles(leftFolder).OrderBy(f => f).ToArray();
        var rightFiles = GetImageFiles(rightFolder).OrderBy(f => f).ToArray();

        if (leftFiles.Length == 0 || rightFiles.Length == 0)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("No calibration images found in folders."));
        }

        if (leftFiles.Length != rightFiles.Length)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"Left and right folders must have same number of images. Left: {leftFiles.Length}, Right: {rightFiles.Length}"));
        }

        var objectPointsList = new List<Point3f[]>();
        var leftImagePointsList = new List<Point2f[]>();
        var rightImagePointsList = new List<Point2f[]>();
        var failedPairs = new List<int>();
        Size imageSize = default;

        for (int i = 0; i < leftFiles.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var leftImg = Cv2.ImRead(leftFiles[i], ImreadModes.Grayscale);
                using var rightImg = Cv2.ImRead(rightFiles[i], ImreadModes.Grayscale);

                if (leftImg.Empty() || rightImg.Empty())
                {
                    failedPairs.Add(i);
                    Logger.LogWarning("Failed to load image pair {Index}: {Left}, {Right}", i, leftFiles[i], rightFiles[i]);
                    continue;
                }

                if (imageSize == default)
                {
                    imageSize = leftImg.Size();
                }

                if (!TryFindCalibrationCorners(leftImg, patternType, patternSize, out var leftCorners) || leftCorners.Length == 0)
                {
                    failedPairs.Add(i);
                    Logger.LogWarning("Pattern not found in left image: {File}", leftFiles[i]);
                    continue;
                }

                if (!TryFindCalibrationCorners(rightImg, patternType, patternSize, out var rightCorners) || rightCorners.Length == 0)
                {
                    failedPairs.Add(i);
                    Logger.LogWarning("Pattern not found in right image: {File}", rightFiles[i]);
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

                Logger.LogDebug("Successfully processed pair {Index}/{Total}", i + 1, leftFiles.Length);
            }
            catch (Exception ex)
            {
                failedPairs.Add(i);
                Logger.LogWarning(ex, "Failed to process image pair {Index}", i);
            }
        }

        if (objectPointsList.Count < minValidPairs)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(
                $"Need at least {minValidPairs} valid image pairs, got {objectPointsList.Count}. Failed pairs: {failedPairs.Count}"));
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

        // 保存结果
        var payload = CreateCalibrationPayload(
            patternType, patternSize, squareSize, imageSize,
            objectPointsList.Count, leftFiles.Length, failedPairs,
            calibrationResult, rectificationResult);

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
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
            leftFiles.First(), rightFiles.First(), calibrationResult, rectificationResult);

        // 输出校正映射
        var outputData = new Dictionary<string, object>
        {
            { "CalibrationData", json },
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
            { "EpipolarError", calibrationResult.EpipolarError },
            { "ValidPairs", objectPointsList.Count },
            { "TotalPairs", leftFiles.Length },
            { "FailedPairs", failedPairs.Count },
            { "OutputPath", outputPath },
            { "Message", $"Stereo calibration completed. Valid pairs: {objectPointsList.Count}/{leftFiles.Length}, Stereo RMS: {calibrationResult.ReprojectionErrorStereo:F4}" }
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
            result.ReprojectionErrorLeft = CalculateReprojectionError(
                objectPoints, leftImagePoints, result.CameraMatrixLeft, result.DistCoeffsLeft);
            result.ReprojectionErrorRight = CalculateReprojectionError(
                objectPoints, rightImagePoints, result.CameraMatrixRight, result.DistCoeffsRight);

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
                StereoRectificationFlags.ZeroDisparity,
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

    private double CalculateReprojectionError(
        List<Point3f[]> objectPoints,
        List<Point2f[]> imagePoints,
        Mat cameraMatrix,
        Mat distCoeffs)
    {
        try
        {
            var totalError = 0.0;
            var totalPoints = 0;

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

                for (int j = 0; j < projected.Length; j++)
                {
                    var error = Math.Sqrt(
                        Math.Pow(projected[j].X - original[j].X, 2) +
                        Math.Pow(projected[j].Y - original[j].Y, 2));
                    totalError += error;
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

    private StereoCalibrationPayload CreateCalibrationPayload(
        string patternType, Size patternSize, double squareSize, Size imageSize,
        int validPairs, int totalPairs, List<int> failedPairs,
        StereoCalibrationResult calib, StereoRectificationResult rect)
    {
        return new StereoCalibrationPayload
        {
            PatternType = patternType,
            BoardWidth = patternSize.Width,
            BoardHeight = patternSize.Height,
            SquareSize = squareSize,
            ImageWidth = imageSize.Width,
            ImageHeight = imageSize.Height,
            ImageCount = validPairs,
            TotalPairs = totalPairs,
            FailedPairIndices = failedPairs,
            Found = true,
            Timestamp = DateTime.UtcNow,
            CameraMatrixLeft = ToJaggedMatrix(calib.CameraMatrixLeft),
            DistCoeffsLeft = FlattenMat(calib.DistCoeffsLeft),
            CameraMatrixRight = ToJaggedMatrix(calib.CameraMatrixRight),
            DistCoeffsRight = FlattenMat(calib.DistCoeffsRight),
            RotationMatrix = ToJaggedMatrix(calib.RotationMatrix),
            TranslationVector = FlattenMat(calib.TranslationVector),
            EssentialMatrix = ToJaggedMatrix(calib.EssentialMatrix),
            FundamentalMatrix = ToJaggedMatrix(calib.FundamentalMatrix),
            ReprojectionErrorLeft = calib.ReprojectionErrorLeft,
            ReprojectionErrorRight = calib.ReprojectionErrorRight,
            ReprojectionErrorStereo = calib.ReprojectionErrorStereo,
            EpipolarError = calib.EpipolarError,
            LeftValidRoi = new[] { rect.LeftValidRoi.X, rect.LeftValidRoi.Y, rect.LeftValidRoi.Width, rect.LeftValidRoi.Height },
            RightValidRoi = new[] { rect.RightValidRoi.X, rect.RightValidRoi.Y, rect.RightValidRoi.Width, rect.RightValidRoi.Height },
            Message = $"Stereo calibration successful. Valid pairs: {validPairs}/{totalPairs}"
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
