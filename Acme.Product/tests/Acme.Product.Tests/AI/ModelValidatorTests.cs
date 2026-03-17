using Acme.Product.Infrastructure.AI.ModelValidation;
using FluentAssertions;
using OpenCvSharp;

namespace Acme.Product.Tests.AI;

public sealed class ModelValidatorTests
{
    [Fact]
    public void ValidatePreprocessing_WithInvalidStd_ShouldReturnError()
    {
        using var validator = CreateValidator();
        validator.SetPreprocessing(
            inputWidth: 2,
            inputHeight: 2,
            mean: [0f, 0f, 0f],
            std: [1f, 0f, 1f],
            channelOrder: ModelInputChannelOrder.RGB);

        var result = validator.ValidatePreprocessing();

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.Contains("Std values must be greater than zero.", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WithMatchingExpectedOutput_ShouldSucceed()
    {
        using var validator = CreateValidator();
        validator.SetPreprocessing(
            inputWidth: 2,
            inputHeight: 2,
            mean: [0f, 0f, 0f],
            std: [1f, 1f, 1f],
            channelOrder: ModelInputChannelOrder.RGB);

        var result = validator.Validate(
            ResolveTestDataPath(@"model_test_suite\identity_2x2\input.png"),
            ResolveTestDataPath(@"model_test_suite\identity_2x2\expected_output.json"));

        result.IsValid.Should().BeTrue(string.Join(Environment.NewLine, result.Errors));
        result.OutputComparisons.Should().ContainSingle();
        result.OutputComparisons[0].MaxAbsoluteError.Should().BeApproximately(0d, 1e-6);
    }

    [Fact]
    public void Validate_WithWrongChannelOrder_ShouldDetectMismatch()
    {
        using var validator = CreateValidator();
        validator.SetPreprocessing(
            inputWidth: 2,
            inputHeight: 2,
            mean: [0f, 0f, 0f],
            std: [1f, 1f, 1f],
            channelOrder: ModelInputChannelOrder.BGR);

        var result = validator.Validate(
            ResolveTestDataPath(@"model_test_suite\identity_2x2\input.png"),
            ResolveTestDataPath(@"model_test_suite\identity_2x2\expected_output.json"),
            allowedMaxRelativeErrorPercent: 5.0);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.Contains("exceeded tolerance", StringComparison.OrdinalIgnoreCase));
        result.OutputComparisons.Should().ContainSingle();
        result.OutputComparisons[0].IsWithinTolerance.Should().BeFalse();
    }

    [Fact]
    public void ValidateSuite_AndReportWriters_ShouldProduceArtifacts()
    {
        using var validator = CreateValidator();
        validator.SetPreprocessing(
            inputWidth: 2,
            inputHeight: 2,
            mean: [0f, 0f, 0f],
            std: [1f, 1f, 1f],
            channelOrder: ModelInputChannelOrder.RGB);

        var suiteResult = validator.ValidateSuite(ResolveTestDataPath(@"model_test_suite\identity_2x2\suite.json"));

        suiteResult.IsValid.Should().BeTrue();
        suiteResult.PassedCount.Should().Be(1);
        suiteResult.FailedCount.Should().Be(0);

        var tempDirectory = Path.Combine(Path.GetTempPath(), "clearvision_model_validator_tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        var templatePath = Path.Combine(tempDirectory, "template.md");
        var reportPath = Path.Combine(tempDirectory, "report.md");

        ModelValidator.WriteMarkdownTemplate(templatePath);
        ModelValidator.WriteMarkdownReport(reportPath, suiteResult.CaseResults[0]);

        File.Exists(templatePath).Should().BeTrue();
        File.Exists(reportPath).Should().BeTrue();
        File.ReadAllText(templatePath).Should().Contain("{{ModelPath}}");
        File.ReadAllText(reportPath).Should().Contain("Model Validation Report");
        File.ReadAllText(reportPath).Should().Contain("IsValid: True");
    }

    private static ModelValidator CreateValidator()
    {
        return new ModelValidator(
            ResolveTestDataPath(@"model_test_suite\identity_2x2\identity_2x2.onnx"),
            executionProvider: "cpu");
    }

    private static string ResolveTestDataPath(string relativePath)
    {
        var baseDir = AppContext.BaseDirectory;
        var candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "TestData", relativePath));
        if (File.Exists(candidate))
        {
            return candidate;
        }

        var dir = new DirectoryInfo(baseDir);
        while (dir != null && !dir.Name.Equals("Acme.Product", StringComparison.OrdinalIgnoreCase))
        {
            dir = dir.Parent;
        }

        if (dir != null)
        {
            candidate = Path.Combine(dir.FullName, "tests", "TestData", relativePath);
        }

        return candidate;
    }
}
