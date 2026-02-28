import webMessageBridge from '../../core/messaging/webMessageBridge.js';
import httpClient from '../../core/messaging/httpClient.js';
import { createSignal } from '../../core/state/store.js';

/**
 * AI 智能助手面板
 * 负责管理 AI 交互界面、发送生成请求、显示思考链和结果
 */
export class AiPanel {
    constructor(containerId, flowCanvas) {
        this.containerId = containerId;
        this.flowCanvas = flowCanvas;
        this.container = document.getElementById(containerId);
        this.sessionStorageKey = 'cv_ai_session_id';
        
        // 状态
        this.isGenerating = false;
        this.history = []; // { sessionId, lastMessage, updatedAtUtc, turnCount }
        this.filteredHistory = [];
        this.historyKeyword = '';
        this.isHistoryPanelOpen = false;
        this.currentThinkingStep = null;
        this.sessionId = this._loadSessionId();
        this.currentResult = null;
        this.lastUserPrompt = '';
        this.attachments = [];
        this._streamBuffer = { thinking: '', content: '' };
        this._streamFlushPending = false;
        
        // 绑定方法
        this._handleGenerate = this._handleGenerate.bind(this);
        this._handleApplyFlow = this._handleApplyFlow.bind(this);
        this._handleNewConversation = this._handleNewConversation.bind(this);
        this._handleAttachmentClick = this._handleAttachmentClick.bind(this);
        this._handleFilePickedEvent = this._handleFilePickedEvent.bind(this);
        this._handleAttachmentReport = this._handleAttachmentReport.bind(this);
        this._toggleHistoryPanel = this._toggleHistoryPanel.bind(this);
        
        // 初始化
        this._init();
    }

    _init() {
        if (!this.container) {
            console.error('[AiPanel] 容器未找到:', this.containerId);
            return;
        }
        
        this.render();
        this._setupMessageListeners();
        this._loadHistory();
    }
    
    activate() {
        this._checkConnection();
        const textarea = this.container.querySelector('.ai-textarea');
        if (textarea) textarea.focus();
    }
    
    _handleNewConversation() {
        this.sessionId = null;
        this._saveSessionId(null);
        this.currentResult = null;
        this.lastUserPrompt = '';
        this.attachments = [];
        this._clearResultPane();
        this._renderAttachments();
        const container = this.container.querySelector('#ai-chat-container');
        if (container) container.innerHTML = '';
        const progress = this.container.querySelector('#ai-progress-container');
        if (progress) progress.innerHTML = '<div class="ai-empty-state" style="text-align:center;color:#999;font-size:14px;margin-top:40px;">等待输入指令...</div>';
        this._addMessage('ai', '您好！我是您的视觉工程助手。已开始新对话。');
    }

