import httpClient from '../../core/messaging/httpClient.js';
import { createModal, closeModal, showToast, createButton } from '../../shared/components/uiComponents.js';

/**
 * 设置模态框管理器
 */
class SettingsModal {
    constructor() {
        this.config = null;
        this.aiConfig = null;
        this.modalOverlay = null;
        this.activeTab = 'general';
        this.users = [];
        this.cameraBindings = [];
        this.isAdmin = false;
    }
    
    /**
     * 打开设置模态框
     */
    async open() {
        console.log('[SettingsModal] === open() START ===');
        
        // 如果已有模态框打开，先关闭
        if (this.modalOverlay) {
            console.log('[SettingsModal] Closing existing modal');
            closeModal(this.modalOverlay);
            this.modalOverlay = null;
        }

        // 检查当前用户角色
        const currentUser = window.currentUser || JSON.parse(localStorage.getItem('cv_current_user') || '{}');
        this.isAdmin = currentUser.role === 'Admin';
        console.log('[SettingsModal] Current user:', currentUser.username, 'Role:', currentUser.role, 'IsAdmin:', this.isAdmin);
        
        // 加载配置
        try {
            console.log('[SettingsModal] Fetching config from /settings...');
            this.config = await httpClient.get('/settings');
            console.log('[SettingsModal] Config loaded:', JSON.stringify(this.config, null, 2));
            
            // 同步相机绑定
            this.cameraBindings = this.config.cameras || [];
            this.activeCameraId = this.config.activeCameraId || '';

            // 加载 AI 配置
            try {
                this.aiConfig = await httpClient.get('/ai/settings');
                console.log('[SettingsModal] AI config loaded');
            } catch (e) {
                console.warn('[SettingsModal] Failed to load AI config:', e);
                this.aiConfig = { provider: 'OpenAI', apiKey: '', model: '', baseUrl: '' };
            }
        } catch (error) {
            console.error('[SettingsModal] Failed to load config:', error);
            showToast('加载配置失败: ' + error.message, 'error');
            
            // 使用默认配置继续
            console.log('[SettingsModal] Using default config');
            this.config = this.getDefaultConfig();
        }

        // 如果是管理员，加载用户列表
        if (this.isAdmin) {
            await this.loadUsers();
        }
        
        // 创建模态框内容
        console.log('[SettingsModal] Creating modal content...');
        const content = this.createContent();
        console.log('[SettingsModal] Content created');
        
        // 创建底部按钮
        console.log('[SettingsModal] Creating footer...');
        const footer = this.createFooter();
        console.log('[SettingsModal] Footer created');
        
        console.log('[SettingsModal] Calling createModal...');
        try {
            this.modalOverlay = createModal({
                title: '⚙️ 系统设置',
                content: content,
                footer: footer,
                width: '1200px'
            });
            console.log('[SettingsModal] Modal created, overlay:', this.modalOverlay);
            
            // 立即强制设置样式，确保显示（解决CSS缓存问题）
            if (this.modalOverlay) {
                console.log('[SettingsModal] Applying forced styles...');
                this.modalOverlay.style.cssText = `
                    position: fixed !important;
                    top: 0 !important;
                    left: 0 !important;
                    width: 100% !important;
                    height: 100% !important;
                    background: rgba(13, 27, 42, 0.9) !important;
                    display: flex !important;
                    justify-content: center !important;
                    align-items: center !important;
                    z-index: 99999 !important;
                    opacity: 1 !important;
                    visibility: visible !important;
                `;
                
                // 同时添加 show 类以触发 CSS 动画
                this.modalOverlay.classList.add('show');
                
                // 确保模态框内容也正确显示
                const modal = this.modalOverlay.querySelector('.cv-modal');
                if (modal) {
                    modal.style.cssText = `
                        background: var(--glass-panel, rgba(15, 36, 53, 0.95)) !important;
                        border: 1px solid var(--glass-border, rgba(255,255,255,0.1)) !important;
                        border-radius: 16px !important;
                        width: 90% !important;
                        max-width: 600px !important;
                        max-height: 85vh !important;
                        display: flex !important;
                        flex-direction: column !important;
                        overflow: hidden !important;
                        box-shadow: 0 20px 60px rgba(0,0,0,0.5) !important;
                    `;
                }
                
                console.log('[SettingsModal] Forced styles applied');
            } else {
                console.error('[SettingsModal] Modal overlay is null!');
                showToast('创建模态框失败', 'error');
                return;
            }
        } catch (error) {
            console.error('[SettingsModal] Error creating modal:', error);
            showToast('创建设置窗口失败: ' + error.message, 'error');
            return;
        }
        
        // 绑定事件
        try {
            this.bindEvents();
            console.log('[SettingsModal] Events bound successfully');
        } catch (error) {
            console.error('[SettingsModal] Error binding events:', error);
        }
        
        console.log('[SettingsModal] === open() END - Modal should be visible ===');
    }
    
