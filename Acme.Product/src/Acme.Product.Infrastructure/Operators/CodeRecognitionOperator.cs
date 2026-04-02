// CodeRecognitionOperator.cs
// Mat 转 Bitmap - 使用 PNG 编解码
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using ZXing;
using ZXing.Common;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 条码识别算子 - 一维码/二维码识别 (CodeRecognition = 9)
/// </summary>
[OperatorMeta(
    DisplayName = "条码识别",
    Description = "一维码/二维码识别，支持 QR、Code128、DataMatrix 等多种码制",
    Category = "识别",
    IconName = "barcode",
    Keywords = new[] { "条码", "二维码", "扫码", "识别", "QR", "读取", "Barcode", "Decode", "Read code" }
)]
[InputPort("Image", "输入图像", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "结果图像", PortDataType.Image)]
[OutputPort("Text", "识别内容", PortDataType.String)]
[OutputPort("CodeCount", "识别数量", PortDataType.Integer)]
[OutputPort("CodeType", "条码类型", PortDataType.String)]
[OperatorParam("CodeType", "码制类型", "enum", DefaultValue = "All", Options = new[] { "All|全部", "QR|QR码", "Code128|Code128", "DataMatrix|DataMatrix", "EAN13|EAN-13", "Code39|Code39" })]
[OperatorParam("MaxResults", "最大结果数", "int", DefaultValue = 10, Min = 1, Max = 100)]
public class CodeRecognitionOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.CodeRecognition;

    public CodeRecognitionOperator(ILogger<CodeRecognitionOperator> logger) : base(logger) { }

    [SupportedOSPlatform("windows6.1")]
    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供输入图像"));
        }

        // 获取参数
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("CodeRecognitionOperator 仅支持 Windows 6.1+ 平台"));
        }

        var codeType = GetStringParam(@operator, "CodeType", "All");
        var maxResults = GetIntParam(@operator, "MaxResults", 10, 1, 100);

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        // 转换为灰度图以提高识别率
        using var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        // 创建结果图像副本用于绘制标记
        var resultImage = src.Clone();

        int width = gray.Width;
        int height = gray.Height;
        int size = width * height;

        var codeResults = new List<Dictionary<string, object>>();

        // 消除 LOH 分配：直接使用内存池租借解码用的亮度缓冲
        byte[] luminance = System.Buffers.ArrayPool<byte>.Shared.Rent(size);
        try
        {
            // 将单通道灰度数据安全拷贝到托管数组
            System.Runtime.InteropServices.Marshal.Copy(gray.Data, luminance, 0, size);

            // 直接构建 LuminanceSource
            var source = new RGBLuminanceSource(luminance, width, height, RGBLuminanceSource.BitmapFormat.Gray8);
            var binarizer = new ZXing.Common.GlobalHistogramBinarizer(source); // 或者 HybridBinarizer
            var binaryBitmap = new BinaryBitmap(binarizer);

            var hints = new Dictionary<DecodeHintType, object>
            {
                { DecodeHintType.TRY_HARDER, true },
                { DecodeHintType.ALSO_INVERTED, true },
                { DecodeHintType.POSSIBLE_FORMATS, GetBarcodeFormats(codeType) }
            };

            var reader = new ZXing.MultiFormatReader();
            var multiReader = new ZXing.Multi.GenericMultipleBarcodeReader(reader);

            // ZXing 的 Java 命名风格保留的小驼峰方法
            var results = multiReader.decodeMultiple(binaryBitmap, hints);

            if (results != null && results.Length > 0)
            {
                for (int i = 0; i < results.Length && i < maxResults; i++)
                {
                    var result = results[i];

                    // 绘制识别区域
                    var points = result.ResultPoints;
                    if (points != null && points.Length >= 2)
                    {
                        for (int j = 0; j < points.Length; j++)
                        {
                            var pt1 = new Point((int)points[j].X, (int)points[j].Y);
                            var pt2 = new Point((int)points[(j + 1) % points.Length].X, (int)points[(j + 1) % points.Length].Y);
                            Cv2.Line(resultImage, pt1, pt2, new Scalar(0, 255, 0), 2);
                        }
                    }

                    codeResults.Add(new Dictionary<string, object>
                    {
                        { "Index", i },
                        { "Text", result.Text },
                        { "Format", result.BarcodeFormat.ToString() },
                        { "Points", result.ResultPoints?.Select(p => new { X = p.X, Y = p.Y }).ToArray() ?? Array.Empty<object>() }
                    });
                }
            }
        }
        catch (ZXing.ReaderException)
        {
            // ZXing 在未找到条码时可能抛出，正常流程忽略
        }
        finally
        {
            // 如果租借到了足够的 buffer 空间，则在解析结束后返还
            System.Buffers.ArrayPool<byte>.Shared.Return(luminance);
        }

        var mainText = codeResults.FirstOrDefault()?.GetValueOrDefault("Text")?.ToString() ?? "";
        var mainFormat = codeResults.FirstOrDefault()?.GetValueOrDefault("Format")?.ToString() ?? "";

        var additionalData = new Dictionary<string, object>
        {
            { "Text", mainText },
            { "CodeType", mainFormat },
            { "CodeResults", codeResults },
            { "ResultCount", codeResults.Count },
            { "Codes", codeResults },
            { "CodeCount", codeResults.Count }
        };
        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, additionalData)));
    }

    /// <summary>
    /// Mat 转 Bitmap - 使用 PNG 编解码 (保留用于兼容早期接口)
    /// </summary>
    [SupportedOSPlatform("windows6.1")]
    public System.Drawing.Bitmap MatToBitmap(Mat mat)
    {
        // 将Mat转换为字节数组
        var imageData = mat.ToBytes(".png");
        using var ms = new System.IO.MemoryStream(imageData);
        return new System.Drawing.Bitmap(ms);
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var maxResults = GetIntParam(@operator, "MaxResults", 10);
        if (maxResults < 1 || maxResults > 100)
        {
            return ValidationResult.Invalid("最大结果数必须在 1-100 之间");
        }
        return ValidationResult.Valid();
    }

    private List<BarcodeFormat> GetBarcodeFormats(string codeType)
    {
        return codeType.ToLower() switch
        {
            "qr" => new List<BarcodeFormat> { BarcodeFormat.QR_CODE },
            "code128" => new List<BarcodeFormat> { BarcodeFormat.CODE_128 },
            "datamatrix" => new List<BarcodeFormat> { BarcodeFormat.DATA_MATRIX },
            "ean13" => new List<BarcodeFormat> { BarcodeFormat.EAN_13 },
            "code39" => new List<BarcodeFormat> { BarcodeFormat.CODE_39 },
            _ => new List<BarcodeFormat>
            {
                BarcodeFormat.QR_CODE,
                BarcodeFormat.CODE_128,
                BarcodeFormat.DATA_MATRIX,
                BarcodeFormat.EAN_13,
                BarcodeFormat.CODE_39,
                BarcodeFormat.EAN_8,
                BarcodeFormat.UPC_A,
                BarcodeFormat.UPC_E,
                BarcodeFormat.CODABAR,
                BarcodeFormat.ITF,
                BarcodeFormat.AZTEC,
                BarcodeFormat.PDF_417
            }
        };
    }
}
