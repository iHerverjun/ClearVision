using System.Collections.Concurrent;
using Acme.Product.Core.Cameras;
using Acme.Product.Core.Entities;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Cameras;

public sealed class CameraFrameStreamCoordinator : ICameraFrameStreamCoordinator
{
    private static readonly TimeSpan DirectAcquireIdleTimeout = TimeSpan.FromSeconds(5);
    private readonly ICameraManager _cameraManager;
    private readonly ILogger<CameraFrameStreamCoordinator> _logger;
    private readonly ConcurrentDictionary<string, ProducerEntry> _producers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, PreviewSessionState> _previewSessions = new(StringComparer.OrdinalIgnoreCase);

    private sealed class ProducerEntry
    {
        public ProducerEntry(string cameraBindingId)
        {
            CameraBindingId = cameraBindingId;
        }

        public string CameraBindingId { get; }
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public int LeaseCount { get; set; }
        public int PreviewSessionCount { get; set; }
        public bool IsRunning { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public CameraTriggerMode TriggerMode { get; set; } = CameraTriggerMode.Software;
        public int TargetFrameRateFps { get; set; } = CameraTriggerModeExtensions.DefaultTargetFrameRateFps;
        public CameraStreamFrame? LatestFrame { get; set; }
        public long Sequence;
        public long LastPublishedTicks;
        public TaskCompletionSource<long> NextFrameSignal { get; set; } = CreateFrameSignal();
        public CancellationTokenSource? IdleStopCts { get; set; }
    }

    private sealed class PreviewSessionState
    {
        public required string SessionId { get; init; }
        public required string CameraBindingId { get; init; }
        public long LastObservedSequence { get; set; }
        public CameraTriggerMode TriggerMode { get; init; }
        public int TargetFrameRateFps { get; init; }
    }

    private sealed record ResolvedBinding(
        string CameraBindingId,
        string SerialNumber,
        double ExposureTimeUs,
        double GainDb,
        CameraTriggerMode TriggerMode,
        int TargetFrameRateFps);

    public CameraFrameStreamCoordinator(
        ICameraManager cameraManager,
        ILogger<CameraFrameStreamCoordinator> logger)
    {
        _cameraManager = cameraManager;
        _logger = logger;
    }

    public async Task<CameraStreamFrame> AcquireFrameAsync(string cameraId, CancellationToken cancellationToken = default)
    {
        var binding = ResolveBinding(cameraId);
        if (!binding.TriggerMode.IsFrameDriven())
        {
            return await AcquireSoftwareFrameAsync(binding, cancellationToken);
        }

        var entry = await EnsureProducerAsync(binding, cancellationToken);
        long? afterSequence;
        await entry.Gate.WaitAsync(cancellationToken);
        try
        {
            entry.IdleStopCts?.Cancel();
            entry.IdleStopCts?.Dispose();
            entry.IdleStopCts = null;
            afterSequence = entry.LatestFrame?.Sequence;
        }
        finally
        {
            entry.Gate.Release();
        }

        var frame = await WaitForFrameCoreAsync(entry, afterSequence, cancellationToken);
        ArmDirectAcquireIdleStop(entry);
        return frame;
    }