    /**
     * 加载用户列表
     */
    async loadUsers() {
        try {
            console.log('[SettingsModal] Loading users...');
            this.users = await httpClient.get('/users');
            console.log('[SettingsModal] Users loaded:', this.users.length);
        } catch (error) {
            console.error('[SettingsModal] Failed to load users:', error);
            showToast('加载用户列表失败: ' + error.message, 'error');
            this.users = [];
        }
    }

    /**
     * 获取默认配置
     */
    getDefaultConfig() {
        return {
            general: {
                softwareTitle: 'ClearVision 检测站',
                theme: 'dark',
                autoStart: false
            },
            communication: {
                plcIpAddress: '192.168.1.100',
                plcPort: 502,
                protocol: 'ModbusTcp',
                heartbeatIntervalMs: 1000
            },
            storage: {
                imageSavePath: 'D:\\VisionData\\Images',
                savePolicy: 'NgOnly',
                retentionDays: 30,
                minFreeSpaceGb: 5
            },
            runtime: {
                autoRun: false,
                stopOnConsecutiveNg: 0
            }
        };
    }
    
    /**
     * 创建内容区域
     */
    createContent() {
        console.log('[SettingsModal] createContent() called');
        const container = document.createElement('div');
        container.className = 'settings-container';
        
        // 标签页导航
        const userManagementTab = this.isAdmin ? '<button class="settings-tab" data-tab="users">用户管理</button>' : '';
        const userManagementSection = this.isAdmin ? this.renderUserManagementTab() : '';
        
        container.innerHTML = `
            <div class="settings-tabs">
                <button class="settings-tab active" data-tab="general">常规</button>
                <button class="settings-tab" data-tab="communication">通讯</button>
                <button class="settings-tab" data-tab="storage">存储</button>
                <button class="settings-tab" data-tab="runtime">运行</button>
                <button class="settings-tab" data-tab="cameras">相机管理</button>
                <button class="settings-tab" data-tab="ai">AI 模型</button>
                ${userManagementTab}
            </div>
            <div class="settings-content">
                ${this.renderGeneralTab()}
                ${this.renderCommunicationTab()}
                ${this.renderStorageTab()}
                ${this.renderRuntimeTab()}
                ${this.renderCameraTab()}
                ${this.renderAiTab()}
                ${userManagementSection}
            </div>
        `;
        
        return container;
    }
    
    renderGeneralTab() {
        const general = this.config?.general || this.getDefaultConfig().general;
        return `
            <div class="settings-section active" data-section="general">
                <div class="settings-group">
                    <div class="settings-group-title">界面设置</div>
                    <div class="settings-row">
                        <div>
                            <div class="settings-label">软件标题</div>
                            <div class="settings-hint">显示在顶部的名称</div>
                        </div>
                        <input type="text" class="settings-input" 
                               id="cfg-softwareTitle" 
                               value="${general.softwareTitle || ''}">
                    </div>
                    <div class="settings-row">
                        <div>
                            <div class="settings-label">界面主题</div>
                        </div>
                        <select class="settings-select" id="cfg-theme">
                            <option value="dark" ${general.theme === 'dark' ? 'selected' : ''}>暗色模式</option>
                            <option value="light" ${general.theme === 'light' ? 'selected' : ''}>亮色模式</option>
                        </select>
                    </div>
                </div>
            </div>
        `;
    }
    
    renderCommunicationTab() {
        const communication = this.config?.communication || this.getDefaultConfig().communication;
        return `
            <div class="settings-section" data-section="communication">
                <div class="settings-group">
                    <div class="settings-group-title">PLC 连接</div>
                    <div class="settings-row">
                        <div class="settings-label">IP 地址</div>
                        <input type="text" class="settings-input" 
                               id="cfg-plcIpAddress" 
                               value="${communication.plcIpAddress || ''}">
                    </div>
                    <div class="settings-row">
                        <div class="settings-label">端口号</div>
                        <input type="number" class="settings-input" 
                               id="cfg-plcPort" 
                               value="${communication.plcPort || 502}">
                    </div>
                    <div class="settings-row">
                        <div class="settings-label">通讯协议</div>
                        <select class="settings-select" id="cfg-protocol">
                            <option value="ModbusTcp" ${communication.protocol === 'ModbusTcp' ? 'selected' : ''}>Modbus TCP</option>
                            <option value="TcpSocket" ${communication.protocol === 'TcpSocket' ? 'selected' : ''}>TCP Socket</option>
                        </select>
                    </div>
                    <div class="settings-row">
                        <div class="settings-label">心跳间隔 (ms)</div>
                        <input type="number" class="settings-input" 
                               id="cfg-heartbeatIntervalMs" 
                               value="${communication.heartbeatIntervalMs || 1000}">
                    </div>
                </div>
            </div>
        `;
    }
    
