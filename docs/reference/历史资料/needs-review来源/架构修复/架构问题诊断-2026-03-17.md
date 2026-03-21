# ClearVision 架构问题诊断报告

**生成日期**: 2026-03-17
**诊断来源**: 基于第二家中转商（Opus 4.6/GPT-5 级别）的深度分析 + Claude Opus 4.6 官方验证

---

## 执行摘要

ClearVision 项目在产品愿景和技术实现上都具备可行性，但当前存在**三个层级的架构问题**，按严重程度排序：

1. **严重缺陷**：实时检测状态管理的生命周期错误（Scoped 服务保存全局状态）
2. **设计债务**：命令面与事件面的通信机制分裂（HTTP + WebMessage 混合态）
3. **产品方向**：LLM 集成缺少闭环反馈机制，导致只能连线不能调参

---

## 问题 1：实时检测状态管理的生命周期错误 ⚠️ 严重

### 问题描述

**现状**：
- `InspectionService` 使用实例字段保存实时检测的运行状态：
  ```csharp
  private readonly Dictionary<Guid, CancellationTokenSource> _realtimeCtsMap = new();
  private readonly Dictionary<Guid, Task> _realtimeTasks = new();
  ```
- 但 `IInspectionService` 在 DI 容器中注册为 **Scoped** 生命周期

**后果**：
- 不同 HTTP 请求拿到不同的 `InspectionService` 实例
- `POST /api/inspection/realtime/start` 保存的 CTS 在 `POST /api/inspection/realtime/stop` 时**找不到**
- "已在运行中"的冲突检查会失效（每个请求都看不到其他请求启动的任务）
- 实时检测无法正常停止，可能导致后台任务泄漏

**影响范围**：
- 实时检测的启动/停止功能完全不可靠
- 多用户或多标签页同时操作时会出现状态混乱
- 长时间运行可能导致内存泄漏（无法停止的后台任务）

**源码位置**：
- `Acme.Product.Application/Services/InspectionService.cs`（实例字段）
- `Acme.Product.Desktop/DependencyInjection.cs`（Scoped 注册）

---

## 问题 2：命令面与事件面的通信机制分裂 ⚠️ 设计债务

### 问题描述

**现状**：
- **命令面**（启动/停止）：已迁移到 HTTP API
  - `POST /api/inspection/realtime/start`
  - `POST /api/inspection/realtime/stop`
  - 前端 `inspectionController.js` 的 `startRealtime()` / `stopRealtime()` 调用 HTTP

- **事件面**（结果推送）：仍然依赖 WebMessage
  - `WebMessageHandler.NotifyInspectionResult(...)` 推送检测结果
  - 只能推送给单个 WebView2 实例（`_webView` 字段）
  - 普通浏览器标签页无法接收实时结果

**后果**：
1. **架构不一致**：控制走 HTTP（RESTful），事件走 WebMessage（宿主专有）
2. **扩展性受限**：无法支持多客户端同时监听同一工程的实时结果
3. **外部集成困难**：外部工具可以通过 HTTP 启动检测，但拿不到持续的结果流
4. **前端状态不一致**：
   - 前端 `inspectionController` 的状态（`isRealtime`、`isRunning`）是本地维护的
   - 后端没有 `stateChanged` 事件推送给前端
   - 当外部 HTTP 启动/停止时，前端状态不会自动同步

**根本原因**：
这是一个**过渡架构**——从"WebView2 专有桥接"向"通用 HTTP API"迁移的中间状态：
- 命令面已迁移完成（HTTP）
- 事件面还未迁移（仍依赖 WebMessage）
- 缺少统一的事件广播机制

**源码位置**：
- `Acme.Product.Desktop/ApiEndpoints.cs`（HTTP 接口）
- `Acme.Product.Desktop/WebMessageHandler.cs`（WebMessage 推送）
- `wwwroot/src/controllers/inspectionController.js`（前端状态管理）

---

## 问题 3：中间算子预览的输入来源不明确 ⚠️ 用户体验

### 问题描述

**场景**：
用户在画布上构建工作流：`ImageAcquisition → Filtering → Thresholding → BlobAnalysis`

当用户想预览 `Thresholding` 的效果时，这个算子需要的输入图像应该是 `Filtering` 的输出，而不是原始图像。

**现状**：
- 文档只说明了"前端调用 `POST /api/operators/{type}/preview`，后端执行 `OperatorPreviewService.PreviewAsync()`"
- 但**没有说明**预览中间算子时，输入图像从哪里来
- 前端有 `resolveInputImageBase64()` 和 `captureBySoftTrigger()`，但不清楚是否会执行上游算子

