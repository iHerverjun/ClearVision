export class PreviewPanel {
    constructor(container, options = {}) {
        this.container = container;
        this.getOperator = options.getOperator ?? (() => null);
        this.previewCoordinator = options.previewCoordinator ?? null;
        this.onOpenImage = options.onOpenImage ?? (() => {});
        this.debounceMs = options.debounceMs ?? 500;

        this.autoPreviewEnabled = true;
        this.collapsed = false;
        this.state = this.previewCoordinator?.getState?.() ?? null;
        this.unsubscribePreview = this.previewCoordinator?.subscribe?.(state => {
            this.state = state;
            this.applyPreviewState();
        }) || null;

        this.render();
        this.applyPreviewState();
    }

    destroy() {
        this.unsubscribePreview?.();
        this.unsubscribePreview = null;
        this._setImage('before', null);
        this._setImage('after', null);
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
                            <img id="preview-before-image" alt="输入图像预览" data-role="preview-before-image" />
                            <div class="placeholder" id="preview-before-placeholder">暂无输入图像</div>
                        </div>
                        <div class="operator-preview-image-card">
                            <div class="title">输出</div>
                            <img id="preview-after-image" alt="输出图像预览" data-role="preview-after-image" />
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
            this.applyPreviewState();
        });

        const autoToggle = this.container.querySelector('#preview-auto-toggle');
        autoToggle?.addEventListener('change', event => {
            this.autoPreviewEnabled = Boolean(event.target.checked);
        });

        const refreshBtn = this.container.querySelector('#btn-preview-refresh');
        refreshBtn?.addEventListener('click', () => {
            this.refresh();
        });

        this.container.querySelectorAll('[data-role="preview-before-image"], [data-role="preview-after-image"]').forEach(image => {
            image.addEventListener('click', event => {
                const source = event.currentTarget.getAttribute('src');
                if (source) {
                    this.onOpenImage(source);
                }
            });
        });
    }

    scheduleAutoPreview() {
        if (!this.autoPreviewEnabled || this.collapsed) {
            return;
        }

        this.previewCoordinator?.requestActivePreview?.({
            immediate: false,
            force: false,
            debounceMs: this.debounceMs
        });
    }

    refresh() {
        this.previewCoordinator?.requestActivePreview?.({
            immediate: true,
            force: true
        });
    }

    applyPreviewState() {
        const operator = this.getOperator();
        if (!operator || !this.state || this.state.activeNodeId !== operator.id) {
            this._setStatus(operator ? '等待预览' : '未选中算子');
            this._setImage('before', null);
            this._setImage('after', null);
            this._renderOutputs(null);
            return;
        }

        const presenter = this.state.presenter;
        this._setStatus(presenter.statusText);
        this._setImage('before', presenter.inputImageSrc);
        this._setImage('after', presenter.outputImageSrc);
        this._renderOutputs(this.state.outputData);
    }

    _setStatus(text) {
        const statusElement = this.container?.querySelector('#preview-status-text');
        if (statusElement) {
            statusElement.textContent = text;
        }
    }

    _setImage(type, imageSource) {
        const isBefore = type === 'before';
        const image = this.container?.querySelector(isBefore ? '#preview-before-image' : '#preview-after-image');
        const placeholder = this.container?.querySelector(
            isBefore ? '#preview-before-placeholder' : '#preview-after-placeholder'
        );

        if (!image || !placeholder) {
            return;
        }

        if (!imageSource) {
            image.removeAttribute('src');
            image.style.display = 'none';
            placeholder.style.display = 'flex';
            return;
        }

        image.src = imageSource;
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
                displayValue = Number.isInteger(value) ? String(value) : value.toFixed(3);
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
