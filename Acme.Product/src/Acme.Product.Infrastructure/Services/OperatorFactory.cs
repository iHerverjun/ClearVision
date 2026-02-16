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
            Category = "输入",
            IconName = "camera",
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image }
            },
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "sourceType", DisplayName = "采集源", DataType = "enum", DefaultValue = "camera", Options = new List<ParameterOption> { new() { Label = "相机", Value = "camera" }, new() { Label = "文件", Value = "file" } } },
                new() { Name = "filePath", DisplayName = "文件路径", DataType = "file", DefaultValue = "" },
                new() { Name = "exposureTime", DisplayName = "曝光时间", DataType = "int", DefaultValue = 5000, MinValue = 100, MaxValue = 1000000 },
                new() { Name = "gain", DisplayName = "增益", DataType = "float", DefaultValue = 1.0, MinValue = 0.0, MaxValue = 24.0 }
            }
        };

        // 预处理
        _metadata[OperatorType.Preprocessing] = new OperatorMetadata
        {
            Type = OperatorType.Preprocessing,
            DisplayName = "预处理",
            Description = "图像预处理操作",
            Category = "预处理",
            IconName = "preprocess"
        };

        // 滤波
        _metadata[OperatorType.Filtering] = new OperatorMetadata
        {
            Type = OperatorType.Filtering,
            DisplayName = "滤波",
            Description = "图像滤波降噪",
            Category = "预处理",
            IconName = "filter",
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
            Description = "检测图像边缘",
            Category = "特征提取",
            IconName = "edge",
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
            Description = "图像阈值分割",
            Category = "预处理",
            IconName = "threshold",
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
            DisplayName = "形态学",
            Description = "形态学操作（腐蚀、膨胀、开闭运算）",
            Category = "预处理",
            IconName = "morphology",
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
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "标记图像", DataType = PortDataType.Image },
                new() { Name = "Blobs", DisplayName = "Blob数据", DataType = PortDataType.Contour }
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
            Description = "图像模板匹配",
            Category = "匹配定位",
            IconName = "template",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true },
                new() { Name = "Template", DisplayName = "模板图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "Position", DisplayName = "匹配位置", DataType = PortDataType.Point }
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
            Description = "几何测量",
            Category = "检测",
            IconName = "measure",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
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
            Description = "查找图像中的轮廓",
            Category = "特征提取",
            IconName = "contour",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image }
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
            Description = "一维码/二维码识别",
            Category = "识别",
            IconName = "barcode",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "Text", DisplayName = "识别内容", DataType = PortDataType.String }
            }
        };

        // 深度学习
        _metadata[OperatorType.DeepLearning] = new OperatorMetadata
        {
            Type = OperatorType.DeepLearning,
            DisplayName = "深度学习",
            Description = "AI缺陷检测",
            Category = "AI检测",
            IconName = "ai",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "Defects", DisplayName = "缺陷列表", DataType = PortDataType.Contour }
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
            Description = "输出检测结果",
            Category = "输出",
            IconName = "output",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "图像", DataType = PortDataType.Image, IsRequired = false },
                new() { Name = "Result", DisplayName = "结果", DataType = PortDataType.Any, IsRequired = false }
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

        // 2. 双边滤波 (BilateralFilter = 14)
        _metadata[OperatorType.BilateralFilter] = new OperatorMetadata
        {
            Type = OperatorType.BilateralFilter,
            DisplayName = "双边滤波",
            Description = "边缘保留的平滑滤波",
            Category = "预处理",
            IconName = "filter",
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
            Description = "霍夫圆检测与测量",
            Category = "检测",
            IconName = "circle-measure",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "Radius", DisplayName = "半径", DataType = PortDataType.Float },
                new() { Name = "Center", DisplayName = "圆心", DataType = PortDataType.Point }
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
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "Angle", DisplayName = "角度", DataType = PortDataType.Float },
                new() { Name = "Length", DisplayName = "长度", DataType = PortDataType.Float }
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
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "Area", DisplayName = "面积", DataType = PortDataType.Float },
                new() { Name = "Perimeter", DisplayName = "周长", DataType = PortDataType.Float }
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
            Description = "平行度/垂直度测量",
            Category = "检测",
            IconName = "geometric-tolerance",
            InputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
            },
            OutputPorts = new List<PortDefinition>
            {
                new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
                new() { Name = "Tolerance", DisplayName = "公差值", DataType = PortDataType.Float }
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
            Description = "根据条件执行不同分支",
            Category = "控制",
            IconName = "branch",
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
                    DefaultValue = 5, MinValue = 3, MaxValue = 10000 }
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
                        new() { Label = "高斯拟合", Value = "GaussianFit" }
                    }
                }
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
                new() { Name = "WriteValue", DisplayName = "写入值", DataType = "string", DefaultValue = "" }
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
                new() { Name = "WriteValue", DisplayName = "写入值", DataType = "string", DefaultValue = "" }
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
                new() { Name = "WriteValue", DisplayName = "写入值", DataType = "string", DefaultValue = "" }
            }
        };
    }
}
