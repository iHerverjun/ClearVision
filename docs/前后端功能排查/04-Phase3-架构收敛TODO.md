# Phase 3 - 架构收敛 TODO

## 阶段目标

- 合并重复入口、重复协议与历史残留实现。
- 清理已经确认废弃的前端残留代码与多余交互路径。
- 把本次排查结果固化为长期可维护的功能矩阵和准入规则。

## 当前状态总览

- 回填日期：2026-03-20
- 核查方式：静态代码与现有文档核查，未启动程序、未执行接口请求。
- 阶段判断：已开始收敛
- 统计：0 项已完成，3 项部分完成，3 项未完成
- 主要阻塞：
  - 标定前端主入口已收敛，但旧 HTTP 协议、旧向导文件和兼容路径仍未正式退场。
  - 旧结果面板残留逻辑仍在活跃调用链里。
  - `aiGenerationDialog.js` 虽不再是主入口，但文件、import 和文档残留仍在。
  - 功能矩阵和新增功能准入规则尚未形成正式文档资产。

## 状态清单

### 1. `[前后端]` 完成标定双轨收敛

- 状态：部分完成
- 来源：报告 D.1，关联报告 A.2
- 判断：
  - 顶部旧“标定”入口已从 `index.html` 下线，首页工具栏不再继续挂载旧的 `CalibrationWizard`。
  - `index.html` 里对旧 `calibrationWizard.js` 的直接加载也已移除，设置页中的 `HandEyeCalibWizard` 成为唯一公开主入口。
  - 但旧向导文件和 HTTP `/api/calibration/solve|save` 兼容协议仍在仓库中，WebMessage `handeye:solve|save` 也仍并行存在，因此目前只完成了“入口收敛”，还没完成“协议退场”。
- 证据：
  - [`index.html`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/index.html)
  - [`calibrationWizard.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/calibration/calibrationWizard.js#L185-L188)
  - [`calibrationWizard.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/calibration/calibrationWizard.js#L318-L381)
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L1758-L1758)
  - [`handEyeCalibWizard.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/core/calibration/handEyeCalibWizard.js#L410-L470)
  - [`ApiEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/ApiEndpoints.cs#L52-L58)
  - [`WebMessageHandler.cs`](../../Acme.Product/src/Acme.Product.Desktop/Handlers/WebMessageHandler.cs#L247-L251)
- 主要缺口：
  - 还需要决定旧 HTTP 标定端点与旧向导文件是彻底删除、归档还是仅保留内部兼容层；在此之前，Phase 3 仍不能算真正完成。

### 2. `[前端]` 清理旧结果面板残留代码

- 状态：未完成
- 来源：报告“建议顺手清理”第 1 条
- 判断：
  - 旧结果面板残留逻辑不只是“代码还在”，而是仍在活跃调用链中。
  - 实时检测完成后，代码先调用现代 `resultPanel.addResult(...)`，随后又继续执行旧的 `updateResultsPanel(result)`，并直接写入当前真实结果容器。
- 证据：
  - [`index.html`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/index.html#L259-L260)
  - [`app.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L510-L520)
  - [`app.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L524-L524)
  - [`app.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L731-L740)
- 主要缺口：
  - 需要移除旧 DOM 写入逻辑，并确认结果页只剩一套容器和一套渲染链路。

### 3. `[前端]` 归档或删除已废弃的 AI 生成对话框模块

- 状态：部分完成
- 来源：报告“建议顺手清理”第 2 条
- 判断：
  - 主用户入口已经切到 AI 面板，不再实例化旧对话框。
  - 但 `aiGenerationDialog.js` 文件、`app.js` 中的顶层 import/变量，以及相关文档残留仍在。
- 证据：
  - [`app.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L99-L100)
  - [`app.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L124-L126)
  - [`app.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L1276-L1310)
  - [`app.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L1297-L1300)
  - [`aiGenerationDialog.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/ai-generation/aiGenerationDialog.js#L1-L8)
- 主要缺口：
  - 需要彻底删除或归档文件，并同步清理无效 import、变量和文档引用。

### 4. `[前后端]` 完成结果页数据流单一化

- 状态：部分完成
- 来源：报告 C.4，关联报告 B.2、B.3
- 判断：
  - 结果页已经开始调用服务端分析接口。
  - 但当前仍同时保留“前端内存结果数组”和“后端分析结果”两套来源。
  - 统计、趋势、导出、清空等动作仍未完全围绕单一正式数据流。
- 证据：
  - [`app.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L689-L716)
  - [`resultPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L165-L201)
  - [`resultPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L294-L305)
  - [`resultPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L430-L438)
  - [`resultPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L756-L772)
  - [`Program.cs`](../../Acme.Product/src/Acme.Product.Desktop/Program.cs#L253-L272)
- 主要缺口：
  - 需要把分页、统计、趋势、报告、导出和清空动作全部统一到同一套正式数据流。

### 5. `[联调/文档]` 固化前后端功能矩阵

- 状态：未完成
- 来源：本次排查整体输出
- 判断：
  - 现有仓库里能找到 API 表、页面容器说明和代码导读，但还没有形成一份单一、可维护的功能矩阵。
  - 目前还做不到“一眼看清每个页面/入口/按钮对应哪个真实后端接口，以及接口状态/使用方”。
- 证据：
  - [`04-Phase3-架构收敛TODO.md`](./04-Phase3-架构收敛TODO.md)
  - [`guide-codebase-deep-dive.md`](../guides/guide-codebase-deep-dive.md)
  - [`ref-frontend-modification-guide.md`](../reference/ref-frontend-modification-guide.md)
- 主要缺口：
  - 需要新增一份独立功能矩阵文档，而不是继续依赖分散的导读和参考文档。

### 6. `[联调/流程]` 建立新增功能准入规则

- 状态：未完成
- 来源：本次排查整体结论
- 判断：
  - 仓库里没有找到一份面向前后端新增功能 gate 的正式准入规则。
  - 现有只看到更泛化的规划/核对清单，尚不足以约束“真实接口、真实回显、禁用态策略、公开级别、版本兼容”等具体要求。
- 证据：
  - [`04-Phase3-架构收敛TODO.md`](./04-Phase3-架构收敛TODO.md)
  - [`DEVELOPMENT_PLANNING_CONSOLIDATED.md`](../DEVELOPMENT_PLANNING_CONSOLIDATED.md)
  - [`plan-operator-implementation-reconciliation.md`](../archive/legacy_md_pre_merge_20260316/plan-operator-implementation-reconciliation.md)
- 主要缺口：
  - 需要在功能矩阵基础上单独固化准入规则，并把它纳入新增功能评审流程。

## 阶段完成标准

- 标定、结果页、AI 相关历史残留实现完成收敛。
- 废弃模块、旧容器、双轨路径被真正移除或进入明确退场流程。
- 本目录中的 TODO 文档从“整改清单”升级为“长期维护基线”。

## 当前对照

- 标定、结果页、AI 相关历史残留实现完成收敛：未达成（其中标定已完成主入口收敛）
- 废弃模块、旧容器、双轨路径被真正移除或进入明确退场流程：未达成
- TODO 文档升级为长期维护基线：未达成
