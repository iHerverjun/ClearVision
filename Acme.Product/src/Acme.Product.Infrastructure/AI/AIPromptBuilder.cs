// AIPromptBuilder.cs
// AI 提示词构建器 - Sprint 5
// 构建包含最新算子库信息的提示词，供 LLM 生成流程
// 作者：蘅芜君

using System.Text;
using System.Text.Json;
using Acme.Product.Core.Enums;

namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// AI 提示词构建器
/// 生成包含完整算子库信息的提示词，帮助 LLM 生成正确的 ClearVision 流程
/// </summary>
public class AIPromptBuilder
{
    private readonly StringBuilder _prompt = new();
    private readonly List<PromptOperatorInfo> _operators = new();

    public AIPromptBuilder()
    {
        InitializeOperatorLibrary();
    }

    /// <summary>
    /// 初始化算子库元数据
    /// </summary>
    private void InitializeOperatorLibrary()
    {
        // 图像采集与处理
        _operators.Add(new PromptOperatorInfo(OperatorType.ImageAcquisition, "图像采集", "从相机获取图像", "Image"));
        _operators.Add(new PromptOperatorInfo(OperatorType.GaussianBlur, "高斯滤波", "图像平滑降噪", "Image", "Image"));
        _operators.Add(new PromptOperatorInfo(OperatorType.Thresholding, "二值化", "阈值分割", "Image", "Image"));
        _operators.Add(new PromptOperatorInfo(OperatorType.EdgeDetection, "边缘检测", "Canny 边缘检测", "Image", "Image"));

        // 深度学习
        _operators.Add(new PromptOperatorInfo(OperatorType.DeepLearning, "深度学习检测", "YOLO 目标检测", "Image", "DetectionList", new[] {
            new PromptParamInfo("ModelPath", "string", "模型文件路径", true),
            new PromptParamInfo("Confidence", "float", "置信度阈值(0-1)", true, "0.5", "0.0", "1.0")
        }));

        // 几何测量
        _operators.Add(new PromptOperatorInfo(OperatorType.CircleMeasurement, "圆测量", "霍夫圆检测", "Image", "CircleData"));
        _operators.Add(new PromptOperatorInfo(OperatorType.LineMeasurement, "直线测量", "霍夫直线检测", "Image", "LineData"));

        // 数据操作（Sprint 1-2）
        _operators.Add(new PromptOperatorInfo(OperatorType.ForEach, "循环处理", "对集合中的每个元素执行子图", "List", "Result", new[] {
            new PromptParamInfo("IoMode", "enum", "执行模式", true, "Parallel", options: new[] { "Parallel", "Sequential" }),
            new PromptParamInfo("MaxParallelism", "int", "最大并行度", false, "8", "1", "64")
        }, specialNotes: "IoMode=Parallel 用于纯计算，IoMode=Sequential 用于含通信的子图"));

        _operators.Add(new PromptOperatorInfo(OperatorType.ArrayIndexer, "数组索引", "从列表中提取单个元素", "List", "Any"));
        _operators.Add(new PromptOperatorInfo(OperatorType.JsonExtractor, "JSON提取", "从 JSON 中提取字段", "String", "Any"));

        // 数值与逻辑（Sprint 3）
        _operators.Add(new PromptOperatorInfo(OperatorType.MathOperation, "数学运算", "加减乘除等运算", "Float", "Float", new[] {
            new PromptParamInfo("Operation", "enum", "运算类型", true, "Add", options: new[] { "Add", "Subtract", "Multiply", "Divide", "Abs", "Min", "Max", "Power", "Sqrt", "Round", "Modulo" })
        }));

        _operators.Add(new PromptOperatorInfo(OperatorType.LogicGate, "逻辑门", "AND/OR/NOT/XOR 等", "Boolean", "Boolean", new[] {
            new PromptParamInfo("Operation", "enum", "逻辑操作", true, "AND", options: new[] { "AND", "OR", "NOT", "XOR", "NAND", "NOR" })
        }));

        _operators.Add(new PromptOperatorInfo(OperatorType.TypeConvert, "类型转换", "类型转换", "Any", "Any"));

        // 通信（Sprint 3）
        _operators.Add(new PromptOperatorInfo(OperatorType.HttpRequest, "HTTP请求", "调用 REST API", "Any", "String", new[] {
            new PromptParamInfo("Url", "string", "请求地址", true),
            new PromptParamInfo("Method", "enum", "请求方法", true, "POST", options: new[] { "GET", "POST", "PUT", "DELETE", "PATCH" })
        }, specialNotes: "通信算子上游必须有 ConditionalBranch 或 ResultJudgment 保护"));

        _operators.Add(new PromptOperatorInfo(OperatorType.MqttPublish, "MQTT发布", "发布 MQTT 消息", "Any", "Boolean", new[] {
            new PromptParamInfo("Broker", "string", "Broker 地址", true),
            new PromptParamInfo("Topic", "string", "主题", true)
        }, specialNotes: "通信算子上游必须有 ConditionalBranch 或 ResultJudgment 保护"));

        // 工业通信
        _operators.Add(new PromptOperatorInfo(OperatorType.ModbusCommunication, "Modbus通信", "Modbus TCP 通信", "Any", "Any"));
        _operators.Add(new PromptOperatorInfo(OperatorType.SiemensS7Communication, "西门子S7", "S7 协议通信", "Any", "Any"));

        // 流程控制
        _operators.Add(new PromptOperatorInfo(OperatorType.ConditionalBranch, "条件分支", "根据条件走不同分支", "Any", "Any", specialNotes: "必须提供 True 和 False 两个分支的输出"));
        _operators.Add(new PromptOperatorInfo(OperatorType.ResultJudgment, "结果判定", "判定检测结果", "Any", "Boolean"));
    }