    renderStorageTab() {
        const storage = this.config?.storage || this.getDefaultConfig().storage;
        return `
            <div class="settings-section" data-section="storage">
                <div class="settings-group">
                    <div class="settings-group-title">图片存储</div>
                    <div class="settings-row">
                        <div>
                            <div class="settings-label">保存路径</div>
                        </div>
                        <input type="text" class="settings-input" 
                               id="cfg-imageSavePath" 
                               value="${storage.imageSavePath || ''}"
                               style="width: 300px;">
                    </div>
                    <div class="settings-row">
                        <div>
                            <div class="settings-label">保存策略</div>
                        </div>
                        <select class="settings-select" id="cfg-savePolicy">
                            <option value="All" ${storage.savePolicy === 'All' ? 'selected' : ''}>保存全部</option>
                            <option value="NgOnly" ${storage.savePolicy === 'NgOnly' ? 'selected' : ''}>仅保存 NG</option>
                            <option value="None" ${storage.savePolicy === 'None' ? 'selected' : ''}>不保存</option>
                        </select>
                    </div>
                    <div class="settings-row">
                        <div>
                            <div class="settings-label">保留天数</div>
                        </div>
                        <input type="number" class="settings-input" 
                               id="cfg-retentionDays" 
                               value="${storage.retentionDays || 30}">
                    </div>
                    <div class="settings-row">
                        <div>
                            <div class="settings-label">最小剩余空间 (GB)</div>
                        </div>
                        <input type="number" class="settings-input" 
                               id="cfg-minFreeSpaceGb" 
                               value="${storage.minFreeSpaceGb || 5}">
                    </div>
                </div>
            </div>
        `;
    }
    
    renderRuntimeTab() {
        const runtime = this.config?.runtime || this.getDefaultConfig().runtime;
        return `
            <div class="settings-section" data-section="runtime">
                <div class="settings-group">
                    <div class="settings-group-title">运行参数</div>
                    <div class="settings-row">
                        <div>
                            <div class="settings-label">自动运行</div>
                            <div class="settings-hint">软件启动后自动开始检测</div>
                        </div>
                        <label class="switch">
                            <input type="checkbox" id="cfg-autoRun" ${runtime.autoRun ? 'checked' : ''}>
                            <span class="slider"></span>
                        </label>
                    </div>
                    <div class="settings-row">
                        <div>
                            <div class="settings-label">连续 NG 停机</div>
                            <div class="settings-hint">检测到连续 N 个 NG 时暂停（0=禁用）</div>
                        </div>
                        <input type="number" class="settings-input" 
                               id="cfg-stopOnConsecutiveNg" 
                               value="${runtime.stopOnConsecutiveNg || 0}">
                    </div>
                </div>
            </div>
        `;
    }

    /**
     * 渲染 AI 模型配置标签页
     */
    renderAiTab() {
        const ai = this.aiConfig || {};
        return `
            <div class="settings-section" data-section="ai">
                <div class="settings-group">
                    <div class="settings-group-title">模型配置</div>
                    <div class="settings-row">
                        <div>
                            <div class="settings-label">AI 供应商</div>
                            <div class="settings-hint">选择 API 协议类型</div>
                        </div>
                        <select class="settings-select" id="cfg-ai-provider">
                            <option value="OpenAI" ${ai.provider === 'OpenAI' ? 'selected' : ''}>OpenAI 兼容（DeepSeek / GPT / 通义等）</option>
                            <option value="Anthropic" ${ai.provider === 'Anthropic' ? 'selected' : ''}>Anthropic（Claude）</option>
                        </select>
                    </div>
                    <div class="settings-row">
                        <div>
                            <div class="settings-label">API Key</div>
                            <div class="settings-hint">模型服务的密钥</div>
                        </div>
                        <div style="display:flex;gap:8px;align-items:center">
                            <input type="password" class="settings-input" id="cfg-ai-apikey" 
                                   value="${ai.apiKey || ''}" placeholder="sk-..." style="width:280px">
                            <button type="button" class="cv-btn cv-btn-secondary" id="btn-toggle-apikey"
                                    style="padding:8px 12px;font-size:12px;min-width:auto;white-space:nowrap">👁</button>
                        </div>
                    </div>
                    <div class="settings-row">
                        <div>
                            <div class="settings-label">模型名称</div>
                            <div class="settings-hint">如 deepseek-chat, gpt-4o, claude-sonnet-4-20250514</div>
                        </div>
                        <input type="text" class="settings-input" id="cfg-ai-model" 
                               value="${ai.model || ''}" placeholder="deepseek-chat">
                    </div>
                    <div class="settings-row">
                        <div>
                            <div class="settings-label">API 地址</div>
                            <div class="settings-hint">自定义端点（留空则使用默认地址）</div>
                        </div>
                        <input type="text" class="settings-input" id="cfg-ai-baseurl" 
                               value="${ai.baseUrl || ''}" placeholder="https://api.deepseek.com/chat/completions" style="width:340px">
                    </div>
                </div>
                <div class="settings-group">
                    <div class="settings-group-title">连接测试</div>
                    <div class="settings-row">
                        <div>
                            <div class="settings-label">验证当前配置</div>
                            <div class="settings-hint">保存配置后点击测试以验证连接是否正常</div>
                        </div>
                        <div style="display:flex;gap:8px;align-items:center">
                            <button type="button" class="cv-btn cv-btn-primary" id="btn-ai-test"
                                    style="padding:8px 16px;font-size:13px;min-width:auto">🔗 测试连接</button>
                            <span id="ai-test-result" style="font-size:13px;color:var(--ink-gray)"></span>
                        </div>
                    </div>
                </div>
            </div>
        `;
    }

