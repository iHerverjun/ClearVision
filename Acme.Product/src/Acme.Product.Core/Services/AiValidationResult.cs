// AiValidationResult.cs
// AI 校验结果模型
// 封装 AI 输出的校验状态与错误详情
// 作者：蘅芜君
namespace Acme.Product.Core.Services;

public class AiValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public static AiValidationResult Success() => new();

    public static AiValidationResult Failure(params string[] errors) =>
        new() { Errors = errors.ToList() };

    public void AddError(string error) => Errors.Add(error);
    public void AddWarning(string warning) => Warnings.Add(warning);
}
