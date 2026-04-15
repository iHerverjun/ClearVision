---
title: "Week6 修复优先级清单"
doc_type: "priority-list"
status: "active"
topic: "算子审计"
created: "2026-04-14"
updated: "2026-04-15"
---

# Week6 修复优先级清单

> 本文是 Week5 工作块 D5“风险收敛、正式入池与发布建议”的正式输出文档。2026-04-15 已按实验室闭环口径完成收敛；当前批次无新增需进入 Week6 的 P1/P2 已确认缺陷项，正式结论以下方“不入池项”表为准。

## 1. 入池规则

- 只有完成证据核验的问题，才允许写入本清单。
- 每条入池问题必须同时具备：`确认结论 + 证据路径 + 复现方式 + 风险等级 + 负责人 + 截止日期`。
- 仍处于“待确认 / 待补证 / 待实验”的事项，继续留在 `Week5-审计台账.md` 或 `Week5-已升级证据链.md`，不得提前入池。
- 本清单不维护候选池，不新增 A/B 项。

## 2. P1/P2 缺陷池

- 当前为空。
- 2026-04-15 D5 正式结论：实验室闭环口径下，Week5 未形成新增需进入 Week6 的 P1/P2 已确认缺陷项；正式收敛结果为 `0 条入池 + 多项不入池结论`。

## 3. 阻塞清单

- 当前为空。
- 说明：`Week5-入口卡片.md` 中 AI 检测真实现场签收事项继续单独跟踪，但已移出当前实验室闭环阻塞路径，不写入本清单。

## 4. 不进 Week6 的已审结项

