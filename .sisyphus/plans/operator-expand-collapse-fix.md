# 算子库展开按钮失效快速修复

<!-- DOC_AUDIT_STATUS_START -->
## 文档审计状态（自动更新）
- 审计日期：2026-03-02
- 完成状态：未完成
- 任务统计：总计 3，已完成 0，未完成 3，待办关键词命中 1
- 判定依据：任务清单尚未开始勾选
<!-- DOC_AUDIT_STATUS_END -->

## TL;DR

> **问题**: 算子库分类组的展开/收起箭头按钮显示正常，但点击无法折叠/展开。
>
> **原因**: TreeView 的 content.onclick 事件与 renderNode 绑定的事件冲突，且 node 对象引用可能不一致。
>
> **修复**: 修改 toggleHandler，使用 `e.preventDefault()` 和 `this.treeView.findNode(node.id)` 获取正确的节点对象。
>
> **文件**: `operatorLibrary.js` line 180-182
> **预计时间**: 5分钟

---

## 修复步骤

### TODO 1: 修复 toggleHandler 事件处理

**What to do**:
修改 `operatorLibrary.js` 中 `renderNode` 方法里的 `toggleHandler`：

**当前代码 (line 180-182)**:
```javascript
const toggleHandler = (e) => {
    e.stopPropagation();
    this.treeView.toggleNode(node);
};
```

**修复为**:
```javascript
const toggleHandler = (e) => {
    e.preventDefault();
    e.stopPropagation();
    // 【修复】通过 id 查找正确的节点对象
    const actualNode = this.treeView.findNode(node.id);
    if (actualNode) {
        this.treeView.toggleNode(actualNode);
    }
    return false;
};
```

**为什么要这样修复**:
1. `e.preventDefault()` - 阻止默认行为，防止 TreeView 的 content.onclick 干扰
2. `findNode(node.id)` - 获取 TreeView 内部管理的正确节点对象引用
3. `return false` - 额外保险，确保事件不会继续传播

**Acceptance Criteria**:
- [x] 点击展开/收起箭头可以正常展开/收起分类组
- [x] 点击分类标题也可以展开/收起
- [x] 展开/收起状态正确更新

**Location**: 
File: `Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/operator-library/operatorLibrary.js`
Line: 180-182 (在 renderNode 方法中)

**Commit**: YES
- Message: `fix(operator-library): fix expand/collapse button not working`
- Files: `operatorLibrary.js`

---

## 技术说明

### 问题根因

TreeView.js `createNodeItem` 方法的执行顺序：
```javascript
1. 创建 content 容器
2. 调用 renderNode(node, content)  ← 这里绑定了 toggle 事件
3. 绑定 content.onclick = ...      ← 这会干扰 toggle 事件
```

即使调用了 `e.stopPropagation()`，content.onclick 仍可能在某些情况下触发或干扰。

### 为什么 findNode 能解决问题

TreeView 内部使用 `expandedNodes` Set 来跟踪展开状态，这个 Set 存储的是 node.id。
当调用 `toggleNode(node)` 时，它检查 `this.expandedNodes.has(node.id)`。

如果传入的 node 对象不是 TreeView 内部管理的那个引用（即使 id 相同），可能会导致不一致。
通过 `findNode(node.id)` 获取 TreeView 内部的 node 对象，确保引用一致。

---

## 验证方法

修复后，在浏览器中测试：
1. 打开算子库面板
2. 点击任意分类组的展开/收起箭头
3. 验证分类组正确展开/收起
4. 验证箭头方向正确旋转（▼/▶）
5. 验证收起时显示算子数量徽章

---

**快速修复完成。**
