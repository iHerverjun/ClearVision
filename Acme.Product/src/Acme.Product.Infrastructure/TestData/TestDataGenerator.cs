using System.Globalization;
using System.Text;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.TestData;

/// <summary>
/// Synthetic test data generator for W0 baseline validation.
/// Generates ISO 12233 slant edge images, geometric shapes, noisy variants,
/// and simple point clouds in PCD format.
/// </summary>
public static class TestDataGenerator
{
    public const int DefaultWidth = 512;
    public const int DefaultHeight = 512;
    public const double DefaultSlantAngleDegrees = 5.0;

    public static IReadOnlyList<string> GenerateAll(string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory is required.", nameof(outputDirectory));
        }

        Directory.CreateDirectory(outputDirectory);

        var rng = new Random(1337);
        var outputs = new List<string>();

        using var slantEdge = GenerateSlantEdge(DefaultWidth, DefaultHeight, DefaultSlantAngleDegrees);
        outputs.Add(SaveImage(slantEdge, Path.Combine(outputDirectory, "iso12233_slant_edge_512.png")));

        using var composite = GenerateCompositeShapes(DefaultWidth, DefaultHeight);
        outputs.Add(SaveImage(composite, Path.Combine(outputDirectory, "shapes_composite.png")));

        using var compositeGaussian = AddGaussianNoise(composite, sigma: 10, rng);
        outputs.Add(SaveImage(compositeGaussian, Path.Combine(outputDirectory, "shapes_composite_gaussian.png")));

        using var compositeSaltPepper = AddSaltPepperNoise(composite, density: 0.05, rng);
        outputs.Add(SaveImage(compositeSaltPepper, Path.Combine(outputDirectory, "shapes_composite_saltpepper.png")));

        using var circle = GenerateCircleImage(DefaultWidth, DefaultHeight, radius: 90);
        outputs.Add(SaveImage(circle, Path.Combine(outputDirectory, "shape_circle.png")));

        using var rectangle = GenerateRectangleImage(DefaultWidth, DefaultHeight, new Size(220, 120));
        outputs.Add(SaveImage(rectangle, Path.Combine(outputDirectory, "shape_rectangle.png")));

        using var triangle = GenerateTriangleImage(DefaultWidth, DefaultHeight);
        outputs.Add(SaveImage(triangle, Path.Combine(outputDirectory, "shape_triangle.png")));

        using var ellipse = GenerateEllipseImage(DefaultWidth, DefaultHeight, new Size(120, 80));
        outputs.Add(SaveImage(ellipse, Path.Combine(outputDirectory, "shape_ellipse.png")));

        using var slantGaussian = AddGaussianNoise(slantEdge, sigma: 10, rng);
        outputs.Add(SaveImage(slantGaussian, Path.Combine(outputDirectory, "iso12233_slant_edge_gaussian.png")));

        using var slantSaltPepper = AddSaltPepperNoise(slantEdge, density: 0.05, rng);
        outputs.Add(SaveImage(slantSaltPepper, Path.Combine(outputDirectory, "iso12233_slant_edge_saltpepper.png")));

        var plane = GeneratePlanePointCloud(count: 1000, size: 120, noiseStd: 0.5, rng);
        outputs.Add(WritePcd(Path.Combine(outputDirectory, "plane_1000.pcd"), plane));

        var sphere = GenerateSpherePointCloud(count: 2000, radius: 50, noiseStd: 0.5, rng);
        outputs.Add(WritePcd(Path.Combine(outputDirectory, "sphere_2000.pcd"), sphere));

        var cylinder = GenerateCylinderPointCloud(count: 1500, radius: 30, height: 100, noiseStd: 0.5, rng);
        outputs.Add(WritePcd(Path.Combine(outputDirectory, "cylinder_1500.pcd"), cylinder));

