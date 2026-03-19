import httpClient from '../../core/messaging/httpClient.js';
import { showToast } from '../../shared/components/uiComponents.js';

/**
 * 流程模板选择器
 * - 拉取模板列表并支持按名称/行业/标签过滤
 * - 选择模板后将模板 FlowJson 转换为画布可反序列化结构
 */
export class TemplateSelector {
    constructor(flowCanvas) {
        this.flowCanvas = flowCanvas;
        this.templates = [];
        this.operatorMetadata = new Map();
        this.activeTag = '';
        this.activeTemplateId = null;
        this.isLoading = false;

        this.overlay = null;
        this.dialog = null;
    }

    async open() {
        this._ensureUi();
        this.overlay.classList.remove('hidden');

        await this._ensureDataLoaded();
        this._renderFilters();
        this._renderTemplateCards();
        this._updateActionButtons();
    }

    close() {
        if (this.overlay) {
            this.overlay.classList.add('hidden');
        }
    }

    async _ensureDataLoaded() {
        if (this.isLoading) {
            return;
        }

        this.isLoading = true;
        try {
            await Promise.all([
                this._loadOperatorMetadata(),
                this._loadTemplates()
            ]);
        } finally {
            this.isLoading = false;
        }
    }

    async _loadTemplates() {
        const templates = await httpClient.get('/templates');
        this.templates = Array.isArray(templates) ? templates : [];
        if (this.activeTemplateId && !this.templates.some(item => String(item.id) === String(this.activeTemplateId))) {
            this.activeTemplateId = null;
        }
    }

    async _loadOperatorMetadata() {
        // 优先复用已加载的算子库，避免额外请求。
        const cachedOperators = window.operatorLibraryPanel?.getOperators?.() ?? [];
        const operators = cachedOperators.length > 0
            ? cachedOperators
            : await httpClient.get('/operators/library');

        this.operatorMetadata.clear();
        for (const operator of operators || []) {
            if (!operator?.type) {
                continue;
            }

            const typeKey = String(operator.type).toLowerCase();
            this.operatorMetadata.set(typeKey, operator);
        }
    }

    _ensureUi() {
        if (this.overlay) {
            return;
        }

        this.overlay = document.createElement('div');
        this.overlay.className = 'template-selector-overlay hidden';
        this.overlay.innerHTML = `
            <div class="template-selector-dialog" role="dialog" aria-modal="true" aria-label="流程模板选择器">
                <div class="template-selector-header">
                    <div>
                        <h3>从模板创建流程</h3>
                        <p>选择预设模板，快速生成可运行的流程骨架。</p>
                    </div>
                    <div class="template-selector-actions">
                        <button type="button" class="btn btn-secondary" id="btn-template-save">另存为模板</button>
                        <button type="button" class="btn btn-secondary" id="btn-template-update">更新当前模板</button>
                        <button type="button" class="btn btn-secondary" id="btn-template-close">关闭</button>
                    </div>
                </div>

                <div class="template-selector-filters">
                    <input
                        id="template-search-input"
                        class="cv-input"
                        type="text"
                        placeholder="搜索模板名称或描述..."
                        autocomplete="off" />
                    <select id="template-industry-select" class="cv-input"></select>
                </div>

                <div class="template-tags" id="template-tags"></div>
                <div class="template-selector-body" id="template-selector-body"></div>
            </div>
        `;

        document.body.appendChild(this.overlay);
        this.dialog = this.overlay.querySelector('.template-selector-dialog');

        const closeBtn = this.overlay.querySelector('#btn-template-close');
        closeBtn.addEventListener('click', () => this.close());
        const saveBtn = this.overlay.querySelector('#btn-template-save');
        saveBtn.addEventListener('click', () => this._saveAsTemplate());
        const updateBtn = this.overlay.querySelector('#btn-template-update');
        updateBtn.addEventListener('click', () => this._updateActiveTemplate());

        this.overlay.addEventListener('click', (event) => {
            if (event.target === this.overlay) {
                this.close();
            }
        });

        const searchInput = this.overlay.querySelector('#template-search-input');
        searchInput.addEventListener('input', () => this._renderTemplateCards());

        const industrySelect = this.overlay.querySelector('#template-industry-select');
        industrySelect.addEventListener('change', () => this._renderTemplateCards());

        this.overlay.addEventListener('keydown', (event) => {
            if (event.key === 'Escape') {
                this.close();
            }
        });

        this._updateActionButtons();
    }