    public async Task<CameraStreamLease> AcquireStreamLeaseAsync(string cameraId, CancellationToken cancellationToken = default)
    {
        var binding = ResolveBinding(cameraId);
        if (!binding.TriggerMode.IsFrameDriven())
        {
            throw new InvalidOperationException($"Camera binding '{binding.CameraBindingId}' is not configured for frame-driven acquisition.");
        }

        var entry = await EnsureProducerAsync(binding, cancellationToken);
        await entry.Gate.WaitAsync(cancellationToken);
        try
        {
            entry.LeaseCount++;
            return new CameraStreamLease(
                Guid.NewGuid().ToString("N"),
                binding.CameraBindingId,
                binding.TriggerMode,
                binding.TargetFrameRateFps);
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    public async Task<CameraStreamFrame> WaitForNextFrameAsync(
        CameraStreamLease lease,
        long? afterSequence = null,
        CancellationToken cancellationToken = default)
    {
        if (!_producers.TryGetValue(lease.CameraBindingId, out var entry))
        {
            throw new KeyNotFoundException($"Camera stream producer not found: {lease.CameraBindingId}");
        }

        return await WaitForFrameCoreAsync(entry, afterSequence, cancellationToken);
    }

    public async Task ReleaseStreamLeaseAsync(CameraStreamLease lease)
    {
        if (!_producers.TryGetValue(lease.CameraBindingId, out var entry))
        {
            return;
        }

        var disposeGate = false;
        await entry.Gate.WaitAsync();
        try
        {
            if (entry.LeaseCount > 0)
            {
                entry.LeaseCount--;
            }

            if (entry.LeaseCount == 0 && entry.PreviewSessionCount == 0)
            {
                await StopProducerCoreAsync(entry);
                _producers.TryRemove(entry.CameraBindingId, out _);
                disposeGate = true;
            }
        }
        finally
        {
            if (!disposeGate)
            {
                entry.Gate.Release();
            }
        }

        if (disposeGate)
        {
            entry.Gate.Dispose();
        }
    }

    public async Task<CameraPreviewSession> StartPreviewSessionAsync(string cameraId, CancellationToken cancellationToken = default)
    {
        var binding = ResolveBinding(cameraId);
        if (!binding.TriggerMode.IsFrameDriven())
        {
            throw new InvalidOperationException($"Camera binding '{binding.CameraBindingId}' is not configured for continuous preview.");
        }

        var entry = await EnsureProducerAsync(binding, cancellationToken);
        var sessionId = Guid.NewGuid().ToString("N");

        await entry.Gate.WaitAsync(cancellationToken);
        try
        {
            entry.PreviewSessionCount++;
            _previewSessions[sessionId] = new PreviewSessionState
            {
                SessionId = sessionId,
                CameraBindingId = binding.CameraBindingId,
                TriggerMode = binding.TriggerMode,
                TargetFrameRateFps = binding.TargetFrameRateFps,
                LastObservedSequence = 0
            };
        }
        finally
        {
            entry.Gate.Release();
        }

        return new CameraPreviewSession(sessionId, binding.CameraBindingId, binding.TriggerMode, binding.TargetFrameRateFps);
    }

    public async Task<CameraStreamFrame> WaitForPreviewFrameAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (!_previewSessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Preview session not found: {sessionId}");
        }

        if (!_producers.TryGetValue(session.CameraBindingId, out var entry))
        {
            throw new KeyNotFoundException($"Camera stream producer not found: {session.CameraBindingId}");
        }

        long? afterSequence = session.LastObservedSequence > 0 ? session.LastObservedSequence : null;
        var frame = await WaitForFrameCoreAsync(entry, afterSequence, cancellationToken);
        session.LastObservedSequence = frame.Sequence;
        return frame;
    }

    public async Task StopPreviewSessionAsync(string sessionId)
    {
        if (!_previewSessions.TryRemove(sessionId, out var session))
        {
            return;
        }

        if (!_producers.TryGetValue(session.CameraBindingId, out var entry))
        {
            return;
        }

        await entry.Gate.WaitAsync();
        var disposeGate = false;
        try
        {
            if (entry.PreviewSessionCount > 0)
            {
                entry.PreviewSessionCount--;
            }

            if (entry.LeaseCount == 0 && entry.PreviewSessionCount == 0)
            {
                await StopProducerCoreAsync(entry);
                _producers.TryRemove(entry.CameraBindingId, out _);
                disposeGate = true;
            }
        }
        finally
        {
            if (!disposeGate)
            {
                entry.Gate.Release();
            }
        }

        if (disposeGate)
        {
            entry.Gate.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sessionId in _previewSessions.Keys.ToArray())
        {
            await StopPreviewSessionAsync(sessionId);
        }

        foreach (var entry in _producers.Values.ToArray())
        {
            await entry.Gate.WaitAsync();
            try
            {
                await StopProducerCoreAsync(entry);
            }
            finally
            {
                entry.Gate.Release();
                entry.Gate.Dispose();
            }
        }

        _producers.Clear();
    }

    private ResolvedBinding ResolveBinding(string cameraId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cameraId);
        var binding = _cameraManager.FindBinding(cameraId);
        if (binding == null)
        {
            return new ResolvedBinding(
                cameraId.Trim(),
                cameraId.Trim(),
                5000.0,
                1.0,
                CameraTriggerMode.Software,
                CameraTriggerModeExtensions.DefaultTargetFrameRateFps);
        }

        binding.Normalize();
        return new ResolvedBinding(
            binding.Id,
            binding.SerialNumber,
            binding.ExposureTimeUs,
            binding.GainDb,
            CameraTriggerModeExtensions.Normalize(binding.TriggerMode),
            CameraTriggerModeExtensions.NormalizeTargetFrameRate(binding.TargetFrameRateFps));
    }

