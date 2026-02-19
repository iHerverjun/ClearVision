# Sprint 5 验收检查清单

## Sprint 5: AI 编排接入

### 目标
完成 AI 编排接入层，提供从自然语言到可执行流程的端到端能力。

---

## Task 5.1: AIPromptBuilder ✅

### 实现检查
- [x] AIPromptBuilder.cs 文件存在于 `src/Acme.Product.Infrastructure/AI/`
- [x] 实现 `WithSystemPrompt()` 方法 - 添加系统提示词头
- [x] 实现 `WithOperatorLibrary()` 方法 - 列出所有可用算子
- [x] 实现 `WithDesignRules()` 方法 - 包含 DAG、通信保护、ForEach 模式规则
- [x] 实现 `WithExamples()` 方法 - 提供多目标检测 MES 上报示例
- [x] 实现 `WithUserRequirement()` 方法 - 添加用户需求
- [x] 实现 `WithOutputRequirements()` 方法 - 指定输出格式
- [x] 实现 `Build()` 方法 - 生成完整提示词
- [x] 实现 `CreateFullPrompt()` 静态方法 - 便捷构建完整提示词

### 代码质量
- [x] 使用中文注释
- [x] 包含作者信息（蘅芜君）
- [x] 算子元数据包含类型、名称、描述、参数信息
- [x] 设计规则包含 SAFETY_001/002 相关内容

### 功能验证
```csharp
// 测试通过：AIPromptBuilder 能生成包含以下内容的提示词
- ClearVision 平台标识
- 算子库（图像采集、深度学习检测、ForEach 等）
- 设计规则（DAG、通信算子保护、ForEach 模式选择）
- 输出格式规范（JSON 结构）
```

---

## Task 5.2: AIGeneratedFlowParser ✅

### 实现检查
- [x] AIGeneratedFlowParser.cs 文件存在于 `src/Acme.Product.Infrastructure/AI/`
- [x] 实现 `Parse()` 方法 - 将 AI 生成的 JSON 解析为 OperatorFlow
- [x] 支持解析算子列表（类型、名称、参数、端口）
- [x] 支持解析连接关系
- [x] 自动调用 Linter 进行验证
- [x] 实现 `ParseResult` 返回类
- [x] 定义 `AIGeneratedFlow`、`AIOperator`、`AIParameter` 等 DTO 类

### 解析能力
- [x] 支持解析标准算子类型（ImageAcquisition、DeepLearning、ForEach 等）
- [x] 支持解析端口类型（Image、Float、Boolean、DetectionList 等）
- [x] 支持解析参数（包含名称和值）
- [x] 支持解析端口连接（source/target operator 和 port ID）

### 错误处理
- [x] JSON 解析失败返回友好错误信息
- [x] 未知算子类型返回错误
- [x] 引用不存在的算子 ID 返回错误
- [x] Linter 检查失败返回详细问题列表

### 单元测试
```csharp
✅ AIGeneratedFlowParserTests 包含以下测试：
- Parse_ValidJson_ShouldReturnSuccess
- Parse_InvalidJson_ShouldReturnFailure
- Parse_UnknownOperatorType_ShouldReturnFailure
- Parse_FullFlow_ShouldMapPortTypes
```

---

## Task 5.3: StubRegistryBuilder ✅

### 实现检查
- [x] StubRegistryBuilder.cs 文件存在于 `src/Acme.Product.Infrastructure/AI/DryRun/`
- [x] 实现所有算子的默认 Stub 定义
- [x] 支持根据算子元数据生成 Stub
- [x] 支持参数化 Stub（MathOperation、LogicGate 等）
- [x] 实现 `BuildForFlow()` 方法 - 为指定流程构建 Stub 注册表

### Stub 覆盖
- [x] 图像处理类：ImageAcquisition、GaussianBlur、EdgeDetection、Thresholding
- [x] 深度学习：DeepLearning（返回 DetectionList）
- [x] 几何测量：CircleMeasurement、LineMeasurement
- [x] 数据操作：ForEach、ArrayIndexer、JsonExtractor
- [x] 数值计算：MathOperation（Add/Subtract/Multiply/Divide/Abs/Min/Max/Power/Sqrt/Round/Modulo）
- [x] 逻辑门：LogicGate（AND/OR/NOT/XOR/NAND/NOR）
- [x] 类型转换：TypeConvert
- [x] 通信类：HttpRequest、MqttPublish、Modbus、S7（返回模拟响应）
- [x] 字符串处理：StringFormat
- [x] 流程控制：ConditionalBranch、ResultJudgment

### 单元测试
```csharp
✅ StubRegistryBuilderTests 包含以下测试：
- ShouldContainStubsForAllOperators
- ExecuteMathOperationStub_ShouldReturnCalculatedValue
- ExecuteLogicGateStub_ShouldReturnBooleanValue
- BuildForFlow_ShouldReturnConfiguredRegistry
```

---

## Task 5.4: AIWorkflowService ✅

