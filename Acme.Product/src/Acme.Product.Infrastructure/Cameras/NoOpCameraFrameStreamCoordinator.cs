using Acme.Product.Core.Cameras;

namespace Acme.Product.Infrastructure.Cameras;

internal sealed class NoOpCameraFrameStreamCoordinator : ICameraFrameStreamCoordinator
{
    public static NoOpCameraFrameStreamCoordinator Instance { get; } = new();

    private NoOpCameraFrameStreamCoordinator()
    {
    }

    public Task<CameraStreamFrame> AcquireFrameAsync(string cameraId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Shared camera stream coordinator is not available in this context.");

    public Task<CameraStreamLease> AcquireStreamLeaseAsync(string cameraId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Shared camera stream coordinator is not available in this context.");

    public Task<CameraStreamFrame> WaitForNextFrameAsync(CameraStreamLease lease, long? afterSequence = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Shared camera stream coordinator is not available in this context.");

    public Task ReleaseStreamLeaseAsync(CameraStreamLease lease) => Task.CompletedTask;

    public Task<CameraPreviewSession> StartPreviewSessionAsync(string cameraId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Shared camera stream coordinator is not available in this context.");

    public Task<CameraStreamFrame> WaitForPreviewFrameAsync(string sessionId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Shared camera stream coordinator is not available in this context.");

    public Task StopPreviewSessionAsync(string sessionId) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
