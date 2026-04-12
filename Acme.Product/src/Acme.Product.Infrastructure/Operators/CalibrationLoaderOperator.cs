using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.Calibration;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Calibration Loader",
    Description = "Loads CalibrationBundleV2 JSON and exposes typed calibration outputs.",
    Category = "Calibration",
    IconName = "file-open",
    Keywords = new[] { "calibration", "bundle", "json", "v2" }
)]
[OutputPort("CalibrationData", "Calibration Data", PortDataType.String)]
[OutputPort("CalibrationBundle", "Calibration Bundle", PortDataType.Any)]
[OutputPort("Accepted", "Accepted", PortDataType.Boolean)]
[OperatorParam("FilePath", "Calibration File Path", "file", DefaultValue = "")]
public class CalibrationLoaderOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.CalibrationLoader;

    public CalibrationLoaderOperator(ILogger<CalibrationLoaderOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var filePath = GetStringParam(@operator, "FilePath", string.Empty);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("FilePath is required."));
        }

        if (!File.Exists(filePath))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"Calibration file not found: {filePath}"));
        }

        try
        {
            var json = File.ReadAllText(filePath);
            if (!CalibrationBundleV2Json.TryDeserialize(json, out var bundle, out var error))
            {
                return Task.FromResult(OperatorExecutionOutput.Failure($"Invalid CalibrationBundleV2: {error}"));
            }

            var output = new Dictionary<string, object>
            {
                ["CalibrationData"] = json,
                ["CalibrationBundle"] = bundle,
                ["Accepted"] = bundle.Quality.Accepted
            };

            return Task.FromResult(OperatorExecutionOutput.Success(output));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load calibration bundle from {FilePath}", filePath);
            return Task.FromResult(OperatorExecutionOutput.Failure($"Failed to load calibration bundle: {ex.Message}"));
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var filePath = GetStringParam(@operator, "FilePath", string.Empty);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return ValidationResult.Invalid("FilePath is required.");
        }

        return ValidationResult.Valid();
    }
}
