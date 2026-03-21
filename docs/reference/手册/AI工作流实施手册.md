# ClearVision AI 自然语言生成工程功能  完整开发实施指导文档

<!-- DOC_AUDIT_STATUS_START -->
## 文档审计状态（自动更新）
- 审计日期：2026-03-02
- 完成状态：未完成
- 任务统计：总计 23，已完成 0，未完成 23，待办关键词命中 0
- 判定依据：任务清单尚未开始勾选
<!-- DOC_AUDIT_STATUS_END -->


> **作者**: 蘅芜君
> **版本**: V1.0
> **创建日期**: 2026-02-18
> **最后更新**: 2026-02-21
> **文档编号**: ref-ai-workflow-guide
> **状态**: 已完成

---
## 关键项目信息速查

### 技术栈
- **桌面宿主**：WinForms + WebView2，入口：`Acme.Product.Desktop/Program.cs`
- **后端**：C# .NET 8，领域核心层 `Acme.Product.Core`，基础设施层 `Acme.Product.Infrastructure`，应用层 `Acme.Product.Application`
- **前端**：HTML5/JS/CSS3，运行于 WebView2，核心画布：`Acme.Product.Desktop/wwwroot/src/core/canvas/flowCanvas.js`
- **通信**：WebView2 双向消息，消息定义在 `Acme.Product.Contracts/Messages/`
- **序列化**：`ProjectJsonSerializer.cs`，DTO 位于 `Acme.Product.Application/DTOs/`

### 算子注册位置（当前 61+ 个算子，以 OperatorFactory 实际注册数量为准）
`Acme.Product.Infrastructure/Services/OperatorFactory.cs`（`InitializeDefaultOperators()` 方法，约第 90 行起）

### 前端画布关键 API（已存在）
```javascript
canvas.addNode(type, x, y, config)      // 添加节点
canvas.addConnection(srcId, srcPort, tgtId, tgtPort)  // 添加连线
canvas.deserialize(data)                 // 反序列化整个流程到画布
canvas.serialize()                       // 序列化当前画布为 JSON
```

### 现有数据类型（PortDataType 枚举）
`Image(0)`, `Integer(1)`, `Float(2)`, `Boolean(3)`, `String(4)`, `Point(5)`, `Rectangle(6)`, `Contour(7)`, `Any(99)`

### 连线规则（已有校验逻辑，须遵守）
1. 一个输入端口只能有一个连接（一对一）
2. 一个输出端口可以连接到多个输入端口（扇出）
3. 不允许环路（`WouldCreateCycle()` 检测）
4. 端口数据类型必须兼容（`checkTypeCompatibility()`）
5. 不允许自连接

---

## 总体架构图

```
用户在前端输入框输入自然语言描述
            │
            ▼
   前端 JS 发送 WebView2 消息
   { type: "GenerateFlow", payload: { description: "..." } }
            │
            ▼
   C# 后端 AiFlowGenerationHandler 接收消息
            │
            ▼
   AiFlowGenerationService.GenerateAsync()
   ├── 构建 System Prompt（算子目录 JSON + 规则 + few-shot 示例）
   ├── 调用 AI API（Claude API / OpenAI API）
   └── 解析 AI 返回的 JSON
            │
            ▼
   AiFlowValidator.Validate()
   ├── 校验算子类型合法性
   ├── 校验端口名合法性
   ├── 校验类型兼容性
   ├── 校验环路
   └── 如有错误 → 携带错误信息重新请求 AI（最多 2 次重试）
            │
            ▼
   AutoLayoutService.Layout()
   └── 拓扑分层，算子坐标自动排列（左→右）
            │
            ▼
   返回合法的 OperatorFlowDto JSON 给前端
            │
            ▼
   前端 canvas.deserialize(flowDto) 渲染到画布
   + 高亮"需用户确认"的参数
```

---

## 阶段零：准备工作（前置检查，无需编写代码）

### 目标
确认项目可以正常构建，确认 NuGet 包配置正常，确认 WebView2 消息通信机制已理解。

### 0.1 确认现有消息通信模式

在 `Acme.Product.Contracts/Messages/` 目录下，找到至少一个已有的消息定义文件，理解其结构模式。现有的消息通信流程大致如下：

**前端 → 后端**（JS 调用 C#）：
```javascript
window.chrome.webview.postMessage(JSON.stringify({
    type: "MessageTypeName",
    payload: { /* 数据 */ }
}));
```

**后端 → 前端**（C# 调用 JS）：
```csharp
// 在 WebView2 宿主中
await webView.CoreWebView2.ExecuteScriptAsync(
    $"window.dispatchEvent(new CustomEvent('backendMessage', {{ detail: {json} }}))");
```

请找到项目中实际使用的消息路由/分发机制，记录处理消息的入口类名和方法名，以便后续阶段在同一位置注册新消息处理器。

### 0.2 确认 NuGet 包

检查 `Acme.Product.Infrastructure` 的 `.csproj` 文件，确认以下包（若无则在后续阶段添加）：
- `System.Net.Http`（内置，.NET 8 默认包含）
- `System.Text.Json`（内置）

不需要引入任何 AI SDK NuGet 包，直接使用 `HttpClient` 调用 REST API，保持依赖最小化。

### 0.3 准备 AI API Key

实现时，API Key 通过 **应用配置文件** 传入，不硬编码。配置项：
```json
{
  "AiFlowGeneration": {
    "Provider": "Anthropic",
    "ApiKey": "sk-ant-...",
    "Model": "claude-opus-4-6",
    "MaxRetries": 2,
    "TimeoutSeconds": 60
  }
}
```

配置文件路径（桌面应用）：`Acme.Product.Desktop/appsettings.json`（若不存在则创建）。

---

## 阶段一：后端 · 数据结构与配置（约 0.5 天）

### 目标
创建 AI 生成功能所需的所有数据结构、配置类和接口定义，不实现具体逻辑。

### 1.1 创建配置类

**文件路径**：`Acme.Product.Infrastructure/AI/AiGenerationOptions.cs`

```csharp
namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// AI 工作流生成功能的配置选项
/// </summary>
public class AiGenerationOptions
{
    public const string SectionName = "AiFlowGeneration";

    /// <summary>
    /// AI 提供商：Anthropic 或 OpenAI
    /// </summary>
    public string Provider { get; set; } = "Anthropic";

    /// <summary>
    /// AI API Key（生产环境从环境变量或密钥管理器读取）
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// 使用的模型名称
    /// Anthropic 推荐：claude-opus-4-6
    /// OpenAI 推荐：gpt-4o
    /// </summary>
    public string Model { get; set; } = "claude-opus-4-6";

    /// <summary>
    /// 校验失败后的最大重试次数
    /// </summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>
    /// API 调用超时（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// 生成结果的最大 token 数
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// 自定义 API 端点地址（可选）。
    /// 用于连接 Ollama 本地模型（如 http://localhost:11434/v1/chat/completions）
    /// 或兼容 OpenAI 协议的国内 API 代理服务（如 DeepSeek、通义千问）。
    /// 为 null 时使用各提供商的默认端点。
    /// </summary>
    public string? BaseUrl { get; set; }
}
```

### 1.2 创建 AI 生成结果 DTO

**文件路径**：`Acme.Product.Application/DTOs/AiGenerationDto.cs`

```csharp
namespace Acme.Product.Application.DTOs;

/// <summary>
/// AI 生成工作流的请求参数
/// </summary>
public record AiFlowGenerationRequest(
    string Description,
    string? AdditionalContext = null
);

/// <summary>
/// AI 生成工作流的响应结果
/// </summary>
public class AiFlowGenerationResult
{
    /// <summary>
    /// 是否生成成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 生成的工作流 DTO（成功时不为 null）
    /// </summary>
    public OperatorFlowDto? Flow { get; set; }

    /// <summary>
    /// 错误消息（失败时不为 null）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// AI 对本次生成的说明（解释为什么选择这些算子）
    /// </summary>
    public string? AiExplanation { get; set; }

    /// <summary>
    /// 需要用户手动确认的参数列表（算子ID → 参数名列表）
    /// </summary>
    public Dictionary<string, List<string>> ParametersNeedingReview { get; set; } = new();

    /// <summary>
    /// 实际使用的 AI 重试次数
    /// </summary>
    public int RetryCount { get; set; }
}

/// <summary>
/// AI 原始输出的结构（AI 应严格按此格式输出 JSON）
/// </summary>
public class AiGeneratedFlowJson
{
    /// <summary>
    /// AI 对生成结果的解释说明
    /// </summary>
    public string Explanation { get; set; } = string.Empty;

    /// <summary>
    /// 生成的算子列表
    /// </summary>
    public List<AiGeneratedOperator> Operators { get; set; } = new();

    /// <summary>
    /// 生成的连线列表
    /// </summary>
    public List<AiGeneratedConnection> Connections { get; set; } = new();

    /// <summary>
    /// 需要用户确认的参数（算子临时ID → 参数名列表）
    /// </summary>
    public Dictionary<string, List<string>> ParametersNeedingReview { get; set; } = new();
}

public class AiGeneratedOperator
{
    /// <summary>
    /// AI 分配的临时 ID，用于在 connections 中引用（格式：op_1, op_2...）
    /// </summary>
    public string TempId { get; set; } = string.Empty;

    /// <summary>
    /// 算子类型，必须与 OperatorType 枚举名完全一致
    /// </summary>
    public string OperatorType { get; set; } = string.Empty;

    /// <summary>
    /// 用户友好的显示名称（可自定义，如"圆测量#1"）
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 算子参数键值对（参数名 → 参数值字符串）
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = new();
}

public class AiGeneratedConnection
{
    public string SourceTempId { get; set; } = string.Empty;
    public string SourcePortName { get; set; } = string.Empty;
    public string TargetTempId { get; set; } = string.Empty;
    public string TargetPortName { get; set; } = string.Empty;
}
```

### 1.3 创建服务接口

**文件路径**：`Acme.Product.Core/Services/IAiFlowGenerationService.cs`

```csharp
using Acme.Product.Application.DTOs;

namespace Acme.Product.Core.Services;

/// <summary>
/// AI 工作流生成服务接口
/// </summary>
public interface IAiFlowGenerationService
{
    /// <summary>
    /// 根据自然语言描述生成工作流
    /// </summary>
    /// <param name="request">生成请求（用户描述 + 可选上下文）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<AiFlowGenerationResult> GenerateFlowAsync(
        AiFlowGenerationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// AI 校验服务接口
/// </summary>
public interface IAiFlowValidator
{
    /// <summary>
    /// 校验 AI 生成的工作流是否合法
    /// </summary>
    ValidationResult Validate(AiGeneratedFlowJson generatedFlow);
}
```

**文件路径**：`Acme.Product.Core/Services/ValidationResult.cs`

```csharp
namespace Acme.Product.Core.Services;

public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public static ValidationResult Success() => new();

    public static ValidationResult Failure(params string[] errors) =>
        new() { Errors = errors.ToList() };

    public void AddError(string error) => Errors.Add(error);
    public void AddWarning(string warning) => Warnings.Add(warning);
}
```

### 1.4 创建 WebView2 消息契约

**文件路径**：`Acme.Product.Contracts/Messages/AiGenerationMessages.cs`

