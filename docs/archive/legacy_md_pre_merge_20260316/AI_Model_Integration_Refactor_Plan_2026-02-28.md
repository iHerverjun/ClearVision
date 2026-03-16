# ClearVision AI 接入重构方案（2026-02-28）

## 1. 决策约束

本方案基于你当前确认的边界执行：

- **暂不处理**密钥下线/轮换/加密存储治理（设计期先保证调试效率）。
- 优先重构“**多平台模型接入能力**”与“**统一架构**”。
- 采用**增量迁移**，避免一次性大改导致生成功能停摆。

## 2. 当前状态摘要（代码现状）

### 2.1 线上主链路

当前真实生成链路是：

`WebMessageHandler -> GenerateFlowMessageHandler -> AiFlowGenerationService -> AiApiClient -> 各 Provider HTTP`

关键文件：

- `Acme.Product/src/Acme.Product.Desktop/Handlers/WebMessageHandler.cs`
- `Acme.Product/src/Acme.Product.Infrastructure/AI/GenerateFlowMessageHandler.cs`
- `Acme.Product/src/Acme.Product.Infrastructure/AI/AiFlowGenerationService.cs`
- `Acme.Product/src/Acme.Product.Infrastructure/AI/AiApiClient.cs`

### 2.2 并存的第二套接入架构

项目里还存在一套 `ILLMConnector + Factory + Store` 架构（OpenAI/Azure/Ollama），但不在主链路中承担生成流量。

关键文件：

- `Acme.Product/src/Acme.Product.Infrastructure/AI/Connectors/LLMConfiguration.cs`
- `Acme.Product/src/Acme.Product.Infrastructure/AI/Connectors/LLMConnectorFactory.cs`
- `Acme.Product/src/Acme.Product.Infrastructure/AI/Connectors/DynamicLLMConnector.cs`
- `Acme.Product/src/Acme.Product.Infrastructure/AI/Connectors/OpenAiConnector.cs`
- `Acme.Product/src/Acme.Product.Infrastructure/AI/Connectors/AzureOpenAiConnector.cs`
- `Acme.Product/src/Acme.Product.Infrastructure/AI/Connectors/OllamaConnector.cs`

### 2.3 主要问题

- 双轨并存，维护成本高，行为不一致风险高。
- Provider 能力靠字符串启发式，缺少显式 capability 声明。
- 模型配置表达力不足（缺 headers/query/body 扩展与认证模式声明）。
- 测试连接逻辑对 keyless 本地模型不友好。

## 3. 目标架构

目标是收敛为一条统一数据面：

`GenerateFlow -> AiGenerationOrchestrator -> IAiConnector -> Provider Adapter`

并保留配置控制面：

`Settings API/UI -> AiModelRegistry -> ai_models.json`

### 3.1 核心抽象（新增）

1. `IAiConnector`
- 统一聊天补全接口（普通 + 流式）。
- 输入为结构化消息（文本 + 图片），输出统一 `AiCompletionResult`。

2. `IAiConnectorFactory`
- 按模型配置的 `Protocol`/`ProviderKind` 返回对应 connector。

3. `IAiModelRegistry`
- 统一模型读取、激活、更新、能力读取与选择。
- 兼容当前 `AiConfigStore` 的 JSON 持久化。

4. `IAiModelSelector`
- 按任务角色与能力选择模型。
- 支持主模型/小模型/推理模型路由。

5. `AiGenerationOrchestrator`
- 负责重试、降级、fallback、附件策略、遥测采集。
- `AiFlowGenerationService` 只做业务编排，不直接耦合 provider 细节。

### 3.2 模型配置标准化（增强）

在现有 `AiModelConfig` 基础上扩展：

- `Protocol`: `openai_compatible | anthropic | azure_openai | ollama_native`
- `AuthMode`: `bearer | header_key | none`
- `AuthHeaderName`: 如 `Authorization`、`x-api-key`
- `ChatPath`: 可配置补全路径（默认自动推导）
- `ExtraHeaders`: `Dictionary<string,string>`
- `ExtraQuery`: `Dictionary<string,string>`
- `ExtraBody`: `Dictionary<string,object>`
- `Capabilities`: 显式能力声明（见下节）
- `RoleBindings`: `generation | reasoning | fallback | validation`
- `Priority`: fallback 顺序

