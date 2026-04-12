---
title: "测量类PR门禁说明"
doc_type: "overview"
status: "active"
topic: "Measurement 模块工业级验收"
created: "2026-04-12"
updated: "2026-04-12"
---

# 测量类PR门禁说明

## 适用范围

正式 `Measurement` 模块的 17 个算子：

- `Measurement`
- `CircleMeasurement`
- `LineMeasurement`
- `ContourMeasurement`
- `AngleMeasurement`
- `GeometricTolerance`
- `GeometricFitting`
- `CaliperTool`
- `WidthMeasurement`
- `PointLineDistance`
- `LineLineDistance`
- `GapMeasurement`
- `GeoMeasurement`
- `SharpnessEvaluation`
- `ColorMeasurement`
- `HistogramAnalysis`
- `PixelStatistics`

## 必过门禁

1. 回归门禁

```powershell
& "./scripts/run-tests-measurement-regression.ps1" -NoBuild -NoRestore -Verbosity minimal
```

2. 精度门禁

```powershell
& "./scripts/run-tests-measurement-accuracy.ps1" -NoBuild -NoRestore -Verbosity minimal
```

3. 稳定性门禁

```powershell
& "./scripts/run-tests-measurement-stability.ps1" -NoBuild -NoRestore -Verbosity minimal
```

4. 性能门禁

```powershell
& "./scripts/run-tests-measurement-performance.ps1" -NoBuild -NoRestore -Verbosity minimal
```

## 结果物

- `Acme.Product/test_results/measurement_performance_budget_report.md`
- `Acme.Product/test_results/measurement_performance_budget_report.json`
- `Acme.Product/test_results/measurement_operator_benchmark_report.md`

## 说明

- `detection-accuracy` 与 `detection-stability` 不再作为 Measurement 模块的正式验收入口。
- 涉及物理单位的结论，必须通过标定链路换算，Measurement 算子默认只输出像素域结果。
