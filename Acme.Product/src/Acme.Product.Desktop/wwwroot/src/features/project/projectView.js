/**
 * 工程视图组件 - 阶段三增强版
 * 展示工程列表，支持搜索、打开、删除、排序、视图切换
 */

import projectManager from './projectManager.js';
import { showToast, createModal, closeModal, createButton } from '../../shared/components/uiComponents.js';
import {
    getFeatureBadge,
    getFeatureDescription,
    getFeatureMeta,
    isFeatureEnabled
} from '../../shared/featureRegistry.js';

export class ProjectView {
    constructor(containerId) {
        this.container = document.getElementById(containerId);
        this.currentTab = 'all'; // 'all' | 'recent'
        this.projects = [];
        this.filteredProjects = [];
        this.viewMode = 'list'; // 'list'
        this.sortBy = 'modifiedAt'; // 'name' | 'createdAt' | 'modifiedAt'
        this.sortOrder = 'desc'; // 'asc' | 'desc'
        
        if (!this.container) {
            console.error('[ProjectView] 容器未找到:', containerId);
            return;
        }
        
        this.init();
    }
    
    async init() {
        this.bindEvents();
        await this.loadProjects();
    }
    
    bindEvents() {
        // 搜索按钮
        const searchBtn = document.getElementById('btn-search-project');
        const searchInput = document.getElementById('project-search-input');
        
        if (searchBtn && searchInput) {
            searchBtn.addEventListener('click', () => this.handleSearch(searchInput.value));
            searchInput.addEventListener('keypress', (e) => {
                if (e.key === 'Enter') this.handleSearch(searchInput.value);
            });
        }
        
        // 新建工程按钮（工程分页内）
        const newProjectBtn = document.getElementById('btn-new-project-inline');
        if (newProjectBtn) {
            newProjectBtn.addEventListener('click', () => this.showNewProjectDialog());
        }
        
        // 导入工程按钮（从全局工具栏迁移至此）
        const importBtn = document.getElementById('btn-import');
        if (importBtn) {
            importBtn.addEventListener('click', () => {
                console.log('[ProjectView] 导入工程');
                if (typeof window.showImportDialog === 'function') {
                    window.showImportDialog();
                } else {
                    showToast('导入功能未就绪', 'warning');
                }
            });
        }
        
        // 导出工程按钮（从全局工具栏迁移至此）
        const exportBtn = document.getElementById('btn-export');
        if (exportBtn) {
            exportBtn.addEventListener('click', () => {
                console.log('[ProjectView] 导出工程');
                if (typeof window.exportProjectToJson === 'function') {
                    window.exportProjectToJson();
                } else {
                    showToast('导出功能未就绪', 'warning');
                }
            });
        }
        
        // Tab 切换
        const tabs = this.container.querySelectorAll('.tab-btn');
        tabs.forEach(tab => {
            tab.addEventListener('click', () => {
                tabs.forEach(t => t.classList.remove('active'));
                tab.classList.add('active');
                this.currentTab = tab.dataset.tab;
                this.loadProjects();
            });
        });
        
        // 视图切换按钮逻辑已移除

        
        // 排序选择器
        const sortSelect = this.container.querySelector('.sort-select');
        if (sortSelect) {
            sortSelect.addEventListener('change', (e) => {
                this.sortBy = e.target.value;
                this.sortProjects();
                this.renderProjects(document.getElementById('project-list'));
            });
        }
    }
    
    async loadProjects() {
        const listContainer = document.getElementById('project-list');
        if (!listContainer) return;
        
        listContainer.innerHTML = this.renderSkeletonLoading();
        
        try {
            if (this.currentTab === 'recent') {
                this.projects = await projectManager.getRecentProjects(10);
            } else {
                this.projects = await projectManager.getProjectList();
            }
            
            this.filteredProjects = [...this.projects];
            this.sortProjects();
            this.renderProjects(listContainer);
        } catch (error) {
            console.error('[ProjectView] 加载工程列表失败:', error);
            listContainer.innerHTML = this.renderEmptyState();
        }
    }
    
