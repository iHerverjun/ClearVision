# 算子库分组展开/收起功能设计计划

<!-- DOC_AUDIT_STATUS_START -->
## 文档审计状态（自动更新）
- 审计日期：2026-03-02
- 完成状态：未完成
- 任务统计：总计 23，已完成 0，未完成 23，待办关键词命中 10
- 判定依据：任务清单尚未开始勾选
<!-- DOC_AUDIT_STATUS_END -->

## TL;DR

> **目标**: 为算子库的分组（预处理、PLC通讯等）添加展开/收起按钮，提升浏览效率。
>
> **当前问题**: TreeView 组件已支持展开/收起，但 operatorLibrary 的自定义渲染覆盖了分类节点的展开按钮。
>
> **解决方案**: 在保留自定义分类渲染的同时，集成展开/收起按钮，并优化视觉样式。
>
> **涉及文件**:
> - `operatorLibrary.js` - 修改分类节点渲染，保留展开按钮
> - `operator-library.css` - 添加展开/收起按钮样式
> - `treeView.js` (可选) - 增强展开/收起交互
>
> **预估工作量**: 小型任务（2-3个TODO）
> **并行执行**: 否（顺序实现）

---

## 现状分析

### 当前架构

```
OperatorLibraryPanel
├── TreeView (树形控件)
│   ├── 分类节点 (category) - 【自定义渲染覆盖了展开按钮】
│   │   └── 算子节点 (operator)
│   └── 分类节点
│       └── 算子节点
└── 底部快捷操作栏
    ├── 展开全部
    ├── 折叠全部
    └── 刷新列表
```

### 问题根因

**TreeView.js** (line 96-104) 已原生支持展开/收起：
```javascript
if (hasChildren) {
    const toggle = document.createElement('span');
    toggle.className = `cv-treeview-toggle ${isExpanded ? 'expanded' : 'collapsed'}`;
    toggle.textContent = isExpanded ? '▼' : '▶';
    toggle.onclick = (e) => {
        e.stopPropagation();
        this.toggleNode(node);
    };
    content.appendChild(toggle);
}
```

**operatorLibrary.js** (line 132-138) 的 renderNode 覆盖了分类节点：
```javascript
element.innerHTML = `
    <span class="tree-node-icon">${node.customIcon || node.icon}</span>
    <span class="tree-node-label">${node.label}</span>
`;
```

**结果**: 分类节点的展开/收起按钮被覆盖，只能通过底部按钮全局控制。

---

## 设计方案

### 方案对比

| 方案 | 描述 | 优点 | 缺点 | 推荐度 |
|------|------|------|------|--------|
| **A. 修复 renderNode** | 在自定义渲染中保留展开按钮 | 改动最小，符合现有架构 | 需要手动处理按钮事件 | ★★★★★ |
| **B. 渲染后注入** | render 完成后动态添加按钮 | 不修改 renderNode 逻辑 | 时序复杂，易出错 | ★★☆☆☆ |
| **C. 修改 TreeView** | 让 TreeView 在 renderNode 后添加按钮 | 通用性强 | 影响其他使用 TreeView 的地方 | ★★★☆☆ |
| **D. 重写分类渲染** | 完全自定义分类节点结构 | 灵活性最高 | 工作量大 | ★★★☆☆ |

**选定方案**: **方案A - 修复 renderNode**

理由：
1. 改动范围最小，仅修改 operatorLibrary.js
2. 保持现有 TreeView 架构不变
3. 通过 CSS 优化按钮样式
4. 易于维护和扩展

### UI 设计

**展开状态**:
```
▼  📁 预处理
   ├── 🔍 滤波
   ├── ⚫ 二值化
   └── 🔄 形态学

▼  📡 PLC通讯
   ├── 🔌 Modbus
   └── 🔗 TCP通信
```

**收起状态**:
```
▶  📁 预处理 (3)

▶  📡 PLC通讯 (2)
```

**交互设计**:
1. **箭头按钮** - 点击展开/收起该组
2. **分类标题** - 点击也可展开/收起
3. **动画过渡** - 平滑的展开/收起动画
4. **数量提示** - 收起时显示该组算子数量

### 视觉样式

**展开/收起按钮**:
- 使用旋转的 Chevron 箭头（SVG）替代文字字符
- 收起时: 右箭头 ▶ (旋转 -90deg)
- 展开时: 下箭头 ▼ (旋转 0deg)
- 添加旋转动画过渡
- 颜色与主题一致（金色/灰色）

