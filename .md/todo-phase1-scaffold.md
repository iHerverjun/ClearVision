# Phase 1：Vite + Vue 3 + TS 脚手架搭建

> **作者**: 蘅芜君
> **所属计划**: 前端重构 6 阶段渐进式迁移
> **预计工时**: 0.5 天
> **产出目标**: 空壳项目可运行，核心依赖全部就绪
> **前置依赖**: 无

---

> [!IMPORTANT]
> **AI 协作规则**：每完成一个 `[ ]` 项后，AI 助手必须立即将其标记为 `[x]`，并在文档底部的「执行日志」中追加一条记录，格式为 `- [日期] 完成：<任务简述>`。

---

## 零、 预备环境（分支与隔离目录）

- [x] 创建并切换到全新的重构 Git 分支（如 `feature/frontend-vue3-refactor`）
- [x] 确保所有新的前端代码只存在于独立的 `frontend/` 目录中，以避免污染旧版代码库

## 一、 项目初始化

- [x] 使用 `npm create vite@latest` 创建 Vue 3 + TypeScript 项目（目标目录 `frontend/`）
- [x] 验证 `npm run dev` 可正常启动开发服务器
- [x] 配置 `vite.config.ts`：
  - [x] 设置 `base` 为 `./`（相对路径，适配 WebView2 本地加载）
  - [x] 配置 `build.outDir` 指向 `../Acme.Product/src/Acme.Product.Desktop/wwwroot`
  - [x] 配置路径别名 `@` → `./src`
- [x] 配置 `tsconfig.json` / `tsconfig.app.json`：
  - [x] 启用 `strict: true`
  - [x] 配置路径别名与 Vite 保持一致

## 二、 核心依赖安装

- [x] 安装 Vue Router：`npm install vue-router@4`
- [x] 安装 Pinia 状态管理：`npm install pinia`
- [x] 安装 Tailwind CSS：
  - [x] `npm install -D tailwindcss @tailwindcss/vite`
  - [x] 创建 `tailwind.config.ts`
  - [x] 在 `main.css` 中添加 `@import "tailwindcss"` 指令
  - [x] 将现有 CSS 变量（`variables.css` 中的 Cinnabar 主题色系）桥接到 Tailwind `theme.extend`
- [x] 安装 Vue I18n：`npm install vue-i18n@9`
- [x] 安装 Axios（HTTP 客户端）：`npm install axios`
- [x] 安装 @vueuse/core（通用 composables）：`npm install @vueuse/core`

## 三、 基础架构搭建

- [x] 创建目录结构：
  ```
  frontend/src/
  ├── assets/          # 静态资源
  ├── components/      # 通用组件
  ├── composables/     # 可复用逻辑
  ├── layouts/         # 布局组件
  ├── pages/           # 页面视图
  │   ├── LoginPage.vue
  │   ├── FlowEditorPage.vue
  │   ├── InspectionPage.vue
  │   ├── ResultsPage.vue
  │   ├── ProjectsPage.vue
  │   └── AiPage.vue
  ├── router/          # 路由配置
  │   └── index.ts
  ├── stores/          # Pinia stores
  │   └── index.ts
  ├── services/        # API 服务层
  │   ├── bridge.ts    # WebView2 通信
  │   └── api.ts       # REST API
  ├── styles/          # 全局样式
  │   ├── main.css     # Tailwind 入口
  │   └── variables.css # 设计令牌
  ├── types/           # TypeScript 类型
  │   └── index.ts
  ├── App.vue          # 根组件
  └── main.ts          # 入口文件
  ```
- [x] 创建空的 `router/index.ts`（包含基础路由占位）
- [x] 创建空的 `stores/index.ts`（Pinia 初始化）
- [x] 在 `main.ts` 中注册 Vue Router、Pinia、I18n 插件

## 四、 设计系统种子

- [x] 将现有 `variables.css` 中的设计令牌（颜色、字体、间距）迁移到 `styles/main.css`
- [x] 在 Tailwind 配置中扩展自定义颜色：
  - [x] `primary` → Cinnabar `#E84855`
  - [x] `surface` → 玻璃态容器背景色
  - [x] `dark` 模式色板
- [x] 创建 `App.vue` 基础骨架（空白布局 + `<RouterView />`）
- [x] 验证暗色/亮色主题 CSS 变量切换机制正常工作

## 五、 构建验证

- [x] `npm run dev` → 开发服务器正常启动
- [x] `npm run build` → 构建产物正确输出到 `wwwroot/`
- [ ] 在 WinForms WebView2 宿主中加载构建产物，页面正常显示

---

## 执行日志

> AI 助手在每次完成任务后，在此处追加记录：

- [2026-02-21] 完成：预备环境与分支隔离创建 (`feature/frontend-vue3-refactor`)
- [2026-02-21] 完成：使用 Vite 创建 Vue 3 + TS 初始脚手架到 `frontend/`
- [2026-02-21] 完成：安装并配置 Tailwind CSS v4 及其主题变量适配
- [2026-02-21] 完成：安装 Vue Router, Pinia, Vue I18n 等前端核心运行时库
- [2026-02-21] 完成：重写与配置 `vite.config.ts`, `tsconfig.json` 并确立全局路径别名
- [2026-02-21] 完成：创建基础占位组件、根路由及状态管理器，成功通过 `npm run build` 测试并输出至 `wwwroot`。
