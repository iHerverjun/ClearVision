---
title: "AI功能提高任务表"
doc_type: "task-list"
status: "active"
topic: "AI功能"
created: "2026-03-20"
updated: "2026-03-23"
---
# AI功能提高任务表

更新时间：2026-03-23  
适用范围：`Acme.Product` 当前 AI 生成主链与线序闭环样板

## 已落地

- [x] AI 主链已支持 `template-first`，可优先命中“端子线序检测”模板
- [x] `GenerateFlowResponse` 已回传 `FailureSummary`、`LastAttemptDiagnostics`、`PendingParameters`、`MissingResources`
- [x] 前端 AI 面板已消费待确认参数、缺失资源和取消生成结果
- [x] 取消生成已从前端按钮贯通到 `WebMessageHandler` 与 `GenerateFlowMessageHandler`

## 当前剩余 AI 主线

- [ ] 把线序场景 preview / autotune 结果接入 AI follow-up，只生成“改参数、不改结构”的下一轮 hint
- [ ] 把线序专用诊断码稳定成 snake_case 场景契约，避免 endpoint / analyzer / 文档各说各话
- [ ] 让 AI follow-up 优先消费 `BoxNms.ScoreThreshold` / `BoxNms.IouThreshold` 建议，不再回退到旧的泛化 `Confidence` 提示
- [ ] 资源缺失时，AI 面板明确引导“先补模型/标签，再继续调参”，不假装可以直接收敛

## 本轮不做

- [ ] 不新增多模型分流
- [ ] 不新增二审模型
- [ ] 不把业务真值交给 AI 自动猜测：`ExpectedLabels`、`ExpectedCount`、`ModelPath`、`LabelsPath`

## 与线序闭环的边界

- 线序 v1 自动调参白名单只允许：
  - `BoxNms.ScoreThreshold`
  - `BoxNms.IouThreshold`
- `DeepLearning.Confidence` 仅保留低置信度地板语义
- `DetectionSequenceJudge.MinConfidence` 固定为 `0.0`

## 参考入口

- [线序检测闭环下一步清单](C:/Users/11234/Desktop/ClearVision/docs/active/线序检测闭环下一步清单.md)
- [AI闭环待复核总览](C:/Users/11234/Desktop/ClearVision/docs/needs-review/AI闭环待复核总览.md)
