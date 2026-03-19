# Phase 1 - 契约对齐 TODO

## 阶段目标

- 统一前后端参数、会话、状态与文案语义。
- 把“看起来可用”但并不真实的页面行为改成真实行为或真实提示。
- 优先让结果页、认证链路、AI 会话与核心状态展示具备可信度。

## 当前状态总览

- 回填日期：2026-03-20
- 核查方式：静态代码与现有测试文件核查，未启动程序、未执行接口请求。
- 阶段判断：未完成
- 统计：3 项已完成，6 项部分完成，1 项未完成
- 主要阻塞：
  - 结果页分页仍是“后端取一批 + 前端再次分页”的双层分页。
  - 相机绑定状态仍含前端硬写值，无法完全追溯到真实设备状态。
  - 标定前端主入口已开始收敛，但后端仍保留旧 HTTP 路线和新 WebMessage 路线，尚未完成协议级收口。

## 状态清单

### 1. `[联调]` 统一结果历史分页契约

- 状态：部分完成
- 来源：报告 A.4
- 判断：
  - 前后端接口参数已经统一为 `pageIndex` / `pageSize`。
  - 但结果页实际仍是后端先固定拉一批数据，前端再按本地 `pageSize=12` 二次分页。
  - 时间过滤场景下，服务层仍有绕开分页参数的分支。
