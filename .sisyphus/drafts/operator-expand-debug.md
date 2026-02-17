# 算子库展开/收起按钮失效问题诊断与修复

## 问题分析

### 根本原因
TreeView.js 的 `createNodeItem` 方法中，在调用 `renderNode` 之后（line 126-133），又给整个 content 容器绑定了点击事件（line 144）：

```javascript
// 事件绑定
content.onclick = (e) => {
    e.stopPropagation();
    this.selectNode(node, !this.options.multiSelect || !e.ctrlKey);
};
```

虽然 renderNode 中给 toggle 按钮绑定了点击事件并调用了 `e.stopPropagation()`，但是：
1. **事件监听器可能被覆盖** - TreeView 后续的操作可能干扰了事件绑定
2. **闭包问题** - renderNode 中的 `node` 对象可能不是 TreeView 内部使用的同一个引用

### 问题验证
查看 operatorLibrary.js renderNode 代码：
```javascript
const toggleHandler = (e) => {
    e.stopPropagation();
    this.treeView.toggleNode(node);  // 这里的 node 可能是问题
};
```

问题：`toggleNode` 方法使用 `node.id` 来检查展开状态，但 renderNode 传入的 node 可能是原始数据对象，而不是 TreeView 内部管理的节点对象。

---

## 修复方案

### 方案：通过 node.id 查找正确的节点对象

在 renderNode 中不直接使用传入的 node 对象，而是通过 node.id 从 TreeView 中查找：

```javascript
const toggleHandler = (e) => {
    e.stopPropagation();
    // 通过 id 查找正确的节点对象
    const actualNode = this.treeView.findNode(node.id);
    if (actualNode) {
        this.treeView.toggleNode(actualNode);
    }
};
```

### 备选方案：阻止 content.onclick 触发

如果上述方案无效，可能是因为 content.onclick 和 toggle 按钮的点击事件冲突。可以让分类节点的 content 点击不触发 select，而是触发 toggle：

```javascript
// 在 renderNode 中，阻止事件冒泡到 content
const toggleHandler = (e) => {
    e.preventDefault();
    e.stopPropagation();
    const actualNode = this.treeView.findNode(node.id);
    if (actualNode) {
        this.treeView.toggleNode(actualNode);
    }
    return false;
};
```

---

## 实施步骤

### 步骤 1: 修改 operatorLibrary.js renderNode

找到 renderNode 中的 toggleHandler：

```javascript
const toggleHandler = (e) => {
    e.stopPropagation();
    this.treeView.toggleNode(node);
};
```

改为：

```javascript
const toggleHandler = (e) => {
    e.stopPropagation();
    e.preventDefault();
    // 通过 id 查找正确的节点对象
    const actualNode = this.treeView.findNode(node.id);
    if (actualNode) {
        this.treeView.toggleNode(actualNode);
    }
};
```

### 步骤 2: 应用同样的修改到 wrapper 点击事件

```javascript
// 点击分类内容也可以展开/收起
if (wrapper) {
    wrapper.style.cursor = 'pointer';
    wrapper.addEventListener('click', toggleHandler);
}
```

### 步骤 3: 确保事件绑定在 render 完成后执行

如果问题仍然存在，可能是 renderNode 执行时 DOM 还没有稳定。可以尝试使用 setTimeout：

```javascript
setTimeout(() => {
    const toggle = element.querySelector('.cv-treeview-toggle');
    const wrapper = element.querySelector('.category-content-wrapper');
    
    if (hasChildren && toggle) {
        toggle.addEventListener('click', toggleHandler);
    }
    
    if (wrapper) {
        wrapper.style.cursor = 'pointer';
        wrapper.addEventListener('click', toggleHandler);
    }
}, 0);
```

---

## 根本原因补充

TreeView 架构问题：

```javascript
createNodeItem(node, level) {
    // 1. 创建基础结构
    const content = document.createElement('div');
    
    // 2. 添加 TreeView 默认的 toggle 按钮
    if (hasChildren) {
        content.appendChild(toggle);  // toggle 有 onclick
    }
    
    // 3. 调用 renderNode - 这里会覆盖 innerHTML，导致 TreeView 的 toggle 被删除
    if (this.options.renderNode) {
        this.options.renderNode(node, content);
    }
    
    // 4. 又给整个 content 绑定 onclick - 这会干扰 renderNode 的事件
    content.onclick = (e) => { ... };
}
```

**关键问题**：
- renderNode 覆盖了 content.innerHTML，删除了 TreeView 默认的 toggle
- TreeView 的 content.onclick 可能会干扰 renderNode 中绑定的事件
- 即使调用了 stopPropagation，content.onclick 仍然会在冒泡阶段触发

**最佳修复**：确保使用正确的 node 对象调用 toggleNode。
