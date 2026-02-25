/**
 * 流程编辑器交互系统
 * 扩展FlowCanvas的拖拽、连线、选择、快捷键功能
 */

import { showToast } from '../../shared/components/uiComponents.js';
import TemplateSelector from './templateSelector.js';

export class FlowEditorInteraction {
    constructor(flowCanvas) {
        this.canvas = flowCanvas;
        this.isConnecting = false;
        this.connectionStart = null;
        this.connectionEnd = null;
        this.isSelecting = false;
        this.selectionBox = null;
        this.selectionStart = null;
        this.multiSelectedNodes = new Set();
        this.copiedNodes = [];
        this.history = [];
        this.historyIndex = -1;
        this.maxHistorySize = 50;
        this.templateSelector = null;

        this.initialize();
    }

    initialize() {
        // 增强原有事件监听
        this.enhanceEventListeners();
        // 绑定键盘快捷键
        this.bindKeyboardShortcuts();
        // 启用算子库拖拽
        this.enableOperatorLibraryDrag();
        // 初始化流程模板选择器入口
        this.initializeTemplateSelector();
        // 初始化历史记录
        this.saveState();
    }

    /**
     * 初始化模板选择器
     */
    initializeTemplateSelector() {
        this.templateSelector = new TemplateSelector(this.canvas);

        const toolbar = document.querySelector('.toolbar-right');
        if (!toolbar) return;

        let templateButton = document.getElementById('btn-template-create');
        if (!templateButton) {
            templateButton = document.createElement('button');
            templateButton.id = 'btn-template-create';
            templateButton.className = 'btn btn-secondary btn-with-icon';
            templateButton.innerHTML = `
                <svg class="btn-icon-svg" viewBox="0 0 24 24" width="16" height="16" fill="currentColor">
                    <path d="M3 5h18v2H3V5zm0 6h18v2H3v-2zm0 6h18v2H3v-2z"/>
                </svg>
                <span>从模板创建</span>
            `;

            const aiButton = document.getElementById('btn-ai-gen');
            if (aiButton && aiButton.parentElement === toolbar) {
                toolbar.insertBefore(templateButton, aiButton);
            } else {
                toolbar.appendChild(templateButton);
            }
        }

        if (templateButton.dataset.boundTemplateSelector === 'true') {
            return;
        }

        templateButton.dataset.boundTemplateSelector = 'true';
        templateButton.addEventListener('click', async () => {
            try {
                await this.templateSelector.open();
            } catch (error) {
                console.error('[FlowEditorInteraction] 打开模板选择器失败:', error);
                showToast(`打开模板选择器失败: ${error.message}`, 'error');
            }
        });
    }

    /**
     * 增强事件监听
     */
    enhanceEventListeners() {
        const originalMouseDown = this.canvas.handleMouseDown.bind(this.canvas);
        const originalMouseMove = this.canvas.handleMouseMove.bind(this.canvas);
        const originalMouseUp = this.canvas.handleMouseUp.bind(this.canvas);

        // 重写鼠标按下事件
        this.canvas.handleMouseDown = (e) => {
            const rect = this.canvas.canvas.getBoundingClientRect();
            const x = (e.clientX - rect.left) / this.canvas.scale + this.canvas.offset.x;
            const y = (e.clientY - rect.top) / this.canvas.scale + this.canvas.offset.y;

            // 检测点击的端口
            const clickedPort = this.getPortAt(x, y);
            
            if (clickedPort) {
                // 开始连线
                this.startConnection(clickedPort, e);
                return;
            }

            // 检测点击的节点
            const clickedNode = this.getNodeAt(x, y);

            if (e.shiftKey || e.ctrlKey) {
                // 多选模式
                if (clickedNode) {
                    this.toggleNodeSelection(clickedNode.id);
                } else {
                    // 开始框选
                    this.startSelection(e);
                }
            } else {
                if (clickedNode) {
                    if (!this.multiSelectedNodes.has(clickedNode.id)) {
                        this.clearSelection();
                        this.selectNode(clickedNode.id);
                    }
                    // 调用原生的拖拽逻辑
                    originalMouseDown(e);
                } else {
                    this.clearSelection();
                    this.startSelection(e);
                }
            }
        };

        // 重写鼠标移动事件
        this.canvas.handleMouseMove = (e) => {
            if (this.isConnecting) {
                this.updateConnectionPreview(e);
            } else if (this.isSelecting) {
                this.updateSelectionBox(e);
            } else {
                originalMouseMove(e);
            }
        };

        // 重写鼠标释放事件
        this.canvas.handleMouseUp = (e) => {
            if (this.isConnecting) {
                this.endConnection(e);
            } else if (this.isSelecting) {
                this.endSelection();
            } else {
                // 拖拽结束，保存状态
                if (this.canvas.draggedNode) {
                    this.saveState();
                }
                originalMouseUp(e);
            }
        };
    }