- 证据：
  - [`app.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L689-L691)
  - [`ApiEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/ApiEndpoints.cs#L237-L240)
  - [`InspectionResultRepository.cs`](../../Acme.Product/src/Acme.Product.Infrastructure/Repositories/InspectionResultRepository.cs#L21-L27)
  - [`resultPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L23-L27)
  - [`resultPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L400-L400)
  - [`resultPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L661-L663)
  - [`InspectionService.cs`](../../Acme.Product/src/Acme.Product.Application/Services/InspectionService.cs#L310-L314)
- 主要缺口：
  - 需要统一结果页的真实分页口径，避免“接口分页”和“页面分页”长期并存。

### 2. `[前后端]` 统一登出语义

- 状态：部分完成
- 来源：报告 A.5
- 判断：
  - 前端已补调 `/api/auth/logout`，并在 finally 中清理本地会话并跳转登录页。
  - 后端端点会移除内存 session token。
  - 但前端失败时只有 `console.warn`，没有用户可见失败提示；也未看到登出审计或专门测试。
- 证据：
  - [`auth.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/auth/auth.js#L70-L80)
  - [`AuthEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/AuthEndpoints.cs#L45-L53)
  - [`AuthService.cs`](../../Acme.Product/src/Acme.Product.Application/Services/AuthService.cs#L89-L99)
- 主要缺口：
  - 需要补齐失败提示、顺序说明和必要的审计/回归验证。

### 3. `[前后端]` 统一 AI 历史会话可回放结构

- 状态：已完成
- 来源：报告 A.6
- 判断：
  - AI 生成结果持久化时已同时保存 AI 原始结果和可恢复画布的快照结构。
  - 前端会优先恢复 `currentCanvasFlowJson`，缺快照时会给出明确提示。
  - 已看到后端消息入口和回归测试覆盖。
- 证据：
  - [`AiFlowGenerationService.cs`](../../Acme.Product/src/Acme.Product.Infrastructure/AI/AiFlowGenerationService.cs#L215-L219)
  - [`ConversationalFlowService.cs`](../../Acme.Product/src/Acme.Product.Infrastructure/AI/ConversationalFlowService.cs#L209-L214)
  - [`ConversationalFlowService.cs`](../../Acme.Product/src/Acme.Product.Infrastructure/AI/ConversationalFlowService.cs#L413-L413)
  - [`aiPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/ai/aiPanel.js#L1054-L1065)
  - [`aiPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/ai/aiPanel.js#L637-L646)
  - [`WebMessageHandler.cs`](../../Acme.Product/src/Acme.Product.Desktop/Handlers/WebMessageHandler.cs#L683-L714)
  - [`ConversationalFlowServiceTests.cs`](../../Acme.Product/tests/Acme.Product.Tests/AI/ConversationalFlowServiceTests.cs#L64-L125)

### 4. `[前后端]` 相机绑定列表只展示真实状态

- 状态：未完成
- 来源：报告 B.1
- 判断：
  - 后端当前返回的绑定配置主要是静态配置字段，没有完整的真实连接状态来源。
  - 前端新增绑定时仍会硬写 `connectionStatus: 'Unknown'`。
  - 发现设备弹窗期待 `ipAddress`，但后端归一化结果并未稳定提供该字段。
- 证据：
  - [`SettingsEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/SettingsEndpoints.cs#L247-L250)
  - [`AppConfig.cs`](../../Acme.Product/src/Acme.Product.Core/Entities/AppConfig.cs#L179-L216)
  - [`ICamera.cs`](../../Acme.Product/src/Acme.Product.Core/Cameras/ICamera.cs#L46-L51)
  - [`SettingsEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/SettingsEndpoints.cs#L326-L331)
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L975-L1006)
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L1045-L1053)
- 主要缺口：
  - 需要形成稳定的 `deviceId/serialNumber/ipAddress/connectionStatus` 真值来源，并彻底移除前端兜底伪状态。

### 5. `[前端]` 删除分析卡片中的演示值回退

- 状态：部分完成
- 来源：报告 B.2
- 判断：
  - JS 层已经不再给置信度硬塞默认百分比，缺失时展示缺省态。
  - 但样式层仍保留固定的“示例范围”视觉区间，仍存在演示性质残留。
- 证据：
  - [`analysisCardsPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/analysisCardsPanel.js#L283-L290)
  - [`analysisCardsPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/analysisCardsPanel.js#L515-L520)
  - [`analysisCards.css`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/shared/styles/analysisCards.css#L194-L196)
- 主要缺口：
  - 需要把样式层的固定演示区间去掉，避免视觉上继续冒充真实阈值范围。

### 6. `[前后端]` 明确“清空记录”的真实语义

- 状态：已完成
- 来源：报告 B.3
- 判断：
  - 按钮文案已改成“清空当前视图”。
  - 确认文案和成功提示都明确说明不会删除后端历史记录。
  - 实际行为也仅清空前端当前视图数据。
- 证据：
  - [`index.html`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/index.html#L639-L643)
  - [`app.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L664-L669)
  - [`resultPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L430-L438)

### 7. `[前端]` 更正“工程已自动保存”提示

- 状态：已完成
- 来源：报告 B.4
- 判断：
  - 自动保存仍然只写入本地 `localStorage` 草稿。
  - 用户可见提示已明确写成“流程草稿已保存到本地缓存”，失败提示也指向本地缓存。
- 证据：
  - [`app.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L1438-L1444)
  - [`app.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L1472-L1482)

### 8. `[前后端]` 补齐普通用户自助改密能力

- 状态：部分完成
- 来源：报告 C.1
- 判断：
  - 设置页已经提供普通用户可见的“修改密码”入口，并会调用 `/auth/change-password`。
  - 后端已有鉴权、旧密码校验和最小长度校验。
  - 但密码复杂度、错误码细分和更完整的策略仍未补齐，页面也明确说明其余策略会后续接入。
- 证据：
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L1334-L1335)
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L2227-L2237)
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L1927-L1928)
  - [`AuthEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/AuthEndpoints.cs#L79-L118)
  - [`AuthService.cs`](../../Acme.Product/src/Acme.Product.Application/Services/AuthService.cs#L152-L186)
- 主要缺口：
  - 需要补齐复杂度规则、错误码口径和完整的测试覆盖。

### 9. `[前后端]` 让结果页开始消费后端分析能力

- 状态：部分完成
- 来源：报告 C.4
- 判断：
  - 前端结果页已经开始调用 `/api/analysis/statistics`、`/defect-distribution`、`/trend`。
  - 后端分析接口已注册。
  - 但结果页仍保留大量前端内存统计、趋势和本地导出逻辑，`analysis/report` 也未真正接入 UI。
- 证据：
  - [`resultPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L165-L201)
  - [`Program.cs`](../../Acme.Product/src/Acme.Product.Desktop/Program.cs#L253-L272)
  - [`resultPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L294-L305)
  - [`resultPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L355-L355)
  - [`resultPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L756-L772)
- 主要缺口：
  - 需要把统计、趋势、报告、导出统一收敛到服务端分析数据流。

### 10. `[联调]` 确定标定能力的唯一保留路线

- 状态：部分完成
- 来源：报告 D.1
- 判断：
  - 顶部旧 HTTP 标定入口已从 `index.html` 工具栏下线，主工作台不再继续暴露旧向导。
  - 设置页中的 `HandEyeCalibWizard` 仍是当前唯一保留的主用户入口。
  - 但后端仍同时保留 HTTP 端点和 WebMessage 分支，仓库内旧 `CalibrationWizard` 文件也尚未正式退场，因此还没有形成“唯一协议”。
- 证据：
  - [`index.html`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/index.html)
  - [`calibrationWizard.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/calibration/calibrationWizard.js#L318-L381)
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L831-L837)
  - [`handEyeCalibWizard.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/core/calibration/handEyeCalibWizard.js#L411-L470)
  - [`ApiEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/ApiEndpoints.cs#L52-L58)
  - [`WebMessageHandler.cs`](../../Acme.Product/src/Acme.Product.Desktop/Handlers/WebMessageHandler.cs#L247-L251)
- 主要缺口：
  - 仍需明确旧 HTTP 标定端点和旧向导文件的退场计划，避免“入口已收、协议未收”的半收敛状态。

## 阶段完成标准

- 结果页、登出、AI 历史回放三项核心契约统一完成。
- 页面不再伪造关键业务状态，也不再使用演示值冒充真实数据。
- 标定路线已有唯一结论，后续阶段只做收敛不再扩散。

## 当前对照

- 结果页、登出、AI 历史回放三项核心契约统一完成：未达成
- 页面不再伪造关键业务状态，也不再使用演示值冒充真实数据：未达成
- 标定路线已有唯一结论：未达成
