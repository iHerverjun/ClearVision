---
title: "Week5 审计台账"
doc_type: "ledger"
status: "active"
topic: "算子审计"
created: "2026-04-14"
updated: "2026-04-14"
---

# Week5 审计台账

## 1. Week5 范围冻结

| 状态 | 结论 | 证据路径 | 复现方式 | 阻塞项 | 负责人 | 截止日期 |
| --- | --- | --- | --- | --- | --- | --- |
| [x] | Week5 范围冻结为 `P1 历史收口复核`、`P1 主功能审计`、`P1 现场链路审计`、`P2 专项能力快审`、`P2 工程维护快审` 五组，不扩写为并行开发大会战。 | `docs/算子审计/TODO.md`；`docs/算子审计/启动计划/Week5-长时启动计划.md` | 以 `Week5-长时启动计划.md` 的工作块 D1-D5 为唯一执行入口。 | 无 | Week5 主执行责任位 | 2026-05-15 |
| [x] | `Week5-入口卡片.md` 中的 AI 检测单条外部阻塞继续并行跟踪，但不内化为 P1/P2 新的内部修复范围。 | `docs/算子审计/Week5-入口卡片.md`；`docs/算子审计/启动计划/Week5-长时启动计划.md` | 对照本文件第 2 节执行线总览与 `Week5-入口卡片.md` 的责任链边界。 | 工业签收实录仍未闭环，但不阻塞 Week5 D1 开工。 | Week5 主执行责任位 + C线负责人（证据与复核） | 2026-05-15 |

## 2. Week5 执行线总览

| 线别 | 覆盖范围 | 本阶段目标 | 自动回填路径 |
| --- | --- | --- | --- |
| D2 线 | `Region`、`Morphology`、`Frequency` | 复核历史收口证据，决定能否按当前口径转 `[x]` | `docs/算子审计/Week5-审计台账.md`；`docs/算子审计/Week5-已升级证据链.md` |
| D3-A 线 | `数据处理`、`流程控制`、`逻辑工具`、`输出` | 完成主功能静态审计、复现入口绑定和首批问题收敛 | `docs/算子审计/Week5-审计台账.md` |
| D3-B 线 | `通信`、`采集`、`3D` | 完成现场链路、时序一致性、硬件/性能边界审计 | `docs/算子审计/Week5-审计台账.md` |
| D4-A 线 | `Texture`、`Analysis`、`AI Inspection` | 完成专项能力快审与适用边界说明 | `docs/算子审计/Week5-审计台账.md`；`docs/算子审计/Week5-已升级证据链.md` |
| D4-B 线 | `变量`、`通用`、`辅助`、`拆分组合` | 完成工程维护性快审、组合稳定性与接口复用审计 | `docs/算子审计/Week5-审计台账.md` |

## 3. D1 启动确认映射（2026-04-14 09:56，UTC+8）

| 分组 | 分类 | 当前状态 | 当前结论 | 下一输出件 |
| --- | --- | --- | --- | --- |
| P1-A | `Region`、`Morphology`、`Frequency` | [x] | Week5 D2 已完成：`Region`、`Morphology` 转 `[x]`；`Frequency` 明确维持 `[-]`，待补当前口径下的精度/性能实测落点。 | `Week5-已升级证据链.md` |
| P1-B | `数据处理`、`流程控制`、`逻辑工具`、`输出` | [-] | D3 首轮静态盘点已启动：实现已全部定位，但已发现 `流程控制 / Flow Control` 分类拆分、`TryCatch` 测试缺口和 `ImageSave` 独立执行测试缺口。 | `Week5-审计台账.md` |
| P1-C | `通信`、`采集`、`3D` | [-] | D3 首轮静态盘点已启动：`通信 / Communication` 分类拆分已确认，`SerialCommunication` 测试缺口显性化；`采集`、`3D` 的实现与基础测试入口整体较完整。 | `Week5-审计台账.md` |
| P2-A | `Texture`、`Analysis`、`AI Inspection` | [ ] | 已纳入 Week5 专项能力快审组，待形成最小复现与适用边界结论。 | `Week5-审计台账.md`；`Week5-已升级证据链.md` |
| P2-B | `变量`、`通用`、`辅助`、`拆分组合` | [ ] | 已纳入 Week5 工程维护快审组，待形成维护性问题清单和不入池结论。 | `Week5-审计台账.md` |

