using System.Collections;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "几何公差",
    Description = "Evaluates a constrained 2D GD&T subset using feature/datum and tolerance-zone semantics.",
    Category = "检测",
    IconName = "geometric-tolerance",
    Keywords = new[]
    {
        "公差",
        "平行度",
        "垂直度",
        "位置度",
        "同心度",
        "GD&T",
        "Tolerance",
        "Datum"
    }
)]
[InputPort("Image", "输入图像", PortDataType.Image, IsRequired = false)]
[InputPort("FeaturePrimary", "Primary Feature", PortDataType.Any, IsRequired = true)]
[InputPort("DatumA", "Datum A", PortDataType.Any, IsRequired = true)]
[InputPort("DatumB", "Datum B", PortDataType.Any, IsRequired = false)]
[InputPort("DatumC", "Datum C", PortDataType.Any, IsRequired = false)]
[OutputPort("Image", "结果图像", PortDataType.Image)]
[OutputPort("Tolerance", "公差带", PortDataType.Float)]
[OutputPort("ZoneDeviation", "偏离公差带", PortDataType.Float)]
[OutputPort("AngularDeviationDeg", "角度偏差(度)", PortDataType.Float)]
[OutputPort("LinearBand", "线性偏差带(像素)", PortDataType.Float)]
[OutputPort("MeasurementModel", "测量模型", PortDataType.String)]
[OutputPort("Accepted", "Accepted", PortDataType.Boolean)]
[OperatorParam("ToleranceType", "Tolerance Type", "enum", DefaultValue = "Parallelism", Options = new[] { "Parallelism|平行度", "Perpendicularity|垂直度", "Position|位置度", "Concentricity|同心度" })]
[OperatorParam("ZoneSize", "Zone Size", "double", DefaultValue = 2.0, Min = 0.0)]
[OperatorParam("EvaluationMode", "Evaluation Mode", "enum", DefaultValue = "CircularZone", Options = new[] { "CircularZone|Circular Zone", "RectangularZone|Rectangular Zone", "Projected2D|Projected 2D" })]
[OperatorParam("NominalX", "Nominal X", "double", DefaultValue = 0.0)]
[OperatorParam("NominalY", "Nominal Y", "double", DefaultValue = 0.0)]
public class GeometricToleranceOperator : OperatorBase
{
    private const string MeasurementModel = "DatumZone2D";

    public override OperatorType OperatorType => OperatorType.GeometricTolerance;

