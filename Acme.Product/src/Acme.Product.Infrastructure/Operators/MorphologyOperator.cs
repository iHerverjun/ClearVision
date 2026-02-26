// MorphologyOperator.cs
// 形态学算子 - 支持腐蚀、膨胀、开运算、闭运算
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 形态学算子 - 支持腐蚀、膨胀、开运算、闭运算
/// </summary>
[OperatorMeta(
    DisplayName = "形态学",
    Description = "腐蚀/膨胀/开运算/闭运算等形态学操作，用于去除毛刺、填充孔洞和分离粘连目标",
    Category = "预处理",
    IconName = "morphology",
    Keywords = new[] { "形态学", "膨胀", "腐蚀", "开运算", "闭运算", "Morphology", "Erode", "Dilate" }
)]
[InputPort("Image", "图像", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "图像", PortDataType.Image)]
[OperatorParam("Operation", "操作类型", "string", DefaultValue = "Erode")]
[OperatorParam("KernelSize", "核大小", "int", DefaultValue = 3, Min = 1, Max = 21)]
[OperatorParam("KernelShape", "核形状", "string", DefaultValue = "Rect")]
[OperatorParam("Iterations", "迭代次数", "int", DefaultValue = 1, Min = 1, Max = 10)]
[OperatorParam("AnchorX", "锚点X", "int", DefaultValue = -1)]
[OperatorParam("AnchorY", "锚点Y", "int", DefaultValue = -1)]
public class MorphologyOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.Morphology;

    public MorphologyOperator(ILogger<MorphologyOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, "Image", out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供输入图像"));
        }

        var operation = GetStringParam(@operator, "Operation", "Erode");
        var kernelSize = GetIntParam(@operator, "KernelSize", 3, min: 1, max: 21);
        var kernelShape = GetStringParam(@operator, "KernelShape", "Rect");
        var iterations = GetIntParam(@operator, "Iterations", 1, min: 1, max: 10);
        var anchorX = GetIntParam(@operator, "AnchorX", -1);
        var anchorY = GetIntParam(@operator, "AnchorY", -1);

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        var shape = kernelShape.ToLower() switch
        {
            "rect" => MorphShapes.Rect,
            "ellipse" => MorphShapes.Ellipse,
            "cross" => MorphShapes.Cross,
            _ => MorphShapes.Rect
        };

        var anchor = new Point(anchorX, anchorY);
        using var kernel = Cv2.GetStructuringElement(shape, new Size(kernelSize, kernelSize), anchor);
        var dst = new Mat();

        var morphOp = operation.ToLower() switch
        {
            "erode" => MorphTypes.Erode,
            "dilate" => MorphTypes.Dilate,
            "open" => MorphTypes.Open,
            "close" => MorphTypes.Close,
            "gradient" => MorphTypes.Gradient,
            "tophat" => MorphTypes.TopHat,
            "blackhat" => MorphTypes.BlackHat,
            _ => MorphTypes.Erode
        };

        Cv2.MorphologyEx(src, dst, morphOp, kernel, iterations: iterations);

        // P0: 使用ImageWrapper实现零拷贝输出
        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst, new Dictionary<string, object>
        {
            { "Operation", operation }
        })));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var kernelSize = GetIntParam(@operator, "KernelSize", 3);
        if (kernelSize < 1 || kernelSize > 21)
        {
            return ValidationResult.Invalid("核大小必须在 1-21 之间");
        }

        var iterations = GetIntParam(@operator, "Iterations", 1);
        if (iterations < 1 || iterations > 10)
        {
            return ValidationResult.Invalid("迭代次数必须在 1-10 之间");
        }

        var operation = GetStringParam(@operator, "Operation", "Erode").ToLower();
        var validOperations = new[] { "erode", "dilate", "open", "close", "gradient", "tophat", "blackhat" };
        if (!validOperations.Contains(operation))
        {
            return ValidationResult.Invalid($"不支持的操作类型: {operation}");
        }

        var kernelShape = GetStringParam(@operator, "KernelShape", "Rect").ToLower();
        var validShapes = new[] { "rect", "ellipse", "cross" };
        if (!validShapes.Contains(kernelShape))
        {
            return ValidationResult.Invalid($"不支持的核形状: {kernelShape}");
        }

        return ValidationResult.Valid();
    }
}
