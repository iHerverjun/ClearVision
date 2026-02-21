# Phase 3：布局骨架迁移 + 视图路由切换

> **作者**: 蘅芜君
> **所属计划**: 前端重构 6 阶段渐进式迁移
> **预计工时**: 2 天
> **产出目标**: 主布局框架可用，所有视图页面可路由切换，导航完全正常
> **前置依赖**: Phase 2（通信层 + 登录）

---

> [!IMPORTANT]
> **AI 协作规则**：每完成一个 `[ ]` 项后，AI 助手必须立即将其标记为 `[x]`，并在文档底部的「执行日志」中追加一条记录。

---

## 一、 主布局组件（MainLayout.vue）

- [ ] 创建 `layouts/MainLayout.vue`：
  - [ ] 顶部 Header 栏：Logo + 项目名称 + 全局操作按钮（保存/撤销/重做）
  - [ ] 左侧 Sidebar 导航栏：视图切换图标（流程/检测/结果/工程/AI）
  - [ ] 底部 StatusBar：连接状态 + 用户名 + 版本号
  - [ ] 中央 `<RouterView />` 内容区
- [ ] 布局采用 CSS Grid / Flexbox 实现响应式自适应
- [ ] Glassmorphism 风格：Sidebar 和 Header 使用半透明毛玻璃效果

## 二、 Header 组件

- [ ] 创建 `components/layout/AppHeader.vue`：
  - [ ] 从现有 `index.html` 的 `#app-header` 区域迁移 HTML 结构
  - [ ] 迁移 `app.js` 中的 Header 相关事件处理：
    - [ ] 保存按钮 → 调用 flowStore 的 save action
    - [ ] 撤销/重做按钮 → 调用 flowStore undo/redo
    - [ ] 运行/停止按钮 → 调用 executionStore run/stop
  - [ ] 将硬编码的按钮事件改为通过 Pinia store 或 `emit` 驱动

## 三、 Sidebar 导航组件

- [ ] 创建 `components/layout/AppSidebar.vue`：
  - [ ] 5 个导航图标按钮：流程编辑器、检测视图、结果面板、工程管理、AI 面板
  - [ ] 当前激活视图高亮指示
  - [ ] 点击切换 → `router.push()` 路由跳转
  - [ ] 保留现有的图标资源或使用 Heroicons / Lucide Icons 替代
- [ ] 从现有 `app.js` 的 `switchView()` / `handleViewSwitch()` 迁移视图切换逻辑
- [ ] 底部放置设置齿轮图标 → 触发 SettingsModal（Phase 5 实现，此处预留 slot）

## 四、 StatusBar 组件

- [ ] 创建 `components/layout/AppStatusBar.vue`：
  - [ ] 左侧：用户名显示（从 `authStore.currentUser` 读取）
  - [ ] 中央：后端连接状态指示（从 bridge 状态读取）
  - [ ] 右侧：应用版本号
- [ ] 从现有 `index.html` 的状态栏区域迁移结构

## 五、 视图页面占位

- [ ] 创建 6 个页面组件（每个仅包含标题占位，后续 Phase 填充）：
  - [ ] `pages/FlowEditorPage.vue` — 「流程编辑器」标题 + 空白画布区
  - [ ] `pages/InspectionPage.vue` — 「检测视图」标题
  - [ ] `pages/ResultsPage.vue` — 「结果面板」标题
  - [ ] `pages/ProjectsPage.vue` — 「工程管理」标题
  - [ ] `pages/AiPage.vue` — 「AI 助手」标题
- [ ] 更新路由配置，所有页面使用 `MainLayout` 作为父布局

## 六、 UI Store（全局 UI 状态）

- [ ] 创建 `stores/ui.ts`（Pinia store）：
  - [ ] `state`: `currentView`, `sidebarCollapsed`, `theme` (`'dark'` | `'light'`)
  - [ ] `actions`: `toggleSidebar()`, `setTheme()`, `toggleTheme()`
  - [ ] `getters`: `isDarkMode`
- [ ] 将现有 `store.js` 中的 Signal 状态迁移到此 Pinia store
- [ ] 主题切换联动 `document.documentElement.dataset.theme` 属性

## 七、 共享通用组件（基础层）

- [ ] 创建基础通用组件，以 Tailwind + 玻璃态风格重写：
  - [ ] `components/shared/GlassCard.vue` — 毛玻璃卡片容器
  - [ ] `components/shared/IconButton.vue` — 图标按钮
  - [ ] `components/shared/Tooltip.vue` — 悬浮提示（替代 `tooltip.js`）
- [ ] 从现有 `shared/components/` 中的 `dialog.js`、`tooltip.js` 提取核心逻辑

## 八、 集成验证

- [ ] 登录后进入主布局 → Header/Sidebar/StatusBar 正常显示
- [ ] Sidebar 点击导航 → 路由切换 → 中央内容区正确渲染对应页面
- [ ] 主题切换按钮 → 暗/亮模式正确切换
- [ ] 浏览器刷新 → 保持当前路由 + 主题状态
- [ ] 在 WebView2 宿主中验证布局渲染正确

---

## 执行日志

> AI 助手在每次完成任务后，在此处追加记录：

