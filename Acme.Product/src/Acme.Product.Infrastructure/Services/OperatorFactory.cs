// OperatorFactory.cs
// 算子工厂实现
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using Acme.Product.Core.ValueObjects;

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// 算子工厂实现
/// </summary>
public class OperatorFactory : IOperatorFactory
{
    private readonly Dictionary<OperatorType, OperatorMetadata> _metadata = new();

    public OperatorFactory()
    {
        InitializeDefaultOperators();
    }

    public Operator CreateOperator(OperatorType type, string name, double x, double y)
    {
        var op = new Operator(name, type, x, y);
        var metadata = GetMetadata(type);

        if (metadata != null)
        {
            // 根据元数据添加端口
            foreach (var portDef in metadata.InputPorts)
            {
                op.AddInputPort(portDef.Name, portDef.DataType, portDef.IsRequired);
            }

            foreach (var portDef in metadata.OutputPorts)
            {
                op.AddOutputPort(portDef.Name, portDef.DataType);
            }

            // 根据元数据添加参数
            foreach (var paramDef in metadata.Parameters)
            {
                var parameter = new Parameter(
                    Guid.NewGuid(),
                    paramDef.Name,
                    paramDef.DisplayName,
                    paramDef.Description ?? string.Empty,
                    paramDef.DataType,
                    paramDef.DefaultValue,
                    paramDef.MinValue,
                    paramDef.MaxValue,
                    paramDef.IsRequired,
                    paramDef.Options
                );
                op.AddParameter(parameter);
            }
        }
        else
        {
            // 为未知类型添加通用输入输出端口 (Fallback)
            op.AddInputPort("Input", PortDataType.Any, false);
            op.AddOutputPort("Output", PortDataType.Any);
        }

        return op;
    }

    public OperatorMetadata? GetMetadata(OperatorType type)
    {
        return _metadata.TryGetValue(type, out var metadata) ? metadata : null;
    }

    public IEnumerable<OperatorMetadata> GetAllMetadata()
    {
        return _metadata.Values;
    }

    public IEnumerable<OperatorType> GetSupportedOperatorTypes()
    {
        return _metadata.Keys;
    }

    public void RegisterOperator(OperatorMetadata metadata)
    {
        _metadata[metadata.Type] = metadata;
    }

