# Phase 6：AI 面板 + 设置面板 + 标定向导 + i18n + 测试

> **作者**: 蘅芜君
> **所属计划**: 前端重构 6 阶段渐进式迁移
> **预计工时**: 6 天（Phase 5 原 3 天 + Phase 6 原 3 天合并）
> **产出目标**: 全部功能模块迁移完成，多语言可用，E2E 测试覆盖
> **前置依赖**: Phase 5（检测/结果/工程）

---

> [!IMPORTANT]
> **AI 协作规则**：每完成一个 `[ ]` 项后，AI 助手必须立即将其标记为 `[x]`，并在文档底部的「执行日志」中追加一条记录。

---

## 一、 AI 面板（AiPage.vue）

### 1.1 基础布局
- [ ] 创建 `pages/AiPage.vue` / `components/ai/AiPanel.vue`：
  - [ ] 对话式 Chat UI（类似 ChatGPT 对话界面）
  - [ ] 左侧：对话历史列表
  - [ ] 右侧：当前对话 + 输入区
- [ ] 从现有 `aiPanel.js`（550 行）迁移核心逻辑

### 1.2 AI 工作流生成对话框
- [ ] 创建 `components/ai/AiGenerationDialog.vue`：
  - [ ] 输入框：用户描述检测需求（自然语言）
  - [ ] AI 响应区：流式显示 AI 生成的工作流描述
  - [ ] 预览区：显示 AI 生成的工作流节点图（基于 Vue Flow 渲染）
  - [ ] 「应用到画布」按钮 → 将 AI 生成的流程加载到编辑器
- [ ] 从现有 `aiGenerationDialog.js` 迁移
- [ ] 集成 DeepSeek API 调用逻辑

### 1.3 AI Store
- [ ] 创建 `stores/ai.ts`（Pinia store）：
  - [ ] `state`: `conversations`, `currentConversationId`, `isGenerating`, `model`
  - [ ] `actions`: `sendMessage()`, `generateWorkflow()`, `applyWorkflow()`
  - [ ] 流式响应处理（SSE / WebSocket）

## 二、 设置面板（SettingsModal）

### 2.1 设置弹窗
- [ ] 创建 `components/settings/SettingsModal.vue`：
  - [ ] 模态弹窗覆盖层（Glassmorphism 毛玻璃背景）
  - [ ] 左侧标签导航，右侧内容区
- [ ] 从现有 `settingsModal.js`（1340 行）迁移

### 2.2 设置标签页
- [ ] 创建各标签页组件：
  - [ ] `components/settings/GeneralTab.vue` — 常规设置：
    - [ ] 主题切换（亮/暗）
    - [ ] 语言设置（中/英）
    - [ ] 自动保存间隔
  - [ ] `components/settings/CameraTab.vue` — 相机管理：
    - [ ] 相机列表（添加/编辑/删除/测试连接）
    - [ ] 相机参数配置
    - [ ] 实时预览
  - [ ] `components/settings/CommunicationTab.vue` — 通信设置：
    - [ ] PLC 连接配置（西门子S7/三菱MC/欧姆龙FINS）
    - [ ] TCP/Serial 端口配置
    - [ ] MQTT Broker 配置
    - [ ] HTTP 端点配置
    - [ ] 连接测试按钮
  - [ ] `components/settings/DatabaseTab.vue` — 数据库设置：
    - [ ] 数据库连接字符串配置
    - [ ] 连接测试
  - [ ] `components/settings/AiTab.vue` — AI 设置：
    - [ ] API Key 配置
    - [ ] 模型选择（DeepSeek / OpenAI）
    - [ ] 超时设置
  - [ ] `components/settings/AboutTab.vue` — 关于：
    - [ ] 版本信息
    - [ ] 开源协议
    - [ ] 作者信息

### 2.3 Settings Store
- [ ] 创建 `stores/settings.ts`（Pinia store）：
  - [ ] 从后端加载设置 → 缓存在本地
  - [ ] 设置变更 → 实时保存到后端
  - [ ] 支持设置导入/导出

## 三、 标定向导

### 3.1 相机标定向导
- [ ] 创建 `components/calibration/CalibrationWizard.vue`：
  - [ ] 多步骤向导（Step 1: 选择标定板 → Step 2: 采集图像 → Step 3: 计算标定）
  - [ ] 实时图像预览 + 角点检测叠加
  - [ ] 标定结果展示（内参矩阵、畸变系数、重投影误差）
- [ ] 从现有 `calibrationWizard.js`（540 行）迁移