    public GeometricToleranceOperator(ILogger<GeometricToleranceOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("FeaturePrimary and DatumA are required"));
        }

        if (!inputs.TryGetValue("FeaturePrimary", out var featureObj) || featureObj == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("FeaturePrimary is required"));
        }

        if (!inputs.TryGetValue("DatumA", out var datumAObj) || datumAObj == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("DatumA is required"));
        }

        var toleranceType = GetStringParam(@operator, "ToleranceType", "Parallelism");
        var evaluationMode = GetStringParam(@operator, "EvaluationMode", "CircularZone");
        var zoneSize = GetDoubleParam(@operator, "ZoneSize", 2.0, 0.0, double.MaxValue);
        var nominalX = GetDoubleParam(@operator, "NominalX", 0.0);
        var nominalY = GetDoubleParam(@operator, "NominalY", 0.0);

        if (!TryEvaluate(toleranceType, evaluationMode, zoneSize, nominalX, nominalY, featureObj, datumAObj, inputs, out var evaluation, out var error))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(error ?? "Unsupported tolerance evaluation"));
        }

        var output = new Dictionary<string, object>
        {
            { "Tolerance", zoneSize },
            { "ZoneDeviation", evaluation.ZoneDeviation },
            { "AngularDeviationDeg", evaluation.AngularDeviationDeg },
            { "LinearBand", evaluation.LinearBand },
            { "ToleranceType", toleranceType },
            { "EvaluationMode", evaluationMode },
            { "MeasurementModel", MeasurementModel },
            { "Accepted", evaluation.Accepted },
            { "Result", evaluation.ResultText },
            { "StatusCode", evaluation.Accepted ? "OK" : "OutOfTolerance" },
            { "StatusMessage", evaluation.Accepted ? "Success" : "Out of tolerance" },
            { "Confidence", 1.0 },
            { "UncertaintyPx", 0.0 }
        };

        if (TryGetInputImage(inputs, "Image", out var imageWrapper) && imageWrapper != null)
        {
            var src = imageWrapper.GetMat();
            if (!src.Empty())
            {
                var resultImage = src.Clone();
                DrawOverlay(resultImage, featureObj, datumAObj, inputs.GetValueOrDefault("DatumB"), evaluation);
                return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, output)));
            }
        }

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var toleranceType = GetStringParam(@operator, "ToleranceType", "Parallelism");
        var validToleranceTypes = new[] { "Parallelism", "Perpendicularity", "Position", "Concentricity" };
        if (!validToleranceTypes.Contains(toleranceType, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("ToleranceType must be Parallelism, Perpendicularity, Position or Concentricity");
        }

        var evaluationMode = GetStringParam(@operator, "EvaluationMode", "CircularZone");
        var validEvaluationModes = new[] { "CircularZone", "RectangularZone", "Projected2D" };
        if (!validEvaluationModes.Contains(evaluationMode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("EvaluationMode must be CircularZone, RectangularZone or Projected2D");
        }

        var zoneSize = GetDoubleParam(@operator, "ZoneSize", 2.0);
        if (zoneSize < 0)
        {
            return ValidationResult.Invalid("ZoneSize must be >= 0");
        }

        return ValidationResult.Valid();
    }

    private static bool TryEvaluate(
        string toleranceType,
        string evaluationMode,
        double zoneSize,
        double nominalX,
        double nominalY,
        object featureObj,
        object datumAObj,
        Dictionary<string, object> inputs,
        out ToleranceEvaluation evaluation,
        out string? error)
    {
        evaluation = default;
        error = null;

        switch (toleranceType)
        {
            case "Parallelism":
                if (!TryParseLine(featureObj, out var featureLine) || !TryParseLine(datumAObj, out var datumLine))
                {
                    error = "Parallelism requires FeaturePrimary and DatumA as lines";
                    return false;
                }

                var angleDeviation = MeasurementGeometryHelper.AngleBetweenLineDirections(featureLine, datumLine);
                var distanceStart = MeasurementGeometryHelper.DistancePointToInfiniteLine(featureLine.StartX, featureLine.StartY, datumLine);
                var distanceEnd = MeasurementGeometryHelper.DistancePointToInfiniteLine(featureLine.EndX, featureLine.EndY, datumLine);
                var linearBand = Math.Abs(distanceStart - distanceEnd);
                evaluation = new ToleranceEvaluation(
                    ZoneDeviation: linearBand,
                    AngularDeviationDeg: angleDeviation,
                    LinearBand: linearBand,
                    Accepted: linearBand <= zoneSize,
                    ResultText: $"Parallelism zone deviation = {linearBand:F4}px, angle deviation = {angleDeviation:F4}deg");
                return true;

            case "Perpendicularity":
                if (!TryParseLine(featureObj, out featureLine) || !TryParseLine(datumAObj, out datumLine))
                {
                    error = "Perpendicularity requires FeaturePrimary and DatumA as lines";
                    return false;
                }

                var rawAngle = MeasurementGeometryHelper.AngleBetweenLineDirections(featureLine, datumLine);
                var perpendicularDeviation = Math.Abs(rawAngle - 90.0);
                var datumDirection = new Position(datumLine.EndX - datumLine.StartX, datumLine.EndY - datumLine.StartY);
                var axisNorm = Math.Sqrt((datumDirection.X * datumDirection.X) + (datumDirection.Y * datumDirection.Y));
                if (axisNorm < 1e-9)
                {
                    error = "DatumA is degenerate";
                    return false;
                }

                var unitAxisX = datumDirection.X / axisNorm;
                var unitAxisY = datumDirection.Y / axisNorm;
                var projectionStart = ((featureLine.StartX - datumLine.StartX) * unitAxisX) + ((featureLine.StartY - datumLine.StartY) * unitAxisY);
                var projectionEnd = ((featureLine.EndX - datumLine.StartX) * unitAxisX) + ((featureLine.EndY - datumLine.StartY) * unitAxisY);
                var perpendicularBand = Math.Abs(projectionEnd - projectionStart);
                evaluation = new ToleranceEvaluation(
                    ZoneDeviation: perpendicularBand,
                    AngularDeviationDeg: perpendicularDeviation,
                    LinearBand: perpendicularBand,
                    Accepted: perpendicularBand <= zoneSize,
                    ResultText: $"Perpendicularity zone deviation = {perpendicularBand:F4}px, angle deviation = {perpendicularDeviation:F4}deg");
                return true;

            case "Position":
                if (!TryResolveCenter(featureObj, out var featureCenter))
                {
                    error = "Position requires FeaturePrimary as point or circle";
                    return false;
                }

                if (!TryParseLine(datumAObj, out datumLine))
                {
                    error = "Position requires DatumA as a line";
                    return false;
                }

                if (!inputs.TryGetValue("DatumB", out var datumBObj) || datumBObj == null || !TryParseLine(datumBObj, out var datumBLine))
                {
                    error = "Position requires DatumB as a line";
                    return false;
                }

                if (!MeasurementGeometryHelper.TryGetInfiniteLineIntersection(datumLine, datumBLine, out var origin))
                {
                    error = "DatumA and DatumB must intersect to define a datum frame";
                    return false;
                }

                var frame = CreateDatumFrame(origin, datumLine, datumBLine);
                var actual = ProjectToFrame(featureCenter, frame);
                var deltaX = actual.X - nominalX;
                var deltaY = actual.Y - nominalY;
                var zoneDeviation = evaluationMode.Equals("RectangularZone", StringComparison.OrdinalIgnoreCase)
                    ? Math.Max(Math.Abs(deltaX), Math.Abs(deltaY))
                    : Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
                var acceptanceLimit = evaluationMode.Equals("RectangularZone", StringComparison.OrdinalIgnoreCase) ? zoneSize / 2.0 : zoneSize / 2.0;
                evaluation = new ToleranceEvaluation(
                    ZoneDeviation: zoneDeviation,
                    AngularDeviationDeg: 0.0,
                    LinearBand: zoneDeviation,
                    Accepted: zoneDeviation <= acceptanceLimit,
                    ResultText: $"Position deviation = {zoneDeviation:F4}px relative to nominal ({nominalX:F2}, {nominalY:F2})");
                return true;

            case "Concentricity":
                if (!TryParseCircle(featureObj, out var featureCircle) || !TryParseCircle(datumAObj, out var datumCircle))
                {
                    error = "Concentricity requires FeaturePrimary and DatumA as circles";
                    return false;
                }

                var centerOffset = MeasurementGeometryHelper.Distance(
                    featureCircle.CenterX,
                    featureCircle.CenterY,
                    datumCircle.CenterX,
                    datumCircle.CenterY);
                evaluation = new ToleranceEvaluation(
                    ZoneDeviation: centerOffset,
                    AngularDeviationDeg: 0.0,
                    LinearBand: centerOffset,
                    Accepted: centerOffset <= zoneSize / 2.0,
                    ResultText: $"Concentricity center offset = {centerOffset:F4}px");
                return true;
            default:
                error = "Unsupported tolerance type";
                return false;
        }
    }

    private static (Position Origin, Position AxisX, Position AxisY) CreateDatumFrame(Position origin, LineData datumA, LineData datumB)
    {
        var axisA = NormalizeDirection(new Position(datumA.EndX - datumA.StartX, datumA.EndY - datumA.StartY));
        var axisB = NormalizeDirection(new Position(datumB.EndX - datumB.StartX, datumB.EndY - datumB.StartY));
        return (origin, axisA, axisB);
    }

    private static Position ProjectToFrame(Position point, (Position Origin, Position AxisX, Position AxisY) frame)
    {
        var vx = point.X - frame.Origin.X;
        var vy = point.Y - frame.Origin.Y;
        return new Position(
            (vx * frame.AxisX.X) + (vy * frame.AxisX.Y),
            (vx * frame.AxisY.X) + (vy * frame.AxisY.Y));
    }

    private static Position NormalizeDirection(Position vector)
    {
        var norm = Math.Sqrt((vector.X * vector.X) + (vector.Y * vector.Y));
        if (norm < 1e-9)
        {
            return new Position(1, 0);
        }

        return new Position(vector.X / norm, vector.Y / norm);
    }

    private static void DrawOverlay(Mat image, object featureObj, object datumAObj, object? datumBObj, ToleranceEvaluation evaluation)
    {
        DrawGeometry(image, datumAObj, new Scalar(0, 255, 0));
        if (datumBObj != null)
        {
            DrawGeometry(image, datumBObj, new Scalar(255, 200, 0));
        }

        DrawGeometry(image, featureObj, new Scalar(0, 0, 255));
        Cv2.PutText(
            image,
            evaluation.Accepted
                ? $"PASS {evaluation.ZoneDeviation:F3}"
                : $"FAIL {evaluation.ZoneDeviation:F3}",
            new Point(10, 24),
            HersheyFonts.HersheySimplex,
            0.6,
            evaluation.Accepted ? new Scalar(0, 255, 0) : new Scalar(0, 0, 255),
            2);
    }

    private static void DrawGeometry(Mat image, object geometry, Scalar color)
    {
        if (TryParseLine(geometry, out var line))
        {
            Cv2.Line(
                image,
                new Point((int)Math.Round(line.StartX), (int)Math.Round(line.StartY)),
                new Point((int)Math.Round(line.EndX), (int)Math.Round(line.EndY)),
                color,
                2);
            return;
        }

        if (TryParseCircle(geometry, out var circle))
        {
            Cv2.Circle(
                image,
                new Point((int)Math.Round(circle.CenterX), (int)Math.Round(circle.CenterY)),
                (int)Math.Round(circle.Radius),
                color,
                2);
            return;
        }

        if (TryResolveCenter(geometry, out var center))
        {
            Cv2.Circle(image, new Point((int)Math.Round(center.X), (int)Math.Round(center.Y)), 4, color, -1);
        }
    }

    private static bool TryResolveCenter(object geometry, out Position center)
    {
        if (TryParsePoint(geometry, out center))
        {
            return true;
        }

        if (TryParseCircle(geometry, out var circle))
        {
            center = new Position(circle.CenterX, circle.CenterY);
            return true;
        }

        center = new Position(0, 0);
        return false;
    }

    private static bool TryParsePoint(object? obj, out Position point)
    {
        point = new Position(0, 0);
        if (obj == null)
        {
            return false;
        }

        if (obj is Position position)
        {
            point = position;
            return true;
        }

        if (obj is IDictionary<string, object> dict &&
            TryGetDouble(dict, "X", out var x) &&
            TryGetDouble(dict, "Y", out var y))
        {
            point = new Position(x, y);
            return true;
        }

        if (obj is IDictionary legacy)
        {
            var normalized = legacy.Cast<DictionaryEntry>()
                .Where(entry => entry.Key != null)
                .ToDictionary(entry => entry.Key!.ToString() ?? string.Empty, entry => entry.Value ?? 0.0, StringComparer.OrdinalIgnoreCase);
            return TryParsePoint(normalized, out point);
        }

        return false;
    }

    private static bool TryParseLine(object? obj, out LineData line)
    {
        line = new LineData();
        if (obj == null)
        {
            return false;
        }

        if (obj is LineData lineData)
        {
            line = lineData;
            return true;
        }

        if (obj is IDictionary<string, object> dict)
        {
            if (TryGetDouble(dict, "StartX", out var sx) &&
                TryGetDouble(dict, "StartY", out var sy) &&
                TryGetDouble(dict, "EndX", out var ex) &&
                TryGetDouble(dict, "EndY", out var ey))
            {
                line = new LineData((float)sx, (float)sy, (float)ex, (float)ey);
                return true;
            }

            if (TryGetDouble(dict, "X1", out sx) &&
                TryGetDouble(dict, "Y1", out sy) &&
                TryGetDouble(dict, "X2", out ex) &&
                TryGetDouble(dict, "Y2", out ey))
            {
                line = new LineData((float)sx, (float)sy, (float)ex, (float)ey);
                return true;
            }
        }

        if (obj is IDictionary legacy)
        {
            var normalized = legacy.Cast<DictionaryEntry>()
                .Where(entry => entry.Key != null)
                .ToDictionary(entry => entry.Key!.ToString() ?? string.Empty, entry => entry.Value ?? 0.0, StringComparer.OrdinalIgnoreCase);
            return TryParseLine(normalized, out line);
        }

        return false;
    }

    private static bool TryParseCircle(object? obj, out CircleSpec circle)
    {
        circle = new CircleSpec(0, 0, 0);
        if (obj == null)
        {
            return false;
        }

        if (obj is CircleData circleData)
        {
            circle = new CircleSpec(circleData.CenterX, circleData.CenterY, circleData.Radius);
            return true;
        }

        if (obj is IDictionary<string, object> dict &&
            TryGetDouble(dict, "CenterX", out var cx) &&
            TryGetDouble(dict, "CenterY", out var cy) &&
            TryGetDouble(dict, "Radius", out var radius))
        {
            circle = new CircleSpec(cx, cy, radius);
            return true;
        }

        if (obj is IDictionary legacy)
        {
            var normalized = legacy.Cast<DictionaryEntry>()
                .Where(entry => entry.Key != null)
                .ToDictionary(entry => entry.Key!.ToString() ?? string.Empty, entry => entry.Value ?? 0.0, StringComparer.OrdinalIgnoreCase);
            return TryParseCircle(normalized, out circle);
        }

        return false;
    }

    private static bool TryGetDouble(IDictionary<string, object> dict, string key, out double value)
    {
        value = 0;
        if (!dict.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        return raw switch
        {
            double d => (value = d) == d,
            float f => (value = f) == f,
            int i => (value = i) == i,
            long l => (value = l) == l,
            _ => double.TryParse(raw.ToString(), out value)
        };
    }

    private sealed record CircleSpec(double CenterX, double CenterY, double Radius);

    private readonly record struct ToleranceEvaluation(
        double ZoneDeviation,
        double AngularDeviationDeg,
        double LinearBand,
        bool Accepted,
        string ResultText);
}