    /**
     * 渲染骨架屏加载状态
     */
    renderSkeletonLoading() {
        return `
            <div class="skeleton-grid">
                ${Array(6).fill(0).map(() => `
                    <div class="skeleton-card">
                        <div class="skeleton-header"></div>
                        <div class="skeleton-body"></div>
                        <div class="skeleton-footer"></div>
                    </div>
                `).join('')}
            </div>
        `;
    }
    
    /**
     * 渲染空状态
     */
    renderEmptyState() {
        return `
            <div class="empty-state">
                <div class="empty-state-icon">
                    <svg viewBox="0 0 24 24" width="64" height="64" fill="none" stroke="currentColor" stroke-width="1.5">
                        <path d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/>
                    </svg>
                </div>
                <h3 class="empty-state-title">还没有工程</h3>
                <p class="empty-state-desc">创建您的第一个工程开始视觉检测之旅</p>
                <button class="btn btn-primary empty-state-action" onclick="document.getElementById('btn-new-project-inline').click()">
                    <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor">
                        <path d="M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z"/>
                    </svg>
                    创建新工程
                </button>
            </div>
        `;
    }
    
    /**
     * 排序工程列表
     */
    sortProjects() {
        this.filteredProjects.sort((a, b) => {
            let valueA, valueB;
            
            switch (this.sortBy) {
                case 'name':
                    valueA = a.name?.toLowerCase() || '';
                    valueB = b.name?.toLowerCase() || '';
                    break;
                case 'createdAt':
                    valueA = new Date(a.createdAt).getTime();
                    valueB = new Date(b.createdAt).getTime();
                    break;
                case 'modifiedAt':
                default:
                    valueA = new Date(a.modifiedAt).getTime();
                    valueB = new Date(b.modifiedAt).getTime();
                    break;
            }
            
            if (this.sortOrder === 'asc') {
                return valueA > valueB ? 1 : -1;
            } else {
                return valueA < valueB ? 1 : -1;
            }
        });
    }
    
    renderProjects(container) {
        if (!this.filteredProjects || this.filteredProjects.length === 0) {
            container.innerHTML = this.renderEmptyState();
            return;
        }
        
        container.innerHTML = this.renderListView();
        
        // 绑定卡片事件
        this.bindCardEvents(container);
    }
    

    
    /**
     * 渲染列表视图
     */
    renderListView() {
        return `
            <div class="projects-list">
                ${this.filteredProjects.map(project => this.createProjectCardList(project)).join('')}
            </div>
        `;
    }

    /**
     * 创建工程卡片（列表视图）
     */
    createProjectCardList(project) {
        const createdDate = new Date(project.createdAt).toLocaleDateString('zh-CN');
        const modifiedDate = new Date(project.modifiedAt).toLocaleDateString('zh-CN');
        const status = project.status || 'ready';
        const statusConfig = this.getStatusConfig(status);
        
        return `
            <div class="project-list-item" data-id="${project.id}">
                <div class="project-list-thumbnail">
                    <svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor">
                        <path d="M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z"/>
                    </svg>
                </div>
                <div class="project-list-info">
                    <div class="project-list-title">${this.escapeHtml(project.name)}</div>
                    <div class="project-list-desc">${this.escapeHtml(project.description || '暂无描述')}</div>
                </div>
                <div class="project-list-meta">
                    <span>${modifiedDate}</span>
                    <span class="project-status-dot" style="color:${statusConfig.color};font-size:12px;">● ${statusConfig.label}</span>
                </div>
                <div class="project-list-actions">
                    <button class="action-btn btn-open" title="打开">
                        <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor"><path d="M19 19H5V5h7V3H5c-1.11 0-2 .9-2 2v14c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2v-7h-2v7zM14 3v2h3.59l-9.83 9.83 1.41 1.41L19 6.41V10h2V3h-7z"/></svg>
                    </button>
                    <button class="action-btn btn-delete" title="删除">
                        <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor"><path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2v-7H6v7zM19 4h-3.5l-1-1h-5.7l-1 1H5v2h14V4z"/></svg>
                    </button>
                </div>
            </div>
        `;
    }
    