```csharp
namespace Acme.Product.Contracts.Messages;

/// <summary>
/// 前端 → 后端：请求 AI 生成工作流
/// </summary>
public record GenerateFlowRequest
{
    public string Type => "GenerateFlow";
    public GenerateFlowRequestPayload Payload { get; init; } = new();
}

public record GenerateFlowRequestPayload
{
    /// <summary>
    /// 用户输入的自然语言描述
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 可选：用户选择的模板/场景类型提示
    /// </summary>
    public string? Hint { get; init; }
}

/// <summary>
/// 后端 → 前端：AI 生成结果
/// </summary>
public record GenerateFlowResponse
{
    public string Type => "GenerateFlowResult";
    public bool Success { get; init; }
    public object? Flow { get; init; }   // OperatorFlowDto 序列化后的对象
    public string? ErrorMessage { get; init; }
    public string? AiExplanation { get; init; }
    public Dictionary<string, List<string>>? ParametersNeedingReview { get; init; }
}

/// <summary>
/// 后端 → 前端：AI 生成进度更新（流式反馈）
/// </summary>
public record GenerateFlowProgress
{
    public string Type => "GenerateFlowProgress";
    public string Stage { get; init; } = string.Empty;  // "calling_ai" | "validating" | "layouting"
    public string Message { get; init; } = string.Empty;
}
```

### 阶段一验收标准
- [ ] 所有文件创建完成，项目可以正常编译（无编译错误）
- [ ] 枚举、接口、DTO 命名空间正确，引用关系正常
- [ ] `appsettings.json` 中已添加 `AiFlowGeneration` 配置节（ApiKey 暂时为空）

---

## 阶段二：后端 · AI 调用与 Prompt 工程（约 1 天）

### 目标
实现 AI API 调用客户端和 System Prompt 构建逻辑。这是整个功能的核心。

### 2.1 构建 System Prompt（最关键的部分）

**文件路径**：`Acme.Product.Infrastructure/AI/PromptBuilder.cs`

System Prompt 分三部分：角色定义、算子目录、输出格式规范。

