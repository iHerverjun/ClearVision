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
        this.flowCanvas = flowCanvas; // 需要引用画布以应用结果
        this.container = document.getElementById(containerId);
        
        // 状态
        this.isGenerating = false;
        this.history = []; // { id, desc, time, result, error }
        this.currentThinkingStep = null;
        
        // 绑定方法
        this._handleGenerate = this._handleGenerate.bind(this);
        this._handleApplyFlow = this._handleApplyFlow.bind(this);
        
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
    
    /**
     * 激活视图时调用
     */
    activate() {
        this._checkConnection();
        // 如果输入框为空且有历史记录，可以恢复上一条？或者聚焦输入框
        const textarea = this.container.querySelector('.ai-textarea');
        if (textarea) textarea.focus();
    }
    
    /**
     * 渲染整体结构
     */
    render() {
        this.container.innerHTML = `
            <header class="ai-header">
                <div class="ai-title">
                    <svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor">
                        <path d="M12 2l2.4 7.2L22 12l-7.6 2.8L12 22l-2.4-7.2L2 12l7.6-2.8z"/>
                    </svg>
                    <span>AI 智能工程助手</span>
                </div>
                <div class="ai-status-indicator" id="ai-conn-status">
                    <span class="status-dot connecting"></span>
                    <span class="status-text">连接中...</span>
                </div>
            </header>
            
            <div class="ai-workspace">
                <!-- 左侧：输入与历史 -->
                <aside class="ai-pane-left">
                    <div class="ai-input-section">
                        <div class="ai-section-title">检测需求描述</div>
                        <div class="ai-input-wrapper">
                            <textarea class="ai-textarea" id="ai-input" 
                                placeholder="请描述您的视觉检测需求...&#10;例如：使用500万像素相机拍摄产品表面，检测是否存在划痕和污渍，如果是NG品则通过串口发送剔除信号。"></textarea>
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
                                <span class="ai-tag" data-text="测量两个圆形孔位的圆心距离。需要先进行相机标定，然后识别圆孔特征，计算像素距离并转换为物理毫米。">孔距测量</span>
                            </div>
                        </div>
                    </div>
                    
                    <div class="ai-history-section">
                        <div class="ai-section-title" style="margin-bottom: 8px;">历史记录</div>
                        <div class="ai-history-list" id="ai-history-list">
                            <!-- 动态填充 -->
                            <div style="padding: 20px; text-align: center; color: var(--text-secondary); font-size: 12px;">暂无历史记录</div>
                        </div>
                    </div>
                </aside>
                
                <!-- 中间：对话与思考链 -->
                <section class="ai-pane-center">
                    <div class="ai-thinking-container" id="ai-chat-container">
                        <!-- 欢迎消息 -->
                        <div class="ai-message ai">
                            <div class="ai-avatar ai">AI</div>
                            <div class="ai-content">
                                <div class="ai-bubble">你好！我是 ClearVision 智能助手。请在左侧描述您的检测需求，我将为您自动生成完整的视觉工程方案。</div>
                            </div>
                        </div>
                    </div>
                </section>
                
                <!-- 右侧：结果预览 -->
                <aside class="ai-pane-right" id="ai-result-pane" style="display:none;">
                    <div class="ai-input-section" style="border-bottom: none; padding-bottom: 0;">
                        <div class="ai-section-title">生成结果预览</div>
                    </div>
                    
                    <div class="ai-result-panel">
                        <!-- 统计卡片 -->
                        <div class="result-card">
                            <div class="card-title">方案概览</div>
                            <div class="ai-explanation" id="ai-result-summary">
                                本方案包含 5 个算子，主要使用深度学习进行缺陷分割。
                            </div>
                        </div>
                        
                        <!-- 算子清单 -->
                            <div class="card-title">包含算子</div>
                            <div class="generated-ops-list" id="ai-result-ops">
                                <!-- 动态生成的算子标签 -->
                            </div>
                        </div>
                        
                        <!-- AI 说明 -->
                        <div class="result-card">
                            <div class="card-title">💡 设计思路</div>
                            <div class="ai-explanation" id="ai-result-reasoning" style="max-height: 150px; overflow-y: auto;">
                                --
                            </div>
                        </div>

                        <!-- AI 思维链（可折叠） -->
                        <div class="result-card" id="ai-thinking-card" style="display:none;">
                            <div class="card-title" style="cursor:pointer;user-select:none;" id="ai-thinking-toggle">
                                🧠 AI 推理过程 <span style="font-size:11px;color:var(--text-secondary);">▼ 点击展开/收起</span>
                            </div>
                            <div class="ai-explanation" id="ai-result-thinking" style="max-height: 300px; overflow-y: auto; white-space: pre-wrap; font-size: 12px; line-height: 1.6; display: none;">
                            </div>
                        </div>
                        
                        <!-- 操作栏 -->
                        <div class="ai-actions">
                            <button class="btn btn-primary btn-apply-flow" id="ai-btn-apply">
                                <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor">
                                    <path d="M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z"/>
                                </svg>
                                应用到当前工程
                            </button>
                        </div>
                    </div>
                </aside>
            </div>
        `;
        
        // 绑定事件
        this.container.querySelector('#ai-btn-gen').addEventListener('click', this._handleGenerate);
        this.container.querySelector('#ai-btn-apply').addEventListener('click', this._handleApplyFlow);

        // 思维链折叠切换
        this.container.querySelector('#ai-thinking-toggle')?.addEventListener('click', () => {
            const thinkingContent = this.container.querySelector('#ai-result-thinking');
            if (thinkingContent) {
                thinkingContent.style.display = thinkingContent.style.display === 'none' ? 'block' : 'none';
            }
        });
        
        // 绑定示例点击
        this.container.querySelectorAll('.ai-tag').forEach(tag => {
            tag.addEventListener('click', () => {
                const text = tag.dataset.text;
                const input = this.container.querySelector('#ai-input');
                input.value = text;
                input.focus();
            });
        });
        
        // 适配输入框快捷键 (Ctrl+Enter)
        this.container.querySelector('#ai-input').addEventListener('keydown', (e) => {
            if (e.ctrlKey && e.key === 'Enter') {
                this._handleGenerate();
            }
        });
    }
    
    _checkConnection() {
        const indicator = this.container.querySelector('#ai-conn-status');
        const dot = indicator.querySelector('.status-dot');
        const text = indicator.querySelector('.status-text'); // NOTE: Assuming HTML structure change for status text or verify selector
        
        // 简单的健康检查
        httpClient.get('/api/health')
            .then(() => {
                dot.className = 'status-dot connected';
                // text.textContent = '服务在线'; // Removed text update if selector might fail or keep simple
            })
            .catch(() => {
                dot.className = 'status-dot disconnected';
                // text.textContent = '连接断开';
            });
    }
    
    _setupMessageListeners() {
        // 监听进度
        webMessageBridge.on('GenerateFlowProgress', (data) => this._updateProgress(data));
        
        // 监听防火墙事件
        webMessageBridge.on('AiFirewallBlocked', (data) => this._handleFirewallBlocked(data));
        
        // 监听结果 (原有消息复用)
        webMessageBridge.on('GenerateFlowResult', (data) => this._handleResult(data));
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
        
        // 1. 添加用户消息到对话区
        this._addMessage('user', description);
        
        // 2. 初始化思考链区域
        const thinkingId = 'thinking-' + Date.now();
        this._addThinkingChain(thinkingId);
        
        try {
            // 3. 发送请求
            this._updateThinkingStep(thinkingId, 'start', '正在连接 AI 助手...');
            
            webMessageBridge.sendMessage("GenerateFlow", { 
                payload: { description } 
            });
            
        } catch (err) {
            this._handleError(err.message);
        }
    }
    
    _updateProgress(data) {
        // data结构: { message: "...", phase: "..." }
        // 兼容旧格式，也许直接收到了字符串
        const msg = typeof data === 'string' ? data : (data.payload?.message || data.message);
        
        if (msg) {
            const lastChain = this.container.querySelector('.thinking-chain:last-child');
            if (lastChain) {
                // 添加新的思考步骤
                const step = document.createElement('div');
                step.className = 'thinking-step active';
                step.innerHTML = `
                    <div class="step-title">${msg}</div>
                    <div class="step-content">...</div>
                `;
                
                // 将上一步设为完成
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
    
    _handleResult(data) {
        this._setGeneratingState(false);
        
        // 结束最后的思考步骤
        const lastChain = this.container.querySelector('.thinking-chain:last-child');
        if (lastChain) {
            const activeStep = lastChain.querySelector('.thinking-step.active');
            if (activeStep) {
                activeStep.classList.remove('active');
                activeStep.classList.add('completed');
                activeStep.querySelector('.step-content').textContent = data.success ? '生成成功' : '生成失败';
            }
        }
        
        if (!data.success) {
            this._addMessage('system', `❌ 生成失败: ${data.errorMessage || '未知错误'}`);
            return;
        }
        
        // 保存结果并显示
        this.currentResult = data;
        this._displayResult(data);
        
        // 添加成功消息
        this._addMessage('ai', '工程方案已生成！请查看右侧预览面板。您可以点击“应用到当前工程”将方案加载到画布。');
        
        // 记录历史
        this._addToHistory({
            desc: this.container.querySelector('#ai-input').value.trim(),
            time: new Date(),
            success: true,
            opCount: data.flow?.operators?.length || 0
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
        resultPane.style.display = 'flex';
        
        const ops = data.flow?.operators || [];
        
        // 摘要
        this.container.querySelector('#ai-result-summary').textContent = 
            `方案包含 ${ops.length} 个算子，${data.flow?.connections?.length || 0} 条连线。`;
            
        // 算子列表
        const opsContainer = this.container.querySelector('#ai-result-ops');
        opsContainer.innerHTML = ops.map(op => `
            <span class="op-tag">
                <span class="op-tag-icon"></span>
                ${op.name}
            </span>
        `).join('');
        
        // 思路说明
        this.container.querySelector('#ai-result-reasoning').textContent = 
            data.aiExplanation || 'AI 未提供详细说明。';

        // 思维链（推理过程）
        const thinkingCard = this.container.querySelector('#ai-thinking-card');
        const thinkingContent = this.container.querySelector('#ai-result-thinking');
        if (data.reasoning && data.reasoning.trim()) {
            thinkingCard.style.display = 'block';
            thinkingContent.textContent = data.reasoning;
            thinkingContent.style.display = 'none'; // 默认收起
        } else {
            thinkingCard.style.display = 'none';
        }
    }
    
    _handleApplyFlow() {
        if (!this.currentResult || !this.flowCanvas) return;
        
        try {
            // 切换到流程视图
            const flowBtn = document.querySelector('.nav-btn[data-view="flow"]');
            if (flowBtn) flowBtn.click();
            
            // 应用流程
            this.flowCanvas.deserialize(this.currentResult.flow);
            
            // 高亮需审查的节点
            if (this.currentResult.parametersNeedingReview) {
                // 需要 flowCanvas 实现 highlightNodes
                // this.flowCanvas.highlightNodes(...)
            }
            
            // 提示成功 (使用全局 showToast，假设存在)
            if (window.showToast) window.showToast('方案已应用到画布', 'success');
            
        } catch (err) {
            console.error('应用流程失败:', err);
            alert('应用流程失败: ' + err.message);
        }
    }
    
    _addMessage(role, text) {
        const container = this.container.querySelector('#ai-chat-container');
        const msg = document.createElement('div');
        msg.className = `ai-message ${role}`;
        msg.innerHTML = `
            <div class="ai-avatar ${role}">${role === 'user' ? 'U' : 'AI'}</div>
            <div class="ai-content">
                <div class="ai-bubble">${text}</div>
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
                 <div class="thinking-chain" id="${id}">
                     <!-- 步骤容器 -->
                 </div>
             </div>
        `;
        container.appendChild(msg);
        this._scrollToBottom();
    }
    
    _updateThinkingStep(chainId, stepId, text) {
        // ... (在 _updateProgress 中简化处理了)
    }
    
    _setGeneratingState(busy) {
        this.isGenerating = busy;
        const btn = this.container.querySelector('#ai-btn-gen');
        btn.disabled = busy;
        btn.innerHTML = busy ? 
            `<span class="typing-dot"></span><span class="typing-dot"></span><span class="typing-dot"></span>` : 
            `<svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor"><path d="M12 2l2.4 7.2L22 12l-7.6 2.8L12 22l-2.4-7.2L2 12l7.6-2.8z"/></svg> 生成工程方案`;
            
        const input = this.container.querySelector('#ai-input');
        input.disabled = busy;
    }
    
    _clearResultPane() {
        this.container.querySelector('#ai-result-pane').style.display = 'none';
    }
    
    _scrollToBottom() {
        const container = this.container.querySelector('#ai-chat-container');
        container.scrollTop = container.scrollHeight;
    }
    
    _addToHistory(entry) {
        this.history.unshift(entry);
        this._renderHistoryList();
    }
    
    _renderHistoryList() {
        const list = this.container.querySelector('#ai-history-list');
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
        // 模拟：可以从 localStorage 加载
        this._renderHistoryList();
    }
}
