/**
 * OperatorLibraryPanel - 算子库面板组件
 * Sprint 4: S4-002 实现
 * 
 * 功能：
 * - 算子分类树形列表
 * - 拖拽算子到画布
 * - 算子搜索过滤
 * - 算子详情预览
 */

import TreeView from '../../shared/components/treeView.js';
import httpClient from '../../core/messaging/httpClient.js';
import { showToast, createInput } from '../../shared/components/uiComponents.js';

export class OperatorLibraryPanel {
    constructor(containerId) {
        this.container = document.getElementById(containerId);
        this.treeView = null;
        this.operators = [];
        this.filteredOperators = [];
        this.categories = new Map();
        this.metadataByType = new Map();

        // 事件回调
        this.onOperatorDragStart = null;
        this.onOperatorSelected = null;

        // 【新增】展开状态持久化
        this.storageKey = 'operator-library-expanded-categories';

        this.initialize();
    }

    /**
     * 初始化面板
     */
    initialize() {
        // 【修复】页面加载时清理可能残留的全局拖拽数据
        if (window.__draggingOperatorData) {
            window.__draggingOperatorData = null;
        }
        
        this.renderUI();
        this.initializeTreeView();
        this.loadOperators();
    }

    /**
     * 渲染UI结构
     */
    renderUI() {
        this.container.innerHTML = `
            <div class="operator-library-wrapper">
                <!-- 搜索栏 -->
                <div class="library-search">
                    <input type="text" 
                           id="operator-search" 
                           class="cv-input" 
                           placeholder="搜索算子..."
                           autocomplete="off">
                    <button id="btn-clear-search" class="cv-btn cv-btn-icon" title="清除搜索">✕</button>
                </div>
                
                <!-- 算子树形列表 -->
                <div class="library-tree" id="library-tree"></div>
                
                <!-- 算子详情预览 -->
                <div class="operator-preview" id="operator-preview">
                    <div class="preview-placeholder">
                        <span class="preview-svg-icon">${this.getSvgIcon('M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L5.03 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z', '0 0 24 24')}</span>
                        <p>选择一个算子查看详情</p>
                    </div>
                </div>
                
                <!-- 快捷操作 -->
                <div class="library-actions">
                    <button id="btn-expand-all" class="cv-btn cv-btn-secondary" title="展开全部">📂</button>
                    <button id="btn-collapse-all" class="cv-btn cv-btn-secondary" title="折叠全部">📁</button>
                    <button id="btn-refresh" class="cv-btn cv-btn-secondary" title="刷新列表">🔄</button>
                </div>
            </div>
        `;
        
        this.bindSearchEvents();
        this.bindActionEvents();
    }

