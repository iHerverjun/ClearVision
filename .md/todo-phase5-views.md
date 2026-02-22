# Phase 5：检测视图 + 结果面板 + 工程管理

> **作者**: 蘅芜君
> **所属计划**: 前端重构 6 阶段渐进式迁移
> **预计工时**: 5 天 + 5 天（UI 增强）= 10 天
> **产出目标**: 检测/结果/工程三大功能视图全部可用，覆盖 14 种 PortDataType 的多元化展示，达到专业工业视觉软件的展示标准
> **前置依赖**: Phase 4（流程编辑器）

---

> [!IMPORTANT]
> **AI 协作规则**：每完成一个 `[ ]` 项后，AI 助手必须立即将其标记为 `[x]`，并在文档底部的「执行日志」中追加一条记录。

> [!NOTE]
> **V2 补充说明**：本计划基于后端 67+ 算子、14 种 `PortDataType`（Image / Integer / Float / Boolean / String / Point / Rectangle / Contour / PointList / DetectionResult / DetectionList / CircleData / LineData / Any）以及 AI 自动工程生成的输出特征做了全面增强。相比 V1 新增：图像族（A）、几何叠加族（G）、AI 生成摘要视图、流程执行时间线、实时运行仪表盘、报表导出、工程版本管理等专业功能。

---

## ⚙️ 框架审查结论（实施约束 — 2026-02-22 确认）

> 以下内容基于对现有前端代码库的实际审查，所有实现**必须**遵循这些约束。

### 全局 App Shell 架构

```
MainLayout.vue（全局壳，所有页面共享）
├── AppHeader.vue（顶栏, 64px）
│   ├── Logo "ClearVision"
│   ├── GlassCard 工程选择器（Default_Project_01 / Saved）
│   ├── GlassCard 运行仪表盘（Play按钮 + Cycle Time + Yield Rate）← 宏观数据已在此
│   └── 操作按钮组（Load JSON / Save / Undo / Redo）
├── AppSidebar.vue（左侧栏, 展开200px / 收起64px, 悬停展开）
│   └── 5 路由项: Flow Editor / Inspection / Results / Projects / AI Assistant
├── .content-area（主舞台）← ★ 页面组件渲染在此容器内
│   ├── 定位: top:96px, left:236px(展开)/100px(收起), bottom:56px, right:16px
│   ├── 样式: background:rgba(255,255,255,0.6) + border-radius:24px + blur(20px)
│   └── <router-view> ← InspectionPage / ResultsPage / ProjectsPage 在这里
└── AppStatusBar.vue（底栏, 40px）
    ├── 用户身份（Role + Username）
    ├── 通信心跳（Host Link + PLC-1 状态点）
    └── 硬件资源（CPU% + RAM + 版本号）
```

> [!CAUTION]
> **页面组件（如 InspectionPage.vue）不需要也不能自行实现顶栏、侧栏、底栏。** 只负责 `.content-area` 容器内部的内容渲染。

### 设计约束（硬性规则）

| 约束项 | 值 | 来源 |
|--------|------|------|
| 主色调 | Light Mode: `--bg-primary: #F5F5F7` / `--bg-secondary: #FFFFFF` | `style.css` |
| 强调色 | 丹砂红 `--accent-red: #FF4D4D` | `style.css` |
| 成功色 | `#10b981` | `AppStatusBar` / `AppHeader` |
| 卡片风格 | `GlassCard.vue`: `rgba(255,255,255,0.7)` + `blur(20px)` + `border-radius: 16px` | `shared/GlassCard.vue` |
| 主舞台圆角 | `border-radius: 24px` | `MainLayout.vue .content-area` |
| 图标库 | `lucide-vue-next`（**禁止**使用 Font Awesome 或其他图标库） | `AppHeader` / `AppSidebar` |
| 按钮组件 | 使用 `shared/IconButton.vue`，禁止自行定义按钮样式 | `shared/IconButton.vue` |
| 暗色模式 | 通过 `document.documentElement.dataset.theme = 'dark'` 切换，`GlassCard` 已适配 | `stores/ui.ts` |
| 字体 | `Inter`, system-ui | `style.css` |
| 过渡动画 | 页面切换: `fade-slide`（opacity + translateY 15px） | `MainLayout.vue` |