    _renderFilters() {
        if (!this.overlay) {
            return;
        }

        const industrySelect = this.overlay.querySelector('#template-industry-select');
        const industries = Array.from(
            new Set(this.templates.map(item => item.industry).filter(Boolean))
        ).sort((a, b) => a.localeCompare(b, 'zh-CN'));

        const previousValue = industrySelect.value;
        industrySelect.innerHTML = `
            <option value="">全部行业</option>
            ${industries.map(industry => `<option value="${industry}">${industry}</option>`).join('')}
        `;
        industrySelect.value = industries.includes(previousValue) ? previousValue : '';

        const tagsContainer = this.overlay.querySelector('#template-tags');
        const allTags = Array.from(
            new Set(this.templates.flatMap(item => item.tags || []).filter(Boolean))
        ).sort((a, b) => a.localeCompare(b, 'zh-CN'));

        const tagButtons = [
            `<button type="button" class="template-tag-btn ${this.activeTag === '' ? 'active' : ''}" data-tag="">全部标签</button>`,
            ...allTags.map(tag => `
                <button type="button" class="template-tag-btn ${this.activeTag === tag ? 'active' : ''}" data-tag="${tag}">
                    ${tag}
                </button>
            `)
        ];

        tagsContainer.innerHTML = tagButtons.join('');
        tagsContainer.querySelectorAll('.template-tag-btn').forEach(button => {
            button.addEventListener('click', () => {
                this.activeTag = button.dataset.tag || '';
                this._renderFilters();
                this._renderTemplateCards();
            });
        });
    }

    _renderTemplateCards() {
        if (!this.overlay) {
            return;
        }

        const body = this.overlay.querySelector('#template-selector-body');
        const searchKeyword = this.overlay.querySelector('#template-search-input').value.trim().toLowerCase();
        const selectedIndustry = this.overlay.querySelector('#template-industry-select').value;

        const filteredTemplates = this.templates.filter(template => {
            const name = (template.name || '').toLowerCase();
            const description = (template.description || '').toLowerCase();
            const tags = (template.tags || []).map(tag => String(tag).toLowerCase());
            const industry = template.industry || '';

            const matchKeyword = !searchKeyword
                || name.includes(searchKeyword)
                || description.includes(searchKeyword)
                || tags.some(tag => tag.includes(searchKeyword));
            const matchIndustry = !selectedIndustry || industry === selectedIndustry;
            const matchTag = !this.activeTag || (template.tags || []).includes(this.activeTag);

            return matchKeyword && matchIndustry && matchTag;
        });

        if (filteredTemplates.length === 0) {
            body.innerHTML = `
                <div class="template-empty-state">
                    <p>没有匹配的模板，请调整筛选条件。</p>
                </div>
            `;
            return;
        }

        body.innerHTML = `
            <div class="template-card-grid">
                ${filteredTemplates.map(template => {
                    const operatorCount = this._estimateOperatorCount(template.flowJson);
                    const tags = (template.tags || []).map(tag => `<span class="template-card-tag">${tag}</span>`).join('');
                    return `
                        <article class="template-card">
                            <div class="template-card-head">
                                <h4>${template.name || '未命名模板'}</h4>
                                <span class="template-card-industry">${template.industry || '通用'}</span>
                            </div>
                            <p class="template-card-description">${template.description || '暂无描述'}</p>
                            <div class="template-card-meta">
                                <span>${operatorCount} 个算子</span>
                                <span>${(template.tags || []).length} 个标签</span>
                            </div>
                            <div class="template-card-tags">${tags}</div>
                            <div class="template-card-actions">
                                <button type="button" class="btn btn-primary btn-template-use" data-id="${template.id}">
                                    使用模板
                                </button>
                            </div>
                        </article>
                    `;
                }).join('')}
            </div>
        `;

        body.querySelectorAll('.btn-template-use').forEach(button => {
            button.addEventListener('click', async () => {
                const templateId = button.dataset.id;
                if (!templateId) {
                    return;
                }

                button.disabled = true;
                try {
                    await this._applyTemplate(templateId);
                } catch (error) {
                    console.error('[TemplateSelector] 应用模板失败:', error);
                    showToast(`应用模板失败: ${error.message}`, 'error');
                } finally {
                    button.disabled = false;
                }
            });
        });
    }

