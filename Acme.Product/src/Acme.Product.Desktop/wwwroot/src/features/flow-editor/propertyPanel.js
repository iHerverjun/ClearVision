import webMessageBridge from '../../core/messaging/webMessageBridge.js';

class PropertyPanel {
    constructor(containerId) {
        this.container = document.getElementById(containerId);
        this.currentOperator = null;
        this.onChangeCallback = null;
        this.bindGlobalEvents();
    }

    /**
     * 绑定全局事件
     */
    bindGlobalEvents() {
        webMessageBridge.on('FilePickedEvent', (event) => {
            // 兼容 PascalCase 和 camelCase
            const isCancelled = event.IsCancelled || event.isCancelled;
            if (isCancelled) return;

            // 兼容 PascalCase 和 camelCase
            const parameterName = event.ParameterName || event.parameterName;
            const filePath = event.FilePath || event.filePath;

            console.log('[PropertyPanel] 收到文件选择事件:', parameterName, filePath);

            const input = this.container.querySelector(`#param-${parameterName}`);
            if (input) {
                input.value = filePath || '';
                // 触发 change 事件以更新状态
                input.dispatchEvent(new Event('change'));

                // 自动应用更改
                this.applyChanges();
            }
        });
    }

    /**
     * 设置算子
     */
    setOperator(operator) {
        this.currentOperator = operator;
        this.render();
    }

    /**
     * 清空面板
     */
    clear() {
        this.currentOperator = null;
        this.container.innerHTML = '<p class="empty-text">选择一个算子查看属性</p>';
    }

    /**
     * 渲染面板 - 阶段四增强版，支持参数分组折叠
     */
    render() {
        if (!this.currentOperator) {
            this.clear();
            return;
        }

        // 兼容 title (画布节点) 和 displayName (算子库)
        const title = this.currentOperator.title || this.currentOperator.displayName || this.currentOperator.type;
        const { type, parameters = [], iconPath, icon } = this.currentOperator;
        
        const iconHtml = iconPath 
            ? `<div class="property-icon"><svg viewBox="0 0 24 24" width="24" height="24" fill="currentColor"><path d="${iconPath}"/></svg></div>`
            : (icon ? `<div class="property-icon text-icon">${icon}</div>` : '');

        let html = `
            <div class="property-header">
                ${iconHtml}
                <div class="header-text">
                    <h4>${title}</h4>
                    <span class="property-type">${type}</span>
                </div>
            </div>
            <div class="property-content">
        `;

        if (parameters.length === 0) {
            html += '<p class="empty-text">该算子没有可配置参数</p>';
        } else {
            // 按分组组织参数
            const groupedParams = this.groupParameters(parameters);
            
            html += '<form class="property-form" id="property-form">';
            
            // 渲染每个分组
            Object.entries(groupedParams).forEach(([groupName, params], index) => {
                const groupId = `param-group-${index}`;
                const isExpanded = index === 0; // 默认展开第一个分组
                
                html += `
                    <div class="param-group ${isExpanded ? 'expanded' : ''}" data-group="${groupId}">
                        <div class="param-group-header" onclick="this.closest('.param-group').classList.toggle('expanded')">
                            <span class="group-toggle-icon">${isExpanded ? '▼' : '▶'}</span>
                            <span class="group-title">${groupName}</span>
                            <span class="group-count">${params.length}</span>
                        </div>
                        <div class="param-group-content">
                `;
                
                params.forEach(param => {
                    html += this.renderParameterEnhanced(param);
                });
                
                html += '</div></div>';
            });
            
            html += `
                </div>
                <div class="property-actions">
                    <button type="button" class="btn btn-primary" id="btn-apply">应用</button>
                    <button type="button" class="btn" id="btn-reset">重置</button>
                </div>
            </form>
            `;
        }

        html += '</div>';
        this.container.innerHTML = html;

        // 绑定事件
        this.bindEvents();
        this.initSliders();
    }
    
    /**
     * 按分组组织参数
     */
    groupParameters(parameters) {
        const groups = { '基本参数': [] };
        
        parameters.forEach(param => {
            const group = param.group || param.category || '基本参数';
            if (!groups[group]) {
                groups[group] = [];
            }
            groups[group].push(param);
        });
        
        // 如果只有基本参数且有多个，保持原样
        // 否则返回所有分组
        return groups;
    }