**分类标题**:
- 左侧显示展开按钮
- 中间显示分类图标 + 名称
- 右侧收起时显示算子数量徽章

---

## 工作计划

### Wave 1: 核心功能实现

#### TODO 1: 修改 operatorLibrary.js renderNode 保留展开按钮

**What to do**:
- 在 `renderNode` 方法中，对分类节点不直接覆盖 innerHTML
- 保留 TreeView 创建的展开/收起按钮结构
- 将自定义内容（图标、标签）插入到 TreeView 创建的 content 中
- 或者：在自定义渲染中手动创建展开按钮并绑定事件

**Code Location**: `operatorLibrary.js` line 103-142

**Implementation Details**:

方案选择：在 renderNode 中检测是否为分类节点，如果是则保留展开按钮结构。

```javascript
renderNode: (node, element) => {
    if (node.type === 'operator') {
        // 算子节点 - 保持现有逻辑
        const operator = node.data;
        element.innerHTML = `
            <div class="operator-item-content">
                <span class="operator-drag-handle">⋮⋮</span>
                <span class="operator-icon">${node.customIcon || node.icon || '📦'}</span>
                <div class="operator-info">
                    <span class="operator-name">${node.label}</span>
                    <span class="operator-desc">${operator?.description || ''}</span>
                </div>
            </div>
        `;
        element.draggable = true;
        element.classList.add('operator-draggable');
        element.classList.add('operator-with-preview');
        
        element.addEventListener('dragstart', (e) => {
            element.classList.add('dragging-shadow');
        });
        
        element.addEventListener('dragend', (e) => {
            element.classList.remove('dragging-shadow');
        });
    } else {
        // 【修改】分类节点 - 保留展开/收起按钮，添加自定义样式
        // 1. 获取现有的展开按钮（如果存在）
        const existingToggle = element.querySelector('.cv-treeview-toggle');
        const existingSpacer = element.querySelector('.cv-treeview-toggle-placeholder');
        
        // 2. 清空内容但保留展开按钮
        if (existingToggle) {
            // 保留展开按钮，在其后插入自定义内容
            const icon = node.customIcon || node.icon || '📁';
            const label = node.label;
            const count = node.children ? `(${node.children.length})` : '';
            
            // 构建内容：展开按钮 + 图标 + 名称 + 数量
            const contentHtml = `
                <span class="tree-node-icon">${icon}</span>
                <span class="tree-node-label">${label}</span>
                <span class="category-count">${count}</span>
            `;
            
            // 插入到展开按钮后面
            const wrapper = document.createElement('div');
            wrapper.className = 'category-content-wrapper';
            wrapper.innerHTML = contentHtml;
            
            element.appendChild(wrapper);
        } else {
            // 叶子节点（不应该出现）或异常情况，使用原始渲染
            element.innerHTML = `
                <span class="tree-node-icon">${node.customIcon || node.icon}</span>
                <span class="tree-node-label">${node.label}</span>
            `;
        }
    }
}
```

**Alternative Approach** (如果 TreeView 先渲染 renderNode 再添加 toggle):

TreeView 的渲染顺序是：先创建基础结构 → 调用 renderNode → 渲染子节点

renderNode 被调用时，toggle 按钮还没有被添加。所以上面的方法可能不适用。

需要改用另一种方式：**renderNode 返回完整的内容，包括展开按钮**。

```javascript
renderNode: (node, element) => {
    if (node.type === 'operator') {
        // 算子节点保持原有逻辑...
    } else {
        // 分类节点 - 手动构建包含展开按钮的完整结构
        const hasChildren = node.children && node.children.length > 0;
        const icon = node.customIcon || node.icon || '📁';
        const label = node.label;
        
        let html = '';
        
        if (hasChildren) {
            // 检查是否已展开
            const isExpanded = this.treeView.expandedNodes.has(node.id) || node.expanded;
            const arrowIcon = isExpanded ? '▼' : '▶';
            
            html += `
                <span class="cv-treeview-toggle ${isExpanded ? 'expanded' : 'collapsed'}"
                      data-node-id="${node.id}">
                    ${arrowIcon}
                </span>
            `;
        } else {
            html += '<span class="cv-treeview-toggle-placeholder"></span>';
        }
        
        html += `
            <span class="tree-node-icon category-icon">${icon}</span>
            <span class="tree-node-label category-label">${label}</span>
        `;
        
        element.innerHTML = html;
        
        // 绑定展开/收起事件
        if (hasChildren) {
            const toggle = element.querySelector('.cv-treeview-toggle');
            toggle.addEventListener('click', (e) => {
                e.stopPropagation();
                this.treeView.toggleNode(node);
            });
        }
    }
}
```

