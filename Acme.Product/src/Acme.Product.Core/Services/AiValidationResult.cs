// AiValidationResult.cs
// AI 校验结果模型
// 封装 AI 输出的校验状态与错误详情
// 作者：蘅芜君
using System.Linq;

namespace Acme.Product.Core.Services;

public class AiValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    // 保持旧的 Errors/Warnings 接口不变，同时补充可序列化的结构化诊断。
    public List<AiValidationDiagnostic> Diagnostics { get; set; } = new();

    public AiValidationDiagnostic? PrimaryError =>
        Diagnostics.FirstOrDefault(item => item.Severity == AiValidationSeverity.Error);

    public static AiValidationResult Success() => new();

    public static AiValidationResult Failure(params string[] errors)
    {
        var result = new AiValidationResult();
        foreach (var error in errors)
        {
            result.AddError(error);
        }

        return result;
    }

    public void AddError(
        string error,
        string code = "validation_error",
        string category = "validation",
        IEnumerable<string>? relatedFields = null,
        string? operatorId = null,
        string? parameterName = null,
        string? sourceTempId = null,
        string? sourcePortName = null,
        string? targetTempId = null,
        string? targetPortName = null,
        string? repairHint = null)
    {
        Errors.Add(error);
        Diagnostics.Add(new AiValidationDiagnostic
        {
            Severity = AiValidationSeverity.Error,
            Code = code,
            Category = category,
            Message = error,
            RelatedFields = NormalizeRelatedFields(relatedFields),
            OperatorId = operatorId,
            ParameterName = parameterName,
            SourceTempId = sourceTempId,
            SourcePortName = sourcePortName,
            TargetTempId = targetTempId,
            TargetPortName = targetPortName,
            RepairHint = repairHint
        });
    }

    public void AddWarning(
        string warning,
        string code = "validation_warning",
        string category = "validation",
        IEnumerable<string>? relatedFields = null,
        string? operatorId = null,
        string? parameterName = null,
        string? sourceTempId = null,
        string? sourcePortName = null,
        string? targetTempId = null,
        string? targetPortName = null,
        string? repairHint = null)
    {
        Warnings.Add(warning);
        Diagnostics.Add(new AiValidationDiagnostic
        {
            Severity = AiValidationSeverity.Warning,
            Code = code,
            Category = category,
            Message = warning,
            RelatedFields = NormalizeRelatedFields(relatedFields),
            OperatorId = operatorId,
            ParameterName = parameterName,
            SourceTempId = sourceTempId,
            SourcePortName = sourcePortName,
            TargetTempId = targetTempId,
            TargetPortName = targetPortName,
            RepairHint = repairHint
        });
    }

    private static List<string> NormalizeRelatedFields(IEnumerable<string>? relatedFields)
    {
        if (relatedFields == null)
            return new List<string>();

        return relatedFields
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .Select(field => field.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

public static class AiValidationSeverity
{
    public const string Error = "error";
    public const string Warning = "warning";
}

public class AiValidationDiagnostic
{
    public string Severity { get; set; } = AiValidationSeverity.Error;
    public string Code { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<string> RelatedFields { get; set; } = new();
    public string? OperatorId { get; set; }
    public string? ParameterName { get; set; }
    public string? SourceTempId { get; set; }
    public string? SourcePortName { get; set; }
    public string? TargetTempId { get; set; }
    public string? TargetPortName { get; set; }
    public string? RepairHint { get; set; }
}
