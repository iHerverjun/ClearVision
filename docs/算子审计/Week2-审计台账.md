---
title: "Week2 审计台账"
doc_type: "ledger"
status: "active"
topic: "算子审计"
created: "2026-04-13"
updated: "2026-04-15"
---

# Week2 审计台账

## 1. Week2 范围冻结

| 状态 | 结论 | 证据路径 | 复现方式 | 阻塞项 | 负责人 | 截止日期 |
| --- | --- | --- | --- | --- | --- | --- |
| [x] | Week2 修复执行范围冻结为 `预处理直接单测缺口`、`EdgeDetection 直接单测缺口` 和 `AI检测验收补证` 三条闭环线。 | `docs/算子审计/Week2-修复优先级清单.md`；`docs/算子审计/Week1-审计周报.md` | 以 `Week2-长时启动计划.md` 的工作块 A/B/C 为唯一执行入口。 | 无 | A/B/C 线负责人 | 2026-04-20 |
| [x] | Week2 目标冻结为“修复已确认缺口并验证回归”，不重新打开 Week1 已审结且不入池的事项。 | `docs/算子审计/启动计划/Week2-长时启动计划.md`；`docs/算子审计/Week2-修复优先级清单.md` | 对照 Week2 清单的“P0/P1 缺陷池 / 阻塞清单 / 不进 Week2 的已审结项”三部分。 | 无 | A/B/C 线负责人 | 2026-04-20 |

## 2. Week2 执行线总览

| 线别 | 修复范围 | 本周目标 | 自动回填路径 |
| --- | --- | --- | --- |
| A线 | `Filtering`、`Thresholding`、`LaplacianSharpen`、`MorphologicalOperation`、`ImageBlend`、`ImageDiff` | 补直接单测并绑定批次回归结果 | `docs/算子审计/Week2-审计台账.md` |
| B线 | `EdgeDetection` + 边缘主链 | 补直接单测并完成边缘主链回归复核 | `docs/算子审计/Week2-审计台账.md` |
| C线 | `AI检测(6)` 验收阻塞 | 补齐签收、模型、阈值、环境和批次结果闭环 | `docs/算子审计/Week2-已升级证据链.md` |

## 3. 启动确认映射（2026-04-13 12:52（UTC+8））

| 审计口径 | 当前实现类 | 当前测试入口 | 启动结论 |
| --- | --- | --- | --- |
| `Filtering` | `GaussianBlurOperator` | `Acme.Product/tests/Acme.Product.Tests/Operators/OperatorTests.cs` 中的 `GaussianBlurOperatorTests` | 已确认存在实现与基础测试入口，但仍缺按审计口径命名/收口的直单测闭环。 |
| `Thresholding` | `ThresholdOperator` | `Acme.Product/tests/Acme.Product.Tests/Operators/OperatorTests.cs` 中的 `ThresholdOperatorTests` | 已确认存在实现与基础测试入口，但仍需按 Week2 口径补批次绑定与同名收口。 |
| `LaplacianSharpen` | `LaplacianSharpenOperator` | 暂未发现独立直单测文件 | 已确认实现入口，Week2 需要新补直单测。 |
| `MorphologicalOperation` | `MorphologicalOperationOperator` | `Phase42MeasurementAndSignalOperatorTests` 中仅有联动覆盖 | 已确认实现入口，Week2 需要独立直单测收口。 |
| `ImageBlend` | `ImageBlendOperator` | 暂未发现独立直单测文件 | 已确认实现入口，Week2 需要新补直单测。 |
| `ImageDiff` | `ImageDiffOperator` | 暂未发现独立直单测文件 | 已确认实现入口，Week2 需要新补直单测。 |
| `EdgeDetection` | `CannyEdgeOperator` | `Acme.Product/tests/Acme.Product.Tests/Operators/OperatorTests.cs` 中的 `CannyEdgeOperatorTests` | 已确认审计口径与实现命名不一致，Week2 需要补 `EdgeDetection` 审计口径直单测并与主链回归绑定。 |

## 4. A线待回填区（预处理直接单测）

