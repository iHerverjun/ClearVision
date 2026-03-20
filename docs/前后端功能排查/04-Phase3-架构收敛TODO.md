# Phase 3 - 架构收敛 TODO

## 阶段目标

- 合并重复入口、重复协议与历史残留实现。
- 清理已经确认废弃的前端残留代码与多余交互路径。
- 把本次排查结果固化为长期可维护的功能矩阵和准入规则。

## 当前状态总览

- 回填日期：2026-03-20
- 核查方式：静态代码与现有文档核查，未启动程序、未执行接口请求。
- 阶段判断：已开始收敛
- 统计：2 项已完成，4 项部分完成
- 主要阻塞：
  - 标定前端主入口已收敛，但旧 HTTP 协议、旧向导文件和兼容路径仍未正式退场。
  - 旧结果面板主调用链已绕开，但残留函数、容器和数据流尚未彻底清理。
  - `aiGenerationDialog.js` 虽不再是主入口，但文件、import 和文档残留仍在。
  - 功能矩阵和新增功能准入规则已补成文档资产，但还需要在后续改动中真正执行。

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

- 状态：部分完成
- 来源：报告“建议顺手清理”第 1 条
- 判断：
  - 实时检测主链路已经优先走现代 `resultPanel.addResult(...)`，并绕开旧右侧结果面板 DOM 写入。
  - 但旧函数、旧容器和相关残留代码尚未完全删除，仓库内仍存在继续误导维护者的历史实现。
- 证据：
  - [`index.html`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/index.html#L259-L260)
  - [`app.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L510-L520)
  - [`app.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L524-L524)
  - [`app.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L731-L740)
- 主要缺口：
  - 需要继续移除旧 DOM 写入函数、无效容器和死代码，并确认结果页只剩一套容器和一套渲染链路。

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
  - 结果页已经开始优先调用服务端分析/报告接口，工程切换时也会主动重置结果上下文。
  - 历史分页已统一到服务端契约，结果页翻页不再走前端二次分页。
  - 但当前仍同时保留“前端内存结果数组”和“后端分析结果”两套来源，统计/趋势/导出/清空还未完全围绕单一正式数据流。
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

- 状态：已完成
- 来源：本次排查整体输出
- 判断：
  - 已新增独立文档 [`06-功能矩阵与新增功能准入规则.md`](./06-功能矩阵与新增功能准入规则.md)，可直接查看“页面入口 -> 主协议/接口 -> 当前状态 -> 是否允许继续扩兼容分支”。
  - 当前缺口已从“没有矩阵”切换为“后续改动能否持续维护矩阵”。
- 证据：
  - [`06-功能矩阵与新增功能准入规则.md`](./06-功能矩阵与新增功能准入规则.md)
- 主要缺口：
  - 需要在后续功能改动中把矩阵维护当成强制同步动作，而不是一次性补文档。

### 6. `[联调/流程]` 建立新增功能准入规则

- 状态：已完成
- 来源：本次排查整体结论
- 判断：
  - 已在 [`06-功能矩阵与新增功能准入规则.md`](./06-功能矩阵与新增功能准入规则.md) 中补齐主入口、主协议、真实状态、回归验证和文档同步规则。
  - 当前缺口已从“没有规则”收敛为“执行层面是否真正按规则评审和回填”。
- 证据：
  - [`06-功能矩阵与新增功能准入规则.md`](./06-功能矩阵与新增功能准入规则.md)
- 主要缺口：
  - 需要把该规则纳入实际评审流程，否则仍会回到“补完文档但不执行”的状态。

## 阶段完成标准

- 标定、结果页、AI 相关历史残留实现完成收敛。
- 废弃模块、旧容器、双轨路径被真正移除或进入明确退场流程。
- 本目录中的 TODO 文档从“整改清单”升级为“长期维护基线”。

## 当前对照

- 标定、结果页、AI 相关历史残留实现完成收敛：未达成（其中标定已完成主入口收敛）
- 废弃模块、旧容器、双轨路径被真正移除或进入明确退场流程：未达成
- TODO 文档升级为长期维护基线：部分达成（已补联调基线、功能矩阵和准入规则，仍待后续持续执行）

