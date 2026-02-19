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