    renderCameraTab() {
        return `
            <div class="settings-section" data-section="cameras">
                <div class="settings-group">
                    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px;">
                        <div class="settings-group-title" style="margin-bottom: 0;">相机绑定管理</div>
                        <button class="cv-btn cv-btn-primary" id="btn-discover-cameras">搜索在线相机</button>
                    </div>
                    
                    <div id="discovery-results" style="display: none; margin-bottom: 20px; background: rgba(0,0,0,0.2); border-radius: 8px; padding: 15px;">
                        <div style="font-size: 13px; color: var(--accent); margin-bottom: 10px; font-weight: bold;">发现可用设备 (点击行以创建绑定):</div>
                        <table class="user-table">
                            <thead>
                                <tr>
                                    <th>序列号/ID</th>
                                    <th>制造商</th>
                                    <th>型号</th>
                                    <th>接口</th>
                                    <th>操作</th>
                                </tr>
                            </thead>
                            <tbody id="discovery-tbody"></tbody>
                        </table>
                    </div>

                    <div class="user-table-container">
                        <table class="user-table" id="camera-bindings-table">
                            <thead>
                                <tr>
                                    <th>逻辑名称</th>
                                    <th>设备序列号</th>
                                    <th>制造商</th>
                                    <th>型号</th>
                                    <th>状态</th>
                                    <th>操作</th>
                                </tr>
                            </thead>
                            <tbody>
                                ${this.cameraBindings.length === 0 ? 
                                    '<tr><td colspan="6" style="text-align:center;color:var(--text-muted);padding:20px;">暂无绑定配置，请点击“搜索相机”</td></tr>' : 
                                    this.cameraBindings.map(b => `
                                        <tr class="camera-row" data-id="${b.id}">
                                            <td><input type="text" class="settings-input-small cam-display-name" value="${b.displayName}" style="width: 100px;"></td>
                                            <td><code class="cam-sn">${b.serialNumber}</code></td>
                                            <td>${b.manufacturer}</td>
                                            <td>${b.modelName || '-'}</td>
                                            <td>
                                                <span class="user-status ${b.isEnabled ? 'active' : 'inactive'}">${b.isEnabled ? '启用' : '禁用'}</span>
                                            </td>
                                            <td>
                                                <button class="user-btn delete-btn" data-action="remove-binding" data-id="${b.id}">删除</button>
                                            </td>
                                        </tr>
                                    `).join('')
                                }
                            </tbody>
                        </table>
                    </div>
                </div>
            </div>
        `;
    }

    renderUserManagementTab() {
        const roleNames = {
            'Admin': '管理员',
            'Engineer': '工程师',
            'Operator': '操作员'
        };

        const userRows = this.users.map(user => `
            <tr class="user-row" data-user-id="${user.id}">
                <td class="user-cell">${user.username}</td>
                <td class="user-cell">${user.displayName}</td>
                <td class="user-cell">
                    <span class="user-role ${user.role.toLowerCase()}">${roleNames[user.role] || user.role}</span>
                </td>
                <td class="user-cell">
                    <span class="user-status ${user.isActive ? 'active' : 'inactive'}">${user.isActive ? '启用' : '禁用'}</span>
                </td>
                <td class="user-cell">
                    <button class="user-btn edit-btn" data-action="edit" data-user-id="${user.id}">编辑</button>
                    <button class="user-btn reset-btn" data-action="reset" data-user-id="${user.id}">重置密码</button>
                    <button class="user-btn delete-btn" data-action="delete" data-user-id="${user.id}">删除</button>
                </td>
            </tr>
        `).join('');

        return `
            <div class="settings-section" data-section="users">
                <div class="settings-group">
                    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px;">
                        <div class="settings-group-title" style="margin-bottom: 0; flex: 1; margin-right: 12px;">
                            用户管理
                        </div>
                        <button class="cv-btn cv-btn-primary" id="btn-add-user" style="font-size: 13px; padding: 6px 12px;">
                            + 添加用户
                        </button>
                    </div>
                    <div class="user-table-container">
                        <table class="user-table">
                            <thead>
                                <tr>
                                    <th class="user-header">用户名</th>
                                    <th class="user-header">显示名称</th>
                                    <th class="user-header">角色</th>
                                    <th class="user-header">状态</th>
                                    <th class="user-header">操作</th>
                                </tr>
                            </thead>
                            <tbody>
                                ${userRows}
                            </tbody>
                        </table>
                    </div>
                </div>
            </div>
        `;
    }

