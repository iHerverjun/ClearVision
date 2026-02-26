import httpClient from '../../core/messaging/httpClient.js';
import { showToast, createModal } from '../../shared/components/uiComponents.js';

class SettingsView {
    constructor(containerId) {
        this.containerId = containerId;
        this.container = document.getElementById(containerId);
        
        this.config = null;
        this.users = [];
        this.cameraBindings = [];
        
        // 尝试从本地存储或全局对象中获取当前用户信息
        const storedUser = localStorage.getItem('cv_current_user');
        const currentUser = window.currentUser || (storedUser ? JSON.parse(storedUser) : {});
        this.isAdmin = currentUser?.role === 'Admin';
        
        this.aiModels = [];
        this.activeAiModelId = null;
        this.editingAiModelId = null;
        this._pendingFormEdits = {}; // 暂存表单中的未保存修改

        console.log('[SettingsView] Initialized for container:', containerId, '| isAdmin:', this.isAdmin);
    }

    /**
     * 初始化或重新加载视图
     * app.js 在切换到 settings 视图时调用
     */
    async refresh() {
        console.log('[SettingsView] === refresh() START ===');
        if (!this.container) {
            console.error('[SettingsView] Container not found:', this.containerId);
            return;
        }
        
        // 可选：添加统一骨架屏或加载提示
        this.container.innerHTML = '<div style="padding:40px;text-align:center;color:var(--text-muted);">正在加载设置...</div>';
        
        // 获取配置信息
        try {
            console.log('[SettingsView] Fetching main config...');
            this.config = await httpClient.get('/settings');
            this.cameraBindings = this.config.cameras || [];
            
            if (this.isAdmin) {
                console.log('[SettingsView] Fetching users list...');
                this.users = await httpClient.get('/users');
            }
        } catch (error) {
            console.error('[SettingsView] Failed to load data:', error);
            showToast('加载系统配置失败: ' + error.message, 'error');
            this.config = this.getDefaultConfig();
        }
        
        await this.loadAiModels();
        
        // 构建全屏两栏布局 DOM
        this.renderLayout();
        
        // 绑定整个容器内的事件
        this.bindEvents();
        
        // 默认激活第一个 Tab
        this.activateTab('general');
        
        console.log('[SettingsView] === refresh() END ===');
    }
    
    async loadAiModels({ preserveEditingId = false } = {}) {
        const previousEditingId = this.editingAiModelId;
        try {
            const models = await httpClient.get('/ai/models');
            this.aiModels = models || [];
        } catch (e) {
            console.warn('[SettingsView] Failed to load AI models from backend:', e);
            this.aiModels = [];
        }
        
        if (this.aiModels.length > 0) {
            const active = this.aiModels.find(m => m.isActive);
            this.activeAiModelId = active ? active.id : this.aiModels[0].id;
        } else {
            this.activeAiModelId = null;
        }
        if (preserveEditingId && previousEditingId && this.aiModels.some(m => m.id === previousEditingId)) {
            this.editingAiModelId = previousEditingId;
        } else {
            this.editingAiModelId = this.activeAiModelId;
        }
        this._pendingFormEdits = {};
    }

    /**
     * 基于两栏结构生成主 HTML
     */
    renderLayout() {
        const userManagementTab = this.isAdmin ? `<div class="settings-menu-item" data-tab="users">
            <svg class="settings-menu-icon" viewBox="0 0 24 24"><path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"/></svg> 
            用户管理
        </div>` : '';
        
        this.container.innerHTML = `
            <div class="settings-layout">
                <aside class="settings-sidebar">
                    <h2 class="settings-sidebar-title">系统配置</h2>
                    <nav class="settings-menu">
                        <div class="settings-menu-item active" data-tab="general">
                            <svg class="settings-menu-icon" viewBox="0 0 24 24"><path d="M19.43 12.98c.04-.32.07-.64.07-.98s-.03-.66-.07-.98l2.11-1.65c.19-.15.24-.42.12-.64l-2-3.46c-.12-.22-.39-.3-.61-.22l-2.49 1c-.52-.4-1.08-.73-1.69-.98l-.38-2.65C14.46 2.18 14.25 2 14 2h-4c-.25 0-.46.18-.49.42l-.38 2.65c-.61.25-1.17.59-1.69.98l-2.49-1c-.23-.09-.49 0-.61.22l-2 3.46c-.13.22-.07.49.12.64l2.11 1.65c-.04.32-.07.65-.07.98s.03.66.07.98l-2.11 1.65c-.19.15-.24.42-.12.64l2 3.46c.12.22.39.3.61.22l2.49-1c.52.4 1.08.73 1.69.98l.38 2.65c.03.24.24.42.49.42h4c.25 0 .46-.18.49-.42l.38-2.65c.61-.25 1.17-.59 1.69-.98l2.49 1c.23.09.49 0 .61-.22l2-3.46c.12-.22.07-.49-.12-.64l-2.11-1.65zM12 15.5c-1.93 0-3.5-1.57-3.5-3.5s1.57-3.5 3.5-3.5 3.5 1.57 3.5 3.5-1.57 3.5-3.5 3.5z"/></svg> 
                            常规设置
                        </div>
                        <div class="settings-menu-item" data-tab="communication">
                            <svg class="settings-menu-icon" viewBox="0 0 24 24"><path d="M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5c-1.66 0-3 1.34-3 3s1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5C6.34 5 5 6.34 5 8s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z"/></svg>
                            通讯连接
                        </div>
                        <div class="settings-menu-item" data-tab="storage">
                            <svg class="settings-menu-icon" viewBox="0 0 24 24"><path d="M2 20h20v-4H2v4zm2-3h2v2H4v-2zM2 4v4h20V4H2zm4 3H4V5h2v2zm-4 7h20v-4H2v4zm2-3h2v2H4v-2z"/></svg>
                            文件存储
                        </div>
                        <div class="settings-menu-item" data-tab="runtime">
                            <svg class="settings-menu-icon" viewBox="0 0 24 24"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 14h-2v-2h2v2zm0-4h-2V7h2v5z"/></svg>
                            执行与运行
                        </div>
                        <div class="settings-menu-item" data-tab="cameras">
                            <svg class="settings-menu-icon" viewBox="0 0 24 24"><circle cx="12" cy="12" r="3.2"/><path d="M9 2L7.17 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2h-3.17L15 2H9zm3 15c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5z"/></svg> 
                            相机管理
                        </div>
                        <div class="settings-menu-item" data-tab="ai">
                            <svg class="settings-menu-icon" viewBox="0 0 24 24"><path d="M21 16.5c0 .38-.21.71-.53.88l-7.9 4.44c-.16.12-.36.18-.57.18-.21 0-.41-.06-.57-.18l-7.9-4.44A.991.991 0 013 16.5v-9c0-.38.21-.71.53-.88l7.9-4.44c.16-.12.36-.18.57-.18.21 0 .41.06.57.18l7.9 4.44c.32.17.53.5.53.88v9zM12 4.15L6.04 7.5 12 10.85l5.96-3.35L12 4.15zM5 15.91l6 3.38v-6.71L5 9.21v6.7zM19 15.91v-6.7l-6 3.37v6.71l6-3.38z"/></svg> 
                            AI 大模型
                        </div>
                        ${userManagementTab}
                    </nav>
                </aside>
                <div class="settings-content-area">
                    <div class="settings-header-banner">
                        <h1 class="settings-main-title">常量预设</h1>
                        <div class="settings-actions">
                            <button class="cv-btn cv-btn-primary" id="btn-save-settings">保存所有更改</button>
                        </div>
                    </div>
                    <div class="settings-tab-panels">
                        <div class="settings-panel active" data-section="general">${this.renderGeneralTab()}</div>
                        <div class="settings-panel" data-section="communication">${this.renderCommunicationTab()}</div>
                        <div class="settings-panel" data-section="storage">${this.renderStorageTab()}</div>
                        <div class="settings-panel" data-section="runtime">${this.renderRuntimeTab()}</div>
                        <div class="settings-panel" data-section="cameras">${this.renderCameraTab()}</div>
                        <div class="settings-panel" data-section="ai">${this.renderAiTab()}</div>
                        ${this.isAdmin ? `<div class="settings-panel" data-section="users">${this.renderUserManagementTab()}</div>` : ''}
                    </div>
                </div>
            </div>
        `;
    }

    activateTab(tabName) {
        if (!this.container) return;
        
        // 侧边栏高亮
        const menuItems = this.container.querySelectorAll('.settings-menu-item');
        menuItems.forEach(item => {
            if (item.dataset.tab === tabName) {
                item.classList.add('active');
                // 同步更新右侧大标题
                const headerTitle = this.container.querySelector('.settings-main-title');
                if (headerTitle) headerTitle.textContent = item.textContent.trim();
            } else {
                item.classList.remove('active');
            }
        });

        // 切换内容面板
        const panels = this.container.querySelectorAll('.settings-panel');
        panels.forEach(panel => {
            if (panel.dataset.section === tabName) {
                panel.classList.add('active');
            } else {
                panel.classList.remove('active');
            }
        });
        
        // 如果是切换到用户管理且是管理员，需要刷新表格
        if (tabName === 'users' && this.isAdmin) {
            this.refreshUserTable();
        } else if (tabName === 'cameras') {
            this.loadCameraBindings();
        }
    }

    bindEvents() {
        if (!this.container) return;

        // 左侧菜单切换
        const menu = this.container.querySelector('.settings-menu');
        if (menu) {
            menu.addEventListener('click', (e) => {
                const menuItem = e.target.closest('.settings-menu-item');
                if (menuItem) {
                    this.activateTab(menuItem.dataset.tab);
                }
            });
        }

        // 保存按钮
        const saveBtn = this.container.querySelector('#btn-save-settings');
        if (saveBtn) {
            saveBtn.addEventListener('click', () => this.save());
        }

        // 绑定相机管理相关事件
        this.bindCameraManagementEvents();

        // 绑定用户管理事件（仅管理员）
        if (this.isAdmin) {
            this.bindUserManagementEvents();
        }

        // 绑定 AI 设置事件
        this.bindAiSettingsEvents();
    }
    