**可能的实现方式**（推测）：
1. **永远用原始图像**：中间算子预览语义偏离真实工作流，调参不可信
2. **每次重新执行上游链路**：性能开销大，连续调参会卡
3. **使用调试缓存**：文档显示 `FlowExecutionService` 有调试缓存能力，但缓存失效策略可能不够精细

**后果**：
- 用户调整中间算子参数时，预览结果可能与实际执行不一致
- 连续调参体验差（每次都要重新执行上游）
- 缓存管理不当可能导致内存泄漏

**源码位置**：
- `Acme.Product.Infrastructure/Services/OperatorPreviewService.cs`
- `Acme.Product.Infrastructure/Services/FlowExecutionService.cs`
- `wwwroot/src/panels/previewPanel.js`
- `wwwroot/src/panels/propertyPanel.js`

---

## 问题 4：LLM 集成缺少闭环反馈机制 ⚠️ 产品方向

### 问题描述

**愿景**：
用户描述场景或上传图片，LLM 理解需求并调用算子库生成可用的视觉工作流。

**现状**：
- 为 118 个算子制作了详细的"算子名片"（元数据）
- LLM 能够根据名片选择算子并连线
- 但 LLM **无法有效调整参数**，只能生成默认值或简单猜测

**根本原因**：
LLM 被用在了它最不擅长的层级——**盲调参数**：
- 参数空间是连续的、强依赖图像分布的
- LLM 没有数值优化能力
- **缺少执行反馈**：LLM 生成 JSON 后就结束了，看不到执行结果、看不到中间图像、看不到错误信息

**系统已有的能力（未被 LLM 利用）**：
- ✅ `ParameterRecommender`：基于图像统计（Otsu、拉普拉斯方差、面积分位数）推荐参数
- ✅ `OperatorPreviewService`：单算子预览执行，返回输出图像和数据
- ✅ `ValidateFlow` + `DryRun`：流程校验和试运行
- ✅ 前端 `lintPanel`：展示校验问题和 DryRun 结果

**缺少的环节**：
- LLM 无法看到预览结果（中间图像、Blob 统计、错误信息）
- LLM 无法根据反馈迭代调整参数
- 没有"可量化的评估指标"（如 BlobCount、面积分布、噪声惩罚）供 LLM 优化

**后果**：
- LLM 只能做"结构性工作"（选算子、连线），无法做"数值优化"（调参）
- 距离"用户说明场景，LLM 生成可用工作流"的愿景还很远
- 118 个算子对现场工人来说仍然太复杂，无法自主构建工作流

---

## 改进方案优先级

### 🔴 P0：修复 Scoped 生命周期 bug（1 周）

**目标**：让实时检测能够正常启动和停止

**方案**：
1. 新增进程级单例 `IInspectionRuntimeCoordinator`
2. 将运行状态从 `InspectionService` 实例字段迁移到协调器
3. 按 `projectId` 维护状态（sessionId、state、CTS、Task）
4. 实现并发控制（每个 projectId 一把锁）

**改动文件**：
- 新增：`Acme.Product.Application/Services/InspectionRuntimeCoordinator.cs`
- 修改：`Acme.Product.Application/Services/InspectionService.cs`（移除实例字段）
- 修改：`Acme.Product.Desktop/DependencyInjection.cs`（注册单例）
- 修改：`Acme.Product.Desktop/ApiEndpoints.cs`（调用协调器）

---

### 🟡 P1：统一命令面与事件面（2-3 周）

**目标**：解决 HTTP 和 WebMessage 的状态不一致问题，支持多客户端监听

**方案**：
1. 引入统一的事件广播机制（推荐 SSE，未来可升级到 SignalR）
2. 新增 `GET /api/inspection/realtime/{projectId}/events`（SSE 端点）
3. 推送事件类型：`stateChanged`、`resultProduced`、`progressChanged`、`stopped`、`faulted`
4. `WebMessageHandler` 改为订阅事件总线，而不是直接被业务层调用
5. 前端改为事件驱动：状态来自后端推送，而不是本地猜测

**改动文件**：
- 新增：`Acme.Product.Application/Events/IInspectionEventBus.cs`
- 修改：`Acme.Product.Application/Services/InspectionService.cs`（发布事件）
- 修改：`Acme.Product.Desktop/ApiEndpoints.cs`（新增 SSE 端点）
- 修改：`Acme.Product.Desktop/WebMessageHandler.cs`（订阅事件总线）
- 修改：`wwwroot/src/controllers/inspectionController.js`（事件驱动状态）

---

### 🟢 P2：实现预览子图与调试缓存（2 周）

**目标**：让中间算子预览使用真实的上游输出，提升调参体验

