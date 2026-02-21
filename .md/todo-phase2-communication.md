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

- [ ] 创建 `services/bridge.ts`，封装 WebView2 postMessage 双向通信：
  - [ ] 定义 `BridgeMessage` 接口（`type`, `payload`, `requestId`）
  - [ ] 实现 `sendMessage(type, payload): Promise<T>` — 发送请求并等待响应
  - [ ] 实现 `onMessage(type, handler)` — 注册消息监听
  - [ ] 实现 `requestId` 自增与 Promise 映射表（超时 30s 自动 reject）
  - [ ] 添加 `mockMode` 检测：当 `window.chrome?.webview` 不存在时，切换到 Mock 模式
- [ ] 创建 `services/bridge.mock.ts` — Mock 模式模拟数据，用于 `npm run dev` 纯浏览器开发
- [ ] 将现有 `webMessageBridge.js` 的全部消息类型枚举迁移为 TypeScript `enum BridgeMessageType`

## 二、 REST API 通信层（api.ts）

- [ ] 创建 `services/api.ts`，基于 Axios 封装 REST 客户端：
  - [ ] 配置 `baseURL`（从环境变量 `VITE_API_BASE_URL` 读取）
  - [ ] 配置请求拦截器：自动附加 `Authorization` 头
  - [ ] 配置响应拦截器：统一错误处理（401 跳转登录、网络错误提示）
  - [ ] 导出类型安全的 API 方法（`api.get<T>()`, `api.post<T>()` 等）
- [ ] 从现有 `httpClient.js` 中提取所有 REST 端点，创建 `services/endpoints.ts`：
  - [ ] 项目管理相关 API
  - [ ] 流程执行相关 API
  - [ ] 设备/相机相关 API
  - [ ] 设置相关 API

## 三、 认证 Store（auth.ts）

- [ ] 创建 `stores/auth.ts`（Pinia store）：
  - [ ] `state`: `user`, `token`, `isAuthenticated`, `permissions`
  - [ ] `actions`: `login(username, password)`, `logout()`, `checkAuth()`
  - [ ] `getters`: `isAdmin`, `currentUser`
- [ ] 从现有 `auth.js` + `app.js` 中的 `initAuth()` 逻辑迁移认证流程
- [ ] 实现 token 持久化（`localStorage` 存储 + 启动时自动恢复）

## 四、 路由守卫

- [ ] 在 `router/index.ts` 中配置路由守卫：
  - [ ] `router.beforeEach`：检查 `authStore.isAuthenticated`
  - [ ] 未登录 → 重定向到 `/login`
  - [ ] 已登录访问 `/login` → 重定向到 `/flow-editor`
- [ ] 定义基础路由表：
  - [ ] `/login` → `LoginPage.vue`
  - [ ] `/flow-editor` → `FlowEditorPage.vue`（占位）
  - [ ] `/inspection` → `InspectionPage.vue`（占位）
  - [ ] `/results` → `ResultsPage.vue`（占位）
  - [ ] `/projects` → `ProjectsPage.vue`（占位）
  - [ ] `/ai` → `AiPage.vue`（占位）
  - [ ] `/settings` → 弹窗路由（不占用独立页面）

## 五、 登录页（LoginPage.vue）

- [ ] 创建 `pages/LoginPage.vue`：
  - [ ] 现代 Glassmorphism 风格登录卡片（居中、毛玻璃背景）
  - [ ] 用户名输入框 + 密码输入框 + 登录按钮
  - [ ] 加载状态指示器
  - [ ] 错误提示（用户名密码错误）
  - [ ] 自动聚焦用户名输入框
- [ ] 从现有 `login.html`（199 行）迁移样式与逻辑
- [ ] 迁移暗色/亮色主题支持
- [ ] 连接 `authStore.login()` action

## 六、 集成验证

- [ ] Mock 模式下完整流程验证：
  - [ ] 访问首页 → 自动跳转登录页
  - [ ] 输入正确凭据 → 登录成功 → 跳转到流程编辑器占位页
  - [ ] 刷新页面 → token 自动恢复 → 保持登录状态
  - [ ] 点击退出 → 清除 token → 跳转登录页
- [ ] WebView2 宿主集成验证：
  - [ ] `bridge.ts` 通过 `window.chrome.webview.postMessage` 正常通信
  - [ ] `api.ts` 通过 REST 正常通信
  - [ ] 登录流程在 WinForms 宿主中正常运行

---

## 执行日志

> AI 助手在每次完成任务后，在此处追加记录：

