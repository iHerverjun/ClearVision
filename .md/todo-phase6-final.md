# Phase 6：功能集成 · 设置面板 · i18n（更新版）

> **作者**: 蘅芜君
> **所属计划**: 前端重构 6 阶段渐进式迁移
> **更新日期**: 2026-02-22
> **当前状态**: 根据实际架构重新审视，去除已不存在的旧 JS 迁移任务。

---

> [!IMPORTANT]
> **架构现状摘要** (Phase 6 计划更新的核心依据)：
> 1. **双通道通讯已建立**：
>    - `WebMessageBridge` (WebView2 `postMessage`) → 实时事件：流程执行、图像推流、文件对话框、AI 生成、手眼标定。
>    - `apiClient` (Axios REST) → CRUD 操作：登录、工程管理、设置读写。
>    - `MockBridge` → 浏览器开发模式下的后端模拟。
> 2. **已有 Pinia Store**：`auth.ts`、`flow.ts`、`execution.ts`、`ui.ts`。
> 3. **旧版 JS 文件已全部移除**：无 `aiPanel.js`、`settingsModal.js` 等需要迁移。
> 4. **C# 后端 `WebMessageHandler.cs` (665 行)**：已实现 `HandleGenerateFlowCommand`、`HandleHandEyeSolveCommand`、`HandleStartInspectionCommand` 等。
> 5. **`vue-i18n` 已安装**：在 `main.ts` 中有基础配置，但语言文件为空壳。
> 6. **路由已就绪**：`/ai-assistant`、`/settings` (重定向到 `?modal=settings`) 已注册。

---

## 一、 AI 面板功能集成

> **目标**：将 `AiChatSidebar.vue` 从静态展示升级为可与 C# 后端 `HandleGenerateFlowCommand` 真实交互的 AI 对话面板。

### 1.1 创建 `stores/ai.ts` (Pinia Store)
- [x] 定义 `state`：`messages: Message[]`、`isGenerating: boolean`、`currentModel: string`。
- [x] 实现 `action: sendPrompt(text: string)`：
  - 通过 `webMessageBridge.sendMessage('GenerateFlowCommand', { prompt }, true)` 向 C# 后端发送请求。
  - 处理返回的 `flowJson`（解析并传递给 `useFlowStore().loadLegacyProject()`）。
- [x] 实现 `action: applyGeneratedFlow(flowJson)`：将 AI 生成结果加载到画布。

### 1.2 改造 `AiChatSidebar.vue`
- [x] 将静态消息列表替换为 `v-for="msg in aiStore.messages"` 动态渲染。
- [x] 输入框绑定 `v-model` + `@keyup.enter` → 调用 `aiStore.sendPrompt()`。
- [x] 快捷指令按钮 → 调用预设 prompt。
- [x] 加载状态：`isGenerating` 为 `true` 时显示骨架屏/加载动画。

### 1.3 改造 `AiInsightsPanel.vue`（与 AI Store 联动）
- [x] 从 `aiStore` 读取最近一次生成的 `flowJson`，动态渲染 Tools Used 和 JSON Preview。

### 1.4 扩展 `bridge.types.ts`
- [x] 添加 AI 相关消息类型常量：`AiGenerateFlow: 'GenerateFlowCommand'`、`AiGenerateFlowResult: 'GenerateFlowResult'`。

### 1.5 扩展 `bridge.mock.ts`
- [x] 为 `GenerateFlowCommand` 添加 mock 响应，返回预设的 `flowJson` 示例。

---

## 二、 设置面板

> **目标**：创建全局设置弹窗，通过 Bridge 读写 C# 后端的 `appsettings.json`。

### 2.1 创建 `stores/settings.ts`
- [x] 定义 `state`：`settings: AppSettings`（主题、语言、相机列表、通讯配置、AI API Key 等）。
- [x] `loadSettings()`：通过 `webMessageBridge.sendMessage(BridgeMessageType.SettingsGet, {}, true)` 从后端加载。
- [x] `saveSettings()`：通过 `webMessageBridge.sendMessage(BridgeMessageType.SettingsSave, settings, true)` 保存到后端。

### 2.2 创建 `components/settings/SettingsModal.vue`
- [x] Glassmorphism 毛玻璃模态弹窗。
- [x] 左侧标签导航 (Lucide 图标)，右侧内容区。
- [x] 通过路由 query `?modal=settings` 或全局事件触发显示。

### 2.3 创建设置标签页组件
- [x] `GeneralTab.vue`：主题切换 (接入 `useUiStore`)、语言选择 (接入 `vue-i18n`)、自动保存间隔。
- [x] `CameraTab.vue`：相机列表（增删改）、参数配置。
- [x] `CommunicationTab.vue`：PLC / TCP / Serial / MQTT / HTTP 连接配置。
- [x] `DatabaseTab.vue`：连接字符串、连接测试。
- [x] `AiTab.vue`：API Key、模型选择、超时设置。
- [x] `AboutTab.vue`：版本信息、开源协议、作者信息。

