using Acme.Product.Core.Cameras;
using Acme.Product.Core.Entities;
using Acme.Product.Infrastructure.Cameras;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Desktop.Tests;

public class CameraFrameStreamCoordinatorTests
{
    [Fact]
    public async Task AcquireFrameAsync_WhenProducerHasCachedFrame_ShouldWaitForNewerFrame()
    {
        var cameraManager = Substitute.For<ICameraManager>();
        var camera = Substitute.For<IIndustrialCamera>();
        camera.IsConnected.Returns(true);
        camera.SetExposureTimeAsync(Arg.Any<double>()).Returns(Task.CompletedTask);
        camera.SetGainAsync(Arg.Any<double>()).Returns(Task.CompletedTask);
        camera.SetTriggerModeAsync(Arg.Any<CameraTriggerMode>()).Returns(Task.CompletedTask);
        camera.StartContinuousAcquisitionAsync(Arg.Any<Func<byte[], Task>>()).Returns(Task.CompletedTask);
        camera.StopContinuousAcquisitionAsync().Returns(Task.CompletedTask);

        Func<byte[], Task>? frameCallback = null;
        camera.When(x => x.StartContinuousAcquisitionAsync(Arg.Any<Func<byte[], Task>>()))
            .Do(callInfo => frameCallback = callInfo.Arg<Func<byte[], Task>>());

        var binding = new CameraBindingConfig
        {
            Id = "binding-1",
            SerialNumber = "SN-001",
            TriggerMode = "Continuous"
        };

        cameraManager.GetBindings().Returns(new List<CameraBindingConfig> { binding });
        cameraManager.GetOrCreateByBindingAsync(binding.Id).Returns(Task.FromResult<ICamera>(camera));
        cameraManager.GetCamera(binding.SerialNumber).Returns(camera);

        await using var sut = new CameraFrameStreamCoordinator(cameraManager, NullLogger<CameraFrameStreamCoordinator>.Instance);
        var previewSession = await sut.StartPreviewSessionAsync(binding.Id);

        var firstFrame = CreatePngBytes(new Scalar(0, 0, 255));
        var secondFrame = CreatePngBytes(new Scalar(0, 255, 0));
        await frameCallback!(firstFrame);

        var acquireTask = sut.AcquireFrameAsync(binding.Id);
        await Task.Delay(100);
        acquireTask.IsCompleted.Should().BeFalse();

        await frameCallback(secondFrame);
        var frame = await acquireTask;

        frame.ImageData.Should().Equal(secondFrame);

        await sut.StopPreviewSessionAsync(previewSession.SessionId);
    }

    private static byte[] CreatePngBytes(Scalar color)
    {
        using var mat = new Mat(2, 2, MatType.CV_8UC3, color);
        return mat.ToBytes(".png");
    }
}
