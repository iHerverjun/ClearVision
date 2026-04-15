using System.Runtime.InteropServices;
using Acme.Product.Core.Cameras;
using Acme.Product.Infrastructure.Cameras;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Desktop.Tests;

public class CameraProviderAdapterTests
{
    [Fact]
    public async Task AcquireSingleFrameAsync_WhenNotGrabbing_ShouldExecuteSoftwareTriggerSequence()
    {
        var provider = Substitute.For<ICameraProvider>();
        provider.IsGrabbing.Returns(false);
        provider.StartGrabbing().Returns(true);
        provider.SetTriggerMode(CameraTriggerMode.Software).Returns(true);
        provider.ExecuteSoftwareTrigger().Returns(true);

        var frameBuffer = new byte[] { 0, 127, 255, 60 };
        var handle = GCHandle.Alloc(frameBuffer, GCHandleType.Pinned);

        try
        {
            provider.GetFrame(3000).Returns(new CameraFrame
            {
                DataPtr = handle.AddrOfPinnedObject(),
                Width = 2,
                Height = 2,
                Size = frameBuffer.Length,
                PixelFormat = CameraPixelFormat.Mono8,
                FrameNumber = 1,
                Timestamp = 1
            });

            var adapter = new CameraProviderAdapter("cam-1", provider, Substitute.For<ILogger<CameraProviderAdapter>>());
            var pngBytes = await adapter.AcquireSingleFrameAsync();

            pngBytes.Should().NotBeNullOrEmpty();
            pngBytes.Take(8).Should().Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

            Received.InOrder(() =>
            {
                provider.StartGrabbing();
                provider.SetTriggerMode(CameraTriggerMode.Software);
                provider.ExecuteSoftwareTrigger();
                provider.GetFrame(3000);
            });
        }
        finally
        {
            handle.Free();
        }
    }

    [Fact]
    public async Task AcquireSingleFrameAsync_WhenAlreadyGrabbing_ShouldNotCallStartGrabbing()
    {
        var provider = Substitute.For<ICameraProvider>();
        provider.IsGrabbing.Returns(true);
        provider.SetTriggerMode(CameraTriggerMode.Software).Returns(true);
        provider.ExecuteSoftwareTrigger().Returns(true);

        var frameBuffer = new byte[] { 10, 20, 30, 40 };
        var handle = GCHandle.Alloc(frameBuffer, GCHandleType.Pinned);

        try
        {
            provider.GetFrame(3000).Returns(new CameraFrame
            {
                DataPtr = handle.AddrOfPinnedObject(),
                Width = 2,
                Height = 2,
                Size = frameBuffer.Length,
                PixelFormat = CameraPixelFormat.Mono8
            });

            var adapter = new CameraProviderAdapter("cam-2", provider, Substitute.For<ILogger<CameraProviderAdapter>>());
            var pngBytes = await adapter.AcquireSingleFrameAsync();

            pngBytes.Should().NotBeNullOrEmpty();
            provider.DidNotReceive().StartGrabbing();
            provider.Received(1).SetTriggerMode(CameraTriggerMode.Software);
            provider.Received(1).ExecuteSoftwareTrigger();
            provider.Received(1).GetFrame(3000);
        }
        finally
        {
            handle.Free();
        }
    }

    [Fact]
    public async Task AcquireSingleFrameAsync_WhenFrameIsNull_ShouldThrowTimeoutException()
    {
        var provider = Substitute.For<ICameraProvider>();
        provider.IsGrabbing.Returns(true);
        provider.GetFrame(3000).Returns((CameraFrame?)null);

        var adapter = new CameraProviderAdapter("cam-3", provider, Substitute.For<ILogger<CameraProviderAdapter>>());

        var act = async () => await adapter.AcquireSingleFrameAsync();

        await act.Should().ThrowAsync<TimeoutException>()
            .WithMessage("*获取图像超时*");

        provider.Received(1).SetTriggerMode(CameraTriggerMode.Software);
        provider.Received(1).ExecuteSoftwareTrigger();
        provider.Received(1).GetFrame(3000);
    }
}
