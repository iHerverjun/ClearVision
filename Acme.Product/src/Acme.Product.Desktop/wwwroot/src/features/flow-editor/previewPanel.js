import httpClient from '../../core/messaging/httpClient.js';

/**
 * 单算子预览面板
 * - 手动刷新
 * - 参数变更后自动预览（防抖）
 */
export class PreviewPanel {
    constructor(container, options = {}) {
        this.container = container;
        this.getOperator = options.getOperator ?? (() => null);
        this.getParameters = options.getParameters ?? (() => ({}));
        this.getInputImageBase64 = options.getInputImageBase64 ?? (() => null);
        this.debounceMs = options.debounceMs ?? 500;

        this.autoPreviewEnabled = true;
        this.collapsed = false;
        this._debounceTimer = null;

        this.render();
    }

    destroy() {
        if (this._debounceTimer) {
            clearTimeout(this._debounceTimer);
            this._debounceTimer = null;
        }
    }

    render() {
        if (!this.container) {
            return;
        }

        this.container.innerHTML = `
            <section class="operator-preview-panel ${this.collapsed ? 'collapsed' : ''}">
                <header class="operator-preview-header">
                    <button type="button" class="operator-preview-toggle" id="btn-preview-toggle">
                        ${this.collapsed ? '▶' : '▼'} 预览
                    </button>
                    <div class="operator-preview-actions">
                        <label class="operator-preview-auto">
                            <input type="checkbox" id="preview-auto-toggle" ${this.autoPreviewEnabled ? 'checked' : ''}/>
                            自动预览
                        </label>
                        <button type="button" class="btn btn-secondary btn-preview-refresh" id="btn-preview-refresh">
                            刷新预览
                        </button>
                    </div>
                </header>

                <div class="operator-preview-body" id="operator-preview-body">
                    <div class="operator-preview-images">
                        <div class="operator-preview-image-card">
                            <div class="title">输入</div>
                            <img id="preview-before-image" alt="输入图像预览" />
                            <div class="placeholder" id="preview-before-placeholder">暂无输入图像</div>
                        </div>
                        <div class="operator-preview-image-card">
                            <div class="title">输出</div>
                            <img id="preview-after-image" alt="输出图像预览" />
                            <div class="placeholder" id="preview-after-placeholder">尚未执行预览</div>
                        </div>
                    </div>

                    <div class="operator-preview-meta">
                        <div class="operator-preview-status" id="preview-status-text">等待预览</div>
                        <div class="operator-preview-outputs" id="preview-output-list">暂无输出摘要</div>
                    </div>
                </div>
            </section>
        `;

        const toggleBtn = this.container.querySelector('#btn-preview-toggle');
        toggleBtn?.addEventListener('click', () => {
            this.collapsed = !this.collapsed;
            this.render();
        });

        const autoToggle = this.container.querySelector('#preview-auto-toggle');
        autoToggle?.addEventListener('change', (event) => {
            this.autoPreviewEnabled = event.target.checked;
        });

        const refreshBtn = this.container.querySelector('#btn-preview-refresh');
        refreshBtn?.addEventListener('click', async () => {
            await this.refresh();
        });
    }

    scheduleAutoPreview() {
        if (!this.autoPreviewEnabled || this.collapsed) {
            return;
        }

        if (this._debounceTimer) {
            clearTimeout(this._debounceTimer);
        }

        this._debounceTimer = setTimeout(() => {
            this.refresh();
        }, this.debounceMs);
    }

    async refresh() {
        const operator = this.getOperator();
        if (!operator?.type) {
            this._setStatus('未选中算子');
            return;
        }

        const inputImageBase64 = await Promise.resolve(this.getInputImageBase64());
        if (!inputImageBase64) {
            this._setStatus('缺少输入图像，请先执行一次检测');
            this._setImage('before', null);
            this._setImage('after', null);
            return;
        }

        const parameters = this.getParameters() || {};
        this._setStatus('预览执行中...');
        this._setImage('before', inputImageBase64);

        try {
            const response = await httpClient.post(`/operators/${encodeURIComponent(operator.type)}/preview`, {
                imageBase64: inputImageBase64,
                parameters
            });

            const isSuccess = Boolean(response?.isSuccess ?? response?.IsSuccess);
            if (!isSuccess) {
                const errorMessage = response?.errorMessage || response?.ErrorMessage || '预览执行失败';
                this._setStatus(`预览失败: ${errorMessage}`);
                this._setImage('after', null);
                this._renderOutputs(null);
                return;
            }

            const resultImage = response?.imageBase64 || response?.resultImageBase64 || response?.ImageBase64;
            const outputs = response?.outputs || response?.Outputs;
            const elapsedMs = response?.executionTimeMs ?? response?.ExecutionTimeMs;

            this._setImage('after', resultImage || null);
            this._renderOutputs(outputs);
            this._setStatus(typeof elapsedMs === 'number'
                ? `预览完成 (${elapsedMs} ms)`
                : '预览完成');
        } catch (error) {
            this._setStatus(`预览错误: ${error.message}`);
            this._setImage('after', null);
            this._renderOutputs(null);
        }
    }

    _setStatus(text) {
        const statusElement = this.container?.querySelector('#preview-status-text');
        if (statusElement) {
            statusElement.textContent = text;
        }
    }

    _setImage(type, imageBase64OrDataUrl) {
        const isBefore = type === 'before';
        const image = this.container?.querySelector(isBefore ? '#preview-before-image' : '#preview-after-image');
        const placeholder = this.container?.querySelector(
            isBefore ? '#preview-before-placeholder' : '#preview-after-placeholder'
        );

        if (!image || !placeholder) {
            return;
        }

        if (!imageBase64OrDataUrl) {
            image.removeAttribute('src');
            image.style.display = 'none';
            placeholder.style.display = 'flex';
            return;
        }

        const source = String(imageBase64OrDataUrl).startsWith('data:')
            ? String(imageBase64OrDataUrl)
            : `data:image/png;base64,${imageBase64OrDataUrl}`;

        image.src = source;
        image.style.display = 'block';
        placeholder.style.display = 'none';
    }

    _renderOutputs(outputs) {
        const outputContainer = this.container?.querySelector('#preview-output-list');
        if (!outputContainer) {
            return;
        }

        if (!outputs || typeof outputs !== 'object' || Object.keys(outputs).length === 0) {
            outputContainer.textContent = '暂无输出摘要';
            return;
        }

        const items = Object.entries(outputs).slice(0, 8).map(([key, value]) => {
            let displayValue;
            if (typeof value === 'number') {
                displayValue = Number.isInteger(value) ? value : value.toFixed(3);
            } else if (typeof value === 'string') {
                displayValue = value.length > 48 ? `${value.substring(0, 48)}...` : value;
            } else if (typeof value === 'boolean') {
                displayValue = value ? 'true' : 'false';
            } else {
                displayValue = JSON.stringify(value);
            }

            return `
                <div class="operator-preview-output-item">
                    <span class="key">${key}</span>
                    <span class="value">${displayValue}</span>
                </div>
            `;
        });

        outputContainer.innerHTML = items.join('');
    }
}

export default PreviewPanel;
