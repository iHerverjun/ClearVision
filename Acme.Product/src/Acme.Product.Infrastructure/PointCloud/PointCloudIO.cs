using System.Globalization;
using System.Numerics;
using System.Text;
using Acme.Product.Infrastructure.Memory;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.PointCloud;

public static class PointCloudIO
{
    public static PointCloud Load(string path, MatPool? pool = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required.", nameof(path));
        }

        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".pcd" => LoadPCD(path, pool),
            ".ply" => LoadPLY(path, pool),
            _ => throw new NotSupportedException($"Unsupported point cloud file extension: {ext}")
        };
    }

    public static PointCloud LoadPCD(string path, MatPool? pool = null)
    {
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        if (lines.Length == 0 || !lines[0].StartsWith("# .PCD", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Not a PCD file or unsupported header.");
        }

        var fields = Array.Empty<string>();
        var types = Array.Empty<string>();
        var sizes = Array.Empty<int>();
        var counts = Array.Empty<int>();
        int width = 0;
        int height = 1;
        int points = 0;
        string dataMode = string.Empty;
        int dataStart = -1;

        for (int i = 0; i < lines.Length; i++)
        {
            var raw = lines[i].Trim();
            if (raw.Length == 0 || raw.StartsWith("#", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            var key = parts[0].ToUpperInvariant();
            if (key == "FIELDS")
            {
                fields = parts.Skip(1).ToArray();
            }
            else if (key == "TYPE")
            {
                types = parts.Skip(1).ToArray();
            }
            else if (key == "SIZE")
            {
                sizes = parts.Skip(1).Select(int.Parse).ToArray();
            }
            else if (key == "COUNT")
            {
                counts = parts.Skip(1).Select(int.Parse).ToArray();
            }
            else if (key == "WIDTH")
            {
                width = int.Parse(parts[1], CultureInfo.InvariantCulture);
            }
            else if (key == "HEIGHT")
            {
                height = int.Parse(parts[1], CultureInfo.InvariantCulture);
            }
            else if (key == "POINTS")
            {
                points = int.Parse(parts[1], CultureInfo.InvariantCulture);
            }
            else if (key == "DATA")
            {
                dataMode = parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty;
                dataStart = i + 1;
                break;
            }
        }

        if (dataStart < 0 || !dataMode.Equals("ascii", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Only ASCII PCD is supported.");
        }

        if (fields.Length == 0 || types.Length == 0 || sizes.Length == 0)
        {
            throw new InvalidOperationException("PCD header is missing required descriptors (FIELDS/TYPE/SIZE).");
        }

        if (counts.Length == 0)
        {
            counts = Enumerable.Repeat(1, fields.Length).ToArray();
        }

        if (points <= 0)
        {
            points = width * height;
        }

        int fx = IndexOf(fields, "x");
        int fy = IndexOf(fields, "y");
        int fz = IndexOf(fields, "z");
        if (fx < 0 || fy < 0 || fz < 0)
        {
            throw new InvalidOperationException("PCD must contain x/y/z fields.");
        }

        int frgb = IndexOf(fields, "rgb");
        int fnx = IndexOf(fields, "normal_x");
        int fny = IndexOf(fields, "normal_y");
        int fnz = IndexOf(fields, "normal_z");

        var p = pool ?? MatPool.Shared;
        var pts = new Mat(points, 3, MatType.CV_32FC1);
        Mat? cols = frgb >= 0 ? new Mat(points, 3, MatType.CV_8UC1) : null;
        Mat? nrm = (fnx >= 0 && fny >= 0 && fnz >= 0) ? new Mat(points, 3, MatType.CV_32FC1) : null;

        var ptsIdx = pts.GetGenericIndexer<float>();
        var colIdx = cols?.GetGenericIndexer<byte>();
        var nrmIdx = nrm?.GetGenericIndexer<float>();

        var culture = CultureInfo.InvariantCulture;

        int row = 0;
        for (int i = dataStart; i < lines.Length && row < points; i++)
        {
            var raw = lines[i].Trim();
            if (raw.Length == 0) continue;

            var values = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (values.Length < fields.Length)
            {
                continue;
            }

            ptsIdx[row, 0] = (float)double.Parse(values[fx], culture);
            ptsIdx[row, 1] = (float)double.Parse(values[fy], culture);
            ptsIdx[row, 2] = (float)double.Parse(values[fz], culture);

            if (frgb >= 0 && colIdx != null)
            {
                var t = types[frgb].ToUpperInvariant();
                uint packed;
                if (t == "U" || t == "I")
                {
                    packed = uint.Parse(values[frgb], culture);
                }
                else if (t == "F")
                {
                    var f = float.Parse(values[frgb], culture);
                    packed = unchecked((uint)BitConverter.SingleToInt32Bits(f));
                }
                else
                {
                    packed = 0;
                }

                colIdx[row, 0] = (byte)(packed & 0xFF);
                colIdx[row, 1] = (byte)((packed >> 8) & 0xFF);
                colIdx[row, 2] = (byte)((packed >> 16) & 0xFF);
            }

            if (nrmIdx != null)
            {
                nrmIdx[row, 0] = (float)double.Parse(values[fnx], culture);
                nrmIdx[row, 1] = (float)double.Parse(values[fny], culture);
                nrmIdx[row, 2] = (float)double.Parse(values[fnz], culture);
            }

            row++;
        }

        if (row != points)
        {
            // Compact to actual rows parsed.
            using var slice = pts.RowRange(0, row);
            var compactPts = slice.Clone();
            pts.Dispose();
            pts = compactPts;

            if (cols != null)
            {
                using var cSlice = cols.RowRange(0, row);
                var compactCols = cSlice.Clone();
                cols.Dispose();
                cols = compactCols;
            }

            if (nrm != null)
            {
                using var nSlice = nrm.RowRange(0, row);
                var compactN = nSlice.Clone();
                nrm.Dispose();
                nrm = compactN;
            }
        }

        return new PointCloud(pts, cols, nrm, isOrganized: height > 1, width: width, height: height, pool: p);
    }

    public static void SavePCD(string path, PointCloud cloud, bool binary = false)
    {
        if (binary)
        {
            throw new NotSupportedException("Binary PCD is not supported yet.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

        var hasColor = cloud.Colors != null;
        var hasNormals = cloud.Normals != null;

        var fields = new List<string> { "x", "y", "z" };
        var sizes = new List<int> { 4, 4, 4 };
        var types = new List<string> { "F", "F", "F" };
        var counts = new List<int> { 1, 1, 1 };

        if (hasColor)
        {
            fields.Add("rgb");
            sizes.Add(4);
            types.Add("U");
            counts.Add(1);
        }

        if (hasNormals)
        {
            fields.AddRange(["normal_x", "normal_y", "normal_z"]);
            sizes.AddRange([4, 4, 4]);
            types.AddRange(["F", "F", "F"]);
            counts.AddRange([1, 1, 1]);
        }

        int width = cloud.IsOrganized ? cloud.Width : cloud.Count;
        int height = cloud.IsOrganized ? cloud.Height : 1;

        var sb = new StringBuilder(capacity: cloud.Count * 48);
        sb.AppendLine("# .PCD v0.7 - Point Cloud Data file format");
        sb.AppendLine("VERSION 0.7");
        sb.AppendLine($"FIELDS {string.Join(' ', fields)}");
        sb.AppendLine($"SIZE {string.Join(' ', sizes)}");
        sb.AppendLine($"TYPE {string.Join(' ', types)}");
        sb.AppendLine($"COUNT {string.Join(' ', counts)}");
        sb.AppendLine($"WIDTH {width}");
        sb.AppendLine($"HEIGHT {height}");
        sb.AppendLine("VIEWPOINT 0 0 0 1 0 0 0");
        sb.AppendLine($"POINTS {cloud.Count}");
        sb.AppendLine("DATA ascii");

        var culture = CultureInfo.InvariantCulture;
        var pIdx = cloud.Points.GetGenericIndexer<float>();
        var cIdx = cloud.Colors?.GetGenericIndexer<byte>();
        var nIdx = cloud.Normals?.GetGenericIndexer<float>();

        for (int r = 0; r < cloud.Count; r++)
        {
            sb.Append(pIdx[r, 0].ToString("R", culture));
            sb.Append(' ');
            sb.Append(pIdx[r, 1].ToString("R", culture));
            sb.Append(' ');
            sb.Append(pIdx[r, 2].ToString("R", culture));

            if (cIdx != null)
            {
                uint packed = (uint)(cIdx[r, 0] | (cIdx[r, 1] << 8) | (cIdx[r, 2] << 16));
                sb.Append(' ');
                sb.Append(packed.ToString(culture));
            }

            if (nIdx != null)
            {
                sb.Append(' ');
                sb.Append(nIdx[r, 0].ToString("R", culture));
                sb.Append(' ');
                sb.Append(nIdx[r, 1].ToString("R", culture));
                sb.Append(' ');
                sb.Append(nIdx[r, 2].ToString("R", culture));
            }

            sb.AppendLine();
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static PointCloud LoadPLY(string path, MatPool? pool = null)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var first = reader.ReadLine();
        if (!string.Equals(first?.Trim(), "ply", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Not a PLY file.");
        }

        int vertexCount = 0;
        var properties = new List<string>();
        bool inHeader = true;

        while (inHeader)
        {
            var line = reader.ReadLine();
            if (line == null)
            {
                throw new InvalidOperationException("Unexpected end of PLY header.");
            }

            line = line.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("comment", StringComparison.OrdinalIgnoreCase)) continue;

            if (line.StartsWith("format", StringComparison.OrdinalIgnoreCase))
            {
                if (!line.Contains("ascii", StringComparison.OrdinalIgnoreCase))
                {
                    throw new NotSupportedException("Only ASCII PLY is supported.");
                }
                continue;
            }

            if (line.StartsWith("element", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 && parts[1].Equals("vertex", StringComparison.OrdinalIgnoreCase))
                {
                    vertexCount = int.Parse(parts[2], CultureInfo.InvariantCulture);
                }
                continue;
            }

            if (line.StartsWith("property", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    properties.Add(parts[^1]);
                }
                continue;
            }

            if (line.Equals("end_header", StringComparison.OrdinalIgnoreCase))
            {
                inHeader = false;
                break;
            }
        }

        if (vertexCount <= 0)
        {
            throw new InvalidOperationException("PLY vertex count missing or invalid.");
        }

        int ix = IndexOf(properties, "x");
        int iy = IndexOf(properties, "y");
        int iz = IndexOf(properties, "z");
        if (ix < 0 || iy < 0 || iz < 0)
        {
            throw new InvalidOperationException("PLY must contain x/y/z properties.");
        }

        int ir = IndexOf(properties, "red");
        int ig = IndexOf(properties, "green");
        int ib = IndexOf(properties, "blue");
        int inx = IndexOf(properties, "nx");
        int iny = IndexOf(properties, "ny");
        int inz = IndexOf(properties, "nz");

        var p = pool ?? MatPool.Shared;
        var pts = new Mat(vertexCount, 3, MatType.CV_32FC1);
        Mat? cols = (ir >= 0 && ig >= 0 && ib >= 0) ? new Mat(vertexCount, 3, MatType.CV_8UC1) : null;
        Mat? nrm = (inx >= 0 && iny >= 0 && inz >= 0) ? new Mat(vertexCount, 3, MatType.CV_32FC1) : null;

        var ptsIdx = pts.GetGenericIndexer<float>();
        var colIdx = cols?.GetGenericIndexer<byte>();
        var nrmIdx = nrm?.GetGenericIndexer<float>();

        var culture = CultureInfo.InvariantCulture;

        int row = 0;
        while (row < vertexCount)
        {
            var line = reader.ReadLine();
            if (line == null) break;
            line = line.Trim();
            if (line.Length == 0) continue;

            var values = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (values.Length < properties.Count) continue;

            ptsIdx[row, 0] = (float)double.Parse(values[ix], culture);
            ptsIdx[row, 1] = (float)double.Parse(values[iy], culture);
            ptsIdx[row, 2] = (float)double.Parse(values[iz], culture);

            if (colIdx != null)
            {
                colIdx[row, 0] = byte.Parse(values[ir], culture);
                colIdx[row, 1] = byte.Parse(values[ig], culture);
                colIdx[row, 2] = byte.Parse(values[ib], culture);
            }

            if (nrmIdx != null)
            {
                nrmIdx[row, 0] = (float)double.Parse(values[inx], culture);
                nrmIdx[row, 1] = (float)double.Parse(values[iny], culture);
                nrmIdx[row, 2] = (float)double.Parse(values[inz], culture);
            }

            row++;
        }

        if (row != vertexCount)
        {
            using var slice = pts.RowRange(0, row);
            var compactPts = slice.Clone();
            pts.Dispose();
            pts = compactPts;

            if (cols != null)
            {
                using var cSlice = cols.RowRange(0, row);
                var compactCols = cSlice.Clone();
                cols.Dispose();
                cols = compactCols;
            }

            if (nrm != null)
            {
                using var nSlice = nrm.RowRange(0, row);
                var compactN = nSlice.Clone();
                nrm.Dispose();
                nrm = compactN;
            }
        }

        return new PointCloud(pts, cols, nrm, isOrganized: false, pool: p);
    }

    public static void SavePLY(string path, PointCloud cloud)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

        var hasColor = cloud.Colors != null;
        var hasNormals = cloud.Normals != null;

        var sb = new StringBuilder(capacity: cloud.Count * 64);
        sb.AppendLine("ply");
        sb.AppendLine("format ascii 1.0");
        sb.AppendLine($"element vertex {cloud.Count}");
        sb.AppendLine("property float x");
        sb.AppendLine("property float y");
        sb.AppendLine("property float z");

        if (hasNormals)
        {
            sb.AppendLine("property float nx");
            sb.AppendLine("property float ny");
            sb.AppendLine("property float nz");
        }

        if (hasColor)
        {
            sb.AppendLine("property uchar red");
            sb.AppendLine("property uchar green");
            sb.AppendLine("property uchar blue");
        }

        sb.AppendLine("end_header");

        var culture = CultureInfo.InvariantCulture;
        var pIdx = cloud.Points.GetGenericIndexer<float>();
        var cIdx = cloud.Colors?.GetGenericIndexer<byte>();
        var nIdx = cloud.Normals?.GetGenericIndexer<float>();

        for (int r = 0; r < cloud.Count; r++)
        {
            sb.Append(pIdx[r, 0].ToString("R", culture));
            sb.Append(' ');
            sb.Append(pIdx[r, 1].ToString("R", culture));
            sb.Append(' ');
            sb.Append(pIdx[r, 2].ToString("R", culture));

            if (nIdx != null)
            {
                sb.Append(' ');
                sb.Append(nIdx[r, 0].ToString("R", culture));
                sb.Append(' ');
                sb.Append(nIdx[r, 1].ToString("R", culture));
                sb.Append(' ');
                sb.Append(nIdx[r, 2].ToString("R", culture));
            }

            if (cIdx != null)
            {
                sb.Append(' ');
                sb.Append(cIdx[r, 0].ToString(culture));
                sb.Append(' ');
                sb.Append(cIdx[r, 1].ToString(culture));
                sb.Append(' ');
                sb.Append(cIdx[r, 2].ToString(culture));
            }

            sb.AppendLine();
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static int IndexOf(IReadOnlyList<string> names, string target)
    {
        for (int i = 0; i < names.Count; i++)
        {
            if (names[i].Equals(target, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }
}

