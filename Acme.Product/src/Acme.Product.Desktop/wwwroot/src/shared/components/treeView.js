/**
 * 树形控件组件
 * 用于显示算子库等层级结构
 */

export class TreeView {
    constructor(containerId, options = {}) {
        this.container = typeof containerId === 'string' 
            ? document.getElementById(containerId) 
            : containerId;
        
        this.options = {
            selectable: true,
            multiSelect: false,
            showRoot: false,
            draggable: false,
            onSelect: null,
            onExpand: null,
            onCollapse: null,
            onDoubleClick: null,
            renderNode: null,
            ...options
        };

        this.root = null;
        this.selectedNodes = new Set();
        this.expandedNodes = new Set();
        
        this.initialize();
    }

    initialize() {
        this.container.className = 'cv-treeview';
        this.container.innerHTML = '';
    }

    /**
     * 设置树数据
     * @param {Array} data - 树节点数据
     */
    setData(data) {
        this.root = {
            id: 'root',
            children: data || [],
            expanded: true
        };
        this.render();
    }

    /**
     * 渲染树
     */
    render() {
        this.container.innerHTML = '';
        if (this.root && this.root.children) {
            const ul = this.createNodeList(this.root.children, 0);
            this.container.appendChild(ul);
        }
    }

    /**
     * 创建节点列表
     */
    createNodeList(nodes, level) {
        const ul = document.createElement('ul');
        ul.className = 'cv-treeview-list';
        ul.style.paddingLeft = level > 0 ? '20px' : '0';

        nodes.forEach(node => {
            const li = this.createNodeItem(node, level);
            ul.appendChild(li);
        });

        return ul;
    }

    /**
     * 创建节点项
     */
    createNodeItem(node, level) {
        const li = document.createElement('li');
        li.className = 'cv-treeview-item';
        li.dataset.id = node.id;

        const hasChildren = node.children && node.children.length > 0;
        const isExpanded = this.expandedNodes.has(node.id) || node.expanded;

        // 节点内容容器
        const content = document.createElement('div');
        content.className = 'cv-treeview-node';
        if (this.selectedNodes.has(node.id)) {
            content.classList.add('selected');
        }

        // 展开/折叠图标
        if (hasChildren) {
            const toggle = document.createElement('span');
            toggle.className = `cv-treeview-toggle ${isExpanded ? 'expanded' : 'collapsed'}`;
            toggle.textContent = isExpanded ? '▼' : '▶';
            toggle.onclick = (e) => {
                e.stopPropagation();
                this.toggleNode(node);
            };
            content.appendChild(toggle);
        } else {
            const spacer = document.createElement('span');
            spacer.className = 'cv-treeview-toggle-placeholder';
            content.appendChild(spacer);
        }

        // 节点图标
        if (node.icon) {
            const icon = document.createElement('span');
            icon.className = 'cv-treeview-icon';
            icon.textContent = node.icon;
            content.appendChild(icon);
        }

        // 节点文本
        const text = document.createElement('span');
        text.className = 'cv-treeview-text';
        text.textContent = node.label || node.name || 'Unnamed';
        content.appendChild(text);

        // 自定义渲染
        if (this.options.renderNode) {
            const customContent = this.options.renderNode(node, content);
            // 只有当返回了不同的元素时才替换，防止循环引用
            if (customContent && customContent !== content) {
                content.innerHTML = '';
                content.appendChild(customContent);
            }
        }

        li.appendChild(content);

        // 子节点列表
        if (hasChildren && isExpanded) {
            const childrenList = this.createNodeList(node.children, level + 1);
            li.appendChild(childrenList);
        }

        // 事件绑定
        content.onclick = (e) => {
            e.stopPropagation();
            // 【修复】如果点击的是展开/收起按钮或分类节点，不触发选择
            if (e.target.closest('.cv-treeview-toggle') || e.target.closest('.category-content-wrapper')) {
                return;
            }
            this.selectNode(node, !this.options.multiSelect || !e.ctrlKey);
        };

        if (this.options.onDoubleClick) {
            content.ondblclick = (e) => {
                e.stopPropagation();
                this.options.onDoubleClick(node);
            };
        }

        // 拖拽支持
        if (this.options.draggable) {
            content.draggable = true;
            content.ondragstart = (e) => {
                e.dataTransfer.setData('application/json', JSON.stringify(node));
                e.dataTransfer.effectAllowed = 'copy';
                content.classList.add('dragging');
            };
            content.ondragend = () => {
                content.classList.remove('dragging');
            };
        }

        return li;
    }

    /**
     * 切换节点展开/折叠
     */
    toggleNode(node) {
        if (this.expandedNodes.has(node.id)) {
            this.expandedNodes.delete(node.id);
            node.expanded = false; // 【修复】同步更新 node.expanded 属性
            if (this.options.onCollapse) {
                this.options.onCollapse(node);
            }
        } else {
            this.expandedNodes.add(node.id);
            node.expanded = true; // 【修复】同步更新 node.expanded 属性
            if (this.options.onExpand) {
                this.options.onExpand(node);
            }
        }
        this.render();
    }

    /**
     * 选择节点
     */
    selectNode(node, clearOthers = true) {
        if (clearOthers) {
            this.selectedNodes.clear();
        }

        if (this.options.selectable) {
            if (this.selectedNodes.has(node.id)) {
                this.selectedNodes.delete(node.id);
            } else {
                this.selectedNodes.add(node.id);
            }
        }

        if (this.options.onSelect) {
            this.options.onSelect(node, Array.from(this.selectedNodes));
        }

        this.render();
    }

    /**
     * 获取选中的节点
     */
    getSelectedNodes() {
        return Array.from(this.selectedNodes).map(id => this.findNode(id));
    }

    /**
     * 查找节点
     */
    findNode(id, nodes = this.root?.children) {
        if (!nodes) return null;

        for (const node of nodes) {
            if (node.id === id) return node;
            if (node.children) {
                const found = this.findNode(id, node.children);
                if (found) return found;
            }
        }
        return null;
    }

    /**
     * 展开所有节点
     */
    expandAll() {
        const collectIds = (nodes) => {
            nodes.forEach(node => {
                if (node.children && node.children.length > 0) {
                    this.expandedNodes.add(node.id);
                    collectIds(node.children);
                }
            });
        };
        
        if (this.root?.children) {
            collectIds(this.root.children);
        }
        this.render();
    }

    /**
     * 折叠所有节点
     */
    collapseAll() {
        this.expandedNodes.clear();
        this.render();
    }

    /**
     * 添加节点
     */
    addNode(parentId, node) {
        const parent = this.findNode(parentId);
        if (parent) {
            if (!parent.children) {
                parent.children = [];
            }
            parent.children.push(node);
            this.expandedNodes.add(parentId);
            this.render();
            return true;
        }
        return false;
    }

    /**
     * 移除节点
     */
    removeNode(id) {
        const removeFromList = (nodes, targetId) => {
            const index = nodes.findIndex(n => n.id === targetId);
            if (index >= 0) {
                nodes.splice(index, 1);
                return true;
            }
            for (const node of nodes) {
                if (node.children && removeFromList(node.children, targetId)) {
                    return true;
                }
            }
            return false;
        };

        if (this.root?.children) {
            const removed = removeFromList(this.root.children, id);
            if (removed) {
                this.selectedNodes.delete(id);
                this.expandedNodes.delete(id);
                this.render();
            }
            return removed;
        }
        return false;
    }
}

export default TreeView;
