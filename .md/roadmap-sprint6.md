# ClearVision Sprint 6 开发路线图与计划

<!-- DOC_AUDIT_STATUS_START -->
## 文档审计状态（自动更新）
- 审计日期：2026-03-02
- 完成状态：未完成
- 任务统计：总计 54，已完成 0，未完成 54，待办关键词命中 0
- 判定依据：任务清单尚未开始勾选
<!-- DOC_AUDIT_STATUS_END -->


> **作者**: 蘅芜君
> **版本**: V1.0
> **创建日期**: 2026-02-19
> **最后更新**: 2026-02-21
> **文档编号**: roadmap-sprint6
> **状态**: 进行中
> **前置文档**: [roadmap-main.md](roadmap-main.md)（V4.3 核查版）

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


---

# 第二部分：Sprint 6 详细开发计划

<!-- DOC_AUDIT_STATUS_START -->
## 文档审计状态（自动更新）
- 审计日期：2026-03-02
- 完成状态：未完成
- 任务统计：总计 54，已完成 0，未完成 54，待办关键词命中 0
- 判定依据：任务清单尚未开始勾选
<!-- DOC_AUDIT_STATUS_END -->


> 以下内容来自原 `ClearVision_Sprint6_开发计划.md`，提供了 Sprint 6 的细化任务分解和具体实施指导。

# ClearVision Sprint 6 开发计划

<!-- DOC_AUDIT_STATUS_START -->
## 文档审计状态（自动更新）
- 审计日期：2026-03-02
- 完成状态：未完成
- 任务统计：总计 54，已完成 0，未完成 54，待办关键词命中 0
- 判定依据：任务清单尚未开始勾选
<!-- DOC_AUDIT_STATUS_END -->



> **版本**: V1.0  
> **制定日期**: 2026-02-20  
> **前置文档**: [ClearVision_开发路线图_V4.md](./ClearVision_开发路线图_V4.md)（V4.2 核查版）  
> **目标**: 收尾 Sprint 1-5 遗留项，完成产品化最后一公里

---

## 📊 现状总结

| 维度 | 进度 | 说明 |
|------|------|------|
| Sprint 1-5 主体 Task | **19/20 完成** | Task 3.4 后端完成、前端未做 |
| 单元/集成测试 | **59 个测试文件** | 覆盖全部核心模块 |
| 性能验收 | **3/3 通过** | MatPool 命中率 ≥90%、P99 达标、内存零泄漏 |
| AI 编排 | **✅ 端到端可用** | 生成→Lint→Dry-Run→部署 全链路通 |

### 剩余工作项

| 编号 | 内容 | 预估工时 | 优先级 |
|------|------|----------|--------|
| **S6-001** | 手眼标定前端三步向导 UI | 3-5 天 | 🔴 P1 |
| **S6-002** | ForEach 子图编辑器 UI | 3-4 天 | 🟠 P2 |
| **S6-003** | Stub 覆盖率不足时前端自动提示 | 0.5 天 | 🟡 P3 |
| **S6-004** | OCR 性能基准 + 集成测试补充 | 1-2 天 | 🟡 P3 |

> **总预估工时**: 8-12 天（约 2-3 工作周）

---

## S6-001：手眼标定前端三步向导 UI

**优先级**: 🔴 P1 — 阻断产品化发布  
**预估工时**: 3-5 天  
**前置条件**: `HandEyeCalibrationService.cs` 后端已完成 ✅  
**相关文件**: 新建 `handEyeCalibWizard.js`、`handEyeCalibWizard.css`；修改 `settingsModal.js`

### 需求描述

将后端已实现的 `HandEyeCalibrationService`（最小二乘法标定解算 + JSON 持久化）通过前端三步向导暴露给用户，实现人机协作的标定配置流程。

### 详细任务分解

#### 步骤 1 — 后端 API 端点（0.5 天）