```csharp
using Acme.Product.Core.Services;

namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// 构建发送给 AI 的 System Prompt
/// </summary>
public class PromptBuilder
{
    private readonly IOperatorFactory _operatorFactory;

    public PromptBuilder(IOperatorFactory operatorFactory)
    {
        _operatorFactory = operatorFactory;
    }

    /// <summary>
    /// 构建完整的 System Prompt
    /// </summary>
    public string BuildSystemPrompt()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine(GetRoleDefinition());
        sb.AppendLine();
        sb.AppendLine(GetOperatorCatalog());
        sb.AppendLine();
        sb.AppendLine(GetConnectionRules());
        sb.AppendLine();
        sb.AppendLine(GetOutputFormatSpec());
        sb.AppendLine();
        sb.AppendLine(GetFewShotExamples());

        return sb.ToString();
    }

    private string GetRoleDefinition() => """
        # 角色定义

        你是 ClearVision 工业视觉检测平台的工作流生成专家。
        你的任务是：根据用户的自然语言描述，从算子库中选择合适的算子，
        确定它们的连接关系和参数配置，生成一个完整的视觉检测工作流。

        ## 工作流基本规则
        1. 每个工作流必须从"图像源"类算子开始（ImageAcquisition）
        2. 每个工作流必须以"结果输出"类算子结束（ResultOutput）
        3. 只使用下方算子目录中列出的算子，不能创造不存在的算子
        4. 连线时必须遵守端口类型兼容性规则
        5. 优先使用最简洁的算子组合完成任务
        """;

    private string GetOperatorCatalog()
    {
        // 从 OperatorFactory 获取所有已注册算子的元数据，动态生成算子目录
        // 这样当算子库更新时，Prompt 自动更新
        var allMetadata = _operatorFactory.GetAllMetadata(); // 需要在 IOperatorFactory 中添加此方法
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# 算子目录（你只能使用以下算子）");
        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine(System.Text.Json.JsonSerializer.Serialize(
            allMetadata.Select(m => new
            {
                operator_id = m.Type.ToString(),
                name = m.DisplayName,
                category = m.Category,
                description = m.Description,
                keywords = m.Keywords ?? Array.Empty<string>(),
                inputs = m.InputPorts.Select(p => new
                {
                    port_name = p.Name,
                    display_name = p.DisplayName,
                    data_type = p.DataType.ToString(),
                    required = p.IsRequired
                }),
                outputs = m.OutputPorts.Select(p => new
                {
                    port_name = p.Name,
                    display_name = p.DisplayName,
                    data_type = p.DataType.ToString()
                }),
                parameters = m.Parameters.Select(p => new
                {
                    param_name = p.Name,
                    display_name = p.DisplayName,
                    type = p.DataType,
                    default_value = p.DefaultValue?.ToString(),
                    required = p.IsRequired
                })
            }),
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
        ));
        sb.AppendLine("```");
        return sb.ToString();
    }

    private string GetConnectionRules() => """
        # 端口类型兼容性规则

        ## 数据类型说明
        - Image：图像数据（绿色端口）
        - Integer / Float：数值类型（橙色端口），Integer 和 Float 可以互连
        - Boolean：布尔值（红色端口）
        - String：字符串（蓝色端口）
        - Point / Rectangle：几何类型（粉色端口），Point 和 Rectangle 可以互连
        - Contour：轮廓数据（紫色端口）
        - Any：任意类型（灰色端口），可以连接任何类型

        ## 连线约束
        - 一个输入端口只能接收一条连线
        - 一个输出端口可以连接到多个输入端口（扇出）
        - 不允许环路（流程必须是有向无环图）
        - 不同类型的端口不可连接（除非其中一方是 Any）
        """;

    private string GetOutputFormatSpec() => """
        # 输出格式规范（严格遵守）

        你必须只输出一个合法的 JSON 对象，不包含任何 Markdown 代码块标记、
        解释性文字前缀或后缀。JSON 结构如下：

        ```
        {
          "explanation": "简要解释为什么选择这些算子和连接方式（50字以内）",
          "operators": [
            {
              "tempId": "op_1",
              "operatorType": "ImageAcquisition",
              "displayName": "图像采集",
              "parameters": {
                "sourceType": "camera",
                "triggerMode": "Software"
              }
            }
          ],
          "connections": [
            {
              "sourceTempId": "op_1",
              "sourcePortName": "Image",
              "targetTempId": "op_2",
              "targetPortName": "Image"
            }
          ],
          "parametersNeedingReview": {
            "op_3": ["ModelPath", "Confidence"]
          }
        }
        ```

        ## 关于 parametersNeedingReview
        列出你无法从用户描述中确定具体值、需要用户手动配置的参数。
        例如：文件路径、IP 地址、模型文件路径、特定尺寸阈值等。

        ## 关于 tempId
        格式固定为 op_1, op_2, op_3...，按算子在流程中的执行顺序递增。

        ## 关于 operatorType
        必须与算子目录中的 operator_id 字段完全一致（大小写敏感）。

        ## 关于端口名
        必须与算子目录中的 port_name 字段完全一致（大小写敏感）。
        """;

    private string GetFewShotExamples() => """
        # 示例（学习这些示例的格式和思路）

        ## 示例 1
        用户描述："检测产品表面缺陷，用相机拍照后分析"

        正确输出：
        {
          "explanation": "相机采集图像，预处理降噪，二值化分离缺陷，Blob分析统计缺陷数量，最终输出结果",
          "operators": [
            {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "相机采集", "parameters": {"sourceType": "camera", "triggerMode": "Hardware"}},
            {"tempId": "op_2", "operatorType": "Filtering", "displayName": "高斯滤波", "parameters": {"KernelSize": "5"}},
            {"tempId": "op_3", "operatorType": "Thresholding", "displayName": "二值化", "parameters": {"UseOtsu": "true"}},
            {"tempId": "op_4", "operatorType": "BlobAnalysis", "displayName": "缺陷Blob分析", "parameters": {"MinArea": "50", "MaxArea": "5000"}},
            {"tempId": "op_5", "operatorType": "ResultOutput", "displayName": "检测结果", "parameters": {}}
          ],
          "connections": [
            {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
            {"sourceTempId": "op_2", "sourcePortName": "Image", "targetTempId": "op_3", "targetPortName": "Image"},
            {"sourceTempId": "op_3", "sourcePortName": "Image", "targetTempId": "op_4", "targetPortName": "Image"},
            {"sourceTempId": "op_4", "sourcePortName": "BlobCount", "targetTempId": "op_5", "targetPortName": "Result"}
          ],
          "parametersNeedingReview": {
            "op_4": ["MinArea", "MaxArea"]
          }
        }

        ## 示例 2
        用户描述："扫描产品二维码，通过Modbus发给PLC"

        正确输出：
        {
          "explanation": "相机采集，条码识别提取文本，Modbus TCP协议发送给PLC",
          "operators": [
            {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "相机采集", "parameters": {"sourceType": "camera", "triggerMode": "Hardware"}},
            {"tempId": "op_2", "operatorType": "CodeRecognition", "displayName": "二维码识别", "parameters": {"CodeType": "QR", "MaxResults": "1"}},
            {"tempId": "op_3", "operatorType": "ModbusCommunication", "displayName": "Modbus发送", "parameters": {"Protocol": "TCP", "Port": "502", "FunctionCode": "WriteMultiple"}}
          ],
          "connections": [
            {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
            {"sourceTempId": "op_2", "sourcePortName": "Text", "targetTempId": "op_3", "targetPortName": "Data"}
          ],
          "parametersNeedingReview": {
            "op_3": ["IpAddress"]
          }
        }

        ## 示例 3
        用户描述："用AI模型检测产品缺陷，有缺陷发NG信号，没缺陷发OK信号给PLC"

        正确输出：
        {
          "explanation": "相机采集后缩放至AI输入尺寸，深度学习推理检测缺陷，条件分支判断缺陷数量，分别通过Modbus发送NG/OK信号",
          "operators": [
            {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "相机采集", "parameters": {"sourceType": "camera", "triggerMode": "Hardware"}},
            {"tempId": "op_2", "operatorType": "ImageResize", "displayName": "图像缩放", "parameters": {"Width": "640", "Height": "640"}},
            {"tempId": "op_3", "operatorType": "DeepLearning", "displayName": "AI缺陷检测", "parameters": {"Confidence": "0.5", "UseGpu": "true"}},
            {"tempId": "op_4", "operatorType": "ConditionalBranch", "displayName": "缺陷判断", "parameters": {"FieldName": "DefectCount", "Condition": "GreaterThan", "CompareValue": "0"}},
            {"tempId": "op_5", "operatorType": "ModbusCommunication", "displayName": "发送NG", "parameters": {"FunctionCode": "WriteSingle", "RegisterAddress": "1", "WriteValue": "1"}},
            {"tempId": "op_6", "operatorType": "ModbusCommunication", "displayName": "发送OK", "parameters": {"FunctionCode": "WriteSingle", "RegisterAddress": "1", "WriteValue": "0"}},
            {"tempId": "op_7", "operatorType": "ResultOutput", "displayName": "检测结果", "parameters": {}}
          ],
          "connections": [
            {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
            {"sourceTempId": "op_2", "sourcePortName": "Image", "targetTempId": "op_3", "targetPortName": "Image"},
            {"sourceTempId": "op_3", "sourcePortName": "DefectCount", "targetTempId": "op_4", "targetPortName": "Value"},
            {"sourceTempId": "op_4", "sourcePortName": "True", "targetTempId": "op_5", "targetPortName": "Data"},
            {"sourceTempId": "op_4", "sourcePortName": "False", "targetTempId": "op_6", "targetPortName": "Data"},
            {"sourceTempId": "op_3", "sourcePortName": "Image", "targetTempId": "op_7", "targetPortName": "Image"}
          ],
          "parametersNeedingReview": {
            "op_3": ["ModelPath", "InputSize"],
            "op_5": ["IpAddress", "Port"],
            "op_6": ["IpAddress", "Port"]
          }
        }

        ## 示例 4
        用户描述："测量零件上两孔之间的距离，结果转换为毫米并输出"

        正确输出：
        {
          "explanation": "相机采集图像，高斯滤波降噪，边缘检测后在图上测量两点距离，通过数学计算转换为物理尺寸毫米，最终输出",
          "operators": [
            {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "相机采集", "parameters": {"sourceType": "camera"}},
            {"tempId": "op_2", "operatorType": "Filtering", "displayName": "滤波", "parameters": {"KernelSize": "5"}},
            {"tempId": "op_3", "operatorType": "EdgeDetection", "displayName": "边缘检测", "parameters": {"Threshold1": "50", "Threshold2": "150"}},
            {"tempId": "op_4", "operatorType": "Measurement", "displayName": "距离测量", "parameters": {"MeasureType": "PointToPoint", "X1": "100", "Y1": "100", "X2": "200", "Y2": "200"}},
            {"tempId": "op_5", "operatorType": "MathOperation", "displayName": "乘像素当量", "parameters": {"Operation": "Multiply", "ValueB": "0.02"}},
            {"tempId": "op_6", "operatorType": "ResultOutput", "displayName": "测量结果", "parameters": {}}
          ],
          "connections": [
            {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
            {"sourceTempId": "op_2", "sourcePortName": "Image", "targetTempId": "op_3", "targetPortName": "Image"},
            {"sourceTempId": "op_3", "sourcePortName": "Image", "targetTempId": "op_4", "targetPortName": "Image"},
            {"sourceTempId": "op_4", "sourcePortName": "Distance", "targetTempId": "op_5", "targetPortName": "ValueA"},
            {"sourceTempId": "op_5", "sourcePortName": "Result", "targetTempId": "op_6", "targetPortName": "Result"}
          ],
          "parametersNeedingReview": {
            "op_4": ["X1", "Y1", "X2", "Y2"],
            "op_5": ["ValueB"]
          }
        }

        ## 示例 5
        用户描述："对产品连续拍照10次，记录每次的检测结果到数据库"

        正确输出：
        {
          "explanation": "循环计数器控制10次拍照，每次采集后二值化分析，结果写入数据库，计数递增",
          "operators": [
            {"tempId": "op_1", "operatorType": "CycleCounter", "displayName": "循环计数", "parameters": {"CycleLimit": "10", "AutoReset": "true"}},
            {"tempId": "op_2", "operatorType": "ImageAcquisition", "displayName": "相机采集", "parameters": {"sourceType": "camera"}},
            {"tempId": "op_3", "operatorType": "Thresholding", "displayName": "二值化", "parameters": {"UseOtsu": "true"}},
            {"tempId": "op_4", "operatorType": "BlobAnalysis", "displayName": "缺陷分析", "parameters": {"MinArea": "100"}},
            {"tempId": "op_5", "operatorType": "ResultJudgment", "displayName": "OK/NG判定", "parameters": {"FieldName": "BlobCount", "Condition": "LessThanOrEqual", "ThresholdValue": "0"}},
            {"tempId": "op_6", "operatorType": "DatabaseWrite", "displayName": "记录到数据库", "parameters": {"DbType": "SQLite", "TableName": "InspectionResults"}},
            {"tempId": "op_7", "operatorType": "ResultOutput", "displayName": "输出结果", "parameters": {}}
          ],
          "connections": [
            {"sourceTempId": "op_1", "sourcePortName": "CycleCount", "targetTempId": "op_7", "targetPortName": "Result"},
            {"sourceTempId": "op_2", "sourcePortName": "Image", "targetTempId": "op_3", "targetPortName": "Image"},
            {"sourceTempId": "op_3", "sourcePortName": "Image", "targetTempId": "op_4", "targetPortName": "Image"},
            {"sourceTempId": "op_4", "sourcePortName": "BlobCount", "targetTempId": "op_5", "targetPortName": "Value"},
            {"sourceTempId": "op_5", "sourcePortName": "IsOk", "targetTempId": "op_6", "targetPortName": "Data"}
          ],
          "parametersNeedingReview": {
            "op_4": ["MinArea", "MaxArea"],
            "op_6": ["ConnectionString", "TableName"]
          }
        }

        ## 示例 6
        用户描述："检测两个定位点之间的距离，如果超过50像素则NG"

        正确输出：
        {
          "explanation": "采集图像后通过圆检测和模板匹配分别定位两个特征点，将两点坐标传入测量算子计算距离，再通过数值比较判断是否超限，最后根据比较结果分支输出",
          "operators": [
            {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "相机采集", "parameters": {"sourceType": "camera"}},
            {"tempId": "op_2", "operatorType": "CircleMeasurement", "displayName": "圆检测定位", "parameters": {"MinRadius": "10", "MaxRadius": "100"}},
            {"tempId": "op_3", "operatorType": "TemplateMatching", "displayName": "模板匹配定位", "parameters": {"Method": "NCC", "Threshold": "0.8"}},
            {"tempId": "op_4", "operatorType": "Measurement", "displayName": "两点距离", "parameters": {"MeasureType": "PointToPoint"}},
            {"tempId": "op_5", "operatorType": "Comparator", "displayName": "超限判断", "parameters": {"Condition": "GreaterThan", "CompareValue": "50"}},
            {"tempId": "op_6", "operatorType": "ConditionalBranch", "displayName": "OK/NG分支", "parameters": {}},
            {"tempId": "op_7", "operatorType": "ResultOutput", "displayName": "输出结果", "parameters": {}}
          ],
          "connections": [
            {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
            {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_3", "targetPortName": "Image"},
            {"sourceTempId": "op_2", "sourcePortName": "Center", "targetTempId": "op_4", "targetPortName": "PointA"},
            {"sourceTempId": "op_3", "sourcePortName": "Position", "targetTempId": "op_4", "targetPortName": "PointB"},
            {"sourceTempId": "op_4", "sourcePortName": "Distance", "targetTempId": "op_5", "targetPortName": "ValueA"},
            {"sourceTempId": "op_5", "sourcePortName": "Result", "targetTempId": "op_6", "targetPortName": "Value"},
            {"sourceTempId": "op_6", "sourcePortName": "True", "targetTempId": "op_7", "targetPortName": "Result"}
          ],
          "parametersNeedingReview": {
            "op_2": ["MinRadius", "MaxRadius"],
            "op_3": ["Threshold"],
            "op_5": ["CompareValue"]
          }
        }

        ## 示例 7
        用户描述："拍照检测后把结果通过TCP发给PLC，发送前等200毫秒让PLC准备好"

        正确输出：
        {
          "explanation": "采集图像后二值化检测缺陷，用结果判定OK/NG，延时200ms等待PLC就绪后通过TCP发送判定结果",
          "operators": [
            {"tempId": "op_1", "operatorType": "ImageAcquisition", "displayName": "相机采集", "parameters": {"sourceType": "camera"}},
            {"tempId": "op_2", "operatorType": "Thresholding", "displayName": "二值化", "parameters": {"UseOtsu": "true"}},
            {"tempId": "op_3", "operatorType": "BlobAnalysis", "displayName": "缺陷检测", "parameters": {"MinArea": "50"}},
            {"tempId": "op_4", "operatorType": "ResultJudgment", "displayName": "OK/NG判定", "parameters": {"FieldName": "BlobCount", "Condition": "LessThanOrEqual", "ThresholdValue": "0"}},
            {"tempId": "op_5", "operatorType": "Delay", "displayName": "等待PLC就绪", "parameters": {"Milliseconds": "200"}},
            {"tempId": "op_6", "operatorType": "TcpCommunication", "displayName": "发送结果", "parameters": {"IpAddress": "192.168.1.10", "Port": "8080"}}
          ],
          "connections": [
            {"sourceTempId": "op_1", "sourcePortName": "Image", "targetTempId": "op_2", "targetPortName": "Image"},
            {"sourceTempId": "op_2", "sourcePortName": "Image", "targetTempId": "op_3", "targetPortName": "Image"},
            {"sourceTempId": "op_3", "sourcePortName": "BlobCount", "targetTempId": "op_4", "targetPortName": "Value"},
            {"sourceTempId": "op_4", "sourcePortName": "IsOk", "targetTempId": "op_5", "targetPortName": "Input"},
            {"sourceTempId": "op_5", "sourcePortName": "Output", "targetTempId": "op_6", "targetPortName": "Data"}
          ],
          "parametersNeedingReview": {
            "op_5": ["Milliseconds"],
            "op_6": ["IpAddress", "Port"]
          }
        }
        """;
}
```

**重要说明**：`_operatorFactory.GetAllMetadata()` 方法可能需要在 `IOperatorFactory` 接口中新增。请检查现有的 `IOperatorFactory` 接口定义，如果没有返回全部元数据的方法，需要添加：

```csharp
// 在 Acme.Product.Core/Services/IOperatorFactory.cs 中新增
IEnumerable<OperatorMetadata> GetAllMetadata();
```

然后在 `OperatorFactory.cs` 实现中添加：
```csharp
public IEnumerable<OperatorMetadata> GetAllMetadata() => _metadata.Values;
```

### 2.2 实现 AI API 客户端

**文件路径**：`Acme.Product.Infrastructure/AI/AiApiClient.cs`

```csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// AI API 调用客户端（支持 Anthropic Claude 和 OpenAI）
/// </summary>
public class AiApiClient
{
    private readonly HttpClient _httpClient;
    private readonly AiGenerationOptions _options;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public AiApiClient(HttpClient httpClient, IOptions<AiGenerationOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    /// <summary>
    /// 调用 AI API 获取工作流 JSON
    /// </summary>
    /// <param name="systemPrompt">系统提示词</param>
    /// <param name="userMessage">用户消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>AI 返回的原始文本</returns>
    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        return _options.Provider.ToLower() switch
        {
            "anthropic" => await CallAnthropicAsync(systemPrompt, userMessage, cancellationToken),
            "openai" => await CallOpenAiAsync(systemPrompt, userMessage, cancellationToken),
            _ => throw new InvalidOperationException($"不支持的 AI 提供商：{_options.Provider}")
        };
    }

