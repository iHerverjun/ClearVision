/**
 * 端口类型颜色映射表 (模块级常量，提高兼容性)
 */
const PORT_TYPE_COLORS = {
    'Image':           '#52c41a',  // 绿色 - 图像
    'String':          '#1890ff',  // 蓝色 - 字符串
    'Integer':         '#fa8c16',  // 橙色 - 整数
    'Float':           '#fa8c16',  // 橙色 - 浮点
    'Boolean':         '#f5222d',  // 红色 - 布尔值
    'Point':           '#eb2f96',  // 粉色 - 坐标
    'Rectangle':       '#eb2f96',  // 粉色 - 矩形
    'Contour':         '#722ed1',  // 紫色 - 轮廓/区域
    'PointList':       '#eb2f96',  // 粉色 - 点列表 (Sprint 1.2)
    'DetectionResult': '#13c2c2',  // 青色 - 检测结果 (Sprint 1.2)
    'DetectionList':   '#13c2c2',  // 青色 - 检测列表 (Sprint 1.2)
    'CircleData':      '#2f54eb',  // 靛蓝 - 圆数据 (Sprint 1.2)
    'LineData':        '#2f54eb',  // 靛蓝 - 直线数据 (Sprint 1.2)
    'Any':             '#bfbfbf',  // 灰色 - 任意
    // 兼容枚举数字值
    0: '#52c41a', 
    1: '#fa8c16', 2: '#fa8c16', 3: '#f5222d',
    4: '#1890ff', 5: '#eb2f96', 6: '#eb2f96', 7: '#722ed1',
    8: '#eb2f96', 9: '#13c2c2', 10: '#13c2c2', 11: '#2f54eb', 12: '#2f54eb',
    99: '#bfbfbf'
};

/**
 * 通信类算子类型集合 - Sprint 4 Task 4.3 安全提示
 */
const COMM_OPERATOR_TYPES = new Set([
    'HttpRequest', 'MqttPublish', 'ModbusCommunication',
    'OmronFinsCommunication', 'MitsubishiMcCommunication',
    'TcpCommunication', 'SerialCommunication', 'DatabaseWrite'
]);


class FlowCanvas {

    constructor(canvasId) {
        this.canvas = document.getElementById(canvasId);
        this.ctx = this.canvas.getContext('2d');
        this.nodes = new Map();
        this.connections = [];
        this.selectedNode = null;
        this.draggedNode = null;
        this.dragOffset = { x: 0, y: 0 };
        this.scale = 1;
        this.offset = { x: 0, y: 0 };

        // 网格设置
        this.gridSize = 20;
        this.gridColor = 'rgba(0, 0, 0, 0.08)'; // 浅色网格 (适配浅色背景)
        
        // 事件回调
        this.onNodeSelected = null;
        this.onConnectionCreated = null;

        // 连线状态管理
        this.isConnecting = false;
        this.connectingFrom = null;  // { nodeId, portIndex, isOutput }
        this.mousePosition = { x: 0, y: 0 };
        this.hoveredPort = null;  // { nodeId, portIndex, isOutput }

        // 事件处理器引用（用于销毁时移除）
        this._resizeHandler = this.resize.bind(this);
        this._mouseDownHandler = this.handleMouseDown.bind(this);
        this._mouseMoveHandler = this.handleMouseMove.bind(this);
        this._mouseUpHandler = this.handleMouseUp.bind(this);
        this._wheelHandler = this.handleWheel.bind(this);
        this._contextMenuHandler = this.handleContextMenu.bind(this);
        this._keyDownHandler = this.handleKeyDown.bind(this);
        this._dblClickHandler = this.handleDoubleClick.bind(this);
        // 【修复】页面可见性变化处理器
        this._visibilityHandler = this.handleVisibilityChange.bind(this);

        // 动画帧ID
        this._animationFrameId = null;
        
        // 【修复】渲染暂停标志
        this._isPaused = false;

        // 【修复】ResizeObserver
        this._resizeObserver = null;


        // 选中的连接
        this.selectedConnection = null;

        // 右键菜单
        this.contextMenu = null;
        this._clickOutsideHandler = this.hideContextMenu.bind(this);

        this.initialize();
    }

    /**
     * 初始化画布
     */
    initialize() {
        this.resize();
        // 移除 window.resize，改用 ResizeObserver
        // window.addEventListener('resize', this._resizeHandler);
        
        // 【修复】使用 ResizeObserver
        if (window.ResizeObserver) {
            this._resizeObserver = new ResizeObserver(entries => {
                for (let entry of entries) {
                    // 只有当尺寸确实变化且均大于0时才调整
                    if (entry.contentRect.width > 0 && entry.contentRect.height > 0) {
                        this.resize();
                    }
                }
            });
            this._resizeObserver.observe(this.canvas.parentElement);
        } else {
            // 降级方案
            window.addEventListener('resize', this._resizeHandler);
        }


        // 绑定事件
        this.canvas.addEventListener('mousedown', this._mouseDownHandler);
        this.canvas.addEventListener('mousemove', this._mouseMoveHandler);
        this.canvas.addEventListener('mouseup', this._mouseUpHandler);
        this.canvas.addEventListener('wheel', this._wheelHandler);
        this.canvas.addEventListener('contextmenu', this._contextMenuHandler);
        this.canvas.addEventListener('dblclick', this._dblClickHandler);
        window.addEventListener('keydown', this._keyDownHandler);

        // 【修复】监听页面可见性变化，后台时暂停渲染
        document.addEventListener('visibilitychange', this._visibilityHandler);

        // 开始渲染循环
        this.render();

        // 初始化小地图
        this.initMinimap();
    }

    /**
     * 【修复】处理页面可见性变化
     */
    handleVisibilityChange() {
        if (document.hidden) {
            // 页面隐藏时暂停渲染
            this._isPaused = true;
            console.log('[FlowCanvas] 页面进入后台，暂停渲染');
        } else {
            // 页面显示时恢复渲染
            this._isPaused = false;
            console.log('[FlowCanvas] 页面回到前台，恢复渲染');
            // 触发一次渲染
            if (!this._animationFrameId) {
                this.render();
            }
        }
    }

    /**
     * 销毁画布，清理所有事件监听和动画循环
     */
    destroy() {
        // 停止渲染循环
        if (this._animationFrameId) {
            cancelAnimationFrame(this._animationFrameId);
            this._animationFrameId = null;
        }

        // 移除窗口事件监听
        if (!this._resizeObserver) {
            window.removeEventListener('resize', this._resizeHandler);
        } else {
            this._resizeObserver.disconnect();
            this._resizeObserver = null;
        }

        window.removeEventListener('keydown', this._keyDownHandler);
        
        // 【修复】移除页面可见性监听
        document.removeEventListener('visibilitychange', this._visibilityHandler);

        // 移除画布事件监听
        this.canvas.removeEventListener('mousedown', this._mouseDownHandler);
        this.canvas.removeEventListener('mousemove', this._mouseMoveHandler);
        this.canvas.removeEventListener('mouseup', this._mouseUpHandler);
        this.canvas.removeEventListener('wheel', this._wheelHandler);
        this.canvas.removeEventListener('contextmenu', this._contextMenuHandler);
        this.canvas.removeEventListener('dblclick', this._dblClickHandler);

        // 清理资源
        this.nodes.clear();
        this.connections = [];
        this.selectedNode = null;
        this.draggedNode = null;
        this.selectedConnection = null;
        
        // 清理小地图
        if (this.minimap) {
            this.minimap.remove();
            this.minimap = null;
        }
        
        // 清理右键菜单
        this.hideContextMenu();
    }

    /**
     * 调整画布大小
     */
    resize() {
        const container = this.canvas.parentElement;
        this.canvas.width = container.clientWidth;
        this.canvas.height = container.clientHeight;
        this.render();
    }

    /**
     * 生成UUID
     */
    generateUUID() {
        if (typeof crypto !== 'undefined' && crypto.randomUUID) {
            return crypto.randomUUID();
        }
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
            var r = Math.random() * 16 | 0, v = c == 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }

    /**
     * 添加节点
     */
    addNode(type, x, y, config = {}) {
        const node = {
            id: this.generateUUID(),
            type,
            x,
            y,
            width: 140,
            height: 60,
            title: config.title || type,
            inputs: (config.inputs || []).map(p => ({
                id: p.id || this.generateUUID(),
                name: p.name,
                type: p.type
            })),
            outputs: (config.outputs || []).map(p => ({
                id: p.id || this.generateUUID(),
                name: p.name,
                type: p.type
            })),
            color: config.color || '#1890ff',
            ...config
        };
        
        this.nodes.set(node.id, node);
        this.render();
        return node;
    }

    /**
     * 删除节点
     */
    removeNode(nodeId) {
        // 删除相关连接
        this.connections = this.connections.filter(
            conn => conn.source !== nodeId && conn.target !== nodeId
        );
        
        this.nodes.delete(nodeId);
        if (this.selectedNode === nodeId) {
            this.selectedNode = null;
        }
        this.render();
    }

