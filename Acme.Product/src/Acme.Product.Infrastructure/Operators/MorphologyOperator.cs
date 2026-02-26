using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// Legacy morphology operator kept for compatibility with existing flows.
/// New flows should prefer <see cref="MorphologicalOperationOperator"/>.
/// </summary>
[OperatorMeta(
    DisplayName = "Morphology (Legacy)",
    Description = "Legacy morphology node. Use Morphological Operation for new workflows.",
    Category = "Preprocessing",
    IconName = "morphology",
    Keywords = new[] { "Morphology", "Erode", "Dilate", "Open", "Close", "Legacy" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OperatorParam("Operation", "Operation", "string", DefaultValue = "Erode")]
[OperatorParam("KernelSize", "Kernel Size", "int", DefaultValue = 3, Min = 1, Max = 51)]
[OperatorParam("KernelShape", "Kernel Shape", "string", DefaultValue = "Rect")]
[OperatorParam("Iterations", "Iterations", "int", DefaultValue = 1, Min = 1, Max = 10)]
[OperatorParam("AnchorX", "Anchor X", "int", DefaultValue = -1)]
[OperatorParam("AnchorY", "Anchor Y", "int", DefaultValue = -1)]
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
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is required."));
        }

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid."));
        }

        var operation = GetStringParam(@operator, "Operation", "Erode");
        var kernelSize = GetIntParam(@operator, "KernelSize", 3, min: 1, max: 51);
        var kernelShape = GetStringParam(@operator, "KernelShape", "Rect");
        var iterations = GetIntParam(@operator, "Iterations", 1, min: 1, max: 10);
        var anchorX = GetIntParam(@operator, "AnchorX", -1);
        var anchorY = GetIntParam(@operator, "AnchorY", -1);

        var dst = MorphologyExecutionHelper.Execute(
            src,
            operation,
            kernelShape,
            kernelSize,
            kernelSize,
            iterations,
            anchorX,
            anchorY);

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst, new Dictionary<string, object>
        {
            { "Operation", operation },
            { "KernelShape", kernelShape },
            { "KernelSize", $"{kernelSize}x{kernelSize}" },
            { "Iterations", iterations },
            { "LegacyCompatible", true }
        })));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var kernelSize = GetIntParam(@operator, "KernelSize", 3);
        if (kernelSize < 1 || kernelSize > 51)
        {
            return ValidationResult.Invalid("KernelSize must be between 1 and 51.");
        }

        var iterations = GetIntParam(@operator, "Iterations", 1);
        if (iterations < 1 || iterations > 10)
        {
            return ValidationResult.Invalid("Iterations must be between 1 and 10.");
        }

        var operation = GetStringParam(@operator, "Operation", "Erode");
        if (!MorphologyExecutionHelper.IsValidOperation(operation))
        {
            return ValidationResult.Invalid($"Unsupported operation: {operation}");
        }

        var kernelShape = GetStringParam(@operator, "KernelShape", "Rect");
        if (!MorphologyExecutionHelper.IsValidShape(kernelShape))
        {
            return ValidationResult.Invalid($"Unsupported kernel shape: {kernelShape}");
        }

        return ValidationResult.Valid();
    }
}
