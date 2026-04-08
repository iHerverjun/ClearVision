// Region.cs
// 区域数据结构 - 基于游程编码(Run-Length Encoding)的高效表示
// 作者：AI Assistant

using System.Text.Json.Serialization;

namespace Acme.Product.Core.ValueObjects;

/// <summary>
/// 游程编码单元 - 表示单行中连续的一段前景像素
/// </summary>
public readonly struct RunLength : IEquatable<RunLength>
{
    public int Y { get; init; }
    public int StartX { get; init; }
    public int EndX { get; init; }

    public RunLength(int y, int startX, int endX)
    {
        Y = y;
        StartX = startX;
        EndX = endX;
    }

    public int Length => EndX - StartX + 1;

    public bool Contains(int x) => x >= StartX && x <= EndX;

    public bool Overlaps(RunLength other)
    {
        if (Y != other.Y) return false;
        return StartX <= other.EndX && EndX >= other.StartX;
    }

    public bool Equals(RunLength other)
    {
        return Y == other.Y && StartX == other.StartX && EndX == other.EndX;
    }

    public override bool Equals(object? obj) => obj is RunLength rl && Equals(rl);

    public override int GetHashCode() => HashCode.Combine(Y, StartX, EndX);

    public override string ToString() => $"RLE(Y={Y}, X=[{StartX},{EndX}])";
}

/// <summary>
/// 区域数据结构 - 使用游程编码高效存储二值区域
/// 对标 Halcon 的 Region 类型
/// </summary>
public class Region : ValueObject
{
    private int? _areaCache;
    private RegionRect? _boundingBoxCache;
    private RegionPoint2f? _centerCache;
    private Dictionary<int, RunLength[]>? _runsByRowCache;

    /// <summary>
    /// 游程编码数据 - 按 Y 坐标排序的游程列表
    /// </summary>
    public List<RunLength> RunLengths { get; init; } = new();

    /// <summary>
    /// 区域边界框
    /// </summary>
    [JsonIgnore]
    public RegionRect BoundingBox => _boundingBoxCache ??= CalculateBoundingBox();

    /// <summary>
    /// 区域面积（像素数）
    /// </summary>
    [JsonIgnore]
    public int Area => _areaCache ??= RunLengths.Sum(r => r.Length);

    /// <summary>
    /// 区域中心点
    /// </summary>
    [JsonIgnore]
    public RegionPoint2f Center => _centerCache ??= CalculateCenter();

    /// <summary>
    /// 是否为空区域
    /// </summary>
    [JsonIgnore]
    public bool IsEmpty => RunLengths.Count == 0;

    /// <summary>
    /// 从游程编码创建区域
    /// </summary>
    public Region(IEnumerable<RunLength> runLengths)
    {
        RunLengths = runLengths.OrderBy(r => r.Y).ThenBy(r => r.StartX).ToList();
    }

    /// <summary>
    /// 创建空区域
    /// </summary>
    public Region()
    {
    }

    /// <summary>
    /// 从二值图像创建区域（使用游程编码）
    /// </summary>
    public static Region FromBinaryImage(byte[] binaryData, int width, int height, int threshold = 127)
    {
        var runLengths = new List<RunLength>();

        for (int y = 0; y < height; y++)
        {
            int x = 0;
            while (x < width)
            {
                // 跳过背景
                while (x < width && binaryData[y * width + x] < threshold)
                    x++;

                if (x >= width) break;

                int startX = x;
                // 找到前景连续段
                while (x < width && binaryData[y * width + x] >= threshold)
                    x++;

                runLengths.Add(new RunLength(y, startX, x - 1));
            }
        }

        return new Region(runLengths);
    }

    /// <summary>
    /// 从二值 Mat 创建区域
    /// </summary>
    public static Region FromMat(OpenCvSharp.Mat binaryMat, int threshold = 127)
    {
        if (binaryMat.Empty())
            return new Region();

        var runLengths = new List<RunLength>();
        int width = binaryMat.Width;
        int height = binaryMat.Height;

        // 安全方式遍历 - 使用 Mat 索引器
        for (int y = 0; y < height; y++)
        {
            int x = 0;
            while (x < width)
            {
                // 跳过背景
                while (x < width && binaryMat.At<byte>(y, x) < threshold)
                    x++;

                if (x >= width) break;

                int startX = x;
                // 找到前景连续段
                while (x < width && binaryMat.At<byte>(y, x) >= threshold)
                    x++;

                runLengths.Add(new RunLength(y, startX, x - 1));
            }
        }

        return new Region(runLengths);
    }

