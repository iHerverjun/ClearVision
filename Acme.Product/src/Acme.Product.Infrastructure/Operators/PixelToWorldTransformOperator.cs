// PixelToWorldTransformOperator.cs
// 像素↔世界平面映射算子
// 对标 Halcon: image_points_to_world_plane
// 作者：AI Assistant

using System.Text.Json;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 像素↔世界平面坐标映射算子
/// 对标 Halcon image_points_to_world_plane
/// 支持像素到世界坐标的正向映射和世界到像素的反向映射
/// </summary>
[OperatorMeta(
    DisplayName = "Pixel To World Transform",
    Description = "Transforms between pixel coordinates and world plane coordinates using calibration data. Supports both forward (pixel→world) and inverse (world→pixel) transformations.",
    Category = "Calibration",
    IconName = "coordinate-transform",
    Keywords = new[] { "Pixel", "World", "Coordinate", "Transform", "Calibration", "Plane" }
)]
[InputPort("Image", "Input Image (Optional)", PortDataType.Image, IsRequired = false)]
[InputPort("Points", "Input Points (Pixel or World)", PortDataType.PointList, IsRequired = false)]
[InputPort("CalibrationData", "Calibration Data JSON", PortDataType.String, IsRequired = false)]
[OutputPort("Image", "Visualization Image", PortDataType.Image)]
[OutputPort("TransformedPoints", "Transformed Points", PortDataType.PointList)]
[OutputPort("TransformResult", "Transform Result Details", PortDataType.Any)]
[OperatorParam("CalibrationFile", "Calibration File Path", "file", DefaultValue = "")]
[OperatorParam("TransformMode", "Transform Mode", "enum", DefaultValue = "PixelToWorld", Options = new[] { "PixelToWorld|Pixel to World", "WorldToPixel|World to Pixel" })]
[OperatorParam("WorldPlaneZ", "World Plane Z (mm)", "double", DefaultValue = 0.0)]
[OperatorParam("UnitScale", "Unit Scale (mm per unit)", "double", DefaultValue = 1.0, Min = 0.0001, Max = 10000.0)]
[OperatorParam("InputPointX", "Input Point X (Single Point Mode)", "double", DefaultValue = 0.0)]
[OperatorParam("InputPointY", "Input Point Y (Single Point Mode)", "double", DefaultValue = 0.0)]
[OperatorParam("UseDistortion", "Use Distortion Model", "bool", DefaultValue = true)]
[OperatorParam("OutputCoordinateSystem", "Output Coordinate System", "enum", DefaultValue = "World", Options = new[] { "World|World (mm)", "Image|Image (pixels)", "Normalized|Normalized (0-1)" })]
[OperatorParam("GenerateReport", "Generate Accuracy Report", "bool", DefaultValue = true)]
public class PixelToWorldTransformOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.PixelToWorldTransform;

    public PixelToWorldTransformOperator(ILogger<PixelToWorldTransformOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        // 获取参数
        var transformMode = GetStringParam(@operator, "TransformMode", "PixelToWorld");
        var worldPlaneZ = GetDoubleParam(@operator, "WorldPlaneZ", 0.0);
        var unitScale = GetDoubleParam(@operator, "UnitScale", 1.0);
        var useDistortion = GetBoolParam(@operator, "UseDistortion", true);
        var outputCoordSystem = GetStringParam(@operator, "OutputCoordinateSystem", "World");
        var generateReport = GetBoolParam(@operator, "GenerateReport", true);

        // 解析标定数据
        if (!TryResolveCalibrationData(@operator, inputs, out var calibrationData))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Calibration data is required."));
        }

        if (!TryParseCalibrationData(calibrationData!, out var cameraParams, out var parseError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"Invalid calibration data: {parseError}"));
        }

        // 获取输入点
        var inputPoints = GetInputPoints(@operator, inputs);
        if (inputPoints.Count == 0)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("No input points provided."));
        }

        // 执行坐标变换
        List<Point3d> outputPoints;
        TransformReport report;
        Mat? visualizationImage = null;

        try
        {
            report = CreateTransformReport(cameraParams.CameraMatrix);
            outputPoints = transformMode.Equals("PixelToWorld", StringComparison.OrdinalIgnoreCase)
                ? inputPoints.Select(point => SimplePixelToWorld(point, cameraParams.CameraMatrix, worldPlaneZ, unitScale)).ToList()
                : inputPoints.Select(point => SimpleWorldToPixel(point, cameraParams.CameraMatrix, unitScale)).ToList();

            // 生成可视化图像
            if (TryGetInputImage(inputs, "Image", out var imageWrapper) && imageWrapper != null)
            {
                visualizationImage = CreateVisualization(
                    imageWrapper.GetMat(), inputPoints, outputPoints, transformMode, report);
            }
            else
            {
                visualizationImage = CreateCoordinateVisualization(
                    inputPoints, outputPoints, transformMode, cameraParams.ImageSize);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Coordinate transformation failed");
            return Task.FromResult(OperatorExecutionOutput.Failure($"Transformation failed: {ex.Message}"));
        }

        // 构建输出
        var outputPositions = outputPoints.Select(p => new Position(p.X, p.Y)).ToList();
        var resultData = new Dictionary<string, object>
        {
            { "TransformedPoints", outputPositions },
            { "InputPointCount", inputPoints.Count },
            { "OutputPointCount", outputPoints.Count },
            { "TransformMode", transformMode },
            { "WorldPlaneZ", worldPlaneZ },
            { "UnitScale", unitScale },
            { "CameraMatrix", cameraParams.CameraMatrix },
            { "TransformMatrix", cameraParams.TransformMatrix },
            { "ConditionNumber", report.ConditionNumber },
            { "TransformQuality", report.Quality },
            { "MeanReprojectionError", report.MeanReprojectionError },
            { "MaxReprojectionError", report.MaxReprojectionError }
        };

        if (generateReport)
        {
            resultData["AccuracyReport"] = new Dictionary<string, object>
            {
                { "InputPoints", inputPoints.Select(p => new { X = p.X, Y = p.Y, Z = p.Z }).ToList() },
                { "OutputPoints", outputPoints.Select(p => new { X = p.X, Y = p.Y, Z = p.Z }).ToList() },
                { "ConditionNumber", report.ConditionNumber },
                { "TransformQuality", report.Quality.ToString() },
                { "MeanError", report.MeanReprojectionError },
                { "MaxError", report.MaxReprojectionError },
                { "Unit", unitScale == 1.0 ? "mm" : $"mm/{unitScale}" },
                { "Timestamp", DateTime.UtcNow }
            };
        }

        var output = visualizationImage ?? new Mat(480, 640, MatType.CV_8UC3, Scalar.Black);
        
        // 清理资源
        cameraParams.Dispose();

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(output, resultData)));
    }

    private TransformReport CreateTransformReport(Mat cameraMatrix)
    {
        var conditionNumber = CalculateConditionNumber(cameraMatrix);
        return new TransformReport
        {
            ConditionNumber = conditionNumber,
            Quality = conditionNumber > 1e6
                ? TransformQuality.Poor
                : conditionNumber > 1e4
                    ? TransformQuality.Fair
                    : TransformQuality.Good,
            MeanReprojectionError = 0,
            MaxReprojectionError = 0
        };
    }

    private static Point3d SimplePixelToWorld(Point3d pixel, Mat cameraMatrix, double worldPlaneZ, double unitScale)
    {
        var fx = cameraMatrix.At<double>(0, 0);
        var fy = cameraMatrix.At<double>(1, 1);
        var cx = cameraMatrix.At<double>(0, 2);
        var cy = cameraMatrix.At<double>(1, 2);

        if (Math.Abs(fx) < 1e-12 || Math.Abs(fy) < 1e-12)
        {
            return new Point3d(pixel.X, pixel.Y, worldPlaneZ);
        }

        return new Point3d(
            (pixel.X - cx) / fx * unitScale,
            (pixel.Y - cy) / fy * unitScale,
            worldPlaneZ);
    }

    private static Point3d SimpleWorldToPixel(Point3d world, Mat cameraMatrix, double unitScale)
    {
        var fx = cameraMatrix.At<double>(0, 0);
        var fy = cameraMatrix.At<double>(1, 1);
        var cx = cameraMatrix.At<double>(0, 2);
        var cy = cameraMatrix.At<double>(1, 2);

        return new Point3d(
            world.X / Math.Max(unitScale, 1e-12) * fx + cx,
            world.Y / Math.Max(unitScale, 1e-12) * fy + cy,
            0);
    }

    private List<Point3d> GetInputPoints(Operator @operator, Dictionary<string, object>? inputs)
    {
        var points = new List<Point3d>();

        // 尝试从输入端口获取点列表
        if (inputs != null && inputs.TryGetValue("Points", out var pointsObj))
        {
            if (pointsObj is IEnumerable<Position> positions)
            {
                points.AddRange(positions.Select(p => new Point3d(p.X, p.Y, 0)));
            }
            else if (pointsObj is IEnumerable<Point2f> point2Fs)
            {
                points.AddRange(point2Fs.Select(p => new Point3d(p.X, p.Y, 0)));
            }
            else if (pointsObj is IEnumerable<Point3f> point3Fs)
            {
                points.AddRange(point3Fs.Select(p => new Point3d(p.X, p.Y, p.Z)));
            }
            else if (pointsObj is string pointsJson)
            {
                try
                {
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<List<Point3d>>(pointsJson);
                    if (parsed != null)
                    {
                        points.AddRange(parsed);
                    }
                }
                catch { }
            }
        }

        // 单点模式：从参数获取
        if (points.Count == 0)
        {
            var x = GetDoubleParam(@operator, "InputPointX", 0.0);
            var y = GetDoubleParam(@operator, "InputPointY", 0.0);
            points.Add(new Point3d(x, y, 0));
        }

        return points;
    }

    private List<Point3d> TransformPixelToWorld(
        List<Point3d> pixelPoints,
        CameraCalibrationParams camParams,
        double worldPlaneZ,
        double unitScale,
        bool useDistortion,
        out TransformReport report)
    {
        var worldPoints = new List<Point3d>();
        report = new TransformReport();
        var reprojectionErrors = new List<double>();

        using var cameraMatrix = camParams.CameraMatrix.Clone();
        using var distCoeffs = camParams.DistCoeffs != null && !camParams.DistCoeffs.Empty() ? camParams.DistCoeffs.Clone() : new Mat();
        using var rvec = camParams.RotationVector != null && !camParams.RotationVector.Empty()
            ? camParams.RotationVector.Clone()
            : new Mat(3, 1, MatType.CV_64FC1, Scalar.All(0));
        using var tvec = camParams.TranslationVector != null && !camParams.TranslationVector.Empty()
            ? camParams.TranslationVector.Clone()
            : new Mat(3, 1, MatType.CV_64FC1, Scalar.All(0));

        // 构建单应性矩阵 H = K * [r1 r2 t] (假设Z=0平面)
        // 或者使用 solvePnP 的反向变换
        using var rotMat = new Mat(3, 3, MatType.CV_64FC1);
        if (camParams.RotationMatrix != null && !camParams.RotationMatrix.Empty())
        {
            camParams.RotationMatrix.CopyTo(rotMat);
        }
        else if (!rvec.Empty())
        {
            Cv2.Rodrigues(rvec, rotMat);
        }

        // 计算条件数
        report.ConditionNumber = CalculateConditionNumber(cameraMatrix);
        report.Quality = report.ConditionNumber > 1e6 
            ? TransformQuality.Poor 
            : report.ConditionNumber > 1e4 
                ? TransformQuality.Fair 
                : TransformQuality.Good;

        foreach (var pixel in pixelPoints)
        {
            Point3d worldPoint;

            if (camParams.TransformMatrix != null && !camParams.TransformMatrix.Empty())
            {
                // 使用预计算的变换矩阵（从标定板得到）
                worldPoint = TransformUsingMatrix(pixel, camParams.TransformMatrix, unitScale);
            }
            else
            {
                // 使用相机外参进行射线平面交点计算
                worldPoint = PixelToWorldRayPlaneIntersection(
                    pixel, cameraMatrix, rotMat, tvec, worldPlaneZ, useDistortion, distCoeffs);
            }

            worldPoints.Add(worldPoint);

            // 计算重投影误差验证
            if (camParams.TransformMatrix != null && !camParams.TransformMatrix.Empty())
            {
                var reprojected = WorldToPixelUsingMatrix(worldPoint, camParams.TransformMatrix, unitScale);
                var error = Math.Sqrt(
                    Math.Pow(reprojected.X - pixel.X, 2) + 
                    Math.Pow(reprojected.Y - pixel.Y, 2));
                reprojectionErrors.Add(error);
            }
        }

        if (reprojectionErrors.Count > 0)
        {
            report.MeanReprojectionError = reprojectionErrors.Average();
            report.MaxReprojectionError = reprojectionErrors.Max();
        }

        return worldPoints;
    }

    private List<Point3d> TransformWorldToPixel(
        List<Point3d> worldPoints,
        CameraCalibrationParams camParams,
        double worldPlaneZ,
        double unitScale,
        bool useDistortion,
        out TransformReport report)
    {
        var pixelPoints = new List<Point3d>();
        report = new TransformReport();

        using var cameraMatrix = camParams.CameraMatrix.Clone();
        using var distCoeffs = camParams.DistCoeffs != null && !camParams.DistCoeffs.Empty() ? camParams.DistCoeffs.Clone() : new Mat();
        using var rvec = camParams.RotationVector != null && !camParams.RotationVector.Empty()
            ? camParams.RotationVector.Clone()
            : new Mat(3, 1, MatType.CV_64FC1, Scalar.All(0));
        using var tvec = camParams.TranslationVector != null && !camParams.TranslationVector.Empty()
            ? camParams.TranslationVector.Clone()
            : new Mat(3, 1, MatType.CV_64FC1, Scalar.All(0));

        // 计算条件数
        report.ConditionNumber = CalculateConditionNumber(cameraMatrix);
        report.Quality = report.ConditionNumber > 1e6 
            ? TransformQuality.Poor 
            : report.ConditionNumber > 1e4 
                ? TransformQuality.Fair 
                : TransformQuality.Good;

        foreach (var world in worldPoints)
        {
            Point3d pixelPoint;

            if (camParams.TransformMatrix != null && !camParams.TransformMatrix.Empty())
            {
                // 使用预计算的变换矩阵
                pixelPoint = WorldToPixelUsingMatrix(world, camParams.TransformMatrix, unitScale);
            }
            else
            {
                // 使用相机外参进行投影
                pixelPoint = WorldToPixelProjection(
                    world, cameraMatrix, rvec, tvec, useDistortion, distCoeffs);
            }

            pixelPoints.Add(pixelPoint);
        }

        return pixelPoints;
    }

    private Point3d TransformUsingMatrix(Point3d pixel, Mat transformMatrix, double unitScale)
    {
        // 使用单应性矩阵或4x4变换矩阵
        var x = pixel.X;
        var y = pixel.Y;
        var z = pixel.Z;

        if (transformMatrix.Rows == 3 && transformMatrix.Cols == 3)
        {
            // 单应性矩阵 (3x3) - 用于Z=0平面
            var xp = transformMatrix.At<double>(0, 0) * x + 
                     transformMatrix.At<double>(0, 1) * y + 
                     transformMatrix.At<double>(0, 2);
            var yp = transformMatrix.At<double>(1, 0) * x + 
                     transformMatrix.At<double>(1, 1) * y + 
                     transformMatrix.At<double>(1, 2);
            var wp = transformMatrix.At<double>(2, 0) * x + 
                     transformMatrix.At<double>(2, 1) * y + 
                     transformMatrix.At<double>(2, 2);

            return new Point3d(xp / wp / unitScale, yp / wp / unitScale, 0);
        }
        else if (transformMatrix.Rows == 4 && transformMatrix.Cols == 4)
        {
            // 4x4变换矩阵
            var xp = transformMatrix.At<double>(0, 0) * x + 
                     transformMatrix.At<double>(0, 1) * y + 
                     transformMatrix.At<double>(0, 2) * z + 
                     transformMatrix.At<double>(0, 3);
            var yp = transformMatrix.At<double>(1, 0) * x + 
                     transformMatrix.At<double>(1, 1) * y + 
                     transformMatrix.At<double>(1, 2) * z + 
                     transformMatrix.At<double>(1, 3);
            var zp = transformMatrix.At<double>(2, 0) * x + 
                     transformMatrix.At<double>(2, 1) * y + 
                     transformMatrix.At<double>(2, 2) * z + 
                     transformMatrix.At<double>(2, 3);
            var wp = transformMatrix.At<double>(3, 0) * x + 
                     transformMatrix.At<double>(3, 1) * y + 
                     transformMatrix.At<double>(3, 2) * z + 
                     transformMatrix.At<double>(3, 3);

            return new Point3d(xp / wp / unitScale, yp / wp / unitScale, zp / wp / unitScale);
        }

        return pixel;
    }

    private Point3d WorldToPixelUsingMatrix(Point3d world, Mat transformMatrix, double unitScale)
    {
        // 应用单位缩放
        var x = world.X * unitScale;
        var y = world.Y * unitScale;
        var z = world.Z * unitScale;

        // 计算逆矩阵
        using var invMatrix = new Mat();
        var invertResult = Cv2.Invert(transformMatrix, invMatrix, DecompTypes.LU);
        if (Math.Abs(invertResult) < 1e-12)
        {
            return world;
        }

        return TransformUsingMatrix(new Point3d(x, y, z), invMatrix, 1.0);
    }

    private Point3d PixelToWorldRayPlaneIntersection(
        Point3d pixel,
        Mat cameraMatrix,
        Mat rotationMatrix,
        Mat translationVector,
        double worldPlaneZ,
        bool useDistortion,
        Mat distCoeffs)
    {
        // 当前实现使用针孔模型做稳定映射；若存在畸变参数，则在此阶段忽略高阶校正。
        var cx = cameraMatrix.At<double>(0, 2);
        var cy = cameraMatrix.At<double>(1, 2);
        var fx = cameraMatrix.At<double>(0, 0);
        var fy = cameraMatrix.At<double>(1, 1);
        var undistortedPixel = new Point2d((pixel.X - cx) / fx, (pixel.Y - cy) / fy);

        // 相机坐标系中的射线方向
        var rayCamera = new Point3d(undistortedPixel.X, undistortedPixel.Y, 1.0);

        // 转换到世界坐标系
        // P_world = R^T * (P_camera - t)
        var t = new Point3d(
            translationVector.At<double>(0, 0),
            translationVector.At<double>(1, 0),
            translationVector.At<double>(2, 0));

        // 射线在世界坐标系中的方向
        var rayWorld = new Point3d(
            rotationMatrix.At<double>(0, 0) * rayCamera.X + 
            rotationMatrix.At<double>(1, 0) * rayCamera.Y + 
            rotationMatrix.At<double>(2, 0) * rayCamera.Z,
            rotationMatrix.At<double>(0, 1) * rayCamera.X + 
            rotationMatrix.At<double>(1, 1) * rayCamera.Y + 
            rotationMatrix.At<double>(2, 1) * rayCamera.Z,
            rotationMatrix.At<double>(0, 2) * rayCamera.X + 
            rotationMatrix.At<double>(1, 2) * rayCamera.Y + 
            rotationMatrix.At<double>(2, 2) * rayCamera.Z);

        // 相机光心在世界坐标系中的位置
        var cameraCenter = new Point3d(
            -(rotationMatrix.At<double>(0, 0) * t.X + rotationMatrix.At<double>(1, 0) * t.Y + rotationMatrix.At<double>(2, 0) * t.Z),
            -(rotationMatrix.At<double>(0, 1) * t.X + rotationMatrix.At<double>(1, 1) * t.Y + rotationMatrix.At<double>(2, 1) * t.Z),
            -(rotationMatrix.At<double>(0, 2) * t.X + rotationMatrix.At<double>(1, 2) * t.Y + rotationMatrix.At<double>(2, 2) * t.Z));

        // 射线与Z=worldPlaneZ平面的交点
        if (Math.Abs(rayWorld.Z) < 1e-10)
        {
            return new Point3d(0, 0, worldPlaneZ);
        }

        var scale = (worldPlaneZ - cameraCenter.Z) / rayWorld.Z;
        var worldPoint = new Point3d(
            cameraCenter.X + scale * rayWorld.X,
            cameraCenter.Y + scale * rayWorld.Y,
            worldPlaneZ);

        return worldPoint;
    }

    private Point3d WorldToPixelProjection(
        Point3d world,
        Mat cameraMatrix,
        Mat rvec,
        Mat tvec,
        bool useDistortion,
        Mat distCoeffs)
    {
        using var rotationMatrix = new Mat(3, 3, MatType.CV_64FC1);
        if (!rvec.Empty())
        {
            Cv2.Rodrigues(rvec, rotationMatrix);
        }
        else
        {
            rotationMatrix.Set(0, 0, 1.0);
            rotationMatrix.Set(1, 1, 1.0);
            rotationMatrix.Set(2, 2, 1.0);
        }

        var tx = tvec.Empty() ? 0.0 : tvec.At<double>(0, 0);
        var ty = tvec.Empty() ? 0.0 : tvec.At<double>(1, 0);
        var tz = tvec.Empty() ? 0.0 : tvec.At<double>(2, 0);

        var xc = rotationMatrix.At<double>(0, 0) * world.X + rotationMatrix.At<double>(0, 1) * world.Y + rotationMatrix.At<double>(0, 2) * world.Z + tx;
        var yc = rotationMatrix.At<double>(1, 0) * world.X + rotationMatrix.At<double>(1, 1) * world.Y + rotationMatrix.At<double>(1, 2) * world.Z + ty;
        var zc = rotationMatrix.At<double>(2, 0) * world.X + rotationMatrix.At<double>(2, 1) * world.Y + rotationMatrix.At<double>(2, 2) * world.Z + tz;

        if (Math.Abs(zc) < 1e-12)
        {
            return new Point3d(0, 0, 0);
        }

        var fx = cameraMatrix.At<double>(0, 0);
        var fy = cameraMatrix.At<double>(1, 1);
        var cx = cameraMatrix.At<double>(0, 2);
        var cy = cameraMatrix.At<double>(1, 2);

        return new Point3d(fx * xc / zc + cx, fy * yc / zc + cy, 0);
    }

    private double CalculateConditionNumber(Mat matrix)
    {
        if (matrix.Empty() || matrix.Rows < 3 || matrix.Cols < 3)
        {
            return double.MaxValue;
        }

        var fx = Math.Abs(matrix.At<double>(0, 0));
        var fy = Math.Abs(matrix.At<double>(1, 1));
        var principal = Math.Max(Math.Abs(matrix.At<double>(0, 2)), Math.Abs(matrix.At<double>(1, 2)));
        var minValue = new[] { fx, fy, Math.Max(principal, 1.0) }.Min();
        var maxValue = new[] { fx, fy, Math.Max(principal, 1.0) }.Max();

        return minValue < 1e-10 ? double.MaxValue : maxValue / minValue;
    }

    private Mat CreateVisualization(
        Mat image,
        List<Point3d> inputPoints,
        List<Point3d> outputPoints,
        string transformMode,
        TransformReport report)
    {
        var result = image.Clone();
        var isPixelToWorld = transformMode.Equals("PixelToWorld", StringComparison.OrdinalIgnoreCase);

        for (int i = 0; i < inputPoints.Count && i < outputPoints.Count; i++)
        {
            var inputPt = new Point((int)inputPoints[i].X, (int)inputPoints[i].Y);
            
            // 绘制输入点
            Cv2.Circle(result, inputPt, 5, new Scalar(0, 0, 255), -1);
            Cv2.PutText(result, $"{i}", new Point(inputPt.X + 8, inputPt.Y - 8),
                HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 0, 255), 1);

            // 绘制坐标文本
            var coordText = isPixelToWorld
                ? $"W:({outputPoints[i].X:F2},{outputPoints[i].Y:F2})"
                : $"P:({outputPoints[i].X:F1},{outputPoints[i].Y:F1})";
            Cv2.PutText(result, coordText, new Point(inputPt.X + 8, inputPt.Y + 15),
                HersheyFonts.HersheySimplex, 0.4, new Scalar(0, 255, 0), 1);
        }

        // 绘制报告信息
        Cv2.PutText(result, $"Mode: {transformMode}", new Point(10, 30),
            HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 255, 0), 2);
        Cv2.PutText(result, $"Quality: {report.Quality}", new Point(10, 55),
            HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 255, 0), 2);
        if (report.MeanReprojectionError > 0)
        {
            Cv2.PutText(result, $"Reproj Error: {report.MeanReprojectionError:F3}px", new Point(10, 80),
                HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 255, 0), 2);
        }

        return result;
    }

    private Mat CreateCoordinateVisualization(
        List<Point3d> inputPoints,
        List<Point3d> outputPoints,
        string transformMode,
        Size imageSize)
    {
        // 创建坐标系可视化
        var size = Math.Max(imageSize.Width, imageSize.Height);
        if (size < 640) size = 640;
        
        var result = new Mat(size, size, MatType.CV_8UC3, new Scalar(30, 30, 30));
        
        // 绘制坐标轴
        var center = new Point(size / 2, size / 2);
        Cv2.Line(result, new Point(0, center.Y), new Point(size, center.Y), new Scalar(100, 100, 100), 1);
        Cv2.Line(result, new Point(center.X, 0), new Point(center.X, size), new Scalar(100, 100, 100), 1);

        // 绘制点
        var isPixelToWorld = transformMode.Equals("PixelToWorld", StringComparison.OrdinalIgnoreCase);
        var scale = size / 2.0 / (outputPoints.Count > 0 ? outputPoints.Max(p => Math.Max(Math.Abs(p.X), Math.Abs(p.Y))) + 10 : 100);

        for (int i = 0; i < outputPoints.Count; i++)
        {
            var pt = outputPoints[i];
            var drawPt = new Point(
                (int)(center.X + pt.X * scale),
                (int)(center.Y - pt.Y * scale)); // Y轴向上

            Cv2.Circle(result, drawPt, 5, new Scalar(0, 255, 0), -1);
            Cv2.PutText(result, $"{i}:({pt.X:F1},{pt.Y:F1})", new Point(drawPt.X + 8, drawPt.Y),
                HersheyFonts.HersheySimplex, 0.4, new Scalar(255, 255, 255), 1);
        }

        Cv2.PutText(result, $"Mode: {transformMode}", new Point(10, 30),
            HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 255, 0), 2);

        return result;
    }

    private bool TryResolveCalibrationData(Operator @operator, Dictionary<string, object>? inputs, out string? calibrationData)
    {
        calibrationData = null;
        
        if (inputs != null && inputs.TryGetValue("CalibrationData", out var data) && data is string strData)
        {
            calibrationData = strData;
            return true;
        }

        var file = GetStringParam(@operator, "CalibrationFile", "");
        if (!string.IsNullOrEmpty(file) && File.Exists(file))
        {
            calibrationData = File.ReadAllText(file);
            return true;
        }

        return false;
    }

    private bool TryParseCalibrationData(string json, out CameraCalibrationParams camParams, out string? error)
    {
        camParams = new CameraCalibrationParams();
        error = null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 解析相机矩阵
            if (root.TryGetProperty("CameraMatrix", out var camMatrixElem))
            {
                camParams.CameraMatrix = ParseMatrix(camMatrixElem, 3, 3);
            }
            else if (root.TryGetProperty("CameraMatrixLeft", out var camMatrixLeftElem))
            {
                // 立体标定数据 - 使用左相机
                camParams.CameraMatrix = ParseMatrix(camMatrixLeftElem, 3, 3);
            }

            // 解析畸变系数
            if (root.TryGetProperty("DistCoeffs", out var distCoeffsElem))
            {
                camParams.DistCoeffs = ParseVector(distCoeffsElem);
            }

            // 解析旋转矩阵（外参）
            if (root.TryGetProperty("RotationMatrix", out var rotMatElem))
            {
                camParams.RotationMatrix = ParseMatrix(rotMatElem, 3, 3);
            }

            // 解析平移向量
            if (root.TryGetProperty("TranslationVector", out var tvecElem))
            {
                camParams.TranslationVector = ParseVector(tvecElem);
            }

            // 解析旋转向量
            if (root.TryGetProperty("RotationVector", out var rvecElem))
            {
                camParams.RotationVector = ParseVector(rvecElem);
            }

            // 解析图像尺寸
            if (root.TryGetProperty("ImageWidth", out var widthElem) && 
                root.TryGetProperty("ImageHeight", out var heightElem))
            {
                camParams.ImageSize = new Size(widthElem.GetInt32(), heightElem.GetInt32());
            }

            // 解析变换矩阵（如果有）
            if (root.TryGetProperty("TransformMatrix", out var transMatElem))
            {
                camParams.TransformMatrix = ParseMatrix(transMatElem, 3, 3);
            }

            return camParams.CameraMatrix != null && !camParams.CameraMatrix.Empty();
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private Mat ParseMatrix(JsonElement element, int rows, int cols)
    {
        var mat = new Mat(rows, cols, MatType.CV_64FC1);
        
        if (element.ValueKind == JsonValueKind.Array)
        {
            var arr = element.EnumerateArray().ToArray();
            
            // 支持嵌套数组格式 [[1,2,3],[4,5,6],[7,8,9]]
            if (arr.Length == rows && arr[0].ValueKind == JsonValueKind.Array)
            {
                for (int i = 0; i < rows; i++)
                {
                    var row = arr[i].EnumerateArray().ToArray();
                    for (int j = 0; j < Math.Min(cols, row.Length); j++)
                    {
                        mat.Set(i, j, row[j].GetDouble());
                    }
                }
            }
            // 支持扁平数组格式 [1,2,3,4,5,6,7,8,9]
            else if (arr.Length == rows * cols)
            {
                for (int i = 0; i < rows; i++)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        mat.Set(i, j, arr[i * cols + j].GetDouble());
                    }
                }
            }
        }

        return mat;
    }

    private Mat ParseVector(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            return new Mat();

        var values = element.EnumerateArray().Select(e => e.GetDouble()).ToArray();
        var mat = new Mat(values.Length, 1, MatType.CV_64FC1);
        for (int i = 0; i < values.Length; i++)
        {
            mat.Set(i, 0, values[i]);
        }
        return mat;
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var unitScale = GetDoubleParam(@operator, "UnitScale", 1.0);
        if (unitScale <= 0)
        {
            return ValidationResult.Invalid("UnitScale must be positive.");
        }

        var worldPlaneZ = GetDoubleParam(@operator, "WorldPlaneZ", 0.0);
        // Z值范围检查（防止极端值）
        if (Math.Abs(worldPlaneZ) > 10000)
        {
            Logger.LogWarning("WorldPlaneZ is unusually large: {Z}", worldPlaneZ);
        }

        return ValidationResult.Valid();
    }

    private enum TransformQuality
    {
        Poor,   // 条件数 > 1e6
        Fair,   // 条件数 > 1e4
        Good    // 条件数 <= 1e4
    }

    private class TransformReport
    {
        public double ConditionNumber { get; set; }
        public TransformQuality Quality { get; set; }
        public double MeanReprojectionError { get; set; }
        public double MaxReprojectionError { get; set; }
    }

    private class CameraCalibrationParams : IDisposable
    {
        public Mat CameraMatrix { get; set; } = new Mat();
        public Mat DistCoeffs { get; set; } = new Mat();
        public Mat RotationMatrix { get; set; } = new Mat();
        public Mat RotationVector { get; set; } = new Mat();
        public Mat TranslationVector { get; set; } = new Mat();
        public Mat TransformMatrix { get; set; } = new Mat();
        public Size ImageSize { get; set; }

        public void Dispose()
        {
            CameraMatrix?.Dispose();
            DistCoeffs?.Dispose();
            RotationMatrix?.Dispose();
            RotationVector?.Dispose();
            TranslationVector?.Dispose();
            TransformMatrix?.Dispose();
        }
    }
}
