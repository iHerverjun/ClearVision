---
title: "Week1 已升级证据链"
doc_type: "evidence-ledger"
status: "completed"
topic: "算子审计"
created: "2026-04-13"
updated: "2026-04-13"
---

# Week1 已升级证据链

## 1. 冻结规则

| 状态 | 结论 | 证据路径 | 复现方式 | 阻塞项 | 负责人 | 截止日期 |
| --- | --- | --- | --- | --- | --- | --- |
| [x] | Week1 C线引用规范冻结为“仓库真实路径 + 行号级定位 + 可执行回归入口”。若历史文档路径与仓库现状不一致，以仓库真实路径为准。 | `docs/算子审计/审计执行标准.md:17-24`；`docs/算子审计/审计执行标准.md:61-70`；`docs/算子审计/TODO.md:60-72` | 任一结论至少绑定 1 条文档/源码证据和 1 条脚本/测试入口；无统一脚本时回落到 `run-dotnet-test-serial.ps1`。 | 无 | C线负责人（证据与复核） | 2026-04-13 |
| [x] | Week1 内“已升级”状态不直接等于可转 `[x]`；只有当“文档证据 + 回归入口 + 结果产物”三者均绑定后，才允许在周五验收口径中转为已核验结论。 | `docs/算子审计/审计执行标准.md`；`docs/算子审计/启动计划/Week1-长时启动计划.md` | 检查本文件第 2-4 节是否同时具备证据源、回归入口和结果产物槽位。 | 无 | C线负责人（证据与复核） | 2026-04-13 |

## 2. 已升级类别证据源索引（Week1 本批次已绑定）

| 类别 | 当前口径 | 文档证据 | 自动化/回归入口 | 当前结论 | 缺口 |
| --- | --- | --- | --- | --- | --- |
| 检测 | 历史上已完成机制层收口，Week1 已补“当前口径的脚本/产物/结论”映射。 | `docs/active/检测类算子工业级提升计划-W7W8-最终落地.md:12-93`；`docs/closed/阶段性完成报告/检测类算子工业级提升计划-W7W8收口完成报告.md:10-52` | `scripts/run-tests-detection-regression.ps1` | [x] 已绑定 `test_results/detection-all-20260413-115808.trx`，本批次 `187/187` 通过。 | 无 |
| 识别 | 识别类已补统一周级入口，并在脚本内固化 `.dotnet/.nuget/packages` 原生依赖搜索回退。 | `算子资料/算子名片/CodeRecognition.md:1-82`；`算子资料/算子名片/OcrRecognition.md:1-62` | `scripts/run-tests-recognition-regression.ps1` | [x] 已绑定 `test_results/recognition-regression-20260413-120340.trx`，本批次 `11/11` 通过。 | 无 |
| 标定 | 标定类已统一 `CalibrationBundleV2` 口径，Week1 已补到“脚本/产物/结论”一体化链路。 | `算子资料/算子手册.md:1662-1692`；`算子资料/算子名片/CATALOG.md:166-180` | `scripts/run-tests-calibration-regression.ps1 -Gate all` | [x] 已绑定 `test_results/calibration-all-20260413-115732.trx`，本批次 `63/63` 通过。 | 无 |

## 3. 升级中复核包

