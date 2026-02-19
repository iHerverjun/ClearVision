# ClearVision Sprint 6 开发路线图

> **文档版本**: V1.0
> **制定日期**: 2026-02-19
> **基于**: ClearVision 路线图 V4 Sprint 5 完成版

---

## Sprint 6 目标

完成 LLM 连接器生态和 AI 编排的高级功能，使 ClearVision 能够接入多种大语言模型（云端和本地），并提供提示词版本管理和 AI 生成流程的版本控制。

---

## 总览

```
Sprint 6: LLM 生态与 AI 编排增强
[LLM 连接器实现]        [提示词版本管理]        [AI 流程版本控制]
     (1 周)                 (3-4 天)                (2-3 天)
```

---

## Task 6.1 — OpenAI LLM 连接器

**优先级**: P0
**预估工时**: 2-3 天
**相关文件**: `OpenAiConnector.cs`

### 功能规格

```csharp
// 支持 OpenAI GPT-4/GPT-3.5 API
public class OpenAiConnector : ILLMConnector
{
    // 配置参数
    - ApiKey: string (必需)
    - Model: string (默认 "gpt-4")
    - BaseUrl: string (默认 "https://api.openai.com/v1")
    - Temperature: float (默认 0.1)
    - MaxTokens: int (默认 4000)
    - Timeout: TimeSpan (默认 60s)
    
    // 功能
    - 流式响应支持
    - 重试机制 (指数退避)
    - Token 使用量统计
    - 错误处理和降级
}
```

### API 调用示例

```http
POST https://api.openai.com/v1/chat/completions
Content-Type: application/json
Authorization: Bearer {api_key}

{
  "model": "gpt-4",
  "messages": [
    {"role": "system", "content": "You are a ClearVision flow generation assistant..."},
    {"role": "user", "content": "{user_prompt}"}
  ],
  "temperature": 0.1,
  "max_tokens": 4000,
  "response_format": {"type": "json_object"}
}
```

---

## Task 6.2 — Azure OpenAI LLM 连接器

**优先级**: P0
**预估工时**: 2 天
**相关文件**: `AzureOpenAiConnector.cs`

### 功能规格

```csharp
// 支持 Azure OpenAI Service
public class AzureOpenAiConnector : ILLMConnector
{
    // 配置参数
    - Endpoint: string (必需, 如 "https://{resource}.openai.azure.com")
    - ApiKey: string (必需)
    - DeploymentName: string (必需, 如 "gpt-4")
    - ApiVersion: string (默认 "2024-02-15-preview")
    - Temperature: float (默认 0.1)
    - MaxTokens: int (默认 4000)
    
    // 功能
    - Entra ID (AAD) 认证支持
    - 托管身份支持
    - 企业级错误处理
}
```

### API 调用示例

```http
POST https://{resource}.openai.azure.com/openai/deployments/{deployment}/chat/completions?api-version=2024-02-15-preview
Content-Type: application/json
api-key: {api_key}

{
  "messages": [
    {"role": "system", "content": "You are a ClearVision flow generation assistant..."},
    {"role": "user", "content": "{user_prompt}"}
  ],
  "temperature": 0.1,
  "max_tokens": 4000,
  "response_format": {"type": "json_object"}
}
```

---

## Task 6.3 — 本地模型 LLM 连接器 (Ollama)

**优先级**: P1
**预估工时**: 2 天
**相关文件**: `OllamaConnector.cs`

### 功能规格

```csharp
// 支持 Ollama 本地部署的大模型
public class OllamaConnector : ILLMConnector
{
    // 配置参数
    - BaseUrl: string (默认 "http://localhost:11434")
    - Model: string (必需, 如 "llama2", "codellama", "mistral")
    - Temperature: float (默认 0.1)
    - ContextWindow: int (默认 4096)
    
    // 功能
    - 本地模型列表查询
    - 模型拉取 (pull)
    - 流式响应
    - 无需 API Key (内网部署)
}
```

### API 调用示例

```http
POST http://localhost:11434/api/generate
Content-Type: application/json

{
  "model": "codellama",
  "prompt": "{system_prompt}\n\n{user_prompt}",
  "stream": false,
  "options": {
    "temperature": 0.1
  }
}
```

---

## Task 6.4 — LLM 连接器工厂和配置管理

**优先级**: P0
**预估工时**: 2 天
**相关文件**: `LLMConnectorFactory.cs`, `LLMConfiguration.cs`

### 功能规格

