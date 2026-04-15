namespace Acme.Product.Core.Cameras;

public interface ICameraFrameStreamCoordinator : IAsyncDisposable
{
    Task<CameraStreamFrame> AcquireFrameAsync(string cameraId, CancellationToken cancellationToken = default);
    Task<CameraStreamLease> AcquireStreamLeaseAsync(string cameraId, CancellationToken cancellationToken = default);
    Task<CameraStreamFrame> WaitForNextFrameAsync(
        CameraStreamLease lease,
        long? afterSequence = null,
        CancellationToken cancellationToken = default);
    Task ReleaseStreamLeaseAsync(CameraStreamLease lease);
    Task<CameraPreviewSession> StartPreviewSessionAsync(string cameraId, CancellationToken cancellationToken = default);
    Task<CameraStreamFrame> WaitForPreviewFrameAsync(string sessionId, CancellationToken cancellationToken = default);
    Task StopPreviewSessionAsync(string sessionId);
}

public sealed record CameraStreamFrame(
    string CameraBindingId,
    byte[] ImageData,
    string ContentType,
    int Width,
    int Height,
    long Sequence,
    DateTime TimestampUtc);

public sealed record CameraStreamLease(
    string LeaseId,
    string CameraBindingId,
    CameraTriggerMode TriggerMode,
    int TargetFrameRateFps);

public sealed record CameraPreviewSession(
    string SessionId,
    string CameraBindingId,
    CameraTriggerMode TriggerMode,
    int TargetFrameRateFps);
