using System.Text.Json;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class CameraCalibrationOperatorTests
{
    private readonly CameraCalibrationOperator _operator;

    public CameraCalibrationOperatorTests()
    {
        _operator = new CameraCalibrationOperator(Substitute.For<ILogger<CameraCalibrationOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeCameraCalibration()
    {
        _operator.OperatorType.Should().Be(OperatorType.CameraCalibration);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("Calib", OperatorType.CameraCalibration, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_SingleImage_ShouldOutputCalibrationData()
    {
        var op = CreateSingleImageOperator();

        using var chessboard = CreateChessboardImage(9, 6, 40);
        var inputs = TestHelpers.CreateImageInputs(chessboard);
        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("CalibrationData");

        var calibrationJson = result.OutputData!["CalibrationData"].ToString();
        calibrationJson.Should().NotBeNullOrWhiteSpace();

        using var doc = JsonDocument.Parse(calibrationJson!);
        var root = doc.RootElement;

        root.GetProperty("schemaVersion").GetInt32().Should().Be(2);
        root.GetProperty("transformModel").GetString().Should().Be("preview");
        root.GetProperty("quality").GetProperty("accepted").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_FolderCalibration_WithSyntheticDataset_ShouldProduceAcceptedBundle()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"camera-calib-{Guid.NewGuid():N}");
        var imageFolder = Path.Combine(tempRoot, "images");
        var outputPath = Path.Combine(tempRoot, "camera_bundle.json");
        Directory.CreateDirectory(imageFolder);

        try
        {
            CreateSyntheticCalibrationDataset(imageFolder, boardWidth: 9, boardHeight: 6, squareSizeMm: 25.0);
            var op = CreateFolderCalibrationOperator(imageFolder, outputPath);

            var result = await _operator.ExecuteAsync(op, null);

            result.IsSuccess.Should().BeTrue(result.ErrorMessage);
            result.OutputData.Should().NotBeNull();
            result.OutputData!["Accepted"].Should().Be(true);
            Convert.ToDouble(result.OutputData["ReprojectionError"]).Should().BeLessThanOrEqualTo(0.35);
            Convert.ToDouble(result.OutputData["MaxReprojectionError"]).Should().BeLessThanOrEqualTo(0.60);
            Convert.ToInt32(result.OutputData["ImageCount"]).Should().BeGreaterOrEqualTo(12);
            Assert.IsType<double[]>(result.OutputData["PerViewErrors"]).Length.Should().BeGreaterOrEqualTo(12);
            File.Exists(outputPath).Should().BeTrue();

            var calibrationJson = result.OutputData["CalibrationData"].ToString();
            calibrationJson.Should().NotBeNullOrWhiteSpace();
            using var doc = JsonDocument.Parse(calibrationJson!);
            doc.RootElement.GetProperty("quality").GetProperty("accepted").GetBoolean().Should().BeTrue();
            doc.RootElement.GetProperty("quality").GetProperty("meanError").GetDouble().Should().BeLessThanOrEqualTo(0.35);
            doc.RootElement.GetProperty("quality").GetProperty("maxError").GetDouble().Should().BeLessThanOrEqualTo(0.60);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("Calib", OperatorType.CameraCalibration, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    private static Operator CreateSingleImageOperator()
    {
        var op = new Operator("Calib", OperatorType.CameraCalibration, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("PatternType", "Chessboard", "string"));
        op.AddParameter(TestHelpers.CreateParameter("BoardWidth", "BoardWidth", "int", 9, 2, 30, true));
        op.AddParameter(TestHelpers.CreateParameter("BoardHeight", "BoardHeight", "int", 6, 2, 30, true));
        op.AddParameter(TestHelpers.CreateParameter("SquareSize", "SquareSize", "double", 25.0, 0.1, 1000.0, true));
        op.AddParameter(TestHelpers.CreateParameter("Mode", "SingleImage", "string"));
        return op;
    }

    private static Operator CreateFolderCalibrationOperator(string folder, string outputPath)
    {
        var op = new Operator("Calib", OperatorType.CameraCalibration, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("PatternType", "Chessboard", "string"));
        op.AddParameter(TestHelpers.CreateParameter("BoardWidth", 9, "int"));
        op.AddParameter(TestHelpers.CreateParameter("BoardHeight", 6, "int"));
        op.AddParameter(TestHelpers.CreateParameter("SquareSize", 25.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("Mode", "FolderCalibration", "string"));
        op.AddParameter(TestHelpers.CreateParameter("ImageFolder", folder, "string"));
        op.AddParameter(TestHelpers.CreateParameter("CalibrationOutputPath", outputPath, "string"));
        return op;
    }

    private static ImageWrapper CreateChessboardImage(int boardWidth, int boardHeight, int squareSize)
    {
        var rows = boardHeight + 1;
        var cols = boardWidth + 1;
        var mat = new Mat(rows * squareSize, cols * squareSize, MatType.CV_8UC3, Scalar.White);

        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < cols; x++)
            {
                if (((x + y) & 1) == 0)
                {
                    var rect = new Rect(x * squareSize, y * squareSize, squareSize, squareSize);
                    Cv2.Rectangle(mat, rect, Scalar.Black, -1);
                }
            }
        }

        Cv2.GaussianBlur(mat, mat, new Size(3, 3), 0);
        return new ImageWrapper(mat);
    }

    private static void CreateSyntheticCalibrationDataset(
        string folder,
        int boardWidth,
        int boardHeight,
        double squareSizeMm)
    {
        const int imageWidth = 1280;
        const int imageHeight = 960;
        using var boardTemplate = CreateCanonicalBoard(boardWidth, boardHeight, squarePixels: 80);
        using var cameraMatrix = CreateCameraMatrix(920.0, 915.0, 646.0, 474.0);
        using var distCoeffs = new Mat(1, 5, MatType.CV_64FC1, Scalar.All(0));

        var poses = new (Vec3d Rotation, Vec3d Translation)[]
        {
            (new Vec3d(0.04, -0.08, 0.02), new Vec3d(-120, -70, 1220)),
            (new Vec3d(-0.05, 0.09, -0.03), new Vec3d(110, -40, 1260)),
            (new Vec3d(0.08, 0.03, 0.04), new Vec3d(-80, 65, 1180)),
            (new Vec3d(-0.07, -0.04, 0.05), new Vec3d(70, 90, 1320)),
            (new Vec3d(0.02, 0.12, -0.04), new Vec3d(-150, 30, 1360)),
            (new Vec3d(-0.09, 0.01, 0.02), new Vec3d(140, -95, 1280)),
            (new Vec3d(0.10, -0.03, -0.05), new Vec3d(-50, 110, 1240)),
            (new Vec3d(-0.02, -0.10, 0.06), new Vec3d(40, -120, 1340)),
            (new Vec3d(0.06, 0.06, 0.01), new Vec3d(-95, 85, 1200)),
            (new Vec3d(-0.08, 0.05, -0.02), new Vec3d(95, 20, 1300)),
            (new Vec3d(0.03, -0.12, 0.03), new Vec3d(-135, -25, 1380)),
            (new Vec3d(-0.04, 0.07, 0.05), new Vec3d(125, 70, 1230)),
            (new Vec3d(0.11, 0.01, -0.03), new Vec3d(-70, -100, 1270)),
            (new Vec3d(-0.06, -0.06, 0.04), new Vec3d(55, 105, 1330))
        };

        for (var i = 0; i < poses.Length; i++)
        {
            var path = Path.Combine(folder, $"view_{i + 1:000}.png");
            WriteProjectedBoard(
                path,
                boardTemplate,
                boardWidth,
                boardHeight,
                squareSizeMm,
                new Size(imageWidth, imageHeight),
                cameraMatrix,
                distCoeffs,
                poses[i].Rotation,
                poses[i].Translation);
        }
    }

    private static Mat CreateCanonicalBoard(int boardWidth, int boardHeight, int squarePixels)
    {
        var cols = boardWidth + 1;
        var rows = boardHeight + 1;
        var board = new Mat(rows * squarePixels, cols * squarePixels, MatType.CV_8UC1, Scalar.White);
        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < cols; x++)
            {
                if (((x + y) & 1) == 0)
                {
                    Cv2.Rectangle(
                        board,
                        new Rect(x * squarePixels, y * squarePixels, squarePixels, squarePixels),
                        Scalar.Black,
                        -1);
                }
            }
        }

        Cv2.Rectangle(board, new Rect(0, 0, board.Cols - 1, board.Rows - 1), new Scalar(32), 3);
        return board;
    }

    private static void WriteProjectedBoard(
        string path,
        Mat boardTemplate,
        int boardWidth,
        int boardHeight,
        double squareSizeMm,
        Size imageSize,
        Mat cameraMatrix,
        Mat distCoeffs,
        Vec3d rotation,
        Vec3d translation)
    {
        var boardWidthMm = (boardWidth + 1) * squareSizeMm;
        var boardHeightMm = (boardHeight + 1) * squareSizeMm;
        var boardCorners = new[]
        {
            new Point3f(0, 0, 0),
            new Point3f((float)boardWidthMm, 0, 0),
            new Point3f((float)boardWidthMm, (float)boardHeightMm, 0),
            new Point3f(0, (float)boardHeightMm, 0)
        };

        using var rvec = new Mat(3, 1, MatType.CV_64FC1);
        rvec.Set(0, 0, rotation.Item0);
        rvec.Set(1, 0, rotation.Item1);
        rvec.Set(2, 0, rotation.Item2);
        using var tvec = new Mat(3, 1, MatType.CV_64FC1);
        tvec.Set(0, 0, translation.Item0);
        tvec.Set(1, 0, translation.Item1);
        tvec.Set(2, 0, translation.Item2);
        using var boardCornerMat = Mat.FromArray(boardCorners);
        using var projected = new Mat();
        using var jacobian = new Mat();
        Cv2.ProjectPoints(boardCornerMat, rvec, tvec, cameraMatrix, distCoeffs, projected, jacobian, 0.0);

        var destination = ReadPoint2fVector(projected);
        var source = new[]
        {
            new Point2f(0, 0),
            new Point2f(boardTemplate.Cols - 1, 0),
            new Point2f(boardTemplate.Cols - 1, boardTemplate.Rows - 1),
            new Point2f(0, boardTemplate.Rows - 1)
        };

        using var homography = Cv2.GetPerspectiveTransform(source, destination);
        using var canvas = new Mat(imageSize.Height, imageSize.Width, MatType.CV_8UC1, new Scalar(180));
        using var warped = new Mat();
        Cv2.WarpPerspective(boardTemplate, warped, homography, imageSize, InterpolationFlags.Linear, BorderTypes.Constant, new Scalar(180));
        using var blurred = new Mat();
        Cv2.GaussianBlur(warped, blurred, new Size(3, 3), 0.6);
        Cv2.ImWrite(path, blurred);
    }

    private static Mat CreateCameraMatrix(double fx, double fy, double cx, double cy)
    {
        var cameraMatrix = new Mat(3, 3, MatType.CV_64FC1, Scalar.All(0));
        cameraMatrix.Set(0, 0, fx);
        cameraMatrix.Set(1, 1, fy);
        cameraMatrix.Set(0, 2, cx);
        cameraMatrix.Set(1, 2, cy);
        cameraMatrix.Set(2, 2, 1.0);
        return cameraMatrix;
    }

    private static Point2f[] ReadPoint2fVector(Mat mat)
    {
        var count = Math.Max(mat.Rows, mat.Cols);
        var points = new Point2f[count];
        for (var i = 0; i < count; i++)
        {
            points[i] = mat.Rows >= mat.Cols ? mat.At<Point2f>(i, 0) : mat.At<Point2f>(0, i);
        }

        return points;
    }
}
