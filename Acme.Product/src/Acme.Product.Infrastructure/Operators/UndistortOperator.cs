using System.Text.Json;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Undistort",
    Description = "Correct lens distortion using calibration data.",
    Category = "Calibration",
    IconName = "undistort",
    Keywords = new[] { "Undistort", "Distortion", "Calibration" }
)]
[InputPort("Image", "Input Image", PortDataType.Image, IsRequired = true)]
[InputPort("CalibrationData", "Calibration Data", PortDataType.String, IsRequired = false)]
[OutputPort("Image", "Undistorted Image", PortDataType.Image)]
[OperatorParam("CalibrationFile", "Calibration File", "file", DefaultValue = "")]
public class UndistortOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.Undistort;

    public UndistortOperator(ILogger<UndistortOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, "Image", out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is required."));
        }

        if (!TryResolveCalibrationData(@operator, inputs, out var calibrationData))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Calibration data is required."));
        }

        if (!TryParseCalibrationData(calibrationData!, out var cameraMatrix, out var distCoeffs, out var parseError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"Invalid calibration data: {parseError}"));
        }

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid."));
        }

        var dst = new Mat();
        using var cameraMat = new Mat(3, 3, MatType.CV_64FC1, cameraMatrix);
        using var distMat = distCoeffs.Length > 0
            ? new Mat(1, distCoeffs.Length, MatType.CV_64FC1, distCoeffs)
            : new Mat();

        Cv2.Undistort(src, dst, cameraMat, distMat);

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst, new Dictionary<string, object>
        {
            { "Applied", true },
            { "Message", "Undistortion applied using provided calibration data." }
        })));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        return ValidationResult.Valid();
    }

    private bool TryResolveCalibrationData(
        Operator @operator,
        Dictionary<string, object>? inputs,
        out string? calibrationData)
    {
        calibrationData = null;
        if (inputs != null &&
            inputs.TryGetValue("CalibrationData", out var calibrationObj) &&
            calibrationObj is string calibrationText &&
            !string.IsNullOrWhiteSpace(calibrationText))
        {
            calibrationData = calibrationText;
            return true;
        }

        var calibrationFile = GetStringParam(@operator, "CalibrationFile", "");
        if (!string.IsNullOrWhiteSpace(calibrationFile) && File.Exists(calibrationFile))
        {
            calibrationData = File.ReadAllText(calibrationFile);
            return true;
        }

        return false;
    }

    private static bool TryParseCalibrationData(
        string calibrationData,
        out double[,] cameraMatrix,
        out double[] distCoeffs,
        out string? error)
    {
        cameraMatrix = new double[3, 3];
        distCoeffs = Array.Empty<double>();
        error = null;

        try
        {
            using var doc = JsonDocument.Parse(calibrationData);
            var root = doc.RootElement;

            if (!TryParseCameraMatrix(root, out cameraMatrix))
            {
                error = "CameraMatrix is missing or has unsupported format.";
                return false;
            }

            distCoeffs = TryParseDistCoeffs(root);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryParseCameraMatrix(JsonElement root, out double[,] cameraMatrix)
    {
        cameraMatrix = new double[3, 3];
        if (!root.TryGetProperty("CameraMatrix", out var matrixElement))
        {
            return false;
        }

        if (matrixElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var length = matrixElement.GetArrayLength();
        if (length == 9)
        {
            var index = 0;
            foreach (var value in matrixElement.EnumerateArray())
            {
                if (!TryReadNumber(value, out var number))
                {
                    return false;
                }

                cameraMatrix[index / 3, index % 3] = number;
                index++;
            }

            return true;
        }

        if (length == 3)
        {
            var rowIndex = 0;
            foreach (var row in matrixElement.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() != 3)
                {
                    return false;
                }

                var colIndex = 0;
                foreach (var value in row.EnumerateArray())
                {
                    if (!TryReadNumber(value, out var number))
                    {
                        return false;
                    }

                    cameraMatrix[rowIndex, colIndex++] = number;
                }

                rowIndex++;
            }

            return true;
        }

        return false;
    }

    private static double[] TryParseDistCoeffs(JsonElement root)
    {
        if (!root.TryGetProperty("DistCoeffs", out var distElement) || distElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<double>();
        }

        var values = new List<double>();
        foreach (var item in distElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Array)
            {
                foreach (var nested in item.EnumerateArray())
                {
                    if (TryReadNumber(nested, out var nestedValue))
                    {
                        values.Add(nestedValue);
                    }
                }
            }
            else if (TryReadNumber(item, out var value))
            {
                values.Add(value);
            }
        }

        return values.ToArray();
    }

    private static bool TryReadNumber(JsonElement element, out double value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                value = element.GetDouble();
                return true;
            case JsonValueKind.String:
                return double.TryParse(element.GetString(), out value);
            default:
                value = 0;
                return false;
        }
    }
}