    bindAiSettingsEvents() {
        const aiTab = this.container.querySelector('[data-section="ai"]');
        if (!aiTab) return;
        
        aiTab.addEventListener('click', async (e) => {
            const btn = e.target.closest('button');
            if (!btn) return;
            
            if (btn.id === 'btn-toggle-apikey') {
                const input = aiTab.querySelector('#cfg-ai-apikey');
                if (input) {
                    input.type = input.type === 'password' ? 'text' : 'password';
                    btn.textContent = input.type === 'password' ? '👁' : '🔒';
                }
            } else if (btn.id === 'btn-add-llm') {
                try {
                    const result = await httpClient.post('/ai/models', {
                        name: '新建模型',
                        provider: 'OpenAI Compatible',
                        model: '',
                        baseUrl: '',
                        apiKey: '',
                        timeoutMs: 120000
                    });
                    await this.loadAiModels();
                    this.editingAiModelId = result.id;
                    this._pendingFormEdits = {};
                    this.refreshAiTableAndForm();
                    showToast('模型已创建', 'success');
                } catch (err) {
                    showToast('创建模型失败: ' + err.message, 'error');
                }
            } else if (btn.dataset.action === 'edit') {
                // 切换编辑前先保存当前编辑（如果有未保存的修改）
                this.editingAiModelId = btn.dataset.id;
                this._pendingFormEdits = {};
                this.refreshAiTableAndForm();
            } else if (btn.dataset.action === 'delete') {
                if (this.aiModels.length <= 1) {
                    showToast('至少需保留一个模型', 'warning');
                    return;
                }
                const id = btn.dataset.id;
                try {
                    await httpClient.delete(`/ai/models/${id}`);
                    await this.loadAiModels();
                    if (this.editingAiModelId === id) {
                        this.editingAiModelId = this.aiModels[0]?.id;
                        this._pendingFormEdits = {};
                    }
                    this.refreshAiTableAndForm();
                    showToast('模型已删除', 'success');
                } catch (err) {
                    showToast('删除失败: ' + err.message, 'error');
                }
            } else if (btn.dataset.action === 'activate') {
                const id = btn.dataset.id;
                try {
                    await httpClient.post(`/ai/models/${id}/activate`, {});
                    await this.loadAiModels();
                    this.refreshAiTableAndForm();
                    showToast('激活模型已切换', 'success');
                } catch (err) {
                    showToast('切换激活失败: ' + err.message, 'error');
                }
            } else if (btn.id === 'btn-ai-test') {
                const modelId = this.editingAiModelId;
                if (!modelId) return;
                const resultEl = aiTab.querySelector('#ai-test-result');
                btn.disabled = true;
                btn.textContent = '⏳ 测试中...';
                if (resultEl) resultEl.textContent = '';

                try {
                    // 先保存当前表单到后端，再测试（确保用的是最新配置）
                    await this._saveCurrentForm();
                    const result = await httpClient.post(`/ai/models/${modelId}/test`, {});
                    if (result.success) {
                        if (resultEl) { resultEl.textContent = '✅ ' + result.message; resultEl.style.color = '#4caf50'; }
                        showToast('AI 测试连接成功', 'success');
                    } else {
                        if (resultEl) { resultEl.textContent = '❌ ' + result.message; resultEl.style.color = 'var(--cinnabar)'; }
                        showToast('AI 连接失败: ' + result.message, 'error');
                    }
                } catch (err) {
                    if (resultEl) { resultEl.textContent = '❌ 请求失败: ' + err.message; resultEl.style.color = 'var(--cinnabar)'; }
                    showToast('AI 请求失败: ' + err.message, 'error');
                } finally {
                    btn.disabled = false;
                    btn.textContent = '🔗 测试连接';
                }
            } else if (btn.id === 'btn-ai-save') {
                const modelId = this.editingAiModelId;
                if (!modelId) return;
                try {
                    await this._saveCurrentForm();
                    await httpClient.post(`/ai/models/${modelId}/activate`, {});
                    await this.loadAiModels({ preserveEditingId: true });
                    this.refreshAiTableAndForm();
                    showToast('模型设置已保存', 'success');
                } catch(err) {
                    showToast('保存失败: ' + err.message, 'error');
                }
            }
        });
        
        // 监听表单输入变化（暂存到 _pendingFormEdits，不再实时写 localStorage）
        ['name', 'provider', 'model', 'baseurl', 'apikey', 'timeout'].forEach(f => {
            aiTab.addEventListener('input', (e) => {
                const el = e.target;
                if(el && el.id === `cfg-ai-${f}`) {
                    const fieldMap = { 'name':'name', 'provider':'provider', 'model':'model', 'baseurl':'baseUrl', 'apikey':'apiKey', 'timeout':'timeoutMs' };
                    this._pendingFormEdits[fieldMap[f]] = el.value;
                    // 名称变化时实时刷新表格行
                    if (f === 'name') {
                        const m = this.aiModels.find(x => x.id === this.editingAiModelId);
                        if (m) { m.name = el.value; this.refreshAiTableOnly(); }
                    }
                }
            });
        });
        
        setTimeout(() => this.refreshAiTableAndForm(), 0);
    }

    /** 将当前编辑表单的值保存到后端 */
    async _saveCurrentForm() {
        const modelId = this.editingAiModelId;
        if (!modelId) return;
        const aiTab = this.container.querySelector('[data-section="ai"]');
        if (!aiTab) return;

        const payload = {
            name: aiTab.querySelector('#cfg-ai-name')?.value || '',
            provider: aiTab.querySelector('#cfg-ai-provider')?.value || 'OpenAI Compatible',
            model: aiTab.querySelector('#cfg-ai-model')?.value || '',
            baseUrl: aiTab.querySelector('#cfg-ai-baseurl')?.value || '',
            apiKey: aiTab.querySelector('#cfg-ai-apikey')?.value || '', // 空 → 后端保留原值
            timeoutMs: parseInt(aiTab.querySelector('#cfg-ai-timeout')?.value || '120000', 10)
        };

        await httpClient.put(`/ai/models/${modelId}`, payload);
        await this.loadAiModels({ preserveEditingId: true });
        this._pendingFormEdits = {};
        this.refreshAiTableOnly();
    }
    
    refreshAiTableOnly() {
        const tbody = this.container.querySelector('#ai-models-table tbody');
        if (!tbody) return;
        
        tbody.innerHTML = this.aiModels.map(m => {
            const isEditing = m.id === this.editingAiModelId;
            const badgeBg = m.provider.includes('Anthropic') ? '#fce7f3' : (m.provider.includes('OpenAI API') ? '#e0e7ff' : '#f3f4f6');
            const badgeColor = m.provider.includes('Anthropic') ? '#db2777' : (m.provider.includes('OpenAI API') ? '#4338ca' : '#475569');
            
            return `
                <tr style="${isEditing ? 'background:#f8fafc;' : ''}">
                    <td class="font-bold">${m.name || '-'}</td>
                    <td><span class="type-badge" style="background:${badgeBg}; color:${badgeColor};">${m.provider}</span></td>
                    <td class="font-mono">${m.model || '-'}</td>
                    <td>
                        ${m.isActive 
                            ? '<span class="settings-status-badge status-connected" style="background:#ecfdf5; padding:2px 8px;"><span class="status-dot"></span> Active</span>' 
                            : `<button class="cv-btn settings-btn-light" style="padding:2px 8px; font-size:12px; height:24px;" data-action="activate" data-id="${m.id}">设为激活</button>`}
                    </td>
                    <td>
                        <button class="action-icon-btn" data-action="edit" data-id="${m.id}" title="编辑">
                            <svg viewBox="0 0 24 24"><path d="M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25z"/></svg>
                        </button>
                        <button class="action-icon-btn" data-action="delete" data-id="${m.id}" title="删除" style="color:var(--cinnabar);">
                            <svg viewBox="0 0 24 24"><path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z"/></svg>
                        </button>
                    </td>
                </tr>
            `;
        }).join('');
    }
    
    refreshAiTableAndForm() {
        this.refreshAiTableOnly();
        
        const formContainer = this.container.querySelector('#ai-detail-form');
        if (!formContainer) return;
        
        const m = this.aiModels.find(x => x.id === this.editingAiModelId);
        if (!m) {
            formContainer.innerHTML = '<div style="padding:40px;text-align:center;color:var(--text-muted);">请选择一个模型进行编辑</div>';
            return;
        }

        // apiKey 不再从后端获取真实值，用 placeholder 提示
        const apiKeyPlaceholder = m.hasApiKey ? '●●●●●●（已配置，留空则不修改）' : '请输入 API Key';
        
        formContainer.innerHTML = `
            <div style="display:flex; gap:16px; margin-bottom:16px;">
                 <div class="settings-fieldset" style="flex:1;">
                     <label>模型昵称</label>
                     <input type="text" class="cv-input" id="cfg-ai-name" value="${m.name || ''}" placeholder="本地别名">
                 </div>
                 <div class="settings-fieldset" style="flex:1;">
                     <label>API 协议</label>
                     <select class="cv-input" id="cfg-ai-provider">
                         <option value="OpenAI API" ${m.provider==='OpenAI API'?'selected':''}>OpenAI API</option>
                         <option value="Anthropic Claude" ${m.provider==='Anthropic Claude'?'selected':''}>Anthropic Claude</option>
                         <option value="OpenAI Compatible" ${m.provider==='OpenAI Compatible'?'selected':''}>本地模型 / OpenAI 兼容 (Ollama, vLLM, GLM等)</option>
                     </select>
                 </div>
                 <div class="settings-fieldset" style="flex:1;">
                     <label>模型选择</label>
                     <input type="text" class="cv-input" id="cfg-ai-model" value="${m.model || ''}" placeholder="如 deepseek-chat">
                 </div>
             </div>
             <div class="settings-fieldset" style="margin-bottom:16px;">
                 <label>API Endpoint (Host & Path)</label>
                 <div style="display:flex;">
                     <span style="padding:10px 12px; background:#f8fafc; border:1px solid #cbd5e1; border-right:none; border-radius:6px 0 0 6px; color:#64748b;">URL:</span>
                     <input type="text" class="cv-input" id="cfg-ai-baseurl" value="${m.baseUrl || ''}" placeholder="如 https://api.deepseek.com/v1" style="border-radius:0 6px 6px 0;">
                 </div>
             </div>
             <div style="display:flex; gap:16px;">
                 <div class="settings-fieldset" style="flex:2;">
                     <label>API Key</label>
                     <div class="input-with-suffix" style="position:relative;">
                         <input type="password" class="cv-input" id="cfg-ai-apikey" value="" placeholder="${apiKeyPlaceholder}" style="padding-right:36px; font-family:monospace;">
                         <button class="icon-action-btn" id="btn-toggle-apikey" style="position:absolute; right:10px; top:50%; transform:translateY(-50%);">👁</button>
                     </div>
                 </div>
                 <div class="settings-fieldset" style="flex:1;">
                     <label>请求超时 (ms)</label>
                     <input type="number" class="cv-input" id="cfg-ai-timeout" value="${m.timeoutMs || 120000}">
                 </div>
             </div>
             <div style="display:flex; justify-content:flex-end; gap:12px; margin-top:24px;">
                 <button class="cv-btn settings-btn-light" id="btn-ai-test">🔗 测试连接</button>
                 <button class="cv-btn settings-btn-danger" id="btn-ai-save">💾 保存并应用该模型集</button>
             </div>
             <div id="ai-test-result" style="margin-top:10px; text-align:right; font-size:13px; font-weight:500;"></div>
        `;
    }
    
