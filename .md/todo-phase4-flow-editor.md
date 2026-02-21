# Phase 4：Vue Flow 节点画布 + 算子面板 + 属性面板

> **作者**: 蘅芜君
> **所属计划**: 前端重构 6 阶段渐进式迁移
> **预计工时**: 10 天（分 4 个子阶段）
> **产出目标**: 核心流程编辑器完全可用，支持 ComfyUI 级节点体验
> **前置依赖**: Phase 3（布局骨架 + 路由）

---

> [!IMPORTANT]
> **AI 协作规则**：每完成一个 `[ ]` 项后，AI 助手必须立即将其标记为 `[x]`，并在文档底部的「执行日志」中追加一条记录。

---

## 一、 Phase 4a — Vue Flow 基础集成（3 天）

### 1.1 安装与配置
- [ ] 安装 `@vue-flow/core` + `@vue-flow/background` + `@vue-flow/controls` + `@vue-flow/minimap`
- [ ] 创建 `components/flow/FlowEditor.vue` 作为画布容器组件
- [ ] 在 `FlowEditorPage.vue` 中集成 `<VueFlow>` 实例
- [ ] 配置画布基础功能：缩放、平移、网格背景、小地图

### 1.2 自定义节点模板
- [ ] 创建 `components/flow/nodes/OperatorNode.vue` — 通用算子节点模板：
  - [ ] 顶部：算子图标 + 名称 + 类别标签
  - [ ] 中部：参数编辑区（Phase 4c 实现，此处预留 slot）
  - [ ] 底部：图像预览缩略图区（Phase 4c 实现，此处预留 slot）
  - [ ] 左侧：输入端口 Handles（按 `PortDataType` 着色）
  - [ ] 右侧：输出端口 Handles（按 `PortDataType` 着色）
- [ ] 定义端口颜色映射 `PORT_TYPE_COLORS`（从现有 `flowCanvas.js` 迁移）：
  - [ ] `Image` → 蓝色
  - [ ] `Integer` → 绿色
  - [ ] `Float` → 橙色
  - [ ] `Boolean` → 红色
  - [ ] `String` → 紫色
  - [ ] `Point` → 青色
  - [ ] 其他类型 → 灰色
- [ ] 创建 `components/flow/nodes/ImageAcquisitionNode.vue` — 图像采集节点特化模板（含相机选择下拉）

### 1.3 自定义连线（Edge）
- [ ] 创建 `components/flow/edges/TypedEdge.vue` — 带类型颜色的连线组件
- [ ] 实现连线颜色根据源端口 `PortDataType` 动态着色
- [ ] 实现端口类型兼容性检查（阻止不兼容类型的连接），迁移自 `checkTypeCompatibility()`

## 二、 Phase 4b — 数据序列化兼容层 + 拖拽（2 天）

### 2.1 Flow Store（Pinia）
- [ ] 创建 `stores/flow.ts`（Pinia store）：
  - [ ] `state`: `nodes`, `edges`, `selectedNodeId`, `isDirty`
  - [ ] `actions`: `addNode()`, `removeNode()`, `addEdge()`, `removeEdge()`, `selectNode()`
  - [ ] `actions`: `serialize()`, `deserialize()`, ` save()`, `load()`
  - [ ] `actions`: `undo()`, `redo()`（操作历史栈）
  - [ ] `getters`: `selectedNode`, `nodeById()`

### 2.2 序列化兼容层
- [ ] 创建 `services/flowSerializer.ts`：
  - [ ] `legacyToVueFlow(jsonData)` — 将现有 FlowCanvas JSON 格式转换为 Vue Flow 节点/边格式
  - [ ] `vueFlowToLegacy(nodes, edges)` — 反向转换（兼容后端 API）
  - [ ] 确保所有现有工程文件可无损加载
- [ ] 编写单元测试，验证双向转换的正确性

### 2.3 拖拽添加节点
- [ ] 实现从算子面板拖拽算子到画布自动创建节点：
  - [ ] 监听 `@dragover` + `@drop` 事件
  - [ ] 根据拖拽数据中的 `OperatorType` 创建对应节点
  - [ ] 节点放置在鼠标释放位置