### 2.4 扩展 `WebMessageHandler.cs`（C# 后端）
- [x] 确认/实现 `SettingsGet` 和 `SettingsSave` 消息处理逻辑。
- [x] 扩展 `bridge.mock.ts` 中对应的 mock 响应。

---

## 三、 标定向导

> **目标**：创建多步骤标定向导组件，对接已有的 C# `HandleHandEyeSolveCommand` / `HandleHandEyeSaveCommand`。

### 3.1 相机标定 `CalibrationWizard.vue`
- [ ] 多步骤向导：选择标定板 → 采集图像 → 计算标定。
- [ ] 通过 Bridge 调用后端标定服务。
- [ ] 结果展示：内参矩阵、畸变系数、重投影误差。

### 3.2 手眼标定 `HandEyeCalibWizard.vue`
- [ ] 多步骤向导：配置机器人 → 多点采集 → 求解变换矩阵。
- [ ] 对接 `HandleHandEyeSolveCommand` 和 `HandleHandEyeSaveCommand`。
- [ ] 精度验证展示。

### 3.3 扩展 `bridge.types.ts`
- [ ] 添加标定相关消息类型常量：`CalibSolve`、`CalibSave`、`HandEyeSolve`、`HandEyeSave`。

---

## 四、 国际化 (i18n) 完整实现

> **现状**：`vue-i18n` 已安装并在 `main.ts` 中配置，但语言文件为空。

### 4.1 创建完整语言文件
- [ ] `locales/zh-CN.json`：提取全部中文硬编码字符串。
- [ ] `locales/en-US.json`：对应英文翻译。

### 4.2 替换模板中的硬编码文本
- [ ] 全部页面标题、按钮文本、提示信息、表单标签 → `$t('key')` 或 `t('key')`。
- [ ] 侧边栏导航项 → i18n 键。

### 4.3 语言切换功能
- [ ] 在 `GeneralTab.vue` 设置面板中实现语言切换 (切换 `i18n.locale`)。
- [ ] 持久化语言选择到 `localStorage`。

---

## 五、 检测页面真实数据接入

> **目标**：将 Inspection 页面从静态 mock 数据升级为接收 `execution.ts` store 的真实执行数据。

### 5.1 `InspectionControls.vue` 绑定 `executionStore`
- [ ] "Single Run" / "Continuous Run" / "Stop" 按钮 → 调用 `executionStore.startExecution()` / `stopExecution()`。
- [ ] 统计数据 (OK/NG/Total/Yield) → 从 `executionStore` 的节点执行状态计算。

### 5.2 `ImageViewer.vue` 接收实时图像流
- [ ] 从 `executionStore` 的 `handleImageStreamEvent` / `handleSharedImageStream` 获取实时画面。
- [ ] `isNg` 状态 → 根据最近一次 `InspectionCompletedEvent.status` 动态切换。

### 5.3 `NodeOutputPanel.vue` 动态展示节点结果
- [ ] 从 `executionStore.nodeStates` 获取每个节点的 `outputData` → 动态渲染测量卡片、OCR 卡片等。

---

## 六、 共享组件 & 遗留清理

### 6.1 通用弹窗组件
- [ ] `components/shared/ModalDialog.vue`：通用模态弹窗基础组件（设置面板和标定向导复用）。
- [ ] `components/shared/Toast.vue`：轻量级提示通知。

### 6.2 全局遗留清理
- [ ] 确认 `window.*` 无全局挂载残留。
- [ ] 验证 `WebView2Host.LoadInitialPage()` 正确指向 Vite 构建产物。
- [ ] 验证 Release 构建流程 (`npm run build` → .NET 发布 → 一体化包)。

---

## 七、 E2E 测试 (可选 / 低优先级)

- [ ] 安装 Playwright：`npm install -D @playwright/test`。
- [ ] 基础测试用例：
  - [ ] 登录 → 路由守卫验证。
  - [ ] 流程编辑器 → 添加/删除节点。
  - [ ] 设置面板 → 主题切换。

---

## 执行日志

> AI 助手在每次完成任务后，在此处追加记录：

- **2026-02-22**: 根据实际架构重新审视 Phase 6 计划。核心变化：(1) 去除已不存在的旧 JS 迁移任务 (2) 明确双通道通讯策略 (3) 新增"检测页面真实数据接入"板块 (4) 调整优先级顺序。
- **2026-02-22**: 完成 Phase 6 第 1 节 (AI 面板功能集成)。创建了 `stores/ai.ts`，打通了前端 UI 到 C# `HandleGenerateFlowCommand` 的交互闭环。
- **2026-02-22**: 完成 Phase 6 第 2 节 (设置面板)。创建了 `stores/settings.ts` 和全局挂载的 `SettingsModal.vue`（含 6 个独立 Tab），并打通了 mock backend 数据。
- **2026-02-22**: 完成 Phase 6 第 3 节 (标定向导)。创建了 `CalibrationWizard.vue` 和 `HandEyeCalibWizard.vue`，实现了多步骤的标定工作流模板，并接入 `InspectionControls.vue` 工具栏。