    private async Task<string> CallAnthropicAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model = _options.Model,
            max_tokens = _options.MaxTokens,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = userMessage }
            }
        };

        var apiUrl = _options.BaseUrl ?? "https://api.anthropic.com/v1/messages";
        var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        request.Headers.Add("x-api-key", _options.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        var response = await _httpClient.SendAsync(request, cts.Token);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseJson);

        // Anthropic 响应结构：{ "content": [{ "type": "text", "text": "..." }] }
        var content = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();

        return content ?? throw new InvalidOperationException("AI 返回了空响应");
    }

    private async Task<string> CallOpenAiAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model = _options.Model,
            max_tokens = _options.MaxTokens,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage }
            },
            response_format = new { type = "json_object" }  // OpenAI JSON 模式
        };

        var apiUrl = _options.BaseUrl ?? "https://api.openai.com/v1/chat/completions";
        var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        var response = await _httpClient.SendAsync(request, cts.Token);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseJson);

        // OpenAI 响应结构：{ "choices": [{ "message": { "content": "..." } }] }
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content ?? throw new InvalidOperationException("AI 返回了空响应");
    }
}
```

### 阶段二验收标准
- [ ] `PromptBuilder` 能正确生成包含全部已注册算子（当前 61+）的 System Prompt
- [ ] `AiApiClient` 能成功调用 Anthropic API（用一个简单的测试用例验证）
- [ ] 项目编译无错误

---

## 阶段三：后端 · 校验层（约 0.5 天）

### 目标
实现对 AI 输出 JSON 的严格校验，确保生成的工作流在加载到画布前是合法的。

### 3.1 实现校验器

**文件路径**：`Acme.Product.Infrastructure/AI/AiFlowValidator.cs`

```csharp
using Acme.Product.Application.DTOs;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;

namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// 校验 AI 生成的工作流是否满足所有约束
/// </summary>
public class AiFlowValidator : IAiFlowValidator
{
    private readonly IOperatorFactory _operatorFactory;

    public AiFlowValidator(IOperatorFactory operatorFactory)
    {
        _operatorFactory = operatorFactory;
    }

    public ValidationResult Validate(AiGeneratedFlowJson generatedFlow)
    {
        var result = new ValidationResult();

        if (generatedFlow.Operators == null || generatedFlow.Operators.Count == 0)
        {
            result.AddError("AI 未生成任何算子");
            return result;
        }

        // 建立 tempId → 算子元数据 的映射，用于后续校验
        var operatorMetaMap = new Dictionary<string, OperatorMetadata>();

        // 1. 校验算子类型合法性
        ValidateOperatorTypes(generatedFlow, result, operatorMetaMap);

        // 如果算子类型校验失败，后续校验意义不大
        if (!result.IsValid) return result;

        // 2. 校验端口名合法性和类型兼容性
        ValidateConnections(generatedFlow, result, operatorMetaMap);

        // 3. 校验无环路
        ValidateNoCycles(generatedFlow, result);

        // 4. 校验输入端口不重复占用
        ValidateNoDuplicateInputs(generatedFlow, result);

        // 5. 警告（不阻止生成，但记录）
        ValidateHasSourceAndOutput(generatedFlow, result, operatorMetaMap);

        return result;
    }

    private void ValidateOperatorTypes(
        AiGeneratedFlowJson flow,
        ValidationResult result,
        Dictionary<string, OperatorMetadata> metaMap)
    {
        var allTempIds = new HashSet<string>();

        foreach (var op in flow.Operators)
        {
            // 检查 tempId 格式
            if (string.IsNullOrWhiteSpace(op.TempId))
            {
                result.AddError("存在算子的 tempId 为空");
                continue;
            }

            if (allTempIds.Contains(op.TempId))
            {
                result.AddError($"tempId 重复：{op.TempId}");
                continue;
            }
            allTempIds.Add(op.TempId);

            // 检查算子类型是否在枚举中
            if (!Enum.TryParse<OperatorType>(op.OperatorType, out var operatorType))
            {
                result.AddError($"算子类型不存在：{op.OperatorType}（tempId={op.TempId}）。" +
                               $"请使用算子目录中的 operator_id 值。");
                continue;
            }

            // 检查算子元数据是否已注册
            var metadata = _operatorFactory.GetMetadata(operatorType);
            if (metadata == null)
            {
                result.AddError($"算子 {op.OperatorType} 未在算子工厂中注册");
                continue;
            }

            metaMap[op.TempId] = metadata;
        }
    }

    private void ValidateConnections(
        AiGeneratedFlowJson flow,
        ValidationResult result,
        Dictionary<string, OperatorMetadata> metaMap)
    {
        if (flow.Connections == null) return;

        foreach (var conn in flow.Connections)
        {
            // 检查源算子存在
            if (!metaMap.TryGetValue(conn.SourceTempId, out var sourceMeta))
            {
                result.AddError($"连线引用了不存在的源算子 tempId：{conn.SourceTempId}");
                continue;
            }

            // 检查目标算子存在
            if (!metaMap.TryGetValue(conn.TargetTempId, out var targetMeta))
            {
                result.AddError($"连线引用了不存在的目标算子 tempId：{conn.TargetTempId}");
                continue;
            }

            // 检查源端口存在
            var sourcePort = sourceMeta.OutputPorts.FirstOrDefault(p => p.Name == conn.SourcePortName);
            if (sourcePort == null)
            {
                result.AddError($"算子 {conn.SourceTempId}({sourceMeta.DisplayName}) " +
                               $"没有名为 '{conn.SourcePortName}' 的输出端口。" +
                               $"可用输出端口：{string.Join(", ", sourceMeta.OutputPorts.Select(p => p.Name))}");
                continue;
            }

            // 检查目标端口存在
            var targetPort = targetMeta.InputPorts.FirstOrDefault(p => p.Name == conn.TargetPortName);
            if (targetPort == null)
            {
                result.AddError($"算子 {conn.TargetTempId}({targetMeta.DisplayName}) " +
                               $"没有名为 '{conn.TargetPortName}' 的输入端口。" +
                               $"可用输入端口：{string.Join(", ", targetMeta.InputPorts.Select(p => p.Name))}");
                continue;
            }

            // 检查类型兼容性
            if (!AreTypesCompatible(sourcePort.DataType, targetPort.DataType))
            {
                result.AddError($"端口类型不兼容：" +
                               $"{conn.SourceTempId}.{conn.SourcePortName}({sourcePort.DataType}) → " +
                               $"{conn.TargetTempId}.{conn.TargetPortName}({targetPort.DataType})");
            }
        }
    }

    private bool AreTypesCompatible(PortDataType source, PortDataType target)
    {
        // Any 类型与任何类型兼容
        if (source == PortDataType.Any || target == PortDataType.Any) return true;
        // 相同类型兼容
        if (source == target) return true;

        // 数值类型互通（Integer ↔ Float）
        var numericTypes = new[] { PortDataType.Integer, PortDataType.Float };
        if (numericTypes.Contains(source) && numericTypes.Contains(target)) return true;

        // 几何类型互通（Point ↔ Rectangle）
        var geometryTypes = new[] { PortDataType.Point, PortDataType.Rectangle };
        if (geometryTypes.Contains(source) && geometryTypes.Contains(target)) return true;

        // String 可以作为数值类型的输入（运行时转换）
        if (source == PortDataType.String && numericTypes.Contains(target)) return true;

        // Boolean 可以作为 Integer 的输入（true=1, false=0）
        if (source == PortDataType.Boolean && target == PortDataType.Integer) return true;

        return false;
    }

    private void ValidateNoCycles(AiGeneratedFlowJson flow, ValidationResult result)
    {
        if (flow.Connections == null || flow.Connections.Count == 0) return;

        // 构建邻接表
        var adjacency = new Dictionary<string, List<string>>();
        foreach (var op in flow.Operators)
            adjacency[op.TempId] = new List<string>();

        foreach (var conn in flow.Connections)
        {
            if (adjacency.ContainsKey(conn.SourceTempId))
                adjacency[conn.SourceTempId].Add(conn.TargetTempId);
        }

        // DFS 检测环路
        var visited = new HashSet<string>();
        var inStack = new HashSet<string>();

        foreach (var node in adjacency.Keys)
        {
            if (HasCycle(node, adjacency, visited, inStack))
            {
                result.AddError("工作流中存在环路（循环依赖），请重新设计流程结构");
                return;
            }
        }
    }

    private bool HasCycle(
        string node,
        Dictionary<string, List<string>> adjacency,
        HashSet<string> visited,
        HashSet<string> inStack)
    {
        if (inStack.Contains(node)) return true;
        if (visited.Contains(node)) return false;

        visited.Add(node);
        inStack.Add(node);

        foreach (var neighbor in adjacency.GetValueOrDefault(node, new List<string>()))
        {
            if (HasCycle(neighbor, adjacency, visited, inStack)) return true;
        }

        inStack.Remove(node);
        return false;
    }

    private void ValidateNoDuplicateInputs(AiGeneratedFlowJson flow, ValidationResult result)
    {
        if (flow.Connections == null) return;

        var inputPortUsage = new HashSet<string>();
        foreach (var conn in flow.Connections)
        {
            var key = $"{conn.TargetTempId}:{conn.TargetPortName}";
            if (!inputPortUsage.Add(key))
            {
                result.AddError($"输入端口被重复连接：算子 {conn.TargetTempId} 的 {conn.TargetPortName} 端口" +
                               $"只能接收一条连线");
            }
        }
    }

    private void ValidateHasSourceAndOutput(
        AiGeneratedFlowJson flow,
        ValidationResult result,
        Dictionary<string, OperatorMetadata> metaMap)
    {
        // 警告：没有源算子
        var hasSource = flow.Operators.Any(op =>
            metaMap.TryGetValue(op.TempId, out var meta) &&
            meta.InputPorts.Count == 0);

        if (!hasSource)
            result.AddWarning("工作流没有图像源算子（无输入端口的算子），建议添加 ImageAcquisition");

        // 警告：没有 ResultOutput
        var hasOutput = flow.Operators.Any(op =>
            op.OperatorType == "ResultOutput" ||
            (metaMap.TryGetValue(op.TempId, out var meta) && meta.Category == "输出"));

        if (!hasOutput)
            result.AddWarning("工作流没有结果输出算子，建议添加 ResultOutput");
    }
}
```

### 阶段三验收标准
- [ ] 校验器对非法算子类型返回明确的错误信息
- [ ] 校验器对端口名错误返回含"可用端口"提示的错误信息
- [ ] 校验器对环路正确检测
- [ ] 校验器对重复输入端口正确检测

---

## 阶段四：后端 · 核心生成服务（约 0.5 天）

### 目标
将 PromptBuilder、AiApiClient、AiFlowValidator 组装为完整的生成服务，并实现自动重试逻辑和 DTO 转换。

### 4.1 实现自动布局服务

**文件路径**：`Acme.Product.Infrastructure/AI/AutoLayoutService.cs`

```csharp
using Acme.Product.Application.DTOs;

namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// 为 AI 生成的算子自动计算画布坐标（拓扑分层布局）
/// </summary>
public class AutoLayoutService
{
    private const double LayerWidth = 200.0;   // 每层的水平间距（像素），与项目现有风格一致
    private const double NodeHeight = 150.0;   // 每个节点的垂直间距（像素）
    private const double StartX = 80.0;        // 起始 X 坐标
    private const double StartY = 100.0;       // 起始 Y 坐标

