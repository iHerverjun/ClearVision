---
title: "检测类PR门禁说明"
doc_type: "overview"
status: "active"
topic: "检测类算子工业化"
created: "2026-04-11"
updated: "2026-04-11"
---

# 检测类PR门禁说明

## 目的

统一检测类（15 算子）PR 合入门槛，确保变更在精度、稳定性、性能、回归四个维度可验收、可追溯。

## 适用范围

适用于以下算子相关代码、测试、参数契约与门禁脚本改动：

- `AngleMeasurement`
- `CaliperTool`
- `CircleMeasurement`
- `ContourMeasurement`
- `GapMeasurement`
- `GeoMeasurement`
- `GeometricTolerance`
- `HistogramAnalysis`
- `LineLineDistance`
- `LineMeasurement`
- `MeasureDistance`
- `PixelStatistics`
- `PointLineDistance`
- `SharpnessEvaluation`
- `WidthMeasurement`

## 必过门禁

1. 检测专项回归（兼容历史门禁）必须通过：
```powershell
& "./scripts/run-tests-detection-regression.ps1" -Gate regression -NoBuild -NoRestore -Verbosity minimal
```

2. 检测精度门禁（`detection-accuracy`）必须通过：
```powershell
& "./scripts/run-tests-detection-regression.ps1" -Gate detection-accuracy -NoBuild -NoRestore -Verbosity minimal
```

3. 检测稳定性门禁（`detection-stability`）必须通过：
```powershell
& "./scripts/run-tests-detection-regression.ps1" -Gate detection-stability -NoBuild -NoRestore -Verbosity minimal
```

4. 检测性能预算门禁必须通过：
```powershell
& "./scripts/run-tests-detection-performance.ps1" -GateProfile auto -NoBuild -NoRestore -Verbosity minimal
```

> 说明：当前 `detection-accuracy` / `detection-stability` 先复用现有测试集合入口，后续可按专项测试覆盖率逐步拆分。

## 性能阈值档位策略（1.5 -> 1.2）

- `standard`：`scale=1.5`（常规开发期）
- `acceptance`：`scale=1.2`（验收周/发布收口）
- `auto`：自动选择
  - 若显式配置 `CV_DETECTION_PERF_GATE_PROFILE=standard|acceptance`，按显式值
  - 若当前分支为 `main` 或 `v*` tag，自动收紧到 `acceptance(1.2)`
  - 若配置 `CV_DETECTION_PERF_TIGHTEN_FROM_UTC` 且当前时间到点，自动收紧到 `acceptance(1.2)`
  - 其他情况使用 `standard(1.5)`

## 失败报告归档规则

检测性能门禁失败时，自动归档到：

- `Acme.Product/test_results/archive/detection_performance_failures/<run-id>/`

归档至少包含：

- `detection_performance_budget_report.md`
- `detection_performance_budget_report.json`
- `failure_summary.md`
- `failure_manifest.json`

## CI 与产物

CI 会执行：

- `Run Detection Regression Gate`
- `Run Detection Accuracy Gate`
- `Run Detection Stability Gate`
- `Run Detection Performance Gate`

并上传：

- `Acme.Product/test_results/detection_performance_budget_report.md`
- `Acme.Product/test_results/detection_performance_budget_report.json`
- `Acme.Product/test_results/archive/detection_performance_failures/**`

## PR 提交要求

提交检测类 PR 时，描述区必须包含：

- 影响算子范围（至少列出算子名）
- 回归门禁结果（通过/失败）
- 精度门禁结果（`detection-accuracy`）
- 稳定性门禁结果（`detection-stability`）
- 性能门禁结果（含 `GateProfile` 与 `Budget Scale`）
- 四报告状态（已更新模板/已产出正式报告）
- 若存在未达项：遗留清单与风险说明

## 不满足门禁时的处理

- 任一门禁失败：PR 不得合入。
- 仅允许修复后重跑，不允许通过“降标准”绕过门禁。
- 若需临时豁免，必须记录审批人与到期时间，并在后续 PR 补回。