    /// <summary>
    /// 添加系统提示词头
    /// </summary>
    public AIPromptBuilder WithSystemPrompt(string? customSystemPrompt = null)
    {
        _prompt.AppendLine("# ClearVision 工业视觉检测平台 - 流程生成助手");
        _prompt.AppendLine();

        if (!string.IsNullOrWhiteSpace(customSystemPrompt))
        {
            _prompt.AppendLine(customSystemPrompt);
        }
        else
        {
            _prompt.AppendLine("你是一个专业的工业视觉检测流程设计助手。你的任务是将用户的自然语言需求转换为结构化的 ClearVision 流程定义。");
        }

        _prompt.AppendLine();
        _prompt.AppendLine("## 输出格式");
        _prompt.AppendLine("你必须输出一个标准的 JSON 对象，格式如下：");
        _prompt.AppendLine("```json");
        _prompt.AppendLine("{");
        _prompt.AppendLine("  \"flowName\": \"流程名称\",");
        _prompt.AppendLine("  \"operators\": [");
        _prompt.AppendLine("    {");
        _prompt.AppendLine("      \"id\": \"guid\",");
        _prompt.AppendLine("      \"name\": \"算子名称\",");
        _prompt.AppendLine("      \"type\": \"算子类型枚举\",");
        _prompt.AppendLine("      \"parameters\": [");
        _prompt.AppendLine("        {\"name\": \"参数名\", \"value\": \"参数值\"}");
        _prompt.AppendLine("      ],");
        _prompt.AppendLine("      \"inputPorts\": [...],");
        _prompt.AppendLine("      \"outputPorts\": [...]");
        _prompt.AppendLine("    }");
        _prompt.AppendLine("  ],");
        _prompt.AppendLine("  \"connections\": [");
        _prompt.AppendLine("    {");
        _prompt.AppendLine("      \"sourceOperatorId\": \"guid\",");
        _prompt.AppendLine("      \"sourcePortId\": \"guid\",");
        _prompt.AppendLine("      \"targetOperatorId\": \"guid\",");
        _prompt.AppendLine("      \"targetPortId\": \"guid\"");
        _prompt.AppendLine("    }");
        _prompt.AppendLine("  ]");
        _prompt.AppendLine("}");
        _prompt.AppendLine("```");
        _prompt.AppendLine();

        return this;
    }