    render() {
        this.container.innerHTML = `
            <div class="ai-workspace">
                <aside class="ai-pane-left">
                    <div class="ai-pane-header">
                        <span class="pane-icon">
                            <svg viewBox="0 0 24 24" width="14" height="14" fill="currentColor"><path d="M20 2H4c-1.1 0-2 .9-2 2v18l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2z"/></svg>
                        </span>
                        <span class="pane-title">CO-PILOT 对话</span>
                        <span class="status-badge online" id="ai-conn-status"><span class="status-dot connected"></span>在线</span>
                        <button class="icon-btn" id="ai-btn-new-session" title="新建对话">新对话</button>
                        <button class="icon-btn ai-btn-history" id="ai-btn-history" title="历史会话" aria-expanded="false">历史</button>
                    </div>

                    <div class="ai-history-panel" id="ai-history-panel">
                        <div class="ai-history-panel-inner">
                            <input
                                type="text"
                                class="ai-history-search"
                                id="ai-history-search"
                                placeholder="搜索历史会话..."
                            />
                            <div class="ai-history-list" id="ai-history-list"></div>
                        </div>
                    </div>
                    
                    <div class="ai-chat-container" id="ai-chat-container">
                        <div class="ai-message ai">
                            <div class="ai-bubble">您好！我是您的视觉工程助手。请描述您想要检测的缺陷，我将为您构建流水线。</div>
                        </div>
                    </div>
                    
                    <div class="ai-input-section">
                        <div class="ai-input-box">
                            <button class="icon-btn" id="ai-btn-attach" title="附件">
                                <svg viewBox="0 0 24 24" width="18" height="18" fill="currentColor"><path d="M16.5 6v11.5c0 2.21-1.79 4-4 4s-4-1.79-4-4V5a2.5 2.5 0 015 0v10.5c0 .55-.45 1-1 1s-1-.45-1-1V6H10v9.5a2.5 2.5 0 005 0V5c0-1.38-1.12-2.5-2.5-2.5S8 3.62 8 5v11.5c0 3.04 2.46 5.5 5.5 5.5s5.5-2.46 5.5-5.5V6h-1.5z"/></svg>
                            </button>
                            <textarea class="ai-textarea" id="ai-input" placeholder="输入指令..."></textarea>
                            <button class="ai-btn-send" id="ai-btn-gen">
                                <svg viewBox="0 0 24 24" width="18" height="18" fill="white"><path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z"/></svg>
                            </button>
                        </div>
                        <div class="ai-attachments" id="ai-attachments"></div>
                        <div class="ai-quick-examples">
                            <div class="examples-header" id="examples-toggle">
                                快捷示例 
                                <svg viewBox="0 0 24 24" width="14" height="14" fill="currentColor" style="vertical-align:middle;"><path d="M7 10l5 5 5-5z"/></svg>
                            </div>
                            <div class="ai-example-tags">
                                <span class="ai-tag" data-text="读取产品上的DataMatrix二维码。">条码读取</span>
                                <span class="ai-tag" data-text="检测金属零件表面的划痕缺陷。先进行高斯滤波去噪，然后使用Canny边缘检测，最后通过Blob分析计算划痕面积。">缺陷检测</span>
                                <span class="ai-tag" data-text="测量两个圆形孔位的圆心距离。">孔距测量</span>
                            </div>
                        </div>
                    </div>
                </aside>
                
                <section class="ai-pane-center">
                    <div class="ai-pane-header">
                        <span class="pane-icon ai-badge">AI</span>
                        <span class="pane-title">工作流生成进度</span>
                    </div>
                    <div class="ai-progress-container" id="ai-progress-container">
                        <div class="ai-empty-state">等待输入指令...</div>
                    </div>
                </section>
                
                <aside class="ai-pane-reasoning" id="ai-reasoning-pane">
                    <div class="reasoning-card design-idea">
                        <div class="card-title">
                            <svg viewBox="0 0 24 24" width="14" height="14" fill="currentColor" style="margin-right:6px;"><path d="M9 21c0 .55.45 1 1 1h4c.55 0 1-.45 1-1v-1H9v1zm3-19C8.14 2 5 5.14 5 9c0 2.38 1.19 4.47 3 5.74V17c0 .55.45 1 1 1h6c.55 0 1-.45 1-1v-2.26c1.81-1.27 3-3.36 3-5.74 0-3.86-3.14-7-7-7zm2.85 11.1l-.85.6V16h-4v-2.3l-.85-.6A4.997 4.997 0 017 9c0-2.76 2.24-5 5-5s5 2.24 5 5c0 1.63-.8 3.16-2.15 4.1z"/></svg>
                            设计思路
                        </div>
                        <div class="ai-explanation" id="ai-result-reasoning">--</div>
                    </div>
                    <div class="reasoning-card logic-deduction">
                        <div class="card-title">
                            <svg viewBox="0 0 24 24" width="14" height="14" fill="currentColor" style="margin-right:6px;"><path d="M9.4 16.6L4.8 12l4.6-4.6L8 6l-6 6 6 6 1.4-1.4zm5.2 0l4.6-4.6-4.6-4.6L16 6l6 6-6 6-1.4-1.4z"/></svg>
                            逻辑推演
                        </div>
                        <div class="ai-explanation logic-json" id="ai-result-thinking"></div>
                    </div>
                </aside>

                <aside class="ai-pane-right" id="ai-result-pane">
                    <div class="result-card overview">
                        <div class="card-title">方案概览</div>
                        <div class="ai-explanation" id="ai-result-summary">--</div>
                    </div>
                    
                    <div class="result-card ops-list">
                        <div class="card-title">生成的算子清单</div>
                        <div class="generated-ops-list" id="ai-result-ops"></div>
                    </div>
                    
                    <div class="apply-container">
                        <button class="btn-apply-flow" id="ai-btn-apply">
                            <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor" style="margin-right:6px;">
                                <path d="M9 16.2L4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4L9 16.2z"/>
                            </svg>
                            应用到环境
                        </button>
                    </div>
                </aside>
            </div>
        `;
        
        // 事件绑定
        const attachBtn = this.container.querySelector('#ai-btn-attach');
        this.container.querySelector('#ai-btn-gen').addEventListener('click', this._handleGenerate);
        this.container.querySelector('#ai-btn-apply').addEventListener('click', this._handleApplyFlow);
        if (attachBtn) attachBtn.addEventListener('click', this._handleAttachmentClick);
        const newSessionBtn = this.container.querySelector('#ai-btn-new-session');
        if (newSessionBtn) newSessionBtn.addEventListener('click', this._handleNewConversation);
        const historyBtn = this.container.querySelector('#ai-btn-history');
        if (historyBtn) historyBtn.addEventListener('click', this._toggleHistoryPanel);
        const historySearch = this.container.querySelector('#ai-history-search');
        if (historySearch) {
            historySearch.addEventListener('input', (event) => {
                this._filterHistory(event.target.value);
            });
        }
        
        this.container.querySelectorAll('.ai-tag').forEach(tag => {
            tag.addEventListener('click', () => {
                const text = tag.dataset.text;
                const input = this.container.querySelector('#ai-input');
                input.value = text;
                input.focus();
                // 触发自动扩展
                input.style.height = 'auto';
                input.style.height = (input.scrollHeight) + 'px';
            });
        });
        
        const aiInput = this.container.querySelector('#ai-input');
        aiInput.addEventListener('keydown', (e) => {
            if (e.ctrlKey && e.key === 'Enter') {
                this._handleGenerate();
            }
        });
        
        // 自动扩展高度
        aiInput.addEventListener('input', () => {
            aiInput.style.height = 'auto';
            aiInput.style.height = (aiInput.scrollHeight) + 'px';
        });

        this._renderAttachments();
    }
    
    _checkConnection() {
        const indicator = this.container.querySelector('#ai-conn-status');
        const dot = indicator?.querySelector('.status-dot');
        if (!dot) return;
        
        httpClient.get('/api/health')
            .then(() => {
                dot.className = 'status-dot connected';
            })
            .catch(() => {
                dot.className = 'status-dot disconnected';
            });
    }
    
