using System.Numerics;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.Calibration;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Hand-Eye Calibration Validator",
    Description = "Validates a hand-eye CalibrationBundleV2 payload and produces quality metrics, HTML report, and pose suggestions.",
    Category = "Calibration",
    IconName = "hand-eye-validation",
    Keywords = new[] { "handeye", "validation", "calibration report" },
    Version = "1.0.1"
)]
[AlgorithmInfo(
    Name = "Hand-Eye Consistency Validation",
    CoreApi = "Pose consistency over static-reference transforms",
    TimeComplexity = "O(N)",
    SpaceComplexity = "O(N)",
    Dependencies = new[] { "System.Numerics" }
)]
[InputPort("RobotPoses", "Robot Poses", PortDataType.Any, IsRequired = true)]
[InputPort("CalibrationBoardPoses", "Calibration Board Poses", PortDataType.Any, IsRequired = true)]
[InputPort("CalibrationData", "Calibration Data", PortDataType.String, IsRequired = false)]
[OutputPort("CalibrationData", "Calibration Data", PortDataType.String)]
[OutputPort("MeanError", "Mean Error", PortDataType.Float)]
[OutputPort("MaxError", "Max Error", PortDataType.Float)]
[OutputPort("MeanRotationError", "Mean Rotation Error", PortDataType.Float)]
[OutputPort("Quality", "Quality", PortDataType.String)]
[OutputPort("HtmlReport", "HTML Report", PortDataType.String)]
[OutputPort("Suggestions", "Suggestions", PortDataType.Any)]
[OutputPort("SuggestedValidationPoses", "Suggested Validation Poses", PortDataType.String)]
[OperatorParam("CalibrationType", "Calibration Type", "enum", DefaultValue = "eye_in_hand", Options = new[] { "eye_in_hand|Eye In Hand", "eye_to_hand|Eye To Hand" })]
public sealed class HandEyeCalibrationValidatorOperator : OperatorBase
{
    public HandEyeCalibrationValidatorOperator(ILogger<HandEyeCalibrationValidatorOperator> logger)
        : base(logger)
    {
    }

    public override OperatorType OperatorType => OperatorType.HandEyeCalibrationValidator;

    protected override async Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!HandEyeCalibrationOperator.TryResolveInputs(inputs, out var robotPoses, out var boardPoses, out var inputError))
        {
            return OperatorExecutionOutput.Failure(inputError);
        }

        if (!TryResolveHandEyeMatrix(inputs, out var handEyeMatrix, out var sourceBundle, out var matrixError))
        {
            return OperatorExecutionOutput.Failure(matrixError);
        }

        var calibrationType = HandEyeCalibrationOperator.ParseCalibrationType(@operator);
        try
        {
            var report = await RunCpuBoundWork(
                () => HandEyeCalibrationValidator.Validate(robotPoses, boardPoses, handEyeMatrix, calibrationType),
                cancellationToken);

            var outputBundle = BuildOutputBundle(sourceBundle, handEyeMatrix, report, calibrationType, robotPoses.Count);
            var calibrationData = CalibrationBundleV2Json.Serialize(outputBundle);

            return OperatorExecutionOutput.Success(new Dictionary<string, object>
            {
                ["CalibrationData"] = calibrationData,
                ["MeanError"] = report.MeanError,
                ["MaxError"] = report.MaxError,
                ["MeanRotationError"] = report.MeanRotationErrorDegrees,
                ["Quality"] = report.Quality,
                ["HtmlReport"] = report.HtmlReport,
                ["Suggestions"] = report.Suggestions,
                ["SuggestedValidationPoses"] = report.SuggestedValidationPosesJson
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Hand-eye validation failed.");
            return OperatorExecutionOutput.Failure($"Hand-eye validation failed: {ex.Message}");
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        try
        {
            _ = HandEyeCalibrationOperator.ParseCalibrationType(@operator);
            return ValidationResult.Valid();
        }
        catch (Exception ex)
        {
            return ValidationResult.Invalid(ex.Message);
        }
    }

    private static bool TryResolveHandEyeMatrix(
        Dictionary<string, object>? inputs,
        out Matrix4x4 matrix,
        out CalibrationBundleV2? sourceBundle,
        out string error)
    {
        matrix = Matrix4x4.Identity;
        sourceBundle = null;
        error = "CalibrationData is required.";

        if (inputs == null)
        {
            return false;
        }

        if (inputs.TryGetValue("CalibrationData", out var calibrationObj) &&
            calibrationObj is string calibrationData &&
            !string.IsNullOrWhiteSpace(calibrationData))
        {
            if (!CalibrationBundleV2Json.TryDeserialize(calibrationData, out var bundle, out var deserializeError))
            {
                error = $"Invalid CalibrationData: {deserializeError}";
                return false;
            }

            if (bundle.CalibrationKind != CalibrationKindV2.HandEye)
            {
                error = $"CalibrationData is not HandEye kind: {bundle.CalibrationKind}.";
                return false;
            }

            if (!CalibrationBundleV2Json.TryRequireTransform3D(bundle, out var transform3D, out var requireError))
            {
                error = requireError;
                return false;
            }

            if (!CalibrationBundleV2PoseHelpers.TryToMatrix4x4(transform3D.Matrix, out matrix, out var matrixError))
            {
                error = matrixError;
                return false;
            }

            sourceBundle = bundle;
            error = string.Empty;
            return true;
        }

        return false;
    }

    private static CalibrationBundleV2 BuildOutputBundle(
        CalibrationBundleV2? sourceBundle,
        Matrix4x4 handEyeMatrix,
        HandEyeValidationReport report,
        RobotHandEyeCalibrationType calibrationType,
        int sampleCount)
    {
        var accepted = !string.Equals(report.Quality, "poor", StringComparison.OrdinalIgnoreCase);
        var diagnostics = new List<string>
        {
            $"quality={report.Quality}",
            $"validated_at={DateTime.UtcNow:O}"
        };
        diagnostics.AddRange(report.Suggestions.Take(3));

        var inverseMatrix = Matrix4x4.Invert(handEyeMatrix, out var inv)
            ? CalibrationBundleV2PoseHelpers.ToJaggedMatrix4x4(inv)
            : null;

        return new CalibrationBundleV2
        {
            CalibrationKind = CalibrationKindV2.HandEye,
            TransformModel = TransformModelV2.Rigid3D,
            SourceFrame = sourceBundle?.SourceFrame ?? "camera",
            TargetFrame = sourceBundle?.TargetFrame ?? (calibrationType == RobotHandEyeCalibrationType.EyeInHand ? "tool" : "base"),
            Unit = sourceBundle?.Unit ?? "m",
            Transform3D = new CalibrationTransform3DV2
            {
                Model = TransformModelV2.Rigid3D,
                Matrix = CalibrationBundleV2PoseHelpers.ToJaggedMatrix4x4(handEyeMatrix),
                InverseMatrix = inverseMatrix
            },
            Quality = new CalibrationQualityV2
            {
                Accepted = accepted,
                MeanError = report.MeanError,
                MaxError = report.MaxError,
                InlierCount = sampleCount,
                TotalSampleCount = sampleCount,
                Diagnostics = diagnostics
            },
            ProducerOperator = nameof(HandEyeCalibrationValidatorOperator)
        };
    }
}