| 状态 | 级别 | 类别 | 正式结论 | 证据路径 | 复现方式 | 负责人 | 截止日期 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| [x] | P1 | `Frequency` | 不入池：主链、精度与性能实验室证据已闭环。 | `docs/算子审计/Week5-已升级证据链.md`；`test_results/week5-lab-audit-closure-20260415.md` | `run-dotnet-test-serial.ps1 + Phase42MeasurementAndSignalOperatorTests` | Week5 主执行责任位 | 2026-04-15 |
| [x] | P1 | `数据处理` | 不入池：代表性数据处理算子直接测试覆盖已完成，当前实验室口径未发现新增缺陷。 | `docs/算子审计/Week5-审计台账.md`；`test_results/week5-lab-audit-closure-20260415.trx` | `run-dotnet-test-serial.ps1 + Aggregator/ArrayIndexer/JsonExtractor/MathOperation/DatabaseWrite/BoxFilter/BoxNms/UnitConvert/PointAlignment/PointCorrection` | Week5 主执行责任位 | 2026-04-15 |
| [x] | P1 | `流程控制` | 不入池：`Comparator / Delay / ConditionalBranch / ForEach / TryCatch / ResultJudgment` 已形成实验室闭环证据。 | `docs/算子审计/Week5-审计台账.md`；`test_results/week5-lab-audit-closure-20260415.trx` | `run-dotnet-test-serial.ps1 + Comparator/Delay/ConditionalBranch/ForEach/TryCatch/ResultJudgment` | Week5 主执行责任位 | 2026-04-15 |
| [x] | P1 | `逻辑工具` | 不入池：`Script / PointSetTool / TextSave / TimerStatistics / TriggerModule` 直接测试稳定。 | `docs/算子审计/Week5-审计台账.md`；`test_results/week5-lab-audit-closure-20260415.trx` | `run-dotnet-test-serial.ps1 + Script/PointSetTool/TextSave/TimerStatistics/TriggerModule` | Week5 主执行责任位 | 2026-04-15 |
| [x] | P1 | `输出` | 不入池：`ImageSave` 契约漂移已修复并复验，`ResultOutput` 当前稳定。 | `Acme.Product/src/Acme.Product.Infrastructure/Operators/ImageSaveOperator.cs`；`test_results/week5-lab-audit-closure-20260415.trx` | `run-dotnet-test-serial.ps1 + ImageSaveOperatorTests,ResultOutputOperatorTests` | Week5 主执行责任位 | 2026-04-15 |
| [x] | P1 | `通信` | 不入池：`HttpRequest / SerialCommunication / Modbus / TCP` 直接测试与现有集成入口均可复现。 | `docs/算子审计/Week5-审计台账.md`；`test_results/week5-lab-audit-closure-20260415.trx` | `run-dotnet-test-serial.ps1 + HttpRequest/SerialCommunication/Modbus/Tcp` | Week5 主执行责任位 | 2026-04-15 |
| [x] | P1 | `采集` | 不入池：`ImageAcquisition` 文件模式和异常路径验证已完成。 | `docs/算子审计/Week5-审计台账.md`；`test_results/week5-lab-audit-closure-20260415.trx` | `run-dotnet-test-serial.ps1 + ImageAcquisitionOperatorTests` | Week5 主执行责任位 | 2026-04-15 |
| [x] | P1 | `3D` | 不入池：6 个 3D 算子代表性测试入口当前实验室口径稳定。 | `docs/算子审计/Week5-审计台账.md`；`test_results/week5-lab-audit-closure-20260415.trx` | `run-dotnet-test-serial.ps1 + EuclideanClusterExtraction/PPFEstimation/PPFMatch/RansacPlaneSegmentation/StatisticalOutlierRemoval/VoxelDownsample` | Week5 主执行责任位 | 2026-04-15 |
| [x] | P2 | `Texture` | 不入池：`GlcmTexture / LawsTextureFilter` 具备直接测试与专项性能验收入口。 | `docs/算子审计/Week5-已升级证据链.md`；`test_results/week5-lab-audit-closure-20260415.trx` | `run-dotnet-test-serial.ps1 + GlcmTextureOperatorTests,LawsTextureFilterOperatorTests` | Week5 主执行责任位 | 2026-04-15 |
| [x] | P2 | `Analysis` | 不入池：`DistanceTransform` 当前实验室口径无新增可信度阻塞。 | `docs/算子审计/Week5-已升级证据链.md`；`test_results/week5-lab-audit-closure-20260415.trx` | `run-dotnet-test-serial.ps1 + DistanceTransformOperatorTests` | Week5 主执行责任位 | 2026-04-15 |
| [x] | P2 | `AI Inspection` | 不入池：`DetectionSequenceJudge` 直接测试稳定，当前实验室口径无新增问题。 | `docs/算子审计/Week5-已升级证据链.md`；`test_results/week5-lab-audit-closure-20260415.trx` | `run-dotnet-test-serial.ps1 + DetectionSequenceJudgeOperatorTests` | Week5 主执行责任位 | 2026-04-15 |
| [x] | P2 | `变量` | 不入池：已补 `VariableRead / VariableWrite / VariableIncrement / CycleCounter` 直接测试。 | `docs/算子审计/Week5-审计台账.md`；`test_results/week5-lab-audit-closure-20260415.trx` | `run-dotnet-test-serial.ps1 + VariableRead/VariableWrite/VariableIncrement/CycleCounter` | Week5 主执行责任位 | 2026-04-15 |
| [x] | P2 | `通用` | 不入池：`LogicGate / Statistics / TypeConvert / StringFormat` 直接测试稳定。 | `docs/算子审计/Week5-审计台账.md`；`test_results/week5-lab-audit-closure-20260415.trx` | `run-dotnet-test-serial.ps1 + LogicGate/Statistics/TypeConvert/StringFormat` | Week5 主执行责任位 | 2026-04-15 |
| [x] | P2 | `辅助` | 不入池：`Comment / RoiManager / RoiTransform` 当前实验室口径稳定。 | `docs/算子审计/Week5-审计台账.md`；`test_results/week5-lab-audit-closure-20260415.trx` | `run-dotnet-test-serial.ps1 + Comment/RoiManager/RoiTransform` | Week5 主执行责任位 | 2026-04-15 |
| [x] | P2 | `拆分组合` | 不入池：`ImageCompose / ImageTiling` 直接测试稳定。 | `docs/算子审计/Week5-审计台账.md`；`test_results/week5-lab-audit-closure-20260415.trx` | `run-dotnet-test-serial.ps1 + ImageComposeOperatorTests,ImageTilingOperatorTests` | Week5 主执行责任位 | 2026-04-15 |

## 5. 执行记录

| 时间（UTC+8） | 动作 | 状态 | 说明 |
| --- | --- | --- | --- |
| 2026-04-14 09:56 | Week5 按用户指令启动，创建 Week6 修复清单骨架 | [x] | 已冻结入池规则：不预置候选池、不把未核验证据问题提前写入。 |
| 2026-04-14 09:56 | D5 前置条件声明 | [x] | 待 D2/D3/D4 完成证据核验后，才允许写入正式 P1/P2 缺陷池、阻塞清单和不入池结论。 |
| 2026-04-15 18:00 | D5 实验室收口结论写入 | [x] | 已完成 `0 条入池 + 15 条不入池结论` 的正式落文档；Week5 周报与 TODO 已同步。 |