**Must NOT do**:
- 不要修改 TreeView.js（保持通用性）
- 不要破坏现有的拖拽功能
- 不要影响算子节点的渲染

**Acceptance Criteria**:
- [x] 分类节点显示展开/收起按钮
- [x] 点击按钮可以展开/收起该组
- [x] 点击分类标题也可以展开/收起
- [x] 算子节点保持原有拖拽功能
- [x] 底部"展开全部"/"折叠全部"按钮仍然有效

**Parallelization**:
- Can Run In Parallel: NO
- Blocks: TODO 2

**Commit**: YES
- Message: `feat(operator-library): add expand/collapse buttons to category groups`
- Files: `operatorLibrary.js`

---

#### TODO 2: 优化展开/收起按钮样式

**What to do**:
- 美化展开/收起按钮的样式
- 使用 SVG 箭头替代字符
- 添加旋转动画
- 添加算子数量徽章（收起时显示）

**Code Location**: `operator-library.css`

**Implementation Details**:

```css
/* 分类节点展开/收起按钮 */
.cv-treeview-node .cv-treeview-toggle {
    width: 20px;
    height: 20px;
    display: flex;
    align-items: center;
    justify-content: center;
    border-radius: 4px;
    color: var(--gold-accent);
    transition: all 0.2s ease;
    cursor: pointer;
    font-size: 12px;
}

.cv-treeview-node .cv-treeview-toggle:hover {
    background: rgba(212, 175, 55, 0.1);
    transform: scale(1.1);
}

.cv-treeview-node .cv-treeview-toggle.expanded {
    transform: rotate(0deg);
}

.cv-treeview-node .cv-treeview-toggle.collapsed {
    transform: rotate(-90deg);
}

/* 使用 SVG 箭头 */
.cv-treeview-node .cv-treeview-toggle::before {
    content: '';
    width: 0;
    height: 0;
    border-left: 5px solid transparent;
    border-right: 5px solid transparent;
    border-top: 6px solid currentColor;
    transition: transform 0.2s ease;
}

.cv-treeview-node .cv-treeview-toggle.collapsed::before {
    transform: rotate(-90deg);
}

/* 分类图标 */
.category-icon {
    color: var(--gold-accent);
    margin-right: 6px;
}

.category-icon svg {
    width: 18px;
    height: 18px;
    fill: currentColor;
}

/* 分类标签 */
.category-label {
    font-weight: 600;
    color: var(--paper-white);
    font-size: var(--font-size-sm);
}

/* 算子数量徽章 */
.category-count {
    margin-left: auto;
    padding: 2px 8px;
    background: rgba(212, 175, 55, 0.15);
    border: 1px solid rgba(212, 175, 55, 0.3);
    border-radius: var(--radius-full);
    font-size: 11px;
    color: var(--gold-accent);
    font-weight: 500;
}

/* 展开/收起动画 */
.cv-treeview-list {
    transition: all 0.3s ease;
    overflow: hidden;
}

/* 分类节点特殊样式 */
.cv-treeview-item[data-type="category"] > .cv-treeview-node {
    background: rgba(255, 255, 255, 0.02);
    border-left: 3px solid transparent;
    margin: 4px 0;
}

.cv-treeview-item[data-type="category"] > .cv-treeview-node:hover {
    background: rgba(231, 76, 60, 0.05);
    border-left-color: var(--cinnabar);
}
```

**Acceptance Criteria**:
- [x] 展开/收起按钮有美观的样式
- [x] 按钮有旋转动画效果
- [x] 收起时显示算子数量
- [x] 样式与整体主题一致

**Parallelization**:
- Can Run In Parallel: NO
- Blocked By: TODO 1

**Commit**: YES (可与 TODO 1 合并)
- Message: `style(operator-library): enhance expand/collapse button styling`
- Files: `operator-library.css`

---

#### TODO 3: 添加分类标题点击展开/收起

