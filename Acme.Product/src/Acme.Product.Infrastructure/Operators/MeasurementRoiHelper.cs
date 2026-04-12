using Acme.Product.Core.Entities;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

internal static class MeasurementRoiHelper
{
    public static Rect ResolveRoi(Operator @operator, int width, int height)
    {
        var x = ReadIntParameter(@operator, "RoiX", 0);
        var y = ReadIntParameter(@operator, "RoiY", 0);
        var w = ReadIntParameter(@operator, "RoiW", 0);
        var h = ReadIntParameter(@operator, "RoiH", 0);

        x = Math.Clamp(x, 0, Math.Max(0, width - 1));
        y = Math.Clamp(y, 0, Math.Max(0, height - 1));

        if (w <= 0)
        {
            w = width - x;
        }

        if (h <= 0)
        {
            h = height - y;
        }

        w = Math.Clamp(w, 1, Math.Max(1, width - x));
        h = Math.Clamp(h, 1, Math.Max(1, height - y));
        return new Rect(x, y, w, h);
    }

    public static int ReadIntParameter(Operator @operator, string name, int fallback)
    {
        var raw = @operator.Parameters.FirstOrDefault(parameter => parameter.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;
        if (raw == null)
        {
            return fallback;
        }

        return int.TryParse(raw.ToString(), out var value) ? value : fallback;
    }
}
