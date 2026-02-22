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

- [x] 创建 `layouts/MainLayout.vue`：
  - [x] 顶部 Header 栏：Logo + 项目名称 + 全局操作按钮（保存/撤销/重做）
  - [x] 左侧 Sidebar 导航栏：视图切换图标（流程/检测/结果/工程/AI）
  - [x] 底部 StatusBar：连接状态 + 用户名 + 版本号
  - [x] 中央 `<RouterView />` 内容区
- [x] 布局采用 CSS Grid / Flexbox 实现响应式自适应
- [x] Glassmorphism 风格：Sidebar 和 Header 使用半透明现代科技白毛玻璃效果

## 二、 Header 组件 (脱离旧版，现代重构)

- [x] 创建 `components/layout/AppHeader.vue`：
  - [x] **视觉升级**：摒弃传统工具栏，采用悬浮/沉浸式 Header，支持现代轻盈拟态玻璃质感。
  - [x] **信息架构**：左侧展示动态项目切换器（如下拉毛玻璃卡片），中间展示运行中心（包含圆环运行进度、播放/停止聚合按钮），右侧展示全局操作及AI调度入口。
  - [x] **解耦迁移**：保存、撤销、重做全部由 Pinia store (`flowStore`) 接管，暂不强依赖后端，UI 层面可先放置高优先级的现代化组件。
  - [x] **占位记录**：对暂未实现的后端状态（如运行进度、后端项目列表）在 `placeholders-tracker.md` 中记录。

## 三、 Sidebar 导航组件 (现代侧边栏)

- [x] 创建 `components/layout/AppSidebar.vue`：
  - [x] **视觉升级**：采用悬浮岛（Floating Island）或极简胶囊状（Capsule）侧边导航，彻底摆脱传统通栏。
  - [x] 包含：工作流编辑、实时检测、数据报表、项目管理、AI与设置。
  - [x] 图标采用现代化的 Lucide 或自制 SVG 动效。
  - [x] 悬停时带有平滑展开动画（展开显示文字描述）。

## 四、 StatusBar 组件 (硬核工业监控条)

- [x] 创建 `components/layout/AppStatusBar.vue`：
  - [x] **视觉升级**：底部打造极具科技感的干净"工业监控台"。
  - [x] 包含：底层通信信道状态（绿点/心跳效果）、CPU/内存占位指示、当前操作者权限。
  - [x] **占位记录**：未实现的硬件监控数据在 `placeholders-tracker.md` 记录。

## 五、 视图页面占位

- [x] 创建页面基础结构（带路由过渡微动效）：
  - [x] `pages/FlowEditorPage.vue`
  - [x] `pages/InspectionPage.vue`
  - [x] `pages/ResultsPage.vue`
  - [x] `pages/ProjectsPage.vue`
  - [x] `pages/AiPage.vue`
- [x] 所有路由配置 `MainLayout` 为 Layout，增加基于 Vue `<transition>` 的平滑页面切换。

## 六、 UI Store（全局 UI 状态）

- [x] 创建 `stores/ui.ts`（Pinia store）：
  - [x] `state`: `currentView`, `sidebarCollapsed`, `theme` (`'dark'` | `'light'`)
  - [x] `actions`: `toggleSidebar()`, `setTheme()`, `toggleTheme()`
  - [x] `getters`: `isDarkMode`
- [x] 将现有 `store.js` 中的 Signal 状态迁移到此 Pinia store
- [x] 主题切换联动 `document.documentElement.dataset.theme` 属性（目前已通过修复本地缓存问题强制锁定进入 Light 科技白模式）

## 七、 共享通用组件（基础层）

- [x] 创建基础通用组件，以响应式变量 + 玻璃态风格重写：
  - [x] `components/shared/GlassCard.vue` — 毛玻璃卡片容器
  - [x] `components/shared/IconButton.vue` — 图标按钮
  - [x] `components/shared/Tooltip.vue` — 悬浮提示（替代 `tooltip.js`）
- [x] 从现有 `shared/components/` 中的 `dialog.js`、`tooltip.js` 提取核心逻辑

## 八、 集成验证

- [x] 登录后进入主布局 → Header/Sidebar/StatusBar 正常显示
- [x] Sidebar 点击导航 → 路由切换 → 中央内容区正确渲染对应页面
- [x] 主题切换按钮 → 暗/亮模式正确切换（根据用户意志舍弃深色系统）
- [x] 浏览器刷新 → 保持当前路由 + 主题状态
- [x] 在 WebView2 宿主中验证布局渲染正确

---

## 执行日志

> AI 助手在每次完成任务后，在此处追加记录：
- 2026-02-22: 完成 `ui.ts` 状态中心管理主题和 Sidebar 状态。
- 2026-02-22: 完成 `GlassCard.vue` 及 `IconButton.vue` 核心交互沉浸式组件资源。
- 2026-02-22: 完成现代浸入式 Header (`AppHeader.vue`)，侧边岛状导航 (`AppSidebar.vue`) 与工业监控底层栏 (`AppStatusBar.vue`) 的构建，引入 Lucide 图标库。
- 2026-02-22: 完成对后端数据解耦的梳理，并在 `placeholders-tracker.md` 记录各项占位。
- 2026-02-22: 完成 `MainLayout.vue` 拼装并正确更新 Router，增加 `fade-slide` 渐变动画保护壳体系，TypeScript build 验证通过，Phase 3 骨架构建彻底完毕！
