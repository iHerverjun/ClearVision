using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Text.RegularExpressions;

namespace Acme.Product.Tests.Operators;

public class ImageSaveOperatorTests
{
    private readonly ImageSaveOperator _operator;

    public ImageSaveOperatorTests()
    {
        _operator = new ImageSaveOperator(Substitute.For<ILogger<ImageSaveOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeImageSave()
    {
        _operator.OperatorType.Should().Be(OperatorType.ImageSave);
    }

    [Fact]
    public void ValidateParameters_WithMetadataParameterNames_ShouldBeValid()
    {
        var outputDir = CreateOutputDirectory();

        try
        {
            var op = new Operator("test", OperatorType.ImageSave, 0, 0);
            op.AddParameter(TestHelpers.CreateParameter("Directory", outputDir, "string"));
            op.AddParameter(TestHelpers.CreateParameter("FileNameTemplate", "sample_{timestamp}.jpg", "string"));
            op.AddParameter(TestHelpers.CreateParameter("Quality", 88, "int"));

            _operator.ValidateParameters(op).IsValid.Should().BeTrue();
        }
        finally
        {
            SafeDeleteDirectory(outputDir);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithMetadataParameterNames_ShouldSaveImageAndExposeSuccessKeys()
    {
        var outputDir = CreateOutputDirectory();
        var image = TestHelpers.CreateTestImage();

        try
        {
            var op = new Operator("test", OperatorType.ImageSave, 0, 0);
            op.AddParameter(TestHelpers.CreateParameter("Directory", outputDir, "string"));
            op.AddParameter(TestHelpers.CreateParameter("FileNameTemplate", "sample_{timestamp}_{Guid}.jpg", "string"));
            op.AddParameter(TestHelpers.CreateParameter("Quality", 88, "int"));

            var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

            result.IsSuccess.Should().BeTrue();
            result.OutputData.Should().NotBeNull();
            result.OutputData!["IsSuccess"].Should().Be(true);
            result.OutputData["Success"].Should().Be(true);

            var filePath = result.OutputData["FilePath"].Should().BeOfType<string>().Subject;
            File.Exists(filePath).Should().BeTrue();
            Path.GetExtension(filePath).Should().Be(".jpg");
            Path.GetFileName(filePath).Should().NotContain("{Guid}");
        }
        finally
        {
            if (image.RefCount > 0)
            {
                image.Release();
            }

            SafeDeleteDirectory(outputDir);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithLegacyParameterNames_ShouldAlsoSaveImage()
    {
        var outputDir = CreateOutputDirectory();
        var image = TestHelpers.CreateTestImage();

        try
        {
            var op = new Operator("test", OperatorType.ImageSave, 0, 0);
            op.AddParameter(TestHelpers.CreateParameter("FolderPath", outputDir, "string"));
            op.AddParameter(TestHelpers.CreateParameter("FileName", "legacy_{timestamp}.bmp", "string"));
            op.AddParameter(TestHelpers.CreateParameter("Format", "bmp", "string"));
            op.AddParameter(TestHelpers.CreateParameter("JpegQuality", 95, "int"));

            var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

            result.IsSuccess.Should().BeTrue();
            var filePath = result.OutputData!["FilePath"].Should().BeOfType<string>().Subject;
            File.Exists(filePath).Should().BeTrue();
            Path.GetExtension(filePath).Should().Be(".bmp");
        }
        finally
        {
            if (image.RefCount > 0)
            {
                image.Release();
            }

            SafeDeleteDirectory(outputDir);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithInjectedMetadataDefaultsAndLegacyValues_ShouldPreferLegacyConfiguration()
    {
        var outputDir = CreateOutputDirectory();
        var image = TestHelpers.CreateTestImage();

        try
        {
            var op = new Operator("test", OperatorType.ImageSave, 0, 0);

            // Simulate migrated nodes where new metadata parameters were injected with defaults.
            op.AddParameter(TestHelpers.CreateParameter("Directory", "C:\\ClearVision\\NG_Images", "string"));
            op.AddParameter(TestHelpers.CreateParameter("FileNameTemplate", "NG_{yyyyMMdd_HHmmss}_{Guid}.jpg", "string"));
            op.AddParameter(TestHelpers.CreateParameter("Quality", 90, "int"));

            // Existing legacy configuration should still win until the user explicitly edits the new fields.
            op.AddParameter(TestHelpers.CreateParameter("FolderPath", outputDir, "string"));
            op.AddParameter(TestHelpers.CreateParameter("FileName", "legacy_{Guid}_{guid}.jpg", "string"));
            op.AddParameter(TestHelpers.CreateParameter("JpegQuality", 81, "int"));

            var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

            result.IsSuccess.Should().BeTrue();

            var filePath = result.OutputData!["FilePath"].Should().BeOfType<string>().Subject;
            Path.GetDirectoryName(filePath).Should().Be(outputDir);

            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var match = Regex.Match(fileName, @"^legacy_([0-9a-f]{32})_([0-9a-f]{32})$");
            match.Success.Should().BeTrue();
            match.Groups[1].Value.Should().Be(match.Groups[2].Value);
        }
        finally
        {
            if (image.RefCount > 0)
            {
                image.Release();
            }

            SafeDeleteDirectory(outputDir);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenMetadataTemplateExplicitWithoutExtension_ShouldNotInheritLegacyExtension()
    {
        var outputDir = CreateOutputDirectory();
        var image = TestHelpers.CreateTestImage();

        try
        {
            var op = new Operator("test", OperatorType.ImageSave, 0, 0);

            op.AddParameter(TestHelpers.CreateParameter("Directory", "C:\\ClearVision\\NG_Images", "string"));
            var metadataTemplate = new Parameter(
                Guid.NewGuid(),
                "FileNameTemplate",
                "FileNameTemplate",
                string.Empty,
                "string",
                "NG_{yyyyMMdd_HHmmss}_{Guid}.jpg");
            metadataTemplate.SetValue("explicit_metadata_name");
            op.AddParameter(metadataTemplate);

            op.AddParameter(TestHelpers.CreateParameter("FolderPath", outputDir, "string"));
            op.AddParameter(TestHelpers.CreateParameter("FileName", "legacy_name.bmp", "string"));

            var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

            result.IsSuccess.Should().BeTrue();

            var filePath = result.OutputData!["FilePath"].Should().BeOfType<string>().Subject;
            Path.GetDirectoryName(filePath).Should().Be(outputDir);
            Path.GetFileNameWithoutExtension(filePath).Should().Be("explicit_metadata_name");
            Path.GetExtension(filePath).Should().Be(".png");
        }
        finally
        {
            if (image.RefCount > 0)
            {
                image.Release();
            }

            SafeDeleteDirectory(outputDir);
        }
    }

    private static string CreateOutputDirectory()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "test_results",
            "image-save-tests",
            Guid.NewGuid().ToString("N")));
    }

    private static void SafeDeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }
}