```csharp
// 连接器类型枚举
public enum LLMProviderType
{
    OpenAI,
    AzureOpenAI,
    Ollama,
    // 可扩展: Anthropic, Google, etc.
}

// 连接器工厂
public class LLMConnectorFactory
{
    ILLMConnector Create(LLMConfiguration config);
    ILLMConnector Create(LLMProviderType type, Dictionary<string, string> settings);
}

// 配置管理
public class LLMConfiguration
{
    LLMProviderType Provider { get; set; }
    Dictionary<string, string> Settings { get; set; }
    bool IsEnabled { get; set; }
    int Priority { get; set; } // 故障转移优先级
}

// 配置存储和加载
public interface ILLMConfigurationStore
{
    Task<LLMConfiguration> LoadAsync(string profileName);
    Task SaveAsync(string profileName, LLMConfiguration config);
    Task<List<string>> ListProfilesAsync();
}
```

### 配置示例 (JSON)

```json
{
  "profiles": [
    {
      "name": "production",
      "provider": "AzureOpenAI",
      "settings": {
        "endpoint": "https://cv-llm-prod.openai.azure.com",
        "deploymentName": "gpt-4",
        "temperature": "0.1"
      },
      "priority": 1
    },
    {
      "name": "fallback",
      "provider": "OpenAI",
      "settings": {
        "model": "gpt-4",
        "temperature": "0.1"
      },
      "priority": 2
    },
    {
      "name": "local",
      "provider": "Ollama",
      "settings": {
        "model": "codellama",
        "baseUrl": "http://localhost:11434"
      },
      "priority": 3
    }
  ]
}
```

---

## Task 6.5 — 提示词版本管理

**优先级**: P1
**预估工时**: 2-3 天
**相关文件**: `PromptVersionManager.cs`, `PromptVersion.cs`

### 功能规格

```csharp
// 提示词版本
public class PromptVersion
{
    Guid Id { get; set; }
    string Name { get; set; }           // 版本名称 (如 "v1.0", "v2.0-improved")
    string Description { get; set; }    // 版本描述
    string Content { get; set; }        // 提示词内容
    DateTime CreatedAt { get; set; }
    string CreatedBy { get; set; }
    bool IsActive { get; set; }         // 是否当前激活
    Dictionary<string, int> Metrics { get; set; }  // 成功率、平均 Token 等
}

// 提示词版本管理器
public class PromptVersionManager
{
    // CRUD 操作
    Task<PromptVersion> CreateVersionAsync(string name, string content, string description);
    Task<PromptVersion> GetVersionAsync(Guid id);
    Task<List<PromptVersion>> ListVersionsAsync();
    Task ActivateVersionAsync(Guid id);
    Task DeleteVersionAsync(Guid id);
    
    // A/B 测试支持
    Task SetABTestAsync(Guid versionA, Guid versionB, double splitRatio);
    Task<ABTestResult> GetABTestResultAsync(Guid testId);
    
    // 获取当前激活的提示词
    Task<string> GetActivePromptAsync();
    
    // 记录使用指标
    Task RecordMetricsAsync(Guid versionId, PromptMetrics metrics);
}

// 提示词使用指标
public class PromptMetrics
{
    bool Success { get; set; }
    int TokenUsage { get; set; }
    long LatencyMs { get; set; }
    string ErrorMessage { get; set; }
}
```

### 数据存储

```json
{
  "activeVersionId": "guid-here",
  "versions": [
    {
      "id": "guid-1",
      "name": "v1.0-baseline",
      "description": "初始版本",
      "content": "...",
      "createdAt": "2026-02-19T10:00:00Z",
      "metrics": {
        "totalCalls": 1250,
        "successRate": 0.92,
        "avgTokens": 2850
      }
    },
    {
      "id": "guid-2",
      "name": "v2.0-improved",
      "description": "优化了 ForEach 模式说明",
      "content": "...",
      "createdAt": "2026-02-20T10:00:00Z",
      "metrics": {
        "totalCalls": 580,
        "successRate": 0.96,
        "avgTokens": 2600
      }
    }
  ]
}
```

---

## Task 6.6 — AI 生成流程的版本控制

**优先级**: P1
**预估工时**: 2-3 天
**相关文件**: `AIGeneratedFlowVersionManager.cs`

### 功能规格