### 3.2 手眼标定向导
- [ ] 创建 `components/calibration/HandEyeCalibWizard.vue`：
  - [ ] 多步骤向导（Step 1: 配置机器人 → Step 2: 多点采集 → Step 3: 求解变换矩阵）
  - [ ] 3D 坐标可视化
  - [ ] 标定精度验证
- [ ] 从现有 `handEyeCalibWizard.js`（590 行）迁移

## 四、 共享组件最终迁移

- [ ] 完成所有剩余共享组件的 Vue 化改写：
  - [ ] `components/shared/ModalDialog.vue` — 通用模态弹窗（替代 `dialog.js`）
  - [ ] `components/shared/SplitPanel.vue` — 可拖拽分割面板（替代 `splitPanel.js`）
  - [ ] `components/shared/TreeView.vue` — 树形视图（替代 `treeView.js`）
  - [ ] `components/shared/Toast.vue` — 轻量提示通知
- [ ] 从现有 `shared/components/uiComponents.js`（12.7 KB）提取：
  - [ ] 确认对话框
  - [ ] 进度条
  - [ ] 加载 Spinner

## 五、 Flow Lint 面板

- [ ] 创建 `components/flow/LintPanel.vue`：
  - [ ] 显示流程图中的警告和错误列表
  - [ ] 点击项 → 定位到对应节点
  - [ ] 从现有 `lintPanel.js`（178 行）迁移

## 六、 国际化（i18n）

- [ ] 创建语言文件：
  - [ ] `locales/zh-CN.json` — 中文（默认）
  - [ ] `locales/en-US.json` — 英文
- [ ] 提取所有硬编码中文字符串为 i18n 键值：
  - [ ] 所有页面标题
  - [ ] 所有按钮文本
  - [ ] 所有提示信息
  - [ ] 所有表单标签
  - [ ] 算子名称和描述（从后端元数据获取，可选前端覆盖）
- [ ] 在 `main.ts` 中配置 Vue I18n 插件
- [ ] 在设置面板中实现语言切换功能
- [ ] 验证切换语言后所有界面文本正确更新

## 七、 RBAC 权限控制

- [ ] 创建 `directives/permission.ts` — `v-permission` 自定义指令：
  - [ ] `v-permission="'admin'"` → 仅管理员可见
  - [ ] `v-permission="'editor'"` → 编辑者可见
  - [ ] 无权限时隐藏元素或显示「无权限」提示
- [ ] 在需要权限控制的位置应用指令：
  - [ ] 设置面板 → 仅管理员可访问
  - [ ] 工程删除 → 仅管理员/拥有者
  - [ ] AI 面板 → 可选权限控制

## 八、 E2E 测试

- [ ] 安装 Playwright：`npm install -D @playwright/test`
- [ ] 创建测试文件：
  - [ ] `tests/e2e/auth.spec.ts` — 登录/登出/权限测试
  - [ ] `tests/e2e/flow-editor.spec.ts` — 流程编辑器基本操作测试
  - [ ] `tests/e2e/inspection.spec.ts` — 检测视图数据展示测试
  - [ ] `tests/e2e/settings.spec.ts` — 设置面板功能测试
  - [ ] `tests/e2e/projects.spec.ts` — 工程管理 CRUD 测试
- [ ] 配置 CI 集成（可选）

## 九、 全局遗留清理

- [ ] 移除所有 `window.*` 全局挂载引用
- [ ] 移除旧前端 `wwwroot/js/` 和 `wwwroot/css/` 目录
- [ ] 更新 `WebView2Host.cs` 的 `LoadInitialPage()` 指向 Vite 构建产物
- [ ] 更新 `WebMessageHandler.cs`（如有必要）
- [ ] 最终全量回归测试

## 十、 C# 宿主适配

- [ ] 修改 `WebView2Host.cs`：
  - [ ] `LoadInitialPage()` 路径指向 Vite 构建产物 `wwwroot/index.html`
  - [ ] 确保 `file://` 协议下 Vite 产物可正常加载
- [ ] 验证 Release 构建流程（`npm run build` → .NET 发布 → 一体化包）

## 十一、 集成验证

- [ ] AI 面板：可发送消息 → AI 响应流式展示 → 生成工作流 → 应用到画布
- [ ] 设置面板：所有标签页功能正常 → 设置保存/加载正确
- [ ] 标定向导：完整的多步骤向导流程可走通
- [ ] i18n：中英文切换 → 所有界面文本正确更新
- [ ] RBAC：权限指令正确隐藏/显示受控元素
- [ ] E2E 测试：所有测试用例通过
- [ ] Release 构建：完整的打包 → 安装 → 运行流程

---

## 执行日志

> AI 助手在每次完成任务后，在此处追加记录：

