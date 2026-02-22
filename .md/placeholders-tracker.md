# 占位符记录 (Placeholders Tracker)

> **创建日期**: 2026-02-22
> **目的**: 记录为了推进前端现代架构，但后端接口/功能尚未完全实现时，前端暂时硬编码或留空的占位符区域。

## 布局与框架占位 (Phase 3)

| 组件位置 | 占位内容 | 原定对接后端功能 | 备注 | 状态 |
|---------|----------|----------------|------|------|
| `AppHeader.vue` | 项目名称切换/下拉列表 | 获取当前加载的项目名及项目列表 | 后续由 ProjectStore 接入 | 🔴 待替换 |
| `AppHeader.vue` | 运行进度条/状态 | 获取 Flow 执行的实时状态 | 后续由 ExecutionStore 接入 | 🔴 待替换 |
| `AppStatusBar.vue` | CPU/内存使用率指示 | 后端性能系统状态 | 需要增强通信协议 | 🔴 待替换 |
| `AppStatusBar.vue` | 相机连接数/状态 | 后端硬件状态总线 | 现有接口较弱，需新增 | 🔴 待替换 |
| `AppSidebar.vue` | 动态通知 Badge | 产线异常或系统预警红点 | 需后端 WebSocket 推送配合 | 🔴 待替换 |

## 页面视图占位 (Phase 3+)

| 组件位置 | 占位内容 | 原定对接后端功能 | 备注 | 状态 |
|---------|----------|----------------|------|------|
| `FlowEditorPage.vue` | 画布区 | FlowCanvas 迁移 | Phase 4 解决 | 🔴 待替换 |
| `InspectionPage.vue` | 实时流及检测框 | 相机抽帧及渲染 | Phase 5 解决 | 🔴 待替换 |
| `ResultsPage.vue` | 数据Dashboard图表 | 历史结果查询与聚合 | 依赖数据库接口完善 | 🔴 待替换 |

## Phase 5 核心视图占位 (根据最新高保真设计补充, 2026-02-22)

| 组件位置 | 占位内容 | 缺失的后端能力/原因 | 备注 | 状态 |
|---------|----------|-------------------|------|------|
| `InspectionPage.vue - Controls` | 连续运行心跳 (Continuous Run) | 后端暂缺持续执行和流式结果推送接口 | 前端暂用 `setInterval` 模拟循环调用单次运行 | 🔴 待替换 |
| `InspectionPage.vue - ImageViewer` | 四周红色告警发光特效 | 需要将检测结果与UI警告状态绑定 | 基于 Mock 数据条件渲染 | 🔴 待替换 |
| `ResultsPage.vue - HistoryRibbon` | 横向无限滚动的历史图片条 | 后端无支持流式返回历史图像的 API (只返回Base64内存易爆) | 前端硬编码占位符图片/Mock列表支持虚拟滚动 | 🔴 待替换 |
| `ResultsPage.vue - Dashboard` | CPK及图表统计数据面板 | 后端未提供跨批次/时间段的数据聚合计算接口 | 先行封装 ECharts 并填入静态 Mock 数据 | 🔴 待替换 |
| `ProjectsPage.vue - Dashboard` | 工程维度统计面板 (柏拉图/良优率) | 项目维度统计缺失 | 先以前端假数据填充 Dashboard UI | 🔴 待替换 |
| `InspectionPage/ResultsPage` | 卡片右侧详细执行耗时 | ExecutionStore/后端输出数据需细化提供算子级别耗时 | 占用空间位，不显示真实数据 | 🔴 待替换 |