    createFooter() {
        const footer = document.createElement('div');
        footer.style.cssText = 'display: flex; gap: 12px; justify-content: flex-end;';
        
        const cancelBtn = document.createElement('button');
        cancelBtn.className = 'cv-btn cv-btn-secondary';
        cancelBtn.textContent = '取消';
        cancelBtn.onclick = () => {
            console.log('[SettingsModal] Cancel clicked');
            closeModal(this.modalOverlay);
            this.modalOverlay = null;
        };
        
        const saveBtn = document.createElement('button');
        saveBtn.className = 'cv-btn cv-btn-primary';
        saveBtn.textContent = '保存';
        saveBtn.onclick = () => {
            console.log('[SettingsModal] Save clicked');
            this.save();
        };
        
        footer.appendChild(cancelBtn);
        footer.appendChild(saveBtn);
        return footer;
    }
    
    bindEvents() {
        if (!this.modalOverlay) {
            console.error('[SettingsModal] Cannot bind events: modalOverlay is null');
            return;
        }
        
        // 标签页切换
        const tabs = this.modalOverlay.querySelectorAll('.settings-tab');
        const sections = this.modalOverlay.querySelectorAll('.settings-section');
        
        console.log(`[SettingsModal] Binding ${tabs.length} tabs and ${sections.length} sections`);
        
        tabs.forEach(tab => {
            tab.addEventListener('click', () => {
                const targetTab = tab.dataset.tab;
                console.log(`[SettingsModal] Tab clicked: ${targetTab}`);
                
                tabs.forEach(t => t.classList.remove('active'));
                sections.forEach(s => s.classList.remove('active'));
                
                tab.classList.add('active');
                const targetSection = this.modalOverlay.querySelector(`[data-section="${targetTab}"]`);
                if (targetSection) {
                    targetSection.classList.add('active');
                }
            });
        });

        // 绑定相机管理相关事件
        this.bindCameraManagementEvents();

        // 绑定用户管理事件（仅管理员）
        if (this.isAdmin) {
            this.bindUserManagementEvents();
        }

        // 绑定 AI 设置事件
        this.bindAiSettingsEvents();
    }

    /**
     * 绑定 AI 设置相关事件
     */
    bindAiSettingsEvents() {
        // API Key 显示/隐藏切换
        const toggleBtn = this.modalOverlay?.querySelector('#btn-toggle-apikey');
        if (toggleBtn) {
            toggleBtn.addEventListener('click', () => {
                const input = this.modalOverlay.querySelector('#cfg-ai-apikey');
                if (input) {
                    input.type = input.type === 'password' ? 'text' : 'password';
                    toggleBtn.textContent = input.type === 'password' ? '👁' : '🔒';
                }
            });
        }

        // 测试连接按钮
        const testBtn = this.modalOverlay?.querySelector('#btn-ai-test');
        if (testBtn) {
            testBtn.addEventListener('click', async () => {
                const resultEl = this.modalOverlay.querySelector('#ai-test-result');
                testBtn.disabled = true;
                testBtn.textContent = '⏳ 测试中...';
                if (resultEl) resultEl.textContent = '';

                try {
                    // 先保存当前 AI 配置
                    await this.saveAiConfig();
                    // 再测试连接
                    const result = await httpClient.post('/ai/test', {});
                    if (result.success) {
                        if (resultEl) { resultEl.textContent = '✅ ' + result.message; resultEl.style.color = '#4caf50'; }
                    } else {
                        if (resultEl) { resultEl.textContent = '❌ ' + result.message; resultEl.style.color = 'var(--cinnabar)'; }
                    }
                } catch (e) {
                    if (resultEl) { resultEl.textContent = '❌ 请求失败: ' + e.message; resultEl.style.color = 'var(--cinnabar)'; }
                } finally {
                    testBtn.disabled = false;
                    testBtn.textContent = '🔗 测试连接';
                }
            });
        }
    }

    bindCameraManagementEvents() {
        const section = this.modalOverlay.querySelector('[data-section="cameras"]');
        if (!section) return;

        const discoverBtn = section.querySelector('#btn-discover-cameras');
        if (discoverBtn) {
            discoverBtn.addEventListener('click', () => this.discoverCameras());
        }

        // 删除绑定
        section.addEventListener('click', (e) => {
            if (e.target.dataset.action === 'remove-binding') {
                const id = e.target.dataset.id;
                this.cameraBindings = this.cameraBindings.filter(b => b.id !== id);
                this.refreshCameraTable();
            }
        });
    }