| 算子 | 当前状态 | 预期补齐内容 | 回归入口 | 备注 |
| --- | --- | --- | --- | --- |
| `Filtering` | [x] | `正常 / 边界 / 异常` 直接单测 | `scripts/run-dotnet-test-serial.ps1` | 已新增 `FilteringOperatorTests.cs`，并纳入 2026-04-13 首批串行验证（32/32 通过）。 |
| `Thresholding` | [x] | `正常 / 边界 / 异常` 直接单测 | `scripts/run-dotnet-test-serial.ps1` | 已新增 `ThresholdingOperatorTests.cs`，并纳入 2026-04-13 首批串行验证（32/32 通过）。 |
| `LaplacianSharpen` | [x] | `正常 / 边界 / 异常` 直接单测 | `scripts/run-dotnet-test-serial.ps1` | 已新增 `LaplacianSharpenOperatorTests.cs`，并纳入 2026-04-13 首批串行验证（32/32 通过）。 |
| `MorphologicalOperation` | [x] | `正常 / 边界 / 异常` 直接单测 | `scripts/run-dotnet-test-serial.ps1` | 已新增 `MorphologicalOperationOperatorTests.cs`，并纳入 2026-04-13 首批串行验证（32/32 通过）。 |
| `ImageBlend` | [x] | `正常 / 边界 / 异常` 直接单测 | `scripts/run-dotnet-test-serial.ps1` | 已新增 `ImageBlendOperatorTests.cs`，并纳入 2026-04-13 首批串行验证（32/32 通过）。 |
| `ImageDiff` | [x] | `正常 / 边界 / 异常` 直接单测 | `scripts/run-dotnet-test-serial.ps1` | 已新增 `ImageDiffOperatorTests.cs`，并纳入 2026-04-13 首批串行验证（32/32 通过）。 |

## 5. B线待回填区（边缘主链）

| 项目 | 当前状态 | 预期补齐内容 | 回归入口 | 备注 |
| --- | --- | --- | --- | --- |
| `EdgeDetection` 直接单测 | [x] | `正常 / 阈值临界 / 噪声异常` 直接单测 | `scripts/run-dotnet-test-serial.ps1` | 已新增 `EdgeDetectionOperatorTests.cs`，并纳入 2026-04-13 首批串行验证（32/32 通过）。 |
| 边缘主链回归 | [x] | `ShadingCorrection -> EdgeDetection/SubpixelEdgeDetection -> ParallelLineFind/EdgeIntersection` 批次回归 | `scripts/run-dotnet-test-serial.ps1` | 已于 2026-04-13 14:46（UTC+8）完成串行复跑并绑定 `test_results/week2-edge-mainchain-20260413-144604.trx`（18/18 通过）。 |

## 6. 周内回填进度

| 日期 | 计划动作 | 当前状态 | 备注 |
| --- | --- | --- | --- |
| 2026-04-13（周一） | Week2 提前启动、冻结 A/B/C 输入范围、确认实现/测试映射 | [x] | 已于 2026-04-13 12:52（UTC+8）启动；A/B 口径映射已冻结，C 线沿用 Week1 `detection-all` 批次作为起点。 |
| 2026-04-13（周一） | A/B 首批审计口径直单测补齐并完成串行验证 | [x] | 已于 2026-04-13 13:54（UTC+8）完成首批验证：`FilteringOperatorTests`、`ThresholdingOperatorTests`、`LaplacianSharpenOperatorTests`、`MorphologicalOperationOperatorTests`、`ImageBlendOperatorTests`、`ImageDiffOperatorTests`、`EdgeDetectionOperatorTests` 共 `32` 个用例全部通过。当前环境并发 MSBuild/restore path walk 不稳定，先用 `dotnet build Acme.Product/tests/Acme.Product.Tests/Acme.Product.Tests.csproj --no-restore /m:1` 单核构建，再用 `scripts/run-dotnet-test-serial.ps1 -NoBuild -NoRestore` 完成验证。 |
| 2026-04-13（周一） | B 线边缘主链回归复跑并绑定 Week2 批次结果 | [x] | 已于 2026-04-13 14:46（UTC+8）完成 `EdgeDetectionOperatorTests`、`ShadingCorrectionOperatorTests`、`SubpixelEdgeDetectionOperatorTests`、`ParallelLineFindOperatorTests`、`EdgeIntersectionOperatorTests` 共 `18` 个用例全部通过，并绑定 `test_results/week2-edge-mainchain-20260413-144604.trx`。 |
| 2026-04-13（周一） | C 线 AI检测验收补证收敛为 1 条外部阻塞 | [-] | 已冻结 Week1 `detection-all-20260413-115808.trx` 为初始批次；当前仅保留 AI检测 Owner 补齐工业签收、模型版本、阈值和环境一致性材料这一条外部验收阻塞。 |
| 2026-04-13（周一） | A/B 风险收敛并生成 Week3 移交结论 | [x] | A/B 缺口已闭环，不新增 Week3 修复项；仅保留 AI 检测验收阻塞进入 `Week3-修复优先级清单.md`。 |
| 2026-04-13（周一） | Week2 周验收和移交完成 | [x] | `Week2-审计周报.md` 已同步 A/B 完成、C 阻塞保留和 Week3 入口状态。 |
| 2026-04-15（周三） | 实验室口径补充收口 P0 类别级闭环 | [x] | `test_results/week2-p0-lab-closure-20260415.trx` 覆盖 `预处理 / 定位 / 特征提取 / 图像处理 / 颜色处理` 代表性测试类，共 `174/174` 通过；上述 5 类已满足当前实验室闭环口径。 |