### 业务逻辑依赖现状

| 依赖项 | 文件 | 状态 | Phase 5 需要做的 |
|--------|------|------|------------------|
| 执行状态 Store | `stores/execution.ts` (545行) | ✅ 已就绪 | 直接消费。已有 `OperatorExecutedEvent`(含outputData/outputImageBase64/executionTimeMs)、`InspectionCompletedEvent`(含status/processingTimeMs)、`ProgressNotificationEvent`、`ImageStreamEvent` |
| 流程 Store | `stores/flow.ts` (8KB) | ✅ 已就绪 | 直接消费。含节点、连线数据 |
| Auth Store | `stores/auth.ts` | ✅ 已就绪 | 直接消费 |
| UI Store | `stores/ui.ts` | ✅ 已就绪 | 含 theme / sidebarCollapsed 状态 |
| WebView2 Bridge | `services/bridge.ts` | ✅ 已就绪 | 通过 `webMessageBridge.sendMessage()` 与后端通信，支持 Mock 模式 |
| Bridge 消息类型 | `services/bridge.types.ts` | ✅ 已就绪 | 包含 `FlowExecute` / `FlowStop` / `ImageStreamEvent` 等 |
| REST Endpoints | `services/endpoints.ts` | ⚠️ 需补充 | 现有: Auth/Project/Flow/Settings/AI。**需新增**: Inspection Results 相关端点 |
| 图像解码 | `services/imageSource.ts` + `execution.ts` 内 | ✅ 已就绪 | PNG/JPEG 自动检测 + base64 ↔ DataURL 转换 + SharedBuffer → Canvas |
| 流程序列化 | `services/flowSerializer.ts` | ✅ 已就绪 | 工程导入/导出 |
| GlassCard 组件 | `components/shared/GlassCard.vue` | ✅ 可直接复用 | props: `interactive` / `hoverGlow` |
| IconButton 组件 | `components/shared/IconButton.vue` | ✅ 可直接复用 | props: `variant` / `size` / `disabled` |
| Inspection Store | — | ❌ 需新建 | `stores/inspection.ts`：管理当前检测状态、节点输出数据、历史记录 |
| Project Store | — | ❌ 需新建 | `stores/project.ts`：工程列表、CRUD 操作 |
| ECharts | — | ❌ 需安装 | `npm install echarts vue-echarts`（用于统计图表） |

---

## 一、 检测视图（InspectionPage.vue）

### 1.1 基础布局
- [ ] 创建 `pages/InspectionPage.vue` 可调整三栏式布局：
  - [ ] 左栏：图像显示区（ImageViewer）— 占主体宽度，可通过拖拽分隔条调整
  - [ ] 中栏：节点输出数据卡片区（按执行顺序纵向排列）
  - [ ] 右栏：综合面板（OK/NG、处理时间、通信状态、AI 摘要）
- [ ] 从现有 `inspectionPanel.js` + `inspectionController.js` 迁移核心逻辑
- [ ] 支持布局模式切换：三栏 / 双栏 / 图像全屏 + 浮窗
- [ ] 响应式断点适配：≥1920px 三栏 / ≥1280px 双栏 / <1280px 图像全屏+底部抽屉