    /// <summary>
    /// 添加可用算子库
    /// </summary>
    public AIPromptBuilder WithOperatorLibrary()
    {
        _prompt.AppendLine("## 可用算子库");
        _prompt.AppendLine();

        var categories = _operators.GroupBy(o => o.Category);

        foreach (var category in categories)
        {
            _prompt.AppendLine($"### {category.Key}");
            _prompt.AppendLine();

            foreach (var op in category)
            {
                _prompt.AppendLine($"- **{op.Name}** (`{op.Type}`): {op.Description}");

                if (!string.IsNullOrEmpty(op.InputType))
                    _prompt.AppendLine($"  - 输入: {op.InputType}");
                if (!string.IsNullOrEmpty(op.OutputType))
                    _prompt.AppendLine($"  - 输出: {op.OutputType}");

                if (op.Parameters.Any())
                {
                    _prompt.AppendLine($"  - 参数:");
                    foreach (var param in op.Parameters)
                    {
                        var required = param.IsRequired ? "(必需)" : "(可选)";
                        var range = !string.IsNullOrEmpty(param.MinValue) || !string.IsNullOrEmpty(param.MaxValue)
                            ? $" [{param.MinValue}~{param.MaxValue}]"
                            : "";
                        var options = param.Options?.Any() == true
                            ? $" 可选值: {string.Join(", ", param.Options)}"
                            : "";
                        _prompt.AppendLine($"    - `{param.Name}`: {param.Description} {required}{range}{options}");
                    }
                }

                if (!string.IsNullOrEmpty(op.SpecialNotes))
                {
                    _prompt.AppendLine($"  - ⚠️ 注意: {op.SpecialNotes}");
                }

                _prompt.AppendLine();
            }
        }

        return this;
    }

    /// <summary>
    /// 添加设计规则
    /// </summary>
    public AIPromptBuilder WithDesignRules()
    {
        _prompt.AppendLine("## 设计规则");
        _prompt.AppendLine();
        _prompt.AppendLine("1. **DAG 原则**: 流程必须是有向无环图，禁止循环依赖");
        _prompt.AppendLine("2. **通信算子保护**: 所有通信算子（HTTP、MQTT、Modbus、S7等）上游必须有 ConditionalBranch 或 ResultJudgment 保护，防止无条件触发外部设备");
        _prompt.AppendLine("3. **ForEach 模式选择**: ");
        _prompt.AppendLine("   - IoMode=Parallel: 用于纯计算子图（图像处理、数值计算）");
        _prompt.AppendLine("   - IoMode=Sequential: 用于含通信算子的子图，保护硬件连接");
        _prompt.AppendLine("4. **类型匹配**: 端口连接时确保数据类型兼容");
        _prompt.AppendLine("5. **参数校验**: 数值参数必须在有效范围内");
        _prompt.AppendLine("6. **分支覆盖**: 设计的流程应能处理正常和异常情况");
        _prompt.AppendLine();

        return this;
    }

    /// <summary>
    /// 添加用户需求
    /// </summary>
    public AIPromptBuilder WithUserRequirement(string requirement)
    {
        _prompt.AppendLine("## 用户需求");
        _prompt.AppendLine();
        _prompt.AppendLine(requirement);
        _prompt.AppendLine();

        return this;
    }

    /// <summary>
    /// 添加示例
    /// </summary>
    public AIPromptBuilder WithExamples()
    {
        _prompt.AppendLine("## 示例");
        _prompt.AppendLine();
        _prompt.AppendLine("### 示例 1：多目标检测 + 逐条 MES 上报");
        _prompt.AppendLine("```json");
        _prompt.AppendLine(@"{
  ""flowName"": ""多目标检测MES上报"",
  ""operators"": [
    { ""id"": ""op1"", ""name"": ""图像采集"", ""type"": ""ImageAcquisition"", ""outputPorts"": [{ ""id"": ""p1"", ""name"": ""Image"", ""dataType"": ""Image"" }] },
    { ""id"": ""op2"", ""name"": ""YOLO检测"", ""type"": ""DeepLearning"", ""parameters"": [{ ""name"": ""ModelPath"", ""value"": ""models/defect.onnx"" }, { ""name"": ""Confidence"", ""value"": ""0.5"" }], ""inputPorts"": [{ ""id"": ""p2"", ""name"": ""Image"" }], ""outputPorts"": [{ ""id"": ""p3"", ""name"": ""DetectionList"" }] },
    { ""id"": ""op3"", ""name"": ""循环处理"", ""type"": ""ForEach"", ""parameters"": [{ ""name"": ""IoMode"", ""value"": ""Sequential"" }], ""inputPorts"": [{ ""id"": ""p4"", ""name"": ""Items"" }] }
  ],
  ""connections"": [
    { ""sourceOperatorId"": ""op1"", ""sourcePortId"": ""p1"", ""targetOperatorId"": ""op2"", ""targetPortId"": ""p2"" },
    { ""sourceOperatorId"": ""op2"", ""sourcePortId"": ""p3"", ""targetOperatorId"": ""op3"", ""targetPortId"": ""p4"" }
  ]
}");
        _prompt.AppendLine("```");
        _prompt.AppendLine();

        return this;
    }

