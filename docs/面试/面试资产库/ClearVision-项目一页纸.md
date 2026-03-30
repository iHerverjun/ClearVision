# ClearVision 项目一页纸

> 用途：开场自我介绍、简历补充说明、面试官要求“一句话说清项目”时的统一口径。
> 使用原则：先定边界，再讲亮点，不靠夸张数字。

---

## 1. 一句话版本

ClearVision 目前更适合定义为一个“围绕视觉流程编排、模板复用和 LLM 辅助生成的工业视觉原型系统”，它的核心价值不是炫算子数量，而是把“搭流程”这件事做得更结构化、更可复用、更容易落地验证。

---

## 2. 项目定位

### 2.1 我建议对外这样定义

- 它不是“已经大规模商用的成熟平台”。
- 它也不是“只会调用 LLM 出一段 JSON 的玩具 Demo”。
- 它更准确的定位是：已经跑通“自然语言需求 -> 模板优先收缩 -> 结构化流程生成 -> Validator 校验 -> DryRun 预演 -> 人工确认 -> 运行时执行”这一条核心链路的原型系统 / MVP。

### 2.2 为什么这个定义更稳

- 仓库里确实有 AI 生成链路的主干代码：
  - `PromptBuilder`
  - `AiFlowGenerationService`
  - `AiFlowValidator`
  - `DryRunService`
  - `GenerateFlowMessageHandler`
- 仓库里也确实有运行时主干：
  - `FlowExecutionService`
  - `InspectionWorker`
  - `InspectionRuntimeCoordinator`
  - `OperatorPreviewService`
  - `CameraManager`
- 线序检测场景包已经把“模板 + 规则 + 资源缺口 + 调参边界”沉淀成了可复用资产，而不是只停留在概念上。

---

## 3. 当前阶段

### 3.1 当前最强的状态

- 已经形成可讲清的主线：LLM 编排链路 + 模板化收缩 + 人工确认边界。
- 已经有明确的试点场景：`wire-sequence-terminal` 端子线序检测。
- 已经有运行时、模板服务、校验器、DryRun、部分诊断输出、基础测试与 benchmark/profiler 基础设施。

### 3.2 目前还不该夸大的地方

- 不能说已经形成成熟行业平台。
- 不能说所有场景都能稳定自动生成可直接上线的流程。
- 不能说当前已经有完整的现场效率统计、商业化规模验证或大规模交付记录。
- 不能说我对所有算子的内部实现都掌控到同一深度。

### 3.3 最稳妥的阶段结论

按 2026-03-28 当前仓库状态，更适合说：

> “我已经把一个围绕视觉流程生成与模板化落地的原型系统做到了核心链路可运行、部分场景可验证、关键边界开始收敛的阶段，但它还不应该被包装成一个已全面成熟的商用平台。”

---

## 4. 个人贡献边界

### 4.1 我可以明确认领的部分

- 主导了项目整体方向和核心叙事，从“堆算法”逐步收敛到“LLM 编排 + 模板优先 + 人工审计边界”。
- 主导了 AI 生成主链路的设计口径，包括 Prompt 结构、模板优先策略、Validator、DryRun、前端消息回传口径。
- 主导了线序检测场景包的模板化落地、规则约束、阈值职责收口和诊断输出整理。
- 对少数主打算子和可讲清的工程链路做了亲自验证，尤其是 `BlobDetection / TemplateMatch / CannyEdge` 以及线序模板相关链路。

### 4.2 我不该说成“完全掌控”的部分

- 不能把整仓所有算子都说成我能逐个深聊 10 分钟。
- 不能把 AI 辅助生成的大量代码包装成“全部纯手写且我逐行深度设计”。
- 不能把没有现场实测记录的效率收益说成已被业务验证。

### 4.3 面试里建议的稳定表达

> “这个项目里我最有掌控度的部分，不是‘我一个人写了多少代码’，而是我把 LLM 编排、模板化收缩、Validator/DryRun、人工确认边界这一条主链路设计清楚并持续收敛了下来。代码实现中有 AI 辅助加速，但关键链路的取舍、边界和验证是我负责的。”

---

## 5. AI 辅助边界

### 5.1 AI 负责什么

- 根据自然语言描述生成结构化流程草案。
- 在命中高频场景时附带模板推荐、待确认参数、缺失资源信息。
- 帮助把已有设计快速落成实现，提升迭代速度。

### 5.2 AI 不负责什么

- 不直接替代真实业务验收。
- 不负责自动知道现场真实相机 ID、模型文件路径、ROI、阈值来源。
- 不意味着“返回合法 JSON 就已经业务正确”。
- 不意味着没有人工确认就可以直接上线使用。

### 5.3 当前系统真正的保险丝

- `AiFlowValidator` 负责结构与规则层校验。
- `DryRunService` + `DryRunStubRegistry` 负责预演与覆盖信息收集。
- 模板场景包负责把算子选择空间收缩到高频可靠骨架。
- 最终仍保留人工确认环节。

---

## 6. 最强证据点

### 6.1 最能说明项目不是“空谈”的证据

- 线序检测场景包：
  - `线序检测/scenario-package-wire-sequence/README.md`
  - `线序检测/scenario-package-wire-sequence/manifest.json`
  - `线序检测/scenario-package-wire-sequence/template/terminal-wire-sequence.flow.template.json`
  - `线序检测/scenario-package-wire-sequence/rules/sequence-rule.v1.json`
- AI 主链路：
  - `Acme.Product/src/Acme.Product.Infrastructure/AI/PromptBuilder.cs`
  - `Acme.Product/src/Acme.Product.Infrastructure/AI/AiFlowGenerationService.cs`
  - `Acme.Product/src/Acme.Product.Infrastructure/AI/AiFlowValidator.cs`
  - `Acme.Product/src/Acme.Product.Infrastructure/AI/DryRun/DryRunService.cs`
  - `Acme.Product/src/Acme.Product.Infrastructure/AI/GenerateFlowMessageHandler.cs`
- 工程与可验证性：
  - `Acme.Product/src/Acme.Product.Infrastructure/Diagnostics/PerformanceProfiler.cs`
  - `Acme.Product/tests/Acme.Product.Tests/Diagnostics/PerformanceProfilerTests.cs`
  - `Acme.Product/tests/Acme.Product.Tests/Performance/OperatorBenchmarkTests.cs`

### 6.2 最能说明“我知道边界”的证据

- [面试禁语与替代表述清单.md](./面试禁语与替代表述清单.md)
- [业务正确性三层验证说明.md](./业务正确性三层验证说明.md)
- [PlanB-断网与离线降级方案.md](./PlanB-断网与离线降级方案.md)

---

## 7. 面试里可以直接这样说

> ClearVision 现在我更愿意把它定义成一个工业视觉原型系统，而不是一个已经全面成熟的商用平台。它最核心的突破点，不是算子数量，而是我把“自然语言需求到可运行视觉流程”的主链路做成了模板优先、可校验、可预演、可人工确认的形式。  
>  
> 我个人最主要的贡献，是这条 LLM 编排与模板化收缩链路的设计和收敛，包括 Prompt 组织、模板命中、Validator、DryRun、消息回传，以及线序场景包的规则和边界整理。代码实现过程中有 AI 辅助加速，但关键链路的取舍、验证和风险边界是我负责的。  
>  
> 所以如果面试想看我最有掌控度的部分，我不会把重点放在“我写了多少行”，而是放在“我怎样让 AI 生成结果变得更可控、更接近真实落地”。

