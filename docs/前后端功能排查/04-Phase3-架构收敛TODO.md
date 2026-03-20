# Phase 3 - 架构收敛 TODO

## 阶段目标

- 合并重复入口、重复协议与历史残留实现。
- 清理已经确认废弃的前端残留代码与多余交互路径。
- 把本次排查结果固化为长期可维护的功能矩阵和准入规则。

## 当前状态总览

- 回填日期：2026-03-20
- 核查方式：静态代码与现有文档核查，未启动程序、未执行接口请求。
- 阶段判断：已开始收敛
- 统计：3 项已完成，3 项部分完成
- 主要阻塞：
  - 标定前端主入口已收敛，旧 `calibrationWizard.js` 也已改成明确废弃占位，但旧 HTTP 协议和兼容路径仍未正式退场。
  - 旧结果面板主调用链已继续收缩，但残留容器与少量历史样式/结构尚未彻底清理。
  - 结果页已显式标注“当前页本地筛选”语义，但统计/趋势/导出/清空还未完全围绕单一正式数据流。
  - 功能矩阵和新增功能准入规则已补成文档资产，但还需要在后续改动中真正执行。

## 状态清单

### 1. `[前后端]` 完成标定双轨收敛

- 状态：部分完成
- 来源：报告 D.1，关联报告 A.2
- 判断：
  - 顶部旧“标定”入口已从 `index.html` 下线，首页工具栏不再继续挂载旧的 `CalibrationWizard`。
  - `index.html` 里对旧 `calibrationWizard.js` 的直接加载也已移除，设置页中的 `HandEyeCalibWizard` 成为唯一公开主入口。
  - 旧 `calibrationWizard.js` 已被收敛成明确废弃占位，不再继续注入 DOM、绑定全局事件或伪装成可用入口。
  - 但 HTTP `/api/calibration/solve|save` 兼容协议仍在仓库中，WebMessage `handeye:solve|save` 也仍并行存在，因此目前完成了“入口收敛 + 废弃显式化”，还没完成“协议退场”。
- 证据：
  - [`index.html`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/index.html)
  - [`calibrationWizard.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/calibration/calibrationWizard.js#L1-L11)
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
  - `app.js` 中旧的 `updateResultsPanel(...)` DOM 写入函数已清理，实时结果更新只再走检测面板与 `ResultPanel` 两条正式链路。
  - 但 inspection 视图中的历史容器命名和少量样式残留仍在，仓库内还没有彻底完成“只剩一套结果语义”的收尾。
- 证据：
  - [`index.html`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/index.html#L259-L260)
  - [`app.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L510-L520)
  - [`app.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L524-L524)
  - [`app.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js)
- 主要缺口：
  - 需要继续清理 inspection 侧历史容器命名和相关样式残留，并确认结果页只剩一套可维护的数据语义。

### 3. `[前端]` 归档或删除已废弃的 AI 生成对话框模块

- 状态：已完成
- 来源：报告“建议顺手清理”第 2 条
- 判断：
  - 主用户入口已经切到 AI 面板，不再实例化旧对话框。
  - `aiGenerationDialog.js` 已改成明确废弃占位，任何继续实例化旧对话框的代码都会立即暴露错误，而不是悄悄保留伪入口。
  - `app.js` 中已不再保留旧对话框 import/实例化链路，AI 面板成为唯一前端生成入口。
- 证据：
  - [`aiGenerationDialog.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/ai-generation/aiGenerationDialog.js#L1-L8)
- 主要缺口：
  - 后续可在确认没有历史引用后彻底删除文件；当前阶段已完成“公开入口退场”和“废弃显式化”。

### 4. `[前后端]` 完成结果页数据流单一化

- 状态：部分完成
- 来源：报告 C.4，关联报告 B.2、B.3
- 判断：
  - 结果页已经开始优先调用服务端分析/报告接口，工程切换时也会主动重置结果上下文。
  - 历史分页已统一到服务端契约，结果页翻页不再走前端二次分页。
  - 当用户在服务端分页结果上使用本地状态/缺陷筛选时，界面现在会明确提示“仅筛选当前已加载页”，并暂停分页，避免误判成全量历史过滤。
  - 但当前仍同时保留“前端内存结果数组”和“后端分析结果”两套来源，统计/趋势/导出/清空还未完全围绕单一正式数据流。
- 证据：
  - [`app.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L689-L716)
  - [`resultPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L165-L201)
  - [`resultPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L294-L305)
  - [`resultPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L430-L438)
  - [`resultPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L756-L772)
  - [`resultPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L888-L1008)
  - [`Program.cs`](../../Acme.Product/src/Acme.Product.Desktop/Program.cs#L253-L272)
- 主要缺口：
  - 需要继续把分页、统计、趋势、报告、导出和清空动作统一到同一套正式数据流；本轮先完成了“语义去误导”，还没完成“数据源彻底单一化”。

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

- 标定、结果页、AI 相关历史残留实现完成收敛：部分达成（AI 旧对话框已退场显式化，标定与结果页仍有协议/容器残留）
- 废弃模块、旧容器、双轨路径被真正移除或进入明确退场流程：部分达成（旧前端入口已显式废弃，但后端兼容协议与少量结构残留仍待收尾）
- TODO 文档升级为长期维护基线：部分达成（已补联调基线、功能矩阵和准入规则，仍待后续持续执行）