    _setupMessageListeners() {
        webMessageBridge.on('GenerateFlowProgress', (data) => this._updateProgress(data));
        webMessageBridge.on('GenerateFlowStreamChunk', (data) => this._handleStreamChunk(data));
        webMessageBridge.on('AiFirewallBlocked', (data) => this._handleFirewallBlocked(data));
        webMessageBridge.on('GenerateFlowResult', (data) => this._handleResult(data));
        webMessageBridge.on('FilePickedEvent', this._handleFilePickedEvent);
        webMessageBridge.on('GenerateFlowAttachmentReport', this._handleAttachmentReport);
        webMessageBridge.on('ListAiSessionsResult', (data) => this._handleListAiSessionsResult(data));
        webMessageBridge.on('GetAiSessionResult', (data) => this._handleGetAiSessionResult(data));
        webMessageBridge.on('DeleteAiSessionResult', (data) => this._handleDeleteAiSessionResult(data));
    }
    
    _getCurrentFlowJson() {
        if (this.flowCanvas && typeof this.flowCanvas.serialize === 'function') {
            return this.flowCanvas.serialize();
        }
        if (this.currentResult && this.currentResult.flow) {
            return this.currentResult.flow;
        }
        return null;
    }

    async _handleGenerate() {
        const input = this.container.querySelector('#ai-input');
        const description = input.value.trim();
        const attachmentPaths = this.attachments.map(item => item.path);
        this.lastUserPrompt = description;
        
        if (!description) {
            this._addMessage('system', '请输入需求描述。');
            return;
        }
        
        if (this.isGenerating) return;
        
        this._setGeneratingState(true);
        this._clearResultPane();
        
        // Reset manual UI for streaming
        const reasoningEl = this.container.querySelector('#ai-result-reasoning');
        const thinkingEl = this.container.querySelector('#ai-result-thinking');
        if (reasoningEl) reasoningEl.innerHTML = '';
        if (thinkingEl) thinkingEl.innerHTML = '';
        this._streamBuffer = { thinking: '', content: '' };
        this._streamFlushPending = false;

        if (attachmentPaths.length > 0) {
            this.attachments = this.attachments.map(item =>
                item.status === 'skipped'
                    ? item
                    : { ...item, status: 'pending', reason: '' });
            this._renderAttachments();
        }

        const userMessage = attachmentPaths.length > 0
            ? `${description}\n\n[附件] ${this.attachments.map(item => item.name).join('，')}`
            : description;
        this._addMessage('user', userMessage);
        
        const thinkingId = 'thinking-' + Date.now();
        this._addThinkingChain(thinkingId);
        
        try {
            this._updateProgress('正在连接 AI 助手...');
            const existingFlowJson = this._getCurrentFlowJson();
            
            webMessageBridge.sendMessage("GenerateFlow", { 
                payload: { 
                    description,
                    sessionId: this.sessionId,
                    existingFlowJson,
                    attachments: attachmentPaths
                } 
            });
            input.value = ''; // 清空输入框
            input.style.height = 'auto'; // 重置高度
        } catch (err) {
            this._handleError(err.message);
        }
    }
    
    _updateProgress(data) {
        // Clear streaming placeholder when real text is streaming
        if (data === "收到 AI 响应，正在解析 JSON 数据...") {
             return;
        }

        const msg = typeof data === 'string' ? data : (data.payload?.message || data.message);
        const phase = typeof data === 'string' ? '' : (data.payload?.phase || data.phase || '');
        const container = this.container.querySelector('#ai-progress-stepper');
        if (msg && container) {
            const step = document.createElement('div');
            step.className = 'stepper-item active';
            step.innerHTML = `
                <div class="stepper-icon">
                    <svg class="check-icon" viewBox="0 0 24 24" width="14" height="14" fill="white" style="display:none;"><path d="M9 16.2L4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4L9 16.2z"/></svg>
                    <div class="dot-icon"></div>
                </div>
                <div class="stepper-content">
                    <div class="stepper-title">${msg}</div>
                    <div class="stepper-bar-container"><div class="stepper-bar-progress"></div></div>
                </div>
            `;
            const prevStep = container.querySelector('.stepper-item.active');
            if (prevStep) {
                prevStep.classList.remove('active');
                prevStep.classList.add('completed');
                prevStep.querySelector('.check-icon').style.display = 'block';
                prevStep.querySelector('.dot-icon').style.display = 'none';
                const bar = prevStep.querySelector('.stepper-bar-container');
                if(bar) bar.style.display = 'none';
            }
            container.appendChild(step);
            const progressWrapper = this.container.querySelector('#ai-progress-container');
            if(progressWrapper) progressWrapper.scrollTop = progressWrapper.scrollHeight;
            
            // 流式提示：根据phase在右侧面板显示动态占位
            this._showPhaseHint(msg, phase);
        }
    }
    
