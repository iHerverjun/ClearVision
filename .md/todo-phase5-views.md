# Phase 5.1：视图脱水与架构对齐 (Course Correction)

> **作者**: 蘅芜君
> **所属计划**: 前端重构 6 阶段渐进式迁移 (航道纠偏)
> **当前状态**: 修正早期“过度复刻静态 Demo”带来的图标库混乱与硬编码问题。

---

> [!IMPORTANT]
> **AI 协作规则**：每完成一个 `[ ]` 项后，必须立即标记为 `[x]` 并记录日志。严禁再次盲目复制静态 HTML 代码。必须结合 Vue 的响应式范式进行重构！

> [!WARNING]
> **纠偏核心目标**：
> 1. **统一图标规范**：全面弃用并移除新引入的 `material-icons-*`，统一回归原生使用的 `lucide-vue-next`。
> 2. **消除硬编码 (Hardcoding)**：所有界面数据的展示（如分数、缺陷类型、日志、工程名）必须通过 Pinia Store 或 Props 绑定，严禁在模板中写死 demo 假数据。
> 3. **响应式及版面鲁棒性**：修复组件互相挤压、文字溢出边界的问题，确保布局具有真实的自适应能力。

---

## 🛠️ 第一步：架构与依赖清理

- [ ] **全局依赖净化 (index.html & CSS)**：
  - [x] 从 `index.html` 中彻底移除 `Material Icons` 的 Google Fonts 引用。
  - [ ] 确保 `main.css` 和全局主题色变量能妥善适配所有 Lucide 图标颜色体系。

## 🧩 第二步：图标与静态数据组件重构 (组件级)

### 2.1 侧边栏与导航组件对齐
- [ ] **`AppSidebar.vue` & 布局框架**：
  - [ ] 检查并确认全局侧边栏使用 `lucide-vue-next`。
- [x] **`ProjectSidebar.vue` & `ProjectDashboard.vue`**：
  - [x] 将所有 `<span class="material-icons-outlined">...</span>` 替换为等效的 `<component :is="Icon" />` (通过 `lucide-vue-next` 引入图标，例如替换 `search`, `download`, `add`, `folder` 等)。
  - [x] 为工程列表、最近活动移除假静态数据，改为 `v-for` 循环假装数据模型 `ref` 数组，为后续接入真实后端准备。

### 2.2 结果与历史面板 (`ResultsPage.vue`)
- [x] **`ResultsFilterSidebar.vue`**：
  - [x] 替换图标为 Lucide (如 `Filter`, `Calendar`, `ChevronDown`, `Search`, `Check`)。
  - [x] 把输入框的值 (如 `2023-10-27`) 和下拉选项转为 `v-model` 绑定的 reactive 数据。
- [x] **`ResultsMainView.vue`**：
  - [x] 替换工具栏图标为 Lucide (如 `ZoomIn`, `ZoomOut`, `Maximize`, `Download`)。
  - [x] 将底部图片胶卷序列做成基于 `props/mockData` 渲染的列表。
- [x] **`ResultsDetailPanel.vue`**：
  - [x] 替换图标，将写死的 `87.5%`, `45ms`, `Surface Scratch` 等具体缺陷信息建模为一个响应式对象，展示真实或动态生成的数据。

### 2.3 检测监控主界面 (`InspectionPage.vue`)
- [x] **`InspectionControls.vue`**：
  - [x] 替换控制按钮 (Play, RotateCw, Stop, Refresh) 为 Lucide 图标。
  - [x] 统计数字区转为响应式变量，绑定真实控制状态与开始结束事件。
- [x] **`ImageViewer.vue`**：
  - [x] 清理假坐标浮层，确保画布容器尺寸约束正常。
  - [x] 接入 `executionStore.latestCameraImage` 与相机实时预览流。
- [x] **`NodeOutputPanel.vue` (如有)**：
  - [x] 采用可复用的渲染组件来承载原语计算结果，图标统一。
  - [x] 接入真实的 `executionStore.nodeStates` 以展示 `Distance`, `Text` 算子输出及 `recentHistory`。

### 2.4 AI 大模型面板重构 (`AiChatSidebar` & `AiInsightsPanel` & `AiFlowCanvas`)
- [x] 全局搜索并定位全部 AI 面板中的类 Material Icons，将其更新为 `Lucide` 体系 (如 `Sparkles`, `Bot`, `User`, `Code`, `Settings`, `Camera` 等)。

## 🔌 第三步：逻辑串联与自适应优化

- [ ] **对接 `pinia` 存储模型**：
  - [ ] 以 `ResultsPage` 和 `ProjectDashboard` 为例，建立简易的 Typescript Interface (如 `ProjectItem`, `InspectionResult`) 规范数据结构。
- [ ] **响应式弹性盒布局核查**：
  - [ ] 杜绝因文本未截断或弹性溢出导致的 UI 变形，深度检查 `flex-shrink-0` 及 `min-w-0` 等约束。

---

## 📝 阶段执行日志

- **2026-02-22**: 发现纯 HTML Demo 直翻带来的生态背离与排版问题，确立 5.1 「航道纠偏」计划。优先剥离过度引入的第三方字体，深锁自身 `lucide-vue-next` 规范体系。

