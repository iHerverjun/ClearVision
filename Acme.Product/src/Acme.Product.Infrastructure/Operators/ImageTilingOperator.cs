using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "图像切片",
    Description = "Splits an image into tiled regions with optional overlap.",
    Category = "拆分组合",
    IconName = "tile",
    Keywords = new[] { "tile", "grid", "split image" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[OutputPort("Tiles", "Tiles", PortDataType.Any)]
[OutputPort("Count", "Count", PortDataType.Integer)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OperatorParam("Rows", "Rows", "int", DefaultValue = 2, Min = 1, Max = 100)]
[OperatorParam("Cols", "Cols", "int", DefaultValue = 2, Min = 1, Max = 100)]
[OperatorParam("Overlap", "Overlap", "int", DefaultValue = 0, Min = 0, Max = 10000)]
[OperatorParam("OutputMode", "Output Mode", "enum", DefaultValue = "Array", Options = new[] { "Array|Array", "Sequential|Sequential" })]
public class ImageTilingOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ImageTiling;

    public ImageTilingOperator(ILogger<ImageTilingOperator> logger) : base(logger)
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

        var rows = GetIntParam(@operator, "Rows", 2, 1, 100);
        var cols = GetIntParam(@operator, "Cols", 2, 1, 100);
        var overlap = GetIntParam(@operator, "Overlap", 0, 0, 10000);

        var tileW = Math.Max(1, src.Width / cols);
        var tileH = Math.Max(1, src.Height / rows);
        var tiles = new List<ImageWrapper>();

        var annotated = src.Clone();
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var x = c * tileW;
                var y = r * tileH;
                var w = c == cols - 1 ? src.Width - x : tileW;
                var h = r == rows - 1 ? src.Height - y : tileH;

                var roiX = Math.Max(0, x - overlap);
                var roiY = Math.Max(0, y - overlap);
                var roiW = Math.Min(src.Width - roiX, w + overlap * 2);
                var roiH = Math.Min(src.Height - roiY, h + overlap * 2);
                var roi = new Rect(roiX, roiY, roiW, roiH);

                using var tileMat = new Mat(src, roi);
                tiles.Add(new ImageWrapper(tileMat.Clone()));

                Cv2.Rectangle(annotated, new Rect(x, y, w, h), new Scalar(0, 255, 255), 1);
            }
        }

        var output = new Dictionary<string, object>
        {
            { "Tiles", tiles },
            { "Count", tiles.Count }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(annotated, output)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var rows = GetIntParam(@operator, "Rows", 2);
        var cols = GetIntParam(@operator, "Cols", 2);
        if (rows <= 0 || cols <= 0)
        {
            return ValidationResult.Invalid("Rows and Cols must be greater than 0");
        }

        var overlap = GetIntParam(@operator, "Overlap", 0);
        if (overlap < 0)
        {
            return ValidationResult.Invalid("Overlap must be >= 0");
        }

        return ValidationResult.Valid();
    }
}

