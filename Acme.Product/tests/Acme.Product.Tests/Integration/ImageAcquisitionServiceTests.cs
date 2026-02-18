// ImageAcquisitionServiceTests.cs
// ImageAcquisitionService 集成测试
// 作者：蘅芜君

using Acme.Product.Application.DTOs;
using Acme.Product.Application.Services;
using Acme.Product.Core.Cameras;
using Acme.Product.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Integration;

/// <summary>
/// ImageAcquisitionService 集成测试
/// Sprint 4: S4-009 实现
/// </summary>
public class ImageAcquisitionServiceIntegrationTests
{
    private readonly ImageAcquisitionService _acquisitionService;
    private readonly ICameraManager _cameraManager;

    public ImageAcquisitionServiceIntegrationTests()
    {
        _cameraManager = Substitute.For<ICameraManager>();
        var logger = Substitute.For<ILogger<ImageAcquisitionService>>();
        _acquisitionService = new ImageAcquisitionService(_cameraManager, logger);
    }

    [Fact]
    public async Task LoadFromFileAsync_WithValidImagePath_ShouldReturnImageDto()
    {
        // Arrange
        var tempPath = Path.GetTempFileName() + ".png";
        try
        {
            await CreateTestImageAsync(tempPath);

            // Act
            var result = await _acquisitionService.LoadFromFileAsync(tempPath);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().NotBeEmpty();
            result.DataBase64.Should().NotBeNullOrEmpty();
            result.Width.Should().BeGreaterThan(0);
            result.Height.Should().BeGreaterThan(0);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public async Task LoadFromFileAsync_WithInvalidPath_ShouldThrowException()
    {
        // Arrange
        var invalidPath = "non_existent_image.jpg";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await _acquisitionService.LoadFromFileAsync(invalidPath);
        });
    }

    [Fact]
    public async Task LoadFromBase64Async_WithValidBase64_ShouldReturnImageDto()
    {
        // Arrange
        var base64Image = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

        // Act
        var result = await _acquisitionService.LoadFromBase64Async(base64Image);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.DataBase64.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoadFromBase64Async_WithInvalidBase64_ShouldThrowException()
    {
        // Arrange
        var invalidBase64 = "!!!invalid_base64!!!";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _acquisitionService.LoadFromBase64Async(invalidBase64);
        });
    }

    [Fact]
    public async Task ValidateImageFileAsync_WithValidImage_ShouldReturnValidResult()
    {
        // Arrange
        var tempPath = Path.GetTempFileName() + ".png";
        try
        {
            await CreateTestImageAsync(tempPath);

            // Act
            var result = await _acquisitionService.ValidateImageFileAsync(tempPath);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Width.Should().BeGreaterThan(0);
            result.Height.Should().BeGreaterThan(0);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public async Task ValidateImageFileAsync_WithInvalidPath_ShouldReturnInvalidResult()
    {
        // Arrange
        var invalidPath = "non_existent_image.jpg";

        // Act
        var result = await _acquisitionService.ValidateImageFileAsync(invalidPath);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("文件不存在");
    }

    [Fact]
    public async Task GetSupportedFormatsAsync_ShouldReturnSupportedFormats()
    {
        // Act
        var formats = await _acquisitionService.GetSupportedFormatsAsync();

        // Assert
        formats.Should().NotBeNull();
        formats.Should().Contain("jpg");
        formats.Should().Contain("png");
        formats.Should().Contain("bmp");
    }

    #region Helper Methods

    private static async Task CreateTestImageAsync(string path)
    {
        var base64Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        var bytes = Convert.FromBase64String(base64Png);
        await File.WriteAllBytesAsync(path, bytes);
    }

    #endregion
}