### 实现检查
- [x] AIWorkflowService.cs 文件存在于 `src/Acme.Product.Infrastructure/AI/`
- [x] 实现 `GenerateFlowAsync()` 方法 - 端到端工作流
- [x] 实现 `ValidateFlow()` 方法 - 流程验证（无需 LLM）
- [x] 集成 PromptBuilder、FlowParser、Linter、DryRunService
- [x] 实现遥测数据收集（各阶段耗时、Token 使用量）
- [x] 实现工作流选项配置（StrictMode、EnableDryRun）

### 工作流步骤
- [x] Step 1: 构建提示词（AIPromptBuilder）
- [x] Step 2: 调用 LLM（ILLMConnector 接口）
- [x] Step 3: 解析生成的流程（AIGeneratedFlowParser）
- [x] Step 4: Linter 验证（FlowLinter）
- [x] Step 5: 双向 Dry-Run（DryRunService + StubRegistry）

### 返回结果
- [x] AIWorkflowResult 包含：
  - 成功/失败状态
  - 生成的流程（OperatorFlow）
  - 错误信息
  - 遥测数据（各阶段耗时）
  - 解析警告
  - Linter 警告
  - DryRun 结果

### 接口定义
- [x] 定义 `ILLMConnector` 接口
- [x] 定义 `LLMResponse` 类
- [x] 定义 `ILogger<T>` 接口（简化版）
- [x] 定义 `AIWorkflowOptions` 配置类
- [x] 定义 `WorkflowTelemetry` 遥测类

---

## Task 5.5: DependencyInjection 注册 ✅

### 注册检查
- [x] 在 DependencyInjection.cs 中注册 Sprint 5 服务：
```csharp
services.AddSingleton<AIPromptBuilder>();
services.AddScoped<AIGeneratedFlowParser>();
services.AddSingleton<StubRegistryBuilder>();
services.AddScoped<AIWorkflowService>();
```

---

## Task 5.6: 单元测试 ✅

### 测试覆盖
- [x] AIPromptBuilder 测试（4 个测试用例）
- [x] AIGeneratedFlowParser 测试（4 个测试用例）
- [x] StubRegistryBuilder 测试（4 个测试用例）
- [x] AIWorkflowService 集成测试（2 个测试用例）

### 测试文件
- [x] `tests/Acme.Product.Tests/AI/Sprint5_AIWorkflowServiceTests.cs`

---

## 端到端验证场景

### 场景 1：简单缺陷检测流程
```
用户需求："采集图像，使用 YOLO 检测缺陷"
预期输出：
- ImageAcquisition -> DeepLearning 的简单流程
- 包含正确的端口连接
- 通过 Linter 验证
```

### 场景 2：多目标检测 + MES 上报
```
用户需求："检测多个目标，将结果逐条上报 MES"
预期输出：
- ImageAcquisition -> DeepLearning -> ForEach -> HttpRequest
- ForEach.IoMode = Sequential（含通信算子）
- 通信算子上游有保护
```

### 场景 3：验证失败场景
```
用户需求："创建一个循环依赖的流程"
预期输出：
- Linter 检测到 STRUCT_003 错误（环路）
- 返回失败结果和错误信息
```

---

## Sprint 5 完成总结

### 已完成工作 ✅

1. **AIPromptBuilder** - 完整的提示词构建器
   - 支持系统提示、算子库、设计规则、示例、用户需求、输出要求
   - 包含 20+ 个算子的元数据

2. **AIGeneratedFlowParser** - AI 生成流程解析器
   - 将 LLM JSON 输出转换为内部 OperatorFlow
   - 自动 Linter 验证
   - 完善的错误处理

3. **StubRegistryBuilder** - Stub 注册表构建器
   - 支持 20+ 算子的默认 Stub
   - 参数化 Stub（MathOperation、LogicGate）
   - 可针对具体流程构建 Stub 注册表

4. **AIWorkflowService** - AI 编排服务
   - 端到端工作流（提示词→LLM→解析→验证→DryRun）
   - 遥测数据收集
   - 可配置选项

5. **依赖注入** - 所有服务已注册

6. **单元测试** - 14 个测试用例覆盖核心功能

### 技术亮点
- ✅ 完整的提示词工程（Prompt Engineering）
- ✅ 自然语言到结构化数据的转换管道
- ✅ 三层验证（解析验证、Linter 验证、DryRun 验证）
- ✅ 可扩展的 Stub 系统（支持双向仿真）
- ✅ 完善的遥测和日志

### 文件清单
```
src/Acme.Product.Infrastructure/AI/
├── AIPromptBuilder.cs              ✅
├── AIGeneratedFlowParser.cs        ✅
├── AIWorkflowService.cs            ✅
└── DryRun/
    ├── DryRunStubRegistry.cs       ✅ (Sprint 4)
    ├── DryRunService.cs            ✅ (Sprint 4)
    └── StubRegistryBuilder.cs      ✅

tests/Acme.Product.Tests/AI/
└── Sprint5_AIWorkflowServiceTests.cs  ✅
```

### 状态
**Sprint 5 完成度：100%**

---

## 下一个 Sprint（可选）

如需继续，Sprint 6 可考虑：
- AI 生成流程的 UI 集成（前端接收和展示 AI 生成的流程）
- LLM 连接器实现（OpenAI、Azure OpenAI、本地模型等）
- 提示词版本管理和 A/B 测试
- AI 生成流程的版本控制和回滚