    _showPhaseHint(msg, phase) {
        const reasoning = this.container.querySelector('#ai-result-reasoning');
        const thinking = this.container.querySelector('#ai-result-thinking');
        const summary = this.container.querySelector('#ai-result-summary');
        
        // 显示动态流式提示 (Only if not already populated with real text)
        const shimmerHtml = `<span class="streaming-hint"><span class="shimmer-text">${msg}</span></span>`;
        
        if (phase === 'connecting' || msg.includes('连接')) {
            if(reasoning && !reasoning.textContent) reasoning.innerHTML = shimmerHtml;
            if(summary && !summary.textContent) summary.innerHTML = shimmerHtml;
        } else if (msg.includes('分析') || msg.includes('提示词') || msg.includes('构建')) {
            if(reasoning && !reasoning.textContent) reasoning.innerHTML = `<span class="streaming-hint"><span class="shimmer-text">正在设计方案思路...</span></span>`;
            if(thinking && !thinking.textContent) thinking.innerHTML = `<span class="streaming-hint"><span class="shimmer-text">正在组织逻辑推演...</span></span>`;
        } else if (msg.includes('生成') || msg.includes('模型')) {
            if(summary && !summary.textContent) summary.innerHTML = `<span class="streaming-hint"><span class="shimmer-text">方案生成中...</span></span>`;
            // Keep reasoning shimmer if text hasn't streamed yet
            if(thinking && !thinking.textContent) thinking.innerHTML = `<span class="streaming-hint"><span class="shimmer-text">正在组织逻辑推演...</span></span>`;
        }
    }
    
    _handleStreamChunk(data) {
        const payload = data.payload || data;
        const chunkType = payload.chunkType; // 'thinking' or 'content'
        const content = payload.content || '';
        
        if (!content) return;

        if (chunkType === 'thinking') {
            this._streamBuffer.thinking += content;
        } else if (chunkType === 'content') {
            this._streamBuffer.content += content;
        } else {
            return;
        }

        if (!this._streamFlushPending) {
            this._streamFlushPending = true;
            requestAnimationFrame(() => this._flushStreamBuffer());
        }
    }

    _flushStreamBuffer() {
        this._streamFlushPending = false;
        const thinkingText = this._streamBuffer?.thinking || '';
        const reasoningText = this._streamBuffer?.content || '';

        this._streamBuffer.thinking = '';
        this._streamBuffer.content = '';

        if (thinkingText) {
            const thinkingEl = this.container.querySelector('#ai-result-thinking');
            this._appendStreamText(thinkingEl, thinkingText);
        }
        if (reasoningText) {
            const reasoningEl = this.container.querySelector('#ai-result-reasoning');
            this._appendStreamText(reasoningEl, reasoningText);
        }

        if ((this._streamBuffer.thinking || this._streamBuffer.content) && !this._streamFlushPending) {
            this._streamFlushPending = true;
            requestAnimationFrame(() => this._flushStreamBuffer());
        }
    }

    _appendStreamText(targetEl, text) {
        if (!targetEl || !text) return;

        const shouldFollowBottom = this._isNearBottom(targetEl);
        if (targetEl.querySelector('.streaming-hint')) {
            targetEl.innerHTML = '';
        }
        targetEl.textContent += text;
        if (shouldFollowBottom) {
            targetEl.scrollTop = targetEl.scrollHeight;
        }
    }

    _isNearBottom(targetEl, threshold = 24) {
        if (!targetEl) return false;
        return (targetEl.scrollHeight - targetEl.scrollTop - targetEl.clientHeight) <= threshold;
    }
    
    _normalizeIntent(intent) {
        if (!intent) return 'UNKNOWN';
        return intent.toUpperCase();
    }

    _getIntentLabel(intent) {
        switch(intent) {
            case 'NEW': return '全新生成';
            case 'MODIFY': return '增量修改';
            case 'EXPLAIN': return '解释说明';
            default: return '智能回复';
        }
    }

    _handleResult(data) {
        if (this._streamFlushPending) {
            this._flushStreamBuffer();
        }

        this._setGeneratingState(false);
        const payload = data.payload || data;
        this.sessionId = payload.sessionId || this.sessionId;
        this._saveSessionId(this.sessionId);
        
        const container = this.container.querySelector('#ai-progress-stepper');
        if (container) {
            const activeStep = container.querySelector('.stepper-item.active');
            if (activeStep) {
                activeStep.classList.remove('active', 'completed', 'failed');
                activeStep.classList.add(payload.success ? 'completed' : 'failed');
                const icon = activeStep.querySelector('.check-icon');
                if (icon) {
                    if (payload.success) {
                        icon.innerHTML = '<path d="M9 16.2L4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4L9 16.2z"/>';
                    } else {
                        icon.innerHTML = '<path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"/>';
                    }
                    icon.style.display = 'block';
                }
                const dot = activeStep.querySelector('.dot-icon');
                if (dot) dot.style.display = 'none';
                activeStep.querySelector('.stepper-title').innerHTML = (payload.success ? '生成成功' : '生成失败');
                const bar = activeStep.querySelector('.stepper-bar-container');
                if(bar) bar.style.display = 'none';
            }
        }
        if (!payload.success) {
            this._addMessage('system', `❌ 生成失败: ${payload.errorMessage || '未知错误'}`);
            return;
        }
        this.currentResult = payload;
        if (this.sessionId) {
            this._addToHistory({
                sessionId: this.sessionId,
                lastMessage: this.lastUserPrompt || payload.aiExplanation || '已生成流程',
                updatedAtUtc: new Date().toISOString(),
                turnCount: 0
            });
        }
        this._displayResult(payload);
    }
    