**What to do**:
- 让分类标题（图标+文字）也能点击展开/收起
- 提升用户体验，点击区域更大

**Code Location**: `operatorLibrary.js` renderNode 方法

**Implementation Details**:

在 renderNode 中为分类内容添加点击事件：

```javascript
renderNode: (node, element) => {
    // ... 前面的代码 ...
    
    if (node.type !== 'operator') {
        // 分类节点渲染完成后，绑定点击事件
        const contentWrapper = element.querySelector('.category-content-wrapper');
        if (contentWrapper && hasChildren) {
            contentWrapper.style.cursor = 'pointer';
            contentWrapper.addEventListener('click', (e) => {
                // 如果点击的是展开按钮，不重复处理
                if (e.target.closest('.cv-treeview-toggle')) {
                    return;
                }
                this.treeView.toggleNode(node);
            });
        }
    }
}
```

**Acceptance Criteria**:
- [x] 点击分类标题可以展开/收起
- [x] 点击展开按钮不会触发两次
- [x] 视觉上有可点击的提示（cursor: pointer）

**Parallelization**:
- Can Run In Parallel: NO
- Blocked By: TODO 1

**Commit**: NO (可与 TODO 1 合并)

---

#### TODO 4: 保存展开状态（可选增强）

**What to do**:
- 将展开状态保存到 localStorage
- 用户刷新页面后保持之前的展开状态

**Code Location**: `operatorLibrary.js`

**Implementation Details**:

```javascript
// 在 constructor 中
this.storageKey = 'operator-library-expanded-categories';

// 初始化时加载保存的状态
initialize() {
    // ... 现有代码 ...
    this.loadExpandedState();
}

// 保存展开状态
saveExpandedState() {
    const expandedIds = Array.from(this.treeView.expandedNodes);
    localStorage.setItem(this.storageKey, JSON.stringify(expandedIds));
}

// 加载展开状态
loadExpandedState() {
    try {
        const saved = localStorage.getItem(this.storageKey);
        if (saved) {
            const expandedIds = JSON.parse(saved);
            expandedIds.forEach(id => this.treeView.expandedNodes.add(id));
        }
    } catch (e) {
        console.warn('Failed to load expanded state:', e);
    }
}

// 在 bindActionEvents 中
bindActionEvents() {
    // ... 现有代码 ...
    
    // 监听展开/收起事件
    this.treeView.options.onExpand = () => this.saveExpandedState();
    this.treeView.options.onCollapse = () => this.saveExpandedState();
}
```

**Acceptance Criteria**:
- [x] 刷新页面后保持展开状态
- [x] 首次访问时默认全部展开

**Parallelization**:
- Can Run In Parallel: NO
- Blocked By: TODO 1

**Commit**: YES
- Message: `feat(operator-library): persist expanded state to localStorage`
- Files: `operatorLibrary.js`

---

## 技术细节补充

### 关于 TreeView 渲染顺序

TreeView 的渲染流程：
```
1. setData(data)
2. render()
3. createNodeList(root.children, 0)
4. for each node:
   a. createNodeItem(node, level)
   b. 创建基础结构（content div）
   c. 检查 hasChildren，准备添加 toggle 按钮
   d. 调用 renderNode(node, content) 【自定义渲染发生在这里】
   e. renderNode 返回后，如果 hasChildren 且没有 toggle，则添加 toggle
   f. 渲染子节点（如果是展开状态）
```

**关键点**: renderNode 被调用时，toggle 按钮还没有被添加。但 TreeView 会在 renderNode 之后检查是否需要添加 toggle。

如果 renderNode 完全覆盖了 innerHTML，TreeView 后续的 toggle 添加逻辑会失效。

**解决方案**: 
- 方案1: renderNode 不覆盖 innerHTML，而是修改现有结构
- 方案2: renderNode 手动创建完整的结构（包括 toggle）
- 方案3: 修改 TreeView 让 toggle 在 renderNode 之前添加

推荐 **方案2** 最可靠。

### CSS 变量参考

```css
:root {
    --gold-accent: #d4af37;      /* 金色 - 强调色 */
    --cinnabar: #e74c3c;         /* 朱红 - 主色 */
    --paper-white: #fdfbf7;      /* 纸白 - 主文字 */
    --paper-dim: #c9c5b8;        /* 暗纸 - 次要文字 */
    --ink-gray: #8b8680;         /* 墨灰 - 辅助文字 */
    --ink-secondary: #3a3530;    /* 次墨 - 背景 */
    --glass-border: rgba(255, 255, 255, 0.08); /* 玻璃边框 */
    --radius-md: 8px;
    --radius-full: 9999px;
    --font-size-sm: 13px;
    --duration-fast: 0.15s;
}
```

