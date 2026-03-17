using System.Numerics;
using Acme.Product.Infrastructure.Memory;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.PointCloud;

/// <summary>
/// Lightweight point cloud container backed by OpenCvSharp Mat.
/// - Points: Nx3 float32 (x,y,z)
/// - Colors: Nx3 uint8 (r,g,b) optional
/// - Normals: Nx3 float32 (nx,ny,nz) optional
/// </summary>
public sealed class PointCloud : IDisposable
{
    private readonly MatPool _pool;
    private bool _disposed;

    public Mat Points { get; }
    public Mat? Colors { get; }
    public Mat? Normals { get; }

    public int Count => Points.Rows;
    public bool IsOrganized { get; }
    public int Width { get; }
    public int Height { get; }

    public PointCloud(Mat points, Mat? colors = null, Mat? normals = null, bool isOrganized = false, int width = 0, int height = 0, MatPool? pool = null)
    {
        Points = points ?? throw new ArgumentNullException(nameof(points));
        Colors = colors;
        Normals = normals;
        _pool = pool ?? MatPool.Shared;

        // Allow an empty point cloud (0 points) as long as the matrix has the expected shape/type.
        // OpenCvSharp considers 0-row mats as Empty(), so we validate by dimensions instead.
        if (Points.Type() != MatType.CV_32FC1 || Points.Cols != 3 || Points.Rows < 0)
        {
            throw new ArgumentException("Points must be Nx3 CV_32FC1.", nameof(points));
        }

        if (colors != null)
        {
            if (colors.Type() != MatType.CV_8UC1 || colors.Cols != 3 || colors.Rows != Points.Rows)
            {
                throw new ArgumentException("Colors must be Nx3 CV_8UC1 and match Points row count.", nameof(colors));
            }
        }

        if (normals != null)
        {
            if (normals.Type() != MatType.CV_32FC1 || normals.Cols != 3 || normals.Rows != Points.Rows)
            {
                throw new ArgumentException("Normals must be Nx3 CV_32FC1 and match Points row count.", nameof(normals));
            }
        }

        if (isOrganized)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException("Organized point cloud requires positive width/height.");
            }
            if (width * height != Points.Rows)
            {
                throw new ArgumentException($"Organized point cloud requires width*height == point count. width={width} height={height} count={Points.Rows}.");
            }
        }

        IsOrganized = isOrganized;
        Width = isOrganized ? width : 0;
        Height = isOrganized ? height : 0;
    }

    public static PointCloud Load(string path, MatPool? pool = null) => PointCloudIO.Load(path, pool);

    public void Save(string path, bool binary = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required.", nameof(path));
        }

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".pcd")
        {
            PointCloudIO.SavePCD(path, this, binary);
            return;
        }

        if (ext == ".ply")
        {
            if (binary)
            {
                throw new NotSupportedException("Binary PLY is not supported.");
            }
            PointCloudIO.SavePLY(path, this);
            return;
        }

        throw new NotSupportedException($"Unsupported point cloud file extension: {ext}");
    }

    public AxisAlignedBoundingBox GetAABB()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var idx = Points.GetGenericIndexer<float>();
        var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        for (int r = 0; r < Points.Rows; r++)
        {
            var x = idx[r, 0];
            var y = idx[r, 1];
            var z = idx[r, 2];

            if (x < min.X) min.X = x;
            if (y < min.Y) min.Y = y;
            if (z < min.Z) min.Z = z;

            if (x > max.X) max.X = x;
            if (y > max.Y) max.Y = y;
            if (z > max.Z) max.Z = z;
        }

        return new AxisAlignedBoundingBox { Min = min, Max = max };
    }

    public PointCloud Transform(Matrix4x4 transform)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var n = Count;
        var outPoints = _pool.Rent(width: 3, height: n, type: MatType.CV_32FC1);
        var outNormals = Normals != null ? _pool.Rent(width: 3, height: n, type: MatType.CV_32FC1) : null;
        Mat? outColors = null;

        if (Colors != null)
        {
            outColors = _pool.Rent(width: 3, height: n, type: MatType.CV_8UC1);
            Colors.CopyTo(outColors);
        }

        var src = Points.GetGenericIndexer<float>();
        var dst = outPoints.GetGenericIndexer<float>();

        Matrix4x4 normalXform;
        if (!Matrix4x4.Invert(transform, out var inv))
        {
            // Degenerate transform: just rotate normals with identity and keep going.
            normalXform = Matrix4x4.Identity;
        }
        else
        {
            normalXform = Matrix4x4.Transpose(inv);
        }

        var nSrc = Normals?.GetGenericIndexer<float>();
        var nDst = outNormals?.GetGenericIndexer<float>();

        for (int r = 0; r < n; r++)
        {
            var v = new Vector3(src[r, 0], src[r, 1], src[r, 2]);
            var tv4 = Vector4.Transform(new Vector4(v, 1f), transform);
            dst[r, 0] = tv4.X;
            dst[r, 1] = tv4.Y;
            dst[r, 2] = tv4.Z;

            if (nSrc != null && nDst != null)
            {
                var nn = new Vector3(nSrc[r, 0], nSrc[r, 1], nSrc[r, 2]);
                var tn4 = Vector4.Transform(new Vector4(nn, 0f), normalXform);
                var tn = new Vector3(tn4.X, tn4.Y, tn4.Z);
                if (tn.LengthSquared() > 1e-20f)
                {
                    tn = Vector3.Normalize(tn);
                }
                nDst[r, 0] = tn.X;
                nDst[r, 1] = tn.Y;
                nDst[r, 2] = tn.Z;
            }
        }

        return new PointCloud(outPoints, outColors, outNormals, IsOrganized, Width, Height, _pool);
    }

    public PointCloud Crop(AxisAlignedBoundingBox box)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var src = Points.GetGenericIndexer<float>();
        var n = Count;

        var keepIndices = new List<int>(capacity: Math.Min(n, 1024));
        for (int r = 0; r < n; r++)
        {
            var x = src[r, 0];
            var y = src[r, 1];
            var z = src[r, 2];
            if (x >= box.Min.X && x <= box.Max.X &&
                y >= box.Min.Y && y <= box.Max.Y &&
                z >= box.Min.Z && z <= box.Max.Z)
            {
                keepIndices.Add(r);
            }
        }

        int keep = keepIndices.Count;
        Mat outPoints;
        Mat? outColors;
        Mat? outNormals;

        if (keep == 0)
        {
            outPoints = new Mat(rows: 0, cols: 3, type: MatType.CV_32FC1);
            outColors = Colors != null ? new Mat(rows: 0, cols: 3, type: MatType.CV_8UC1) : null;
            outNormals = Normals != null ? new Mat(rows: 0, cols: 3, type: MatType.CV_32FC1) : null;
            return new PointCloud(outPoints, outColors, outNormals, isOrganized: false, pool: _pool);
        }

        outPoints = _pool.Rent(width: 3, height: keep, type: MatType.CV_32FC1);
        outColors = Colors != null ? _pool.Rent(width: 3, height: keep, type: MatType.CV_8UC1) : null;
        outNormals = Normals != null ? _pool.Rent(width: 3, height: keep, type: MatType.CV_32FC1) : null;

        var dst = outPoints.GetGenericIndexer<float>();
        var cSrc = Colors?.GetGenericIndexer<byte>();
        var cDst = outColors?.GetGenericIndexer<byte>();
        var nSrc = Normals?.GetGenericIndexer<float>();
        var nDst = outNormals?.GetGenericIndexer<float>();

        for (int w = 0; w < keep; w++)
        {
            int r = keepIndices[w];
            dst[w, 0] = src[r, 0];
            dst[w, 1] = src[r, 1];
            dst[w, 2] = src[r, 2];

            if (cSrc != null && cDst != null)
            {
                cDst[w, 0] = cSrc[r, 0];
                cDst[w, 1] = cSrc[r, 1];
                cDst[w, 2] = cSrc[r, 2];
            }

            if (nSrc != null && nDst != null)
            {
                nDst[w, 0] = nSrc[r, 0];
                nDst[w, 1] = nSrc[r, 1];
                nDst[w, 2] = nSrc[r, 2];
            }
        }

        return new PointCloud(outPoints, outColors, outNormals, isOrganized: false, pool: _pool);
    }

    public static PointCloud FromDepthMap(Mat depth, Mat cameraMatrix, MatPool? pool = null)
    {
        if (depth == null) throw new ArgumentNullException(nameof(depth));
        if (cameraMatrix == null) throw new ArgumentNullException(nameof(cameraMatrix));
        if (depth.Empty()) throw new ArgumentException("Depth is empty.", nameof(depth));
        if (cameraMatrix.Rows != 3 || cameraMatrix.Cols != 3) throw new ArgumentException("Camera matrix must be 3x3.", nameof(cameraMatrix));

        var p = pool ?? MatPool.Shared;
        int width = depth.Cols;
        int height = depth.Rows;
        int n = width * height;

        // Expect depth in meters (float32). Support uint16 millimeters as a convenience.
        Mat depthF;
        bool ownsDepthF = false;
        if (depth.Type() == MatType.CV_32FC1)
        {
            depthF = depth;
        }
        else if (depth.Type() == MatType.CV_16UC1)
        {
            depthF = new Mat();
            depth.ConvertTo(depthF, MatType.CV_32FC1, 1.0 / 1000.0);
            ownsDepthF = true;
        }
        else
        {
            throw new ArgumentException("Depth must be CV_32FC1 (meters) or CV_16UC1 (millimeters).", nameof(depth));
        }

        try
        {
            var points = p.Rent(width: 3, height: n, type: MatType.CV_32FC1);

            double fx = cameraMatrix.At<double>(0, 0);
            double fy = cameraMatrix.At<double>(1, 1);
            double cx = cameraMatrix.At<double>(0, 2);
            double cy = cameraMatrix.At<double>(1, 2);

            // Support float camera matrices too.
            if (fx == 0 && cameraMatrix.Type() == MatType.CV_32FC1)
            {
                fx = cameraMatrix.At<float>(0, 0);
                fy = cameraMatrix.At<float>(1, 1);
                cx = cameraMatrix.At<float>(0, 2);
                cy = cameraMatrix.At<float>(1, 2);
            }

            var dIdx = depthF.GetGenericIndexer<float>();
            var pIdx = points.GetGenericIndexer<float>();

            int row = 0;
            for (int v = 0; v < height; v++)
            {
                for (int u = 0; u < width; u++)
                {
                    var z = dIdx[v, u];
                    var x = (float)((u - cx) * z / fx);
                    var y = (float)((v - cy) * z / fy);
                    pIdx[row, 0] = x;
                    pIdx[row, 1] = y;
                    pIdx[row, 2] = z;
                    row++;
                }
            }

            return new PointCloud(points, colors: null, normals: null, isOrganized: true, width: width, height: height, pool: p);
        }
        finally
        {
            if (ownsDepthF)
            {
                depthF.Dispose();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        ReturnMat(Points);
        if (Colors != null) ReturnMat(Colors);
        if (Normals != null) ReturnMat(Normals);

        GC.SuppressFinalize(this);
    }

    private void ReturnMat(Mat mat)
    {
        if (mat.IsDisposed)
        {
            return;
        }

        // Don't pool empty mats.
        if (mat.Rows <= 0 || mat.Cols <= 0)
        {
            mat.Dispose();
            return;
        }

        _pool.Return(mat);
    }
}
