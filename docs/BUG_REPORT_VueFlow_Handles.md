# 🐞 故障跟踪报告: Vue Flow 节点端点 (Handles) 隐身问题总结

**日期**: 2026-02-22
**目标组件**: `OperatorNode.vue` & `FlowEditor.vue`
**症状描述**: 在 Vue Flow 画布上拖出算子卡片后，卡片两边的用于连线的红绿蓝彩色输入/输出端点（Handles / Ports）在视觉上完全不可见，导致连线功能受阻。

---

## 🔍 问题前因与底层机制分析 (Root Cause Analysis)

在排查这个顽固的隐身问题时，我们发现这涉及到了 Vue 3 渲染器、CSS 盒模型约束以及外部组件的作用域等多个复杂机制的交织。

### 1. 第一层阻碍：CSS 物理裁切 (`overflow: hidden`)
为了保持卡片漂亮的毛玻璃属性和柔和的圆角（`16px`），我们在卡片的最外层 `.operator-node` 上施加了 `overflow: hidden;`。
- **冲突原理**：由于 Vue Flow 的连线端点需要通过绝对定位挂在卡片的**物理边界之外**（`left: -6px` / `right: -6px`），它们被视为了“越界元素”，直接被浏览器的渲染引擎裁切（隐藏）掉了。

### 2. 第二层阻碍：插槽标识映射断裂 (Slot Naming Mismatch)
当我们首次尝试移除 `overflow: hidden` 后依然无效，此时我们通过 Vue devtools 切片发现，实际上底层渲染的节点并没有套用我们精美的 `OperatorNode.vue` 模板！
- **冲突原理**：在 `FlowEditor.vue` 里注册自定义节点时，使用的是 `<template #node-operator>`，但数据源生成的新节点绑定的类型却是字符串 `'operator-node'`。这种 Mismatch 导致 Vue Flow 降级渲染了一个内建的“白板默认节点”，所以我们的任何 Handle 定义根本就没有挂载到 DOM 树上。

### 3. 第三层阻碍：枚举降级与 CSS 深度作用域隔离 (Scoped piercing limit)
完全对齐了 Slots 之后，定制卡片出来了，但端点还是不可见。
- **代码污染 1 (Enum 脱离)**：在 `<script setup>` 里我们使用了 `<Handle :position="Position.Left" />`，但有时因为 Vite 热更的边缘情况，外部库的枚举类型未被正确计算，导致 Vue Flow 根本拒绝渲染那些不知所云的 Handle 结构。
- **代码污染 2 (Scoped CSS 墙)**：Vue 3 的 `<style scoped>` 会强制隔离样式。我们给 `.custom-handle` 指定的红蓝配色、12px 的大小属性，根本渗透不进外部子组件（`@vue-flow/core/Handle`）里自带的默认 `.vue-flow__handle` 标签身上，并且父级行容器缺少了显式的参照锚点 `position: relative` 导致绝对坐标体系坍缩至无穷小或跑偏。

---

## 🛠️ 解决记录与技术对策 (Attempted Solutions)

我们针对这三大阻力，实施了三重防波堤打击：

| 排查阶段 | 问题点 | 解决办法及修改文件 | 状态 |
| :--- | :--- | :--- | :--- |
| **Phase A** | 模板挂载错位 | 修改 `FlowEditor.vue`，将插槽命名对齐为 `<template #node-operator-node>` | ✅ 修复生效（卡片已变身玻璃材质） |
| **Phase B** | Handles 被物理隐身 | 修改 `OperatorNode.vue`，删去根容器的 `overflow: hidden` 控制，转为精确对 Header 的顶部单独上圆角。 | ✅ 修复生效（释放被吞噬的外部悬浮空间） |
| **Phase C** | Vue 3 层级与隔离干涉 | 放弃脆弱的 `Position` Enum，改用静态字面量 `position="left"`。在 CSS 中大范围启用 `:deep(...)` 穿透指令与 `!important`，并补齐缺少的 `position: relative !important` 在 `port-row` 父集上。 | 🔄 终端依然表示未渲染成功 |

---

## 🛡️ @debug 延伸研判与待验证猜想 (Next Steps)

虽然我们已经完成了代码层面最严密的封锁线，如果此刻界面依然**看不见小圆点**，这就突破了目前组件内部逻辑的范畴。

作为下一步定位问题的锚点，可能的原因在这些外部作用域上：
1. **Z-Index 被遮罩层反杀**：是否画布上的其它 Vue Flow 面板（如 Controls / Panel 等层叠上下文）强行盖住了所有的 Nodes Handles？
2. **Handle CSS Source Code 拦截**：在 `FlowEditor.vue` 的顶层我们导入了 `@vue-flow/core/dist/theme-default.css`。这个原生 CSS 包可能会在深层包含类似于把 `.vue-flow__handle` `opacity: 0` 隐藏的强制重置（它本意或许是需要用户悬浮才显示）。
3. **数据层 Port ID 等于 Null**：在数据序列化/生成过程中，如果 `port.id` 生成为空（Undefined），Vue Flow 为了避免引擎报错连线寻址不到，会在渲染层级直接跳过生成这个失效的 Handle DOM。

如果你同意，我们可以针对最后这三个外部的 “悬案可能” 开火排查！
