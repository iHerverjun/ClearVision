// FlowTemplateService.cs
// 流程模板服务
// 负责流程模板加载、查询与模板化生成支持
// 作者：蘅芜君
using System.Text.Json;
using Acme.Product.Core.Entities;

namespace Acme.Product.Infrastructure.AI;

public interface IFlowTemplateService
{
    Task<IReadOnlyList<FlowTemplate>> GetTemplatesAsync(
        string? industry = null,
        CancellationToken cancellationToken = default);

    Task<FlowTemplate?> GetTemplateAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<FlowTemplate> SaveTemplateAsync(
        FlowTemplate template,
        CancellationToken cancellationToken = default);
}

public class FlowTemplateService : IFlowTemplateService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _templateFilePath;
    private readonly object _syncRoot = new();

    public FlowTemplateService(string? storageRootPath = null)
    {
        var rootPath = string.IsNullOrWhiteSpace(storageRootPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClearVision")
            : storageRootPath;

        var templateDirectory = Path.Combine(rootPath, "templates");
        _templateFilePath = Path.Combine(templateDirectory, "flow_templates.json");
        EnsureTemplateStore();
    }

    public Task<IReadOnlyList<FlowTemplate>> GetTemplatesAsync(
        string? industry = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var templates = LoadTemplates();
        if (string.IsNullOrWhiteSpace(industry))
            return Task.FromResult<IReadOnlyList<FlowTemplate>>(templates);

        var filtered = templates
            .Where(template => template.Industry.Equals(industry, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Task.FromResult<IReadOnlyList<FlowTemplate>>(filtered);
    }

    public Task<FlowTemplate?> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var template = LoadTemplates().FirstOrDefault(item => item.Id == id);
        return Task.FromResult(template);
    }

    public Task<FlowTemplate> SaveTemplateAsync(FlowTemplate template, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (template == null)
            throw new ArgumentNullException(nameof(template));

        lock (_syncRoot)
        {
            var templates = LoadTemplates();
            var existing = templates.FirstOrDefault(item => item.Id == template.Id);
            if (existing == null)
            {
                if (template.Id == Guid.Empty)
                    template.Id = Guid.NewGuid();

                template.CreatedAt = template.CreatedAt == default ? DateTime.UtcNow : template.CreatedAt;
                templates.Add(template);
            }
            else
            {
                existing.Name = template.Name;
                existing.Description = template.Description;
                existing.Industry = template.Industry;
                existing.Tags = template.Tags;
                existing.FlowJson = template.FlowJson;
            }

            SaveTemplates(templates);
        }

        return Task.FromResult(template);
    }

    private void EnsureTemplateStore()
    {
        var directory = Path.GetDirectoryName(_templateFilePath)
                        ?? throw new InvalidOperationException("Template directory path is invalid.");
        Directory.CreateDirectory(directory);

        if (File.Exists(_templateFilePath))
            return;

        var defaults = CreateBuiltInTemplates();
        SaveTemplates(defaults);
    }

    private List<FlowTemplate> LoadTemplates()
    {
        lock (_syncRoot)
        {
            EnsureTemplateStore();

            try
            {
                var json = File.ReadAllText(_templateFilePath);
                var templates = JsonSerializer.Deserialize<List<FlowTemplate>>(json, _jsonOptions);

                if (templates == null || templates.Count == 0)
                {
                    templates = CreateBuiltInTemplates();
                    SaveTemplates(templates);
                }

                return templates;
            }
            catch
            {
                var templates = CreateBuiltInTemplates();
                SaveTemplates(templates);
                return templates;
            }
        }
    }

    private void SaveTemplates(List<FlowTemplate> templates)
    {
        var json = JsonSerializer.Serialize(templates, _jsonOptions);
        File.WriteAllText(_templateFilePath, json);
    }

    private static List<FlowTemplate> CreateBuiltInTemplates()
    {
        return new List<FlowTemplate>
        {
            CreateTemplate(
                "传统缺陷检测",
                "相机采集后进行去噪、二值化和 Blob 缺陷分析。",
                "3C电子",
                ["缺陷检测", "传统视觉", "表面检测"],
                new
                {
                    explanation = "基础传统缺陷检测流程，适合规则表面与稳定光照场景。",
                    operators = new object[]
                    {
                        Node("op_1", "ImageAcquisition", "图像采集", new Dictionary<string, string> { ["sourceType"] = "camera" }),
                        Node("op_2", "Filtering", "降噪", new Dictionary<string, string> { ["KernelSize"] = "5" }),
                        Node("op_3", "Thresholding", "二值化", new Dictionary<string, string> { ["UseOtsu"] = "true" }),
                        Node("op_4", "BlobAnalysis", "缺陷分析", new Dictionary<string, string> { ["MinArea"] = "50", ["MaxArea"] = "5000" }),
                        Node("op_5", "ResultOutput", "结果输出")
                    },
                    connections = new object[]
                    {
                        Link("op_1", "Image", "op_2", "Image"),
                        Link("op_2", "Image", "op_3", "Image"),
                        Link("op_3", "Image", "op_4", "Image"),
                        Link("op_4", "BlobCount", "op_5", "Result")
                    },
                    parametersNeedingReview = new Dictionary<string, List<string>>
                    {
                        ["op_4"] = ["MinArea", "MaxArea"]
                    }
                }),
            CreateTemplate(
                "AI缺陷检测",
                "深度学习推理 + 分支判定 + 通信输出。",
                "半导体",
                ["AI", "缺陷检测", "PLC通信"],
                new
                {
                    explanation = "适用于复杂纹理与多缺陷类型场景。",
                    operators = new object[]
                    {
                        Node("op_1", "ImageAcquisition", "图像采集", new Dictionary<string, string> { ["sourceType"] = "camera" }),
                        Node("op_2", "ImageResize", "尺寸适配", new Dictionary<string, string> { ["Width"] = "640", ["Height"] = "640" }),
                        Node("op_3", "DeepLearning", "AI检测", new Dictionary<string, string> { ["Confidence"] = "0.5" }),
                        Node("op_4", "ConditionalBranch", "缺陷分支", new Dictionary<string, string> { ["Condition"] = "GreaterThan", ["CompareValue"] = "0" }),
                        Node("op_5", "ModbusCommunication", "NG信号"),
                        Node("op_6", "ResultOutput", "结果输出")
                    },
                    connections = new object[]
                    {
                        Link("op_1", "Image", "op_2", "Image"),
                        Link("op_2", "Image", "op_3", "Image"),
                        Link("op_3", "DefectCount", "op_4", "Value"),
                        Link("op_4", "True", "op_5", "Data"),
                        Link("op_3", "Image", "op_6", "Image")
                    },
                    parametersNeedingReview = new Dictionary<string, List<string>>
                    {
                        ["op_3"] = ["ModelPath", "InputSize"],
                        ["op_5"] = ["IpAddress", "Port"]
                    }
                }),
            CreateTemplate(
                "尺寸间距测量",
                "边缘检测 + 间距测量 + 坐标换算。",
                "汽车零部件",
                ["测量", "间距", "标定"],
                new
                {
                    explanation = "用于孔距、焊点间距、引脚间距等场景。",
                    operators = new object[]
                    {
                        Node("op_1", "ImageAcquisition", "图像采集"),
                        Node("op_2", "Filtering", "预处理"),
                        Node("op_3", "EdgeDetection", "边缘检测"),
                        Node("op_4", "GapMeasurement", "间距测量", new Dictionary<string, string> { ["ExpectedWidth"] = "0.5", ["Tolerance"] = "0.05" }),
                        Node("op_5", "CoordinateTransform", "像素转毫米"),
                        Node("op_6", "ResultOutput", "测量结果")
                    },
                    connections = new object[]
                    {
                        Link("op_1", "Image", "op_2", "Image"),
                        Link("op_2", "Image", "op_3", "Image"),
                        Link("op_3", "Image", "op_4", "Image"),
                        Link("op_4", "Width", "op_5", "PixelX"),
                        Link("op_5", "PhysicalX", "op_6", "Result")
                    },
                    parametersNeedingReview = new Dictionary<string, List<string>>
                    {
                        ["op_5"] = ["PixelSize", "CalibrationFile"]
                    }
                }),
            CreateTemplate(
                "条码读取写PLC",
                "条码识别结果写入 PLC。",
                "食品包装",
                ["条码", "追溯", "通信"],
                new
                {
                    explanation = "用于包装线、追溯码采集与上位机联动。",
                    operators = new object[]
                    {
                        Node("op_1", "ImageAcquisition", "图像采集"),
                        Node("op_2", "CodeRecognition", "条码识别", new Dictionary<string, string> { ["MaxResults"] = "1" }),
                        Node("op_3", "ModbusCommunication", "PLC写入"),
                        Node("op_4", "ResultOutput", "结果输出")
                    },
                    connections = new object[]
                    {
                        Link("op_1", "Image", "op_2", "Image"),
                        Link("op_2", "Text", "op_3", "Data"),
                        Link("op_2", "Text", "op_4", "Result")
                    },
                    parametersNeedingReview = new Dictionary<string, List<string>>
                    {
                        ["op_3"] = ["IpAddress", "Port", "RegisterAddress"]
                    }
                }),
            CreateTemplate(
                "OCR文本追溯",
                "OCR 识别后写入数据库并输出结果。",
                "食品包装",
                ["OCR", "追溯", "数据库"],
                new
                {
                    explanation = "用于批次号、日期码、序列号识别与归档。",
                    operators = new object[]
                    {
                        Node("op_1", "ImageAcquisition", "图像采集"),
                        Node("op_2", "OcrRecognition", "OCR识别"),
                        Node("op_3", "DatabaseWrite", "写入数据库"),
                        Node("op_4", "ResultOutput", "结果输出")
                    },
                    connections = new object[]
                    {
                        Link("op_1", "Image", "op_2", "Image"),
                        Link("op_2", "Text", "op_3", "Data"),
                        Link("op_2", "Text", "op_4", "Result")
                    },
                    parametersNeedingReview = new Dictionary<string, List<string>>
                    {
                        ["op_3"] = ["ConnectionString", "TableName"]
                    }
                }),
            CreateTemplate(
                "环形件缺陷检测",
                "圆心定位 + 极坐标展开 + 表面缺陷检测。",
                "轴承行业",
                ["环形件", "缺陷检测", "极坐标"],
                new
                {
                    explanation = "适合轴承、瓶盖等环形零件外观检测。",
                    operators = new object[]
                    {
                        Node("op_1", "ImageAcquisition", "图像采集"),
                        Node("op_2", "CircleMeasurement", "圆心定位"),
                        Node("op_3", "PolarUnwrap", "极坐标展开"),
                        Node("op_4", "SurfaceDefectDetection", "缺陷检测"),
                        Node("op_5", "ResultOutput", "结果输出")
                    },
                    connections = new object[]
                    {
                        Link("op_1", "Image", "op_2", "Image"),
                        Link("op_1", "Image", "op_3", "Image"),
                        Link("op_2", "Center", "op_3", "Center"),
                        Link("op_3", "Image", "op_4", "Image"),
                        Link("op_4", "Image", "op_5", "Image")
                    },
                    parametersNeedingReview = new Dictionary<string, List<string>>()
                }),
            CreateTemplate(
                "多工位循环检测",
                "循环计数驱动的多次检测与结果归档。",
                "通用制造",
                ["循环", "多工位", "批量检测"],
                new
                {
                    explanation = "用于单工位重复检测或多工位轮询。",
                    operators = new object[]
                    {
                        Node("op_1", "CycleCounter", "循环计数", new Dictionary<string, string> { ["CycleLimit"] = "10" }),
                        Node("op_2", "ImageAcquisition", "图像采集"),
                        Node("op_3", "Thresholding", "二值化"),
                        Node("op_4", "ResultJudgment", "结果判定"),
                        Node("op_5", "DatabaseWrite", "记录结果"),
                        Node("op_6", "ResultOutput", "结果输出")
                    },
                    connections = new object[]
                    {
                        Link("op_2", "Image", "op_3", "Image"),
                        Link("op_3", "Image", "op_4", "Image"),
                        Link("op_4", "IsOk", "op_5", "Data"),
                        Link("op_4", "IsOk", "op_6", "Result")
                    },
                    parametersNeedingReview = new Dictionary<string, List<string>>
                    {
                        ["op_5"] = ["ConnectionString", "TableName"]
                    }
                }),
            CreateTemplate(
                "检测结果分拣",
                "判定后分支发送 OK/NG 通信信号。",
                "通用制造",
                ["分拣", "OK/NG", "通信"],
                new
                {
                    explanation = "典型在线剔除控制模板。",
                    operators = new object[]
                    {
                        Node("op_1", "ImageAcquisition", "图像采集"),
                        Node("op_2", "DeepLearning", "AI检测", new Dictionary<string, string> { ["Confidence"] = "0.5" }),
                        Node("op_3", "ConditionalBranch", "分支判定", new Dictionary<string, string> { ["Condition"] = "GreaterThan", ["CompareValue"] = "0" }),
                        Node("op_4", "ModbusCommunication", "NG信号"),
                        Node("op_5", "ModbusCommunication", "OK信号"),
                        Node("op_6", "ResultOutput", "结果输出")
                    },
                    connections = new object[]
                    {
                        Link("op_1", "Image", "op_2", "Image"),
                        Link("op_2", "DefectCount", "op_3", "Value"),
                        Link("op_3", "True", "op_4", "Data"),
                        Link("op_3", "False", "op_5", "Data"),
                        Link("op_2", "Image", "op_6", "Image")
                    },
                    parametersNeedingReview = new Dictionary<string, List<string>>
                    {
                        ["op_2"] = ["ModelPath"],
                        ["op_4"] = ["IpAddress", "Port"],
                        ["op_5"] = ["IpAddress", "Port"]
                    }
                })
        };
    }

    private static FlowTemplate CreateTemplate(
        string name,
        string description,
        string industry,
        IEnumerable<string> tags,
        object flowDefinition)
    {
        return new FlowTemplate
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Industry = industry,
            Tags = tags.ToList(),
            FlowJson = JsonSerializer.Serialize(flowDefinition, _jsonOptions),
            CreatedAt = DateTime.UtcNow
        };
    }

    private static object Node(
        string tempId,
        string operatorType,
        string displayName,
        Dictionary<string, string>? parameters = null)
    {
        return new
        {
            tempId,
            operatorType,
            displayName,
            parameters = parameters ?? new Dictionary<string, string>()
        };
    }

    private static object Link(
        string sourceTempId,
        string sourcePortName,
        string targetTempId,
        string targetPortName)
    {
        return new
        {
            sourceTempId,
            sourcePortName,
            targetTempId,
            targetPortName
        };
    }
}