    /**
     * 初始化树形控件
     */
    initializeTreeView() {
        const treeContainer = this.container.querySelector('#library-tree');
        
        this.treeView = new TreeView(treeContainer, {
            selectable: true,
            multiSelect: false,
            draggable: false,
            onSelect: (node) => {
                if (node.type === 'operator') {
                    this.showOperatorPreview(node.data);
                    if (this.onOperatorSelected) {
                        this.onOperatorSelected(node.data);
                    }
                }
            },
            onExpand: () => this.saveExpandedState(),
            onCollapse: () => this.saveExpandedState(),
            renderNode: (node, element) => {
                // 自定义渲染算子节点
                if (node.type === 'operator') {
                    console.log('[OperatorLibrary] renderNode 渲染算子:', node.label);
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
                    
                    // 添加拖拽预览效果
                    element.addEventListener('dragstart', (e) => {
                        element.classList.add('dragging-shadow');
                    });
                    
                    element.addEventListener('dragend', (e) => {
                        element.classList.remove('dragging-shadow');
                    });
                    
                    console.log('[OperatorLibrary] 算子元素设置完成, draggable:', element.draggable, 'classList:', element.className);
                } else {
                    // 【新增】分类节点 - 自定义渲染包含展开/收起按钮
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
                            e.preventDefault();
                            e.stopPropagation();
                            console.log('[OperatorLibrary] Toggle clicked, node.id:', node.id);
                            
                            // 通过 id 查找正确的节点对象
                            const actualNode = this.treeView.findNode(node.id);
                            
                            if (actualNode) {
                                this.treeView.toggleNode(actualNode);
                                console.log('[OperatorLibrary] After toggle, expandedNodes:', Array.from(this.treeView.expandedNodes));
                            }
                            return false;
                        };
                        
                        if (toggle) {
                            toggle.onclick = toggleHandler;
                            console.log('[OperatorLibrary] Toggle onclick bound for:', node.id);
                        }
                        
                        // 点击分类内容也可以展开/收起
                        if (wrapper) {
                            wrapper.style.cursor = 'pointer';
                            wrapper.onclick = toggleHandler;
                            console.log('[OperatorLibrary] Wrapper onclick bound for:', node.id);
                        }
                    }
                }
                // 不返回 element，因为已经在原对象上修改
                // treeView.js 会检查返回值，如果不返回则使用原 element
            }
        });

        // 使用事件委托处理拖拽 - 修复拖拽失效问题
        // 事件绑定在容器上，TreeView 重绘不会导致事件丢失
        treeContainer.addEventListener('dragstart', (e) => {
            console.log('[OperatorLibrary] dragstart 事件触发', e.target);
            
            const operatorEl = e.target.closest('.operator-draggable');
            if (!operatorEl) {
                console.log('[OperatorLibrary] 未找到 .operator-draggable 元素');
                return;
            }
            console.log('[OperatorLibrary] 找到算子元素:', operatorEl);
            
            // 从父级 li 元素获取节点 ID
            const li = operatorEl.closest('[data-id]');
            if (!li) {
                console.log('[OperatorLibrary] 未找到父级 li 元素');
                return;
            }
            console.log('[OperatorLibrary] 找到 li 元素, data-id:', li.dataset.id);
            
            const nodeId = li.dataset.id;
            // 从 treeView 中查找对应的节点数据
            const node = this.treeView.findNode(nodeId);
            console.log('[OperatorLibrary] 查找节点结果:', node);
            
            if (node && node.data) {
                // 设置数据传递
                e.dataTransfer.setData('application/json', JSON.stringify(node.data));
                e.dataTransfer.effectAllowed = 'copy';
                
                // 【修复】备份数据到全局变量，防止 WebView2 环境下 dataTransfer 数据丢失
                window.__draggingOperatorData = node.data;
                console.log('[OperatorLibrary] 开始拖拽算子:', node.data.type);

                operatorEl.classList.add('dragging');
                
                if (this.onOperatorDragStart) {
                    this.onOperatorDragStart(node.data);
                }
                
                // 监听拖拽结束事件，移除样式
                const onDragEnd = () => {
                    operatorEl.classList.remove('dragging');
                    // 延迟清理全局变量，确保 drop 事件能读取到
                    setTimeout(() => {
                        if (window.__draggingOperatorData === node.data) {
                            window.__draggingOperatorData = null;
                        }
                    }, 500);
                    operatorEl.removeEventListener('dragend', onDragEnd);
                };
                operatorEl.addEventListener('dragend', onDragEnd);
            }
        });
    }

    /**
     * 加载算子列表
     */
    async loadOperators() {
        try {
            const operators = await this.loadOperatorsFromMetadata();
            this.operators = operators;
            this.filteredOperators = operators;
            this.renderOperatorTree();
            showToast(`已加载 ${operators.length} 个算子`, 'success');
        } catch (error) {
            console.error('[OperatorLibraryPanel] 加载算子失败:', error);
            // 使用默认算子数据
            this.operators = this.getDefaultOperators();
            this.filteredOperators = this.operators;
            this.renderOperatorTree();
            showToast('使用默认算子数据', 'warning');
        }
    }

    async loadOperatorsFromMetadata() {
        try {
            const types = await httpClient.get('/operators/types');
            if (Array.isArray(types) && types.length > 0) {
                const operators = await Promise.all(types.map(async (type) => {
                    const typeIdentifier = typeof type === 'string'
                        ? type
                        : (type?.name || type?.Name || type?.type || type?.Type || String(type));
                    try {
                        const metadata = await httpClient.get(`/operators/${encodeURIComponent(typeIdentifier)}/metadata`);
                        const normalized = this.normalizeOperatorMetadata(metadata, typeIdentifier);
                        if (normalized) {
                            this.metadataByType.set(normalized.type, normalized);
                        }
                        return normalized;
                    } catch (error) {
                        console.warn('[OperatorLibraryPanel] 加载算子元数据失败:', typeIdentifier, error);
                        return null;
                    }
                }));

                const validOperators = operators.filter(Boolean);
                if (validOperators.length > 0) {
                    return validOperators;
                }
            }
        } catch (error) {
            console.warn('[OperatorLibraryPanel] 获取算子类型失败，回退到算子库接口:', error);
        }

        const operators = await httpClient.get('/operators/library');
        return operators.map(operator => this.normalizeOperatorMetadata(operator, operator.type || operator.Type)).filter(Boolean);
    }

    normalizeOperatorMetadata(metadata, fallbackType = '') {
        if (!metadata || typeof metadata !== 'object') {
            return null;
        }

        const type = String(metadata.type || metadata.Type || fallbackType || '').trim();
        if (!type) {
            return null;
        }

        const category = metadata.category || metadata.Category || '其他';
        const displayName = metadata.displayName || metadata.DisplayName || metadata.name || metadata.Name || type;
        const parameters = metadata.parameters || metadata.Parameters || [];
        const inputPorts = metadata.inputPorts || metadata.InputPorts || [];
        const outputPorts = metadata.outputPorts || metadata.OutputPorts || [];

        return {
            ...metadata,
            type,
            category,
            displayName,
            description: metadata.description || metadata.Description || '暂无描述',
            parameters,
            inputPorts,
            outputPorts,
            inputType: metadata.inputType || metadata.InputType || (inputPorts[0]?.dataType || inputPorts[0]?.DataType || '图像'),
            outputType: metadata.outputType || metadata.OutputType || (outputPorts[0]?.dataType || outputPorts[0]?.DataType || '图像/数据')
        };
    }

    /**
     * 获取默认算子数据
     */
    getDefaultOperators() {
        return [
            { 
                type: 'ImageAcquisition', 
                displayName: '图像采集', 
                category: '输入', 
                icon: '📷', 
                description: '从相机或文件获取图像',
                parameters: [
                    { name: 'sourceType', displayName: '采集源', type: 'enum', dataType: 'enum', defaultValue: 'camera', options: [{label: '相机', value: 'camera'}, {label: '文件', value: 'file'}] },
                    { name: 'cameraId', displayName: '相机', type: 'cameraBinding', dataType: 'cameraBinding', defaultValue: '' },
                    { name: 'filePath', displayName: '文件路径', type: 'file', dataType: 'file', defaultValue: '', description: '支持 .bmp, .png, .jpg' }
                ]
            },
            { 
                type: 'Filtering', 
                displayName: '滤波', 
                category: '预处理', 
                icon: '🔍', 
                description: '图像滤波降噪处理',
                parameters: [
                    { name: 'method', displayName: '滤波方法', type: 'enum', dataType: 'enum', defaultValue: 'gaussian', options: [{label: '高斯滤波', value: 'gaussian'}, {label: '中值滤波', value: 'median'}, {label: '均值滤波', value: 'mean'}] },
                    { name: 'kernelSize', displayName: '核大小', type: 'int', dataType: 'int', defaultValue: 3, min: 3, max: 15, description: '必须为奇数' }
                ]
            },
            { 
                type: 'EdgeDetection', 
                displayName: '边缘检测', 
                category: '特征提取', 
                icon: '〰️', 
                description: '检测图像边缘特征',
                parameters: [
                    { name: 'algorithm', displayName: '算子类型', type: 'enum', dataType: 'enum', defaultValue: 'canny', options: [{label: 'Canny', value: 'canny'}, {label: 'Sobel', value: 'sobel'}, {label: 'Laplacian', value: 'laplacian'}] },
                    { name: 'threshold1', displayName: '阈值 1', type: 'int', dataType: 'int', defaultValue: 50, min: 0, max: 255 },
                    { name: 'threshold2', displayName: '阈值 2', type: 'int', dataType: 'int', defaultValue: 150, min: 0, max: 255 }
                ]
            },
            { 
                type: 'Thresholding', 
                displayName: '二值化', 
                category: '预处理', 
                icon: '⚫', 
                description: '图像阈值分割',
                parameters: [
                    { name: 'method', displayName: '阈值方法', type: 'enum', dataType: 'enum', defaultValue: 'fixed', options: [{label: '固定阈值', value: 'fixed'}, {label: 'Otsu', value: 'otsu'}, {label: 'Adaptive', value: 'adaptive'}] },
                    { name: 'threshold', displayName: '阈值', type: 'int', dataType: 'int', defaultValue: 128, min: 0, max: 255 },
                    { name: 'invert', displayName: '反转结果', type: 'bool', dataType: 'bool', defaultValue: false }
                ]
            },
            { 
                type: 'Morphology', 
                displayName: '形态学', 
                category: '预处理', 
                icon: '🔄', 
                description: '腐蚀、膨胀、开闭运算',
                parameters: [
                    { name: 'operation', displayName: '操作类型', type: 'enum', dataType: 'enum', defaultValue: 'erode', options: [{label: '腐蚀', value: 'erode'}, {label: '膨胀', value: 'dilate'}, {label: '开运算', value: 'open'}, {label: '闭运算', value: 'close'}] },
                    { name: 'kernelSize', displayName: '核大小', type: 'int', dataType: 'int', defaultValue: 3, min: 3, max: 21 },
                    { name: 'iterations', displayName: '迭代次数', type: 'int', dataType: 'int', defaultValue: 1, min: 1, max: 10 }
                ]
            },
            { 
                type: 'BlobAnalysis', 
                displayName: 'Blob分析', 
                category: '特征提取', 
                icon: '🔵', 
                description: '连通区域分析',
                parameters: [
                    { name: 'minArea', displayName: '最小面积', type: 'int', dataType: 'int', defaultValue: 100, min: 0 },
                    { name: 'maxArea', displayName: '最大面积', type: 'int', dataType: 'int', defaultValue: 100000, min: 0 },
                    { name: 'color', displayName: '目标颜色', type: 'enum', dataType: 'enum', defaultValue: 'white', options: [{label: '白色', value: 'white'}, {label: '黑色', value: 'black'}] }
                ]
            },
            { 
                type: 'TemplateMatching', 
                displayName: '模板匹配', 
                category: '检测', 
                icon: '🎯', 
                description: '图像模板匹配定位',
                parameters: [
                    { name: 'method', displayName: '匹配方法', type: 'enum', dataType: 'enum', defaultValue: 'ncc', options: [{label: '归一化相关 (NCC)', value: 'ncc'}, {label: '平方差 (SQDIFF)', value: 'sqdiff'}] },
                    { name: 'threshold', displayName: '匹配分数阈值', type: 'float', dataType: 'float', defaultValue: 0.8, min: 0.1, max: 1.0 },
                    { name: 'maxMatches', displayName: '最大匹配数', type: 'int', dataType: 'int', defaultValue: 1, min: 1, max: 100 }
                ]
            },
            { 
                type: 'Measurement', 
                displayName: '测量', 
                category: '检测', 
                icon: '📏', 
                description: '几何尺寸测量',
                parameters: [
                    { name: 'type', displayName: '测量类型', type: 'enum', dataType: 'enum', defaultValue: 'distance', options: [{label: '距离', value: 'distance'}, {label: '角度', value: 'angle'}, {label: '圆径', value: 'radius'}] }    
                ]
            },
            { 
                type: 'DeepLearning', 
                displayName: '深度学习', 
                category: 'AI检测', 
                icon: '🧠', 
                description: 'AI缺陷检测',
                parameters: [
                    { name: 'modelPath', displayName: '模型路径', type: 'file', dataType: 'file', defaultValue: '' },
                    { name: 'confidence', displayName: '置信度阈值', type: 'float', dataType: 'float', defaultValue: 0.5, min: 0.0, max: 1.0 }
                ]
            },
            { 
                type: 'ResultOutput', 
                displayName: '结果输出', 
                category: '输出', 
                icon: '📤', 
                description: '输出检测结果',
                parameters: [
                    { name: 'format', displayName: '输出格式', type: 'enum', dataType: 'enum', defaultValue: 'json', options: [{label: 'JSON', value: 'json'}, {label: 'CSV', value: 'csv'}, {label: 'Text', value: 'text'}] },
                    { name: 'saveToFile', displayName: '保存到文件', type: 'bool', dataType: 'bool', defaultValue: true }
                ]
            },
            { 
                type: 'ResultJudgment', 
                displayName: '结果判定', 
                category: '流程控制', 
                icon: '⚖️', 
                description: '通用判定逻辑（数量/范围/阈值），输出OK/NG结果',
                inputPorts: [
                    { name: 'Value', displayName: '输入值', dataType: 'Any', isRequired: true },
                    { name: 'Confidence', displayName: '置信度', dataType: 'Float', isRequired: false }
                ],
                outputPorts: [
                    { name: 'JudgmentResult', displayName: '判定结果', dataType: 'String' },
                    { name: 'IsOk', displayName: '是否OK', dataType: 'Boolean' },
                    { name: 'Details', displayName: '详细信息', dataType: 'String' }
                ],
                parameters: [
                    { name: 'FieldName', displayName: '判定字段', type: 'string', dataType: 'string', defaultValue: 'Value' },
                    { name: 'Condition', displayName: '判定条件', type: 'enum', dataType: 'enum', defaultValue: 'Equal', options: [
                        {label: '等于', value: 'Equal'}, {label: '大于', value: 'GreaterThan'}, {label: '小于', value: 'LessThan'}, {label: '范围内', value: 'Range'}
                    ]},
                    { name: 'ExpectValue', displayName: '期望值', type: 'string', dataType: 'string', defaultValue: '1' }
                ]
            }
        ];
    }

    /**
     * 获取SVG图标内容
     */
    getSvgIcon(path, viewBox = "0 0 24 24") {
        return `<svg viewBox="${viewBox}" width="16" height="16" fill="currentColor"><path d="${path}"/></svg>`;
    }

    /**
     * 获取算子图标的 SVG Path 字符串
     * 按 type 匹配，未匹配则尝试 category，最后使用默认图标
     */
    getOperatorIconPath(type, category = null) {
        // 工业风格 SVG 路径定义 (Material Design / Fluent 风格)
        const icons = {
            // 输入
            'ImageAcquisition': 'M9 3L7.17 5H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2h-3.17L15 3H9zm3 15c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5z', // 相机
            'ReadImage': 'M20 6h-8l-2-2H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2zm0 12H4V8h16v10zM8 13.01l1.41 1.41L11 12.83l1.59 1.58L14 13l-3-3-3 3z', // 文件夹图片
            
            // 预处理 (部分未被 P2 修正覆盖的基础类)
            'Morphology': 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm5 11h-4v4h-2v-4H7v-2h4V7h2v4h4v2z', // 加号/形态
            'ColorConversion': 'M12 3c-4.97 0-9 4.03-9 9s4.03 9 9 9c.83 0 1.5-.67 1.5-1.5 0-.39-.15-.74-.39-1.01-.23-.26-.38-.61-.38-.99 0-.83.67-1.5 1.5-1.5H16c2.76 0 5-2.24 5-5 0-4.42-4.03-8-9-8zm-5.5 9c-.83 0-1.5-.67-1.5-1.5S5.67 9 6.5 9 8 9.67 8 10.5 7.33 12 6.5 12zm3-4C8.67 8 8 7.33 8 6.5S8.67 5 9.5 5s1.5.67 1.5 1.5S10.33 8 9.5 8zm5 0c-.83 0-1.5-.67-1.5-1.5S13.67 5 14.5 5s1.5.67 1.5 1.5S15.33 8 14.5 8zm3 4c-.83 0-1.5-.67-1.5-1.5S16.67 9 17.5 9s1.5.67 1.5 1.5-.67 1.5-1.5 1.5z', // 调色板
            'Preprocessing': 'M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L5.03 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12-.22.37-.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z', // 齿轮
            'Undistort': 'M21 5c-1.11-.35-2.33-.5-3.5-.5-1.95 0-4.05 0.4-5.5 1.5-1.45-1.1-3.55-1.5-5.5-1.5S2.45 4.9 1 6v14.65c0 .25.25.5.5.5.1 0 .15-.05.25-.05C3.1 20.45 5.05 20 6.5 20c1.95 0 4.05.4 5.5 1.5 1.35-.85 3.8-1.5 5.5-1.5 1.65 0 3.35.3 4.75 1.05.1.05.15.05.25.05.25 0 .5-.25.5-.5V6c-.6-.45-1.25-.75-2-1zm0 13.5c-1.1-.35-2.3-.5-3.5-.5-1.7 0-4.15.65-5.5 1.5V8c1.35-.85 3.8-1.5 5.5-1.5 1.2 0 2.4.15 3.5.5v11.5z', // 书本/矫正
            
            // 特征提取
            'EdgeDetection': 'M3 17h18v2H3zm0-7h18v5H3zm0-7h18v5H3z', // 边缘/线条
            'SubpixelEdgeDetection': 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z', // 精细目标
            'BlobAnalysis': 'M12 2C6.48 2 2 6.48 2 12s4.47 10 10 10 10-4.47 10-10S17.53 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z', // 斑点/圆形
            
            // ROI / 标定
            'RoiManager': 'M3 5v4h2V5h4V3H5c-1.1 0-2 .9-2 2zm2 10H3v4c0 1.1.9 2 2 2h4v-2H5v-4zm14 4h-4v2h4c1.1 0 2-.9 2-2v-4h-2v4zm0-16h-4v2h4v4h2V5c0-1.1-.9-2-2-2z', // 聚焦框
            'CameraCalibration': 'M9.4 16.6L4.8 12l4.6-4.6L8 6l-6 6 6 6 1.4-1.4zm5.2 0l4.6-4.6-4.6-4.6L16 6l6 6-6 6-1.4-1.4z', // 代码/校准
            'CoordinateTransform': 'M12 8c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2zm8.9 11c-.5-3.4-3.5-6-7-6.9v-2.2c1.4.3 2.8-.7 2.8-2.2 0-1.1-.9-2-2-2s-2 .9-2 2c0 1.4 1.4 2.5 2.8 2.2V12c-3.6.9-6.6 4-7 7H2v2h20v-2h-.1z', // 坐标系

            // 通信
            'SerialCommunication': 'M20 18c1.1 0 1.99-.9 1.99-2L22 6c0-1.1-.9-2-2-2H4c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2H0v2h24v-2h-4zM4 6h16v10H4V6z', // 显示器
            'ModbusCommunication': 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 14H9V8h2v8zm4 0h-2V8h2v8z', // 暂停/传输
            'TcpCommunication': 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z', // 地球/网络

            // 输出
            'ResultOutput': 'M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z', // 下载
            'DatabaseWrite': 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zm.31-8.86c-1.77-.45-2.34-.94-2.34-1.67 0-.84.79-1.43 2.1-1.43 1.38 0 1.9.66 1.94 1.64h1.71c-.05-1.34-.87-2.57-2.49-2.97V5H10.9v1.69c-1.51.32-2.72 1.3-2.72 2.81 0 1.79 1.49 2.69 3.66 3.21 1.95.46 2.34 1.15 2.34 1.87 0 .53-.39 1.39-2.1 1.39-1.6 0-2.23-.72-2.32-1.64H8.04c.1 1.7 1.36 2.66 2.86 2.97V19h2.34v-1.67c1.52-.29 2.72-1.16 2.73-2.77-.01-2.2-1.9-2.96-3.66-3.42z', // 数据库/货币符号 (暂代)
            
            // === 缺失算子图标补充 ===
            
            // 预处理类（7种）
            'ContourDetection': 'M3 5v14h18V5H3zm16 12H5V7h14v10zM7 9h4v4H7V9zm6 0h4v4h-4V9z', // 轮廓检测 - 嵌套矩形边框
            'MedianBlur': 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 16c-3.31 0-6-2.69-6-6s2.69-6 6-6 6 2.69 6 6-2.69 6-6 6zm0-8c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z', // 中值滤波 - 水滴模糊
            'BilateralFilter': 'M3 7v10h18V7H3zm2 2h5v6H5V9zm7 0h5v6h-5V9z', // 双边滤波 - 双层滤波
            'ImageResize': 'M12 2l4 4h-3v4h-2V6H8l4-4zM4 14v4h4v-2H6v-2H4zm16 0v4h-4v-2h2v-2h2z', // 图像缩放 - 四角箭头
            'ImageCrop': 'M17 15h2v-2h-2v2zM7 11v6h6v-2H9v-4H7zM5 7v4h2V9h4V7H5zM17 7v4h2V9h-2V7z', // 图像裁剪 - 裁切框
            'ImageRotate': 'M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46C19.54 15.03 20 13.57 20 12c0-4.42-3.58-8-8-8zm0 14c-3.31 0-6-2.69-6-6 0-1.01.25-1.97.7-2.8L4.24 7.74C4.46 8.97 4 10.43 4 12c0 4.42 3.58 8 8 8v3l4-4-4-4v3z', // 图像旋转 - 循环箭头
            'PerspectiveTransform': 'M2 6l4-2 8 2 8-2v14l-8 2-8-2-4 2V6zm4 2v8l6 1.5V8.5L6 8z', // 透视变换 - 透视梯形
            
            // 检测/测量类（新增+修正）
            'CaliperTool': 'M2 2v20h4V11h6v3l5-4-5-4v3H6V2H2zm20 8v12h-2V10h2z', // 卡尺
            'WidthMeasurement': 'M2 6h2v12H2V6zm18 0h2v12h-2V6zM6 11h12v2H6v-2zM4 13l3 3v-2h10v2l3-3-3-3v2H7V9L4 13z', // 宽度测量
            'GapMeasurement': 'M2 4h4v16H2V4zm16 0h4v16h-4V4zM8 11h8v2H8v-2zm-1 2l3 3v-2h4v2l3-3-3-3v2H10V9L7 13z', // 间隙测量
            'AngleMeasurement': 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8 0-4.41 3.59-8 8-8s8 3.59 8 8-3.59 8-8 8zm-4.7-5.3l5.7-2.4 2.4-5.7-5.7 2.4-2.4 5.7zM14 11c-.55 0-1-.45-1-1s.45-1 1-1 1 .45 1 1-.45 1-1 1zM2 22h20v-2H2v2z', // 角度测量 (量角器)
            'CircleMeasurement': 'M12 4c-4.41 0-8 3.59-8 8s3.59 8 8 8 8-3.59 8-8-3.59-8-8-8zm0 14c-3.31 0-6-2.69-6-6s2.69-6 6-6 6 2.69 6 6-2.69 6-6 6zm1-6v4h-2v-4H8l4-4 4 4h-3z', // 圆测量
            'GeoMeasurement': 'M12 2L1 21h22L12 2zm0 3.83l7.65 13.17H4.35L12 5.83zM10 16h4v2h-4v-2zM12 11l2 3h-4l2-3z', // 几何测量
            'GeometricFitting': 'M20 7h-2V5h-2v2h-2V5h-2v2h-2V5H8v2H6V5H4v2H2v2h2v2H2v2h2v2H2v2h2v2h2v-2h2v2h2v-2h2v2h2v-2h2v2h2v-2h2v-2h-2v-2h2v-2h-2V9h2V7zm-4 8H8V9h8v6z M10 10.5c.83 0 1.5.67 1.5 1.5s-.67 1.5-1.5 1.5-1.5-.67-1.5-1.5.67-1.5 1.5-1.5zm4 0c.83 0 1.5.67 1.5 1.5s-.67 1.5-1.5 1.5-1.5-.67-1.5-1.5.67-1.5 1.5-1.5z', // 几何拟合
            'GeometricTolerance': 'M3 3h18v6H3V3zm2 2v2h14V5H5zm-2 6h8v10H3V11zm2 2v6h4v-6H5zm10-2h4v10h-4V11zm2 2v6h-2v-6h2z', // 几何公差
            'HistogramAnalysis': 'M5 19h14V5h2v16H3V5h2v14zM7 11h2v6H7v-6zm4-3h2v9h-2V8zm4-4h2v13h-2V4zm-8-3L8 3v4L4 4v4l4-3 5 5 6-6 1.4 1.4-7.4 7.4L8 6.8z', // 直方图分析
            'LineLineDistance': 'M2 4h16v2H2V4zm4 14h16v2H6v-2zM8 8h2v8H8V8zm-2 2l3 3v-2h8v2l3-3-3-3v2H9V9L6 11z', // 线线距离
            'PixelStatistics': 'M3 3h18v18H3V3zm2 2v4h4V5H5zm6 0v4h4V5h-4zm6 0v4h4V5h-4zM5 11v4h4v-4H5zm6 0v4h4v-4h-4zm6 0v4h4v-4h-4zM5 17v2h4v-2H5zm6 0v2h4v-2h-4zm6 0v2h4v-2h-4z', // 像素统计
            'PointLineDistance': 'M4 4h16v2H4V4zm8 5c1.1 0 2 .9 2 2s-.9 2-2 2-2-.9-2-2 .9-2 2-2z M11 13v6h2v-6h-2zM9 17l3 3 3-3V14h-2v3h-2v-3H9v3z', // 点线距离
            'SharpnessEvaluation': 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z M12 6c-3.31 0-6 2.69-6 6s2.69 6 6 6 6-2.69 6-6-2.69-6-6-6zm0 10c-2.21 0-4-1.79-4-4s1.79-4 4-4 4 1.79 4 4-1.79 4-4 4z M5.5 12h3M15.5 12h3M12 5.5v3M12 15.5v3', // 清晰度评估
            'LineMeasurement': 'M2 20h2v-3l12.42-12.42c.39-.39.39-1.02 0-1.41l-1.17-1.17c-.39-.39-1.02-.39-1.41 0L3 14.42V18h3.58zM15 4l5 5-11 11H4v-5L15 4z', // 直线测量 (已优化)
            'ContourMeasurement': 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z M4.5 9h2c.83 0 1.5.67 1.5 1.5v3c0 .83-.67 1.5-1.5 1.5h-2 M19.5 9h-2c-.83 0-1.5.67-1.5 1.5v3c0 .83.67 1.5 1.5 1.5h2 M9 4.5v2c0 .83.67 1.5 1.5 1.5h3c.83 0 1.5-.67 1.5-1.5v-2 M9 19.5v-2c0-.83.67-1.5 1.5-1.5h3c.83 0 1.5.67 1.5 1.5v2', // 轮廓测量 (已优化)
            'BlobLabeling': 'M6 2h4v4H6V2zm8 0h4v4h-4V2z M10 21V11l-3 3-1.4-1.4L12 6.2l6.4 6.4-1.4 1.4-3-3V21h-4z', // Blob标记
            
            // AI 检测类（4种）
            'DeepLearning': 'M13 3c-4.97 0-9 4.03-9 9 0 2.58 1.08 4.9 2.82 6.53L5.4 19.95c-1.87-1.87-3-4.45-3-7.3 0-5.7 4.62-10.32 10.3-10.32S23 6.95 23 12.65c0 2.85-1.13 5.43-3 7.3l-1.42-1.42C20.32 16.9 21.4 14.58 21.4 12.65c0-4.97-4.03-9-9-9zm-4 4v3H6v2h3v3h2v-3h3V9h-3V6H9zm10 4v4h-2v2h2v3h2v-3h2v-2h-2v-4h-2z M12 10c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z M8 18h8v2H8v-2z', // 深度学习 (神经元/大脑)
            'DualModalVoting': 'M7 3h4v4H7V3zm6 0h4v4h-4V3z M4 9h6v2H4V9zm10 0h6v2h-6V9z M10 14h4v6h-4v-6z M12 13L9 11l6-2-3 4z', // 双模态投票 (融合箭头)
            'EdgePairDefect': 'M3 4h18v2H3V4zm0 14h18v2H3v-2z M14.12 9.88l1.41-1.41L12 5l-3.54 3.54 1.41 1.41L12 7.83l2.12 2.05z M9.88 14.12L8.46 15.54 12 19l3.54-3.54-1.41-1.41L12 16.17l-2.12-2.05z M9 10L6 12l3 2v-4zm6 0v4l3-2-3-2z', // 边缘对缺陷
            'SurfaceDefectDetection': 'M20 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zM4 18V6h16v12H4z M9 9h2v6H9V9zm7 0h-2L11 12l3 3h2l-3-3 3-3z', // 表面缺陷检测

            // 通信类
            'SiemensS7Communication': 'M4 4h16v16H4V4zm2 2v12h12V6H6zm2 2h8v2H8V8zm0 4h8v2H8v-2z', // 西门子S7 - PLC芯片矩形
            'MitsubishiMcCommunication': 'M12 4l8 8-8 8-8-8 8-8zM6 12l6 6 6-6-6-6-6 6z', // 三菱MC - PLC菱形
            'OmronFinsCommunication': 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zM12 6v12c-3.31 0-6-2.69-6-6s2.69-6 6-6z', // 欧姆龙FINS - PLC圆形
            'ModbusRtuCommunication': 'M6 2h12v20H6V2zm2 2v16h8V4H8zM9 6h6v2H9V6zm0 4h6v2H9v-2zm0 4h6v2H9v-2z', // Modbus RTU - 串口D形
            
            // 流程控制类
            'ConditionalBranch': 'M12 2L2 12l10 10 10-10L12 2zm0 3.5L18.5 12 12 18.5 5.5 12 12 5.5z', // 条件分支 - 菱形分叉
            'ResultJudgment': 'M9 16.2L4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4L9 16.2zM9 11l-4.2-4.2L5.2 5.4 9 9.2 12.8 5.4l1.4 1.4L9 11z', // 结果判定 - 对勾/叉号
            'TryCatch': 'M12 1L3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4zm0 10.99h7c-.53 4.12-3.28 7.79-7 8.94V12H5V6.3l7-3.11v8.8z', // 异常捕获 - 盾牌
            
            // 变量类
            'VariableRead': 'M9 7H5v10h4v-2H7V9h2V7zm6 0h4v10h-4v-2h2V9h-2V7z', // 变量读取 - 输入括号
            'VariableWrite': 'M7 7v2h2v6H7v2h6V7H7zm8 0v10h4V7h-4z', // 变量写入 - 输出括号
            'VariableIncrement': 'M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z', // 变量递增 - 加号/计数器
            
            // 其他
            'CycleCounter': 'M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46C19.54 15.03 20 13.57 20 12c0-4.42-3.58-8-8-8zM6 12c0-1.01.25-1.97.7-2.8L5.24 7.74C4.46 8.97 4 10.43 4 12c0 4.42 3.58 8 8 8v3l4-4-4-4v3c-3.31 0-6-2.69-6-6z', // 循环计数器 - 循环箭头
            'ClaheEnhancement': 'M5 9.2h3V19H5zM10.6 5h2.8v14h-2.8zm5.6 8H19v6h-2.8zM3 3h18v2H3V3z', // CLAHE增强 - 直方图增强
            'MorphologicalOperation': 'M12 2l-5 5h3v6h-3l5 5 5-5h-3V7h3l-5-5z', // 形态学操作 - 侵蚀膨胀十字
            'GaussianBlur': 'M4 12c0-4.42 3.58-8 8-8s8 3.58 8 8-3.58 8-8 8-8-3.58-8-8zm2 0c0 3.31 2.69 6 6 6s6-2.69 6-6-2.69-6-6-6-6 2.69-6 6z', // 高斯滤波 - 高斯曲线
            'LaplacianSharpen': 'M12 2L2 22h20L12 2zm0 3.5L18.5 20h-13L12 5.5z', // 拉普拉斯锐化 - 锐化三角
            'OnnxInference': 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z', // ONNX推理 - 神经网络勾选
            'ImageAdd': 'M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2zM3 5h4v2H3V5zm0 12h4v2H3v-2zm14 0h4v2h-4v-2zm0-12h4v2h-4V5z', // 图像加法 - 图层+加号
            'ImageSubtract': 'M5 13h14v-2H5v2zM3 5h4v2H3V5zm0 12h4v2H3v-2zm14 0h4v2h-4v-2zm0-12h4v2h-4V5z', // 图像减法 - 图层+减号
            'ImageBlend': 'M2 6h20v12H2V6zm2 2v8h8V8H4zm10 0v8h6V8h-6z', // 图像融合 - 重叠图层

            // ======================================
            // === 缺失算子图标补充 (P0批次) ===
            // ======================================
            
            // 测量类
            'Measurement': 'M2 20h2v-3l12.42-12.42c.39-.39.39-1.02 0-1.41l-1.17-1.17c-.39-.39-1.02-.39-1.41 0L3 14.42V18h3.58zM15 4l5 5-11 11H4v-5L15 4z', // 游标卡尺
            'CaliperTool': 'M2 2v20h4V11h6v3l5-4-5-4v3H6V2H2zm20 8v12h-2V10h2z', // 卡尺
            'WidthMeasurement': 'M2 6h2v12H2V6zm18 0h2v12h-2V6zM6 11h12v2H6v-2zM4 13l3 3v-2h10v2l3-3-3-3v2H7V9L4 13z', // 宽度测量
            'GapMeasurement': 'M2 4h4v16H2V4zm16 0h4v16h-4V4zM8 11h8v2H8v-2zm-1 2l3 3v-2h4v2l3-3-3-3v2H10V9L7 13z', // 间隙测量
            'AngleMeasurement': 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8 0-4.41 3.59-8 8-8s8 3.59 8 8-3.59 8-8 8zm-4.7-5.3l5.7-2.4 2.4-5.7-5.7 2.4-2.4 5.7zM14 11c-.55 0-1-.45-1-1s.45-1 1-1 1 .45 1 1-.45 1-1 1zM2 22h20v-2H2v2z', // 角度测量 (量角器)
            'CircleMeasurement': 'M12 4c-4.41 0-8 3.59-8 8s3.59 8 8 8 8-3.59 8-8-3.59-8-8-8zm0 14c-3.31 0-6-2.69-6-6s2.69-6 6-6 6 2.69 6 6-2.69 6-6 6zm1-6v4h-2v-4H8l4-4 4 4h-3z', // 圆测量
            'LineMeasurement': 'M4 11h16v2H4zM4 8v8H2V8h2zm18 0v8h-2V8h2z', // 直线测量 (双边卡尺)
            'ContourMeasurement': 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z M4.5 9h2c.83 0 1.5.67 1.5 1.5v3c0 .83-.67 1.5-1.5 1.5h-2 M19.5 9h-2c-.83 0-1.5.67-1.5 1.5v3c0 .83.67 1.5 1.5 1.5h2 M9 4.5v2c0 .83.67 1.5 1.5 1.5h3c.83 0 1.5-.67 1.5-1.5v-2 M9 19.5v-2c0-.83.67-1.5 1.5-1.5h3c.83 0 1.5.67 1.5 1.5v2', // 轮廓测量
            'GeoMeasurement': 'M12 2L1 21h22L12 2zm0 3.83l7.65 13.17H4.35L12 5.83zM10 16h4v2h-4v-2zM12 11l2 3h-4l2-3z', // 几何测量
            'GeometricFitting': 'M20 7h-2V5h-2v2h-2V5h-2v2h-2V5H8v2H6V5H4v2H2v2h2v2H2v2h2v2H2v2h2v2h2v-2h2v2h2v-2h2v2h2v-2h2v2h2v-2h2v-2h-2v-2h2v-2h-2V9h2V7zm-4 8H8V9h8v6z M10 10.5c.83 0 1.5.67 1.5 1.5s-.67 1.5-1.5 1.5-1.5-.67-1.5-1.5.67-1.5 1.5-1.5zm4 0c.83 0 1.5.67 1.5 1.5s-.67 1.5-1.5 1.5-1.5-.67-1.5-1.5.67-1.5 1.5-1.5z', // 几何拟合
            'GeometricTolerance': 'M3 3h18v6H3V3zm2 2v2h14V5H5zm-2 6h8v10H3V11zm2 2v6h4v-6H5zm10-2h4v10h-4V11zm2 2v6h-2v-6h2z', // 几何公差
            'HistogramAnalysis': 'M5 19h14V5h2v16H3V5h2v14zM7 11h2v6H7v-6zm4-3h2v9h-2V8zm4-4h2v13h-2V4zm-8-3L8 3v4L4 4v4l4-3 5 5 6-6 1.4 1.4-7.4 7.4L8 6.8z', // 直方图分析
            'LineLineDistance': 'M2 4h16v2H2V4zm4 14h16v2H6v-2zM8 8h2v8H8V8zm-2 2l3 3v-2h8v2l3-3-3-3v2H9V9L6 11z', // 线线距离
            'PixelStatistics': 'M3 3h18v18H3V3zm2 2v4h4V5H5zm6 0v4h4V5h-4zm6 0v4h4V5h-4zM5 11v4h4v-4H5zm6 0v4h4v-4h-4zm6 0v4h4v-4h-4zM5 17v2h4v-2H5zm6 0v2h4v-2h-4zm6 0v2h4v-2h-4z', // 像素统计
            'PointLineDistance': 'M4 4h16v2H4V4zm8 5c1.1 0 2 .9 2 2s-.9 2-2 2-2-.9-2-2 .9-2 2-2z M11 13v6h2v-6h-2zM9 17l3 3 3-3V14h-2v3h-2v-3H9v3z', // 点线距离
            'SharpnessEvaluation': 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z M12 6c-3.31 0-6 2.69-6 6s2.69 6 6 6 6-2.69 6-6-2.69-6-6-6zm0 10c-2.21 0-4-1.79-4-4s1.79-4 4-4 4 1.79 4 4-1.79 4-4 4z M5.5 12h3M15.5 12h3M12 5.5v3M12 15.5v3', // 清晰度评估
            
            // AI 检测类
            'DeepLearning': 'M13 3c-4.97 0-9 4.03-9 9 0 2.58 1.08 4.9 2.82 6.53L5.4 19.95c-1.87-1.87-3-4.45-3-7.3 0-5.7 4.62-10.32 10.3-10.32S23 6.95 23 12.65c0 2.85-1.13 5.43-3 7.3l-1.42-1.42C20.32 16.9 21.4 14.58 21.4 12.65c0-4.97-4.03-9-9-9zm-4 4v3H6v2h3v3h2v-3h3V9h-3V6H9zm10 4v4h-2v2h2v3h2v-3h2v-2h-2v-4h-2z M12 10c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z M8 18h8v2H8v-2z', // 深度学习 (神经元/大脑)
            'DualModalVoting': 'M7 3h4v4H7V3zm6 0h4v4h-4V3z M4 9h6v2H4V9zm10 0h6v2h-6V9z M10 14h4v6h-4v-6z M12 13L9 11l6-2-3 4z', // 双模态投票 (融合箭头)
            'EdgePairDefect': 'M3 4h18v2H3V4zm0 14h18v2H3v-2z M14.12 9.88l1.41-1.41L12 5l-3.54 3.54 1.41 1.41L12 7.83l2.12 2.05z M9.88 14.12L8.46 15.54 12 19l3.54-3.54-1.41-1.41L12 16.17l-2.12-2.05z M9 10L6 12l3 2v-4zm6 0v4l3-2-3-2z', // 边缘对缺陷
            'SurfaceDefectDetection': 'M20 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zM4 18V6h16v12H4z M9 9h2v6H9V9zm7 0h-2L11 12l3 3h2l-3-3 3-3z', // 表面缺陷检测

            // ======================================
            // === 缺失算子图标补充 (P1批次) ===
            // ======================================
            
            // 定位类
            'BlobLabeling': 'M6 2c-1.1 0-2 .9-2 2v2c0 1.1.9 2 2 2h2c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2H6zm10 0c-1.1 0-2 .9-2 2v2c0 1.1.9 2 2 2h2c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2h-2z M11 13H5v8h6v-8zm8 0h-6v8h6v-8z M1 11h2v10H1V11zm20 0h2v10h-2V11z M14 9H4V1h10v8z M11.5 5h-5v2h5V5', // Blob 标记 (连通域 + 编号)
            'CornerDetection': 'M3 3h6v2H5v4H3V3zm18 0h-6v2h4v4h2V3zM3 21h6v-2H5v-4H3v6zm18 0h-6v-2h4v-4h2v6z M12 8v3H9v2h3v3h2v-3h3v-2h-3V8h-2z M8 8h2v2H8z M14 14h2v2h-2z M8 14h2v2H8z M14 8h2v2h-2z', // 角点检测 (四个角+十字)
            'EdgeIntersection': 'M4 4l16 16m0-16L4 20 M12 9c-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3-1.34-3-3-3zm0 4c-.55 0-1-.45-1-1s.45-1 1-1 1 .45 1 1-.45 1-1 1z M5 19H3v-2l1.6-1.6L6.6 17 5 19zm14 0h2v-2l-1.6-1.6-2 1.6L19 19zM5 5H3v2l1.6 1.6L6.6 7 5 5zm14 0h2v2l-1.6 1.6-2-1.6L19 5z', // 边缘交叉 (X线+交点圆)
            'ParallelLineFind': 'M4 6h16v2H4V6zm0 10h16v2H4v-2z M11 2v4h2V2h-2zm0 16v4h2v-4h-2z M8 4l-4 4 4 4v-3h8v3l4-4-4-4v3H8V4zm0 10l-4 4 4 4v-3h8v3l4-4-4-4v3H8v-3z', // 平行线查找 (两平线+寻找箭头)
            'PositionCorrection': 'M12 2L2 12l3 3 7-7 7 7 3-3-10-10z M12 22l-4-4h3v-4h2v4h3l-4 4z M8 12c-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4-1.79-4-4-4z', // 位置校正 (十字架微调)
            'QuadrilateralFind': 'M4.5 4.5l11-2 4 13-14 3-1-14z M3 3c-.55 0-1 .45-1 1s.45 1 1 1 1-.45 1-1-.45-1-1-1zm12.5-2c-.55 0-1 .45-1 1s.45 1 1 1 1-.45 1-1-.45-1-1-1zm4.5 13c-.55 0-1 .45-1 1s.45 1 1 1 1-.45 1-1-.45-1-1-1zM4 19c-.55 0-1 .45-1 1s.45 1 1 1 1-.45 1-1-.45-1-1-1z M10 10l4 4m0-4l-4 4', // 四边形查找 (不规则四边形+四顶点+寻找十字)
            'RectangleDetection': 'M3 5v14h18V5H3zm16 12H5V7h14v10z M8 10h8v4H8v-4z M12 8v2 M12 14v2 M8 12h2 M14 12h2 M7 9h2v2H7z M15 13h2v2h-2z', // 矩形检测 (内外框+准心)
            
            // 匹配类 (特征/形状)
            'AkazeFeatureMatch': 'M4 8c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm16 4c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm-6-8c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zM8 16c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z M5.5 9.5l7-4M6 11l8 3 M15.5 7l3.5 5.5 M9 16.5l8-4', // AKAZE特征提取 (散点与连线网)
            'GradientShapeMatch': 'M12 2L2 12l10 10 10-10L12 2zm0 3.5L18.5 12 12 18.5 5.5 12 12 5.5z M12 7v4H8L12 7z M12 17v-4h4L12 17z M16 11h-4V7l4 4z M8 13h4v4l-4-4z', // 梯度形状匹配 (边缘梯度向量图)
            'OrbFeatureMatch': 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z M9.5 8.5c-.83 0-1.5.67-1.5 1.5s.67 1.5 1.5 1.5 1.5-.67 1.5-1.5-.67-1.5-1.5-1.5zM14.5 12.5c-.83 0-1.5.67-1.5 1.5s.67 1.5 1.5 1.5 1.5-.67 1.5-1.5-.67-1.5-1.5-1.5z M10.5 10.5l3 3 M7 12H5M19 12h-2M12 7V5M12 19v-2', // ORB特征匹配 (圆形内的多个角点+坐标)
            'PyramidShapeMatch': 'M12 2L1 21h22L12 2zm0 3.83l7.65 13.17H4.35L12 5.83z M6.8 12h10.4M8.5 9h7M5 15h14 M12 14v4M10 12v3M14 12v3M9 9v3M15 9v3M11 6v3M13 6v3', // 金字塔形状匹配 (分层金字塔+内部子结构)
            'TemplateMatching': 'M20 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 14H4V6h16v12z M15 9H9v6h6V9z M13 11h-2v2h2v-2z M8 5h2v2H8z M14 5h2v2h-2z M8 17h2v2H8z M14 17h2v2h-2z M5 8h2v2H5z M5 14h2v2H5z M17 8h2v2h-2z M17 14h2v2h-2z', // 模板定位匹配 (搜索框与内层模板矩形与散列搜索点)
            'ShapeMatching': 'M12 6c3.31 0 6 2.69 6 6s-2.69 6-6 6-6-2.69-6-6 2.69-6 6-6m0-2c-4.42 0-8 3.58-8 8s3.58 8 8 8 8-3.58 8-8-3.58-8-8-8z M12 14l-4-4h8l-4 4z M8 8l4-4 4 4H8z M8 16l4 4 4-4H8z', // 形状匹配 (圆与内部寻找游标)
            
            // ======================================
            // === 缺失算子图标补充 (P2 上半批次) ===
            // ======================================
            
            // 预处理类 (13个)
            'Threshold': 'M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 16H5V5h14v14z M7 12h10v2H7z M7 8h10v2H7zM7 16h10v2H7z M12 5v14h2V5z', // 二值化 (直方图切片/阈值滑块)
            'AdaptiveThreshold': 'M3 5H1v14c0 1.1.9 2 2 2h14v-2H3V5zm4 14h14V5H7v14zM9 7h10v10H9V7z M11 15l2-3 2 3h-4zm2-6l2 3h-4l2-3z', // 自适应二值化 (局部窗口+阈值浮动)
            'CannyEdge': 'M3 17h18v2H3zm0-7h18v5H3zm0-7h18v5H3z M12 3v18H9V3h3z', // Canny 边缘 (相交边缘线)
            'ShadingCorrection': 'M20 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 14H4V6h16v12z M5 8h14 M5 12h14 M5 16h14 M12 5v14 M8 5v14 M16 5v14 M6 6c3 0 5 1.5 6 3s3 3 6 3', // 阴影/平场校正 (网格+明暗渐变调整曲线)
            'HistogramEqualization': 'M5 9.2h3V19H5zM10.6 5h2.8v14h-2.8zm5.6 8H19v6h-2.8z M3 21h18v-2H3v2z M2 3h2v16H2z', // 直方图均衡化 (拉伸的柱状图)
            'BilateralFilter': 'M3 7v10h18V7H3zm2 2h5v6H5V9zm7 0h5v6h-5V9z M19 9h-5v6h5v-6z', // 双边滤波 (保留边缘的平滑块)
            'MorphologicalOperation': 'M12 2l-5 5h3v6h-3l5 5 5-5h-3V7h3l-5-5z M12 10v4h-2v-4h2z M10 12h4v-2h-4v2z', // 形态学 (十字膨胀核)
            'ClaheEnhancement': 'M5 9.2h3V19H5zM10.6 5h2.8v14h-2.8zm5.6 8H19v6h-2.8z M3 3h18v2H3V3z M12 1v4M8 1v4M16 1v4', // CLAHE 限制对比度 (限制高频的直方图)
            'ContourDetection': 'M3 5v14h18V5H3zm16 12H5V7h14v10zM7 9h4v4H7V9zm6 0h4v4h-4V9z M9 11h8v4H9v-4z', // 轮廓检测 (连序边界框)
            'ImageResize': 'M12 2l4 4h-3v4h-2V6H8l4-4zM4 14v4h4v-2H6v-2H4zm16 0v4h-4v-2h2v-2h2z M10 10l-4-4v3H4v-5h5v2H6l4 4-2 2zM14 14l4 4v-3h2v5h-5v-2h3l-4-4 2-2z', // 图像缩放 (双向推拉对角线)
            'ImageCrop': 'M17 15h2v-2h-2v2zM7 11v6h6v-2H9v-4H7zM5 7v4h2V9h4V7H5zM17 7v4h2V9h-2V7z M3 3v4h2V5h2V3H3zM19 3v2h-2v2h2V3h-2zM19 19v-4h-2v2h-2v2h4zM3 19v-2h2v-2H3v4z', // 图像裁剪 (带四个边角的裁剪外框)
            'ImageRotate': 'M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46C19.54 15.03 20 13.57 20 12c0-4.42-3.58-8-8-8zm0 14c-3.31 0-6-2.69-6-6 0-1.01.25-1.97.7-2.8L4.24 7.74C4.46 8.97 4 10.43 4 12c0 4.42 3.58 8 8 8v3l4-4-4-4v3z M12 10v4l3-2-3-2z', // 图像旋转 (双向旋转箭头+内部旋钮)
            'PerspectiveTransform': 'M2 6l4-2 8 2 8-2v14l-8 2-8-2-4 2V6zm4 2v8l6 1.5V8.5L6 8z M12 12m-2 0a2 2 0 1 0 4 0a2 2 0 1 0 -4 0', // 透视变换 (梯形视角+中心点)

            // 图像处理类 (4个)
            'AffineTransform': 'M3 3v18h18V3H3zm2 2h14v14H5V5z M6 18l6-12h6l-6 12H6z M9 15h4M12 9h4', // 仿射变换 (剪切平移后的平行四边形)
            'CopyMakeBorder': 'M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 16H5V5h14v14z M8 8h8v8H8V8z M3 3h4v2H3v-2z M17 3h4v2h-4v-2z M3 19h4v2H3v-2z M17 19h4v2h-4v-2z', // 边界填充 (内外框与填充阴影线)
            'ImageStitching': 'M4 4h7v16H4V4zm9 0h7v16h-7V4z M10 7h4v2h-4V7zm0 4h4v2h-4v-2zm0 4h4v2h-4v-2z', // 图像拼接 (左右两张图+中间缝合线)
            'PolarUnwrap': 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z M12 6c-3.31 0-6 2.69-6 6s2.69 6 6 6 6-2.69 6-6-2.69-6-6-6z M3 21h18v-2H3v2z M7 21l3-4v4H7zm6 0l3-4v4h-3z', // 极坐标展开 (内外圆环到底部平展带)

            // 数据处理类 (7个)
            'Aggregator': 'M4 6h4v4H4V6zm0 8h4v4H4v-4zm12-4h4v4h-4v-4z M8 8h4v2H8V8zm0 8h4v-2H8v2z M12 12h4v-2h-4v2z', // 聚合器 (多节点连向单节点)
            'ArrayIndexer': 'M4 4h4v2H6v12h2v2H4V4zm16 0h-4v2h2v12h-2v2h4V4z M10 10h4v4h-4v-4z', // 数组索引 (方括号包裹元素)
            'BoxFilter': 'M3 3h18v18H3V3zm2 2v14h14V5H5z M7 7h10v10H7V7z M9 9h6v6H9V9z M12 11v2', // 框过滤 (三重嵌套过滤框)
            'BoxNms': 'M5 5h10v10H5V5zm6 6h10v10H11V11z M14 8l-2 2 2 2 2-2-2-2z M8 14h2v2H8z M17 17h2v2h-2z', // NMS 抑制 (两个重叠框与消除的十字)
            'JsonExtractor': 'M10 4H7v4c0 1.1-.9 2-2 2v4c1.1 0 2 .9 2 2v4h3v-2H8v-2c0-1.1-.9-2-2-2 1.1 0 2-.9 2-2V6h2V4zm4 0h3v4c0 1.1.9 2 2 2v4c-1.1 0-2 .9-2 2v4h-3v-2h2v-2c0-1.1.9-2 2-2-1.1 0-2-.9-2-2V6h-2V4z M9 12h6v2H9v-2z', // JSON 提取 (大括号包围箭头)
            'PointAlignment': 'M3 12h4v2H3v-2zm14 0h4v2h-4v-2z M12 3h2v4h-2V3zm0 14h2v4h-2v-4z M10 10h4v4h-4v-4z', // 点对齐 (十字架对正)
            'PointCorrection': 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z M12 8v4l3 3-1.5 1.5L10 13V8h2z M16 12h2 M6 12h2', // 点校正 (偏移钟盘微调)

            // 标定额外类 (2个)
            'CalibrationLoader': 'M9.4 16.6L4.8 12l4.6-4.6L8 6l-6 6 6 6 1.4-1.4zm5.2 0l4.6-4.6-4.6-4.6L16 6l6 6-6 6-1.4-1.4z M12 18v-8M9 13l3-3 3 3', // 标定加载 (带导入箭头的标定板)
            'TranslationRotationCalibration': 'M12 2L1 21h22L12 2zm0 3.83l7.65 13.17H4.35L12 5.83z M12 9v6M9 12h6 M10 15a4 4 0 0 1 4 0', // 平移旋转标定 (坐标架与旋转弧线)

            // 通信补充类 (2个)
            'HttpRequest': 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z M12 6h3v2h-3V6z', // HTTP 请求 (网络地球加标记)
            'MqttPublish': 'M20 2H4c-1.1 0-1.99.9-1.99 2L2 22l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm-6 12l-4-4h3V6h2v4h3l-4 4z', // MQTT 发布 (消息气泡加发送箭头)

            // 识别与数值、颜色类 (4个)
            'OcrRecognition': 'M3 3h18v6H3V3zm2 2v2h14V5H5zm4 7h2v6H9v-6zm4 0h2v6h-2v-6zm-8 4v2h14v-2H5zM5 11h2v3H5v-3zM17 11h2v3h-2v-3z', // OCR 识别 (文本框与ABC字符提取框)
            'ColorMeasurement': 'M12 3c-4.97 0-9 4.03-9 9s4.03 9 9 9c.83 0 1.5-.67 1.5-1.5 0-.39-.15-.74-.39-1.01-.23-.26-.38-.61-.38-.99 0-.83.67-1.5 1.5-1.5H16c2.76 0 5-2.24 5-5 0-4.42-4.03-8-9-8zm-5.5 9c-.83 0-1.5-.67-1.5-1.5S5.67 9 6.5 9 8 9.67 8 10.5 7.33 12 6.5 12zm3-4C8.67 8 8 7.33 8 6.5S8.67 5 9.5 5s1.5.67 1.5 1.5S10.33 8 9.5 8zm5 0c-.83 0-1.5-.67-1.5-1.5S13.67 5 14.5 5s1.5.67 1.5 1.5S15.33 8 14.5 8zm3 4c-.83 0-1.5-.67-1.5-1.5S16.67 9 17.5 9s1.5.67 1.5 1.5-.67 1.5-1.5 1.5z M12 12l3 3v2h-2l-3-3z', // 颜色测量 (滴管与调色板)
            'UnitConvert': 'M12 4l-4 4h3v8H8l4 4 4-4h-3V8h3l-4-4z M5 12H3 M21 12h-2 M12 12h-4 M16 12h-4', // 单位转换 (互转标识与刻度线)
            'MathOperation': 'M3 5h4v2H3V5zm0 12h4v2H3v-2zm14 0h4v2h-4v-2zm0-12h4v2h-4V5z M8 12c0-2.21 1.79-4 4-4s4 1.79 4 4-1.79 4-4 4-4-1.79-4-4z M12 15l-3-3v2h6v-2l-3 3z', // 数学运算 (公式符号函数/根号化简)

            // 通用系统、流程控制、辅助类 (16个)
            'Comparator': 'M10 8h9v2h-9zm0 4h9v2h-9zm0 4h9v2h-9z M4 9l4 3-4 3v-2l2-1-2-1V9z', // 比较器 (条件刻度大于小于)
            'Delay': 'M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8zM12.5 7H11v6l5.25 3.15.75-1.23-4.5-2.67z', // 延时 (时钟/沙漏)
            'ForEach': 'M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46C19.54 15.03 20 13.57 20 12c0-4.42-3.58-8-8-8zM6 12c0-1.01.25-1.97.7-2.8L5.24 7.74C4.46 8.97 4 10.43 4 12c0 4.42 3.58 8 8 8v3l4-4-4-4v3c-3.31 0-6-2.69-6-6z M9 10h6v2H9zM9 14h6v2H9z', // 遍历 (循环箭头+内部数组)
            'LogicGate': 'M9 5v14H7V5h2zm4 0v14h-2V5h2zm4 0v14h-2V5h2z M4 11h16v2H4z', // 逻辑门 (并列网门, AND/OR抽象)
            'Statistics': 'M10 20h4V4h-4v16zm-6 0h4v-8H4v8zM16 9v11h4V9h-4z M12 2v2 M6 10v2 M18 7v2', // 统计 (柱状图+ Σ抽象)
            'StringFormat': 'M5 5h14v2H5V5zm0 12h14v2H5v-2zm0-6h9v2H5v-2z M16 11h2v2h-2z M5 8h4v2H5V8zm6 0h4v2h-4V8z', // 字符串格式化 (格式占位符)
            'TypeConvert': 'M12 4l-4 4h3v8H8l4 4 4-4h-3V8h3l-4-4z M6 12H4 M20 12h-2', // 类型转换 (明确类型转换箭头)
            'PointSetTool': 'M2 2h4v4H2V2zm6 0h4v4H8V2zm6 0h4v4h-4V2zm6 0h4v4h-4V2zM2 8h4v4H2V8zm18 0h4v4h-4V8zM2 14h4v4H2v-4zm18 0h4v4h-4v-4z M11 12l2-2 2 2-2 2-2-2z', // 点集工具 (散列格图与集工具)
            'ScriptOperator': 'M9.4 16.6L4.8 12l4.6-4.6L8 6l-6 6 6 6 1.4-1.4zm5.2 0l4.6-4.6-4.6-4.6L16 6l6 6-6 6-1.4-1.4z M12 5l-2 14h2l2-14z', // 脚本 (纯代码括号)
            'TextSave': 'M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z M11 12H9v2h2v-2z M10 19v-2', // 文本保存 (文件加保存标识)
            'TimerStatistics': 'M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8zm.5-13H11v6l5.25 3.15.75-1.23-4.5-2.67z M8 12h2v4H8z M14 9h2v7h-2z', // 计时统计 (秒表盘+柱图)
            'TriggerModule': 'M7 2v11h3v9l7-12h-4l4-8z M10 2h4v8 M16 2h2 M6 2h2 M10 22v-4 M14 22v-4', // 触发模块 (长闪电加引脚)
            'Comment': 'M20 2H4c-1.1 0-2 .9-2 2v18l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm0 14H5.17L4 17.17V4h16v12z M7 9h10v2H7zM7 6h10v2H7z M7 12h7v2H7z', // 注释 (评论气泡+文字排版)
            'ImageSave': 'M19 12v7H5v-7H3v7c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2v-7h-2zm-6 .67l2.59-2.58L17 11.5l-5 5-5-5 1.41-1.41L11 12.67V3h2v9.67z', // 图像保存 (下载带边框)
            'ImageCompose': 'M4 4h6v6H4V4zm10 0h6v6h-6V4zM4 14h6v6H4v-6zm10 0h6v6h-6v-6z M10 10h4v4h-4v-4z M12 6v2 M12 16v2 M6 12h2 M16 12h2', // 图像组合 (多图层块合并)
            'ImageTiling': 'M3 3h18v18H3V3zm8 16h8V11h-8v8zm0-10h8V5h-8v4zm-6 10h4V11H5v8zm0-10h4V5H5v4z M3 10h18 M10 3v18', // 图像分块 (分割网格与切分线)

        };

        if (icons[type]) {
            return icons[type];
        }

        // 如果没有匹配到具体算子，尝试按照类别返回通用图标，避免全部显示齿轮
        if (category) {
            const categoryPaths = {
                '输入': 'M9 3L7.17 5H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2h-3.17L15 3H9zm3 15c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5z', // 相机
                '预处理': 'M20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83zM3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25z', // 笔/编辑
                '特征提取': 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-8h-2V7h2v2z', // 信息
                '检测': 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z', // 勾选
                '测量': 'M21 6H3c-1.1 0-2 .9-2 2v8c0 1.1.9 2 2 2h18c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2zm0 10H3V8h2v4h2V8h2v4h2V8h2v4h2V8h2v8z', // 尺子
                'AI检测': 'M20 6h-8l-2-2H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2zm-6 10H6v-2h8v2zm4-4H6v-2h12v2z', // AI/文件夹
                '通信': 'M20 18c1.1 0 1.99-.9 1.99-2L22 6c0-1.1-.9-2-2-2H4c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2H0v2h24v-2h-4zM4 6h16v10H4V6z', // 屏幕
                '输出': 'M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z', // 下载
                '标定': 'M9.4 16.6L4.8 12l4.6-4.6L8 6l-6 6 6 6 1.4-1.4zm5.2 0l4.6-4.6-4.6-4.6L16 6l6 6-6 6-1.4-1.4z', // 标定
            };
            
            // 缺失类别 fallback 补充
            const missingCategoryPaths = {
                '控制': 'M12 2L2 12l10 10 10-10L12 2zm0 3.5L18.5 12 12 18.5 5.5 12 12 5.5z', // 分叉/菱形
                '流程控制': 'M20 12l-4-4v3H4v2h12v3l4-4zM4 6h16v2H4V6zm0 12h16v-2H4v2z', // 流程图箭头
                '变量': 'M6 4h12l-2 16H8L6 4zm3 2l1 12h4l1-12H9z', // 花括号/变量符号
                '数据': 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z', // 数据/信息
                '辅助': 'M19.43 12.98c.04-.32.07-.64.07-.98s-.03-.66-.07-.98l2.11-1.65c.19-.15.24-.42.12-.64l-2-3.46c-.12-.22-.39-.3-.61-.22l-2.49 1c-.52-.4-1.08-.73-1.69-.98l-.38-2.65C14.46 2.18 14.25 2 14 2h-4c-.25 0-.46.18-.5.42l-.38 2.65c-.61.25-1.17.59-1.69.98l-2.49-1c-.23-.09-.49 0-.61.22l-2 3.46c-.13.22-.07.49.12.64l2.11 1.65c-.04.32-.07.65-.07.98s.03.66.07.98l-2.11 1.65c-.19.15-.24.42-.12.64l2 3.46c.12.22.39.3.61.22l2.49-1.01c.52.4 1.08.73 1.69.98l.38 2.65c.04.24.25.42.5.42h4c.25 0 .46-.18.5-.42l.38-2.65c.61-.25 1.17-.59 1.69-.98l2.49 1.01c.22.08.49 0 .61-.22l2-3.46c.12-.22.07-.49-.12-.64l-2.11-1.65zM12 15.5c-1.93 0-3.5-1.57-3.5-3.5s1.57-3.5 3.5-3.5 3.5 1.57 3.5 3.5-1.57 3.5-3.5 3.5z', // 工具箱/齿轮
                '颜色处理': 'M12 3c-4.97 0-9 4.03-9 9s4.03 9 9 9c.83 0 1.5-.67 1.5-1.5 0-.39-.15-.74-.39-1.01-.23-.26-.38-.61-.38-.99 0-.83.67-1.5 1.5-1.5H16c2.76 0 5-2.24 5-5 0-4.42-4.03-8-9-8z', // 调色板
                '匹配定位': 'M12 8c-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4-1.79-4-4-4zm8.94 3c-.46-4.17-3.77-7.48-7.94-7.94V1h-2v2.06C6.83 3.52 3.52 6.83 3.06 11H1v2h2.06c.46 4.17 3.77 7.48 7.94 7.94V23h2v-2.06c4.17-.46 7.48-3.77 7.94-7.94H23v-2h-2.06zM12 19c-3.87 0-7-3.13-7-7s3.13-7 7-7 7 3.13 7 7-3.13 7-7 7z', // 十字准星
                '识别': 'M3 11h8V3H3v8zm2-6h4v4H5V5zM3 21h8v-8H3v8zm2-6h4v4H5v-4zM13 3v8h8V3h-8zm6 6h-4V5h4v4zM13 13h2v2h-2zM15 15h2v2h-2zM13 17h2v2h-2zM17 13h2v2h-2zM19 15h2v2h-2zM17 17h2v2h-2zM15 19h2v2h-2zM19 19h2v2h-2z', // 扫描码/二维码
            };
            
            if (categoryPaths[category]) {
                return categoryPaths[category];
            }
            
            if (missingCategoryPaths[category]) {
                return missingCategoryPaths[category];
            }
        }

        // 最后的回退图标 (齿轮)
        return this.getSvgIcon('M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L5.03 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z');
    }

    /**
     * 分类图标映射 (SVG)
     */
    getCategoryIcon(category) {
        const icons = {
            '输入': 'M5 13h14v-2H5v2zm-2 4h14v-2H3v2zM7 7v2h14V7H7z', // 列表
            '预处理': 'M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z', // 笔
            '特征提取': 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z', // 信息
            '检测': 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z', // 勾选
            'AI检测': 'M9 21c0 .55.45 1 1 1h4c.55 0 1-.45 1-1v-1H9v1zm3-19C8.14 2 5 5.14 5 9c0 2.38 1.19 4.47 3 5.74V17c0 .55.45 1 1 1h6c.55 0 1-.45 1-1v-2.26c1.81-1.27 3-3.36 3-5.74 0-3.86-3.14-7-7-7zm2.85 11.1l-.85.6V16h-4v-2.3l-.85-.6A4.997 4.997 0 0 1 7 9c0-2.76 2.24-5 5-5s5 2.24 5 5c0 1.63-.8 3.16-2.15 4.1z', // 灯泡
            '测量': 'M21 6H3c-1.1 0-2 .9-2 2v8c0 1.1.9 2 2 2h18c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2zm0 10H3V8h2v4h2V8h2v4h2V8h2v4h2V8h2v8z', // 尺子
            '通信': 'M4 6h16v10H4V6zM20 18c1.1 0 1.99-.9 1.99-2L22 6c0-1.1-.9-2-2-2H4c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2H0v2h24v-2h-4z', // 电脑
            '输出': 'M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z', // 下载
            '标定': 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 17h-2v-2h2v2zm2.07-7.75l-.9.92C13.45 12.9 13 13.5 13 15h-2v-.5c0-1.1.45-2.1 1.17-2.83l1.24-1.26c.37-.36.59-.86.59-1.41 0-1.1-.9-2-2-2s-2 .9-2 2H8c0-2.21 1.79-4 4-4s4 1.79 4 4c0 .88-.36 1.68-.93 2.25z', // 问号/校准
            '其他': 'M10 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2h-8l-2-2z', // 文件夹
        };
        const path = icons[category] || 'M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z'; // 文件
        return this.getSvgIcon(path);
    }

    /**
     * 渲染算子树
     */
    renderOperatorTree() {
        // 按类别分组
        const grouped = this.groupByCategory(this.filteredOperators);
        
        // 构建树形数据
        const treeData = Object.entries(grouped).map(([category, operators]) => ({
            id: `category_${category}`,
            label: category,
            type: 'category',
            icon: null, // 禁止 TreeView 默认渲染 (会转义 SVG)
            customIcon: this.getCategoryIcon(category),
            expanded: true,
            children: operators.map((op, index) => {
                // 预先获取图标路径并注入到 operator 数据中，
                // 这样拖拽到画布时，flowEditorInteraction.js 就能直接使用正确的图标
                const iconPath = this.getOperatorIconPath(op.type, category);
                op.iconPath = iconPath;
                
                return {
                    id: `operator_${op.type}_${index}`,
                    label: op.displayName || op.name,
                    type: 'operator',
                    icon: null, // 禁止 TreeView 默认渲染
                    customIcon: op.icon || this.getSvgIcon(iconPath),
                    data: op
                };
            })
        }));
        
        this.treeView.setData(treeData);

        // 【关键修复】初始化 expandedNodes，确保所有设置了 expanded: true 的节点 ID 都在 Set 中
        // 这样第一次点击就能正确切换状态
        treeData.forEach(node => {
            if (node.expanded) {
                this.treeView.expandedNodes.add(node.id);
            }
        });
        
        // 【新增】加载保存的展开状态（覆盖默认值）
        this.loadExpandedState();
        
        // 【调试】打印展开状态
        console.log('[OperatorLibrary] After setData, expandedNodes:', Array.from(this.treeView.expandedNodes));
    }

    /**
     * 按类别分组
     */
    groupByCategory(operators) {
        return operators.reduce((acc, op) => {
            const category = op.category || '其他';
            if (!acc[category]) {
                acc[category] = [];
            }
            acc[category].push(op);
            return acc;
        }, {});
    }

    /**
     * 【新增】保存展开状态到 localStorage
     */
    saveExpandedState() {
        try {
            const expandedIds = Array.from(this.treeView.expandedNodes);
            localStorage.setItem(this.storageKey, JSON.stringify(expandedIds));
        } catch (e) {
            console.warn('[OperatorLibrary] Failed to save expanded state:', e);
        }
    }

    /**
     * 【新增】从 localStorage 加载展开状态
     */
    loadExpandedState() {
        try {
            const saved = localStorage.getItem(this.storageKey);
            console.log('[OperatorLibrary] Loading expanded state from localStorage:', saved);
            if (saved) {
                const expandedIds = JSON.parse(saved);
                console.log('[OperatorLibrary] Parsed expandedIds:', expandedIds);
                expandedIds.forEach(id => this.treeView.expandedNodes.add(id));
                console.log('[OperatorLibrary] After loading, expandedNodes:', Array.from(this.treeView.expandedNodes));
                // 加载状态后重新渲染
                this.treeView.render();
            }
        } catch (e) {
            console.warn('[OperatorLibrary] Failed to load expanded state:', e);
        }
    }

    /**
     * 绑定搜索事件
     */
    bindSearchEvents() {
        const searchInput = this.container.querySelector('#operator-search');
        const clearBtn = this.container.querySelector('#btn-clear-search');
        
        // 搜索输入
        let searchTimeout;
        searchInput.addEventListener('input', (e) => {
            clearTimeout(searchTimeout);
            searchTimeout = setTimeout(() => {
                this.searchOperators(e.target.value);
            }, 300);
        });
        
        // 清除搜索
        clearBtn.addEventListener('click', () => {
            searchInput.value = '';
            this.searchOperators('');
        });
    }

    /**
     * 搜索算子
     */
    searchOperators(keyword) {
        if (!keyword.trim()) {
            this.filteredOperators = this.operators;
        } else {
            const lowerKeyword = keyword.toLowerCase();
            this.filteredOperators = this.operators.filter(op => 
                (op.displayName || op.name).toLowerCase().includes(lowerKeyword) ||
                (op.description && op.description.toLowerCase().includes(lowerKeyword)) ||
                (op.category && op.category.toLowerCase().includes(lowerKeyword))
            );
        }
        
        this.renderOperatorTree();
        
        // 显示搜索结果
        if (keyword.trim()) {
            showToast(`找到 ${this.filteredOperators.length} 个算子`, 'info');
        }
    }

    /**
     * 绑定操作按钮事件
     */
    bindActionEvents() {
        // 展开全部
        this.container.querySelector('#btn-expand-all').addEventListener('click', () => {
            this.treeView.expandAll();
        });
        
        // 折叠全部
        this.container.querySelector('#btn-collapse-all').addEventListener('click', () => {
            this.treeView.collapseAll();
        });
        
        // 刷新列表
        this.container.querySelector('#btn-refresh').addEventListener('click', () => {
            this.loadOperators();
        });
    }

    /**
     * 显示算子预览
     */
    showOperatorPreview(operator) {
        const preview = this.container.querySelector('#operator-preview');
        const resolvedOperator = this.metadataByType.get(operator.type) || operator;
        const inputPorts = Array.isArray(resolvedOperator.inputPorts) ? resolvedOperator.inputPorts : [];
        const outputPorts = Array.isArray(resolvedOperator.outputPorts) ? resolvedOperator.outputPorts : [];
        
        preview.innerHTML = `
            <div class="operator-detail">
                <div class="detail-header">
                    <span class="detail-icon">${resolvedOperator.icon || this.getSvgIcon(this.getOperatorIconPath(resolvedOperator.type, resolvedOperator.category))}</span>
                    <h4>${resolvedOperator.displayName || resolvedOperator.name}</h4>
                </div>
                <div class="detail-meta">
                    <span class="detail-category">${resolvedOperator.category || '其他'}</span>
                    <span class="detail-type">${resolvedOperator.type}</span>
                </div>
                <p class="detail-description">${resolvedOperator.description || '暂无描述'}</p>
                
                <div class="detail-params">
                    <h5>参数配置</h5>
                    ${this.renderParameterList(resolvedOperator.parameters)}
                </div>
                
                <div class="detail-ports">
                    <h5>端口定义</h5>
                    <div class="ports-list">
                        ${inputPorts.length > 0
                            ? inputPorts.map(port => `
                                <div class="port-item input">
                                    <span class="port-dot input"></span>
                                    <span>输入: ${port.displayName || port.DisplayName || port.name || port.Name || '未命名'} (${port.dataType || port.DataType || 'Any'})</span>
                                </div>
                            `).join('')
                            : `
                                <div class="port-item input">
                                    <span class="port-dot input"></span>
                                    <span>输入: ${resolvedOperator.inputType || '图像'}</span>
                                </div>
                            `}
                        ${outputPorts.length > 0
                            ? outputPorts.map(port => `
                                <div class="port-item output">
                                    <span class="port-dot output"></span>
                                    <span>输出: ${port.displayName || port.DisplayName || port.name || port.Name || '未命名'} (${port.dataType || port.DataType || 'Any'})</span>
                                </div>
                            `).join('')
                            : `
                                <div class="port-item output">
                                    <span class="port-dot output"></span>
                                    <span>输出: ${resolvedOperator.outputType || '图像/数据'}</span>
                                </div>
                            `}
                    </div>
                </div>
                <div class="detail-actions" style="margin-top:16px;">
                    <button class="cv-btn cv-btn-secondary" id="btn-show-autotune-strategies">查看自动调参策略</button>
                    <div id="autotune-strategies-panel" style="margin-top:12px;"></div>
                </div>
            </div>
        `;

        preview.querySelector('#btn-show-autotune-strategies')?.addEventListener('click', async () => {
            const panel = preview.querySelector('#autotune-strategies-panel');
            if (!panel) {
                return;
            }

            panel.innerHTML = '<div class="params-empty">正在加载自动调参策略...</div>';

            try {
                const strategies = await httpClient.get('/autotune/strategies');
                if (!Array.isArray(strategies) || strategies.length === 0) {
                    panel.innerHTML = '<div class="params-empty">暂无可用自动调参策略</div>';
                    return;
                }

                panel.innerHTML = `
                    <div class="detail-params">
                        <h5>自动调参策略</h5>
                        <ul class="params-list">
                            ${strategies.map(strategy => `
                                <li class="param-item">
                                    <span class="param-name">${strategy.name || strategy.Name || '未命名策略'}</span>
                                    <span class="param-type">${strategy.category || strategy.Category || '策略'}</span>
                                    <span class="param-default">${strategy.description || strategy.Description || '暂无描述'}</span>
                                </li>
                            `).join('')}
                        </ul>
                    </div>
                `;
            } catch (error) {
                console.error('[OperatorLibraryPanel] 获取自动调参策略失败:', error);
                panel.innerHTML = `<div class="params-empty">加载失败：${error.message}</div>`;
            }
        });
    }

    /**
     * 渲染参数列表
     */
    renderParameterList(parameters) {
        if (!parameters || parameters.length === 0) {
            return '<p class="params-empty">此算子无需配置参数</p>';
        }
        
        return `
            <ul class="params-list">
                ${parameters.map(param => `
                    <li class="param-item">
                        <span class="param-name">${param.name}</span>
                        <span class="param-type">${param.type}</span>
                        <span class="param-default">默认: ${param.defaultValue}</span>
                    </li>
                `).join('')}
            </ul>
        `;
    }

    /**
     * 获取算子列表
     */
    getOperators() {
        return this.operators;
    }

    /**
     * 获取分类列表
     */
    getCategories() {
        return [...new Set(this.operators.map(op => op.category || '其他'))];
    }

    /**
     * 刷新算子列表
     */
    refresh() {
        return this.loadOperators();
    }
}

export default OperatorLibraryPanel;
