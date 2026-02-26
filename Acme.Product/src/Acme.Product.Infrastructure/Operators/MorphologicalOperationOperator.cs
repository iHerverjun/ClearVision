using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Morphological Operation",
    Description = "Erode, Dilate, Open, Close, Gradient, TopHat, BlackHat.",
    Category = "Preprocessing",
    IconName = "morphology"
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OperatorParam("Operation", "Operation", "enum", DefaultValue = "Close", Options = new[] { "Erode|Erode", "Dilate|Dilate", "Open|Open", "Close|Close", "Gradient|Gradient", "TopHat|TopHat", "BlackHat|BlackHat" })]
[OperatorParam("KernelShape", "Kernel Shape", "enum", DefaultValue = "Rect", Options = new[] { "Rect|Rect", "Cross|Cross", "Ellipse|Ellipse" })]
[OperatorParam("KernelWidth", "Kernel Width", "int", DefaultValue = 3, Min = 1, Max = 51)]
[OperatorParam("KernelHeight", "Kernel Height", "int", DefaultValue = 3, Min = 1, Max = 51)]
[OperatorParam("Iterations", "Iterations", "int", DefaultValue = 1, Min = 1, Max = 10)]
[OperatorParam("AnchorX", "Anchor X", "int", DefaultValue = -1)]
[OperatorParam("AnchorY", "Anchor Y", "int", DefaultValue = -1)]
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
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is required."));
        }

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid."));
        }

        var operation = GetStringParam(@operator, "Operation", "Close");
        var kernelShape = GetStringParam(@operator, "KernelShape", "Rect");
        var kernelWidth = GetIntParam(@operator, "KernelWidth", 3, min: 1, max: 51);
        var kernelHeight = GetIntParam(@operator, "KernelHeight", 3, min: 1, max: 51);
        var iterations = GetIntParam(@operator, "Iterations", 1, min: 1, max: 10);
        var anchorX = GetIntParam(@operator, "AnchorX", -1);
        var anchorY = GetIntParam(@operator, "AnchorY", -1);

        var dst = MorphologyExecutionHelper.Execute(
            src,
            operation,
            kernelShape,
            kernelWidth,
            kernelHeight,
            iterations,
            anchorX,
            anchorY);

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
        var operation = GetStringParam(@operator, "Operation", "Close");
        if (!MorphologyExecutionHelper.IsValidOperation(operation))
        {
            return ValidationResult.Invalid($"Unsupported morphology operation: {operation}");
        }

        var kernelShape = GetStringParam(@operator, "KernelShape", "Rect");
        if (!MorphologyExecutionHelper.IsValidShape(kernelShape))
        {
            return ValidationResult.Invalid($"Unsupported kernel shape: {kernelShape}");
        }

        var kernelWidth = GetIntParam(@operator, "KernelWidth", 3);
        if (kernelWidth < 1 || kernelWidth > 51)
        {
            return ValidationResult.Invalid("KernelWidth must be between 1 and 51.");
        }

        var kernelHeight = GetIntParam(@operator, "KernelHeight", 3);
        if (kernelHeight < 1 || kernelHeight > 51)
        {
            return ValidationResult.Invalid("KernelHeight must be between 1 and 51.");
        }

        var iterations = GetIntParam(@operator, "Iterations", 1);
        if (iterations < 1 || iterations > 10)
        {
            return ValidationResult.Invalid("Iterations must be between 1 and 10.");
        }

        return ValidationResult.Valid();
    }
}
