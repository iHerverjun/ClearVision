using System.Collections;
using System.Globalization;
using System.Text.Json;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Calibration;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Pixel To World Transform",
    Description = "Transforms coordinates via CalibrationBundleV2 using either Transform2D or camera ray-plane intersection.",
    Category = "Calibration",
    IconName = "coordinate-transform",
    Keywords = new[] { "pixel", "world", "coordinate", "transform", "calibration", "ray-plane" }
)]
[InputPort("Image", "Input Image (Optional)", PortDataType.Image, IsRequired = false)]
[InputPort("Points", "Input Points", PortDataType.PointList, IsRequired = false)]
[InputPort("CalibrationData", "Calibration Bundle V2 JSON", PortDataType.String, IsRequired = false)]
[OutputPort("Image", "Visualization Image", PortDataType.Image)]
[OutputPort("TransformedPoints", "Transformed Points", PortDataType.PointList)]
[OutputPort("TransformResult", "Transform Result Details", PortDataType.Any)]
[OperatorParam("TransformMode", "Transform Mode", "enum", DefaultValue = "PixelToWorld", Options = new[] { "PixelToWorld|Pixel to World", "WorldToPixel|World to Pixel" })]
[OperatorParam("WorldPlaneZ", "World Plane Z (mm)", "double", DefaultValue = 0.0)]
[OperatorParam("UnitScale", "Unit Scale (mm per unit)", "double", DefaultValue = 1.0, Min = 0.0001, Max = 10000.0)]
[OperatorParam("InputPointX", "Input Point X (Single Point Mode)", "double", DefaultValue = 0.0)]
[OperatorParam("InputPointY", "Input Point Y (Single Point Mode)", "double", DefaultValue = 0.0)]
[OperatorParam("UseDistortion", "Use Distortion Model", "bool", DefaultValue = true)]
[OperatorParam("GenerateReport", "Generate Accuracy Report", "bool", DefaultValue = true)]
public class PixelToWorldTransformOperator : OperatorBase
{
    private const double Epsilon = 1e-12;

    public override OperatorType OperatorType => OperatorType.PixelToWorldTransform;