| 类别 | 复核结论 | 证据路径 | 复现方式 | 阻塞项 | 负责人 | 截止日期 |
| --- | --- | --- | --- | --- | --- | --- |
| 匹配定位（8） | [x] 已完成当前口径复核，并绑定本周 phase42 批次结果；本轮不进入 Week2 缺陷池。 | `docs/算子审计/TODO.md:35-36`；`算子资料/算子名片/CATALOG.md:107-117`；`test_results/phase42-regression-20260413-115751.trx` | `scripts/run-tests-phase42-regression.ps1`；`Acme.Product/tests/Acme.Product.Tests/Operators/PlanarMatchingOperatorTests.cs:1-275`；`Acme.Product/tests/Acme.Product.Tests/Operators/LocalDeformableMatchingPhase42Tests.cs:1-103` | 无 | C线负责人（证据与复核） | 2026-04-13 |
| AI检测（6） | [-] 已绑定本周 `detection-all` 批次结果并通过，但仍缺工业验收签收与模型/阈值一致性说明，暂不转 `[x]`。 | `docs/算子审计/TODO.md:35-36`；`算子资料/算子名片/CATALOG.md:62-70`；`test_results/detection-all-20260413-115808.trx` | `scripts/run-tests-detection-regression.ps1 -Gate all`；`Acme.Product/tests/Acme.Product.Tests/Operators/AnomalyDetectionOperatorTests.cs:1-249`；`Acme.Product/tests/Acme.Product.Tests/Operators/DeepLearningOperatorTests.cs:1-599`；`Acme.Product/tests/Acme.Product.Tests/Operators/SurfaceDefectDetectionOperatorTests.cs:1-81` | 待补工业验收签收记录、模型版本/阈值一致性说明。 | C线负责人（证据与复核） | 2026-04-17 |

## 4. 回归脚本 -> 结果产物 -> 文档结论 映射表（本周已执行）

| 类别 | 回归入口 | 结果产物 | 文档结论落点 | 当前状态 | 备注 |
| --- | --- | --- | --- | --- | --- |
| 检测 | `scripts/run-tests-detection-regression.ps1 -Gate all` | `test_results/detection-all-20260413-115808.trx` | 本文件第 2 节“检测” + `Week1-审计周报.md` | [x] | 已覆盖回归/准确率/稳定性批次。 |
| 识别 | `scripts/run-tests-recognition-regression.ps1` | `test_results/recognition-regression-20260413-120340.trx` | 本文件第 2 节“识别” + `Week1-审计周报.md` | [x] | 脚本已固化 OCR 原生依赖包目录回退。 |
| 标定 | `scripts/run-tests-calibration-regression.ps1 -Gate all` | `test_results/calibration-all-20260413-115732.trx` | 本文件第 2 节“标定” + `Week1-审计周报.md` | [x] | `HandEyeCalibrationValidator` 覆盖并入 `HandEyeCalibrationOperatorTests`。 |
| 匹配定位复核 | `scripts/run-tests-phase42-regression.ps1` | `test_results/phase42-regression-20260413-115751.trx` | 本文件第 3 节“匹配定位（8）” + `Week1-审计周报.md` | [x] | 当前批次 `57/57` 通过。 |
| AI检测复核 | `scripts/run-tests-detection-regression.ps1 -Gate all` | `test_results/detection-all-20260413-115808.trx` | 本文件第 3 节“AI检测（6）” + `Week1-审计周报.md` | [-] | 仍需同步工业签收、模型版本和阈值说明。 |

## 5. 当前缺口

- AI检测当前只完成了“脚本 -> 结果产物 -> 文档结论”绑定，仍缺工业验收签收记录与模型/阈值一致性说明。
- `HandEyeCalibrationValidator` 不再视为缺口；其验证覆盖已并入 `Acme.Product/tests/Acme.Product.Tests/Operators/HandEyeCalibrationOperatorTests.cs`。

## 6. 周内执行记录

| 日期 | 计划项 | 当前状态 | 备注 |
| --- | --- | --- | --- |
| 2026-04-13（周一） | 检测 / 识别 / 标定证据源索引首版 | [x] | 已于 2026-04-13 提前产出首版。 |
| 2026-04-13（周一） | 补齐识别/标定统一回归脚本 | [x] | 新增 `run-tests-recognition-regression.ps1`、`run-tests-calibration-regression.ps1`。 |
| 2026-04-13（周一） | 绑定本周批次结果产物 | [x] | 已生成 `recognition`、`calibration`、`phase42`、`detection` 四批 `trx` 结果。 |