    async discoverCameras() {
        const tbody = this.modalOverlay.querySelector('#discovery-tbody');
        const resultsDiv = this.modalOverlay.querySelector('#discovery-results');
        if (!tbody || !resultsDiv) return;

        showToast('正在搜索在线相机...', 'info');
        resultsDiv.style.display = 'block';
        tbody.innerHTML = '<tr><td colspan="5" style="text-align:center;padding:20px;">正在枚举设备...</td></tr>';

        try {
            const devices = await httpClient.get('/cameras/discover');
            if (!devices || devices.length === 0) {
                tbody.innerHTML = '<tr><td colspan="5" style="text-align:center;padding:20px;color:var(--text-muted);">未发现任何在线相机</td></tr>';
                return;
            }

            tbody.innerHTML = devices.map(d => `
                <tr style="cursor: pointer;" class="discovery-row" data-sn="${d.cameraId}" data-man="${d.manufacturer}" data-model="${d.model}">
                    <td><code>${d.cameraId}</code></td>
                    <td>${d.manufacturer}</td>
                    <td>${d.model}</td>
                    <td>${d.connectionType}</td>
                    <td><button class="cv-btn cv-btn-primary" style="padding:4px 8px;font-size:12px;">绑定</button></td>
                </tr>
            `).join('');

            // 绑定点击事件
            tbody.querySelectorAll('tr').forEach(row => {
                row.onclick = () => {
                    const sn = row.dataset.sn;
                    const manufacturer = row.dataset.man;
                    const model = row.dataset.model;

                    // 检查是否已存在
                    if (this.cameraBindings.find(b => b.serialNumber === sn)) {
                        showToast('该相机已在绑定列表中', 'warning');
                        return;
                    }

                    const displayName = prompt('请输入该相机的逻辑名称 (如: Top_Camera):', `Cam_${this.cameraBindings.length + 1}`);
                    if (displayName) {
                        this.cameraBindings.push({
                            id: Math.random().toString(36).substr(2, 8),
                            displayName: displayName,
                            serialNumber: sn,
                            manufacturer: manufacturer,
                            modelName: model,
                            isEnabled: true
                        });
                        this.refreshCameraTable();
                        showToast(`已绑定: ${displayName}`, 'success');
                    }
                };
            });

        } catch (error) {
            showToast('搜索相机失败: ' + error.message, 'error');
            tbody.innerHTML = `<tr><td colspan="5" style="text-align:center;padding:20px;color:var(--accent);">错误: ${error.message}</td></tr>`;
        }
    }

    refreshCameraTable() {
        const tbody = this.modalOverlay.querySelector('#camera-bindings-table tbody');
        if (!tbody) return;

        if (this.cameraBindings.length === 0) {
            tbody.innerHTML = '<tr><td colspan="6" style="text-align:center;color:var(--text-muted);padding:20px;">暂无绑定配置，请点击“搜索相机”</td></tr>';
            return;
        }

        tbody.innerHTML = this.cameraBindings.map(b => `
            <tr class="camera-row" data-id="${b.id}">
                <td><input type="text" class="settings-input-small cam-display-name" value="${b.displayName}" style="width: 100px;"></td>
                <td><code class="cam-sn">${b.serialNumber}</code></td>
                <td>${b.manufacturer}</td>
                <td>${b.modelName || '-'}</td>
                <td>
                    <span class="user-status ${b.isEnabled ? 'active' : 'inactive'}">${b.isEnabled ? '启用' : '禁用'}</span>
                </td>
                <td>
                    <button class="user-btn delete-btn" data-action="remove-binding" data-id="${b.id}">删除</button>
                </td>
            </tr>
        `).join('');
    }

    /**
     * 绑定用户管理事件
     */
    bindUserManagementEvents() {
        // 添加用户按钮
        const addBtn = this.modalOverlay.querySelector('#btn-add-user');
        if (addBtn) {
            addBtn.addEventListener('click', () => this.showAddUserDialog());
        }

        // 用户操作按钮（编辑、重置密码、删除）
        const actionBtns = this.modalOverlay.querySelectorAll('.user-btn');
        actionBtns.forEach(btn => {
            btn.addEventListener('click', (e) => {
                const action = e.target.dataset.action;
                const userId = e.target.dataset.userId;
                const user = this.users.find(u => u.id === userId);
                
                if (!user) return;

                switch (action) {
                    case 'edit':
                        this.showEditUserDialog(user);
                        break;
                    case 'reset':
                        this.showResetPasswordDialog(user);
                        break;
                    case 'delete':
                        this.showDeleteUserDialog(user);
                        break;
                }
            });
        });
    }

