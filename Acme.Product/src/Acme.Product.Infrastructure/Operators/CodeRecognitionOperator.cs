// CodeRecognitionOperator.cs
// Mat 转 Bitmap - 使用 PNG 编解码
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using ZXing;
using ZXing.Common;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 条码识别算子 - 一维码/二维码识别 (CodeRecognition = 9)
/// </summary>
public class CodeRecognitionOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.CodeRecognition;

    public CodeRecognitionOperator(ILogger<CodeRecognitionOperator> logger) : base(logger) { }

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
        var codeType = GetStringParam(@operator, "CodeType", "All");
        var maxResults = GetIntParam(@operator, "MaxResults", 10, 1, 100);

        using var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        // 转换为灰度图以提高识别率
        using var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        // 创建结果图像副本用于绘制标记
        using var resultImage = src.Clone();

        // 配置条码读取器 - 使用Bitmap作为类型参数
        var reader = new BarcodeReader<System.Drawing.Bitmap>(bitmap =>
        {
            // 将Bitmap转换为LuminanceSource
            var luminance = new byte[bitmap.Width * bitmap.Height];
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    luminance[y * bitmap.Width + x] = (byte)((pixel.R + pixel.G + pixel.B) / 3);
                }
            }
            return new RGBLuminanceSource(luminance, bitmap.Width, bitmap.Height, RGBLuminanceSource.BitmapFormat.Gray8);
        })
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                TryHarder = true,
                TryInverted = true,
                PossibleFormats = GetBarcodeFormats(codeType)
            }
        };

        // 将Mat转换为Bitmap
        using var bitmap = MatToBitmap(gray);
        
        // 识别条码 - 使用多码识别
        var results = reader.DecodeMultiple(bitmap);
        var codeResults = new List<Dictionary<string, object>>();

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
    /// Mat 转 Bitmap - 使用 PNG 编解码
    /// </summary>
    private System.Drawing.Bitmap MatToBitmap(Mat mat)
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
