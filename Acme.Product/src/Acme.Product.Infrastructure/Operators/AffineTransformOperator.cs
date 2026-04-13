// AffineTransformOperator.cs
// 仿射变换算子
// 执行旋转、缩放、平移等仿射变换
// 作者：蘅芜君
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

            if (!TryParsePointArray(srcPointsJson, out var srcPoints, out var srcError) || srcPoints.Length < 3)
            {
                return Task.FromResult(OperatorExecutionOutput.Failure($"SrcPoints must contain at least 3 points. {srcError}"));
            }

            if (!TryParsePointArray(dstPointsJson, out var dstPoints, out var dstError) || dstPoints.Length < 3)
            {
                return Task.FromResult(OperatorExecutionOutput.Failure($"DstPoints must contain at least 3 points. {dstError}"));
            }

            if (!TryEnsureNonCollinear(srcPoints.Take(3).ToArray(), "SrcPoints", out var srcDegenerateError))
            {
                return Task.FromResult(OperatorExecutionOutput.Failure(srcDegenerateError));
            }

            if (!TryEnsureNonCollinear(dstPoints.Take(3).ToArray(), "DstPoints", out var dstDegenerateError))
            {
                return Task.FromResult(OperatorExecutionOutput.Failure(dstDegenerateError));
            }

            affineMatrix = Cv2.GetAffineTransform(srcPoints.Take(3).ToArray(), dstPoints.Take(3).ToArray());
            if (!TryValidateAffineMatrix(affineMatrix, out var matrixError))
            {
                affineMatrix.Dispose();
                return Task.FromResult(OperatorExecutionOutput.Failure(matrixError));
            }
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
            if (!TryParsePointArray(srcPoints, out var src, out var srcError) || src.Length < 3)
            {
                return ValidationResult.Invalid($"SrcPoints must contain at least 3 points. {srcError}");
            }

            if (!TryParsePointArray(dstPoints, out var dst, out var dstError) || dst.Length < 3)
            {
                return ValidationResult.Invalid($"DstPoints must contain at least 3 points. {dstError}");
            }

            if (!TryEnsureNonCollinear(src.Take(3).ToArray(), "SrcPoints", out var srcDegenerateError))
            {
                return ValidationResult.Invalid(srcDegenerateError);
            }

            if (!TryEnsureNonCollinear(dst.Take(3).ToArray(), "DstPoints", out var dstDegenerateError))
            {
                return ValidationResult.Invalid(dstDegenerateError);
            }
        }

        var scale = GetDoubleParam(@operator, "Scale", 1.0);
        if (scale <= 0)
        {
            return ValidationResult.Invalid("Scale must be greater than 0");
        }

        return ValidationResult.Valid();
    }

    private static bool TryParsePointArray(string json, out Point2f[] points, out string error)
    {
        points = Array.Empty<Point2f>();
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "JSON is empty.";
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                error = "JSON root must be an array.";
                return false;
            }

            var parsed = new List<Point2f>();
            var index = 0;
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() >= 2)
                {
                    if (!TryParseElementAsFloat(element[0], out var x, out var xError))
                    {
                        error = $"Point[{index}].X {xError}";
                        return false;
                    }

                    if (!TryParseElementAsFloat(element[1], out var y, out var yError))
                    {
                        error = $"Point[{index}].Y {yError}";
                        return false;
                    }

                    parsed.Add(new Point2f(x, y));
                }
                else if (element.ValueKind == JsonValueKind.Object)
                {
                    if (!TryGetPropertyAsFloat(element, out var x, out var xError, "x", "X"))
                    {
                        error = $"Point[{index}].X {xError}";
                        return false;
                    }

                    if (!TryGetPropertyAsFloat(element, out var y, out var yError, "y", "Y"))
                    {
                        error = $"Point[{index}].Y {yError}";
                        return false;
                    }

                    parsed.Add(new Point2f(x, y));
                }
                else
                {
                    error = $"Point[{index}] must be an array or object.";
                    return false;
                }

                index++;
            }

            points = parsed.ToArray();
            if (points.Length == 0)
            {
                error = "Point array is empty.";
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = $"Invalid point JSON: {ex.Message}";
            return false;
        }
    }

    private static bool TryParseElementAsFloat(JsonElement element, out float value, out string error)
    {
        value = 0f;
        error = string.Empty;
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                var parsed = element.GetSingle();
                if (!float.IsFinite(parsed))
                {
                    error = "must be finite.";
                    return false;
                }

                value = parsed;
                return true;
            case JsonValueKind.String:
                if (float.TryParse(element.GetString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var fromString) &&
                    float.IsFinite(fromString))
                {
                    value = fromString;
                    return true;
                }

                error = "must be a valid number.";
                return false;
            default:
                error = "must be a number or numeric string.";
                return false;
        }
    }

    private static bool TryGetPropertyAsFloat(JsonElement obj, out float value, out string error, params string[] names)
    {
        value = 0f;
        error = "is required.";
        foreach (var name in names)
        {
            if (!obj.TryGetProperty(name, out var propertyValue))
            {
                continue;
            }

            return TryParseElementAsFloat(propertyValue, out value, out error);
        }

        return false;
    }

    private static bool TryEnsureNonCollinear(Point2f[] points, string label, out string error)
    {
        if (points.Length < 3)
        {
            error = $"{label} must contain at least 3 points.";
            return false;
        }

        var twiceArea =
            (points[1].X - points[0].X) * (points[2].Y - points[0].Y) -
            (points[2].X - points[0].X) * (points[1].Y - points[0].Y);
        if (Math.Abs(twiceArea) <= 1e-6)
        {
            error = $"{label} is degenerate (collinear points).";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateAffineMatrix(Mat matrix, out string error)
    {
        error = string.Empty;
        if (matrix.Empty() || matrix.Rows != 2 || matrix.Cols != 3)
        {
            error = "Affine matrix is invalid.";
            return false;
        }

        var a = matrix.At<double>(0, 0);
        var b = matrix.At<double>(0, 1);
        var c = matrix.At<double>(0, 2);
        var d = matrix.At<double>(1, 0);
        var e = matrix.At<double>(1, 1);
        var f = matrix.At<double>(1, 2);
        if (!double.IsFinite(a) || !double.IsFinite(b) || !double.IsFinite(c) ||
            !double.IsFinite(d) || !double.IsFinite(e) || !double.IsFinite(f))
        {
            error = "Affine matrix contains non-finite values.";
            return false;
        }

        var det = a * e - b * d;
        if (Math.Abs(det) <= 1e-12)
        {
            error = "Affine matrix is singular or near-singular.";
            return false;
        }

        return true;
    }
}