    private async Task<ProducerEntry> EnsureProducerAsync(ResolvedBinding binding, CancellationToken cancellationToken)
    {
        var entry = _producers.GetOrAdd(binding.CameraBindingId, id => new ProducerEntry(id));
        await entry.Gate.WaitAsync(cancellationToken);
        try
        {
            if (entry.IsRunning)
            {
                return entry;
            }

            await StartProducerCoreAsync(entry, binding, cancellationToken);
            return entry;
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    private async Task StartProducerCoreAsync(ProducerEntry entry, ResolvedBinding binding, CancellationToken cancellationToken)
    {
        var camera = await GetConnectedCameraAsync(binding, cancellationToken);
        await ApplyCommonCameraSettingsAsync(camera, binding);
        if (camera is IIndustrialCamera industrialCamera)
        {
            await industrialCamera.SetTriggerModeAsync(binding.TriggerMode);
        }

        await camera.StartContinuousAcquisitionAsync(async imageData =>
        {
            try
            {
                if (!TryEnterPublishWindow(entry, binding.TargetFrameRateFps))
                {
                    return;
                }

                var frame = CreateFrame(binding.CameraBindingId, imageData, "image/jpeg");
                PublishFrame(entry, frame);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish shared camera frame. CameraBindingId={CameraBindingId}", binding.CameraBindingId);
            }

            await Task.CompletedTask;
        });

        entry.IsRunning = true;
        entry.SerialNumber = binding.SerialNumber;
        entry.TriggerMode = binding.TriggerMode;
        entry.TargetFrameRateFps = binding.TargetFrameRateFps;
        entry.LatestFrame = null;
        entry.Sequence = 0;
        entry.LastPublishedTicks = 0;
        entry.NextFrameSignal = CreateFrameSignal();

        _logger.LogInformation(
            "Shared camera stream started. CameraBindingId={CameraBindingId}, TriggerMode={TriggerMode}, TargetFrameRateFps={TargetFrameRateFps}",
            binding.CameraBindingId,
            binding.TriggerMode,
            binding.TargetFrameRateFps);
    }

    private async Task StopProducerCoreAsync(ProducerEntry entry)
    {
        if (!entry.IsRunning)
        {
            return;
        }

        try
        {
            var camera = _cameraManager.GetCamera(entry.SerialNumber);
            if (camera != null)
            {
                await camera.StopContinuousAcquisitionAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stop shared camera stream cleanly. CameraBindingId={CameraBindingId}", entry.CameraBindingId);
        }
        finally
        {
            entry.IdleStopCts?.Cancel();
            entry.IdleStopCts?.Dispose();
            entry.IdleStopCts = null;
            entry.IsRunning = false;
            entry.SerialNumber = string.Empty;
            entry.LatestFrame = null;
            entry.Sequence = 0;
            entry.LastPublishedTicks = 0;
            entry.NextFrameSignal = CreateFrameSignal();
        }
    }

    private void ArmDirectAcquireIdleStop(ProducerEntry entry)
    {
        var idleCts = new CancellationTokenSource();
        CancellationTokenSource? previousCts;

        lock (entry)
        {
            previousCts = entry.IdleStopCts;
            entry.IdleStopCts = idleCts;
        }

        previousCts?.Cancel();
        previousCts?.Dispose();

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DirectAcquireIdleTimeout, idleCts.Token);
                await entry.Gate.WaitAsync(idleCts.Token);
                var disposeGate = false;
                try
                {
                    if (idleCts.IsCancellationRequested)
                    {
                        return;
                    }

                    if (entry.LeaseCount == 0 && entry.PreviewSessionCount == 0)
                    {
                        await StopProducerCoreAsync(entry);
                        _producers.TryRemove(entry.CameraBindingId, out _);
                        disposeGate = true;
                    }
                }
                finally
                {
                    if (!disposeGate)
                    {
                        entry.Gate.Release();
                    }
                }

                if (disposeGate)
                {
                    entry.Gate.Dispose();
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                lock (entry)
                {
                    if (ReferenceEquals(entry.IdleStopCts, idleCts))
                    {
                        entry.IdleStopCts = null;
                    }
                }

                idleCts.Dispose();
            }
        });
    }

