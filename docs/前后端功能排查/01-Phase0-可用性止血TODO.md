# Phase 0 - 可用性止血 TODO

## 阶段目标

- 先修复会直接导致功能不可用的断链问题。
- 建立统一的 API 路径拼接规则，阻止同类问题继续扩散。
- 让 AI 面板、标定入口、节点预览三条核心链路恢复真实可用。

## 当前状态总览

- 回填日期：2026-03-20
- 核查方式：静态代码与现有测试文件核查，未启动程序、未执行接口请求。
- 阶段判断：基本完成，待一次完整联调回归确认
- 统计：3 项已完成，2 项部分完成
- 主要阻塞：
  - `httpClient` 路径约定已经稳定，但后端告警/自动化测试仍未补齐。
  - Phase 0 三条主链路虽然已接通，但当前证据仍以静态核查和前端语法校验为主，缺一次完整联调回归确认。

## 状态清单

### 1. `[联调]` 统一 `httpClient` 路径约定

- 状态：部分完成
- 来源：报告 A.1、A.2、A.3
- 判断：
  - `httpClient` 已把浏览器环境基础地址统一设为 `.../api`，调用方默认走相对路径。
  - `httpClient` 会主动剥离传入路径中的 `/api` 前缀，运行代码中未再发现 `/api/api/*`。
  - 旧标定和 `preview-node` 等关键调用已改为相对路径。
- 证据：
  - [`httpClient.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/core/messaging/httpClient.js#L73-L74)
  - [`httpClient.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/core/messaging/httpClient.js#L143-L145)
  - [`calibrationWizard.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/calibration/calibrationWizard.js#L318-L318)
  - [`inspectionController.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/inspectionController.js#L413-L413)
- 主要缺口：
  - 未看到后端侧把“客户端自动补 `/api`”沉淀为接口约定或异常路径告警日志。
  - 未看到专门覆盖该路径规约的自动化测试。

### 2. `[前端]` 修复 AI 面板健康检查链路

- 状态：已完成
- 来源：报告 A.1
- 判断：
  - AI 面板连接状态可以反映健康检查结果。
  - AI 面板健康检查语义已统一到 `/api/health`，不再由 `aiPanel.js` 直接打根路径。
  - 当前剩余工作已从“修链路”转为“补联调回归基线”。
- 证据：
  - [`aiPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/ai/aiPanel.js#L226-L236)
  - [`Program.cs`](../../Acme.Product/src/Acme.Product.Desktop/Program.cs#L194-L194)
  - [`ApiEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/ApiEndpoints.cs#L34-L34)
- 主要缺口：
  - 仍需在联调时确认“面板状态变化 -> `/api/health` 响应 -> UI 提示”完整闭环。

### 3. `[前端/联调]` 修复顶部旧标定向导断链

- 状态：已完成
- 来源：报告 A.2
- 判断：
  - 顶部旧入口已从 `index.html` 工具栏下线，用户不再从主工作台直接进入旧 HTTP 标定向导。
  - 旧向导原先已经改为走相对路径 `calibration/solve`、`calibration/save`，因此不存在 `/api/api/calibration/*` 的断链问题。
  - 本阶段按“止血”和“避免继续误导用户”判断，该项已完成；后续真正的协议退场与历史代码清理转入 Phase 1 / Phase 3 跟踪。
- 证据：
  - [`index.html`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/index.html)
  - [`calibrationWizard.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/calibration/calibrationWizard.js#L185-L188)
  - [`calibrationWizard.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/calibration/calibrationWizard.js#L318-L318)
  - [`calibrationWizard.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/calibration/calibrationWizard.js#L381-L381)
  - [`ApiEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/ApiEndpoints.cs#L52-L58)
- 备注：
  - 该项完成仅表示“旧入口不再继续暴露给用户”，不代表后续 Phase 1/Phase 3 的标定双轨收敛已完成。

### 4. `[前端]` 修复节点预览调试链路

- 状态：已完成
- 来源：报告 A.3
- 判断：
  - `preview-node` 后端端点已经落地，前端控制器也具备调用实现，并有端点测试覆盖。
  - 当前主预览面板已优先接入节点级 `preview-node`，失败时再回退旧单算子预览链路。
  - 本阶段的“链路打通”目标已达成，后续保留的旧回退逻辑转入 Phase 3 关注其退场边界。
- 证据：
  - [`PreviewNodeEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/PreviewNodeEndpoints.cs#L35-L136)
  - [`inspectionController.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/inspectionController.js#L400-L426)
  - [`propertyPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/flow-editor/propertyPanel.js#L667-L684)
  - [`previewPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/flow-editor/previewPanel.js#L145-L148)
  - [`PreviewNodeEndpointsTests.cs`](../../Acme.Product/tests/Acme.Product.Desktop.Tests/PreviewNodeEndpointsTests.cs#L20-L40)
- 主要缺口：
  - 仍需联调验证“点击预览 -> 结果回显 -> 回退链路兜底”完整行为。

### 5. `[联调]` 建立 Phase 0 冒烟回归清单

- 状态：已完成
- 来源：由报告 A.1、A.2、A.3 汇总而来
- 判断：
  - 已新增独立文档 [`05-联调回归基线.md`](./05-联调回归基线.md)，把 AI `/api/health`、节点级预览回退、旧标定入口退场、结果页服务端报告、连续运行保护、认证超时/锁定收敛为统一基线。
  - 当前缺口已经从“没有回归清单”收敛为“需要持续按该清单执行并回填结果”。
- 证据：
  - [`05-联调回归基线.md`](./05-联调回归基线.md)
- 主要缺口：
  - 仍需在后续联调中持续把“已执行 / 未执行 / 被阻塞”结果回填到任务记录中。

## 阶段完成标准

- 上述三条断链问题全部关闭。
- 不再存在新增的重复 `/api` 前缀调用。
- 相关冒烟回归清单已沉淀，可供后续阶段复用。

## 当前对照

- 三条断链问题全部关闭：已达成（待一次完整联调回归确认）
- 不再存在新增的重复 `/api` 前缀调用：基本达成
- 冒烟回归清单已沉淀：已达成