    /// <summary>
    /// 将区域转换为二值 Mat
    /// </summary>
    public OpenCvSharp.Mat ToMat()
    {
        if (IsEmpty)
            return new OpenCvSharp.Mat(1, 1, OpenCvSharp.MatType.CV_8UC1, OpenCvSharp.Scalar.All(0));

        var bbox = BoundingBox;
        var mat = new OpenCvSharp.Mat(bbox.Height, bbox.Width, OpenCvSharp.MatType.CV_8UC1, OpenCvSharp.Scalar.All(0));

        foreach (var run in RunLengths)
        {
            int y = run.Y - bbox.Y;
            if (y < 0 || y >= bbox.Height) continue;

            int startX = run.StartX - bbox.X;
            int endX = run.EndX - bbox.X;

            startX = Math.Max(0, startX);
            endX = Math.Min(bbox.Width - 1, endX);

            if (startX <= endX)
            {
                mat.Row(y).ColRange(startX, endX + 1).SetTo(OpenCvSharp.Scalar.All(255));
            }
        }

        return mat;
    }

    /// <summary>
    /// 判断点是否在区域内
    /// </summary>
    public bool ContainsPoint(int x, int y)
    {
        if (IsEmpty)
        {
            return false;
        }

        var bbox = BoundingBox;
        if (!bbox.Contains(x, y))
        {
            return false;
        }

        var runsByRow = _runsByRowCache ??= BuildRunsByRowIndex();
        if (!runsByRow.TryGetValue(y, out var rowRuns))
        {
            return false;
        }

        int left = 0;
        int right = rowRuns.Length - 1;

        while (left <= right)
        {
            int mid = left + ((right - left) / 2);
            var run = rowRuns[mid];

            if (x < run.StartX)
            {
                right = mid - 1;
            }
            else if (x > run.EndX)
            {
                left = mid + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 获取区域的轮廓点（外边界）
    /// </summary>
    public List<OpenCvSharp.Point> GetContourPoints()
    {
        // 使用 Mat 提取轮廓（简化实现）
        using var mat = ToMat();
        using var fullMat = new OpenCvSharp.Mat(BoundingBox.Height + 2, BoundingBox.Width + 2, 
            OpenCvSharp.MatType.CV_8UC1, OpenCvSharp.Scalar.All(0));
        
        var roi = new OpenCvSharp.Rect(1, 1, BoundingBox.Width, BoundingBox.Height);
        mat.CopyTo(fullMat[roi]);

        OpenCvSharp.Cv2.FindContours(fullMat, out var contours, out _, 
            OpenCvSharp.RetrievalModes.External, OpenCvSharp.ContourApproximationModes.ApproxSimple);

        var points = new List<OpenCvSharp.Point>();
        foreach (var contour in contours)
        {
            foreach (var pt in contour)
            {
                points.Add(new OpenCvSharp.Point(pt.X - 1 + BoundingBox.X, pt.Y - 1 + BoundingBox.Y));
            }
        }

        return points;
    }

    /// <summary>
    /// 区域平移
    /// </summary>
    public Region Translate(int dx, int dy)
    {
        var translated = RunLengths.Select(r => new RunLength(r.Y + dy, r.StartX + dx, r.EndX + dx));
        return new Region(translated);
    }

    /// <summary>
    /// 区域缩放（简化实现 - 基于 Mat 重采样）
    /// </summary>
    public Region Scale(double scaleX, double scaleY)
    {
        if (IsEmpty) return new Region();

        using var mat = ToMat();
        var scaledMat = new OpenCvSharp.Mat();
        OpenCvSharp.Cv2.Resize(mat, scaledMat, new OpenCvSharp.Size(0, 0), scaleX, scaleY, 
            OpenCvSharp.InterpolationFlags.Nearest);

        var result = FromMat(scaledMat);
        scaledMat.Dispose();

        // 调整位置
        var bbox = BoundingBox;
        return result.Translate(
            (int)(bbox.X * scaleX) - bbox.X,
            (int)(bbox.Y * scaleY) - bbox.Y);
    }

    /// <summary>
    /// 合并相邻的游程（优化存储）
    /// </summary>
    public Region MergeAdjacentRuns()
    {
        if (RunLengths.Count <= 1) return this;

        var merged = new List<RunLength>();
        var current = RunLengths[0];

        for (int i = 1; i < RunLengths.Count; i++)
        {
            var next = RunLengths[i];

            // 同一行且相邻或重叠
            if (next.Y == current.Y && next.StartX <= current.EndX + 1)
            {
                current = new RunLength(current.Y, current.StartX, Math.Max(current.EndX, next.EndX));
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }
        merged.Add(current);

        return new Region(merged);
    }

    private RegionRect CalculateBoundingBox()
    {
        if (IsEmpty) return new RegionRect(0, 0, 0, 0);

        int minX = RunLengths.Min(r => r.StartX);
        int maxX = RunLengths.Max(r => r.EndX);
        int minY = RunLengths.Min(r => r.Y);
        int maxY = RunLengths.Max(r => r.Y);

        return new RegionRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private RegionPoint2f CalculateCenter()
    {
        if (IsEmpty) return new RegionPoint2f(0, 0);

        double sumX = 0;
        double sumY = 0;
        int count = 0;

        foreach (var run in RunLengths)
        {
            for (int x = run.StartX; x <= run.EndX; x++)
            {
                sumX += x;
                sumY += run.Y;
                count++;
            }
        }

        return new RegionPoint2f((float)(sumX / count), (float)(sumY / count));
    }

    private Dictionary<int, RunLength[]> BuildRunsByRowIndex()
    {
        return RunLengths
            .GroupBy(run => run.Y)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(run => run.StartX).ToArray());
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return RunLengths.Count;
        foreach (var run in RunLengths)
        {
            yield return run.Y;
            yield return run.StartX;
            yield return run.EndX;
        }
    }

    public override string ToString()
    {
        return $"Region(Runs={RunLengths.Count}, Area={Area}, BBox={BoundingBox})";
    }
}

/// <summary>
/// 区域连通性类型
/// </summary>
public enum ConnectivityType
{
    FourConnected = 4,
    EightConnected = 8
}

/// <summary>
/// 形态学核形状
/// </summary>
public enum MorphologyKernelShape
{
    Rectangle,
    Ellipse,
    Cross
}

/// <summary>
/// 形态学核 - 用于区域形态学操作
/// </summary>
public class MorphologyKernel : ValueObject
{
    private List<(int dx, int dy)>? _offsetCache;

    public MorphologyKernelShape Shape { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }

    public MorphologyKernel(MorphologyKernelShape shape, int width, int height)
    {
        Shape = shape;
        Width = width;
        Height = height;
    }

    public static MorphologyKernel Rectangle(int width, int height) => 
        new(MorphologyKernelShape.Rectangle, width, height);

    public static MorphologyKernel Ellipse(int width, int height) => 
        new(MorphologyKernelShape.Ellipse, width, height);

    public static MorphologyKernel Cross(int size) => 
        new(MorphologyKernelShape.Cross, size, size);

    /// <summary>
    /// 获取核的扫描偏移列表
    /// </summary>
    public IEnumerable<(int dx, int dy)> GetOffsets()
    {
        if (_offsetCache != null)
        {
            return _offsetCache;
        }

        if (Width <= 0 || Height <= 0)
        {
            _offsetCache = new List<(int dx, int dy)>();
            return _offsetCache;
        }

        using var kernel = OpenCvSharp.Cv2.GetStructuringElement(
            ToOpenCvShape(),
            new OpenCvSharp.Size(Width, Height));

        int anchorX = (Width - 1) / 2;
        int anchorY = (Height - 1) / 2;
        var offsets = new List<(int dx, int dy)>();

        for (int y = 0; y < kernel.Rows; y++)
        {
            for (int x = 0; x < kernel.Cols; x++)
            {
                if (kernel.At<byte>(y, x) == 0)
                {
                    continue;
                }

                offsets.Add((x - anchorX, y - anchorY));
            }
        }

        _offsetCache = offsets;
        return _offsetCache;
    }

    private OpenCvSharp.MorphShapes ToOpenCvShape()
    {
        return Shape switch
        {
            MorphologyKernelShape.Rectangle => OpenCvSharp.MorphShapes.Rect,
            MorphologyKernelShape.Ellipse => OpenCvSharp.MorphShapes.Ellipse,
            MorphologyKernelShape.Cross => OpenCvSharp.MorphShapes.Cross,
            _ => OpenCvSharp.MorphShapes.Rect
        };
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Shape;
        yield return Width;
        yield return Height;
    }
}

/// <summary>
/// 矩形结构（简化版，避免与 OpenCvSharp.Rect 冲突时的序列化问题）
/// </summary>
public readonly struct RegionRect : IEquatable<RegionRect>
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }

    public RegionRect(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int Left => X;
    public int Top => Y;
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;

    public bool IntersectsWith(RegionRect other)
    {
        return Left < other.Right && Right > other.Left && 
               Top < other.Bottom && Bottom > other.Top;
    }

    public RegionRect Intersect(RegionRect other)
    {
        int x1 = Math.Max(Left, other.Left);
        int y1 = Math.Max(Top, other.Top);
        int x2 = Math.Min(Right, other.Right);
        int y2 = Math.Min(Bottom, other.Bottom);

        if (x2 <= x1 || y2 <= y1)
            return new RegionRect(0, 0, 0, 0);

        return new RegionRect(x1, y1, x2 - x1, y2 - y1);
    }

    public static implicit operator OpenCvSharp.Rect(RegionRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    public static implicit operator RegionRect(OpenCvSharp.Rect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    public bool Equals(RegionRect other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
    public override bool Equals(object? obj) => obj is RegionRect r && Equals(r);
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    public override string ToString() => $"RegionRect(X={X}, Y={Y}, W={Width}, H={Height})";
}

/// <summary>
/// 2D点（简化版）
/// </summary>
public readonly struct RegionPoint2f : IEquatable<RegionPoint2f>
{
    public float X { get; init; }
    public float Y { get; init; }

    public RegionPoint2f(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static implicit operator OpenCvSharp.Point2f(RegionPoint2f point) => new(point.X, point.Y);

    public static implicit operator RegionPoint2f(OpenCvSharp.Point2f point) => new(point.X, point.Y);

    public bool Equals(RegionPoint2f other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is RegionPoint2f p && Equals(p);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"RegionPoint2f({X:F2}, {Y:F2})";
}
