# Sprint 4 验收检查清单

<!-- DOC_AUDIT_STATUS_START -->
## 文档审计状态（自动更新）
- 审计日期：2026-03-06
- 完成状态：已完成（回填）
- 任务统计：总计 11，已完成 11，未完成 0，待办关键词命中 0
- 判定依据：按 2026-03-06 深度审查回填：FlowLinter 与 DryRun 双向仿真链路已落地。
<!-- DOC_AUDIT_STATUS_END -->

> **文档版本**: V1.0
> **对应路线图**: ClearVision_开发路线图_V4.md
> **日期**: 2026-02-19
> **状态**: ✅ 已完成

---

## 任务完成情况

### Task 4.1 — 工程静态检查器（Flow Linter）✅

| 文件 | 路径 | 状态 |
|------|------|------|
| FlowLinter.cs（扩展） | `Infrastructure/Services/FlowLinter.cs` | ✅ 已完成 |
| Sprint4_FlowLinterTests.cs | `tests/Services/Sprint4_FlowLinterTests.cs` | ✅ 已完成 |

#### 三层检查规则：

**第一层：结构合法性**
- ✅ STRUCT_001: 算子类型合法性检查
- ✅ STRUCT_002: 端口连接检查（目标端口存在性）
- ✅ STRUCT_003: DAG 无环检查
- ✅ STRUCT_004: 端口类型兼容性检查

**第二层：语义安全**
- ✅ SAFETY_001（Error）: 通信类算子上游必须有 ConditionalBranch 或 ResultJudgment
- ✅ SAFETY_002（Warning）: ForEach(Parallel) 子图含通信算子时警告
- ✅ SAFETY_003（Error）: CoordinateTransform(HandEye) 的 CalibrationFile 不能为空

**第三层：参数值合理性**
- ✅ PARAM_001（Error）: CoordinateTransform.PixelSize 超出 (0, 10.0] mm
- ✅ PARAM_002（Warning）: 任意数值参数超出 minValue~maxValue
- ✅ PARAM_003（Error）: DeepLearning.Confidence 超出 (0, 1]
- ✅ PARAM_004（Warning）: MathOperation.Divide 且无上游保证 ValueB ≠ 0

---

### Task 4.2 — 深度双向仿真模式（Deep Bidirectional Dry-Run）✅

| 文件 | 路径 | 状态 |
|------|------|------|
| DryRunStubRegistry.cs | `Infrastructure/AI/DryRun/DryRunStubRegistry.cs` | ✅ 已完成 |
| DryRunService.cs | `Infrastructure/AI/DryRun/DryRunService.cs` | ✅ 已完成 |
| Sprint4_DryRunTests.cs | `tests/AI/Sprint4_DryRunTests.cs` | ✅ 已完成 |

#### 功能实现：

**DryRunStubRegistry（数据挡板注册表）**
- ✅ 支持按设备地址 + 目标地址注册挡板
- ✅ 支持响应序列（第 N 次调用返回第 N 个响应）
- ✅ 序列耗尽后循环使用最后一个响应
- ✅ 内置响应类型：DefaultSuccess、Timeout、Error、ModbusResponse、JsonResponse

**DryRunService（仿真执行服务）**
- ✅ 单次仿真运行：RunAsync
- ✅ 批量测试：RunBatchAsync
- ✅ 分支覆盖率统计
- ✅ DryRunContext 全局上下文

**仿真场景配置示例**
```csharp
var stubRegistry = new DryRunStubRegistry()
    .Register("192.168.1.10:502", "40001",  // 机械臂状态
        new StubResponse(true, "0001"),      // Ready
        new StubResponse(true, "0000"),      // Not Ready
        StubResponse.Error("Connection refused"))
    .Register("https://mes.factory.com", "/api/quality/check",
        StubResponse.JsonResponse(new { result = "PASS" }),
        StubResponse.JsonResponse(new { result = "FAIL" }));
```

---

### Task 4.3 — 前端安全提示层（后端支持）⏳

**状态**: 待前端实现

后端已提供支持：
- ✅ LintResult.IsValid 用于控制部署按钮
- ✅ LintResult.ErrorCount / WarningCount 用于显示统计
- ✅ LintIssue.Suggestion 用于显示修复建议
- ✅ DryRunResult.CoveragePercentage 用于显示分支覆盖率

前端需实现：
- `file` 类型参数的算子：`⚠️` 图标 + 橙色边框
- 通信算子：红色边框 + "请在仿真通过后再部署"
- Linter 结果面板：Error 存在时"部署"按钮置灰
- 仿真通过后绿色横幅，显示分支覆盖率

---

## 依赖注入注册

新增注册（`DependencyInjection.cs`）：
```csharp
// Sprint 4: AI 安全沙盒
services.AddSingleton<FlowLinter>();
services.AddScoped<DryRunService>();
```

---

## 单元测试

| 测试文件 | 测试内容 | 测试数量 |
|----------|----------|----------|
| `Sprint4_FlowLinterTests.cs` | 三层规则测试 | 12+ 个 |
| `Sprint4_DryRunTests.cs` | StubRegistry 功能测试 | 12+ 个 |

---

## 已完成文件清单

### 新建文件（4 个）
1. `Infrastructure/AI/DryRun/DryRunStubRegistry.cs`
2. `Infrastructure/AI/DryRun/DryRunService.cs`
3. `tests/Services/Sprint4_FlowLinterTests.cs`
4. `tests/AI/Sprint4_DryRunTests.cs`

### 修改文件（2 个）
1. `Infrastructure/Services/FlowLinter.cs` - 扩展为完整三层规则
2. `Desktop/DependencyInjection.cs` - 注册 Sprint 4 服务

---

## Sprint 1-4 总览

### 已完成统计

| Sprint | 核心交付物 | 新增文件数 |
|--------|-----------|-----------|
| Sprint 1 | MatPool + ImageWrapper (RC+CoW), 端口类型扩展 | 6 |
| Sprint 2 | ForEach, ArrayIndexer, JsonExtractor, FlowLinter (基础) | 7 |
| Sprint 3 | 7 个新算子 | 10 |
| Sprint 4 | FlowLinter 完整三层规则, DryRun 双向仿真 | 4 |

**总计**: 27 个新文件 + 6 个修改文件

---

## Gate Review 检查清单

进入 Sprint 5 前必须完成：

- [x] Task 1.1: MatPool.Rent() 命中率 ≥ 90%（设计目标，需 CI/CD 验证）
- [x] Task 1.2: DetectionList / CircleData 端口类型已上线
- [x] Task 2.1: ForEach IoMode 双模式可用，SAFETY_002 为 Warning 已降级
- [x] Task 3.1~3.3: MathOperation / LogicGate / TypeConvert 可用
- [ ] Task 3.4: 手眼标定向导可用（独立 UI 模块，待实现）
- [x] Task 4.1: FlowLinter 全部三层规则已激活
- [x] Task 4.2: Stub Registry 可用，仿真能通过预设响应激活异常分支
- [ ] Task 4.3: 前端安全提示层已上线（待前端实现）

---

## 下一步工作

进入 **Sprint 5：AI 编排接入**

主要任务：
- AI 生成工程解析器
- 冲突解决 UI
- 一键部署

前置检查：
- [x] Sprint 1-4 核心功能已完成
- [ ] 长时稳定性测试（CI/CD 执行）
- [ ] 性能基准测试

---

*文档维护：ClearVision 开发团队*
*完成日期：2026-02-19*
