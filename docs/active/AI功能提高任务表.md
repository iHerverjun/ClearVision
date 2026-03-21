---
title: "AI功能提高任务表"
doc_type: "task-list"
status: "active"
topic: "AI功能"
created: "2026-03-20"
updated: "2026-03-20"
---
# AI功能提高任务表

更新时间：2026-03-20  
适用范围：`Acme.Product` 当前 AI 工作流生成主链路

> **收敛说明**：结合[项目现状与未来方向纲领](../reference/总纲/项目现状与未来方向纲领-2026-03-19.md)方向，本任务表已做战略性收缩。当前阶段的主要矛盾是**"模板闭环 + 预览反馈 + 参数调优 + 人机协同"**，而非 API 安全加密、多模型角色分配、可观测性基础设施等横向扩张。以下非主要矛盾任务已移除：
>
> - ~~P0-01/P0-02：API 密钥安全与加密存储~~（安全卫生任务，非产品主线）
> - ~~P0-03：多协议路由与高级配置打通~~（多模型基础设施，非当前瓶颈）
> - ~~P0-04：升级模型测试接口~~（多模型验证，非场景闭环核心）
> - ~~P1-01：按角色选模~~（多模型分配策略，当前只需单模型工作）
> - ~~P1-02：主链统一 telemetry~~（可观测性基础设施，可后置）
> - ~~P1-05：设置页 AI 高级配置~~（多模型配置 UI，非主线）
> - ~~P2-03：效果对比与实验机制~~（过早优化，当前无足够数据支撑）

## 目标

聚焦当前 AI 功能的 **2 个核心改进方向**，与项目总纲"模板起步、反馈驱动、参数收敛、经验沉淀"对齐：

1. **前端闭环不完整**：用户看得到摘要，但无法真正处理待确认参数、缺失资源、取消请求。
2. **AI 架构分叉与修复能力不足**：新旧两套 AI 链路并存，重试仍是机械重生而非定向修复。

## 优先级定义

- `P1`：直接影响用户反馈闭环与日常可用性，优先处理。
- `P2`：架构收敛与修复能力演进，分阶段推进。

## 总执行顺序

1. `P1-01` 前端落地待确认参数/缺失资源交互。
2. `P1-02` 增加"取消生成"能力。
3. `P2-01` 收敛新旧两套 AI 架构。
4. `P2-02` 实施真正的闭环修复框架。

## P1 任务

| ID | 任务 | 目标 | 具体文件 | 改动要点 | 验收标准 |
|---|---|---|---|---|---|
| `P1-01` | 前端落地结构化待办 | 让 `PendingParameters/MissingResources` 可操作 | `Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/ai/aiPanel.js` | 增加侧栏或结果卡片，按算子展示待确认参数、缺失资源，并支持一键复制到下一轮 `hint` | 用户能直接看见"哪些参数待补、缺什么资源" |
|  |  |  | `Acme.Product/src/Acme.Product.Contracts/Messages/AiGenerationMessages.cs` | 如有必要，补充 `FailureSummary` 或 `LastAttemptDiagnostics` 契约字段 | 契约可支持更强闭环 |
| `P1-02` | 增加取消生成能力 | 避免长请求卡死 UI | `Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/ai/aiPanel.js` | 增加"取消生成"按钮；生成中允许用户主动中断 | 用户可在生成中手动取消 |
|  |  |  | `Acme.Product/src/Acme.Product.Desktop/Handlers/WebMessageHandler.cs` | 为 `GenerateFlow` 建立 `CancellationTokenSource` 管理，支持按会话/请求取消 | 取消后后端不继续推送流式内容 |
|  |  |  | `Acme.Product/src/Acme.Product.Infrastructure/AI/GenerateFlowMessageHandler.cs` | 明确区分超时、用户取消、系统失败三类结果 | 前端能准确显示"已取消" |

### P1 顺序细化

1. 先做 `P1-01`，把结构化结果交给用户，服务"反馈驱动"闭环。
2. 再做 `P1-02`，补取消链路与交互闭环。

## P2 任务

