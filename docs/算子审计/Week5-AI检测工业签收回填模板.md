---
title: "Week5 AI检测工业签收回填模板"
doc_type: "handoff-template"
status: "active"
topic: "算子审计"
created: "2026-04-15"
updated: "2026-04-15"
---

# Week5 AI检测工业签收回填模板

## 1. 使用说明

- 本模板只承接 `docs/算子审计/Week5-入口卡片.md` 中的唯一外部阻塞，不替代 `Week2-已升级证据链.md` 的历史基线。
- 回填顺序固定为：`签收记录 -> 附件索引 -> 生产模型唯一标识 -> TRX 绑定 -> 复核结论`。
- 在签收附件未到位前，不重开新的 AI 检测批次，也不把 AI 检测状态转为 `[x]`。
- 当前阶段该模板仅用于保留后续真实现场阶段入口，不再阻塞当前实验室闭环判断。

## 2. 冻结批次信息

| 字段 | 当前值 |
| --- | --- |
| 冻结批次 | `test_results/detection-all-20260413-115808.trx` |
| 回归入口 | `scripts/run-tests-detection-regression.ps1 -Gate all` |
| 当前结论 | `继续阻塞 [-]` |
| 阻塞原因 | 工业签收实录未闭环，签收附件中的生产模型唯一标识映射缺失 |
| 当前不触发补跑原因 | 现有 `.trx` 与模型/阈值/环境可得证据一致，缺口不属于结果冲突 |

## 3. 工业签收记录

| 字段 | 待回填值 |
| --- | --- |
| 签收人 | |
| 签收时间（UTC+8） | |
| 签收范围 | |
| 签收结论 | |
| 附件索引 | |
| 现场/业务责任位 | |

## 4. 附件索引

| 附件编号 | 文件名 / 截图号 | 页码 / 截图区间 | 内容摘要 | 当前状态 |
| --- | --- | --- | --- | --- |
| A1 | | | 工业签收记录 | [ ] |
| A2 | | | 生产模型唯一标识映射 | [ ] |
| A3 | | | 现场阈值 / 环境补充说明（如有） | [ ] |

## 5. 生产模型唯一标识 -> TRX 绑定表

| 算子 | 当前仓库已知模型/链路口径 | 当前仓库已知阈值/环境口径 | 对应 TRX / 测试入口 | 签收附件中的生产模型唯一标识 | 附件定位 | 当前状态 |
| --- | --- | --- | --- | --- | --- | --- |
| `AnomalyDetection` | `ModelId=patchcore_demo_bank`；`EmbeddingModelId=anomaly_embedding_identity_2x2` | `Threshold=0.15（测试）/ 默认0.35`；环境基线见 `test_results/week4-detection-environment-20260413-193155.txt` | `Acme.Product.Tests.Operators.AnomalyDetectionOperatorTests.ExecuteAsync_TrainMode_ShouldPersistFeatureBankAndReturnPreviewOutputs`；`...ExecuteAsync_InferenceModeWithModelId_ShouldResolveFeatureBankFromCatalog`；`...ExecuteAsync_InferenceMode_ShouldDetectInjectedDefect` | | | [ ] |
| `DeepLearning` | 测试口径为 `ModelPath + ModelVersion=Auto/YOLOv5/v6/v8/v11`；生产模型唯一标识待附件补齐 | `Confidence=0.05`；`BoxNms IoU=0.45`；`Score=0.25`；环境基线同上 | `Acme.Product.Tests.Operators.DeepLearningOperatorTests.ExecuteAsync_WithDifferentYoloVersions_ShouldAcceptAllFormats(...)`；`...ExecuteAsync_WithNamedTargetClassesAndBundledLabels_ShouldContinuePastLabelResolution` | | | [ ] |
| `DualModalVoting` | 非独立模型推理算子，继承上游模型链路 | `ConfidenceThreshold=0.5` | 当前仓库落点：`scripts/run-tests-detection-regression.ps1 -Gate all` + `Acme.Product/tests/Acme.Product.Tests/Operators/DualModalVotingOperatorTests.cs` | | | [ ] |
| `EdgePairDefect` | 非独立模型推理算子，继承上游检测链路 | 继承上游阈值口径 | `Acme.Product.Tests.Operators.EdgePairDefectOperatorTests.ExecuteAsync_WithDifferentSampleCounts_ShouldKeepDefectSegmentCountStable`；`...ExecuteAsync_WithProvidedParallelLines_ShouldHaveZeroDefects` | | | [ ] |
| `SemanticSegmentation` | `ModelId=semantic_identity_2x2`；`catalog version=1.0.0` | 环境基线同上 | 当前仓库落点：`Acme.Product/tests/Acme.Product.Tests/Operators/SemanticSegmentationOperatorTests.cs`；若签收附件到位后发现批次不一致，再回查 `detection-all-20260413-115808.trx` 与脚本入口 | | | [ ] |
| `SurfaceDefectDetection` | 非独立模型推理算子，继承上游模型链路 | `Threshold=10.0 / 20.0（测试）` | `Acme.Product.Tests.Operators.SurfaceDefectDetectionOperatorTests.ExecuteAsync_ReferenceDiffMode_ShouldDetectDefect`；`...ExecuteAsync_WithShiftedReference_ShouldExposeAlignmentDiagnostics` | | | [ ] |

## 6. 回填完成后的同步动作

1. 先更新本文件第 3-5 节，补齐签收记录、附件索引和生产模型唯一标识映射。
2. 再同步更新三处历史文档：`docs/算子审计/Week3-修复优先级清单.md`、`docs/算子审计/Week2-已升级证据链.md`、`docs/算子审计/Week2-审计周报.md`。
3. 最后由 `Week5-入口卡片.md` 持有人复核是否可解除 `继续阻塞 [-]`。

## 7. 当前仓库复核结论（2026-04-15，UTC+8）

- 已重新扫描 `docs/`、`test_results/`、`models/`，当前仍未发现可直接绑定到本批次的工业签收 PDF/图片或附件索引。
- 批次、模型、阈值和环境可得项仍沿用 `docs/算子审计/Week2-已升级证据链.md` 第 5 节冻结口径。
- 在真实签收附件到位前，本模板只作为唯一回填落点，不改变外部阻塞状态。
