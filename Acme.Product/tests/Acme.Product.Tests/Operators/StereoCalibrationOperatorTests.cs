using System.IO;
using System.Linq;
using System.Reflection;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Calibration;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class StereoCalibrationOperatorTests
{
    private readonly StereoCalibrationOperator _operator;

    public StereoCalibrationOperatorTests()
    {
        _operator = new StereoCalibrationOperator(Substitute.For<ILogger<StereoCalibrationOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeStereoCalibration()
    {
        _operator.OperatorType.Should().Be(OperatorType.StereoCalibration);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("StereoCalibration", OperatorType.StereoCalibration, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingRightImage_ShouldReturnFailure()
    {
        var op = new Operator("StereoCalibration", OperatorType.StereoCalibration, 0, 0);
        using var leftImage = TestHelpers.CreateTestImage();
        var inputs = new Dictionary<string, object> { { "LeftImage", leftImage } };

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidImages_ShouldReturnSuccess()
    {
        var op = new Operator("StereoCalibration", OperatorType.StereoCalibration, 0, 0);
        using var leftImage = TestHelpers.CreateTestImage();
        using var rightImage = TestHelpers.CreateTestImage();
        var inputs = new Dictionary<string, object>
        {
            { "LeftImage", leftImage },
            { "RightImage", rightImage }
        };

        var result = await _operator.ExecuteAsync(op, inputs);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_FolderCalibration_WithMismatchedStableKeys_ShouldFailClosed()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"stereo-calib-tests-{Guid.NewGuid():N}");
        var leftFolder = Path.Combine(tempRoot, "left");
        var rightFolder = Path.Combine(tempRoot, "right");
        Directory.CreateDirectory(leftFolder);
        Directory.CreateDirectory(rightFolder);

        try
        {
            WriteImage(Path.Combine(leftFolder, "part_001_left.png"));
            WriteImage(Path.Combine(leftFolder, "part_002_left.png"));
            WriteImage(Path.Combine(leftFolder, "part_003_left.png"));

            WriteImage(Path.Combine(rightFolder, "part_001_right.png"));
            WriteImage(Path.Combine(rightFolder, "part_003_right.png"));
            WriteImage(Path.Combine(rightFolder, "part_004_right.png"));

            var op = CreateFolderCalibrationOperator(leftFolder, rightFolder, Path.Combine(tempRoot, "stereo.json"), minValidPairs: 3);

            var result = await _operator.ExecuteAsync(op, null);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().NotBeNull();
            result.ErrorMessage!.Should().Contain("mismatch");
            result.ErrorMessage.Should().Contain("part_002");
            result.ErrorMessage.Should().Contain("part_004");
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
    public void EvaluateStereoCalibrationQuality_WithPerViewOutlier_ShouldRejectAcceptance()
    {
        var method = typeof(StereoCalibrationOperator).GetMethod("EvaluateStereoCalibrationQuality", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var pairKeys = Enumerable.Range(1, 12)
            .Select(index => $"pair_{index:000}")
            .ToArray();

        var leftPerViewErrors = new double[] { 0.18, 0.21, 0.19, 0.22, 0.20, 0.17, 0.23, 0.18, 0.21, 0.20, 0.22, 0.91 };
        var rightPerViewErrors = new double[] { 0.17, 0.20, 0.18, 0.19, 0.21, 0.18, 0.20, 0.19, 0.18, 0.20, 0.21, 0.24 };

        var quality = method!.Invoke(null, new object[]
        {
            "Chessboard",
            new Size(9, 6),
            25.0,
            12,
            12,
            12,
            0.24,
            0.20,
            0.21,
            0.95,
            leftPerViewErrors,
            rightPerViewErrors,
            pairKeys,
            Array.Empty<string>()
        }) as CalibrationQualityV2;

        quality.Should().NotBeNull();
        quality!.Accepted.Should().BeFalse();
        quality.MaxError.Should().BeApproximately(0.91, 1e-6);
        quality.Diagnostics.Should().Contain(d => d.Contains("Quality gate failed", StringComparison.OrdinalIgnoreCase));
        quality.Diagnostics.Should().Contain(d => d.Contains("EpipolarError=0.9500", StringComparison.OrdinalIgnoreCase));
        quality.Diagnostics.Should().Contain(d => d.Contains("pair_012", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CalculateReprojectionStats_WithMismatchedIntrinsics_ShouldHonorProvidedCalibration()
    {
        var method = typeof(StereoCalibrationOperator).GetMethod("CalculateReprojectionStats", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull();

        var objectPoints = CreateSyntheticObjectPointViews();
        using var actualCameraMatrix = CreateCameraMatrix(900, 880, 320, 240);
        using var actualDistCoeffs = new Mat(1, 5, MatType.CV_64FC1, Scalar.All(0));
        var imagePoints = ProjectSyntheticViews(objectPoints, actualCameraMatrix, actualDistCoeffs);

        using var mismatchedCameraMatrix = CreateCameraMatrix(220, 210, 40, 30);
        using var mismatchedDistCoeffs = new Mat(1, 5, MatType.CV_64FC1, Scalar.All(0));

        var stats = method!.Invoke(_operator, new object[] { objectPoints, imagePoints, mismatchedCameraMatrix, mismatchedDistCoeffs });
        stats.Should().NotBeNull();

        var meanError = Convert.ToDouble(stats!.GetType().GetProperty("MeanError")!.GetValue(stats));
        meanError.Should().BeGreaterThan(10.0);
    }

    [Fact]
    public void ValidateParameters_WithValidParams_ShouldBeValid()
    {
        var op = new Operator("StereoCalibration", OperatorType.StereoCalibration, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("BoardWidth", 9));
        op.Parameters.Add(TestHelpers.CreateParameter("BoardHeight", 6));
        op.Parameters.Add(TestHelpers.CreateParameter("SquareSize", 25.0));
        op.Parameters.Add(TestHelpers.CreateParameter("MinValidPairs", 12));

        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_WithInvalidMinValidPairs_ShouldBeInvalid()
    {
        var op = new Operator("StereoCalibration", OperatorType.StereoCalibration, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("MinValidPairs", 2));

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateParameters_WithInvalidBoardDimensions_ShouldBeInvalid()
    {
        var op = new Operator("StereoCalibration", OperatorType.StereoCalibration, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("BoardWidth", 1));

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    private static Operator CreateFolderCalibrationOperator(string leftFolder, string rightFolder, string outputPath, int minValidPairs)
    {
        var op = new Operator("StereoCalibration", OperatorType.StereoCalibration, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Mode", "FolderCalibration", "string"));
        op.AddParameter(TestHelpers.CreateParameter("LeftImageFolder", leftFolder, "string"));
        op.AddParameter(TestHelpers.CreateParameter("RightImageFolder", rightFolder, "string"));
        op.AddParameter(TestHelpers.CreateParameter("CalibrationOutputPath", outputPath, "string"));
        op.AddParameter(TestHelpers.CreateParameter("MinValidPairs", minValidPairs, "int"));
        return op;
    }

    private static void WriteImage(string path)
    {
        using var image = new Mat(16, 16, MatType.CV_8UC1, Scalar.Black);
        Cv2.ImWrite(path, image);
    }

    private static List<Point3f[]> CreateSyntheticObjectPointViews()
    {
        var points = new[]
        {
            new Point3f(0, 0, 0),
            new Point3f(40, 0, 5),
            new Point3f(0, 35, 10),
            new Point3f(35, 30, 0),
            new Point3f(15, 10, 25),
            new Point3f(45, 15, 30),
            new Point3f(10, 45, 20),
            new Point3f(38, 42, 15)
        };

        return new List<Point3f[]>
        {
            points.ToArray(),
            points.ToArray(),
            points.ToArray()
        };
    }

    private static List<Point2f[]> ProjectSyntheticViews(
        IReadOnlyList<Point3f[]> objectPoints,
        Mat cameraMatrix,
        Mat distCoeffs)
    {
        var rotations = new[]
        {
            new Vec3d(0.08, -0.04, 0.02),
            new Vec3d(-0.05, 0.06, 0.03),
            new Vec3d(0.04, 0.03, -0.07)
        };
        var translations = new[]
        {
            new Vec3d(-10, -5, 420),
            new Vec3d(15, 12, 470),
            new Vec3d(-20, 18, 520)
        };

        var projectedViews = new List<Point2f[]>(objectPoints.Count);
        for (var i = 0; i < objectPoints.Count; i++)
        {
            using var objectPointMat = Mat.FromArray(objectPoints[i]);
            using var rvec = new Mat(3, 1, MatType.CV_64FC1);
            rvec.Set(0, 0, rotations[i].Item0);
            rvec.Set(1, 0, rotations[i].Item1);
            rvec.Set(2, 0, rotations[i].Item2);
            using var tvec = new Mat(3, 1, MatType.CV_64FC1);
            tvec.Set(0, 0, translations[i].Item0);
            tvec.Set(1, 0, translations[i].Item1);
            tvec.Set(2, 0, translations[i].Item2);
            using var imagePointMat = new Mat();
            using var jacobian = new Mat();

            Cv2.ProjectPoints(objectPointMat, rvec, tvec, cameraMatrix, distCoeffs, imagePointMat, jacobian, 0.0);
            projectedViews.Add(ReadPoint2fVector(imagePointMat));
        }

        return projectedViews;
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