- [ ] 在 `MainForm.cs` 中添加 WebView Bridge 消息处理：
  - `handeye:solve` — 接收标定点数据，调用 `SolveAsync()` 返回解算结果
  - `handeye:save` — 接收解算结果 + 文件名，调用 `SaveCalibrationAsync()` 持久化
- [ ] 在 DI 容器中注册 `IHandEyeCalibrationService` → `HandEyeCalibrationService`

#### 步骤 2 — 前端三步向导 UI（2-3 天）

- [ ] 创建 `handEyeCalibWizard.js`
  - **Step 1 — 采集标定点**
    - 实时相机画面区域（调用现有 `cameraManager.js`）
    - 像素坐标自动提取（点击画面获取 XY）
    - 机械臂物理坐标手动输入：`X [___] Y [___] (mm)`
    - [▶ 采集当前点] 按钮 → 添加到点位表格
    - 点位表格：序号、像素X、像素Y、物理X、物理Y、[删除]
    - 最少 4 个点位后激活"下一步"
  - **Step 2 — 验证与求解**
    - [▶ 计算标定矩阵] 按钮 → 调用 `handeye:solve`
    - 展示：Origin XY、Scale XY、RMSE 误差
    - 误差可视化（误差柱状图或数值高亮）
    - RMSE < 阈值时显示绿色 ✅，否则橙色 ⚠️ 提示重新采点
    - 支持删除异常点后重新计算
  - **Step 3 — 保存**
    - 文件名输入框（默认 `hand_eye_calib`）
    - [💾 保存标定文件] 按钮 → 调用 `handeye:save`
    - 保存成功后提示路径 + 关闭向导

- [ ] 创建 `handEyeCalibWizard.css`
  - 遵循现有 **红/白极简科技风** 设计系统
  - 三步进度条（Step Indicator）
  - 遮罩弹窗模式（Modal Overlay）
  - 响应式布局、深色模式适配

#### 步骤 3 — 集成入口（0.5 天）

- [ ] 在 `settingsModal.js` 的设置面板中添加「手眼标定」按钮入口
- [ ] 确保 `CoordinateTransform(HandEye)` 算子运行时能正确加载生成的 JSON 文件
- [ ] 端到端测试：采集 → 求解 → 保存 → 算子加载 → 坐标转换验证

### 验收标准

- [ ] 三步向导 UI 完整可用，无 JS 报错
- [ ] 采集 9 个标定点后解算 RMSE < 1mm
- [ ] JSON 文件成功保存到 AppData 目录
- [ ] `CoordinateTransform(HandEye)` 加载 JSON 后坐标转换误差 < 0.5mm
- [ ] 深色/浅色模式下 UI 显示正常

---

## S6-002：ForEach 子图编辑器 UI

**优先级**: 🟠 P2 — 增强用户体验  
**预估工时**: 3-4 天  
**前置条件**: `ForEachOperator.cs` 后端已完成 ✅  
**相关文件**: 修改 `flowCanvas.js`、`flowRenderer.js`；新建 `forEachContainer.js`、`forEachContainer.css`

### 详细任务分解

#### 子任务 1 — 容器节点渲染（1.5 天）

- [ ] ForEach 节点渲染为可展开/折叠的容器节点
  - 折叠态：标准节点大小 + 虚线边框 + 内部算子数量角标
  - 展开态：放大的虚线容器，内部显示子图算子缩略图
- [ ] 标题栏显示 IoMode 标签
  - `IoMode=Parallel` → `⚡ 并行`（蓝色标签）
  - `IoMode=Sequential` → `🔗 串行`（橙色标签 + 橙色边框）
- [ ] 单击标签可快速切换 IoMode

#### 子任务 2 — 子图编辑模式（1.5 天）

- [ ] 双击 ForEach 节点进入子图编辑模式
  - 画布平移/缩放到子图范围
  - 显示面包屑导航：`主流程 > ForEach子图`
  - 添加半透明遮罩隔离主流程视觉层
