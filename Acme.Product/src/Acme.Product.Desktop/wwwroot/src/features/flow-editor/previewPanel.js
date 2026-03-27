import { renderDiagnosticsCardsHtml } from '../inspection/analysisCardsPanel.js';

export class PreviewPanel {
    constructor(container, options = {}) {
        this.container = container;
        this.getOperator = options.getOperator ?? (() => null);
        this.previewCoordinator = options.previewCoordinator ?? null;
        this.onOpenImage = options.onOpenImage ?? (() => {});
        this.onAnalyzePreview = options.onAnalyzePreview ?? null;
        this.onAutoTune = options.onAutoTune ?? null;
        this.debounceMs = options.debounceMs ?? 500;

        this.autoPreviewEnabled = true;
        this.collapsed = false;
        this.analysisResult = null;
        this.isAnalyzing = false;
        this.isAutoTuning = false;
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

        const operator = this.getOperator();
        const showWireSequenceActions = operator?.type === 'DetectionSequenceJudge';
        const analyzeLabel = this.isAnalyzing ? '分析中...' : '预览并分析';
        const autoTuneLabel = this.isAutoTuning ? '调参中...' : '一键自动调参';

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
                        ${showWireSequenceActions ? `
                            <button type="button" class="btn btn-secondary btn-preview-analyze" id="btn-preview-analyze" ${this.isAnalyzing || this.isAutoTuning ? 'disabled' : ''}>
                                ${analyzeLabel}
                            </button>
                            <button type="button" class="btn btn-secondary btn-preview-autotune" id="btn-preview-autotune" ${this.isAnalyzing || this.isAutoTuning ? 'disabled' : ''}>
                                ${autoTuneLabel}
                            </button>
                        ` : ''}
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
                        <div class="operator-preview-diagnostics" id="preview-diagnostics-panel"></div>
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

        const analyzeBtn = this.container.querySelector('#btn-preview-analyze');
        analyzeBtn?.addEventListener('click', () => {
            this._handleAnalyzePreview();
        });

        const autoTuneBtn = this.container.querySelector('#btn-preview-autotune');
        autoTuneBtn?.addEventListener('click', () => {
            this._handleAutoTune();
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

    scheduleAutoPreview(options = {}) {
        if (!this.autoPreviewEnabled || this.collapsed) {
            return;
        }

        const debounceMs = options.debounceMs ?? this.debounceMs;
        const force = Boolean(options.force);
        this.previewCoordinator?.requestActivePreview?.({
            immediate: false,
            force,
            debounceMs
        });
    }

    refresh() {
        this.analysisResult = null;
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

        const analysisResult = this.analysisResult?.targetNodeId === operator.id
            ? this.analysisResult
            : null;

        if (analysisResult) {
            const statusText = analysisResult.success
                ? (this.isAutoTuning ? '线序自动调参已完成' : '线序分析已完成')
                : (analysisResult.errorMessage || '线序分析未完成');
            this._setStatus(statusText);
            this._setImage('before', analysisResult.inputImageSrc || this.state.presenter.inputImageSrc);
            this._setImage('after', analysisResult.previewImageSrc || this.state.presenter.outputImageSrc);
            this._renderOutputs(analysisResult.outputs || this.state.outputData);
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
        const diagnosticsContainer = this.container?.querySelector('#preview-diagnostics-panel');
        if (!outputContainer) {
            return;
        }

        if (!outputs || typeof outputs !== 'object' || Object.keys(outputs).length === 0) {
            outputContainer.textContent = '暂无输出摘要';
            if (diagnosticsContainer) {
                diagnosticsContainer.innerHTML = '';
            }
            return;
        }

        const items = Object.entries(outputs)
            .filter(([key]) => ![
                'image',
                'originalimage',
                'diagnostics',
                'result',
                'data',
                'output',
                'filepath',
                'text',
                'detectionlist',
                'objects',
                'defects',
                'rawcandidatecount',
                'visualizationdetectioncount',
                'internalnmsenabled'
            ].includes(String(key).toLowerCase()))
            .slice(0, 8)
            .map(([key, value]) => {
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

        outputContainer.innerHTML = items.length > 0 ? items.join('') : '暂无输出摘要';

        if (diagnosticsContainer) {
            diagnosticsContainer.innerHTML = renderDiagnosticsCardsHtml(outputs, 'OK', {
                compact: true,
                containerClass: 'analysis-cards-container ac-diagnostics-inline ac-diagnostics-preview'
            });
        }
    }

    async _handleAnalyzePreview() {
        if (typeof this.onAnalyzePreview !== 'function' || this.isAnalyzing || this.isAutoTuning) {
            return;
        }

        try {
            this.isAnalyzing = true;
            this.render();
            this.applyPreviewState();
            const result = await this.onAnalyzePreview({
                operator: this.getOperator(),
                previewState: this.state
            });
            this.analysisResult = this._normalizeAnalysisResult(result);
        } catch (error) {
            console.error('[PreviewPanel] 线序分析失败:', error);
        } finally {
            this.isAnalyzing = false;
            this.render();
            this.applyPreviewState();
        }
    }

    async _handleAutoTune() {
        if (typeof this.onAutoTune !== 'function' || this.isAnalyzing || this.isAutoTuning) {
            return;
        }

        try {
            this.isAutoTuning = true;
            this.render();
            this.applyPreviewState();
            const result = await this.onAutoTune({
                operator: this.getOperator(),
                previewState: this.state
            });
            this.analysisResult = this._normalizeAnalysisResult(result);
        } catch (error) {
            console.error('[PreviewPanel] 线序自动调参失败:', error);
        } finally {
            this.isAutoTuning = false;
            this.render();
            this.applyPreviewState();
        }
    }

    _normalizeAnalysisResult(result) {
        if (!result || typeof result !== 'object') {
            return null;
        }

        const previewImageBase64 = result.previewImageBase64 || result.PreviewImageBase64 || null;
        const inputImageBase64 = result.inputImageBase64 || result.InputImageBase64 || null;
        const outputs = result.outputs || result.Outputs || null;

        return {
            targetNodeId: result.targetNodeId || result.TargetNodeId || null,
            success: Boolean(result.success ?? result.Success),
            errorMessage: result.errorMessage || result.ErrorMessage || null,
            inputImageSrc: inputImageBase64 ? `data:image/png;base64,${inputImageBase64}` : null,
            previewImageSrc: previewImageBase64 ? `data:image/png;base64,${previewImageBase64}` : null,
            outputs
        };
    }
}

export default PreviewPanel;
