---
title: "检测类PR门禁说明"
doc_type: "overview"
status: "active"
topic: "检测与AI流程门禁"
created: "2026-04-11"
updated: "2026-04-12"
---

# 检测类PR门禁说明

## 说明

- 本文档保留给既有检测与 AI 流程使用。
- 正式 `Measurement` 模块已经迁移到独立门禁链路，详见 [测量类PR门禁说明](./测量类PR门禁说明.md)。
- `detection-accuracy` 与 `detection-stability` 不再作为 Measurement 模块的正式验收入口。

## 现有检测性能门禁

```powershell
& "./scripts/run-tests-detection-performance.ps1" -GateProfile auto -NoBuild -NoRestore -Verbosity minimal
```
