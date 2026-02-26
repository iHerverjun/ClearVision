using System.Globalization;
using System.Text.Json;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "仿射变换",
    Description = "Applies 2D affine transform using 3-point or rotate-scale-translate mode.",
    Category = "图像处理",
    IconName = "affine",
    Keywords = new[] { "affine", "warp", "rotate", "scale", "translate" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OutputPort("TransformMatrix", "Transform Matrix", PortDataType.Any)]
[OperatorParam("Mode", "Mode", "enum", DefaultValue = "RotateScaleTranslate", Options = new[] { "ThreePoint|ThreePoint", "RotateScaleTranslate|RotateScaleTranslate" })]
[OperatorParam("SrcPoints", "Source Points", "string", DefaultValue = "[[0,0],[100,0],[0,100]]")]
[OperatorParam("DstPoints", "Destination Points", "string", DefaultValue = "[[0,0],[100,0],[0,100]]")]
[OperatorParam("Angle", "Angle", "double", DefaultValue = 0.0, Min = -3600.0, Max = 3600.0)]
[OperatorParam("Scale", "Scale", "double", DefaultValue = 1.0, Min = 0.001, Max = 1000.0)]
[OperatorParam("TranslateX", "Translate X", "double", DefaultValue = 0.0, Min = -100000.0, Max = 100000.0)]
[OperatorParam("TranslateY", "Translate Y", "double", DefaultValue = 0.0, Min = -100000.0, Max = 100000.0)]
[OperatorParam("OutputWidth", "Output Width", "int", DefaultValue = 0, Min = 0, Max = 10000)]
[OperatorParam("OutputHeight", "Output Height", "int", DefaultValue = 0, Min = 0, Max = 10000)]
public class AffineTransformOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.AffineTransform;

    public AffineTransformOperator(ILogger<AffineTransformOperator> logger) : base(logger)
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

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid"));
        }

        var mode = GetStringParam(@operator, "Mode", "RotateScaleTranslate");
        var width = GetIntParam(@operator, "OutputWidth", 0, 0, 10000);
        var height = GetIntParam(@operator, "OutputHeight", 0, 0, 10000);

        if (width <= 0)
        {
            width = src.Width;
        }

        if (height <= 0)
        {
            height = src.Height;
        }

        Mat affineMatrix;

        if (mode.Equals("ThreePoint", StringComparison.OrdinalIgnoreCase))
        {
            var srcPointsJson = GetStringParam(@operator, "SrcPoints", string.Empty);
            var dstPointsJson = GetStringParam(@operator, "DstPoints", string.Empty);

            if (!TryParsePointArray(srcPointsJson, out var srcPoints) || srcPoints.Length < 3)
            {
                return Task.FromResult(OperatorExecutionOutput.Failure("SrcPoints must contain at least 3 points"));
            }

            if (!TryParsePointArray(dstPointsJson, out var dstPoints) || dstPoints.Length < 3)
            {
                return Task.FromResult(OperatorExecutionOutput.Failure("DstPoints must contain at least 3 points"));
            }

            affineMatrix = Cv2.GetAffineTransform(srcPoints.Take(3).ToArray(), dstPoints.Take(3).ToArray());
        }
        else
        {
            var angle = GetDoubleParam(@operator, "Angle", 0.0, -3600.0, 3600.0);
            var scale = GetDoubleParam(@operator, "Scale", 1.0, 0.001, 1000.0);
            var tx = GetDoubleParam(@operator, "TranslateX", 0.0, -100000.0, 100000.0);
            var ty = GetDoubleParam(@operator, "TranslateY", 0.0, -100000.0, 100000.0);

            var center = new Point2f(src.Width / 2f, src.Height / 2f);
            affineMatrix = Cv2.GetRotationMatrix2D(center, angle, scale);
            affineMatrix.Set(0, 2, affineMatrix.At<double>(0, 2) + tx);
            affineMatrix.Set(1, 2, affineMatrix.At<double>(1, 2) + ty);
        }

        var transformed = new Mat();
        Cv2.WarpAffine(src, transformed, affineMatrix, new Size(width, height), InterpolationFlags.Linear, BorderTypes.Constant, Scalar.Black);

        var matrixArray = new[]
        {
            new[] { affineMatrix.At<double>(0, 0), affineMatrix.At<double>(0, 1), affineMatrix.At<double>(0, 2) },
            new[] { affineMatrix.At<double>(1, 0), affineMatrix.At<double>(1, 1), affineMatrix.At<double>(1, 2) }
        };

        var output = new Dictionary<string, object>
        {
            { "TransformMatrix", matrixArray }
        };

        affineMatrix.Dispose();

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(transformed, output)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var mode = GetStringParam(@operator, "Mode", "RotateScaleTranslate");
        var validModes = new[] { "ThreePoint", "RotateScaleTranslate" };
        if (!validModes.Contains(mode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Mode must be ThreePoint or RotateScaleTranslate");
        }

        if (mode.Equals("ThreePoint", StringComparison.OrdinalIgnoreCase))
        {
            var srcPoints = GetStringParam(@operator, "SrcPoints", string.Empty);
            var dstPoints = GetStringParam(@operator, "DstPoints", string.Empty);
            if (!TryParsePointArray(srcPoints, out var src) || src.Length < 3)
            {
                return ValidationResult.Invalid("SrcPoints must contain at least 3 points");
            }

            if (!TryParsePointArray(dstPoints, out var dst) || dst.Length < 3)
            {
                return ValidationResult.Invalid("DstPoints must contain at least 3 points");
            }
        }

        var scale = GetDoubleParam(@operator, "Scale", 1.0);
        if (scale <= 0)
        {
            return ValidationResult.Invalid("Scale must be greater than 0");
        }

        return ValidationResult.Valid();
    }

    private static bool TryParsePointArray(string json, out Point2f[] points)
    {
        points = Array.Empty<Point2f>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var parsed = new List<Point2f>();
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() >= 2)
                {
                    var x = ParseElementAsFloat(element[0]);
                    var y = ParseElementAsFloat(element[1]);
                    parsed.Add(new Point2f(x, y));
                }
                else if (element.ValueKind == JsonValueKind.Object)
                {
                    var x = GetPropertyAsFloat(element, "x", "X");
                    var y = GetPropertyAsFloat(element, "y", "Y");
                    parsed.Add(new Point2f(x, y));
                }
            }

            points = parsed.ToArray();
            return points.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static float ParseElementAsFloat(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetSingle(),
            JsonValueKind.String => float.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0f,
            _ => 0f
        };
    }

    private static float GetPropertyAsFloat(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (!obj.TryGetProperty(name, out var value))
            {
                continue;
            }

            return ParseElementAsFloat(value);
        }

        return 0f;
    }
}
