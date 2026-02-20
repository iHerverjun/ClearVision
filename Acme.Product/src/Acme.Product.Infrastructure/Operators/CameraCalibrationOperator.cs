// CameraCalibrationOperator.cs
// 相机标定算子 - 棋盘格// 功能实现圆点标定
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System.Text.Json;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 相机标定算子 - 棋盘格/圆点标定
/// </summary>
public class CameraCalibrationOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.CameraCalibration;

    public CameraCalibrationOperator(ILogger<CameraCalibrationOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        // 获取参数
        var patternType = GetStringParam(@operator, "PatternType", "Chessboard");
        var boardWidth = GetIntParam(@operator, "BoardWidth", 9, 2, 30);
        var boardHeight = GetIntParam(@operator, "BoardHeight", 6, 2, 30);
        var squareSize = GetDoubleParam(@operator, "SquareSize", 25.0, 0.1, 1000.0);
        var mode = GetStringParam(@operator, "Mode", "SingleImage");
        var imageFolder = GetStringParam(@operator, "ImageFolder", "");
        var calibrationOutputPath = GetStringParam(@operator, "CalibrationOutputPath", "calibration_result.json");

        var patternSize = new Size(boardWidth, boardHeight);

        if (mode == "FolderCalibration")
        {
            return ExecuteFolderCalibration(patternType, patternSize, squareSize, imageFolder, calibrationOutputPath, cancellationToken);
        }
        else
        {
            return ExecuteSingleImageCalibration(@operator, inputs, patternType, patternSize, squareSize, cancellationToken);
        }
    }

    private Task<OperatorExecutionOutput> ExecuteSingleImageCalibration(
        Operator @operator,
        Dictionary<string, object>? inputs,
        string patternType,
        Size patternSize,
        double squareSize,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供输入图像"));
        }

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        // 创建结果图像
        var resultImage = src.Clone();

        // 转换为灰度图
        using var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        // 查找角点
        Point2f[]? corners = null;
        bool found = false;

        if (patternType == "Chessboard")
        {
            found = Cv2.FindChessboardCorners(gray, patternSize, out corners,
                ChessboardFlags.AdaptiveThresh | ChessboardFlags.NormalizeImage | ChessboardFlags.FastCheck);
        }
        else if (patternType == "CircleGrid")
        {
            found = Cv2.FindCirclesGrid(gray, patternSize, out corners,
                FindCirclesGridFlags.SymmetricGrid);
        }

        if (!found || corners == null || corners.Length == 0)
        {
            // 未找到标定板，返回原始图像
            var additionalData = new Dictionary<string, object>
            {
                { "CalibrationData", "" },
                { "Found", false },
                { "Message", "未检测到标定板" }
            };
            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(src, additionalData)));
        }

        // 精细化角点位置
        if (patternType == "Chessboard")
        {
            Cv2.CornerSubPix(gray, corners, new Size(11, 11), new Size(-1, -1),
                new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 30, 0.1));
        }

        // 绘制角点
        Cv2.DrawChessboardCorners(resultImage, patternSize, corners, found);

        // 准备世界坐标系中的3D点
        var objectPoints = new List<Point3f>();
        for (int i = 0; i < patternSize.Height; i++)
        {
            for (int j = 0; j < patternSize.Width; j++)
            {
                objectPoints.Add(new Point3f(j * (float)squareSize, i * (float)squareSize, 0));
            }
        }

        // 创建标定数据结构
        var calibrationData = new
        {
            PatternType = patternType,
            BoardWidth = patternSize.Width,
            BoardHeight = patternSize.Height,
            SquareSize = squareSize,
            ImageWidth = src.Width,
            ImageHeight = src.Height,
            Corners = corners.Select(c => new { X = c.X, Y = c.Y }).ToArray(),
            ObjectPoints = objectPoints.Select(p => new { X = p.X, Y = p.Y, Z = p.Z }).ToArray(),
            Found = true,
            Timestamp = DateTime.UtcNow
        };

        var calibrationJson = JsonSerializer.Serialize(calibrationData, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        // 显示信息
        Cv2.PutText(resultImage, $"Corners: {corners.Length}", new Point(10, 30),
            HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 255, 0), 2);

        var outputData = new Dictionary<string, object>
        {
            { "CalibrationData", calibrationJson },
            { "Found", true },
            { "CornerCount", corners.Length },
            { "Message", $"检测到 {corners.Length} 个角点" }
        };
        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, outputData)));
    }

    private Task<OperatorExecutionOutput> ExecuteFolderCalibration(
        string patternType,
        Size patternSize,
        double squareSize,
        string imageFolder,
        string outputPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(imageFolder) || !Directory.Exists(imageFolder))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("标定图片文件夹不存在"));
        }

        // 获取图片文件
        var imageFiles = Directory.GetFiles(imageFolder, "*.png")
            .Concat(Directory.GetFiles(imageFolder, "*.jpg"))
            .Concat(Directory.GetFiles(imageFolder, "*.jpeg"))
            .Concat(Directory.GetFiles(imageFolder, "*.bmp"))
            .ToArray();

        if (imageFiles.Length == 0)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("文件夹中没有找到图片文件"));
        }

        var objectPoints = new List<Mat>();
        var imagePoints = new List<Mat>();
        Size imageSize = default;
        int successCount = 0;
        var failedFiles = new List<string>();

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
                    imageSize = img.Size();

                Point2f[]? corners = null;
                bool found = false;

                if (patternType == "Chessboard")
                {
                    found = Cv2.FindChessboardCorners(img, patternSize, out corners,
                        ChessboardFlags.AdaptiveThresh | ChessboardFlags.NormalizeImage | ChessboardFlags.FastCheck);
                }
                else if (patternType == "CircleGrid")
                {
                    found = Cv2.FindCirclesGrid(img, patternSize, out corners,
                        FindCirclesGridFlags.SymmetricGrid);
                }

                if (found && corners != null && corners.Length > 0)
                {
                    // 精细化角点
                    if (patternType == "Chessboard")
                    {
                        Cv2.CornerSubPix(img, corners, new Size(11, 11), new Size(-1, -1),
                            new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 30, 0.001));
                    }

                    imagePoints.Add(Mat.FromArray(corners));

                    // 构建世界坐标
                    var objPts = new Point3f[patternSize.Width * patternSize.Height];
                    for (int j = 0; j < patternSize.Height; j++)
                        for (int i = 0; i < patternSize.Width; i++)
                            objPts[j * patternSize.Width + i] = new Point3f(i * (float)squareSize, j * (float)squareSize, 0);
                    objectPoints.Add(Mat.FromArray(objPts));

                    successCount++;
                }
                else
                {
                    failedFiles.Add(Path.GetFileName(file));
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "处理文件 {File} 时出错", file);
                failedFiles.Add(Path.GetFileName(file));
            }
        }

        if (objectPoints.Count < 3)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"成功检测的标定板图片不足，只有 {objectPoints.Count} 张，需要至少 3 张"));
        }

        // 执行标定
        using var cameraMatrix = new Mat();
        using var distCoeffs = new Mat();
        Mat[]? rvecs = null;
        Mat[]? tvecs = null;

        var reprojError = Cv2.CalibrateCamera(
            objectPoints, imagePoints, imageSize,
            cameraMatrix, distCoeffs,
            out rvecs, out tvecs,
            CalibrationFlags.None,
            new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 30, 1e-6));

        // 释放临时Mat数组
        if (rvecs != null)
        {
            foreach (var rvec in rvecs) rvec?.Dispose();
        }
        if (tvecs != null)
        {
            foreach (var tvec in tvecs) tvec?.Dispose();
        }

        // 构建标定结果数据
        var calibData = new CalibrationResult
        {
            PatternType = patternType,
            BoardWidth = patternSize.Width,
            BoardHeight = patternSize.Height,
            SquareSize = squareSize,
            ImageWidth = imageSize.Width,
            ImageHeight = imageSize.Height,
            CameraMatrix = new double[,]
            {
                { cameraMatrix.At<double>(0, 0), cameraMatrix.At<double>(0, 1), cameraMatrix.At<double>(0, 2) },
                { cameraMatrix.At<double>(1, 0), cameraMatrix.At<double>(1, 1), cameraMatrix.At<double>(1, 2) },
                { cameraMatrix.At<double>(2, 0), cameraMatrix.At<double>(2, 1), cameraMatrix.At<double>(2, 2) }
            },
            DistCoeffs = Enumerable.Range(0, distCoeffs.Cols)
                .Select(i => distCoeffs.At<double>(0, i))
                .ToArray(),
            ReprojectionError = reprojError,
            ImageCount = successCount,
            FailedFiles = failedFiles,
            Timestamp = DateTime.UtcNow
        };

        // 保存 JSON
        var json = JsonSerializer.Serialize(calibData, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        try
        {
            File.WriteAllText(outputPath, json);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "保存标定结果到 {Path} 失败", outputPath);
        }

        // 创建结果图像（使用最后一张成功处理的图片作为背景）
        using var lastImg = Cv2.ImRead(imageFiles.Last(), ImreadModes.Color);
        var resultImage = lastImg.Clone();

        // 显示标定结果信息
        var info = $"Images: {successCount}/{imageFiles.Length}, Error: {reprojError:F4}";
        Cv2.PutText(resultImage, info, new Point(10, 30),
            HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 255, 0), 2);

        var additionalData = new Dictionary<string, object>
        {
            { "CalibrationData", json },
            { "ReprojectionError", reprojError },
            { "ImageCount", successCount },
            { "TotalImages", imageFiles.Length },
            { "OutputPath", outputPath },
            { "Message", $"标定完成，使用了 {successCount}/{imageFiles.Length} 张图片，重投影误差: {reprojError:F4}" }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, additionalData)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var boardWidth = GetIntParam(@operator, "BoardWidth", 9);
        var boardHeight = GetIntParam(@operator, "BoardHeight", 6);
        var squareSize = GetDoubleParam(@operator, "SquareSize", 25.0);
        var mode = GetStringParam(@operator, "Mode", "SingleImage");

        if (boardWidth < 2 || boardWidth > 30)
        {
            return ValidationResult.Invalid("棋盘格宽度必须在 2-30 之间");
        }
        if (boardHeight < 2 || boardHeight > 30)
        {
            return ValidationResult.Invalid("棋盘格高度必须在 2-30 之间");
        }
        if (squareSize <= 0 || squareSize > 1000)
        {
            return ValidationResult.Invalid("方格尺寸必须在 0-1000 mm 之间");
        }

        if (mode != "SingleImage" && mode != "FolderCalibration")
        {
            return ValidationResult.Invalid("模式必须是 SingleImage 或 FolderCalibration");
        }

        return ValidationResult.Valid();
    }

    private class CalibrationResult
    {
        public string PatternType { get; set; } = "";
        public int BoardWidth { get; set; }
        public int BoardHeight { get; set; }
        public double SquareSize { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public double[,] CameraMatrix { get; set; } = new double[3, 3];
        public double[] DistCoeffs { get; set; } = Array.Empty<double>();
        public double ReprojectionError { get; set; }
        public int ImageCount { get; set; }
        public List<string> FailedFiles { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }
}
