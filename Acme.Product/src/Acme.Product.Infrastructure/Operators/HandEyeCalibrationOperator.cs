using System.Numerics;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.Calibration;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "手眼标定",
    Description = "Solves eye-in-hand or simplified eye-to-hand calibration from robot poses and calibration-board poses.",
    Category = "Calibration",
    IconName = "hand-eye-calibration",
    Keywords = new[] { "handeye", "robot", "calibration", "AX=XB", "手眼标定" },
    Version = "1.0.0"
)]
[AlgorithmInfo(
    Name = "OpenCV Hand-Eye Calibration",
    CoreApi = "OpenCvSharp.Cv2.CalibrateHandEye",
    TimeComplexity = "O(N)",
    SpaceComplexity = "O(N)",
    Dependencies = new[] { "OpenCvSharp" }
)]
[InputPort("RobotPoses", "Robot Poses", PortDataType.Any, IsRequired = true)]
[InputPort("CalibrationBoardPoses", "Calibration Board Poses", PortDataType.Any, IsRequired = true)]
[OutputPort("HandEyeMatrix", "Hand Eye Matrix", PortDataType.Any)]
[OutputPort("InverseHandEyeMatrix", "Inverse Hand Eye Matrix", PortDataType.Any)]
[OutputPort("ReprojectionError", "Reprojection Error", PortDataType.Float)]
[OutputPort("CalibrationQuality", "Calibration Quality", PortDataType.String)]
[OutputPort("MatrixConvention", "Matrix Convention", PortDataType.String)]
[OutputPort("HtmlReport", "HTML Report", PortDataType.String)]
[OutputPort("Suggestions", "Suggestions", PortDataType.Any)]
[OutputPort("SuggestedValidationPoses", "Suggested Validation Poses", PortDataType.String)]
[OperatorParam("CalibrationType", "Calibration Type", "enum", DefaultValue = "eye_in_hand", Options = new[] { "eye_in_hand|Eye In Hand", "eye_to_hand|Eye To Hand" })]
[OperatorParam("Method", "Method", "enum", DefaultValue = "TSAI", Options = new[] { "TSAI|Tsai", "PARK|Park", "HORAUD|Horaud", "ANDREFF|Andreff", "DANIILIDIS|Daniilidis" })]
[OperatorParam("CameraMatrix", "Camera Matrix", "string", DefaultValue = "")]
[OperatorParam("DistortionCoeffs", "Distortion Coeffs", "string", DefaultValue = "")]
public sealed class HandEyeCalibrationOperator : OperatorBase
{
    public HandEyeCalibrationOperator(ILogger<HandEyeCalibrationOperator> logger)
        : base(logger)
    {
    }

    public override OperatorType OperatorType => OperatorType.HandEyeCalibration;

    protected override async Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryResolveInputs(inputs, out var robotPoses, out var boardPoses, out var inputError))
        {
            return OperatorExecutionOutput.Failure(inputError);
        }

        RobotHandEyeCalibrationResult result;
        try
        {
            result = await RunCpuBoundWork(
                () => HandEyeCalibrationSolver.Solve(robotPoses, boardPoses, ParseCalibrationType(@operator), ParseMethod(@operator)),
                cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Hand-eye calibration failed.");
            return OperatorExecutionOutput.Failure($"Hand-eye calibration failed: {ex.Message}");
        }

        if (!result.Success)
        {
            return OperatorExecutionOutput.Failure(result.ErrorMessage ?? "Hand-eye calibration failed.");
        }

        var output = new Dictionary<string, object>
        {
            ["HandEyeMatrix"] = result.HandEyeMatrix,
            ["InverseHandEyeMatrix"] = result.InverseHandEyeMatrix,
            ["ReprojectionError"] = result.Validation.MeanError,
            ["CalibrationQuality"] = result.Validation.Quality,
            ["MatrixConvention"] = result.MatrixConvention,
            ["HtmlReport"] = result.Validation.HtmlReport,
            ["Suggestions"] = result.Validation.Suggestions,
            ["SuggestedValidationPoses"] = result.Validation.SuggestedValidationPosesJson
        };

        return OperatorExecutionOutput.Success(output);
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        try
        {
            _ = ParseCalibrationType(@operator);
            _ = ParseMethod(@operator);
            return ValidationResult.Valid();
        }
        catch (Exception ex)
        {
            return ValidationResult.Invalid(ex.Message);
        }
    }

    internal static bool TryResolveInputs(
        Dictionary<string, object>? inputs,
        out List<Matrix4x4> robotPoses,
        out List<Matrix4x4> boardPoses,
        out string error)
    {
        robotPoses = [];
        boardPoses = [];
        error = string.Empty;

        if (inputs == null)
        {
            error = "RobotPoses and CalibrationBoardPoses are required.";
            return false;
        }

        if (!inputs.TryGetValue("RobotPoses", out var robotPoseObj) ||
            !Pose3DSerialization.TryParsePoseList(robotPoseObj, out robotPoses, out error))
        {
            return false;
        }

        if (!inputs.TryGetValue("CalibrationBoardPoses", out var boardPoseObj) ||
            !Pose3DSerialization.TryParsePoseList(boardPoseObj, out boardPoses, out error))
        {
            return false;
        }

        if (robotPoses.Count != boardPoses.Count)
        {
            error = "Robot pose count must match calibration board pose count.";
            return false;
        }

        return true;
    }

    internal static RobotHandEyeCalibrationType ParseCalibrationType(Operator @operator)
    {
        return GetCalibrationType(GetStringParamStatic(@operator, "CalibrationType", "eye_in_hand"));
    }

    internal static HandEyeCalibrationMethod ParseMethod(Operator @operator)
    {
        var raw = GetStringParamStatic(@operator, "Method", "TSAI");
        if (Enum.TryParse<HandEyeCalibrationMethod>(raw, true, out var method))
        {
            return method;
        }

        throw new InvalidOperationException("Method must be TSAI, PARK, HORAUD, ANDREFF or DANIILIDIS.");
    }

    internal static RobotHandEyeCalibrationType GetCalibrationType(string raw)
    {
        return raw.Trim().ToLowerInvariant() switch
        {
            "eye_in_hand" => RobotHandEyeCalibrationType.EyeInHand,
            "eye_to_hand" => RobotHandEyeCalibrationType.EyeToHand,
            _ => throw new InvalidOperationException("CalibrationType must be 'eye_in_hand' or 'eye_to_hand'.")
        };
    }

    private static string GetStringParamStatic(Operator @operator, string name, string defaultValue)
    {
        var parameter = @operator.Parameters.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        if (parameter?.Value == null)
        {
            return defaultValue;
        }

        return Convert.ToString(parameter.Value, System.Globalization.CultureInfo.InvariantCulture) ?? defaultValue;
    }
}
