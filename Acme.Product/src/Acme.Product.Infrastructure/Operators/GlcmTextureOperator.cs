using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.ImageProcessing;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "GLCM Texture Features",
    Description = "Compute Gray-Level Co-occurrence Matrix (GLCM) texture features.",
    Category = "Texture",
    IconName = "texture",
    Keywords = new[] { "Texture", "GLCM", "Contrast", "Correlation", "Energy", "Entropy" },
    Version = "1.0.0"
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[OutputPort("Contrast", "Contrast", PortDataType.Float)]
[OutputPort("Correlation", "Correlation", PortDataType.Float)]
[OutputPort("Energy", "Energy", PortDataType.Float)]
[OutputPort("Homogeneity", "Homogeneity", PortDataType.Float)]
[OutputPort("Entropy", "Entropy", PortDataType.Float)]
[OutputPort("PerDirection", "Per Direction Features", PortDataType.Any)]
[OperatorParam("Levels", "Quantization Levels", "int", DefaultValue = 16, Min = 2, Max = 256)]
[OperatorParam("Distance", "Distance", "int", DefaultValue = 1, Min = 1, Max = 64)]
[OperatorParam("DirectionsDeg", "Directions (deg)", "string", DefaultValue = "0,45,90,135")]
[OperatorParam("Symmetric", "Symmetric", "bool", DefaultValue = true)]
[OperatorParam("Normalize", "Normalize", "bool", DefaultValue = true)]
[OperatorParam("RoiX", "ROI X", "int", DefaultValue = 0, Min = 0)]
[OperatorParam("RoiY", "ROI Y", "int", DefaultValue = 0, Min = 0)]
[OperatorParam("RoiW", "ROI W", "int", DefaultValue = 0, Min = 0)]
[OperatorParam("RoiH", "ROI H", "int", DefaultValue = 0, Min = 0)]
public sealed class GlcmTextureOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.GlcmTexture;

    public GlcmTextureOperator(ILogger<GlcmTextureOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is required"));
        }

        var src = imageWrapper.MatReadOnly;
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid"));
        }

        var levels = GetIntParam(@operator, "Levels", 16, min: 2, max: 256);
        var distance = GetIntParam(@operator, "Distance", 1, min: 1, max: 64);
        var directionsDeg = GetStringParam(@operator, "DirectionsDeg", "0,45,90,135");
        var symmetric = GetBoolParam(@operator, "Symmetric", true);
        var normalize = GetBoolParam(@operator, "Normalize", true);

        var roi = ResolveRoi(@operator, src.Width, src.Height);
        if (roi.Width <= 0 || roi.Height <= 0)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("ROI is invalid"));
        }

        IReadOnlyList<GlcmDirection> directions;
        try
        {
            directions = ParseDirections(directionsDeg);
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"DirectionsDeg invalid: {ex.Message}"));
        }

        GlcmFeatures mean;
        IReadOnlyDictionary<GlcmDirection, GlcmFeatures> perDirection;
        try
        {
            using var roiMat = new Mat(src, roi);
            (mean, perDirection) = GlcmTexture.Compute(
                roiMat,
                levels: levels,
                distance: distance,
                directions: directions,
                symmetric: symmetric,
                normalize: normalize);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "GLCM compute failed.");
            return Task.FromResult(OperatorExecutionOutput.Failure($"GLCM compute failed: {ex.Message}"));
        }

        var per = perDirection.ToDictionary(
            kvp => kvp.Key.Degrees.ToString(),
            kvp => (object)new Dictionary<string, object>
            {
                ["Contrast"] = kvp.Value.Contrast,
                ["Correlation"] = kvp.Value.Correlation,
                ["Energy"] = kvp.Value.Energy,
                ["Homogeneity"] = kvp.Value.Homogeneity,
                ["Entropy"] = kvp.Value.Entropy
            });

        var output = new Dictionary<string, object>
        {
            ["Contrast"] = mean.Contrast,
            ["Correlation"] = mean.Correlation,
            ["Energy"] = mean.Energy,
            ["Homogeneity"] = mean.Homogeneity,
            ["Entropy"] = mean.Entropy,
            ["PerDirection"] = per
        };

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        _ = GetIntParam(@operator, "Levels", 16, min: 2, max: 256);
        _ = GetIntParam(@operator, "Distance", 1, min: 1, max: 64);
        var directions = GetStringParam(@operator, "DirectionsDeg", "0,45,90,135");
        try
        {
            _ = ParseDirections(directions);
        }
        catch (Exception ex)
        {
            return ValidationResult.Invalid($"DirectionsDeg invalid: {ex.Message}");
        }

        return ValidationResult.Valid();
    }

    private static IReadOnlyList<GlcmDirection> ParseDirections(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return GlcmTexture.GetDefaultDirections();

        var parts = s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return GlcmTexture.GetDefaultDirections();

        var list = new List<GlcmDirection>();
        foreach (var p in parts)
        {
            if (!int.TryParse(p, out var deg))
            {
                throw new FormatException($"Cannot parse direction: \"{p}\"");
            }

            deg %= 360;
            if (deg < 0) deg += 360;

            // Support common 4 directions. Anything else is rejected for now.
            list.Add(deg switch
            {
                0 => new GlcmDirection(1, 0, 0),
                45 => new GlcmDirection(1, -1, 45),
                90 => new GlcmDirection(0, -1, 90),
                135 => new GlcmDirection(-1, -1, 135),
                _ => throw new ArgumentOutOfRangeException(nameof(s), $"Unsupported direction: {deg}. Use 0,45,90,135.")
            });
        }

        return list;
    }

    private Rect ResolveRoi(Operator @operator, int width, int height)
    {
        var x = GetIntParam(@operator, "RoiX", 0, min: 0, max: width);
        var y = GetIntParam(@operator, "RoiY", 0, min: 0, max: height);
        var w = GetIntParam(@operator, "RoiW", 0, min: 0, max: width);
        var h = GetIntParam(@operator, "RoiH", 0, min: 0, max: height);

        if (w <= 0) w = width - x;
        if (h <= 0) h = height - y;

        w = Math.Clamp(w, 0, width - x);
        h = Math.Clamp(h, 0, height - y);

        return new Rect(x, y, w, h);
    }
}