    /**
     * 绑定键盘快捷键
     */
    bindKeyboardShortcuts() {
        document.addEventListener('keydown', (e) => {
            // 复制
            if ((e.ctrlKey || e.metaKey) && e.key === 'c') {
                e.preventDefault();
                this.copySelectedNodes();
            }

            // 粘贴
            if ((e.ctrlKey || e.metaKey) && e.key === 'v') {
                e.preventDefault();
                this.pasteNodes();
            }

            // 删除
            if (e.key === 'Delete' || e.key === 'Backspace') {
                // 如果焦点在输入框、文本区域或可编辑元素中，不拦截
                if (e.target.tagName === 'INPUT' || 
                    e.target.tagName === 'TEXTAREA' || 
                    e.target.tagName === 'SELECT' || 
                    e.target.isContentEditable) {
                    return;
                }
                e.preventDefault();
                this.deleteSelectedNodes();
            }

            // 撤销
            if ((e.ctrlKey || e.metaKey) && e.key === 'z') {
                e.preventDefault();
                this.undo();
            }

            // 重做
            if ((e.ctrlKey || e.metaKey) && e.key === 'y') {
                e.preventDefault();
                this.redo();
            }

            // 全选
            if ((e.ctrlKey || e.metaKey) && e.key === 'a') {
                e.preventDefault();
                this.selectAll();
            }

            // 取消选择
            if (e.key === 'Escape') {
                this.clearSelection();
                this.cancelConnection();
            }
        });
    }

    /**
     * 启用算子库拖拽
     */
    enableOperatorLibraryDrag() {
        const library = document.getElementById('operator-library');
        if (!library) return;

        library.addEventListener('dragstart', (e) => {
            if (e.target.classList.contains('operator-item')) {
                const operatorType = e.target.dataset.type;
                const operatorName = e.target.dataset.name || operatorType;
                e.dataTransfer.setData('application/json', JSON.stringify({
                    type: operatorType,
                    name: operatorName
                }));
            }
        });

        this.canvas.canvas.addEventListener('dragover', (e) => {
            e.preventDefault();
            e.dataTransfer.dropEffect = 'copy';
        });

        this.canvas.canvas.addEventListener('drop', (e) => {
            e.preventDefault();
            
            let operator = null;
            
            // 优先从全局变量获取 (针对 WebView2 环境)
            if (window.__draggingOperatorData) {
                operator = window.__draggingOperatorData;
                window.__draggingOperatorData = null; // 使用后清除
            } else {
                const data = e.dataTransfer.getData('application/json');
                if (data) {
                    try {
                        operator = JSON.parse(data);
                    } catch (err) {
                        console.error('拖拽解析失败:', err);
                    }
                }
            }

            if (operator) {
                try {
                    const rect = this.canvas.canvas.getBoundingClientRect();
                    const x = (e.clientX - rect.left) / this.canvas.scale + this.canvas.offset.x;
                    const y = (e.clientY - rect.top) / this.canvas.scale + this.canvas.offset.y;

                    // 修复：优先使用 displayName，兼容 name 或 type
                    const operatorTitle = operator.displayName || operator.name || operator.type;

                    // 使用更新后的 addOperatorNode，传递整个 operator 对象以获取参数和端口配置
                    this.addOperatorNode(operator.type, x, y, operator);
                    this.saveState();
                    showToast(`已添加算子: ${operatorTitle}`, 'success');
                } catch (err) {
                    console.error('添加算子失败:', err);
                }
            }
        });
    }