```csharp
// AI 生成流程版本
public class AIGeneratedFlowVersion
{
    Guid Id { get; set; }
    Guid FlowId { get; set; }
    string FlowName { get; set; }
    int VersionNumber { get; set; }     // 版本号 (1, 2, 3...)
    string VersionName { get; set; }    // 自定义名称
    string Description { get; set; }
    string UserRequirement { get; set; } // 原始用户需求
    OperatorFlow Flow { get; set; }     // 流程定义
    PromptVersionInfo UsedPrompt { get; set; }  // 使用的提示词版本
    LLMProviderType UsedProvider { get; set; }  // 使用的 LLM 提供商
    DateTime GeneratedAt { get; set; }
    string GeneratedBy { get; set; }
    WorkflowTelemetry Telemetry { get; set; }   // 生成时的遥测数据
    DryRunResult? ValidationResult { get; set; } // 验证结果
    bool IsDeployed { get; set; }       // 是否已部署
    DateTime? DeployedAt { get; set; }
}

// 流程版本管理器
public class AIGeneratedFlowVersionManager
{
    // 保存新版本
    Task<AIGeneratedFlowVersion> SaveVersionAsync(
        OperatorFlow flow, 
        string userRequirement,
        PromptVersionInfo promptInfo,
        LLMProviderType provider,
        WorkflowTelemetry telemetry);
    
    // 获取版本
    Task<AIGeneratedFlowVersion> GetVersionAsync(Guid versionId);
    Task<List<AIGeneratedFlowVersion>> GetFlowHistoryAsync(Guid flowId);
    
    // 版本比较
    Task<FlowDiff> CompareVersionsAsync(Guid versionA, Guid versionB);
    
    // 回滚
    Task<AIGeneratedFlowVersion> RollbackAsync(Guid flowId, int targetVersion);
    
    // 标记部署状态
    Task MarkAsDeployedAsync(Guid versionId);
    
    // 删除旧版本 (保留策略)
    Task CleanupOldVersionsAsync(Guid flowId, int keepCount);
}

// 流程差异
public class FlowDiff
{
    List<OperatorDiff> AddedOperators { get; set; }
    List<OperatorDiff> RemovedOperators { get; set; }
    List<OperatorDiff> ModifiedOperators { get; set; }
    List<ConnectionDiff> ConnectionChanges { get; set; }
}
```

---

## 优先级与工时汇总

| 编号 | 任务 | 优先级 | 内容 | 工时 |
|------|------|--------|------|------|
| 6.1 | LLM 连接器 | 🔴 P0 | OpenAI 连接器 | 2-3 天 |
| 6.2 | LLM 连接器 | 🔴 P0 | Azure OpenAI 连接器 | 2 天 |
| 6.3 | LLM 连接器 | 🟠 P1 | Ollama 本地模型连接器 | 2 天 |
| 6.4 | 配置管理 | 🔴 P0 | 连接器工厂 + 配置管理 | 2 天 |
| 6.5 | 版本管理 | 🟠 P1 | 提示词版本管理 | 2-3 天 |
| 6.6 | 版本控制 | 🟠 P1 | AI 流程版本控制 | 2-3 天 |
| 6.7 | DI 注册 | 🟡 P2 | DependencyInjection 更新 | 0.5 天 |
| 6.8 | 测试 | 🟡 P2 | 单元测试 | 1-2 天 |
| 6.9 | 文档 | 🟢 P3 | 验收清单 | 0.5 天 |

**总计预估工时**: 14-18 天（约 3-4 工作周）

---

## 验收标准

### Task 6.1-6.3: LLM 连接器

- [ ] OpenAI 连接器能成功调用 GPT-4 API 并返回 JSON 格式响应
- [ ] Azure OpenAI 连接器支持 API Key 和 Entra ID 认证
- [ ] Ollama 连接器能连接本地模型并生成响应
- [ ] 所有连接器都实现 ILLMConnector 接口
- [ ] 支持重试机制和错误处理
- [ ] Token 使用量正确统计

### Task 6.4: 连接器工厂

- [ ] 工厂能根据配置创建正确的连接器实例
- [ ] 支持配置文件加载和保存
- [ ] 支持多配置文件切换
- [ ] 支持故障转移优先级配置

### Task 6.5: 提示词版本管理

- [ ] 能创建、激活、删除提示词版本
- [ ] 能获取当前激活的提示词
- [ ] 能记录和查看使用指标
- [ ] 支持 A/B 测试配置 (可选)

### Task 6.6: 流程版本控制

- [ ] 能保存 AI 生成流程的版本
- [ ] 能查看流程历史版本
- [ ] 能比较两个版本的差异
- [ ] 能回滚到指定版本
- [ ] 支持版本保留策略

### Task 6.7-6.9: 基础设施

- [ ] 所有服务正确注册到 DI
- [ ] 单元测试覆盖率 ≥ 80%
- [ ] 验收清单文档完整

---

*文档维护：ClearVision 开发团队*
*基于：ClearVision 路线图 V4 Sprint 5 完成版*