    bindCameraManagementEvents() {
        const section = this.container.querySelector('[data-section="cameras"]');
        if (!section) return;

        const discoverBtn = section.querySelector('#btn-discover-cameras');
        if (discoverBtn) {
            discoverBtn.addEventListener('click', () => this.discoverCameras());
        }

        const calibBtn = section.querySelector('#btn-hand-eye-calib');
        if (calibBtn) {
            calibBtn.addEventListener('click', async () => {
                try {
                    // 动态导入向导并显示
                    const module = await import('../../core/calibration/handEyeCalibWizard.js');
                    const wizard = new module.HandEyeCalibWizard(window.cameraManager);
                    wizard.show();
                } catch (e) {
                    showToast('无法加载手眼标定向导: ' + e.message, 'error');
                }
            });
        }

        const tbody = this.container.querySelector('#camera-bindings-table tbody');
        if (tbody) {
            tbody.addEventListener('click', (e) => {
                const tr = e.target.closest('tr.camera-row');
                if (!tr) return;

                // 点击选中行，展示详情
                this.selectCameraRow(tr);

                // 删除按钮
                const deleteBtn = e.target.closest('.action-icon-btn');
                if (deleteBtn) {
                    const id = tr.dataset.id;
                    if (confirm('确定要删除此相机配置吗？')) {
                        this.cameraBindings = this.cameraBindings.filter(b => b.id !== id);
                        this.refreshCameraTable();
                        showToast('已移除相机配置', 'success');
                    }
                }
            });
        }
    }

    async loadCameraBindings() {
        const tbody = this.container.querySelector('#camera-bindings-table tbody');
        if (tbody) {
            tbody.innerHTML = `<tr><td colspan="5" style="text-align:center; padding: 24px;"><div class="cv-spinner" style="margin-right:8px; display:inline-block;"></div>正在加载相机配置...</td></tr>`;
        }

        try {
            const bindings = await httpClient.get('/cameras/bindings');
            this.cameraBindings = bindings || [];
            this.refreshCameraTable();
        } catch (error) {
            console.error('Failed to load camera bindings:', error);
            if (tbody) {
                tbody.innerHTML = `<tr><td colspan="5" style="text-align:center; padding: 20px; color:var(--accent);">加载配置失败: ` + error.message + `</td></tr>`;
            }
        }
    }

    async discoverCameras() {
        showToast('正在搜索在线相机...', 'info');
        const discoverBtn = this.container.querySelector('#btn-discover-cameras');
        if (discoverBtn) discoverBtn.disabled = true;

        try {
            const devices = await httpClient.get('/cameras/discover');
            
            if (devices && devices.length > 0) {
                showToast(`找到 ${devices.length} 个相机设备`, 'success');
                this.showDiscoveryModal(devices);
            } else {
                showToast('未发现任何在线相机', 'warning');
            }
        } catch (error) {
            showToast('搜索相机失败: ' + error.message, 'error');
        } finally {
            if (discoverBtn) discoverBtn.disabled = false;
        }
    }

    showDiscoveryModal(devices) {
        const contentDiv = document.createElement('div');
        contentDiv.innerHTML = `
            <div class="settings-card-table-wrapper" style="max-height: 400px; overflow-y: auto;">
                <table class="settings-modern-table" style="margin: 0; width: 100%;">
                    <thead>
                        <tr>
                            <th>序列号 (IP)</th>
                            <th>制造商</th>
                            <th>型号</th>
                            <th>驱动/协议</th>
                            <th>操作</th>
                        </tr>
                    </thead>
                    <tbody>
                        ${devices.map(d => `
                            <tr>
                                <td><code style="background:var(--panel-bg); padding:2px 6px; border-radius:4px; font-size:13px; font-family:var(--font-mono);">${d.cameraId}</code></td>
                                <td>${d.manufacturer || '-'}</td>
                                <td>${d.model || '-'}</td>
                                <td>${d.connectionType || '-'}</td>
                                <td>
                                    <button class="cv-btn cv-btn-primary btn-bind-camera" 
                                            data-sn="${d.cameraId}" 
                                            data-man="${d.manufacturer}" 
                                            data-model="${d.model}" 
                                            style="padding:6px 12px; font-size:13px; border-radius:6px;">
                                        添加绑定
                                    </button>
                                </td>
                            </tr>
                        `).join('')}
                    </tbody>
                </table>
            </div>
            <p style="margin-top:16px; font-size:13px; color:var(--text-muted);">
                * 点击“添加绑定”可以将选中设备汇入到您的视觉工程工作区中。
            </p>
        `;

        // 这里需要引入外面的全局方法或者之前 import 进来的 createModal
        // 顶部的 import { showToast, createModal } from '../../shared/components/uiComponents.js'; 已经确保了可用
        const modal = createModal({
            title: '配置向导：发现在线相机',
            content: contentDiv,
            width: '720px'
        });

        // 绑定点击事件
        const bindBtns = contentDiv.querySelectorAll('.btn-bind-camera');
        bindBtns.forEach(btn => {
            btn.addEventListener('click', async () => {
                const sn = btn.dataset.sn;
                const manufacturer = btn.dataset.man;
                const model = btn.dataset.model;

                // 检查是否已存在
                if (this.cameraBindings.find(b => b.serialNumber === sn)) {
                    showToast('该相机已在绑定列表中，无需重复添加', 'warning');
                    return;
                }

                const displayName = prompt('请输入该相机的逻辑命名 (如: Left_Camera_01):', `Cam_${this.cameraBindings.length + 1}`);
                if (!displayName) return;

                const newBinding = {
                    id: Math.random().toString(36).substr(2, 8), // 模拟 GUID
                    displayName: displayName,
                    serialNumber: sn,
                    manufacturer: manufacturer,
                    modelName: model,
                    ipAddress: sn, // 简单的演示赋值
                    isEnabled: true
                };

                this.cameraBindings.push(newBinding);
                this.refreshCameraTable();
                showToast(`已成功绑定逻辑相机: ${displayName}`, 'success');
                
                // 置灰当前按钮，防止重复点击
                btn.disabled = true;
                btn.textContent = '已绑定';
                btn.classList.add('settings-btn-light');
                btn.classList.remove('cv-btn-primary');
            });
        });
    }

    refreshCameraTable() {
        const tbody = this.container.querySelector('#camera-bindings-table tbody');
        if (!tbody) return;

        if (!this.cameraBindings || this.cameraBindings.length === 0) {
            tbody.innerHTML = '<tr><td colspan="5" style="text-align:center; color:var(--text-muted); padding:24px;">暂无绑定配置，请点击“搜索相机”以发现及更新</td></tr>';
            return;
        }

        tbody.innerHTML = this.cameraBindings.map((b, index) => {
            const isConnected = b.isEnabled !== false; // 假设字段
            const statusClass = isConnected ? 'status-connected' : 'status-error';
            const statusDotClass = isConnected ? 'status-dot' : 'status-dot status-error';
            const statusText = isConnected ? '已连接' : '已断开';
            const bgClass = index === 0 ? '#fee2e2' : '#e0e7ff';
            const fgClass = index === 0 ? 'var(--cinnabar)' : 'var(--primary)';

            return `
            <tr class="camera-row" data-id="${b.id}" style="cursor: pointer;">
                <td>
                    <div style="display:flex; align-items:center; gap:12px;">
                        <div style="width:32px; height:32px; background:${bgClass}; border-radius:8px; display:flex; align-items:center; justify-content:center; color:${fgClass};">
                            <svg viewBox="0 0 24 24" style="width:18px;height:18px;fill:currentColor;"><path d="M12 4C7.58 4 4 7.58 4 12s3.58 8 8 8 8-3.58 8-8-3.58-8-8-8zm0 14c-3.31 0-6-2.69-6-6s2.69-6 6-6 6 2.69 6 6-2.69 6-6 6zM12 7c-2.76 0-5 2.24-5 5s2.24 5 5 5 5-2.24 5-5-2.24-5-5-5zm0 8c-1.65 0-3-1.35-3-3s1.35-3 3-3 3 1.35 3 3-1.35 3-3 3z"/></svg>
                        </div>
                        <div>
                            <div class="font-bold">${b.displayName || '未命名相机'}</div>
                            <div class="text-muted" style="font-size:12px;">${b.serialNumber || '未知'}</div>
                        </div>
                    </div>
                </td>
                <td><span class="font-mono">${b.ipAddress || '192.168.x.x'}</span></td>
                <td>${b.manufacturer || '未知'}</td>
                <td><span class="settings-status-badge ${statusClass}"><span class="${statusDotClass}"></span> ${statusText}</span></td>
                <td><button class="action-icon-btn"><svg viewBox="0 0 24 24"><path d="M12 8c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2zm0 2c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm0 6c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z"/></svg></button></td>
            </tr>
            `;
        }).join('');
    }