### 1.2 ImageViewer 组件
- [ ] 创建 `components/inspection/ImageViewer.vue`：
  - [ ] Canvas 2D 图像渲染（支持缩放/平移/适应窗口/1:1 像素）
  - [ ] 鼠标滚轮缩放 + 中键拖拽平移，支持触控手势
  - [ ] 叠加 overlay 层：绘制测量线、缺陷框、ROI 区域、几何拟合图形
  - [ ] 多图层管理：原图层 / 处理结果层 / 标注层（每层可独立开关可见性）
  - [ ] 从现有 `imageViewer.js` 迁移渲染逻辑
  - [ ] 支持双击全屏查看
  - [ ] 右下角显示当前坐标（像素 + 物理坐标，若有标定数据）
  - [ ] 图像信息悬浮提示：分辨率、通道数、位深度、文件大小

### 1.3 节点输出数据卡片（8 族展示组件）

> **设计原则**：每张卡片统一使用 Glassmorphism 毛玻璃样式 + 丹砂红主题色点缀。卡片头部显示对应节点名称 + 执行耗时小标签。

#### 族 A — 图像输出（PortDataType.Image）
- [ ] 创建 `components/inspection/ImageResultCard.vue`：
  - [ ] 缩略图预览（点击展开到 ImageViewer）
  - [ ] 支持中间处理图像查看（如滤波后、二值化后、边缘检测后）
  - [ ] 图像对比模式：原图 vs 处理结果，滑块左右切换
  - [ ] 图像直方图概览（灰度/RGB 分布迷你图表）
  - [ ] 标记来源算子名称和处理步骤序号

#### 族 B — 文本输出（PortDataType.String）
- [ ] 创建 `components/inspection/TextResultCard.vue`：
  - [ ] OCR 识别文本大字体展示（自动检测显示字号：短文本放大，长文本适配）
  - [ ] 条码内容 + 条码类型标签（QR / DataMatrix / Code128 等）
  - [ ] 一键复制按钮
  - [ ] 字符串格式化结果展示（来自 StringFormat 算子）
  - [ ] HTTP/MQTT 响应体 JSON 高亮展示
  - [ ] Glassmorphism 毛玻璃卡片样式

#### 族 C — 数值测量（PortDataType.Float / Integer）
- [ ] 创建 `components/inspection/MeasurementGauge.vue`：
  - [ ] 数值仪表盘式展示（Distance, Angle, Area, Radius 等）
  - [ ] 上下限公差带指示（超限变红，临近公差变橙）
  - [ ] 单位标注（mm, °, px 等）
  - [ ] 微动画数值跳动效果
  - [ ] 支持历史趋势迷你折线图（最近 N 次检测的该数值变化）
  - [ ] CPK/Cp 统计指标展示（来自 Statistics 算子输出）

#### 族 D — 判定/缺陷检测（PortDataType.DetectionResult / DetectionList）
- [ ] 创建 `components/inspection/DefectTable.vue`：
  - [ ] DetectionList 可折叠表格
  - [ ] 每行显示：缺陷类型、置信度进度条、bbox 坐标、面积
  - [ ] 点击行 → 在图像上高亮对应的缺陷框（飞入动画）
  - [ ] 按置信度/面积排序
  - [ ] 空状态：显示 "✓ 无缺陷" 绿色提示
  - [ ] 缺陷热力图概览（在缩略图上叠加所有缺陷区域的热力分布）

#### 族 E — 通信状态（Modbus / TCP / S7 / MC / FINS / HTTP / MQTT / Serial）
- [ ] 创建 `components/inspection/CommStatusCard.vue`：
  - [ ] 每个通信节点一行：协议图标 + 设备名称 + 状态圆点（绿/红/橙）+ 响应摘要
  - [ ] 展开查看完整请求/响应报文内容
  - [ ] 超时/断连 → 红色警告 + 重试倒计时
  - [ ] 报文时间戳 + 通信耗时显示
  - [ ] 支持区分 8 种通信协议图标（Modbus/TCP/S7/MC/FINS/HTTP/MQTT/Serial）

