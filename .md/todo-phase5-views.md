# Phase 5：检测视图 + 结果面板 + 工程管理

> **作者**: 蘅芜君
> **所属计划**: 前端重构 6 阶段渐进式迁移
> **预计工时**: 3 天 + 3 天（UI 增强）= 6 天
> **产出目标**: 检测/结果/工程三大功能视图全部可用，支持丰富的数据类型展示
> **前置依赖**: Phase 4（流程编辑器）

---

> [!IMPORTANT]
> **AI 协作规则**：每完成一个 `[ ]` 项后，AI 助手必须立即将其标记为 `[x]`，并在文档底部的「执行日志」中追加一条记录。

---

## 一、 检测视图（InspectionPage.vue）

### 1.1 基础布局
- [ ] 创建 `pages/InspectionPage.vue` 三栏式布局：
  - [ ] 左栏：图像显示区（ImageViewer）
  - [ ] 中栏：节点输出数据卡片区
  - [ ] 右栏：综合面板（OK/NG、处理时间、通信状态）
- [ ] 从现有 `inspectionPanel.js` + `inspectionController.js` 迁移核心逻辑

### 1.2 ImageViewer 组件
- [ ] 创建 `components/inspection/ImageViewer.vue`：
  - [ ] Canvas 2D 图像渲染（支持缩放/平移/适应窗口）
  - [ ] 叠加 overlay 层：绘制测量线、缺陷框、ROI 区域
  - [ ] 从现有 `imageViewer.js` 迁移渲染逻辑
  - [ ] 支持双击全屏查看

### 1.3 节点输出数据卡片（6 族展示组件）

#### 族 B — 文本输出
- [ ] 创建 `components/inspection/TextResultCard.vue`：
  - [ ] OCR 识别文本大字体展示
  - [ ] 条码内容 + 条码类型标签
  - [ ] 一键复制按钮
  - [ ] Glassmorphism 毛玻璃卡片样式

#### 族 C — 数值测量
- [ ] 创建 `components/inspection/MeasurementGauge.vue`：
  - [ ] 数值仪表盘式展示（Distance, Angle, Area 等）
  - [ ] 上下限公差带指示（超限变红）
  - [ ] 单位标注（mm, °, px 等）
  - [ ] 微动画数值跳动效果

#### 族 D — 判定/缺陷
- [ ] 创建 `components/inspection/DefectTable.vue`：
  - [ ] DetectionList 可折叠表格
  - [ ] 每行显示：缺陷类型、置信度、bbox 坐标
  - [ ] 点击行 → 在图像上高亮对应的缺陷框
  - [ ] 空状态：显示 "✓ 无缺陷" 提示

#### 族 E — 通信状态
- [ ] 创建 `components/inspection/CommStatusCard.vue`：
  - [ ] 每个通信节点一行：PLC名称 + 状态圆点（绿/红）+ 响应摘要
  - [ ] 展开查看完整响应内容
  - [ ] 超时/断连 → 红色警告

#### 族 F — 集合/结构型
- [ ] 创建 `components/inspection/DataTreeView.vue`：
  - [ ] JSON 树形展示 + 可展开/收起
  - [ ] 列表数据表格展示（虚拟滚动）

#### 容器组件
- [ ] 创建 `components/inspection/NodeOutputPanel.vue`：
  - [ ] 根据节点输出端口的 `PortDataType` 动态选择渲染对应卡片组件
  - [ ] 支持多节点输出同时展示（纵向滚动）

### 1.4 综合面板
- [ ] 创建 `components/inspection/InspectionSummary.vue`：
  - [ ] OK/NG 大型指示灯（全屏可见级别）
  - [ ] 总处理时间显示
  - [ ] 当前检测计数（OK 数 / NG 数 / 总数）
  - [ ] 连续检测循环控制（开始/暂停/停止）

## 二、 结果面板（ResultsPage.vue）

### 2.1 基础布局
- [ ] 创建 `pages/ResultsPage.vue`：
  - [ ] 上方：统计 Dashboard（柱状图 + 饼图 + 趋势线）
  - [ ] 下方：结果历史表格（分页/筛选/排序）
- [ ] 从现有 `resultPanel.js`（800 行）迁移数据展示逻辑

### 2.2 统计 Dashboard
- [ ] 创建 `components/results/StatsChart.vue`：
  - [ ] OK/NG 比率饼图
  - [ ] 近 N 次检测趋势折线图
  - [ ] CPK 统计指标卡片
  - [ ] 使用 ECharts 或 Chart.js 实现图表
- [ ] 安装数据可视化库：`npm install echarts vue-echarts`

### 2.3 历史表格
- [ ] 创建 `components/results/ResultTable.vue`：
  - [ ] 列：序号、时间、判定结果（OK/NG）、处理时间、关键测量值
  - [ ] 支持按时间范围筛选
  - [ ] 支持按结果（OK/NG）筛选
  - [ ] 点击行 → 展开详情（图像 + 完整输出数据）
  - [ ] 支持导出 CSV/Excel

## 三、 工程管理（ProjectsPage.vue）

### 3.1 基础布局
- [ ] 创建 `pages/ProjectsPage.vue`：
  - [ ] 左侧：工程列表（树形或卡片）
  - [ ] 右侧：工程详情 + 操作区
- [ ] 从现有 `projectView.js` + `projectManager.js` 迁移

### 3.2 工程列表
- [ ] 创建 `components/projects/ProjectList.vue`：
  - [ ] 显示所有工程：名称、创建时间、最后修改时间、缩略图
  - [ ] 搜索过滤
  - [ ] 右键菜单：重命名、复制、删除、导出
  - [ ] 双击打开 → 切换到流程编辑器视图并加载该工程

### 3.3 工程操作
- [ ] 创建 `components/projects/ProjectActions.vue`：
  - [ ] 新建工程按钮
  - [ ] 导入工程（.json 文件）
  - [ ] 导出工程
  - [ ] 批量删除

### 3.4 Project Store
- [ ] 创建 `stores/project.ts`（Pinia store）：
  - [ ] `state`: `projects`, `currentProjectId`, `isLoading`
  - [ ] `actions`: `fetchProjects()`, `createProject()`, `openProject()`, `deleteProject()`, `exportProject()`
  - [ ] 与后端 REST API 对接

## 四、 集成验证

- [ ] 检测视图：运行流程后，6 族输出数据卡片正确展示各类型数据
- [ ] OCR 文本结果正确显示 + 可复制
- [ ] 测量数值仪表盘正确显示 + 公差带指示
- [ ] 缺陷表格与图像联动高亮
- [ ] 通信状态卡片实时更新
- [ ] 结果面板：统计图表正确渲染 + 历史表格分页正常
- [ ] 工程管理：新建/打开/删除工程流程完整
- [ ] 现有工程数据加载正确、无数据丢失

---

## 执行日志

> AI 助手在每次完成任务后，在此处追加记录：