    /**
     * 显示添加用户对话框
     */
    showAddUserDialog() {
        const content = `
            <div class="user-form">
                <div class="form-row">
                    <label>用户名</label>
                    <input type="text" id="new-username" class="settings-input" placeholder="请输入用户名">
                </div>
                <div class="form-row">
                    <label>密码</label>
                    <input type="password" id="new-password" class="settings-input" placeholder="请输入密码">
                </div>
                <div class="form-row">
                    <label>显示名称</label>
                    <input type="text" id="new-displayName" class="settings-input" placeholder="请输入显示名称">
                </div>
                <div class="form-row">
                    <label>角色</label>
                    <select id="new-role" class="settings-select">
                        <option value="0">管理员</option>
                        <option value="1">工程师</option>
                        <option value="2" selected>操作员</option>
                    </select>
                </div>
            </div>
        `;

        // 创建按钮
        const cancelBtn = createButton({
            text: '取消',
            type: 'secondary',
            onClick: () => closeModal(overlay)
        });

        const saveBtn = createButton({
            text: '保存',
            type: 'primary',
            onClick: async () => {
                const username = overlay.querySelector('#new-username').value.trim();
                const password = overlay.querySelector('#new-password').value;
                const displayName = overlay.querySelector('#new-displayName').value.trim();
                const role = parseInt(overlay.querySelector('#new-role').value);

                if (!username || !password) {
                    showToast('用户名和密码不能为空', 'error');
                    return;
                }

                try {
                    await httpClient.post('/users', {
                        username,
                        password,
                        displayName: displayName || username,
                        role
                    });
                    showToast('用户创建成功', 'success');
                    closeModal(overlay);
                    // 刷新用户列表
                    await this.loadUsers();
                    this.refreshUserTable();
                } catch (error) {
                    showToast('创建失败: ' + error.message, 'error');
                }
            }
        });

        const overlay = createModal({
            title: '添加用户',
            content: content,
            width: '400px',
            footer: [cancelBtn, saveBtn]
        });
    }

    /**
     * 显示编辑用户对话框
     */
    showEditUserDialog(user) {
        const content = `
            <div class="user-form">
                <div class="form-row">
                    <label>用户名</label>
                    <input type="text" class="settings-input" value="${user.username}" disabled>
                </div>
                <div class="form-row">
                    <label>显示名称</label>
                    <input type="text" id="edit-displayName" class="settings-input" value="${user.displayName}">
                </div>
                <div class="form-row">
                    <label>角色</label>
                    <select id="edit-role" class="settings-select">
                        <option value="0" ${user.role === 'Admin' ? 'selected' : ''}>管理员</option>
                        <option value="1" ${user.role === 'Engineer' ? 'selected' : ''}>工程师</option>
                        <option value="2" ${user.role === 'Operator' ? 'selected' : ''}>操作员</option>
                    </select>
                </div>
                <div class="form-row">
                    <label>状态</label>
                    <select id="edit-active" class="settings-select">
                        <option value="true" ${user.isActive ? 'selected' : ''}>启用</option>
                        <option value="false" ${!user.isActive ? 'selected' : ''}>禁用</option>
                    </select>
                </div>
            </div>
        `;

        // 创建按钮
        const cancelBtn = createButton({
            text: '取消',
            type: 'secondary',
            onClick: () => closeModal(overlay)
        });

        const saveBtn = createButton({
            text: '保存',
            type: 'primary',
            onClick: async () => {
                const displayName = overlay.querySelector('#edit-displayName').value.trim();
                const role = parseInt(overlay.querySelector('#edit-role').value);
                const isActive = overlay.querySelector('#edit-active').value === 'true';

                try {
                    await httpClient.put('/users/' + user.id, {
                        displayName: displayName || user.username,
                        role,
                        isActive
                    });
                    showToast('用户更新成功', 'success');
                    closeModal(overlay);
                    await this.loadUsers();
                    this.refreshUserTable();
                } catch (error) {
                    showToast('更新失败: ' + error.message, 'error');
                }
            }
        });

        const overlay = createModal({
            title: '编辑用户 - ' + user.username,
            content: content,
            width: '400px',
            footer: [cancelBtn, saveBtn]
        });
    }

    /**
     * 显示重置密码对话框
     */
    showResetPasswordDialog(user) {
        const content = `
            <div class="user-form">
                <p>正在重置用户 <strong>${user.username}</strong> 的密码</p>
                <div class="form-row">
                    <label>新密码</label>
                    <input type="password" id="reset-password" class="settings-input" placeholder="请输入新密码">
                </div>
            </div>
        `;

        const dialog = createModal({
            title: '重置密码',
            content: content,
            width: '400px'
        });

        const footer = dialog.querySelector('.cv-modal-footer');
        if (footer) {
            const saveBtn = document.createElement('button');
            saveBtn.className = 'cv-btn cv-btn-primary';
            saveBtn.textContent = '确认重置';
            saveBtn.onclick = async () => {
                const newPassword = dialog.querySelector('#reset-password').value;

                if (!newPassword || newPassword.length < 6) {
                    showToast('密码长度至少为6位', 'error');
                    return;
                }

                try {
                    await httpClient.post('/users/' + user.id + '/reset-password', {
                        newPassword
                    });
                    showToast('密码重置成功', 'success');
                    closeModal(dialog);
                } catch (error) {
                    showToast('重置失败: ' + error.message, 'error');
                }
            };
            footer.appendChild(saveBtn);
        }
    }