    /// <summary>
    /// 为流程 DTO 中的所有算子分配坐标
    /// </summary>
    public void ApplyLayout(OperatorFlowDto flowDto)
    {
        if (flowDto.Operators == null || flowDto.Operators.Count == 0) return;

        var layers = ComputeTopologicalLayers(flowDto);

        for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            var layer = layers[layerIndex];
            for (int nodeIndex = 0; nodeIndex < layer.Count; nodeIndex++)
            {
                var operatorId = layer[nodeIndex];
                var op = flowDto.Operators.FirstOrDefault(o => o.Id.ToString() == operatorId);
                if (op != null)
                {
                    op.X = StartX + layerIndex * LayerWidth;
                    op.Y = StartY + nodeIndex * NodeHeight;
                }
            }
        }
    }

    private List<List<string>> ComputeTopologicalLayers(OperatorFlowDto flowDto)
    {
        var operatorIds = flowDto.Operators.Select(o => o.Id.ToString()).ToList();

        // 计算每个算子的入度（被多少算子指向）
        var inDegree = operatorIds.ToDictionary(id => id, _ => 0);

        // 构建邻接表（谁指向谁）
        var adjacency = operatorIds.ToDictionary(id => id, _ => new List<string>());
        var reverseAdj = operatorIds.ToDictionary(id => id, _ => new List<string>());

        if (flowDto.Connections != null)
        {
            foreach (var conn in flowDto.Connections)
            {
                var srcId = conn.SourceOperatorId.ToString();
                var tgtId = conn.TargetOperatorId.ToString();

                if (adjacency.ContainsKey(srcId) && inDegree.ContainsKey(tgtId))
                {
                    adjacency[srcId].Add(tgtId);
                    reverseAdj[tgtId].Add(srcId);
                    inDegree[tgtId]++;
                }
            }
        }

        // Kahn 算法拓扑排序，同一层的节点并行
        var layers = new List<List<string>>();
        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));

        while (queue.Count > 0)
        {
            var currentLayer = new List<string>();
            var layerSize = queue.Count;

            for (int i = 0; i < layerSize; i++)
            {
                var node = queue.Dequeue();
                currentLayer.Add(node);

                foreach (var neighbor in adjacency[node])
                {
                    inDegree[neighbor]--;
                    if (inDegree[neighbor] == 0)
                        queue.Enqueue(neighbor);
                }
            }

            if (currentLayer.Count > 0)
                layers.Add(currentLayer);
        }

        // 处理未被排入的孤立节点（无连线的算子）
        var layouted = layers.SelectMany(l => l).ToHashSet();
        var isolated = operatorIds.Where(id => !layouted.Contains(id)).ToList();
        if (isolated.Count > 0)
            layers.Add(isolated);

        return layers;
    }
}
```

### 4.2 实现核心生成服务

**文件路径**：`Acme.Product.Infrastructure/AI/AiFlowGenerationService.cs`

```csharp
using System.Text.Json;
using Acme.Product.Application.DTOs;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acme.Product.Infrastructure.AI;

public class AiFlowGenerationService : IAiFlowGenerationService
{
    private readonly AiApiClient _apiClient;
    private readonly PromptBuilder _promptBuilder;
    private readonly AiFlowValidator _validator;
    private readonly AutoLayoutService _layoutService;
    private readonly IOperatorFactory _operatorFactory;
    private readonly AiGenerationOptions _options;
    private readonly ILogger<AiFlowGenerationService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AiFlowGenerationService(
        AiApiClient apiClient,
        PromptBuilder promptBuilder,
        AiFlowValidator validator,
        AutoLayoutService layoutService,
        IOperatorFactory operatorFactory,
        IOptions<AiGenerationOptions> options,
        ILogger<AiFlowGenerationService> logger)
    {
        _apiClient = apiClient;
        _promptBuilder = promptBuilder;
        _validator = validator;
        _layoutService = layoutService;
        _operatorFactory = operatorFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AiFlowGenerationResult> GenerateFlowAsync(
        AiFlowGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = _promptBuilder.BuildSystemPrompt();
        var userMessage = BuildUserMessage(request);

        AiGeneratedFlowJson? generatedFlow = null;
        ValidationResult? lastValidation = null;
        int retryCount = 0;

        for (int attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("调用 AI API，第 {Attempt} 次尝试", attempt + 1);

                // 构建消息（第一次是用户描述，重试时追加错误信息）
                var currentMessage = attempt == 0
                    ? userMessage
                    : BuildRetryMessage(userMessage, lastValidation!);

                var rawResponse = await _apiClient.CompleteAsync(systemPrompt, currentMessage, cancellationToken);

                _logger.LogDebug("AI 原始响应：{Response}", rawResponse);

                // 解析 AI 输出的 JSON
                generatedFlow = ParseAiResponse(rawResponse);
                if (generatedFlow == null)
                {
                    lastValidation = ValidationResult.Failure("AI 返回的内容不是合法的 JSON 格式");
                    retryCount++;
                    continue;
                }

                // 校验
                lastValidation = _validator.Validate(generatedFlow);
                if (lastValidation.IsValid)
                {
                    // 校验通过，转换为 DTO 并返回
                    var flowDto = ConvertToFlowDto(generatedFlow, request.Description);
                    _layoutService.ApplyLayout(flowDto);

                    return new AiFlowGenerationResult
                    {
                        Success = true,
                        Flow = flowDto,
                        AiExplanation = generatedFlow.Explanation,
                        ParametersNeedingReview = generatedFlow.ParametersNeedingReview,
                        RetryCount = retryCount
                    };
                }

                _logger.LogWarning("AI 生成内容校验失败，错误：{Errors}",
                    string.Join("; ", lastValidation.Errors));
                retryCount++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "AI API 调用失败");
                return new AiFlowGenerationResult
                {
                    Success = false,
                    ErrorMessage = $"AI 服务调用失败：{ex.Message}"
                };
            }
        }

        // 所有重试均失败
        return new AiFlowGenerationResult
        {
            Success = false,
            ErrorMessage = $"AI 生成的工作流未通过校验（已重试 {retryCount} 次）：" +
                          string.Join("；", lastValidation?.Errors ?? new List<string>()),
            RetryCount = retryCount
        };
    }

    private string BuildUserMessage(AiFlowGenerationRequest request)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"请根据以下描述生成工作流：");
        sb.AppendLine();
        sb.AppendLine(request.Description);

        if (!string.IsNullOrWhiteSpace(request.AdditionalContext))
        {
            sb.AppendLine();
            sb.AppendLine($"补充信息：{request.AdditionalContext}");
        }

        sb.AppendLine();
        sb.AppendLine("请严格按照规定的 JSON 格式输出，不要包含任何其他文字。");

        return sb.ToString();
    }

    private string BuildRetryMessage(string originalMessage, ValidationResult failedValidation)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(originalMessage);
        sb.AppendLine();
        sb.AppendLine("【你上次的输出存在以下错误，请修正后重新生成】");
        foreach (var error in failedValidation.Errors)
            sb.AppendLine($"- {error}");
        sb.AppendLine();
        sb.AppendLine("请重新生成完整的 JSON，不要只修改部分内容。");

        return sb.ToString();
    }

    private AiGeneratedFlowJson? ParseAiResponse(string rawResponse)
    {
        try
        {
            // 清理可能的 Markdown 代码块包装
            var json = rawResponse.Trim();
            if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                json = json[7..];
            if (json.StartsWith("```"))
                json = json[3..];
            if (json.EndsWith("```"))
                json = json[..^3];
            json = json.Trim();

            return JsonSerializer.Deserialize<AiGeneratedFlowJson>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("解析 AI 响应 JSON 失败：{Error}", ex.Message);
            return null;
        }
    }

    private OperatorFlowDto ConvertToFlowDto(AiGeneratedFlowJson generated, string userDescription)
    {
        // tempId → 实际 Guid 的映射
        var idMapping = new Dictionary<string, Guid>();
        foreach (var op in generated.Operators)
            idMapping[op.TempId] = Guid.NewGuid();

        var operators = generated.Operators.Select(op =>
        {
            var metadata = _operatorFactory.GetMetadata(Enum.Parse<OperatorType>(op.OperatorType));
            return new OperatorDto
            {
                Id = idMapping[op.TempId],
                Type = Enum.Parse<OperatorType>(op.OperatorType),
                Name = op.DisplayName,
                X = 0, // 由 AutoLayoutService 填充
                Y = 0,
                Parameters = op.Parameters.ToDictionary(
                    kv => kv.Key,
                    kv => (object)kv.Value)
            };
        }).ToList();

        // ❗❗ 关键转换逻辑：端口名称 → 端口索引/ID
        // AI 返回的是 PortName（字符串），但项目实际 ConnectionDto 需要 PortId（GUID）。
        // 在查找端口时必须严格按 **方向** 过滤：
        //   - SourcePortName 必须在源算子的 OutputPorts 中查找
        //   - TargetPortName 必须在目标算子的 InputPorts 中查找
        // 这是因为同一算子可能存在同名的输入和输出端口（如 "Image"）。
        var connections = generated.Connections?.Select(conn =>
        {
            var srcOperator = generated.Operators.First(o => o.TempId == conn.SourceTempId);
            var tgtOperator = generated.Operators.First(o => o.TempId == conn.TargetTempId);
            var srcMeta = _operatorFactory.GetMetadata(Enum.Parse<OperatorType>(srcOperator.OperatorType));
            var tgtMeta = _operatorFactory.GetMetadata(Enum.Parse<OperatorType>(tgtOperator.OperatorType));

            // ❗ 严格方向过滤：源只查 OutputPorts，目标只查 InputPorts
            var srcPortIndex = srcMeta.OutputPorts.FindIndex(p => p.Name == conn.SourcePortName);
            var tgtPortIndex = tgtMeta.InputPorts.FindIndex(p => p.Name == conn.TargetPortName);

            if (srcPortIndex < 0)
                throw new InvalidOperationException(
                    $"源算子 {srcOperator.OperatorType} 不存在输出端口 '{conn.SourcePortName}'，" +
                    $"可用输出端口：{string.Join(", ", srcMeta.OutputPorts.Select(p => p.Name))}");
            if (tgtPortIndex < 0)
                throw new InvalidOperationException(
                    $"目标算子 {tgtOperator.OperatorType} 不存在输入端口 '{conn.TargetPortName}'，" +
                    $"可用输入端口：{string.Join(", ", tgtMeta.InputPorts.Select(p => p.Name))}");

            return new ConnectionDto
            {
                Id = Guid.NewGuid(),
                SourceOperatorId = idMapping[conn.SourceTempId],
                SourcePortIndex = srcPortIndex,
                SourcePortName = conn.SourcePortName,
                TargetOperatorId = idMapping[conn.TargetTempId],
                TargetPortIndex = tgtPortIndex,
                TargetPortName = conn.TargetPortName
            };
        }).ToList() ?? new List<ConnectionDto>();

        return new OperatorFlowDto
        {
            Id = Guid.NewGuid(),
            Name = $"AI生成 - {userDescription[..Math.Min(20, userDescription.Length)]}",
            Operators = operators,
            Connections = connections
        };
    }
}
```

**注意**：`OperatorDto`、`ConnectionDto`、`OperatorFlowDto` 是项目中已有的 DTO 类。上述代码中的属性名（`Id`、`Type`、`Name`、`X`、`Y`、`Parameters`、`SourceOperatorId`、`SourcePortIndex` 等）需要与实际 DTO 的属性名保持一致，请在实现前确认 `Acme.Product.Application/DTOs/` 目录下的实际字段名，如有不一致则以实际字段名为准。

### 阶段四验收标准
- [ ] `AutoLayoutService` 能为 5 个算子的串行流程正确分配不重叠的坐标
- [ ] `AiFlowGenerationService` 在校验失败时能正确重试（最多 2 次）
- [ ] `ConvertToFlowDto` 能正确将 AI 输出转换为项目现有的 DTO 格式

---

## 阶段五：后端 · 依赖注入注册（约 0.5 小时）

### 目标
在 DI 容器中注册所有新增服务。

### 5.1 创建扩展方法

**文件路径**：`Acme.Product.Infrastructure/AI/AiGenerationServiceExtensions.cs`

```csharp
using Acme.Product.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.Product.Infrastructure.AI;