### 3.3 能力声明（Capability Matrix）

新增 `AiModelCapabilities`：

- `SupportsStreaming`
- `SupportsVisionInput`
- `SupportsReasoningStream`
- `SupportsJsonMode`
- `SupportsToolCall`
- `SupportsSystemPrompt`
- `MaxImageCount`
- `MaxImageBytes`

规则：

- 附件策略不再靠 `model.Contains("reasoner")`。
- 按 capability 进行前置拦截与降级。

## 4. 分阶段实施（可直接排期）

## 阶段 A：统一抽象与兼容适配（不改外部行为）

目标：先把主链路挂到统一接口，行为保持不变。

改动：

1. 新增抽象与 orchestrator 框架
- 新建目录：`Acme.Product/src/Acme.Product.Infrastructure/AI/Runtime/`
- 文件：
  - `IAiConnector.cs`
  - `IAiConnectorFactory.cs`
  - `IAiModelRegistry.cs`
  - `IAiModelSelector.cs`
  - `AiGenerationOrchestrator.cs`

2. 新增过渡适配器
- `AiApiClientAdapterConnector.cs`（内部调用当前 `AiApiClient`）。
- 作用：先把主流程挂上新抽象，不立即重写 provider 细节。

3. 调整服务注入
- `AiGenerationServiceExtensions.cs` 改为注入 orchestrator。
- `AiFlowGenerationService` 不再直接持有 `AiApiClient`。

完成定义（DoD）：

- 现有功能行为不变。
- 现有测试通过（尤其 AI 相关测试）。
- 主链路已经只依赖 `IAiConnector`。

预计工期：`3-4` 人天。

## 阶段 B：Provider 适配统一化（收敛双轨）

目标：把旧 `Connectors/*` 与 `AiApiClient` 逻辑统一到同一实现体系。

改动：

1. Provider Adapter 拆分
- `OpenAiCompatibleConnector`（覆盖 OpenAI/OpenAI 兼容厂商）。
- `AnthropicConnector`
- `AzureOpenAiConnectorV2`（可复用现有类的 HTTP 结构）
- `OllamaNativeConnector`（支持 keyless）

2. 工厂统一
- `AiConnectorFactory` 基于 `Protocol` 构建 connector。

3. 退役旧路径
- `AiApiClient` 标记为过渡层，逐步只保留工具函数或彻底下线。
- `AIWorkflowService`/`DynamicLLMConnector` 复用同一 connector 工厂，避免再分叉。

完成定义（DoD）：

- 主流程不再直接调用 `AiApiClient`。
- OpenAI/Anthropic/OpenAI-compatible/Ollama 通过统一 connector 跑通。
- 连接测试可覆盖 keyless 模型。

预计工期：`5-7` 人天。

## 阶段 C：能力驱动路由 + 降级策略

目标：让“模型支持什么”可声明、可路由、可降级。

改动：

1. 能力模型落地
- `AiModelCapabilities` 加入 `AiModelConfig`。
- 新增默认能力映射与回填逻辑。

2. 选择器落地
- `AiModelSelector` 按 `RoleBindings + Capabilities + Priority` 选择模型。

3. 生成策略升级
- `AiGenerationOrchestrator` 内实现：
  - 图片输入前置能力检查
  - `400/429/5xx` 分类处理
  - fallback 切换（主 -> 备）

完成定义（DoD）：

- 删除 `IsModelKnownNotToSupportImageInput` 这类硬编码判断。
- 可配置“主模型失败后自动切换备模型”。
- 前端能收到明确降级原因。

预计工期：`3-4` 人天。

## 阶段 D：设置页与 API 契约升级（保兼容）

目标：配置可表达复杂 provider 接入，不破坏现有配置。

改动：

