using System.Numerics;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Calibration;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public sealed class HandEyeCalibrationOperatorTests
{
    [Fact]
    public async Task ExecuteAsync_WithSyntheticEyeInHandSamples_ShouldRecoverHandEyeMatrix()
    {
        var sut = new HandEyeCalibrationOperator(Substitute.For<ILogger<HandEyeCalibrationOperator>>());
        var op = CreateCalibrationOperator();
        var (robotPoses, boardPoses, expectedCameraToTool) = CreateSyntheticEyeInHandDataset();

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["RobotPoses"] = robotPoses,
            ["CalibrationBoardPoses"] = boardPoses
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        var estimated = result.OutputData!["HandEyeMatrix"].Should().BeOfType<Matrix4x4>().Subject;

        TranslationError(estimated, expectedCameraToTool).Should().BeLessThan(0.005);
        RotationErrorDegrees(estimated, expectedCameraToTool).Should().BeLessThan(0.5);
        result.OutputData["CalibrationQuality"].Should().Be("good");
        result.OutputData["HtmlReport"].Should().BeOfType<string>().Which.Should().Contain("Hand-Eye Calibration Validation Report");
    }

    [Fact]
    public async Task ValidatorOperator_WithConsistentMatrix_ShouldProduceReport()
    {
        var calibrationOperator = new HandEyeCalibrationOperator(Substitute.For<ILogger<HandEyeCalibrationOperator>>());
        var validatorOperator = new HandEyeCalibrationValidatorOperator(Substitute.For<ILogger<HandEyeCalibrationValidatorOperator>>());
        var calibrationOp = CreateCalibrationOperator();
        var validatorOp = CreateValidatorOperator();
        var (robotPoses, boardPoses, _) = CreateSyntheticEyeInHandDataset();

        var calibrationResult = await calibrationOperator.ExecuteAsync(calibrationOp, new Dictionary<string, object>
        {
            ["RobotPoses"] = robotPoses,
            ["CalibrationBoardPoses"] = boardPoses
        });

        calibrationResult.IsSuccess.Should().BeTrue(calibrationResult.ErrorMessage);

        var validationResult = await validatorOperator.ExecuteAsync(validatorOp, new Dictionary<string, object>
        {
            ["RobotPoses"] = robotPoses,
            ["CalibrationBoardPoses"] = boardPoses,
            ["HandEyeMatrix"] = calibrationResult.OutputData!["HandEyeMatrix"]
        });

        validationResult.IsSuccess.Should().BeTrue(validationResult.ErrorMessage);
        validationResult.OutputData!["Quality"].Should().Be("good");
        Convert.ToDouble(validationResult.OutputData["MeanError"]).Should().BeLessThan(0.001);
        validationResult.OutputData["SuggestedValidationPoses"].Should().BeOfType<string>();
    }

    private static Operator CreateCalibrationOperator()
    {
        var op = new Operator("handeye", OperatorType.HandEyeCalibration, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("CalibrationType", "eye_in_hand", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Method", "TSAI", "string"));
        op.AddParameter(TestHelpers.CreateParameter("CameraMatrix", string.Empty, "string"));
        op.AddParameter(TestHelpers.CreateParameter("DistortionCoeffs", string.Empty, "string"));
        return op;
    }

    private static Operator CreateValidatorOperator()
    {
        var op = new Operator("handeye_validator", OperatorType.HandEyeCalibrationValidator, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("CalibrationType", "eye_in_hand", "string"));
        return op;
    }

    private static (List<Matrix4x4> RobotPoses, List<Matrix4x4> BoardPoses, Matrix4x4 ExpectedCameraToTool) CreateSyntheticEyeInHandDataset()
    {
        var expectedCameraToTool = CreateTransform(new Vector3(0.030f, -0.015f, 0.080f), 5f, -8f, 12f);
        var targetToBase = CreateTransform(new Vector3(0.450f, 0.120f, 0.250f), 0f, 0f, 0f);
        var inverseCameraToTool = Invert(expectedCameraToTool);

        var robotPoses = new List<Matrix4x4>();
        var boardPoses = new List<Matrix4x4>();

        var samples = new[]
        {
            (new Vector3(0.10f, 0.02f, 0.35f), 0f, 5f, -8f),
            (new Vector3(0.12f, -0.04f, 0.32f), 6f, -4f, 15f),
            (new Vector3(0.08f, 0.06f, 0.38f), -10f, 7f, -12f),
            (new Vector3(0.15f, -0.01f, 0.40f), 8f, 9f, 4f),
            (new Vector3(0.18f, 0.03f, 0.34f), -6f, -7f, 18f),
            (new Vector3(0.11f, -0.06f, 0.36f), 11f, 3f, -16f),
            (new Vector3(0.16f, 0.08f, 0.42f), -12f, 10f, 9f),
            (new Vector3(0.09f, -0.02f, 0.31f), 4f, -9f, 20f),
            (new Vector3(0.14f, 0.01f, 0.39f), -8f, 6f, -5f)
        };

        foreach (var (translation, roll, pitch, yaw) in samples)
        {
            var baseToTool = CreateTransform(translation, roll, pitch, yaw);
            var targetToCamera = targetToBase * baseToTool * inverseCameraToTool;
            var cameraToTarget = Invert(targetToCamera);

            robotPoses.Add(baseToTool);
            boardPoses.Add(cameraToTarget);
        }

        return (robotPoses, boardPoses, expectedCameraToTool);
    }

    private static Matrix4x4 CreateTransform(Vector3 translation, float rollDeg, float pitchDeg, float yawDeg)
    {
        var rotation = Matrix4x4.CreateFromYawPitchRoll(
            DegreesToRadians(yawDeg),
            DegreesToRadians(pitchDeg),
            DegreesToRadians(rollDeg));
        rotation.M41 = translation.X;
        rotation.M42 = translation.Y;
        rotation.M43 = translation.Z;
        rotation.M44 = 1f;
        return rotation;
    }

    private static Matrix4x4 Invert(Matrix4x4 matrix)
    {
        Matrix4x4.Invert(matrix, out var inverted).Should().BeTrue();
        return inverted;
    }

    private static double TranslationError(Matrix4x4 estimated, Matrix4x4 expected)
    {
        var estimatedTranslation = new Vector3(estimated.M41, estimated.M42, estimated.M43);
        var expectedTranslation = new Vector3(expected.M41, expected.M42, expected.M43);
        return (estimatedTranslation - expectedTranslation).Length();
    }

    private static double RotationErrorDegrees(Matrix4x4 estimated, Matrix4x4 expected)
    {
        var estimatedQ = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(estimated));
        var expectedQ = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(expected));
        var dot = Math.Abs((double)Quaternion.Dot(estimatedQ, expectedQ));
        dot = Math.Clamp(dot, 0d, 1d);
        return 2d * Math.Acos(dot) * (180d / Math.PI);
    }

    private static float DegreesToRadians(float degrees) => degrees * (MathF.PI / 180f);
}