    /**
     * 添加连接
     */
    addConnection(sourceId, sourcePort, targetId, targetPort) {
        const connection = {
            id: this.generateUUID(),
            source: sourceId,
            sourcePort,
            target: targetId,
            targetPort
        };
        
        this.connections.push(connection);
        this.render();
        return connection;
    }

    /**
     * 绘制网格
     */
    drawGrid() {
        const { width, height } = this.canvas;
        
        this.ctx.strokeStyle = this.gridColor;
        this.ctx.lineWidth = 1;
        
        // 计算偏移后的起始位置
        const startX = Math.floor(this.offset.x / this.gridSize) * this.gridSize;
        const startY = Math.floor(this.offset.y / this.gridSize) * this.gridSize;
        
        // 计算当前缩放下的可视区域宽度
        // 之前只考虑 offset.x 没除以 scale，导致缩小时网格绘制范围不足
        const visibleWidth = width / this.scale;
        const visibleHeight = height / this.scale;
        
        this.ctx.beginPath();
        
        // 垂直线 (渲染范围需覆盖 offset + visibleWidth)
        for (let x = startX; x < this.offset.x + visibleWidth; x += this.gridSize) {
            const screenX = (x - this.offset.x) * this.scale;
            this.ctx.moveTo(screenX, 0);
            this.ctx.lineTo(screenX, height);
        }
        
        // 水平线
        for (let y = startY; y < this.offset.y + visibleHeight; y += this.gridSize) {
            const screenY = (y - this.offset.y) * this.scale;
            this.ctx.moveTo(0, screenY);
            this.ctx.lineTo(width, screenY);
        }
        
        this.ctx.stroke();
    }

    /**
     * 绘制节点 - 阶段四增强版
     * 渐变填充 + 图标 + 状态发光效果
     */
    drawNode(node) {
        const x = (node.x - this.offset.x) * this.scale;
        const y = (node.y - this.offset.y) * this.scale;
        const w = node.width * this.scale;
        const h = node.height * this.scale;
        const isSelected = this.selectedNode === node.id;

        // 根据状态调整边框颜色和发光效果
        let borderColor = isSelected ? node.color : 'rgba(255, 255, 255, 0.1)';
        let borderWidth = isSelected ? 3 : 1;
        let glowColor = null;

        // === Sprint 4 Task 4.3: 安全提示层 ===
        const isCommunicationOp = COMM_OPERATOR_TYPES.has(node.type);
        const hasFileParam = node.parameters && node.parameters.some(
            p => p.dataType === 'file' && p.value
        );

        if (node.status === 'running') {
            borderColor = '#3498db';
            borderWidth = 3;
            glowColor = 'rgba(52, 152, 219, 0.6)';
        } else if (node.status === 'success') {
            borderColor = '#2ecc71';
            glowColor = 'rgba(46, 204, 113, 0.5)';
        } else if (node.status === 'error') {
            borderColor = '#e74c3c';
            glowColor = 'rgba(231, 76, 60, 0.5)';
        } else if (isCommunicationOp) {
            // 通信算子：红色警戒边框
            borderColor = '#f5222d';
            borderWidth = 2;
            glowColor = 'rgba(245, 34, 45, 0.3)';
        } else if (hasFileParam) {
            // 含 file 参数的算子：橙色提示边框
            borderColor = '#fa8c16';
            borderWidth = 2;
        } else if (isSelected) {
            glowColor = `${node.color}80`; // 50% opacity
        }

        // 状态发光效果
        if (glowColor) {
            this.ctx.shadowColor = glowColor;
            this.ctx.shadowBlur = 15;
            this.ctx.shadowOffsetX = 0;
            this.ctx.shadowOffsetY = 0;
        } else {
            this.ctx.shadowColor = 'rgba(0, 0, 0, 0.3)';
            this.ctx.shadowBlur = 8;
            this.ctx.shadowOffsetX = 2;
            this.ctx.shadowOffsetY = 2;
        }

        // 节点背景 - 渐变填充
        const gradient = this.ctx.createLinearGradient(x, y, x, y + h);
        gradient.addColorStop(0, isSelected ? 'rgba(45, 74, 94, 0.9)' : 'rgba(26, 58, 82, 0.8)');
        gradient.addColorStop(1, isSelected ? 'rgba(26, 58, 82, 0.95)' : 'rgba(13, 27, 42, 0.9)');
        
        this.ctx.fillStyle = gradient;
        this.ctx.strokeStyle = borderColor;
        this.ctx.lineWidth = borderWidth;

        // 绘制圆角矩形
        this.roundRect(x, y, w, h, 8);
        this.ctx.fill();
        this.ctx.stroke();

        // 重置阴影
        this.ctx.shadowColor = 'transparent';

        // 标题栏 - 渐变
        const headerGradient = this.ctx.createLinearGradient(x, y, x + w, y);
        headerGradient.addColorStop(0, node.color);
        headerGradient.addColorStop(1, this.adjustColor(node.color, -20));
        this.ctx.fillStyle = headerGradient;
        this.roundRect(x, y, w, 24 * this.scale, { tl: 8, tr: 8, bl: 0, br: 0 });
        this.ctx.fill();

        // 图标
        if (node.iconPath) {
            const targetSize = 16 * this.scale;
            const scaleFactor = targetSize / 24; // ViewBox 24x24
            
            this.ctx.save();
            this.ctx.translate(x + 8 * this.scale, y + 4 * this.scale);
            this.ctx.scale(scaleFactor, scaleFactor);
            this.ctx.fillStyle = '#ffffff'; // 图标永远白色
            const path = new Path2D(node.iconPath);
            this.ctx.fill(path);
            this.ctx.restore();
        } else if (node.icon) {
            this.ctx.fillStyle = '#ffffff';
            this.ctx.font = `${14 * this.scale}px sans-serif`;
            this.ctx.textAlign = 'left';
            this.ctx.textBaseline = 'middle';
            this.ctx.fillText(node.icon, x + 8 * this.scale, y + 12 * this.scale);
        }

        // 标题文字
        this.ctx.fillStyle = '#ffffff';
        this.ctx.font = `bold ${11 * this.scale}px sans-serif`;
        this.ctx.textAlign = 'left';
        this.ctx.textBaseline = 'middle';
        const titleX = (node.icon || node.iconPath) ? x + 28 * this.scale : x + 10 * this.scale;
        this.ctx.fillText(node.title, titleX, y + 12 * this.scale);

        // 绘制状态指示器
        if (node.status) {
            const indicatorY = y + 40 * this.scale;
            this.drawStatusIndicator(x + w - 12 * this.scale, indicatorY, node.status);
        }

        // 绘制端口
        this.drawPorts(node, x, y, w, h);

        // === Sprint 4 Task 4.3: 绘制安全标记 ===
        if (isCommunicationOp) {
            // 通信算子：右上角绘制 ⚠ 图标
            this.ctx.fillStyle = '#f5222d';
            this.ctx.font = `bold ${14 * this.scale}px sans-serif`;
            this.ctx.textAlign = 'right';
            this.ctx.textBaseline = 'top';
            this.ctx.fillText('⚠', x + w - 4 * this.scale, y + 2 * this.scale);
        }
    }

    /**
     * 调整颜色亮度
     */
    adjustColor(color, amount) {
        const hex = color.replace('#', '');
        const r = Math.max(0, Math.min(255, parseInt(hex.substr(0, 2), 16) + amount));
        const g = Math.max(0, Math.min(255, parseInt(hex.substr(2, 2), 16) + amount));
        const b = Math.max(0, Math.min(255, parseInt(hex.substr(4, 2), 16) + amount));
        return `rgb(${r}, ${g}, ${b})`;
    }

    /**
     * 绘制状态指示器
     * @param {number} x - 中心X坐标
     * @param {number} y - 中心Y坐标
     * @param {string} status - 状态
     */
    drawStatusIndicator(x, y, status) {
        const radius = 6 * this.scale;

        this.ctx.beginPath();
        this.ctx.arc(x, y, radius, 0, Math.PI * 2);

        switch (status) {
            case 'running':
                this.ctx.fillStyle = '#1890ff';
                this.ctx.fill();
                // 绘制旋转的进度环
                this.ctx.beginPath();
                this.ctx.arc(x, y, radius + 2 * this.scale, 0, Math.PI * 2);
                this.ctx.strokeStyle = 'rgba(24, 144, 255, 0.5)';
                this.ctx.lineWidth = 2 * this.scale;
                this.ctx.stroke();
                break;
            case 'success':
                this.ctx.fillStyle = '#52c41a';
                this.ctx.fill();
                // 绘制对勾
                this.ctx.strokeStyle = '#ffffff';
                this.ctx.lineWidth = 2 * this.scale;
                this.ctx.beginPath();
                this.ctx.moveTo(x - 3 * this.scale, y);
                this.ctx.lineTo(x - 1 * this.scale, y + 2 * this.scale);
                this.ctx.lineTo(x + 3 * this.scale, y - 2 * this.scale);
                this.ctx.stroke();
                break;
            case 'error':
                this.ctx.fillStyle = '#f5222d';
                this.ctx.fill();
                // 绘制X
                this.ctx.strokeStyle = '#ffffff';
                this.ctx.lineWidth = 2 * this.scale;
                this.ctx.beginPath();
                this.ctx.moveTo(x - 2 * this.scale, y - 2 * this.scale);
                this.ctx.lineTo(x + 2 * this.scale, y + 2 * this.scale);
                this.ctx.moveTo(x + 2 * this.scale, y - 2 * this.scale);
                this.ctx.lineTo(x - 2 * this.scale, y + 2 * this.scale);
                this.ctx.stroke();
                break;
        }
    }