    _handleFirewallBlocked(data) {
        this._setGeneratingState(false);
        const chatContainer = this.container.querySelector('#ai-chat-container');
        const alert = document.createElement('div');
        alert.className = 'firewall-alert';
        alert.innerHTML = `
            <div class="firewall-icon">🚫</div>
            <div class="firewall-content">
                <div class="firewall-title">${data.payload?.message || '网络连接被拦截'}</div>
                <div class="firewall-desc">${data.payload?.detail || '请检查防火墙设置或网络代理。'}</div>
            </div>
        `;
        chatContainer.appendChild(alert);
        this._scrollToBottom();
    }
    
    _handleError(msg) {
        this._setGeneratingState(false);
        this._addMessage('system', `❌ 系统错误: ${msg}`);
    }
    
    _displayResult(data) {
        // Stream chunk UI handles the text printing in real-time,
        // but if there wasn't a stream (fallback), we ensure it sits here
        const reasoningEl = this.container.querySelector('#ai-result-reasoning');
        if (reasoningEl && !reasoningEl.textContent.trim()) {
            this._typewriterEffect(reasoningEl, data.aiExplanation || '暂无详细思路。');
        }

        const thinkingEl = this.container.querySelector('#ai-result-thinking');
        if (thinkingEl && !thinkingEl.textContent.trim()) {
            if (data.reasoning && data.reasoning.trim()) {
                this._typewriterEffect(thinkingEl, data.reasoning, 8);
            } else {
                thinkingEl.textContent = '';
            }
        }

        const ops = data.flow?.operators || [];
        const connections = data.flow?.connections || [];
        
        // 方案概览数字动画
        this.container.querySelector('#ai-result-summary').innerHTML = 
            `该方案包含 <span class="result-count">${ops.length}</span> 个算子和 <span class="result-count">${connections.length}</span> 条连线。`;
            
        // 算子列表逐个淡入
        const opsContainer = this.container.querySelector('#ai-result-ops');
        opsContainer.innerHTML = '';
        ops.forEach((op, i) => {
            const item = document.createElement('div');
            item.className = 'generated-op-item';
            item.style.opacity = '0';
            item.style.transform = 'translateX(12px)';
            item.innerHTML = `
                <div class="op-dot"></div>
                <div class="op-name">${op.name}</div>
            `;
            opsContainer.appendChild(item);
            setTimeout(() => {
                item.style.transition = 'all 0.3s var(--ease-ink-smooth)';
                item.style.opacity = '1';
                item.style.transform = 'translateX(0)';
            }, 80 * i);
        });
        
        // 添加结果成功消息到聊天
        this._addMessage('ai', `工程方案已生成！包含 ${ops.length} 个算子、${connections.length} 条连线。可继续输入修改指令。`);
    }
    
    /**
     * 打字机效果：每次追加 chunkSize 个字符
     */
    _typewriterEffect(el, text, chunkSize = 3) {
        if (!el) return;
        el.textContent = '';
        let idx = 0;
        const write = () => {
            if (idx < text.length) {
                el.textContent += text.slice(idx, idx + chunkSize);
                idx += chunkSize;
                requestAnimationFrame(write);
            }
        };
        write();
    }
    
    _handleApplyFlow() {
        if (!this.currentResult || !this.flowCanvas) return;
        try {
            const flowBtn = document.querySelector('.nav-btn[data-view="flow"]');
            if (flowBtn) flowBtn.click();
            this.flowCanvas.deserialize(this.currentResult.flow);
            if (window.showToast) window.showToast('方案已应用到画布', 'success');
        } catch (err) {
            console.error('应用流程失败:', err);
            alert('应用流程失败: ' + err.message);
        }
    }
    
    _addMessage(role, text, options = {}) {
        const container = this.container.querySelector('#ai-chat-container');
        const msg = document.createElement('div');
        msg.className = `ai-message ${role}`;
        
        const escapeHtml = (str) => {
            const div = document.createElement('div');
            div.textContent = str;
            return div.innerHTML;
        };
        const safeText = escapeHtml(text);
        
        if (role === 'ai') {
            msg.innerHTML = `<div class="ai-bubble">${safeText}</div>`;
        } else if (role === 'user') {
            msg.innerHTML = `<div class="user-bubble">${safeText}</div>`;
        } else {
            msg.innerHTML = `<div class="system-bubble">${safeText}</div>`;
        }
        
        container.appendChild(msg);
        this._scrollToBottom();
        return msg;
    }
    
    _addThinkingChain(id) {
        const container = this.container.querySelector('#ai-progress-container');
        if (container) container.innerHTML = `<div class="stepper-wrapper" id="ai-progress-stepper"></div>`;
    }
    
    _updateThinkingStep(chainId, stepId, text) {}
    
    _setGeneratingState(busy) {
        this.isGenerating = busy;
        const btn = this.container.querySelector('#ai-btn-gen');
        if(btn) {
            btn.disabled = busy;
            if(busy) {
                btn.innerHTML = `<svg viewBox="0 0 24 24" width="18" height="18" fill="white"><path d="M12 4V2A10 10 0 0 0 2 12h2a8 8 0 0 1 8-8z"><animateTransform attributeName="transform" type="rotate" from="0 12 12" to="360 12 12" dur="1s" repeatCount="indefinite"/></path></svg>`;
            } else {
                btn.innerHTML = `<svg viewBox="0 0 24 24" width="18" height="18" fill="white"><path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z"/></svg>`;
            }
        }
        const attachBtn = this.container.querySelector('#ai-btn-attach');
        if (attachBtn) attachBtn.disabled = busy;
        this.container.querySelectorAll('.ai-attachment-remove').forEach(btnEl => {
            btnEl.disabled = busy;
        });
        const input = this.container.querySelector('#ai-input');
        if(input) input.disabled = busy;
    }