    public PixelToWorldTransformOperator(ILogger<PixelToWorldTransformOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var transformMode = GetStringParam(@operator, "TransformMode", "PixelToWorld");
        var isPixelToWorld = transformMode.Equals("PixelToWorld", StringComparison.OrdinalIgnoreCase);
        var isWorldToPixel = transformMode.Equals("WorldToPixel", StringComparison.OrdinalIgnoreCase);
        if (!isPixelToWorld && !isWorldToPixel)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("TransformMode must be PixelToWorld or WorldToPixel."));
        }

        var worldPlaneZ = GetDoubleParam(@operator, "WorldPlaneZ", 0.0);
        var unitScale = GetDoubleParam(@operator, "UnitScale", 1.0);
        var useDistortion = GetBoolParam(@operator, "UseDistortion", true);
        var generateReport = GetBoolParam(@operator, "GenerateReport", true);

        if (!TryResolveCalibrationData(@operator, inputs, out var calibrationJson))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("CalibrationBundleV2 data is required."));
        }

        if (!CalibrationBundleV2Json.TryDeserialize(calibrationJson!, out var bundle, out var parseError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"Invalid CalibrationBundleV2: {parseError}"));
        }

        if (!CalibrationBundleV2Json.TryRequireAccepted(bundle, out var acceptedError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(acceptedError));
        }

        var inputPoints = GetInputPoints(@operator, inputs);
        if (inputPoints.Count == 0)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("No input points provided."));
        }

        if (bundle.Transform2D != null)
        {
            return Task.FromResult(ExecutePlanarPath(
                inputs,
                bundle,
                inputPoints,
                isPixelToWorld,
                worldPlaneZ,
                unitScale,
                generateReport));
        }

        return Task.FromResult(ExecuteRayPlanePath(
            inputs,
            bundle,
            inputPoints,
            isPixelToWorld,
            worldPlaneZ,
            unitScale,
            useDistortion,
            generateReport));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var unitScale = GetDoubleParam(@operator, "UnitScale", 1.0);
        if (unitScale <= 0 || !double.IsFinite(unitScale))
        {
            return ValidationResult.Invalid("UnitScale must be a positive finite number.");
        }

        var worldPlaneZ = GetDoubleParam(@operator, "WorldPlaneZ", 0.0);
        if (!double.IsFinite(worldPlaneZ))
        {
            return ValidationResult.Invalid("WorldPlaneZ must be finite.");
        }

        return ValidationResult.Valid();
    }

    private OperatorExecutionOutput ExecutePlanarPath(
        Dictionary<string, object>? inputs,
        CalibrationBundleV2 bundle,
        IReadOnlyList<Point3d> inputPoints,
        bool isPixelToWorld,
        double worldPlaneZ,
        double unitScale,
        bool generateReport)
    {
        if (!IsSupportedPlanarKind(bundle.CalibrationKind))
        {
            return OperatorExecutionOutput.Failure(
                $"Planar path requires CalibrationKind PlanarTransform2D/RigidTransform2D, got {bundle.CalibrationKind}.");
        }

        if (!CalibrationPlanarTransformRuntime.TryCreate(
                bundle,
                new[] { TransformModelV2.ScaleOffset, TransformModelV2.Similarity, TransformModelV2.Affine, TransformModelV2.Homography },
                out var runtime,
                out var runtimeError))
        {
            return OperatorExecutionOutput.Failure(runtimeError);
        }

        var outputPoints = new List<Point3d>(inputPoints.Count);
        foreach (var point in inputPoints)
        {
            if (isPixelToWorld)
            {
                if (!runtime.TryApplyForward(point.X, point.Y, out var worldXmm, out var worldYmm, out var applyError))
                {
                    return OperatorExecutionOutput.Failure($"Planar forward transform failed: {applyError}");
                }

                outputPoints.Add(new Point3d(worldXmm / unitScale, worldYmm / unitScale, worldPlaneZ / unitScale));
            }
            else
            {
                var worldXmm = point.X * unitScale;
                var worldYmm = point.Y * unitScale;
                if (!runtime.TryApplyInverse(worldXmm, worldYmm, out var pixelX, out var pixelY, out var applyError))
                {
                    return OperatorExecutionOutput.Failure($"Planar inverse transform failed: {applyError}");
                }

                outputPoints.Add(new Point3d(pixelX, pixelY, 0));
            }
        }

        return BuildSuccessOutput(
            inputs,
            inputPoints,
            outputPoints,
            isPixelToWorld ? "PixelToWorld" : "WorldToPixel",
            "PlanarTransform2D",
            runtime.Model.ToString(),
            bundle,
            worldPlaneZ,
            unitScale,
            generateReport,
            additionalDiagnostics: null);
    }

    private OperatorExecutionOutput ExecuteRayPlanePath(
        Dictionary<string, object>? inputs,
        CalibrationBundleV2 bundle,
        IReadOnlyList<Point3d> inputPoints,
        bool isPixelToWorld,
        double worldPlaneZ,
        double unitScale,
        bool useDistortion,
        bool generateReport)
    {
        if (!TryCreateRayPlaneContext(bundle, out var context, out var contextError))
        {
            return OperatorExecutionOutput.Failure(contextError);
        }

        var diagnostics = new List<string>();
        if (useDistortion && bundle.Distortion is { Coefficients.Length: > 0 })
        {
            diagnostics.Add("Distortion coefficients were provided; ray-plane path currently uses pinhole projection.");
        }

        var outputPoints = new List<Point3d>(inputPoints.Count);
        foreach (var point in inputPoints)
        {
            if (isPixelToWorld)
            {
                if (!TryPixelToWorldByRayPlane(context, point.X, point.Y, worldPlaneZ, out var worldPointMm, out var error))
                {
                    return OperatorExecutionOutput.Failure($"Ray-plane PixelToWorld failed: {error}");
                }

                outputPoints.Add(new Point3d(
                    worldPointMm.X / unitScale,
                    worldPointMm.Y / unitScale,
                    worldPointMm.Z / unitScale));
            }
            else
            {
                var hasExplicitZ = Math.Abs(point.Z) > Epsilon;
                var worldZmm = hasExplicitZ ? point.Z * unitScale : worldPlaneZ;
                var worldPointMm = new Point3d(point.X * unitScale, point.Y * unitScale, worldZmm);
                if (!TryWorldToPixelByProjection(context, worldPointMm, out var pixelPoint, out var error))
                {
                    return OperatorExecutionOutput.Failure($"Ray-plane WorldToPixel failed: {error}");
                }

                outputPoints.Add(new Point3d(pixelPoint.X, pixelPoint.Y, 0));
            }
        }

        return BuildSuccessOutput(
            inputs,
            inputPoints,
            outputPoints,
            isPixelToWorld ? "PixelToWorld" : "WorldToPixel",
            "RayPlaneIntersection",
            "Projection",
            bundle,
            worldPlaneZ,
            unitScale,
            generateReport,
            diagnostics);
    }

    private OperatorExecutionOutput BuildSuccessOutput(
        Dictionary<string, object>? inputs,
        IReadOnlyList<Point3d> inputPoints,
        IReadOnlyList<Point3d> outputPoints,
        string transformMode,
        string path,
        string model,
        CalibrationBundleV2 bundle,
        double worldPlaneZ,
        double unitScale,
        bool generateReport,
        IReadOnlyList<string>? additionalDiagnostics)
    {
        var transformedPositions = outputPoints.Select(p => new Position(p.X, p.Y)).ToList();
        var resultData = new Dictionary<string, object>
        {
            ["TransformedPoints"] = transformedPositions,
            ["TransformResult"] = new Dictionary<string, object>
            {
                ["TransformMode"] = transformMode,
                ["Path"] = path,
                ["Model"] = model,
                ["InputCount"] = inputPoints.Count,
                ["OutputCount"] = outputPoints.Count,
                ["WorldPlaneZ"] = worldPlaneZ,
                ["UnitScale"] = unitScale,
                ["CalibrationKind"] = bundle.CalibrationKind.ToString(),
                ["SourceFrame"] = bundle.SourceFrame,
                ["TargetFrame"] = bundle.TargetFrame
            }
        };

        if (generateReport)
        {
            resultData["AccuracyReport"] = new Dictionary<string, object>
            {
                ["InputPoints"] = inputPoints.Select(p => new { p.X, p.Y, p.Z }).ToList(),
                ["OutputPoints"] = outputPoints.Select(p => new { p.X, p.Y, p.Z }).ToList(),
                ["Diagnostics"] = additionalDiagnostics?.ToList() ?? new List<string>(),
                ["TimestampUtc"] = DateTime.UtcNow
            };
        }

        Mat visualization;
        if (TryGetInputImage(inputs, "Image", out var imageWrapper) && imageWrapper != null)
        {
            var image = imageWrapper.GetMat();
            if (image.Empty())
            {
                return OperatorExecutionOutput.Failure("Input image is invalid.");
            }

            visualization = DrawVisualization(image, inputPoints, outputPoints, transformMode);
        }
        else
        {
            visualization = DrawVisualization(new Mat(480, 640, MatType.CV_8UC3, Scalar.Black), inputPoints, outputPoints, transformMode);
        }

        return OperatorExecutionOutput.Success(CreateImageOutput(visualization, resultData));
    }

    private Mat DrawVisualization(
        Mat source,
        IReadOnlyList<Point3d> inputPoints,
        IReadOnlyList<Point3d> outputPoints,
        string transformMode)
    {
        var result = source.Clone();
        for (var i = 0; i < inputPoints.Count && i < outputPoints.Count; i++)
        {
            var x = (int)Math.Round(inputPoints[i].X);
            var y = (int)Math.Round(inputPoints[i].Y);
            Cv2.Circle(result, new Point(x, y), 4, new Scalar(0, 0, 255), -1);
            var label = transformMode.Equals("PixelToWorld", StringComparison.OrdinalIgnoreCase)
                ? $"W({outputPoints[i].X:F2},{outputPoints[i].Y:F2})"
                : $"P({outputPoints[i].X:F1},{outputPoints[i].Y:F1})";
            Cv2.PutText(
                result,
                label,
                new Point(x + 6, y - 6),
                HersheyFonts.HersheySimplex,
                0.4,
                new Scalar(0, 255, 0),
                1);
        }

        Cv2.PutText(
            result,
            $"Mode: {transformMode}",
            new Point(10, 25),
            HersheyFonts.HersheySimplex,
            0.6,
            new Scalar(255, 255, 0),
            2);
        return result;
    }

    private static bool IsSupportedPlanarKind(CalibrationKindV2 kind)
    {
        return kind == CalibrationKindV2.PlanarTransform2D || kind == CalibrationKindV2.RigidTransform2D;
    }

    private bool TryResolveCalibrationData(Operator @operator, Dictionary<string, object>? inputs, out string? calibrationData)
    {
        calibrationData = null;
        if (inputs != null &&
            inputs.TryGetValue("CalibrationData", out var dataObj) &&
            dataObj is string inlineData &&
            !string.IsNullOrWhiteSpace(inlineData))
        {
            calibrationData = inlineData;
            return true;
        }

        var inlineParameterData = GetStringParam(@operator, "CalibrationData", string.Empty);
        if (!string.IsNullOrWhiteSpace(inlineParameterData))
        {
            calibrationData = inlineParameterData;
            return true;
        }

        return false;
    }

    private List<Point3d> GetInputPoints(Operator @operator, Dictionary<string, object>? inputs)
    {
        var points = new List<Point3d>();
        if (inputs != null && inputs.TryGetValue("Points", out var rawPoints) && rawPoints != null)
        {
            TryAppendInputPoints(rawPoints, points);
        }

        if (points.Count == 0)
        {
            var x = GetDoubleParam(@operator, "InputPointX", 0.0);
            var y = GetDoubleParam(@operator, "InputPointY", 0.0);
            points.Add(new Point3d(x, y, 0));
        }

        return points;
    }

    private static void TryAppendInputPoints(object rawPoints, ICollection<Point3d> output)
    {
        if (rawPoints is IEnumerable<Position> positions)
        {
            foreach (var position in positions)
            {
                output.Add(new Point3d(position.X, position.Y, 0));
            }

            return;
        }

        if (rawPoints is IEnumerable<Point2f> point2Fs)
        {
            foreach (var point in point2Fs)
            {
                output.Add(new Point3d(point.X, point.Y, 0));
            }

            return;
        }

        if (rawPoints is IEnumerable<Point3f> point3Fs)
        {
            foreach (var point in point3Fs)
            {
                output.Add(new Point3d(point.X, point.Y, point.Z));
            }

            return;
        }

        if (rawPoints is IEnumerable<Point3d> point3Ds)
        {
            foreach (var point in point3Ds)
            {
                output.Add(point);
            }

            return;
        }

        if (rawPoints is string json && !string.IsNullOrWhiteSpace(json))
        {
            TryAppendPointsFromJson(json, output);
            return;
        }

        if (rawPoints is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item is Position pos)
                {
                    output.Add(new Point3d(pos.X, pos.Y, 0));
                }
                else if (item is Point3d p3d)
                {
                    output.Add(p3d);
                }
                else if (item is Point2f p2f)
                {
                    output.Add(new Point3d(p2f.X, p2f.Y, 0));
                }
            }
        }
    }

    private static void TryAppendPointsFromJson(string json, ICollection<Point3d> output)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!TryGetNumber(item, "X", out var x) || !TryGetNumber(item, "Y", out var y))
                {
                    continue;
                }

                var z = TryGetNumber(item, "Z", out var parsedZ) ? parsedZ : 0.0;
                output.Add(new Point3d(x, y, z));
            }
        }
        catch
        {
            // Ignore invalid point JSON and let caller handle empty point collection.
        }
    }

    private static bool TryGetNumber(JsonElement obj, string name, out double value)
    {
        value = 0;
        foreach (var property in obj.EnumerateObject())
        {
            if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Number)
            {
                value = property.Value.GetDouble();
                return true;
            }

            if (property.Value.ValueKind == JsonValueKind.String &&
                double.TryParse(property.Value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            return false;
        }

        return false;
    }

    private static bool TryCreateRayPlaneContext(
        CalibrationBundleV2 bundle,
        out RayPlaneContext context,
        out string error)
    {
        context = default;
        error = string.Empty;

        if (bundle.Intrinsics == null || !CalibrationBundleV2Json.HasMatrix(bundle.Intrinsics.CameraMatrix, 3, 3))
        {
            error = "Ray-plane path requires Intrinsics.CameraMatrix (3x3).";
            return false;
        }

        if (bundle.Transform3D == null || !CalibrationBundleV2Json.HasMatrix(bundle.Transform3D.Matrix, 4, 4))
        {
            error = "Ray-plane path requires Transform3D.Matrix (4x4).";
            return false;
        }

        if (!CalibrationBundleV2Helpers.IsFiniteMatrix(bundle.Intrinsics.CameraMatrix) ||
            !CalibrationBundleV2Helpers.IsFiniteMatrix(bundle.Transform3D.Matrix))
        {
            error = "Calibration matrix data contains NaN or Infinity.";
            return false;
        }

        var k = bundle.Intrinsics.CameraMatrix;
        var fx = k[0][0];
        var fy = k[1][1];
        var cx = k[0][2];
        var cy = k[1][2];

        if (fx <= Epsilon || fy <= Epsilon)
        {
            error = "Camera matrix is invalid because fx/fy must be positive.";
            return false;
        }

        var source = bundle.SourceFrame?.ToLowerInvariant() ?? string.Empty;
        var target = bundle.TargetFrame?.ToLowerInvariant() ?? string.Empty;
        var rawTransform = bundle.Transform3D.Matrix;

        double[][] cameraToWorld;
        if (source.Contains("camera") && target.Contains("world"))
        {
            cameraToWorld = CloneMatrix(rawTransform);
        }
        else if (source.Contains("world") && target.Contains("camera"))
        {
            if (!TryInvert4x4(rawTransform, out cameraToWorld))
            {
                error = "Transform3D is singular and cannot be inverted.";
                return false;
            }
        }
        else
        {
            cameraToWorld = CloneMatrix(rawTransform);
        }

        if (!TryInvert4x4(cameraToWorld, out var worldToCamera))
        {
            error = "Camera-to-world matrix is singular.";
            return false;
        }

        context = new RayPlaneContext(fx, fy, cx, cy, cameraToWorld, worldToCamera);
        return true;
    }

    private static bool TryPixelToWorldByRayPlane(
        RayPlaneContext context,
        double pixelX,
        double pixelY,
        double worldPlaneZ,
        out Point3d worldPoint,
        out string error)
    {
        worldPoint = default;
        error = string.Empty;

        var rayCamera = Normalize(new Point3d(
            (pixelX - context.Cx) / context.Fx,
            (pixelY - context.Cy) / context.Fy,
            1.0));

        var rayWorld = TransformDirection(context.CameraToWorld, rayCamera);
        if (Math.Abs(rayWorld.Z) <= Epsilon)
        {
            error = "Ray is parallel to the target world plane.";
            return false;
        }

        var cameraCenter = TransformPoint(context.CameraToWorld, new Point3d(0, 0, 0));
        var scale = (worldPlaneZ - cameraCenter.Z) / rayWorld.Z;
        worldPoint = new Point3d(
            cameraCenter.X + scale * rayWorld.X,
            cameraCenter.Y + scale * rayWorld.Y,
            worldPlaneZ);

        if (!IsFinite(worldPoint))
        {
            error = "Computed world point is not finite.";
            return false;
        }

        return true;
    }

    private static bool TryWorldToPixelByProjection(
        RayPlaneContext context,
        Point3d worldPoint,
        out Point3d pixelPoint,
        out string error)
    {
        pixelPoint = default;
        error = string.Empty;

        var cameraPoint = TransformPoint(context.WorldToCamera, worldPoint);
        if (Math.Abs(cameraPoint.Z) <= Epsilon)
        {
            error = "Point projects to infinity (camera Z is zero).";
            return false;
        }

        var x = cameraPoint.X / cameraPoint.Z;
        var y = cameraPoint.Y / cameraPoint.Z;
        var u = context.Fx * x + context.Cx;
        var v = context.Fy * y + context.Cy;
        pixelPoint = new Point3d(u, v, 0);

        if (!IsFinite(pixelPoint))
        {
            error = "Projected pixel point is not finite.";
            return false;
        }

        return true;
    }

    private static Point3d TransformPoint(double[][] matrix, Point3d point)
    {
        var x = matrix[0][0] * point.X + matrix[0][1] * point.Y + matrix[0][2] * point.Z + matrix[0][3];
        var y = matrix[1][0] * point.X + matrix[1][1] * point.Y + matrix[1][2] * point.Z + matrix[1][3];
        var z = matrix[2][0] * point.X + matrix[2][1] * point.Y + matrix[2][2] * point.Z + matrix[2][3];
        var w = matrix[3][0] * point.X + matrix[3][1] * point.Y + matrix[3][2] * point.Z + matrix[3][3];
        if (Math.Abs(w) <= Epsilon)
        {
            return new Point3d(double.NaN, double.NaN, double.NaN);
        }

        return new Point3d(x / w, y / w, z / w);
    }

    private static Point3d TransformDirection(double[][] matrix, Point3d direction)
    {
        var x = matrix[0][0] * direction.X + matrix[0][1] * direction.Y + matrix[0][2] * direction.Z;
        var y = matrix[1][0] * direction.X + matrix[1][1] * direction.Y + matrix[1][2] * direction.Z;
        var z = matrix[2][0] * direction.X + matrix[2][1] * direction.Y + matrix[2][2] * direction.Z;
        return Normalize(new Point3d(x, y, z));
    }

    private static Point3d Normalize(Point3d vector)
    {
        var norm = Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z);
        if (norm <= Epsilon)
        {
            return new Point3d(double.NaN, double.NaN, double.NaN);
        }

        return new Point3d(vector.X / norm, vector.Y / norm, vector.Z / norm);
    }

    private static bool TryInvert4x4(double[][] matrix, out double[][] inverse)
    {
        inverse = Array.Empty<double[]>();
        try
        {
            using var mat = CalibrationBundleV2Helpers.ToMat(matrix);
            using var inv = new Mat();
            var invertResult = Cv2.Invert(mat, inv, DecompTypes.LU);
            if (Math.Abs(invertResult) <= Epsilon)
            {
                return false;
            }

            inverse = CalibrationBundleV2Helpers.ToJaggedMatrix(inv);
            return CalibrationBundleV2Json.HasMatrix(inverse, 4, 4) && CalibrationBundleV2Helpers.IsFiniteMatrix(inverse);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsFinite(Point3d point)
    {
        return double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);
    }

    private static double[][] CloneMatrix(double[][] source)
    {
        var clone = new double[source.Length][];
        for (var i = 0; i < source.Length; i++)
        {
            clone[i] = source[i].ToArray();
        }

        return clone;
    }

    private readonly record struct RayPlaneContext(
        double Fx,
        double Fy,
        double Cx,
        double Cy,
        double[][] CameraToWorld,
        double[][] WorldToCamera);
}
