using System.Numerics;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.Calibration;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "手眼标定验证",
    Description = "Validates a hand-eye calibration matrix and produces quality metrics, HTML report, and pose suggestions.",
    Category = "Calibration",
    IconName = "hand-eye-validation",
    Keywords = new[] { "handeye", "validation", "calibration report", "手眼标定验证" },
    Version = "1.0.0"
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
[InputPort("HandEyeMatrix", "Hand Eye Matrix", PortDataType.Any, IsRequired = true)]
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

        var matrixError = "HandEyeMatrix is required.";
        if (inputs == null || !inputs.TryGetValue("HandEyeMatrix", out var matrixObj) ||
            !Pose3DSerialization.TryParsePose(matrixObj, out Matrix4x4 handEyeMatrix, out matrixError))
        {
            return OperatorExecutionOutput.Failure(matrixError);
        }

        try
        {
            var report = await RunCpuBoundWork(
                () => HandEyeCalibrationValidator.Validate(
                    robotPoses,
                    boardPoses,
                    handEyeMatrix,
                    HandEyeCalibrationOperator.ParseCalibrationType(@operator)),
                cancellationToken);

            return OperatorExecutionOutput.Success(new Dictionary<string, object>
            {
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
}