---

## 风险与缓解

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| 拖拽功能受影响 | 高 | 确保算子节点的 renderNode 逻辑不变 |
| TreeView 其他用途受影响 | 中 | 只修改 operatorLibrary 的 renderNode，不修改 TreeView 本身 |
| 样式冲突 | 低 | 使用特定类名，避免全局样式污染 |
| 状态不同步 | 中 | 正确绑定 toggle 事件，使用 treeView.toggleNode() API |

---

## 最终验证清单

- [x] 每个分类组都有展开/收起按钮
- [x] 点击按钮可以展开/收起
- [x] 点击分类标题也可以展开/收起
- [x] 展开/收起有平滑动画
- [x] 收起时显示算子数量徽章
- [x] 算子拖拽功能正常
- [x] 底部"展开全部"/"折叠全部"按钮有效
- [x] 展开状态可以保存（可选）
- [x] 样式美观，与主题一致

---

## 附录: 完整代码参考

### operatorLibrary.js - renderNode 完整修改

```javascript
renderNode: (node, element) => {
    if (node.type === 'operator') {
        // 算子节点 - 保持现有逻辑不变
        const operator = node.data;
        element.innerHTML = `
            <div class="operator-item-content">
                <span class="operator-drag-handle">⋮⋮</span>
                <span class="operator-icon">${node.customIcon || node.icon || '📦'}</span>
                <div class="operator-info">
                    <span class="operator-name">${node.label}</span>
                    <span class="operator-desc">${operator?.description || ''}</span>
                </div>
            </div>
        `;
        element.draggable = true;
        element.classList.add('operator-draggable');
        element.classList.add('operator-with-preview');
        
        element.addEventListener('dragstart', (e) => {
            element.classList.add('dragging-shadow');
        });
        
        element.addEventListener('dragend', (e) => {
            element.classList.remove('dragging-shadow');
        });
    } else {
        // 分类节点 - 自定义渲染包含展开按钮
        const hasChildren = node.children && node.children.length > 0;
        const isExpanded = this.treeView.expandedNodes.has(node.id) || node.expanded;
        const icon = node.customIcon || node.icon || '📁';
        const label = node.label;
        const count = node.children ? node.children.length : 0;
        
        // 构建完整内容
        let html = '';
        
        // 展开/收起按钮
        if (hasChildren) {
            const arrowIcon = isExpanded 
                ? '<svg viewBox="0 0 24 24" width="12" height="12"><path fill="currentColor" d="M7 10l5 5 5-5z"/></svg>'
                : '<svg viewBox="0 0 24 24" width="12" height="12"><path fill="currentColor" d="M8.59 16.59L13.17 12 8.59 7.41 10 6l6 6-6 6-1.41-1.41z"/></svg>';
            
            html += `
                <span class="cv-treeview-toggle ${isExpanded ? 'expanded' : 'collapsed'}"
                      data-node-id="${node.id}">
                    ${arrowIcon}
                </span>
            `;
        } else {
            html += '<span class="cv-treeview-toggle-placeholder"></span>';
        }
        
        // 分类内容包装器
        html += `
            <div class="category-content-wrapper">
                <span class="tree-node-icon category-icon">${icon}</span>
                <span class="tree-node-label category-label">${label}</span>
                ${!isExpanded && count > 0 ? `<span class="category-count">${count}</span>` : ''}
            </div>
        `;
        
        element.innerHTML = html;
        
        // 绑定展开/收起事件
        if (hasChildren) {
            const toggle = element.querySelector('.cv-treeview-toggle');
            const wrapper = element.querySelector('.category-content-wrapper');
            
            const toggleHandler = (e) => {
                e.stopPropagation();
                this.treeView.toggleNode(node);
            };
            
            toggle.addEventListener('click', toggleHandler);
            
            // 点击分类内容也可以展开/收起
            if (wrapper) {
                wrapper.style.cursor = 'pointer';
                wrapper.addEventListener('click', toggleHandler);
            }
        }
    }
}
```

---

**计划完成。执行 `/start-work` 开始实施。**