    private void InitializeDefaultOperators()
    {
        // 图像采集
        _metadata[OperatorType.ImageAcquisition] = new OperatorMetadata
        {
            Type = OperatorType.ImageAcquisition,
            DisplayName = "图像采集",
            Description = "从文件或相机采集图像",
            Category = "采集",
            IconName = "camera",
            Keywords = new[] { "采集", "相机", "拍照", "取图", "摄像头", "图像输入", "Acquire", "Camera", "Capture" },
            InputPorts = new List<PortDefinition>(),
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "sourceType", DisplayName = "采集源", DataType = "enum", DefaultValue = "file", Options = new List<ParameterOption> { new() { Label = "文件", Value = "file" }, new() { Label = "相机", Value = "camera" } } },
                new() { Name = "filePath", DisplayName = "文件路径", DataType = "file", DefaultValue = "" },
                new() { Name = "cameraId", DisplayName = "相机", DataType = "cameraBinding", DefaultValue = "" },
                new() { Name = "exposureTime", DisplayName = "曝光时间", DataType = "double", DefaultValue = 5000.0, MinValue = 1.0, MaxValue = 1000000.0 },
                new() { Name = "gain", DisplayName = "增益", DataType = "double", DefaultValue = 1.0, MinValue = 1.0, MaxValue = 20.0 },
                new() { Name = "triggerMode", DisplayName = "触发模式", DataType = "enum", DefaultValue = "Software", Options = new List<ParameterOption> { new() { Label = "软触发", Value = "Software" }, new() { Label = "外触发", Value = "Hardware" } } }
            }
        };

        // 滤波
        // ==================== Phase 1 Operators ====================

        _metadata[OperatorType.CaliperTool] = new OperatorMetadata
        {
            Type = OperatorType.CaliperTool,
            DisplayName = "卡尺工具",
            Description = "Detects edge pairs along a scan line and reports width.",
            Category = "检测",
            IconName = "caliper",
            Keywords = new[] { "caliper", "edge pair", "width", "distance", "edge" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "SearchRegion", DisplayName = "Search Region", DataType = PortDataType.Rectangle, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image },
                new() { Name = "Width", DisplayName = "Width", DataType = PortDataType.Float },
                new() { Name = "EdgePairs", DisplayName = "Edge Pairs", DataType = PortDataType.PointList },
                new() { Name = "PairCount", DisplayName = "Pair Count", DataType = PortDataType.Integer }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Direction", DisplayName = "Direction", DataType = "enum", DefaultValue = "Horizontal", Options = new List<ParameterOption>
                {
                    new() { Label = "Horizontal", Value = "Horizontal" },
                    new() { Label = "Vertical", Value = "Vertical" },
                    new() { Label = "Custom", Value = "Custom" }
                } },
                new() { Name = "Angle", DisplayName = "Angle", DataType = "double", DefaultValue = 0.0, MinValue = -180.0, MaxValue = 180.0 },
                new() { Name = "Polarity", DisplayName = "Polarity", DataType = "enum", DefaultValue = "Both", Options = new List<ParameterOption>
                {
                    new() { Label = "DarkToLight", Value = "DarkToLight" },
                    new() { Label = "LightToDark", Value = "LightToDark" },
                    new() { Label = "Both", Value = "Both" }
                } },
                new() { Name = "EdgeThreshold", DisplayName = "Edge Threshold", DataType = "double", DefaultValue = 18.0, MinValue = 1.0, MaxValue = 255.0 },
                new() { Name = "ExpectedCount", DisplayName = "Expected Count", DataType = "int", DefaultValue = 1, MinValue = 1, MaxValue = 100 },
                new() { Name = "SubpixelAccuracy", DisplayName = "Subpixel Accuracy", DataType = "bool", DefaultValue = false }
            }
        };

        _metadata[OperatorType.WidthMeasurement] = new OperatorMetadata
        {
            Type = OperatorType.WidthMeasurement,
            DisplayName = "宽度测量",
            Description = "Measures width between parallel edges or lines.",
            Category = "检测",
            IconName = "ruler",
            Keywords = new[] { "width", "thickness", "gap", "distance" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "Line1", DisplayName = "Line 1", DataType = PortDataType.LineData, IsRequired = false },
                new() { Name = "Line2", DisplayName = "Line 2", DataType = PortDataType.LineData, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image },
                new() { Name = "Width", DisplayName = "Width", DataType = PortDataType.Float },
                new() { Name = "MinWidth", DisplayName = "Min Width", DataType = PortDataType.Float },
                new() { Name = "MaxWidth", DisplayName = "Max Width", DataType = PortDataType.Float }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "MeasureMode", DisplayName = "Measure Mode", DataType = "enum", DefaultValue = "AutoEdge", Options = new List<ParameterOption>
                {
                    new() { Label = "AutoEdge", Value = "AutoEdge" },
                    new() { Label = "ManualLines", Value = "ManualLines" }
                } },
                new() { Name = "NumSamples", DisplayName = "Sample Count", DataType = "int", DefaultValue = 24, MinValue = 10, MaxValue = 100 },
                new() { Name = "Direction", DisplayName = "Direction", DataType = "enum", DefaultValue = "Perpendicular", Options = new List<ParameterOption>
                {
                    new() { Label = "Perpendicular", Value = "Perpendicular" },
                    new() { Label = "Custom", Value = "Custom" }
                } }
            }
        };

        _metadata[OperatorType.PointLineDistance] = new OperatorMetadata
        {
            Type = OperatorType.PointLineDistance,
            DisplayName = "点线距离",
            Description = "Computes perpendicular distance from a point to a line.",
            Category = "检测",
            IconName = "distance",
            Keywords = new[] { "point", "line", "distance", "perpendicular" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Point", DisplayName = "Point", DataType = PortDataType.Point, IsRequired = true },
                new() { Name = "Line", DisplayName = "Line", DataType = PortDataType.LineData, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Distance", DisplayName = "Distance", DataType = PortDataType.Float },
                new() { Name = "FootPoint", DisplayName = "Foot Point", DataType = PortDataType.Point }
            }
        };

        _metadata[OperatorType.LineLineDistance] = new OperatorMetadata
        {
            Type = OperatorType.LineLineDistance,
            DisplayName = "线线距离",
            Description = "Computes distance and angle between two lines.",
            Category = "检测",
            IconName = "parallel",
            Keywords = new[] { "line distance", "angle", "parallel" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Line1", DisplayName = "Line 1", DataType = PortDataType.LineData, IsRequired = true },
                new() { Name = "Line2", DisplayName = "Line 2", DataType = PortDataType.LineData, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Distance", DisplayName = "Distance", DataType = PortDataType.Float },
                new() { Name = "Angle", DisplayName = "Angle", DataType = PortDataType.Float },
                new() { Name = "Intersection", DisplayName = "Intersection", DataType = PortDataType.Point },
                new() { Name = "HasIntersection", DisplayName = "Has Intersection", DataType = PortDataType.Boolean },
                new() { Name = "IsParallel", DisplayName = "Is Parallel", DataType = PortDataType.Boolean }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "ParallelThreshold", DisplayName = "Parallel Threshold", DataType = "double", DefaultValue = 2.0, MinValue = 0.0, MaxValue = 45.0 }
            }
        };

        _metadata[OperatorType.BoxNms] = new OperatorMetadata
        {
            Type = OperatorType.BoxNms,
            DisplayName = "候选框抑制",
            Description = "Runs non-maximum suppression on detection boxes.",
            Category = "数据处理",
            IconName = "nms",
            Keywords = new[] { "nms", "box", "iou", "suppression" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Detections", DisplayName = "Detections", DataType = PortDataType.DetectionList, IsRequired = true },
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Detections", DisplayName = "Detections", DataType = PortDataType.DetectionList },
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image },
                new() { Name = "Count", DisplayName = "Count", DataType = PortDataType.Integer }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "IouThreshold", DisplayName = "IoU Threshold", DataType = "double", DefaultValue = 0.45, MinValue = 0.1, MaxValue = 1.0 },
                new() { Name = "ScoreThreshold", DisplayName = "Score Threshold", DataType = "double", DefaultValue = 0.25, MinValue = 0.0, MaxValue = 1.0 },
                new() { Name = "MaxDetections", DisplayName = "Max Detections", DataType = "int", DefaultValue = 100, MinValue = 1, MaxValue = 1000 }
            }
        };

        _metadata[OperatorType.BoxFilter] = new OperatorMetadata
        {
            Type = OperatorType.BoxFilter,
            DisplayName = "候选框筛选",
            Description = "Filters detections by area, class, region, or score.",
            Category = "数据处理",
            IconName = "filter",
            Keywords = new[] { "box filter", "class filter", "area filter", "score" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Detections", DisplayName = "Detections", DataType = PortDataType.DetectionList, IsRequired = true },
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Detections", DisplayName = "Detections", DataType = PortDataType.DetectionList },
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image },
                new() { Name = "Count", DisplayName = "Count", DataType = PortDataType.Integer }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "FilterMode", DisplayName = "Filter Mode", DataType = "enum", DefaultValue = "Area", Options = new List<ParameterOption>
                {
                    new() { Label = "Area", Value = "Area" },
                    new() { Label = "Class", Value = "Class" },
                    new() { Label = "Region", Value = "Region" },
                    new() { Label = "Score", Value = "Score" }
                } },
                new() { Name = "MinArea", DisplayName = "Min Area", DataType = "int", DefaultValue = 0, MinValue = 0 },
                new() { Name = "MaxArea", DisplayName = "Max Area", DataType = "int", DefaultValue = 9999999, MinValue = 0 },
                new() { Name = "TargetClasses", DisplayName = "Target Classes", DataType = "string", DefaultValue = "" },
                new() { Name = "MinScore", DisplayName = "Min Score", DataType = "double", DefaultValue = 0.0, MinValue = 0.0, MaxValue = 1.0 },
                new() { Name = "RegionX", DisplayName = "Region X", DataType = "int", DefaultValue = 0 },
                new() { Name = "RegionY", DisplayName = "Region Y", DataType = "int", DefaultValue = 0 },
                new() { Name = "RegionW", DisplayName = "Region Width", DataType = "int", DefaultValue = 0 },
                new() { Name = "RegionH", DisplayName = "Region Height", DataType = "int", DefaultValue = 0 }
            }
        };

        _metadata[OperatorType.SharpnessEvaluation] = new OperatorMetadata
        {
            Type = OperatorType.SharpnessEvaluation,
            DisplayName = "清晰度评估",
            Description = "Evaluates focus quality of an image.",
            Category = "检测",
            IconName = "focus",
            Keywords = new[] { "sharpness", "focus", "blur", "laplacian", "tenengrad" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Score", DisplayName = "Score", DataType = PortDataType.Float },
                new() { Name = "IsSharp", DisplayName = "Is Sharp", DataType = PortDataType.Boolean },
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Method", DisplayName = "Method", DataType = "enum", DefaultValue = "Laplacian", Options = new List<ParameterOption>
                {
                    new() { Label = "Laplacian", Value = "Laplacian" },
                    new() { Label = "Brenner", Value = "Brenner" },
                    new() { Label = "Tenengrad", Value = "Tenengrad" },
                    new() { Label = "SMD", Value = "SMD" }
                } },
                new() { Name = "Threshold", DisplayName = "Threshold", DataType = "double", DefaultValue = 100.0, MinValue = 0.0 },
                new() { Name = "RoiX", DisplayName = "ROI X", DataType = "int", DefaultValue = 0 },
                new() { Name = "RoiY", DisplayName = "ROI Y", DataType = "int", DefaultValue = 0 },
                new() { Name = "RoiW", DisplayName = "ROI Width", DataType = "int", DefaultValue = 0 },
                new() { Name = "RoiH", DisplayName = "ROI Height", DataType = "int", DefaultValue = 0 }
            }
        };

        _metadata[OperatorType.PositionCorrection] = new OperatorMetadata
        {
            Type = OperatorType.PositionCorrection,
            DisplayName = "位置修正",
            Description = "Corrects downstream ROI coordinates using reference/base offsets.",
            Category = "定位",
            IconName = "position",
            Keywords = new[] { "position correction", "roi offset", "translation", "rotation" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "ReferencePoint", DisplayName = "Reference Point", DataType = PortDataType.Point, IsRequired = true },
                new() { Name = "BasePoint", DisplayName = "Base Point", DataType = PortDataType.Point, IsRequired = true },
                new() { Name = "RoiX", DisplayName = "ROI X", DataType = PortDataType.Integer, IsRequired = false },
                new() { Name = "RoiY", DisplayName = "ROI Y", DataType = PortDataType.Integer, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "CorrectedX", DisplayName = "Corrected X", DataType = PortDataType.Integer },
                new() { Name = "CorrectedY", DisplayName = "Corrected Y", DataType = PortDataType.Integer },
                new() { Name = "OffsetX", DisplayName = "Offset X", DataType = PortDataType.Float },
                new() { Name = "OffsetY", DisplayName = "Offset Y", DataType = PortDataType.Float },
                new() { Name = "Angle", DisplayName = "Angle", DataType = PortDataType.Float }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "CorrectionMode", DisplayName = "Correction Mode", DataType = "enum", DefaultValue = "Translation", Options = new List<ParameterOption>
                {
                    new() { Label = "Translation", Value = "Translation" },
                    new() { Label = "TranslationRotation", Value = "TranslationRotation" }
                } },
                new() { Name = "ReferenceAngle", DisplayName = "Reference Angle", DataType = "double", DefaultValue = 0.0, MinValue = -360.0, MaxValue = 360.0 },
                new() { Name = "CurrentAngle", DisplayName = "Current Angle", DataType = "double", DefaultValue = 0.0, MinValue = -360.0, MaxValue = 360.0 }
            }
        };

        _metadata[OperatorType.NPointCalibration] = new OperatorMetadata
        {
            Type = OperatorType.NPointCalibration,
            DisplayName = "N点标定",
            Description = "Builds affine or perspective calibration from user point pairs.",
            Category = "标定",
            IconName = "n-point",
            Keywords = new[] { "n-point", "affine", "perspective", "calibration" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "TransformMatrix", DisplayName = "Transform Matrix", DataType = PortDataType.Any },
                new() { Name = "PixelSize", DisplayName = "Pixel Size", DataType = PortDataType.Float },
                new() { Name = "ReprojectionError", DisplayName = "Reprojection Error", DataType = PortDataType.Float }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "CalibrationMode", DisplayName = "Calibration Mode", DataType = "enum", DefaultValue = "Affine", Options = new List<ParameterOption>
                {
                    new() { Label = "Affine", Value = "Affine" },
                    new() { Label = "Perspective", Value = "Perspective" }
                } },
                new() { Name = "PointPairs", DisplayName = "Point Pairs", DataType = "string", DefaultValue = "", IsRequired = true },
                new() { Name = "SavePath", DisplayName = "Save Path", DataType = "file", DefaultValue = "" }
            }
        };

        _metadata[OperatorType.CalibrationLoader] = new OperatorMetadata
        {
            Type = OperatorType.CalibrationLoader,
            DisplayName = "标定加载",
            Description = "Loads calibration data from JSON/XML/YAML file.",
            Category = "标定",
            IconName = "file-open",
            Keywords = new[] { "load calibration", "calibration file", "matrix" },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "TransformMatrix", DisplayName = "Transform Matrix", DataType = PortDataType.Any },
                new() { Name = "CameraMatrix", DisplayName = "Camera Matrix", DataType = PortDataType.Any },
                new() { Name = "DistCoeffs", DisplayName = "Dist Coeffs", DataType = PortDataType.Any },
                new() { Name = "PixelSize", DisplayName = "Pixel Size", DataType = PortDataType.Float }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "FilePath", DisplayName = "File Path", DataType = "file", DefaultValue = "", IsRequired = true },
                new() { Name = "FileFormat", DisplayName = "File Format", DataType = "enum", DefaultValue = "JSON", Options = new List<ParameterOption>
                {
                    new() { Label = "JSON", Value = "JSON" },
                    new() { Label = "XML", Value = "XML" },
                    new() { Label = "YAML", Value = "YAML" }
                } }
            }
        };

        _metadata[OperatorType.UnitConvert] = new OperatorMetadata
        {
            Type = OperatorType.UnitConvert,
            DisplayName = "单位换算",
            Description = "Converts value between pixel, mm, um and inch.",
            Category = "数据处理",
            IconName = "unit",
            Keywords = new[] { "unit convert", "pixel to mm", "mm", "um", "inch" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Value", DisplayName = "Value", DataType = PortDataType.Float, IsRequired = true },
                new() { Name = "PixelSize", DisplayName = "Pixel Size", DataType = PortDataType.Float, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Result", DisplayName = "Result", DataType = PortDataType.Float },
                new() { Name = "Unit", DisplayName = "Unit", DataType = PortDataType.String }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "FromUnit", DisplayName = "From Unit", DataType = "enum", DefaultValue = "Pixel", Options = new List<ParameterOption>
                {
                    new() { Label = "Pixel", Value = "Pixel" },
                    new() { Label = "mm", Value = "mm" },
                    new() { Label = "um", Value = "um" },
                    new() { Label = "inch", Value = "inch" }
                } },
                new() { Name = "ToUnit", DisplayName = "To Unit", DataType = "enum", DefaultValue = "mm", Options = new List<ParameterOption>
                {
                    new() { Label = "Pixel", Value = "Pixel" },
                    new() { Label = "mm", Value = "mm" },
                    new() { Label = "um", Value = "um" },
                    new() { Label = "inch", Value = "inch" }
                } },
                new() { Name = "Scale", DisplayName = "Scale", DataType = "double", DefaultValue = 1.0, MinValue = 0.000000001, MaxValue = 1000000.0 },
                new() { Name = "UseCalibration", DisplayName = "Use Calibration", DataType = "bool", DefaultValue = false }
            }
        };

        _metadata[OperatorType.TimerStatistics] = new OperatorMetadata
        {
            Type = OperatorType.TimerStatistics,
            DisplayName = "计时统计",
            Description = "Measures elapsed and cycle time statistics.",
            Category = "逻辑工具",
            IconName = "timer",
            Keywords = new[] { "timer", "elapsed", "cycle time", "ct", "statistics" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Trigger", DisplayName = "Trigger", DataType = PortDataType.Any, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "ElapsedMs", DisplayName = "Elapsed (ms)", DataType = PortDataType.Float },
                new() { Name = "TotalMs", DisplayName = "Total (ms)", DataType = PortDataType.Float },
                new() { Name = "AverageMs", DisplayName = "Average (ms)", DataType = PortDataType.Float },
                new() { Name = "Count", DisplayName = "Count", DataType = PortDataType.Integer }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Mode", DisplayName = "Mode", DataType = "enum", DefaultValue = "SingleShot", Options = new List<ParameterOption>
                {
                    new() { Label = "SingleShot", Value = "SingleShot" },
                    new() { Label = "Cumulative", Value = "Cumulative" }
                } },
                new() { Name = "ResetInterval", DisplayName = "Reset Interval", DataType = "int", DefaultValue = 0, MinValue = 0, MaxValue = 1000000 }
            }
        };




        // ==================== Phase 2 Operators: Sprint 1 ====================

        _metadata[OperatorType.ScriptOperator] = new OperatorMetadata
        {
            Type = OperatorType.ScriptOperator,
            DisplayName = "脚本算子",
            Description = "Runs user-defined expression or script snippet.",
            Category = "逻辑工具",
            IconName = "script",
            Keywords = new[] { "script", "custom", "code", "expression", "formula" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Input1", DisplayName = "Input 1", DataType = PortDataType.Any, IsRequired = false },
                new() { Name = "Input2", DisplayName = "Input 2", DataType = PortDataType.Any, IsRequired = false },
                new() { Name = "Input3", DisplayName = "Input 3", DataType = PortDataType.Any, IsRequired = false },
                new() { Name = "Input4", DisplayName = "Input 4", DataType = PortDataType.Any, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Output1", DisplayName = "Output 1", DataType = PortDataType.Any },
                new() { Name = "Output2", DisplayName = "Output 2", DataType = PortDataType.Any }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "ScriptLanguage", DisplayName = "Script Language", DataType = "enum", DefaultValue = "CSharpExpression", Options = new List<ParameterOption>
                {
                    new() { Label = "CSharpExpression", Value = "CSharpExpression" },
                    new() { Label = "CSharpScript", Value = "CSharpScript" }
                } },
                new() { Name = "Code", DisplayName = "Code", DataType = "string", DefaultValue = "Input1 + Input2" },
                new() { Name = "Timeout", DisplayName = "Timeout (ms)", DataType = "int", DefaultValue = 5000, MinValue = 1, MaxValue = 120000 }
            }
        };

        _metadata[OperatorType.TriggerModule] = new OperatorMetadata
        {
            Type = OperatorType.TriggerModule,
            DisplayName = "触发模块",
            Description = "Generates software, timer, or external triggers.",
            Category = "逻辑工具",
            IconName = "trigger",
            Keywords = new[] { "trigger", "start", "timer", "external signal" },
            InputPorts = new List<PortDefinition>(),
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Triggered", DisplayName = "Triggered", DataType = PortDataType.Boolean },
                new() { Name = "Timestamp", DisplayName = "Timestamp", DataType = PortDataType.String },
                new() { Name = "TriggerCount", DisplayName = "Trigger Count", DataType = PortDataType.Integer }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "TriggerMode", DisplayName = "Trigger Mode", DataType = "enum", DefaultValue = "Software", Options = new List<ParameterOption>
                {
                    new() { Label = "Software", Value = "Software" },
                    new() { Label = "Timer", Value = "Timer" },
                    new() { Label = "ExternalSignal", Value = "ExternalSignal" }
                } },
                new() { Name = "Interval", DisplayName = "Interval (ms)", DataType = "int", DefaultValue = 1000, MinValue = 1, MaxValue = 3600000 },
                new() { Name = "AutoRepeat", DisplayName = "Auto Repeat", DataType = "bool", DefaultValue = true }
            }
        };

        _metadata[OperatorType.PointAlignment] = new OperatorMetadata
        {
            Type = OperatorType.PointAlignment,
            DisplayName = "点位对齐",
            Description = "Computes offset and distance between current and reference points.",
            Category = "数据处理",
            IconName = "align-point",
            Keywords = new[] { "alignment", "offset", "reference point", "distance" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "CurrentPoint", DisplayName = "Current Point", DataType = PortDataType.Point, IsRequired = true },
                new() { Name = "ReferencePoint", DisplayName = "Reference Point", DataType = PortDataType.Point, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "OffsetX", DisplayName = "Offset X", DataType = PortDataType.Float },
                new() { Name = "OffsetY", DisplayName = "Offset Y", DataType = PortDataType.Float },
                new() { Name = "Distance", DisplayName = "Distance", DataType = PortDataType.Float }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "OutputUnit", DisplayName = "Output Unit", DataType = "enum", DefaultValue = "Pixel", Options = new List<ParameterOption>
                {
                    new() { Label = "Pixel", Value = "Pixel" },
                    new() { Label = "mm", Value = "mm" }
                } },
                new() { Name = "PixelSize", DisplayName = "Pixel Size", DataType = "double", DefaultValue = 1.0, MinValue = 0.000000001, MaxValue = 1000000.0 }
            }
        };

        _metadata[OperatorType.PointCorrection] = new OperatorMetadata
        {
            Type = OperatorType.PointCorrection,
            DisplayName = "点位修正",
            Description = "Computes translation/rotation correction from detected to reference point.",
            Category = "数据处理",
            IconName = "point-correction",
            Keywords = new[] { "correction", "compensation", "robot", "pick place" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "DetectedPoint", DisplayName = "Detected Point", DataType = PortDataType.Point, IsRequired = true },
                new() { Name = "DetectedAngle", DisplayName = "Detected Angle", DataType = PortDataType.Float, IsRequired = false },
                new() { Name = "ReferencePoint", DisplayName = "Reference Point", DataType = PortDataType.Point, IsRequired = true },
                new() { Name = "ReferenceAngle", DisplayName = "Reference Angle", DataType = PortDataType.Float, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "CorrectionX", DisplayName = "Correction X", DataType = PortDataType.Float },
                new() { Name = "CorrectionY", DisplayName = "Correction Y", DataType = PortDataType.Float },
                new() { Name = "CorrectionAngle", DisplayName = "Correction Angle", DataType = PortDataType.Float },
                new() { Name = "TransformMatrix", DisplayName = "Transform Matrix", DataType = PortDataType.Any }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "CorrectionMode", DisplayName = "Correction Mode", DataType = "enum", DefaultValue = "TranslationOnly", Options = new List<ParameterOption>
                {
                    new() { Label = "TranslationOnly", Value = "TranslationOnly" },
                    new() { Label = "TranslationRotation", Value = "TranslationRotation" }
                } },
                new() { Name = "OutputUnit", DisplayName = "Output Unit", DataType = "enum", DefaultValue = "Pixel", Options = new List<ParameterOption>
                {
                    new() { Label = "Pixel", Value = "Pixel" },
                    new() { Label = "mm", Value = "mm" }
                } },
                new() { Name = "PixelSize", DisplayName = "Pixel Size", DataType = "double", DefaultValue = 1.0, MinValue = 0.000000001, MaxValue = 1000000.0 }
            }
        };

        _metadata[OperatorType.GapMeasurement] = new OperatorMetadata
        {
            Type = OperatorType.GapMeasurement,
            DisplayName = "间隙测量",
            Description = "Measures spacing using points or image projection.",
            Category = "检测",
            IconName = "gap",
            Keywords = new[] { "gap", "spacing", "pitch", "distance" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = false },
                new() { Name = "Points", DisplayName = "Points", DataType = PortDataType.PointList, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image },
                new() { Name = "Gaps", DisplayName = "Gaps", DataType = PortDataType.Any },
                new() { Name = "MeanGap", DisplayName = "Mean Gap", DataType = PortDataType.Float },
                new() { Name = "MinGap", DisplayName = "Min Gap", DataType = PortDataType.Float },
                new() { Name = "MaxGap", DisplayName = "Max Gap", DataType = PortDataType.Float },
                new() { Name = "Count", DisplayName = "Count", DataType = PortDataType.Integer }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Direction", DisplayName = "Direction", DataType = "enum", DefaultValue = "Auto", Options = new List<ParameterOption>
                {
                    new() { Label = "Horizontal", Value = "Horizontal" },
                    new() { Label = "Vertical", Value = "Vertical" },
                    new() { Label = "Auto", Value = "Auto" }
                } },
                new() { Name = "MinGap", DisplayName = "Min Gap", DataType = "double", DefaultValue = 0.0, MinValue = 0.0, MaxValue = 1000000.0 },
                new() { Name = "MaxGap", DisplayName = "Max Gap", DataType = "double", DefaultValue = 0.0, MinValue = 0.0, MaxValue = 1000000.0 },
                new() { Name = "ExpectedCount", DisplayName = "Expected Count", DataType = "int", DefaultValue = 0, MinValue = 0, MaxValue = 10000 }
            }
        };

        // ==================== Phase 2 Operators: Sprint 2 ====================

        _metadata[OperatorType.PolarUnwrap] = new OperatorMetadata
        {
            Type = OperatorType.PolarUnwrap,
            DisplayName = "极坐标展开",
            Description = "Unwraps annular image regions into rectangular view.",
            Category = "图像处理",
            IconName = "polar",
            Keywords = new[] { "polar", "unwrap", "ring", "annular" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "Center", DisplayName = "Center", DataType = PortDataType.Point, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "CenterX", DisplayName = "Center X", DataType = "int", DefaultValue = 0 },
                new() { Name = "CenterY", DisplayName = "Center Y", DataType = "int", DefaultValue = 0 },
                new() { Name = "InnerRadius", DisplayName = "Inner Radius", DataType = "int", DefaultValue = 0, MinValue = 0, MaxValue = 100000 },
                new() { Name = "OuterRadius", DisplayName = "Outer Radius", DataType = "int", DefaultValue = 100, MinValue = 1, MaxValue = 100000 },
                new() { Name = "StartAngle", DisplayName = "Start Angle", DataType = "double", DefaultValue = 0.0, MinValue = -3600.0, MaxValue = 3600.0 },
                new() { Name = "EndAngle", DisplayName = "End Angle", DataType = "double", DefaultValue = 360.0, MinValue = -3600.0, MaxValue = 3600.0 },
                new() { Name = "OutputWidth", DisplayName = "Output Width", DataType = "int", DefaultValue = 0, MinValue = 0, MaxValue = 20000 },
                new() { Name = "UseWarpPolar", DisplayName = "Use WarpPolar", DataType = "bool", DefaultValue = true }
            }
        };

        _metadata[OperatorType.ShadingCorrection] = new OperatorMetadata
        {
            Type = OperatorType.ShadingCorrection,
            DisplayName = "光照校正",
            Description = "Corrects uneven illumination by background or model-based methods.",
            Category = "预处理",
            IconName = "shading",
            Keywords = new[] { "shading", "flat field", "illumination", "background" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "Background", DisplayName = "Background", DataType = PortDataType.Image, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Method", DisplayName = "Method", DataType = "enum", DefaultValue = "GaussianModel", Options = new List<ParameterOption>
                {
                    new() { Label = "DivideByBackground", Value = "DivideByBackground" },
                    new() { Label = "GaussianModel", Value = "GaussianModel" },
                    new() { Label = "MorphologicalTopHat", Value = "MorphologicalTopHat" }
                } },
                new() { Name = "KernelSize", DisplayName = "Kernel Size", DataType = "int", DefaultValue = 51, MinValue = 3, MaxValue = 501 }
            }
        };

        _metadata[OperatorType.FrameAveraging] = new OperatorMetadata
        {
            Type = OperatorType.FrameAveraging,
            DisplayName = "帧平均",
            Description = "Averages multi-frame input to reduce temporal noise.",
            Category = "预处理",
            IconName = "frame-average",
            Keywords = new[] { "frame", "averaging", "multi-frame", "denoise" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image },
                new() { Name = "FrameCount", DisplayName = "Frame Count", DataType = PortDataType.Integer }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "FrameCount", DisplayName = "Frame Count", DataType = "int", DefaultValue = 8, MinValue = 1, MaxValue = 64 },
                new() { Name = "Mode", DisplayName = "Mode", DataType = "enum", DefaultValue = "Mean", Options = new List<ParameterOption>
                {
                    new() { Label = "Mean", Value = "Mean" },
                    new() { Label = "Median", Value = "Median" }
                } }
            }
        };

        _metadata[OperatorType.AffineTransform] = new OperatorMetadata
        {
            Type = OperatorType.AffineTransform,
            DisplayName = "仿射变换",
            Description = "Applies 2D affine transform using 3-point or rotate-scale-translate mode.",
            Category = "图像处理",
            IconName = "affine",
            Keywords = new[] { "affine", "warp", "rotate", "scale", "translate" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image },
                new() { Name = "TransformMatrix", DisplayName = "Transform Matrix", DataType = PortDataType.Any }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Mode", DisplayName = "Mode", DataType = "enum", DefaultValue = "RotateScaleTranslate", Options = new List<ParameterOption>
                {
                    new() { Label = "ThreePoint", Value = "ThreePoint" },
                    new() { Label = "RotateScaleTranslate", Value = "RotateScaleTranslate" }
                } },
                new() { Name = "SrcPoints", DisplayName = "Source Points", DataType = "string", DefaultValue = "[[0,0],[100,0],[0,100]]" },
                new() { Name = "DstPoints", DisplayName = "Destination Points", DataType = "string", DefaultValue = "[[0,0],[100,0],[0,100]]" },
                new() { Name = "Angle", DisplayName = "Angle", DataType = "double", DefaultValue = 0.0, MinValue = -3600.0, MaxValue = 3600.0 },
                new() { Name = "Scale", DisplayName = "Scale", DataType = "double", DefaultValue = 1.0, MinValue = 0.001, MaxValue = 1000.0 },
                new() { Name = "TranslateX", DisplayName = "Translate X", DataType = "double", DefaultValue = 0.0, MinValue = -100000.0, MaxValue = 100000.0 },
                new() { Name = "TranslateY", DisplayName = "Translate Y", DataType = "double", DefaultValue = 0.0, MinValue = -100000.0, MaxValue = 100000.0 },
                new() { Name = "OutputWidth", DisplayName = "Output Width", DataType = "int", DefaultValue = 0, MinValue = 0, MaxValue = 10000 },
                new() { Name = "OutputHeight", DisplayName = "Output Height", DataType = "int", DefaultValue = 0, MinValue = 0, MaxValue = 10000 }
            }
        };

        _metadata[OperatorType.ColorMeasurement] = new OperatorMetadata
        {
            Type = OperatorType.ColorMeasurement,
            DisplayName = "颜色测量",
            Description = "Measures average Lab/HSV values and computes DeltaE.",
            Category = "颜色处理",
            IconName = "color-measure",
            Keywords = new[] { "color", "deltaE", "lab", "hsv" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "ReferenceColor", DisplayName = "Reference Color", DataType = PortDataType.Any, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "L", DisplayName = "L", DataType = PortDataType.Float },
                new() { Name = "A", DisplayName = "A", DataType = PortDataType.Float },
                new() { Name = "B", DisplayName = "B", DataType = PortDataType.Float },
                new() { Name = "H", DisplayName = "H", DataType = PortDataType.Float },
                new() { Name = "S", DisplayName = "S", DataType = PortDataType.Float },
                new() { Name = "V", DisplayName = "V", DataType = PortDataType.Float },
                new() { Name = "DeltaE", DisplayName = "DeltaE", DataType = PortDataType.Float },
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "ColorSpace", DisplayName = "Color Space", DataType = "enum", DefaultValue = "Lab", Options = new List<ParameterOption>
                {
                    new() { Label = "Lab", Value = "Lab" },
                    new() { Label = "HSV", Value = "HSV" }
                } },
                new() { Name = "RoiX", DisplayName = "ROI X", DataType = "int", DefaultValue = 0 },
                new() { Name = "RoiY", DisplayName = "ROI Y", DataType = "int", DefaultValue = 0 },
                new() { Name = "RoiW", DisplayName = "ROI W", DataType = "int", DefaultValue = 0 },
                new() { Name = "RoiH", DisplayName = "ROI H", DataType = "int", DefaultValue = 0 },
                new() { Name = "RefL", DisplayName = "Ref L", DataType = "double", DefaultValue = 0.0 },
                new() { Name = "RefA", DisplayName = "Ref A", DataType = "double", DefaultValue = 0.0 },
                new() { Name = "RefB", DisplayName = "Ref B", DataType = "double", DefaultValue = 0.0 }
            }
        };

        // ==================== Phase 2 Operators: Sprint 3 ====================

        _metadata[OperatorType.SurfaceDefectDetection] = new OperatorMetadata
        {
            Type = OperatorType.SurfaceDefectDetection,
            DisplayName = "表面缺陷检测",
            Description = "Detects surface defects using gradient, reference diff, or local contrast.",
            Category = "AI检测",
            IconName = "surface-defect",
            Keywords = new[] { "surface defect", "scratch", "stain", "traditional detection" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "Reference", DisplayName = "Reference", DataType = PortDataType.Image, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image },
                new() { Name = "DefectMask", DisplayName = "Defect Mask", DataType = PortDataType.Image },
                new() { Name = "DefectCount", DisplayName = "Defect Count", DataType = PortDataType.Integer },
                new() { Name = "DefectArea", DisplayName = "Defect Area", DataType = PortDataType.Float }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Method", DisplayName = "Method", DataType = "enum", DefaultValue = "GradientMagnitude", Options = new List<ParameterOption>
                {
                    new() { Label = "GradientMagnitude", Value = "GradientMagnitude" },
                    new() { Label = "ReferenceDiff", Value = "ReferenceDiff" },
                    new() { Label = "LocalContrast", Value = "LocalContrast" }
                } },
                new() { Name = "Threshold", DisplayName = "Threshold", DataType = "double", DefaultValue = 35.0, MinValue = 0.0, MaxValue = 255.0 },
                new() { Name = "MinArea", DisplayName = "Min Area", DataType = "int", DefaultValue = 20, MinValue = 0, MaxValue = 10000000 },
                new() { Name = "MaxArea", DisplayName = "Max Area", DataType = "int", DefaultValue = 1000000, MinValue = 0, MaxValue = 10000000 },
                new() { Name = "MorphCleanSize", DisplayName = "Morph Clean Size", DataType = "int", DefaultValue = 3, MinValue = 1, MaxValue = 301 }
            }
        };

        _metadata[OperatorType.EdgePairDefect] = new OperatorMetadata
        {
            Type = OperatorType.EdgePairDefect,
            DisplayName = "边缘对缺陷",
            Description = "Checks edge-pair spacing deviations against expected width.",
            Category = "AI检测",
            IconName = "edge-pair-defect",
            Keywords = new[] { "edge pair", "notch", "bump", "deviation" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "Line1", DisplayName = "Line 1", DataType = PortDataType.LineData, IsRequired = false },
                new() { Name = "Line2", DisplayName = "Line 2", DataType = PortDataType.LineData, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image },
                new() { Name = "DefectCount", DisplayName = "Defect Count", DataType = PortDataType.Integer },
                new() { Name = "MaxDeviation", DisplayName = "Max Deviation", DataType = PortDataType.Float },
                new() { Name = "Deviations", DisplayName = "Deviations", DataType = PortDataType.Any }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "ExpectedWidth", DisplayName = "Expected Width", DataType = "double", DefaultValue = 20.0, MinValue = 0.0, MaxValue = 100000.0 },
                new() { Name = "Tolerance", DisplayName = "Tolerance", DataType = "double", DefaultValue = 2.0, MinValue = 0.0, MaxValue = 100000.0 },
                new() { Name = "NumSamples", DisplayName = "Sample Count", DataType = "int", DefaultValue = 100, MinValue = 5, MaxValue = 5000 },
                new() { Name = "EdgeMethod", DisplayName = "Edge Method", DataType = "enum", DefaultValue = "Canny", Options = new List<ParameterOption>
                {
                    new() { Label = "Canny", Value = "Canny" },
                    new() { Label = "Sobel", Value = "Sobel" }
                } }
            }
        };

        _metadata[OperatorType.RectangleDetection] = new OperatorMetadata
        {
            Type = OperatorType.RectangleDetection,
            DisplayName = "矩形检测",
            Description = "Detects rectangular/quadrilateral objects from contours.",
            Category = "定位",
            IconName = "rectangle",
            Keywords = new[] { "rectangle", "quadrilateral", "box", "locate" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image },
                new() { Name = "Rectangles", DisplayName = "Rectangles", DataType = PortDataType.Any },
                new() { Name = "Count", DisplayName = "Count", DataType = PortDataType.Integer },
                new() { Name = "Center", DisplayName = "Center", DataType = PortDataType.Point },
                new() { Name = "Angle", DisplayName = "Angle", DataType = PortDataType.Float },
                new() { Name = "Width", DisplayName = "Width", DataType = PortDataType.Float },
                new() { Name = "Height", DisplayName = "Height", DataType = PortDataType.Float }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "MinArea", DisplayName = "Min Area", DataType = "int", DefaultValue = 100, MinValue = 0, MaxValue = 100000000 },
                new() { Name = "MaxArea", DisplayName = "Max Area", DataType = "int", DefaultValue = 10000000, MinValue = 0, MaxValue = 100000000 },
                new() { Name = "AngleTolerance", DisplayName = "Angle Tolerance", DataType = "double", DefaultValue = 15.0, MinValue = 0.0, MaxValue = 90.0 },
                new() { Name = "ApproxEpsilon", DisplayName = "Approx Epsilon", DataType = "double", DefaultValue = 0.02, MinValue = 0.0001, MaxValue = 1000.0 }
            }
        };

        _metadata[OperatorType.TranslationRotationCalibration] = new OperatorMetadata
        {
            Type = OperatorType.TranslationRotationCalibration,
            DisplayName = "平移旋转标定",
            Description = "Fits image-to-robot transform from calibration point pairs.",
            Category = "标定",
            IconName = "calibration",
            Keywords = new[] { "calibration", "hand-eye", "translation", "rotation" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "TransformMatrix", DisplayName = "Transform Matrix", DataType = PortDataType.Any },
                new() { Name = "RotationCenter", DisplayName = "Rotation Center", DataType = PortDataType.Point },
                new() { Name = "CalibrationError", DisplayName = "Calibration Error", DataType = PortDataType.Float }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "CalibrationPoints", DisplayName = "Calibration Points", DataType = "string", DefaultValue = "[]" },
                new() { Name = "Method", DisplayName = "Method", DataType = "enum", DefaultValue = "LeastSquares", Options = new List<ParameterOption>
                {
                    new() { Label = "LeastSquares", Value = "LeastSquares" },
                    new() { Label = "SVD", Value = "SVD" }
                } },
                new() { Name = "SavePath", DisplayName = "Save Path", DataType = "file", DefaultValue = "" }
            }
        };

        // ==================== Phase 3 Operators ====================

        _metadata[OperatorType.CornerDetection] = new OperatorMetadata
        {
            Type = OperatorType.CornerDetection,
            DisplayName = "角点检测",
            Description = "Detects corner points using Harris or Shi-Tomasi.",
            Category = "定位",
            IconName = "corner",
            Keywords = new[] { "corner", "vertex", "harris", "shitomasi" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image },
                new() { Name = "Corners", DisplayName = "Corners", DataType = PortDataType.PointList },
                new() { Name = "Count", DisplayName = "Count", DataType = PortDataType.Integer }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Method", DisplayName = "Method", DataType = "enum", DefaultValue = "ShiTomasi", Options = new List<ParameterOption>
                {
                    new() { Label = "Harris", Value = "Harris" },
                    new() { Label = "ShiTomasi", Value = "ShiTomasi" }
                } },
                new() { Name = "MaxCorners", DisplayName = "Max Corners", DataType = "int", DefaultValue = 100, MinValue = 1, MaxValue = 5000 },
                new() { Name = "QualityLevel", DisplayName = "Quality Level", DataType = "double", DefaultValue = 0.01, MinValue = 0.000001, MaxValue = 1.0 },
                new() { Name = "MinDistance", DisplayName = "Min Distance", DataType = "double", DefaultValue = 10.0, MinValue = 0.0, MaxValue = 10000.0 },
                new() { Name = "BlockSize", DisplayName = "Block Size", DataType = "int", DefaultValue = 3, MinValue = 2, MaxValue = 31 }
            }
        };

        _metadata[OperatorType.EdgeIntersection] = new OperatorMetadata
        {
            Type = OperatorType.EdgeIntersection,
            DisplayName = "边线交点",
            Description = "Computes line intersection and angle between two lines.",
            Category = "定位",
            IconName = "intersection",
            Keywords = new[] { "intersection", "cross point", "line angle" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Line1", DisplayName = "Line 1", DataType = PortDataType.LineData, IsRequired = true },
                new() { Name = "Line2", DisplayName = "Line 2", DataType = PortDataType.LineData, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Point", DisplayName = "Point", DataType = PortDataType.Point },
                new() { Name = "Angle", DisplayName = "Angle", DataType = PortDataType.Float },
                new() { Name = "HasIntersection", DisplayName = "Has Intersection", DataType = PortDataType.Boolean }
            }
        };

        _metadata[OperatorType.ParallelLineFind] = new OperatorMetadata
        {
            Type = OperatorType.ParallelLineFind,
            DisplayName = "平行线查找",
            Description = "Finds best pair of near-parallel lines in an image.",
            Category = "定位",
            IconName = "parallel",
            Keywords = new[] { "parallel", "dual edge", "rails" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image },
                new() { Name = "Line1", DisplayName = "Line 1", DataType = PortDataType.LineData },
                new() { Name = "Line2", DisplayName = "Line 2", DataType = PortDataType.LineData },
                new() { Name = "Distance", DisplayName = "Distance", DataType = PortDataType.Float },
                new() { Name = "Angle", DisplayName = "Angle", DataType = PortDataType.Float },
                new() { Name = "PairCount", DisplayName = "Pair Count", DataType = PortDataType.Integer }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "AngleTolerance", DisplayName = "Angle Tolerance", DataType = "double", DefaultValue = 5.0, MinValue = 0.0, MaxValue = 45.0 },
                new() { Name = "MinLength", DisplayName = "Min Length", DataType = "double", DefaultValue = 40.0, MinValue = 1.0, MaxValue = 100000.0 },
                new() { Name = "MinDistance", DisplayName = "Min Distance", DataType = "double", DefaultValue = 2.0, MinValue = 0.0, MaxValue = 100000.0 },
                new() { Name = "MaxDistance", DisplayName = "Max Distance", DataType = "double", DefaultValue = 200.0, MinValue = 0.0, MaxValue = 100000.0 }
            }
        };

        _metadata[OperatorType.QuadrilateralFind] = new OperatorMetadata
        {
            Type = OperatorType.QuadrilateralFind,
            DisplayName = "四边形查找",
            Description = "Finds quadrilateral contours without right-angle constraints.",
            Category = "定位",
            IconName = "quadrilateral",
            Keywords = new[] { "quadrilateral", "polygon", "trapezoid" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image },
                new() { Name = "Vertices", DisplayName = "Vertices", DataType = PortDataType.PointList },
                new() { Name = "Count", DisplayName = "Count", DataType = PortDataType.Integer },
                new() { Name = "Area", DisplayName = "Area", DataType = PortDataType.Float },
                new() { Name = "Center", DisplayName = "Center", DataType = PortDataType.Point }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "MinArea", DisplayName = "Min Area", DataType = "int", DefaultValue = 100, MinValue = 0, MaxValue = 100000000 },
                new() { Name = "MaxArea", DisplayName = "Max Area", DataType = "int", DefaultValue = 10000000, MinValue = 0, MaxValue = 100000000 },
                new() { Name = "ApproxEpsilon", DisplayName = "Approx Epsilon", DataType = "double", DefaultValue = 0.02, MinValue = 0.0001, MaxValue = 1000.0 },
                new() { Name = "ConvexOnly", DisplayName = "Convex Only", DataType = "bool", DefaultValue = false }
            }
        };

        _metadata[OperatorType.GeoMeasurement] = new OperatorMetadata
        {
            Type = OperatorType.GeoMeasurement,
            DisplayName = "几何测量",
            Description = "General geometry measurement between point/line/circle elements.",
            Category = "检测",
            IconName = "geometry",
            Keywords = new[] { "geometry", "point-line", "line-circle", "circle-circle" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Element1", DisplayName = "Element 1", DataType = PortDataType.Any, IsRequired = true },
                new() { Name = "Element2", DisplayName = "Element 2", DataType = PortDataType.Any, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Distance", DisplayName = "Distance", DataType = PortDataType.Float },
                new() { Name = "Angle", DisplayName = "Angle", DataType = PortDataType.Float },
                new() { Name = "Intersection1", DisplayName = "Intersection 1", DataType = PortDataType.Point },
                new() { Name = "Intersection2", DisplayName = "Intersection 2", DataType = PortDataType.Point },
                new() { Name = "MeasureType", DisplayName = "Measure Type", DataType = PortDataType.String }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Element1Type", DisplayName = "Element1 Type", DataType = "enum", DefaultValue = "Auto", Options = new List<ParameterOption>
                {
                    new() { Label = "Auto", Value = "Auto" },
                    new() { Label = "Point", Value = "Point" },
                    new() { Label = "Line", Value = "Line" },
                    new() { Label = "Circle", Value = "Circle" }
                } },
                new() { Name = "Element2Type", DisplayName = "Element2 Type", DataType = "enum", DefaultValue = "Auto", Options = new List<ParameterOption>
                {
                    new() { Label = "Auto", Value = "Auto" },
                    new() { Label = "Point", Value = "Point" },
                    new() { Label = "Line", Value = "Line" },
                    new() { Label = "Circle", Value = "Circle" }
                } }
            }
        };

        _metadata[OperatorType.ImageStitching] = new OperatorMetadata
        {
            Type = OperatorType.ImageStitching,
            DisplayName = "图像拼接",
            Description = "Stitches two images into a larger panorama-like output.",
            Category = "图像处理",
            IconName = "stitch",
            Keywords = new[] { "stitch", "panorama", "merge image" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image1", DisplayName = "Image 1", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "Image2", DisplayName = "Image 2", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image },
                new() { Name = "OverlapRatio", DisplayName = "Overlap Ratio", DataType = PortDataType.Float }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Method", DisplayName = "Method", DataType = "enum", DefaultValue = "FeatureBased", Options = new List<ParameterOption>
                {
                    new() { Label = "FeatureBased", Value = "FeatureBased" },
                    new() { Label = "Manual", Value = "Manual" }
                } },
                new() { Name = "OverlapPercent", DisplayName = "Overlap Percent", DataType = "double", DefaultValue = 20.0, MinValue = 0.0, MaxValue = 90.0 },
                new() { Name = "BlendMode", DisplayName = "Blend Mode", DataType = "enum", DefaultValue = "Linear", Options = new List<ParameterOption>
                {
                    new() { Label = "Linear", Value = "Linear" },
                    new() { Label = "MultiBand", Value = "MultiBand" }
                } }
            }
        };

        _metadata[OperatorType.ImageTiling] = new OperatorMetadata
        {
            Type = OperatorType.ImageTiling,
            DisplayName = "图像切片",
            Description = "Splits an image into tiled regions with optional overlap.",
            Category = "拆分组合",
            IconName = "tile",
            Keywords = new[] { "tile", "grid", "split image" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Tiles", DisplayName = "Tiles", DataType = PortDataType.Any },
                new() { Name = "Count", DisplayName = "Count", DataType = PortDataType.Integer },
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Rows", DisplayName = "Rows", DataType = "int", DefaultValue = 2, MinValue = 1, MaxValue = 100 },
                new() { Name = "Cols", DisplayName = "Cols", DataType = "int", DefaultValue = 2, MinValue = 1, MaxValue = 100 },
                new() { Name = "Overlap", DisplayName = "Overlap", DataType = "int", DefaultValue = 0, MinValue = 0, MaxValue = 10000 },
                new() { Name = "OutputMode", DisplayName = "Output Mode", DataType = "enum", DefaultValue = "Array", Options = new List<ParameterOption>
                {
                    new() { Label = "Array", Value = "Array" },
                    new() { Label = "Sequential", Value = "Sequential" }
                } }
            }
        };

        _metadata[OperatorType.ImageNormalize] = new OperatorMetadata
        {
            Type = OperatorType.ImageNormalize,
            DisplayName = "图像归一化",
            Description = "Normalizes pixel distribution for robust downstream processing.",
            Category = "预处理",
            IconName = "normalize",
            Keywords = new[] { "normalize", "minmax", "zscore", "equalize" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Method", DisplayName = "Method", DataType = "enum", DefaultValue = "MinMax", Options = new List<ParameterOption>
                {
                    new() { Label = "MinMax", Value = "MinMax" },
                    new() { Label = "ZScore", Value = "ZScore" },
                    new() { Label = "Histogram", Value = "Histogram" }
                } },
                new() { Name = "Alpha", DisplayName = "Alpha", DataType = "double", DefaultValue = 0.0 },
                new() { Name = "Beta", DisplayName = "Beta", DataType = "double", DefaultValue = 255.0 }
            }
        };

        _metadata[OperatorType.ImageCompose] = new OperatorMetadata
        {
            Type = OperatorType.ImageCompose,
            DisplayName = "图像组合",
            Description = "Composes multiple images by concat/grid/channel merge.",
            Category = "拆分组合",
            IconName = "compose",
            Keywords = new[] { "compose", "concat", "grid", "merge channels" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image1", DisplayName = "Image 1", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "Image2", DisplayName = "Image 2", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "Image3", DisplayName = "Image 3", DataType = PortDataType.Image, IsRequired = false },
                new() { Name = "Image4", DisplayName = "Image 4", DataType = PortDataType.Image, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Mode", DisplayName = "Mode", DataType = "enum", DefaultValue = "Horizontal", Options = new List<ParameterOption>
                {
                    new() { Label = "Horizontal", Value = "Horizontal" },
                    new() { Label = "Vertical", Value = "Vertical" },
                    new() { Label = "Grid", Value = "Grid" },
                    new() { Label = "ChannelMerge", Value = "ChannelMerge" }
                } },
                new() { Name = "Padding", DisplayName = "Padding", DataType = "int", DefaultValue = 0, MinValue = 0, MaxValue = 1000 },
                new() { Name = "BackgroundColor", DisplayName = "Background Color", DataType = "string", DefaultValue = "#000000" }
            }
        };

        _metadata[OperatorType.CopyMakeBorder] = new OperatorMetadata
        {
            Type = OperatorType.CopyMakeBorder,
            DisplayName = "边界填充",
            Description = "Pads image border using OpenCV border policies.",
            Category = "图像处理",
            IconName = "border",
            Keywords = new[] { "border", "pad", "copy make border" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Top", DisplayName = "Top", DataType = "int", DefaultValue = 0, MinValue = 0, MaxValue = 10000 },
                new() { Name = "Bottom", DisplayName = "Bottom", DataType = "int", DefaultValue = 0, MinValue = 0, MaxValue = 10000 },
                new() { Name = "Left", DisplayName = "Left", DataType = "int", DefaultValue = 0, MinValue = 0, MaxValue = 10000 },
                new() { Name = "Right", DisplayName = "Right", DataType = "int", DefaultValue = 0, MinValue = 0, MaxValue = 10000 },
                new() { Name = "BorderType", DisplayName = "Border Type", DataType = "enum", DefaultValue = "Constant", Options = new List<ParameterOption>
                {
                    new() { Label = "Constant", Value = "Constant" },
                    new() { Label = "Replicate", Value = "Replicate" },
                    new() { Label = "Reflect", Value = "Reflect" },
                    new() { Label = "Wrap", Value = "Wrap" }
                } },
                new() { Name = "Color", DisplayName = "Color", DataType = "string", DefaultValue = "#000000" }
            }
        };

        _metadata[OperatorType.TextSave] = new OperatorMetadata
        {
            Type = OperatorType.TextSave,
            DisplayName = "文本保存",
            Description = "Saves text or structured data to text/csv/json file.",
            Category = "逻辑工具",
            IconName = "save-text",
            Keywords = new[] { "save text", "export csv", "log", "json export" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Data", DisplayName = "Data", DataType = PortDataType.Any, IsRequired = false },
                new() { Name = "Text", DisplayName = "Text", DataType = PortDataType.String, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "FilePath", DisplayName = "File Path", DataType = PortDataType.String },
                new() { Name = "Success", DisplayName = "Success", DataType = PortDataType.Boolean }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "FilePath", DisplayName = "File Path", DataType = "file", DefaultValue = "output_{date}_{time}.txt" },
                new() { Name = "Format", DisplayName = "Format", DataType = "enum", DefaultValue = "Text", Options = new List<ParameterOption>
                {
                    new() { Label = "Text", Value = "Text" },
                    new() { Label = "CSV", Value = "CSV" },
                    new() { Label = "JSON", Value = "JSON" }
                } },
                new() { Name = "AppendMode", DisplayName = "Append Mode", DataType = "bool", DefaultValue = true },
                new() { Name = "AddTimestamp", DisplayName = "Add Timestamp", DataType = "bool", DefaultValue = true },
                new() { Name = "Encoding", DisplayName = "Encoding", DataType = "enum", DefaultValue = "UTF8", Options = new List<ParameterOption>
                {
                    new() { Label = "UTF8", Value = "UTF8" },
                    new() { Label = "GBK", Value = "GBK" }
                } }
            }
        };

        _metadata[OperatorType.PointSetTool] = new OperatorMetadata
        {
            Type = OperatorType.PointSetTool,
            DisplayName = "点集工具",
            Description = "Merges/sorts/filters point lists and computes set properties.",
            Category = "逻辑工具",
            IconName = "point-set",
            Keywords = new[] { "point set", "sort points", "convex hull", "bounding rect" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Points1", DisplayName = "Points 1", DataType = PortDataType.PointList, IsRequired = true },
                new() { Name = "Points2", DisplayName = "Points 2", DataType = PortDataType.PointList, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Points", DisplayName = "Points", DataType = PortDataType.PointList },
                new() { Name = "Count", DisplayName = "Count", DataType = PortDataType.Integer },
                new() { Name = "Center", DisplayName = "Center", DataType = PortDataType.Point },
                new() { Name = "BoundingBox", DisplayName = "Bounding Box", DataType = PortDataType.Rectangle }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Operation", DisplayName = "Operation", DataType = "enum", DefaultValue = "Merge", Options = new List<ParameterOption>
                {
                    new() { Label = "Merge", Value = "Merge" },
                    new() { Label = "Sort", Value = "Sort" },
                    new() { Label = "Filter", Value = "Filter" },
                    new() { Label = "ConvexHull", Value = "ConvexHull" },
                    new() { Label = "BoundingRect", Value = "BoundingRect" }
                } },
                new() { Name = "SortBy", DisplayName = "Sort By", DataType = "enum", DefaultValue = "X", Options = new List<ParameterOption>
                {
                    new() { Label = "X", Value = "X" },
                    new() { Label = "Y", Value = "Y" },
                    new() { Label = "Distance", Value = "Distance" }
                } },
                new() { Name = "FilterMinX", DisplayName = "Filter Min X", DataType = "double", DefaultValue = -1e9 },
                new() { Name = "FilterMinY", DisplayName = "Filter Min Y", DataType = "double", DefaultValue = -1e9 },
                new() { Name = "FilterMaxX", DisplayName = "Filter Max X", DataType = "double", DefaultValue = 1e9 },
                new() { Name = "FilterMaxY", DisplayName = "Filter Max Y", DataType = "double", DefaultValue = 1e9 }
            }
        };

        _metadata[OperatorType.BlobLabeling] = new OperatorMetadata
        {
            Type = OperatorType.BlobLabeling,
            DisplayName = "连通域标注",
            Description = "Classifies connected blobs by geometric features and draws labels.",
            Category = "定位",
            IconName = "blob-label",
            Keywords = new[] { "blob", "label", "classify connected component" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "Blobs", DisplayName = "Blobs", DataType = PortDataType.Contour, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image },
                new() { Name = "Labels", DisplayName = "Labels", DataType = PortDataType.Any },
                new() { Name = "Count", DisplayName = "Count", DataType = PortDataType.Integer }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "LabelBy", DisplayName = "Label By", DataType = "enum", DefaultValue = "Area", Options = new List<ParameterOption>
                {
                    new() { Label = "Area", Value = "Area" },
                    new() { Label = "Circularity", Value = "Circularity" },
                    new() { Label = "AspectRatio", Value = "AspectRatio" },
                    new() { Label = "Position", Value = "Position" }
                } },
                new() { Name = "Thresholds", DisplayName = "Thresholds", DataType = "string", DefaultValue = "[]" },
                new() { Name = "DrawLabels", DisplayName = "Draw Labels", DataType = "bool", DefaultValue = true }
            }
        };

        _metadata[OperatorType.HistogramAnalysis] = new OperatorMetadata
        {
            Type = OperatorType.HistogramAnalysis,
            DisplayName = "直方图分析",
            Description = "Computes histogram and distribution statistics for selected channel.",
            Category = "检测",
            IconName = "histogram",
            Keywords = new[] { "histogram", "distribution", "peak", "median" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image },
                new() { Name = "Mean", DisplayName = "Mean", DataType = PortDataType.Float },
                new() { Name = "StdDev", DisplayName = "StdDev", DataType = PortDataType.Float },
                new() { Name = "Mode", DisplayName = "Mode", DataType = PortDataType.Integer },
                new() { Name = "Median", DisplayName = "Median", DataType = PortDataType.Integer },
                new() { Name = "Peak", DisplayName = "Peak", DataType = PortDataType.Integer },
                new() { Name = "Valley", DisplayName = "Valley", DataType = PortDataType.Integer }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Channel", DisplayName = "Channel", DataType = "enum", DefaultValue = "Gray", Options = new List<ParameterOption>
                {
                    new() { Label = "Gray", Value = "Gray" },
                    new() { Label = "R", Value = "R" },
                    new() { Label = "G", Value = "G" },
                    new() { Label = "B", Value = "B" }
                } },
                new() { Name = "BinCount", DisplayName = "Bin Count", DataType = "int", DefaultValue = 256, MinValue = 2, MaxValue = 1024 },
                new() { Name = "RoiX", DisplayName = "ROI X", DataType = "int", DefaultValue = 0, MinValue = 0 },
                new() { Name = "RoiY", DisplayName = "ROI Y", DataType = "int", DefaultValue = 0, MinValue = 0 },
                new() { Name = "RoiW", DisplayName = "ROI W", DataType = "int", DefaultValue = 0, MinValue = 0 },
                new() { Name = "RoiH", DisplayName = "ROI H", DataType = "int", DefaultValue = 0, MinValue = 0 }
            }
        };

        _metadata[OperatorType.PixelStatistics] = new OperatorMetadata
        {
            Type = OperatorType.PixelStatistics,
            DisplayName = "像素统计",
            Description = "Computes ROI/masked pixel-level statistics.",
            Category = "检测",
            IconName = "pixel-stats",
            Keywords = new[] { "pixel statistics", "mean", "stddev", "min max", "non-zero" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "Mask", DisplayName = "Mask", DataType = PortDataType.Image, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Mean", DisplayName = "Mean", DataType = PortDataType.Float },
                new() { Name = "StdDev", DisplayName = "StdDev", DataType = PortDataType.Float },
                new() { Name = "Min", DisplayName = "Min", DataType = PortDataType.Integer },
                new() { Name = "Max", DisplayName = "Max", DataType = PortDataType.Integer },
                new() { Name = "Median", DisplayName = "Median", DataType = PortDataType.Integer },
                new() { Name = "NonZeroCount", DisplayName = "NonZero Count", DataType = PortDataType.Integer }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "RoiX", DisplayName = "ROI X", DataType = "int", DefaultValue = 0, MinValue = 0 },
                new() { Name = "RoiY", DisplayName = "ROI Y", DataType = "int", DefaultValue = 0, MinValue = 0 },
                new() { Name = "RoiW", DisplayName = "ROI W", DataType = "int", DefaultValue = 0, MinValue = 0 },
                new() { Name = "RoiH", DisplayName = "ROI H", DataType = "int", DefaultValue = 0, MinValue = 0 },
                new() { Name = "Channel", DisplayName = "Channel", DataType = "enum", DefaultValue = "Gray", Options = new List<ParameterOption>
                {
                    new() { Label = "Gray", Value = "Gray" },
                    new() { Label = "R", Value = "R" },
                    new() { Label = "G", Value = "G" },
                    new() { Label = "B", Value = "B" },
                    new() { Label = "All", Value = "All" }
                } }
            }
        };

        _metadata[OperatorType.Filtering] = new OperatorMetadata
        {
            Type = OperatorType.Filtering,
            DisplayName = "滤波",
            Description = "利用高斯/均值核去除图像噪声，适用于金属表面、PCB板等工业场景预处理",
            Category = "预处理",
            IconName = "filter",
            Keywords = new[] { "滤波", "降噪", "平滑", "模糊", "去噪", "高斯", "均值", "Filter", "Blur", "Denoise" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "KernelSize", DisplayName = "核大小", DataType = "int", DefaultValue = 5, MinValue = 1, MaxValue = 31 },
                new() { Name = "SigmaX", DisplayName = "Sigma X", DataType = "double", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 10.0 },
                new() { Name = "SigmaY", DisplayName = "Sigma Y", DataType = "double", DefaultValue = 0.0, MinValue = 0.0, MaxValue = 10.0 },
                new() { Name = "BorderType", DisplayName = "边界填充", DataType = "enum", DefaultValue = "4", Options = new List<ParameterOption>
                {
                    new() { Label = "Constant", Value = "0" },
                    new() { Label = "Replicate", Value = "1" },
                    new() { Label = "Reflect", Value = "2" },
                    new() { Label = "Wrap", Value = "3" },
                    new() { Label = "Default", Value = "4" }
                } }
            }
        };

        // 边缘检测
        _metadata[OperatorType.EdgeDetection] = new OperatorMetadata
        {
            Type = OperatorType.EdgeDetection,
            DisplayName = "边缘检测",
            Description = "利用 Canny/Sobel 等算法检测图像边缘，用于尺寸测量和缺陷定位前的轮廓提取",
            Category = "特征提取",
            IconName = "edge",
            Keywords = new[] { "边缘", "轮廓提取", "Canny", "Sobel", "边界", "找边", "Edge", "Contour extraction" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image },
                new() { Name = "Edges", DisplayName = "边缘", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Threshold1", DisplayName = "低阈值", DataType = "double", DefaultValue = 50.0, MinValue = 0.0, MaxValue = 255.0 },
                new() { Name = "Threshold2", DisplayName = "高阈值", DataType = "double", DefaultValue = 150.0, MinValue = 0.0, MaxValue = 255.0 },
                new() { Name = "AutoThreshold", DisplayName = "Auto Threshold", DataType = "bool", DefaultValue = false },
                new() { Name = "AutoThresholdSigma", DisplayName = "Auto Threshold Sigma", DataType = "double", DefaultValue = 0.33, MinValue = 0.01, MaxValue = 1.0 },
                new() { Name = "EnableGaussianBlur", DisplayName = "启用高斯模糊", DataType = "bool", DefaultValue = true },
                new() { Name = "GaussianKernelSize", DisplayName = "高斯核大小", DataType = "int", DefaultValue = 5, MinValue = 3, MaxValue = 15 },
                new() { Name = "ApertureSize", DisplayName = "Sobel孔径", DataType = "enum", DefaultValue = "3", Options = new List<ParameterOption>
                {
                    new() { Label = "3", Value = "3" },
                    new() { Label = "5", Value = "5" },
                    new() { Label = "7", Value = "7" }
                } }
            }
        };

        // 二值化
        _metadata[OperatorType.Thresholding] = new OperatorMetadata
        {
            Type = OperatorType.Thresholding,
            DisplayName = "二值化",
            Description = "全局/自适应/Otsu 二值化分割，将图像转为前景/背景二值图，用于缺陷区域分离",
            Category = "预处理",
            IconName = "threshold",
            Keywords = new[] { "二值化", "阈值", "分割", "黑白", "Otsu", "Threshold", "Binarize", "Segmentation" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Threshold", DisplayName = "阈值", DataType = "double", DefaultValue = 127.0, MinValue = 0.0, MaxValue = 255.0 },
                new() { Name = "MaxValue", DisplayName = "最大值", DataType = "double", DefaultValue = 255.0, MinValue = 0.0, MaxValue = 255.0 },
                new() { Name = "Type", DisplayName = "类型", DataType = "enum", DefaultValue = "0", Options = new List<ParameterOption>
                {
                    new() { Label = "Binary", Value = "0" },
                    new() { Label = "Binary Inv", Value = "1" },
                    new() { Label = "Trunc", Value = "2" },
                    new() { Label = "To Zero", Value = "3" },
                    new() { Label = "To Zero Inv", Value = "4" },
                    new() { Label = "Otsu", Value = "8" },
                    new() { Label = "Triangle", Value = "16" }
                } },
                new() { Name = "UseOtsu", DisplayName = "使用Otsu", DataType = "bool", DefaultValue = false }
            }
        };

        // 形态学
        _metadata[OperatorType.Morphology] = new OperatorMetadata
        {
            Type = OperatorType.Morphology,
            DisplayName = "Morphology (Legacy)",
            Description = "Legacy morphology node. Use Morphological Operation for new workflows.",
            Category = "预处理",
            IconName = "morphology",
            Keywords = new[] { "形态学", "膨胀", "腐蚀", "开运算", "闭运算", "Morphology", "Erode", "Dilate" },
            Tags = new[] { "Legacy", "Deprecated", "Compatibility" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Operation", DisplayName = "操作类型", DataType = "string", DefaultValue = "Erode" },
                new() { Name = "KernelSize", DisplayName = "核大小", DataType = "int", DefaultValue = 3, MinValue = 1, MaxValue = 21 },
                new() { Name = "KernelShape", DisplayName = "核形状", DataType = "string", DefaultValue = "Rect" },
                new() { Name = "Iterations", DisplayName = "迭代次数", DataType = "int", DefaultValue = 1, MinValue = 1, MaxValue = 10 },
                new() { Name = "AnchorX", DisplayName = "锚点X", DataType = "int", DefaultValue = -1 },
                new() { Name = "AnchorY", DisplayName = "锚点Y", DataType = "int", DefaultValue = -1 }
            }
        };

        // Blob分析

        // Blob分析
        _metadata[OperatorType.BlobAnalysis] = new OperatorMetadata
        {
            Type = OperatorType.BlobAnalysis,
            DisplayName = "Blob分析",
            Description = "连通区域分析",
            Category = "特征提取",
            IconName = "blob",
            Keywords = new[] { "连通域", "缺陷区域", "斑点", "面积提取", "缺陷分析", "Blob", "Connected components" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "标记图像", DataType = PortDataType.Image },
                new() { Name = "Blobs", DisplayName = "Blob数据", DataType = PortDataType.Contour },
                new() { Name = "BlobCount", DisplayName = "Blob数量", DataType = PortDataType.Integer }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "MinArea", DisplayName = "最小面积", DataType = "int", DefaultValue = 100, MinValue = 0 },
                new() { Name = "MaxArea", DisplayName = "最大面积", DataType = "int", DefaultValue = 100000, MinValue = 0 },
                new() { Name = "Color", DisplayName = "目标颜色", DataType = "enum", DefaultValue = "White", Options = new List<ParameterOption> { new() { Label = "白色", Value = "White" }, new() { Label = "黑色", Value = "Black" } } }
            }
        };

        // 模板匹配
        _metadata[OperatorType.TemplateMatching] = new OperatorMetadata
        {
            Type = OperatorType.TemplateMatching,
            DisplayName = "模板匹配",
            Description = "NCC/SQDiff 模板匹配，用于定位目标位置和缺失检测",
            Category = "匹配定位",
            IconName = "template",
            Keywords = new[] { "模板匹配", "定位", "找图", "特征匹配", "关联定位", "Template", "Match", "Locate" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "Template", DisplayName = "模板图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "Position", DisplayName = "匹配位置", DataType = PortDataType.Point },
                new() { Name = "Score", DisplayName = "匹配分数", DataType = PortDataType.Float },
                new() { Name = "IsMatch", DisplayName = "是否匹配", DataType = PortDataType.Boolean }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Method", DisplayName = "匹配方法", DataType = "enum", DefaultValue = "NCC", Options = new List<ParameterOption> { new() { Label = "归一化相关 (NCC)", Value = "NCC" }, new() { Label = "平方差 (SQDiff)", Value = "SQDiff" } } },
                new() { Name = "Threshold", DisplayName = "匹配分数阈值", DataType = "double", DefaultValue = 0.8, MinValue = 0.1, MaxValue = 1.0 },
                new() { Name = "MaxMatches", DisplayName = "最大匹配数", DataType = "int", DefaultValue = 1, MinValue = 1, MaxValue = 100 }
            }
        };

        // 测量
        _metadata[OperatorType.Measurement] = new OperatorMetadata
        {
            Type = OperatorType.Measurement,
            DisplayName = "测量",
            Description = "两点/水平/垂直距离测量，支持图像坐标和 Point 输入两种模式，用于尺寸检测",
            Category = "检测",
            IconName = "measure",
            Keywords = new[] { "测量", "距离", "长度", "卡尺", "尺寸", "两点间距", "Measure", "Distance", "Length", "Size" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = false },
                new() { Name = "PointA", DisplayName = "起点", DataType = PortDataType.Point, IsRequired = false },
                new() { Name = "PointB", DisplayName = "终点", DataType = PortDataType.Point, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "Distance", DisplayName = "测量距离", DataType = PortDataType.Float }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "X1", DisplayName = "起点X", DataType = "int", DefaultValue = 0 },
                new() { Name = "Y1", DisplayName = "起点Y", DataType = "int", DefaultValue = 0 },
                new() { Name = "X2", DisplayName = "终点X", DataType = "int", DefaultValue = 100 },
                new() { Name = "Y2", DisplayName = "终点Y", DataType = "int", DefaultValue = 100 },
                new() { Name = "MeasureType", DisplayName = "测量类型", DataType = "enum", DefaultValue = "PointToPoint", Options = new List<ParameterOption> { new() { Label = "点到点", Value = "PointToPoint" }, new() { Label = "水平", Value = "Horizontal" }, new() { Label = "垂直", Value = "Vertical" } } }
            }
        };

        // 轮廓检测
        _metadata[OperatorType.ContourDetection] = new OperatorMetadata
        {
            Type = OperatorType.ContourDetection,
            DisplayName = "轮廓检测",
            Description = "查找图像轮廓，提取边缘点集和层次关系，供后续测量和拟合使用",
            Category = "特征提取",
            IconName = "contour",
            Keywords = new[] { "轮廓", "边界", "形状", "多边形", "边缘点", "Contour", "Shape", "Boundary" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "Contours", DisplayName = "轮廓数据", DataType = PortDataType.Contour },
                new() { Name = "ContourCount", DisplayName = "轮廓数量", DataType = PortDataType.Integer }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Mode", DisplayName = "检索模式", DataType = "enum", DefaultValue = "External", Options = new List<ParameterOption> { new() { Label = "外部", Value = "External" }, new() { Label = "列表", Value = "List" }, new() { Label = "树", Value = "Tree" } } },
                new() { Name = "Method", DisplayName = "近似方法", DataType = "enum", DefaultValue = "Simple", Options = new List<ParameterOption> { new() { Label = "简单", Value = "Simple" }, new() { Label = "无", Value = "None" } } },
                new() { Name = "MinArea", DisplayName = "最小面积", DataType = "int", DefaultValue = 100 },
                new() { Name = "MaxArea", DisplayName = "最大面积", DataType = "int", DefaultValue = 100000 },
                new() { Name = "Threshold", DisplayName = "二值化阈值", DataType = "double", DefaultValue = 127.0 }
            }
        };

        // 条码识别
        _metadata[OperatorType.CodeRecognition] = new OperatorMetadata
        {
            Type = OperatorType.CodeRecognition,
            DisplayName = "条码识别",
            Description = "一维码/二维码识别，支持 QR、Code128、DataMatrix 等多种码制",
            Category = "识别",
            IconName = "barcode",
            Keywords = new[] { "条码", "二维码", "扫码", "识别", "QR", "读取", "Barcode", "Decode", "Read code" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "Text", DisplayName = "识别内容", DataType = PortDataType.String },
                new() { Name = "CodeCount", DisplayName = "识别数量", DataType = PortDataType.Integer },
                new() { Name = "CodeType", DisplayName = "条码类型", DataType = PortDataType.String }
            }
        };

        // 深度学习
        _metadata[OperatorType.DeepLearning] = new OperatorMetadata
        {
            Type = OperatorType.DeepLearning,
            DisplayName = "深度学习",
            Description = "AI 深度学习推理，支持 YOLOv5/v6/v8/v11 等模型，用于缺陷检测和目标分类",
            Category = "AI检测",
            IconName = "ai",
            Keywords = new[] { "深度学习", "AI", "模型", "推理", "缺陷识别", "目标检测", "YOLO", "判断瑕疵", "Deep learning" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "Defects", DisplayName = "缺陷列表", DataType = PortDataType.DetectionList },
                new() { Name = "DefectCount", DisplayName = "缺陷数量", DataType = PortDataType.Integer }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "ModelPath", DisplayName = "模型路径", DataType = "file", DefaultValue = "" },
                new() { Name = "Confidence", DisplayName = "置信度阈值", DataType = "double", DefaultValue = 0.5, MinValue = 0.0, MaxValue = 1.0 },
                new()
                {
                    Name = "ModelVersion",
                    DisplayName = "YOLO版本",
                    DataType = "enum",
                    DefaultValue = "Auto",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "自动检测", Value = "Auto" },
                        new() { Label = "YOLOv5", Value = "YOLOv5" },
                        new() { Label = "YOLOv6", Value = "YOLOv6" },
                        new() { Label = "YOLOv8", Value = "YOLOv8" },
                        new() { Label = "YOLOv11", Value = "YOLOv11" }
                    }
                },
                new() { Name = "InputSize", DisplayName = "输入尺寸", DataType = "int", DefaultValue = 640, MinValue = 320, MaxValue = 1280 },
                new() { Name = "TargetClasses", DisplayName = "目标类别", DataType = "string", DefaultValue = "", Description = "检测目标类别（逗号分隔，如 person,car），为空则检测所有类别" },
                new() { Name = "LabelFile", DisplayName = "标签文件路径", DataType = "file", DefaultValue = "", Description = "自定义标签文件路径（每行一个标签），为空则使用COCO 80类或自动查找模型目录下的labels.txt" }
            }
        };

        // 结果输出
        _metadata[OperatorType.ResultOutput] = new OperatorMetadata
        {
            Type = OperatorType.ResultOutput,
            DisplayName = "结果输出",
            Description = "汇总检测结果并输出，支持 JSON/CSV/Text 格式，可选保存到文件",
            Category = "输出",
            IconName = "output",
            Keywords = new[] { "输出", "结果", "结束", "呈现", "记录", "Output", "Result", "Display" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image, IsRequired = false },
                new() { Name = "Result", DisplayName = "结果", DataType = PortDataType.Any, IsRequired = false },
                new() { Name = "Text", DisplayName = "文本", DataType = PortDataType.String, IsRequired = false },
                new() { Name = "Data", DisplayName = "数据", DataType = PortDataType.Any, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Output", DisplayName = "输出", DataType = PortDataType.Any }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Format", DisplayName = "输出格式", DataType = "enum", DefaultValue = "JSON", Options = new List<ParameterOption> { new() { Label = "JSON", Value = "JSON" }, new() { Label = "CSV", Value = "CSV" }, new() { Label = "Text", Value = "Text" } } },
                new() { Name = "SaveToFile", DisplayName = "保存到文件", DataType = "bool", DefaultValue = true }
            }
        };

        // ==================== Phase 1 新增算子 ====================

        // 1. 中值滤波 (MedianBlur = 13)
        _metadata[OperatorType.MedianBlur] = new OperatorMetadata
        {
            Type = OperatorType.MedianBlur,
            DisplayName = "中值滤波",
            Description = "有效去除椒盐噪声同时保留边缘",
            Category = "预处理",
            IconName = "filter",
            Keywords = new[] { "中值", "滤波", "椒盐噪声", "去噪", "Median", "Filter", "Salt and pepper" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "KernelSize", DisplayName = "核大小", DataType = "int", DefaultValue = 5, MinValue = 1, MaxValue = 31 }
            }
        };

        // 2. 均值滤波 (MeanFilter = 215)
        _metadata[OperatorType.MeanFilter] = new OperatorMetadata
        {
            Type = OperatorType.MeanFilter,
            DisplayName = "均值滤波",
            Description = "Applies mean (box blur) filtering to smooth image noise.",
            Category = "预处理",
            IconName = "filter",
            Keywords = new[] { "mean filter", "box blur", "box filter", "smooth", "denoise" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "Image", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "KernelSize", DisplayName = "Kernel Size", DataType = "int", DefaultValue = 5, MinValue = 1, MaxValue = 63 },
                new() { Name = "BorderType", DisplayName = "Border Type", DataType = "enum", DefaultValue = "4", Options = new List<ParameterOption>
                {
                    new() { Label = "Constant", Value = "0" },
                    new() { Label = "Replicate", Value = "1" },
                    new() { Label = "Reflect", Value = "2" },
                    new() { Label = "Wrap", Value = "3" },
                    new() { Label = "Default", Value = "4" }
                } }
            }
        };

        // 2. 双边滤波 (BilateralFilter = 14)
        _metadata[OperatorType.BilateralFilter] = new OperatorMetadata
        {
            Type = OperatorType.BilateralFilter,
            DisplayName = "双边滤波",
            Description = "边缘保留的平滑滤波",
            Category = "预处理",
            IconName = "filter",
            Keywords = new[] { "双边", "滤波", "边缘保留", "平滑", "纹理", "Bilateral", "Edge-preserving" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Diameter", DisplayName = "直径", DataType = "int", DefaultValue = 9, MinValue = 1, MaxValue = 25 },
                new() { Name = "SigmaColor", DisplayName = "色彩Sigma", DataType = "double", DefaultValue = 75.0, MinValue = 1.0, MaxValue = 255.0 },
                new() { Name = "SigmaSpace", DisplayName = "空间Sigma", DataType = "double", DefaultValue = 75.0, MinValue = 1.0, MaxValue = 255.0 }
            }
        };

        // 3. 图像缩放 (ImageResize = 15)
        _metadata[OperatorType.ImageResize] = new OperatorMetadata
        {
            Type = OperatorType.ImageResize,
            DisplayName = "图像缩放",
            Description = "调整图像尺寸",
            Category = "预处理",
            IconName = "resize",
            Keywords = new[] { "缩放", "放大", "缩小", "尺寸", "Resize", "Scale", "Zoom" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Width", DisplayName = "目标宽度", DataType = "int", DefaultValue = 640, MinValue = 1, MaxValue = 8192 },
                new() { Name = "Height", DisplayName = "目标高度", DataType = "int", DefaultValue = 480, MinValue = 1, MaxValue = 8192 },
                new() { Name = "ScaleFactor", DisplayName = "缩放比例", DataType = "double", DefaultValue = 1.0, MinValue = 0.01, MaxValue = 10.0 },
                new() { Name = "Interpolation", DisplayName = "插值方法", DataType = "enum", DefaultValue = "Linear", Options = new List<ParameterOption>
                {
                    new() { Label = "最近邻", Value = "Nearest" },
                    new() { Label = "双线性", Value = "Linear" },
                    new() { Label = "三次", Value = "Cubic" },
                    new() { Label = "区域", Value = "Area" }
                } },
                new() { Name = "UseScale", DisplayName = "使用比例", DataType = "bool", DefaultValue = false }
            }
        };

        // 4. 图像裁剪 (ImageCrop = 16)
        _metadata[OperatorType.ImageCrop] = new OperatorMetadata
        {
            Type = OperatorType.ImageCrop,
            DisplayName = "图像裁剪",
            Description = "ROI区域提取",
            Category = "预处理",
            IconName = "crop",
            Keywords = new[] { "裁剪", "切割", "ROI", "区域提取", "Crop", "Region" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "X", DisplayName = "起始X", DataType = "int", DefaultValue = 0, MinValue = 0 },
                new() { Name = "Y", DisplayName = "起始Y", DataType = "int", DefaultValue = 0, MinValue = 0 },
                new() { Name = "Width", DisplayName = "宽度", DataType = "int", DefaultValue = 100, MinValue = 1 },
                new() { Name = "Height", DisplayName = "高度", DataType = "int", DefaultValue = 100, MinValue = 1 }
            }
        };

        // 5. 图像旋转 (ImageRotate = 17)
        _metadata[OperatorType.ImageRotate] = new OperatorMetadata
        {
            Type = OperatorType.ImageRotate,
            DisplayName = "图像旋转",
            Description = "任意角度旋转",
            Category = "预处理",
            IconName = "rotate",
            Keywords = new[] { "旋转", "角度", "翻转", "校正", "Rotate", "Angle", "Flip" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Angle", DisplayName = "旋转角度", DataType = "double", DefaultValue = 0.0, MinValue = -360.0, MaxValue = 360.0 },
                new() { Name = "CenterX", DisplayName = "中心X", DataType = "int", DefaultValue = -1 },
                new() { Name = "CenterY", DisplayName = "中心Y", DataType = "int", DefaultValue = -1 },
                new() { Name = "Scale", DisplayName = "缩放比例", DataType = "double", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 10.0 },
                new() { Name = "AutoResize", DisplayName = "自动调整尺寸", DataType = "bool", DefaultValue = true }
            }
        };

        // 6. 透视变换 (PerspectiveTransform = 18)
        _metadata[OperatorType.PerspectiveTransform] = new OperatorMetadata
        {
            Type = OperatorType.PerspectiveTransform,
            DisplayName = "透视变换",
            Description = "四边形透视校正",
            Category = "预处理",
            IconName = "perspective",
            Keywords = new[] { "透视", "变换", "矫正", "仿射", "四边形", "Perspective", "Warp", "Transform" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "SrcPoints", DisplayName = "源点集合", DataType = PortDataType.PointList, IsRequired = false },
                new() { Name = "DstPoints", DisplayName = "目标点集合", DataType = PortDataType.PointList, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "SrcPointsJson", DisplayName = "源点集合(JSON)", DataType = "string", DefaultValue = "" },
                new() { Name = "DstPointsJson", DisplayName = "目标点集合(JSON)", DataType = "string", DefaultValue = "" },
                new() { Name = "SrcX1", DisplayName = "源点1 X", DataType = "double", DefaultValue = 0.0 },
                new() { Name = "SrcY1", DisplayName = "源点1 Y", DataType = "double", DefaultValue = 0.0 },
                new() { Name = "SrcX2", DisplayName = "源点2 X", DataType = "double", DefaultValue = 100.0 },
                new() { Name = "SrcY2", DisplayName = "源点2 Y", DataType = "double", DefaultValue = 0.0 },
                new() { Name = "SrcX3", DisplayName = "源点3 X", DataType = "double", DefaultValue = 100.0 },
                new() { Name = "SrcY3", DisplayName = "源点3 Y", DataType = "double", DefaultValue = 100.0 },
                new() { Name = "SrcX4", DisplayName = "源点4 X", DataType = "double", DefaultValue = 0.0 },
                new() { Name = "SrcY4", DisplayName = "源点4 Y", DataType = "double", DefaultValue = 100.0 },
                new() { Name = "DstX1", DisplayName = "目标点1 X", DataType = "double", DefaultValue = 0.0 },
                new() { Name = "DstY1", DisplayName = "目标点1 Y", DataType = "double", DefaultValue = 0.0 },
                new() { Name = "DstX2", DisplayName = "目标点2 X", DataType = "double", DefaultValue = 640.0 },
                new() { Name = "DstY2", DisplayName = "目标点2 Y", DataType = "double", DefaultValue = 0.0 },
                new() { Name = "DstX3", DisplayName = "目标点3 X", DataType = "double", DefaultValue = 640.0 },
                new() { Name = "DstY3", DisplayName = "目标点3 Y", DataType = "double", DefaultValue = 480.0 },
                new() { Name = "DstX4", DisplayName = "目标点4 X", DataType = "double", DefaultValue = 0.0 },
                new() { Name = "DstY4", DisplayName = "目标点4 Y", DataType = "double", DefaultValue = 480.0 },
                new() { Name = "OutputWidth", DisplayName = "输出宽度", DataType = "int", DefaultValue = 640, MinValue = 1, MaxValue = 8192 },
                new() { Name = "OutputHeight", DisplayName = "输出高度", DataType = "int", DefaultValue = 480, MinValue = 1, MaxValue = 8192 }
            }
        };

        // 7. 条码识别 - 补充参数 (CodeRecognition = 9)
        // 条码识别已有元数据定义，需要补充 Parameters
        var codeRecognitionMetadata = _metadata[OperatorType.CodeRecognition];
        codeRecognitionMetadata.Parameters = new List<ParameterDefinition>
        {
            new() { Name = "CodeType", DisplayName = "码制类型", DataType = "enum", DefaultValue = "All", Options = new List<ParameterOption>
            {
                new() { Label = "全部", Value = "All" },
                new() { Label = "QR码", Value = "QR" },
                new() { Label = "Code128", Value = "Code128" },
                new() { Label = "DataMatrix", Value = "DataMatrix" },
                new() { Label = "EAN-13", Value = "EAN13" },
                new() { Label = "Code39", Value = "Code39" }
            } },
            new() { Name = "MaxResults", DisplayName = "最大结果数", DataType = "int", DefaultValue = 10, MinValue = 1, MaxValue = 100 }
        };

        // 8. 圆测量 (CircleMeasurement = 19)
        _metadata[OperatorType.CircleMeasurement] = new OperatorMetadata
        {
            Type = OperatorType.CircleMeasurement,
            DisplayName = "圆测量",
            Description = "霍夫变换检测圆形并测量半径与圆心坐标，适用于孔径检测和圆形定位",
            Category = "检测",
            IconName = "circle-measure",
            Keywords = new[] { "圆", "半径", "圆心", "霍夫", "孔", "圆检测", "Circle", "Radius", "Hough" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "Radius", DisplayName = "半径", DataType = PortDataType.Float },
                new() { Name = "Center", DisplayName = "圆心", DataType = PortDataType.Point },
                new() { Name = "Circle", DisplayName = "圆数据", DataType = PortDataType.CircleData },
                new() { Name = "CircleCount", DisplayName = "圆数量", DataType = PortDataType.Integer }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Method", DisplayName = "检测方法", DataType = "enum", DefaultValue = "HoughCircle", Options = new List<ParameterOption>
                {
                    new() { Label = "霍夫圆", Value = "HoughCircle" },
                    new() { Label = "拟合椭圆", Value = "FitEllipse" }
                } },
                new() { Name = "MinRadius", DisplayName = "最小半径", DataType = "int", DefaultValue = 10, MinValue = 0 },
                new() { Name = "MaxRadius", DisplayName = "最大半径", DataType = "int", DefaultValue = 200, MinValue = 0 },
                new() { Name = "Dp", DisplayName = "分辨率比", DataType = "double", DefaultValue = 1.0, MinValue = 0.5, MaxValue = 4.0 },
                new() { Name = "MinDist", DisplayName = "最小圆距", DataType = "double", DefaultValue = 50.0, MinValue = 1.0 },
                new() { Name = "Param1", DisplayName = "Canny阈值", DataType = "double", DefaultValue = 100.0, MinValue = 0.0, MaxValue = 255.0 },
                new() { Name = "Param2", DisplayName = "累加器阈值", DataType = "double", DefaultValue = 30.0, MinValue = 0.0, MaxValue = 255.0 }
            }
        };

        // 9. 直线测量 (LineMeasurement = 20)
        _metadata[OperatorType.LineMeasurement] = new OperatorMetadata
        {
            Type = OperatorType.LineMeasurement,
            DisplayName = "直线测量",
            Description = "霍夫直线检测与测量",
            Category = "检测",
            IconName = "line-measure",
            Keywords = new[] { "直线", "线段", "角度", "霍夫", "Line", "Hough", "Segment" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "Angle", DisplayName = "角度", DataType = PortDataType.Float },
                new() { Name = "Length", DisplayName = "长度", DataType = PortDataType.Float },
                new() { Name = "Line", DisplayName = "直线数据", DataType = PortDataType.LineData },
                new() { Name = "LineCount", DisplayName = "直线数量", DataType = PortDataType.Integer }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Method", DisplayName = "检测方法", DataType = "enum", DefaultValue = "HoughLine", Options = new List<ParameterOption>
                {
                    new() { Label = "霍夫直线", Value = "HoughLine" },
                    new() { Label = "拟合直线", Value = "FitLine" }
                } },
                new() { Name = "Threshold", DisplayName = "累加阈值", DataType = "int", DefaultValue = 100, MinValue = 1 },
                new() { Name = "MinLength", DisplayName = "最小长度", DataType = "double", DefaultValue = 50.0, MinValue = 0.0 },
                new() { Name = "MaxGap", DisplayName = "最大间隙", DataType = "double", DefaultValue = 10.0, MinValue = 0.0 }
            }
        };

        // 10. 轮廓测量 (ContourMeasurement = 21)
        _metadata[OperatorType.ContourMeasurement] = new OperatorMetadata
        {
            Type = OperatorType.ContourMeasurement,
            DisplayName = "轮廓测量",
            Description = "轮廓分析与测量",
            Category = "检测",
            IconName = "contour-measure",
            Keywords = new[] { "轮廓", "面积", "周长", "形状分析", "Contour", "Area", "Perimeter" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "Area", DisplayName = "面积", DataType = PortDataType.Float },
                new() { Name = "Perimeter", DisplayName = "周长", DataType = PortDataType.Float },
                new() { Name = "ContourCount", DisplayName = "轮廓数量", DataType = PortDataType.Integer }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Threshold", DisplayName = "二值化阈值", DataType = "double", DefaultValue = 127.0, MinValue = 0.0, MaxValue = 255.0 },
                new() { Name = "MinArea", DisplayName = "最小面积", DataType = "int", DefaultValue = 100, MinValue = 0 },
                new() { Name = "MaxArea", DisplayName = "最大面积", DataType = "int", DefaultValue = 100000, MinValue = 0 },
                new() { Name = "SortBy", DisplayName = "排序依据", DataType = "enum", DefaultValue = "Area", Options = new List<ParameterOption>
                {
                    new() { Label = "面积", Value = "Area" },
                    new() { Label = "周长", Value = "Perimeter" }
                } }
            }
        };

        // ==================== Phase 2 新增算子 ====================

        // 1. 角度测量 (AngleMeasurement = 22)
        _metadata[OperatorType.AngleMeasurement] = new OperatorMetadata
        {
            Type = OperatorType.AngleMeasurement,
            DisplayName = "角度测量",
            Description = "基于三点计算角度",
            Category = "检测",
            IconName = "angle-measure",
            Keywords = new[] { "角度", "三点", "弧度", "夹角", "Angle", "Degree", "Radian" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "Angle", DisplayName = "角度", DataType = PortDataType.Float }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Point1X", DisplayName = "点1 X", DataType = "int", DefaultValue = 0 },
                new() { Name = "Point1Y", DisplayName = "点1 Y", DataType = "int", DefaultValue = 0 },
                new() { Name = "Point2X", DisplayName = "点2 X(顶点)", DataType = "int", DefaultValue = 100 },
                new() { Name = "Point2Y", DisplayName = "点2 Y(顶点)", DataType = "int", DefaultValue = 100 },
                new() { Name = "Point3X", DisplayName = "点3 X", DataType = "int", DefaultValue = 200 },
                new() { Name = "Point3Y", DisplayName = "点3 Y", DataType = "int", DefaultValue = 0 },
                new() { Name = "Unit", DisplayName = "角度单位", DataType = "enum", DefaultValue = "Degree", Options = new List<ParameterOption>
                {
                    new() { Label = "度", Value = "Degree" },
                    new() { Label = "弧度", Value = "Radian" }
                } }
            }
        };

        // 2. 几何公差 (GeometricTolerance = 23)
        _metadata[OperatorType.GeometricTolerance] = new OperatorMetadata
        {
            Type = OperatorType.GeometricTolerance,
            DisplayName = "几何公差",
            Description = "角度偏差测量（仅角度模型，非完整GD&T公差带）",
            Category = "检测",
            IconName = "geometric-tolerance",
            Keywords = new[] { "公差", "平行度", "垂直度", "GD&T", "Tolerance", "Parallelism", "Perpendicularity", "AngleOnly" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "Tolerance", DisplayName = "角度偏差", DataType = PortDataType.Float },
                new() { Name = "AngularDeviationDeg", DisplayName = "角度偏差(度)", DataType = PortDataType.Float },
                new() { Name = "LinearBand", DisplayName = "线性跳动带(像素)", DataType = PortDataType.Float },
                new() { Name = "MeasurementModel", DisplayName = "测量模型", DataType = PortDataType.String }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "MeasureType", DisplayName = "测量类型", DataType = "enum", DefaultValue = "Parallelism", Options = new List<ParameterOption>
                {
                    new() { Label = "平行度", Value = "Parallelism" },
                    new() { Label = "垂直度", Value = "Perpendicularity" }
                } },
                new() { Name = "Line1_X1", DisplayName = "线1起点X", DataType = "int", DefaultValue = 0 },
                new() { Name = "Line1_Y1", DisplayName = "线1起点Y", DataType = "int", DefaultValue = 0 },
                new() { Name = "Line1_X2", DisplayName = "线1终点X", DataType = "int", DefaultValue = 100 },
                new() { Name = "Line1_Y2", DisplayName = "线1终点Y", DataType = "int", DefaultValue = 100 },
                new() { Name = "Line2_X1", DisplayName = "线2起点X", DataType = "int", DefaultValue = 0 },
                new() { Name = "Line2_Y1", DisplayName = "线2起点Y", DataType = "int", DefaultValue = 200 },
                new() { Name = "Line2_X2", DisplayName = "线2终点X", DataType = "int", DefaultValue = 100 },
                new() { Name = "Line2_Y2", DisplayName = "线2终点Y", DataType = "int", DefaultValue = 200 }
            }
        };

        // 3. 相机标定 (CameraCalibration = 24)
        _metadata[OperatorType.CameraCalibration] = new OperatorMetadata
        {
            Type = OperatorType.CameraCalibration,
            DisplayName = "相机标定",
            Description = "棋盘格/圆点标定",
            Category = "标定",
            IconName = "calibration",
            Keywords = new[] { "标定", "棋盘格", "内参", "畸变", "Calibration", "Chessboard", "Intrinsic" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "CalibrationData", DisplayName = "标定数据", DataType = PortDataType.String }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "PatternType", DisplayName = "标定板类型", DataType = "enum", DefaultValue = "Chessboard", Options = new List<ParameterOption>
                {
                    new() { Label = "棋盘格", Value = "Chessboard" },
                    new() { Label = "圆点格", Value = "CircleGrid" }
                } },
                new() { Name = "BoardWidth", DisplayName = "棋盘格宽度", DataType = "int", DefaultValue = 9, MinValue = 2, MaxValue = 30 },
                new() { Name = "BoardHeight", DisplayName = "棋盘格高度", DataType = "int", DefaultValue = 6, MinValue = 2, MaxValue = 30 },
                new() { Name = "SquareSize", DisplayName = "方格尺寸(mm)", DataType = "double", DefaultValue = 25.0, MinValue = 0.1, MaxValue = 1000.0 },
                new() { Name = "Mode", DisplayName = "模式", DataType = "enum", DefaultValue = "SingleImage",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "单图检测", Value = "SingleImage" },
                        new() { Label = "文件夹标定", Value = "FolderCalibration" }
                    }
                },
                new() { Name = "ImageFolder", DisplayName = "标定图片文件夹", DataType = "string", DefaultValue = "" },
                new() { Name = "CalibrationOutputPath", DisplayName = "标定结果保存路径", DataType = "string",
                    DefaultValue = "calibration_result.json" }
            }
        };

        // 4. 畸变校正 (Undistort = 25)
        _metadata[OperatorType.Undistort] = new OperatorMetadata
        {
            Type = OperatorType.Undistort,
            DisplayName = "畸变校正",
            Description = "基于标定数据校正图像畸变",
            Category = "标定",
            IconName = "undistort",
            Keywords = new[] { "畸变", "校正", "矫正", "去畸变", "Undistort", "Distortion", "Correct" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "CalibrationData", DisplayName = "标定数据", DataType = PortDataType.String, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "校正图像", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "CalibrationFile", DisplayName = "标定文件路径", DataType = "file", DefaultValue = "" }
            }
        };

        // 5. 坐标转换 (CoordinateTransform = 26)
        _metadata[OperatorType.CoordinateTransform] = new OperatorMetadata
        {
            Type = OperatorType.CoordinateTransform,
            DisplayName = "坐标转换",
            Description = "像素坐标到物理坐标转换",
            Category = "标定",
            IconName = "coordinate-transform",
            Keywords = new[] { "坐标", "像素", "物理", "毫米", "转换", "Coordinate", "Pixel to mm", "Physical" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = false },
                new() { Name = "PixelX", DisplayName = "像素X", DataType = PortDataType.Float, IsRequired = false },
                new() { Name = "PixelY", DisplayName = "像素Y", DataType = PortDataType.Float, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "PhysicalX", DisplayName = "物理X(mm)", DataType = PortDataType.Float },
                new() { Name = "PhysicalY", DisplayName = "物理Y(mm)", DataType = PortDataType.Float }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "PixelX", DisplayName = "像素X坐标", DataType = "double", DefaultValue = 0.0 },
                new() { Name = "PixelY", DisplayName = "像素Y坐标", DataType = "double", DefaultValue = 0.0 },
                new() { Name = "PixelSize", DisplayName = "像素尺寸(mm/px)", DataType = "double", DefaultValue = 0.01, MinValue = 0.0001, MaxValue = 100.0 },
                new() { Name = "CalibrationFile", DisplayName = "标定文件路径", DataType = "file", DefaultValue = "" }
            }
        };

        // ==================== Phase 3 新增算子 ====================

        // 1. Modbus通信 (ModbusCommunication = 27)
        _metadata[OperatorType.ModbusCommunication] = new OperatorMetadata
        {
            Type = OperatorType.ModbusCommunication,
            DisplayName = "Modbus通信",
            Description = "工业设备Modbus RTU/TCP通信",
            Category = "通信",
            IconName = "modbus",
            Keywords = new[] { "Modbus", "PLC", "通信", "寄存器", "RTU", "TCP", "工业", "Communication" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Data", DisplayName = "数据", DataType = PortDataType.Any, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Response", DisplayName = "响应", DataType = PortDataType.String },
                new() { Name = "Status", DisplayName = "状态", DataType = PortDataType.Boolean }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Protocol", DisplayName = "协议", DataType = "enum", DefaultValue = "TCP", Options = new List<ParameterOption>
                {
                    new() { Label = "TCP", Value = "TCP" },
                    new() { Label = "RTU", Value = "RTU" }
                } },
                new() { Name = "IpAddress", DisplayName = "IP地址", DataType = "string", DefaultValue = "192.168.1.1" },
                new() { Name = "Port", DisplayName = "端口", DataType = "int", DefaultValue = 502, MinValue = 1, MaxValue = 65535 },
                new() { Name = "SlaveId", DisplayName = "从机ID", DataType = "int", DefaultValue = 1, MinValue = 1, MaxValue = 247 },
                new() { Name = "RegisterAddress", DisplayName = "寄存器地址", DataType = "int", DefaultValue = 0 },
                new() { Name = "RegisterCount", DisplayName = "寄存器数量", DataType = "int", DefaultValue = 1, MinValue = 1, MaxValue = 125 },
                new() { Name = "FunctionCode", DisplayName = "功能码", DataType = "enum", DefaultValue = "ReadHolding", Options = new List<ParameterOption>
                {
                    new() { Label = "读线圈", Value = "ReadCoils" },
                    new() { Label = "读保持寄存器", Value = "ReadHolding" },
                    new() { Label = "写单寄存器", Value = "WriteSingle" },
                    new() { Label = "写多寄存器", Value = "WriteMultiple" }
                } },
                new() { Name = "WriteValue", DisplayName = "写入值", DataType = "string", DefaultValue = "" }
            }
        };

        // 2. TCP通信 (TcpCommunication = 28)
        _metadata[OperatorType.TcpCommunication] = new OperatorMetadata
        {
            Type = OperatorType.TcpCommunication,
            DisplayName = "TCP通信",
            Description = "TCP/IP网络通信",
            Category = "通信",
            IconName = "tcp",
            Keywords = new[] { "TCP", "网络", "Socket", "通信", "发送", "接收", "IP", "Communication" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Data", DisplayName = "数据", DataType = PortDataType.Any, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Response", DisplayName = "响应", DataType = PortDataType.String },
                new() { Name = "Status", DisplayName = "状态", DataType = PortDataType.Boolean }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Mode", DisplayName = "模式", DataType = "enum", DefaultValue = "Client", Options = new List<ParameterOption>
                {
                    new() { Label = "客户端", Value = "Client" },
                    new() { Label = "服务器", Value = "Server" }
                } },
                new() { Name = "IpAddress", DisplayName = "IP地址", DataType = "string", DefaultValue = "127.0.0.1" },
                new() { Name = "Port", DisplayName = "端口", DataType = "int", DefaultValue = 8080, MinValue = 1, MaxValue = 65535 },
                new() { Name = "SendData", DisplayName = "发送数据", DataType = "string", DefaultValue = "" },
                new() { Name = "Timeout", DisplayName = "超时(ms)", DataType = "int", DefaultValue = 5000, MinValue = 100, MaxValue = 30000 },
                new() { Name = "Encoding", DisplayName = "编码", DataType = "enum", DefaultValue = "UTF8", Options = new List<ParameterOption>
                {
                    new() { Label = "UTF-8", Value = "UTF8" },
                    new() { Label = "ASCII", Value = "ASCII" },
                    new() { Label = "GBK", Value = "GBK" }
                } }
            }
        };

        // 3. 数据库写入 (DatabaseWrite = 29)
        _metadata[OperatorType.DatabaseWrite] = new OperatorMetadata
        {
            Type = OperatorType.DatabaseWrite,
            DisplayName = "数据库写入",
            Description = "检测结果存储到数据库",
            Category = "数据",
            IconName = "database",
            Keywords = new[] { "数据库", "存储", "写入", "记录", "SQL", "Database", "Store", "Write" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Data", DisplayName = "数据", DataType = PortDataType.Any, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Status", DisplayName = "状态", DataType = PortDataType.Boolean },
                new() { Name = "RecordId", DisplayName = "记录ID", DataType = PortDataType.String }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "ConnectionString", DisplayName = "连接字符串", DataType = "string", DefaultValue = "" },
                new() { Name = "TableName", DisplayName = "表名", DataType = "string", DefaultValue = "InspectionResults" },
                new() { Name = "DbType", DisplayName = "数据库类型", DataType = "enum", DefaultValue = "SQLite", Options = new List<ParameterOption>
                {
                    new() { Label = "SQLite", Value = "SQLite" },
                    new() { Label = "SQLServer", Value = "SQLServer" },
                    new() { Label = "MySQL", Value = "MySQL" }
                } }
            }
        };

        // 4. 条件分支 (ConditionalBranch = 30)
        _metadata[OperatorType.ConditionalBranch] = new OperatorMetadata
        {
            Type = OperatorType.ConditionalBranch,
            DisplayName = "条件分支",
            Description = "根据数值/字符串/布尔条件执行 True/False 两路分支，常用于 OK/NG 判定路由",
            Category = "控制",
            IconName = "branch",
            Keywords = new[] { "条件", "分支", "判断", "如果", "否则", "IF", "Branch", "Condition", "Switch" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Value", DisplayName = "判断值", DataType = PortDataType.Any, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "True", DisplayName = "True分支", DataType = PortDataType.Any },
                new() { Name = "False", DisplayName = "False分支", DataType = PortDataType.Any }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Condition", DisplayName = "条件", DataType = "enum", DefaultValue = "GreaterThan", Options = new List<ParameterOption>
                {
                    new() { Label = "大于", Value = "GreaterThan" },
                    new() { Label = "小于", Value = "LessThan" },
                    new() { Label = "等于", Value = "Equal" },
                    new() { Label = "不等于", Value = "NotEqual" },
                    new() { Label = "包含", Value = "Contains" }
                } },
                new() { Name = "CompareValue", DisplayName = "比较值", DataType = "string", DefaultValue = "0" },
                new() { Name = "FieldName", DisplayName = "字段名", DataType = "string", DefaultValue = "" }
            }
        };

        // 5. 颜色空间转换 (ColorConversion = 38)
        _metadata[OperatorType.ColorConversion] = new OperatorMetadata
        {
            Type = OperatorType.ColorConversion,
            DisplayName = "颜色空间转换",
            Description = "BGR/GRAY/HSV/Lab/YUV等颜色空间转换",
            Category = "预处理",
            IconName = "color-convert",
            Keywords = new[] { "颜色", "色彩", "灰度", "HSV", "Lab", "转换", "Color", "Convert", "Gray" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输出图像", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "ConversionCode", DisplayName = "转换类型", DataType = "enum", DefaultValue = "BGR2GRAY", Options = new List<ParameterOption>
                {
                    new() { Label = "BGR转灰度", Value = "BGR2GRAY" },
                    new() { Label = "BGR转HSV", Value = "BGR2HSV" },
                    new() { Label = "BGR转Lab", Value = "BGR2Lab" },
                    new() { Label = "BGR转YUV", Value = "BGR2YUV" },
                    new() { Label = "灰度转BGR", Value = "GRAY2BGR" },
                    new() { Label = "HSV转BGR", Value = "HSV2BGR" }
                } }
            }
        };

        // 6. 自适应阈值 (AdaptiveThreshold = 39)
        _metadata[OperatorType.AdaptiveThreshold] = new OperatorMetadata
        {
            Type = OperatorType.AdaptiveThreshold,
            DisplayName = "自适应阈值",
            Description = "Mean和Gaussian自适应阈值",
            Category = "预处理",
            IconName = "adaptive-threshold",
            Keywords = new[] { "自适应", "阈值", "局部", "Adaptive", "Local threshold", "Gaussian" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输出图像", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "MaxValue", DisplayName = "最大值", DataType = "double", DefaultValue = 255.0, MinValue = 0.0, MaxValue = 255.0 },
                new() { Name = "AdaptiveMethod", DisplayName = "自适应方法", DataType = "enum", DefaultValue = "Gaussian", Options = new List<ParameterOption>
                {
                    new() { Label = "高斯加权", Value = "Gaussian" },
                    new() { Label = "均值", Value = "Mean" }
                } },
                new() { Name = "ThresholdType", DisplayName = "阈值类型", DataType = "enum", DefaultValue = "Binary", Options = new List<ParameterOption>
                {
                    new() { Label = "二值化", Value = "Binary" },
                    new() { Label = "反二值化", Value = "BinaryInv" }
                } },
                new() { Name = "BlockSize", DisplayName = "块大小", DataType = "int", DefaultValue = 11, MinValue = 3, MaxValue = 99 },
                new() { Name = "C", DisplayName = "常数C", DataType = "double", DefaultValue = 2.0, MinValue = -100.0, MaxValue = 100.0 }
            }
        };

        // 7. 直方图均衡化 (HistogramEqualization = 40)
        _metadata[OperatorType.HistogramEqualization] = new OperatorMetadata
        {
            Type = OperatorType.HistogramEqualization,
            DisplayName = "直方图均衡化",
            Description = "全局均衡化和CLAHE自适应均衡化",
            Category = "预处理",
            IconName = "histogram",
            Keywords = new[] { "直方图", "均衡化", "对比度", "增强", "CLAHE", "Histogram", "Equalize", "Contrast" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输出图像", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Method", DisplayName = "方法", DataType = "enum", DefaultValue = "Global", Options = new List<ParameterOption>
                {
                    new() { Label = "全局均衡化", Value = "Global" },
                    new() { Label = "CLAHE自适应", Value = "CLAHE" }
                } },
                new() { Name = "ClipLimit", DisplayName = "裁剪限制", DataType = "double", DefaultValue = 2.0, MinValue = 0.0, MaxValue = 100.0 },
                new() { Name = "TileGridSize", DisplayName = "网格大小", DataType = "int", DefaultValue = 8, MinValue = 1, MaxValue = 64 }
            }
        };

        // ==================== Phase 1 关键能力补齐 ====================

        // 1. 几何拟合 (GeometricFitting = 41)
        _metadata[OperatorType.GeometricFitting] = new OperatorMetadata
        {
            Type = OperatorType.GeometricFitting,
            DisplayName = "几何拟合",
            Description = "从轮廓点拟合直线、圆或椭圆",
            Category = "测量",
            IconName = "fit",
            Keywords = new[] { "拟合", "直线拟合", "圆拟合", "椭圆", "最小二乘", "Fit", "Line fit", "Circle fit" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "FitResult", DisplayName = "拟合结果", DataType = PortDataType.Any }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "FitType", DisplayName = "拟合类型", DataType = "enum", DefaultValue = "Circle",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "直线", Value = "Line" },
                        new() { Label = "圆", Value = "Circle" },
                        new() { Label = "椭圆", Value = "Ellipse" }
                    }
                },
                new() { Name = "Threshold", DisplayName = "二值化阈值", DataType = "double",
                    DefaultValue = 127.0, MinValue = 0.0, MaxValue = 255.0 },
                new() { Name = "MinArea", DisplayName = "最小轮廓面积", DataType = "int",
                    DefaultValue = 100, MinValue = 0 },
                new() { Name = "MinPoints", DisplayName = "最少拟合点数", DataType = "int",
                    DefaultValue = 5, MinValue = 3, MaxValue = 10000 },
                new() { Name = "RobustMethod", DisplayName = "Robust Method", DataType = "enum", DefaultValue = "LeastSquares",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "LeastSquares", Value = "LeastSquares" },
                        new() { Label = "Ransac", Value = "Ransac" }
                    }
                },
                new() { Name = "RansacIterations", DisplayName = "Ransac Iterations", DataType = "int",
                    DefaultValue = 200, MinValue = 10, MaxValue = 5000 },
                new() { Name = "RansacInlierThreshold", DisplayName = "Ransac Inlier Threshold", DataType = "double",
                    DefaultValue = 2.0, MinValue = 0.1, MaxValue = 50.0 }
            }
        };

        // 2. ROI管理器 (RoiManager = 42)
        _metadata[OperatorType.RoiManager] = new OperatorMetadata
        {
            Type = OperatorType.RoiManager,
            DisplayName = "ROI管理器",
            Description = "矩形/圆形/多边形区域选择",
            Category = "辅助",
            IconName = "roi",
            Keywords = new[] { "ROI", "区域", "感兴趣区", "掩膜", "选区", "Region", "Mask", "Area of interest" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "ROI图像", DataType = PortDataType.Image },
                new() { Name = "Mask", DisplayName = "掩膜", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Shape", DisplayName = "形状", DataType = "enum", DefaultValue = "Rectangle",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "矩形", Value = "Rectangle" },
                        new() { Label = "圆形", Value = "Circle" },
                        new() { Label = "多边形", Value = "Polygon" }
                    }
                },
                new() { Name = "Operation", DisplayName = "操作", DataType = "enum", DefaultValue = "Crop",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "裁剪", Value = "Crop" },
                        new() { Label = "掩膜", Value = "Mask" }
                    }
                },
                new() { Name = "X", DisplayName = "X", DataType = "int", DefaultValue = 0, MinValue = 0 },
                new() { Name = "Y", DisplayName = "Y", DataType = "int", DefaultValue = 0, MinValue = 0 },
                new() { Name = "Width", DisplayName = "宽度", DataType = "int", DefaultValue = 200, MinValue = 1 },
                new() { Name = "Height", DisplayName = "高度", DataType = "int", DefaultValue = 200, MinValue = 1 },
                new() { Name = "CenterX", DisplayName = "圆心X", DataType = "int", DefaultValue = 100 },
                new() { Name = "CenterY", DisplayName = "圆心Y", DataType = "int", DefaultValue = 100 },
                new() { Name = "Radius", DisplayName = "半径", DataType = "int", DefaultValue = 50, MinValue = 1 },
                new() { Name = "PolygonPoints", DisplayName = "多边形顶点(JSON)", DataType = "string",
                    DefaultValue = "[[10,10],[200,10],[200,200],[10,200]]" }
            }
        };

        // 3. 形状匹配 (ShapeMatching = 43)
        _metadata[OperatorType.ShapeMatching] = new OperatorMetadata
        {
            Type = OperatorType.ShapeMatching,
            DisplayName = "形状匹配",
            Description = "旋转/缩放不变的高级模板匹配",
            Category = "匹配定位",
            IconName = "shape-match",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "搜索图像", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "Template", DisplayName = "模板图像", DataType = PortDataType.Image, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "Matches", DisplayName = "匹配结果", DataType = PortDataType.Any }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "TemplatePath", DisplayName = "模板文件路径", DataType = "file", DefaultValue = "" },
                new() { Name = "MinScore", DisplayName = "最小匹配分数", DataType = "double",
                    DefaultValue = 0.7, MinValue = 0.1, MaxValue = 1.0 },
                new() { Name = "MaxMatches", DisplayName = "最大匹配数", DataType = "int",
                    DefaultValue = 1, MinValue = 1, MaxValue = 50 },
                new() { Name = "AngleStart", DisplayName = "起始角度", DataType = "double",
                    DefaultValue = -30.0, MinValue = -180.0, MaxValue = 180.0 },
                new() { Name = "AngleExtent", DisplayName = "角度范围", DataType = "double",
                    DefaultValue = 60.0, MinValue = 0.0, MaxValue = 360.0 },
                new() { Name = "AngleStep", DisplayName = "角度步长", DataType = "double",
                    DefaultValue = 1.0, MinValue = 0.1, MaxValue = 10.0 },
                new() { Name = "NumLevels", DisplayName = "金字塔层数", DataType = "int",
                    DefaultValue = 3, MinValue = 1, MaxValue = 6 }
            }
        };

        // 4. 亚像素边缘提取 (SubpixelEdgeDetection = 44)
        _metadata[OperatorType.SubpixelEdgeDetection] = new OperatorMetadata
        {
            Type = OperatorType.SubpixelEdgeDetection,
            DisplayName = "亚像素边缘",
            Description = "高精度亚像素级边缘提取",
            Category = "颜色处理",
            IconName = "edge-subpixel",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "Edges", DisplayName = "边缘点集", DataType = PortDataType.Any }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "LowThreshold", DisplayName = "低阈值", DataType = "double",
                    DefaultValue = 50.0, MinValue = 0.0, MaxValue = 255.0 },
                new() { Name = "HighThreshold", DisplayName = "高阈值", DataType = "double",
                    DefaultValue = 150.0, MinValue = 0.0, MaxValue = 255.0 },
                new() { Name = "Sigma", DisplayName = "高斯Sigma", DataType = "double",
                    DefaultValue = 1.0, MinValue = 0.1, MaxValue = 10.0 },
                new() { Name = "Method", DisplayName = "亚像素方法", DataType = "enum", DefaultValue = "GradientInterp",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "梯度插值", Value = "GradientInterp" },
                        new() { Label = "高斯拟合", Value = "GaussianFit" },
                        new() { Label = "Steger", Value = "Steger" }
                    }
                },
                new() { Name = "EdgeThreshold", DisplayName = "Edge Threshold", DataType = "double",
                    DefaultValue = 25.0, MinValue = 0.0, MaxValue = 255.0 }
            }
        };

        // ==================== Phase 3 新增算子 ====================

        // 1. 颜色检测 (ColorDetection = 45)
        _metadata[OperatorType.ColorDetection] = new OperatorMetadata
        {
            Type = OperatorType.ColorDetection,
            DisplayName = "颜色检测",
            Description = "基于 HSV/Lab 空间的颜色分析与分级",
            Category = "颜色处理",
            IconName = "color",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "ColorInfo", DisplayName = "颜色信息", DataType = PortDataType.Any }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "ColorSpace", DisplayName = "颜色空间", DataType = "enum", DefaultValue = "HSV",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "HSV", Value = "HSV" },
                        new() { Label = "Lab", Value = "Lab" }
                    }
                },
                new() { Name = "AnalysisMode", DisplayName = "分析模式", DataType = "enum", DefaultValue = "Average",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "平均色", Value = "Average" },
                        new() { Label = "主色提取", Value = "Dominant" },
                        new() { Label = "颜色范围检测", Value = "Range" }
                    }
                },
                new() { Name = "HueLow", DisplayName = "H下限", DataType = "int", DefaultValue = 0, MinValue = 0, MaxValue = 180 },
                new() { Name = "HueHigh", DisplayName = "H上限", DataType = "int", DefaultValue = 180, MinValue = 0, MaxValue = 180 },
                new() { Name = "SatLow", DisplayName = "S下限", DataType = "int", DefaultValue = 50, MinValue = 0, MaxValue = 255 },
                new() { Name = "SatHigh", DisplayName = "S上限", DataType = "int", DefaultValue = 255, MinValue = 0, MaxValue = 255 },
                new() { Name = "ValLow", DisplayName = "V下限", DataType = "int", DefaultValue = 50, MinValue = 0, MaxValue = 255 },
                new() { Name = "ValHigh", DisplayName = "V上限", DataType = "int", DefaultValue = 255, MinValue = 0, MaxValue = 255 },
                new() { Name = "DominantK", DisplayName = "主色数量K", DataType = "int", DefaultValue = 3, MinValue = 1, MaxValue = 10 }
            }
        };

        // 2. 串口通信 (SerialCommunication = 46)
        _metadata[OperatorType.SerialCommunication] = new OperatorMetadata
        {
            Type = OperatorType.SerialCommunication,
            DisplayName = "串口通信",
            Description = "RS-232/485 串口数据收发",
            Category = "通信",
            IconName = "serial",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Data", DisplayName = "发送数据", DataType = PortDataType.Any, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Response", DisplayName = "接收数据", DataType = PortDataType.Any }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "PortName", DisplayName = "串口号", DataType = "string", DefaultValue = "COM1" },
                new() { Name = "BaudRate", DisplayName = "波特率", DataType = "enum", DefaultValue = "9600",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "9600", Value = "9600" },
                        new() { Label = "19200", Value = "19200" },
                        new() { Label = "38400", Value = "38400" },
                        new() { Label = "57600", Value = "57600" },
                        new() { Label = "115200", Value = "115200" }
                    }
                },
                new() { Name = "DataBits", DisplayName = "数据位", DataType = "int", DefaultValue = 8, MinValue = 5, MaxValue = 8 },
                new() { Name = "StopBits", DisplayName = "停止位", DataType = "enum", DefaultValue = "One",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "1", Value = "One" },
                        new() { Label = "1.5", Value = "OnePointFive" },
                        new() { Label = "2", Value = "Two" }
                    }
                },
                new() { Name = "Parity", DisplayName = "校验位", DataType = "enum", DefaultValue = "None",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "无", Value = "None" },
                        new() { Label = "奇校验", Value = "Odd" },
                        new() { Label = "偶校验", Value = "Even" }
                    }
                },
                new() { Name = "SendData", DisplayName = "发送内容", DataType = "string", DefaultValue = "" },
                new() { Name = "Encoding", DisplayName = "编码", DataType = "enum", DefaultValue = "UTF8",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "UTF-8", Value = "UTF8" },
                        new() { Label = "ASCII", Value = "ASCII" },
                        new() { Label = "HEX", Value = "HEX" }
                    }
                },
                new() { Name = "TimeoutMs", DisplayName = "超时(毫秒)", DataType = "int", DefaultValue = 3000, MinValue = 100, MaxValue = 30000 }
            }
        };

        // ==================== PLC 通信算子 ====================

        // 1. 西门子S7通信 (SiemensS7Communication = 50)
        _metadata[OperatorType.SiemensS7Communication] = new OperatorMetadata
        {
            Type = OperatorType.SiemensS7Communication,
            DisplayName = "西门子S7通信",
            Description = "西门子S7系列PLC读写通信（S7-200/300/400/1200/1500）",
            Category = "通信",
            IconName = "s7",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Data", DisplayName = "数据", DataType = PortDataType.Any, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Response", DisplayName = "响应", DataType = PortDataType.String },
                new() { Name = "Status", DisplayName = "状态", DataType = PortDataType.Boolean }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "IpAddress", DisplayName = "IP地址", DataType = "string", DefaultValue = "192.168.0.1" },
                new() { Name = "Port", DisplayName = "端口", DataType = "int", DefaultValue = 102, MinValue = 1, MaxValue = 65535 },
                new() { Name = "CpuType", DisplayName = "CPU类型", DataType = "enum", DefaultValue = "S71200",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "S7-200", Value = "S7200" },
                        new() { Label = "S7-200 Smart", Value = "S7200Smart" },
                        new() { Label = "S7-300", Value = "S7300" },
                        new() { Label = "S7-400", Value = "S7400" },
                        new() { Label = "S7-1200", Value = "S71200" },
                        new() { Label = "S7-1500", Value = "S71500" }
                    }
                },
                new() { Name = "Rack", DisplayName = "机架号", DataType = "int", DefaultValue = 0, MinValue = 0, MaxValue = 15 },
                new() { Name = "Slot", DisplayName = "插槽号", DataType = "int", DefaultValue = 1, MinValue = 0, MaxValue = 15 },
                new() { Name = "Address", DisplayName = "PLC地址", DataType = "string", DefaultValue = "DB1.DBW100" },
                new() { Name = "DataType", DisplayName = "数据类型", DataType = "enum", DefaultValue = "Word",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "位 (Bool)", Value = "Bit" },
                        new() { Label = "字节 (Byte)", Value = "Byte" },
                        new() { Label = "字 (Word/UInt16)", Value = "Word" },
                        new() { Label = "短整型 (Int16)", Value = "Int16" },
                        new() { Label = "双字 (DWord/UInt32)", Value = "DWord" },
                        new() { Label = "整型 (Int32)", Value = "Int32" },
                        new() { Label = "浮点 (Float)", Value = "Float" },
                        new() { Label = "双精度 (Double)", Value = "Double" },
                        new() { Label = "字符串 (String)", Value = "String" }
                    }
                },
                new() { Name = "Operation", DisplayName = "操作", DataType = "enum", DefaultValue = "Read",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "读取", Value = "Read" },
                        new() { Label = "写入", Value = "Write" }
                    }
                },
                new() { Name = "WriteValue", DisplayName = "写入值", DataType = "string", DefaultValue = "" },
                // 【第二优先级】轮询等待模式参数
                new() { Name = "PollingMode", DisplayName = "轮询模式", DataType = "enum", DefaultValue = "None",
                    Description = "读取时是否启用轮询等待",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "不等待", Value = "None" },
                        new() { Label = "等待指定值", Value = "WaitForValue" }
                    }
                },
                new() { Name = "PollingCondition", DisplayName = "等待条件", DataType = "enum", DefaultValue = "Equal",
                    Description = "等待的条件类型",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "等于", Value = "Equal" },
                        new() { Label = "不等于", Value = "NotEqual" },
                        new() { Label = "大于", Value = "GreaterThan" },
                        new() { Label = "小于", Value = "LessThan" },
                        new() { Label = "大于等于", Value = "GreaterOrEqual" },
                        new() { Label = "小于等于", Value = "LessOrEqual" }
                    }
                },
                new() { Name = "PollingValue", DisplayName = "等待值", DataType = "string", DefaultValue = "1",
                    Description = "等待的目标值（如触发信号值）" },
                new() { Name = "PollingTimeout", DisplayName = "等待超时(ms)", DataType = "int", DefaultValue = 30000, MinValue = 100, MaxValue = 300000,
                    Description = "最长等待时间（毫秒）" },
                new() { Name = "PollingInterval", DisplayName = "轮询间隔(ms)", DataType = "int", DefaultValue = 50, MinValue = 10, MaxValue = 5000,
                    Description = "每次读取间隔（毫秒）" }
            }
        };

        // 2. 三菱MC通信 (MitsubishiMcCommunication = 51)
        _metadata[OperatorType.MitsubishiMcCommunication] = new OperatorMetadata
        {
            Type = OperatorType.MitsubishiMcCommunication,
            DisplayName = "三菱MC通信",
            Description = "三菱MC协议PLC读写通信（FX5U/Q/iQ-R/iQ-F）",
            Category = "通信",
            IconName = "mc",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Data", DisplayName = "数据", DataType = PortDataType.Any, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Response", DisplayName = "响应", DataType = PortDataType.String },
                new() { Name = "Status", DisplayName = "状态", DataType = PortDataType.Boolean }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "IpAddress", DisplayName = "IP地址", DataType = "string", DefaultValue = "192.168.3.1" },
                new() { Name = "Port", DisplayName = "端口", DataType = "int", DefaultValue = 5002, MinValue = 1, MaxValue = 65535 },
                new() { Name = "Address", DisplayName = "PLC地址", DataType = "string", DefaultValue = "D100" },
                new() { Name = "Length", DisplayName = "读取长度", DataType = "int", DefaultValue = 1, MinValue = 1, MaxValue = 960 },
                new() { Name = "DataType", DisplayName = "数据类型", DataType = "enum", DefaultValue = "Word",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "位 (Bool)", Value = "Bit" },
                        new() { Label = "字 (Word/UInt16)", Value = "Word" },
                        new() { Label = "短整型 (Int16)", Value = "Int16" },
                        new() { Label = "双字 (DWord/UInt32)", Value = "DWord" },
                        new() { Label = "整型 (Int32)", Value = "Int32" },
                        new() { Label = "浮点 (Float)", Value = "Float" },
                        new() { Label = "双精度 (Double)", Value = "Double" }
                    }
                },
                new() { Name = "Operation", DisplayName = "操作", DataType = "enum", DefaultValue = "Read",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "读取", Value = "Read" },
                        new() { Label = "写入", Value = "Write" }
                    }
                },
                new() { Name = "WriteValue", DisplayName = "写入值", DataType = "string", DefaultValue = "" },
                // 【第二优先级】轮询等待模式参数
                new() { Name = "PollingMode", DisplayName = "轮询模式", DataType = "enum", DefaultValue = "None",
                    Description = "读取时是否启用轮询等待",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "不等待", Value = "None" },
                        new() { Label = "等待指定值", Value = "WaitForValue" }
                    }
                },
                new() { Name = "PollingCondition", DisplayName = "等待条件", DataType = "enum", DefaultValue = "Equal",
                    Description = "等待的条件类型",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "等于", Value = "Equal" },
                        new() { Label = "不等于", Value = "NotEqual" },
                        new() { Label = "大于", Value = "GreaterThan" },
                        new() { Label = "小于", Value = "LessThan" },
                        new() { Label = "大于等于", Value = "GreaterOrEqual" },
                        new() { Label = "小于等于", Value = "LessOrEqual" }
                    }
                },
                new() { Name = "PollingValue", DisplayName = "等待值", DataType = "string", DefaultValue = "1",
                    Description = "等待的目标值（如触发信号值）" },
                new() { Name = "PollingTimeout", DisplayName = "等待超时(ms)", DataType = "int", DefaultValue = 30000, MinValue = 100, MaxValue = 300000,
                    Description = "最长等待时间（毫秒）" },
                new() { Name = "PollingInterval", DisplayName = "轮询间隔(ms)", DataType = "int", DefaultValue = 50, MinValue = 10, MaxValue = 5000,
                    Description = "每次读取间隔（毫秒）" }
            }
        };

        // 3. 欧姆龙FINS通信 (OmronFinsCommunication = 52)
        _metadata[OperatorType.OmronFinsCommunication] = new OperatorMetadata
        {
            Type = OperatorType.OmronFinsCommunication,
            DisplayName = "欧姆龙FINS通信",
            Description = "欧姆龙FINS/TCP协议PLC读写通信（CP1H/CJ2M/NJ/NX）",
            Category = "通信",
            IconName = "fins",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Data", DisplayName = "数据", DataType = PortDataType.Any, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Response", DisplayName = "响应", DataType = PortDataType.String },
                new() { Name = "Status", DisplayName = "状态", DataType = PortDataType.Boolean }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "IpAddress", DisplayName = "IP地址", DataType = "string", DefaultValue = "192.168.250.1" },
                new() { Name = "Port", DisplayName = "端口", DataType = "int", DefaultValue = 9600, MinValue = 1, MaxValue = 65535 },
                new() { Name = "Address", DisplayName = "PLC地址", DataType = "string", DefaultValue = "DM100" },
                new() { Name = "Length", DisplayName = "读取长度", DataType = "int", DefaultValue = 1, MinValue = 1, MaxValue = 999 },
                new() { Name = "DataType", DisplayName = "数据类型", DataType = "enum", DefaultValue = "Word",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "位 (Bool)", Value = "Bit" },
                        new() { Label = "字 (Word/UInt16)", Value = "Word" },
                        new() { Label = "短整型 (Int16)", Value = "Int16" },
                        new() { Label = "双字 (DWord/UInt32)", Value = "DWord" },
                        new() { Label = "整型 (Int32)", Value = "Int32" },
                        new() { Label = "浮点 (Float)", Value = "Float" },
                        new() { Label = "双精度 (Double)", Value = "Double" }
                    }
                },
                new() { Name = "Operation", DisplayName = "操作", DataType = "enum", DefaultValue = "Read",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "读取", Value = "Read" },
                        new() { Label = "写入", Value = "Write" }
                    }
                },
                new() { Name = "WriteValue", DisplayName = "写入值", DataType = "string", DefaultValue = "" },
                // 【第二优先级】轮询等待模式参数
                new() { Name = "PollingMode", DisplayName = "轮询模式", DataType = "enum", DefaultValue = "None",
                    Description = "读取时是否启用轮询等待",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "不等待", Value = "None" },
                        new() { Label = "等待指定值", Value = "WaitForValue" }
                    }
                },
                new() { Name = "PollingCondition", DisplayName = "等待条件", DataType = "enum", DefaultValue = "Equal",
                    Description = "等待的条件类型",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "等于", Value = "Equal" },
                        new() { Label = "不等于", Value = "NotEqual" },
                        new() { Label = "大于", Value = "GreaterThan" },
                        new() { Label = "小于", Value = "LessThan" },
                        new() { Label = "大于等于", Value = "GreaterOrEqual" },
                        new() { Label = "小于等于", Value = "LessOrEqual" }
                    }
                },
                new() { Name = "PollingValue", DisplayName = "等待值", DataType = "string", DefaultValue = "1",
                    Description = "等待的目标值（如触发信号值）" },
                new() { Name = "PollingTimeout", DisplayName = "等待超时(ms)", DataType = "int", DefaultValue = 30000, MinValue = 100, MaxValue = 300000,
                    Description = "最长等待时间（毫秒）" },
                new() { Name = "PollingInterval", DisplayName = "轮询间隔(ms)", DataType = "int", DefaultValue = 50, MinValue = 10, MaxValue = 5000,
                    Description = "每次读取间隔（毫秒）" }
            }
        };

        // ==================== 【第一优先级】结果判定算子 ====================
        // ResultJudgment = 60
        _metadata[OperatorType.ResultJudgment] = new OperatorMetadata
        {
            Type = OperatorType.ResultJudgment,
            DisplayName = "结果判定",
            Description = "通用判定逻辑（数量/范围/阈值），输出OK/NG结果",
            Category = "流程控制",
            IconName = "judgment",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Value", DisplayName = "输入值", DataType = PortDataType.Any, IsRequired = true },
                new() { Name = "Confidence", DisplayName = "置信度", DataType = PortDataType.Float, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "IsOk", DisplayName = "是否OK", DataType = PortDataType.Boolean },
                new() { Name = "JudgmentValue", DisplayName = "判定值", DataType = PortDataType.String },
                new() { Name = "Details", DisplayName = "详细信息", DataType = PortDataType.String }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "FieldName", DisplayName = "判定字段", DataType = "string", DefaultValue = "Value",
                    Description = "要从上游输入中读取的字段名，如 DefectCount, Distance" },
                new() { Name = "Condition", DisplayName = "判定条件", DataType = "enum", DefaultValue = "Equal",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "等于 (Equal)", Value = "Equal" },
                        new() { Label = "大于 (GreaterThan)", Value = "GreaterThan" },
                        new() { Label = "小于 (LessThan)", Value = "LessThan" },
                        new() { Label = "大于等于 (GreaterOrEqual)", Value = "GreaterOrEqual" },
                        new() { Label = "小于等于 (LessOrEqual)", Value = "LessOrEqual" },
                        new() { Label = "范围内 (Range)", Value = "Range" },
                        new() { Label = "包含 (Contains)", Value = "Contains" },
                        new() { Label = "开头是 (StartsWith)", Value = "StartsWith" },
                        new() { Label = "结尾是 (EndsWith)", Value = "EndsWith" }
                    }
                },
                new() { Name = "ExpectValue", DisplayName = "期望值", DataType = "string", DefaultValue = "4",
                    Description = "判定目标值，如 4（螺钉数）、0（缺陷数）、9.5（尺寸下限）" },
                new() { Name = "ExpectValueMin", DisplayName = "范围最小值", DataType = "string", DefaultValue = "",
                    Description = "用于Range条件，设置范围下限" },
                new() { Name = "ExpectValueMax", DisplayName = "范围最大值", DataType = "string", DefaultValue = "",
                    Description = "用于Range条件，设置范围上限" },
                new() { Name = "MinConfidence", DisplayName = "最小置信度", DataType = "double", DefaultValue = 0.0, MinValue = 0.0, MaxValue = 1.0,
                    Description = "置信度低于此值时判定为NG（0表示不检查置信度）" },
                new() { Name = "OkOutputValue", DisplayName = "OK输出值", DataType = "string", DefaultValue = "1",
                    Description = "判定为OK时输出的值（用于PLC写入）" },
                new() { Name = "NgOutputValue", DisplayName = "NG输出值", DataType = "string", DefaultValue = "0",
                    Description = "判定为NG时输出的值（用于PLC写入）" }
            }
        };

        // ==================== 【第三优先级】新增算子 ====================

        // 1. CLAHE自适应直方图均衡化 (ClaheEnhancement = 71)
        _metadata[OperatorType.ClaheEnhancement] = new OperatorMetadata
        {
            Type = OperatorType.ClaheEnhancement,
            DisplayName = "CLAHE增强",
            Description = "自适应直方图均衡化，用于局部对比度增强",
            Category = "预处理",
            IconName = "clahe",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "增强图像", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "ClipLimit", DisplayName = "裁剪限制", DataType = "double", DefaultValue = 2.0, MinValue = 0, MaxValue = 40,
                    Description = "对比度限制阈值，防止过度放大噪声" },
                new() { Name = "TileWidth", DisplayName = "网格宽度", DataType = "int", DefaultValue = 8, MinValue = 2, MaxValue = 64 },
                new() { Name = "TileHeight", DisplayName = "网格高度", DataType = "int", DefaultValue = 8, MinValue = 2, MaxValue = 64 },
                new() { Name = "ColorSpace", DisplayName = "颜色空间", DataType = "enum", DefaultValue = "Lab",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "Lab - L通道", Value = "Lab" },
                        new() { Label = "HSV - V通道", Value = "HSV" },
                        new() { Label = "灰度", Value = "Gray" },
                        new() { Label = "所有通道", Value = "All" }
                    }
                }
            }
        };

        // 2. 形态学操作 (MorphologicalOperation = 72)
        _metadata[OperatorType.MorphologicalOperation] = new OperatorMetadata
        {
            Type = OperatorType.MorphologicalOperation,
            DisplayName = "形态学操作",
            Description = "腐蚀/膨胀/开运算/闭运算/梯度/顶帽/黑帽",
            Category = "预处理",
            IconName = "morphology",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "处理后图像", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Operation", DisplayName = "操作类型", DataType = "enum", DefaultValue = "Close",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "腐蚀", Value = "Erode" },
                        new() { Label = "膨胀", Value = "Dilate" },
                        new() { Label = "开运算", Value = "Open" },
                        new() { Label = "闭运算", Value = "Close" },
                        new() { Label = "梯度", Value = "Gradient" },
                        new() { Label = "顶帽", Value = "TopHat" },
                        new() { Label = "黑帽", Value = "BlackHat" }
                    }
                },
                new() { Name = "KernelShape", DisplayName = "核形状", DataType = "enum", DefaultValue = "Rect",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "矩形", Value = "Rect" },
                        new() { Label = "十字形", Value = "Cross" },
                        new() { Label = "椭圆形", Value = "Ellipse" }
                    }
                },
                new() { Name = "KernelWidth", DisplayName = "核宽度", DataType = "int", DefaultValue = 3, MinValue = 1, MaxValue = 51 },
                new() { Name = "KernelHeight", DisplayName = "核高度", DataType = "int", DefaultValue = 3, MinValue = 1, MaxValue = 51 },
                new() { Name = "Iterations", DisplayName = "迭代次数", DataType = "int", DefaultValue = 1, MinValue = 1, MaxValue = 10 }
            }
        };

        // 3. 拉普拉斯锐化 (LaplacianSharpen = 74)
        _metadata[OperatorType.LaplacianSharpen] = new OperatorMetadata
        {
            Type = OperatorType.LaplacianSharpen,
            DisplayName = "拉普拉斯锐化",
            Description = "基于拉普拉斯算子的边缘增强",
            Category = "预处理",
            IconName = "sharpen",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "锐化图像", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "KernelSize", DisplayName = "核大小", DataType = "int", DefaultValue = 3, MinValue = 1, MaxValue = 7,
                    Description = "必须是奇数" },
                new() { Name = "Scale", DisplayName = "缩放因子", DataType = "double", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 10.0 },
                new() { Name = "SharpenStrength", DisplayName = "锐化强度", DataType = "double", DefaultValue = 1.0, MinValue = 0, MaxValue = 5.0 }
            }
        };

        // 4. 图像加法 (ImageAdd = 76)
        _metadata[OperatorType.ImageAdd] = new OperatorMetadata
        {
            Type = OperatorType.ImageAdd,
            DisplayName = "图像加法",
            Description = "两幅图像叠加/合并",
            Category = "预处理",
            IconName = "add",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image1", DisplayName = "图像1", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "Image2", DisplayName = "图像2", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "合成图像", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Scale1", DisplayName = "图像1权重", DataType = "double", DefaultValue = 1.0, MinValue = 0, MaxValue = 10.0 },
                new() { Name = "Scale2", DisplayName = "图像2权重", DataType = "double", DefaultValue = 1.0, MinValue = 0, MaxValue = 10.0 },
                new() { Name = "Offset", DisplayName = "亮度偏移", DataType = "double", DefaultValue = 0, MinValue = -255, MaxValue = 255 },
                new() { Name = "SizeMismatchPolicy", DisplayName = "尺寸不一致策略", DataType = "enum", DefaultValue = "Resize", Options = new List<ParameterOption>
                {
                    new() { Label = "Resize", Value = "Resize" },
                    new() { Label = "Fail", Value = "Fail" },
                    new() { Label = "CropToOverlap", Value = "Crop" },
                    new() { Label = "AnchorPaste", Value = "AnchorPaste" }
                } },
                new() { Name = "OffsetX", DisplayName = "图像2偏移X", DataType = "int", DefaultValue = 0, MinValue = -100000, MaxValue = 100000 },
                new() { Name = "OffsetY", DisplayName = "图像2偏移Y", DataType = "int", DefaultValue = 0, MinValue = -100000, MaxValue = 100000 }
            }
        };

        // 5. 图像减法 (ImageSubtract = 77)
        _metadata[OperatorType.ImageSubtract] = new OperatorMetadata
        {
            Type = OperatorType.ImageSubtract,
            DisplayName = "图像减法",
            Description = "差异检测",
            Category = "预处理",
            IconName = "subtract",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image1", DisplayName = "图像1", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "Image2", DisplayName = "图像2", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "差异图像", DataType = PortDataType.Image },
                new() { Name = "MinDifference", DisplayName = "最小差异", DataType = PortDataType.Float },
                new() { Name = "MaxDifference", DisplayName = "最大差异", DataType = PortDataType.Float },
                new() { Name = "MeanDifference", DisplayName = "平均差异", DataType = PortDataType.Float }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "AbsoluteDiff", DisplayName = "绝对差异", DataType = "bool", DefaultValue = true,
                    Description = "使用绝对差异或简单相减" }
            }
        };

        // 6. 图像融合 (ImageBlend = 78)
        _metadata[OperatorType.ImageBlend] = new OperatorMetadata
        {
            Type = OperatorType.ImageBlend,
            DisplayName = "图像融合",
            Description = "加权混合/透明叠加",
            Category = "预处理",
            IconName = "blend",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Background", DisplayName = "背景", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "Foreground", DisplayName = "前景", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "融合图像", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Alpha", DisplayName = "背景权重", DataType = "double", DefaultValue = 0.5, MinValue = 0, MaxValue = 1.0 },
                new() { Name = "Beta", DisplayName = "前景权重", DataType = "double", DefaultValue = 0.5, MinValue = 0, MaxValue = 1.0 },
                new() { Name = "Gamma", DisplayName = "亮度偏移", DataType = "double", DefaultValue = 0, MinValue = -255, MaxValue = 255 }
            }
        };

        // ==================== 【第三优先级】变量和流程控制算子 ====================

        // 1. 变量读取 (VariableRead = 80)
        _metadata[OperatorType.VariableRead] = new OperatorMetadata
        {
            Type = OperatorType.VariableRead,
            DisplayName = "变量读取",
            Description = "从全局变量表读取值",
            Category = "变量",
            IconName = "variable-read",
            InputPorts = new List<PortDefinition>(),
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Value", DisplayName = "值", DataType = PortDataType.Any },
                new() { Name = "Exists", DisplayName = "是否存在", DataType = PortDataType.Boolean },
                new() { Name = "CycleCount", DisplayName = "循环计数", DataType = PortDataType.Integer }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "VariableName", DisplayName = "变量名", DataType = "string", DefaultValue = "",
                    Description = "要读取的变量名称" },
                new() { Name = "DefaultValue", DisplayName = "默认值", DataType = "string", DefaultValue = "0",
                    Description = "变量不存在时的默认值" },
                new() { Name = "DataType", DisplayName = "数据类型", DataType = "enum", DefaultValue = "String",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "字符串", Value = "String" },
                        new() { Label = "整数", Value = "Int" },
                        new() { Label = "浮点数", Value = "Double" },
                        new() { Label = "布尔值", Value = "Bool" }
                    }
                }
            }
        };

        // 2. 变量写入 (VariableWrite = 81)
        _metadata[OperatorType.VariableWrite] = new OperatorMetadata
        {
            Type = OperatorType.VariableWrite,
            DisplayName = "变量写入",
            Description = "写入值到全局变量表",
            Category = "变量",
            IconName = "variable-write",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Value", DisplayName = "值", DataType = PortDataType.Any, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "VariableName", DisplayName = "变量名", DataType = PortDataType.String },
                new() { Name = "Value", DisplayName = "写入的值", DataType = PortDataType.Any },
                new() { Name = "CycleCount", DisplayName = "循环计数", DataType = PortDataType.Integer }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "VariableName", DisplayName = "变量名", DataType = "string", DefaultValue = "",
                    Description = "要写入的变量名称" },
                new() { Name = "DataType", DisplayName = "数据类型", DataType = "enum", DefaultValue = "String",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "字符串", Value = "String" },
                        new() { Label = "整数", Value = "Int" },
                        new() { Label = "浮点数", Value = "Double" },
                        new() { Label = "布尔值", Value = "Bool" }
                    }
                },
                new() { Name = "UseInputValue", DisplayName = "使用输入值", DataType = "bool", DefaultValue = true,
                    Description = "优先使用上游输入的值，否则使用下方静态值" },
                new() { Name = "StaticValue", DisplayName = "静态值", DataType = "string", DefaultValue = "0",
                    Description = "当没有上游输入时使用的值" }
            }
        };

        // 3. 变量递增 (VariableIncrement = 82)
        _metadata[OperatorType.VariableIncrement] = new OperatorMetadata
        {
            Type = OperatorType.VariableIncrement,
            DisplayName = "变量递增",
            Description = "计数器自增/自减，支持重置条件",
            Category = "变量",
            IconName = "counter",
            InputPorts = new List<PortDefinition>(),
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "VariableName", DisplayName = "变量名", DataType = PortDataType.String },
                new() { Name = "PreviousValue", DisplayName = "前值", DataType = PortDataType.Integer },
                new() { Name = "NewValue", DisplayName = "新值", DataType = PortDataType.Integer },
                new() { Name = "Delta", DisplayName = "增量", DataType = PortDataType.Integer },
                new() { Name = "WasReset", DisplayName = "是否已重置", DataType = PortDataType.Boolean }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "VariableName", DisplayName = "变量名", DataType = "string", DefaultValue = "counter",
                    Description = "计数器变量名称" },
                new() { Name = "Delta", DisplayName = "增量", DataType = "int", DefaultValue = 1,
                    Description = "每次递增的值（可为负数实现递减）" },
                new() { Name = "ResetCondition", DisplayName = "重置条件", DataType = "enum", DefaultValue = "None",
                    Description = "满足条件时重置计数器",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "不重置", Value = "None" },
                        new() { Label = "大于阈值", Value = "GreaterThan" },
                        new() { Label = "小于阈值", Value = "LessThan" },
                        new() { Label = "等于阈值", Value = "Equal" }
                    }
                },
                new() { Name = "ResetThreshold", DisplayName = "重置阈值", DataType = "int", DefaultValue = 100 },
                new() { Name = "ResetValue", DisplayName = "重置后值", DataType = "int", DefaultValue = 0,
                    Description = "重置后的起始值" }
            }
        };

        // 4. 异常捕获 (TryCatch = 83)
        _metadata[OperatorType.TryCatch] = new OperatorMetadata
        {
            Type = OperatorType.TryCatch,
            DisplayName = "异常捕获",
            Description = "Try-Catch 流程控制",
            Category = "流程控制",
            IconName = "trycatch",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Input", DisplayName = "输入", DataType = PortDataType.Any, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Try", DisplayName = "Try分支", DataType = PortDataType.Any },
                new() { Name = "Catch", DisplayName = "Catch分支", DataType = PortDataType.Any },
                new() { Name = "Error", DisplayName = "错误信息", DataType = PortDataType.String },
                new() { Name = "HasError", DisplayName = "是否有错", DataType = PortDataType.Boolean }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "EnableCatch", DisplayName = "启用Catch", DataType = "bool", DefaultValue = true,
                    Description = "是否启用异常捕获" },
                new() { Name = "CatchOutputError", DisplayName = "输出错误信息", DataType = "bool", DefaultValue = true },
                new() { Name = "CatchOutputStackTrace", DisplayName = "输出堆栈", DataType = "bool", DefaultValue = false }
            }
        };

        // 5. 循环计数器 (CycleCounter = 84)
        _metadata[OperatorType.CycleCounter] = new OperatorMetadata
        {
            Type = OperatorType.CycleCounter,
            DisplayName = "循环计数器",
            Description = "获取当前循环次数和统计信息",
            Category = "变量",
            IconName = "cycle",
            InputPorts = new List<PortDefinition>(),
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "CycleCount", DisplayName = "当前次数", DataType = PortDataType.Integer },
                new() { Name = "MaxCycles", DisplayName = "最大次数", DataType = PortDataType.Integer },
                new() { Name = "IsLimitReached", DisplayName = "是否达到限制", DataType = PortDataType.Boolean },
                new() { Name = "RemainingCycles", DisplayName = "剩余次数", DataType = PortDataType.Integer },
                new() { Name = "Progress", DisplayName = "进度(%)", DataType = PortDataType.Float }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Action", DisplayName = "操作", DataType = "enum", DefaultValue = "Read",
                    Description = "读取/重置/递增",
                    Options = new List<ParameterOption>
                    {
                        new() { Label = "读取", Value = "Read" },
                        new() { Label = "重置", Value = "Reset" },
                        new() { Label = "递增", Value = "Increment" }
                    }
                },
                new() { Name = "MaxCycles", DisplayName = "最大循环次数", DataType = "int", DefaultValue = 0,
                    Description = "0表示无限制" }
            }
        };

        // ==================== 清霜V3迁移：特征匹配算子 ====================

        // 1. AKAZE特征匹配 (AkazeFeatureMatch = 90)
        _metadata[OperatorType.AkazeFeatureMatch] = new OperatorMetadata
        {
            Type = OperatorType.AkazeFeatureMatch,
            DisplayName = "AKAZE特征匹配",
            Description = "基于AKAZE特征的鲁棒模板匹配，对光照/旋转/缩放变化具有强鲁棒性",
            Category = "匹配定位",
            IconName = "feature-match",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "搜索图像", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "Template", DisplayName = "模板图像", DataType = PortDataType.Image, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "Position", DisplayName = "匹配位置", DataType = PortDataType.Point },
                new() { Name = "IsMatch", DisplayName = "是否匹配", DataType = PortDataType.Boolean },
                new() { Name = "Score", DisplayName = "匹配分数", DataType = PortDataType.Float }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "TemplatePath", DisplayName = "模板路径", DataType = "file", DefaultValue = "" },
                new() { Name = "Threshold", DisplayName = "检测阈值", DataType = "double", DefaultValue = 0.001, MinValue = 0.0001, MaxValue = 0.1 },
                new() { Name = "MinMatchCount", DisplayName = "最小匹配数", DataType = "int", DefaultValue = 10, MinValue = 3, MaxValue = 100 },
                new() { Name = "EnableSymmetryTest", DisplayName = "对称测试", DataType = "bool", DefaultValue = true },
                new() { Name = "MaxFeatures", DisplayName = "最大特征点", DataType = "int", DefaultValue = 500, MinValue = 100, MaxValue = 2000 }
            }
        };

        // 2. ORB特征匹配 (OrbFeatureMatch = 91)
        _metadata[OperatorType.OrbFeatureMatch] = new OperatorMetadata
        {
            Type = OperatorType.OrbFeatureMatch,
            DisplayName = "ORB特征匹配",
            Description = "基于ORB特征的快速模板匹配，适合实时应用",
            Category = "匹配定位",
            IconName = "orb-match",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "搜索图像", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "Template", DisplayName = "模板图像", DataType = PortDataType.Image, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "Position", DisplayName = "匹配位置", DataType = PortDataType.Point },
                new() { Name = "IsMatch", DisplayName = "是否匹配", DataType = PortDataType.Boolean },
                new() { Name = "Score", DisplayName = "匹配分数", DataType = PortDataType.Float }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "TemplatePath", DisplayName = "模板路径", DataType = "file", DefaultValue = "" },
                new() { Name = "MaxFeatures", DisplayName = "最大特征点", DataType = "int", DefaultValue = 500, MinValue = 100, MaxValue = 2000 },
                new() { Name = "ScaleFactor", DisplayName = "尺度因子", DataType = "double", DefaultValue = 1.2, MinValue = 1.0, MaxValue = 2.0 },
                new() { Name = "NLevels", DisplayName = "金字塔层数", DataType = "int", DefaultValue = 8, MinValue = 1, MaxValue = 12 },
                new() { Name = "EdgeThreshold", DisplayName = "边缘阈值", DataType = "int", DefaultValue = 31, MinValue = 3, MaxValue = 100 }
            }
        };

        // 3. 梯度形状匹配 (GradientShapeMatch = 92)
        _metadata[OperatorType.GradientShapeMatch] = new OperatorMetadata
        {
            Type = OperatorType.GradientShapeMatch,
            DisplayName = "梯度形状匹配",
            Description = "基于梯度方向的形状匹配，支持旋转不变性",
            Category = "匹配定位",
            IconName = "shape-match",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "搜索图像", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "Template", DisplayName = "模板图像", DataType = PortDataType.Image, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "Position", DisplayName = "匹配位置", DataType = PortDataType.Point },
                new() { Name = "Angle", DisplayName = "旋转角度", DataType = PortDataType.Float },
                new() { Name = "IsMatch", DisplayName = "是否匹配", DataType = PortDataType.Boolean },
                new() { Name = "Score", DisplayName = "匹配分数", DataType = PortDataType.Float }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "TemplatePath", DisplayName = "模板路径", DataType = "file", DefaultValue = "" },
                new() { Name = "MinScore", DisplayName = "最小分数(%)", DataType = "double", DefaultValue = 80.0, MinValue = 0.0, MaxValue = 100.0 },
                new() { Name = "AngleRange", DisplayName = "角度范围(±)", DataType = "int", DefaultValue = 180, MinValue = 0, MaxValue = 180 },
                new() { Name = "AngleStep", DisplayName = "角度步长", DataType = "int", DefaultValue = 1, MinValue = 1, MaxValue = 10 },
                new() { Name = "MagnitudeThreshold", DisplayName = "梯度阈值", DataType = "int", DefaultValue = 30, MinValue = 0, MaxValue = 255 }
            }
        };

        // 4. 金字塔形状匹配 (PyramidShapeMatch = 93)
        _metadata[OperatorType.PyramidShapeMatch] = new OperatorMetadata
        {
            Type = OperatorType.PyramidShapeMatch,
            DisplayName = "金字塔形状匹配",
            Description = "多尺度金字塔形状匹配，速度快，适合大尺寸图像",
            Category = "匹配定位",
            IconName = "pyramid-match",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "搜索图像", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "Template", DisplayName = "模板图像", DataType = PortDataType.Image, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "Position", DisplayName = "匹配位置", DataType = PortDataType.Point },
                new() { Name = "Angle", DisplayName = "旋转角度", DataType = PortDataType.Float },
                new() { Name = "IsMatch", DisplayName = "是否匹配", DataType = PortDataType.Boolean },
                new() { Name = "Score", DisplayName = "匹配分数", DataType = PortDataType.Float }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "TemplatePath", DisplayName = "模板路径", DataType = "file", DefaultValue = "" },
                new() { Name = "MinScore", DisplayName = "最小分数(%)", DataType = "double", DefaultValue = 80.0, MinValue = 0.0, MaxValue = 100.0 },
                new() { Name = "AngleRange", DisplayName = "角度范围(±)", DataType = "int", DefaultValue = 180, MinValue = 0, MaxValue = 180 },
                new() { Name = "PyramidLevels", DisplayName = "金字塔层数", DataType = "int", DefaultValue = 3, MinValue = 1, MaxValue = 5 },
                new() { Name = "MagnitudeThreshold", DisplayName = "梯度阈值", DataType = "int", DefaultValue = 30, MinValue = 0, MaxValue = 255 }
            }
        };

        // 5. 双模态投票 (DualModalVoting = 94)
        _metadata[OperatorType.DualModalVoting] = new OperatorMetadata
        {
            Type = OperatorType.DualModalVoting,
            DisplayName = "双模态投票",
            Description = "结合深度学习和传统算法结果进行投票决策",
            Category = "AI检测",
            IconName = "voting",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "DLResult", DisplayName = "深度学习结果", DataType = PortDataType.Any, IsRequired = true },
                new() { Name = "TraditionalResult", DisplayName = "传统算法结果", DataType = PortDataType.Any, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "IsOk", DisplayName = "是否OK", DataType = PortDataType.Boolean },
                new() { Name = "Confidence", DisplayName = "综合置信度", DataType = PortDataType.Float },
                new() { Name = "JudgmentValue", DisplayName = "判定值", DataType = PortDataType.String }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "VotingStrategy", DisplayName = "投票策略", DataType = "enum", DefaultValue = "WeightedAverage", Options = new List<ParameterOption>
                {
                    new() { Label = "加权平均", Value = "WeightedAverage" },
                    new() { Label = "一致同意", Value = "Unanimous" },
                    new() { Label = "多数表决", Value = "Majority" },
                    new() { Label = "优先深度学习", Value = "PrioritizeDeepLearning" },
                    new() { Label = "优先传统算法", Value = "PrioritizeTraditional" }
                } },
                new() { Name = "DLWeight", DisplayName = "DL权重", DataType = "double", DefaultValue = 0.6, MinValue = 0.0, MaxValue = 1.0 },
                new() { Name = "TraditionalWeight", DisplayName = "传统算法权重", DataType = "double", DefaultValue = 0.4, MinValue = 0.0, MaxValue = 1.0 },
                new() { Name = "ConfidenceThreshold", DisplayName = "置信度阈值", DataType = "double", DefaultValue = 0.5, MinValue = 0.0, MaxValue = 1.0 },
                new() { Name = "OkOutputValue", DisplayName = "OK输出值", DataType = "string", DefaultValue = "1" },
                new() { Name = "NgOutputValue", DisplayName = "NG输出值", DataType = "string", DefaultValue = "0" }
            }
        };
        // ==================== Sprint 2: ForEach 与数据操作算子 ====================

        // 1. ForEach 循环 (ForEach = 100)
        _metadata[OperatorType.ForEach] = new OperatorMetadata
        {
            Type = OperatorType.ForEach,
            DisplayName = "ForEach 循环",
            Description = "对集合中的每个元素执行子图",
            Category = "流程控制",
            IconName = "loop",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Items", DisplayName = "集合", DataType = PortDataType.Any, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Results", DisplayName = "结果列表", DataType = PortDataType.Any }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "IoMode", DisplayName = "执行模式", DataType = "enum", DefaultValue = "Parallel", Options = new List<ParameterOption> { new() { Label = "并行(纯计算)", Value = "Parallel" }, new() { Label = "串行(含通信)", Value = "Sequential" } } },
                new() { Name = "MaxParallelism", DisplayName = "最大并行度", DataType = "int", DefaultValue = 8, MinValue = 1, MaxValue = 64 },
                new() { Name = "Timeout", DisplayName = "超时(ms)", DataType = "int", DefaultValue = 30000 },
                new() { Name = "FailFast", DisplayName = "遇错即停", DataType = "bool", DefaultValue = true }
            }
        };

        // 2. 数组索引器 (ArrayIndexer = 101)
        _metadata[OperatorType.ArrayIndexer] = new OperatorMetadata
        {
            Type = OperatorType.ArrayIndexer,
            DisplayName = "数组索引器",
            Description = "从列表中按索引或条件提取元素",
            Category = "数据处理",
            IconName = "index",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "List", DisplayName = "列表", DataType = PortDataType.Any, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Item", DisplayName = "元素", DataType = PortDataType.Any }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Mode", DisplayName = "提取模式", DataType = "enum", DefaultValue = "Index", Options = new List<ParameterOption>
                {
                    new() { Label = "按索引", Value = "Index" },
                    new() { Label = "最大置信度", Value = "MaxConfidence" },
                    new() { Label = "最大面积", Value = "MaxArea" },
                    new() { Label = "最小面积", Value = "MinArea" },
                    new() { Label = "第一个", Value = "First" },
                    new() { Label = "最后一个", Value = "Last" }
                } },
                new() { Name = "Index", DisplayName = "索引", DataType = "int", DefaultValue = 0 }
            }
        };

        // 3. JSON 提取器 (JsonExtractor = 102)
        _metadata[OperatorType.JsonExtractor] = new OperatorMetadata
        {
            Type = OperatorType.JsonExtractor,
            DisplayName = "JSON 提取器",
            Description = "按 JSONPath 从字符串中提取字段",
            Category = "数据处理",
            IconName = "json",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Json", DisplayName = "JSON字符串", DataType = PortDataType.String, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Value", DisplayName = "提取值", DataType = PortDataType.Any },
                new() { Name = "IsSuccess", DisplayName = "是否成功", DataType = PortDataType.Boolean }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "JsonPath", DisplayName = "JSONPath", DataType = "string", DefaultValue = "$.data" }
            }
        };

        // ==================== Sprint 3: 基础算子重构与扩充 ====================

        // 1. 数值计算 (MathOperation = 110)
        _metadata[OperatorType.MathOperation] = new OperatorMetadata
        {
            Type = OperatorType.MathOperation,
            DisplayName = "数值计算",
            Description = "支持加减乘除、取绝对值、开方等常用运算",
            Category = "数据处理",
            IconName = "calc",
            Keywords = new[] { "计算", "数学", "加减乘除", "数值", "判断大小", "运算", "Math", "Calculate", "Add", "Subtract", "Multiply", "Divide" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "ValueA", DisplayName = "数值 A", DataType = PortDataType.Float, IsRequired = true },
                new() { Name = "ValueB", DisplayName = "数值 B", DataType = PortDataType.Float, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Result", DisplayName = "结果", DataType = PortDataType.Float },
                new() { Name = "IsPositive", DisplayName = "大于零", DataType = PortDataType.Boolean }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Operation", DisplayName = "运算类型", DataType = "enum", DefaultValue = "Add", Options = new List<ParameterOption>
                {
                    new() { Label = "加 (+)", Value = "Add" },
                    new() { Label = "减 (-)", Value = "Subtract" },
                    new() { Label = "乘 (×)", Value = "Multiply" },
                    new() { Label = "除 (÷)", Value = "Divide" },
                    new() { Label = "绝对值 (Abs)", Value = "Abs" },
                    new() { Label = "取小 (Min)", Value = "Min" },
                    new() { Label = "取大 (Max)", Value = "Max" },
                    new() { Label = "幂运算 (Power)", Value = "Power" },
                    new() { Label = "平方根 (Sqrt)", Value = "Sqrt" },
                    new() { Label = "取整 (Round)", Value = "Round" },
                    new() { Label = "取余 (Modulo)", Value = "Modulo" }
                } }
            }
        };

        // 2. 逻辑门 (LogicGate = 111)
        _metadata[OperatorType.LogicGate] = new OperatorMetadata
        {
            Type = OperatorType.LogicGate,
            DisplayName = "逻辑门",
            Description = "布尔逻辑运算 (AND, OR, NOT, XOR...)",
            Category = "通用",
            IconName = "logic",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "InputA", DisplayName = "输入 A", DataType = PortDataType.Boolean, IsRequired = true },
                new() { Name = "InputB", DisplayName = "输入 B", DataType = PortDataType.Boolean, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Result", DisplayName = "输出", DataType = PortDataType.Boolean }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Operation", DisplayName = "逻辑操作", DataType = "enum", DefaultValue = "AND", Options = new List<ParameterOption>
                {
                    new() { Label = "AND (与)", Value = "AND" },
                    new() { Label = "OR (或)", Value = "OR" },
                    new() { Label = "NOT (非)", Value = "NOT" },
                    new() { Label = "XOR (异或)", Value = "XOR" },
                    new() { Label = "NAND (与非)", Value = "NAND" },
                    new() { Label = "NOR (或非)", Value = "NOR" }
                } }
            }
        };

        // 3. 类型转换 (TypeConvert = 112)
        _metadata[OperatorType.TypeConvert] = new OperatorMetadata
        {
            Type = OperatorType.TypeConvert,
            DisplayName = "类型转换",
            Description = "在不同数据类型间进行强制转换",
            Category = "通用",
            IconName = "convert",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Input", DisplayName = "输入", DataType = PortDataType.Any, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Output", DisplayName = "输出", DataType = PortDataType.Any }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "TargetType", DisplayName = "目标类型", DataType = "enum", DefaultValue = "String", Options = new List<ParameterOption>
                {
                    new() { Label = "String", Value = "String" },
                    new() { Label = "Float", Value = "Float" },
                    new() { Label = "Integer", Value = "Integer" },
                    new() { Label = "Boolean", Value = "Boolean" }
                } },
                new() { Name = "Format", DisplayName = "格式字符串", DataType = "string", DefaultValue = "" }
            }
        };

        // 4. HTTP 请求 (HttpRequest = 113)
        _metadata[OperatorType.HttpRequest] = new OperatorMetadata
        {
            Type = OperatorType.HttpRequest,
            DisplayName = "HTTP 请求",
            Description = "调用外部 REST API",
            Category = "通信",
            IconName = "http",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Body", DisplayName = "请求体", DataType = PortDataType.String, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Response", DisplayName = "响应内容", DataType = PortDataType.String },
                new() { Name = "StatusCode", DisplayName = "状态码", DataType = PortDataType.Integer },
                new() { Name = "IsSuccess", DisplayName = "是否成功", DataType = PortDataType.Boolean }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Url", DisplayName = "API 地址", DataType = "string", DefaultValue = "http://localhost:5000/api" },
                new() { Name = "Method", DisplayName = "方法", DataType = "enum", DefaultValue = "POST", Options = new List<ParameterOption>
                {
                    new() { Label = "GET", Value = "GET" },
                    new() { Label = "POST", Value = "POST" },
                    new() { Label = "PUT", Value = "PUT" },
                    new() { Label = "DELETE", Value = "DELETE" }
                } },
                new() { Name = "Timeout", DisplayName = "超时(ms)", DataType = "int", DefaultValue = 5000 },
                new() { Name = "MaxRetries", DisplayName = "最大重试", DataType = "int", DefaultValue = 3 }
            }
        };

        // 5. MQTT 发布 (MqttPublish = 114)
        _metadata[OperatorType.MqttPublish] = new OperatorMetadata
        {
            Type = OperatorType.MqttPublish,
            DisplayName = "MQTT 发布",
            Description = "向消息队列推送数据",
            Category = "通信",
            IconName = "mqtt",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Payload", DisplayName = "消息负载", DataType = PortDataType.Any, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "IsSuccess", DisplayName = "是否成功", DataType = PortDataType.Boolean }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Broker", DisplayName = "Broker地址", DataType = "string", DefaultValue = "localhost" },
                new() { Name = "Port", DisplayName = "端口", DataType = "int", DefaultValue = 1883 },
                new() { Name = "Topic", DisplayName = "主题", DataType = "string", DefaultValue = "cv/results" },
                new() { Name = "Qos", DisplayName = "QoS", DataType = "int", DefaultValue = 1 }
            }
        };

        // 6. 字符串格式化 (StringFormat = 115)
        _metadata[OperatorType.StringFormat] = new OperatorMetadata
        {
            Type = OperatorType.StringFormat,
            DisplayName = "字符串格式化",
            Description = "按模板生成字符串",
            Category = "通用",
            IconName = "text",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Arg1", DisplayName = "参数 1", DataType = PortDataType.Any },
                new() { Name = "Arg2", DisplayName = "参数 2", DataType = PortDataType.Any }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Result", DisplayName = "结果", DataType = PortDataType.String }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Template", DisplayName = "模板", DataType = "string", DefaultValue = "Result is {0} and {1}" }
            }
        };

        // 7. 图像保存 (ImageSave = 116)
        _metadata[OperatorType.ImageSave] = new OperatorMetadata
        {
            Type = OperatorType.ImageSave,
            DisplayName = "图像保存",
            Description = "保存检测图像到本地硬盘",
            Category = "输出",
            IconName = "save",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "FilePath", DisplayName = "保存路径", DataType = PortDataType.String },
                new() { Name = "IsSuccess", DisplayName = "是否成功", DataType = PortDataType.Boolean }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Directory", DisplayName = "目录", DataType = "string", DefaultValue = "C:\\ClearVision\\NG_Images" },
                new() { Name = "FileNameTemplate", DisplayName = "命名规则", DataType = "string", DefaultValue = "NG_{yyyyMMdd_HHmmss}_{Guid}.jpg" },
                new() { Name = "Quality", DisplayName = "质量", DataType = "int", DefaultValue = 90, MinValue = 1, MaxValue = 100 }
            }
        };

        // ==================== Phase 3: 缺失补齐 ====================

        // 1. OCR识别 (OcrRecognition = 117)
        _metadata[OperatorType.OcrRecognition] = new OperatorMetadata
        {
            Type = OperatorType.OcrRecognition,
            DisplayName = "OCR 识别",
            Description = "识别图像中的文本内容",
            Category = "识别",
            IconName = "text-recognition",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Text", DisplayName = "识别文本", DataType = PortDataType.String },
                new() { Name = "IsSuccess", DisplayName = "成功", DataType = PortDataType.Boolean }
            }
        };

        // 2. 图像对比 (ImageDiff = 118)
        _metadata[OperatorType.ImageDiff] = new OperatorMetadata
        {
            Type = OperatorType.ImageDiff,
            DisplayName = "图像对比",
            Description = "分析两幅图像的差异",
            Category = "预处理",
            IconName = "diff",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "BaseImage", DisplayName = "基准图", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "CompareImage", DisplayName = "对比图", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "DiffImage", DisplayName = "差异图", DataType = PortDataType.Image },
                new() { Name = "DiffRate", DisplayName = "差异率", DataType = PortDataType.Float }
            }
        };

        // 3. 统计分析 (Statistics = 119) - Sprint 3 Task 3.6e CPK 增强
        _metadata[OperatorType.Statistics] = new OperatorMetadata
        {
            Type = OperatorType.Statistics,
            DisplayName = "统计分析",
            Description = "计算均值、标准差、CPK 等质量统计指标",
            Category = "通用",
            IconName = "stats",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Value", DisplayName = "输入值", DataType = PortDataType.Float, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Mean", DisplayName = "均值", DataType = PortDataType.Float },
                new() { Name = "StdDev", DisplayName = "标准差", DataType = PortDataType.Float },
                new() { Name = "Count", DisplayName = "样本数", DataType = PortDataType.Integer },
                new() { Name = "Min", DisplayName = "最小值", DataType = PortDataType.Float },
                new() { Name = "Max", DisplayName = "最大值", DataType = PortDataType.Float },
                new() { Name = "Cpk", DisplayName = "过程能力指数", DataType = PortDataType.Float },
                new() { Name = "IsCapable", DisplayName = "能力达标", DataType = PortDataType.Boolean }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "USL", DisplayName = "规格上限", DataType = "double", DefaultValue = "", Description = "Upper Specification Limit，留空则不计算 CPK" },
                new() { Name = "LSL", DisplayName = "规格下限", DataType = "double", DefaultValue = "", Description = "Lower Specification Limit，留空则不计算 CPK" },
                new() { Name = "WindowSize", DisplayName = "Window Size", DataType = "int", DefaultValue = 1000, MinValue = 2, MaxValue = 50000 },
                new() { Name = "StateTtlMinutes", DisplayName = "State TTL Minutes", DataType = "int", DefaultValue = 120, MinValue = 1, MaxValue = 10080 },
                new() { Name = "Reset", DisplayName = "Reset", DataType = "bool", DefaultValue = false }
            }
        };

        // ==================== 胶水算子 ====================

        // Comparator（数值比较算子）
        _metadata[OperatorType.Comparator] = new OperatorMetadata
        {
            Type = OperatorType.Comparator,
            DisplayName = "数值比较",
            Description = "比较两个数值的大小关系，输出布尔判定结果与差值",
            Category = "逻辑控制",
            IconName = "compare",
            Keywords = new[] { "比较", "判断", "大于", "小于", "等于", "超限", "阈值判定", "公差", "Compare", "Threshold", "GreaterThan", "LessThan" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "ValueA", DisplayName = "数值 A", DataType = PortDataType.Float, IsRequired = true },
                new() { Name = "ValueB", DisplayName = "数值 B", DataType = PortDataType.Float, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Result", DisplayName = "判定结果", DataType = PortDataType.Boolean },
                new() { Name = "Difference", DisplayName = "差值", DataType = PortDataType.Float }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Condition", DisplayName = "比较条件", DataType = "enum", DefaultValue = "GreaterThan", Options = new List<ParameterOption>
                {
                    new() { Label = "大于", Value = "GreaterThan" },
                    new() { Label = "大于等于", Value = "GreaterThanOrEqual" },
                    new() { Label = "小于", Value = "LessThan" },
                    new() { Label = "小于等于", Value = "LessThanOrEqual" },
                    new() { Label = "等于", Value = "Equal" },
                    new() { Label = "不等于", Value = "NotEqual" },
                    new() { Label = "在范围内", Value = "InRange" }
                } },
                new() { Name = "CompareValue", DisplayName = "默认比较值", DataType = "double", DefaultValue = 0.0, Description = "当 ValueB 未连线时使用此值" },
                new() { Name = "Tolerance", DisplayName = "容差", DataType = "double", DefaultValue = 0.0001, MinValue = 0.0, Description = "等于/不等于判断的容差" },
                new() { Name = "RangeMin", DisplayName = "范围下限", DataType = "double", DefaultValue = 0.0, Description = "InRange 模式的下限" },
                new() { Name = "RangeMax", DisplayName = "范围上限", DataType = "double", DefaultValue = 1.0, Description = "InRange 模式的上限" }
            }
        };

        // Aggregator（数据聚合算子）
        _metadata[OperatorType.Aggregator] = new OperatorMetadata
        {
            Type = OperatorType.Aggregator,
            DisplayName = "数据聚合",
            Description = "将多路输入数据合并为列表，并提取极值与均值",
            Category = "数据处理",
            IconName = "merge",
            Keywords = new[] { "聚合", "合并", "汇总", "最大值", "最小值", "均值", "多路合并", "Aggregate", "Merge", "Max", "Min", "Average" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Value1", DisplayName = "值 1", DataType = PortDataType.Any, IsRequired = false },
                new() { Name = "Value2", DisplayName = "值 2", DataType = PortDataType.Any, IsRequired = false },
                new() { Name = "Value3", DisplayName = "值 3", DataType = PortDataType.Any, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "MergedList", DisplayName = "合并列表", DataType = PortDataType.Any },
                new() { Name = "MaxValue", DisplayName = "最大值", DataType = PortDataType.Float },
                new() { Name = "MinValue", DisplayName = "最小值", DataType = PortDataType.Float },
                new() { Name = "Average", DisplayName = "均值", DataType = PortDataType.Float }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Mode", DisplayName = "聚合模式", DataType = "enum", DefaultValue = "Merge", Options = new List<ParameterOption>
                {
                    new() { Label = "合并列表", Value = "Merge" },
                    new() { Label = "提取最大值", Value = "Max" },
                    new() { Label = "提取最小值", Value = "Min" },
                    new() { Label = "计算均值", Value = "Average" }
                } }
            }
        };

        // Delay（延时算子）
        _metadata[OperatorType.Delay] = new OperatorMetadata
        {
            Type = OperatorType.Delay,
            DisplayName = "延时",
            Description = "等待指定时间后继续执行，常用于通信前等待下位机就绪",
            Category = "流程控制",
            IconName = "timer",
            Keywords = new[] { "延时", "等待", "暂停", "定时", "休眠", "Delay", "Wait", "Sleep", "Timer" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Input", DisplayName = "透传输入", DataType = PortDataType.Any, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Output", DisplayName = "透传输出", DataType = PortDataType.Any },
                new() { Name = "ElapsedMs", DisplayName = "实际耗时(ms)", DataType = PortDataType.Integer }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Milliseconds", DisplayName = "延时毫秒", DataType = "int", DefaultValue = 200, MinValue = 0, MaxValue = 60000 }
            }
        };

        // Comment（注释算子）
        _metadata[OperatorType.Comment] = new OperatorMetadata
        {
            Type = OperatorType.Comment,
            DisplayName = "注释",
            Description = "在工作流中添加说明文本，不影响数据流，仅用于标注设计意图",
            Category = "辅助",
            IconName = "comment",
            Keywords = new[] { "注释", "备注", "说明", "标注", "文本", "Comment", "Note", "Annotation" },
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Input", DisplayName = "透传输入", DataType = PortDataType.Any, IsRequired = false }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Output", DisplayName = "透传输出", DataType = PortDataType.Any },
                new() { Name = "Message", DisplayName = "注释内容", DataType = PortDataType.String }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "Text", DisplayName = "注释文本", DataType = "string", DefaultValue = "" }
            }
        };
        OperatorFactoryMetadataMerge.Apply(_metadata);
    }
}