## 三、 Phase 4c — 节点内参数编辑 + 图像预览（3 天）

### 3.1 节点内参数 Widget
- [ ] 在 `OperatorNode.vue` 中根据算子元数据动态渲染参数控件：
  - [ ] `string` 类型 → `<input>` 文本输入
  - [ ] `number` / `float` 类型 → `<input type="number">` 或 slider
  - [ ] `boolean` 类型 → toggle 开关
  - [ ] `select` 类型 → `<select>` 下拉菜单
  - [ ] `file` 类型 → 文件选择器
- [ ] 参数变更时实时同步到 `flowStore` 并标记 `isDirty`

### 3.2 节点内图像预览
- [ ] 在 `OperatorNode.vue` 底部添加图像预览区域：
  - [ ] 执行后显示该节点的输出图像缩略图（128x96）
  - [ ] 点击缩略图 → 在独立的 ImageViewer 中打开全尺寸图像
  - [ ] 无执行结果时显示占位图标

### 3.3 属性面板（PropertyPanel）
- [ ] 创建 `components/flow/PropertyPanel.vue`：
  - [ ] 当选中节点时，右侧面板显示该节点的完整参数表单
  - [ ] 参数表单与节点内 Widget 双向同步
  - [ ] 从现有 `propertyPanel.js`（550 行）迁移参数渲染逻辑
  - [ ] 支持参数分组折叠
  - [ ] 支持参数依赖联动（某些参数根据其他参数的值显隐）

## 四、 Phase 4d — 执行状态动画 + 右键菜单 + 节点分组（2 天）

### 4.1 执行状态动画
- [ ] 实现节点执行状态视觉反馈：
  - [ ] `idle` → 默认样式
  - [ ] `running` → 边框脉冲动画（Cinnabar 色辉光）
  - [ ] `success` → 绿色边框 + ✓ 图标（持续 2s 后恢复）
  - [ ] `error` → 红色边框 + ✗ 图标 + 错误提示
- [ ] 创建 `stores/execution.ts`（Pinia store）：
  - [ ] `state`: `isRunning`, `nodeStates` (Map<nodeId, status>), `executionTime`
  - [ ] `actions`: `startExecution()`, `stopExecution()`, `updateNodeState()`
  - [ ] 监听 WebView2 bridge 的执行状态推送消息，实时更新

### 4.2 右键上下文菜单
- [ ] 创建 `components/flow/ContextMenu.vue`：
  - [ ] 画布空白处右键：添加节点（搜索）、粘贴、全选
  - [ ] 节点上右键：复制、删除、复制节点ID、查看输出
  - [ ] 连线上右键：删除连线
  - [ ] 多选后右键：批量删除、创建分组

### 4.3 节点分组
- [ ] 实现框选多节点 → 创建分组功能：
  - [ ] 分组显示为半透明彩色矩形背景
  - [ ] 分组可命名、可折叠
  - [ ] 拖拽分组标题 → 整组移动

## 五、 算子面板（OperatorLibrary）

- [ ] 创建 `components/flow/OperatorLibrary.vue`：
  - [ ] 按类别分组显示所有算子（图像处理、检测、通信、标定等）
  - [ ] 搜索框：支持中/英文关键词过滤
  - [ ] 每个算子项显示：图标 + 名称 + 简短描述
  - [ ] 支持拖拽算子到画布
- [ ] 从现有 `operatorLibrary.js`（1320 行）迁移：
  - [ ] 算子分类数据
  - [ ] 搜索过滤逻辑
  - [ ] 拖拽交互

## 六、 集成验证

- [ ] 加载现有工程 JSON 文件 → 节点与连线正确显示
- [ ] 从算子面板拖拽添加新节点 → 正确创建 → 可连线
- [ ] 节点内参数编辑 → 与属性面板双向同步
- [ ] 点击运行 → 节点依次显示执行状态动画
- [ ] 执行完成 → 节点缩略图显示输出结果
- [ ] 右键菜单 → 所有操作正常
- [ ] 保存工程 → 序列化正确 → 后端可解析

---

## 执行日志

> AI 助手在每次完成任务后，在此处追加记录：

