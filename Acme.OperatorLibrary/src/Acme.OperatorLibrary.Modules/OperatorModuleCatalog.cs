using System.Collections.Generic;
using System.Linq;
using Acme.Product.Core.Enums;

namespace Acme.OperatorLibrary.Modules;

/// <summary>
/// Stable module map for package consumers.
/// </summary>
public enum OperatorModule
{
    ImageProcessing = 0,
    Measurement = 1,
    Calibration = 2,
    Communication = 3,
    FlowControl = 4,
    AI = 5
}

public static class OperatorModuleCatalog
{
    private static readonly IReadOnlyDictionary<OperatorType, OperatorModule> _moduleByType =
        System.Enum.GetValues<OperatorType>()
            .ToDictionary(type => type, Classify, System.Collections.Generic.EqualityComparer<OperatorType>.Default);

    private static readonly IReadOnlyDictionary<OperatorModule, IReadOnlyList<OperatorType>> _typesByModule =
        System.Enum.GetValues<OperatorModule>()
            .ToDictionary(
                module => module,
                module => (IReadOnlyList<OperatorType>)_moduleByType
                    .Where(item => item.Value == module)
                    .Select(item => item.Key)
                    .OrderBy(type => (int)type)
                    .ToArray());

    public static OperatorModule GetModule(OperatorType type)
    {
        return _moduleByType.TryGetValue(type, out var module) ? module : OperatorModule.ImageProcessing;
    }

    public static IReadOnlyList<OperatorType> GetTypes(OperatorModule module)
    {
        return _typesByModule.TryGetValue(module, out var types) ? types : [];
    }

    public static IReadOnlyList<OperatorType> ImageProcessingTypes => GetTypes(OperatorModule.ImageProcessing);

    public static IReadOnlyList<OperatorType> MeasurementTypes => GetTypes(OperatorModule.Measurement);

    public static IReadOnlyList<OperatorType> CalibrationTypes => GetTypes(OperatorModule.Calibration);

    public static IReadOnlyList<OperatorType> CommunicationTypes => GetTypes(OperatorModule.Communication);

    public static IReadOnlyList<OperatorType> FlowControlTypes => GetTypes(OperatorModule.FlowControl);

    public static IReadOnlyList<OperatorType> AiTypes => GetTypes(OperatorModule.AI);

    private static OperatorModule Classify(OperatorType type)
    {
        return type switch
        {
            OperatorType.CameraCalibration
                or OperatorType.Undistort
                or OperatorType.CoordinateTransform
                or OperatorType.NPointCalibration
                or OperatorType.CalibrationLoader
                or OperatorType.TranslationRotationCalibration
                => OperatorModule.Calibration,

            OperatorType.ModbusCommunication
                or OperatorType.TcpCommunication
                or OperatorType.SerialCommunication
                or OperatorType.SiemensS7Communication
                or OperatorType.MitsubishiMcCommunication
                or OperatorType.OmronFinsCommunication
                or OperatorType.ModbusRtuCommunication
                or OperatorType.HttpRequest
                or OperatorType.MqttPublish
                => OperatorModule.Communication,

            OperatorType.DeepLearning
                or OperatorType.OnnxInference
                or OperatorType.DualModalVoting
                or OperatorType.SurfaceDefectDetection
                or OperatorType.EdgePairDefect
                or OperatorType.BoxNms
                or OperatorType.BoxFilter
                or OperatorType.CodeRecognition
                or OperatorType.OcrRecognition
                => OperatorModule.AI,

            OperatorType.Measurement
                or OperatorType.CircleMeasurement
                or OperatorType.LineMeasurement
                or OperatorType.ContourMeasurement
                or OperatorType.AngleMeasurement
                or OperatorType.GeometricTolerance
                or OperatorType.GeometricFitting
                or OperatorType.CaliperTool
                or OperatorType.WidthMeasurement
                or OperatorType.PointLineDistance
                or OperatorType.LineLineDistance
                or OperatorType.GapMeasurement
                or OperatorType.GeoMeasurement
                or OperatorType.SharpnessEvaluation
                or OperatorType.ColorMeasurement
                or OperatorType.HistogramAnalysis
                or OperatorType.PixelStatistics
                => OperatorModule.Measurement,

            OperatorType.ConditionalBranch
                or OperatorType.ResultJudgment
                or OperatorType.ResultOutput
                or OperatorType.DatabaseWrite
                or OperatorType.VariableRead
                or OperatorType.VariableWrite
                or OperatorType.VariableIncrement
                or OperatorType.TryCatch
                or OperatorType.CycleCounter
                or OperatorType.ForEach
                or OperatorType.ArrayIndexer
                or OperatorType.JsonExtractor
                or OperatorType.MathOperation
                or OperatorType.LogicGate
                or OperatorType.TypeConvert
                or OperatorType.StringFormat
                or OperatorType.Aggregator
                or OperatorType.Comment
                or OperatorType.Comparator
                or OperatorType.Delay
                or OperatorType.UnitConvert
                or OperatorType.TimerStatistics
                or OperatorType.ScriptOperator
                or OperatorType.TriggerModule
                or OperatorType.TextSave
                or OperatorType.PointSetTool
                => OperatorModule.FlowControl,

            _ => OperatorModule.ImageProcessing
        };
    }
}