## 4. P1-A 历史收口复核待回填区

| 类别 | 当前状态 | 复核重点 | 证据目标 | 备注 |
| --- | --- | --- | --- | --- |
| `Region` | [x] | 历史交付与当前布尔运算主链是否仍可复现 | `docs/closed/阶段性完成报告/阶段4.2完成报告.md` + `test_results/week5-d2-phase42-current.trx` | D2 已完成复核；`RegionBooleanOperators_ShouldProduceConsistentAreas` 当前批次通过。 |
| `Morphology` | [x] | 区域级腐蚀/膨胀/开/闭/骨架链路是否仍可复现 | `docs/closed/阶段性完成报告/阶段4.2完成报告.md` + `test_results/week5-d2-phase42-current.trx` | D2 已完成复核；`RegionErosion/Dilation/Opening/Closing/Skeleton` 当前批次通过。 |
| `Frequency` | [-] | 频域主链可复现性与精度/性能证据是否同时成立 | `docs/closed/阶段性完成报告/阶段4.2完成报告.md` + `test_results/week5-d2-phase42-current.trx` | D2 已完成复核；当前仅确认 `FFT1D -> FrequencyFilter -> InverseFFT1D` 主链可复现，仍待补当前口径下的精度/性能实测落点。 |

## 5. P1-B / P1-C 主审计待回填区

| 类别 | 当前状态 | 主审计重点 | 最小输出 | 备注 |
| --- | --- | --- | --- | --- |
| `数据处理` | [-] | 结果可信度、可追溯性、异常输入收口 | 实现审计 + 最小复现入口 + 风险分级 | 10/10 实现可定位，整体测试入口较完整；下一步聚焦 `DatabaseWrite` 等外部依赖算子的异常路径与可追溯性。 |
| `流程控制` | [-] | 分支切换、失败收口、异常路径可控性 | 实现审计 + 最小复现入口 + 风险分级 | 已确认分类口径拆分：`ResultJudgment` 归在 `Flow Control`；已新增 `TryCatchOperatorTests`、`ResultJudgmentOperatorTests` 作为直接测试补位，但当前测试项目构建受无关集成依赖阻塞，尚未完成回归确认。 |
| `逻辑工具` | [-] | 业务分支正确性、条件判断退化场景 | 实现审计 + 最小复现入口 + 风险分级 | `ScriptOperator` 源文件已定位；`PointSetTool`、`TextSave`、`TimerStatistics`、`TriggerModule` 均有直接测试入口。 |
| `输出` | [-] | 对外契约、发布签收、系统对接稳定性 | 实现审计 + 最小复现入口 + 风险分级 | `ResultOutput` 有较完整直接测试；`ImageSave` 已确认存在参数/输出契约漂移（`Directory/FileNameTemplate/Quality` vs `FolderPath/FileName/JpegQuality`，`IsSuccess` vs `Success`），本轮已补兼容修复与 `ImageSaveOperatorTests`，待项目构建阻塞解除后完成回归确认。 |
| `通信` | [-] | 现场联动、协议失败路径、重试与超时 | 链路审计 + 最小复现入口 + 风险分级 | 已确认分类口径拆分：`MitsubishiMcCommunication` 归在 `Communication`；`SerialCommunication` 无测试命中，`HttpRequest` 缺直接测试，`Omron/Mitsubishi` 以集成测试为主。 |
| `采集` | [-] | 入口稳定性、时序一致性、数据源切换 | 链路审计 + 最小复现入口 + 风险分级 | `ImageAcquisition` 实现与测试入口较多；下一步聚焦硬件离线、时序漂移和降级路径。 |
| `3D` | [-] | 精度预算、性能预算、硬件依赖边界 | 链路审计 + 最小复现入口 + 风险分级 | 6/6 实现与 operator/pointcloud/integration tests 均可定位；下一步聚焦性能预算与 acceptance 对齐。 |

