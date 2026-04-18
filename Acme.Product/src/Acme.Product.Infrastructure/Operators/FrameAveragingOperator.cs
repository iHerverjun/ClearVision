// FrameAveragingOperator.cs
// 帧平均算子
// 对连续帧进行平均或中值融合降噪
// 作者：蘅芜君
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System.Collections.Concurrent;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "帧平均",
    Description = "Averages multi-frame input to reduce temporal noise.",
    Category = "预处理",
    IconName = "frame-average",
    Keywords = new[] { "frame", "averaging", "multi-frame", "denoise" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OutputPort("FrameCount", "Frame Count", PortDataType.Integer)]
[OperatorParam("FrameCount", "Frame Count", "int", DefaultValue = 8, Min = 1, Max = 64)]
[OperatorParam("Mode", "Mode", "enum", DefaultValue = "Mean", Options = new[] { "Mean|Mean", "Median|Median" })]
public class FrameAveragingOperator : OperatorBase, IDisposable
{
    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<Guid, FrameWindowState> _states = new();
    private readonly object _cleanupSync = new();
    private DateTime _lastCleanupUtc = DateTime.MinValue;

    public override OperatorType OperatorType => OperatorType.FrameAveraging;

    public FrameAveragingOperator(ILogger<FrameAveragingOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is required"));
        }

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid"));
        }

        var frameCount = GetIntParam(@operator, "FrameCount", 8, 1, 64);
        var mode = GetStringParam(@operator, "Mode", "Mean");
        var nowUtc = DateTime.UtcNow;
        var state = _states.GetOrAdd(@operator.Id, static _ => new FrameWindowState());

        List<Mat> snapshot;
        lock (state.SyncRoot)
        {
            if (state.Frames.Count > 0)
            {
                var reference = state.Frames.Peek();
                if (reference.Rows != src.Rows || reference.Cols != src.Cols || reference.Type() != src.Type())
                {
                    state.Clear();
                }
            }

            state.Frames.Enqueue(src.Clone());
            while (state.Frames.Count > frameCount)
            {
                var old = state.Frames.Dequeue();
                old.Dispose();
            }

            snapshot = state.Frames.Select(f => f.Clone()).ToList();
            state.LastTouchedUtc = nowUtc;
        }

        TryCleanupStaleStates(nowUtc);

        Mat result;
        try
        {
            result = mode.Equals("Median", StringComparison.OrdinalIgnoreCase)
                ? ComputeMedian(snapshot)
                : ComputeMean(snapshot);
        }
        finally
        {
            foreach (var mat in snapshot)
            {
                mat.Dispose();
            }
        }

        var output = new Dictionary<string, object>
        {
            { "FrameCount", snapshot.Count }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(result, output)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var frameCount = GetIntParam(@operator, "FrameCount", 8);
        if (frameCount < 1 || frameCount > 64)
        {
            return ValidationResult.Invalid("FrameCount must be in [1, 64]");
        }

        var mode = GetStringParam(@operator, "Mode", "Mean");
        var validModes = new[] { "Mean", "Median" };
        if (!validModes.Contains(mode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Mode must be Mean or Median");
        }

        return ValidationResult.Valid();
    }

    public void Dispose()
    {
        foreach (var state in _states.Values)
        {
            lock (state.SyncRoot)
            {
                state.Clear();
            }
        }

        _states.Clear();
    }

    private sealed class FrameWindowState
    {
        public object SyncRoot { get; } = new();
        public Queue<Mat> Frames { get; } = new();
        public DateTime LastTouchedUtc { get; set; } = DateTime.UtcNow;

        public void Clear()
        {
            while (Frames.Count > 0)
            {
                var stale = Frames.Dequeue();
                stale.Dispose();
            }
        }
    }

    private void TryCleanupStaleStates(DateTime nowUtc)
    {
        if ((nowUtc - _lastCleanupUtc) < CleanupInterval)
        {
            return;
        }

        lock (_cleanupSync)
        {
            if ((nowUtc - _lastCleanupUtc) < CleanupInterval)
            {
                return;
            }

            var staleBefore = nowUtc - StateTtl;
            foreach (var entry in _states)
            {
                var shouldRemove = false;
                var state = entry.Value;
                lock (state.SyncRoot)
                {
                    shouldRemove = state.LastTouchedUtc < staleBefore;
                }

                if (!shouldRemove || !_states.TryRemove(entry.Key, out var removedState))
                {
                    continue;
                }

                lock (removedState.SyncRoot)
                {
                    removedState.Clear();
                }
            }

            _lastCleanupUtc = nowUtc;
        }
    }

    private static Mat ComputeMean(IReadOnlyList<Mat> frames)
    {
        if (frames.Count == 0)
        {
            throw new InvalidOperationException("No frames available for averaging");
        }

        EnsureSameShapeAndType(frames);

        var channelCount = frames[0].Channels();
        var accumType = channelCount switch
        {
            1 => MatType.CV_32FC1,
            2 => MatType.CV_32FC2,
            3 => MatType.CV_32FC3,
            4 => MatType.CV_32FC4,
            _ => throw new InvalidOperationException($"Unsupported channel count for frame averaging: {channelCount}")
        };

        using var accum = new Mat(frames[0].Rows, frames[0].Cols, accumType, Scalar.All(0));
        using var noMask = new Mat();

        foreach (var frame in frames)
        {
            using var temp = new Mat();
            frame.ConvertTo(temp, accumType);
            Cv2.Accumulate(temp, accum, noMask);
        }

        var result = new Mat();
        accum.ConvertTo(result, frames[0].Type(), 1.0 / frames.Count);
        return result;
    }

    private static Mat ComputeMedian(IReadOnlyList<Mat> frames)
    {
        if (frames.Count == 0)
        {
            throw new InvalidOperationException("No frames available for averaging");
        }

        EnsureSameShapeAndType(frames);

        var rows = frames[0].Rows;
        var channels = frames[0].Channels();
        var depth = frames[0].Depth();
        var flatWidth = frames[0].Cols * channels;
        var flattenedViews = new Mat[frames.Count];

        try
        {
            for (var i = 0; i < frames.Count; i++)
            {
                flattenedViews[i] = frames[i].Reshape(1, rows);
            }

            return depth switch
            {
                MatType.CV_8U => ComputeMedianTyped<byte>(flattenedViews, rows, flatWidth, channels, depth),
                MatType.CV_16U => ComputeMedianTyped<ushort>(flattenedViews, rows, flatWidth, channels, depth),
                MatType.CV_32F => ComputeMedianTyped<float>(flattenedViews, rows, flatWidth, channels, depth),
                MatType.CV_64F => ComputeMedianTyped<double>(flattenedViews, rows, flatWidth, channels, depth),
                _ => throw new InvalidOperationException($"Unsupported depth for frame median fusion: {depth}")
            };
        }
        finally
        {
            foreach (var view in flattenedViews)
            {
                view?.Dispose();
            }
        }
    }

    private static Mat ComputeMedianTyped<T>(
        IReadOnlyList<Mat> flattenedFrames,
        int rows,
        int flatWidth,
        int channels,
        MatType depth)
        where T : unmanaged, IComparable<T>
    {
        var resultFlat = new Mat(rows, flatWidth, MatType.MakeType(depth, 1));
        var resultIndexer = resultFlat.GetGenericIndexer<T>();
        var frameIndexers = flattenedFrames.Select(frame => frame.GetGenericIndexer<T>()).ToArray();
        var samples = new T[flattenedFrames.Count];
        var medianIndex = flattenedFrames.Count / 2;

        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < flatWidth; col++)
            {
                for (var frame = 0; frame < flattenedFrames.Count; frame++)
                {
                    samples[frame] = frameIndexers[frame][row, col];
                }

                Array.Sort(samples);
                resultIndexer[row, col] = samples[medianIndex];
            }
        }

        using var reshaped = resultFlat.Reshape(channels, rows);
        return reshaped.Clone();
    }

    private static void EnsureSameShapeAndType(IReadOnlyList<Mat> frames)
    {
        var reference = frames[0];
        var rows = reference.Rows;
        var cols = reference.Cols;
        var type = reference.Type();

        for (var i = 1; i < frames.Count; i++)
        {
            if (frames[i].Rows != rows || frames[i].Cols != cols || frames[i].Type() != type)
            {
                throw new InvalidOperationException("All frames must have the same size and type");
            }
        }
    }
}