    selectCameraRow(tr) {
        // 取消其他行高亮
        const allRows = this.container.querySelectorAll('tr.camera-row');
        allRows.forEach(r => r.style.backgroundColor = '');
        
        // 高亮当前行
        tr.style.backgroundColor = 'var(--panel-bg)';

        const id = tr.getAttribute('data-id');
        const cam = this.cameraBindings.find(b => b.id === id);
        
        // 更新参数面板
        if (cam) {
            const nameEl = this.container.querySelector('#current-cam-name');
            if (nameEl) nameEl.textContent = cam.displayName || '未命名相机';

            // 更新输入框 (如果有)
            const exposeInput = this.container.querySelector('input[type="number"]');
            if(exposeInput) {
               exposeInput.value = 5000;
            }
        }
    }

    // （演示用空壳。需要配合 Modal使用，这里只挂载入口）
    bindUserManagementEvents() {
        const addBtn = this.container.querySelector('#btn-add-user');
        if (addBtn) addBtn.addEventListener('click', () => showToast('请在旧版面板或完成 UI 重构后添加用户。', 'info'));
    }

    // ----- UI 渲染块（为了测试顺利，简化版） -----
    getDefaultConfig() {
        return {
            general: { softwareTitle: 'ClearVision', theme: 'dark', autoStart: false },
            communication: { plcIpAddress: '192.168.1.100', plcPort: 502, protocol: 'ModbusTcp', heartbeatIntervalMs: 1000 },
            storage: { imageSavePath: 'D:\\VisionData\\Images', savePolicy: 'NgOnly', retentionDays: 30, minFreeSpaceGb: 5 },
            runtime: { autoRun: false, stopOnConsecutiveNg: 0 }
        };
    }

    renderGeneralTab() {
        const general = this.config?.general || this.getDefaultConfig().general;
        return `
            <div class="settings-section-title">
                <h2>常规设置</h2>
                <p>配置系统层面的基础选项，包括界面显示和启动行为。</p>
            </div>
            
            <div class="settings-modern-card">
                <div class="settings-card-header">
                    <div class="settings-header-left">
                        <svg viewBox="0 0 24 24" class="settings-header-icon"><path d="M19.43 12.98c.04-.32.07-.64.07-.98s-.03-.66-.07-.98l2.11-1.65c.19-.15.24-.42.12-.64l-2-3.46c-.12-.22-.39-.3-.61-.22l-2.49 1c-.52-.4-1.08-.73-1.69-.98l-.38-2.65C14.46 2.18 14.25 2 14 2h-4c-.25 0-.46.18-.49.42l-.38 2.65c-.61.25-1.17.59-1.69.98l-2.49-1c-.23-.09-.49 0-.61.22l-2 3.46c-.13.22-.07.49-.12-.64l2.11 1.65c-.04.32-.07.65-.07.98s.03.66.07.98l-2.11 1.65c-.19.15-.24.42-.12.64l2 3.46c.12.22.39.3.61.22l2.49-1c.52.4 1.08.73 1.69.98l.38 2.65c.03.24.24.42.49.42h4c.25 0 .46-.18.49-.42l.38-2.65c.61-.25 1.17-.59 1.69-.98l2.49 1c.23.09.49 0 .61-.22l2-3.46c.12-.22.07-.49-.12-.64l-2.11-1.65zM12 15.5c-1.93 0-3.5-1.57-3.5-3.5s1.57-3.5 3.5-3.5 3.5 1.57 3.5 3.5-1.57 3.5-3.5 3.5z"/></svg>
                        <span>基础配置</span>
                    </div>
                </div>
                <div class="settings-card-body">
                    <div class="settings-fieldset" style="max-width: 400px;">
                        <label>软件标题 (Software Title)</label>
                        <div class="input-with-icon">
                            <svg class="input-icon" viewBox="0 0 24 24" style="fill:#94a3b8;"><path d="M5 4v3h5.5v12h3V7H19V4z"/></svg>
                            <input type="text" class="cv-input" id="cfg-softwareTitle" value="${general.softwareTitle || ''}" placeholder="ClearVision">
                        </div>
                        <span class="settings-field-hint">将显示在系统顶部的全局标题栏上</span>
                    </div>
                    
                    <div style="margin-top:24px; display:flex; gap:24px;">
                        <div class="settings-fieldset" style="flex:1;">
                            <label>系统主题 (Theme)</label>
                            <select class="cv-input" id="cfg-theme">
                                <option value="light" ${general.theme === 'light' ? 'selected' : ''}>浅色主题 (Light)</option>
                                <option value="dark" ${general.theme === 'dark' ? 'selected' : ''}>深色主题 (Dark)</option>
                            </select>
                        </div>
                        <div class="settings-fieldset" style="flex:1; display:flex; flex-direction:column; justify-content:flex-end;">
                            <label style="display:flex; align-items:center; gap:8px; cursor:pointer; margin-bottom:12px;">
                                <input type="checkbox" id="cfg-autoStart" ${general.autoStart ? 'checked' : ''} style="width:16px; height:16px; accent-color:var(--cinnabar);">
                                开机自动启动软件
                            </label>
                        </div>
                    </div>
                </div>
                <div class="settings-card-body" style="border-top:1px solid #e2e8f0; display:flex; justify-content:flex-end; padding:16px 24px;">
                    <button class="cv-btn settings-btn-danger">
                        <svg viewBox="0 0 24 24" style="width:16px; height:16px; margin-right:6px; fill:currentColor;"><path d="M17 3H5c-1.11 0-2 .9-2 2v14c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V7l-4-4zm-5 16c-1.66 0-3-1.34-3-3s1.34-3 3-3 3 1.34 3 3-1.34 3-3 3zm3-10H5V5h10v4z"/></svg>
                        保存常规设置
                    </button>
                </div>
            </div>
        `;
    }

