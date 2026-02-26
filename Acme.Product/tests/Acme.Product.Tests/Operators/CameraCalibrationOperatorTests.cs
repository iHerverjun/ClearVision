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
    public async Task ExecuteAsync_SingleImage_ShouldOutputCameraMatrixInCalibrationData()
    {
        var op = new Operator("Calib", OperatorType.CameraCalibration, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("PatternType", "Chessboard", "string"));
        op.AddParameter(TestHelpers.CreateParameter("BoardWidth", "BoardWidth", "int", 9, 2, 30, true));
        op.AddParameter(TestHelpers.CreateParameter("BoardHeight", "BoardHeight", "int", 6, 2, 30, true));
        op.AddParameter(TestHelpers.CreateParameter("SquareSize", "SquareSize", "double", 25.0, 0.1, 1000.0, true));
        op.AddParameter(TestHelpers.CreateParameter("Mode", "SingleImage", "string"));

        using var chessboard = CreateChessboardImage(9, 6, 40);
        var inputs = TestHelpers.CreateImageInputs(chessboard);
        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("CalibrationData");

        var calibrationJson = result.OutputData!["CalibrationData"].ToString();
        calibrationJson.Should().NotBeNullOrWhiteSpace();

        using var doc = JsonDocument.Parse(calibrationJson!);
        doc.RootElement.TryGetProperty("CameraMatrix", out var matrixElement).Should().BeTrue();
        matrixElement.ValueKind.Should().Be(JsonValueKind.Array);
        matrixElement.GetArrayLength().Should().Be(3);
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("Calib", OperatorType.CameraCalibration, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
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

        // Mild blur helps corner detector stability on synthetic image.
        Cv2.GaussianBlur(mat, mat, new Size(3, 3), 0);
        return new ImageWrapper(mat);
    }
}