public static class AiGenerationServiceExtensions
{
    public static IServiceCollection AddAiFlowGeneration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 注册配置
        services.Configure<AiGenerationOptions>(
            configuration.GetSection(AiGenerationOptions.SectionName));

        // 注册 HttpClient（为 AI API 调用配置超时）
        services.AddHttpClient<AiApiClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(120); // 给充足的超时时间
        });

        // 注册各服务
        services.AddScoped<PromptBuilder>();
        services.AddScoped<AiFlowValidator>();
        services.AddScoped<AutoLayoutService>();
        services.AddScoped<IAiFlowGenerationService, AiFlowGenerationService>();

        return services;
    }
}
```

### 5.2 在入口点注册

在 `Acme.Product.Desktop/Program.cs` 或主服务注册位置（找到现有的 `services.Add...` 调用），添加：

```csharp
// 在现有服务注册代码附近添加
services.AddAiFlowGeneration(configuration);
```

### 阶段五验收标准
- [ ] 项目启动后不报 DI 注册相关错误
- [ ] 可以通过 DI 容器解析 `IAiFlowGenerationService`

---

## 阶段六：后端 · WebView2 消息处理（约 1 小时）

### 目标
将 AI 生成服务接入 WebView2 消息通信管道。

### 6.1 创建消息处理器

**文件路径**：`Acme.Product.Infrastructure/AI/GenerateFlowMessageHandler.cs`

```csharp
using System.Text.Json;
using Acme.Product.Application.DTOs;
using Acme.Product.Contracts.Messages;
using Acme.Product.Core.Services;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// 处理前端发来的 GenerateFlow 消息
/// </summary>
public class GenerateFlowMessageHandler
{
    private readonly IAiFlowGenerationService _generationService;
    private readonly ILogger<GenerateFlowMessageHandler> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GenerateFlowMessageHandler(
        IAiFlowGenerationService generationService,
        ILogger<GenerateFlowMessageHandler> logger)
    {
        _generationService = generationService;
        _logger = logger;
    }

    public async Task<string> HandleAsync(
        string description,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("收到 AI 生成请求：{Description}", description);

        try
        {
            var result = await _generationService.GenerateFlowAsync(
                new AiFlowGenerationRequest(description),
                cancellationToken);

            var response = new GenerateFlowResponse
            {
                Success = result.Success,
                Flow = result.Flow,
                ErrorMessage = result.ErrorMessage,
                AiExplanation = result.AiExplanation,
                ParametersNeedingReview = result.ParametersNeedingReview
            };

            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理 AI 生成请求时发生未预期错误");

            var errorResponse = new GenerateFlowResponse
            {
                Success = false,
                ErrorMessage = $"服务内部错误：{ex.Message}"
            };

            return JsonSerializer.Serialize(errorResponse, _jsonOptions);
        }
    }
}
```

### 6.2 在现有消息路由中注册新消息类型

找到项目中处理 WebView2 消息的分发逻辑（通常在桌面层的某个 `MessageRouter` 或 `WebMessageHandler` 类中），添加对 `GenerateFlow` 消息类型的处理。

参照已有消息处理器的注册方式，将 `GenerateFlowMessageHandler` 注册进去。具体代码因现有架构而异，以下是通用模式：

```csharp
// 在消息分发 switch/if 中添加
case "GenerateFlow":
    var payload = message.GetProperty("payload");
    var descriptionStr = payload.GetProperty("description").GetString() ?? "";
    var handler = serviceProvider.GetRequiredService<GenerateFlowMessageHandler>();
    var resultJson = await handler.HandleAsync(descriptionStr, cancellationToken);
    // 将 resultJson 发回前端
    await SendToFrontendAsync(resultJson);
    break;
```

同时在 DI 注册中添加 Handler：
```csharp
services.AddScoped<GenerateFlowMessageHandler>();
```

### 阶段六验收标准
- [ ] 从前端发送 `{ type: "GenerateFlow", payload: { description: "..." } }` 消息后，后端能正确接收并处理
- [ ] 后端能将结果以 `GenerateFlowResult` 类型消息发回前端

---

## 阶段七：前端 · UI 组件（约 1 天）

### 目标
在前端实现用户输入界面和生成结果的渲染逻辑。

### 7.1 创建 AI 生成对话框组件

**文件路径**：`Acme.Product.Desktop/wwwroot/src/features/ai-generation/aiGenerationDialog.js`

```javascript
/**
 * AI 工作流生成对话框
 * 提供自然语言输入，调用后端 AI 生成接口，并将结果渲染到画布
 */
export class AiGenerationDialog {
    constructor(canvas) {
        this.canvas = canvas;
        this.isGenerating = false;
        this._init();
    }

    _init() {
        this._injectStyles();
        this._createDialogDom();
        this._bindEvents();
        this._setupMessageListener();
    }

    /**
     * 打开对话框
     */
    open() {
        document.getElementById('ai-gen-overlay').style.display = 'flex';
        document.getElementById('ai-gen-input').focus();
    }

    /**
     * 关闭对话框
     */
    close() {
        if (this.isGenerating) return; // 生成中不允许关闭
        document.getElementById('ai-gen-overlay').style.display = 'none';
        this._resetState();
    }

    _createDialogDom() {
        const overlay = document.createElement('div');
        overlay.id = 'ai-gen-overlay';
        overlay.innerHTML = `
            <div class="ai-gen-dialog">
                <div class="ai-gen-header">
                    <span class="ai-gen-title">✨ AI 一键生成工程</span>
                    <button class="ai-gen-close" id="ai-gen-close-btn">×</button>
                </div>

                <div class="ai-gen-body">
                    <div class="ai-gen-tip">
                        用自然语言描述你的检测需求，AI 将自动选择算子并连线。
                    </div>

                    <!-- 示例场景快捷选择 -->
                    <div class="ai-gen-examples">
                        <span class="ai-gen-examples-label">快速示例：</span>
                        <button class="ai-gen-example-btn" data-text="用相机拍照检测产品表面缺陷，统计缺陷数量">缺陷检测</button>
                        <button class="ai-gen-example-btn" data-text="扫描产品上的二维码，识别内容后通过Modbus发送给PLC">条码读取</button>
                        <button class="ai-gen-example-btn" data-text="测量两个圆孔之间的距离，需要先进行相机标定">孔距测量</button>
                        <button class="ai-gen-example-btn" data-text="用深度学习检测缺陷，同时用传统算法验证，两者都通过才算合格">双模态检测</button>
                    </div>

                    <!-- 输入框 -->
                    <textarea
                        id="ai-gen-input"
                        class="ai-gen-textarea"
                        placeholder="例如：用相机采集图像，检测产品表面划痕，将检测结果保存到数据库..."
                        rows="4"
                    ></textarea>

                    <!-- 进度区域（生成中显示） -->
                    <div class="ai-gen-progress" id="ai-gen-progress" style="display:none">
                        <div class="ai-gen-spinner"></div>
                        <span id="ai-gen-progress-text">正在连接 AI...</span>
                    </div>

                    <!-- AI 说明（生成成功后显示） -->
                    <div class="ai-gen-explanation" id="ai-gen-explanation" style="display:none">
                        <div class="ai-gen-explanation-label">💡 AI 说明</div>
                        <div id="ai-gen-explanation-text" class="ai-gen-explanation-content"></div>
                    </div>

                    <!-- 错误信息 -->
                    <div class="ai-gen-error" id="ai-gen-error" style="display:none">
                        <span id="ai-gen-error-text"></span>
                    </div>

                    <!-- 参数确认提示 -->
                    <div class="ai-gen-review-hint" id="ai-gen-review-hint" style="display:none">
                        ⚠️ 部分参数需要你手动确认（画布中已用橙色高亮标记）
                    </div>
                </div>

                <div class="ai-gen-footer">
                    <button class="ai-gen-btn-cancel" id="ai-gen-cancel-btn">取消</button>
                    <button class="ai-gen-btn-generate" id="ai-gen-generate-btn">
                        ✨ 生成工程
                    </button>
                </div>
            </div>
        `;
        document.body.appendChild(overlay);
    }