    renderCommunicationTab() {
        const comm = this.config?.communication || this.getDefaultConfig().communication;
        return `
            <div class="settings-section-title">
                <h2>PLC 通讯配置</h2>
                <p>配置与外部控制器的数据交换协议和地址映射。</p>
            </div>

            <!-- Block 1: 通讯连接设置 -->
            <div class="settings-modern-card">
                <div class="settings-card-header has-badge">
                    <div class="settings-header-left">
                        <svg viewBox="0 0 24 24" class="settings-header-icon"><path d="M19 15v4H5v-4h14m1-2H4c-.55 0-1 .45-1 1v6c0 .55.45 1 1 1h16c.55 0 1-.45 1-1v-6c0-.55-.45-1-1-1zM7 18.5c-.82 0-1.5-.67-1.5-1.5s.68-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5zM19 5v4H5V5h14m1-2H4c-.55 0-1 .45-1 1v6c0 .55.45 1 1 1h16c.55 0 1-.45 1-1V4c0-.55-.45-1-1-1zM7 8.5c-.82 0-1.5-.67-1.5-1.5S6.18 5.5 7 5.5s1.5.67 1.5 1.5S7.82 8.5 7 8.5z"/></svg>
                        <span>通讯连接设置</span>
                    </div>
                    <div class="settings-status-badge status-connected">
                        <span class="status-dot"></span> 已连接
                    </div>
                </div>
                
                <div class="settings-card-body horizontal-flex">
                    <div class="settings-fieldset" style="flex:1.5;">
                        <label>通讯协议</label>
                        <select class="cv-input" id="cfg-protocol">
                            <option value="ModbusTcp" ${comm.protocol === 'ModbusTcp' ? 'selected' : ''}>Modbus TCP</option>
                            <option value="S7" ${comm.protocol === 'S7' ? 'selected' : ''}>Siemens S7</option>
                            <option value="CIP" ${comm.protocol === 'CIP' ? 'selected' : ''}>EtherNet/IP (CIP)</option>
                        </select>
                    </div>
                    <div class="settings-fieldset" style="flex:2;">
                        <label>PLC IP地址</label>
                        <div class="input-with-icon">
                            <svg class="input-icon" viewBox="0 0 24 24"><path d="M4 6h16v2H4zm0 5h16v2H4zm0 5h16v2H4z"/></svg>
                            <input type="text" class="cv-input" id="cfg-plcIpAddress" value="${comm.plcIpAddress || ''}">
                        </div>
                    </div>
                    <div class="settings-fieldset" style="flex:1;">
                        <label>端口号</label>
                        <input type="number" class="cv-input" id="cfg-plcPort" value="${comm.plcPort || 502}">
                    </div>
                    <div class="settings-fieldset-action">
                        <button class="cv-btn settings-btn-dark">
                            <svg viewBox="0 0 24 24"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z"/></svg>
                            连接测试
                        </button>
                    </div>
                </div>
            </div>

            <!-- Block 2: 地址映射表 -->
            <div class="settings-modern-card" style="margin-top: 24px;">
                <div class="settings-card-header">
                    <div class="settings-header-left">
                        <svg viewBox="0 0 24 24" class="settings-header-icon"><path d="M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-5 14H7v-2h7v2zm3-4H7v-2h10v2zm0-4H7V7h10v2z"/></svg>
                        <span>地址映射表 (Address Mapping)</span>
                    </div>
                    <div class="settings-header-actions">
                        <button class="icon-action-btn"><svg viewBox="0 0 24 24"><path d="M9 16h6v-6h4l-7-7-7 7h4zm-4 2h14v2H5z"/></svg></button>
                        <button class="icon-action-btn"><svg viewBox="0 0 24 24"><path d="M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z"/></svg></button>
                        <button class="cv-btn settings-btn-light" style="padding: 4px 12px; margin-left: 8px;">
                            <span style="font-size: 16px; margin-right: 4px;">+</span> 添加变量
                        </button>
                    </div>
                </div>
                
                <div class="settings-card-table-wrapper">
                    <table class="settings-modern-table">
                        <thead>
                            <tr>
                                <th>变量名称</th>
                                <th>PLC地址</th>
                                <th>数据类型</th>
                                <th>读/写</th>
                                <th>注释</th>
                                <th>操作</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr>
                                <td class="font-bold">Trigger_Start</td>
                                <td class="font-mono">D1000</td>
                                <td><span class="type-badge badge-bool">BOOL</span></td>
                                <td><span class="rw-badge rw-read">R</span></td>
                                <td class="text-muted italic">检测启动信号</td>
                                <td>
                                    <button class="action-icon-btn"><svg viewBox="0 0 24 24"><path d="M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z"/></svg></button>
                                    <button class="action-icon-btn"><svg viewBox="0 0 24 24"><path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z"/></svg></button>
                                </td>
                            </tr>
                            <tr>
                                <td class="font-bold">Product_ID</td>
                                <td class="font-mono">D1002</td>
                                <td><span class="type-badge badge-int">INT16</span></td>
                                <td><span class="rw-badge rw-read">R</span></td>
                                <td class="text-muted italic">当前产品编号</td>
                                <td>
                                    <button class="action-icon-btn"><svg viewBox="0 0 24 24"><path d="M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z"/></svg></button>
                                    <button class="action-icon-btn"><svg viewBox="0 0 24 24"><path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z"/></svg></button>
                                </td>
                            </tr>
                            <tr>
                                <td class="font-bold">Result_OK</td>
                                <td class="font-mono">D2000</td>
                                <td><span class="type-badge badge-bool">BOOL</span></td>
                                <td><span class="rw-badge rw-write">W</span></td>
                                <td class="text-muted italic">OK结果输出</td>
                                <td>
                                    <button class="action-icon-btn"><svg viewBox="0 0 24 24"><path d="M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z"/></svg></button>
                                    <button class="action-icon-btn"><svg viewBox="0 0 24 24"><path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z"/></svg></button>
                                </td>
                            </tr>
                            <tr>
                                <td class="font-bold">Result_NG</td>
                                <td class="font-mono">D2001</td>
                                <td><span class="type-badge badge-bool">BOOL</span></td>
                                <td><span class="rw-badge rw-write">W</span></td>
                                <td class="text-muted italic">NG结果输出</td>
                                <td>
                                    <button class="action-icon-btn"><svg viewBox="0 0 24 24"><path d="M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z"/></svg></button>
                                    <button class="action-icon-btn"><svg viewBox="0 0 24 24"><path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z"/></svg></button>
                                </td>
                            </tr>
                            <tr>
                                <td class="font-bold">Coordinate_X</td>
                                <td class="font-mono">D2010</td>
                                <td><span class="type-badge badge-float">FLOAT</span></td>
                                <td><span class="rw-badge rw-write">W</span></td>
                                <td class="text-muted italic">定位X坐标</td>
                                <td>
                                    <button class="action-icon-btn"><svg viewBox="0 0 24 24"><path d="M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z"/></svg></button>
                                    <button class="action-icon-btn"><svg viewBox="0 0 24 24"><path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z"/></svg></button>
                                </td>
                            </tr>
                        </tbody>
                    </table>
                </div>
            </div>
            
            <!-- 底部浮动操作区 -->
            <div class="settings-floating-footer">
                <button class="cv-btn settings-btn-light" style="width: 100px;">取消</button>
                <button class="cv-btn settings-btn-danger" style="width: 140px;" id="btn-save-plc">
                    <svg viewBox="0 0 24 24" style="width: 18px; height: 18px; margin-right: 6px; fill: currentColor;"><path d="M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46C19.54 15.03 20 13.57 20 12c0-4.42-3.58-8-8-8zm0 14c-3.31 0-6-2.69-6-6 0-1.01.25-1.97.7-2.8L5.24 7.74C4.46 8.97 4 10.43 4 12c0 4.42 3.58 8 8 8v3l4-4-4-4v3z"/></svg> 
                    同步配置
                </button>
            </div>
        `;
    }

    renderStorageTab() {
        const storage = this.config?.storage || this.getDefaultConfig().storage;
        return `
            <div class="settings-section-title">
                <h2>文件与存储管理</h2>
                <p>配置图像数据保存路径、清理策略与磁盘容量预警。</p>
            </div>
            
            <div class="settings-modern-card" style="margin-bottom: 24px;">
                <div class="settings-card-header">
                    <div class="settings-header-left">
                        <svg viewBox="0 0 24 24" class="settings-header-icon"><path d="M2 20h20v-4H2v4zm2-3h2v2H4v-2zM2 4v4h20V4H2zm4 3H4V5h2v2zm-4 7h20v-4H2v4zm2-3h2v2H4v-2z"/></svg> 
                        <span>存储路径配置</span>
                    </div>
                </div>
                <div class="settings-card-body">
                    <div class="settings-fieldset">
                        <label>默认保存路径 (Image Save Path)</label>
                        <div style="display:flex; gap:12px;">
                            <div class="input-with-icon" style="flex:1;">
                                <svg class="input-icon" viewBox="0 0 24 24" style="fill:#fbbf24;"><path d="M10 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2h-8l-2-2z"/></svg>
                                <input type="text" class="cv-input" id="cfg-imageSavePath" value="${storage.imageSavePath || 'D:\\VisionData\\Images'}">
                            </div>
                            <button class="cv-btn settings-btn-light" style="padding:0 20px;">更改目录</button>
                        </div>
                    </div>
                </div>
            </div>

            <div style="display:flex; gap:24px;">
                <div class="settings-modern-card" style="flex:1.5;">
                    <div class="settings-card-header">
                        <div class="settings-header-left">
                           <svg viewBox="0 0 24 24" class="settings-header-icon"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z"/></svg>
                           <span>清理策略与预警</span>
                        </div>
                    </div>
                    <div class="settings-card-body">
                        <div style="display:flex; gap:24px; margin-bottom: 20px;">
                            <div class="settings-fieldset" style="flex:1;">
                                <label>保存策略 (Save Policy)</label>
                                <select class="cv-input" id="cfg-savePolicy">
                                    <option value="All" ${storage.savePolicy === 'All' ? 'selected' : ''}>保存所有图像 (All)</option>
                                    <option value="NgOnly" ${storage.savePolicy === 'NgOnly' ? 'selected' : ''}>仅保存 NG 图像</option>
                                    <option value="None" ${storage.savePolicy === 'None' ? 'selected' : ''}>不保存 (None)</option>
                                </select>
                            </div>
                            <div class="settings-fieldset" style="flex:1;">
                                <label>自动清理阈值 (天)</label>
                                <div class="input-with-suffix" style="position:relative;">
                                    <input type="number" class="cv-input" value="${storage.retentionDays || 30}" style="padding-right:36px;">
                                    <span style="position:absolute; right:12px; top:50%; transform:translateY(-50%); color:#94a3b8; font-size:13px;">Days</span>
                                </div>
                            </div>
                        </div>
                        <div class="settings-fieldset">
                            <label>磁盘低空间预警 (GB)</label>
                            <div class="input-with-suffix" style="position:relative; max-width: 200px;">
                                <input type="number" class="cv-input" value="${storage.minFreeSpaceGb || 5}" style="padding-right:36px;">
                                <span style="position:absolute; right:12px; top:50%; transform:translateY(-50%); color:#94a3b8; font-size:13px;">GB</span>
                            </div>
                            <span class="settings-field-hint">当磁盘剩余空间不足该值时，系统会报警并禁止生产启动。</span>
                        </div>
                    </div>
                </div>
                
                <div class="settings-modern-card" style="flex:1;">
                    <div class="settings-card-body" style="padding: 32px 24px;">
                        <div style="display:flex; justify-content:space-between; margin-bottom:12px;">
                            <span style="font-size:13px; font-weight:600; color:#475569;">D:\\ 磁盘空间</span>
                            <span style="font-size:13px; font-weight:700; color:#0f172a;">85% 已用</span>
                        </div>
                        <div style="background:#e2e8f0; height:8px; border-radius:4px; overflow:hidden; margin-bottom:24px;">
                            <div style="background:var(--cinnabar); width:85%; height:100%;"></div>
                        </div>
                        
                        <div style="display:flex; justify-content:space-between; margin-bottom:8px; font-size:13px;">
                            <span class="text-muted">已用空间</span>
                            <span class="font-bold">425 GB</span>
                        </div>
                        <div style="display:flex; justify-content:space-between; font-size:13px;">
                            <span class="text-muted">可用空间</span>
                            <span class="font-bold" style="color:#059669;">75 GB</span>
                        </div>
                        
                        <button class="cv-btn settings-btn-light" style="width:100%; margin-top:32px;">立即清理过期文件</button>
                    </div>
                </div>
            </div>
        `;
    }