    _handleAttachmentClick() {
        if (this.isGenerating) return;
        webMessageBridge.sendMessage('PickFileCommand', {
            parameterName: 'aiAttachment',
            filter: 'Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All Files|*.*'
        });
    }

    _handleFilePickedEvent(data) {
        const payload = data?.payload || data || {};
        if (payload.parameterName !== 'aiAttachment') return;
        if (payload.isCancelled || !payload.filePath) return;

        const normalizedPath = payload.filePath.trim();
        if (!normalizedPath) return;

        const exists = this.attachments.some(item =>
            item.path.toLowerCase() === normalizedPath.toLowerCase());
        if (exists) {
            this._addMessage('system', '该附件已存在，无需重复添加。');
            return;
        }

        const attachment = {
            path: normalizedPath,
            name: this._getFileName(normalizedPath),
            status: 'ready',
            reason: ''
        };
        this.attachments.push(attachment);
        this._renderAttachments();
        this._addMessage('system', `已添加附件：${attachment.name}`);
    }

    _handleAttachmentReport(data) {
        const payload = data?.payload || data || {};
        const sent = Array.isArray(payload.sent) ? payload.sent : [];
        const skipped = Array.isArray(payload.skipped) ? payload.skipped : [];

        if (sent.length === 0 && skipped.length === 0) return;

        const sentMap = new Map(sent
            .filter(item => item?.path)
            .map(item => [String(item.path).toLowerCase(), item]));
        const skippedMap = new Map(skipped
            .filter(item => item?.path)
            .map(item => [String(item.path).toLowerCase(), item]));

        this.attachments = this.attachments.map(item => {
            const key = item.path.toLowerCase();
            if (skippedMap.has(key)) {
                const skipInfo = skippedMap.get(key);
                return {
                    ...item,
                    status: 'skipped',
                    reason: this._formatSkipReason(skipInfo?.reason)
                };
            }
            if (sentMap.has(key)) {
                return {
                    ...item,
                    status: 'sent',
                    reason: ''
                };
            }
            return item;
        });

        this._renderAttachments();

        const sentNames = sent.map(item => item?.name).filter(Boolean);
        const skippedNames = skipped.map(item => {
            const name = item?.name || this._getFileName(item?.path || '');
            const reason = this._formatSkipReason(item?.reason);
            return reason ? `${name}(${reason})` : name;
        }).filter(Boolean);

        const sections = [];
        if (sentNames.length > 0) {
            sections.push(`已发送: ${sentNames.join('，')}`);
        }
        if (skippedNames.length > 0) {
            sections.push(`已跳过: ${skippedNames.join('，')}`);
        }
        if (sections.length > 0) {
            this._addMessage('system', `附件处理结果\n${sections.join('\n')}`);
        }
    }

    _removeAttachment(path) {
        this.attachments = this.attachments.filter(item => item.path !== path);
        this._renderAttachments();
    }

    _renderAttachments() {
        const container = this.container?.querySelector('#ai-attachments');
        if (!container) return;

        if (!this.attachments.length) {
            container.innerHTML = '';
            return;
        }

        const chips = this.attachments.map(item => {
            const title = item.reason ? `${item.path}\n${item.reason}` : item.path;
            const statusLabel = this._getAttachmentStatusLabel(item.status, item.reason);
            const statusClass = `status-${item.status || 'ready'}`;
            return `
                <div class="ai-attachment-chip" title="${this._escapeHtml(title)}">
                    <span class="ai-attachment-name">${this._escapeHtml(item.name)}</span>
                    <span class="ai-attachment-status ${statusClass}">${this._escapeHtml(statusLabel)}</span>
                    <button class="ai-attachment-remove" data-path="${this._escapeHtml(item.path)}" type="button" aria-label="remove attachment">×</button>
                </div>
            `;
        }).join('');

        container.innerHTML = `<div class="ai-attachment-list">${chips}</div>`;
        container.querySelectorAll('.ai-attachment-remove').forEach(btn => {
            btn.addEventListener('click', () => this._removeAttachment(btn.dataset.path || ''));
            btn.disabled = this.isGenerating;
        });
    }

    _getAttachmentStatusLabel(status, reason) {
        switch (status) {
            case 'pending': return '发送中';
            case 'sent': return '已发送';
            case 'skipped': return reason ? `已跳过(${reason})` : '已跳过';
            default: return '待发送';
        }
    }

    _formatSkipReason(reason) {
        switch (reason) {
            case 'file_missing': return '文件不存在';
            case 'unsupported_format': return '格式不支持';
            case 'file_too_large': return '文件过大';
            case 'read_failed': return '读取失败';
            case 'limit_exceeded': return '超出数量上限';
            case 'model_not_support_image': return '当前模型不支持图片';
            default: return reason || '';
        }
    }

    _getFileName(filePath) {
        const parts = String(filePath || '').split(/[/\\]/);
        return parts[parts.length - 1] || filePath;
    }

    _escapeHtml(value) {
        const div = document.createElement('div');
        div.textContent = value ?? '';
        return div.innerHTML;
    }
    
