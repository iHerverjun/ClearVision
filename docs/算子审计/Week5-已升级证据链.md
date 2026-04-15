---
title: "Week5 已升级证据链"
doc_type: "evidence-ledger"
status: "active"
topic: "算子审计"
created: "2026-04-14"
updated: "2026-04-15"
---

# Week5 已升级证据链

## 1. Week5 冻结规则

| 状态 | 结论 | 证据路径 | 复现方式 | 阻塞项 | 负责人 | 截止日期 |
| --- | --- | --- | --- | --- | --- | --- |
| [x] | Week5 证据链只承接 `P1 历史收口复核` 与 `P2 专项能力快审` 中需要形成“脚本/测试 -> 结果产物 -> 文档结论”映射的事项。 | `docs/算子审计/启动计划/Week5-长时启动计划.md`；`docs/算子审计/Week5-审计台账.md` | 以 `Week5-长时启动计划.md` 的工作块 D2/D4 为唯一执行入口。 | 无 | Week5 主执行责任位 | 2026-05-15 |
| [x] | AI 检测外部阻塞继续以 `Week5-入口卡片.md` 为单独跟踪入口，不并入本文件的新证据绑定范围。 | `docs/算子审计/Week5-入口卡片.md`；`docs/算子审计/启动计划/Week5-长时启动计划.md` | 对照 `Week5-入口卡片.md` 的责任链与本文件第 2-4 节边界。 | 工业签收未闭环，故不写入本文件的新分类闭环结论。 | C线负责人（证据与复核） | 2026-05-15 |

## 2. P1 历史收口复核包

| 类别 | 当前口径 | 文档证据 | 自动化/回归入口 | 当前结论 | 缺口 |
| --- | --- | --- | --- | --- | --- |
| `Region` | 历史交付文档与当前仓库实现一致；Week5 已按当前口径完成复核。 | `docs/closed/阶段性完成报告/阶段4.2完成报告.md`；`docs/closed/对标Halcon算子深化-0316/阶段4.2-区域处理与测量专题.md` | `scripts/run-tests-phase42-regression.ps1`；`scripts/run-dotnet-test-serial.ps1`（`Phase42RegionProcessingOperatorTests`） | [x] Week5 D2 复核完成：区域布尔运算链路与 Region 数据结构交付证据在当前仓库仍成立。 | 无新增阻塞。 |
| `Morphology` | 历史交付文档与当前仓库实现一致；Week5 已按当前口径完成复核。 | `docs/closed/阶段性完成报告/阶段4.2完成报告.md`；`docs/closed/对标Halcon算子深化-0316/阶段4.2-区域处理与测量专题.md` | `scripts/run-tests-phase42-regression.ps1`；`scripts/run-dotnet-test-serial.ps1`（`Phase42RegionProcessingOperatorTests`） | [x] Week5 D2 复核完成：区域级腐蚀/膨胀/开/闭/骨架链路当前可复现，且与历史交付物一致。 | 无新增阻塞。 |
| `Frequency` | 历史交付文档可定位，FFT/filter/inverse FFT 主链当前可复现；2026-04-15 已补精度/性能实验室实测落点并回写算子名片。 | `docs/closed/阶段性完成报告/阶段4.2完成报告.md`；`docs/closed/对标Halcon算子深化-0316/阶段4.2-区域处理与测量专题.md`；`算子资料/算子名片/FFT1D.md`；`算子资料/算子名片/FrequencyFilter.md`；`算子资料/算子名片/InverseFFT1D.md`；`test_results/week5-lab-audit-closure-20260415.md` | `scripts/run-tests-phase42-regression.ps1`；`scripts/run-dotnet-test-serial.ps1`（`Phase42MeasurementAndSignalOperatorTests`） | [x] Week5 D2/D3 收口完成：`FFT1D -> FrequencyFilter -> InverseFFT1D` 主链当前批次通过，且已补 `FftAndInverseFft_ShouldReconstructBinAlignedSignalWithinTolerance` 与 `FrequencyOperators_LabBudget1024PointChain_ShouldStayWithinBudgetAndAttenuateHighFrequency` 两条实验室精度/性能证据。 | 无新增阻塞。 |

## 3. 回归脚本 -> 结果产物 -> 验收结论 映射表