    renderRuntimeTab() {
        const runtime = this.config?.runtime || this.getDefaultConfig().runtime;
        return `
            <div class="settings-section-title" style="display:flex; justify-content:space-between; align-items:flex-end;">
                <div>
                    <h2>执行与控制 (Runtime)</h2>
                    <p>配置自动运行逻辑、异常停机条件及硬件联动保护。</p>
                </div>
                <div class="settings-status-badge status-connected" style="background:#fef2f2; color:#b91c1c; border-color:#fca5a5;">
                    <span class="status-dot" style="background:#dc2626;"></span> 保护机制已启用
                </div>
            </div>

            <div class="settings-modern-card">
                <div class="settings-card-header">
                    <div class="settings-header-left">
                        <svg viewBox="0 0 24 24" class="settings-header-icon" style="fill:#dc2626;"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z"/></svg>
                        <span>生产保护机制 (Production Guards)</span>
                    </div>
                </div>
                <div class="settings-card-body">
                    <div style="display:flex; gap:24px;">
                        <div class="settings-fieldset" style="flex:1;">
                            <label>连续 NG 停机报警阈值</label>
                            <div class="input-with-suffix" style="position:relative;">
                                <input type="number" class="cv-input" id="cfg-stopOnConsecutiveNg" value="${runtime.stopOnConsecutiveNg || 0}" style="padding-right:48px;">
                                <span style="position:absolute; right:12px; top:50%; transform:translateY(-50%); color:#94a3b8; font-size:13px;">件/次</span>
                            </div>
                            <span class="settings-field-hint">设为 0 时关闭此功能。当连续检测失败次数达到设定值，系统将暂停并发送报警至 PLC。</span>
                        </div>
                        
                        <div class="settings-fieldset" style="flex:1;">
                            <label>缺料等待超时 (秒)</label>
                            <div class="input-with-suffix" style="position:relative;">
                                <input type="number" class="cv-input" value="30" style="padding-right:36px;">
                                <span style="position:absolute; right:12px; top:50%; transform:translateY(-50%); color:#94a3b8; font-size:13px;">s</span>
                            </div>
                            <span class="settings-field-hint">触发信号到来前超出该时间未收到下一次触发，抛出超时警告。</span>
                        </div>
                    </div>

                    <div style="margin-top: 24px;">
                        <div class="settings-fieldset">
                            <label style="display:flex; align-items:center; gap:8px; cursor:pointer;">
                                <input type="checkbox" id="cfg-autoRun" ${runtime.autoRun ? 'checked' : ''} style="width:16px; height:16px; accent-color:var(--cinnabar);">
                                软件就绪后自动进入“连续运行”状态
                            </label>
                            <span class="settings-field-hint" style="margin-left: 24px;">注意：启用此项可能导致系统启动即抛出触发信号需求，请确保外部环境安全。</span>
                        </div>
                    </div>
                </div>
                <div class="settings-card-body" style="border-top:1px solid #e2e8f0; display:flex; justify-content:flex-end; padding:16px 24px;">
                    <button class="cv-btn settings-btn-danger">
                        <svg viewBox="0 0 24 24" style="width:16px; height:16px; margin-right:6px; fill:currentColor;"><path d="M17 3H5c-1.11 0-2 .9-2 2v14c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V7l-4-4zm-5 16c-1.66 0-3-1.34-3-3s1.34-3 3-3 3 1.34 3 3-1.34 3-3 3zm3-10H5V5h10v4z"/></svg>
                        应用保护规则
                    </button>
                </div>
            </div>
        `;
    }
    
    renderCameraTab() {
        return `
            <div class="settings-section-title" style="display:flex; justify-content:space-between; align-items:flex-end;">
                <div>
                    <h2>相机管理</h2>
                    <p>配置和管理视觉系统连接的工业相机参数。</p>
                </div>
                <div class="settings-actions">
                    <button class="cv-btn settings-btn-light" id="btn-discover-cameras">
                        <svg viewBox="0 0 24 24" style="width:16px; height:16px; margin-right:6px; fill:currentColor;"><path d="M17.65 6.35C16.2 4.9 14.21 4 12 4c-4.42 0-7.99 3.58-7.99 8s3.57 8 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08c-.82 2.33-3.04 4-5.65 4-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z"/></svg>
                        搜索相机
                    </button>
                    <!-- <button class="cv-btn settings-btn-danger">
                        <span style="font-size:16px; margin-right:4px;">+</span> 添加相机
                    </button> -->
                </div>
            </div>

            <div class="settings-modern-card">
                <div class="settings-card-table-wrapper">
                    <table class="settings-modern-table" id="camera-bindings-table">
                        <thead>
                            <tr>
                                <th>名称</th>
                                <th>IP地址/序列号</th>
                                <th>驱动类型</th>
                                <th>状态</th>
                                <th>操作</th>
                            </tr>
                        </thead>
                        <tbody>
                            <!-- 加载后端数据 -->
                            <tr><td colspan="5" style="text-align:center; padding: 24px;"><div class="cv-spinner" style="margin-right:8px; display:inline-block;"></div>正在加载相机配置...</td></tr>
                        </tbody>
                    </table>
                </div>
            </div>

            <!-- Parameters Card -->
            <div class="settings-modern-card" style="margin-top:24px; background:#fafbfc;">
                <div class="settings-card-header" style="background:#ffffff; display:flex; justify-content:space-between;">
                    <div class="settings-header-left">
                        <svg viewBox="0 0 24 24" class="settings-header-icon" style="fill:#94a3b8;"><path d="M3 17v2h6v-2H3zM3 5v2h10V5H3zm10 16v-2h8v-2h-8v-2h-2v6h2zM7 9v2H3v2h4v2h2V9H7zm14 4v-2H11v2h10zm-6-4h2V7h4V5h-4V3h-2v6z"/></svg>
                        <span>参数配置: <span id="current-cam-name">未选择相机</span></span>
                    </div>
                </div>
                <div class="settings-card-body">
                    <div style="display:grid; grid-template-columns: repeat(3, 1fr); gap:24px; margin-bottom: 24px;">
                        <div class="settings-fieldset">
                            <label>曝光时间 (Exposure Time)</label>
                            <div class="input-with-suffix" style="position:relative;">
                                <input type="number" class="cv-input" value="" style="padding-right:36px;">
                                <span style="position:absolute; right:12px; top:50%; transform:translateY(-50%); color:#94a3b8; font-size:13px;">µs</span>
                            </div>
                            <span class="settings-field-hint">范围: 10 - 1000000 µs</span>
                        </div>
                        <div class="settings-fieldset">
                            <label>增益 (Gain)</label>
                            <div class="input-with-suffix" style="position:relative;">
                                <input type="number" step="0.1" class="cv-input" value="" style="padding-right:36px;">
                                <span style="position:absolute; right:12px; top:50%; transform:translateY(-50%); color:#94a3b8; font-size:13px;">dB</span>
                            </div>
                            <span class="settings-field-hint">范围: 0.0 - 24.0 dB</span>
                        </div>
                        <div class="settings-fieldset">
                            <label>触发模式 (Trigger Mode)</label>
                            <select class="cv-input">
                                <option>Hardware Trigger (Line 1)</option>
                                <option>Software Trigger</option>
                                <option>Continuous</option>
                            </select>
                            <span class="settings-field-hint">当前信号源: PLC_Output_01</span>
                        </div>
                    </div>
                    <div style="display:grid; grid-template-columns: repeat(3, 1fr); gap:24px;">
                        <div class="settings-fieldset">
                            <label>采集帧率 (Frame Rate)</label>
                            <div class="input-with-suffix" style="position:relative;">
                                <input type="number" class="cv-input" value="" style="padding-right:36px;">
                                <span style="position:absolute; right:12px; top:50%; transform:translateY(-50%); color:#94a3b8; font-size:13px;">fps</span>
                            </div>
                        </div>
                        <div class="settings-fieldset">
                            <label>图像宽度 (Width)</label>
                            <div class="input-with-suffix" style="position:relative;">
                                <input type="number" class="cv-input" value="" style="padding-right:36px;">
                                <span style="position:absolute; right:12px; top:50%; transform:translateY(-50%); color:#94a3b8; font-size:13px;">px</span>
                            </div>
                        </div>
                        <div class="settings-fieldset">
                            <label>图像高度 (Height)</label>
                            <div class="input-with-suffix" style="position:relative;">
                                <input type="number" class="cv-input" value="" style="padding-right:36px;">
                                <span style="position:absolute; right:12px; top:50%; transform:translateY(-50%); color:#94a3b8; font-size:13px;">px</span>
                            </div>
                        </div>
                    </div>
                </div>
                <div class="settings-card-body" style="border-top:1px solid #e2e8f0; display:flex; justify-content:flex-end; gap:12px; padding:16px 24px;">
                    <button class="cv-btn settings-btn-light" id="btn-hand-eye-calib">手眼标定向导</button>
                    <button class="cv-btn settings-btn-light">重置默认</button>
                    <button class="cv-btn settings-btn-danger">
                        <svg viewBox="0 0 24 24" style="width:16px; height:16px; margin-right:6px; fill:currentColor;"><path d="M17 3H5c-1.11 0-2 .9-2 2v14c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V7l-4-4zm-5 16c-1.66 0-3-1.34-3-3s1.34-3 3-3 3 1.34 3 3-1.34 3-3 3zm3-10H5V5h10v4z"/></svg>
                        保存配置
                    </button>
                </div>
            </div>
        `;
    }