    async _applyTemplate(templateId) {
        let template = this.templates.find(item => String(item.id) === String(templateId));
        if (!template || !template.flowJson) {
            template = await httpClient.get(`/templates/${templateId}`);
        }

        if (!template?.flowJson) {
            throw new Error('模板内容为空，无法应用。');
        }

        const flowData = this._convertTemplateToCanvasFlow(template);
        this.flowCanvas.deserialize(flowData);
        this.flowCanvas.selectedNode = null;
        if (typeof this.flowCanvas.onNodeSelected === 'function') {
            this.flowCanvas.onNodeSelected(null);
        }

        this.activeTemplateId = template.id || null;
        this._updateActionButtons();
        showToast(`模板已应用：${template.name}`, 'success');
        this.close();
    }

    _convertTemplateToCanvasFlow(template) {
        const parsedFlow = typeof template.flowJson === 'string'
            ? JSON.parse(template.flowJson)
            : template.flowJson;

        if (this._isCanvasFlow(parsedFlow)) {
            return parsedFlow;
        }

        const operators = parsedFlow?.operators || [];
        const connections = parsedFlow?.connections || [];
        if (operators.length === 0) {
            throw new Error('模板未包含任何算子。');
        }

        const layout = this._buildLayout(operators, connections);
        const requiredInputPorts = this._collectRequiredPorts(connections, 'targetTempId', 'targetPortName');
        const requiredOutputPorts = this._collectRequiredPorts(connections, 'sourceTempId', 'sourcePortName');

        const nodeMapping = new Map(); // tempId -> { id, inputs, outputs }
        const operatorDtos = operators.map((operator, index) => {
            const tempId = operator.tempId || `temp_${index}`;
            const operatorType = operator.operatorType || operator.type;
            const metadata = this.operatorMetadata.get(String(operatorType).toLowerCase());

            const inputs = this._buildPorts(
                metadata?.inputPorts,
                requiredInputPorts.get(tempId),
                true
            );
            const outputs = this._buildPorts(
                metadata?.outputPorts,
                requiredOutputPorts.get(tempId),
                false
            );
            const parameterList = this._buildParameterList(
                metadata?.parameters,
                operator.parameters
            );

            const nodeId = this._generateId();
            const position = layout.get(tempId) || { x: 120 + index * 220, y: 120 };

            const dto = {
                id: nodeId,
                name: operator.displayName || metadata?.displayName || operatorType,
                type: operatorType,
                x: position.x,
                y: position.y,
                isEnabled: true,
                inputPorts: inputs,
                outputPorts: outputs,
                parameters: parameterList
            };

            nodeMapping.set(tempId, {
                id: nodeId,
                inputs,
                outputs
            });

            return dto;
        });

        const connectionDtos = [];
        for (const connection of connections) {
            const source = nodeMapping.get(connection.sourceTempId);
            const target = nodeMapping.get(connection.targetTempId);
            if (!source || !target) {
                continue;
            }

            const sourcePort = this._findPortByName(source.outputs, connection.sourcePortName);
            const targetPort = this._findPortByName(target.inputs, connection.targetPortName);
            if (!sourcePort || !targetPort) {
                continue;
            }

            connectionDtos.push({
                id: this._generateId(),
                sourceOperatorId: source.id,
                sourcePortId: sourcePort.id,
                targetOperatorId: target.id,
                targetPortId: targetPort.id
            });
        }

        return {
            operators: operatorDtos,
            connections: connectionDtos
        };
    }

    _isCanvasFlow(flow) {
        if (!flow || !Array.isArray(flow.operators) || !Array.isArray(flow.connections)) {
            return false;
        }

        if (flow.operators.length === 0) {
            return false;
        }

        const firstOperator = flow.operators[0];
        return Boolean(firstOperator.id && firstOperator.type);
    }

    _updateActionButtons() {
        if (!this.overlay) {
            return;
        }

        const updateBtn = this.overlay.querySelector('#btn-template-update');
        if (!updateBtn) {
            return;
        }

        const hasActiveTemplate = Boolean(this.activeTemplateId);
        updateBtn.disabled = !hasActiveTemplate;
        updateBtn.title = hasActiveTemplate ? '' : '请先应用一个模板后再更新';
    }

    async _saveAsTemplate() {
        try {
            const payload = this._buildTemplatePayload();
            const created = await httpClient.post('/templates', payload);
            if (created?.id) {
                this.activeTemplateId = created.id;
            }
            await this._loadTemplates();
            this._renderFilters();
            this._renderTemplateCards();
            this._updateActionButtons();
            showToast('模板保存成功', 'success');
        } catch (error) {
            if (error?.message === '已取消保存。') {
                showToast('已取消保存模板', 'info');
                return;
            }
            console.error('[TemplateSelector] 保存模板失败:', error);
            showToast(`保存模板失败: ${error.message}`, 'error');
        }
    }

