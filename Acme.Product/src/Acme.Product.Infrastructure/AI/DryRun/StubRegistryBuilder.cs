// StubRegistryBuilder.cs
// Stub 注册表构建器 - Sprint 5
// 基于算子元数据生成默认 Stub，支撑双向 Dry-Run
// 作者：蘅芜君

using System.Collections.Concurrent;
using Acme.Product.Core.Enums;

namespace Acme.Product.Infrastructure.AI.DryRun;

/// <summary>
/// Stub 注册表构建器
/// 根据算子元数据生成默认的 Stub 实现，支持双向 Dry-Run
/// </summary>
public class StubRegistryBuilder
{
    private readonly DryRunStubRegistry _registry;
    private readonly ConcurrentDictionary<OperatorType, StubDefinition> _definitions;

    public StubRegistryBuilder(DryRunStubRegistry registry)
    {
        _registry = registry;
        _definitions = new ConcurrentDictionary<OperatorType, StubDefinition>();
        InitializeDefinitions();
    }

    /// <summary>
    /// 初始化所有算子的 Stub 定义
    /// </summary>
    private void InitializeDefinitions()
    {
        // 图像处理类 - 返回模拟图像
        RegisterStub(OperatorType.ImageAcquisition, PortDataType.Image,
            () => new StubResult { OutputType = PortDataType.Image, SimulatedValue = "SimulatedImage:1920x1080" });

        RegisterStub(OperatorType.GaussianBlur, PortDataType.Image,
            () => new StubResult { OutputType = PortDataType.Image, SimulatedValue = "BlurredImage" });

        RegisterStub(OperatorType.EdgeDetection, PortDataType.Image,
            () => new StubResult { OutputType = PortDataType.Image, SimulatedValue = "EdgeImage" });

        RegisterStub(OperatorType.Thresholding, PortDataType.Image,
            () => new StubResult { OutputType = PortDataType.Image, SimulatedValue = "BinaryImage" });

        // 深度学习类 - 返回检测结果
        RegisterStub(OperatorType.DeepLearning, PortDataType.DetectionList,
            () => new StubResult
            {
                OutputType = PortDataType.DetectionList,
                SimulatedValue = "[{\"label\":\"defect\",\"confidence\":0.95,\"bbox\":[100,100,200,200]}]"
            });

        // 几何测量类
        RegisterStub(OperatorType.CircleMeasurement, PortDataType.CircleData,
            () => new StubResult
            {
                OutputType = PortDataType.CircleData,
                SimulatedValue = "{\"center\":[500,400],\"radius\":50}"
            });

        RegisterStub(OperatorType.LineMeasurement, PortDataType.LineData,
            () => new StubResult
            {
                OutputType = PortDataType.LineData,
                SimulatedValue = "{\"start\":[100,100],\"end\":[500,100]}"
            });

        // 数据操作类
        RegisterStub(OperatorType.ForEach, PortDataType.Any,
            () => new StubResult { OutputType = PortDataType.Any, SimulatedValue = "IterationResults" });

        RegisterStub(OperatorType.ArrayIndexer, PortDataType.Any,
            (inputs) =>
            {
                var index = inputs.GetValueOrDefault("Index", "0");
                return new StubResult { OutputType = PortDataType.Any, SimulatedValue = $"Element[{index}]" };
            });

        RegisterStub(OperatorType.JsonExtractor, PortDataType.String,
            () => new StubResult { OutputType = PortDataType.String, SimulatedValue = "extracted_value" });

        RegisterStub(OperatorType.Statistics, PortDataType.Float,
            () => new StubResult { OutputType = PortDataType.Float, SimulatedValue = "1.33" });

        // 数值计算类
        RegisterStub(OperatorType.MathOperation, PortDataType.Float,
            (inputs) =>
            {
                var op = inputs.GetValueOrDefault("Operation", "Add");
                var a = float.TryParse(inputs.GetValueOrDefault("InputA", "10"), out var valA) ? valA : 10;
                var b = float.TryParse(inputs.GetValueOrDefault("InputB", "5"), out var valB) ? valB : 5;

                var result = op switch
                {
                    "Add" => a + b,
                    "Subtract" => a - b,
                    "Multiply" => a * b,
                    "Divide" => b != 0 ? a / b : 0,
                    "Abs" => Math.Abs(a),
                    "Min" => Math.Min(a, b),
                    "Max" => Math.Max(a, b),
                    "Power" => Math.Pow(a, b),
                    "Sqrt" => Math.Sqrt(Math.Abs(a)),
                    "Round" => Math.Round(a),
                    "Modulo" => b != 0 ? a % b : 0,
                    _ => 0
                };

                return new StubResult { OutputType = PortDataType.Float, SimulatedValue = result.ToString("F2") };
            });

        // 逻辑门
        RegisterStub(OperatorType.LogicGate, PortDataType.Boolean,
            (inputs) =>
            {
                var op = inputs.GetValueOrDefault("Operation", "AND");
                var a = bool.TryParse(inputs.GetValueOrDefault("InputA", "true"), out var valA) && valA;
                var b = bool.TryParse(inputs.GetValueOrDefault("InputB", "false"), out var valB) && valB;

                var result = op switch
                {
                    "AND" => a && b,
                    "OR" => a || b,
                    "NOT" => !a,
                    "XOR" => a ^ b,
                    "NAND" => !(a && b),
                    "NOR" => !(a || b),
                    _ => false
                };

                return new StubResult { OutputType = PortDataType.Boolean, SimulatedValue = result.ToString() };
            });

        // 类型转换
        RegisterStub(OperatorType.TypeConvert, PortDataType.Any,
            (inputs) =>
            {
                var targetType = inputs.GetValueOrDefault("TargetType", "String");
                var value = inputs.GetValueOrDefault("Input", "converted_value");
                return new StubResult { OutputType = PortDataType.Any, SimulatedValue = $"{targetType}:{value}" };
            });

        // 通信类 - 模拟成功响应（注册到字符串键）
        RegisterCommunicationStub("HttpRequest", () => "{\"status\":200,\"data\":{\"id\":123,\"result\":\"success\"}}");
        RegisterStub(OperatorType.HttpRequest, PortDataType.String,
            () => new StubResult { OutputType = PortDataType.String, SimulatedValue = "{\"status\":200}", IsCommunication = true });

        RegisterCommunicationStub("MqttPublish", () => "true");
        RegisterStub(OperatorType.MqttPublish, PortDataType.Boolean,
            () => new StubResult { OutputType = PortDataType.Boolean, SimulatedValue = "true", IsCommunication = true });

        RegisterCommunicationStub("Modbus", () => "ModbusResponse");
        RegisterCommunicationStub("SiemensS7", () => "S7Response");

        // 字符串处理
        RegisterStub(OperatorType.StringFormat, PortDataType.String,
            (inputs) =>
            {
                var format = inputs.GetValueOrDefault("Format", "{0}");
                var arg0 = inputs.GetValueOrDefault("Arg0", "value");
                return new StubResult { OutputType = PortDataType.String, SimulatedValue = format.Replace("{0}", arg0) };
            });

        // 图像保存
        RegisterStub(OperatorType.ImageSave, PortDataType.Boolean,
            () => new StubResult { OutputType = PortDataType.Boolean, SimulatedValue = "true", IsCommunication = true });

        // 流程控制
        RegisterStub(OperatorType.ConditionalBranch, PortDataType.Any,
            (inputs) =>
            {
                var condition = bool.TryParse(inputs.GetValueOrDefault("Condition", "true"), out var val) && val;
                return new StubResult
                {
                    OutputType = PortDataType.Any,
                    SimulatedValue = condition ? "TrueBranch" : "FalseBranch",
                    BranchTaken = condition ? "True" : "False"
                };
            });

        RegisterStub(OperatorType.ResultJudgment, PortDataType.Boolean,
            () => new StubResult { OutputType = PortDataType.Boolean, SimulatedValue = "true" });
    }