#### 族 F — 集合/结构型（PortDataType.Any / 复杂 JSON）
- [ ] 创建 `components/inspection/DataTreeView.vue`：
  - [ ] JSON 树形展示 + 可展开/收起
  - [ ] 列表数据表格展示（虚拟滚动，支持 1000+ 行）
  - [ ] ForEach 子图聚合结果展示：迭代计数 + 通过率 + 逐项结果展开
  - [ ] Aggregator 算子多路数据合并结果展示
  - [ ] 数据搜索/过滤功能

#### 族 G — 几何数据（PortDataType.Point / PointList / Rectangle / Contour / CircleData / LineData）
- [ ] 创建 `components/inspection/GeometryOverlayCard.vue`：
  - [ ] 点坐标列表展示：X, Y 表格 + 在图像上标注点位
  - [ ] 矩形区域：宽×高数值 + 图像上矩形叠加显示
  - [ ] 轮廓数据：轮廓面积/周长/中心 + 图像上轮廓描边
  - [ ] 圆数据：圆心坐标 + 半径 + 图像上画圆（虚线标注半径）
  - [ ] 直线数据：起点/终点坐标 + 角度 + 图像上画线
  - [ ] 几何拟合结果可视化（GeometricFitting 算子输出）
  - [ ] 所有几何数据支持一键复制坐标

#### 族 H — 布尔/判定（PortDataType.Boolean）
- [ ] 创建 `components/inspection/BooleanResultCard.vue`：
  - [ ] 大号 ✓/✗ 图标 + OK/NG 文字
  - [ ] ConditionalBranch 走向可视化（True/False 分支高亮指示）
  - [ ] Comparator 比较结果展示：ValueA OP ValueB → True/False
  - [ ] LogicGate 逻辑门输入→输出真值展示

#### 容器组件
- [ ] 创建 `components/inspection/NodeOutputPanel.vue`：
  - [ ] 根据节点输出端口的 `PortDataType` 动态选择渲染对应卡片组件
  - [ ] 支持多节点输出同时展示（纵向滚动）
  - [ ] 节点卡片支持折叠/展开，记忆用户偏好
  - [ ] 卡片间支持拖拽重新排序
  - [ ] 过滤器：按数据类型筛选关注的输出卡片

### 1.4 综合面板
- [ ] 创建 `components/inspection/InspectionSummary.vue`：
  - [ ] OK/NG 大型指示灯（全屏可见级别，流水线工人无需走近即可判断）
  - [ ] 总处理时间显示（精确到 ms）
  - [ ] 当前检测计数（OK 数 / NG 数 / 总数 / 良品率百分比）
  - [ ] 连续检测循环控制（开始/暂停/停止）
  - [ ] 连续 NG 告警：超过阈值弹出醒目警告（支持配置阈值）

### 1.5 流程执行时间线 ⭐ 新增
- [ ] 创建 `components/inspection/ExecutionTimeline.vue`：
  - [ ] 甘特图式算子执行时间线，每个算子一行，宽度=耗时
  - [ ] 颜色编码：绿色≤预期 / 橙色>预期 / 红色>超限
  - [ ] 高亮关键路径（总耗时瓶颈所在链路）
  - [ ] 支持点击算子条 → 跳转到该算子的输出卡片
  - [ ] 底部显示总耗时 + 各阶段占比饼图
  - [ ] 支持折叠到仅显示总耗时一行

### 1.6 AI 工程摘要视图 ⭐ 新增
- [ ] 创建 `components/inspection/AiFlowSummary.vue`：
  - [ ] 显示当前运行工程是否为 AI 自动生成（AI 标签标识）
  - [ ] 工程描述：AI 生成时的用户原始需求文本
  - [ ] 流程拓扑简图：DAG 缩微图 + 算子数量/连线数量统计
  - [ ] AI 置信度指标：Linter 检查通过率 + DryRun 仿真结果
  - [ ] 快捷操作："编辑流程" / "重新生成" / "查看 Linter 报告"

---