    async _updateActiveTemplate() {
        const activeTemplate = this.templates.find(item => String(item.id) === String(this.activeTemplateId));
        if (!activeTemplate?.id) {
            showToast('请先应用一个模板，再执行更新。', 'warning');
            return;
        }

        try {
            const payload = this._buildTemplatePayload(activeTemplate);
            await httpClient.put(`/templates/${activeTemplate.id}`, payload);
            await this._loadTemplates();
            this._renderFilters();
            this._renderTemplateCards();
            this._updateActionButtons();
            showToast('模板更新成功', 'success');
        } catch (error) {
            if (error?.message === '已取消保存。') {
                showToast('已取消更新模板', 'info');
                return;
            }
            console.error('[TemplateSelector] 更新模板失败:', error);
            showToast(`更新模板失败: ${error.message}`, 'error');
        }
    }

    _buildTemplatePayload(existingTemplate = null) {
        if (!this.flowCanvas || typeof this.flowCanvas.serialize !== 'function') {
            throw new Error('当前画布不支持序列化，无法保存模板。');
        }

        const flowData = this.flowCanvas.serialize();
        if (!flowData || !Array.isArray(flowData.operators) || flowData.operators.length === 0) {
            throw new Error('当前流程为空，无法保存模板。');
        }

        const defaultName = existingTemplate?.name || `自定义模板-${new Date().toISOString().slice(0, 10)}`;
        const defaultDescription = existingTemplate?.description || '';
        const defaultIndustry = existingTemplate?.industry || '';
        const defaultTags = Array.isArray(existingTemplate?.tags) ? existingTemplate.tags.join(',') : '';

        const name = window.prompt('请输入模板名称：', defaultName);
        if (name === null) {
            throw new Error('已取消保存。');
        }
        if (!String(name).trim()) {
            throw new Error('模板名称不能为空。');
        }

        const description = window.prompt('请输入模板描述（可留空）：', defaultDescription);
        if (description === null) {
            throw new Error('已取消保存。');
        }

        const industry = window.prompt('请输入行业（可留空）：', defaultIndustry);
        if (industry === null) {
            throw new Error('已取消保存。');
        }

        const tagsInput = window.prompt('请输入标签（逗号分隔，可留空）：', defaultTags);
        if (tagsInput === null) {
            throw new Error('已取消保存。');
        }

        return {
            name: String(name).trim(),
            description: String(description).trim(),
            industry: String(industry).trim(),
            tags: this._parseTags(tagsInput),
            flowData
        };
    }

    _parseTags(tagsInput) {
        if (!tagsInput) {
            return [];
        }

        return String(tagsInput)
            .split(',')
            .map(tag => tag.trim())
            .filter(Boolean);
    }

    _buildLayout(operators, connections) {
        const depth = new Map();
        operators.forEach(op => depth.set(op.tempId, 0));

        // DAG 近似层级布局，避免全部算子叠在一起。
        for (let i = 0; i < operators.length; i++) {
            for (const conn of connections) {
                const sourceDepth = depth.get(conn.sourceTempId) ?? 0;
                const targetDepth = depth.get(conn.targetTempId) ?? 0;
                if (targetDepth < sourceDepth + 1) {
                    depth.set(conn.targetTempId, sourceDepth + 1);
                }
            }
        }

        const laneCounter = new Map();
        const layout = new Map();

        for (const operator of operators) {
            const level = depth.get(operator.tempId) ?? 0;
            const lane = laneCounter.get(level) ?? 0;
            laneCounter.set(level, lane + 1);

            layout.set(operator.tempId, {
                x: 140 + level * 250,
                y: 120 + lane * 150
            });
        }

        return layout;
    }

    _collectRequiredPorts(connections, ownerKey, portNameKey) {
        const result = new Map();
        for (const connection of connections) {
            const ownerId = connection[ownerKey];
            const portName = connection[portNameKey];
            if (!ownerId || !portName) {
                continue;
            }

            if (!result.has(ownerId)) {
                result.set(ownerId, new Set());
            }

            result.get(ownerId).add(String(portName));
        }

        return result;
    }

