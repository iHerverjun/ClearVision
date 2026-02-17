using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 匹配结果结构
/// </summary>
public readonly struct ShapeMatchResult
{
    public readonly Point Position;
    public readonly double Angle;
    public readonly double Score;
    public readonly bool IsValid;

    public ShapeMatchResult(Point position, double angle, double score)
    {
        Position = position;
        Angle = angle;
        Score = score;
        IsValid = score > 0;
    }

    public static ShapeMatchResult Empty => new ShapeMatchResult(new Point(-1, -1), 0, 0);
}

/// <summary>
/// 特征点结构
/// </summary>
internal readonly struct FeaturePoint
{
    public readonly short X;
    public readonly short Y;
    public readonly byte Direction;

    public FeaturePoint(short x, short y, byte direction)
    {
        X = x;
        Y = y;
        Direction = direction;
    }
}

/// <summary>
/// 旋转后的模板
/// </summary>
internal sealed class RotatedTemplate
{
    public readonly double Angle;
    public readonly FeaturePoint[] Features;
    public readonly int MinX, MaxX, MinY, MaxY;
    public readonly int Width, Height;

    public RotatedTemplate(double angle, FeaturePoint[] features, int minX, int maxX, int minY, int maxY)
    {
        Angle = angle;
        Features = features;
        MinX = minX;
        MaxX = maxX;
        MinY = minY;
        MaxY = maxY;
        Width = maxX - minX + 1;
        Height = maxY - minY + 1;
    }
}

/// <summary>
/// 基于梯度形状的模板匹配器 - 从清霜V3移植
/// </summary>
public sealed class GradientShapeMatcher : IDisposable
{
    private const int NumDirections = 8;
    private const double DirectionStep = Math.PI / 4.0;
    private const int DefaultMagnitudeThreshold = 30;
    private const int DefaultAngleStep = 1;
    private const double DefaultMinFeatureDistance = 2;

    private List<RotatedTemplate> _templates = new();
    private int _magnitudeThreshold;
    private int _angleStep;
    private bool _isDisposed;
    private bool _isTrained;
    private readonly bool[,] _directionMatchLut;

    public GradientShapeMatcher(int magnitudeThreshold = DefaultMagnitudeThreshold, int angleStep = DefaultAngleStep)
    {
        _magnitudeThreshold = magnitudeThreshold;
        _angleStep = angleStep;
        _directionMatchLut = BuildDirectionMatchLut();
    }

    /// <summary>
    /// 训练模板
    /// </summary>
    public void Train(Mat image, int angleRange = 180, Mat? mask = null)
    {
        if (image == null || image.Empty())
            throw new ArgumentException("模板图像不能为空", nameof(image));

        using var gray = EnsureGray(image);
        var baseFeatures = ExtractFeatures(gray, mask);

        if (baseFeatures.Count < 10)
            throw new InvalidOperationException($"特征点不足 ({baseFeatures.Count})");

        int centerX = gray.Width / 2;
        int centerY = gray.Height / 2;

        _templates.Clear();

        for (int angle = -angleRange; angle <= angleRange; angle += _angleStep)
        {
            var rotatedTemplate = CreateRotatedTemplate(
                baseFeatures, angle, centerX, centerY, gray.Width, gray.Height);
            _templates.Add(rotatedTemplate);
        }

        _isTrained = true;
    }

    /// <summary>
    /// 匹配
    /// </summary>
    public ShapeMatchResult Match(Mat sceneImage, double minScore = 80, Rect? searchRegion = null)
    {
        if (!_isTrained)
            throw new InvalidOperationException("模板未训练");

        if (sceneImage == null || sceneImage.Empty())
            throw new ArgumentException("场景图像不能为空", nameof(sceneImage));

        using var gray = EnsureGray(sceneImage);
        var (sceneDirections, sceneMagnitudes) = ComputeSceneGradients(gray);
        Rect region = searchRegion ?? new Rect(0, 0, gray.Width, gray.Height);

        return FindBestMatch(sceneDirections, sceneMagnitudes, region, minScore);
    }