## 二、 实时运行仪表盘（MonitorPage.vue）⭐ 新增

> **背景**: 专业工业视觉软件必备的产线级监控界面，支持 7×24 长时间运行场景。

### 2.1 基础布局
- [ ] 创建 `pages/MonitorPage.vue` 全屏 Dashboard 布局：
  - [ ] 顶栏：当前工程名称 + 运行时长 + 系统时钟
  - [ ] 主体：可配置的卡片网格（支持拖拽编排、持久化布局）
  - [ ] 底栏：全局状态指示条

### 2.2 产线级监控卡片
- [ ] 创建 `components/monitor/ProductionKPI.vue`：
  - [ ] 当日产量（OK+NG总数）大字体显示
  - [ ] 良品率（当日/当班/累计）
  - [ ] 节拍时间（CT）：当前值 / 目标值 / 最近 100 次平均
  - [ ] CT 趋势折线图（实时滚动）
- [ ] 创建 `components/monitor/AlarmPanel.vue`：
  - [ ] 实时告警列表（按严重程度排序：Error > Warning > Info）
  - [ ] 连续 NG 告警、通信中断告警、内存超限告警
  - [ ] 告警确认/静音操作
  - [ ] 告警历史日志（可导出）
- [ ] 创建 `components/monitor/SystemResourceCard.vue`：
  - [ ] CPU / 内存 / GPU 使用率实时曲线
  - [ ] MatPool 内存池使用状态：桶数量 / 缓冲命中率
  - [ ] 相机连接状态 + 帧率

---

## 三、 结果面板（ResultsPage.vue）

### 3.1 基础布局
- [ ] 创建 `pages/ResultsPage.vue`：
  - [ ] 上方：统计卡片快速摘要栏（4~6 个 KPI 指标卡）
  - [ ] 中间：统计 Dashboard（柱状图 + 饼图 + 趋势线 + CPK 图）
  - [ ] 下方：结果历史表格（分页/筛选/排序）
- [ ] 从现有 `resultPanel.js`（800 行）迁移数据展示逻辑

### 3.2 KPI 摘要卡片 ⭐ 新增
- [ ] 创建 `components/results/KpiSummaryBar.vue`：
  - [ ] 今日检测总数（大数字 + 环比昨日箭头）
  - [ ] 良品率（百分比 + 圆环进度图）
  - [ ] 平均 CT（带目标线对比）
  - [ ] 最大/最小测量值（带公差范围条）
  - [ ] NG Top 3 缺陷类型（迷你条形图）
  - [ ] 各卡片支持点击展开详情

### 3.3 统计 Dashboard
- [ ] 创建 `components/results/StatsChart.vue`：
  - [ ] OK/NG 比率饼图（可交互，点击 NG 扇区直接筛选 NG 记录）
  - [ ] 近 N 次检测趋势折线图（支持时间范围选择器）
  - [ ] CPK 统计图表：正态分布曲线 + USL/LSL 线 + Cp/Cpk 指标
  - [ ] 按小时/班次/日的产量柱状图
  - [ ] 缺陷 Pareto 图（缺陷类型频次 + 累积百分比）
  - [ ] 使用 ECharts 或 Chart.js 实现图表
- [ ] 安装数据可视化库：`npm install echarts vue-echarts`

### 3.4 历史表格
- [ ] 创建 `components/results/ResultTable.vue`：
  - [ ] 列：序号、时间戳、判定结果（OK/NG 彩色标签）、处理时间、关键测量值（自动识别数值类型端口）
  - [ ] 支持按时间范围筛选（快捷选项：最近 1h / 8h / 24h / 自定义）
  - [ ] 支持按结果（OK/NG）筛选
  - [ ] 支持按测量值范围筛选（超限项高亮标红）
  - [ ] 点击行 → 展开详情面板：
    - [ ] 检测图像缩略图（点击大图查看）
    - [ ] 所有算子输出数据预览（复用 8 族卡片的迷你版本）
    - [ ] 执行时间线缩微图
  - [ ] 虚拟滚动支持（10 万+ 记录无卡顿）
  - [ ] 列排序（点击列头循环切换升序/降序）

