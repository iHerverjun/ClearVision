import ImageCanvas from '../../core/canvas/imageCanvas.js';
import { rectFromParams, rectToParams } from './roiGeometry.mjs';

export class RoiEditorPanel {
    constructor(container, options = {}) {
        this.container = container;
        this.getOperator = options.getOperator ?? (() => null);
        this.previewCoordinator = options.previewCoordinator ?? null;
        this.onRectChanged = options.onRectChanged ?? (() => {});
        this.onRequestSyncFromParams = options.onRequestSyncFromParams ?? (() => {});
        this.canvasId = `roi-editor-canvas-${Math.random().toString(36).slice(2)}`;
        this.imageCanvas = null;
        this.unsubscribePreview = null;
        this.previewState = this.previewCoordinator?.getState?.() ?? null;
        this.currentImageSource = null;
        this.currentShape = 'Rectangle';
        this.currentRect = null;

        this.render();
        this.bindPreview();
        this.initializeCanvas();
        this.applyState();
    }

    destroy() {
        this.unsubscribePreview?.();
        this.unsubscribePreview = null;
        this.imageCanvas?.destroy();
        this.imageCanvas = null;
        this.container = null;
    }

    render() {
        if (!this.container) {
            return;
        }

        this.container.innerHTML = `
            <section class="roi-editor-panel">
                <div class="roi-editor-header">
                    <div class="roi-editor-title-group">
                        <div class="roi-editor-title">ROI 编辑器</div>
                        <div class="roi-editor-subtitle" id="roi-editor-subtitle">拖拽框选矩形区域，自动同步到 X / Y / Width / Height</div>
                    </div>
                    <div class="roi-editor-actions">
                        <button type="button" class="btn btn-secondary btn-sm" id="btn-roi-fit">适应窗口</button>
                        <button type="button" class="btn btn-secondary btn-sm" id="btn-roi-actual">1:1</button>
                        <button type="button" class="btn btn-secondary btn-sm" id="btn-roi-sync">从参数回填</button>
                    </div>
                </div>
                <div class="roi-editor-stage" id="roi-editor-stage">
                    <canvas id="${this.canvasId}" class="roi-editor-canvas"></canvas>
                    <div class="roi-editor-empty" id="roi-editor-empty">请先让该节点成功生成一次预览输入图像</div>
                    <div class="roi-editor-readonly hidden" id="roi-editor-readonly">图上编辑当前仅支持矩形 ROI，圆形/多边形仍使用参数输入</div>
                </div>
            </section>
        `;

        this.container.querySelector('#btn-roi-fit')?.addEventListener('click', () => this.imageCanvas?.fitToWindow());
        this.container.querySelector('#btn-roi-actual')?.addEventListener('click', () => this.imageCanvas?.actualSize());
        this.container.querySelector('#btn-roi-sync')?.addEventListener('click', () => {
            this.onRequestSyncFromParams?.();
            this.syncOverlayFromOperator();
        });
    }

    bindPreview() {
        if (!this.previewCoordinator?.subscribe) {
            return;
        }

        this.unsubscribePreview = this.previewCoordinator.subscribe(state => {
            this.previewState = state;
            this.applyState();
        });
    }

    initializeCanvas() {
        if (!this.container) {
            return;
        }

        this.imageCanvas = new ImageCanvas(this.canvasId, {
            interactionMode: 'roi-rect',
            onOverlayChanged: (rect, phase) => this.handleRectChanged(rect, phase)
        });
    }

    refreshFromOperator() {
        this.syncOverlayFromOperator();
    }

    handleRectChanged(rect, phase) {
        this.currentRect = rect;
        this.onRectChanged(rectToParams(rect), phase);
    }

    syncOverlayFromOperator() {
        if (!this.imageCanvas || !this.hasEditableImage()) {
            return;
        }

        const operator = this.getOperator();
        if (!operator) {
            return;
        }

        const values = this.extractRectParams(operator);
        const rect = rectFromParams(values);
        this.currentRect = rect;
        this.imageCanvas.setEditableRectangle(rect);
    }

    extractRectParams(operator) {
        const values = {};
        (operator?.parameters || []).forEach(param => {
            const key = String(param?.name || param?.Name || '');
            const value = param?.value ?? param?.Value ?? param?.defaultValue ?? param?.DefaultValue;
            values[key] = value;
        });
        return values;
    }

    hasEditableImage() {
        return Boolean(this.currentImageSource) && this.currentShape === 'Rectangle';
    }

    async applyState() {
        const operator = this.getOperator();
        this.currentShape = this.readOperatorValue(operator, 'Shape', 'Rectangle');
        const inputImageSrc = this.resolveInputImageSrc();
        const imageChanged = inputImageSrc !== this.currentImageSource;
        this.currentImageSource = inputImageSrc;

        const emptyState = this.container?.querySelector('#roi-editor-empty');
        const readonlyState = this.container?.querySelector('#roi-editor-readonly');
        const stage = this.container?.querySelector('#roi-editor-stage');
        const subtitle = this.container?.querySelector('#roi-editor-subtitle');

        if (subtitle) {
            subtitle.textContent = this.currentShape === 'Rectangle'
                ? '拖拽框选矩形区域，自动同步到 X / Y / Width / Height'
                : '图上编辑当前仅支持矩形 ROI，圆形/多边形仍使用参数输入';
        }

        if (readonlyState) {
            readonlyState.classList.toggle('hidden', this.currentShape === 'Rectangle');
        }

        if (emptyState) {
            emptyState.style.display = this.currentShape === 'Rectangle' && !inputImageSrc ? 'flex' : 'none';
        }

        if (stage) {
            stage.classList.toggle('is-disabled', this.currentShape !== 'Rectangle');
        }

        if (!this.imageCanvas) {
            return;
        }

        if (!inputImageSrc) {
            this.imageCanvas.clear();
            return;
        }

        if (this.currentShape !== 'Rectangle') {
            this.imageCanvas.clearEditableRectangle();
            return;
        }

        if (imageChanged) {
            await this.imageCanvas.loadImage(inputImageSrc);
        }

        this.syncOverlayFromOperator();
    }

    resolveInputImageSrc() {
        const activeNodeId = this.previewState?.activeNodeId;
        const operatorId = this.getOperator()?.id || null;
        if (!operatorId || activeNodeId !== operatorId) {
            return null;
        }

        return this.previewState?.presenter?.inputImageSrc || null;
    }

    readOperatorValue(operator, name, fallback = '') {
        const parameter = (operator?.parameters || []).find(item =>
            String(item?.name || item?.Name || '').toLowerCase() === String(name).toLowerCase());
        const value = parameter?.value ?? parameter?.Value ?? parameter?.defaultValue ?? parameter?.DefaultValue;
        return value == null ? fallback : String(value);
    }
}

export default RoiEditorPanel;