    /**
     * 绘制端口
     */
    drawPorts(node, x, y, w, h) {
        const portRadius = 5 * this.scale;
        
        // 渲染输入端口 - 垂直均分
        node.inputs.forEach((input, index) => {
            const portY = y + (h * (index + 1)) / (node.inputs.length + 1);
            const color = PORT_TYPE_COLORS[input.type] || PORT_TYPE_COLORS['Any'];
            
            this.ctx.beginPath();
            this.ctx.arc(x, portY, portRadius, 0, Math.PI * 2);
            this.ctx.fillStyle = color;
            this.ctx.fill();
            this.ctx.strokeStyle = '#ffffff';
            this.ctx.lineWidth = 1;
            this.ctx.stroke();

            // 绘制类型名 (靠近端口的小标签)
            if (this.scale > 0.8) {
                this.ctx.fillStyle = 'rgba(255, 255, 255, 0.5)';
                this.ctx.font = `${8 * this.scale}px sans-serif`;
                this.ctx.textAlign = 'left';
                const typeName = typeof input.type === 'string' ? input.type : 'Any';
                this.ctx.fillText(input.name || typeName, x + 8 * this.scale, portY + 3 * this.scale);
            }
        });
        
        // 渲染输出端口 - 垂直均分
        node.outputs.forEach((output, index) => {
            const portY = y + (h * (index + 1)) / (node.outputs.length + 1);
            const color = PORT_TYPE_COLORS[output.type] || PORT_TYPE_COLORS['Any'];

            this.ctx.beginPath();
            this.ctx.arc(x + w, portY, portRadius, 0, Math.PI * 2);
            this.ctx.fillStyle = color;
            this.ctx.fill();
            this.ctx.strokeStyle = '#ffffff';
            this.ctx.lineWidth = 1;
            this.ctx.stroke();

            // 绘制类型名
            if (this.scale > 0.8) {
                this.ctx.fillStyle = 'rgba(255, 255, 255, 0.5)';
                this.ctx.font = `${8 * this.scale}px sans-serif`;
                this.ctx.textAlign = 'right';
                const typeName = typeof output.type === 'string' ? output.type : 'Any';
                this.ctx.fillText(output.name || typeName, x + w - 8 * this.scale, portY + 3 * this.scale);
            }
        });
    }

    /**
     * 获取端口在屏幕上的坐标
     * @param {string} nodeId - 节点ID
     * @param {number} portIndex - 端口索引
     * @param {boolean} isOutput - 是否是输出端口
     * @returns {{x: number, y: number}} 端口坐标
     */
    getPortPosition(nodeId, portIndex, isOutput) {
        const node = this.nodes.get(nodeId);
        if (!node) return null;

        const x = (node.x - this.offset.x) * this.scale;
        const y = (node.y - this.offset.y) * this.scale;
        const w = node.width * this.scale;
        const h = node.height * this.scale;

        const portsCount = isOutput ? node.outputs.length : node.inputs.length;
        const portY = y + (h * (portIndex + 1)) / (portsCount + 1);

        if (isOutput) {
            return { x: x + w, y: portY };
        } else {
            return { x: x, y: portY };
        }
    }

    /**
     * 检测鼠标位置是否在端口上
     * @param {number} x - 鼠标X坐标（世界坐标）
     * @param {number} y - 鼠标Y坐标（世界坐标）
     * @returns {{nodeId: string, portIndex: number, isOutput: boolean}|null}
     */
    getPortAt(x, y) {
        const screenX = (x - this.offset.x) * this.scale;
        const screenY = (y - this.offset.y) * this.scale;
        const hitRadius = 15 * this.scale; // 点击检测半径

        for (const [nodeId, node] of this.nodes) {
            const nodeScreenX = (node.x - this.offset.x) * this.scale;
            const nodeScreenY = (node.y - this.offset.y) * this.scale;
            const w = node.width * this.scale;
            const h = node.height * this.scale;

            // 检测输入端口 (垂直分布)
            for (let i = 0; i < node.inputs.length; i++) {
                const portY = nodeScreenY + (h * (i + 1)) / (node.inputs.length + 1);
                const dx = screenX - nodeScreenX;
                const dy = screenY - portY;
                if (Math.sqrt(dx * dx + dy * dy) < hitRadius) {
                    return { nodeId, portIndex: i, isOutput: false };
                }
            }

            // 检测输出端口 (垂直分布)
            for (let i = 0; i < node.outputs.length; i++) {
                const portY = nodeScreenY + (h * (i + 1)) / (node.outputs.length + 1);
                const dx = screenX - (nodeScreenX + w);
                const dy = screenY - portY;
                if (Math.sqrt(dx * dx + dy * dy) < hitRadius) {
                    return { nodeId, portIndex: i, isOutput: true };
                }
            }
        }

        return null;
    }

    /**
     * 获取指定端口上的连接
     * @param {string} nodeId - 节点ID
     * @param {number} portIndex - 端口索引
     * @param {boolean} isOutput - 是否是输出端口
     * @returns {Object|null} 连接对象或null
     */
    getConnectionAtPort(nodeId, portIndex, isOutput) {
        if (isOutput) {
            // 输出端口可能有多个连接，返回第一个
            return this.connections.find(conn => 
                conn.source === nodeId && conn.sourcePort === portIndex
            ) || null;
        } else {
            // 输入端口只能有一个连接
            return this.connections.find(conn => 
                conn.target === nodeId && conn.targetPort === portIndex
            ) || null;
        }
    }

    /**
     * 获取指定端口上的所有连接（用于输出端口）
     * @param {string} nodeId - 节点ID
     * @param {number} portIndex - 端口索引
     * @param {boolean} isOutput - 是否是输出端口
     * @returns {Array} 连接对象数组
     */
    getConnectionsAtPort(nodeId, portIndex, isOutput) {
        if (isOutput) {
            return this.connections.filter(conn => 
                conn.source === nodeId && conn.sourcePort === portIndex
            );
        } else {
            const conn = this.connections.find(conn => 
                conn.target === nodeId && conn.targetPort === portIndex
            );
            return conn ? [conn] : [];
        }
    }

    /**
     * 开始创建连接
     * @param {string} nodeId - 起始节点ID
     * @param {number} portIndex - 起始端口索引
     */
    startConnection(nodeId, portIndex) {
        this.isConnecting = true;
        this.connectingFrom = { nodeId, portIndex, isOutput: true };
        this.canvas.style.cursor = 'crosshair';
        console.log('[FlowCanvas] 开始连线，从节点:', nodeId, '端口:', portIndex);
    }

    /**
     * 完成连接创建
     * @param {string} nodeId - 目标节点ID
     * @param {number} portIndex - 目标端口索引
     */
    finishConnection(nodeId, portIndex) {
        if (!this.isConnecting || !this.connectingFrom) return;

        // 检查类型兼容性
        const sourceNode = this.nodes.get(this.connectingFrom.nodeId);
        const targetNode = this.nodes.get(nodeId);
        
        if (!sourceNode || !targetNode || !sourceNode.outputs[this.connectingFrom.portIndex]) {
            this.cancelConnection();
            return;
        }

        const sourcePort = sourceNode.outputs[this.connectingFrom.portIndex];
        const targetPort = targetNode.inputs[portIndex];

        if (!this.checkTypeCompatibility(sourcePort.type, targetPort.type)) {
            console.warn(`[FlowCanvas] 类型不匹配: ${sourcePort.type} -> ${targetPort.type}`);
            if (window.showToast) window.showToast(`类型不匹配: ${sourcePort.type} -> ${targetPort.type}`, 'warning');
            this.cancelConnection();
            return;
        }

        // 检查连接有效性
        if (this.connectingFrom.nodeId === nodeId) {
            console.warn('[FlowCanvas] 不能连接到自己');
            this.cancelConnection();
            return;
        }

        // 检查是否已存在相同连接
        const existingConn = this.connections.find(conn =>
            conn.source === this.connectingFrom.nodeId &&
            conn.sourcePort === this.connectingFrom.portIndex &&
            conn.target === nodeId &&
            conn.targetPort === portIndex
        );

        if (existingConn) {
            console.warn('[FlowCanvas] 连接已存在');
            this.cancelConnection();
            return;
        }

        // 检查输入端口是否已被占用（一个输入端口只能有一个连接）
        const targetPortOccupied = this.connections.find(conn =>
            conn.target === nodeId &&
            conn.targetPort === portIndex
        );

        if (targetPortOccupied) {
            console.warn('[FlowCanvas] 目标输入端口已被占用');
            this.cancelConnection();
            return;
        }

        // 创建连接
        const connection = this.addConnection(
            this.connectingFrom.nodeId,
            this.connectingFrom.portIndex,
            nodeId,
            portIndex
        );

        console.log('[FlowCanvas] 连接已创建:', connection);

        // 触发回调
        if (this.onConnectionCreated) {
            this.onConnectionCreated(connection);
        }

        this.cancelConnection();
    }