    private unsafe List<FeaturePoint> ExtractFeatures(Mat gray, Mat? mask)
    {
        int width = gray.Width;
        int height = gray.Height;

        using var gradX = new Mat();
        using var gradY = new Mat();

        Cv2.Sobel(gray, gradX, MatType.CV_16S, 1, 0, 3);
        Cv2.Sobel(gray, gradY, MatType.CV_16S, 0, 1, 3);

        int centerX = width / 2;
        int centerY = height / 2;

        var features = new List<FeaturePoint>(width * height / 16);

        short* gxPtr = (short*)gradX.DataPointer;
        short* gyPtr = (short*)gradY.DataPointer;
        int gxStep = (int)gradX.Step() / sizeof(short);
        int gyStep = (int)gradY.Step() / sizeof(short);

        byte* maskPtr = null;
        int maskStep = 0;
        if (mask != null && !mask.Empty())
        {
            maskPtr = mask.DataPointer;
            maskStep = (int)mask.Step();
        }

        for (int y = 1; y < height - 1; y++)
        {
            short* gxRow = gxPtr + y * gxStep;
            short* gyRow = gyPtr + y * gyStep;
            byte* maskRow = maskPtr != null ? maskPtr + y * maskStep : null;

            for (int x = 1; x < width - 1; x++)
            {
                if (maskRow != null && maskRow[x] == 0)
                    continue;

                short gx = gxRow[x];
                short gy = gyRow[x];

                int magnitude = FastMagnitude(gx, gy);
                if (magnitude < _magnitudeThreshold)
                    continue;

                byte direction = QuantizeDirection(gx, gy);
                short relX = (short)(x - centerX);
                short relY = (short)(y - centerY);

                features.Add(new FeaturePoint(relX, relY, direction));
            }
        }

        return SparsifyFeatures(features, DefaultMinFeatureDistance);
    }

    private List<FeaturePoint> SparsifyFeatures(List<FeaturePoint> features, double minDistance)
    {
        if (minDistance <= 0 || features.Count < 100)
            return features;

        double minDistSq = minDistance * minDistance;
        var sparse = new List<FeaturePoint>(features.Count / 4);
        var occupied = new HashSet<long>();
        int gridSize = (int)Math.Ceiling(minDistance);

        foreach (var f in features)
        {
            int gx = f.X / gridSize;
            int gy = f.Y / gridSize;
            long key = ((long)gx << 32) | (uint)gy;

            if (!occupied.Contains(key))
            {
                sparse.Add(f);
                occupied.Add(key);
            }
        }

        return sparse;
    }

    private RotatedTemplate CreateRotatedTemplate(List<FeaturePoint> baseFeatures, double angleDeg,
        int centerX, int centerY, int templateWidth, int templateHeight)
    {
        double angleRad = angleDeg * Math.PI / 180.0;
        double cosA = Math.Cos(angleRad);
        double sinA = Math.Sin(angleRad);

        int directionOffset = (int)Math.Round(angleDeg / 45.0);
        directionOffset = ((directionOffset % NumDirections) + NumDirections) % NumDirections;

        var rotatedFeatures = new FeaturePoint[baseFeatures.Count];
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;

        for (int i = 0; i < baseFeatures.Count; i++)
        {
            var f = baseFeatures[i];
            double newX = f.X * cosA - f.Y * sinA;
            double newY = f.X * sinA + f.Y * cosA;

            short rx = (short)Math.Round(newX);
            short ry = (short)Math.Round(newY);
            byte newDir = (byte)((f.Direction + directionOffset) % NumDirections);

            rotatedFeatures[i] = new FeaturePoint(rx, ry, newDir);

            if (rx < minX) minX = rx;
            if (rx > maxX) maxX = rx;
            if (ry < minY) minY = ry;
            if (ry > maxY) maxY = ry;
        }

        return new RotatedTemplate(angleDeg, rotatedFeatures, minX, maxX, minY, maxY);
    }

    private unsafe (byte[,] directions, ushort[,] magnitudes) ComputeSceneGradients(Mat gray)
    {
        int width = gray.Width;
        int height = gray.Height;

        var directions = new byte[height, width];
        var magnitudes = new ushort[height, width];

        using var gradX = new Mat();
        using var gradY = new Mat();

        Cv2.Sobel(gray, gradX, MatType.CV_16S, 1, 0, 3);
        Cv2.Sobel(gray, gradY, MatType.CV_16S, 0, 1, 3);

        short* gxPtr = (short*)gradX.DataPointer;
        short* gyPtr = (short*)gradY.DataPointer;
        int gxStep = (int)gradX.Step() / sizeof(short);
        int gyStep = (int)gradY.Step() / sizeof(short);

        Parallel.For(0, height, y =>
        {
            short* gxRow = gxPtr + y * gxStep;
            short* gyRow = gyPtr + y * gyStep;

            for (int x = 0; x < width; x++)
            {
                short gx = gxRow[x];
                short gy = gyRow[x];

                int mag = FastMagnitude(gx, gy);
                magnitudes[y, x] = (ushort)Math.Min(mag, ushort.MaxValue);

                if (mag >= _magnitudeThreshold)
                    directions[y, x] = QuantizeDirection(gx, gy);
                else
                    directions[y, x] = 0xFF;
            }
        });

        return (directions, magnitudes);
    }