    /**
     * 获取状态配置
     */
    getStatusConfig(status) {
        const configs = {
            'ready': { label: '就绪', color: '#2ecc71' },
            'running': { label: '运行中', color: '#3498db' },
            'error': { label: '错误', color: '#e74c3c' },
            'draft': { label: '草稿', color: '#95a5a6' }
        };
        return configs[status] || configs['ready'];
    }
    
    /**
     * 绑定卡片事件
     */
    bindCardEvents(container) {
        // 选择列表行
        const items = container.querySelectorAll('.project-list-item');
        items.forEach(card => {
            const projectId = card.dataset.id;
            
            // 双击打开
            card.addEventListener('dblclick', () => this.openProject(projectId));
            
            // 打开按钮
            card.querySelector('.btn-open')?.addEventListener('click', (e) => {
                e.stopPropagation();
                this.openProject(projectId);
            });
            
            // 删除按钮
            card.querySelector('.btn-delete')?.addEventListener('click', (e) => {
                e.stopPropagation();
                const name = card.querySelector('.project-list-title')?.textContent 
                          || '未知工程';
                this.confirmDelete(projectId, name);
            });
        });
    }
    
    async openProject(projectId) {
        try {
            showToast('正在打开工程...', 'info');
            const project = await projectManager.openProject(projectId);
            
            if (project) {
                showToast(`工程 "${project.name}" 已打开`, 'success');
            }
        } catch (error) {
            console.error('[ProjectView] 打开工程失败:', error);
            showToast('打开工程失败: ' + error.message, 'error');
        }
    }
    
    confirmDelete(projectId, projectName) {
        const content = document.createElement('p');
        content.textContent = `确定要删除工程 "${projectName}" 吗？此操作无法撤销。`;
        
        let modalOverlay = null;
        
        const btnCancel = createButton({
            text: '取消',
            type: 'secondary',
            onClick: () => closeModal(modalOverlay)
        });
        
        const btnDelete = createButton({
            text: '删除',
            type: 'danger',
            onClick: async () => {
                try {
                    await projectManager.deleteProject(projectId);
                    showToast('工程已删除', 'success');
                    closeModal(modalOverlay);
                    await this.loadProjects();
                } catch (error) {
                    showToast('删除失败: ' + error.message, 'error');
                }
            }
        });
        
        modalOverlay = createModal({
            title: '确认删除',
            content,
            footer: [btnCancel, btnDelete],
            width: '400px'
        });
    }
    
    async handleSearch(keyword) {
        if (!keyword.trim()) {
            await this.loadProjects();
            return;
        }
        
        const listContainer = document.getElementById('project-list');
        if (!listContainer) return;
        
        listContainer.innerHTML = this.renderSkeletonLoading();
        
        try {
            this.projects = await projectManager.searchProjects(keyword);
            this.filteredProjects = [...this.projects];
            this.sortProjects();
            this.renderProjects(listContainer);
        } catch (error) {
            console.error('[ProjectView] 搜索失败:', error);
            listContainer.innerHTML = this.renderEmptyState();
        }
    }
    
    // 刷新列表（外部调用）
    async refresh() {
        await this.loadProjects();
    }
    
    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
    