    /**
     * 取消当前连接操作
     */
    cancelConnection() {
        this.isConnecting = false;
        this.connectingFrom = null;
        this.canvas.style.cursor = 'default';
        this.render(); // 刷新以清除高亮
    }

    /**
     * 检查类型兼容性
     */
    checkTypeCompatibility(sourceType, targetType) {
        // 枚举转换映射 (兼容数字和字符串)
        const normalize = (t) => {
            if (t === 'Any' || t === 99) return 'Any';
            if (t === 'Image' || t === 0) return 'Image';
            if (t === 'Integer' || t === 1 || t === 'Float' || t === 2) return 'Number';
            if (t === 'Boolean' || t === 3) return 'Boolean';
            if (t === 'String' || t === 4) return 'String';
            if (t === 'Point' || t === 5 || t === 'Rectangle' || t === 6 || t === 'PointList' || t === 8) return 'Geometry';
            if (t === 'Contour' || t === 7) return 'Contour';
            if (t === 'DetectionResult' || t === 9 || t === 'DetectionList' || t === 10) return 'Detection';
            if (t === 'CircleData' || t === 11) return 'CircleData';
            if (t === 'LineData' || t === 12) return 'LineData';
            return t;
        };

        const s = normalize(sourceType);
        const t = normalize(targetType);

        if (s === 'Any' || t === 'Any') return true;
        return s === t;
    }

    /**
     * 连线时高亮兼容的端口
     */
    highlightCompatiblePorts() {
        if (!this.isConnecting || !this.connectingFrom) return;
        
        const sourceNode = this.nodes.get(this.connectingFrom.nodeId);
        if (!sourceNode) return;
        
        const sourcePort = this.connectingFrom.isOutput ? 
            sourceNode.outputs[this.connectingFrom.portIndex] : 
            sourceNode.inputs[this.connectingFrom.portIndex];
        
        if (!sourcePort) return;

        for (const [nodeId, node] of this.nodes) {
            if (nodeId === this.connectingFrom.nodeId) continue;

            const targetPorts = this.connectingFrom.isOutput ? node.inputs : node.outputs;
            targetPorts.forEach((port, index) => {
                if (this.checkTypeCompatibility(sourcePort.type, port.type)) {
                    const pos = this.getPortPosition(nodeId, index, !this.connectingFrom.isOutput);
                    if (pos) {
                        this.ctx.beginPath();
                        this.ctx.arc(pos.x, pos.y, 10 * this.scale, 0, Math.PI * 2);
                        this.ctx.fillStyle = 'rgba(82, 196, 26, 0.2)';
                        this.ctx.fill();
                        this.ctx.strokeStyle = '#52c41a';
                        this.ctx.setLineDash([2, 2]);
                        this.ctx.stroke();
                        this.ctx.setLineDash([]);
                    }
                }
            });
        }
    }

    /**
     * 删除连接
     * @param {string} connectionId - 连接ID
     */
    removeConnection(connectionId) {
        this.connections = this.connections.filter(conn => conn.id !== connectionId);
        this.render();
    }

    /**
     * 绘制临时连线（拖拽过程中）
     */
    drawTempConnection() {
        if (!this.isConnecting || !this.connectingFrom) return;

        const startPos = this.getPortPosition(
            this.connectingFrom.nodeId,
            this.connectingFrom.portIndex,
            this.connectingFrom.isOutput
        );

        if (!startPos) return;

        // 【新增】连线时高亮兼容端口
        this.highlightCompatiblePorts();

        const endX = (this.mousePosition.x - this.offset.x) * this.scale;
        const endY = (this.mousePosition.y - this.offset.y) * this.scale;

        // 绘制虚线
        this.ctx.beginPath();
        this.ctx.moveTo(startPos.x, startPos.y);

        const controlPoint1X = startPos.x + (endX - startPos.x) / 2;
        const controlPoint2X = startPos.x + (endX - startPos.x) / 2;

        this.ctx.bezierCurveTo(
            controlPoint1X, startPos.y,
            controlPoint2X, endY,
            endX, endY
        );

        this.ctx.strokeStyle = '#1890ff';
        this.ctx.lineWidth = 2 * this.scale;
        this.ctx.setLineDash([5 * this.scale, 5 * this.scale]);
        this.ctx.stroke();
        this.ctx.setLineDash([]);
    }

    /**
     * 绘制连接线 - 阶段四增强版，带数据流动粒子动画
     */
    drawConnection(connection) {
        const sourceNode = this.nodes.get(connection.source);
        const targetNode = this.nodes.get(connection.target);
        
        if (!sourceNode || !targetNode) return;
        
        // 【修正】使用 getPortPosition 以支持垂直分布
        const start = this.getPortPosition(connection.source, connection.sourcePort, true);
        const end = this.getPortPosition(connection.target, connection.targetPort, false);
        
        if (!start || !end) return;

        const controlPoint1X = start.x + (end.x - start.x) / 2;
        const controlPoint2X = start.x + (end.x - start.x) / 2;
        
        // 绘制贝塞尔曲线基础线
        this.ctx.beginPath();
        this.ctx.moveTo(start.x, start.y);
        this.ctx.bezierCurveTo(
            controlPoint1X, start.y,
            controlPoint2X, end.y,
            end.x, end.y
        );
        
        // 根据连接状态设置样式
        if (connection.status === 'active') {
            this.ctx.strokeStyle = '#2ecc71';
            this.ctx.shadowColor = 'rgba(46, 204, 113, 0.5)';
            this.ctx.shadowBlur = 10;
        } else if (connection.status === 'error') {
            this.ctx.strokeStyle = '#e74c3c';
            this.ctx.shadowColor = 'rgba(231, 76, 60, 0.5)';
            this.ctx.shadowBlur = 10;
        } else {
            this.ctx.strokeStyle = '#3498db';
            this.ctx.shadowColor = 'transparent';
            this.ctx.shadowBlur = 0;
        }
        
        this.ctx.lineWidth = 2 * this.scale;
        this.ctx.stroke();
        this.ctx.shadowBlur = 0;
        
        // 绘制数据流动粒子动画
        if (connection.status === 'active' || connection.status === 'flowing') {
            this.drawFlowParticles(start.x, start.y, controlPoint1X, start.y, 
                                   controlPoint2X, end.y, end.x, end.y, connection);
        }
    }
    
    /**
     * 绘制数据流动粒子 - 阶段四增强
     */
    drawFlowParticles(startX, startY, cp1x, cp1y, cp2x, cp2y, endX, endY, connection) {
        // 初始化粒子系统
        if (!connection.particles) {
            connection.particles = [];
            for (let i = 0; i < 5; i++) {
                connection.particles.push({
                    t: i / 5,
                    speed: 0.005 + Math.random() * 0.003
                });
            }
        }
        
        // 更新和绘制粒子
        connection.particles.forEach(particle => {
            // 更新位置
            particle.t += particle.speed;
            if (particle.t > 1) particle.t = 0;
            
            // 计算贝塞尔曲线上的点
            const t = particle.t;
            const mt = 1 - t;
            const x = mt * mt * mt * startX + 
                     3 * mt * mt * t * cp1x + 
                     3 * mt * t * t * cp2x + 
                     t * t * t * endX;
            const y = mt * mt * mt * startY + 
                     3 * mt * mt * t * cp1y + 
                     3 * mt * t * t * cp2y + 
                     t * t * t * endY;
            
            // 绘制发光粒子
            const gradient = this.ctx.createRadialGradient(x, y, 0, x, y, 6 * this.scale);
            gradient.addColorStop(0, 'rgba(255, 255, 255, 1)');
            gradient.addColorStop(0.5, 'rgba(52, 152, 219, 0.8)');
            gradient.addColorStop(1, 'rgba(52, 152, 219, 0)');
            
            this.ctx.beginPath();
            this.ctx.arc(x, y, 6 * this.scale, 0, Math.PI * 2);
            this.ctx.fillStyle = gradient;
            this.ctx.fill();
        });
    }

