// MorphologyExecutionHelper.cs
// 形态学执行辅助器
// 封装形态学操作执行过程中的公共计算逻辑
// 作者：蘅芜君
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

internal static class MorphologyExecutionHelper
{
    private static readonly string[] ValidOperations =
    {
        "erode",
        "dilate",
        "open",
        "opening",
        "close",
        "closing",
        "gradient",
        "tophat",
        "top_hat",
        "blackhat",
        "black_hat"
    };

    private static readonly string[] ValidShapes =
    {
        "rect",
        "rectangle",
        "cross",
        "crossshape",
        "ellipse"
    };

    public static bool IsValidOperation(string operation)
    {
        return ValidOperations.Contains(operation, StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsValidShape(string shape)
    {
        return ValidShapes.Contains(shape, StringComparer.OrdinalIgnoreCase);
    }

    public static Mat Execute(
        Mat src,
        string operation,
        string kernelShape,
        int kernelWidth,
        int kernelHeight,
        int iterations,
        int anchorX = -1,
        int anchorY = -1)
    {
        var shape = ParseShape(kernelShape);
        var morphType = ParseOperation(operation);
        var anchor = (anchorX == -1 || anchorY == -1)
            ? new Point(-1, -1)
            : new Point(anchorX, anchorY);

        using var kernel = Cv2.GetStructuringElement(shape, new Size(kernelWidth, kernelHeight), anchor);
        var dst = new Mat();
        Cv2.MorphologyEx(src, dst, morphType, kernel, anchor, iterations);
        return dst;
    }

    private static MorphShapes ParseShape(string kernelShape)
    {
        return kernelShape.ToLowerInvariant() switch
        {
            "rect" or "rectangle" => MorphShapes.Rect,
            "cross" or "crossshape" => MorphShapes.Cross,
            "ellipse" => MorphShapes.Ellipse,
            _ => MorphShapes.Rect
        };
    }

    private static MorphTypes ParseOperation(string operation)
    {
        return operation.ToLowerInvariant() switch
        {
            "erode" => MorphTypes.Erode,
            "dilate" => MorphTypes.Dilate,
            "open" or "opening" => MorphTypes.Open,
            "close" or "closing" => MorphTypes.Close,
            "gradient" => MorphTypes.Gradient,
            "tophat" or "top_hat" => MorphTypes.TopHat,
            "blackhat" or "black_hat" => MorphTypes.BlackHat,
            _ => MorphTypes.Close
        };
    }
}