    /// <summary>
    /// 添加输出要求
    /// </summary>
    public AIPromptBuilder WithOutputRequirements()
    {
        _prompt.AppendLine("## 输出要求");
        _prompt.AppendLine();
        _prompt.AppendLine("1. 只输出 JSON，不要输出任何其他文字说明");
        _prompt.AppendLine("2. 确保所有算子 ID 使用有效的 GUID 格式");
        _prompt.AppendLine("3. 确保端口 ID 唯一且在算子内保持一致");
        _prompt.AppendLine("4. 参数值类型必须与定义匹配");
        _prompt.AppendLine("5. 通信算子必须添加保护性上游节点");
        _prompt.AppendLine();
        _prompt.AppendLine("请根据以上信息生成流程 JSON：");
        _prompt.AppendLine();

        return this;
    }

    /// <summary>
    /// 构建完整提示词
    /// </summary>
    public string Build()
    {
        return _prompt.ToString();
    }

    /// <summary>
    /// 创建完整提示词（快捷方法）
    /// </summary>
    public static string CreateFullPrompt(string userRequirement)
    {
        return new AIPromptBuilder()
            .WithSystemPrompt()
            .WithOperatorLibrary()
            .WithDesignRules()
            .WithExamples()
            .WithUserRequirement(userRequirement)
            .WithOutputRequirements()
            .Build();
    }
}

/// <summary>
/// 算子元数据
/// </summary>
public class PromptOperatorInfo
{
    public OperatorType Type { get; }
    public string Name { get; }
    public string Description { get; }
    public string InputType { get; }
    public string OutputType { get; }
    public List<PromptParamInfo> Parameters { get; }
    public string SpecialNotes { get; }
    public string Category => GetCategory(Type);

    public PromptOperatorInfo(
        OperatorType type,
        string name,
        string description,
        string inputType = "",
        string outputType = "",
        PromptParamInfo[]? parameters = null,
        string specialNotes = "")
    {
        Type = type;
        Name = name;
        Description = description;
        InputType = inputType;
        OutputType = outputType;
        Parameters = parameters?.ToList() ?? new List<PromptParamInfo>();
        SpecialNotes = specialNotes;
    }

    private string GetCategory(OperatorType type)
    {
        return type switch
        {
            OperatorType.ImageAcquisition or OperatorType.GaussianBlur or OperatorType.Thresholding or OperatorType.EdgeDetection => "图像处理",
            OperatorType.DeepLearning => "深度学习",
            OperatorType.CircleMeasurement or OperatorType.LineMeasurement => "几何测量",
            OperatorType.ForEach or OperatorType.ArrayIndexer or OperatorType.JsonExtractor => "数据操作",
            OperatorType.MathOperation or OperatorType.LogicGate or OperatorType.TypeConvert => "数值逻辑",
            OperatorType.HttpRequest or OperatorType.MqttPublish or OperatorType.ModbusCommunication or OperatorType.SiemensS7Communication => "通信",
            OperatorType.ConditionalBranch or OperatorType.ResultJudgment => "流程控制",
            _ => "其他"
        };
    }
}

/// <summary>
/// 参数元数据
/// </summary>
public class PromptParamInfo
{
    public string Name { get; }
    public string DataType { get; }
    public string Description { get; }
    public bool IsRequired { get; }
    public string DefaultValue { get; }
    public string MinValue { get; }
    public string MaxValue { get; }
    public string[]? Options { get; }

    public PromptParamInfo(
        string name,
        string dataType,
        string description,
        bool isRequired = false,
        string defaultValue = "",
        string minValue = "",
        string maxValue = "",
        string[]? options = null)
    {
        Name = name;
        DataType = dataType;
        Description = description;
        IsRequired = isRequired;
        DefaultValue = defaultValue;
        MinValue = minValue;
        MaxValue = maxValue;
        Options = options;
    }
}