    _buildPorts(metadataPorts, requiredPortNames, isInput) {
        const ports = [];
        const existingNames = new Set();

        for (const metadataPort of metadataPorts || []) {
            const name = metadataPort.name || metadataPort.Name;
            if (!name) {
                continue;
            }

            const dataType = metadataPort.dataType || metadataPort.DataType || 'Any';
            ports.push({
                id: this._generateId(),
                name,
                dataType,
                direction: isInput ? 0 : 1,
                isRequired: Boolean(metadataPort.isRequired ?? metadataPort.IsRequired)
            });
            existingNames.add(String(name).toLowerCase());
        }

        for (const requiredName of requiredPortNames || []) {
            if (existingNames.has(String(requiredName).toLowerCase())) {
                continue;
            }

            ports.push({
                id: this._generateId(),
                name: requiredName,
                dataType: 'Any',
                direction: isInput ? 0 : 1,
                isRequired: false
            });
        }

        if (ports.length === 0) {
            ports.push({
                id: this._generateId(),
                name: isInput ? 'Input' : 'Output',
                dataType: 'Any',
                direction: isInput ? 0 : 1,
                isRequired: false
            });
        }

        return ports;
    }

    _buildParameterList(metadataParameters, templateParameters) {
        const values = templateParameters || {};
        const list = [];
        const seen = new Set();

        for (const parameter of metadataParameters || []) {
            const name = parameter.name || parameter.Name;
            if (!name) {
                continue;
            }

            const dataType = parameter.dataType || parameter.DataType || 'string';
            const value = this._resolveTemplateValue(values, name);
            list.push({
                id: this._generateId(),
                name,
                displayName: parameter.displayName || parameter.DisplayName || name,
                description: parameter.description || parameter.Description || '',
                dataType,
                defaultValue: parameter.defaultValue ?? parameter.DefaultValue ?? null,
                min: parameter.min ?? parameter.Min ?? parameter.minValue ?? parameter.MinValue,
                max: parameter.max ?? parameter.Max ?? parameter.maxValue ?? parameter.MaxValue,
                options: parameter.options ?? parameter.Options ?? null,
                value: value !== undefined
                    ? this._convertValueByType(value, dataType)
                    : (parameter.defaultValue ?? parameter.DefaultValue ?? null)
            });
            seen.add(String(name).toLowerCase());
        }

        // 模板中存在但元数据中没有的参数，保留为 string 参数避免信息丢失。
        for (const [key, value] of Object.entries(values)) {
            if (seen.has(String(key).toLowerCase())) {
                continue;
            }

            list.push({
                id: this._generateId(),
                name: key,
                displayName: key,
                description: '',
                dataType: 'string',
                defaultValue: '',
                value: value ?? ''
            });
        }

        return list;
    }

    _resolveTemplateValue(values, parameterName) {
        if (!values || typeof values !== 'object') {
            return undefined;
        }

        if (Object.prototype.hasOwnProperty.call(values, parameterName)) {
            return values[parameterName];
        }

        const lowerName = String(parameterName).toLowerCase();
        const matchedKey = Object.keys(values).find(key => key.toLowerCase() === lowerName);
        return matchedKey ? values[matchedKey] : undefined;
    }

    _convertValueByType(rawValue, dataType) {
        const normalizedType = String(dataType || '').toLowerCase();

        if (normalizedType === 'int' || normalizedType === 'integer') {
            const parsed = parseInt(rawValue, 10);
            return Number.isNaN(parsed) ? 0 : parsed;
        }

        if (normalizedType === 'double' || normalizedType === 'float' || normalizedType === 'number') {
            const parsed = parseFloat(rawValue);
            return Number.isNaN(parsed) ? 0 : parsed;
        }

        if (normalizedType === 'bool' || normalizedType === 'boolean') {
            if (typeof rawValue === 'boolean') {
                return rawValue;
            }

            return String(rawValue).toLowerCase() === 'true';
        }

        return rawValue;
    }

    _findPortByName(ports, expectedName) {
        if (!expectedName) {
            return null;
        }

        const expected = String(expectedName).toLowerCase();
        return (ports || []).find(port => String(port.name).toLowerCase() === expected) ?? null;
    }

    _estimateOperatorCount(flowJson) {
        try {
            const parsed = typeof flowJson === 'string' ? JSON.parse(flowJson) : flowJson;
            return parsed?.operators?.length ?? 0;
        } catch {
            return 0;
        }
    }

    _generateId() {
        if (window.crypto && typeof window.crypto.randomUUID === 'function') {
            return window.crypto.randomUUID();
        }

        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (char) => {
            const rand = Math.random() * 16 | 0;
            const value = char === 'x' ? rand : ((rand & 0x3) | 0x8);
            return value.toString(16);
        });
    }
}

export default TemplateSelector;