    /**
     * 绘制圆角矩形
     */
    roundRect(x, y, w, h, r) {
        this.ctx.beginPath();
        this.ctx.moveTo(x + r, y);
        this.ctx.lineTo(x + w - r, y);
        this.ctx.quadraticCurveTo(x + w, y, x + w, y + r);
        this.ctx.lineTo(x + w, y + h - r);
        this.ctx.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
        this.ctx.lineTo(x + r, y + h);
        this.ctx.quadraticCurveTo(x, y + h, x, y + h - r);
        this.ctx.lineTo(x, y + r);
        this.ctx.quadraticCurveTo(x, y, x + r, y);
        this.ctx.closePath();
    }

    /**
     * 渲染循环
     */
    render() {
        // 【修复】如果暂停，不执行渲染
        if (this._isPaused) {
            this._animationFrameId = null;
            return;
        }
        
        // 清空画布
        this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);

        // 绘制网格
        this.drawGrid();

        // 绘制连接线
        this.connections.forEach(conn => this.drawConnection(conn));

        // 绘制临时连线（拖拽过程中）
        if (this.isConnecting) {
            this.drawTempConnection();
        }

        // 绘制节点
        this.nodes.forEach(node => this.drawNode(node));

        // 绘制悬停端口高亮
        if (this.hoveredPort && !this.isConnecting) {
            this.drawPortHighlight(this.hoveredPort);
        }

        this._animationFrameId = requestAnimationFrame(() => this.render());
        