    /**
     * 显示删除用户对话框
     */
    showDeleteUserDialog(user) {
        const currentUser = window.currentUser || JSON.parse(localStorage.getItem('cv_current_user') || '{}');
        
        if (user.id === currentUser.id) {
            showToast('不能删除当前登录的用户', 'error');
            return;
        }

        if (confirm(`确定要删除用户 "${user.username}" 吗？此操作不可恢复。`)) {
            httpClient.delete('/users/' + user.id)
                .then(() => {
                    showToast('用户删除成功', 'success');
                    this.loadUsers().then(() => this.refreshUserTable());
                })
                .catch(error => {
                    showToast('删除失败: ' + error.message, 'error');
                });
        }
    }

    /**
     * 刷新用户表格
     */
    refreshUserTable() {
        const userSection = this.modalOverlay.querySelector('[data-section="users"]');
        if (userSection) {
            const newContent = this.renderUserManagementTab();
            const tempDiv = document.createElement('div');
            tempDiv.innerHTML = newContent;
            const newSection = tempDiv.firstElementChild;
            userSection.innerHTML = newSection.innerHTML;
            this.bindUserManagementEvents();
        }
    }
    
    /**
     * 保存 AI 配置（独立保存，不影响AppConfig）
     */
    async saveAiConfig() {
        const aiPayload = {
            provider: document.getElementById('cfg-ai-provider')?.value || 'OpenAI',
            apiKey: document.getElementById('cfg-ai-apikey')?.value || '',
            model: document.getElementById('cfg-ai-model')?.value || '',
            baseUrl: document.getElementById('cfg-ai-baseurl')?.value || ''
        };
        await httpClient.put('/ai/settings', aiPayload);
        console.log('[SettingsModal] AI config saved');
    }

    /**
     * 保存配置
     */
    async save() {
        console.log('[SettingsModal] Saving config...');
        
        // 收集表单数据
        const config = {
            general: {
                softwareTitle: document.getElementById('cfg-softwareTitle')?.value || '',
                theme: document.getElementById('cfg-theme')?.value || 'dark',
                autoStart: false
            },
            communication: {
                plcIpAddress: document.getElementById('cfg-plcIpAddress')?.value || '',
                plcPort: parseInt(document.getElementById('cfg-plcPort')?.value || '502', 10),
                protocol: document.getElementById('cfg-protocol')?.value || 'ModbusTcp',
                heartbeatIntervalMs: parseInt(document.getElementById('cfg-heartbeatIntervalMs')?.value || '1000', 10)
            },
            storage: {
                imageSavePath: document.getElementById('cfg-imageSavePath')?.value || '',
                savePolicy: document.getElementById('cfg-savePolicy')?.value || 'NgOnly',
                retentionDays: parseInt(document.getElementById('cfg-retentionDays')?.value || '30', 10),
                minFreeSpaceGb: parseInt(document.getElementById('cfg-minFreeSpaceGb')?.value || '5', 10)
            },
            runtime: {
                autoRun: document.getElementById('cfg-autoRun')?.checked || false,
                stopOnConsecutiveNg: parseInt(document.getElementById('cfg-stopOnConsecutiveNg')?.value || '0', 10)
            },
            cameras: this.collectCameraBindings(),
            activeCameraId: this.activeCameraId || ''
        };
        
        console.log('[SettingsModal] Config to save:', JSON.stringify(config, null, 2));
        
        try {
            // 首先保存全局配置 (AppConfig)
            await httpClient.put('/settings', config);

            // 保存相机绑定
            await httpClient.put('/cameras/bindings', {
                bindings: config.cameras,
                activeCameraId: config.activeCameraId
            });

            // 保存 AI 配置（独立存储）
            await this.saveAiConfig();

            console.log('[SettingsModal] Config saved successfully');
            showToast('设置已保存', 'success');
            closeModal(this.modalOverlay);
            this.modalOverlay = null;
            
            // 立即应用主题
            if (config.general.theme) {
                document.documentElement.dataset.theme = config.general.theme;
            }
        } catch (error) {
            console.error('[SettingsModal] Failed to save config:', error);
            showToast('保存失败: ' + error.message, 'error');
        }
    }

    collectCameraBindings() {
        const section = this.modalOverlay.querySelector('[data-section="cameras"]');
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
}

// 导出单例
console.log('[SettingsModal] Module loaded, creating singleton');
const settingsModal = new SettingsModal();
console.log('[SettingsModal] Singleton created:', settingsModal);
export default settingsModal;
