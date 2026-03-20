# LLM 闭环修复框架

**日期**: 2026-03-20  
**状态**: 第一阶段已落地

---

## 现状结论

当前仓库里已经落地的闭环，不再是“失败后原样重试”，而是面向 AI 工作流生成主链的一套 **结构化诊断 + 定向修复提示 + 失败摘要回传** 机制。

本次落地范围聚焦：

- `AiFlowGenerationService` 的生成重试回路
- `AiFlowValidator` 的结构化校验诊断
- `GenerateFlowMessageHandler` / `AiGenerationMessages` 的前后端消息透传
- 前端 AI 面板对 `FailureSummary` / `LastAttemptDiagnostics` / `PendingParameters` / `MissingResources` 的消费

未落地范围：

- 预览图像驱动的参数自动调优
- 独立二审模型
- 专门的 `AutoTuneService`
- 面向实验统计的评估流水线

也就是说，这份文档描述的是 **工作流生成主链的闭环修复**，不是旧版 “AutoTuneService + PreviewMetricsAnalyzer” 设想稿。

---

## 当前架构

```text
用户需求 / 历史上下文 / 当前流程 / 附件
        │
        ▼
PromptBuilder + 会话上下文整理
        │
        ▼
AiFlowGenerationService 调用模型
        │
        ▼
解析 AI JSON
        │
        ▼
AiFlowValidator 输出结构化诊断
        │
        ├─ 校验通过 → 转 DTO / 布局 / Dry-Run / 返回结果
        │
        └─ 校验失败 → 生成修复优先级 + 最近输出摘要 + 结构化问题清单
                      │
                      ▼
                构造定向 retry prompt
                      │
                      ▼
                  再次请求模型
                      │
                      ▼
            达到重试上限后返回 FailureSummary + LastAttemptDiagnostics
```

---

## 已落地能力

### 1. 结构化校验诊断

`Acme.Product/src/Acme.Product.Core/Services/AiValidationResult.cs`

当前 `AiValidationResult` 保留了原有的：

- `Errors`
- `Warnings`

同时新增：

- `Diagnostics`
- `PrimaryError`

每条诊断包含：

- `Severity`
- `Code`
- `Category`
- `Message`
- `RelatedFields`
- `OperatorId`
- `ParameterName`
- `SourceTempId`
- `SourcePortName`
- `TargetTempId`
- `TargetPortName`
- `RepairHint`

这让 validator 不再只吐一串字符串，而是能明确告诉生成链：

- 错在什么类别
- 错在哪个字段
- 优先修什么

### 2. 定向修复 prompt

`Acme.Product/src/Acme.Product.Infrastructure/AI/AiFlowGenerationService.cs`

重试 prompt 现在会显式带入：

- 原始用户请求
- 修复优先级
- 结构化问题清单
- 最近一次输出摘要
- 最近一次原始输出片段

并明确要求模型：

- 优先修复错误
- 尽量保留已经正确的结构
- 只返回完整 JSON

### 3. 失败摘要与最近诊断

`AiFlowGenerationResult` 现在会在失败时回传：

- `CompletionStatus`
- `FailureType`
- `FailureSummary`
- `LastAttemptDiagnostics`

其中 `FailureSummary` 用于前端快速展示；
`LastAttemptDiagnostics` 用于更细的闭环提示或后续调试。

### 4. 取消 / 超时 / 系统失败分流

生成终态已区分：

- `completed`
- `cancelled`
- `timed_out`
- `failed`

失败类型当前至少区分：

- `user_cancelled`
- `timeout`
- `system_error`

这套状态会一路传到：

- `GenerateFlowMessageHandler`
- `AiGenerationMessages`
- AI 面板前端

前端因此可以把“已取消”与“系统失败”区分开来。

### 5. 前端待补闭环

AI 面板现在可以消费：

- `PendingParameters`
- `MissingResources`
- `FailureSummary`
- `LastAttemptDiagnostics`

并提供：

- 复制待补文本
- 插入输入框
- 作为下一轮 `hint`

这让“修复回路”不只停留在后端重试，也能把待补信息直接回交给用户。

---

## 代码落点

### 主链核心

- `Acme.Product/src/Acme.Product.Infrastructure/AI/AiFlowGenerationService.cs`
- `Acme.Product/src/Acme.Product.Infrastructure/AI/AiFlowValidator.cs`
- `Acme.Product/src/Acme.Product.Core/Services/AiValidationResult.cs`
- `Acme.Product/src/Acme.Product.Core/DTOs/AiGenerationDto.cs`

### 前后端消息桥

- `Acme.Product/src/Acme.Product.Infrastructure/AI/GenerateFlowMessageHandler.cs`
- `Acme.Product/src/Acme.Product.Contracts/Messages/AiGenerationMessages.cs`
- `Acme.Product/src/Acme.Product.Desktop/Handlers/WebMessageHandler.cs`

### 前端消费层

- `Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/ai/aiPanel.js`
- `Acme.Product/src/Acme.Product.Desktop/wwwroot/src/shared/styles/ai-panel.css`

---

## 当前边界

这套闭环已经能解决：

- 工作流 JSON 结构错误
- 算子类型错误
- 连线端口错误
- 参数缺失 / 越界 / 枚举非法
- 用户取消与超时分流

但还没有解决：

- 基于运行预览指标的自动参数收敛
- 生成结果的二模型复审
- 面向数据集的效果评估与实验管理

因此当前阶段的准确表述应为：

> ClearVision 已落地“面向工作流生成主链的结构化闭环修复”，但尚未落地“面向视觉预览反馈的自动调参闭环”。

---

## 后续建议

下一阶段若继续演进，建议顺序如下：

1. 将 `LastAttemptDiagnostics` 与前端结构化展示进一步细化，而不只是摘要透传。
2. 为 `AiFlowGenerationService` 增补直接单元测试，避免只靠邻接测试覆盖。
3. 在必要时引入“二审/复核模型”，只用于高风险失败场景，而不是默认串行增加成本。
4. 如要重启 AutoTune 方向，应另起文档，不要再与当前工作流生成闭环混写。
