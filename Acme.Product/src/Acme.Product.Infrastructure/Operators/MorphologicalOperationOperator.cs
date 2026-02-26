// MorphologicalOperationOperator.cs
// 形态学操作算子 - 腐蚀// 功能实现膨胀// 功能实现开运算// 功能实现闭运算// 功能实现梯度// 功能实现顶帽// 功能实现黑帽
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 形态学操作算子 - 腐蚀/膨胀/开运算/闭运算/梯度/顶帽/黑帽
/// 【第三优先级】图像预处理算子扩展
/// </summary>
[OperatorMeta(
    DisplayName = "形态学操作",
    Description = "腐蚀/膨胀/开运算/闭运算/梯度/顶帽/黑帽",
    Category = "预处理",
    IconName = "morphology"
)]
[InputPort("Image", "图像", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "处理后图像", PortDataType.Image)]
[OperatorParam("Operation", "操作类型", "enum", DefaultValue = "Close", Options = new[] { "Erode|腐蚀", "Dilate|膨胀", "Open|开运算", "Close|闭运算", "Gradient|梯度", "TopHat|顶帽", "BlackHat|黑帽" })]
[OperatorParam("KernelShape", "核形状", "enum", DefaultValue = "Rect", Options = new[] { "Rect|矩形", "Cross|十字形", "Ellipse|椭圆形" })]
[OperatorParam("KernelWidth", "核宽度", "int", DefaultValue = 3, Min = 1, Max = 51)]
[OperatorParam("KernelHeight", "核高度", "int", DefaultValue = 3, Min = 1, Max = 51)]
[OperatorParam("Iterations", "迭代次数", "int", DefaultValue = 1, Min = 1, Max = 10)]
public class MorphologicalOperationOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.MorphologicalOperation;

    public MorphologicalOperationOperator(ILogger<MorphologicalOperationOperator> logger) : base(logger)
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

        // 获取参数
        var operation = GetStringParam(@operator, "Operation", "Close"); // Erode / Dilate / Open / Close / Gradient / TopHat / BlackHat
        var kernelShape = GetStringParam(@operator, "KernelShape", "Rect"); // Rect / Cross / Ellipse
        var kernelWidth = GetIntParam(@operator, "KernelWidth", 3, min: 1, max: 51);
        var kernelHeight = GetIntParam(@operator, "KernelHeight", 3, min: 1, max: 51);
        var iterations = GetIntParam(@operator, "Iterations", 1, min: 1, max: 10);
        var anchorX = GetIntParam(@operator, "AnchorX", -1);
        var anchorY = GetIntParam(@operator, "AnchorY", -1);

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        // 创建结构元素
        var shape = kernelShape.ToLower() switch
        {
            "rect" or "rectangle" => MorphShapes.Rect,
            "cross" or "crossshape" => MorphShapes.Cross,
            "ellipse" => MorphShapes.Ellipse,
            _ => MorphShapes.Rect
        };

        using var kernel = Cv2.GetStructuringElement(shape, new Size(kernelWidth, kernelHeight));

        // 设置锚点
        var anchor = (anchorX == -1 || anchorY == -1) 
            ? new Point(-1, -1) 
            : new Point(anchorX, anchorY);

        var dst = new Mat();

        // 执行形态学操作
        var morphOp = operation.ToLower() switch
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

        Cv2.MorphologyEx(src, dst, morphOp, kernel, anchor, iterations);

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst, new Dictionary<string, object>
        {
            { "Operation", operation },
            { "KernelShape", kernelShape },
            { "KernelSize", $"{kernelWidth}x{kernelHeight}" },
            { "Iterations", iterations }
        })));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var operation = GetStringParam(@operator, "Operation", "Close").ToLower();
        var validOperations = new[] { "erode", "dilate", "open", "opening", "close", "closing", "gradient", "tophat", "top_hat", "blackhat", "black_hat" };
        if (!validOperations.Contains(operation))
            return ValidationResult.Invalid($"不支持的形态学操作: {operation}");

        var kernelShape = GetStringParam(@operator, "KernelShape", "Rect").ToLower();
        var validShapes = new[] { "rect", "rectangle", "cross", "crossshape", "ellipse" };
        if (!validShapes.Contains(kernelShape))
            return ValidationResult.Invalid($"不支持的核形状: {kernelShape}");

        var kernelWidth = GetIntParam(@operator, "KernelWidth", 3);
        if (kernelWidth < 1 || kernelWidth > 51)
            return ValidationResult.Invalid("核宽度必须在 1-51 之间");

        var kernelHeight = GetIntParam(@operator, "KernelHeight", 3);
        if (kernelHeight < 1 || kernelHeight > 51)
            return ValidationResult.Invalid("核高度必须在 1-51 之间");

        var iterations = GetIntParam(@operator, "Iterations", 1);
        if (iterations < 1 || iterations > 10)
            return ValidationResult.Invalid("迭代次数必须在 1-10 之间");

        return ValidationResult.Valid();
    }
}