    renderAiTab() {
        const aiInfo = "DeepSeek-V3";
        return `
            <div class="settings-section-title">
                <h2>AI & LLM 模型管理</h2>
                <p>集成深度学习本地模型与云端大语言模型 API 配置。</p>
            </div>

            <!-- Block 1: Model Tab & List -->
            <div class="settings-modern-card">
                <div class="settings-card-header" style="background:white; border-bottom:1px solid #e2e8f0; padding:0; display:flex;">
                    <div style="display:flex; padding-top:16px;">
                        <div style="padding:0 24px 12px; color:#94a3b8; font-weight:600; font-size:14px; cursor:pointer;">
                            <svg viewBox="0 0 24 24" style="width:16px; height:16px; vertical-align:text-bottom; margin-right:4px; fill:currentColor;"><path d="M19.14,12.94c0.04-0.3,0.06-0.61,0.06-0.94c0-0.32-0.02-0.64-0.06-0.94l2.03-1.58c0.18-0.14,0.23-0.41,0.12-0.61 l-1.92-3.32c-0.12-0.22-0.37-0.29-0.59-0.22l-2.39,0.96c-0.5-0.38-1.03-0.7-1.62-0.94L14.4,2.81c-0.04-0.24-0.24-0.41-0.48-0.41 h-3.84c-0.24,0-0.43,0.17-0.47,0.41L9.25,5.35C8.66,5.59,8.12,5.92,7.63,6.29L5.24,5.33c-0.22-0.08-0.47,0-0.59,0.22L2.73,8.87 C2.62,9.08,2.66,9.34,2.86,9.48l2.03,1.58C4.84,11.36,4.8,11.69,4.8,12s0.02,0.64,0.06,0.94l-2.03,1.58 c-0.18,0.14-0.23,0.41-0.12,0.61l1.92,3.32c0.12,0.22,0.37,0.29,0.59,0.22l2.39-0.96c0.5,0.38,1.03,0.7,1.62,0.94l0.36,2.54 c0.05,0.24,0.24,0.41,0.48,0.41h3.84c0.24,0,0.43-0.17,0.47-0.41l0.36-2.54c0.59-0.24,1.13-0.56,1.62-0.94l2.39,0.96 c0.22,0.08,0.47,0,0.59-0.22l1.92-3.32c0.12-0.22,0.07-0.49-0.12-0.61L19.14,12.94z M12,15.6c-1.98,0-3.6-1.62-3.6-3.6 s1.62-3.6,3.6-3.6s3.6,1.62,3.6,3.6S13.98,15.6,12,15.6z"/></svg> 本地模型
                        </div>
                        <div style="padding:0 24px 12px; color:var(--cinnabar); font-weight:600; font-size:14px; border-bottom:2px solid var(--cinnabar); cursor:pointer;">
                            <svg viewBox="0 0 24 24" style="width:16px; height:16px; vertical-align:text-bottom; margin-right:4px; fill:currentColor;"><path d="M19.35 10.04C18.67 6.59 15.64 4 12 4 9.11 4 6.6 5.64 5.35 8.04 2.34 8.36 0 10.91 0 14c0 3.31 2.69 6 6 6h13c2.76 0 5-2.24 5-5 0-2.64-2.05-4.78-4.65-4.96zM14 13v4h-4v-4H7l5-5 5 5h-3z"/></svg> 大语言模型 (LLM)
                        </div>
                    </div>
                    <div style="margin-left:auto; padding:12px 24px;">
                        <button class="cv-btn settings-btn-light" style="height:32px;" id="btn-add-llm">+ 添加 LLM</button>
                    </div>
                </div>
                <div class="settings-card-table-wrapper">
                    <table class="settings-modern-table" id="ai-models-table">
                        <thead>
                            <tr>
                                <th>名称</th>
                                <th>协议</th>
                                <th>模型标识</th>
                                <th>状态 (Active)</th>
                                <th>操作</th>
                            </tr>
                        </thead>
                        <tbody>
                            <!-- Javascript Dynamically Inserted Here -->
                        </tbody>
                    </table>
                </div>
            </div>

            <!-- Block 2: 详情配置 -->
            <div style="display:flex; gap:24px;">
                <div class="settings-modern-card" style="flex:2;">
                    <div class="settings-card-header" style="background:#ffffff;">
                        <div class="settings-header-left">
                            <svg viewBox="0 0 24 24" class="settings-header-icon"><path d="M19.35 10.04C18.67 6.59 15.64 4 12 4 9.11 4 6.6 5.64 5.35 8.04 2.34 8.36 0 10.91 0 14c0 3.31 2.69 6 6 6h13c2.76 0 5-2.24 5-5 0-2.64-2.05-4.78-4.65-4.96zM14 13v4h-4v-4H7l5-5 5 5h-3z" fill="var(--text-muted)"/></svg>
                            <span>配置所选模型 (Editing)</span>
                        </div>
                    </div>
                    <div class="settings-card-body" id="ai-detail-form">
                        <!-- Loaded by JS -->
                    </div>
                </div>

                <div class="settings-modern-card" style="flex:1; border:2px solid #a7f3d0; box-shadow:0 10px 25px -5px rgba(16, 185, 129, 0.1);">
                    <div class="settings-card-body" style="text-align:center; padding:32px 24px;">
                        <h3 style="font-size:15px; color:#475569; margin:0 0 24px;">API 性能概览</h3>
                        
                        <div style="position:relative; width:140px; height:140px; margin:0 auto 24px;">
                            <!-- Fake Donut Chart SVG -->
                            <svg viewBox="0 0 36 36" style="width:100%; height:100%;">
                                <path d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831" fill="none" stroke="#f1f5f9" stroke-width="4"></path>
                                <path d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831" fill="none" stroke="#10b981" stroke-width="4" stroke-dasharray="80, 100" stroke-dashoffset="0" style="transition: stroke-dasharray 1s ease-out;"></path>
                            </svg>
                            <div style="position:absolute; top:50%; left:50%; transform:translate(-50%, -50%);">
                                <div style="font-size:32px; font-weight:700; color:#0f172a; line-height:1;">450</div>
                                <div style="font-size:12px; color:#94a3b8; font-weight:600; text-transform:uppercase;">ms</div>
                            </div>
                        </div>

                        <div style="font-size:13px; font-weight:600; color:#10b981; margin-bottom:24px;">● 网络延迟 (Latency)</div>

                        <div style="text-align:left; font-size:13px; border-top:1px dashed #e2e8f0; padding-top:16px;">
                            <div style="display:flex; justify-content:space-between; margin-bottom:8px;">
                                <span class="text-muted">Token 消耗 (Daily)</span>
                                <span class="font-bold">14.2K / 50K</span>
                            </div>
                            <div style="background:#e2e8f0; height:6px; border-radius:3px; overflow:hidden; margin-bottom:16px;">
                                <div style="background:#3b82f6; width:28%; height:100%;"></div>
                            </div>

                            <div style="display:flex; justify-content:space-between; margin-bottom:8px;">
                                <span class="text-muted">RPM (Requests/Min)</span>
                                <span class="font-bold">45 / 100</span>
                            </div>
                            <div style="background:#e2e8f0; height:6px; border-radius:3px; overflow:hidden;">
                                <div style="background:#ec4899; width:45%; height:100%;"></div>
                            </div>
                        </div>

                        <div style="background:#ecfdf5; border:1px solid #d1fae5; border-radius:8px; padding:12px; margin-top:24px; text-align:left; font-size:12px; color:#065f46; display:flex; gap:8px;">
                            <svg viewBox="0 0 24 24" style="width:16px; height:16px; fill:#10b981; flex-shrink:0;"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"/></svg>
                            DeepSeek-V3 连接稳定，当前延迟适合非实时分析任务。
                        </div>
                    </div>
                </div>
            </div>
        `;
    }

    renderUserManagementTab() {
        return `
            <div class="settings-section-title" style="display:flex; justify-content:space-between; align-items:flex-end;">
                <div>
                    <h2>用户权限管理</h2>
                    <p>管理系统操作人员账号，分配不同级别的使用权限。</p>
                </div>
                <div class="settings-actions">
                    <button class="cv-btn settings-btn-danger" id="btn-add-user">
                        <span style="font-size:16px; margin-right:4px;">+</span> 新增用户
                    </button>
                </div>
            </div>

            <div class="settings-modern-card">
                <div class="settings-card-table-wrapper">
                    <table class="settings-modern-table" id="settings-user-table">
                        <thead>
                            <tr>
                                <th>用户名 (Username)</th>
                                <th>角色 (Role)</th>
                                <th>最后登录</th>
                                <th>状态</th>
                                <th>操作</th>
                            </tr>
                        </thead>
                        <tbody>
                            <!-- Loaded by JS -->
                        </tbody>
                    </table>
                </div>
            </div>
            
            <div class="settings-modern-card" style="margin-top:24px;">
                <div class="settings-card-header">
                    <div class="settings-header-left">
                        <svg viewBox="0 0 24 24" class="settings-header-icon"><path d="M12 1L3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4zm0 10.99h7c-.53 4.12-3.28 7.79-7 8.94V12H5V6.3l7-3.11v8.8z"/></svg> 
                        <span>安全与密码策略</span>
                    </div>
                </div>
                <div class="settings-card-body" style="display:flex; gap:24px;">
                    <div class="settings-fieldset" style="flex:1;">
                        <label>密码最小长度</label>
                        <input type="number" class="cv-input" value="6">
                    </div>
                    <div class="settings-fieldset" style="flex:1;">
                        <label>会话自动超时 (分钟)</label>
                        <input type="number" class="cv-input" value="30">
                    </div>
                    <div class="settings-fieldset" style="flex:1;">
                        <label>登录失败锁定次数</label>
                        <input type="number" class="cv-input" value="5">
                    </div>
                </div>
                <div class="settings-card-body" style="border-top:1px solid #e2e8f0; padding-top:16px;">
                    <button class="cv-btn settings-btn-danger">保存安全策略</button>
                </div>
            </div>
            
            <!-- 模态框容器 -->
            <div id="user-modal-container"></div>
        `;
    }