| 类别 | 回归入口 | 结果产物 | 文档结论落点 | 当前状态 | 备注 |
| --- | --- | --- | --- | --- | --- |
| `Region` | `scripts/run-tests-phase42-regression.ps1`；`scripts/run-dotnet-test-serial.ps1` + `Phase42RegionProcessingOperatorTests` | `test_results/phase42-regression-20260413-115751.trx`；`test_results/week5-d2-phase42-current.trx` | 本文件第 2 节 + `Week5-审计台账.md` | [x] | 当前批次覆盖 `RegionBooleanOperators_ShouldProduceConsistentAreas`。 |
| `Morphology` | `scripts/run-tests-phase42-regression.ps1`；`scripts/run-dotnet-test-serial.ps1` + `Phase42RegionProcessingOperatorTests` | `test_results/phase42-regression-20260413-115751.trx`；`test_results/week5-d2-phase42-current.trx` | 本文件第 2 节 + `Week5-审计台账.md` | [x] | 当前批次覆盖 `RegionErosion`、`RegionDilation`、`RegionOpening`、`RegionClosing`、`RegionSkeleton`。 |
| `Frequency` | `scripts/run-tests-phase42-regression.ps1`；`scripts/run-dotnet-test-serial.ps1` + `Phase42MeasurementAndSignalOperatorTests` | `test_results/phase42-regression-20260413-115751.trx`；`test_results/week5-d2-phase42-current.trx`；`test_results/week5-lab-audit-closure-20260415.trx` | 本文件第 2 节 + `Week5-审计台账.md` | [x] | 当前批次覆盖 `FftFilterAndInverseFft_ShouldPreserveSignalLength`、`FftAndInverseFft_ShouldReconstructBinAlignedSignalWithinTolerance`、`FrequencyOperators_LabBudget1024PointChain_ShouldStayWithinBudgetAndAttenuateHighFrequency`。 |
| `Texture` | `scripts/run-dotnet-test-serial.ps1` + `GlcmTextureOperatorTests,LawsTextureFilterOperatorTests` | `test_results/week5-lab-audit-closure-20260415.trx` | 本文件第 4 节 + `Week5-审计台账.md` | [x] | 已形成“不入池”结论。 |
| `Analysis` | `scripts/run-dotnet-test-serial.ps1` + `DistanceTransformOperatorTests` | `test_results/week5-lab-audit-closure-20260415.trx` | 本文件第 4 节 + `Week5-审计台账.md` | [x] | 已形成“不入池”结论。 |
| `AI Inspection` | `scripts/run-dotnet-test-serial.ps1` + `DetectionSequenceJudgeOperatorTests` | `test_results/week5-lab-audit-closure-20260415.trx` | 本文件第 4 节 + `Week5-审计台账.md` | [x] | 已形成“不入池”结论。 |

## 4. P2 专项能力快审包

| 类别 | 当前口径 | 文档证据 | 自动化/回归入口 | 当前结论 | 缺口 |
| --- | --- | --- | --- | --- | --- |
| `Texture` | Laws/GLCM 两类纹理特征在实验室样本下具备直接测试和专项性能验收入口。 | `算子资料/算子名片/LawsTextureFilter.md`；`算子资料/算子名片/GlcmTexture.md` | `scripts/run-dotnet-test-serial.ps1`（`GlcmTextureOperatorTests,LawsTextureFilterOperatorTests`）；`Acme.Product/tests/Acme.Product.Tests/Integration/Phase2SpecializedPerformanceAcceptanceTests.cs` | [x] 不入池 | 当前实验室口径下已形成最小复现与性能边界，无新增需 Week6 承接的问题。 |
| `Analysis` | `DistanceTransform` 具备直接测试入口，当前实验室口径下无新增可信度阻塞。 | `算子资料/算子名片/DistanceTransform.md` | `scripts/run-dotnet-test-serial.ps1`（`DistanceTransformOperatorTests`） | [x] 不入池 | 保留为稳定能力项。 |
| `AI Inspection` | `DetectionSequenceJudge` 已有直接测试和场景模板绑定，当前实验室口径下无新增规则边界问题。 | `算子资料/算子名片/DetectionSequenceJudge.md` | `scripts/run-dotnet-test-serial.ps1`（`DetectionSequenceJudgeOperatorTests`） | [x] 不入池 | 场景专项能力当前稳定。 |

## 5. 当前缺口

- `Region`、`Morphology` 已完成 Week5 D2 当前口径复核，当前无新增阻塞。
- `Frequency` 的精度/性能缺口已于 2026-04-15 补齐并转 `[x]`。
- `Texture`、`Analysis`、`AI Inspection` 的专项能力快审已完成，当前形成“不入池”结论。
- D2 执行记录显示：完整 restore 路径在当前沙箱环境下长时间停留在“确定要还原的项目”，因此当前批次采用 `-NoBuild -NoRestore` 在已有构建产物上完成复核；日志见 `test_results/week5-d2-phase42-run.log`、`test_results/week5-d2-phase42-run-nobuild.log`。

## 6. 周内执行记录

| 日期 | 计划项 | 当前状态 | 备注 |
| --- | --- | --- | --- |
| 2026-04-14（周二） | Week5 按用户指令启动，创建证据链骨架 | [x] | 已于 2026-04-14 09:56（UTC+8）创建本文件，并冻结 Week5 证据边界。 |
| 2026-04-14（周二） | D2 完成：P1 历史收口复核 | [x] | 已定位阶段4.2 历史交付文档、现有测试类和当前定向批次；`Region`、`Morphology` 转 `[x]`，`Frequency` 明确维持 `[-]`。当前批次：`test_results/week5-d2-phase42-current.trx`（13/13 通过）。 |
| 2026-04-14（周二） | D4 预备执行：P2 专项能力快审 | [x] | 已于 2026-04-15 完成 `Texture`、`Analysis`、`AI Inspection` 的最小复现入口绑定与三选一结论。 |
| 2026-04-15（周三） | D3/D4 实验室收口批次 | [x] | `test_results/week5-lab-audit-closure-20260415.trx` 覆盖 P1/P2 代表性测试类，共 `298/298` 通过。 |
