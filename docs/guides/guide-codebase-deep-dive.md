# ClearVision 当前版本源码级详解

> **文档定位**：给同事做系统性学习、源码导读、功能对照与二次开发入口使用
> **覆盖范围**：`Acme.Product` 主产品、`Acme.PlcComm` 通信组件，以及与桌面宿主/前端/AI/标定/结果分析直接相关的主链路代码
> **生成基线**：基于当前仓库内容整理，时间点为 `2026-03-12`
> **阅读建议**：先看“软件能力总览”和“运行架构”，再按“功能模块详解”与“主要代码方法导读”逐步下钻

---

## 目录

1. [文档范围与阅读方式](#1-文档范围与阅读方式)
2. [软件能力总览](#2-软件能力总览)
3. [仓库结构与关键指标](#3-仓库结构与关键指标)
4. [运行架构与启动链路](#4-运行架构与启动链路)
5. [当前版本的核心功能拆解](#5-当前版本的核心功能拆解)
6. [核心领域模型](#6-核心领域模型)
7. [HTTP API 总表](#7-http-api-总表)
8. [桌面宿主与后端主要代码方法导读](#8-桌面宿主与后端主要代码方法导读)
9. [前端主要代码方法导读](#9-前端主要代码方法导读)
10. [典型端到端链路](#10-典型端到端链路)
11. [学习路径建议](#11-学习路径建议)
12. [补充说明与延伸阅读](#12-补充说明与延伸阅读)

---

## 1. 文档范围与阅读方式

### 1.1 这份文档解决什么问题

这份文档不是简单的用户手册，而是**“功能—架构—源码”三层对齐的导读文档**。目标是帮助同事快速建立以下认知：

1. **这个软件现在到底能做什么**。
2. **这些功能在代码里分别落在哪些项目、哪些文件、哪些类**。
3. **主链路是怎么跑通的**，例如：前端如何发起请求、后端如何执行流程、结果如何回显。
4. **主要代码方法分别负责什么**，便于定点阅读和二次开发。

### 1.2 覆盖范围

本说明重点覆盖以下部分：

- `Acme.Product/src/Acme.Product.Desktop`：桌面宿主、内置 Web 服务、API、WebView2 桥接。
- `Acme.Product/src/Acme.Product.Application`：应用服务层，如工程、检测、认证、用户管理、结果分析。
- `Acme.Product/src/Acme.Product.Infrastructure`：流程执行、图像采集、配置、AI、相机、算子元数据、预览等。
- `Acme.Product/src/Acme.Product.Core`：实体、值对象、接口、枚举、消息契约依赖。
- `Acme.Product/src/Acme.Product.Desktop/wwwroot/src`：前端视图层、画布、属性面板、检测页、设置页、AI 面板等。
- `Acme.Product/src/Acme.PlcComm`：PLC 通信协议实现。

### 1.3 有意不展开的内容

为了让文档既详尽又可读，以下内容**不逐个展开内部算法细节**：

- `build_check`、`vendor`、`artifacts`、`obj`、`bin` 等构建产物。
- 测试项目中的所有用例实现。
- 118 个具体算子文件内部的每个算法细节。

但需要强调：**算子系统本身已经纳入本文档**，本文会说明算子如何被扫描、注册、展示、拖拽、执行、预览、推荐参数，以及它们与主产品如何集成。若要继续深入每个算子的参数、输入输出与业务语义，可继续看：

- `docs/OPERATOR_CATALOG.md`
- `docs/operators/CATALOG.md`
- `docs/operators/*.md`
- `Acme.Product/src/Acme.Product.Infrastructure/Operators/`

### 1.4 阅读顺序建议

- 想先理解产品：看第 2、4、5 章。
- 想先理解后端：看第 6、7、8 章。
- 想先理解前端：看第 4、5、9 章。
- 想跟踪一次完整检测流程：看第 10 章。
- 想接手 AI、标定、PLC、相机中的某一块：看第 5、8、9、12 章对应小节。

---

## 2. 软件能力总览

从当前代码实现看，ClearVision 已经不是“单一图像处理 Demo”，而是一个**本地运行的工业视觉桌面平台**。它把桌面宿主、内嵌 Web 前端、流程编排、检测执行、相机接入、PLC 通信、AI 生成、结果分析整合成一个完整产品。

### 2.1 当前版本能做的事情

#### A. 账号与权限

- 支持登录、登出、令牌校验、修改密码。
- 支持用户管理：查询用户、新增用户、编辑用户、删除用户、重置密码。
- 支持角色判断，前端有管理员/工程师等角色感知逻辑。

#### B. 工程管理

- 创建工程、编辑工程、删除工程。
- 打开最近工程、搜索工程。
- 导入/导出工程。
- 工程流程可独立持久化为 JSON。

#### C. 流程编辑

- 提供拖拽式节点画布。
- 支持算子拖入、连线、断线、复制、粘贴、撤销、重做、框选。
- 支持模板应用。
- 支持流程序列化/反序列化。
- 支持节点状态显示、最小地图、上下文菜单。

#### D. 算子库与参数编辑

- 通过分类树展示算子库。
- 当前仓库文档统计总算子数为 **118**，目录平均质量分为 **88.6** (`A=77 / B=35 / C=6`).
- 支持关键字搜索、分类图标、参数面板编辑。
- 支持参数推荐、预览执行、相机绑定下拉、输入图像联动。

#### E. 图像查看与检测执行

- 支持单图检测。
- 支持相机触发检测。
- 支持实时检测。
- 支持“流程驱动模式”实时检测。
- 支持检测完成后显示输出图像、缺陷框、结果状态、处理时长。

#### F. 结果中心与分析

- 支持历史结果列表。
- 支持分页、筛选、时间范围过滤。
- 支持统计面板、良率、缺陷分布、趋势图。
- 支持导出 CSV/JSON。
- 支持后端生成分析报告。

#### G. 标定能力

- 支持前端标定向导。
- 支持手眼标定求解与保存。
- 提供 `/api/calibration/solve` 与 `/api/calibration/save`。

#### H. 设置中心

- 常规设置：软件标题、主题、自启动等。
- 通信设置：PLC IP、端口、协议、心跳、地址映射。
- 存储设置：图像目录、保存策略、保留天数、磁盘空间下限。
- 运行时设置：自动运行、连续 NG 停机阈值。
- 相机管理：发现设备、绑定设备、保存曝光/增益/触发方式、软触发采图。
- AI 模型管理：模型列表、增删改、激活、测试连接。
- 用户管理：用户表格、弹窗编辑、重置密码。

#### I. AI 辅助编排

- 支持自然语言描述生成流程。
- 支持会话化历史记录。
- 支持附件输入（图像/文件）参与生成。
- 支持生成过程中的流式进度、思考链、结果解释。
- 支持将生成结果直接应用到画布。

#### J. 工业设备集成

- 相机管理已具备抽象层，支持真实相机/文件相机/模拟相机。
- PLC 通信已支持西门子、欧姆龙、三菱协议实现。
- 设置页支持 PLC 地址映射维护与连通性测试。

### 2.2 当前系统的产品定位

如果用一句话概括：

> **ClearVision 是一个以流程图编排为核心、以本地桌面部署为载体、以工业视觉检测为主场景的零/低代码工业视觉平台。**

---

## 3. 仓库结构与关键指标

### 3.1 代码分层一览

| 目录                                             | 角色       | 说明                                            |
| ------------------------------------------------ | ---------- | ----------------------------------------------- |
| `Acme.Product/src/Acme.Product.Desktop`        | 宿主层     | WinForms + WebView2 + 内嵌 ASP.NET Core Web API |
| `Acme.Product/src/Acme.Product.Application`    | 应用层     | 工程、检测、认证、结果分析、用户管理等业务服务  |
| `Acme.Product/src/Acme.Product.Infrastructure` | 基础设施层 | 流程执行、算子实现、AI、相机、配置、数据访问    |
| `Acme.Product/src/Acme.Product.Core`           | 领域层     | 实体、接口、枚举、值对象、核心抽象              |
| `Acme.Product/src/Acme.Product.Contracts`      | 契约层     | Web 消息模型、AI 生成消息模型                   |
| `Acme.Product/src/Acme.PlcComm`                | 通信组件   | PLC 协议客户端与地址/报文构建                   |
| `Acme.Product/tests`                           | 测试层     | 单元/集成/UI 测试                               |
| `docs`                                         | 文档层     | 用户文档、算子文档、审计报告、路线图            |

### 3.2 当前仓库的几个重要数字

基于当前代码扫描结果，可快速建立以下量级认知：

- `Acme.Product/src` 下 C# 源文件数：**397**
- 前端 `wwwroot/src` 下 JS 文件数：**30**
- `docs/OPERATOR_CATALOG.md` / `docs/operators/CATALOG.md` 当前同步统计：算子总数 **118**，目录平均质量分 **88.6**.
- `Endpoints/` 目录下定义的 HTTP API 数量：**54**
- `Program.RegisterExtendedApiEndpoints` 额外补充的 HTTP API 数量：**8**
- 当前 HTTP 路由总量（含 `/health`）：**62**

### 3.3 值得特别注意的架构特征

当前代码中存在几条非常重要的“产品级实现选择”：

1. **桌面应用本质上是“WinForms 外壳 + 本地 Web 服务 + Web 前端”**。不是传统纯 WinForms 业务界面，而是 Web 前端被桌面化承载。
2. **流程持久化采用了“数据库 + JSON 文件覆盖”的混合策略**。工程实体在数据库中，流程 JSON 则通过 `IProjectFlowStorage` / `JsonFileProjectFlowStorage` 管理。
3. **前后端通信采用“双通道”**：

   - 标准 HTTP API：用于绝大多数 CRUD、设置、工程、结果、模型管理。
   - WebMessage / SharedBuffer：用于 WebView2 深度集成、AI 生成、文件选择、手眼标定、共享图像缓冲区等。
4. **AI 相关逻辑有“新主链 + 老兼容链”并存**。当前前端 AI 面板主链更偏向 `AiFlowGenerationService + GenerateFlowMessageHandler + AiApiClient + AiConfigStore`；同时仓库中还保留了 `AIWorkflowService` 一套更完整的 Prompt/Linter/DryRun 流程。
5. **算子元数据也存在“双轨”**。
   当前运行时更核心的是 `OperatorFactory + OperatorMetadataScanner + OperatorFactoryMetadataMerge`；而 `OperatorService.InitializeMetadataCache()` 中还保留了部分手工缓存式元数据构造逻辑，更像兼容层或早期实现痕迹。

---

## 4. 运行架构与启动链路

### 4.1 启动总流程

当前产品的启动链路可概括为：

1. 进入 `Program.Main()`。
2. 注册全局异常捕获、原生 DLL 搜索路径、程序集解析器。
3. 启动本地 Web 服务器 `StartWebServer()`。
4. Web 服务器注册依赖注入、数据库、API、静态资源、认证中间件。
5. 启动 WinForms 主窗口 `MainForm`。
6. `MainForm_Load()` 中初始化 `WebView2Host`。
7. `WebView2Host` 将 API 基地址注入前端页面，并加载 `index.html`。
8. 前端 `app.js` 在 `DOMContentLoaded` 时初始化全站模块。

### 4.2 桌面宿主是如何工作的

#### 角色拆分

- `Program.cs`：启动本地 Web 服务和 WinForms 宿主。
- `MainForm.cs`：承载顶层窗口与菜单。
- `WebView2Host.cs`：配置 WebView2、注入 API Base URL、处理 SharedBuffer 与页面加载。
- `WebMessageHandler.cs`：处理 WebView2 前后端消息。

#### 为什么这样设计

这种设计兼顾了三点：

- **桌面部署能力**：便于接入相机 SDK、本地文件、PLC、本地数据库。
- **前端开发效率**：复杂交互界面用 HTML/CSS/JS 来做更快。
- **后端能力聚合**：所有检测、配置、AI、持久化接口统一走本地 Web API。

### 4.3 前后端通信模型

#### 方式一：HTTP API

前端通过 `httpClient.js` 请求本地 Minimal API：

- 优先使用 `window.__API_BASE_URL__`。
- 如果没有注入，则尝试从 `localStorage` 恢复上次端口。
- 还可通过 `/health` 自动探测端口。

适用场景：

- 登录与权限
- 工程 CRUD
- 流程保存
- 检测执行与历史查询
- 设置保存
- AI 模型管理
- 相机发现与绑定
- PLC 测试与映射

#### 方式二：WebMessage

前端通过 `window.chrome.webview.postMessage` 发送消息；桌面侧由 `WebMessageHandler` 处理。适合：

- 执行单算子
- 更新流程
- 启停检测
- 文件选择
- AI 生成流
- AI 会话读取/删除
- 手眼标定求解与保存

#### 方式三：SharedBuffer

对于图像数据，`WebView2Host.SendSharedBuffer()` 会通过共享缓冲区把数据直接推送到脚本侧。这样可以减少大型图像在 JSON/base64 中转时的损耗。

### 4.4 数据与配置持久化

当前版本的主要持久化点如下：

| 类型                   | 存储位置                                     | 说明                               |
| ---------------------- | -------------------------------------------- | ---------------------------------- |
| 工程实体/检测结果/用户 | `vision.db` / EF Core SQLite               | 主数据存储                         |
| 流程 JSON              | `IProjectFlowStorage` 对应目录             | 工程流程的独立文件化存储           |
| 应用设置               | `config.json`                              | 一般设置、通信、运行时、相机绑定等 |
| AI 模型配置            | `ai_models.json` / 兼容 `ai_config.json` | 模型列表与激活状态                 |
| 图像缓存               | 内存 +`IImageCacheRepository`              | 临时图像缓存与预览                 |

## 5. 当前版本的核心功能拆解

本章从“用户实际看到的功能”倒推到代码模块。

### 5.1 登录与权限

#### 功能表现

- 用户登录、退出。
- 获取当前登录用户。
- 修改密码。
- 页面访问前校验 Token。
- 用户管理页面仅特定角色可操作。

#### 关键代码

- 前端：`wwwroot/src/features/auth/auth.js`
- 后端接口：`Endpoints/AuthEndpoints.cs`
- 中间件：`Middleware/AuthMiddleware.cs`
- 服务：`Application/Services/AuthService.cs`
- 仓储：`Infrastructure/Repositories/UserRepository.cs`

#### 设计特点

- 认证令牌保存在前端 `localStorage`。
- `httpClient.js` 自动附带 `Authorization` 头。
- `AuthMiddleware` 做服务端兜底拦截。
- 首次启动会创建默认管理员 `admin / admin123`。

### 5.2 工程管理

#### 功能表现

- 新建工程。
- 打开工程。
- 编辑工程信息。
- 搜索工程、最近工程。
- 删除工程。
- 导入/导出工程。

#### 关键代码

- 前端：`projectManager.js`、`projectView.js`
- 后端接口：`Endpoints/ApiEndpoints.cs` 中 `/api/projects/*`
- 服务：`ProjectService.cs`
- 仓储：`ProjectRepository.cs`
- 流程存储：`JsonFileProjectFlowStorage.cs`

#### 设计特点

- 工程元信息走数据库。
- 流程定义走 JSON 文件。
- 打开工程时会把流程 JSON 再反序列化为 `OperatorFlowDto`，并通过 `EnrichFlowDtoWithMetadata()` 回填部分参数元数据。

### 5.3 流程编辑器

#### 功能表现

- 画布拖拽节点。
- 端口连线/断线。
- 多选、复制、粘贴、撤销、重做。
- 最小地图、节点菜单、节点状态显示。
- 支持模板快速套用。

#### 关键代码

- 主入口：`wwwroot/src/app.js`
- 画布：`core/canvas/flowCanvas.js`
- 交互：`features/flow-editor/flowEditorInteraction.js`
- 属性面板：`features/flow-editor/propertyPanel.js`
- 预览面板：`features/flow-editor/previewPanel.js`
- 模板选择：`features/flow-editor/templateSelector.js`

#### 设计特点

- `FlowCanvas` 管图元和绘制。
- `FlowEditorInteraction` 管鼠标/键盘/拖放/框选/撤销重做。
- `PropertyPanel` 管参数编辑与推荐。
- `PreviewPanel` 管“单节点预览”和软触发采图。

### 5.4 算子库

#### 功能表现

- 左侧分类树展示算子。
- 搜索过滤。
- 分类图标和算子图标。
- 拖入画布生成节点。

#### 关键代码

- 前端：`features/operator-library/operatorLibrary.js`
- 通用树组件：`shared/components/treeView.js`
- 后端接口：`/api/operators/library`、`/api/operators/types`、`/api/operators/{type}/metadata`
- 元数据：`OperatorFactory.cs`、`OperatorMetadataScanner.cs`

#### 设计特点

- 元数据既要给后端创建节点使用，也要给前端展示分类、端口、参数。
- 算子库 UI 与运行时执行器解耦：前端只关心元数据，真正执行交给 `IFlowExecutionService` 与 `IOperatorExecutor`。

### 5.5 参数编辑、参数推荐与预览

#### 功能表现

- 点击节点后显示参数表单。
- 根据参数类型生成输入框、下拉框、布尔开关等。
- 某些算子支持“推荐参数”。
- 可直接对节点做预览执行，查看输出图像和输出数据。

#### 关键代码

- `propertyPanel.js`
- `previewPanel.js`
- `OperatorPreviewService.cs`
- `ParameterRecommender.cs`

#### 设计特点

- 推荐逻辑并不是“AI 随机猜”，而是基于图像统计（如 Otsu、噪声分数、面积分位数、拉普拉斯方差）给建议。
- 预览服务会对输入值做归一化，并尝试抽取可展示的图像结果。

### 5.6 检测执行

#### 功能表现

- 单图执行。
- 相机单次执行。
- 连续实时执行。
- 流程驱动实时执行。
- 输出 OK/NG/Error、耗时、缺陷信息、输出图像。

#### 关键代码

- 前端：`inspectionController.js`、`inspectionPanel.js`、`analysisCardsPanel.js`
- 后端接口：`/api/inspection/*`
- 应用服务：`InspectionService.cs`
- 引擎：`FlowExecutionService.cs`
- 图像采集：`ImageAcquisitionService.cs`

#### 设计特点

- `InspectionService` 是“产品视角”的检测服务。
- `FlowExecutionService` 是“引擎视角”的流程执行器。
- 前者负责结果持久化、状态判定、实时循环；后者负责执行顺序、并行层、输出字典、调试会话。

### 5.7 结果管理与分析

#### 功能表现

- 历史结果列表。
- KPI 统计。
- 良率、趋势、缺陷分布。
- 导出与报告。

#### 关键代码

- 前端：`resultPanel.js`
- 后端接口：`/api/inspection/history/*`、`/api/inspection/statistics/*`、`/api/analysis/*`
- 服务：`ResultAnalysisService.cs`
- 仓储：`InspectionResultRepository.cs`

#### 设计特点

- `resultPanel.js` 既承担列表展示，也承担统计聚合与导出。
- `ResultAnalysisService` 负责更偏分析报表层的后端数据加工。

### 5.8 设置中心

#### 功能表现

- 总设置页包含多个 Tab：常规、通信、存储、运行时、相机、AI、用户管理。

#### 关键代码

- 前端：`settingsView.js`
- 后端：`SettingsEndpoints.cs`、`PlcEndpoints.cs`、`UserEndpoints.cs`
- 配置服务：`JsonConfigurationService.cs`
- AI 模型：`AiConfigStore.cs`
- 相机：`CameraManager.cs`

#### 设计特点

- 设置页是当前产品里一个**集成度最高**的页面。
- 它不仅改配置，还会调用设备发现、软触发、AI 模型测试、用户管理等接口。

### 5.9 标定

#### 功能表现

- 通用标定向导。
- 手眼标定向导。
- 标定结果求解与保存。

#### 关键代码

- 前端：`calibrationWizard.js`、`handEyeCalibWizard.js`
- 后端接口：`/api/calibration/solve`、`/api/calibration/save`
- 服务：`HandEyeCalibrationService.cs`

### 5.10 AI 工作流生成

#### 功能表现

- 用户输入自然语言描述。
- 可附带附件。
- 系统向模型发送请求。
- 前端接收流式进度、思考链、最终流程 JSON。
- 用户可将生成流程应用到画布。

#### 关键代码

- 前端：`aiPanel.js`、`aiGenerationDialog.js`
- WebMessage：`webMessageBridge.js`
- 后端消息处理：`WebMessageHandler.cs`、`GenerateFlowMessageHandler.cs`
- AI 主链：`AiFlowGenerationService.cs`、`AiApiClient.cs`、`AiConfigStore.cs`
- 兼容/旧链：`AIWorkflowService.cs`

#### 设计特点

- 当前实现已经支持模型列表管理，而不是把 AI 供应商写死。
- `AiApiClient` 同时兼容 OpenAI/Anthropic 风格调用与流式解析。
- `AiFlowGenerationService` 会处理附件筛选、提示词组装、失败重试、JSON 解析、布局补全。

### 5.11 相机与 PLC 集成

#### 相机

- 支持设备发现、绑定、参数保存、软触发采图。
- 代码上有 `CameraManager`、`FileCamera`、`MockCamera`、多种厂商相机 Provider。

#### PLC

- 提供连接测试、地址映射管理。
- 协议层已具备西门子、欧姆龙、三菱实现。

#### 关键代码

- 相机：`Infrastructure/Cameras/*`
- PLC：`Acme.PlcComm/*`
- 接口：`SettingsEndpoints.cs`、`PlcEndpoints.cs`

---

## 6. 核心领域模型

### 6.1 核心实体一览

| 实体                 | 作用       | 关键字段                                                                                  | 关键行为                                   |
| -------------------- | ---------- | ----------------------------------------------------------------------------------------- | ------------------------------------------ |
| `Project`          | 工程聚合根 | `Name`、`Description`、`Version`、`Flow`、`GlobalSettings`、`LastOpenedAt`    | 更新信息、记录打开时间、更新流程           |
| `OperatorFlow`     | 流程实体   | `Name`、`Operators`、`Connections`                                                  | 增删算子、增删连接、拓扑排序、环检测       |
| `Operator`         | 节点实体   | `Name`、`Type`、位置、端口、参数、执行状态                                            | 更新位置、增删端口、更新参数、标记执行状态 |
| `InspectionResult` | 检测结果   | `ProjectId`、状态、缺陷列表、输出图像、输出数据 JSON                                    | 添加缺陷、设定结果、设定输出图像/数据      |
| `User`             | 用户实体   | 用户名、密码哈希、显示名、角色、激活状态                                                  | 修改密码、停用/启用、改角色                |
| `AppConfig`        | 全局配置   | `General`、`Communication`、`Storage`、`Runtime`、`Cameras`、`ActiveCameraId` | 作为设置中心的数据总根                     |
| `FlowTemplate`     | 模板       | 名称、描述、行业、标签、`FlowJson`                                                      | 作为模板选择器的数据来源                   |

### 6.2 `Project` 的职责

`Project` 代表一个完整视觉工程，是很多业务操作的根对象。它并不自己执行图像处理，而是负责：

- 管理工程级元信息。
- 挂载当前流程 `OperatorFlow`。
- 记录全局设置和最近打开时间。

关键方法：

- `UpdateInfo()`：更新工程名称和描述。
- `UpdateVersion()`：更新版本号。
- `RecordOpen()`：记录最近打开时间。
- `SetGlobalSetting()` / `GetGlobalSetting()`：读写工程级配置项。
- `UpdateFlow()`：替换当前流程。

### 6.3 `OperatorFlow` 的职责

`OperatorFlow` 是整个流程引擎的核心数据模型。它承担：

- 保存节点列表和连接列表。
- 进行合法性校验。
- 计算执行顺序。
- 防止出现环路。

关键方法：

- `AddOperator()` / `RemoveOperator()` / `ClearOperators()`
- `AddConnection()` / `RemoveConnection()` / `ClearConnections()`
- `GetExecutionOrder()`：按拓扑关系给出执行顺序。
- `ValidateConnection()`：校验连接两端是否合法。
- `WouldCreateCycle()` / `HasCycle()`：防止回路。

### 6.4 `Operator` 的职责

`Operator` 是单个节点。它本身是运行时结构，不直接包含所有算法实现，但保存了执行所需的基本元数据：

- 节点名称与类型。
- 在画布中的位置。
- 输入输出端口定义。
- 参数定义和值。
- 执行状态与错误信息。

关键方法：

- `UpdateName()` / `UpdatePosition()`
- `AddInputPort()` / `AddOutputPort()`
- `LoadInputPort()` / `LoadOutputPort()`：恢复已有端口 ID 时使用。
- `AddParameter()` / `UpdateParameter()`
- `Enable()` / `Disable()`
- `MarkExecutionStarted()` / `MarkExecutionCompleted()` / `MarkExecutionFailed()` / `ResetExecutionStatus()`

### 6.5 `InspectionResult` 的职责

它既是数据库持久化对象，也是前端结果面板展示的数据来源。

关键方法：

- `AddDefect()`：追加缺陷。
- `SetResult()`：设置 OK/NG/Error 与耗时。
- `MarkAsError()`：异常路径快捷标记。
- `GetNGCount()`：统计不良数。
- `SetOutputImage()`：保存输出图像。
- `SetOutputDataJson()`：保存完整输出 JSON。

### 6.6 `AppConfig` 的结构

`AppConfig` 是设置中心的总根对象，包含：

- `GeneralConfig`：软件标题、主题、自启动。
- `CommunicationConfig`：PLC IP、端口、协议、心跳、地址映射。
- `StorageConfig`：图像保存目录、保存策略、保留天数、空间下限。
- `RuntimeConfig`：自动运行、连续 NG 停止阈值。
- `CameraBindingConfig`：相机逻辑绑定、序列号、曝光、增益、触发模式等。

---

## 7. HTTP API 总表

> 说明：本节按业务域分组列出当前主产品的 HTTP 接口，便于快速找前后端对接点。
> 其中一部分定义在 `Endpoints/*.cs`，一部分定义在 `Program.RegisterExtendedApiEndpoints()`。

### 7.1 健康检查

| 方法    | 路径            | 作用                       |
| ------- | --------------- | -------------------------- |
| `GET` | `/health`     | 本地宿主健康检查与端口探测 |
| `GET` | `/api/health` | API 层健康检查             |

### 7.2 认证接口

| 方法     | 路径                          | 作用         |
| -------- | ----------------------------- | ------------ |
| `POST` | `/api/auth/login`           | 登录         |
| `POST` | `/api/auth/logout`          | 登出         |
| `GET`  | `/api/auth/me`              | 获取当前用户 |
| `POST` | `/api/auth/change-password` | 修改密码     |

### 7.3 用户管理接口

| 方法       | 路径                               | 作用         |
| ---------- | ---------------------------------- | ------------ |
| `GET`    | `/api/users`                     | 获取用户列表 |
| `GET`    | `/api/users/{id}`                | 获取单个用户 |
| `POST`   | `/api/users`                     | 创建用户     |
| `PUT`    | `/api/users/{id}`                | 更新用户     |
| `DELETE` | `/api/users/{id}`                | 删除用户     |
| `POST`   | `/api/users/{id}/reset-password` | 重置密码     |

### 7.4 工程接口

| 方法       | 路径                        | 作用         |
| ---------- | --------------------------- | ------------ |
| `GET`    | `/api/projects`           | 获取全部工程 |
| `GET`    | `/api/projects/recent`    | 获取最近工程 |
| `GET`    | `/api/projects/search`    | 搜索工程     |
| `GET`    | `/api/projects/{id}`      | 获取工程详情 |
| `POST`   | `/api/projects`           | 创建工程     |
| `PUT`    | `/api/projects/{id}`      | 更新工程     |
| `DELETE` | `/api/projects/{id}`      | 删除工程     |
| `PUT`    | `/api/projects/{id}/flow` | 保存流程     |

### 7.5 检测接口

| 方法     | 路径                                       | 作用         |
| -------- | ------------------------------------------ | ------------ |
| `POST` | `/api/inspection/execute`                | 执行单次检测 |
| `GET`  | `/api/inspection/history/{projectId}`    | 获取历史结果 |
| `GET`  | `/api/inspection/statistics/{projectId}` | 获取统计信息 |
| `POST` | `/api/inspection/realtime/start`         | 启动实时检测 |
| `POST` | `/api/inspection/realtime/stop`          | 停止实时检测 |

### 7.6 算子与模板接口

| 方法     | 路径                                           | 作用               |
| -------- | ---------------------------------------------- | ------------------ |
| `GET`  | `/api/operators/library`                     | 获取算子库         |
| `GET`  | `/api/operators/types`                       | 获取算子类型列表   |
| `GET`  | `/api/operators/{type}/metadata`             | 获取单个算子元数据 |
| `GET`  | `/api/templates`                             | 获取模板列表       |
| `GET`  | `/api/templates/{id}`                        | 获取模板详情       |
| `POST` | `/api/operators/{type}/recommend-parameters` | 推荐参数           |
| `POST` | `/api/operators/{type}/preview`              | 预览节点执行结果   |

### 7.7 图像与标定接口

| 方法     | 路径                       | 作用              |
| -------- | -------------------------- | ----------------- |
| `POST` | `/api/calibration/solve` | 求解手眼/标定结果 |
| `POST` | `/api/calibration/save`  | 保存标定文件      |
| `POST` | `/api/images/upload`     | 上传图像到缓存    |
| `GET`  | `/api/images/{id}`       | 获取缓存图像      |

### 7.8 设置接口

| 方法     | 路径                         | 作用         |
| -------- | ---------------------------- | ------------ |
| `GET`  | `/api/settings`            | 获取全局设置 |
| `PUT`  | `/api/settings`            | 保存全局设置 |
| `POST` | `/api/settings/reset`      | 恢复默认设置 |
| `GET`  | `/api/settings/disk-usage` | 查询磁盘占用 |

### 7.9 AI 模型管理接口

| 方法       | 路径                             | 作用             |
| ---------- | -------------------------------- | ---------------- |
| `GET`    | `/api/ai/models`               | 获取 AI 模型列表 |
| `POST`   | `/api/ai/models`               | 新增模型         |
| `PUT`    | `/api/ai/models/{id}`          | 更新模型         |
| `DELETE` | `/api/ai/models/{id}`          | 删除模型         |
| `POST`   | `/api/ai/models/{id}/activate` | 激活模型         |
| `POST`   | `/api/ai/models/{id}/test`     | 测试模型连通性   |

### 7.10 相机接口

| 方法     | 路径                                  | 作用         |
| -------- | ------------------------------------- | ------------ |
| `GET`  | `/api/cameras/discover`             | 发现全部相机 |
| `GET`  | `/api/cameras/discover/huaray`      | 发现华睿相机 |
| `GET`  | `/api/cameras/discover/hikvision`   | 发现海康相机 |
| `GET`  | `/api/cameras/bindings`             | 获取相机绑定 |
| `PUT`  | `/api/cameras/bindings`             | 保存相机绑定 |
| `POST` | `/api/cameras/soft-trigger-capture` | 软触发采图   |

### 7.11 PLC 接口

| 方法     | 路径                         | 作用              |
| -------- | ---------------------------- | ----------------- |
| `POST` | `/api/plc/test-connection` | 测试 PLC 连接     |
| `GET`  | `/api/plc/mappings`        | 获取 PLC 地址映射 |
| `PUT`  | `/api/plc/mappings`        | 保存 PLC 地址映射 |

### 7.12 Demo 与分析接口

| 方法     | 路径                                              | 作用             |
| -------- | ------------------------------------------------- | ---------------- |
| `POST` | `/api/demo/create`                              | 创建演示工程     |
| `POST` | `/api/demo/create-simple`                       | 创建简化演示工程 |
| `GET`  | `/api/demo/guide`                               | 获取演示指南     |
| `GET`  | `/api/analysis/statistics/{projectId}`          | 统计分析         |
| `GET`  | `/api/analysis/defect-distribution/{projectId}` | 缺陷分布         |
| `GET`  | `/api/analysis/trend/{projectId}`               | 趋势分析         |
| `GET`  | `/api/analysis/report/{projectId}`              | 报告生成         |

## 8. 桌面宿主与后端主要代码方法导读

> 本章按“文件/类 -> 主要方法 -> 方法职责”展开。为了保证可读性，纯工具型的小 helper 不全列；但主链路方法会尽量覆盖。

### 8.1 宿主入口：`Program.cs`

#### 类职责

`Program` 是整个桌面产品的真正启动入口，负责把 WinForms、Web 服务、数据库初始化、默认账号初始化和扩展 API 接口串起来。

#### 主要方法

| 方法                                                 | 作用                                                                |
| ---------------------------------------------------- | ------------------------------------------------------------------- |
| `Main()`                                           | 注册异常处理、启动本地 Web 服务、配置日志并运行主窗体               |
| `StartWebServer()`                                 | 创建 `WebApplication`、注册服务、初始化数据库、挂载静态资源和 API |
| `RegisterExtendedApiEndpoints(WebApplication app)` | 注册 Demo/Analysis 等扩展 API                                       |
| `StopWebServer()`                                  | 应用关闭时优雅停止本地 Web 服务                                     |
| `FindAvailablePort(int startPort, int endPort)`    | 动态寻找可用端口                                                    |
| `GetWebPort()`                                     | 供 `WebView2Host` 获取当前 API 端口                               |

#### 阅读重点

- 看它如何把 `AddVisionServices()` 与 `AddAiFlowGeneration()` 组合起来。
- 看它如何 `EnsureCreated()` 数据库，并初始化默认管理员。
- 看它如何挂载 `AuthMiddleware` 与各个 Endpoint。

### 8.2 主窗口：`MainForm.cs`

#### 类职责

`MainForm` 是 WinForms 侧的 UI 外壳，真正业务界面在 WebView2 中。

#### 主要方法

| 方法                       | 作用                          |
| -------------------------- | ----------------------------- |
| `MainForm()`             | 初始化窗体和事件绑定          |
| `MainForm_Load()`        | 创建并初始化 `WebView2Host` |
| `InitializeMenu()`       | 构建顶部菜单或宿主级命令      |
| `MainForm_FormClosing()` | 关闭前清理宿主与资源          |
| `InitializeComponent()`  | WinForms 组件初始化           |

### 8.3 Web 宿主桥：`WebView2Host.cs`

#### 类职责

`WebView2Host` 是桌面侧“浏览器运行环境”的总控件，负责注入 API 地址、初始化 WebView2、处理 SharedBuffer、执行脚本与缓存控制。

#### 主要方法

| 方法                                                     | 作用                                                |
| -------------------------------------------------------- | --------------------------------------------------- |
| `InitializeAsync(...)`                                 | 初始化 WebView2 宿主和环境                          |
| `ConfigureWebView2Async()`                             | 配置 WebView2 设置、注入 API Base URL、注册资源规则 |
| `RegisterMessageHandlers()`                            | 绑定消息处理机制                                    |
| `OnWebMessageReceived(...)`                            | 接收前端发送的 WebMessage                           |
| `HandleMessageAsync(WebMessage message)`               | 将消息路由给 `WebMessageHandler`                  |
| `GetWwwRootPath()`                                     | 定位前端静态资源目录                                |
| `LoadInitialPage()`                                    | 加载起始页                                          |
| `Navigate(string url)`                                 | 页面导航                                            |
| `ExecuteScriptAsync(string script)`                    | 从宿主执行脚本                                      |
| `SendSharedBuffer(byte[] data, int width, int height)` | 通过共享缓冲区向前端推图像                          |
| `ClearCacheAsync()`                                    | 清理 WebView2 缓存                                  |
| `ForceReloadAsync()`                                   | 强制刷新页面                                        |
| `GenerateCssVersion()`                                 | 生成 CSS 版本号用于缓存失效                         |
| `OnWebResourceRequested(...)`                          | 资源请求拦截点                                      |
| `DisposeAsync()`                                       | 释放 WebView2 与订阅                                |

### 8.4 Web 消息调度：`WebMessageHandler.cs`

#### 类职责

`WebMessageHandler` 是桌面侧 WebMessage 的总入口，负责把前端消息翻译成后端服务调用，并把结果再推回前端。

#### 主要方法

| 方法                                                                | 作用                                      |
| ------------------------------------------------------------------- | ----------------------------------------- |
| `HandleAsync(WebMessage message)`                                 | 统一处理一条消息并返回通用响应            |
| `Initialize(WebView2 webViewControl)`                             | 绑定 WebView2 对象                        |
| `OnWebMessageReceived(...)`                                       | 接收消息事件并异步转发                    |
| `HandleWebMessageAsync(...)`                                      | 解析 JSON、识别消息类型、路由到具体处理器 |
| `HandleExecuteOperatorCommand(string messageJson)`                | 执行单算子                                |
| `HandleUpdateFlowCommand(string messageJson)`                     | 保存流程                                  |
| `HandleStartInspectionCommand(string messageJson)`                | 启动检测                                  |
| `HandleStopInspectionCommand()`                                   | 停止检测                                  |
| `HandlePickFileCommand(string messageJson)`                       | 打开文件选择                              |
| `HandleListAiSessionsCommand()`                                   | 获取 AI 会话列表                          |
| `HandleGetAiSessionCommand(string messageJson)`                   | 获取 AI 会话详情                          |
| `HandleDeleteAiSessionCommand(string messageJson)`                | 删除 AI 会话                              |
| `ExtractSessionId(string messageJson)`                            | 从消息中提取会话 ID                       |
| `HandleGenerateFlowCommand(string messageJson)`                   | 触发 AI 流程生成                          |
| `HandleHandEyeSolveCommand(string messageJson)`                   | 触发手眼标定求解                          |
| `HandleHandEyeSaveCommand(string messageJson)`                    | 保存标定结果                              |
| `NotifyInspectionResult(InspectionResult result, Guid projectId)` | 主动向前端推送检测结果                    |
| `SendProgressMessage(string type, object payload)`                | 主动推送进度消息                          |
| `PostWebMessageJson(string json)`                                 | 低层消息发送                              |

#### 当前系统里它处理的典型消息

- `ExecuteOperatorCommand`
- `UpdateFlowCommand`
- `StartInspectionCommand`
- `StopInspectionCommand`
- `PickFileCommand`
- `GenerateFlow`
- `ListAiSessions`
- `GetAiSession`
- `DeleteAiSession`
- `handeye:solve`
- `handeye:save`

### 8.5 认证中间件：`AuthMiddleware.cs`

#### 类职责

对 HTTP 请求做统一权限校验。

#### 主要方法

| 方法                                                           | 作用                       |
| -------------------------------------------------------------- | -------------------------- |
| `InvokeAsync(HttpContext context, IAuthService authService)` | 核心认证拦截逻辑           |
| `IsWhitelisted(string path)`                                 | 判断是否跳过认证           |
| `ExtractToken(HttpContext context)`                          | 从 Header 等位置提取 Token |
| `WriteUnauthorizedResponse(HttpContext context)`             | 输出 401 响应              |

### 8.6 API 汇总类

#### `ApiEndpoints.cs`

| 方法                            | 作用                               |
| ------------------------------- | ---------------------------------- |
| `MapVisionApiEndpoints(...)`  | 注册视觉主接口                     |
| `MapProjectEndpoints(...)`    | 注册工程接口                       |
| `MapInspectionEndpoints(...)` | 注册检测接口                       |
| `MapOperatorEndpoints(...)`   | 注册算子、模板、预览、参数推荐接口 |
| `TryDecodeImage(...)`         | 图像解码辅助                       |
| `MapImageEndpoints(...)`      | 注册图像缓存接口                   |

#### `AuthEndpoints.cs`

| 方法                                        | 作用                            |
| ------------------------------------------- | ------------------------------- |
| `MapAuthEndpoints(...)`                   | 注册登录/登出/用户信息/改密接口 |
| `GetTokenFromHeader(HttpContext context)` | 从 Header 提取 Token            |

#### `PlcEndpoints.cs`

| 方法                                     | 作用                        |
| ---------------------------------------- | --------------------------- |
| `MapPlcEndpoints(...)`                 | 注册 PLC 连接测试与映射接口 |
| `NormalizeMappings(...)`               | 标准化 PLC 映射配置         |
| `SafeDisconnectAsync(...)`             | 安全断开 PLC 客户端         |
| `TryBuildPlcCommConnectionString(...)` | 构造 PLC 连接串             |
| `TestTcpReachabilityAsync(...)`        | 先测 TCP 是否可达           |

#### `SettingsEndpoints.cs`

| 方法                                        | 作用                            |
| ------------------------------------------- | ------------------------------- |
| `MapSettingsEndpoints(...)`               | 注册设置、AI 模型、相机相关接口 |
| `MapDiscoveredDevices(...)`               | 映射发现到的相机设备信息        |
| `BuildHuarayDiagnostics(int deviceCount)` | 构建华睿设备发现诊断信息        |
| `TryReadPngDimensions(...)`               | 读取 PNG 尺寸                   |
| `TryBuildDiskUsage(...)`                  | 计算磁盘占用                    |
| `CloneStringList(...)`                    | 复制列表防止引用污染            |

#### `UserEndpoints.cs`

| 方法                                  | 作用                   |
| ------------------------------------- | ---------------------- |
| `MapUserEndpoints(...)`             | 注册用户管理接口       |
| `IsAdminAsync(HttpContext context)` | 判断当前请求是否管理员 |

### 8.7 认证与用户：应用服务层

#### `AuthService.cs`

| 方法                                                                           | 作用                     |
| ------------------------------------------------------------------------------ | ------------------------ |
| `LoginAsync(string username, string password)`                               | 登录、校验密码、创建会话 |
| `LogoutAsync(string token)`                                                  | 登出并失效会话           |
| `ValidateTokenAsync(string token)`                                           | 校验 Token 是否有效      |
| `GetSessionAsync(string token)`                                              | 获取会话信息             |
| `ChangePasswordAsync(string userId, string oldPassword, string newPassword)` | 修改密码                 |

#### `UserManagementService.cs`

| 方法                                                      | 作用             |
| --------------------------------------------------------- | ---------------- |
| `GetAllUsersAsync(...)`                                 | 获取全部用户     |
| `GetActiveUsersAsync(...)`                              | 获取活跃用户     |
| `GetUserByIdAsync(string id)`                           | 获取单个用户     |
| `CreateUserAsync(CreateUserRequest request)`            | 创建用户         |
| `UpdateUserAsync(string id, UpdateUserRequest request)` | 更新用户         |
| `DeleteUserAsync(string id)`                            | 删除用户         |
| `ResetPasswordAsync(string id, string newPassword)`     | 重置密码         |
| `IsUsernameAvailableAsync(string username)`             | 用户名可用性检查 |

### 8.8 工程与算子：应用服务层

#### `ProjectService.cs`

`ProjectService` 是工程管理最重要的应用服务之一。它的一个关键特征是：**流程保存优先走文件存储，而不是把整张流程图完全交给 EF Core 做复杂聚合持久化**。

| 方法                                                       | 作用                                  |
| ---------------------------------------------------------- | ------------------------------------- |
| `CreateAsync(CreateProjectRequest request)`              | 创建工程并在必要时保存流程 JSON       |
| `GetByIdAsync(Guid id)`                                  | 获取工程详情，并尝试从文件恢复流程    |
| `EnrichFlowDtoWithMetadata(OperatorFlowDto flowDto)`     | 补全流程 DTO 中缺失的参数选项等元数据 |
| `GetAllAsync()`                                          | 获取工程列表                          |
| `UpdateAsync(Guid id, UpdateProjectRequest request)`     | 更新工程信息，必要时更新流程文件      |
| `UpdateFlowAsync(Guid id, UpdateFlowRequest request)`    | 直接保存流程 JSON                     |
| `MapDtoToFlow(OperatorFlowDto dto, Guid? flowId = null)` | DTO 转领域实体                        |
| `DeleteAsync(Guid id)`                                   | 删除工程                              |
| `SearchAsync(string keyword)`                            | 搜索工程                              |
| `GetRecentlyOpenedAsync(int count = 10)`                 | 获取最近工程                          |
| `MapToDto(Project project)`                              | 实体转 DTO                            |
| `MapFlowToDto(OperatorFlow flow)`                        | 流程转 DTO                            |
| `MapOperatorToDto(Operator op)`                          | 节点转 DTO                            |
| `MapPortToDto(Port port)`                                | 端口转 DTO                            |
| `MapParameterToDto(Parameter param)`                     | 参数转 DTO                            |
| `MapConnectionToDto(OperatorConnection conn)`            | 连线转 DTO                            |

#### `OperatorService.cs`

| 方法                                                    | 作用                       |
| ------------------------------------------------------- | -------------------------- |
| `InitializeMetadataCache()`                           | 初始化一份兼容式元数据缓存 |
| `GetLibraryAsync()`                                   | 获取算子库 DTO 列表        |
| `GetByIdAsync(Guid id)`                               | 根据 ID 获取算子           |
| `GetByTypeAsync(OperatorType type)`                   | 根据类型获取算子           |
| `CreateAsync(CreateOperatorRequest request)`          | 创建算子                   |
| `UpdateAsync(Guid id, UpdateOperatorRequest request)` | 更新算子                   |
| `DeleteAsync(Guid id)`                                | 删除算子                   |
| `ValidateParametersAsync(...)`                        | 校验参数                   |
| `GetOperatorTypesAsync()`                             | 获取算子类型说明           |
| `GetMetadataAsync(OperatorType type)`                 | 获取元数据                 |
| `MapToDto(...)` / `MapEntityToDto(...)`             | DTO 与实体转换             |

### 8.9 检测与结果分析：应用服务层

#### `InspectionService.cs`

`InspectionService` 是产品业务里最关键的服务之一。它负责把“输入图像/相机 + 工程流程”转成用户能看到的检测结果，并负责实时循环与结果持久化。

| 方法                                                                         | 作用                               |
| ---------------------------------------------------------------------------- | ---------------------------------- |
| `ExecuteSingleAsync(Guid projectId, byte[] imageData)`                     | 单图检测入口（基于项目流程）       |
| `ExecuteSingleAsync(Guid projectId, byte[] imageData, OperatorFlow? flow)` | 单图检测主实现，支持前端直接传流程 |
| `ExecuteSingleAsync(Guid projectId, string cameraId)`                      | 从相机采图后执行检测               |
| `StartRealtimeInspectionAsync(...)`                                        | 启动相机驱动的实时检测             |
| `StartRealtimeInspectionFlowAsync(...)`                                    | 启动流程驱动的实时检测             |
| `StopRealtimeInspectionAsync(Guid projectId)`                              | 停止实时检测                       |
| `RunRealtimeInspectionLoopAsync(...)`                                      | 实时检测循环主体                   |
| `GetStopOnConsecutiveNgThreshold()`                                        | 读取连续 NG 停机阈值               |
| `PersistResultImageAsync(...)`                                             | 持久化结果图像                     |
| `TrySetOutputDataJson(...)`                                                | 把输出字典写回结果 JSON            |
| `DetermineStatusFromFlowOutput(...)`                                       | 根据流程输出判定 OK/NG/Error       |
| `GetInspectionHistoryAsync(...)`                                           | 获取检测历史                       |
| `GetStatisticsAsync(...)`                                                  | 获取统计汇总                       |

#### `ResultAnalysisService.cs`

| 方法                                    | 作用             |
| --------------------------------------- | ---------------- |
| `GetStatisticsAsync(...)`             | 统计汇总         |
| `GetDefectDistributionAsync(...)`     | 缺陷分布         |
| `GetConfidenceDistributionAsync(...)` | 置信度分布       |
| `GetTrendAnalysisAsync(...)`          | 趋势分析         |
| `ExportToCsvAsync(...)`               | 导出 CSV         |
| `ExportToJsonAsync(...)`              | 导出 JSON        |
| `GenerateReportAsync(...)`            | 生成报告         |
| `ComparePeriodsAsync(...)`            | 分时段对比       |
| `GetDefectHeatmapAsync(...)`          | 缺陷热力图       |
| `GenerateRecommendations(...)`        | 根据统计给出建议 |
| `GenerateComparisonSummary(...)`      | 生成对比总结     |
| `CalculateChange(...)`                | 计算变化率       |

#### `DemoProjectService.cs`

| 方法                                 | 作用             |
| ------------------------------------ | ---------------- |
| `CreateDemoProjectAsync()`         | 生成完整演示工程 |
| `CreateSimpleDemoProjectAsync()`   | 生成简化演示工程 |
| `GetDemoGuide()`                   | 获取演示工程说明 |
| `MapProjectToDto(Project project)` | 实体转 DTO       |

### 8.10 流程执行引擎：`FlowExecutionService.cs`

`FlowExecutionService` 是后端执行引擎的核心。它不关心“这个结果在 UI 上长什么样”，而是关心：

- 流程怎么排执行顺序。
- 每个节点如何拿输入、产输出。
- 输出如何在后续节点间传递。
- 是否允许并行。
- 如何保留调试状态与调试缓存。

#### 主要方法

| 方法                                                                                   | 作用                          |
| -------------------------------------------------------------------------------------- | ----------------------------- |
| `ExecuteFlowAsync(...)`                                                              | 执行整条流程                  |
| `ExecuteFlowSequentialAsync(...)`                                                    | 按顺序执行流程                |
| `ExecuteFlowParallelAsync(...)`                                                      | 按层并行执行流程              |
| `BuildExecutionLayers(...)`                                                          | 计算可并行执行的层级          |
| `ExecuteOperatorInternalAsync(...)`                                                  | 内部执行单节点                |
| `ExecuteOperatorAsync(Operator operator, Dictionary<string, object>? inputs = null)` | 外部显式执行单节点            |
| `ValidateFlow(OperatorFlow flow)`                                                    | 流程静态校验                  |
| `GetExecutionStatus(Guid flowId)`                                                    | 获取流程运行状态              |
| `CancelExecutionAsync(Guid flowId)`                                                  | 取消流程执行                  |
| `TryNormalizeOutputValue(...)`                                                       | 归一化输出值，便于序列化/传输 |
| `ApplyFanOutRefCounts(...)`                                                          | 管理分叉节点的引用计数        |
| `ShouldSkipImplicitFallbackValue(...)`                                               | 判断是否跳过隐式回退值        |
| `IsStandardImageOutputKey(string key)`                                               | 判断是否标准图像输出键        |
| `ContainsImageWrapperReference(...)`                                                 | 检查图像包装引用              |
| `ExecuteFlowDebugAsync(...)`                                                         | 执行调试版流程                |
| `ClearDebugCacheAsync(Guid debugSessionId)`                                          | 清理调试缓存                  |
| `TouchDebugSession(Guid debugSessionId)`                                             | 更新调试会话活跃时间          |
| `CleanupStaleDebugSessions(...)`                                                     | 清理过期调试会话              |
| `CleanupStaleExecutionStatuses(...)`                                                 | 清理过期运行状态              |
| `Dispose()`                                                                          | 释放内部资源                  |

### 8.11 图像采集与配置：基础设施服务层

#### `ImageAcquisitionService.cs`

| 方法                                                              | 作用                 |
| ----------------------------------------------------------------- | -------------------- |
| `LoadFromFileAsync(...)`                                        | 从文件加载图像       |
| `LoadFromBytesAsync(...)`                                       | 从字节加载图像       |
| `LoadFromBase64Async(...)`                                      | 从 Base64 加载图像   |
| `AcquireFromCameraAsync(string cameraId, ...)`                  | 从相机采集单帧       |
| `StartContinuousAcquisitionAsync(...)`                          | 启动连续采集         |
| `StopContinuousAcquisitionAsync(string cameraId)`               | 停止连续采集         |
| `GetSupportedFormatsAsync()`                                    | 获取支持的图像格式   |
| `ValidateImageFileAsync(string filePath)`                       | 校验图像文件         |
| `PreprocessAsync(Guid imageId, ImagePreprocessOptions options)` | 对缓存图像做预处理   |
| `SaveToFileAsync(Guid imageId, string filePath, ...)`           | 将缓存图像保存到文件 |
| `GetImageInfoAsync(Guid imageId)`                               | 获取图像信息         |
| `ReleaseImageAsync(Guid imageId)`                               | 释放缓存图像         |
| `AddToCache(...)` / `RemoveFromCache(...)`                    | 管理内存缓存         |
| `GetFormatFromFileName(...)`                                    | 推断格式             |
| `Dispose()`                                                     | 释放缓存和采集任务   |

#### `JsonConfigurationService.cs`

| 方法                            | 作用                                              |
| ------------------------------- | ------------------------------------------------- |
| `LoadAsync()`                 | 从 `config.json` 读取配置，不存在则创建默认配置 |
| `SaveAsync(AppConfig config)` | 保存配置                                          |
| `GetCurrent()`                | 获取缓存中的当前配置                              |

#### `HandEyeCalibrationService.cs`

| 方法                                                                       | 作用                 |
| -------------------------------------------------------------------------- | -------------------- |
| `SolveAsync(List<CalibrationPoint> points)`                              | 根据点对求解标定结果 |
| `SaveCalibrationAsync(HandEyeCalibrationResult result, string fileName)` | 保存标定文件         |

### 8.12 算子元数据、预览与推荐：基础设施服务层

#### `OperatorFactory.cs`

| 方法                                            | 作用                             |
| ----------------------------------------------- | -------------------------------- |
| `CreateOperator(...)`                         | 根据元数据实例化节点、端口、参数 |
| `GetMetadata(OperatorType type)`              | 获取单个元数据                   |
| `GetAllMetadata()`                            | 获取全部元数据                   |
| `GetSupportedOperatorTypes()`                 | 获取支持的枚举类型               |
| `RegisterOperator(OperatorMetadata metadata)` | 运行时注册元数据                 |
| `InitializeDefaultOperators()`                | 加载默认元数据集合               |

#### `OperatorMetadataScanner.cs`

| 方法                                       | 作用                  |
| ------------------------------------------ | --------------------- |
| `Scan()`                                 | 扫描默认算子程序集    |
| `Scan(Assembly assembly)`                | 扫描单个程序集        |
| `Scan(IEnumerable<Assembly> assemblies)` | 扫描多个程序集        |
| `GetCandidateOperatorTypes(...)`         | 找出候选算子类型      |
| `GetLoadableTypes(...)`                  | 安全读取可加载类型    |
| `TryBuildMetadata(Type operatorClrType)` | 从特性构造元数据      |
| `TryResolveOperatorType(...)`            | 推断 `OperatorType` |
| `BuildOptions(string[]? options)`        | 构造参数选项列表      |

#### `OperatorPreviewService.cs`

| 方法                             | 作用                 |
| -------------------------------- | -------------------- |
| `PreviewAsync(...)`            | 对单个算子做预览执行 |
| `ApplyParameters(...)`         | 将参数应用到预览算子 |
| `NormalizeInputValue(...)`     | 归一化预览输入       |
| `TryExtractImageBase64(...)`   | 尝试抽取可显示图像   |
| `TryConvertToBase64(...)`      | 转换图像输出         |
| `IsImageCarrier(...)`          | 判断是否图像载体     |
| `TryNormalizeOutputValue(...)` | 输出归一化           |
| `DisposeImageCarriers(...)`    | 释放图像载体资源     |
| `Failure(string message)`      | 构造失败结果         |

#### `ParameterRecommender.cs`

| 方法                                             | 作用                           |
| ------------------------------------------------ | ------------------------------ |
| `Recommend(OperatorType type, Mat inputImage)` | 根据输入图像和算子类型推荐参数 |
| `RecommendThresholding(Mat source)`            | 为阈值类算子推荐参数           |
| `RecommendFiltering(Mat source)`               | 为滤波类算子推荐参数           |
| `RecommendBlobAnalysis(Mat source)`            | 为 Blob 分析推荐参数           |
| `RecommendSharpnessEvaluation(Mat source)`     | 为清晰度类推荐参数             |
| `EnsureGray(Mat source)`                       | 灰度化辅助                     |
| `Percentile(...)`                              | 分位数计算                     |

### 8.13 智能检测、相机与设备：基础设施服务层

#### `IntelligentDetectionService.cs`

| 方法                                           | 作用                       |
| ---------------------------------------------- | -------------------------- |
| `ExecuteWithRetryAsync(...)`                 | 带重试策略执行检测         |
| `AdjustExposureAsync(...)`                   | 根据亮度自适应调整曝光     |
| `CalculateImageBrightness(byte[] imageData)` | 计算图像亮度               |
| `DualModalVoting(...)`                       | 深度学习与传统算法结果融合 |
| `ParseFlowResult(...)`                       | 流程执行结果转检测结果     |
| `MergeItems(...)`                            | 合并检测项                 |
| `CalculateIoU(...)`                          | 计算矩形 IoU               |

#### `CameraManager.cs`

| 方法                                            | 作用               |
| ----------------------------------------------- | ------------------ |
| `EnumerateCamerasAsync()`                     | 枚举相机           |
| `GetOrCreateCameraAsync(string cameraId)`     | 获取或创建相机实例 |
| `GetOrCreateByBindingAsync(string bindingId)` | 按绑定获取相机     |
| `OpenCameraAsync(string cameraId)`            | 打开相机           |
| `CloseCameraAsync(string cameraId)`           | 关闭相机           |
| `GetCamera(string cameraId)`                  | 获取已存在相机     |
| `DisconnectAllAsync()`                        | 断开所有相机       |
| `LoadBindings(...)`                           | 加载绑定           |
| `GetBindings()`                               | 获取绑定           |
| `UpdateBindings(...)`                         | 更新绑定           |
| `Dispose()`                                   | 释放相机管理器     |

### 8.14 AI：新主链与兼容链

#### `AiConfigStore.cs`

| 方法                                            | 作用                     |
| ----------------------------------------------- | ------------------------ |
| `GetAll()`                                    | 获取全部模型配置         |
| `GetById(string id)`                          | 获取单个模型             |
| `Get()`                                       | 获取当前激活配置映射     |
| `Add(AiModelConfig model)`                    | 新增模型                 |
| `Update(string id, AiModelConfig updated)`    | 更新模型                 |
| `Delete(string id)`                           | 删除模型                 |
| `SetActive(string id)`                        | 激活模型                 |
| `LoadOrMigrate(AiGenerationOptions fallback)` | 从新文件或旧文件迁移配置 |
| `EnsureOneActive(...)`                        | 保证至少一个激活模型     |
| `EnsureCapabilities(...)`                     | 补齐能力描述             |
| `EnsureAdvancedFields(...)`                   | 补齐高级字段             |
| `Save()`                                      | 持久化模型配置           |
| `CloneModel(...)`                             | 深拷贝模型               |

#### `AiApiClient.cs`

| 方法                                                           | 作用                    |
| -------------------------------------------------------------- | ----------------------- |
| `CompleteAsync(...)`                                         | 非流式调用 AI           |
| `StreamCompleteAsync(...)`                                   | 流式调用 AI             |
| `CallAnthropicAsync(...)`                                    | 调用 Anthropic 风格接口 |
| `StreamAnthropicAsync(...)`                                  | Anthropic 流式          |
| `CallOpenAiAsync(...)`                                       | 调用 OpenAI 风格接口    |
| `StreamOpenAiAsync(...)`                                     | OpenAI 流式             |
| `BuildAnthropicMessage(...)`                                 | 构造 Anthropic 消息     |
| `BuildAnthropicContentParts(...)`                            | 构造 Anthropic 内容段   |
| `BuildOpenAiMessage(...)`                                    | 构造 OpenAI 消息        |
| `BuildOpenAiContentParts(...)`                               | 构造 OpenAI 内容段      |
| `TryReadImageData(...)`                                      | 读取图像附件            |
| `GetImageMediaType(...)`                                     | 获取图像 MIME           |
| `IsSupportedImageExtension(...)`                             | 判断附件扩展名是否支持  |
| `ExtractOpenAiMessageContent(...)`                           | 抽取普通响应文本        |
| `TryExtractDeltaContent(...)`                                | 抽取流式分块            |
| `TryProcessOpenAiStreamPayload(...)`                         | 解析 OpenAI 流式载荷    |
| `TryExtractReasoningChunk(...)`                              | 提取 reasoning 分块     |
| `TryExtractChoiceContent(...)`                               | 提取 choice 内容        |
| `TryExtractTextProperty(...)` / `TryExtractTextValue(...)` | 文本抽取辅助            |
| `TryExtractJsonObject(...)`                                  | 从文本中抽 JSON 对象    |
| `TryParseOpenAiNonStreamingPayload(...)`                     | 解析非流式 OpenAI 结果  |
| `EnsureSuccessStatusCodeWithDetailsAsync(...)`               | 错误增强                |
| `TextPart(...)` / `ImageFile(...)`                         | 构建消息内容部件        |

#### `AiFlowGenerationService.cs`

| 方法                                        | 作用                |
| ------------------------------------------- | ------------------- |
| `GenerateFlowAsync(...)`                  | AI 流程生成主入口   |
| `BuildUserMessage(...)`                   | 构造用户消息        |
| `BuildUserChatMessage(...)`               | 组装多模态聊天消息  |
| `AnalyzeMultimodalAttachments(...)`       | 分析附件可用性      |
| `NormalizeAttachmentPaths(...)`           | 规整附件路径        |
| `IsBadRequestHttpException(Exception ex)` | 判断是否 400 类异常 |
| `BuildFallbackAttachmentReport(...)`      | 构建附件回退报告    |
| `BuildAttachmentContext(...)`             | 构造附件文本上下文  |
| `DescribeAttachment(string filePath)`     | 描述附件            |
| `FormatByteSize(long byteSize)`           | 格式化文件大小      |
| `BuildRetryMessage(...)`                  | 构造重试提示词      |
| `ParseAiResponse(string rawResponse)`     | 解析 AI 返回 JSON   |
| `ConvertToFlowDto(...)`                   | AI 结果转流程 DTO   |
| `ConvertDtoToEntity(...)`                 | DTO 转领域流程      |

#### `GenerateFlowMessageHandler.cs`

| 方法                 | 作用                           |
| -------------------- | ------------------------------ |
| `HandleAsync(...)` | 对接 WebMessage 的 AI 生成入口 |

#### `AIWorkflowService.cs`

> 这套链路更像“完整工作流编排引擎”的兼容/保留实现，含 PromptBuilder、Parser、Linter、DryRun。

| 方法                                                                                  | 作用                      |
| ------------------------------------------------------------------------------------- | ------------------------- |
| `GenerateFlowAsync(...)`                                                            | 完整 AI 工作流生成        |
| `ValidateFlow(OperatorFlow flow, bool enableDryRun = true)`                         | 对已有流程做校验和 DryRun |
| `Success(...)` / `Failure(...)`                                                   | 生成统一结果              |
| `LogInformation(...)` / `LogWarning(...)` / `LogError(...)` / `LogDebug(...)` | 工作流日志辅助            |

### 8.15 仓储与持久化层

#### `ProjectRepository.cs`

| 方法                                       | 作用           |
| ------------------------------------------ | -------------- |
| `GetAllAsync()`                          | 获取全部工程   |
| `GetByNameAsync(string name)`            | 按名称查工程   |
| `GetRecentlyOpenedAsync(int count = 10)` | 最近工程       |
| `SearchAsync(string keyword)`            | 搜索工程       |
| `GetWithFlowAsync(Guid id)`              | 带流程加载工程 |
| `UpdateFlowAsync(Project project)`       | 更新流程       |

#### `InspectionResultRepository.cs`

| 方法                         | 作用         |
| ---------------------------- | ------------ |
| `GetByProjectIdAsync(...)` | 分页查结果   |
| `GetByTimeRangeAsync(...)` | 按时间查结果 |
| `GetStatisticsAsync(...)`  | 统计结果     |

#### `ImageCacheRepository.cs`

| 方法                                          | 作用         |
| --------------------------------------------- | ------------ |
| `AddAsync(byte[] imageData, string format)` | 加入缓存     |
| `GetAsync(Guid id)`                         | 读取缓存     |
| `DeleteAsync(Guid id)`                      | 删除缓存     |
| `CleanExpiredAsync(TimeSpan expiration)`    | 清理过期缓存 |

### 8.16 PLC 通信组件：`Acme.PlcComm`

#### 组件职责

该组件把多厂商 PLC 协议做成统一客户端抽象。

#### 核心抽象

- `PlcBaseClient`：统一连接、读写、重连、日志、异常上报。
- `OperateResult`：统一结果模型。
- `PlcAddress`：统一地址模型。
- `IPlcClient`：统一客户端接口。

#### 主要方法

##### `PlcBaseClient.cs`

| 方法                                              | 作用            |
| ------------------------------------------------- | --------------- |
| `ConnectAsync(...)`                             | 建连            |
| `DisconnectAsync()`                             | 断连            |
| `ReadAsync(string address, ushort length, ...)` | 读原始字节      |
| `WriteAsync(string address, byte[] value, ...)` | 写原始字节      |
| `ReadStringAsync(...)`                          | 读字符串        |
| `WriteStringAsync(...)`                         | 写字符串        |
| `PingAsync(...)`                                | 心跳/可达性检测 |
| `ExecuteWithReconnectAsync(...)`                | 自动重连包装    |
| `LogFrame(...)`                                 | 帧日志          |
| `RaiseError(...)`                               | 错误上报        |
| `GetRetryDelay(int retry)`                      | 重试延时        |
| `DisconnectInternalAsync(...)`                  | 内部断开        |
| `Dispose()`                                     | 释放资源        |

##### 地址解析器 / 报文构建器

- `S7AddressParser`：解析西门子地址。
- `FinsAddressParser` / `FinsFrameBuilder`：欧姆龙 FINS 地址和报文。
- `McAddressParser` / `McFrameBuilder`：三菱 MC 地址和报文。

##### 协议客户端

- `SiemensS7Client`：西门子实现。
- `OmronFinsClient`：欧姆龙实现。
- `MitsubishiMcClient`：三菱实现。

## 9. 前端主要代码方法导读

> 本章按“基础框架 -> 核心画布 -> 业务页面”顺序阅读。

### 9.1 应用入口：`app.js`

#### 文件职责

这是前端总入口。它负责：

- 全局错误监听。
- 登录校验。
- 初始化各视图模块。
- 管理导航切换。
- 管理加载页和欢迎页。

#### 主要方法

| 方法                                              | 作用                         |
| ------------------------------------------------- | ---------------------------- |
| `addErrorLog(logEntry)`                         | 记录全局错误                 |
| `trackedSubscribe(subscribeFn, callback)`       | 统一管理订阅，避免泄漏       |
| `initializeApp()`                               | 整体初始化前端应用           |
| `initializeNavigation()`                        | 导航栏绑定                   |
| `switchView(view)`                              | 切换视图                     |
| `initializeInspectionPanel()`                   | 初始化检测面板               |
| `initializeInspectionImageViewer()`             | 初始化检测视图里的图像查看器 |
| `updateInspectionResultsPanel(result)`          | 更新检测结果区               |
| `initializeOperatorLibraryPanel()`              | 初始化算子库                 |
| `initializeImageViewer()`                       | 初始化主图像查看器           |
| `initializeInspectionController()`              | 初始化检测控制器             |
| `initializePropertyPanel()`                     | 初始化属性面板               |
| `initializeProjectView()`                       | 初始化工程页                 |
| `initializeResultPanel()`                       | 初始化结果页                 |
| `loadInspectionHistory()`                       | 加载检测历史                 |
| `updateResultsPanel(data)`                      | 刷新结果摘要                 |
| `initializeOperatorLibrary()`                   | 拉取算子数据                 |
| `renderOperatorLibrary(operators)`              | 渲染算子库                   |
| `groupByCategory(operators)`                    | 算子分组                     |
| `getDefaultOperators()`                         | 本地回退算子列表             |
| `handleDragStart(event)`                        | 算子拖拽开始                 |
| `initializeFlowEditor()`                        | 初始化流程编辑器             |
| `showLoadingScreen()` / `hideLoadingScreen()` | 管理加载页                   |
| `showWelcomeScreen()`                           | 首次欢迎页                   |

### 9.2 基础通信与状态

#### `httpClient.js`

| 方法                               | 作用              |
| ---------------------------------- | ----------------- |
| `constructor(baseUrl = null)`    | 初始化客户端      |
| `discoverPort()`                 | 探测本地 API 端口 |
| `saveSuccessfulPort(url)`        | 缓存成功端口      |
| `get(url, params = null)`        | GET 请求          |
| `post(url, data = null)`         | POST 请求         |
| `postForBlob(url, data = null)`  | POST 并返回二进制 |
| `put(url, data = null)`          | PUT 请求          |
| `delete(url)`                    | DELETE 请求       |
| `handleNetworkError(error, url)` | 网络异常处理      |
| `handleBlobResponse(response)`   | 二进制响应处理    |
| `handleResponse(response)`       | 通用响应处理      |

#### `webMessageBridge.js`

| 方法                                                       | 作用                   |
| ---------------------------------------------------------- | ---------------------- |
| `constructor()`                                          | 初始化消息桥状态       |
| `initialize()`                                           | 绑定 WebView2 消息通道 |
| `handleSharedBuffer(event)`                              | 接收共享图像缓冲区     |
| `enableMockMode()`                                       | 模拟模式               |
| `handleMessage(event)`                                   | 处理宿主推送消息       |
| `sendMessage(type, data = null, expectResponse = false)` | 发送消息并可等待响应   |
| `postMessage(message)`                                   | 低层发送               |
| `sendResponse(requestId, data)`                          | 回复成功消息           |
| `sendError(requestId, error)`                            | 回复错误消息           |
| `on(type, handler)`                                      | 订阅消息               |
| `off(type, handler = null)`                              | 取消订阅               |

#### `store.js`

| 方法                                        | 作用           |
| ------------------------------------------- | -------------- |
| `Signal.constructor(initialValue)`        | 创建响应式信号 |
| `Signal.subscribe(callback)`              | 订阅变化       |
| `Signal._notify()`                        | 分发变化       |
| `createSignal(initialValue)`              | 创建信号工厂   |
| `createComputed(computeFn, dependencies)` | 创建计算信号   |

### 9.3 画布与图像基础组件

#### `flowCanvas.js`

`FlowCanvas` 是前端最重要、也最复杂的类之一。可以把它看成“流程图形引擎”。

##### 初始化与生命周期

- `constructor(canvasId)`：初始化画布对象、内部状态。
- `initialize()`：绑定画布、事件与初始渲染。
- `handleVisibilityChange()`：页面可见性变化处理。
- `resize()`：自适应尺寸。
- `destroy()` / `Dispose` 等价职责：销毁资源。

##### 节点与连线管理

- `generateUUID()`：生成前端节点 ID。
- `addNode(type, x, y, config = {})`：新增节点。
- `removeNode(nodeId)`：删除节点。
- `addConnection(sourceId, sourcePort, targetId, targetPort)`：新增连线。
- `removeConnection(connectionId)`：删除连线。
- `startConnection(nodeId, portIndex)`：开始拖线。
- `finishConnection(nodeId, portIndex)`：完成连线。
- `cancelConnection()`：取消连线。
- `checkTypeCompatibility(sourceType, targetType)`：检查端口类型兼容性。
- `highlightCompatiblePorts()`：高亮可连端口。

##### 绘制相关

- `drawGrid()`：背景网格。
- `drawNode(node)`：节点主体绘制。
- `drawStatusIndicator(x, y, status)`：绘制节点状态点。
- `drawPorts(node, x, y, w, h)`：绘制端口。
- `drawTempConnection()`：拖线中预览。
- `drawConnection(connection)`：正式连线。
- `drawFlowParticles(...)`：连线粒子动效。
- `roundRect(...)`：圆角矩形绘制。
- `render()`：主渲染入口。
- `drawPortHighlight(port)`：端口高亮。
- `initMinimap()` / `drawMinimap()` / `renderWithMinimap()`：最小地图。

##### 命中检测与几何辅助

- `getPortPosition(...)`
- `getPortAt(x, y)`
- `getConnectionAtPort(...)`
- `getConnectionsAtPort(...)`
- `isPointOnConnection(x, y, connection)`
- `getConnectionAt(x, y)`
- `getNodesBounds()`
- `normalizePortType(type)`

##### 交互与状态

- `handleMouseDown(e)` / `handleMouseMove(e)` / `handleMouseUp()`
- `handleDoubleClick(e)`
- `handleWheel(e)`
- `handleContextMenu(e)`
- `handleKeyDown(e)`
- `setNodeStatus(nodeId, status)`
- `resetAllStatus()`
- `clear()`：清空当前画布内容。
- `runNode(nodeId)`：运行节点。
- `duplicateNode(nodeId)`：复制节点。
- `deleteNode(nodeId)`：删除节点。
- `toggleNodeDisabled(nodeId)`：禁用/启用节点。
- `showNodeHelp(node)`：显示节点帮助。

##### 序列化

- `serialize()`：画布转 JSON。
- `deserialize(data)`：JSON 回填画布。

> 备注：该类体量很大，是前端最值得“专项走读”的文件之一。

#### `imageCanvas.js`

| 方法                                                | 作用           |
| --------------------------------------------------- | -------------- |
| `constructor(canvasId)`                           | 初始化图像画布 |
| `initialize()`                                    | 绑定事件       |
| `destroy()`                                       | 销毁           |
| `resize()`                                        | 尺寸调整       |
| `loadImage(imageSource)`                          | 加载图像       |
| `loadImageData(byteArray, format = 'png')`        | 从字节加载     |
| `loadImageFromBuffer(buffer, width, height)`      | 从缓冲区加载   |
| `resetView()`                                     | 重置视图       |
| `fitToScreen()`                                   | 适应窗口       |
| `actualSize()`                                    | 原尺寸显示     |
| `addOverlay(...)`                                 | 增加覆盖层     |
| `removeOverlay(overlayId)`                        | 删除覆盖层     |
| `clearOverlays()`                                 | 清空覆盖层     |
| `drawImage()` / `drawOverlays()` / `render()` | 图像与标注绘制 |
| `drawInfo()`                                      | 画布信息显示   |
| `handleMouseDown/Move/Up/Wheel/DoubleClick()`     | 平移缩放交互   |
| `getViewState()` / `setViewState(state)`        | 视图状态读写   |
| `clear()`                                         | 清空画布       |

#### `lintPanel.js`

| 方法                               | 作用             |
| ---------------------------------- | ---------------- |
| `constructor(containerId)`       | 初始化 Lint 面板 |
| `update(issues)`                 | 更新问题列表     |
| `hasErrors()`                    | 是否有错误       |
| `getStats()`                     | 获取统计         |
| `render()`                       | 渲染面板         |
| `toggleCollapse()`               | 折叠/展开        |
| `showDryRunBanner(dryRunResult)` | 显示 DryRun 结果 |
| `destroy()`                      | 销毁             |

### 9.4 标定前端模块

#### `handEyeCalibWizard.js`

| 方法                            | 作用               |
| ------------------------------- | ------------------ |
| `constructor(cameraManager)`  | 初始化手眼标定向导 |
| `createUI()`                  | 创建 DOM           |
| `attachEvents()`              | 绑定事件           |
| `checkAddButtonState()`       | 校验是否可添加点对 |
| `renderTable()`               | 刷新点表格         |
| `goToStep(step)`              | 步骤切换           |
| `show()` / `hide()`         | 打开/关闭向导      |
| `renderFrame(event)`          | 显示实时图像       |
| `solveCalibration()`          | 请求后端求解       |
| `handleSolveResult(res)`      | 处理求解结果       |
| `saveCalibration()`           | 请求保存           |
| `handleSaveResult(res)`       | 处理保存结果       |
| `onWebMessageReceived(event)` | 响应宿主消息       |

#### `calibrationWizard.js`

| 方法                                    | 作用               |
| --------------------------------------- | ------------------ |
| `constructor()`                       | 初始化通用标定向导 |
| `init()`                              | 整体初始化         |
| `injectHtmlElement()`                 | 注入 UI            |
| `bindEvents()`                        | 绑定交互           |
| `open()` / `close()`                | 打开/关闭          |
| `reset()`                             | 重置状态           |
| `handleImageLoad(e)`                  | 处理标定图加载     |
| `handleImageClick(e)`                 | 处理标定点点击     |
| `addPoint()` / `removePoint(index)` | 增删点             |
| `renderPoints()`                      | 刷新点列表         |
| `nextStep()` / `prevStep()`         | 步骤流转           |
| `fillResultData()`                    | 填充结果显示       |
| `finish()`                            | 完成标定           |
| `updateView()`                        | 更新当前界面       |
| `showError(msg)`                      | 错误提示           |

### 9.5 认证与工程前端模块

#### `auth.js`

| 方法                             | 作用         |
| -------------------------------- | ------------ |
| `getToken()`                   | 获取 Token   |
| `getCurrentUser()`             | 获取当前用户 |
| `isAuthenticated()`            | 是否已登录   |
| `hasRole(role)`                | 是否具备角色 |
| `isAdmin()` / `isEngineer()` | 常用角色判断 |
| `initAuth()`                   | 初始化认证态 |
| `login(username, password)`    | 登录         |
| `logout()`                     | 登出         |
| `validateTokenAsync()`         | 校验 Token   |
| `changePassword(...)`          | 修改密码     |

#### `projectManager.js`

| 方法                                          | 作用                 |
| --------------------------------------------- | -------------------- |
| `constructor()`                             | 初始化工程状态管理器 |
| `getProjectList()`                          | 获取工程列表         |
| `getRecentProjects(count = 10)`             | 获取最近工程         |
| `searchProjects(keyword)`                   | 搜索工程             |
| `createProject(name, description = '')`     | 创建工程             |
| `openProject(projectId)`                    | 打开工程             |
| `saveProject(projectData = null)`           | 保存工程             |
| `deleteProject(projectId)`                  | 删除工程             |
| `closeProject()`                            | 关闭当前工程         |
| `updateProject(updates)`                    | 更新工程本地状态     |
| `updateFlow(flowData)`                      | 更新流程本地状态     |
| `hasUnsavedChanges()`                       | 是否有未保存修改     |
| `getCurrentProject()`                       | 获取当前工程         |
| `updateStatusBar(project)`                  | 更新状态栏           |
| `updateTitle()`                             | 更新页面标题         |
| `exportProject(projectId, format = 'json')` | 导出工程             |
| `importProject(file)`                       | 导入工程             |

#### `projectView.js`

| 方法                                      | 作用                     |
| ----------------------------------------- | ------------------------ |
| `constructor(containerId)`              | 初始化工程页             |
| `init()`                                | 首次加载                 |
| `bindEvents()`                          | 绑定搜索/新建/删除等事件 |
| `loadProjects()`                        | 加载工程列表             |
| `renderSkeletonLoading()`               | 骨架屏                   |
| `renderEmptyState()`                    | 空状态                   |
| `sortProjects()`                        | 排序                     |
| `renderProjects(container)`             | 渲染工程集合             |
| `renderListView()`                      | 列表模式                 |
| `createProjectCardList(project)`        | 构造工程卡片             |
| `getStatusConfig(status)`               | 映射状态样式             |
| `bindCardEvents(container)`             | 卡片事件绑定             |
| `openProject(projectId)`                | 打开工程                 |
| `confirmDelete(projectId, projectName)` | 删除确认                 |
| `handleSearch(keyword)`                 | 搜索工程                 |
| `refresh()`                             | 刷新页面                 |
| `escapeHtml(text)`                      | 文本转义                 |
| `showNewProjectDialog()`                | 新建工程弹窗             |

### 9.6 算子库与流程编辑前端模块

#### `operatorLibrary.js`

| 方法                                              | 作用              |
| ------------------------------------------------- | ----------------- |
| `constructor(containerId)`                      | 初始化算子库面板  |
| `initialize()`                                  | 整体初始化        |
| `renderUI()`                                    | 渲染 UI 框架      |
| `initializeTreeView()`                          | 初始化树组件      |
| `loadOperators()`                               | 拉取算子库        |
| `getDefaultOperators()`                         | 回退算子清单      |
| `getSvgIcon(...)`                               | 构建 SVG 图标     |
| `getOperatorIconPath(type, category = null)`    | 获取算子图标路径  |
| `getCategoryIcon(category)`                     | 获取分类图标      |
| `renderOperatorTree()`                          | 渲染树            |
| `groupByCategory(operators)`                    | 按分类分组        |
| `saveExpandedState()` / `loadExpandedState()` | 保存/恢复展开状态 |
| `bindSearchEvents()`                            | 绑定搜索          |
| `searchOperators(keyword)`                      | 搜索过滤          |
| `bindActionEvents()`                            | 绑定动作按钮      |
| `showOperatorPreview(operator)`                 | 显示算子预览      |
| `renderParameterList(parameters)`               | 参数列表渲染      |
| `getOperators()` / `getCategories()`          | 查询数据          |
| `refresh()`                                     | 刷新面板          |

#### `flowEditorInteraction.js`

| 方法                                                                                                                      | 作用               |
| ------------------------------------------------------------------------------------------------------------------------- | ------------------ |
| `constructor(flowCanvas)`                                                                                               | 初始化交互控制器   |
| `initialize()`                                                                                                          | 整体初始化         |
| `initializeTemplateSelector()`                                                                                          | 初始化模板选择器   |
| `enhanceEventListeners()`                                                                                               | 加强画布事件接管   |
| `tryDisconnectPortConnections(port)`                                                                                    | 断开端口相关连线   |
| `getCanvasWorldPoint(e)`                                                                                                | 屏幕坐标转画布坐标 |
| `startPan(e)` / `updatePan(e)` / `endPan(e)`                                                                        | 画布平移           |
| `startNodeDrag(...)` / `updateNodeDrag(e)` / `endNodeDrag(e)`                                                       | 节点拖拽           |
| `syncCursorToPointer(e)`                                                                                                | 同步光标           |
| `bindKeyboardShortcuts()`                                                                                               | 注册快捷键         |
| `enableOperatorLibraryDrag()`                                                                                           | 支持从算子库拖入   |
| `addOperatorNode(type, x, y, data = null)`                                                                              | 新增节点           |
| `getPortAt(x, y)` / `getNodeAt(x, y)`                                                                                 | 命中检测           |
| `startConnection(port, e)` / `updateConnectionPreview(e)` / `endConnection(...)`                                    | 连线交互           |
| `cancelConnection()`                                                                                                    | 取消拖线           |
| `startSelection(e)` / `updateSelectionBox(e)` / `endSelection()`                                                    | 框选               |
| `selectNode(nodeId)` / `toggleNodeSelection(nodeId)` / `clearSelection()` / `selectAll()` / `updateSelection()` | 节点选中管理       |
| `copySelectedNodes()` / `pasteNodes()` / `deleteSelectedNodes()`                                                    | 复制粘贴删除       |
| `saveState()` / `undo()` / `redo()` / `restoreState()`                                                            | 历史状态管理       |
| `drawBezierCurve(...)`                                                                                                  | 贝塞尔预览         |

#### `propertyPanel.js`

| 方法                                                                          | 作用                     |
| ----------------------------------------------------------------------------- | ------------------------ |
| `constructor(containerId)`                                                  | 初始化属性面板           |
| `bindGlobalEvents()`                                                        | 绑定全局事件             |
| `setOperator(operator)`                                                     | 设置当前节点             |
| `clear()`                                                                   | 清空面板                 |
| `render()`                                                                  | 渲染表单                 |
| `groupParameters(parameters)`                                               | 参数分组                 |
| `renderParameter(param)` / `renderParameterEnhanced(param)`               | 渲染不同类型参数         |
| `initSliders()`                                                             | 初始化滑块               |
| `bindEvents()`                                                              | 表单事件绑定             |
| `loadCameraBindingsForSelects(forceRefresh = false)`                        | 加载相机绑定选项         |
| `fetchCameraBindings(forceRefresh = false)`                                 | 请求相机绑定             |
| `populateCameraBindingSelects(...)`                                         | 填充下拉                 |
| `updateCurrentOperatorParams(values)`                                       | 更新当前节点参数         |
| `getValues()`                                                               | 读取表单值               |
| `applyChanges()`                                                            | 提交表单修改             |
| `initPreviewPanel()`                                                        | 初始化预览面板           |
| `canRecommend(type)`                                                        | 判断当前节点是否支持推荐 |
| `_notifyValueChanged()`                                                     | 广播参数变化             |
| `recommendParameters()`                                                     | 请求参数推荐             |
| `applyRecommendedValues(recommendedValues)`                                 | 应用推荐值               |
| `acceptRecommendation()` / `revertRecommendation()`                       | 接受/回滚推荐            |
| `_toggleRecommendationActions(visible)`                                     | 推荐按钮状态             |
| `_clearRecommendationHighlights()` / `_restoreRecommendationHighlights()` | 高亮控制                 |
| `_readInputValue(input)` / `_writeInputValue(input, rawValue)`            | 表单读写                 |
| `_applyValuesToForm(values)`                                                | 批量回填                 |
| `_toBoolean(value)`                                                         | 值归一                   |
| `resolveInputImageBase64()`                                                 | 解析预览输入图像         |
| `extractImageBase64(result)` / `normalizeBase64Image(imageValue)`         | 图像数据归一             |
| `resetChanges()`                                                            | 重置未提交修改           |
| `onChange(callback)`                                                        | 注册变化监听             |
| `showToast(message, type = 'info')`                                         | 局部提示                 |

#### `previewPanel.js`

| 方法                                      | 作用                     |
| ----------------------------------------- | ------------------------ |
| `constructor(container, options = {})`  | 初始化预览面板           |
| `destroy()`                             | 销毁                     |
| `render()`                              | 渲染 UI                  |
| `scheduleAutoPreview()`                 | 调度自动预览             |
| `refresh()`                             | 请求预览执行             |
| `captureBySoftTrigger()`                | 通过当前相机做软触发采图 |
| `_setStatus(text)`                      | 设置状态文字             |
| `_setImage(type, imageBase64OrDataUrl)` | 切换预览图像             |
| `_renderOutputs(outputs)`               | 渲染输出数据             |

#### `templateSelector.js`

| 方法                                       | 作用             |
| ------------------------------------------ | ---------------- |
| `constructor(flowCanvas)`                | 初始化模板选择器 |
| `open()` / `close()`                   | 打开/关闭        |
| `_ensureDataLoaded()`                    | 确保数据就绪     |
| `_loadTemplates()`                       | 加载模板         |
| `_loadOperatorMetadata()`                | 加载算子元数据   |
| `_ensureUi()`                            | 初始化 UI 框架   |
| `_renderFilters()`                       | 渲染筛选器       |
| `_renderTemplateCards()`                 | 渲染模板卡片     |
| `_applyTemplate(templateId)`             | 应用模板         |
| `_convertTemplateToCanvasFlow(template)` | 模板转画布流程   |
| `_buildLayout(...)`                      | 生成布局         |
| `_collectRequiredPorts(...)`             | 收集必要端口     |
| `_buildPorts(...)`                       | 构建端口列表     |
| `_buildParameterList(...)`               | 构建参数列表     |
| `_resolveTemplateValue(...)`             | 解析模板值       |
| `_convertValueByType(...)`               | 按类型转换值     |
| `_findPortByName(...)`                   | 端口匹配         |
| `_estimateOperatorCount(flowJson)`       | 估算模板规模     |
| `_generateId()`                          | 生成 ID          |

### 9.7 图像查看、检测与结果前端模块

#### `imageViewer.js`

| 方法                                                                                                                         | 作用             |
| ---------------------------------------------------------------------------------------------------------------------------- | ---------------- |
| `constructor(containerId)`                                                                                                 | 初始化图像查看器 |
| `initialize()`                                                                                                             | 初始绑定         |
| `renderUI()`                                                                                                               | 渲染工具栏与画布 |
| `bindToolbarEvents()`                                                                                                      | 工具栏事件       |
| `bindCanvasEvents()`                                                                                                       | 画布事件         |
| `loadFromFile(file)` / `loadFromUrl(url)` / `loadFromBase64(...)` / `loadFromByteArray(...)` / `loadImage(source)` | 各类图像加载入口 |
| `showDefects(defects)`                                                                                                     | 绘制缺陷框       |
| `getDefectColor(type)`                                                                                                     | 缺陷颜色映射     |
| `getDefectProp(defect, propName)`                                                                                          | 缺陷属性读取     |
| `renderDefectList()`                                                                                                       | 缺陷列表渲染     |
| `selectDefect(defectId)`                                                                                                   | 选中缺陷         |
| `getOverlayAt(x, y)`                                                                                                       | 命中覆盖层       |
| `zoomIn()` / `zoomOut()` / `zoomTo(scale)`                                                                             | 缩放             |
| `fitToWindow()` / `actualSize()`                                                                                         | 视图切换         |
| `clearAnnotations()` / `toggleAnnotations()`                                                                             | 标注管理         |
| `hidePlaceholder()` / `showPlaceholder()`                                                                                | 占位态           |
| `updateImageInfo()` / `updateZoomInfo()`                                                                                 | 信息刷新         |
| `getCurrentImage()` / `getDefects()`                                                                                     | 数据读取         |

#### `inspectionController.js`

| 方法                                                                      | 作用                 |
| ------------------------------------------------------------------------- | -------------------- |
| `constructor()`                                                         | 初始化检测控制器状态 |
| `setProject(projectId)`                                                 | 绑定当前工程         |
| `setCamera(cameraId)`                                                   | 绑定当前相机         |
| `initializeWebMessage()`                                                | 注册 Web 消息订阅    |
| `executeSingle(imageData = null)`                                       | 单次执行             |
| `startRealtime()`                                                       | 启动实时检测         |
| `startRealtimeFlowMode()`                                               | 启动流程驱动实时检测 |
| `stopRealtime()`                                                        | 停止实时检测         |
| `handleInspectionCompleted(result)`                                     | 处理检测完成         |
| `handleInspectionError(error)`                                          | 处理检测失败         |
| `updateProgress(data)`                                                  | 刷新进度             |
| `getInspectionHistory(...)`                                             | 拉取历史             |
| `getStatistics(...)`                                                    | 拉取统计             |
| `onInspectionCompleted(callback)`                                       | 注册完成回调         |
| `onInspectionError(callback)`                                           | 注册错误回调         |
| `getState()` / `getLastResult()` / `isRunning()` / `isRealtime()` | 状态查询             |

#### `inspectionPanel.js`

| 方法                               | 作用               |
| ---------------------------------- | ------------------ |
| `constructor(containerId)`       | 初始化检测页       |
| `initialize()`                   | 渲染与初始化       |
| `bindEvents()`                   | 绑定按钮事件       |
| `setupSubscriptions()`           | 绑定检测控制器回调 |
| `handleRunSingle()`              | 处理单次执行按钮   |
| `handleRunContinuous()`          | 处理连续执行按钮   |
| `handleStop()`                   | 停止执行           |
| `handleInspectionResult(result)` | 处理一次检测结果   |
| `updateStatus(status, text)`     | 更新状态条         |
| `updateCounters()`               | 更新计数           |
| `setButtonsState(isRunning)`     | 切换按钮可用性     |
| `addRecentResult(result)`        | 维护最近结果列表   |
| `renderRecentResults()`          | 渲染最近结果       |
| `reset()`                        | 重置页面           |
| `extractTextPreview(outputData)` | 提取文本摘要       |
| `escapeHtml(text)`               | 转义               |
| `refresh()`                      | 刷新               |
| `loadRuntimeConfig()`            | 读取运行时配置     |
| `tryAutoRunIfNeeded()`           | 按配置自动启动     |
| `dispose()`                      | 销毁               |

#### `analysisCardsPanel.js`

| 方法                                                  | 作用               |
| ----------------------------------------------------- | ------------------ |
| `constructor(containerId)`                          | 初始化分析卡片面板 |
| `updateCards(outputData, status, processingTimeMs)` | 刷新卡片           |
| `clear()`                                           | 清空               |
| `_classifyOutputData(...)`                          | 按类型归类输出     |
| `_renderCards(groups)`                              | 渲染总卡片集合     |
| `_renderMeasurementCard(items)`                     | 测量类卡片         |
| `_renderOcrCodeCard(items)`                         | OCR/码识别类卡片   |
| `_renderDefectCard(items)`                          | 缺陷类卡片         |
| `_renderObjectDetectionCard(items)`                 | 目标检测卡片       |
| `_renderMatchCard(items)`                           | 匹配类卡片         |
| `_renderGenericCard(items)`                         | 通用卡片           |
| `_renderRangeBar(value, isNG)`                      | 数值范围条         |
| `_wrapCard(...)`                                    | 卡片外层包装       |
| `_bindToggleEvents()`                               | 卡片折叠交互       |
| `_toDisplayName(key)`                               | 字段名转显示名     |
| `_escapeHtml(text)`                                 | 转义               |
| `_extractDetectionArray(rawValue)`                  | 提取检测数组       |
| `dispose()`                                         | 销毁               |

#### `resultPanel.js`

| 方法                                        | 作用                     |
| ------------------------------------------- | ------------------------ |
| `constructor(containerId)`                | 初始化结果页             |
| `bindEvents()`                            | 绑定筛选/导出/翻页等事件 |
| `setTimeRange(range)`                     | 切换时间范围             |
| `updateStatistics(stats)`                 | 更新统计                 |
| `addResult(result)`                       | 追加结果                 |
| `loadResults(results, total = null)`      | 批量加载结果             |
| `calculateStatistics()`                   | 前端统计聚合             |
| `updateDefectTypeFilter()`                | 更新缺陷筛选项           |
| `updateTrendData()`                       | 刷新趋势数据             |
| `applyFilters()`                          | 应用过滤条件             |
| `setFilter(type, value)`                  | 设置筛选器               |
| `goToPage(page)`                          | 翻页                     |
| `clear()`                                 | 清空结果                 |
| `render()`                                | 主渲染入口               |
| `renderKPIs()`                            | KPI 区                   |
| `renderYieldChart()`                      | 良率图                   |
| `renderDefectDistribution()`              | 缺陷分布                 |
| `renderTrendChart()`                      | 趋势图                   |
| `renderResultsList()`                     | 列表区                   |
| `renderPagination()`                      | 分页区                   |
| `exportResults(format = 'json')`          | 导出结果                 |
| `convertToCSV(results)`                   | CSV 转换                 |
| `showResultDetail(result)`                | 明细弹窗                 |
| `renderOutputDataPreview(outputData)`     | 输出预览                 |
| `renderOutputDataTable(outputData)`       | 输出表格                 |
| `escapeHtml(text)`                        | 转义                     |
| `getLatestResult()` / `getAllResults()` | 数据读取                 |

### 9.8 设置与 AI 前端模块

#### `settingsView.js`

这是前端中另一个非常值得专项阅读的大文件，它几乎是“产品后台中心”的前端集合。

##### 全局框架方法

- `constructor(containerId)`
- `refresh()`
- `renderLayout()`
- `activateTab(tabName)`
- `bindEvents()`
- `save()`

##### PLC 相关方法

- `bindPlcSettingsEvents()`
- `loadPlcMappings({ force = false } = {})`
- `renderPlcMappingsTable()`
- `addPlcMapping()`
- `deletePlcMapping(index)`
- `updatePlcMappingField(index, field, element)`
- `collectPlcMappingsFromTable()`
- `savePlcMappings({ silent = false } = {})`
- `testPlcConnection()`
- `updatePlcConnectionBadge(status, message = '')`

##### AI 模型管理方法

- `loadAiModels({ preserveEditingId = false } = {})`
- `bindAiSettingsEvents()`
- `getActiveTabName()`
- `hasPendingAiChanges()`
- `_saveCurrentForm()`
- `refreshAiTableOnly()`
- `refreshAiTableAndForm()`

##### 相机管理方法

- `bindCameraManagementEvents()`
- `loadCameraBindings()`
- `discoverCameras(vendor = 'all', sourceButton = null)`
- `showDiscoveryModal(devices, vendorText = '在线')`
- `refreshCameraTable()`
- `selectCameraRow(tr)`
- `updateCameraParameterPanel(cam)`
- `saveSelectedCameraParameters()`
- `collectCameraBindings()`
- `resolveActiveCameraId()`
- `saveCameraBindings({ silent = false } = {})`

##### Tab 渲染方法

- `renderGeneralTab()`
- `renderCommunicationTab()`
- `renderStorageTab()`
- `renderRuntimeTab()`
- `renderCameraTab()`
- `renderAiTab()`
- `renderUserManagementTab()`

##### 用户管理与磁盘工具方法

- `refreshUserTable()`
- `bindUserManagementEvents()`
- `showUserModal(mode, user)`
- `loadDiskUsage()`
- `updateDiskUsageCard()`
- `getDefaultConfig()`
- `escapeHtml(value)`

#### `aiPanel.js`

`AiPanel` 是当前 AI 主工作台，覆盖“输入、附件、会话、流式输出、流程应用”完整闭环。

| 方法                                                                   | 作用                 |
| ---------------------------------------------------------------------- | -------------------- |
| `constructor(containerId, flowCanvas)`                               | 初始化 AI 面板       |
| `_init()`                                                            | 初始构建与事件注册   |
| `activate()`                                                         | 面板激活             |
| `_handleNewConversation()`                                           | 新建会话             |
| `render()`                                                           | 渲染主界面           |
| `_checkConnection()`                                                 | 检查 AI 连通性       |
| `_setupMessageListeners()`                                           | 注册 WebMessage 监听 |
| `_getCurrentFlowJson()`                                              | 读取当前画布流程     |
| `_handleGenerate()`                                                  | 发起生成             |
| `_updateProgress(data)`                                              | 进度更新             |
| `_showPhaseHint(msg, phase)`                                         | 阶段提示             |
| `_handleStreamChunk(data)`                                           | 流式分块处理         |
| `_flushStreamBuffer()`                                               | 刷新缓冲区           |
| `_appendStreamText(targetEl, text)`                                  | 追加流式文本         |
| `_isNearBottom(targetEl, threshold = 24)`                            | 判断是否靠近底部     |
| `_normalizeIntent(intent)` / `_getIntentLabel(intent)`             | 意图标准化           |
| `_handleResult(data)`                                                | 处理最终生成结果     |
| `_handleFirewallBlocked(data)`                                       | 处理网络受限场景     |
| `_handleError(msg)`                                                  | 错误显示             |
| `_displayResult(data)`                                               | 渲染最终结果         |
| `_typewriterEffect(el, text, chunkSize = 3)`                         | 打字机效果           |
| `_handleApplyFlow()`                                                 | 应用生成的流程       |
| `_addMessage(role, text, options = {})`                              | 添加聊天消息         |
| `_addThinkingChain(id)` / `_updateThinkingStep(...)`               | 思考链 UI            |
| `_setGeneratingState(busy)`                                          | 切换生成状态         |
| `_handleAttachmentClick()`                                           | 选择附件             |
| `_handleFilePickedEvent(data)`                                       | 文件选择结果处理     |
| `_handleAttachmentReport(data)`                                      | 附件可用性报告       |
| `_removeAttachment(path)` / `_renderAttachments()`                 | 附件管理             |
| `_getAttachmentStatusLabel(...)` / `_formatSkipReason(reason)`     | 附件状态文案         |
| `_getFileName(filePath)` / `_escapeHtml(value)`                    | 展示辅助             |
| `_clearResultPane()` / `_scrollToBottom()`                         | 结果区控制           |
| `_toggleHistoryPanel()`                                              | 切换历史面板         |
| `_addToHistory(entry)` / `_normalizeSessionSummary(entry)`         | 历史会话维护         |
| `_filterHistory(keyword = '')` / `_renderHistoryList()`            | 历史过滤渲染         |
| `_formatHistoryTime(value)`                                          | 时间格式化           |
| `_loadHistory()`                                                     | 拉取会话历史         |
| `_handleListAiSessionsResult(data)`                                  | 处理历史列表         |
| `_switchToSession(sessionId)`                                        | 切换会话             |
| `_handleGetAiSessionResult(data)`                                    | 处理单会话详情       |
| `_parseFlowJson(raw)`                                                | 解析流程 JSON        |
| `_extractOperators(flow)` / `_extractConnections(flow)`            | 提取图结构           |
| `_isCanvasFlowLike(flow)`                                            | 判断是否画布结构     |
| `_normalizeSessionFlowForCanvas(flow, sessionId = '')`               | 会话流程转画布结构   |
| `_deleteSession(sessionId)` / `_handleDeleteAiSessionResult(data)` | 删除会话             |
| `_loadSessionId()` / `_saveSessionId(sessionId)`                   | 会话 ID 本地保存     |

#### `aiGenerationDialog.js`

这是一个更轻量的 AI 生成对话框实现，可视为 AI 入口的另一种 UI 形态。

| 方法                                            | 作用           |
| ----------------------------------------------- | -------------- |
| `constructor(canvas)`                         | 初始化对话框   |
| `_init()`                                     | 组装初始状态   |
| `open()` / `close()`                        | 打开/关闭      |
| `_createDialogDom()`                          | 创建 DOM       |
| `_bindEvents()`                               | 绑定事件       |
| `_setupMessageListener()`                     | 注册消息监听   |
| `_handleGenerate()`                           | 发起生成       |
| `_handleGenerationResult(message)`            | 处理生成结果   |
| `_highlightReviewParams(paramsNeedingReview)` | 标亮需复核参数 |
| `_setGeneratingState(generating)`             | 切换生成态     |
| `_updateProgress(message)`                    | 进度刷新       |
| `_showError(message)`                         | 错误提示       |
| `_resetState()`                               | 重置对话框     |
| `_injectStyles()`                             | 注入样式       |

### 9.9 通用 UI 组件

#### `dialog.js`

- `Dialog.create(title, content, buttons = [])`：创建通用弹窗。
- `Dialog.close()`：关闭弹窗。
- 该组件被工程选择、新建工程等多个场景复用。

#### `treeView.js`

- `initialize()`：初始化树。
- `setData(data)`：设置数据。
- `render()`：渲染树。
- `createNodeList()` / `createNodeItem()`：节点渲染。
- `toggleNode(node)`：展开/折叠。
- `selectNode(node, clearOthers = true)`：选中。
- `findNode(id, nodes)`：查节点。
- `expandAll()` / `collapseAll()`：全展开/全折叠。
- `addNode(parentId, node)` / `removeNode(id)`：动态变更树。

#### `uiComponents.js`

- `createButton()`：按钮。
- `createInput()`：输入框。
- `createLabeledInput()`：带标签输入项。
- `createSelect()`：下拉框。
- `createCheckbox()`：勾选框。
- `showToast()`：全局提示。
- `createLoading()` / `showFullscreenLoading()` / `hideLoading()`：加载动画。
- `createModal()` / `closeModal()`：通用模态框。

---

## 10. 典型端到端链路

### 10.1 链路一：打开工程并显示流程

1. 前端工程页触发 `projectManager.openProject(projectId)`。
2. `httpClient.get('/projects/{id}')` 请求后端。
3. `ProjectService.GetByIdAsync()` 读取数据库中的工程实体。
4. 再通过 `IProjectFlowStorage.LoadFlowJsonAsync()` 尝试加载 JSON 流程。
5. `EnrichFlowDtoWithMetadata()` 回填参数选项。
6. 前端拿到 `OperatorFlowDto` 后调用 `flowCanvas.deserialize(data)`。
7. 画布渲染节点、端口与连线。

### 10.2 链路二：点击“单次检测”

1. `inspectionPanel.handleRunSingle()` 触发执行。
2. `inspectionController.executeSingle()` 调用 `/api/inspection/execute`。
3. `InspectionService.ExecuteSingleAsync()` 读取工程流程或使用前端直传流程。
4. `FlowExecutionService.ExecuteFlowAsync()` 执行流程。
5. 结果经 `DetermineStatusFromFlowOutput()` 判定 OK/NG/Error。
6. `InspectionResult` 持久化到数据库。
7. 前端收到结果后：
   - `inspectionPanel.handleInspectionResult(result)` 更新状态；
   - `analysisCardsPanel.updateCards(...)` 渲染分析卡片；
   - `imageViewer.showDefects(defects)` 显示缺陷框；
   - `resultPanel.addResult(result)` 更新结果页。

### 10.3 链路三：编辑节点参数并做预览

1. 用户在画布选中节点。
2. `app.js` 中订阅选中变化后调用 `propertyPanel.setOperator(operator)`。
3. `propertyPanel.render()` 渲染参数表单。
4. 用户修改参数并点击预览。
5. `previewPanel.refresh()` 向 `/api/operators/{type}/preview` 发请求。
6. `OperatorPreviewService.PreviewAsync()` 构造预览执行环境。
7. 返回输出图像与输出字典，前端刷新预览图与输出面板。

### 10.4 链路四：AI 生成流程并应用到画布

1. 用户在 `AiPanel` 中输入需求并选择附件。
2. `webMessageBridge.sendMessage('GenerateFlow', ...)` 发到桌面宿主。
3. `WebMessageHandler.HandleGenerateFlowCommand()` 调用 `GenerateFlowMessageHandler`。
4. `AiFlowGenerationService.GenerateFlowAsync()`：
   - 组装提示词；
   - 分析附件；
   - 调用 `AiApiClient`；
   - 解析 JSON；
   - 转换为 `OperatorFlowDto`。
5. 前端持续收到流式进度与思考链。
6. 最终 `AiPanel._handleResult(data)` 显示生成结果。
7. 用户点击应用后，`_handleApplyFlow()` 把流程写入 `flowCanvas`。

### 10.5 链路五：设置页保存 PLC 映射

1. `settingsView.renderCommunicationTab()` 渲染表格。
2. 用户编辑映射行后点击保存。
3. `settingsView.savePlcMappings()` 收集表格值。
4. `PUT /api/plc/mappings` 提交后端。
5. `PlcEndpoints.NormalizeMappings()` 规范化数据。
6. `JsonConfigurationService.SaveAsync()` 保存到 `config.json`。

---

## 11. 学习路径建议

### 11.1 给前端同事

推荐顺序：

1. `wwwroot/src/app.js`
2. `core/messaging/httpClient.js`
3. `core/messaging/webMessageBridge.js`
4. `core/canvas/flowCanvas.js`
5. `features/flow-editor/*`
6. `features/inspection/*`
7. `features/settings/settingsView.js`
8. `features/ai/aiPanel.js`

### 11.2 给后端/平台同事

推荐顺序：

1. `Program.cs`
2. `DependencyInjection.cs`
3. `Endpoints/*.cs`
4. `Application/Services/ProjectService.cs`
5. `Application/Services/InspectionService.cs`
6. `Infrastructure/Services/FlowExecutionService.cs`
7. `Infrastructure/Services/OperatorFactory.cs`
8. `Infrastructure/Services/OperatorMetadataScanner.cs`
9. `Infrastructure/AI/*`

### 11.3 给算法/视觉同事

推荐顺序：

1. 本文第 5、8、10 章
2. `FlowExecutionService.cs`
3. `OperatorPreviewService.cs`
4. `ParameterRecommender.cs`
5. `Infrastructure/Operators/*`
6. `docs/OPERATOR_CATALOG.md`
7. `docs/operators/*.md`

### 11.4 给设备/自动化同事

推荐顺序：

1. `settingsView.js` 中通信与相机相关部分
2. `SettingsEndpoints.cs`
3. `PlcEndpoints.cs`
4. `Infrastructure/Cameras/*`
5. `Acme.PlcComm/*`

---

## 12. 补充说明与延伸阅读

### 12.1 当前仓库里最值得重点关注的几个“交汇点”

如果同事时间有限，优先研究这几个交汇点：

1. `Program.cs`决定整个产品怎么启动、怎么组装。
2. `FlowExecutionService.cs`决定流程到底怎么跑，是执行引擎核心。
3. `ProjectService.cs`决定工程与流程怎么落盘。
4. `propertyPanel.js` + `previewPanel.js`决定节点编辑体验与预览能力。
5. `settingsView.js`决定配置中心、设备管理、模型管理、用户管理如何统一承载。
6. `aiPanel.js` + `AiFlowGenerationService.cs`
   决定 AI 编排主链路。

### 12.2 当前实现中同事阅读时要有的“认知提醒”

- **流程存储不是纯 EF 思维**：实际以 JSON 流程文件为主。
- **AI 相关不是单一路径**：新旧链路并存，读代码要先分清“当前 UI 主链”与“兼容/保留链”。
- **算子元数据也不是单来源**：`OperatorFactory` 路线更值得优先理解。
- **前端不是传统 MVVM**：更偏模块化原生 JS + 自定义响应式信号。
- **桌面宿主能力不可忽视**：很多能力只有在 WinForms + WebView2 + 本地 API 的组合下才能成立。

### 12.3 推荐继续阅读的文档

- `docs/guides/guide-user.md`
- `docs/guides/guide-deployment.md`
- `docs/OPERATOR_CATALOG.md`
- `docs/operators/*.md`
- `docs/BUG_AUDIT_2026-03-04.md`
- `docs/AI_Model_Integration_Refactor_Plan_2026-02-28.md`

### 12.4 一句话总结

如果把当前仓库抽象成一句话：

> **ClearVision 当前版本已经具备“工业视觉桌面平台”的完整雏形：前端可编排、后端可执行、设备可接入、结果可分析、AI 可辅助。真正的学习重点，不在某一个算法文件，而在这些能力如何通过宿主层、应用层、流程引擎和前端交互层拼接成一个产品。**