    /**
     * 渲染参数控件
     */
    renderParameter(param) {
        const { name, displayName, description, dataType, value, defaultValue, min, max, isRequired } = param;
        
        let inputHtml = '';
        const requiredMark = isRequired ? '<span class="required">*</span>' : '';
        
        switch (dataType) {
            case 'int':
            case 'double':
            case 'float':
                inputHtml = `
                    <input type="number" 
                           id="param-${name}" 
                           name="${name}" 
                           value="${value !== undefined ? value : defaultValue}"
                           ${min !== undefined ? `min="${min}"` : ''}
                           ${max !== undefined ? `max="${max}"` : ''}
                           step="${dataType === 'int' ? 1 : 0.1}"
                           class="form-input"
                           data-type="${dataType}">
                `;
                break;
                
            case 'string':
                inputHtml = `
                    <input type="text" 
                           id="param-${name}" 
                           name="${name}" 
                           value="${value !== undefined ? value : defaultValue || ''}"
                           class="form-input"
                           data-type="string">
                `;
                break;
                
            case 'bool':
            case 'boolean':
                const checked = (value !== undefined ? value : defaultValue) ? 'checked' : '';
                inputHtml = `
                    <label class="switch">
                        <input type="checkbox" 
                               id="param-${name}" 
                               name="${name}" 
                               ${checked}
                               data-type="boolean">
                        <span class="slider"></span>
                    </label>
                `;
                break;
                
            case 'enum':
            case 'select':
                const options = param.options || [];
                inputHtml = `
                    <select id="param-${name}" 
                            name="${name}" 
                            class="form-select"
                            data-type="enum">
                        ${options.map(opt => {
                            const label = typeof opt === 'string' ? opt : (opt.label || opt.Label || 'undefined');
                            const val = typeof opt === 'string' ? opt : (opt.value ?? opt.Value);
                            const currentVal = value !== undefined ? value : defaultValue;
                            return `
                                <option value="${val}" ${val === currentVal ? 'selected' : ''}>
                                    ${label}
                                </option>
                            `;
                        }).join('')}
                    </select>
                `;
                break;
                
            case 'color':
                inputHtml = `
                    <input type="color" 
                           id="param-${name}" 
                           name="${name}" 
                           value="${value !== undefined ? value : defaultValue || '#000000'}"
                           class="form-color"
                           data-type="color">
                `;
                break;
                
            case 'file':
                inputHtml = `
                    <div class="file-picker-wrapper">
                        <input type="text" 
                               id="param-${name}" 
                               name="${name}" 
                               value="${value !== undefined ? value : defaultValue || ''}"
                               class="form-input"
                               readonly
                               data-type="file">
                        <button type="button" class="btn btn-sm btn-secondary btn-pick-file" data-param="${name}">...</button>
                    </div>
                `;
                break;
                
            default:
                inputHtml = `
                    <input type="text" 
                           id="param-${name}" 
                           name="${name}" 
                           value="${value !== undefined ? value : defaultValue || ''}"
                           class="form-input"
                           data-type="${dataType}">
                `;
        }

        return `
            <div class="form-group">
                <label for="param-${name}" class="form-label">
                    ${displayName || name} ${requiredMark}
                </label>
                ${inputHtml}
                ${description ? `<p class="form-description">${description}</p>` : ''}
            </div>
        `;
    }

    /**
     * 渲染增强版参数控件 - 带滑块和颜色选择器
     */
    renderParameterEnhanced(param) {
        const { name, displayName, description, dataType, value, defaultValue, min, max, isRequired, step } = param;
        
        const requiredMark = isRequired ? '<span class="required">*</span>' : '';
        const currentValue = value !== undefined ? value : defaultValue;
        let inputHtml = '';
        
        switch (dataType) {
            case 'int':
            case 'double':
            case 'float':
                // 数值类型：输入框 + 滑块
                const hasRange = min !== undefined && max !== undefined;
                const stepValue = step || (dataType === 'int' ? 1 : 0.1);
                
                inputHtml = `
                    <div class="number-input-wrapper">
                        <input type="number" 
                               id="param-${name}" 
                               name="${name}" 
                               value="${currentValue}"
                               ${min !== undefined ? `min="${min}"` : ''}
                               ${max !== undefined ? `max="${max}"` : ''}
                               step="${stepValue}"
                               class="form-input number-input"
                               data-type="${dataType}">
                        ${hasRange ? `
                            <input type="range" 
                                   class="param-slider"
                                   min="${min}" 
                                   max="${max}" 
                                   step="${stepValue}"
                                   value="${currentValue}"
                                   oninput="document.getElementById('param-${name}').value = this.value; document.getElementById('param-${name}').dispatchEvent(new Event('change'));">
                        ` : ''}
                    </div>
                `;
                break;
                
            case 'color':
                // 增强的颜色选择器
                inputHtml = `
                    <div class="color-picker-wrapper">
                        <input type="color" 
                               id="param-${name}" 
                               name="${name}" 
                               value="${currentValue || '#000000'}"
                               class="form-color-hidden"
                               data-type="color">
                        <div class="color-preview-box" onclick="document.getElementById('param-${name}').click()" style="background-color: ${currentValue || '#000000'}">
                            <span class="color-value">${currentValue || '#000000'}</span>
                        </div>
                    </div>
                `;
                break;
                
            default:
                // 其他类型使用默认渲染
                return this.renderParameter(param);
        }
        
        return `
            <div class="form-group param-enhanced">
                <label for="param-${name}" class="form-label">
                    ${displayName || name} ${requiredMark}
                </label>
                ${inputHtml}
                ${description ? `<p class="form-description">${description}</p>` : ''}
            </div>
        `;
    }

