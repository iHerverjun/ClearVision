/**
 * Tooltip 系统 - 阶段五
 * 全局工具提示功能
 */

class TooltipSystem {
    constructor() {
        this.tooltip = null;
        this._boundMouseOver = this.handleMouseOver.bind(this);
        this._boundMouseOut = this.handleMouseOut.bind(this);
        this._boundMouseMove = this.handleMouseMove.bind(this);
        this.init();
    }

    init() {
        // 创建 tooltip 元素
        this.tooltip = document.createElement('div');
        this.tooltip.className = 'global-tooltip';
        this.tooltip.style.cssText = `
            position: fixed;
            padding: 6px 12px;
            background: rgba(0, 0, 0, 0.9);
            color: #fff;
            font-size: 12px;
            border-radius: 4px;
            white-space: nowrap;
            z-index: 99999;
            pointer-events: none;
            opacity: 0;
            transition: opacity 0.2s;
            max-width: 300px;
            line-height: 1.4;
        `;
        document.body.appendChild(this.tooltip);

        // 全局事件委托
        document.addEventListener('mouseover', this._boundMouseOver);
        document.addEventListener('mouseout', this._boundMouseOut);
        document.addEventListener('mousemove', this._boundMouseMove);
    }

    handleMouseOver(e) {
        const target = e.target.closest('[data-tooltip]');
        if (!target) return;

        const content = target.dataset.tooltip;
        const shortcut = target.dataset.shortcut;
        
        let html = content;
        if (shortcut) {
            html += ` <span style="color: #aaa; margin-left: 8px;">${shortcut}</span>`;
        }

        this.tooltip.innerHTML = html;
        this.tooltip.style.opacity = '1';
        
        // 初始位置
        this.updatePosition(e);
    }

    handleMouseOut(e) {
        const target = e.target.closest('[data-tooltip]');
        if (target) {
            this.tooltip.style.opacity = '0';
        }
    }

    handleMouseMove(e) {
        if (this.tooltip.style.opacity === '1') {
            this.updatePosition(e);
        }
    }

    updatePosition(e) {
        const offset = 10;
        let left = e.clientX + offset;
        let top = e.clientY + offset;

        // 边界检测
        const rect = this.tooltip.getBoundingClientRect();
        if (left + rect.width > window.innerWidth) {
            left = e.clientX - rect.width - offset;
        }
        if (top + rect.height > window.innerHeight) {
            top = e.clientY - rect.height - offset;
        }

        this.tooltip.style.left = `${left}px`;
        this.tooltip.style.top = `${top}px`;
    }

    destroy() {
        document.removeEventListener('mouseover', this._boundMouseOver);
        document.removeEventListener('mouseout', this._boundMouseOut);
        document.removeEventListener('mousemove', this._boundMouseMove);

        if (this.tooltip?.parentNode) {
            this.tooltip.parentNode.removeChild(this.tooltip);
        }

        this.tooltip = null;
    }
}

// 初始化 - 修复：检查DOM是否已加载，避免事件错过
function initTooltipSystem() {
    if (!window.tooltipSystem) {
        window.tooltipSystem = new TooltipSystem();
        console.log('[Tooltip] 工具提示系统已初始化');
    }
}

if (document.readyState === 'loading') {
    // DOM还在加载中，等待事件
    document.addEventListener('DOMContentLoaded', initTooltipSystem);
} else {
    // DOM已经加载完成，立即初始化
    initTooltipSystem();
}

export default TooltipSystem;
