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
        
        // 状态
        this.isGenerating = false;
        this.history = []; // { id, desc, time, result, error }
        this.currentThinkingStep = null;
        this.sessionId = null;
        this.currentResult = null;
        
        // 绑定方法
        this._handleGenerate = this._handleGenerate.bind(this);
        this._handleApplyFlow = this._handleApplyFlow.bind(this);
        this._handleNewConversation = this._handleNewConversation.bind(this);
        
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
        this.currentResult = null;
        this._clearResultPane();
        this._addMessage('system', '已开始新对话。后续描述将按全新需求处理。');
    }

    render() {
        this.container.innerHTML = `
            <header class="ai-header">
                <div class="ai-title">
                    <svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor">
                        <path d="M12 2l2.4 7.2L22 12l-7.6 2.8L12 22l-2.4-7.2L2 12l7.6-2.8z"/>
                    </svg>
                    <span>AI 智能工程助手</span>
                </div>
                <div class="ai-header-actions">
                    <button class="ai-btn-secondary" id="ai-btn-new-session" title="清空会话上下文并开始新对话">
                        <svg viewBox="0 0 24 24" width="14" height="14" fill="currentColor" style="margin-right:4px;"><path d="M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z"/></svg>
                        新对话
                    </button>
                    <div class="ai-status-indicator" id="ai-conn-status">
                        <span class="status-dot connecting"></span>
                        <span class="status-text">连接中...</span>
                    </div>
                </div>
            </header>
            
            <div class="ai-workspace">
                <!-- 左侧：输入与历史 -->
                <aside class="ai-pane-left">
                    <div class="ai-input-section">
                        <div class="ai-section-title">检测需求描述</div>
                        <div class="ai-input-wrapper">
                            <textarea class="ai-textarea" id="ai-input" 
                                placeholder="请描述您的视觉检测需求...&#10;例如：使用500万像素相机拍摄产品表面，检测是否存在划痕和污渍，如果是NG品则自动剔除。"></textarea>
                        </div>
                        <button class="ai-btn-generate" id="ai-btn-gen">
                            <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor"><path d="M12 2l2.4 7.2L22 12l-7.6 2.8L12 22l-2.4-7.2L2 12l7.6-2.8z"/></svg>
                            生成工程方案
                        </button>
                        
                        <div class="ai-examples">
                            <div class="ai-section-title">快速示例</div>
                            <div class="ai-example-tags">
                                <span class="ai-tag" data-text="读取产品上的DataMatrix二维码，并将解码结果通过Modbus TCP协议写入PLC寄存器D100。">条码读取</span>
                                <span class="ai-tag" data-text="检测金属零件表面的划痕缺陷。先进行高斯滤波去噪，然后使用Canny边缘检测，最后通过Blob分析计算划痕面积。">缺陷检测</span>
                                <span class="ai-tag" data-text="测量两个圆形孔位的圆心距离。需要先进行相机标定，然后识别圆孔特征，计算像素距离。">孔距测量</span>
                            </div>
                        </div>
                    </div>
                    
                    <div class="ai-history-section">
                        <div class="ai-section-title" style="margin-bottom: 8px;">历史记录</div>
                        <div class="ai-history-list" id="ai-history-list">
                            <div style="padding: 20px; text-align: center; color: var(--ai-text-mute); font-size: 12px;">暂无历史记录</div>
                        </div>
                    </div>
                </aside>
                
                <!-- 中间：对话链 (缩窄宽度以容纳4列) -->
                <section class="ai-pane-center">
                    <div class="ai-thinking-container" id="ai-chat-container">
                        <div class="ai-message ai">
                            <div class="ai-avatar ai">AI</div>
                            <div class="ai-content">
                                <div class="ai-bubble">你好！我是 ClearVision 智能助手。请在左侧描述您的检测需求，我将为您自动生成或修改视觉工程方案。</div>
                            </div>
                        </div>
                    </div>
                </section>
                
                <!-- 独立列：推理过程 -->
                <aside class="ai-pane-reasoning" id="ai-reasoning-pane" style="display:none;">
                    <div class="ai-input-section" style="border-bottom: none; padding-bottom: 0;">
                        <div class="ai-section-title">
                            <svg viewBox="0 0 24 24" width="14" height="14" fill="currentColor" style="vertical-align:text-bottom; margin-right:4px;"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z"/></svg>
                            智能推理分析
                        </div>
                    </div>
                    <div class="ai-result-panel" style="flex:1; overflow:hidden; display:flex; flex-direction:column; padding-top:10px;">
                        <div class="result-card style-red-border" style="margin-bottom: 12px; flex-shrink: 0;">
                            <div class="card-title text-red">设计思路</div>
                            <div class="ai-explanation" id="ai-result-reasoning" style="max-height: 200px; overflow-y: auto;">
                                --
                            </div>
                        </div>
                        <div class="result-card" id="ai-thinking-card" style="flex: 1; display:flex; flex-direction:column; overflow:hidden;">
                            <div class="card-title">逻辑推演</div>
                            <div class="ai-explanation" id="ai-result-thinking" style="flex:1; overflow-y: auto; white-space: pre-wrap; font-size: 12px; line-height: 1.6; font-family: 'JetBrains Mono', Consolas, monospace;"></div>
                        </div>
                    </div>
                </aside>

                <!-- 右侧：方案预览与行动 -->
                <aside class="ai-pane-right" id="ai-result-pane" style="display:none;">
                    <div class="ai-input-section" style="border-bottom: none; padding-bottom: 0;">
                        <div class="ai-section-title">生成结果</div>
                    </div>
                    
                    <div class="ai-result-panel" style="padding-top:10px;">
                        <div class="result-card">
                            <div class="card-title">方案概览</div>
                            <div class="ai-explanation" id="ai-result-summary">--</div>
                        </div>
                        
                        <div class="result-card" style="flex:1; display:flex; flex-direction:column;">
                            <div class="card-title">包含算子</div>
                            <div class="generated-ops-list" id="ai-result-ops" style="flex:1; overflow-y:auto; align-content: flex-start;"></div>
                        </div>
                        
                        <div class="ai-actions">
                            <button class="btn btn-primary btn-apply-flow" id="ai-btn-apply">
                                <svg viewBox="0 0 24 24" width="18" height="18" fill="currentColor">
                                    <path d="M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z"/>
                                </svg>
                                应用到环境
                            </button>
                        </div>
                    </div>
                </aside>
            </div>
        `;
        
        // 事件绑定
        this.container.querySelector('#ai-btn-gen').addEventListener('click', this._handleGenerate);
        this.container.querySelector('#ai-btn-apply').addEventListener('click', this._handleApplyFlow);
        this.container.querySelector('#ai-btn-new-session').addEventListener('click', this._handleNewConversation);
        
        this.container.querySelectorAll('.ai-tag').forEach(tag => {
            tag.addEventListener('click', () => {
                const text = tag.dataset.text;
                const input = this.container.querySelector('#ai-input');
                input.value = text;
                input.focus();
            });
        });
        
        this.container.querySelector('#ai-input').addEventListener('keydown', (e) => {
            if (e.ctrlKey && e.key === 'Enter') {
                this._handleGenerate();
            }
        });
    }
    
    _checkConnection() {
        const indicator = this.container.querySelector('#ai-conn-status');
        const dot = indicator.querySelector('.status-dot');
        
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
        webMessageBridge.on('AiFirewallBlocked', (data) => this._handleFirewallBlocked(data));
        webMessageBridge.on('GenerateFlowResult', (data) => this._handleResult(data));
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
        
        if (!description) {
            this._addMessage('system', '请输入需求描述。');
            return;
        }
        
        if (this.isGenerating) return;
        
        this._setGeneratingState(true);
        this._clearResultPane();
        
        this._addMessage('user', description);
        
        const thinkingId = 'thinking-' + Date.now();
        this._addThinkingChain(thinkingId);
        
        try {
            this._updateThinkingStep(thinkingId, 'start', '正在连接 AI 助手...');
            const existingFlowJson = this._getCurrentFlowJson();
            
            webMessageBridge.sendMessage("GenerateFlow", { 
                payload: { 
                    description,
                    sessionId: this.sessionId,
                    existingFlowJson
                } 
            });
            
        } catch (err) {
            this._handleError(err.message);
        }
    }
    
    _updateProgress(data) {
        const msg = typeof data === 'string' ? data : (data.payload?.message || data.message);
        
        if (msg) {
            const lastChain = this.container.querySelector('.thinking-chain:last-child');
            if (lastChain) {
                const step = document.createElement('div');
                step.className = 'thinking-step active';
                step.innerHTML = `
                    <div class="step-title">${msg}</div>
                    <div class="step-content">...</div>
                `;
                
                const prevStep = lastChain.querySelector('.thinking-step.active');
                if (prevStep) {
                    prevStep.classList.remove('active');
                    prevStep.classList.add('completed');
                    const content = prevStep.querySelector('.step-content');
                    if (content.textContent === '...') content.textContent = '完成';
                }
                
                lastChain.appendChild(step);
                this._scrollToBottom();
            }
        }
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
        this._setGeneratingState(false);
        
        const payload = data.payload || data;
        const sessionId = payload.sessionId || null;
        this.sessionId = sessionId || this.sessionId;

        const detectedIntent = this._normalizeIntent(
            payload.detectedIntent ?? data.detectedIntent
        );
        
        const lastChain = this.container.querySelector('.thinking-chain:last-child');
        if (lastChain) {
            const activeStep = lastChain.querySelector('.thinking-step.active');
            if (activeStep) {
                activeStep.classList.remove('active');
                activeStep.classList.add('completed');
                activeStep.querySelector('.step-content').textContent = payload.success ? '生成成功' : '生成失败';
            }
        }
        
        if (!payload.success) {
            this._addMessage('system', `❌ 生成失败: ${payload.errorMessage || '未知错误'}`);
            return;
        }
        
        this.currentResult = payload;
        this._displayResult(payload);
        
        this._addMessage(
            'ai', 
            '工程方案已生成！请查看右侧推理过程与预览面板。您可以继续输入修改要求进行增量调整。',
            { intent: detectedIntent }
        );
        
        this._addToHistory({
            desc: this.container.querySelector('#ai-input').value.trim(),
            time: new Date(),
            success: true,
            opCount: payload.flow?.operators?.length || 0
        });
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
        const resultPane = this.container.querySelector('#ai-result-pane');
        const reasoningPane = this.container.querySelector('#ai-reasoning-pane');
        resultPane.style.display = 'flex';
        reasoningPane.style.display = 'flex';
        
        const ops = data.flow?.operators || [];
        
        this.container.querySelector('#ai-result-summary').textContent = 
            `方案包含 ${ops.length} 个算子，${data.flow?.connections?.length || 0} 条连线。`;
            
        const opsContainer = this.container.querySelector('#ai-result-ops');
        opsContainer.innerHTML = ops.map(op => `
            <span class="op-tag">
                <span class="op-tag-icon"></span>
                ${op.name}
            </span>
        `).join('');
        
        this.container.querySelector('#ai-result-reasoning').textContent = 
            data.aiExplanation || 'AI 未提供详细说明。';

        const thinkingCard = this.container.querySelector('#ai-thinking-card');
        const thinkingContent = this.container.querySelector('#ai-result-thinking');
        if (data.reasoning && data.reasoning.trim()) {
            thinkingCard.style.display = 'flex';
            thinkingContent.textContent = data.reasoning;
        } else {
            thinkingCard.style.display = 'none';
        }
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
        
        let headerHtml = '';
        if (options.intent) {
            const label = this._getIntentLabel(options.intent);
            const intentClass = options.intent.toLowerCase();
            headerHtml = `<div class="intent-tag intent-${intentClass}">${label}</div>`;
        }
        
        msg.innerHTML = `
            <div class="ai-avatar ${role}">${role === 'user' ? 'U' : 'AI'}</div>
            <div class="ai-content">
                ${headerHtml}
                <div class="ai-bubble">${safeText}</div>
            </div>
        `;
        container.appendChild(msg);
        this._scrollToBottom();
        return msg;
    }
    
    _addThinkingChain(id) {
        const container = this.container.querySelector('#ai-chat-container');
        const msg = document.createElement('div');
        msg.className = 'ai-message ai';
        msg.style.alignItems = 'flex-start';
        msg.innerHTML = `
             <div class="ai-avatar ai">AI</div>
             <div class="ai-content">
                 <div class="thinking-chain" id="${id}"></div>
             </div>
        `;
        container.appendChild(msg);
        this._scrollToBottom();
    }
    
    _updateThinkingStep(chainId, stepId, text) {} // Ignored
    
    _setGeneratingState(busy) {
        this.isGenerating = busy;
        const btn = this.container.querySelector('#ai-btn-gen');
        if(btn){
            btn.disabled = busy;
            btn.innerHTML = busy ? 
                `<span class="typing-dot"></span><span class="typing-dot"></span><span class="typing-dot"></span>` : 
                `<svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor"><path d="M12 2l2.4 7.2L22 12l-7.6 2.8L12 22l-2.4-7.2L2 12l7.6-2.8z"/></svg> 生成工程方案`;
        }
        const input = this.container.querySelector('#ai-input');
        if(input) input.disabled = busy;
    }
    
    _clearResultPane() {
        const r1 = this.container.querySelector('#ai-result-pane');
        const r2 = this.container.querySelector('#ai-reasoning-pane');
        if(r1) r1.style.display = 'none';
        if(r2) r2.style.display = 'none';
    }
    
    _scrollToBottom() {
        const container = this.container.querySelector('#ai-chat-container');
        if(container) container.scrollTop = container.scrollHeight;
    }
    
    _addToHistory(entry) {
        this.history.unshift(entry);
        this._renderHistoryList();
    }
    
    _renderHistoryList() {
        const list = this.container.querySelector('#ai-history-list');
        if (!list) return;
        if (this.history.length === 0) {
            list.innerHTML = '<div style="padding: 20px; text-align: center; color: var(--ai-text-mute); font-size: 12px;">暂无历史记录</div>';
            return;
        }
        
        list.innerHTML = this.history.map(item => `
            <div class="ai-history-item">
                <div class="history-desc">${item.desc}</div>
                <div class="history-meta">
                    <span>${item.time.toLocaleTimeString()}</span>
                    <span>${item.opCount} 算子</span>
                </div>
            </div>
        `).join('');
    }
    
    _loadHistory() {
        this._renderHistoryList();
    }
}