**方案**：
1. 新增 `PreviewInFlowAsync()`：执行目标节点依赖的最小上游子图
2. 使用调试缓存保存上游输出（key = debugSessionId + operatorId + paramsHash + upstreamHash）
3. 用户只改目标节点参数时，上游结果复用，快速重跑
4. 复用现有的 `IImageCacheRepository` 管理图像内存
5. 复用现有的 `CleanupStaleDebugSessions()` 清理过期缓存

**改动文件**：
- 新增：`Acme.Product.Infrastructure/Services/FlowNodePreviewService.cs`
- 修改：`Acme.Product.Desktop/ApiEndpoints.cs`（新增 `POST /api/flows/preview-node`）
- 修改：`wwwroot/src/panels/previewPanel.js`（调用新接口）
- 修改：`wwwroot/src/panels/propertyPanel.js`（管理 debugSessionId）

---

### 🔵 P3：实现 LLM 迭代调参闭环（3-4 周）

**目标**：让 LLM 能够根据执行反馈自动调整参数，而不是盲猜

**方案**：
1. **扩展预览反馈信息**（5 组信息）：
   - 可复现的运行上下文（flowSnapshot、operatorMetadata、currentParameters）
   - 中间结果可视化（previewImages、imageStats、直方图、拉普拉斯方差）
   - Blob 级别归因信息（blobStats、areaDistribution、rejectReason）
   - 诊断标签（SpecularHighlightsDominant、MaskTooNoisy、StrapFragmented）
   - 可优化目标与评分（countError、noisePenalty、fragmentPenalty）

2. **新增迭代调参服务**：
   ```csharp
   public interface IAutoTuneService
   {
       Task<AutoTuneResult> AutoTuneOperatorAsync(
           OperatorType type,
           Mat inputImage,
           IReadOnlyDictionary<string, object> initialParameters,
           AutoTuneGoal goal,
           CancellationToken ct
       );
   }
   ```

3. **调参策略**（基于视觉算法知识）：
   - 针对"白色反光导致误检"：提高阈值、自适应阈值、形态学开运算
   - 针对"包装带被切碎"：形态学闭运算、调整 MinArea
   - 针对"照明不均"：自适应阈值、光照归一化
   - 判断"参数问题"还是"算法问题"：小范围参数扫掠、中间产物趋势分析、诊断标签触发

4. **迭代流程**：
   ```
   LLM 生成初始工作流
   → 系统执行并返回详细反馈（图像、统计、诊断）
   → LLM 根据反馈调整参数
   → 重新执行并评分
   → 重复 2-3 次或达到目标
   ```

**改动文件**：
- 新增：`Acme.Product.Infrastructure/Services/PreviewMetricsAnalyzer.cs`（提取指标和诊断）
- 扩展：`Acme.Product.Infrastructure/Services/OperatorPreviewService.cs`（新增 `PreviewWithMetricsAsync`）
- 扩展：`Acme.Product.Infrastructure/Services/ParameterRecommender.cs`（新增 `RecommendNextAsync`）
- 新增：`Acme.Product.Application/Services/AutoTuneService.cs`（编排迭代循环）
- 修改：`Acme.Product.Desktop/ApiEndpoints.cs`（新增 `/api/operators/{type}/auto-tune`）

---

## 问题 5：算子库的可用性问题 ⚠️ 产品定位

### 问题描述

**现状**：
- 算子库包含 118 个算子
- 每个算子都有详细的"算子名片"（参数、端口、方法说明）
- 目标用户包括"现场工人"

**问题**：
- 118 个算子对非专业用户来说**认知负担过大**
- 即使有详细文档，工人也很难理解"什么场景用什么算子"
- 算子之间的"隐含约束"没有被强约束地表达（如"工作流必须从 ImageAcquisition 开始"这种规则在文档和代码中不一致）
- LLM 也无法有效利用这 118 个算子（只能连线，不能调参）

**建议**：
1. **算子分层**：
   - Basic（20-30 个）：给工人用，覆盖 80% 的常见场景
   - Advanced（50-60 个）：给工程师用
   - Internal/Deprecated（剩余）：保留但不出现在默认库

2. **模板化**：
   - 为高频场景（包装带检测、螺丝计数、表面划痕检测等）制作"黄金模板"
   - 模板只暴露 5-10 个关键参数，其他参数使用推荐值
   - 工人学习的是"配方"，而不是"算子 API"

3. **元数据机器化**：
   - 端口类型约束（强类型检查）
   - 参数范围/单位/互斥关系（可校验）
   - 前置条件（如"BlobAnalysis 需要二值图像"）
   - 推荐策略入口（关联到 `ParameterRecommender`）

---

## 最小闭环验证计划（4-6 周）

### 目标
验证"LLM + 闭环反馈 + 自动调参"方向的可行性

### 里程碑

#### Week 1：修复 Scoped bug
- [ ] 实现 `IInspectionRuntimeCoordinator`（单例）
- [ ] 迁移运行状态管理
- [ ] 测试实时检测的启动/停止功能