    /// <summary>
    /// 注册算子 Stub 定义
    /// </summary>
    private void RegisterStub(OperatorType type, PortDataType outputType, Func<Dictionary<string, string>, StubResult> generator)
    {
        _definitions[type] = new StubDefinition(type, outputType, generator);
    }

    /// <summary>
    /// 注册简单 Stub（无输入依赖）
    /// </summary>
    private void RegisterStub(OperatorType type, PortDataType outputType, Func<StubResult> generator)
    {
        RegisterStub(type, outputType, _ => generator());
    }

    /// <summary>
    /// 注册通信类 Stub（使用字符串键）
    /// </summary>
    private void RegisterCommunicationStub(string deviceType, Func<string> responseGenerator)
    {
        // 注册到 DryRunStubRegistry 使用模拟地址
        _registry.Register($"sim://{deviceType}", "/api/stub",
            StubResponse.JsonResponse(new { result = responseGenerator() }));
    }

    /// <summary>
    /// 获取 Stub 定义
    /// </summary>
    public StubDefinition? GetDefinition(OperatorType type)
    {
        return _definitions.GetValueOrDefault(type);
    }

    /// <summary>
    /// 检查是否有 Stub 定义
    /// </summary>
    public bool HasStub(OperatorType type)
    {
        return _definitions.ContainsKey(type);
    }

    /// <summary>
    /// 执行 Stub 并返回结果
    /// </summary>
    public StubResult ExecuteStub(OperatorType type, Dictionary<string, string> inputs)
    {
        if (_definitions.TryGetValue(type, out var definition))
        {
            return definition.Generate(inputs);
        }
        return new StubResult { OutputType = PortDataType.Any, SimulatedValue = "default_stub" };
    }

    /// <summary>
    /// 为整个流程构建完整的 Stub 注册表
    /// </summary>
    public DryRunStubRegistry BuildForFlow(List<OperatorType> operatorTypes)
    {
        var registry = new DryRunStubRegistry();

        foreach (var type in operatorTypes)
        {
            if (_definitions.TryGetValue(type, out var definition))
            {
                // 注册到 registry（这里使用类型名作为设备地址）
                registry.Register($"op://{type}", "/output",
                    StubResponse.JsonResponse(new { value = definition.Generate(new Dictionary<string, string>()).SimulatedValue }));
            }
        }

        return registry;
    }
}

/// <summary>
/// Stub 定义
/// </summary>
public class StubDefinition
{
    public OperatorType OperatorType { get; }
    public PortDataType OutputType { get; }
    public Func<Dictionary<string, string>, StubResult> Generator { get; }

    public StubDefinition(OperatorType operatorType, PortDataType outputType, Func<Dictionary<string, string>, StubResult> generator)
    {
        OperatorType = operatorType;
        OutputType = outputType;
        Generator = generator;
    }

    public StubResult Generate(Dictionary<string, string> inputs)
    {
        return Generator(inputs);
    }
}

/// <summary>
/// Stub 生成结果
/// </summary>
public class StubResult
{
    public PortDataType OutputType { get; set; }
    public string SimulatedValue { get; set; } = string.Empty;
    public bool IsCommunication { get; set; }
    public string? BranchTaken { get; set; }
}