    /**
     * 添加算子节点
     */
    addOperatorNode(type, x, y, data = null) {
        // 算子配置 (移植自 app.js)
        const operatorConfigs = {
            // 输入
            'ImageAcquisition': { title: '图像采集', color: '#52c41a', iconPath: 'M9 3L7.17 5H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2h-3.17L15 3H9zm3 15c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5z' },
            
            // 预处理
            'Filtering': { title: '滤波', color: '#1890ff', iconPath: 'M10 18h4v-2h-4v2zM3 6v2h18V6H3zm3 7h12v-2H6v2z' },
            'Thresholding': { title: '二值化', color: '#eb2f96', iconPath: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z' },
            'Morphology': { title: '形态学', color: '#fa8c16', iconPath: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm5 11h-4v4h-2v-4H7v-2h4V7h2v4h4v2z' },
            'ColorConversion': { title: '颜色空间转换', color: '#fa8c16', iconPath: 'M12 3c-4.97 0-9 4.03-9 9s4.03 9 9 9c.83 0 1.5-.67 1.5-1.5 0-.39-.15-.74-.39-1.01-.23-.26-.38-.61-.38-.99 0-.83.67-1.5 1.5-1.5H16c2.76 0 5-2.24 5-5 0-4.42-4.03-8-9-8zm-5.5 9c-.83 0-1.5-.67-1.5-1.5S5.67 9 6.5 9 8 9.67 8 10.5 7.33 12 6.5 12zm3-4C8.67 8 8 7.33 8 6.5S8.67 5 9.5 5s1.5.67 1.5 1.5S10.33 8 9.5 8zm5 0c-.83 0-1.5-.67-1.5-1.5S13.67 5 14.5 5s1.5.67 1.5 1.5S15.33 8 14.5 8zm3 4c-.83 0-1.5-.67-1.5-1.5S16.67 9 17.5 9s1.5.67 1.5 1.5-.67 1.5-1.5 1.5z' },
            'AdaptiveThreshold': { title: '自适应阈值', color: '#eb2f96', iconPath: 'M3 5H1v16c0 1.1.9 2 2 2h16v-2H3V5zm18-4H7c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V3c0-1.1-.9-2-2-2zm0 16H7V3h14v14z' },
            'HistogramEqualization': { title: '直方图均衡化', color: '#2f54eb', iconPath: 'M5 9.2h3V19H5zM10.6 5h2.8v14h-2.8zm5.6 8H19v6h-2.8z' },
            
            // 特征提取
            'EdgeDetection': { title: '边缘检测', color: '#722ed1', iconPath: 'M3 17h18v2H3zm0-7h18v5H3zm0-7h18v5H3z' },
            'SubpixelEdgeDetection': { title: '亚像素边缘', color: '#722ed1', iconPath: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z' },
            'BlobAnalysis': { title: 'Blob分析', color: '#13c2c2', iconPath: 'M12 2C6.47 2 2 6.47 2 12s4.47 10 10 10 10-4.47 10-10S17.53 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z' },
            
            // 检测 / 匹配
            'TemplateMatching': { title: '模板匹配', color: '#f5222d', iconPath: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 13h4v-2h-4v2zm0-4h4V9h-4v2z' },
            'ShapeMatching': { title: '形状匹配', color: '#52c41a', iconPath: 'M12 6c3.31 0 6 2.69 6 6s-2.69 6-6 6-6-2.69-6-6 2.69-6 6-6m0-2c-4.42 0-8 3.58-8 8s3.58 8 8 8 8-3.58 8-8-3.58-8-8-8z' },
            'Measurement': { title: '测量', color: '#2f54eb', iconPath: 'M21 6H3c-1.1 0-2 .9-2 2v8c0 1.1.9 2 2 2h18c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2zm0 10H3V8h2v4h2V8h2v4h2V8h2v4h2V8h2v8z' },
            'GeometricFitting': { title: '几何拟合', color: '#eb2f96', iconPath: 'M12 6c3.31 0 6 2.69 6 6s-2.69 6-6 6-6-2.69-6-6 2.69-6 6-6m0-2c-4.42 0-8 3.58-8 8s3.58 8 8 8 8-3.58 8-8-3.58-8-8-8z' },
            'ColorDetection': { title: '颜色检测', color: '#fa541c', iconPath: 'M12 3c-4.97 0-9 4.03-9 9s4.03 9 9 9c.83 0 1.5-.67 1.5-1.5 0-.39-.15-.74-.39-1.01-.23-.26-.38-.61-.38-.99 0-.83.67-1.5 1.5-1.5H16c2.76 0 5-2.24 5-5 0-4.42-4.03-8-9-8zm-5.5 9c-.83 0-1.5-.67-1.5-1.5S5.67 9 6.5 9 8 9.67 8 10.5 7.33 12 6.5 12zm3-4C8.67 8 8 7.33 8 6.5S8.67 5 9.5 5s1.5.67 1.5 1.5S10.33 8 9.5 8zm5 0c-.83 0-1.5-.67-1.5-1.5S13.67 5 14.5 5s1.5.67 1.5 1.5S15.33 8 14.5 8zm3 4c-.83 0-1.5-.67-1.5-1.5S16.67 9 17.5 9s1.5.67 1.5 1.5-.67 1.5-1.5 1.5z' },

            // AI
            'DeepLearning': { title: '深度学习', color: '#a0d911', iconPath: 'M20 6h-8l-2-2H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2zm-6 10H6v-2h8v2zm4-4H6v-2h12v2z' },
            
            // ROI / 标定
            'RoiManager': { title: 'ROI管理器', color: '#1890ff', iconPath: 'M3 5v4h2V5h4V3H5c-1.1 0-2 .9-2 2zm2 10H3v4c0 1.1.9 2 2 2h4v-2H5v-4zm14 4h-4v2h4c1.1 0 2-.9 2-2v-4h-2v4zm0-16h-4v2h4v4h2V5c0-1.1-.9-2-2-2z' },

            // 通信
            'SerialCommunication': { title: '串口通信', color: '#13c2c2', iconPath: 'M20 18c1.1 0 1.99-.9 1.99-2L22 6c0-1.1-.9-2-2-2H4c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2H0v2h24v-2h-4zM4 6h16v10H4V6z' },
            'ModbusCommunication': { title: 'Modbus通信', color: '#13c2c2', iconPath: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 14H9V8h2v8zm4 0h-2V8h2v8z' },
            'TcpCommunication': { title: 'TCP通信', color: '#13c2c2', iconPath: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z' },
            
            // 输出
            'ResultOutput': { title: '结果输出', color: '#595959', iconPath: 'M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z' },
            'DatabaseWrite': { title: '数据库写入', color: '#595959', iconPath: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zm.31-8.86c-1.77-.45-2.34-.94-2.34-1.67 0-.84.79-1.43 2.1-1.43 1.38 0 1.9.66 1.94 1.64h1.71c-.05-1.34-.87-2.57-2.49-2.97V5H10.9v1.69c-1.51.32-2.72 1.3-2.72 2.81 0 1.79 1.49 2.69 3.66 3.21 1.95.46 2.34 1.15 2.34 1.87 0 .53-.39 1.39-2.1 1.39-1.6 0-2.23-.72-2.32-1.64H8.04c.1 1.7 1.36 2.66 2.86 2.97V19h2.34v-1.67c1.52-.29 2.72-1.16 2.73-2.77-.01-2.2-1.9-2.96-3.66-3.42z' }
        };

        // 优先使用传入数据的配置，否则使用默认配置
        const defaultConfig = operatorConfigs[type] || { title: type, color: '#1890ff', iconPath: 'M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L5.03 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z' };
        
        // 确定标题：data.displayName > defaultConfig.title > data.name > type
        const title = data?.displayName || defaultConfig.title || data?.name || type;

        const nodeConfig = {
            title: title,
            color: defaultConfig.color,
            iconPath: data?.iconPath || defaultConfig.iconPath,
            // icon: data?.icon || defaultConfig.icon, // Removed emoji icon
            // 传递参数 - 使用深拷贝确保每个节点有独立的参数副本
            parameters: data?.parameters?.map(p => ({...p})) || [],
            // 传递端口配置 (如果有) 或使用默认值
            inputs: data?.inputPorts?.map(p => ({name: p.name, type: p.dataType})) || [{ name: 'input', type: 'Any' }],
            outputs: data?.outputPorts?.map(p => ({name: p.name, type: p.dataType})) || [{ name: 'output', type: 'Any' }]
        };

        const node = this.canvas.addNode(type, x, y, nodeConfig);
        return node;
    }

    /**
     * 获取端口
     */
    getPortAt(x, y) {
        for (const [id, node] of this.canvas.nodes) {
            // 输入端口
            for (let i = 0; i < node.inputs.length; i++) {
                const input = node.inputs[i];
                const px = node.x;
                const py = node.y + node.height / 2;
                const dist = Math.sqrt(Math.pow(x - px, 2) + Math.pow(y - py, 2));
                if (dist < 10) {
                    return { nodeId: id, portIndex: i, type: 'input', port: input };
                }
            }

            // 输出端口
            for (let i = 0; i < node.outputs.length; i++) {
                const output = node.outputs[i];
                const px = node.x + node.width;
                const py = node.y + node.height / 2;
                const dist = Math.sqrt(Math.pow(x - px, 2) + Math.pow(y - py, 2));
                if (dist < 10) {
                    return { nodeId: id, portIndex: i, type: 'output', port: output };
                }
            }
        }
        return null;
    }

    /**
     * 获取节点
     */
    getNodeAt(x, y) {
        for (const [id, node] of this.canvas.nodes) {
            if (x >= node.x && x <= node.x + node.width &&
                y >= node.y && y <= node.y + node.height) {
                return node;
            }
        }
        return null;
    }

    /**
     * 开始连线
     */
    startConnection(port, e) {
        this.isConnecting = true;
        this.connectionStart = port;
        
        const rect = this.canvas.canvas.getBoundingClientRect();
        this.connectionEnd = {
            x: (e.clientX - rect.left) / this.canvas.scale + this.canvas.offset.x,
            y: (e.clientY - rect.top) / this.canvas.scale + this.canvas.offset.y
        };
    }

    /**
     * 更新连线预览
     */
    updateConnectionPreview(e) {
        const rect = this.canvas.canvas.getBoundingClientRect();
        this.connectionEnd = {
            x: (e.clientX - rect.left) / this.canvas.scale + this.canvas.offset.x,
            y: (e.clientY - rect.top) / this.canvas.scale + this.canvas.offset.y
        };
        this.canvas.render();

        // 绘制预览线
        const startNode = this.canvas.nodes.get(this.connectionStart.nodeId);
        if (startNode) {
            const startX = startNode.x + (this.connectionStart.type === 'output' ? startNode.width : 0);
            const startY = startNode.y + startNode.height / 2;

            this.canvas.ctx.beginPath();
            this.canvas.ctx.strokeStyle = '#1890ff';
            this.canvas.ctx.lineWidth = 2;
            this.canvas.ctx.setLineDash([5, 5]);
            this.drawBezierCurve(
                (startX - this.canvas.offset.x) * this.canvas.scale,
                (startY - this.canvas.offset.y) * this.canvas.scale,
                (this.connectionEnd.x - this.canvas.offset.x) * this.canvas.scale,
                (this.connectionEnd.y - this.canvas.offset.y) * this.canvas.scale
            );
            this.canvas.ctx.stroke();
            this.canvas.ctx.setLineDash([]);
        }
    }

    /**
     * 结束连线
     */
    endConnection(e) {
        console.log('[DEBUG endConnection] === START CONNECTION ===');
        console.log('[DEBUG endConnection] connectionStart:', JSON.stringify(this.connectionStart));
        
        const rect = this.canvas.canvas.getBoundingClientRect();
        const x = (e.clientX - rect.left) / this.canvas.scale + this.canvas.offset.x;
        const y = (e.clientY - rect.top) / this.canvas.scale + this.canvas.offset.y;

        console.log(`[DEBUG endConnection] Mouse position: x=${x}, y=${y}`);

        const endPort = this.getPortAt(x, y);
        
        console.log('[DEBUG endConnection] getPortAt result:', JSON.stringify(endPort));
        console.log('[DEBUG endConnection] connectionEnd will be:', JSON.stringify({x, y, port: endPort}));

        if (endPort && endPort.type !== this.connectionStart.type) {
            // 确保从输出连接到输入
            const source = this.connectionStart.type === 'output' ? this.connectionStart : endPort;
            const target = this.connectionStart.type === 'input' ? this.connectionStart : endPort;

            // 检查端口数据类型兼容性
            if (source.port.type !== 'any' && target.port.type !== 'any' && source.port.type !== target.port.type) {
                showToast(`类型不匹配: ${source.port.type} -> ${target.port.type}`, 'warning');
                this.cancelConnection();
                return;
            }

            // 检查是否已存在连接
            const exists = this.canvas.connections.some(conn =>
                conn.source === source.nodeId &&
                conn.target === target.nodeId &&
                conn.sourcePort === source.portIndex &&
                conn.targetPort === target.portIndex
            );

            console.log('[DEBUG endConnection] Source:', JSON.stringify(source));
            console.log('[DEBUG endConnection] Target:', JSON.stringify(target));
            console.log('[DEBUG endConnection] Connection exists:', exists);
            console.log('[DEBUG endConnection] Same node check:', source.nodeId === target.nodeId);

            if (!exists && source.nodeId !== target.nodeId) {
                console.log(`[DEBUG endConnection] Adding connection: ${source.nodeId}:${source.portIndex} -> ${target.nodeId}:${target.portIndex}`);
                this.canvas.addConnection(source.nodeId, source.portIndex, target.nodeId, target.portIndex);
                this.saveState();
                showToast('连接成功', 'success');
            } else {
                console.log('[DEBUG endConnection] Connection skipped - exists or invalid');
                showToast('连接已存在或无效', 'warning');
            }
        } else {
            console.log('[DEBUG endConnection] No valid end port found or same type port');
        }

        console.log('[DEBUG endConnection] === END CONNECTION ===');
        this.cancelConnection();
    }

    /**
     * 取消连线
     */
    cancelConnection() {
        this.isConnecting = false;
        this.connectionStart = null;
        this.connectionEnd = null;
        this.canvas.render();
    }

    /**
     * 开始框选
     */
    startSelection(e) {
        this.isSelecting = true;
        const rect = this.canvas.canvas.getBoundingClientRect();
        this.selectionStart = {
            x: e.clientX - rect.left,
            y: e.clientY - rect.top
        };

        // 创建选择框
        this.selectionBox = document.createElement('div');
        this.selectionBox.className = 'flow-selection-box';
        this.selectionBox.style.position = 'absolute';
        this.selectionBox.style.border = '2px dashed #1890ff';
        this.selectionBox.style.background = 'rgba(24, 144, 255, 0.1)';
        this.selectionBox.style.pointerEvents = 'none';
        this.canvas.canvas.parentElement.appendChild(this.selectionBox);
    }

    /**
     * 更新选择框
     */
    updateSelectionBox(e) {
        if (!this.selectionBox) return;

        const rect = this.canvas.canvas.getBoundingClientRect();
        const currentX = e.clientX - rect.left;
        const currentY = e.clientY - rect.top;

        const left = Math.min(this.selectionStart.x, currentX);
        const top = Math.min(this.selectionStart.y, currentY);
        const width = Math.abs(currentX - this.selectionStart.x);
        const height = Math.abs(currentY - this.selectionStart.y);

        this.selectionBox.style.left = `${left}px`;
        this.selectionBox.style.top = `${top}px`;
        this.selectionBox.style.width = `${width}px`;
        this.selectionBox.style.height = `${height}px`;
    }

    /**
     * 结束框选
     */
    endSelection() {
        if (this.selectionBox) {
            const boxRect = this.selectionBox.getBoundingClientRect();
            const canvasRect = this.canvas.canvas.getBoundingClientRect();

            // 计算选择框在画布坐标系中的范围
            const left = (boxRect.left - canvasRect.left) / this.canvas.scale + this.canvas.offset.x;
            const top = (boxRect.top - canvasRect.top) / this.canvas.scale + this.canvas.offset.y;
            const right = left + boxRect.width / this.canvas.scale;
            const bottom = top + boxRect.height / this.canvas.scale;

            // 选择框内的节点
            for (const [id, node] of this.canvas.nodes) {
                if (node.x >= left && node.x + node.width <= right &&
                    node.y >= top && node.y + node.height <= bottom) {
                    this.multiSelectedNodes.add(id);
                }
            }

            this.selectionBox.remove();
            this.selectionBox = null;
        }

        this.isSelecting = false;
        this.selectionStart = null;
        this.updateSelection();
    }

    /**
     * 选择节点
     */
    selectNode(nodeId) {
        this.multiSelectedNodes.add(nodeId);
        this.canvas.selectedNode = nodeId;
        this.updateSelection();
    }

    /**
     * 切换节点选择
     */
    toggleNodeSelection(nodeId) {
        if (this.multiSelectedNodes.has(nodeId)) {
            this.multiSelectedNodes.delete(nodeId);
            if (this.canvas.selectedNode === nodeId) {
                this.canvas.selectedNode = null;
            }
        } else {
            this.multiSelectedNodes.add(nodeId);
            this.canvas.selectedNode = nodeId;
        }
        this.updateSelection();
    }

    /**
     * 清除选择
     */
    clearSelection() {
        this.multiSelectedNodes.clear();
        this.canvas.selectedNode = null;
        this.updateSelection();
    }

    /**
     * 全选
     */
    selectAll() {
        this.multiSelectedNodes.clear();
        for (const id of this.canvas.nodes.keys()) {
            this.multiSelectedNodes.add(id);
        }
        this.updateSelection();
        showToast(`已选择 ${this.multiSelectedNodes.size} 个节点`, 'info');
    }

    /**
     * 更新选择显示
     */
    updateSelection() {
        this.canvas.render();
    }

    /**
     * 复制选中节点
     */
    copySelectedNodes() {
        if (this.multiSelectedNodes.size === 0) return;

        this.copiedNodes = [];
        for (const nodeId of this.multiSelectedNodes) {
            const node = this.canvas.nodes.get(nodeId);
            if (node) {
                this.copiedNodes.push({ ...node });
            }
        }

        showToast(`已复制 ${this.copiedNodes.length} 个节点`, 'success');
    }

    /**
     * 粘贴节点
     */
    pasteNodes() {
        if (this.copiedNodes.length === 0) {
            showToast('剪贴板为空', 'warning');
            return;
        }

        const offset = 20;
        this.clearSelection();

        this.copiedNodes.forEach(node => {
            const newNode = this.canvas.addNode(node.type, node.x + offset, node.y + offset, {
                title: node.title,
                color: node.color,
                inputs: node.inputs,
                outputs: node.outputs
            });
            this.multiSelectedNodes.add(newNode.id);
        });

        this.saveState();
        showToast(`已粘贴 ${this.copiedNodes.length} 个节点`, 'success');
    }

    /**
     * 删除选中节点
     */
    deleteSelectedNodes() {
        if (this.multiSelectedNodes.size === 0) return;

        const count = this.multiSelectedNodes.size;
        for (const nodeId of this.multiSelectedNodes) {
            this.canvas.removeNode(nodeId);
        }

        this.multiSelectedNodes.clear();
        this.canvas.selectedNode = null;
        this.saveState();
        showToast(`已删除 ${count} 个节点`, 'success');
    }

    /**
     * 保存状态（历史记录）
     */
    saveState() {
        const state = {
            nodes: Array.from(this.canvas.nodes.entries()),
            connections: [...this.canvas.connections]
        };

        // 移除当前指针之后的历史
        this.history = this.history.slice(0, this.historyIndex + 1);

        // 添加新状态
        this.history.push(JSON.stringify(state));

        // 限制历史大小
        if (this.history.length > this.maxHistorySize) {
            this.history.shift();
        } else {
            this.historyIndex++;
        }
    }

    /**
     * 撤销
     */
    undo() {
        if (this.historyIndex > 0) {
            this.historyIndex--;
            this.restoreState();
            showToast('已撤销', 'info');
        } else {
            showToast('没有可撤销的操作', 'warning');
        }
    }

    /**
     * 重做
     */
    redo() {
        if (this.historyIndex < this.history.length - 1) {
            this.historyIndex++;
            this.restoreState();
            showToast('已重做', 'info');
        } else {
            showToast('没有可重做的操作', 'warning');
        }
    }

    /**
     * 恢复状态
     */
    restoreState() {
        const state = JSON.parse(this.history[this.historyIndex]);
        this.canvas.nodes = new Map(state.nodes);
        this.canvas.connections = state.connections;
        this.canvas.render();
    }

    /**
     * 绘制贝塞尔曲线
     */
    drawBezierCurve(x1, y1, x2, y2) {
        const cp1x = x1 + (x2 - x1) / 2;
        const cp1y = y1;
        const cp2x = x1 + (x2 - x1) / 2;
        const cp2y = y2;

        this.canvas.ctx.moveTo(x1, y1);
        this.canvas.ctx.bezierCurveTo(cp1x, cp1y, cp2x, cp2y, x2, y2);
    }
}

export default FlowEditorInteraction;
