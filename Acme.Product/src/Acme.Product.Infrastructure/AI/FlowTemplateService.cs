// FlowTemplateService.cs
// 流程模板服务
// 负责流程模板加载、查询与模板化生成支持
// 作者：蘅芜君
using System.Text.Json;
using Acme.Product.Core.Entities;
using Acme.Product.Infrastructure.Services;

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

    Task<FlowTemplate> CreateTemplateAsync(
        FlowTemplate template,
        CancellationToken cancellationToken = default);

    Task<FlowTemplate?> UpdateTemplateAsync(
        Guid id,
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
    private const string AirConditioningIndustry = "空调制造";
    private const string TemplateDefaultRegionExtent = "999999";
    private const string WireSequenceScenarioKey = "wire-sequence-terminal";
    private const string WireSequenceTemplateName = "端子线序检测";
    private const string WireSequenceIndustry = "线束装配";
    private const string WireSequenceDefaultRegionExtent = "999999";
    private static readonly IReadOnlyDictionary<string, string> _deprecatedBuiltInTemplates =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["传统缺陷检测"] = "3C电子",
            ["AI缺陷检测"] = "半导体",
            ["尺寸间距测量"] = "汽车零部件",
            ["条码读取写PLC"] = "食品包装",
            ["OCR文本追溯"] = "食品包装",
            ["环形件缺陷检测"] = "轴承行业",
            ["多工位循环检测"] = "通用制造",
            ["检测结果分拣"] = "通用制造"
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

    public Task<FlowTemplate> CreateTemplateAsync(FlowTemplate template, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (template == null)
            throw new ArgumentNullException(nameof(template));

        lock (_syncRoot)
        {
            var templates = LoadTemplates();
            template.Id = template.Id == Guid.Empty ? Guid.NewGuid() : template.Id;
            template.CreatedAt = template.CreatedAt == default ? DateTime.UtcNow : template.CreatedAt;
            templates.Add(template);
            SaveTemplates(templates);
        }

        return Task.FromResult(template);
    }

    public Task<FlowTemplate?> UpdateTemplateAsync(Guid id, FlowTemplate template, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (template == null)
            throw new ArgumentNullException(nameof(template));

        lock (_syncRoot)
        {
            var templates = LoadTemplates();
            var existing = templates.FirstOrDefault(item => item.Id == id);
            if (existing == null)
                return Task.FromResult<FlowTemplate?>(null);

            existing.Name = template.Name;
            existing.Description = template.Description;
            existing.Industry = template.Industry;
            existing.Tags = template.Tags;
            existing.FlowJson = template.FlowJson;

            SaveTemplates(templates);
            return Task.FromResult<FlowTemplate?>(existing);
        }
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
                    BackupCorruptedTemplateFile();
                    templates = CreateBuiltInTemplates();
                    SaveTemplates(templates);
                }

                var changed = RemoveDeprecatedBuiltInTemplates(templates);
                if (MergeBuiltInTemplates(templates))
                {
                    changed = true;
                }

                if (changed)
                {
                    SaveTemplates(templates);
                }

                return templates;
            }
            catch
            {
                BackupCorruptedTemplateFile();
                var templates = CreateBuiltInTemplates();
                SaveTemplates(templates);
                return templates;
            }
        }
    }

    private static bool RemoveDeprecatedBuiltInTemplates(List<FlowTemplate> templates)
    {
        return templates.RemoveAll(IsDeprecatedBuiltInTemplate) > 0;
    }

    private static bool MergeBuiltInTemplates(List<FlowTemplate> templates)
    {
        var changed = false;
        foreach (var builtInTemplate in CreateBuiltInTemplates())
        {
            var existing = templates.FirstOrDefault(item => IsSameTemplateDefinition(item, builtInTemplate));
            if (existing == null)
            {
                templates.Add(builtInTemplate);
                changed = true;
                continue;
            }

            if (!ShouldUpgradeBuiltInTemplate(existing, builtInTemplate))
                continue;

            ApplyBuiltInTemplate(existing, builtInTemplate);
            changed = true;
        }

        return changed;
    }

    private static bool IsSameTemplateDefinition(FlowTemplate existing, FlowTemplate candidate)
    {
        if (!string.IsNullOrWhiteSpace(existing.ScenarioKey) &&
            !string.IsNullOrWhiteSpace(candidate.ScenarioKey) &&
            string.Equals(existing.ScenarioKey, candidate.ScenarioKey, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(existing.Name, candidate.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDeprecatedBuiltInTemplate(FlowTemplate template)
    {
        if (IsWireSequenceTemplate(template))
        {
            return false;
        }

        return _deprecatedBuiltInTemplates.TryGetValue(template.Name, out var expectedIndustry) &&
            string.Equals(template.Industry, expectedIndustry, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWireSequenceTemplate(FlowTemplate template)
    {
        if (string.Equals(template.ScenarioKey, WireSequenceScenarioKey, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(template.Name, WireSequenceTemplateName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(template.Industry, WireSequenceIndustry, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldUpgradeBuiltInTemplate(FlowTemplate existing, FlowTemplate candidate)
    {
        if (string.IsNullOrWhiteSpace(existing.TemplateVersion))
            return !string.IsNullOrWhiteSpace(candidate.TemplateVersion);

        return CompareTemplateVersions(candidate.TemplateVersion, existing.TemplateVersion) > 0;
    }

    private static int CompareTemplateVersions(string? left, string? right)
    {
        if (Version.TryParse(left, out var leftVersion) && Version.TryParse(right, out var rightVersion))
            return leftVersion.CompareTo(rightVersion);

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyBuiltInTemplate(FlowTemplate target, FlowTemplate source)
    {
        target.Name = source.Name;
        target.Description = source.Description;
        target.Industry = source.Industry;
        target.Tags = source.Tags;
        target.FlowJson = source.FlowJson;
        target.TemplateVersion = source.TemplateVersion;
        target.ScenarioKey = source.ScenarioKey;
        target.ScenarioPackage = source.ScenarioPackage == null
            ? null
            : new ScenarioPackageBinding
            {
                PackageKey = source.ScenarioPackage.PackageKey,
                PackageVersion = source.ScenarioPackage.PackageVersion,
                AssetVersionIds = source.ScenarioPackage.AssetVersionIds.ToList(),
                RequiredResources = source.ScenarioPackage.RequiredResources.ToList()
            };
    }

    private void SaveTemplates(List<FlowTemplate> templates)
    {
        var json = JsonSerializer.Serialize(templates, _jsonOptions);
        ValidateTemplatePayload(json);

        var directory = Path.GetDirectoryName(_templateFilePath)
                        ?? throw new InvalidOperationException("Template directory path is invalid.");
        Directory.CreateDirectory(directory);

        var tempFilePath = Path.Combine(directory, $"flow_templates.{Guid.NewGuid():N}.tmp");
        var backupPath = Path.Combine(directory, $"flow_templates.swapbackup.{DateTime.UtcNow:yyyyMMddHHmmssfff}.{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(tempFilePath, json);
            ValidateTemplatePayload(File.ReadAllText(tempFilePath));

            if (File.Exists(_templateFilePath))
            {
                File.Replace(tempFilePath, _templateFilePath, backupPath, true);
                TryDeleteFile(backupPath);
            }
            else
            {
                File.Move(tempFilePath, _templateFilePath);
            }
        }
        finally
        {
            TryDeleteFile(tempFilePath);
        }
    }

    private static void ValidateTemplatePayload(string json)
    {
        var parsed = JsonSerializer.Deserialize<List<FlowTemplate>>(json, _jsonOptions);
        if (parsed == null)
            throw new InvalidDataException("Serialized template payload is invalid.");
    }

    private void BackupCorruptedTemplateFile()
    {
        if (!File.Exists(_templateFilePath))
            return;

        var directory = Path.GetDirectoryName(_templateFilePath)
                        ?? throw new InvalidOperationException("Template directory path is invalid.");
        Directory.CreateDirectory(directory);

        var backupPath = Path.Combine(directory, $"flow_templates.corrupted.{DateTime.UtcNow:yyyyMMddHHmmssfff}.{Guid.NewGuid():N}.json");
        File.Copy(_templateFilePath, backupPath, overwrite: false);
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch
        {
            // 忽略清理阶段异常，避免掩盖主流程写入结果
        }
    }

    private static List<FlowTemplate> CreateBuiltInTemplates()
    {
        return new List<FlowTemplate>
        {
            new FlowTemplate
            {
                Id = Guid.NewGuid(),
                Name = WireSequenceTemplateName,
                Description = "端子线序检测模板，先做全图 YOLO 检测，再按 ROI 区域过滤框，最后做 NMS 与顺序判定。",
                Industry = WireSequenceIndustry,
                Tags = ["线序", "YOLO", "端子", "ROI"],
                TemplateVersion = "1.4.2",
                ScenarioKey = WireSequenceScenarioKey,
                ScenarioPackage = new ScenarioPackageBinding
                {
                    PackageKey = WireSequenceScenarioKey,
                    PackageVersion = "1.4.0",
                    AssetVersionIds =
                    [
                        "template:terminal-wire-sequence-template@1.4.0",
                        "model:wire-seq-yolo@1.2.0",
                        "rule:wire-sequence-rule@1.4.0",
                        "label:wire-label-set@1.1.0"
                    ],
                    RequiredResources =
                    [
                        "DeepLearning.ModelPath"
                    ]
                },
                FlowJson = JsonSerializer.Serialize(new
                {
                    explanation = "适用于线束装配工位的端子线序判定，先做全图 YOLO 检测，再用 ROI 区域过滤框，最后做 NMS 去重和顺序判定。",
                    expectedSequence = new[] { "Wire_Black", "Wire_Blue" },
                    expectedDetectionCount = 2,
                    requiredResources = new[] { "DeepLearning.ModelPath" },
                    tunableParameters = new[]
                    {
                        "BoxNms.ScoreThreshold",
                        "BoxNms.IouThreshold"
                    },
                    operators = new object[]
                    {
                        Node("op_1", "ImageAcquisition", "图像采集", new Dictionary<string, string> { ["sourceType"] = "camera" }),
                        Node("op_2", "DeepLearning", "线根检测", new Dictionary<string, string>
                        {
                            ["ModelPath"] = "",
                            ["LabelsPath"] = "",
                            ["Confidence"] = "0.05",
                            ["InputSize"] = "640",
                            ["TargetClasses"] = "Wire_Black,Wire_Blue",
                            ["EnableInternalNms"] = "false",
                            ["DetectionMode"] = "Object"
                        }),
                        Node("op_3", "BoxFilter", "ROI框过滤", new Dictionary<string, string>
                        {
                            ["FilterMode"] = "Region",
                            ["RegionX"] = "0",
                            ["RegionY"] = "0",
                            ["RegionW"] = TemplateDefaultRegionExtent,
                            ["RegionH"] = TemplateDefaultRegionExtent,
                            ["MinScore"] = "0.0"
                        }),
                        Node("op_4", "BoxNms", "候选框去重", new Dictionary<string, string>
                        {
                            ["IouThreshold"] = "0.45",
                            ["ScoreThreshold"] = "0.25",
                            ["MaxDetections"] = "10",
                            ["ShowSuppressed"] = "false"
                        }),
                        Node("op_5", "DetectionSequenceJudge", "顺序判定", new Dictionary<string, string>
                        {
                            ["ExpectedLabels"] = "Wire_Black,Wire_Blue",
                            ["SortBy"] = "CenterY",
                            ["Direction"] = "TopToBottom",
                            ["ExpectedCount"] = "2",
                            ["MinConfidence"] = "0.0"
                        }),
                        Node("op_6", "ResultOutput", "结果输出", new Dictionary<string, string>
                        {
                            ["Format"] = "JSON",
                            ["SaveToFile"] = "true"
                        })
                    },
                    connections = new object[]
                    {
                        Link("op_1", "Image", "op_2", "Image"),
                        Link("op_2", "Objects", "op_3", "Detections"),
                        Link("op_1", "Image", "op_3", "Image"),
                        Link("op_3", "Detections", "op_4", "Detections"),
                        Link("op_1", "Image", "op_4", "Image"),
                        Link("op_2", "OriginalImage", "op_4", "SourceImage"),  // 使用原始图像重新绘制，避免图像叠加污染
                        Link("op_4", "Detections", "op_5", "Detections"),
                        Link("op_4", "Image", "op_6", "Image"),
                        Link("op_4", "Diagnostics", "op_6", "Data"),
                        Link("op_5", "Diagnostics", "op_6", "Result"),
                        Link("op_5", "Message", "op_6", "Text")
                    },
                    parametersNeedingReview = new Dictionary<string, List<string>>
                    {
                        ["op_2"] = ["ModelPath"],
                        ["op_3"] = ["RegionX", "RegionY", "RegionW", "RegionH"],
                        ["op_4"] = ["ScoreThreshold", "IouThreshold"],
                        ["op_5"] = ["ExpectedLabels", "ExpectedCount"]
                    }
                }, _jsonOptions),
                CreatedAt = DateTime.UtcNow
            },
            CreateAiInspectionTemplate(
                name: "包装箱外观检测",
                description: "适合包装终检工位的包装箱外观缺陷检测，默认骨架为缩放 + AI 检测 + Region ROI 过滤 + NMS + OK/NG 判定；需人工确认模型、类别、ROI 与阈值。",
                explanation: "适用于包装终检工位的包装箱外观检测，默认检测箱体破损、压痕、脏污、封箱异常、标签异常等缺陷。",
                tags: ["包装箱", "外观", "AI", "YOLO"],
                targetClasses: "CartonDamage,CartonDent,CartonStain,SealAnomaly,LabelAnomaly",
                detectionMode: "Defect",
                detectionPort: "Defects",
                judgmentCondition: "Equal",
                judgmentExpectValue: "0",
                includeTargetClassesInReview: true),
            CreateAiInspectionTemplate(
                name: "空调内机外观检测",
                description: "适合总装/终检工位的空调内机外观检测，默认骨架为缩放 + AI 检测 + Region ROI 过滤 + NMS + OK/NG 判定；需人工确认模型、类别、ROI 与阈值。",
                explanation: "适用于总装或终检工位的空调内机外观检测，默认检测面板划伤、面板缝隙、污渍、磕碰等缺陷。",
                tags: ["内机", "外观", "AI", "YOLO"],
                targetClasses: "PanelScratch,PanelGap,Stain,Damage",
                detectionMode: "Defect",
                detectionPort: "Defects",
                judgmentCondition: "Equal",
                judgmentExpectValue: "0",
                includeTargetClassesInReview: true),
            CreateAiInspectionTemplate(
                name: "空调外机外观检测",
                description: "适合总装/终检工位的空调外机外观检测，默认骨架为缩放 + AI 检测 + Region ROI 过滤 + NMS + OK/NG 判定；需人工确认模型、类别、ROI 与阈值。",
                explanation: "适用于总装或终检工位的空调外机外观检测，默认检测翅片变形、护网破损、凹陷、缺件等缺陷。",
                tags: ["外机", "外观", "AI", "YOLO"],
                targetClasses: "FinDeform,NetDamage,Dent,MissingPart",
                detectionMode: "Defect",
                detectionPort: "Defects",
                judgmentCondition: "Equal",
                judgmentExpectValue: "0",
                includeTargetClassesInReview: true),
            CreateAiInspectionTemplate(
                name: "遥控器漏装检测",
                description: "适合包装位/附件位的遥控器漏装检测，默认骨架为缩放 + AI 目标检测 + Region ROI 过滤 + NMS + 有无判定；需人工确认模型、ROI 与阈值。",
                explanation: "适用于包装位或附件位的遥控器漏装检测，默认判断附件区域中是否存在遥控器目标。",
                tags: ["遥控器", "漏装", "附件", "AI"],
                targetClasses: "RemoteController",
                detectionMode: "Object",
                detectionPort: "Objects",
                judgmentCondition: "GreaterOrEqual",
                judgmentExpectValue: "1",
                includeTargetClassesInReview: false),
            CreateCopperHoleSpacingTemplate()
        };
    }

    private static FlowTemplate CreateAiInspectionTemplate(
        string name,
        string description,
        string explanation,
        IReadOnlyList<string> tags,
        string targetClasses,
        string detectionMode,
        string detectionPort,
        string judgmentCondition,
        string judgmentExpectValue,
        bool includeTargetClassesInReview)
    {
        var reviewParameters = new Dictionary<string, List<string>>
        {
            ["op_3"] = includeTargetClassesInReview
                ? ["ModelPath", "TargetClasses", "Confidence"]
                : ["ModelPath", "Confidence"],
            ["op_4"] = ["RegionX", "RegionY", "RegionW", "RegionH"],
            ["op_5"] = ["ScoreThreshold", "IouThreshold"]
        };

        return new FlowTemplate
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Industry = AirConditioningIndustry,
            Tags = tags.ToList(),
            TemplateVersion = "1.0.0",
            FlowJson = JsonSerializer.Serialize(new
            {
                explanation,
                requiredResources = new[] { "DeepLearning.ModelPath" },
                tunableParameters = new[]
                {
                    "DeepLearning.Confidence",
                    "BoxNms.ScoreThreshold",
                    "BoxNms.IouThreshold"
                },
                operators = new object[]
                {
                    Node("op_1", "ImageAcquisition", "图像采集", new Dictionary<string, string> { ["sourceType"] = "camera" }),
                    Node("op_2", "ImageResize", "尺寸适配", new Dictionary<string, string>
                    {
                        ["Width"] = "640",
                        ["Height"] = "640"
                    }),
                    Node("op_3", "DeepLearning", detectionMode == "Object" ? "遥控器检测" : "外观缺陷检测", new Dictionary<string, string>
                    {
                        ["ModelPath"] = "",
                        ["LabelsPath"] = "",
                        ["Confidence"] = "0.5",
                        ["InputSize"] = "640",
                        ["TargetClasses"] = targetClasses,
                        ["EnableInternalNms"] = "false",
                        ["DetectionMode"] = detectionMode
                    }),
                    Node("op_4", "BoxFilter", "ROI区域过滤", new Dictionary<string, string>
                    {
                        ["FilterMode"] = "Region",
                        ["RegionX"] = "0",
                        ["RegionY"] = "0",
                        ["RegionW"] = TemplateDefaultRegionExtent,
                        ["RegionH"] = TemplateDefaultRegionExtent,
                        ["MinScore"] = "0.0"
                    }),
                    Node("op_5", "BoxNms", "候选框抑制", new Dictionary<string, string>
                    {
                        ["IouThreshold"] = "0.45",
                        ["ScoreThreshold"] = "0.25",
                        ["MaxDetections"] = "20",
                        ["ShowSuppressed"] = "false"
                    }),
                    Node("op_6", "ResultJudgment", detectionMode == "Object" ? "漏装判定" : "外观判定", new Dictionary<string, string>
                    {
                        ["Condition"] = judgmentCondition,
                        ["ExpectValue"] = judgmentExpectValue,
                        ["MinConfidence"] = "0.0"
                    }),
                    Node("op_7", "ResultOutput", "结果输出", new Dictionary<string, string>
                    {
                        ["Format"] = "JSON",
                        ["SaveToFile"] = "true"
                    })
                },
                connections = new object[]
                {
                    Link("op_1", "Image", "op_2", "Image"),
                    Link("op_2", "Image", "op_3", "Image"),
                    Link("op_3", detectionPort, "op_4", "Detections"),
                    Link("op_2", "Image", "op_4", "Image"),
                    Link("op_4", "Detections", "op_5", "Detections"),
                    Link("op_2", "Image", "op_5", "Image"),
                    Link("op_3", "OriginalImage", "op_5", "SourceImage"),
                    Link("op_5", "Count", "op_6", "Value"),
                    Link("op_5", "Image", "op_7", "Image"),
                    Link("op_5", "Diagnostics", "op_7", "Data"),
                    Link("op_6", "JudgmentResult", "op_7", "Result"),
                    Link("op_6", "Details", "op_7", "Text")
                },
                parametersNeedingReview = reviewParameters
            }, _jsonOptions),
            CreatedAt = DateTime.UtcNow
        };
    }

    private static FlowTemplate CreateCopperHoleSpacingTemplate()
    {
        return new FlowTemplate
        {
            Id = Guid.NewGuid(),
            Name = "两器铜孔间距检测",
            Description = "适合两器装配工位的铜孔间距检测，默认骨架为滤波 + 边缘检测 + 间距测量 + 范围判定；需人工确认预处理、阈值与像素间距范围。",
            Industry = AirConditioningIndustry,
            Tags = ["两器", "铜孔", "间距", "测量"],
            TemplateVersion = "1.0.0",
            FlowJson = JsonSerializer.Serialize(new
            {
                explanation = "适用于两器装配工位的铜孔间距检测，先做滤波和边缘增强，再按投影与多扫描方式测量像素间距。",
                tunableParameters = new[]
                {
                    "Filtering.KernelSize",
                    "EdgeDetection.Threshold1",
                    "EdgeDetection.Threshold2",
                    "GapMeasurement.MinGap",
                    "GapMeasurement.MaxGap"
                },
                operators = new object[]
                {
                    Node("op_1", "ImageAcquisition", "图像采集", new Dictionary<string, string> { ["sourceType"] = "camera" }),
                    Node("op_2", "Filtering", "滤波预处理", new Dictionary<string, string>
                    {
                        ["KernelSize"] = "5",
                        ["SigmaX"] = "1.0",
                        ["SigmaY"] = "0.0",
                        ["BorderType"] = "4"
                    }),
                    Node("op_3", "EdgeDetection", "边缘检测", new Dictionary<string, string>
                    {
                        ["Threshold1"] = "50",
                        ["Threshold2"] = "150",
                        ["AutoThreshold"] = "false",
                        ["AutoThresholdSigma"] = "0.33",
                        ["EnableGaussianBlur"] = "false",
                        ["GaussianKernelSize"] = "5",
                        ["ApertureSize"] = "3"
                    }),
                    Node("op_4", "GapMeasurement", "间距测量", new Dictionary<string, string>
                    {
                        ["Direction"] = "Auto",
                        ["MinGap"] = "0",
                        ["MaxGap"] = "0",
                        ["ExpectedCount"] = "0",
                        ["RobustMode"] = "true",
                        ["OutlierSigmaK"] = "3.0",
                        ["MinValidSamples"] = "4",
                        ["MultiScanCount"] = "8"
                    }),
                    Node("op_5", "ResultJudgment", "范围判定", new Dictionary<string, string>
                    {
                        ["Condition"] = "Range",
                        ["ExpectValueMin"] = "0",
                        ["ExpectValueMax"] = "999999",
                        ["MinConfidence"] = "0.0"
                    }),
                    Node("op_6", "ResultOutput", "结果输出", new Dictionary<string, string>
                    {
                        ["Format"] = "JSON",
                        ["SaveToFile"] = "true"
                    })
                },
                connections = new object[]
                {
                    Link("op_1", "Image", "op_2", "Image"),
                    Link("op_2", "Image", "op_3", "Image"),
                    Link("op_3", "Image", "op_4", "Image"),
                    Link("op_4", "MeanGap", "op_5", "Value"),
                    Link("op_4", "Image", "op_6", "Image"),
                    Link("op_4", "MeanGap", "op_6", "Data"),
                    Link("op_5", "JudgmentResult", "op_6", "Result"),
                    Link("op_5", "Details", "op_6", "Text")
                },
                parametersNeedingReview = new Dictionary<string, List<string>>
                {
                    ["op_2"] = ["KernelSize", "SigmaX", "SigmaY"],
                    ["op_3"] = ["Threshold1", "Threshold2", "AutoThresholdSigma"],
                    ["op_4"] = ["Direction", "MinGap", "MaxGap", "MultiScanCount", "MinValidSamples"],
                    ["op_5"] = ["ExpectValueMin", "ExpectValueMax"]
                }
            }, _jsonOptions),
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