## 6. P2 快审待回填区

| 类别 | 当前状态 | 快审重点 | 预期结论类型 | 备注 |
| --- | --- | --- | --- | --- |
| `Texture` | [ ] | 专项能力边界、适用场景、可替代性 | 入池 / 不入池 / 延后 | P2 专项能力组。 |
| `Analysis` | [ ] | 专项分析输出可信度与依赖说明 | 入池 / 不入池 / 延后 | P2 专项能力组。 |
| `AI Inspection` | [ ] | 场景专项能力、模型/规则边界 | 入池 / 不入池 / 延后 | P2 专项能力组。 |
| `变量` | [ ] | 参数可维护性、默认值风险、配置边界 | 入池 / 不入池 / 延后 | P2 工程维护组。 |
| `通用` | [ ] | 通用工具复用边界、异常收口 | 入池 / 不入池 / 延后 | P2 工程维护组。 |
| `辅助` | [ ] | 辅助类算子的调用边界与退化影响 | 入池 / 不入池 / 延后 | P2 工程维护组。 |
| `拆分组合` | [ ] | 组合稳定性、上下游契约、拼装退化风险 | 入池 / 不入池 / 延后 | P2 工程维护组。 |

## 7. 周内回填进度

| 日期 | 计划动作 | 当前状态 | 备注 |
| --- | --- | --- | --- |
| 2026-04-14（周二） | 按用户指令启动 Week5，完成 D1 范围冻结与结果骨架创建 | [x] | 已于 2026-04-14 09:56（UTC+8）完成 `Week5-审计台账.md`、`Week5-已升级证据链.md`、`Week6-修复优先级清单.md`、`Week5-审计周报.md` 4 份结果文档骨架创建。 |
| 2026-04-14（周二） | 绑定 Week5 执行边界与 AI 检测外部阻塞关系 | [x] | 已确认 AI 检测外部阻塞继续并行跟踪，但不并入 P1/P2 内部修复范围。 |
| 2026-04-14（周二） | 完成 D2：P1 历史收口复核 | [x] | 已完成历史交付文档、现有测试类与当前定向批次重绑；`Region`、`Morphology` 转 `[x]`，`Frequency` 维持 `[-]`。当前定向批次：`test_results/week5-d2-phase42-current.trx`（13/13 通过）。 |
| 2026-04-14（周二） | 启动 D3：P1 主功能与现场链路主审计（静态首版） | [-] | 已完成实现/测试入口盘点，并显性化 2 类高信号问题：`流程控制 / 通信` 分类拆分，以及 `TryCatch`、`SerialCommunication`、`ImageSave` 等测试覆盖缺口。 |
| 2026-04-14（周二） | D3 定向补位：输出/流程控制高信号缺口 | [-] | 已补 `ImageSaveOperator` 契约兼容修复，并新增 `TryCatchOperatorTests`、`ResultJudgmentOperatorTests`、`ImageSaveOperatorTests`；进一步定位确认：测试项目 `csproj` 与本机全局包缓存均已包含 `Microsoft.Data.SqlClient` / `MySqlConnector`，当前阻塞点是 `Acme.Product.Tests.csproj` 的 restore graph 无法刷新 `obj/project.assets.json`，导致编译仍使用 2026-03-26 的旧 assets。 |
| 2026-04-14（周二） | D3 阻塞绕行实验：SDK9 | [-] | 已临时切换到 `9.0.301` 工具链验证；restore/build 阻塞未解除，说明问题不只是 SDK10 行为差异。实验后已恢复 `global.json` 原配置。 |