    private async Task<CameraStreamFrame> AcquireSoftwareFrameAsync(ResolvedBinding binding, CancellationToken cancellationToken)
    {
        var camera = await GetConnectedCameraAsync(binding, cancellationToken);
        await ApplyCommonCameraSettingsAsync(camera, binding);
        if (camera is IIndustrialCamera industrialCamera)
        {
            await industrialCamera.SetTriggerModeAsync(CameraTriggerMode.Software);
        }

        var imageData = await camera.AcquireSingleFrameAsync();
        return CreateFrame(binding.CameraBindingId, imageData, "image/png");
    }

    private async Task<ICamera> GetConnectedCameraAsync(ResolvedBinding binding, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var existingCamera = _cameraManager.GetCamera(binding.SerialNumber);
        if (existingCamera?.IsConnected == true)
        {
            return existingCamera;
        }

        return await _cameraManager.GetOrCreateByBindingAsync(binding.CameraBindingId);
    }

    private static async Task ApplyCommonCameraSettingsAsync(ICamera camera, ResolvedBinding binding)
    {
        await camera.SetExposureTimeAsync(binding.ExposureTimeUs);
        await camera.SetGainAsync(binding.GainDb);
    }

    private async Task<CameraStreamFrame> WaitForFrameCoreAsync(
        ProducerEntry entry,
        long? afterSequence,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var latestFrame = entry.LatestFrame;
            var currentSequence = Interlocked.Read(ref entry.Sequence);
            if (latestFrame != null && (!afterSequence.HasValue || currentSequence > afterSequence.Value))
            {
                return latestFrame;
            }

            var signal = entry.NextFrameSignal;
            await signal.Task.WaitAsync(cancellationToken);
        }

        throw new OperationCanceledException(cancellationToken);
    }

    private static void PublishFrame(ProducerEntry entry, CameraStreamFrame frame)
    {
        var nextSequence = Interlocked.Increment(ref entry.Sequence);
        var publishedFrame = frame with
        {
            Sequence = nextSequence,
            TimestampUtc = DateTime.UtcNow
        };

        entry.LatestFrame = publishedFrame;
        var completedSignal = entry.NextFrameSignal;
        entry.NextFrameSignal = CreateFrameSignal();
        completedSignal.TrySetResult(nextSequence);
    }

    private static bool TryEnterPublishWindow(ProducerEntry entry, int targetFrameRateFps)
    {
        var minFrameTicks = TimeSpan.FromSeconds(1d / CameraTriggerModeExtensions.NormalizeTargetFrameRate(targetFrameRateFps)).Ticks;
        while (true)
        {
            var previousTicks = Interlocked.Read(ref entry.LastPublishedTicks);
            var nowTicks = DateTime.UtcNow.Ticks;
            if (previousTicks != 0 && nowTicks - previousTicks < minFrameTicks)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref entry.LastPublishedTicks, nowTicks, previousTicks) == previousTicks)
            {
                return true;
            }
        }
    }

    private static CameraStreamFrame CreateFrame(string cameraBindingId, byte[] imageData, string contentType)
    {
        using var decoded = Cv2.ImDecode(imageData, ImreadModes.Unchanged);
        if (decoded.Empty())
        {
            throw new InvalidOperationException("Unable to decode camera frame.");
        }

        return new CameraStreamFrame(
            cameraBindingId,
            imageData,
            contentType,
            decoded.Width,
            decoded.Height,
            0,
            DateTime.UtcNow);
    }

    private static TaskCompletionSource<long> CreateFrameSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
