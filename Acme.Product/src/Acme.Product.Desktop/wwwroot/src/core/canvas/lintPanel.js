/**
 * lintPanel.js
 * Linter 结果面板组件 - Sprint 4 Task 4.3
 * 展示 FlowLinter 检查结果，Error 存在时阻止部署
 * 作者：蘅芜君
 */

class LintPanel {
    constructor(containerId) {
        this.container = document.getElementById(containerId);
        if (!this.container) {
            // 动态创建容器
            this.container = document.createElement('div');
            this.container.id = containerId || 'lint-panel';
            this.container.className = 'lint-panel';
            document.body.appendChild(this.container);
        }
        this.issues = [];
        this.isCollapsed = true;
        this.onDeployBlocked = null;
    }

    /**
     * 更新 Lint 结果
     * @param {Array} issues - Lint 问题列表 [{code, severity, message, suggestion, operatorName}]
     */
    update(issues) {
        this.issues = issues || [];
        this.render();
    }

    /**
     * 检查是否有阻塞性错误
     */
    hasErrors() {
        return this.issues.some(i => i.severity === 'Error');
    }

    /**
     * 获取统计信息
     */
    getStats() {
        const errors = this.issues.filter(i => i.severity === 'Error').length;
        const warnings = this.issues.filter(i => i.severity === 'Warning').length;
        return { errors, warnings, total: this.issues.length };
    }

    /**
     * 渲染面板
     */
    render() {
        const stats = this.getStats();
        const hasErrors = stats.errors > 0;

        this.container.innerHTML = '';
        this.container.className = `lint-panel ${hasErrors ? 'lint-panel--error' : (stats.warnings > 0 ? 'lint-panel--warning' : 'lint-panel--clean')}`;

        // 头部
        const header = document.createElement('div');
        header.className = 'lint-panel__header';
        header.onclick = () => this.toggleCollapse();

        const statusIcon = hasErrors ? '❌' : (stats.warnings > 0 ? '⚠️' : '✅');
        const statusText = hasErrors
            ? `${stats.errors} 个错误, ${stats.warnings} 个警告`
            : (stats.warnings > 0 ? `${stats.warnings} 个警告` : '检查通过');

        header.innerHTML = `
            <span class="lint-panel__status">${statusIcon} ${statusText}</span>
            <span class="lint-panel__toggle">${this.isCollapsed ? '▶' : '▼'}</span>
        `;
        this.container.appendChild(header);

        // 问题列表
        if (!this.isCollapsed && this.issues.length > 0) {
            const list = document.createElement('div');
            list.className = 'lint-panel__list';

            this.issues.forEach(issue => {
                const item = document.createElement('div');
                item.className = `lint-panel__item lint-panel__item--${issue.severity.toLowerCase()}`;
                item.innerHTML = `
                    <span class="lint-panel__code">${issue.code}</span>
                    <span class="lint-panel__severity ${issue.severity.toLowerCase()}">${issue.severity}</span>
                    <span class="lint-panel__message">${issue.message}</span>
                    ${issue.operatorName ? `<span class="lint-panel__operator">@ ${issue.operatorName}</span>` : ''}
                    ${issue.suggestion ? `<div class="lint-panel__suggestion">💡 ${issue.suggestion}</div>` : ''}
                `;
                list.appendChild(item);
            });

            this.container.appendChild(list);
        }

        // 部署按钮状态通知
        if (this.onDeployBlocked) {
            this.onDeployBlocked(hasErrors);
        }
    }

    /**
     * 展开/折叠
     */
    toggleCollapse() {
        this.isCollapsed = !this.isCollapsed;
        this.render();
    }

    /**
     * 显示仿真完成横幅
     * @param {Object} dryRunResult - {coveragePercentage, coveredBranches, totalBranches, isSuccess}
     */
    showDryRunBanner(dryRunResult) {
        const banner = document.createElement('div');
        const isPass = dryRunResult.isSuccess && dryRunResult.coveragePercentage >= 80;
        banner.className = `lint-panel__banner ${isPass ? 'lint-panel__banner--pass' : 'lint-panel__banner--fail'}`;
        banner.innerHTML = `
            <span>${isPass ? '✅' : '❌'} 仿真${isPass ? '通过' : '未通过'}</span>
            <span>分支覆盖率: ${dryRunResult.coveragePercentage.toFixed(1)}% (${dryRunResult.coveredBranches}/${dryRunResult.totalBranches})</span>
        `;

        // 插入到面板顶部
        this.container.insertBefore(banner, this.container.firstChild);

        // 10 秒后自动淡出
        setTimeout(() => {
            banner.classList.add('lint-panel__banner--fade');
            setTimeout(() => banner.remove(), 500);
        }, 10000);
    }

    /**
     * 销毁
     */
    destroy() {
        if (this.container) {
            this.container.innerHTML = '';
        }
        this.issues = [];
    }
}

// 导出
if (typeof module !== 'undefined' && module.exports) {
    module.exports = LintPanel;
}
