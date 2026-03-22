export class NodePreviewOverlay {
    constructor(container, flowCanvas, previewCoordinator, options = {}) {
        this.container = container;
        this.flowCanvas = flowCanvas;
        this.previewCoordinator = previewCoordinator;
        this.onOpenImage = options.onOpenImage ?? (() => {});
        this.dismissedNodeId = null;
        this.state = this.previewCoordinator?.getState?.() ?? null;

        this.root = document.createElement('div');
        this.root.className = 'node-preview-overlay-root';
        this.container?.appendChild(this.root);

        this.unsubscribePreview = this.previewCoordinator?.subscribe?.(state => {
            const previousNodeId = this.state?.activeNodeId || null;
            const currentNodeId = state?.activeNodeId || null;
            if (previousNodeId !== currentNodeId) {
                this.dismissedNodeId = null;
            }

            this.state = state;
            this.render();
        }) || null;

        this.unsubscribeView = this.flowCanvas?.subscribeViewState?.(() => {
            this.updatePosition();
        }) || null;
    }

    destroy() {
        this.unsubscribePreview?.();
        this.unsubscribeView?.();
        this.root?.remove();
    }

    isVisible() {
        const activeNodeId = this.state?.activeNodeId || null;
        const overlayEnabled = Boolean(this.state?.presenter?.overlayEnabled);
        return Boolean(activeNodeId && overlayEnabled && this.dismissedNodeId !== activeNodeId);
    }

    render() {
        if (!this.root) {
            return;
        }

        if (!this.isVisible()) {
            this.root.innerHTML = '';
            this.root.classList.add('hidden');
            return;
        }

        const presenter = this.state.presenter;
        const primaryImageSrc = presenter.outputImageSrc || presenter.inputImageSrc || null;
        const primaryImageLabel = presenter.outputImageSrc ? '输出图像' : (presenter.inputImageSrc ? '输入图像' : '');
        const summaryItems = presenter.summaryItems.length > 0
            ? presenter.summaryItems.map(item => `
                <div class="node-preview-summary-item">
                    <span class="key">${item.key}</span>
                    <span class="value">${item.value}</span>
                </div>
            `).join('')
            : '<div class="node-preview-summary-empty">暂无输出摘要</div>';

        this.root.classList.remove('hidden');
        this.root.innerHTML = `
            <div class="node-preview-card" data-node-preview-card="true">
                <div class="node-preview-card-header">
                    <div class="node-preview-title-group">
                        <div class="node-preview-title">${presenter.title || '节点预览'}</div>
                        <div class="node-preview-status">${presenter.statusText}</div>
                    </div>
                    <div class="node-preview-actions">
                        <button type="button" class="node-preview-action-btn" data-action="refresh">刷新</button>
                        <button type="button" class="node-preview-action-btn" data-action="close">关闭</button>
                    </div>
                </div>
                <div class="node-preview-media ${primaryImageSrc ? 'has-image' : ''}" data-action="${primaryImageSrc ? 'open-image' : ''}">
                    ${primaryImageSrc
                        ? `
                            <img src="${primaryImageSrc}" alt="${primaryImageLabel || '节点预览'}" />
                            ${primaryImageLabel ? `<div class="node-preview-media-chip">${primaryImageLabel}</div>` : ''}
                          `
                        : `<div class="node-preview-media-placeholder">${presenter.isLoading ? '预览生成中...' : (presenter.hasError ? '预览失败' : '暂无图像输出')}</div>`}
                </div>
                <div class="node-preview-summary">${summaryItems}</div>
            </div>
        `;

        const refreshButton = this.root.querySelector('[data-action="refresh"]');
        refreshButton?.addEventListener('click', event => {
            event.preventDefault();
            event.stopPropagation();
            this.previewCoordinator?.requestActivePreview?.({
                force: true,
                immediate: true
            });
        });

        const closeButton = this.root.querySelector('[data-action="close"]');
        closeButton?.addEventListener('click', event => {
            event.preventDefault();
            event.stopPropagation();
            this.dismissedNodeId = this.state?.activeNodeId || null;
            this.render();
        });

        const imageContainer = this.root.querySelector('[data-action="open-image"]');
        imageContainer?.addEventListener('click', event => {
            event.preventDefault();
            event.stopPropagation();
            if (primaryImageSrc) {
                this.onOpenImage(primaryImageSrc);
            }
        });

        this.updatePosition();
    }

    updatePosition() {
        if (!this.isVisible() || !this.root) {
            return;
        }

        const card = this.root.querySelector('[data-node-preview-card="true"]');
        const activeNodeId = this.state?.activeNodeId || null;
        if (!card || !activeNodeId) {
            return;
        }

        const nodeRect = this.flowCanvas?.getNodeScreenRect?.(activeNodeId);
        const containerRect = this.container?.getBoundingClientRect?.();
        if (!nodeRect || !containerRect) {
            this.root.classList.add('hidden');
            return;
        }

        const cardWidth = card.offsetWidth || 280;
        const cardHeight = card.offsetHeight || 220;
        const gap = 12;
        const padding = 8;
        const containerWidth = containerRect.width;
        const containerHeight = containerRect.height;

        let left = nodeRect.x + nodeRect.width + gap;
        if (left + cardWidth > containerWidth - padding) {
            left = nodeRect.x - cardWidth - gap;
        }

        if (left < padding) {
            left = padding;
        }

        let top = nodeRect.y;
        if (top + cardHeight > containerHeight - padding) {
            top = containerHeight - cardHeight - padding;
        }

        if (top < padding) {
            top = padding;
        }

        card.style.left = `${Math.round(left)}px`;
        card.style.top = `${Math.round(top)}px`;
    }
}

export default NodePreviewOverlay;