| ID | 任务 | 目标 | 具体文件 | 改动要点 | 验收标准 |
|---|---|---|---|---|---|
| `P2-01` | 收敛新旧 AI 架构 | 减少维护分叉 | `Acme.Product/src/Acme.Product.Infrastructure/AI/AIWorkflowService.cs` | 明确其定位：要么迁移进主链，要么退役为兼容层 | 仓库中只有一套被正式支持的 AI 主链 |
|  |  |  | `Acme.Product/src/Acme.Product.Infrastructure/AI/AIPromptBuilder.cs` | 旧版已标记过时，决定下线或仅保留迁移测试 | 不再让旧 prompt builder 成为主要测试对象 |
|  |  |  | `Acme.Product/src/Acme.Product.Infrastructure/AI/AIGeneratedFlowParser.cs` | 判断是否仍需保留；如保留，明确仅服务旧格式导入 | 新旧职责边界清晰 |
|  |  |  | `Acme.Product/tests/Acme.Product.Tests/AI/Sprint5_AIWorkflowServiceTests.cs` | 将旧链测试降级为兼容测试，新增主链端到端测试 | AI 测试不再主要覆盖旧链 |
| `P2-02` | 实施真正的闭环修复框架 | 从"重试"升级到"修复回路" | `docs/needs-review/AI闭环待复核总览.md` | 将文档从"设计完成，待实施"变为已落地方案与现状说明 | 文档与代码一致 |
|  |  |  | `Acme.Product/src/Acme.Product.Infrastructure/AI/AiFlowGenerationService.cs` | 引入失败分类、修复目标、上一轮输出摘要、必要时二次审查模型 | 重试从机械再生变成定向修复 |
|  |  |  | `Acme.Product/src/Acme.Product.Infrastructure/AI/AiFlowValidator.cs` | 增加结构化错误码、错误类别、涉及字段信息 | 失败反馈不再只有字符串 |

### P2 顺序细化

1. 先做 `P2-01`，避免两套架构继续一起演化。
2. 再做 `P2-02`，把闭环修复能力落到主链。

## 文件级改动顺序建议

### 第一批：前端闭环

1. `Acme.Product/src/Acme.Product.Contracts/Messages/AiGenerationMessages.cs`
2. `Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/ai/aiPanel.js`
3. `Acme.Product/src/Acme.Product.Desktop/Handlers/WebMessageHandler.cs`
4. `Acme.Product/src/Acme.Product.Infrastructure/AI/GenerateFlowMessageHandler.cs`

### 第二批：架构收敛与闭环修复

1. `Acme.Product/src/Acme.Product.Infrastructure/AI/AIWorkflowService.cs`
2. `Acme.Product/src/Acme.Product.Infrastructure/AI/AIPromptBuilder.cs`
3. `Acme.Product/src/Acme.Product.Infrastructure/AI/AIGeneratedFlowParser.cs`
4. `Acme.Product/src/Acme.Product.Infrastructure/AI/AiFlowGenerationService.cs`
5. `Acme.Product/src/Acme.Product.Infrastructure/AI/AiFlowValidator.cs`
6. `Acme.Product/tests/Acme.Product.Tests/AI/Sprint5_AIWorkflowServiceTests.cs`

## 测试补充清单

### 主链必补

- `Acme.Product/tests/Acme.Product.Tests/AI/`
  - 新增：取消生成后的状态测试
  - 新增：失败分类与修复回路测试
  - 新增：主链端到端测试（覆盖收敛后的唯一架构）

### 旧链处理

- `Acme.Product/tests/Acme.Product.Tests/AI/Sprint5_AIWorkflowServiceTests.cs`
  - 调整为"兼容链测试"或拆到 legacy 测试分组
- `Acme.Product/tests/Acme.Product.Tests/AI/LLMConnectorSmokeTests.cs`
  - 明确哪些仍服务主链，哪些只服务旧 connector

## 每阶段完成标准

### P1 完成标准

- 前端能展示并处理待确认参数和缺失资源。
- 用户可以取消生成中的请求。

### P2 完成标准

- AI 主链只保留一套正式架构。
- 闭环修复从文档状态变成代码能力。

## 建议里程碑

- 里程碑 A：`P1` 全部完成，把用户反馈闭环和日常可用性拉起来。
- 里程碑 B：`P2-01` 完成，把架构维护成本降下来。
- 里程碑 C：`P2-02` 完成，让 AI 修复从重试升级为定向闭环。