    _bindEvents() {
        // 关闭按钮
        document.getElementById('ai-gen-close-btn')
            .addEventListener('click', () => this.close());
        document.getElementById('ai-gen-cancel-btn')
            .addEventListener('click', () => this.close());

        // 点击遮罩关闭
        document.getElementById('ai-gen-overlay')
            .addEventListener('click', (e) => {
                if (e.target.id === 'ai-gen-overlay') this.close();
            });

        // 生成按钮
        document.getElementById('ai-gen-generate-btn')
            .addEventListener('click', () => this._handleGenerate());

        // 快捷示例按钮
        document.querySelectorAll('.ai-gen-example-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                document.getElementById('ai-gen-input').value = btn.dataset.text;
            });
        });

        // Ctrl+Enter 快捷生成
        document.getElementById('ai-gen-input')
            .addEventListener('keydown', (e) => {
                if (e.ctrlKey && e.key === 'Enter') this._handleGenerate();
            });
    }

    _setupMessageListener() {
        // 监听后端返回的生成结果
        window.addEventListener('backendMessage', (e) => {
            const message = e.detail;
            if (message.type === 'GenerateFlowResult') {
                this._handleGenerationResult(message);
            } else if (message.type === 'GenerateFlowProgress') {
                this._updateProgress(message.message);
            }
        });
    }

    async _handleGenerate() {
        const description = document.getElementById('ai-gen-input').value.trim();
        if (!description) {
            this._showError('请输入检测需求描述');
            return;
        }

        if (this.isGenerating) return;

        this._setGeneratingState(true);

        try {
            // 发送消息给后端
            window.chrome.webview.postMessage(JSON.stringify({
                type: 'GenerateFlow',
                payload: { description }
            }));
        } catch (err) {
            this._setGeneratingState(false);
            this._showError('发送请求失败：' + err.message);
        }
    }

    _handleGenerationResult(message) {
        this._setGeneratingState(false);

        if (!message.success) {
            this._showError(message.errorMessage || 'AI 生成失败，请重试');
            return;
        }

        // 显示 AI 说明
        if (message.aiExplanation) {
            document.getElementById('ai-gen-explanation-text').textContent = message.aiExplanation;
            document.getElementById('ai-gen-explanation').style.display = 'block';
        }

        // 渲染到画布
        try {
            this.canvas.deserialize(message.flow);

            // 高亮需要用户确认的参数
            if (message.parametersNeedingReview &&
                Object.keys(message.parametersNeedingReview).length > 0) {
                this._highlightReviewParams(message.parametersNeedingReview);
                document.getElementById('ai-gen-review-hint').style.display = 'block';
            }

            // 延迟关闭，让用户看到说明
            setTimeout(() => this.close(), 2000);

        } catch (err) {
            this._showError('工作流渲染失败：' + err.message);
            console.error('[AiGeneration] 渲染失败', err, message.flow);
        }
    }

    /**
     * 高亮需要用户确认参数的算子节点
     * @param {Object} paramsNeedingReview - { operatorId: ['param1', 'param2'] }
     */
    _highlightReviewParams(paramsNeedingReview) {
        // 通知画布高亮特定节点
        // 具体实现取决于 flowCanvas.js 的节点渲染机制
        // 方案：在节点上添加一个橙色的"需确认"标记
        if (typeof this.canvas.highlightNodes === 'function') {
            const nodeIds = Object.keys(paramsNeedingReview);
            this.canvas.highlightNodes(nodeIds, 'review-needed');
        }
    }

    _setGeneratingState(generating) {
        this.isGenerating = generating;
        const btn = document.getElementById('ai-gen-generate-btn');
        const progress = document.getElementById('ai-gen-progress');
        const input = document.getElementById('ai-gen-input');

        btn.disabled = generating;
        btn.textContent = generating ? '生成中...' : '✨ 生成工程';
        progress.style.display = generating ? 'flex' : 'none';
        input.disabled = generating;

        // 清除之前的错误和说明
        if (generating) {
            document.getElementById('ai-gen-error').style.display = 'none';
            document.getElementById('ai-gen-explanation').style.display = 'none';
            document.getElementById('ai-gen-review-hint').style.display = 'none';
        }
    }

    _updateProgress(message) {
        document.getElementById('ai-gen-progress-text').textContent = message;
    }

    _showError(message) {
        const el = document.getElementById('ai-gen-error');
        document.getElementById('ai-gen-error-text').textContent = message;
        el.style.display = 'block';
    }

    _resetState() {
        document.getElementById('ai-gen-input').value = '';
        document.getElementById('ai-gen-error').style.display = 'none';
        document.getElementById('ai-gen-explanation').style.display = 'none';
        document.getElementById('ai-gen-review-hint').style.display = 'none';
        document.getElementById('ai-gen-progress').style.display = 'none';
    }

    _injectStyles() {
        const style = document.createElement('style');
        style.textContent = `
            #ai-gen-overlay {
                display: none;
                position: fixed; inset: 0; z-index: 9999;
                background: rgba(0,0,0,0.5);
                align-items: center; justify-content: center;
            }
            .ai-gen-dialog {
                background: #1e1e2e; border-radius: 12px;
                width: 560px; max-width: 90vw;
                box-shadow: 0 20px 60px rgba(0,0,0,0.5);
                border: 1px solid #383850;
                font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
                color: #e0e0e0;
            }
            .ai-gen-header {
                padding: 16px 20px;
                border-bottom: 1px solid #383850;
                display: flex; justify-content: space-between; align-items: center;
            }
            .ai-gen-title { font-size: 16px; font-weight: 600; color: #a78bfa; }
            .ai-gen-close {
                background: none; border: none; color: #888;
                font-size: 20px; cursor: pointer; padding: 0 4px; line-height: 1;
            }
            .ai-gen-close:hover { color: #e0e0e0; }
            .ai-gen-body { padding: 20px; display: flex; flex-direction: column; gap: 12px; }
            .ai-gen-tip { font-size: 13px; color: #888; }
            .ai-gen-examples { display: flex; gap: 8px; flex-wrap: wrap; align-items: center; }
            .ai-gen-examples-label { font-size: 12px; color: #666; white-space: nowrap; }
            .ai-gen-example-btn {
                font-size: 12px; padding: 4px 10px;
                background: #2d2d44; border: 1px solid #4a4a66; border-radius: 20px;
                color: #a0a0c0; cursor: pointer; transition: all 0.2s;
            }
            .ai-gen-example-btn:hover { background: #3d3d60; border-color: #7c7caa; color: #e0e0e0; }
            .ai-gen-textarea {
                width: 100%; padding: 10px 12px; box-sizing: border-box;
                background: #13131f; border: 1px solid #383850; border-radius: 8px;
                color: #e0e0e0; font-size: 14px; line-height: 1.5; resize: vertical;
                outline: none; transition: border-color 0.2s;
            }
            .ai-gen-textarea:focus { border-color: #7c3aed; }
            .ai-gen-progress {
                display: flex; align-items: center; gap: 10px;
                font-size: 13px; color: #a78bfa;
            }
            .ai-gen-spinner {
                width: 16px; height: 16px; border-radius: 50%;
                border: 2px solid #383850; border-top-color: #a78bfa;
                animation: ai-spin 0.8s linear infinite;
            }
            @keyframes ai-spin { to { transform: rotate(360deg); } }
            .ai-gen-explanation {
                background: #1a1a30; border: 1px solid #3d3d60;
                border-radius: 8px; padding: 12px;
            }
            .ai-gen-explanation-label { font-size: 12px; color: #888; margin-bottom: 6px; }
            .ai-gen-explanation-content { font-size: 13px; color: #b0b0d0; line-height: 1.5; }
            .ai-gen-error {
                background: #2d1515; border: 1px solid #5a2020;
                border-radius: 8px; padding: 10px 12px;
                font-size: 13px; color: #ff8080;
            }
            .ai-gen-review-hint {
                font-size: 13px; color: #f59e0b;
                background: #2d2010; border: 1px solid #5a4010;
                border-radius: 8px; padding: 10px 12px;
            }
            .ai-gen-footer {
                padding: 16px 20px;
                border-top: 1px solid #383850;
                display: flex; justify-content: flex-end; gap: 10px;
            }
            .ai-gen-btn-cancel {
                padding: 8px 20px; border-radius: 6px;
                background: transparent; border: 1px solid #383850;
                color: #888; cursor: pointer; font-size: 14px;
            }
            .ai-gen-btn-cancel:hover { border-color: #6060a0; color: #e0e0e0; }
            .ai-gen-btn-generate {
                padding: 8px 24px; border-radius: 6px;
                background: linear-gradient(135deg, #7c3aed, #4f46e5);
                border: none; color: white; cursor: pointer;
                font-size: 14px; font-weight: 500;
                transition: opacity 0.2s;
            }
            .ai-gen-btn-generate:hover:not(:disabled) { opacity: 0.85; }
            .ai-gen-btn-generate:disabled { opacity: 0.5; cursor: not-allowed; }
        `;
        document.head.appendChild(style);
    }
}
```

### 7.2 在主应用中注册 AI 生成对话框

找到前端的主入口文件（`index.html` 或主 JS 文件），在画布初始化完成后，初始化并挂载 AI 生成对话框：

```javascript
// 在画布初始化之后
import { AiGenerationDialog } from './features/ai-generation/aiGenerationDialog.js';

// 假设 canvas 是已初始化的 FlowCanvas 实例
const aiGenDialog = new AiGenerationDialog(canvas);

// 将 aiGenDialog 暴露到全局，方便工具栏按钮调用
window.aiGenDialog = aiGenDialog;
```

### 7.3 在工具栏添加触发按钮

找到前端工具栏的 HTML 或 JS 创建代码，添加"AI 生成"按钮：

```html
<!-- 在工具栏的合适位置添加 -->
<button class="toolbar-btn ai-gen-trigger" onclick="window.aiGenDialog.open()" title="AI 一键生成工程 (Ctrl+G)">
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <path d="M12 2L2 7l10 5 10-5-10-5z"/>
        <path d="M2 17l10 5 10-5"/>
        <path d="M2 12l10 5 10-5"/>
    </svg>
    AI 生成
</button>
```

同时添加全局快捷键：
```javascript
document.addEventListener('keydown', (e) => {
    if (e.ctrlKey && e.key === 'g') {
        e.preventDefault();
        window.aiGenDialog.open();
    }
});
```

### 7.4 确认 canvas.deserialize() 的输入格式

在调用 `canvas.deserialize(message.flow)` 前，需要确认前端 `flowCanvas.js` 中 `deserialize()` 方法期望的数据格式，与后端返回的 `OperatorFlowDto` 序列化后的格式完全匹配。

请检查 `flowCanvas.js` 中 `deserialize(data)` 的实现，确认：
- `data.operators` 数组中每个算子的字段名（`id`, `type`, `name`, `x`, `y`, `parameters` 等）
- `data.connections` 数组中每个连线的字段名（`sourceOperatorId`, `sourcePortIndex`, `targetOperatorId`, `targetPortIndex` 等）

如果后端 DTO 序列化为驼峰命名（`camelCase`），而前端 `deserialize` 期望其他格式，需在后端序列化时统一使用 `JsonNamingPolicy.CamelCase`。

### 阶段七验收标准
- [ ] 工具栏出现"AI 生成"按钮，点击弹出对话框
- [ ] Ctrl+G 快捷键能打开对话框
- [ ] 示例快捷按钮能填充输入框
- [ ] 生成中显示 loading 状态
- [ ] 生成成功后工作流渲染到画布
- [ ] 生成失败显示清晰的错误信息

---

## 阶段八：集成测试与问题修复（约 0.5 天）

### 目标
端到端测试完整流程，修复发现的问题。

### 8.1 测试场景清单

对每个场景执行完整的端到端测试：

| 编号 | 用户输入 | 期望结果 |
|------|----------|----------|
| T01 | "用相机检测产品表面缺陷" | 生成含 ImageAcquisition、预处理、BlobAnalysis、ResultOutput 的流程 |
| T02 | "扫描二维码发给PLC" | 生成含 ImageAcquisition、BarcodeRecognition、ModbusCommunication 的流程 |
| T03 | "测量两个圆孔的距离" | 生成含 ImageAcquisition、圆测量×2、Measurement、ResultOutput 的流程，圆测量间不互连 |
| T04 | "故意输入无意义描述：abcdefg" | 生成失败或给出最接近的方案，错误信息清晰 |
| T05 | 超长描述（200字以上）| 正确处理，不报错 |
| T06 | 同时点击两次生成按钮 | 第二次点击无效（按钮已禁用） |
| T07 | 生成过程中关闭对话框 | 不允许关闭（等待完成） |
| T08 | 网络断开状态下尝试生成 | 显示网络错误信息 |

### 8.2 常见问题及解决方案

**问题 1：AI 输出的 JSON 中包含算子目录没有的算子类型**
- 原因：AI 可能"幻觉"出不存在的算子类型
- 解决：校验器已处理，会触发重试，重试消息中会明确指出错误的算子类型
- 如仍然频繁出现，在 Prompt 中增加更强调的指令：`"严禁使用算子目录中不存在的 operator_id"`

**问题 2：坐标布局重叠**
- 原因：并行分支的算子在同一层，`NodeHeight` 不够
- 解决：增大 `AutoLayoutService` 中的 `NodeHeight` 常量

**问题 3：`canvas.deserialize()` 格式不匹配**
- 原因：后端 DTO 的属性名与前端期望的字段名不一致（如大小写、命名风格）
- 解决：在后端的消息序列化时，添加字段映射或使用 `JsonPropertyName` 特性

**问题 4：AI 响应超时**
- 原因：AI API 响应超过了 `TimeoutSeconds` 配置
- 解决：将 `TimeoutSeconds` 从 60 增加到 90，并在前端对话框添加更长时间的等待提示

**问题 5：重试后仍然失败**
- 原因：校验错误信息不够具体，AI 无法理解如何修改
- 解决：在 `BuildRetryMessage` 中提供更具体的提示，例如把可用端口名也列出来

### 8.3 日志排查

启用详细日志时，以下日志条目有助于排查问题：

```
[AiFlowGenerationService] 调用 AI API，第 1 次尝试
[AiApiClient] 发送请求到 https://api.anthropic.com/v1/messages
[AiFlowGenerationService] AI 原始响应：{...JSON...}
[AiFlowValidator] 校验通过 / 校验失败，错误：{...}
[AutoLayoutService] 布局完成，共 5 层，算子坐标已分配
[GenerateFlowMessageHandler] 返回结果给前端，成功=True
```

---

## 阶段九：体验优化（可选，约 1 天）

### 9.1 在前端支持流式生成进度反馈

在后端 `GenerateFlowMessageHandler` 中，在关键步骤向前端推送进度消息：

```csharp
// 调用 AI 之前
await SendProgressAsync("calling_ai", "正在向 AI 发送请求...");

