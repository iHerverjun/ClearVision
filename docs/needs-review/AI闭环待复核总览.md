---
title: "AI闭环待复核总览"
doc_type: "overview"
status: "needs-review"
topic: "AI闭环"
created: "2026-03-21"
updated: "2026-03-23"
source_docs:
  - "docs/reference/历史资料/needs-review来源/拆分稿/AI闭环/LLM闭环修复框架.md"
---

# AI闭环待复核总览

## 当前判断

AI 这条线当前不再是“主链有没有闭环”的问题，而是“哪些闭环已经落地，哪些闭环仍在 active 执行中”。

当前可以确认：

- 生成侧结构化闭环已经落地
- 线序场景的 preview / autotune / AI follow-up 闭环仍在 active 执行中

## 已落地的闭环层

- `AiFlowValidator` 已输出结构化诊断
- `AiFlowGenerationService` 已支持定向重试而不是机械重生
- `GenerateFlowResponse` 已回传：
  - `FailureSummary`
  - `LastAttemptDiagnostics`
  - `PendingParameters`
  - `MissingResources`
- AI 面板已支持取消生成、待确认参数和缺失资源展示

这部分已经不是阻塞线序检测的主要问题。

## 仍在 active 执行中的闭环层

真正还没完全收口的是线序专项闭环：

- 节点级 preview 与诊断码收口
- 场景级自动调参
- AI follow-up 只改参数、不改结构

这些工作已转入：

- [线序检测闭环下一步清单](C:/Users/11234/Desktop/ClearVision/docs/active/线序检测闭环下一步清单.md)

## 为什么还留在 needs-review

因为这里剩下的是“归档边界判断”，不是“方案还没写”：

1. 生成侧结构化闭环是否可以单独转入 `closed/`
2. 线序专项闭环是否应继续以 active 专项文档独立推进
3. `AI闭环` 一词后续是否只保留生成主链含义，而不再混入场景调参闭环

在这些归档判断完成前，本文件继续保留在 `needs-review`。