- [ ] 子图有独立的 `CurrentItem` 源节点
  - 系统自动注入，不可删除
  - 输出端口：`CurrentItem(Any)`、`CurrentIndex(Integer)`、`TotalCount(Integer)`
- [ ] 退出子图编辑模式（面包屑点击 / ESC 键）

#### 子任务 3 — 数据持久化（0.5 天）

- [ ] 子图结构序列化/反序列化与主流程 JSON 一致
- [ ] 确保拖入子图的算子在 FlowLinter 中正确检查

### 验收标准

- [ ] ForEach 节点可折叠/展开，IoMode 标签正确显示
- [ ] 双击进入子图编辑模式，子图与主流程视觉隔离
- [ ] `CurrentItem` 节点自动出现且不可删除
- [ ] 子图编辑后保存/加载流程 JSON 无丢失
- [ ] IoMode 切换后 Linter SAFETY_002 规则正确触发

---

## S6-003：Stub 覆盖率前端提示增强

**优先级**: 🟡 P3  
**预估工时**: 0.5 天  
**相关文件**: 修改 `lintPanel.js`

### 任务描述

- [ ] Dry-Run 执行完成后，自动计算分支覆盖率
- [ ] 覆盖率 < 80% 时在 lintPanel 中显示橙色警告条：
  - 文案：「分支覆盖率 XX%，存在未验证的条件分支。建议补充更多 Stub 场景以提高仿真可信度。」
- [ ] 提供"查看未覆盖分支"展开面板，列出具体的未激活条件

---

## S6-004：OCR 测试补充

**优先级**: 🟡 P3  
**预估工时**: 1-2 天  
**相关文件**: 新建 `OcrRecognitionOperatorTests.cs`、测试图像资源

### 任务描述

- [ ] 创建 OCR 算子单元测试文件
- [ ] 性能基准测试：1920×1080 图像 → OCR 推理耗时 ≤ 500ms
- [ ] 集成测试用例：
  - 喷码日期识别（如 `2026-02-20`）
  - 批次号识别（如 `LOT_2026_001`）
  - 序列号识别（混合数字字母）
  - 旋转 90° 文字识别
- [ ] 目标准确率 ≥ 95%

---

## 📅 执行排期建议

```
        第 1 周                    第 2 周                 第 3 周（缓冲）
┌─────────────────────┐  ┌─────────────────────┐  ┌───────────────┐
│  S6-001 手眼标定 UI  │  │  S6-002 ForEach UI  │  │  S6-003/004   │
│  (Day 1-5)          │  │  (Day 6-9)          │  │  收尾+回归     │
│  ├ API 端点 (0.5d)  │  │  ├ 容器渲染 (1.5d)  │  │  (Day 10-12)  │
│  ├ 三步向导 (2-3d)  │  │  ├ 子图编辑 (1.5d)  │  │               │
│  └ 集成入口 (0.5d)  │  │  └ 持久化 (0.5d)    │  │               │
└─────────────────────┘  └─────────────────────┘  └───────────────┘
```

---

## 🎯 Sprint 6 完成后的产品状态

| 维度 | 状态 |
|------|------|
| 算子库 | ✅ 30+ 算子全部可用 |
| 内存安全 | ✅ RC + CoW + MatPool，7×24 无 OOM |
| 并发执行 | ✅ ForEach 双模式 + 子图可视化编辑 |
| AI 编排 | ✅ 自然语言 → 工程一键生成 |
| 安全沙盒 | ✅ Linter + 双向 Dry-Run + Stub Registry |
| 手眼标定 | ✅ 三步向导完整可用 |
| 测试覆盖 | ✅ 60+ 测试文件 + 性能验收通过 |
| **整体完成度** | **~95%** → 可进入内部试用阶段 |

---

*文档维护：ClearVision 开发团队*  
*评审节点：每周五下午进行进度评审*
