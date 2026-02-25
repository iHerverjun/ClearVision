import webMessageBridge from '../../core/messaging/webMessageBridge.js';
import httpClient from '../../core/messaging/httpClient.js';
import inspectionController from '../inspection/inspectionController.js';
import PreviewPanel from './previewPanel.js';

class PropertyPanel {
    constructor(containerId) {
        this.container = document.getElementById(containerId);
        this.currentOperator = null;
        this.onChangeCallback = null;
        this.previewPanel = null;
        this.pendingRecommendation = null;
        this.recommendedFieldNames = new Set();
        this.recommendationSupportedOperators = new Set([
            'Thresholding',
            'Filtering',
            'GaussianBlur',
            'BlobAnalysis',
            'SharpnessEvaluation'
        ]);
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
        if (!operator || this.currentOperator?.id !== operator.id) {
            this.pendingRecommendation = null;
            this.recommendedFieldNames.clear();
        }
        this.currentOperator = operator;
        this.render();
    }

    /**
     * 清空面板
     */
    clear() {
        if (this.previewPanel) {
            this.previewPanel.destroy();
            this.previewPanel = null;
        }
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
        const canRecommend = this.canRecommend(type);
        
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
                <div class="property-header-actions">
                    ${canRecommend ? `<button type="button" class="btn btn-secondary btn-recommend" id="btn-recommend">智能推荐</button>` : ''}
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
                <div class="recommendation-actions ${this.pendingRecommendation ? '' : 'hidden'}" id="recommendation-actions">
                    <span>已应用智能推荐参数</span>
                    <div class="recommendation-buttons">
                        <button type="button" class="btn btn-primary btn-sm" id="btn-accept-recommendation">接受</button>
                        <button type="button" class="btn btn-sm" id="btn-revert-recommendation">撤销</button>
                    </div>
                </div>
                <div class="property-actions">
                    <button type="button" class="btn btn-primary" id="btn-apply">应用</button>
                    <button type="button" class="btn" id="btn-reset">重置</button>
                </div>
            </form>
            `;
        }

        html += `
                <div id="operator-preview-container"></div>
            </div>
        `;
        this.container.innerHTML = html;

        // 绑定事件
        this.bindEvents();
        this.initSliders();
        this.initPreviewPanel();

        if (this.pendingRecommendation) {
            this._restoreRecommendationHighlights();
        }
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
                        <input type="text" id="param-${name}" name="${name}" value="${value !== undefined ? value : defaultValue || ''}" class="form-input" readonly data-type="file">
                        <button type="button" class="btn btn-sm btn-secondary btn-pick-file" data-param="${name}">...</button>
                    </div>
                `;
                break;
                
            case 'cameraBinding':
                // 从 window.cv_config 或 API 获取绑定列表
                const config = window.cv_config || {};
                const bindings = config.cameras || [];
                inputHtml = `
                    <select id="param-${name}" name="${name}" class="form-select" data-type="string">
                        <option value="">-- 请选择相机 --</option>
                        ${bindings.map(b => `
                            <option value="${b.id}" ${b.id === currentValue ? 'selected' : ''}>
                                ${b.displayName} (${b.serialNumber})
                            </option>
                        `).join('')}
                    </select>
                    ${bindings.length === 0 ? '<p class="form-description error">未检测到全局相机配置，请在“系统设置”中添加。</p>' : ''}
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
        const recommendBtn = document.getElementById('btn-recommend');
        if (recommendBtn) {
            recommendBtn.addEventListener('click', () => this.recommendParameters());
        }

        const acceptBtn = document.getElementById('btn-accept-recommendation');
        if (acceptBtn) {
            acceptBtn.addEventListener('click', () => this.acceptRecommendation());
        }

        const revertBtn = document.getElementById('btn-revert-recommendation');
        if (revertBtn) {
            revertBtn.addEventListener('click', () => this.revertRecommendation());
        }

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
                this._notifyValueChanged();
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
            if (!name || input.type === 'range') {
                return;
            }

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
        this._notifyValueChanged();

        // 显示成功提示
        this.showToast('参数已应用', 'success');
    }

    /**
     * 初始化预览面板
     */
    initPreviewPanel() {
        const container = this.container.querySelector('#operator-preview-container');
        if (!container) {
            if (this.previewPanel) {
                this.previewPanel.destroy();
                this.previewPanel = null;
            }
            return;
        }

        if (this.previewPanel) {
            this.previewPanel.destroy();
        }

        this.previewPanel = new PreviewPanel(container, {
            getOperator: () => this.currentOperator,
            getParameters: () => this.getValues(),
            getInputImageBase64: () => this.resolveInputImageBase64(),
            debounceMs: 500
        });

        this.previewPanel.scheduleAutoPreview();
    }

    /**
     * 当前算子是否支持智能推荐
     */
    canRecommend(type) {
        return this.recommendationSupportedOperators.has(type);
    }

    /**
     * 参数变更后的统一通知
     */
    _notifyValueChanged() {
        const values = this.getValues();
        this.updateCurrentOperatorParams(values);

        if (this.onChangeCallback) {
            this.onChangeCallback(values);
        }

        if (this.previewPanel) {
            this.previewPanel.scheduleAutoPreview();
        }

        return values;
    }

    async recommendParameters() {
        if (!this.currentOperator || !this.canRecommend(this.currentOperator.type)) {
            return;
        }

        const recommendBtn = document.getElementById('btn-recommend');
        if (recommendBtn) {
            recommendBtn.disabled = true;
            recommendBtn.textContent = '推荐中...';
        }

        try {
            const imageBase64 = await this.resolveInputImageBase64();
            if (!imageBase64) {
                this.showToast('未找到可用输入图像，请先执行一次检测流程', 'warning');
                return;
            }

            const response = await httpClient.post(
                `/operators/${encodeURIComponent(this.currentOperator.type)}/recommend-parameters`,
                { imageBase64 }
            );

            const recommended = response?.parameters || response?.Parameters || {};
            if (Object.keys(recommended).length === 0) {
                this.showToast('该算子暂无可推荐参数', 'info');
                return;
            }

            const changedCount = this.applyRecommendedValues(recommended);
            if (changedCount === 0) {
                this.showToast('推荐结果与当前参数一致', 'info');
                return;
            }

            this.showToast(`已应用 ${changedCount} 个推荐参数`, 'success');
        } catch (error) {
            console.error('[PropertyPanel] 参数推荐失败:', error);
            this.showToast(`参数推荐失败: ${error.message}`, 'error');
        } finally {
            if (recommendBtn) {
                recommendBtn.disabled = false;
                recommendBtn.textContent = '智能推荐';
            }
        }
    }

    applyRecommendedValues(recommendedValues) {
        const form = document.getElementById('property-form');
        if (!form || !recommendedValues) return 0;

        const previousValues = this.getValues();
        const allInputs = Array.from(form.querySelectorAll('input[name], select[name]'));

        this._clearRecommendationHighlights();
        this.recommendedFieldNames.clear();

        Object.entries(recommendedValues).forEach(([name, value]) => {
            const input = allInputs.find(item =>
                item.name && item.name.toLowerCase() === String(name).toLowerCase());
            if (!input) return;

            const oldValue = this._readInputValue(input);
            this._writeInputValue(input, value);
            const newValue = this._readInputValue(input);

            if (JSON.stringify(oldValue) === JSON.stringify(newValue)) {
                return;
            }

            this.recommendedFieldNames.add(input.name);
            const group = input.closest('.form-group');
            if (group) {
                group.classList.add('param-recommended');
            }
        });

        if (this.recommendedFieldNames.size === 0) {
            return 0;
        }

        this.pendingRecommendation = {
            previousValues,
            fields: Array.from(this.recommendedFieldNames)
        };
        this._toggleRecommendationActions(true);
        this._notifyValueChanged();

        return this.recommendedFieldNames.size;
    }

    acceptRecommendation() {
        this.pendingRecommendation = null;
        this._clearRecommendationHighlights();
        this._toggleRecommendationActions(false);
        this.showToast('已接受推荐参数', 'success');
    }

    revertRecommendation() {
        if (!this.pendingRecommendation?.previousValues) {
            return;
        }

        this._applyValuesToForm(this.pendingRecommendation.previousValues);
        this.pendingRecommendation = null;
        this._clearRecommendationHighlights();
        this._toggleRecommendationActions(false);
        this._notifyValueChanged();
        this.showToast('已撤销推荐参数', 'info');
    }

    _toggleRecommendationActions(visible) {
        const actions = document.getElementById('recommendation-actions');
        if (!actions) return;

        actions.classList.toggle('hidden', !visible);
    }

    _clearRecommendationHighlights() {
        this.container.querySelectorAll('.param-recommended').forEach(element => {
            element.classList.remove('param-recommended');
        });
    }

    _restoreRecommendationHighlights() {
        const form = document.getElementById('property-form');
        if (!form || this.recommendedFieldNames.size === 0) {
            this._toggleRecommendationActions(false);
            return;
        }

        const inputs = form.querySelectorAll('input[name], select[name]');
        inputs.forEach(input => {
            if (this.recommendedFieldNames.has(input.name)) {
                const group = input.closest('.form-group');
                if (group) {
                    group.classList.add('param-recommended');
                }
            }
        });

        this._toggleRecommendationActions(true);
    }

    _readInputValue(input) {
        const type = input.dataset.type;
        if (type === 'boolean' || type === 'bool') {
            return Boolean(input.checked);
        }

        if (type === 'int') {
            const value = parseInt(input.value, 10);
            return Number.isNaN(value) ? 0 : value;
        }

        if (type === 'double' || type === 'float') {
            const value = parseFloat(input.value);
            return Number.isNaN(value) ? 0 : value;
        }

        return input.value;
    }

    _writeInputValue(input, rawValue) {
        const type = input.dataset.type;
        if (type === 'boolean' || type === 'bool') {
            input.checked = this._toBoolean(rawValue);
            return;
        }

        if (type === 'int') {
            input.value = `${parseInt(rawValue, 10) || 0}`;
        } else if (type === 'double' || type === 'float') {
            const parsed = parseFloat(rawValue);
            input.value = `${Number.isNaN(parsed) ? 0 : parsed}`;
        } else {
            input.value = rawValue ?? '';
        }

        // 数值输入框存在滑块时，同步滑块值
        const slider = input.parentElement?.querySelector('.param-slider');
        if (slider) {
            slider.value = input.value;
        }

        // 颜色选择器预览同步
        if (input.type === 'color') {
            const preview = input.parentElement?.querySelector('.color-preview-box');
            const valueText = input.parentElement?.querySelector('.color-value');
            if (preview) preview.style.backgroundColor = input.value;
            if (valueText) valueText.textContent = input.value;
        }
    }

    _applyValuesToForm(values) {
        const form = document.getElementById('property-form');
        if (!form) return;

        const inputs = form.querySelectorAll('input[name], select[name]');
        inputs.forEach(input => {
            if (!Object.prototype.hasOwnProperty.call(values, input.name)) {
                return;
            }

            this._writeInputValue(input, values[input.name]);
        });
    }

    _toBoolean(value) {
        if (typeof value === 'boolean') {
            return value;
        }

        return String(value).toLowerCase() === 'true';
    }

    async resolveInputImageBase64() {
        const inspectionResult = window._lastInspectionResult || inspectionController.getLastResult?.();
        const fromResult = this.extractImageBase64(inspectionResult);
        if (fromResult) {
            return fromResult;
        }

        return null;
    }

    extractImageBase64(result) {
        if (!result || typeof result !== 'object') {
            return null;
        }

        const candidateKeys = [
            'outputImage',
            'OutputImage',
            'imageBase64',
            'ImageBase64',
            'resultImageBase64',
            'ResultImageBase64',
            'inputImage',
            'InputImage'
        ];

        for (const key of candidateKeys) {
            const value = result[key];
            if (typeof value === 'string' && value.trim()) {
                return this.normalizeBase64Image(value);
            }
        }

        const outputData = result.outputData || result.OutputData;
        if (outputData && typeof outputData === 'object') {
            for (const value of Object.values(outputData)) {
                if (typeof value === 'string' && (value.startsWith('data:image/') || value.length > 128)) {
                    return this.normalizeBase64Image(value);
                }
            }
        }

        return null;
    }

    normalizeBase64Image(imageValue) {
        if (!imageValue || typeof imageValue !== 'string') {
            return null;
        }

        const trimmed = imageValue.trim();
        const commaIndex = trimmed.indexOf(',');
        if (trimmed.startsWith('data:image/') && commaIndex > 0) {
            return trimmed.substring(commaIndex + 1);
        }

        return trimmed;
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
        this.pendingRecommendation = null;
        this.recommendedFieldNames.clear();
        this.render();
        this._notifyValueChanged();

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