    async refreshUserTable() {
        if (!this.isAdmin) return;
        const tbody = this.container.querySelector('#settings-user-table tbody');
        if (!tbody) return;
        
        try {
            this.users = await httpClient.get('/users');
            
            tbody.innerHTML = this.users.map(u => {
                let roleColor = '#475569';
                let roleBg = '#f1f5f9';
                let roleName = '角色未知';
                
                if (u.role === 'Admin' || u.role === 0) {
                    roleColor = '#b91c1c'; roleBg = '#fef2f2'; roleName = '系统管理员';
                } else if (u.role === 'Engineer' || u.role === 1) {
                    roleColor = '#1d4ed8'; roleBg = '#dbeafe'; roleName = '工程师';
                } else if (u.role === 'Operator' || u.role === 2) {
                    roleColor = '#475569'; roleBg = '#f1f5f9'; roleName = '操作员';
                }

                const initial = u.username.charAt(0).toUpperCase();
                // 如果最后登录时间为空，显示"从未登录"，否则格式化
                const lastLogin = u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleString() : '从未登录';
                
                const statusHtml = u.isActive 
                    ? '<span class="settings-status-badge status-connected"><span class="status-dot"></span> 正常</span>'
                    : '<span class="settings-status-badge status-disconnected" style="color:#ef4444;"><span class="status-dot" style="background:#ef4444;"></span> 已禁用</span>';
                
                return `
                    <tr>
                        <td>
                            <div style="display:flex; align-items:center; gap:12px;">
                                <div style="width:32px; height:32px; background:${roleBg}; border-radius:50%; display:flex; align-items:center; justify-content:center; color:${roleColor}; font-weight:700; font-size:14px;">${initial}</div>
                                <div class="font-bold">${u.username}</div>
                            </div>
                        </td>
                        <td><span class="type-badge" style="background:${roleBg}; color:${roleColor};">${roleName}</span></td>
                        <td class="text-muted">${lastLogin}</td>
                        <td>${statusHtml}</td>
                        <td>
                            <button class="action-icon-btn" data-action="edit" data-id="${u.id}" title="编辑用户"><svg viewBox="0 0 24 24"><path d="M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25z"/></svg></button>
                            ${u.username.toLowerCase() !== 'admin' ? `<button class="action-icon-btn" data-action="reset-pwd" data-id="${u.id}" title="重置密码"><svg viewBox="0 0 24 24"><path d="M12.65 10C11.83 7.67 9.61 6 7 6c-3.31 0-6 2.69-6 6s2.69 6 6 6c2.61 0 4.83-1.67 5.65-4h2.35l2-2 2 2 2-2 2 2V10h-6.35zM7 14c-1.1 0-2-.89-2-2s.9-2 2-2 2 .89 2 2-.9 2-2 2z"/></svg></button>` : ''}
                            ${u.username.toLowerCase() !== 'admin' ? `<button class="action-icon-btn" data-action="toggle-status" data-id="${u.id}" title="${u.isActive?'禁用':'启用'}"><svg viewBox="0 0 24 24"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z"/></svg></button>` : ''}
                            ${u.username.toLowerCase() !== 'admin' ? `<button class="action-icon-btn" data-action="delete" data-id="${u.id}" title="删除用户" style="color:var(--cinnabar);"><svg viewBox="0 0 24 24"><path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z"/></svg></button>` : ''}
                        </td>
                    </tr>
                `;
            }).join('');
        } catch (error) {
            console.error('[SettingsView] loadUsers err', error);
            tbody.innerHTML = `<tr><td colspan="5" style="text-align:center;color:var(--cinnabar);">加载失败: ${error.message}</td></tr>`;
        }
    }

    bindUserManagementEvents() {
        const tab = this.container.querySelector('[data-section="users"]');
        if (!tab) return;
        
        tab.addEventListener('click', async (e) => {
            const btn = e.target.closest('button');
            if (!btn) return;
            
            if (btn.id === 'btn-add-user') {
                this.showUserModal('create', null);
                return;
            }
            
            const action = btn.dataset.action;
            const id = btn.dataset.id;
            if (!action || !id) return;
            
            const user = this.users.find(u => u.id === id);
            if (!user) return;
            
            if (action === 'edit') {
                this.showUserModal('edit', user);
            } else if (action === 'delete') {
                if (confirm(`确定要删除用户 ${user.username} 吗？`)) {
                    try {
                        await httpClient.delete(`/users/${id}`);
                        showToast('用户已删除', 'success');
                        this.refreshUserTable();
                    } catch(err) {
                        showToast('删除失败: ' + err.message, 'error');
                    }
                }
            } else if (action === 'toggle-status') {
                try {
                    await httpClient.put(`/users/${id}`, {
                        displayName: user.displayName,
                        role: user.role,
                        isActive: !user.isActive
                    });
                    showToast(`用户已${user.isActive?'禁用':'启用'}`, 'success');
                    this.refreshUserTable();
                } catch(err) {
                    showToast('操作失败: ' + err.message, 'error');
                }
            } else if (action === 'reset-pwd') {
                const newPwd = prompt(`请输入为 ${user.username} 重置的新密码 (至少6位):`);
                if (newPwd) {
                    try {
                        await httpClient.post(`/users/${id}/reset-password`, { newPassword: newPwd });
                        showToast('密码重置成功', 'success');
                    } catch(err) {
                        showToast('密码重置失败: ' + err.message, 'error');
                    }
                }
            }
        });
    }

    showUserModal(mode, user) {
        const container = this.container.querySelector('#user-modal-container');
        if (!container) return;
        
        const title = mode === 'create' ? '新增用户' : '编辑用户';
        
        let roleVal = user ? user.role : 2; // Default to Operator (2)
        if (roleVal === 'Admin') roleVal = 0;
        else if (roleVal === 'Engineer') roleVal = 1;
        else if (roleVal === 'Operator') roleVal = 2;

        container.innerHTML = `
            <div class="cv-modal-overlay">
                <div class="cv-modal">
                    <div class="cv-modal-header">
                        <div class="cv-modal-title">${title}</div>
                        <button class="cv-modal-close" id="btn-close-usermodal">×</button>
                    </div>
                    <div class="cv-modal-body">
                        <div class="settings-fieldset" style="margin-bottom:16px;">
                            <label>用户名 (登录账号)</label>
                            <input type="text" class="cv-input" id="modal-user-username" value="${user ? user.username : ''}" ${mode === 'edit' ? 'disabled' : ''}>
                        </div>
                        ${mode === 'create' ? `
                        <div class="settings-fieldset" style="margin-bottom:16px;">
                            <label>初始密码 (至少6位)</label>
                            <input type="password" class="cv-input" id="modal-user-password">
                        </div>
                        ` : ''}
                        <div class="settings-fieldset" style="margin-bottom:16px;">
                            <label>显示名称 (可选)</label>
                            <input type="text" class="cv-input" id="modal-user-displayname" value="${user?.displayName || ''}">
                        </div>
                        <div class="settings-fieldset" style="margin-bottom:16px;">
                            <label>用户角色</label>
                            <select class="cv-input" id="modal-user-role">
                                <option value="0" ${roleVal === 0 ? 'selected' : ''}>系统管理员</option>
                                <option value="1" ${roleVal === 1 ? 'selected' : ''}>工程师</option>
                                <option value="2" ${roleVal === 2 ? 'selected' : ''}>操作员</option>
                            </select>
                        </div>
                    </div>
                    <div class="cv-modal-footer">
                        <button class="cv-btn cv-btn-secondary" id="btn-cancel-usermodal">取消</button>
                        <button class="cv-btn cv-btn-primary" id="btn-save-usermodal">保存</button>
                    </div>
                </div>
            </div>
        `;

        const overlay = container.querySelector('.cv-modal-overlay');
        const closeModal = () => container.innerHTML = '';
        
        container.querySelector('#btn-close-usermodal').addEventListener('click', closeModal);
        container.querySelector('#btn-cancel-usermodal').addEventListener('click', closeModal);
        overlay.addEventListener('click', e => { if (e.target === overlay) closeModal(); });

        container.querySelector('#btn-save-usermodal').addEventListener('click', async () => {
            const displayName = container.querySelector('#modal-user-displayname').value;
            const role = parseInt(container.querySelector('#modal-user-role').value, 10);
            
            try {
                if (mode === 'create') {
                    const username = container.querySelector('#modal-user-username').value;
                    const password = container.querySelector('#modal-user-password').value;
                    await httpClient.post('/users', { username, password, displayName, role });
                    showToast('用户创建成功', 'success');
                } else {
                    await httpClient.put(`/users/${user.id}`, {
                        displayName,
                        role,
                        isActive: user.isActive
                    });
                    showToast('用户信息已更新', 'success');
                }
                closeModal();
                this.refreshUserTable();
            } catch (err) {
                showToast('保存失败: ' + err.message, 'error');
            }
        });
    }

    // saveAiConfig 已被 _saveCurrentForm() 替代，不再需要此方法

    collectCameraBindings() {
        const section = this.container.querySelector('[data-section="cameras"]');
        if (!section) return this.cameraBindings;

        const rows = section.querySelectorAll('.camera-row');
        const updatedBindings = [];

        rows.forEach(row => {
            const id = row.dataset.id;
            const displayName = row.querySelector('.cam-display-name')?.value || 'Camera';
            const original = this.cameraBindings.find(b => b.id === id);
            
            if (original) {
                updatedBindings.push({
                    ...original,
                    displayName: displayName
                });
            }
        });

        this.cameraBindings = updatedBindings;
        return updatedBindings;
    }

    /**
     * 收集输入并调用 API
     */
    async save() {
        console.log('[SettingsView] Saving config...');
        
        // 收集表单数据
        const config = {
            general: {
                softwareTitle: document.getElementById('cfg-softwareTitle')?.value || '',
                theme: document.getElementById('cfg-theme')?.value || 'dark', // 这里由于测试版没有theme下拉，默认为dark或直接留空
                autoStart: false
            },
            communication: {
                plcIpAddress: document.getElementById('cfg-plcIpAddress')?.value || '',
                plcPort: parseInt(document.getElementById('cfg-plcPort')?.value || '502', 10),
                protocol: document.getElementById('cfg-protocol')?.value || 'ModbusTcp',
                heartbeatIntervalMs: 1000
            },
            storage: {
                imageSavePath: document.getElementById('cfg-imageSavePath')?.value || '',
                savePolicy: document.getElementById('cfg-savePolicy')?.value || 'NgOnly',
                retentionDays: 30,
                minFreeSpaceGb: 5
            },
            runtime: {
                autoRun: document.getElementById('cfg-autoRun')?.checked || false,
                stopOnConsecutiveNg: parseInt(document.getElementById('cfg-stopOnConsecutiveNg')?.value || '0', 10)
            },
            cameras: this.collectCameraBindings(),
            activeCameraId: ''
        };
        
        try {
            // 首先保存全局配置 (AppConfig)
            await httpClient.put('/settings', config);

            // 保存相机绑定
            await httpClient.put('/cameras/bindings', {
                bindings: config.cameras,
                activeCameraId: config.activeCameraId
            });

            // 保存 AI 配置（通过后端 API 保存当前编辑的模型）
            await this._saveCurrentForm();

            console.log('[SettingsView] Config saved successfully');
            showToast('所有设置已生效并保存。', 'success');
            
            // 立即应用主题
            if (config.general.theme) {
                document.documentElement.dataset.theme = config.general.theme;
            }
        } catch (error) {
            console.error('[SettingsView] Failed to save config:', error);
            showToast('保存设置通讯错误: ' + error.message, 'error');
        }
    }
}

// 暴露出初始化方法给外界（比如 app.js）
window.initializeSettingsView = function() {
    console.log('[SettingsView] Registering globally...');
    window.cvSettingsView = new SettingsView('settings-view');
    window.cvSettingsView.refresh();
};
