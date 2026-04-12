using OpenCvSharp;

namespace Acme.Product.Infrastructure.Calibration;

public static class CalibrationBundleV2Helpers
{
    public static CalibrationQualityV2 CreateAcceptedQuality(
        double meanError,
        double maxError,
        int inlierCount,
        int totalSampleCount,
        IEnumerable<string>? diagnostics = null)
    {
        return new CalibrationQualityV2
        {
            Accepted = true,
            MeanError = meanError,
            MaxError = maxError,
            InlierCount = inlierCount,
            TotalSampleCount = totalSampleCount,
            Diagnostics = diagnostics?.ToList() ?? new List<string>()
        };
    }

    public static CalibrationQualityV2 CreatePreviewQuality(
        IEnumerable<string>? diagnostics = null,
        double meanError = 0,
        double maxError = 0,
        int sampleCount = 0)
    {
        return new CalibrationQualityV2
        {
            Accepted = false,
            MeanError = meanError,
            MaxError = maxError,
            InlierCount = sampleCount,
            TotalSampleCount = sampleCount,
            Diagnostics = diagnostics?.ToList() ?? new List<string>()
        };
    }

    public static double[][] ToJaggedMatrix(Mat matrix)
    {
        if (matrix.Empty())
        {
            return Array.Empty<double[]>();
        }

        var result = new double[matrix.Rows][];
        for (var r = 0; r < matrix.Rows; r++)
        {
            result[r] = new double[matrix.Cols];
            for (var c = 0; c < matrix.Cols; c++)
            {
                result[r][c] = matrix.At<double>(r, c);
            }
        }

        return result;
    }

    public static double[] ToFlatVector(Mat matrix)
    {
        if (matrix.Empty())
        {
            return Array.Empty<double>();
        }

        var values = new double[matrix.Rows * matrix.Cols];
        var index = 0;
        for (var r = 0; r < matrix.Rows; r++)
        {
            for (var c = 0; c < matrix.Cols; c++)
            {
                values[index++] = matrix.At<double>(r, c);
            }
        }

        return values;
    }

    public static Mat ToMat(double[][] matrix)
    {
        if (matrix.Length == 0)
        {
            return new Mat();
        }

        var rows = matrix.Length;
        var cols = matrix[0].Length;
        var mat = new Mat(rows, cols, MatType.CV_64FC1);
        for (var r = 0; r < rows; r++)
        {
            if (matrix[r].Length != cols)
            {
                throw new InvalidOperationException("Matrix rows must have a consistent column count.");
            }

            for (var c = 0; c < cols; c++)
            {
                mat.Set(r, c, matrix[r][c]);
            }
        }

        return mat;
    }

    public static Mat ToColumnVector(double[] values)
    {
        if (values.Length == 0)
        {
            return new Mat();
        }

        var mat = new Mat(values.Length, 1, MatType.CV_64FC1);
        for (var i = 0; i < values.Length; i++)
        {
            mat.Set(i, 0, values[i]);
        }

        return mat;
    }

    public static bool IsFiniteMatrix(double[][] matrix)
    {
        foreach (var row in matrix)
        {
            foreach (var value in row)
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static bool IsFiniteVector(double[] values)
    {
        foreach (var value in values)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return false;
            }
        }

        return true;
    }
}
