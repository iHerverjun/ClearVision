// ImageSaveOperator.cs
// 图像保存算子 - Sprint 3 Task 3.6b
// NG 图像存档，支持多种格式
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 图像保存算子
/// 
/// 功能：
/// - 保存图像到指定路径
/// - 支持 PNG/JPEG/BMP 格式
/// - 支持自动创建目录
/// - 支持文件名模板（含时间戳）
/// 
/// 使用场景：
/// - NG 图像存档
/// - 检测结果保存
/// - 调试图像输出
/// </summary>
[OperatorMeta(
    DisplayName = "图像保存",
    Description = "保存检测图像到本地硬盘",
    Category = "输出",
    IconName = "save"
)]
[InputPort("Image", "图像", PortDataType.Image, IsRequired = true)]
[OutputPort("FilePath", "保存路径", PortDataType.String)]
[OutputPort("IsSuccess", "是否成功", PortDataType.Boolean)]
[OperatorParam("Directory", "目录", "string", DefaultValue = "C:\\ClearVision\\NG_Images")]
[OperatorParam("FileNameTemplate", "命名规则", "string", DefaultValue = "NG_{yyyyMMdd_HHmmss}_{Guid}.jpg")]
[OperatorParam("Quality", "质量", "int", DefaultValue = 90, Min = 1, Max = 100)]
public class ImageSaveOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ImageSave;

    public ImageSaveOperator(ILogger<ImageSaveOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        // 获取输入图像
        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供输入图像"));
        }

        // 获取参数
        var folderPath = ResolveDirectory(@operator);
        var fileName = ResolveFileNameTemplate(@operator);
        var format = ResolveFormat(@operator, fileName);
        var jpegQuality = ResolveJpegQuality(@operator);
        var overwrite = GetBoolParam(@operator, "Overwrite", false);
        var createFolder = GetBoolParam(@operator, "CreateFolder", true);

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Directory/FolderPath 参数不能为空"));
        }

        try
        {
            // 创建目录
            if (createFolder && !Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // 替换文件名模板
            var actualFileName = ReplaceFileNameTemplate(fileName);
            
            // 确保扩展名正确
            var extension = format.ToLower() switch
            {
                "jpg" or "jpeg" => ".jpg",
                "bmp" => ".bmp",
                _ => ".png"
            };

            if (!actualFileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                actualFileName = Path.ChangeExtension(actualFileName, extension);
            }

            var fullPath = Path.Combine(folderPath, actualFileName);

            // 检查文件是否已存在
            if (File.Exists(fullPath) && !overwrite)
            {
                // 添加序号
                var dir = Path.GetDirectoryName(fullPath);
                var name = Path.GetFileNameWithoutExtension(fullPath);
                var ext = Path.GetExtension(fullPath);
                int counter = 1;
                
                do
                {
                    actualFileName = $"{name}_{counter:D3}{ext}";
                    fullPath = Path.Combine(dir!, actualFileName);
                    counter++;
                } while (File.Exists(fullPath));
            }

            // 获取 Mat 并保存
            var mat = imageWrapper.MatReadOnly;
            
            var formatParams = format.ToLower() switch
            {
                "jpg" or "jpeg" => new[] { new ImageEncodingParam(ImwriteFlags.JpegQuality, jpegQuality) },
                _ => null
            };

            // 保存图像
            if (formatParams != null)
            {
                Cv2.ImWrite(fullPath, mat, formatParams);
            }
            else
            {
                Cv2.ImWrite(fullPath, mat);
            }

            Logger.LogInformation("[ImageSave] 图像已保存: {Path}, 格式={Format}, 大小={Width}x{Height}",
                fullPath, format, mat.Width, mat.Height);

            return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
            {
                { "IsSuccess", true },
                { "Success", true },
                { "FilePath", fullPath },
                { "FileName", actualFileName },
                { "Format", format },
                { "Width", mat.Width },
                { "Height", mat.Height },
                { "FileSize", new FileInfo(fullPath).Length }
            }));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[ImageSave] 保存失败");
            return Task.FromResult(OperatorExecutionOutput.Failure($"图像保存失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 替换文件名模板
    /// </summary>
    private static string ReplaceFileNameTemplate(string template)
    {
        var result = template;
        var now = DateTime.Now;
        var guidToken = Guid.NewGuid().ToString("N");

        result = result.Replace("{timestamp}", now.ToString("yyyyMMdd_HHmmss"));
        result = result.Replace("{date}", now.ToString("yyyyMMdd"));
        result = result.Replace("{time}", now.ToString("HHmmss"));
        result = result.Replace("{year}", now.Year.ToString());
        result = result.Replace("{month}", now.Month.ToString("D2"));
        result = result.Replace("{day}", now.Day.ToString("D2"));
        result = result.Replace("{Guid}", guidToken);
        result = result.Replace("{guid}", guidToken);

        return result;
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var folderPath = ResolveDirectory(@operator);
        var format = ResolveFormat(@operator, ResolveFileNameTemplate(@operator));
        var jpegQuality = ResolveJpegQuality(@operator);

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return ValidationResult.Invalid("Directory/FolderPath 不能为空");
        }

        var validFormats = new[] { "png", "jpg", "jpeg", "bmp" };
        if (!validFormats.Contains(format.ToLower()))
        {
            return ValidationResult.Invalid($"Format 必须是以下之一: {string.Join(", ", validFormats)}");
        }

        if (jpegQuality < 1 || jpegQuality > 100)
        {
            return ValidationResult.Invalid("JpegQuality 必须在 1-100 之间");
        }

        return ValidationResult.Valid();
    }

    private string ResolveDirectory(Operator @operator)
    {
        var directory = GetStringParam(@operator, "Directory", "");
        var legacyDirectory = GetStringParam(@operator, "FolderPath", "");

        if (IsExplicitlyConfigured(@operator, "Directory"))
        {
            return directory;
        }

        if (!string.IsNullOrWhiteSpace(legacyDirectory))
        {
            return legacyDirectory;
        }

        return directory;
    }

    private string ResolveFileNameTemplate(Operator @operator)
    {
        var template = GetStringParam(@operator, "FileNameTemplate", "");
        var legacyTemplate = GetStringParam(@operator, "FileName", "image_{timestamp}.png");

        if (IsExplicitlyConfigured(@operator, "FileNameTemplate"))
        {
            return template;
        }

        if (!string.IsNullOrWhiteSpace(legacyTemplate))
        {
            return legacyTemplate;
        }

        return template;
    }

    private string ResolveFormat(Operator @operator, string fileNameTemplate)
    {
        var explicitFormat = GetStringParam(@operator, "Format", "");
        if (!string.IsNullOrWhiteSpace(explicitFormat))
        {
            return explicitFormat.Trim().TrimStart('.').ToLowerInvariant();
        }

        var extension = Path.GetExtension(fileNameTemplate);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension.TrimStart('.').ToLowerInvariant();
        }

        return "png";
    }

    private int ResolveJpegQuality(Operator @operator)
    {
        var metadataQuality = GetIntParam(@operator, "Quality", 90, 1, 100);
        var legacyQuality = GetIntParam(@operator, "JpegQuality", metadataQuality, 1, 100);

        if (IsExplicitlyConfigured(@operator, "Quality"))
        {
            return metadataQuality;
        }

        if (HasParameter(@operator, "JpegQuality"))
        {
            return legacyQuality;
        }

        return metadataQuality;
    }

    private static bool HasParameter(Operator @operator, string name)
    {
        return @operator.Parameters.Any(parameter =>
            string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsExplicitlyConfigured(Operator @operator, string name)
    {
        var parameter = @operator.Parameters.FirstOrDefault(item =>
            string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));

        if (parameter == null)
        {
            return false;
        }

        return !string.Equals(parameter.ValueJson, parameter.DefaultValueJson, StringComparison.Ordinal);
    }
}