1. 后端 API 升级
- 文件：`Acme.Product/src/Acme.Product.Desktop/Endpoints/SettingsEndpoints.cs`
- 新增/扩展字段透传：
  - `protocol`
  - `authMode`
  - `authHeaderName`
  - `extraHeaders`
  - `extraQuery`
  - `extraBody`
  - `capabilities`
  - `roleBindings`
  - `priority`

2. 前端设置页升级
- 文件：`Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js`
- 表单新增高级配置折叠区。
- Provider 下拉改为 `Protocol` 选项（保留旧值自动映射）。

3. 配置迁移
- `ai_models.json` 自动迁移旧字段：
  - `Provider=OpenAI Compatible` -> `protocol=openai_compatible`
  - `Provider=Anthropic Claude` -> `protocol=anthropic`
  - `Provider=OpenAI API` -> `protocol=openai_compatible`（默认官方 endpoint）

完成定义（DoD）：

- 老配置文件无需人工改动可启动。
- 新配置可表达非标准兼容 API。
- 设置页可以完成新增、编辑、测试、激活全流程。

预计工期：`3-4` 人天。

## 阶段 E：测试补全与清理

目标：确保重构后稳定性可回归，清理废弃代码。

测试新增：

- `ConnectorFactoryTests`
- `ModelSelectorTests`
- `CapabilityRoutingTests`
- `FallbackPolicyTests`
- `SettingsEndpointsAiConfigCompatibilityTests`

现有测试调整：

- `AiApiClientMultimodalTests` 迁移到统一 connector 测试套件。

代码清理：

- 删除或冻结不再使用的旧入口。
- 清理重复 DTO/重复 provider 判断逻辑。

预计工期：`2-3` 人天。

## 5. 关键兼容策略

### 5.1 运行时开关

新增配置：

```json
"AiFlowGeneration": {
  "UseUnifiedConnectorPipeline": true
}
```

用途：

- 灰度期间允许快速回退到旧链路。

### 5.2 JSON 向后兼容

`AiConfigStore` 读取时：

- 先读新字段。
- 无新字段时按旧 `Provider` 自动映射。
- 保存时写入新字段，并保留旧字段一版过渡期（可选）。

### 5.3 API 向后兼容

- `GET /api/ai/models` 保留当前字段。
- 新字段以 optional 方式返回，不要求前端一次性升级。

## 6. 验收标准

满足以下条件视为重构完成：

1. 主链路只有一套 connector 调用路径。
2. OpenAI、Anthropic、OpenAI-compatible、Ollama 全部在新链路可用。
3. 多模态支持走 capability 判断，不再靠模型名启发式。
4. 设置页可配置高级 provider 参数并成功连通测试。
5. 回归测试通过，且具备开关回退能力。

## 7. 风险与回滚

主要风险：

- 新旧链路并存阶段行为不一致。
- 配置迁移 bug 导致模型配置缺失。
- 前端高级表单与后端字段不一致。

缓解：

- 阶段 A 先挂适配器，避免行为突变。
- 全程保留 `UseUnifiedConnectorPipeline` 开关。
- 给 `ai_models.json` 迁移加单元测试和样本文件回放测试。

回滚策略：

- 出现生产问题时将 `UseUnifiedConnectorPipeline=false`。
- 保留旧链路代码直至 E 阶段完成并稳定一周后再删除。

## 8. 建议执行顺序（最小可行路径）

推荐按下面顺序启动：

1. 阶段 A（统一抽象 + 适配器接管）
2. 阶段 C（能力声明 + 降级策略）
3. 阶段 D（设置页/API 升级）
4. 阶段 B（Provider 适配彻底收敛）
5. 阶段 E（清理与封口）

这样可以先拿到“架构统一 + 能力治理”的核心收益，再处理代码整洁度。

## 9. 预估总工期

- 核心路径（A+C+D）：`9-12` 人天
- 完整收敛（A+B+C+D+E）：`16-22` 人天

## 10. 本方案不覆盖项（按你的决定）

以下内容已明确排除在本次重构之外：

- 明文密钥下线与轮换
- 密钥安全存储升级
- 凭据审计与合规体系

后续可以作为独立安全专题再做，不阻断当前接入架构重构。

