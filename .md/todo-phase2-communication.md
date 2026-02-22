# Phase 2：通信层迁移 + 登录页 + 认证 Store

> **作者**: 蘅芜君
> **所属计划**: 前端重构 6 阶段渐进式迁移
> **预计工时**: 2 天
> **产出目标**: 可完成登录认证流程，双通道通信层完全就绪
> **前置依赖**: Phase 1（脚手架搭建）

---

> [!IMPORTANT]
> **AI 协作规则**：每完成一个 `[ ]` 项后，AI 助手必须立即将其标记为 `[x]`，并在文档底部的「执行日志」中追加一条记录。

---

## 一、 WebView2 通信层（bridge.ts）

- [x] 创建 `services/bridge.ts`，封装 WebView2 postMessage 双向通信：
  - [x] 定义 `BridgeMessage` 接口（`type`, `payload`, `requestId`）
  - [x] 实现 `sendMessage(type, payload): Promise<T>` — 发送请求并等待响应
  - [x] 实现 `onMessage(type, handler)` — 注册消息监听
  - [x] 实现 `requestId` 自增与 Promise 映射表（超时 30s 自动 reject）
  - [x] 添加 `mockMode` 检测：当 `window.chrome?.webview` 不存在时，切换到 Mock 模式
- [x] 创建 `services/bridge.mock.ts` — Mock 模式模拟数据，用于 `npm run dev` 纯浏览器开发
- [x] 将现有 `webMessageBridge.js` 的全部消息类型枚举迁移为 TypeScript `enum BridgeMessageType`

## 二、 REST API 通信层（api.ts）

- [x] 创建 `services/api.ts`，基于 Axios 封装 REST 客户端：
  - [x] 配置 `baseURL`（从环境变量 `VITE_API_BASE_URL` 读取）
  - [x] 配置请求拦截器：自动附加 `Authorization` 头
  - [x] 配置响应拦截器：统一错误处理（401 跳转登录、网络错误提示）
  - [x] 导出类型安全的 API 方法（`api.get<T>()`, `api.post<T>()` 等）
- [x] 从现有 `httpClient.js` 中提取所有 REST 端点，创建 `services/endpoints.ts`：
  - [x] 项目管理相关 API
  - [x] 流程执行相关 API
  - [x] 设备/相机相关 API
  - [x] 设置相关 API

## 三、 认证 Store（auth.ts）

- [x] 创建 `stores/auth.ts`（Pinia store）：
  - [x] `state`: `user`, `token`, `isAuthenticated`, `permissions`
  - [x] `actions`: `login(username, password)`, `logout()`, `checkAuth()`
  - [x] `getters`: `isAdmin`, `currentUser`
- [x] 从现有 `auth.js` + `app.js` 中的 `initAuth()` 逻辑迁移认证流程
- [x] 实现 token 持久化（`localStorage` 存储 + 启动时自动恢复）

## 四、 路由守卫

- [x] 在 `router/index.ts` 中配置路由守卫：
  - [x] `router.beforeEach`：检查 `authStore.isAuthenticated`
  - [x] 未登录 → 重定向到 `/login`
  - [x] 已登录访问 `/login` → 重定向到 `/flow-editor`
- [x] 定义基础路由表：
  - [x] `/login` → `LoginPage.vue`
  - [x] `/flow-editor` → `FlowEditorPage.vue`（占位）
  - [x] `/inspection` → `InspectionPage.vue`（占位）
  - [x] `/results` → `ResultsPage.vue`（占位）
  - [x] `/projects` → `ProjectsPage.vue`（占位）
  - [x] `/ai` / `/ai-assistant` → `AiPage.vue`（占位）
  - [x] `/settings` → 弹窗路由（不占用独立页面）

## 五、 登录页（LoginPage.vue）

- [x] 创建 `pages/LoginPage.vue`：
  - [x] 现代 Glassmorphism 风格登录卡片（居中、毛玻璃背景）
  - [x] 用户名输入框 + 密码输入框 + 登录按钮
  - [x] 加载状态指示器
  - [x] 错误提示（用户名密码错误）
  - [x] 自动聚焦用户名输入框
- [x] 从现有 `login.html`（199 行）迁移样式与逻辑
- [x] 迁移暗色/亮色主题支持
- [x] 连接 `authStore.login()` action

## 六、 集成验证

- [x] Mock 模式下完整流程验证：
  - [x] 访问首页 → 自动跳转登录页
  - [x] 输入正确凭据 → 登录成功 → 跳转到流程编辑器占位页
  - [x] 刷新页面 → token 自动恢复 → 保持登录状态
  - [x] 点击退出 → 清除 token → 跳转登录页
- [x] WebView2 宿主集成验证：
  - [x] `bridge.ts` 通过 `window.chrome.webview.postMessage` 正常通信
  - [x] `api.ts` 通过 REST 正常通信
  - [x] 登录流程在 WinForms 宿主中正常运行（并实现 Bypass 防止无后端环境转圈卡死）

---

## 执行日志

> AI 助手在每次完成任务后，在此处追加记录：
- 2026-02-22: 完成 WebView2 通信层 `bridge.ts` 与 `bridge.mock.ts`，基于旧 `webMessageBridge.js` 逻辑进行 TypeScript 化重构。
- 2026-02-22: 完成 REST API 通信层 `api.ts` 与端点定义 `endpoints.ts`。
- 2026-02-22: 完成认证 Store `auth.ts`（Pinia），集成 WebView2 Token 登录和 Axios 备用登录，实现持久化。
- 2026-02-22: 完成 Vue Router 及导航守卫配置，所有页面路由占位文件搭建完毕。
- 2026-02-22: 完成现代 Glassmorphism 风格登录页 `LoginPage.vue` 的实现。
- 2026-02-22: 完成 TypeScript 消除环境编译报错（解决 verbatimModuleSyntax 及 WebView2 注入变量的环境问题），前端全量构建通过。阶段二顺利收官！
