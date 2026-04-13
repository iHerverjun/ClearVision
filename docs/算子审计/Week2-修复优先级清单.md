---
title: "Week2 修复优先级清单"
doc_type: "priority-list"
status: "active"
topic: "算子审计"
created: "2026-04-13"
updated: "2026-04-13"
---

# Week2 修复优先级清单

> 本文是工作块D“风险收敛与 Week2 入池”的正式输出文档，只记录已经确认可入池的问题、阻塞和不入池结论，不再预置候选项或按天排程。

## 1. 入池规则

- 只有已经完成证据核验的问题，才允许写入本清单。
- 每条入池问题必须同时具备：`确认结论 + 证据路径 + 复现方式 + 风险等级 + 负责人 + 截止日期`。
- 仍处于“待确认 / 待补证 / 待实验”的事项，继续留在 `Week1-审计台账.md` 或 `Week1-已升级证据链.md`，不得提前入池。
- 本清单不再维护“候选项”“可能问题”“按天待补项”。

## 2. P0/P1 缺陷池

| 状态 | 风险 | 来源工作块 | 类别 | 项目 | 确认结论 | 证据路径 | 复现方式 | 阻塞项 | 负责人 | 截止日期 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| [x] | P1 | A | 预处理 | 预处理直接单测缺口 | Week2 已补齐 `Filtering`、`Thresholding`、`LaplacianSharpen`、`MorphologicalOperation`、`ImageBlend`、`ImageDiff` 的同名单测，并完成首批串行验证。 | `docs/算子审计/Week2-审计台账.md`；`docs/算子审计/Week2-审计周报.md` | `scripts/run-dotnet-test-serial.ps1` + `FilteringOperatorTests,ThresholdingOperatorTests,LaplacianSharpenOperatorTests,MorphologicalOperationOperatorTests,ImageBlendOperatorTests,ImageDiffOperatorTests` | 无 | A线负责人（算法审计） | 2026-04-20 |
| [x] | P1 | B | 定位/特征提取 | `EdgeDetection` 直接单测缺口 | Week2 已补 `EdgeDetectionOperatorTests`，并完成 `EdgeDetection + ShadingCorrection/SubpixelEdgeDetection/ParallelLineFind/EdgeIntersection` 主链回归批次。 | `docs/算子审计/Week2-审计台账.md`；`test_results/week2-edge-mainchain-20260413-144604.trx` | `scripts/run-dotnet-test-serial.ps1` + `EdgeDetectionOperatorTests,ShadingCorrectionOperatorTests,SubpixelEdgeDetectionOperatorTests,ParallelLineFindOperatorTests,EdgeIntersectionOperatorTests` | 无 | B线负责人（链路稳定性） | 2026-04-20 |

## 3. 阻塞清单

| 状态 | 阻塞类型 | 描述 | 解除动作 | 升级路径 | 负责人 | 截止日期 |
| --- | --- | --- | --- | --- | --- | --- |
| [-] | 验收阻塞 | AI检测已绑定 `test_results/detection-all-20260413-115808.trx`，但仍缺工业验收签收与模型/阈值一致性说明，暂不允许按 `[x]` 进入 Week2 修复闭环。 | 补工业验收签收记录、模型版本、阈值说明，并与 `detection-all` 批次结果绑定。 | C线负责人 -> AI检测 Owner -> Week2 审核人 | C线负责人（证据与复核） | 2026-04-17 |

## 4. 不进 Week2 的已审结项

| 状态 | 来源工作块 | 类别 | 项目 | 不入池原因 | 证据路径 | 负责人 | 备注 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| [x] | C | 标定 | `HandEyeCalibrationValidator` 独立测试文件缺口 | 验证覆盖已并入 `HandEyeCalibrationOperatorTests`，且 `calibration-all-20260413-115732.trx` 本批次 `63/63` 通过。 | `docs/算子审计/Week1-已升级证据链.md`；`test_results/calibration-all-20260413-115732.trx` | C线负责人（证据与复核） | 不单独立项。 |
| [x] | C | 识别/标定 | 统一周级回归脚本缺口 | 已新增 `run-tests-recognition-regression.ps1`、`run-tests-calibration-regression.ps1` 并落地批次结果。 | `docs/算子审计/Week1-已升级证据链.md`；`scripts/run-tests-recognition-regression.ps1`；`scripts/run-tests-calibration-regression.ps1` | C线负责人（证据与复核） | 已完成。 |
| [x] | C | 匹配定位 | 当前批次结果产物绑定 | `phase42-regression-20260413-115751.trx` 本批次 `57/57` 通过，当前口径复核完成。 | `docs/算子审计/Week1-已升级证据链.md`；`test_results/phase42-regression-20260413-115751.trx` | C线负责人（证据与复核） | 不进入 Week2 缺陷池。 |