    _clearResultPane() {
        const e1 = this.container.querySelector('#ai-result-reasoning'); if(e1) e1.textContent = '';
        const e2 = this.container.querySelector('#ai-result-thinking'); if(e2) e2.textContent = '';
        const e3 = this.container.querySelector('#ai-result-summary'); if(e3) e3.textContent = '';
        const e4 = this.container.querySelector('#ai-result-ops'); if(e4) e4.innerHTML = '';
        const progress = this.container.querySelector('#ai-progress-container');
        if(progress) progress.innerHTML = '';
        this._streamBuffer = { thinking: '', content: '' };
        this._streamFlushPending = false;
    }
    
    _scrollToBottom() {
        const container = this.container.querySelector('#ai-chat-container');
        if(container) container.scrollTop = container.scrollHeight;
    }

    _toggleHistoryPanel() {
        const panel = this.container.querySelector('#ai-history-panel');
        const historyBtn = this.container.querySelector('#ai-btn-history');
        if (!panel || !historyBtn) return;

        this.isHistoryPanelOpen = !this.isHistoryPanelOpen;
        panel.classList.toggle('expanded', this.isHistoryPanelOpen);
        historyBtn.setAttribute('aria-expanded', this.isHistoryPanelOpen ? 'true' : 'false');

        if (this.isHistoryPanelOpen) {
            this._loadHistory();
            const searchInput = this.container.querySelector('#ai-history-search');
            if (searchInput) searchInput.focus();
        }
    }
    
    _addToHistory(entry) {
        const normalized = this._normalizeSessionSummary(entry);
        if (!normalized) return;

        this.history = [normalized, ...this.history.filter(item => item.sessionId !== normalized.sessionId)]
            .sort((a, b) => new Date(b.updatedAtUtc).getTime() - new Date(a.updatedAtUtc).getTime());
        this._filterHistory(this.historyKeyword);
    }

    _normalizeSessionSummary(entry) {
        const sessionId = String(entry?.sessionId ?? entry?.SessionId ?? '').trim();
        if (!sessionId) return null;

        const lastMessage = String(entry?.lastMessage ?? entry?.LastMessage ?? '').trim();
        const updatedAtUtc = String(entry?.updatedAtUtc ?? entry?.UpdatedAtUtc ?? new Date().toISOString());
        const turnCountRaw = Number(entry?.turnCount ?? entry?.TurnCount ?? 0);
        return {
            sessionId,
            lastMessage: lastMessage || '（空会话）',
            updatedAtUtc,
            turnCount: Number.isFinite(turnCountRaw) ? turnCountRaw : 0
        };
    }

    _filterHistory(keyword = '') {
        this.historyKeyword = String(keyword || '').trim().toLowerCase();
        if (!this.historyKeyword) {
            this.filteredHistory = [...this.history];
        } else {
            this.filteredHistory = this.history.filter(item => {
                const text = `${item.lastMessage} ${item.sessionId}`.toLowerCase();
                return text.includes(this.historyKeyword);
            });
        }

        this._renderHistoryList();
    }
    
    _renderHistoryList() {
        const list = this.container.querySelector('#ai-history-list');
        if (!list) return;
        const rows = this.filteredHistory.length > 0 || this.historyKeyword
            ? this.filteredHistory
            : this.history;
        if (rows.length === 0) {
            list.innerHTML = '<div class="ai-history-empty">暂无历史记录</div>';
            return;
        }
        
        list.innerHTML = rows.map(item => `
            <div class="ai-history-item ${item.sessionId === this.sessionId ? 'active' : ''}" data-session-id="${this._escapeHtml(item.sessionId)}">
                <div class="history-desc">${this._escapeHtml(item.lastMessage)}</div>
                <div class="history-meta">
                    <span>${this._escapeHtml(this._formatHistoryTime(item.updatedAtUtc))}</span>
                    <span>${this._escapeHtml(String(item.turnCount))} 轮</span>
                </div>
                <button class="ai-history-delete" type="button" data-session-id="${this._escapeHtml(item.sessionId)}" title="删除会话">删除</button>
            </div>
        `).join('');

        list.querySelectorAll('.ai-history-item').forEach(itemEl => {
            itemEl.addEventListener('click', (event) => {
                if (event.target.closest('.ai-history-delete')) return;
                const sessionId = itemEl.dataset.sessionId || '';
                this._switchToSession(sessionId);
            });
        });

        list.querySelectorAll('.ai-history-delete').forEach(btn => {
            btn.addEventListener('click', (event) => {
                event.stopPropagation();
                this._deleteSession(btn.dataset.sessionId || '');
            });
        });
    }

    _formatHistoryTime(value) {
        const timestamp = new Date(value);
        if (Number.isNaN(timestamp.getTime())) return '--';
        return timestamp.toLocaleString();
    }
    
    _loadHistory() {
        webMessageBridge.sendMessage('ListAiSessions');
    }

    _handleListAiSessionsResult(data) {
        const payload = data?.payload || data || {};
        if (!payload.success) {
            if (payload.errorMessage) {
                this._addMessage('system', `历史加载失败: ${payload.errorMessage}`);
            }
            return;
        }

        const sessions = Array.isArray(payload.sessions) ? payload.sessions : [];
        this.history = sessions
            .map(item => this._normalizeSessionSummary(item))
            .filter(Boolean)
            .sort((a, b) => new Date(b.updatedAtUtc).getTime() - new Date(a.updatedAtUtc).getTime());
        this._filterHistory(this.historyKeyword);
    }