    /**
     * 初始化滑块同步
     */
    initSliders() {
        const sliders = this.container.querySelectorAll('.param-slider');
        sliders.forEach(slider => {
            const targetInput = slider.parentElement.querySelector('input[type="number"]');
            if (targetInput) {
                // 输入框改变时更新滑块
                targetInput.addEventListener('input', () => {
                    slider.value = targetInput.value;
                });
            }
        });
        
        // 颜色选择器预览更新
        const colorInputs = this.container.querySelectorAll('input[type="color"]');
        colorInputs.forEach(input => {
            input.addEventListener('input', (e) => {
                const preview = input.parentElement.querySelector('.color-preview-box');
                const valueText = input.parentElement.querySelector('.color-value');
                if (preview) preview.style.backgroundColor = e.target.value;
                if (valueText) valueText.textContent = e.target.value;
            });
        });
    }

    /**
     * 绑定事件
     */
    bindEvents() {
        const form = document.getElementById('property-form');
        if (!form) return;

        // 应用按钮
        const applyBtn = document.getElementById('btn-apply');
        if (applyBtn) {
            applyBtn.addEventListener('click', () => this.applyChanges());
        }

        // 重置按钮
        const resetBtn = document.getElementById('btn-reset');
        if (resetBtn) {
            resetBtn.addEventListener('click', () => this.resetChanges());
        }

        // 实时更新
        const inputs = form.querySelectorAll('input, select');
        inputs.forEach(input => {
            input.addEventListener('change', () => {
                const values = this.getValues();
                // 【关键修复】实时同步到当前算子对象
                this.updateCurrentOperatorParams(values);
                
                if (this.onChangeCallback) {
                    this.onChangeCallback(values);
                }
            });
        });

        // 文件选择按钮
        const fileBtns = form.querySelectorAll('.btn-pick-file');
        fileBtns.forEach(btn => {
            btn.addEventListener('click', () => {
                const paramName = btn.dataset.param;
                webMessageBridge.sendMessage('PickFileCommand', {
                    parameterName: paramName,
                    filter: 'Image Files|*.bmp;*.jpg;*.png;*.jpeg|All Files|*.*'
                });
            });
        });
    }

    /**
     * 【修复】同步 UI 值到算子参数对象
     * @param {Object} values - 从 getValues() 获取的键值对
     */
    updateCurrentOperatorParams(values) {
        if (!this.currentOperator || !this.currentOperator.parameters) return;
        
        this.currentOperator.parameters.forEach(param => {
            if (values[param.name] !== undefined) {
                // 同时更新 value 和 defaultValue 以适应不同的语义层
                param.value = values[param.name];
                // 如果是新创建的算子可能没有 value 只有 defaultValue，所以也同步一份
                if (param.defaultValue !== undefined) {
                    // 仅当原始定义就支持该字段时同步，避免污染
                    // 注意：这里保持 defaultValue 逻辑是为了兼容 app.js 及 serialize 的旧逻辑
                }
            }
        });
    }

    /**
     * 获取当前值
     */
    getValues() {
        const form = document.getElementById('property-form');
        if (!form) return {};

        const values = {};
        const inputs = form.querySelectorAll('input, select');
        
        inputs.forEach(input => {
            const name = input.name;
            const type = input.dataset.type;
            
            switch (type) {
                case 'int':
                    // 【修复】处理空或非数字情况
                    const intVal = parseInt(input.value, 10);
                    values[name] = isNaN(intVal) ? 0 : intVal;
                    break;
                case 'double':
                case 'float':
                    const floatVal = parseFloat(input.value);
                    values[name] = isNaN(floatVal) ? 0.0 : floatVal;
                    break;
                case 'boolean':
                case 'bool':
                    values[name] = input.checked;
                    break;
                default:
                    values[name] = input.value;
            }
        });

        return values;
    }

    /**
     * 应用更改
     */
    applyChanges() {
        const values = this.getValues();
        
        if (this.currentOperator) {
            // 更新算子参数
            this.currentOperator.parameters.forEach(param => {
                if (values[param.name] !== undefined) {
                    param.value = values[param.name];
                }
            });
        }

        if (this.onChangeCallback) {
            this.onChangeCallback(values);
        }

        // 显示成功提示
        this.showToast('参数已应用', 'success');
    }

    /**
     * 重置更改
     */
    resetChanges() {
        if (this.currentOperator) {
            this.currentOperator.parameters.forEach(param => {
                param.value = param.defaultValue;
            });
        }
        
        this.render();
        
        if (this.onChangeCallback) {
            this.onChangeCallback(this.getValues());
        }

        this.showToast('参数已重置', 'info');
    }

    /**
     * 设置变更回调
     */
    onChange(callback) {
        this.onChangeCallback = callback;
    }

    /**
     * 显示提示
     */
    showToast(message, type = 'info') {
        // 创建提示元素
        const toast = document.createElement('div');
        toast.className = `toast toast-${type}`;
        toast.textContent = message;
        
        document.body.appendChild(toast);
        
        // 动画显示
        setTimeout(() => toast.classList.add('show'), 10);
        
        // 自动隐藏
        setTimeout(() => {
            toast.classList.remove('show');
            setTimeout(() => toast.remove(), 300);
        }, 2000);
    }
}

export default PropertyPanel;
export { PropertyPanel };
