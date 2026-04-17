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
            leftPerViewErrors,
            rightPerViewErrors,
            pairKeys,
            Array.Empty<string>()
        }) as CalibrationQualityV2;

        quality.Should().NotBeNull();
        quality!.Accepted.Should().BeFalse();
        quality.MaxError.Should().BeApproximately(0.91, 1e-6);
        quality.Diagnostics.Should().Contain(d => d.Contains("Quality gate failed", StringComparison.OrdinalIgnoreCase));
        quality.Diagnostics.Should().Contain(d => d.Contains("pair_012", StringComparison.OrdinalIgnoreCase));
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
}