### 3.5 数据导出 ⭐ 增强
- [ ] 创建 `components/results/ExportTools.vue`：
  - [ ] 导出 CSV（含表头 + 筛选后数据）
  - [ ] 导出 Excel（.xlsx，含格式化 + 图表 sheet）
  - [ ] 导出检测报告 PDF（含统计图表 + 抽样图像 + 结论摘要）
  - [ ] 定时自动导出配置（每班/每日/自定义周期）
  - [ ] 导出路径/命名规则配置

---

## 四、 工程管理（ProjectsPage.vue）

### 4.1 基础布局
- [ ] 创建 `pages/ProjectsPage.vue`：
  - [ ] 左侧：工程列表（卡片网格 / 列表模式切换）
  - [ ] 右侧：工程详情 + 操作区
- [ ] 从现有 `projectView.js` + `projectManager.js` 迁移

### 4.2 工程列表
- [ ] 创建 `components/projects/ProjectList.vue`：
  - [ ] 卡片模式：缩略图 + 工程名 + 创建时间 + AI 生成标签
  - [ ] 列表模式：表格展示更多字段
  - [ ] 搜索过滤（支持名称、创建日期、标签）
  - [ ] 排序选项：按名称 / 修改时间 / 创建时间
  - [ ] 右键菜单：重命名、复制、删除、导出、查看版本历史
  - [ ] 双击打开 → 切换到流程编辑器视图并加载该工程
  - [ ] AI 自动生成的工程带特殊标识（AI 图标 + 原始需求描述 tooltip）
  - [ ] 工程标签/分组管理（如：生产线A、生产线B、调试用、模板）

### 4.3 工程操作
- [ ] 创建 `components/projects/ProjectActions.vue`：
  - [ ] 新建空白工程按钮
  - [ ] AI 一键生成工程入口（跳转 AI 页面 or 弹出快捷输入框）
  - [ ] 从模板创建工程（内置常用场景模板）
  - [ ] 导入工程（.json / .zip 文件）
  - [ ] 导出工程（含可选的连带资源导出：标定文件、模型文件等）
  - [ ] 批量删除（二次确认弹窗）

### 4.4 工程版本与历史 ⭐ 新增
- [ ] 创建 `components/projects/ProjectVersionHistory.vue`：
  - [ ] 版本列表：每次保存自动生成版本号 + 时间戳 + 变更摘要
  - [ ] 版本差异对比：两个版本间的算子/连线变更高亮
  - [ ] 一键回滚到历史版本
  - [ ] AI 生成 vs 人工修改的变更标注

### 4.5 工程详情预览 ⭐ 新增
- [ ] 创建 `components/projects/ProjectDetail.vue`：
  - [ ] 工程元数据：名称、描述、创建/修改时间、作者
  - [ ] 流程拓扑缩微图（只读预览）
  - [ ] 算子统计：算子总数、各分类数量饼图
  - [ ] 资源依赖列表：引用的标定文件、ONNX 模型、模板图像
  - [ ] 最近运行记录：最近 5 次检测结果和耗时

### 4.6 Project Store
- [ ] 创建 `stores/project.ts`（Pinia store）：
  - [ ] `state`: `projects`, `currentProjectId`, `isLoading`, `tags`, `sortBy`
  - [ ] `actions`: `fetchProjects()`, `createProject()`, `openProject()`, `deleteProject()`, `exportProject()`, `duplicateProject()`, `createFromTemplate()`, `getVersionHistory()`, `rollbackVersion()`
  - [ ] 与后端 REST API 对接
  - [ ] 本地缓存 + 乐观更新

---

## 五、 日志与调试视图（LogPage.vue）⭐ 新增

