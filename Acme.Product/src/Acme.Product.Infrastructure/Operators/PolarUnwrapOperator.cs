using System.Collections;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "极坐标展开",
    Description = "Unwraps annular image regions into rectangular view.",
    Category = "图像处理",
    IconName = "polar",
    Keywords = new[] { "polar", "unwrap", "ring", "annular" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[InputPort("Center", "Center", PortDataType.Point, IsRequired = false)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OperatorParam("CenterX", "Center X", "int", DefaultValue = 0)]
[OperatorParam("CenterY", "Center Y", "int", DefaultValue = 0)]
[OperatorParam("InnerRadius", "Inner Radius", "int", DefaultValue = 0, Min = 0, Max = 100000)]
[OperatorParam("OuterRadius", "Outer Radius", "int", DefaultValue = 100, Min = 1, Max = 100000)]
[OperatorParam("StartAngle", "Start Angle", "double", DefaultValue = 0.0, Min = -3600.0, Max = 3600.0)]
[OperatorParam("EndAngle", "End Angle", "double", DefaultValue = 360.0, Min = -3600.0, Max = 3600.0)]
[OperatorParam("OutputWidth", "Output Width", "int", DefaultValue = 0, Min = 0, Max = 20000)]
public class PolarUnwrapOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.PolarUnwrap;

    public PolarUnwrapOperator(ILogger<PolarUnwrapOperator> logger) : base(logger)
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

        var center = ResolveCenter(@operator, inputs, src.Width, src.Height);
        var innerRadius = GetIntParam(@operator, "InnerRadius", 0, 0, Math.Max(0, Math.Min(src.Width, src.Height) / 2));
        var outerRadius = GetIntParam(@operator, "OuterRadius", Math.Min(src.Width, src.Height) / 2, 1, Math.Min(src.Width, src.Height) / 2);
        var startAngle = GetDoubleParam(@operator, "StartAngle", 0.0, -3600.0, 3600.0);
        var endAngle = GetDoubleParam(@operator, "EndAngle", 360.0, -3600.0, 3600.0);
        var outputWidth = GetIntParam(@operator, "OutputWidth", 0, 0, 20000);

        if (outerRadius <= innerRadius)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("OuterRadius must be greater than InnerRadius"));
        }

        var angleSpan = endAngle - startAngle;
        if (Math.Abs(angleSpan) < 1e-6)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("StartAngle and EndAngle cannot be equal"));
        }

        if (angleSpan < 0)
        {
            angleSpan += 360.0;
        }

        var width = outputWidth > 0
            ? outputWidth
            : Math.Max(1, (int)Math.Round((2 * Math.PI * outerRadius) * (Math.Abs(angleSpan) / 360.0)));
        var height = Math.Max(1, outerRadius - innerRadius);

        using var mapX = new Mat(height, width, MatType.CV_32FC1);
        using var mapY = new Mat(height, width, MatType.CV_32FC1);

        for (var y = 0; y < height; y++)
        {
            var radius = innerRadius + y;
            for (var x = 0; x < width; x++)
            {
                var angle = startAngle + angleSpan * x / Math.Max(1, width - 1);
                var rad = angle * Math.PI / 180.0;
                var srcX = (float)(center.X + radius * Math.Cos(rad));
                var srcY = (float)(center.Y + radius * Math.Sin(rad));
                mapX.Set(y, x, srcX);
                mapY.Set(y, x, srcY);
            }
        }

        var unwrapped = new Mat();
        Cv2.Remap(src, unwrapped, mapX, mapY, InterpolationFlags.Linear, BorderTypes.Constant, Scalar.Black);

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(unwrapped)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var inner = GetIntParam(@operator, "InnerRadius", 0);
        var outer = GetIntParam(@operator, "OuterRadius", 1);
        if (inner < 0)
        {
            return ValidationResult.Invalid("InnerRadius must be >= 0");
        }

        if (outer <= inner)
        {
            return ValidationResult.Invalid("OuterRadius must be greater than InnerRadius");
        }

        return ValidationResult.Valid();
    }

    private Position ResolveCenter(Operator @operator, Dictionary<string, object>? inputs, int width, int height)
    {
        if (inputs != null && inputs.TryGetValue("Center", out var centerObj) && TryParsePoint(centerObj, out var centerFromInput))
        {
            return centerFromInput;
        }

        var x = GetDoubleParam(@operator, "CenterX", width / 2.0);
        var y = GetDoubleParam(@operator, "CenterY", height / 2.0);
        return new Position(x, y);
    }

    private static bool TryParsePoint(object? obj, out Position point)
    {
        point = new Position(0, 0);
        if (obj == null)
        {
            return false;
        }

        switch (obj)
        {
            case Position p:
                point = p;
                return true;
            case Point p:
                point = new Position(p.X, p.Y);
                return true;
            case Point2f p:
                point = new Position(p.X, p.Y);
                return true;
            case Point2d p:
                point = new Position(p.X, p.Y);
                return true;
        }

        if (obj is IDictionary<string, object> dict &&
            TryGetDouble(dict, "X", out var x) &&
            TryGetDouble(dict, "Y", out var y))
        {
            point = new Position(x, y);
            return true;
        }

        if (obj is IDictionary legacy)
        {
            var normalized = legacy.Cast<DictionaryEntry>()
                .Where(e => e.Key != null)
                .ToDictionary(e => e.Key!.ToString() ?? string.Empty, e => e.Value ?? 0.0, StringComparer.OrdinalIgnoreCase);
            return TryParsePoint(normalized, out point);
        }

        return false;
    }

    private static bool TryGetDouble(IDictionary<string, object> dict, string key, out double value)
    {
        value = 0;
        if (!dict.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        return raw switch
        {
            double d => (value = d) == d,
            float f => (value = f) == f,
            int i => (value = i) == i,
            long l => (value = l) == l,
            _ => double.TryParse(raw.ToString(), out value)
        };
    }
}
