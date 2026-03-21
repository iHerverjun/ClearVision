---
title: "Phase 1 - 契约对齐 TODO"
doc_type: "task-list"
status: "closed"
topic: "前后端功能排查"
created: "2026-03-20"
updated: "2026-03-20"
---
# Phase 1 - 契约对齐 TODO

## 阶段目标

- 统一前后端参数、会话、状态与文案语义。
- 把“看起来可用”但并不真实的页面行为改成真实行为或真实提示。
- 优先让结果页、认证链路、AI 会话与核心状态展示具备可信度。

## 当前状态总览

- 回填日期：2026-03-20
- 核查方式：静态代码与现有测试文件核查，未启动程序、未执行接口请求。
- 阶段判断：已完成
- 统计：10 项已完成
- 主要阻塞：
  - 本阶段范围内的契约对齐已经完成；后续若继续压缩结果页渲染态和历史兼容层，转入 Phase 3 / `08-analysisData` 跟踪。

## 状态清单

### 1. `[联调]` 统一结果历史分页契约

- 状态：已完成
- 来源：报告 A.4
- 判断：
  - 前后端接口参数已经统一为 `pageIndex` / `pageSize`。
  - 结果页已经开始优先消费服务端分析/报告接口，导出也优先走服务端报告。
  - 历史列表现在由后端返回 `items + totalCount + pageIndex + pageSize`，前端翻页直接驱动服务端重新取页。
  - 时间过滤场景也统一走分页查询，不再绕开 `pageIndex` / `pageSize`。