> **背景**: 工业软件的运维可观测性基础，用于 AI 工程调试和产线排障。

### 5.1 基础布局
- [ ] 创建 `pages/LogPage.vue`：
  - [ ] 实时日志流（WebSocket 推送）
  - [ ] 历史日志查询（分页 + 时间范围 + 级别筛选）

### 5.2 日志组件
- [ ] 创建 `components/logs/LogViewer.vue`：
  - [ ] 日志级别彩色标签：DEBUG(灰) / INFO(蓝) / WARN(橙) / ERROR(红)
  - [ ] 按算子/来源 模块筛选
  - [ ] 关键字搜索 + 高亮匹配
  - [ ] 自动滚动到底部（可暂停）
  - [ ] 通信报文日志：请求/响应报文十六进制 + 解析视图
  - [ ] AI 生成日志：Prompt 内容 / LLM 原始响应 / Parser 结果 / Linter 报告

---

## 六、 全局共享能力 ⭐ 新增

### 6.1 通用数据协议
- [ ] 创建 `services/outputRenderer.ts`：
  - [ ] 统一的 `PortDataType → 组件映射表`（14 种类型全覆盖）
  - [ ] 通用的数据格式化工具函数（数值精度、单位换算、时间格式化）
  - [ ] 未知数据类型的 fallback JSON 展示
  - [ ] 图像 base64 → Blob URL 转换 + 内存自动回收

### 6.2 WebSocket 实时推送集成
- [ ] 创建 `services/realtimeService.ts`：
  - [ ] 算子执行进度实时推送（用于执行时间线动态填充）
  - [ ] 检测结果实时推送（用于仪表盘更新）
  - [ ] 告警事件推送
  - [ ] 通信状态变更推送
  - [ ] 断线重连 + 消息队列缓冲

### 6.3 主题与国际化增强
- [ ] 检测视图适配深色模式（产线低光环境常用）
- [ ] OK/NG 指示灯支持 高对比度模式（色盲友好）
- [ ] 关键状态文字支持中英双语切换

---

## 七、 集成验证

### 7.1 数据类型全覆盖验证
- [ ] Image 输出：中间处理图正确展示 + 图像对比滑块
- [ ] String 输出：OCR 文本正确显示 + 条码类型标签
- [ ] Float/Integer 输出：测量仪表盘渲染 + 公差带指示
- [ ] Boolean 输出：OK/NG 指示灯 + 分支走向可视化
- [ ] DetectionList 输出：缺陷表格与图像联动高亮
- [ ] Point/PointList 输出：坐标在图像上正确叠加
- [ ] Rectangle 输出：矩形框在图像上正确绘制
- [ ] Contour 输出：轮廓描边正确叠加
- [ ] CircleData 输出：圆及半径标注正确绘制
- [ ] LineData 输出：直线及角度标注正确绘制
- [ ] Any 输出：JSON 树形展示 + ForEach 聚合结果

### 7.2 通信协议全覆盖验证
- [ ] 8 种通信协议状态卡正确区分图标和状态

### 7.3 AI 工程验证
- [ ] AI 生成的工程标签正确展示
- [ ] AI 工程摘要视图正确读取原始需求
- [ ] Linter 报告快捷跳转可用

### 7.4 产线级功能验证
- [ ] 连续运行 1 小时：检测视图内存无泄漏（图像 Blob URL 正确回收）
- [ ] 实时运行仪表盘：KPI 数据持续滚动更新
- [ ] 结果面板：10 万+ 条历史数据虚拟滚动流畅
- [ ] CSV/PDF 导出生成正确

### 7.5 工程管理验证
- [ ] 新建/打开/删除工程流程完整
- [ ] 现有工程数据加载正确、无数据丢失
- [ ] 工程版本回滚不丢失数据

---

## 执行日志

> AI 助手在每次完成任务后，在此处追加记录：