    _switchToSession(sessionId) {
        if (!sessionId) return;
        if (this.isGenerating) {
            this._addMessage('system', '正在生成中，暂时无法切换历史会话。');
            return;
        }

        webMessageBridge.sendMessage('GetAiSession', {
            payload: { sessionId }
        });
    }

    _handleGetAiSessionResult(data) {
        const payload = data?.payload || data || {};
        if (!payload.success) {
            this._addMessage('system', `会话恢复失败: ${payload.errorMessage || '未知错误'}`);
            return;
        }

        const session = payload.session;
        if (!session) {
            this._addMessage('system', '会话恢复失败: 会话数据为空');
            return;
        }

        const sessionId = String(session.sessionId ?? session.SessionId ?? '').trim();
        if (!sessionId) {
            this._addMessage('system', '会话恢复失败: 会话 ID 无效');
            return;
        }

        this.sessionId = sessionId;
        this._saveSessionId(this.sessionId);
        this.currentResult = null;
        this._clearResultPane();

        const chatContainer = this.container.querySelector('#ai-chat-container');
        if (chatContainer) chatContainer.innerHTML = '';

        const rawHistory = Array.isArray(session.history)
            ? session.history
            : (Array.isArray(session.History) ? session.History : []);
        const normalizedHistory = rawHistory
            .map(turn => ({
                role: String(turn?.role ?? turn?.Role ?? '').trim().toLowerCase(),
                message: String(turn?.message ?? turn?.Message ?? '')
            }))
            .filter(turn => turn.message.trim().length > 0);

        if (normalizedHistory.length === 0) {
            this._addMessage('ai', '已恢复历史会话（当前没有可展示的消息）。');
        } else {
            normalizedHistory.forEach(turn => {
                const role = turn.role === 'user' ? 'user' : 'ai';
                this._addMessage(role, turn.message);
            });
        }

        const flowRaw = session.currentFlowJson ?? session.CurrentFlowJson;
        let parsedFlow = null;
        if (flowRaw) {
            try {
                parsedFlow = JSON.parse(flowRaw);
            } catch {
                parsedFlow = null;
            }
        }

        const reasoningEl = this.container.querySelector('#ai-result-reasoning');
        const thinkingEl = this.container.querySelector('#ai-result-thinking');
        const summaryEl = this.container.querySelector('#ai-result-summary');
        const opsEl = this.container.querySelector('#ai-result-ops');

        const explanation = parsedFlow?.explanation || parsedFlow?.Explanation || '--';
        if (reasoningEl) reasoningEl.textContent = explanation || '--';
        if (thinkingEl) thinkingEl.textContent = '';

        const operators = Array.isArray(parsedFlow?.operators)
            ? parsedFlow.operators
            : (Array.isArray(parsedFlow?.Operators) ? parsedFlow.Operators : []);
        const connections = Array.isArray(parsedFlow?.connections)
            ? parsedFlow.connections
            : (Array.isArray(parsedFlow?.Connections) ? parsedFlow.Connections : []);

        if (summaryEl) {
            summaryEl.textContent = `该方案包含 ${operators.length} 个算子和 ${connections.length} 条连线。`;
        }

        if (opsEl) {
            if (operators.length === 0) {
                opsEl.innerHTML = '<div class="ai-history-empty">暂无算子数据</div>';
            } else {
                opsEl.innerHTML = operators.map(op => {
                    const name = op?.displayName || op?.DisplayName || op?.name || op?.Name || '未命名算子';
                    return `
                        <div class="generated-op-item">
                            <div class="op-dot"></div>
                            <div class="op-name">${this._escapeHtml(String(name))}</div>
                        </div>
                    `;
                }).join('');
            }
        }

        const updatedAtUtc = session.updatedAtUtc ?? session.UpdatedAtUtc ?? new Date().toISOString();
        const latestMessage = normalizedHistory.length > 0
            ? normalizedHistory[normalizedHistory.length - 1].message
            : '（空会话）';
        this._addToHistory({
            sessionId,
            lastMessage: latestMessage,
            updatedAtUtc,
            turnCount: normalizedHistory.length
        });
    }

    _deleteSession(sessionId) {
        if (!sessionId) return;
        webMessageBridge.sendMessage('DeleteAiSession', {
            payload: { sessionId }
        });
    }

    _handleDeleteAiSessionResult(data) {
        const payload = data?.payload || data || {};
        if (!payload.success) {
            this._addMessage('system', `删除会话失败: ${payload.errorMessage || '未知错误'}`);
            return;
        }

        const deletedSessionId = String(payload.sessionId ?? payload.SessionId ?? '').trim();
        if (!deletedSessionId) return;

        this.history = this.history.filter(item => item.sessionId !== deletedSessionId);
        this._filterHistory(this.historyKeyword);

        if (this.sessionId === deletedSessionId) {
            this._handleNewConversation();
        }
    }

    _loadSessionId() {
        try {
            return localStorage.getItem(this.sessionStorageKey);
        } catch {
            return null;
        }
    }

    _saveSessionId(sessionId) {
        try {
            if (sessionId) {
                localStorage.setItem(this.sessionStorageKey, sessionId);
            } else {
                localStorage.removeItem(this.sessionStorageKey);
            }
        } catch {
            // ignore localStorage failures
        }
    }
}
