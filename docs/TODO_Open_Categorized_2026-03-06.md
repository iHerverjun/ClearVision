# 当前开放待办分类清单（2026-03-06）

## 分类标签

- `TYPE-A`：真实未完成（代码与验收证据不足，或文档明确仍有开放项）
- `TYPE-B`：主要已实现，但文档未更新（状态滞后）
- `TYPE-C`：规划型待立项（研究/设计文档，不应按即时完成率硬判）

> 更新说明（2026-03-06）：本轮已完成 `TYPE-B` 中“文档状态同步债务”的回填，原第 6 类从开放待办转为已关闭记录；当前开放项优先关注第 1-5 类。

---

## 1) 发布与历史尾项

| 文档 | 分类 | 当前判断 | 备注 |
|---|---|---|---|
| `docs/completed_phases/TODO_Old.md` | TYPE-A | 部分完成（历史修复已闭环） | 已回填 S6-P0/P1；发布最小闭环仍保留 |
| `docs/roadmaps/roadmap-sprint6.md` | TYPE-A | 进行中 | 代码实现已大于文档状态，仍需统一验收闭环 |


### 1.1 历史 backlog 明细（由 `TODO_Old.md` 迁入）

| 来源 ID | 类型 | 当前归类 | 备注 |
|---|---|---|---|
| `S6-P2-001` | 历史优化 | Backlog | `InvokeRequired` 模式现代化，不阻塞当前发布 |
| `S6-P2-002` | 并发优化 | Backlog | `ImageAcquisitionService` 锁策略优化，属于性能后续项 |
| `S7-005` | 流程编辑器增强 | Backlog | 撤销 / 重做 |
| `S7-006` | 流程编辑器增强 | Backlog | 复制 / 粘贴节点 |
| `S7-007` | 流程编辑器增强 | Backlog | 框选多节点 |
| `S7-008` | 流程编辑器增强 | Backlog | 工程自动保存 |
| `S7-009` | 流程编辑器增强 | Backlog | 工程导入 / 导出 |
---

## 2) PLC 通信稳定性治理

| 文档 | 分类 | 当前判断 | 备注 |
|---|---|---|---|
| `docs/plans/plan-plc-communication.md` | TYPE-B + TYPE-A | 架构已大幅落地，但清单未闭环 | `Acme.PlcComm` 与算子侧关键实现在位，文档任务仍未回填 |
| `docs/PLC_Communication_Audit_Roadmap_2026-02-27.md` | TYPE-B + TYPE-A | P0 多项已修，P1/P2 仍需收口 | PLC 子集测试通过 18/18，但文档的 16 项未勾选 |

建议：先把“已修项”回填关闭，再把确实未完成项单独拆出新 TODO，避免持续误报。

---

## 3) AI 模型接入与编排平台化

| 文档 | 分类 | 当前判断 | 备注 |
|---|---|---|---|
| `docs/AI_Model_Integration_Refactor_Plan_2026-02-28.md` | TYPE-B + TYPE-A | 部分落地 | `AI/Runtime/*` 与连接器文件已在，但阶段 DoD 和迁移验收未文档化关闭 |
| `docs/roadmaps/roadmap-sprint6.md`（LLM 部分） | TYPE-A | 待收口 | Sprint6 目标存在实现痕迹，但缺正式验收闭环 |

---

## 4) 用户系统与权限管理

| 文档 | 分类 | 当前判断 | 备注 |
|---|---|---|---|
| `docs/UserSystem_Plan.md` | TYPE-B + TYPE-A | 主要功能已落地，仍需统一验收 | 核心实体/服务/API/登录页面已存在；前端组织与原计划文件级拆分有差异 |

建议：按“后端已完成 / 前端交互验收 / 文档回填”拆成三条可执行任务。

---

## 5) 系统缺陷与性能整改

| 文档 | 分类 | 当前判断 | 备注 |
|---|---|---|---|
| `docs/BUG_AUDIT_2026-03-04.md` | TYPE-A | 待整改 | 缺陷报告性质，需按优先级落地修复 |
| `docs/performance_audit_2026-02-28.md` | TYPE-A | 待整改 | 性能隐患已识别，仍需逐项实施与回归 |
| `docs/audits/SYSTEM_CONFIG_AUDIT_2026-02-27.md` | TYPE-A | 待整改 | 系统配置功能存在“可保存但不生效”等问题 |

---

## 6) 文档状态同步债务（本轮已关闭，留档）

| 文档 | 分类 | 当前判断 | 备注 |
|---|---|---|---|
| `docs/completed_phases/TODO_Phase2_DevGuide.md` | TYPE-B | 已实现，状态未更新 | 关键测试文件与全量测试结果支持完成 |
| `docs/completed_phases/TODO_Phase3_DevGuide.md` | TYPE-B | 已实现，状态未更新 | CameraCalibration/CodeRecognition 证据在位 |
| `docs/completed_phases/TODO_Phase4_DevGuide.md` | TYPE-B | 已实现，状态未更新 | PLC 健壮性代码与测试在位 |
| `docs/completed_phases/TODO_Phase5_DevGuide.md` | TYPE-B | 已实现，状态未更新 | 前端 `file` 参数与集成测试在位 |
| `docs/TODO_OperatorLibrary_AlgorithmAudit.md` | TYPE-B | 内容闭环，状态未更新 | 7 批审计报告与勘误已完成 |
| `docs/quality_ai_evolution_todo.md` | TYPE-B | 主要任务已落地，状态未更新 | Sprint7 相关实现与测试已存在 |
| `docs/plans/plan-ocr-native-dependency.md` | TYPE-B | OCR 测试通过，状态未更新 | OCR 过滤测试通过 7/7 |
| `Acme.Product/tests/Acme.Product.Tests/Sprint1_Acceptance_Checklist.md` | TYPE-B | 内容已完成，勾选未回填 | 文档正文与勾选冲突 |
| `Acme.Product/tests/Acme.Product.Tests/Sprint2_Acceptance_Checklist.md` | TYPE-B | 内容已完成，勾选未回填 | 文档正文与勾选冲突 |
| `Acme.Product/tests/Acme.Product.Tests/Sprint3_Acceptance_Checklist.md` | TYPE-B | 标题“已完成”，审计块“进行中” | 状态字段冲突 |
| `Acme.Product/tests/Acme.Product.Tests/Sprint4_Acceptance_Checklist.md` | TYPE-B | 标题“已完成”，勾选未全闭环 | 状态字段冲突 |
| `docs/roadmaps/roadmap-main.md` | TYPE-B | 自述“已完成”与自动状态冲突 | 建议统一单一状态来源 |

---

## 7) 规划与研究文档（暂不作为当前待办缺陷）

| 文档 | 分类 | 当前判断 | 备注 |
|---|---|---|---|
| `docs/plans/plan-operator-enhancement.md` | TYPE-C | 研究规划 | 算法对标文档，宜按里程碑拆分后执行 |
| `docs/plans/plan-v3-migration.md` | TYPE-C | 迁移规划 | 指导性强，但不等于当前迭代待办 |

---

## 建议优先级（仅供排期）

1. `P0`：先处理 TYPE-A 中的缺陷整改与发布尾项（第 1、5 类）。
2. `P1`：处理 PLC/AI 的“部分完成 + 文档未闭环”项（第 2、3 类）。
3. `P2`：集中做一次文档状态同步（第 6 类），减少后续误判与重复审计成本。