        return outputs;
    }

    /// <summary>
    /// Default test image used by benchmarks.
    /// Caller is responsible for disposing the returned Mat.
    /// </summary>
    public static Mat GenerateTestImage(int width = DefaultWidth, int height = DefaultHeight)
    {
        return GenerateCompositeShapes(width, height);
    }

    public static Mat GenerateSlantEdge(int width, int height, double angleDegrees, byte lowValue = 0, byte highValue = 255)
    {
        var mat = new Mat(height, width, MatType.CV_8UC1);
        double angleRad = angleDegrees * Math.PI / 180.0;
        double nx = -Math.Sin(angleRad);
        double ny = Math.Cos(angleRad);
        double cx = (width - 1) / 2.0;
        double cy = (height - 1) / 2.0;

        var indexer = mat.GetGenericIndexer<byte>();
        for (int y = 0; y < height; y++)
        {
            double dy = y - cy;
            for (int x = 0; x < width; x++)
            {
                double dx = x - cx;
                double value = nx * dx + ny * dy;
                indexer[y, x] = value >= 0 ? highValue : lowValue;
            }
        }

        return mat;
    }

    public static Mat GenerateCompositeShapes(int width, int height)
    {
        var mat = CreateCanvas(width, height);
        DrawCompositeShapes(mat);
        return mat;
    }

    public static Mat GenerateCircleImage(int width, int height, int radius)
    {
        var mat = CreateCanvas(width, height);
        var center = new Point(width / 2, height / 2);
        Cv2.Circle(mat, center, radius, Scalar.White, thickness: -1, LineTypes.AntiAlias);
        return mat;
    }

    public static Mat GenerateRectangleImage(int width, int height, Size size)
    {
        var mat = CreateCanvas(width, height);
        var rect = new Rect((width - size.Width) / 2, (height - size.Height) / 2, size.Width, size.Height);
        Cv2.Rectangle(mat, rect, Scalar.White, thickness: -1, LineTypes.AntiAlias);
        return mat;
    }

    public static Mat GenerateTriangleImage(int width, int height)
    {
        var mat = CreateCanvas(width, height);
        var points = new[]
        {
            new Point(width / 2, (int)(height * 0.2)),
            new Point((int)(width * 0.8), (int)(height * 0.8)),
            new Point((int)(width * 0.2), (int)(height * 0.8))
        };
        Cv2.FillConvexPoly(mat, points, Scalar.White, LineTypes.AntiAlias);
        return mat;
    }

    public static Mat GenerateEllipseImage(int width, int height, Size axes)
    {
        var mat = CreateCanvas(width, height);
        var center = new Point(width / 2, height / 2);
        Cv2.Ellipse(mat, center, axes, 0, 0, 360, Scalar.White, thickness: -1, LineTypes.AntiAlias);
        return mat;
    }

    public static Mat AddGaussianNoise(Mat source, double sigma, Random rng)
    {
        if (source.Empty())
        {
            throw new ArgumentException("Source Mat is empty.", nameof(source));
        }

        if (source.Type() != MatType.CV_8UC1)
        {
            throw new ArgumentException("Gaussian noise expects 8-bit single channel Mat.", nameof(source));
        }

        var dst = source.Clone();
        var srcIdx = source.GetGenericIndexer<byte>();
        var dstIdx = dst.GetGenericIndexer<byte>();

        for (int y = 0; y < source.Rows; y++)
        {
            for (int x = 0; x < source.Cols; x++)
            {
                double noise = NextGaussian(rng, 0, sigma);
                int value = (int)Math.Round(srcIdx[y, x] + noise);
                if (value < 0) value = 0;
                if (value > 255) value = 255;
                dstIdx[y, x] = (byte)value;
            }
        }

        return dst;
    }

    public static Mat AddSaltPepperNoise(Mat source, double density, Random rng)
    {
        if (source.Empty())
        {
            throw new ArgumentException("Source Mat is empty.", nameof(source));
        }

        if (source.Type() != MatType.CV_8UC1)
        {
            throw new ArgumentException("Salt & pepper noise expects 8-bit single channel Mat.", nameof(source));
        }

        if (density <= 0)
        {
            return source.Clone();
        }

        if (density >= 1)
        {
            var full = new Mat(source.Size(), MatType.CV_8UC1, Scalar.White);
            return full;
        }

        var dst = source.Clone();
        var indexer = dst.GetGenericIndexer<byte>();

        for (int y = 0; y < dst.Rows; y++)
        {
            for (int x = 0; x < dst.Cols; x++)
            {
                double r = rng.NextDouble();
                if (r < density)
                {
                    indexer[y, x] = rng.NextDouble() < 0.5 ? (byte)0 : (byte)255;
                }
            }
        }

        return dst;
    }

    public static List<Point3d> GeneratePlanePointCloud(int count, double size, double noiseStd, Random rng)
    {
        var half = size / 2.0;
        var points = new List<Point3d>(count);

        for (int i = 0; i < count; i++)
        {
            double x = RandomRange(rng, -half, half) + NextGaussian(rng, 0, noiseStd);
            double y = RandomRange(rng, -half, half) + NextGaussian(rng, 0, noiseStd);
            double z = NextGaussian(rng, 0, noiseStd);
            points.Add(new Point3d(x, y, z));
        }

        return points;
    }

    public static List<Point3d> GenerateSpherePointCloud(int count, double radius, double noiseStd, Random rng)
    {
        var points = new List<Point3d>(count);

        for (int i = 0; i < count; i++)
        {
            double u = rng.NextDouble() * 2.0 - 1.0;
            double phi = rng.NextDouble() * 2.0 * Math.PI;
            double sqrt = Math.Sqrt(1.0 - u * u);
            double x = radius * sqrt * Math.Cos(phi) + NextGaussian(rng, 0, noiseStd);
            double y = radius * sqrt * Math.Sin(phi) + NextGaussian(rng, 0, noiseStd);
            double z = radius * u + NextGaussian(rng, 0, noiseStd);
            points.Add(new Point3d(x, y, z));
        }

        return points;
    }

    public static List<Point3d> GenerateCylinderPointCloud(int count, double radius, double height, double noiseStd, Random rng)
    {
        var points = new List<Point3d>(count);
        double halfHeight = height / 2.0;

        for (int i = 0; i < count; i++)
        {
            double theta = rng.NextDouble() * 2.0 * Math.PI;
            double z = RandomRange(rng, -halfHeight, halfHeight);
            double x = radius * Math.Cos(theta) + NextGaussian(rng, 0, noiseStd);
            double y = radius * Math.Sin(theta) + NextGaussian(rng, 0, noiseStd);
            points.Add(new Point3d(x, y, z));
        }

        return points;
    }

    public static string WritePcd(string filePath, IReadOnlyList<Point3d> points)
    {
        if (points.Count == 0)
        {
            throw new ArgumentException("Point cloud is empty.", nameof(points));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? ".");

        var sb = new StringBuilder(points.Count * 32);
        sb.AppendLine("# .PCD v0.7 - Point Cloud Data file format");
        sb.AppendLine("VERSION 0.7");
        sb.AppendLine("FIELDS x y z");
        sb.AppendLine("SIZE 4 4 4");
        sb.AppendLine("TYPE F F F");
        sb.AppendLine("COUNT 1 1 1");
        sb.AppendLine($"WIDTH {points.Count}");
        sb.AppendLine("HEIGHT 1");
        sb.AppendLine("VIEWPOINT 0 0 0 1 0 0 0");
        sb.AppendLine($"POINTS {points.Count}");
        sb.AppendLine("DATA ascii");

        var culture = CultureInfo.InvariantCulture;
        foreach (var p in points)
        {
            sb.AppendLine($"{p.X.ToString("F6", culture)} {p.Y.ToString("F6", culture)} {p.Z.ToString("F6", culture)}");
        }

        File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return filePath;
    }

    private static Mat CreateCanvas(int width, int height, byte background = 0)
    {
        return new Mat(height, width, MatType.CV_8UC1, new Scalar(background));
    }

    private static void DrawCompositeShapes(Mat canvas)
    {
        int width = canvas.Width;
        int height = canvas.Height;

        var circleCenter = new Point((int)(width * 0.25), (int)(height * 0.25));
        int circleRadius = (int)(Math.Min(width, height) * 0.18);
        Cv2.Circle(canvas, circleCenter, circleRadius, Scalar.White, thickness: -1, LineTypes.AntiAlias);

        var rect = new Rect((int)(width * 0.55), (int)(height * 0.12), (int)(width * 0.3), (int)(height * 0.25));
        Cv2.Rectangle(canvas, rect, Scalar.White, thickness: -1, LineTypes.AntiAlias);

        var triangle = new[]
        {
            new Point((int)(width * 0.15), (int)(height * 0.65)),
            new Point((int)(width * 0.35), (int)(height * 0.9)),
            new Point((int)(width * 0.05), (int)(height * 0.9))
        };
        Cv2.FillConvexPoly(canvas, triangle, Scalar.White, LineTypes.AntiAlias);

        var ellipseCenter = new Point((int)(width * 0.7), (int)(height * 0.75));
        var ellipseAxes = new Size((int)(width * 0.18), (int)(height * 0.12));
        Cv2.Ellipse(canvas, ellipseCenter, ellipseAxes, 0, 0, 360, Scalar.White, thickness: -1, LineTypes.AntiAlias);
    }

    private static string SaveImage(Mat image, string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? ".");
        Cv2.ImWrite(filePath, image, new[] { (int)ImwriteFlags.PngCompression, 3 });
        return filePath;
    }

    private static double RandomRange(Random rng, double min, double max)
    {
        return min + (max - min) * rng.NextDouble();
    }

    private static double NextGaussian(Random rng, double mean, double stdDev)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        return mean + stdDev * randStdNormal;
    }
}