    /**
     * 显示新建工程对话框
     */
    showNewProjectDialog() {
        const demoFeature = getFeatureMeta('project.demoCreation');
        const demoCreationEnabled = isFeatureEnabled('project.demoCreation');
        const demoBadge = getFeatureBadge('project.demoCreation');
        const content = document.createElement('div');
        content.className = 'new-project-form';
        content.innerHTML = `
            <div class="form-group">
                <label for="new-project-name">工程名称 <span class="required">*</span></label>
                <input type="text" id="new-project-name" class="cv-input" placeholder="请输入工程名称" />
            </div>
            <div class="form-group">
                <label for="new-project-desc">描述</label>
                <input type="text" id="new-project-desc" class="cv-input" placeholder="可选描述" />
            </div>
            <div class="form-group">
                <label>创建方式</label>
                <div style="display:flex; flex-direction:column; gap:8px; margin-top:8px;">
                    <label style="display:flex; align-items:center; gap:8px;">
                        <input type="radio" name="new-project-mode" value="standard" checked />
                        标准工程
                    </label>
                    <label style="display:flex; align-items:center; gap:8px;">
                        <input type="radio" name="new-project-mode" value="demo" ${demoCreationEnabled ? '' : 'disabled'} />
                        示例工程（完整引导） <span class="type-badge">${demoBadge}</span>
                    </label>
                    <label style="display:flex; align-items:center; gap:8px;">
                        <input type="radio" name="new-project-mode" value="simple-demo" ${demoCreationEnabled ? '' : 'disabled'} />
                        示例工程（简化版） <span class="type-badge">${demoBadge}</span>
                    </label>
                </div>
                <div style="margin-top:8px; color:var(--text-muted); font-size:12px;">${getFeatureDescription('project.demoCreation', demoFeature.description)}</div>
            </div>
        `;
        
        let modalOverlay = null;
        
        const btnCancel = createButton({
            text: '取消',
            type: 'secondary',
            onClick: () => closeModal(modalOverlay)
        });
        
        const btnCreate = createButton({
            text: '创建',
            onClick: async () => {
                const nameInput = document.getElementById('new-project-name');
                const descInput = document.getElementById('new-project-desc');
                const modeInput = content.querySelector('input[name="new-project-mode"]:checked');
                const mode = modeInput?.value || 'standard';
                const name = nameInput?.value?.trim();
                const desc = descInput?.value?.trim() || '';
                
                if ((mode === 'demo' || mode === 'simple-demo') && !demoCreationEnabled) {
                    showToast(getFeatureDescription('project.demoCreation', '示例工程入口当前不可用'), 'warning');
                    return;
                }

                if (mode === 'standard' && !name) {
                    showToast('请输入工程名称', 'warning');
                    nameInput?.focus();
                    return;
                }
                
                try {
                    const project = mode === 'demo'
                        ? await projectManager.createDemoProject('full')
                        : (mode === 'simple-demo'
                            ? await projectManager.createDemoProject('simple')
                            : await projectManager.createProject(name, desc));
                    const displayName = project?.name || name || '示例工程';
                    showToast(`工程 "${displayName}" 已创建`, 'success');
                    closeModal(modalOverlay);
                } catch (error) {
                    console.error('[ProjectView] 创建工程失败:', error);
                    showToast('创建失败: ' + error.message, 'error');
                }
            }
        });

        const btnGuide = createButton({
            text: '引导说明',
            type: 'secondary',
            onClick: async () => {
                if (!demoCreationEnabled) {
                    showToast(getFeatureDescription('project.demoCreation', '示例工程引导当前不可用'), 'warning');
                    return;
                }

                try {
                    const guide = await projectManager.getDemoGuide();
                    const guideText = typeof guide === 'string'
                        ? guide
                        : JSON.stringify(guide, null, 2);
                    window.alert(guideText);
                } catch (error) {
                    console.error('[ProjectView] 获取引导说明失败:', error);
                    showToast('获取引导说明失败: ' + error.message, 'error');
                }
            }
        });
        
        modalOverlay = createModal({
            title: '新建工程',
            content,
            footer: [btnCancel, btnGuide, btnCreate],
            width: '400px'
        });
        
        // 自动聚焦到名称输入框
        setTimeout(() => {
            document.getElementById('new-project-name')?.focus();
        }, 100);
    }
}

export default ProjectView;
