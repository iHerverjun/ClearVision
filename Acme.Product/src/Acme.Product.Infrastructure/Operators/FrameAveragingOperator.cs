// FrameAveragingOperator.cs
// 帧平均算子
// 对连续帧进行平均或中值融合降噪
// 作者：蘅芜君
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

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
public class FrameAveragingOperator : OperatorBase
{
    private readonly object _syncRoot = new();
    private readonly Queue<Mat> _frames = new();

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

        List<Mat> snapshot;
        lock (_syncRoot)
        {
            if (_frames.Count > 0)
            {
                var reference = _frames.Peek();
                if (reference.Rows != src.Rows || reference.Cols != src.Cols || reference.Type() != src.Type())
                {
                    while (_frames.Count > 0)
                    {
                        var stale = _frames.Dequeue();
                        stale.Dispose();
                    }
                }
            }

            _frames.Enqueue(src.Clone());
            while (_frames.Count > frameCount)
            {
                var old = _frames.Dequeue();
                old.Dispose();
            }

            snapshot = _frames.Select(f => f.Clone()).ToList();
        }

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

        using var stacked = BuildTemporalStack(frames);
        using var sorted = new Mat();
        Cv2.Sort(stacked, sorted, SortFlags.EveryColumn | SortFlags.Ascending);

        var medianIndex = frames.Count / 2;
        using var medianRow = sorted.Row(medianIndex);
        using var medianFlat = medianRow.Clone();
        using var medianReshaped = medianFlat.Reshape(channels, rows);
        return medianReshaped.Clone();
    }

    private static Mat BuildTemporalStack(IReadOnlyList<Mat> frames)
    {
        var flattened = new Mat[frames.Count];

        try
        {
            for (var i = 0; i < frames.Count; i++)
            {
                using var reshaped = frames[i].Reshape(1, 1);
                flattened[i] = reshaped.Clone();
            }

            var stacked = new Mat();
            Cv2.VConcat(flattened, stacked);
            return stacked;
        }
        finally
        {
            foreach (var mat in flattened)
            {
                mat?.Dispose();
            }
        }
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

