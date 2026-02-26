// OcrRecognitionOperator.cs
// OCR识别算子实现
// 作者：蘅芜君

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// OCR识别算子 - 文字识别
/// </summary>
[OperatorMeta(
    DisplayName = "OCR 识别",
    Description = "识别图像中的文本内容",
    Category = "识别",
    IconName = "text-recognition"
)]
[InputPort("Image", "图像", PortDataType.Image, IsRequired = true)]
[OutputPort("Text", "识别文本", PortDataType.String)]
[OutputPort("IsSuccess", "成功", PortDataType.Boolean)]
public class OcrRecognitionOperator : OperatorBase
{
    private readonly OcrEngineProvider _ocrEngineProvider;

    public override OperatorType OperatorType => OperatorType.OcrRecognition;

    public OcrRecognitionOperator(
        ILogger<OcrRecognitionOperator> logger,
        OcrEngineProvider ocrEngineProvider) : base(logger)
    {
        _ocrEngineProvider = ocrEngineProvider;
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
            return Task.FromResult(OperatorExecutionOutput.Failure("输入图像为空"));

        try
        {
            // 将 Mat 编码为字节数组供 PaddleOCRSharp 使用
            Cv2.ImEncode(".jpg", imageWrapper.MatReadOnly, out byte[] imageBytes);

            // 调用全局单例引擎进行文本识别
            var ocrResult = _ocrEngineProvider.DetectText(imageBytes);

            string resultText = ocrResult?.Text ?? string.Empty;

            var output = new Dictionary<string, object>
            {
                { "Text", resultText },
                { "IsSuccess", true }
            };

            return Task.FromResult(OperatorExecutionOutput.Success(output));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "OCR 识别失败");
            return Task.FromResult(OperatorExecutionOutput.Failure($"OCR 识别失败: {ex.Message}"));
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var modelPathParam = @operator.Parameters?.FirstOrDefault(p => p.Name == "ModelPath");
        var modelPath = modelPathParam?.Value?.ToString() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(modelPath) && !System.IO.Directory.Exists(modelPath) && !System.IO.File.Exists(modelPath))
        {
            return ValidationResult.Invalid($"指定的 OCR 模型路径不存在: {modelPath}");
        }

        return ValidationResult.Valid();
    }
}