// AI 返回后，开始校验之前
await SendProgressAsync("validating", "AI 已响应，正在校验工作流...");

// 布局计算前
await SendProgressAsync("layouting", "正在计算算子布局...");
```

### 9.2 为需要用户确认的节点添加视觉标记

在 `flowCanvas.js` 中实现 `highlightNodes(nodeIds, type)` 方法（如果还未实现）：

```javascript
// 在 FlowCanvas 类中添加
highlightNodes(nodeIds, highlightType) {
    nodeIds.forEach(nodeId => {
        const node = this.nodes.get(nodeId);
        if (node) {
            node.highlight = highlightType; // 记录高亮类型
        }
    });
    this.render(); // 重新渲染画布

    // 在绘制节点时，检查 node.highlight 并绘制对应的视觉标记
    // （在 drawNode() 方法中添加橙色边框或警告图标）
}
```

### 9.3 生成历史记录

在前端维护最近 5 次的生成描述历史，允许用户快速重用：

```javascript
// 在 AiGenerationDialog 中
_saveToHistory(description) {
    const history = JSON.parse(sessionStorage.getItem('ai-gen-history') || '[]');
    history.unshift(description);
    sessionStorage.setItem('ai-gen-history', JSON.stringify(history.slice(0, 5)));
}
```

### 9.4 API Key 管理界面

在设置页面（如果有）添加 AI 配置项，允许用户在 UI 中输入 API Key，而不需要手动修改配置文件：

```javascript
// 前端设置界面
// 保存时发送消息给后端更新 AiGenerationOptions.ApiKey
```

---

## 附录 A：文件清单

以下是本次功能涉及的所有新增/修改文件：

### 新增文件

| 文件路径 | 说明 |
|----------|------|
| `Acme.Product.Infrastructure/AI/AiGenerationOptions.cs` | 配置类 |
| `Acme.Product.Infrastructure/AI/PromptBuilder.cs` | Prompt 构建器 |
| `Acme.Product.Infrastructure/AI/AiApiClient.cs` | AI API 客户端 |
| `Acme.Product.Infrastructure/AI/AiFlowValidator.cs` | 校验器 |
| `Acme.Product.Infrastructure/AI/AutoLayoutService.cs` | 自动布局 |
| `Acme.Product.Infrastructure/AI/AiFlowGenerationService.cs` | 核心生成服务 |
| `Acme.Product.Infrastructure/AI/AiGenerationServiceExtensions.cs` | DI 注册扩展 |
| `Acme.Product.Infrastructure/AI/GenerateFlowMessageHandler.cs` | 消息处理器 |
| `Acme.Product.Application/DTOs/AiGenerationDto.cs` | 请求/响应 DTO |
| `Acme.Product.Core/Services/IAiFlowGenerationService.cs` | 服务接口 |
| `Acme.Product.Core/Services/ValidationResult.cs` | 校验结果类 |
| `Acme.Product.Contracts/Messages/AiGenerationMessages.cs` | 消息契约 |
| `Acme.Product.Desktop/wwwroot/src/features/ai-generation/aiGenerationDialog.js` | 前端对话框组件 |

### 修改文件

| 文件路径 | 修改内容 |
|----------|----------|
| `Acme.Product.Core/Services/IOperatorFactory.cs` | 新增 `GetAllMetadata()` 方法声明 |
| `Acme.Product.Infrastructure/Services/OperatorFactory.cs` | 实现 `GetAllMetadata()` |
| `Acme.Product.Desktop/appsettings.json` | 新增 `AiFlowGeneration` 配置节 |
| `Acme.Product.Desktop/Program.cs` | 注册 AI 生成服务 `AddAiFlowGeneration()` |
| 现有消息路由文件 | 注册 `GenerateFlow` 消息处理器 |
| 前端工具栏文件 | 添加"AI 生成"按钮 |
| 前端主入口文件 | 初始化 `AiGenerationDialog` |

---

## 附录 B：appsettings.json 完整配置示例

```json
{
  "AiFlowGeneration": {
    "Provider": "Anthropic",
    "ApiKey": "sk-ant-api03-你的密钥...",
    "Model": "claude-opus-4-6",
    "MaxRetries": 2,
    "TimeoutSeconds": 60,
    "MaxTokens": 4096,
    "BaseUrl": null
  }
}
```

**配置示例（Ollama 本地模型）**：
```json
{
  "AiFlowGeneration": {
    "Provider": "OpenAI",
    "ApiKey": "ollama",
    "Model": "qwen2.5:32b",
    "BaseUrl": "http://localhost:11434/v1/chat/completions"
  }
}
```

**安全提示**：生产环境中请勿将 API Key 直接写入代码或提交到版本控制。建议使用 Windows 的 `DPAPI` 加密存储或通过环境变量注入。

---

## 附录 C：算子类型名称对照表

AI 在生成时必须使用下方 `operatorType` 字段中的精确字符串（区分大小写）：

> 以下为当前已注册的全部算子快照。实际运行时以 `OperatorFactory.GetAllMetadata()` 返回结果为准。

| 类别 | operatorType（枚举名） | 中文名 |
|------|----------------------|--------|
| 采集 | `ImageAcquisition` | 图像采集 |
| 预处理 | `Filtering` | 滤波 |
| 预处理 | `GaussianBlur` | 高斯滤波 |
| 预处理 | `MedianBlur` | 中值滤波 |
| 预处理 | `BilateralFilter` | 双边滤波 |
| 预处理 | `Thresholding` | 二值化 |
| 预处理 | `AdaptiveThreshold` | 自适应阈值 |
| 预处理 | `ColorConversion` | 颜色空间转换 |
| 预处理 | `HistogramEqualization` | 直方图均衡化 |
| 预处理 | `ClaheEnhancement` | CLAHE增强 |
| 预处理 | `LaplacianSharpen` | 拉普拉斯锐化 |
| 预处理 | `Morphology` | 形态学操作 |
| 预处理 | `ImageResize` | 图像缩放 |
| 预处理 | `ImageCrop` | 图像裁剪 |
| 预处理 | `ImageRotate` | 图像旋转 |
| 预处理 | `PerspectiveTransform` | 透视变换 |
| 预处理 | `RoiManager` | ROI管理器 |
| 特征提取 | `EdgeDetection` | 边缘检测 |
| 特征提取 | `SubpixelEdgeDetection` | 亚像素边缘检测 |
| 特征提取 | `ContourDetection` | 轮廓检测 |
| 特征提取 | `BlobAnalysis` | Blob分析 |
| 匹配定位 | `TemplateMatching` | 模板匹配 |
| 匹配定位 | `ShapeMatching` | 形状匹配 |
| 匹配定位 | `OrbFeatureMatch` | ORB特征匹配 |
| 匹配定位 | `GradientShapeMatch` | 梯度形状匹配 |
| 匹配定位 | `PyramidShapeMatch` | 金字塔形状匹配 |
| AI检测 | `DeepLearning` | 深度学习推理 |
| AI检测 | `DualModalVoting` | 双模态投票 |
| 识别 | `CodeRecognition` | 条码识别 |
| 检测 | `Measurement` | 距离测量 |
| 检测 | `CircleMeasurement` | 圆测量 |
| 检测 | `LineMeasurement` | 直线测量 |
| 检测 | `AngleMeasurement` | 角度测量 |
| 检测 | `GeometricTolerance` | 几何公差 |
| 检测 | `GeometricFitting` | 几何拟合 |
| 检测 | `ColorDetection` | 颜色检测 |
| 检测 | `ContourMeasurement` | 轮廓测量 |
| 标定 | `CameraCalibration` | 相机标定 |
| 标定 | `Undistort` | 畸变校正 |
| 标定 | `CoordinateTransform` | 坐标转换 |
| 图像运算 | `ImageAdd` | 图像加法 |
| 图像运算 | `ImageSubtract` | 图像减法 |
| 图像运算 | `ImageBlend` | 图像融合 |
| 控制 | `ConditionalBranch` | 条件分支 |
| 控制 | `TryCatch` | 异常捕获 |
| 控制 | `VariableRead` | 变量读取 |
| 控制 | `VariableWrite` | 变量写入 |
| 控制 | `VariableIncrement` | 变量递增 |
| 控制 | `CycleCounter` | 循环计数器 |
| 输出 | `ResultOutput` | 结果输出 |
| 输出 | `ResultJudgment` | 结果判定 |
| 通信 | `ModbusCommunication` | Modbus通信 |
| 通信 | `TcpCommunication` | TCP通信 |
| 通信 | `SerialCommunication` | 串口通信 |
| 通信 | `DatabaseWrite` | 数据库写入 |
| 通信 | `SiemensS7Communication` | 西门子S7通信 |
| 通信 | `MitsubishiMcCommunication` | 三菱MC通信 |
| 通信 | `OmronFinsCommunication` | 欧姆龙Fins通信 |

---

## 附录 D：已知限制

1. **参数自动推断**：AI 只能推断可以从用户描述中明确得到的参数（如采集源类型、触发模式），对于需要实测的参数（如 `MinRadius`、`Threshold`）只能给出合理默认值，需用户手动调整。

2. **复杂控制流**：对于含条件分支的复杂流程，AI 的生成准确率低于简单线性流程，建议提供更详细的描述或使用模板系统。

3. **上下文长度**：当算子库继续扩展（当前 61+ 个算子），System Prompt 长度会持续增加。当超过 100 个算子时，可考虑按类别分组，只传送与用户描述相关的算子子集以控制成本。

4. **网络依赖**：该功能依赖网络访问 AI API，离线环境无法使用。如需离线支持，可通过配置 `BaseUrl` 连接本地 Ollama 服务（使用 OpenAI 兼容协议）：
   ```json
   {
     "AiFlowGeneration": {
       "Provider": "OpenAI",
       "BaseUrl": "http://localhost:11434/v1/chat/completions",
       "Model": "qwen2.5:32b",
       "ApiKey": "ollama"
     }
   }
   ```

5. **国内 API 支持**：通过 `BaseUrl` 配置可兼容所有 OpenAI 协议兼容的服务商（如 DeepSeek、通义千问、智谱 GLM 等），无需修改代码。

---

*文档版本：1.1 | 更新日期：2026-02-19 | 适用项目：ClearVision*