        // 绘制小地图
        this.drawMinimap();
    }

    /**
     * 规范化端口类型，确保其符合后端枚举名称 (PascalCase)
     */
    normalizePortType(type) {
        if (!type) return 'Any';
        // 如果后端传过来的是枚举数字，则保持不变，后端 JsonStringEnumConverter 可以解析
        if (typeof type === 'number') return type;
        
        const map = {
            'any': 'Any',
            'image': 'Image',
            'string': 'String',
            'integer': 'Integer',
            'float': 'Float',
            'boolean': 'Boolean',
            'point': 'Point',
            'rectangle': 'Rectangle',
            'contour': 'Contour'
        };
        
        return map[type.toLowerCase()] || type;
    }

    /**
     * 序列化流程数据 - 适配后端 DTO (camelCase)
     * 后端 Program.cs 配置 JsonNamingPolicy.CamelCase，所以必须使用小驼峰
     */
    serialize() {
        // 【修复】先确保所有节点的端口都有稳定的 ID
        // 这样 operators 和 connections 都会使用相同的 ID
        for (const node of this.nodes.values()) {
            if (node.inputs) {
                for (const port of node.inputs) {
                    if (!port.id) {
                        port.id = this.generateUUID();
                    }
                }
            }
            if (node.outputs) {
                for (const port of node.outputs) {
                    if (!port.id) {
                        port.id = this.generateUUID();
                    }
                }
            }
        }

        // 构建 Operators 列表 (camelCase)
        const operators = Array.from(this.nodes.values()).map(node => ({
            id: node.id,
            name: node.title,
            type: node.type,
            x: node.x,
            y: node.y,
            inputPorts: (node.inputs || []).map(p => ({
                id: p.id || p.Id || this.generateUUID(), // 【修复】同时检查大小写
                name: p.name,
                dataType: this.normalizePortType(p.type), // PortDataType enum
                direction: 0, // Input
                isRequired: false
            })),
            outputPorts: (node.outputs || []).map(p => ({
                id: p.id || p.Id || this.generateUUID(), // 【修复】同时检查大小写
                name: p.name,
                dataType: this.normalizePortType(p.type),
                direction: 1, // Output
                isRequired: false
            })),
            parameters: (node.parameters || []).map(p => ({
                name: p.name,
                value: p.value !== undefined ? p.value : p.defaultValue,
                dataType: p.dataType || p.type
            })),
            isEnabled: true
        }));

        // 构建 Connections 列表 (camelCase)
        console.log('[DEBUG serialize] === START SERIALIZE ===');
        console.log('[DEBUG serialize] Raw connections count:', this.connections.length);
        console.log('[DEBUG serialize] Raw connections:', JSON.stringify(this.connections, null, 2));
        console.log('[DEBUG serialize] Nodes in canvas:', Array.from(this.nodes.keys()));
        
        const connections = this.connections
            .filter(conn => {
                // 过滤掉无效的连接（source 或 target 为空、undefined 或空GUID）
                const isValidSource = conn.source && conn.source !== '00000000-0000-0000-0000-000000000000';
                const isValidTarget = conn.target && conn.target !== '00000000-0000-0000-0000-000000000000';
                if (!isValidSource || !isValidTarget) {
                    console.warn(`[DEBUG serialize] 过滤掉无效连接: source=${conn.source}, target=${conn.target}`);
                }
                return isValidSource && isValidTarget;
            })
            .map(conn => {
                const sourceNode = this.nodes.get(conn.source);
                const targetNode = this.nodes.get(conn.target);

                console.log(`[DEBUG serialize] Processing connection: source=${conn.source}, target=${conn.target}`);
                console.log(`[DEBUG serialize]   sourceNode exists: ${!!sourceNode}, targetNode exists: ${!!targetNode}`);

                // 【修复】添加端口索引边界检查，并同时检查 id/Id 属性
                let sourcePortId = null;
                let targetPortId = null;

                if (sourceNode && conn.sourcePort >= 0 && conn.sourcePort < sourceNode.outputs.length) {
                    const port = sourceNode.outputs[conn.sourcePort];
                    sourcePortId = port?.id || port?.Id;
                    if (!sourcePortId) {
                        console.error(`[DEBUG serialize] 源端口索引 ${conn.sourcePort} 存在但没有ID，生成新ID`);
                        port.id = this.generateUUID(); // 为端口分配ID
                        sourcePortId = port.id;
                    }
                } else {
                    console.error(`[DEBUG serialize] 源端口索引越界: ${conn.sourcePort}, 可用端口数: ${sourceNode?.outputs?.length || 0}`);
                }

                if (targetNode && conn.targetPort >= 0 && conn.targetPort < targetNode.inputs.length) {
                    const port = targetNode.inputs[conn.targetPort];
                    targetPortId = port?.id || port?.Id;
                    if (!targetPortId) {
                        console.error(`[DEBUG serialize] 目标端口索引 ${conn.targetPort} 存在但没有ID，生成新ID`);
                        port.id = this.generateUUID(); // 为端口分配ID
                        targetPortId = port.id;
                    }
                } else {
                    console.error(`[DEBUG serialize] 目标端口索引越界: ${conn.targetPort}, 可用端口数: ${targetNode?.inputs?.length || 0}`);
                }

                console.log(`[DEBUG serialize]   sourcePortId: ${sourcePortId}, targetPortId: ${targetPortId}`);

                // 【修复】如果无法获取端口ID，跳过此连接而不是生成错误的UUID
                if (!sourcePortId || !targetPortId) {
                    console.error(`[DEBUG serialize] 跳过无效连接: sourcePortId=${sourcePortId}, targetPortId=${targetPortId}`);
                    return null;
                }

                const result = {
                    id: conn.id,
                    sourceOperatorId: conn.source,
                    sourcePortId: sourcePortId,
                    targetOperatorId: conn.target,
                    targetPortId: targetPortId
                };

                console.log(`[DEBUG serialize]   Serialized connection:`, JSON.stringify(result));
                return result;
            })
            .filter(conn => conn !== null); // 过滤掉无效的连接

        // UpdateFlowRequest 期望的结构 (camelCase 会被后端自动映射)
        const result = {
            operators: operators,
            connections: connections
        };
        
        console.log('[DEBUG serialize] === FINAL SERIALIZED DATA ===');
        console.log('[DEBUG serialize] Operators count:', operators.length);
        console.log('[DEBUG serialize] Operator IDs:', operators.map(o => o.id));
        console.log('[DEBUG serialize] Connections count:', connections.length);
        console.log('[DEBUG serialize] Connections:', JSON.stringify(connections, null, 2));
        console.log('[DEBUG serialize] === END SERIALIZE ===');
        
        return result;
    }

    /**
     * 反序列化流程数据
     */
    deserialize(data) {
        if (!data) return;
        this.clear();

        // 支持多种嵌套结构 (后端 DTO 可能包装在 project.flow 中)
        const flowData = data.project?.flow || data.flow || data;
        
        // 处理列表属性 (驼峰/帕斯卡/旧版 nodes 键)
        const operators = flowData.operators || flowData.Operators || flowData.nodes || [];
        const connections = flowData.connections || flowData.Connections || [];

        console.log('[FlowCanvas] 开始反序列化. 算子数:', operators.length, '连接数:', connections.length);

        if (operators) {
            operators.forEach(op => {
                // 适配后端 DTO (PascalCase) 或前端 (camelCase)
                const id = op.id || op.Id;
                const type = op.type || op.Type;
                const title = op.name || op.Name || op.title || type;
                
                // 【修复】标准化端口数据，统一使用小写属性名（id/name/type）
                const normalizePort = (p) => ({
                    id: p.id || p.Id || this.generateUUID(),
                    name: p.name || p.Name,
                    type: p.type || p.Type || p.dataType || p.DataType || 0
                });

                const inputs = (op.inputPorts || op.InputPorts || op.inputs || []).map(normalizePort);
                const outputs = (op.outputPorts || op.OutputPorts || op.outputs || []).map(normalizePort);

                const node = {
                    id: id,
                    type: type,
                    x: op.x || op.X || 0,
                    y: op.y || op.Y || 0,
                    width: 140,
                    height: 60,
                    title: title,
                    inputs: inputs,
                    outputs: outputs,
                    parameters: op.parameters || op.Parameters || [],
                    color: '#1890ff' // Default
                };

                // Restore color logic based on type
                if (node.type === 'ImageAcquisition') node.color = '#52c41a';
                if (node.type === 'ResultOutput') node.color = '#595959';
                
                this.nodes.set(node.id, node);
            });
        }

        if (connections) {
            this.connections = connections.map(conn => {
                // Adapt backend DTO (PascalCase) or frontend (camelCase)
                const id = conn.id || conn.Id;
                const sourceId = conn.sourceOperatorId || conn.SourceOperatorId || conn.source;
                const targetId = conn.targetOperatorId || conn.TargetOperatorId || conn.target;

                const sourcePortId = conn.sourcePortId || conn.SourcePortId;
                const targetPortId = conn.targetPortId || conn.TargetPortId;

                const sourceNode = this.nodes.get(sourceId);
                const targetNode = this.nodes.get(targetId);

                let sourcePortIndex = conn.sourcePort || 0;
                let targetPortIndex = conn.targetPort || 0;

                // Find index by Port ID if available (Backend/DTO usually provides IDs)
                if (sourcePortId && sourceNode && sourceNode.outputs) {
                    // Note: Node outputs might be objects with 'Id' or 'id' depending on how they were deserialized above
                    // But we simply copied the array. Let's check the array content structure if it came from DTO
                    // DTO InputPorts/OutputPorts have 'Id'. Frontend 'inputs/outputs' have 'id'.
                    // We need to handle that map above?
                    // Actually, in 'node' construction above, we assigned 'inputs' directly.
                    // If it came from DTO, the objects inside have 'Id', 'Name', etc.
                    // If it came from frontend, they have 'id', 'name'.
                    // We should normalize ports too?
                    // For now, let's just find by checking both 'id' and 'Id'.
                    const idx = sourceNode.outputs.findIndex(p => (p.id === sourcePortId) || (p.Id === sourcePortId));
                    if (idx !== -1) sourcePortIndex = idx;
                }

                if (targetPortId && targetNode && targetNode.inputs) {
                    const idx = targetNode.inputs.findIndex(p => (p.id === targetPortId) || (p.Id === targetPortId));
                    if (idx !== -1) targetPortIndex = idx;
                }

                return {
                    id: id,
                    source: sourceId,
                    sourcePort: sourcePortIndex,
                    target: targetId,
                    targetPort: targetPortIndex
                };
            }).filter(conn => {
                // 过滤掉无效的连接（source 或 target 为空、undefined 或空GUID）
                const isValidSource = conn.source && conn.source !== '00000000-0000-0000-0000-000000000000';
                const isValidTarget = conn.target && conn.target !== '00000000-0000-0000-0000-000000000000';
                if (!isValidSource || !isValidTarget) {
                    console.warn('[FlowCanvas] 过滤掉无效连接:', conn);
                }
                return isValidSource && isValidTarget;
            });
        }

        this.render();
    }

    /**
     * 绘制端口高亮效果
     * @param {{nodeId: string, portIndex: number, isOutput: boolean, hasConnection: boolean}} port
     */
    drawPortHighlight(port) {
        const pos = this.getPortPosition(port.nodeId, port.portIndex, port.isOutput);
        if (!pos) return;

        // 绘制高亮圆环
        this.ctx.beginPath();
        this.ctx.arc(pos.x, pos.y, 8 * this.scale, 0, Math.PI * 2);
        this.ctx.strokeStyle = port.isOutput ? '#1890ff' : '#52c41a';
        this.ctx.lineWidth = 2 * this.scale;
        this.ctx.stroke();

        // 绘制发光效果
        this.ctx.beginPath();
        this.ctx.arc(pos.x, pos.y, 12 * this.scale, 0, Math.PI * 2);
        this.ctx.fillStyle = port.isOutput
            ? 'rgba(24, 144, 255, 0.2)'
            : 'rgba(82, 196, 26, 0.2)';
        this.ctx.fill();

        // 【新增】如果端口已连接，绘制断开指示
        if (port.hasConnection) {
            // 绘制红色虚线圆环表示可断开
            this.ctx.beginPath();
            this.ctx.arc(pos.x, pos.y, 14 * this.scale, 0, Math.PI * 2);
            this.ctx.strokeStyle = 'rgba(231, 76, 60, 0.6)'; // 红色半透明
            this.ctx.lineWidth = 2 * this.scale;
            this.ctx.setLineDash([4 * this.scale, 2 * this.scale]);
            this.ctx.stroke();
            this.ctx.setLineDash([]);
        }
    }

    /**
     * 处理鼠标按下
     */
    handleMouseDown(e) {
        const rect = this.canvas.getBoundingClientRect();
        const x = (e.clientX - rect.left) / this.scale + this.offset.x;
        const y = (e.clientY - rect.top) / this.scale + this.offset.y;

        // 更新鼠标位置
        this.mousePosition = { x, y };

        // 首先检测是否点击了端口
        const port = this.getPortAt(x, y);
        if (port) {
            if (port.isOutput) {
                // 【新增】检查输出端口是否已有连接
                const existingConns = this.getConnectionsAtPort(port.nodeId, port.portIndex, true);
                
                if (existingConns.length > 0) {
                    // 断开该端口的所有连接
                    existingConns.forEach(conn => {
                        this.removeConnection(conn.id);
                    });
                    if (window.showToast) {
                        const msg = existingConns.length === 1 
                            ? '连接已断开' 
                            : `已断开 ${existingConns.length} 个连接`;
                        window.showToast(msg, 'info');
                    }
                    console.log('[FlowCanvas] 已断开连接:', existingConns.map(c => c.id));
                } else {
                    // 没有连接，从输出端口开始连线
                    this.startConnection(port.nodeId, port.portIndex);
                }
                return;
            } else if (this.isConnecting) {
                // 从输入端口完成连线
                this.finishConnection(port.nodeId, port.portIndex);
                return;
            } else {
                // 【新增】点击输入端口时检查是否已有连接
                const existingConn = this.getConnectionAtPort(port.nodeId, port.portIndex, false);
                
                if (existingConn) {
                    // 断开该输入端口的连接
                    this.removeConnection(existingConn.id);
                    if (window.showToast) {
                        window.showToast('连接已断开', 'info');
                    }
                    console.log('[FlowCanvas] 已断开连接:', existingConn.id);
                    return;
                }
            }
        }

        // 如果在连线状态但点击了空白处，取消连线
        if (this.isConnecting) {
            this.cancelConnection();
            return;
        }

        // 查找点击的节点
        for (const [id, node] of this.nodes) {
            if (x >= node.x && x <= node.x + node.width &&
                y >= node.y && y <= node.y + node.height) {
                this.selectedNode = id;
                this.draggedNode = id;
                this.dragOffset = { x: x - node.x, y: y - node.y };

                // 触发节点选中回调
                if (this.onNodeSelected) {
                    this.onNodeSelected(node);
                }

                this.render();
                return;
            }
        }

        this.selectedNode = null;

        // 触发取消选中回调
        if (this.onNodeSelected) {
            this.onNodeSelected(null);
        }

        this.render();
    }

    /**
     * 处理双击事件（主要用于子图展开等高级交互）
     */
    handleDoubleClick(e) {
        const rect = this.canvas.getBoundingClientRect();
        const x = (e.clientX - rect.left) / this.scale + this.offset.x;
        const y = (e.clientY - rect.top) / this.scale + this.offset.y;

        // 查找双击的节点
        for (const [id, node] of this.nodes) {
            if (x >= node.x && x <= node.x + node.width &&
                y >= node.y && y <= node.y + node.height) {
                
                // 触发双击事件回调
                if (this.onNodeDoubleClicked) {
                    this.onNodeDoubleClicked(node);
                }
                return;
            }
        }
    }

    /**
     * 处理鼠标移动
     */
    handleMouseMove(e) {
        const rect = this.canvas.getBoundingClientRect();
        const x = (e.clientX - rect.left) / this.scale + this.offset.x;
        const y = (e.clientY - rect.top) / this.scale + this.offset.y;

        // 更新鼠标位置
        this.mousePosition = { x, y };

        // 处理连线状态
        if (this.isConnecting) {
            // 检测悬停的端口
            const port = this.getPortAt(x, y);
            if (port && !port.isOutput && port.nodeId !== this.connectingFrom?.nodeId) {
                // 悬停在有效的输入端口上
                this.hoveredPort = port;
                this.canvas.style.cursor = 'pointer';
            } else {
                this.hoveredPort = null;
                this.canvas.style.cursor = 'crosshair';
            }
            return;
        }

        // 处理节点拖拽
        if (this.draggedNode) {
            const dragX = x - this.dragOffset.x;
            const dragY = y - this.dragOffset.y;

            const node = this.nodes.get(this.draggedNode);
            if (node) {
                node.x = Math.round(dragX / 10) * 10; // 对齐网格
                node.y = Math.round(dragY / 10) * 10;
            }
            return;
        }

        // 检测端口悬停（改变光标）
        const port = this.getPortAt(x, y);
        if (port) {
            // 【新增】检测端口是否有连接
            const hasConnection = this.getConnectionAtPort(port.nodeId, port.portIndex, port.isOutput) !== null;
            
            if (hasConnection && !this.isConnecting) {
                // 已连接且不在连线模式下，显示可断开提示
                this.canvas.style.cursor = 'pointer';
                this.hoveredPort = { ...port, hasConnection: true };
            } else if (this.isConnecting) {
                this.canvas.style.cursor = 'crosshair';
                this.hoveredPort = port;
            } else {
                this.canvas.style.cursor = 'pointer';
                this.hoveredPort = port;
            }
        } else {
            this.canvas.style.cursor = 'default';
            this.hoveredPort = null;
        }
    }

    /**
     * 处理鼠标释放
     */
    handleMouseUp() {
        this.draggedNode = null;
    }

    /**
     * 处理滚轮缩放
     */
    handleWheel(e) {
        e.preventDefault();
        
        const delta = e.deltaY > 0 ? 0.9 : 1.1;
        // 调整缩放范围：0.2 (20%) - 2.0 (200%) - 用户反馈缩放过小不方便定位
        const newScale = Math.max(0.2, Math.min(2.0, this.scale * delta));
        
        if (newScale !== this.scale) {
            const rect = this.canvas.getBoundingClientRect();
            const mouseX = e.clientX - rect.left;
            const mouseY = e.clientY - rect.top;
            
            // 以鼠标为中心缩放
            this.offset.x += mouseX / this.scale - mouseX / newScale;
            this.offset.y += mouseY / this.scale - mouseY / newScale;
            this.scale = newScale;
        }
    }

    /**
     * 清空画布
     */
    clear() {
        this.nodes.clear();
        this.connections = [];
        this.selectedNode = null;
        this.render();
    }

    /**
     * 检测鼠标位置是否在连接线上
     * @param {number} x - 鼠标X坐标（世界坐标）
     * @param {number} y - 鼠标Y坐标（世界坐标）
     * @param {Object} connection - 连接线对象
     * @returns {boolean}
     */
    isPointOnConnection(x, y, connection) {
        const sourceNode = this.nodes.get(connection.source);
        const targetNode = this.nodes.get(connection.target);

        if (!sourceNode || !targetNode) return false;

        const startX = (sourceNode.x + sourceNode.width - this.offset.x) * this.scale;
        const startY = (sourceNode.y + sourceNode.height / 2 - this.offset.y) * this.scale;
        const endX = (targetNode.x - this.offset.x) * this.scale;
        const endY = (targetNode.y + targetNode.height / 2 - this.offset.y) * this.scale;

        const screenX = (x - this.offset.x) * this.scale;
        const screenY = (y - this.offset.y) * this.scale;

        // 简单的点到线段距离检测（使用贝塞尔曲线的控制点近似）
        const controlPoint1X = startX + (endX - startX) / 2;
        const controlPoint1Y = startY;
        const controlPoint2X = startX + (endX - startX) / 2;
        const controlPoint2Y = endY;

        // 采样贝塞尔曲线上的点
        const samples = 10;
        const threshold = 10 * this.scale;

        for (let t = 0; t <= 1; t += 1 / samples) {
            const mt = 1 - t;
            const px = mt * mt * mt * startX +
                       3 * mt * mt * t * controlPoint1X +
                       3 * mt * t * t * controlPoint2X +
                       t * t * t * endX;
            const py = mt * mt * mt * startY +
                       3 * mt * mt * t * controlPoint1Y +
                       3 * mt * t * t * controlPoint2Y +
                       t * t * t * endY;

            const dx = screenX - px;
            const dy = screenY - py;
            if (Math.sqrt(dx * dx + dy * dy) < threshold) {
                return true;
            }
        }

        return false;
    }

    /**
     * 获取鼠标位置下的连接线
     * @param {number} x - 鼠标X坐标（世界坐标）
     * @param {number} y - 鼠标Y坐标（世界坐标）
     * @returns {Object|null}
     */
    getConnectionAt(x, y) {
        // 倒序遍历，优先选择最上面的连接线
        for (let i = this.connections.length - 1; i >= 0; i--) {
            if (this.isPointOnConnection(x, y, this.connections[i])) {
                return this.connections[i];
            }
        }
        return null;
    }

    /**
     * 处理右键菜单
     */
    handleContextMenu(e) {
        e.preventDefault();

        const rect = this.canvas.getBoundingClientRect();
        const x = (e.clientX - rect.left) / this.scale + this.offset.x;
        const y = (e.clientY - rect.top) / this.scale + this.offset.y;

        // 检查是否右键点击了连接线
        const connection = this.getConnectionAt(x, y);
        if (connection) {
            if (confirm('确定要删除这条连接线吗？')) {
                this.removeConnection(connection.id);
            }
            return;
        }

        // 检查是否右键点击了节点
        for (const [id, node] of this.nodes) {
            if (x >= node.x && x <= node.x + node.width &&
                y >= node.y && y <= node.y + node.height) {
                if (confirm(`确定要删除节点 "${node.title}" 吗？`)) {
                    this.removeNode(id);
                }
                return;
            }
        }
    }

    /**
     * 处理键盘事件
     */
    handleKeyDown(e) {
        // 如果焦点在输入框、文本区域或可编辑元素中，不拦截快捷键
        if (e.target.tagName === 'INPUT' || 
            e.target.tagName === 'TEXTAREA' || 
            e.target.tagName === 'SELECT' || 
            e.target.isContentEditable) {
            return;
        }

        // Delete 键或 Backspace 键删除选中的节点或连接线
        if (e.key === 'Delete' || e.key === 'Backspace') {
            if (this.selectedNode) {
                if (confirm('确定要删除选中的节点吗？')) {
                    this.removeNode(this.selectedNode);
                }
            } else if (this.selectedConnection) {
                if (confirm('确定要删除选中的连接线吗？')) {
                    this.removeConnection(this.selectedConnection.id);
                }
            }
        }

        // Escape 键取消连线
        if (e.key === 'Escape' && this.isConnecting) {
            this.cancelConnection();
        }
    }

    /**
     * 设置节点状态
     * @param {string} nodeId - 节点ID
     * @param {string} status - 状态: 'idle' | 'running' | 'success' | 'error'
     */
    setNodeStatus(nodeId, status) {
        const node = this.nodes.get(nodeId);
        if (node) {
            node.status = status;
            this.render();
        }
    }

    /**
     * 重置所有节点状态
     */
    resetAllStatus() {
        this.nodes.forEach(node => {
            node.status = 'idle';
        });
        this.render();
    }

    // ==========================================================================
    // 阶段四增强：右键菜单功能
    // ==========================================================================

    /**
     * 处理右键菜单事件
     */
    handleContextMenu(e) {
        e.preventDefault();
        
        const rect = this.canvas.getBoundingClientRect();
        const x = (e.clientX - rect.left) / this.scale + this.offset.x;
        const y = (e.clientY - rect.top) / this.scale + this.offset.y;
        
        // 查找右键点击的节点
        let clickedNode = null;
        for (const [id, node] of this.nodes) {
            if (x >= node.x && x <= node.x + node.width &&
                y >= node.y && y <= node.y + node.height) {
                clickedNode = { id, node };
                break;
            }
        }
        
        if (clickedNode) {
            this.showNodeContextMenu(e.clientX, e.clientY, clickedNode.id);
        }
    }

    /**
     * 显示节点右键菜单
     */
    showNodeContextMenu(x, y, nodeId) {
        this.hideContextMenu();
        
        const node = this.nodes.get(nodeId);
        if (!node) return;
        
        const menu = document.createElement('div');
        menu.className = 'flow-context-menu';
        menu.style.cssText = `
            position: fixed;
            left: ${x}px;
            top: ${y}px;
            background: rgba(15, 36, 53, 0.95);
            backdrop-filter: blur(10px);
            border: 1px solid rgba(255, 255, 255, 0.1);
            border-radius: 8px;
            padding: 8px 0;
            min-width: 160px;
            z-index: 1000;
            box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3);
            animation: contextMenuFadeIn 0.15s ease-out;
        `;
        
        const menuItems = [
            { icon: '▶️', label: '运行', action: () => this.runNode(nodeId) },
            { icon: '📋', label: '复制', action: () => this.duplicateNode(nodeId) },
            { icon: '❌', label: '删除', action: () => this.deleteNode(nodeId), danger: true },
            { icon: '🚫', label: node.disabled ? '启用' : '禁用', action: () => this.toggleNodeDisabled(nodeId) },
            { icon: '❓', label: '查看帮助', action: () => this.showNodeHelp(node) }
        ];
        
        menuItems.forEach(item => {
            const menuItem = document.createElement('div');
            menuItem.className = 'context-menu-item';
            menuItem.style.cssText = `
                padding: 8px 16px;
                cursor: pointer;
                display: flex;
                align-items: center;
                gap: 8px;
                font-size: 13px;
                color: ${item.danger ? '#e74c3c' : '#fdfbf7'};
                transition: all 0.2s;
            `;
            menuItem.innerHTML = `<span>${item.icon}</span><span>${item.label}</span>`;
            menuItem.addEventListener('mouseenter', () => {
                menuItem.style.background = item.danger ? 'rgba(231, 76, 60, 0.2)' : 'rgba(231, 76, 60, 0.1)';
            });
            menuItem.addEventListener('mouseleave', () => {
                menuItem.style.background = 'transparent';
            });
            menuItem.addEventListener('click', () => {
                item.action();
                this.hideContextMenu();
            });
            menu.appendChild(menuItem);
        });
        
        document.body.appendChild(menu);
        this.contextMenu = menu;
        
        // 添加动画样式
        if (!document.getElementById('contextMenuStyles')) {
            const style = document.createElement('style');
            style.id = 'contextMenuStyles';
            style.textContent = `
                @keyframes contextMenuFadeIn {
                    from { opacity: 0; transform: scale(0.95); }
                    to { opacity: 1; transform: scale(1); }
                }
            `;
            document.head.appendChild(style);
        }
        
        // 点击外部关闭菜单
        setTimeout(() => {
            document.addEventListener('click', this._clickOutsideHandler);
        }, 0);
    }

    /**
     * 隐藏右键菜单
     */
    hideContextMenu() {
        if (this.contextMenu) {
            this.contextMenu.remove();
            this.contextMenu = null;
        }
        document.removeEventListener('click', this._clickOutsideHandler);
    }

    /**
     * 清空画布
     */
    clear() {
        this.nodes.clear();
        this.connections = [];
        this.selectedNode = null;
        this.draggedNode = null;
        this.selectedConnection = null;
        this.render();
        console.log('[FlowCanvas] 画布已清空');
    }

    /**
     * 运行单个节点
     */
    runNode(nodeId) {
        console.log('[FlowCanvas] 运行节点:', nodeId);
        this.setNodeStatus(nodeId, 'running');
        // 这里可以触发实际的节点执行逻辑
        setTimeout(() => {
            this.setNodeStatus(nodeId, 'success');
        }, 1000);
    }

    /**
     * 复制节点
     */
    duplicateNode(nodeId) {
        const node = this.nodes.get(nodeId);
        if (!node) return;
        
        const newNode = {
            ...node,
            id: this.generateUUID(),
            x: node.x + 30,
            y: node.y + 30,
            title: node.title + ' (副本)'
        };
        
        this.nodes.set(newNode.id, newNode);
        this.selectedNode = newNode.id;
        this.render();
    }

    /**
     * 删除节点
     */
    deleteNode(nodeId) {
        // 删除相关连接
        this.connections = this.connections.filter(conn => 
            conn.source !== nodeId && conn.target !== nodeId
        );
        
        this.nodes.delete(nodeId);
        if (this.selectedNode === nodeId) {
            this.selectedNode = null;
        }
        this.render();
    }

    /**
     * 切换节点禁用状态
     */
    toggleNodeDisabled(nodeId) {
        const node = this.nodes.get(nodeId);
        if (node) {
            node.disabled = !node.disabled;
            this.render();
        }
    }

    /**
     * 显示节点帮助
     */
    showNodeHelp(node) {
        alert(`节点类型: ${node.type}\n名称: ${node.title}\n\n这是一个 ${node.type} 算子节点。`);
    }

    // ==========================================================================
    // 阶段四增强：小地图功能
    // ==========================================================================

    /**
     * 初始化小地图
     */
    initMinimap() {
        if (this.minimap) return;
        
        this.minimap = document.createElement('div');
        this.minimap.className = 'flow-minimap';
        this.minimap.style.cssText = `
            position: absolute;
            right: 20px;
            bottom: 20px;
            width: 200px;
            height: 150px;
            background: rgba(15, 36, 53, 0.9);
            border: 1px solid rgba(255, 255, 255, 0.1);
            border-radius: 8px;
            overflow: hidden;
            z-index: 100;
            box-shadow: 0 4px 16px rgba(0, 0, 0, 0.3);
        `;
        
        this.minimapCanvas = document.createElement('canvas');
        this.minimapCanvas.width = 200;
        this.minimapCanvas.height = 150;
        this.minimap.appendChild(this.minimapCanvas);
        
        this.canvas.parentElement.appendChild(this.minimap);
        
        // 点击小地图导航
        this.minimapCanvas.addEventListener('click', (e) => {
            const rect = this.minimapCanvas.getBoundingClientRect();
            const x = (e.clientX - rect.left) / rect.width;
            const y = (e.clientY - rect.top) / rect.height;
            
            // 计算视口中心位置
            const bounds = this.getNodesBounds();
            if (bounds) {
                const targetX = bounds.minX + x * (bounds.maxX - bounds.minX + bounds.width);
                const targetY = bounds.minY + y * (bounds.maxY - bounds.minY + bounds.height);
                
                this.offset.x = targetX - this.canvas.width / 2 / this.scale;
                this.offset.y = targetY - this.canvas.height / 2 / this.scale;
                this.render();
            }
        });
    }

    /**
     * 获取所有节点的边界
     */
    getNodesBounds() {
        if (this.nodes.size === 0) return null;
        
        let minX = Infinity, minY = Infinity;
        let maxX = -Infinity, maxY = -Infinity;
        
        this.nodes.forEach(node => {
            minX = Math.min(minX, node.x);
            minY = Math.min(minY, node.y);
            maxX = Math.max(maxX, node.x + node.width);
            maxY = Math.max(maxY, node.y + node.height);
        });
        
        return { minX, minY, maxX, maxY, width: maxX - minX, height: maxY - minY };
    }

    /**
     * 绘制小地图
     */
    drawMinimap() {
        if (!this.minimapCanvas) return;
        
        const ctx = this.minimapCanvas.getContext('2d');
        const width = this.minimapCanvas.width;
        const height = this.minimapCanvas.height;
        
        // 清空
        ctx.clearRect(0, 0, width, height);
        
        const bounds = this.getNodesBounds();
        if (!bounds) return;
        
        // 添加内边距
        const padding = 20;
        const scaleX = width / (bounds.width + padding * 2);
        const scaleY = height / (bounds.height + padding * 2);
        const scale = Math.min(scaleX, scaleY);
        
        const offsetX = (width - (bounds.width + padding * 2) * scale) / 2 + padding * scale;
        const offsetY = (height - (bounds.height + padding * 2) * scale) / 2 + padding * scale;
        
        // 绘制节点
        this.nodes.forEach(node => {
            const x = offsetX + (node.x - bounds.minX) * scale;
            const y = offsetY + (node.y - bounds.minY) * scale;
            const w = Math.max(4, node.width * scale);
            const h = Math.max(3, node.height * scale);
            
            ctx.fillStyle = node.disabled ? '#666' : (node.color || '#1890ff');
            ctx.fillRect(x, y, w, h);
            
            // 选中高亮
            if (node.id === this.selectedNode) {
                ctx.strokeStyle = '#fff';
                ctx.lineWidth = 2;
                ctx.strokeRect(x - 1, y - 1, w + 2, h + 2);
            }
        });
        
        // 绘制视口框
        const viewportX = offsetX + (this.offset.x - bounds.minX) * scale;
        const viewportY = offsetY + (this.offset.y - bounds.minY) * scale;
        const viewportW = this.canvas.width / this.scale * scale;
        const viewportH = this.canvas.height / this.scale * scale;
        
        ctx.strokeStyle = 'rgba(231, 76, 60, 0.8)';
        ctx.lineWidth = 2;
        ctx.strokeRect(viewportX, viewportY, viewportW, viewportH);
    }

    /**
     * 更新渲染循环以包含小地图
     */
    renderWithMinimap() {
        this.render();
        this.drawMinimap();
    }
}

export default FlowCanvas;
export { FlowCanvas };