#### Week 2-3：统一事件机制
- [ ] 实现 `IInspectionEventBus`
- [ ] 新增 SSE 端点 `/api/inspection/realtime/{projectId}/events`
- [ ] 前端改为事件驱动状态管理
- [ ] 测试多标签页同时监听

#### Week 4：实现预览子图
- [ ] 实现 `FlowNodePreviewService`（预览上游子图）
- [ ] 实现调试缓存（按 paramsHash + upstreamHash）
- [ ] 前端支持"连续调参快速预览"

#### Week 5-6：端到端验证（选一个高频场景）
- [ ] 选择场景：包装带检测（2 条白色包装带）
- [ ] 准备 10-20 张样例图
- [ ] 实现 `PreviewWithMetricsAsync`（返回 BlobCount、面积分布、诊断标签）
- [ ] 实现简单的自动调参（Threshold + MinArea 迭代 2-3 次）
- [ ] 测试：用户上传图片 → LLM 生成工作流 → 自动调参 → 达到 BlobCount=2

**验收标准**：
- 新手用户能在 5 分钟内完成一个包装带检测工作流
- 自动调参成功率 > 70%（10 张图中至少 7 张能调到正确结果）
- 前端状态与后端完全同步，无状态不一致

---

## 技术债务清单

### 高优先级
1. ✅ Scoped 服务保存全局状态（问题 1）
2. ✅ 命令面与事件面分裂（问题 2）
3. ✅ 前端状态本地维护，缺少后端推送（问题 2）

### 中优先级
4. ⚠️ 中间算子预览输入来源不明确（问题 3）
5. ⚠️ 调试缓存失效策略可能不够精细（问题 3）
6. ⚠️ LLM 无法看到执行反馈（问题 4）

### 低优先级
7. 📋 算子库缺少分层（问题 5）
8. 📋 缺少高频场景的黄金模板（问题 5）
9. 📋 元数据约束不够机器化（问题 5）

---

## 竞争力分析

### 你的核心优势
1. ✅ **118 个算子库**：覆盖面广，包含 Halcon 级别的专业算法
2. ✅ **画布式 UI**：可视化工作流编辑，降低编程门槛
3. ✅ **已有的基础设施**：预览、推荐、校验、DryRun 都已实现
4. ✅ **LLM 集成的前瞻性**：方向正确，只是缺少闭环

### 与海康 Vision Master 的差距
1. ❌ **稳定性**：当前有严重的架构 bug（Scoped 问题）
2. ❌ **易用性**：118 个算子对工人来说太复杂，缺少模板
3. ❌ **LLM 能力**：只能连线，不能调参，距离"智能生成"还很远
4. ⚠️ **生态**：缺少培训体系、案例库、社区支持

### 可行性判断

**如果你能在 4-6 周内完成最小闭环验证，证明：**
- 新手能在 5 分钟内完成一个检测工作流
- 自动调参成功率 > 70%
- 系统稳定运行（无状态混乱、无内存泄漏）

**那么这个项目在你们公司内部就有明确的价值。**

**至于对标大厂、商业化推广，那是解决了上述问题之后才需要考虑的事情。**

---

## 下一步行动建议

### 立即行动（本周）
1. 修复 Scoped bug（P0）
2. 选择一个高频场景作为验证目标（如包装带检测）
3. 准备 10-20 张该场景的样例图

### 短期目标（4-6 周）
1. 完成最小闭环验证
2. 证明"LLM + 闭环反馈"方向可行
3. 沉淀 1-2 个黄金模板

### 中期目标（3-6 个月）
1. 修复所有 P0/P1 架构问题
2. 为 5-10 个高频场景制作模板
3. 培训 2-3 个现场工人，验证易用性
4. 收集真实产线的反馈数据

---

## 附录：关键源码位置速查

| 问题 | 关键文件 | 行号/章节 |
|------|---------|----------|
| Scoped bug | `InspectionService.cs`、`DependencyInjection.cs` | - |
| HTTP 接口 | `ApiEndpoints.cs` | 第 672-673 行 |
| WebMessage 推送 | `WebMessageHandler.cs` | 第 842 行 |
| 前端状态管理 | `inspectionController.js` | 第 1802-1821 行 |
| 预览服务 | `OperatorPreviewService.cs` | 第 1135-1147 行 |
| 参数推荐 | `ParameterRecommender.cs` | 第 1149-1159 行 |
| 流程执行 | `FlowExecutionService.cs` | 第 1039-1069 行 |
| 调试缓存 | `FlowExecutionService.cs` | 第 1065-1069 行 |

---

**结论**：项目有未来，但需要先解决架构问题，再谈产品愿景。