- 证据：
  - [`app.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L689-L691)
  - [`ApiEndpoints.cs`](../../../Acme.Product/src/Acme.Product.Desktop/Endpoints/ApiEndpoints.cs#L237-L240)
  - [`InspectionResultRepository.cs`](../../../Acme.Product/src/Acme.Product.Infrastructure/Repositories/InspectionResultRepository.cs#L21-L27)
  - [`resultPanel.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L23-L27)
  - [`resultPanel.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L400-L400)
  - [`resultPanel.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L661-L663)
  - [`InspectionService.cs`](../../../Acme.Product/src/Acme.Product.Application/Services/InspectionService.cs#L310-L314)
- 主要缺口：
  - 已完成分页契约收口；后续只需继续减少结果页本地缓存态和服务端正式数据流并存的范围。

### 2. `[前后端]` 统一登出语义

- 状态：已完成
- 来源：报告 A.5
- 判断：
  - 前端已补调 `/api/auth/logout`，并在 finally 中清理本地会话并跳转登录页。
  - 后端端点会移除内存 session token。
  - 服务端登出响应现已携带明确审计口径 `Audit = "server-session-cleared"`。
  - 当前在服务端登出失败时，会把“本地已清理、服务端可能仍在线”的提示带到登录页，用户可见反馈已补上。
  - 已存在针对登出端点和服务端会话失效的自动化测试。
- 证据：
  - [`auth.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/auth/auth.js#L88-L102)
  - [`login.html`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/login.html#L116-L152)
  - [`AuthEndpoints.cs`](../../../Acme.Product/src/Acme.Product.Desktop/Endpoints/AuthEndpoints.cs#L47-L55)
  - [`AuthService.cs`](../../../Acme.Product/src/Acme.Product.Application/Services/AuthService.cs#L113-L136)
  - [`AuthServiceTests.cs`](../../../Acme.Product/tests/Acme.Product.Tests/Services/AuthServiceTests.cs#L108-L128)
  - [`AuthEndpointsTests.cs`](../../../Acme.Product/tests/Acme.Product.Desktop.Tests/AuthEndpointsTests.cs#L16-L30)

### 3. `[前后端]` 统一 AI 历史会话可回放结构

- 状态：已完成
- 来源：报告 A.6
- 判断：
  - AI 生成结果持久化时已同时保存 AI 原始结果和可恢复画布的快照结构。
  - 前端会优先恢复 `currentCanvasFlowJson`，缺快照时会给出明确提示。
  - 已看到后端消息入口和回归测试覆盖。
- 证据：
  - [`AiFlowGenerationService.cs`](../../../Acme.Product/src/Acme.Product.Infrastructure/AI/AiFlowGenerationService.cs#L215-L219)
  - [`ConversationalFlowService.cs`](../../../Acme.Product/src/Acme.Product.Infrastructure/AI/ConversationalFlowService.cs#L209-L214)
  - [`ConversationalFlowService.cs`](../../../Acme.Product/src/Acme.Product.Infrastructure/AI/ConversationalFlowService.cs#L413-L413)
  - [`aiPanel.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/ai/aiPanel.js#L1054-L1065)
  - [`aiPanel.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/ai/aiPanel.js#L637-L646)
  - [`WebMessageHandler.cs`](../../../Acme.Product/src/Acme.Product.Desktop/Handlers/WebMessageHandler.cs#L683-L714)
  - [`ConversationalFlowServiceTests.cs`](../../../Acme.Product/tests/Acme.Product.Tests/AI/ConversationalFlowServiceTests.cs#L64-L125)

### 4. `[前后端]` 相机绑定列表只展示真实状态

- 状态：已完成
- 来源：报告 B.1
- 判断：
  - `/api/cameras/bindings` 现已按后端真值返回 `ConnectionStatus`，优先区分 `Connected / Online / Offline / Disabled / Unbound`。
  - 前端新增绑定时已移除硬写 `connectionStatus: 'Unknown'`，保存后会重新回读后端状态。
  - 发现设备弹窗现在会优先消费设备枚举返回的 `ipAddress`；当底层 SDK 能提供真 IP 时，前端已不再回退到伪值。
- 证据：
  - [`SettingsEndpoints.cs`](../../../Acme.Product/src/Acme.Product.Desktop/Endpoints/SettingsEndpoints.cs#L247-L250)
  - [`AppConfig.cs`](../../../Acme.Product/src/Acme.Product.Core/Entities/AppConfig.cs#L179-L216)
  - [`ICamera.cs`](../../../Acme.Product/src/Acme.Product.Core/Cameras/ICamera.cs#L46-L51)
  - [`SettingsEndpoints.cs`](../../../Acme.Product/src/Acme.Product.Desktop/Endpoints/SettingsEndpoints.cs#L326-L331)
  - [`IIndustrialCamera.cs`](../../../Acme.Product/src/Acme.Product.Core/Cameras/IIndustrialCamera.cs#L134-L162)
  - [`SettingsEndpoints.cs`](../../../Acme.Product/src/Acme.Product.Desktop/Endpoints/SettingsEndpoints.cs#L370-L378)
  - [`HikvisionCamera.cs`](../../../Acme.Product/src/Acme.Product.Infrastructure/Cameras/HikvisionCamera.cs#L192-L198)
  - [`MindVisionCamera.cs`](../../../Acme.Product/src/Acme.Product.Infrastructure/Cameras/MindVisionCamera.cs#L105-L113)
  - [`settingsView.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L975-L1006)
  - [`settingsView.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L1045-L1053)
- 备注：
  - 当前 UI 已不再伪造连接状态或伪造 IP；发现接口若拿不到 IP，将保持为空值而非继续造假。

### 5. `[前端]` 删除分析卡片中的演示值回退

- 状态：已完成
- 来源：报告 B.2
- 判断：
  - JS 层已经不再给置信度硬塞默认百分比，缺失时展示缺省态。
  - 原先样式层残留的固定“示例范围”区间也已删除，不再继续给用户制造“这是正式阈值范围”的误解。
- 证据：
  - [`analysisCardsPanel.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/analysisCardsPanel.js)
  - [`analysisCards.css`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/shared/styles/analysisCards.css)

### 6. `[前后端]` 明确“清空记录”的真实语义

- 状态：已完成
- 来源：报告 B.3
- 判断：
  - 按钮文案已改成“清空当前视图”。
  - 确认文案和成功提示都明确说明不会删除后端历史记录。
  - 实际行为也仅清空前端当前视图数据。
- 证据：
  - [`index.html`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/index.html#L639-L643)
  - [`app.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L664-L669)
  - [`resultPanel.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L430-L438)

### 7. `[前端]` 更正“工程已自动保存”提示

- 状态：已完成
- 来源：报告 B.4
- 判断：
  - 自动保存仍然只写入本地 `localStorage` 草稿。
  - 用户可见提示已明确写成“流程草稿已保存到本地缓存”，失败提示也指向本地缓存。
- 证据：
  - [`app.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L1438-L1444)
  - [`app.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L1472-L1482)

### 8. `[前后端]` 补齐普通用户自助改密能力

- 状态：已完成
- 来源：报告 C.1
- 判断：
  - 设置页已经提供普通用户可见的“修改密码”入口，并会调用 `/auth/change-password`。
  - 后端现已具备鉴权、旧密码校验、最小长度校验、密码复用禁止和“大写/小写/数字”复杂度校验。
  - `/api/auth/change-password` 失败时已返回稳定 `ErrorCode`，便于前端和回归脚本识别具体失败原因。
  - 已存在应用层与端点层自动化测试覆盖弱密码、密码复用和成功修改场景。
- 证据：
  - [`settingsView.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L1334-L1335)
  - [`settingsView.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L2227-L2237)
  - [`settingsView.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L1927-L1928)
  - [`AuthEndpoints.cs`](../../../Acme.Product/src/Acme.Product.Desktop/Endpoints/AuthEndpoints.cs#L81-L152)
  - [`AuthService.cs`](../../../Acme.Product/src/Acme.Product.Application/Services/AuthService.cs#L183-L259)
  - [`AuthServiceTests.cs`](../../../Acme.Product/tests/Acme.Product.Tests/Services/AuthServiceTests.cs#L130-L185)
  - [`AuthEndpointsTests.cs`](../../../Acme.Product/tests/Acme.Product.Desktop.Tests/AuthEndpointsTests.cs#L32-L82)

### 9. `[前后端]` 让结果页开始消费后端分析能力

- 状态：已完成
- 来源：报告 C.4
- 判断：
  - 前端结果页已经开始调用 `/api/analysis/statistics`、`/defect-distribution`、`/trend`。
  - `analysis/report` 已接入结果页主链路，导出也开始优先消费服务端报告。
  - 工程切换时结果上下文会重置，旧右侧结果面板 DOM 写入也已绕开主链路。
  - 结果卡片预览和详情展示也已进一步收敛到 `analysisData`；`outputData` 主要保留为详情页调试区。
  - `serverPaged` 模式下，实时结果不再直接手工写入本地历史数组，而是回源刷新服务端历史页。
  - `serverPaged` 场景下，`status / defectType` 筛选也已回源到服务端历史与分析接口，不再只是当前页本地筛选。
  - `serverPaged` 场景下，如果服务端报告未就绪，结果页也不再回退导出当前页本地数据。
  - 后端分析接口已注册。
  - 当前结果页已经实现“分页、筛选、统计、趋势、报告、导出”主链路优先回源到服务端分析/历史接口。
  - 已有 Playwright 回归覆盖 `serverPaged + status` 筛选会回源刷新服务端历史与分析。
- 证据：
  - [`resultPanel.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L165-L201)
  - [`Program.cs`](../../../Acme.Product/src/Acme.Product.Desktop/Program.cs#L253-L272)
  - [`resultPanel.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L909-L909)
  - [`resultPanel.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L1201-L1261)
  - [`resultPanel.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L432-L445)
  - [`resultPanel.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L1017-L1057)
  - [`resultPanel.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L148-L177)
  - [`ApiEndpoints.cs`](../../../Acme.Product/src/Acme.Product.Desktop/Endpoints/ApiEndpoints.cs#L208-L220)
  - [`Program.cs`](../../../Acme.Product/src/Acme.Product.Desktop/Program.cs#L256-L275)
  - [`high-frequency-regression.spec.ts`](../../../Acme.Product/tests/Acme.Product.UI.Tests/tests/e2e/high-frequency-regression.spec.ts#L408-L421)

### 10. `[联调]` 确定标定能力的唯一保留路线

- 状态：已完成
- 来源：报告 D.1
- 判断：
  - 顶部旧 HTTP 标定入口早已从 `index.html` 工具栏下线，主工作台不再继续暴露旧向导。
  - 设置页中的 `HandEyeCalibWizard` 现在既是唯一公开主入口，也是唯一保留协议路径。
  - 旧 HTTP `/api/calibration/*` 端点已从后端移除，旧 `calibrationWizard.js` / `calibrationWizard.css` 也已删除，仓库内不再保留公开双轨实现。
- 证据：
  - [`index.html`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/index.html)
  - [`settingsView.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L831-L837)
  - [`handEyeCalibWizard.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/core/calibration/handEyeCalibWizard.js#L411-L470)
  - [`ApiEndpoints.cs`](../../../Acme.Product/src/Acme.Product.Desktop/Endpoints/ApiEndpoints.cs)
  - [`WebMessageHandler.cs`](../../../Acme.Product/src/Acme.Product.Desktop/Handlers/WebMessageHandler.cs#L247-L251)

## 阶段完成标准

- 结果页、登出、AI 历史回放三项核心契约统一完成。
- 页面不再伪造关键业务状态，也不再使用演示值冒充真实数据。
- 标定路线已有唯一结论，后续阶段只做收敛不再扩散。

## 当前对照

- 结果页、登出、AI 历史回放三项核心契约统一完成：已达成
- 页面不再伪造关键业务状态，也不再使用演示值冒充真实数据：已达成
- 标定路线已有唯一结论：已达成




