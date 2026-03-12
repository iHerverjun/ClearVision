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

    [Fact]
    public async Task AcquireFromCameraAsync_WithConnectedCamera_ShouldReturnImageDto()
    {
        // Arrange
        var camera = Substitute.For<ICamera>();
        camera.IsConnected.Returns(true);
        camera.AcquireSingleFrameAsync().Returns(Task.FromResult(CreateTestImageBytes()));
        _cameraManager.GetCamera("camera-1").Returns(camera);

        // Act
        var result = await _acquisitionService.AcquireFromCameraAsync("camera-1");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Width.Should().BeGreaterThan(0);
        result.Height.Should().BeGreaterThan(0);
        result.DataBase64.Should().NotBeNullOrEmpty();
        await camera.Received(1).AcquireSingleFrameAsync();
    }

    [Fact]
    public async Task StartContinuousAcquisitionAsync_WithConnectedCamera_ShouldInvokeCallback()
    {
        // Arrange
        var camera = Substitute.For<ICamera>();
        camera.IsConnected.Returns(true);
        camera.AcquireSingleFrameAsync().Returns(Task.FromResult(CreateTestImageBytes()));
        _cameraManager.GetCamera("camera-stream").Returns(camera);

        var frameReceived = new TaskCompletionSource<ImageDto>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        await _acquisitionService.StartContinuousAcquisitionAsync(
            "camera-stream",
            20,
            image =>
            {
                frameReceived.TrySetResult(image);
                return Task.CompletedTask;
            });

        var completed = await Task.WhenAny(frameReceived.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        await _acquisitionService.StopContinuousAcquisitionAsync("camera-stream");

        // Assert
        completed.Should().Be(frameReceived.Task);
        var result = await frameReceived.Task;
        result.Width.Should().BeGreaterThan(0);
        result.Height.Should().BeGreaterThan(0);
        await camera.Received().AcquireSingleFrameAsync();
    }

    [Fact]
    public async Task PreprocessAsync_WithResizeAndGrayscale_ShouldReturnProcessedImage()
    {
        // Arrange
        var source = await _acquisitionService.LoadFromBase64Async(CreateTestImageBase64());

        // Act
        var result = await _acquisitionService.PreprocessAsync(source.Id, new ImagePreprocessOptions
        {
            TargetWidth = 16,
            TargetHeight = 16,
            KeepAspectRatio = false,
            ConvertToGrayscale = true
        });

        var info = await _acquisitionService.GetImageInfoAsync(result.Id);

        // Assert
        result.Width.Should().Be(16);
        result.Height.Should().Be(16);
        info.Should().NotBeNull();
        info!.Width.Should().Be(16);
        info.Height.Should().Be(16);
        info.Channels.Should().Be(1);
    }

    [Fact]
    public async Task SaveToFileAsync_WithCachedImage_ShouldWriteFile()
    {
        // Arrange
        var image = await _acquisitionService.LoadFromBase64Async(CreateTestImageBase64());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var filePath = Path.Combine(tempDir, "saved-image.png");

        try
        {
            // Act
            var savedPath = await _acquisitionService.SaveToFileAsync(image.Id, filePath, "png", 95);

            // Assert
            savedPath.Should().Be(filePath);
            File.Exists(savedPath).Should().BeTrue();
            new FileInfo(savedPath).Length.Should().BeGreaterThan(0);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetImageInfoAsync_WithCachedImage_ShouldReturnMetadata()
    {
        // Arrange
        var image = await _acquisitionService.LoadFromBase64Async(CreateTestImageBase64());

        // Act
        var info = await _acquisitionService.GetImageInfoAsync(image.Id);

        // Assert
        info.Should().NotBeNull();
        info!.Id.Should().Be(image.Id);
        info.Width.Should().Be(image.Width);
        info.Height.Should().Be(image.Height);
        info.IsInMemory.Should().BeTrue();
        info.FileSize.Should().BeGreaterThan(0);
    }

    #region Helper Methods

    private static string CreateTestImageBase64()
    {
        return "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
    }

    private static byte[] CreateTestImageBytes()
    {
        return Convert.FromBase64String(CreateTestImageBase64());
    }

    private static async Task CreateTestImageAsync(string path)
    {
        var bytes = CreateTestImageBytes();
        await File.WriteAllBytesAsync(path, bytes);
    }

    #endregion
}