    private ShapeMatchResult FindBestMatch(byte[,] sceneDirections, ushort[,] sceneMagnitudes, 
        Rect searchRegion, double minScore)
    {
        int sceneWidth = sceneDirections.GetLength(1);
        int sceneHeight = sceneDirections.GetLength(0);

        var results = new ConcurrentBag<ShapeMatchResult>();
        double minScoreNormalized = minScore / 100.0;

        Parallel.ForEach(_templates, template =>
        {
            int startX = Math.Max(searchRegion.X - template.MinX, -template.MinX);
            int startY = Math.Max(searchRegion.Y - template.MinY, -template.MinY);
            int endX = Math.Min(searchRegion.X + searchRegion.Width - template.MaxX, sceneWidth - template.MaxX);
            int endY = Math.Min(searchRegion.Y + searchRegion.Height - template.MaxY, sceneHeight - template.MaxY);

            int stepX = 2;
            int stepY = 2;

            double bestScore = 0;
            int bestX = -1, bestY = -1;

            for (int y = startY; y < endY; y += stepY)
            {
                for (int x = startX; x < endX; x += stepX)
                {
                    double score = ComputeMatchScore(sceneDirections, sceneMagnitudes, template, x, y);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestX = x;
                        bestY = y;
                    }
                }
            }

            if (bestScore >= minScoreNormalized * 0.8 && bestX >= 0)
            {
                (bestX, bestY, bestScore) = RefineMatch(sceneDirections, sceneMagnitudes, template, bestX, bestY, stepX, stepY);
            }

            if (bestScore >= minScoreNormalized)
            {
                results.Add(new ShapeMatchResult(new Point(bestX, bestY), template.Angle, bestScore * 100));
            }
        });

        ShapeMatchResult best = ShapeMatchResult.Empty;
        foreach (var r in results)
        {
            if (r.Score > best.Score)
                best = r;
        }

        return best;
    }

    private (int x, int y, double score) RefineMatch(byte[,] sceneDirections, ushort[,] sceneMagnitudes,
        RotatedTemplate template, int centerX, int centerY, int rangeX, int rangeY)
    {
        double bestScore = 0;
        int bestX = centerX, bestY = centerY;

        for (int dy = -rangeY; dy <= rangeY; dy++)
        {
            for (int dx = -rangeX; dx <= rangeX; dx++)
            {
                double score = ComputeMatchScore(sceneDirections, sceneMagnitudes, template, centerX + dx, centerY + dy);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestX = centerX + dx;
                    bestY = centerY + dy;
                }
            }
        }

        return (bestX, bestY, bestScore);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeMatchScore(byte[,] sceneDirections, ushort[,] sceneMagnitudes,
        RotatedTemplate template, int cx, int cy)
    {
        int sceneWidth = sceneDirections.GetLength(1);
        int sceneHeight = sceneDirections.GetLength(0);

        int matchCount = 0;
        int validCount = 0;
        var features = template.Features;
        int featureCount = features.Length;
        int minRequired = featureCount * 2 / 3;

        for (int i = 0; i < featureCount; i++)
        {
            var f = features[i];
            int sx = cx + f.X;
            int sy = cy + f.Y;

            if (sx < 0 || sx >= sceneWidth || sy < 0 || sy >= sceneHeight)
                continue;

            validCount++;
            byte sceneDir = sceneDirections[sy, sx];

            if (sceneDir == 0xFF)
                continue;

            if (_directionMatchLut[f.Direction, sceneDir])
                matchCount++;

            int remaining = featureCount - i - 1;
            if (matchCount + remaining < minRequired)
                break;
        }

        if (validCount == 0)
            return 0;

        return (double)matchCount / featureCount;
    }

    private static bool[,] BuildDirectionMatchLut()
    {
        var lut = new bool[NumDirections, NumDirections];

        for (int t = 0; t < NumDirections; t++)
        {
            for (int s = 0; s < NumDirections; s++)
            {
                int diff = Math.Abs(t - s);
                if (diff > NumDirections / 2)
                    diff = NumDirections - diff;
                lut[t, s] = diff <= 1;
            }
        }

        return lut;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FastMagnitude(int gx, int gy)
    {
        int absX = gx >= 0 ? gx : -gx;
        int absY = gy >= 0 ? gy : -gy;

        if (absX > absY)
            return absX + (absY * 3 >> 3);
        else
            return absY + (absX * 3 >> 3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte QuantizeDirection(int gx, int gy)
    {
        double angle = Math.Atan2(gy, gx);
        if (angle < 0)
            angle += 2 * Math.PI;

        int quantized = (int)((angle + DirectionStep / 2) / DirectionStep) % NumDirections;
        return (byte)quantized;
    }

    private static Mat EnsureGray(Mat image)
    {
        if (image.Channels() == 1)
            return image.Clone();

        var gray = new Mat();
        Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        return gray;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _templates?.Clear();
            _templates = null!;
            _isDisposed = true;
        }
    }
}
