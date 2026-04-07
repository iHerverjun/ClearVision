import webMessageBridge from '../../core/messaging/webMessageBridge.js';
import httpClient from '../../core/messaging/httpClient.js';
import { createSignal } from '../../core/state/store.js';
import { buildWireSequenceFollowupHint } from '../flow-editor/wireSequenceAssist.js';

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
        this.nextHintDraft = '';
        this.activeGenerateRequestId = null;
        this.activeGenerateSessionId = null;
        this.isCancellingGenerate = false;
        this.attachments = [];
        this.pendingParameterDrafts = {};
        this.pendingParameterDraftSignature = '';
        this.operatorMetadataCache = new Map();
        this.operatorMetadataLoading = new Map();
        this.cameraBindingsCache = [];
        this.cameraBindingsLoadingPromise = null;
        this.currentResultVersion = 0;
        this.appliedResultVersion = 0;
        this.currentCanvasRevision = this.flowCanvas?.getFlowRevision?.() || 0;
        this.appliedCanvasRevision = 0;
        this.pendingOperatorBindings = {};
        this.unsubscribeStructureState = null;
        this.pendingParameterFilePickContext = null;
        this.pendingParameterHighlightTimer = null;
        this.pendingParameterConfirmedDraftSignature = '';
        this.pendingParameterConfirmedValueSignature = '';
        this._streamBuffer = { thinking: '', content: '' };
        this._streamFlushPending = false;
        
        // 绑定方法
        this._handleGenerate = this._handleGenerate.bind(this);
        this._handleApplyFlow = this._handleApplyFlow.bind(this);
        this._handleConfirmPendingParameters = this._handleConfirmPendingParameters.bind(this);
        this._handlePendingParameterReview = this._handlePendingParameterReview.bind(this);
        this._handleNewConversation = this._handleNewConversation.bind(this);
        this._handleAttachmentClick = this._handleAttachmentClick.bind(this);
        this._handleFilePickedEvent = this._handleFilePickedEvent.bind(this);
        this._handleAttachmentReport = this._handleAttachmentReport.bind(this);
        this._handleCancelGenerate = this._handleCancelGenerate.bind(this);
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
        this._setupCanvasStructureSync();
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
        this.nextHintDraft = '';
        this.activeGenerateRequestId = null;
        this.activeGenerateSessionId = null;
        this.isCancellingGenerate = false;
        this.attachments = [];
        this._resetPendingDraftState();
        this._resetCurrentResultSyncState();
        this.pendingParameterFilePickContext = null;
        this._clearResultPane();
        this._renderAttachments();
        this._renderQueuedHintBanner();
        const container = this.container.querySelector('#ai-chat-container');
        if (container) container.innerHTML = '';
        const progress = this.container.querySelector('#ai-progress-container');
        if (progress) progress.innerHTML = '<div class="ai-empty-state" style="text-align:center;color:#999;font-size:14px;margin-top:40px;">等待输入指令...</div>';
        this._addMessage('ai', '您好！我是您的视觉工程助手。已开始新对话。');
    }

    _setupCanvasStructureSync() {
        this.currentCanvasRevision = this.flowCanvas?.getFlowRevision?.() || 0;
        if (!this.flowCanvas?.subscribeStructureState) {
            return;
        }

        if (this.unsubscribeStructureState) {
            this.unsubscribeStructureState();
        }

        this.unsubscribeStructureState = this.flowCanvas.subscribeStructureState((payload) => {
            const revision = Number(payload?.flowRevision);
            if (Number.isFinite(revision)) {
                this.currentCanvasRevision = revision;
            } else {
                this.currentCanvasRevision = this.flowCanvas?.getFlowRevision?.() || this.currentCanvasRevision;
            }

            if (!this._isCurrentResultAppliedToCanvas() || !this.currentResult?.flow) {
                return;
            }

            this._syncPendingParameterDrafts(this.currentResult, this.currentResult.flow, { force: true });
            this._renderFollowupChecklist(this.currentResult, this.currentResult.flow);
            const editor = this.container?.querySelector('#ai-result-parameter-editor');
            if (editor && !editor.classList.contains('is-empty')) {
                this._renderParameterDraftEditor(this.currentResult, this.currentResult.flow);
            }
        });
    }

    _resetPendingDraftState() {
        this.pendingParameterDrafts = {};
        this.pendingParameterDraftSignature = '';
        this.pendingOperatorBindings = {};
        this.pendingParameterConfirmedDraftSignature = '';
        this.pendingParameterConfirmedValueSignature = '';
    }

    _resetCurrentResultSyncState() {
        this.currentResult = null;
        this.currentResultVersion = 0;
        this.appliedResultVersion = 0;
        this.appliedCanvasRevision = this.currentCanvasRevision;
    }

    _setCurrentResult(payload) {
        this.currentResult = payload;
        this.currentResultVersion += 1;
        this.appliedResultVersion = 0;
        this.appliedCanvasRevision = 0;
    }

    _markCurrentResultAppliedToCanvas() {
        if (!this.currentResultVersion) return;
        this.currentCanvasRevision = this.flowCanvas?.getFlowRevision?.() || this.currentCanvasRevision;
        this.appliedResultVersion = this.currentResultVersion;
        this.appliedCanvasRevision = this.currentCanvasRevision;
    }

    _isCurrentResultAppliedToCanvas() {
        return Boolean(this.currentResult && this.currentResultVersion > 0 && this.appliedResultVersion === this.currentResultVersion);
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
                            <button class="ai-btn-cancel" id="ai-btn-cancel" type="button" title="取消生成">取消</button>
                            <button class="ai-btn-send" id="ai-btn-gen">
                                <svg viewBox="0 0 24 24" width="18" height="18" fill="white"><path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z"/></svg>
                            </button>
                        </div>
                        <div class="ai-attachments" id="ai-attachments"></div>
                        <div class="ai-followup-hint-banner" id="ai-followup-hint-banner"></div>
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
                    <div class="ai-results-scroll" id="ai-results-scroll">
                        <div class="result-card overview">
                            <div class="card-title">方案概览</div>
                            <div class="ai-explanation" id="ai-result-summary">--</div>
                        </div>

                        <div class="result-card ops-list">
                            <div class="card-title">生成的算子清单</div>
                            <div class="generated-ops-list" id="ai-result-ops"></div>
                        </div>

                        <div class="result-card followup-card">
                            <div class="card-title">待补信息</div>
                            <div class="ai-followup-panel is-empty" id="ai-result-followups">
                                <div class="ai-followup-empty">当前没有待确认参数或缺失资源。</div>
                            </div>
                        </div>

                        <div class="result-card parameter-editor-card">
                            <div class="card-title">参数补录与审核</div>
                            <div class="ai-parameter-editor is-empty" id="ai-result-parameter-editor">
                                <div class="ai-followup-empty">当前没有待确认参数，暂无需补录。</div>
                            </div>
                        </div>
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
        const cancelBtn = this.container.querySelector('#ai-btn-cancel');
        this.container.querySelector('#ai-btn-gen').addEventListener('click', this._handleGenerate);
        this.container.querySelector('#ai-btn-apply').addEventListener('click', this._handleApplyFlow);
        if (attachBtn) attachBtn.addEventListener('click', this._handleAttachmentClick);
        if (cancelBtn) cancelBtn.addEventListener('click', this._handleCancelGenerate);
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
        this._renderQueuedHintBanner();
        this._renderFollowupChecklist(null);
    }
    
    _checkConnection() {
        const indicator = this.container.querySelector('#ai-conn-status');
        const dot = indicator?.querySelector('.status-dot');
        if (!dot) return;
        
        httpClient.get('/health')
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
        webMessageBridge.on('CancelGenerateFlowResult', (data) => this._handleCancelResult(data));
        webMessageBridge.on('FilePickedEvent', this._handleFilePickedEvent);
        webMessageBridge.on('GenerateFlowAttachmentReport', this._handleAttachmentReport);
        webMessageBridge.on('ListAiSessionsResult', (data) => this._handleListAiSessionsResult(data));
        webMessageBridge.on('GetAiSessionResult', (data) => this._handleGetAiSessionResult(data));
        webMessageBridge.on('DeleteAiSessionResult', (data) => this._handleDeleteAiSessionResult(data));
    }
    
    _getCurrentFlowJson() {
        let baseFlow = null;
        if (this.currentResult && this.currentResult.flow && !this._isCurrentResultAppliedToCanvas()) {
            baseFlow = this.currentResult.flow;
        } else if (this.flowCanvas && typeof this.flowCanvas.serialize === 'function') {
            baseFlow = this.flowCanvas.serialize();
        } else if (this.currentResult && this.currentResult.flow) {
            baseFlow = this.currentResult.flow;
        }

        return this._buildFlowWithPendingDrafts(baseFlow);
    }

    _dispatchGenerateRequest({
        description,
        hint = '',
        userMessage = '',
        attachmentPaths = [],
        existingFlowJson = null,
        clearInput = true
    }) {
        const input = this.container.querySelector('#ai-input');
        const normalizedDescription = String(description || '').trim();
        const normalizedHint = String(hint || '').trim();
        const requestId = this._createGenerateRequestId();

        if (!normalizedDescription) {
            this._addMessage('system', '请输入需求描述。');
            return false;
        }

        if (this.isGenerating) return false;

        this.lastUserPrompt = String(userMessage || normalizedDescription).trim();
        this._setGeneratingState(true);
        this._clearResultPane();
        this.activeGenerateRequestId = requestId;
        this.activeGenerateSessionId = this.sessionId;
        this.isCancellingGenerate = false;

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

        this._addMessage('user', userMessage || normalizedDescription);
        const thinkingId = `thinking-${Date.now()}`;
        this._addThinkingChain(thinkingId);

        try {
            this._updateProgress('正在连接 AI 助手...');
            webMessageBridge.sendMessage('GenerateFlow', {
                payload: {
                    description: normalizedDescription,
                    hint: normalizedHint || null,
                    requestId,
                    sessionId: this.sessionId,
                    existingFlowJson: existingFlowJson ?? this._getCurrentFlowJson(),
                    attachments: attachmentPaths
                }
            });
            this.nextHintDraft = '';
            this._renderQueuedHintBanner();
            if (clearInput && input) {
                input.value = '';
                input.style.height = 'auto';
            }
            return true;
        } catch (err) {
            this._handleError(err.message);
            return false;
        }
    }

    async _handleGenerate() {
        const input = this.container.querySelector('#ai-input');
        const description = input.value.trim();
        const attachmentPaths = this.attachments.map(item => item.path);
        const hint = this.nextHintDraft.trim();
        const userMessage = attachmentPaths.length > 0
            ? `${description}\n\n[附件] ${this.attachments.map(item => item.name).join('，')}`
            : description;
        this._dispatchGenerateRequest({
            description,
            hint,
            userMessage,
            attachmentPaths,
            clearInput: true
        });
    }

    async _handlePendingParameterReview() {
        if (this.isGenerating) return;

        if (!this.currentResult?.flow) {
            this._addMessage('system', '当前没有可审核的方案，请先生成工程方案。');
            return;
        }

        const pending = this._normalizePendingParameters(
            this.currentResult?.pendingParameters ?? this.currentResult?.PendingParameters
        );
        if (pending.length === 0) {
            this._addMessage('system', '当前没有待确认参数，无需提交 AI 审核。');
            return;
        }

        const operators = this._getPendingOperatorSourceOperators(this.currentResult.flow);
        const confirmationState = this._getPendingParameterConfirmationState(pending, operators);
        if (!confirmationState.canReview) {
            this._addMessage('system', '请先确认全部参数，再提交审核。');
            return;
        }

        const reviewRequest = this._buildPendingParameterReviewRequest();
        if (!reviewRequest) {
            this._addMessage('system', '当前没有可提交的参数审核内容。');
            return;
        }

        this._dispatchGenerateRequest({
            description: '请审核并更新当前方案中的待确认参数，保持流程结构稳定，仅调整参数和必要补充信息。',
            hint: reviewRequest.hint,
            userMessage: reviewRequest.userMessage,
            existingFlowJson: reviewRequest.existingFlowJson,
            attachmentPaths: [],
            clearInput: true
        });
    }

    _handleConfirmPendingParameters(data = this.currentResult, flow = null) {
        if (this.isGenerating) return;

        if (!this.currentResult?.flow) {
            this._addMessage('system', '当前没有可提交审核的方案，请先生成工程方案。');
            return;
        }

        const pending = this._normalizePendingParameters(data?.pendingParameters ?? data?.PendingParameters);
        if (pending.length === 0) {
            this._addMessage('system', '当前没有待确认参数，无需执行确认。');
            return;
        }

        const operators = this._getPendingOperatorSourceOperators(flow || data?.flow || data?.Flow || null);
        const groups = this._collectPendingDraftGroups(pending, operators);
        const confirmationState = this._getPendingParameterConfirmationState(pending, operators, groups);
        if (confirmationState.isConfirmed) {
            return;
        }
        if (!confirmationState.canConfirm) {
            this._addMessage('system', '请先填写全部待确认参数，再执行统一确认。');
            return;
        }

        this.pendingParameterConfirmedDraftSignature = this.pendingParameterDraftSignature;
        this.pendingParameterConfirmedValueSignature = confirmationState.valueSignature;
        this._updatePendingDraftSummary(data, flow);
    }

    _handleCancelGenerate() {
        if (!this.isGenerating || this.isCancellingGenerate) return;

        const requestId = this.activeGenerateRequestId;
        const sessionId = this.activeGenerateSessionId || this.sessionId;
        if (!requestId) return;

        this.isCancellingGenerate = true;

        webMessageBridge.sendMessage('CancelGenerateFlow', {
            payload: {
                requestId,
                sessionId
            }
        });

        this._updateProgress({
            message: '正在取消生成...',
            phase: 'cancelling'
        });
        this._addMessage('system', '已发送取消请求，正在等待后端停止当前生成。');
    }
    
    _updateProgress(data) {
        // Clear streaming placeholder when real text is streaming
        if (data === "收到 AI 响应，正在解析 JSON 数据...") {
             return;
        }

        if (typeof data !== 'string') {
            const payload = data?.payload || data || {};
            if (!this._shouldHandleGenerateRealtimePayload(payload)) {
                return;
            }
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
        if (!this._shouldHandleGenerateRealtimePayload(payload)) {
            return;
        }

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

        const payload = data.payload || data;
        if (!this._shouldHandleGenerateTerminalPayload(payload)) {
            return;
        }

        const isCancelled = this._isCancelledResult(payload);
        this.isCancellingGenerate = false;
        this._setGeneratingState(false);
        this.sessionId = payload.sessionId || this.sessionId;
        this._saveSessionId(this.sessionId);
        
        const container = this.container.querySelector('#ai-progress-stepper');
        if (container) {
            const activeStep = container.querySelector('.stepper-item.active');
            if (activeStep) {
                activeStep.classList.remove('active', 'completed', 'failed', 'cancelled');
                activeStep.classList.add(payload.success ? 'completed' : (isCancelled ? 'cancelled' : 'failed'));
                const icon = activeStep.querySelector('.check-icon');
                if (icon) {
                    if (payload.success) {
                        icon.innerHTML = '<path d="M9 16.2L4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4L9 16.2z"/>';
                    } else if (isCancelled) {
                        icon.innerHTML = '<path d="M7 7h10v10H7z"/>';
                    } else {
                        icon.innerHTML = '<path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"/>';
                    }
                    icon.style.display = 'block';
                }
                const dot = activeStep.querySelector('.dot-icon');
                if (dot) dot.style.display = 'none';
                activeStep.querySelector('.stepper-title').innerHTML = payload.success ? '生成成功' : (isCancelled ? '已取消' : '生成失败');
                const bar = activeStep.querySelector('.stepper-bar-container');
                if(bar) bar.style.display = 'none';
            }
        }

        if (isCancelled) {
            this._clearActiveRequestState();
            this._addMessage('system', '已取消本次生成。');
            return;
        }

        if (!payload.success) {
            this._clearActiveRequestState();
            this._addMessage('system', `❌ 生成失败: ${payload.failureSummary || payload.errorMessage || '未知错误'}`);
            return;
        }

        this._clearActiveRequestState();
        this._setCurrentResult(payload);
        this._resetPendingDraftState();
        this._rebuildPendingOperatorBindings({
            pending: payload?.pendingParameters ?? payload?.PendingParameters,
            flow: payload?.flow ?? payload?.Flow ?? null,
            preferIndexFallback: true
        });
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

    _handleCancelResult(data) {
        const payload = data?.payload || data || {};
        if (!this._shouldHandleGenerateRealtimePayload(payload)) {
            return;
        }

        const status = this._normalizeGenerateStatus(payload);
        if (status === 'cancelled' || status === 'canceled') {
            this._addMessage('system', payload.message || '已发送取消请求，正在等待后端停止当前生成。');
            return;
        }

        this.isCancellingGenerate = false;
        this._addMessage('system', `取消生成未生效: ${payload.errorMessage || payload.message || '未知错误'}`);
    }
    
    _handleFirewallBlocked(data) {
        this._setGeneratingState(false);
        this._clearActiveRequestState();
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
        this._clearActiveRequestState();
        this._addMessage('system', `❌ 系统错误: ${msg}`);
    }
    
    _displayResult(data, options = {}) {
        const { appendChatMessage = true } = options;
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

        const flow = data?.flow || data?.Flow || null;
        const ops = this._extractOperators(flow);
        const connections = this._extractConnections(flow);
        this._syncPendingParameterDrafts(data, flow);

        const summaryLines = [
            `该方案包含 <span class="result-count">${ops.length}</span> 个算子和 <span class="result-count">${connections.length}</span> 条连线。`
        ];
        const templateSummary = this._buildTemplateFirstSummary(data);
        if (templateSummary) {
            summaryLines.push(templateSummary);
        }
        this.container.querySelector('#ai-result-summary').innerHTML = summaryLines.join('<br/>');

        // 算子列表逐个淡入
        const opsContainer = this.container.querySelector('#ai-result-ops');
        opsContainer.innerHTML = '';
        ops.forEach((op, i) => {
            const opName = op?.displayName || op?.DisplayName || op?.name || op?.Name || '未命名算子';
            const item = document.createElement('div');
            item.className = 'generated-op-item';
            item.style.opacity = '0';
            item.style.transform = 'translateX(12px)';
            item.innerHTML = `
                <div class="op-dot"></div>
                <div class="op-name">${this._escapeHtml(String(opName))}</div>
            `;
            opsContainer.appendChild(item);
            setTimeout(() => {
                item.style.transition = 'all 0.3s var(--ease-ink-smooth)';
                item.style.opacity = '1';
                item.style.transform = 'translateX(0)';
            }, 80 * i);
        });

        const matchedTemplateName = data?.recommendedTemplate?.templateName || '';
        const templateNotice = matchedTemplateName ? ` 已按模板优先命中「${matchedTemplateName}」。` : '';
        this._renderFollowupChecklist(data, flow);
        this._renderParameterDraftEditor(data, flow);
        if (appendChatMessage) {
            this._addMessage('ai', `工程方案已生成！包含 ${ops.length} 个算子、${connections.length} 条连线。${templateNotice}可继续输入修改指令。`);
        }
    }

    _buildTemplateFirstSummary(data) {
        const recommended = data?.recommendedTemplate || null;
        const pending = Array.isArray(data?.pendingParameters) ? data.pendingParameters : [];
        const missing = Array.isArray(data?.missingResources) ? data.missingResources : [];

        if (!recommended && pending.length === 0 && missing.length === 0) {
            return '';
        }

        const parts = [];
        if (recommended && recommended.templateName) {
            const templateName = this._escapeHtml(String(recommended.templateName));
            const reason = this._escapeHtml(String(recommended.matchReason || '命中高频场景'));
            const confidence = Number(recommended.confidence);
            const confidenceText = Number.isFinite(confidence) && confidence > 0
                ? `，置信度 ${(confidence * 100).toFixed(0)}%`
                : '';
            parts.push(`模板优先：<span class="result-count">${templateName}</span>（${reason}${confidenceText}）`);
        }

        if (pending.length > 0) {
            parts.push(`待确认参数：<span class="result-count">${pending.length}</span> 组`);
        }

        if (missing.length > 0) {
            const missingPreview = missing
                .slice(0, 2)
                .map(item => this._escapeHtml(String(item?.resourceKey || item?.description || '未知资源')))
                .join('、');
            const suffix = missing.length > 2 ? '...' : '';
            parts.push(`缺失资源：<span class="result-count">${missing.length}</span> 项（${missingPreview}${suffix}）`);
        }

        return parts.join('；');
    }

    _renderFollowupChecklist(data, flow = null) {
        const container = this.container?.querySelector('#ai-result-followups');
        if (!container) return;

        const pending = this._normalizePendingParameters(data?.pendingParameters ?? data?.PendingParameters);
        const missing = this._normalizeMissingResources(data?.missingResources ?? data?.MissingResources);
        const recommended = this._normalizeRecommendedTemplate(data?.recommendedTemplate ?? data?.RecommendedTemplate);
        const operators = this._getPendingOperatorSourceOperators(flow || data?.flow || data?.Flow || null);

        if (!recommended && pending.length === 0 && missing.length === 0) {
            container.classList.add('is-empty');
            container.innerHTML = '<div class="ai-followup-empty">当前没有待确认参数或缺失资源。</div>';
            return;
        }

        const followupText = this._buildFollowupHintText({ recommended, pending, missing, operators });
        const recommendedHtml = recommended
            ? `
                <div class="ai-followup-template">
                    <div class="ai-followup-section-label">模板建议</div>
                    <div class="ai-followup-template-name">${this._escapeHtml(recommended.templateName)}</div>
                    <div class="ai-followup-template-reason">${this._escapeHtml(recommended.matchReason || '建议延续当前模板骨架继续补齐缺失项。')}</div>
                </div>
            `
            : '';

        const pendingHtml = pending.length > 0
            ? `
                <div class="ai-followup-section">
                    <div class="ai-followup-section-header">
                        <div class="ai-followup-section-label">待确认参数</div>
                        <div class="ai-followup-section-tip">点击可跳到下方填写区</div>
                    </div>
                    <div class="ai-followup-list">
                        ${pending.map(item => {
                            const context = this._resolvePendingOperatorContext(item.operatorId, operators);
                            const groupKey = this._getPendingDraftGroupKey(item.operatorId);
                            return `
                            <button class="ai-followup-item ai-followup-nav" type="button" data-followup-nav="${this._escapeHtml(groupKey)}">
                                <div class="ai-followup-item-title">${this._escapeHtml(context.label)}</div>
                                <div class="ai-followup-item-body">需要补充：${this._escapeHtml(item.parameterNames.join('、'))}</div>
                            </button>
                        `;
                        }).join('')}
                    </div>
                </div>
            `
            : '';

        const missingHtml = missing.length > 0
            ? `
                <div class="ai-followup-section">
                    <div class="ai-followup-section-label">缺失资源</div>
                    <div class="ai-followup-list">
                        ${missing.map(item => `
                            <div class="ai-followup-item">
                                <div class="ai-followup-item-title">${this._escapeHtml(item.resourceType || '资源')}</div>
                                <div class="ai-followup-item-body">${this._escapeHtml(item.description || item.resourceKey || '缺少必要资源')}</div>
                                ${item.resourceKey ? `<div class="ai-followup-item-meta">${this._escapeHtml(item.resourceKey)}</div>` : ''}
                            </div>
                        `).join('')}
                    </div>
                </div>
            `
            : '';

        container.classList.remove('is-empty');
        container.innerHTML = `
            ${recommendedHtml}
            ${pendingHtml}
            ${missingHtml}
            <div class="ai-followup-actions">
                <div class="ai-followup-actions-hint">可复制成下一轮补充文本，也可直接挂到下一次生成的 hint。</div>
                <div class="ai-followup-action-row">
                    <button class="ai-followup-action" type="button" data-followup-action="copy">复制待补文本</button>
                    <button class="ai-followup-action" type="button" data-followup-action="insert">插入输入框</button>
                    <button class="ai-followup-action" type="button" data-followup-action="queue">用于下一轮提示</button>
                </div>
            </div>
        `;

        container.querySelectorAll('[data-followup-nav]').forEach(button => {
            button.disabled = this.isGenerating;
            button.addEventListener('click', () => {
                this._scrollToPendingDraftGroup(button.dataset.followupNav || '');
            });
        });

        container.querySelectorAll('[data-followup-action]').forEach(button => {
            button.disabled = this.isGenerating;
            button.addEventListener('click', async () => {
                const action = button.dataset.followupAction;
                if (action === 'copy') {
                    const copied = await this._copyTextToClipboard(followupText);
                    this._addMessage('system', copied ? '待补信息已复制，可直接粘贴到下一轮说明。' : '复制失败，请手动复制待补信息。');
                    return;
                }

                if (action === 'insert') {
                    this._appendFollowupTextToInput(followupText);
                    this._addMessage('system', '待补信息已插入输入框，可继续补充修改需求。');
                    return;
                }

                if (action === 'queue') {
                    this.nextHintDraft = followupText;
                    this._renderQueuedHintBanner();
                    this._addMessage('system', '待补信息已挂到下一轮 hint，下一次生成会自动附带。');
                }
            });
        });
    }

    _renderParameterDraftEditor(data, flow = null) {
        const container = this.container?.querySelector('#ai-result-parameter-editor');
        if (!container) return;

        const pending = this._normalizePendingParameters(data?.pendingParameters ?? data?.PendingParameters);
        const operators = this._getPendingOperatorSourceOperators(flow || data?.flow || data?.Flow || null);
        this._syncPendingParameterDrafts(data, flow);

        if (pending.length === 0) {
            container.classList.add('is-empty');
            container.innerHTML = '<div class="ai-followup-empty">当前没有待确认参数，暂无需补录。</div>';
            return;
        }

        const groups = this._collectPendingDraftGroups(pending, operators);
        const confirmationState = this._getPendingParameterConfirmationState(pending, operators, groups);
        const { totals } = confirmationState;
        const signature = this.pendingParameterDraftSignature;

        this._ensurePendingDraftMetadata(groups, signature);
        if (groups.some(group => group.fields.some(field => field.dataType === 'camerabinding'))) {
            this._ensureCameraBindings(signature);
        }

        container.classList.remove('is-empty');
        container.innerHTML = `
            <div class="ai-parameter-editor-summary">
                已填写 <span class="result-count">${totals.filled}</span> / <span class="result-count">${totals.total}</span> 项。
                <span class="ai-parameter-editor-summary-note">${confirmationState.isConfirmed ? '参数已确认，可直接提交审核。' : '请先填写并确认全部参数，再提交审核。'}</span>
            </div>
            <div class="ai-parameter-group-list">
                ${groups.map(group => `
                    <section class="ai-parameter-group" data-draft-group="${this._escapeHtml(group.groupKey)}">
                        <div class="ai-parameter-group-header">
                            <div>
                                <div class="ai-parameter-group-title">${this._escapeHtml(group.label)}</div>
                                <div class="ai-parameter-group-meta">
                                    ${group.operatorType ? this._escapeHtml(group.operatorType) : '未识别算子类型'}
                                    ${group.operator ? '' : ' · 当前画布快照中未找到精确算子，提交时将按名称提示 AI 继续审核'}
                                </div>
                            </div>
                            <button class="ai-parameter-group-jump" type="button" data-followup-nav="${this._escapeHtml(group.groupKey)}">定位</button>
                        </div>
                        <div class="ai-parameter-field-list">
                            ${group.fields.map(field => this._renderPendingDraftField(group, field, confirmationState)).join('')}
                        </div>
                    </section>
                `).join('')}
            </div>
            <div class="ai-parameter-editor-actions">
                <div class="ai-parameter-editor-actions-hint">${confirmationState.isConfirmed ? '参数已确认，提交审核会带上当前方案、已填写参数、仍未填写项和输入框中的补充说明。' : '请先确认全部参数，再提交审核。审核会带上当前方案、已填写参数、仍未填写项和输入框中的补充说明。'}</div>
                <div class="ai-parameter-editor-action-row">
                    <button class="ai-parameter-confirm-btn" type="button" id="ai-btn-confirm-parameters">确认全部参数</button>
                    <button class="ai-parameter-review-btn" type="button" id="ai-btn-review-parameters">提交审核</button>
                </div>
            </div>
        `;

        container.querySelectorAll('[data-followup-nav]').forEach(button => {
            button.disabled = this.isGenerating;
            button.addEventListener('click', () => {
                this._scrollToPendingDraftGroup(button.dataset.followupNav || '');
            });
        });

        container.querySelectorAll('[data-draft-input="true"]').forEach(inputEl => {
            const updateDraft = () => {
                const operatorId = inputEl.dataset.draftOperatorId || '';
                const parameterName = inputEl.dataset.draftParameterName || '';
                const fieldType = inputEl.dataset.fieldType || '';
                const value = this._readPendingDraftInputValue(inputEl);
                this._setPendingDraftConfirmedValue(operatorId, parameterName, value, fieldType, 'user_input');
                this._updatePendingDraftSummary(data, flow);
                this._renderFollowupChecklist(data, flow);
            };

            inputEl.addEventListener('change', updateDraft);
            if (inputEl.tagName === 'INPUT') {
                inputEl.addEventListener('input', updateDraft);
            }
        });

        container.querySelectorAll('[data-draft-adopt="true"]').forEach(button => {
            button.disabled = this.isGenerating;
            button.addEventListener('click', () => {
                const operatorId = button.dataset.draftOperatorId || '';
                const parameterName = button.dataset.draftParameterName || '';
                const groupsForAdopt = this._collectPendingDraftGroups(pending, operators);
                const targetGroup = groupsForAdopt.find(group => group.operatorId === operatorId);
                const targetField = targetGroup?.fields.find(field =>
                    String(field.parameterName || '').trim().toLowerCase() === String(parameterName || '').trim().toLowerCase()
                );
                if (!targetField) return;
                this._setPendingDraftConfirmedValue(
                    operatorId,
                    parameterName,
                    targetField.suggestedValue,
                    targetField.dataType,
                    'user_input'
                );
                this._renderFollowupChecklist(data, flow);
                this._renderParameterDraftEditor(data, flow);
            });
        });

        container.querySelectorAll('[data-draft-file-pick]').forEach(button => {
            button.disabled = this.isGenerating;
            button.addEventListener('click', () => {
                this._pickPendingDraftFile(
                    button.dataset.draftOperatorId || '',
                    button.dataset.draftParameterName || ''
                );
            });
        });

        const confirmButton = container.querySelector('#ai-btn-confirm-parameters');
        if (confirmButton) {
            confirmButton.addEventListener('click', () => this._handleConfirmPendingParameters(data, flow));
        }

        const reviewButton = container.querySelector('#ai-btn-review-parameters');
        if (reviewButton) {
            reviewButton.addEventListener('click', this._handlePendingParameterReview);
        }
        this._updatePendingDraftSummary(data, flow);
    }

    _collectPendingDraftGroups(pending, operators) {
        return pending.map(item => {
            const context = this._resolvePendingOperatorContext(item.operatorId, operators);
            const metadata = this._getCachedOperatorMetadata(context.operatorType);
            const fields = item.parameterNames.map(parameterName => {
                const parameterMetadata = this._findMetadataParameter(metadata, parameterName);
                const entry = this._getPendingDraftEntry(item.operatorId, parameterName);
                return this._normalizePendingDraftField({
                    operatorId: item.operatorId,
                    parameterName,
                    entry,
                    metadata: parameterMetadata
                });
            });

            return {
                operatorId: item.operatorId,
                operatorType: context.operatorType,
                operator: context.operator,
                label: context.label,
                groupKey: this._getPendingDraftGroupKey(item.operatorId),
                fields
            };
        });
    }

    _renderPendingDraftField(group, field, confirmationState = null) {
        const inputId = this._buildPendingDraftInputId(group.operatorId, field.parameterName);
        const label = this._escapeHtml(field.displayName || field.parameterName);
        const description = field.description
            ? `<div class="ai-parameter-field-desc">${this._escapeHtml(field.description)}</div>`
            : '';
        const currentValue = field.confirmedValue;
        const currentValueText = currentValue === null || currentValue === undefined ? '' : String(currentValue);
        const hasSuggestedValue = this._hasPendingDraftValue(field.suggestedValue, field.dataType);
        const hasConfirmedValue = this._hasPendingDraftValue(field.confirmedValue, field.dataType);
        const isBatchConfirmed = Boolean(confirmationState?.isConfirmed && hasConfirmedValue);
        const showAdoptSuggestion = hasSuggestedValue && !this._arePendingDraftValuesEquivalent(field.confirmedValue, field.suggestedValue, field.dataType);
        const suggestionHtml = hasSuggestedValue
            ? `
                <div class="ai-parameter-field-suggestion">
                    <span class="ai-parameter-field-suggestion-label">建议值：${this._escapeHtml(this._formatPendingDraftValueForDisplay(field.suggestedValue, field))}</span>
                    ${showAdoptSuggestion ? `
                        <button
                            class="ai-parameter-suggestion-btn"
                            type="button"
                            data-draft-adopt="true"
                            data-draft-operator-id="${this._escapeHtml(group.operatorId)}"
                            data-draft-parameter-name="${this._escapeHtml(field.parameterName)}"
                        >
                            采用建议值
                        </button>
                    ` : ''}
                </div>
            `
            : '';
        const sourceHint = field.source === 'canvas_override'
            ? '<div class="ai-parameter-field-desc">当前值已从画布同步。</div>'
            : '';

        let controlHtml = '';
        if (field.dataType === 'boolean' || field.dataType === 'bool') {
            const normalizedBoolean = this.normalizeBooleanLike(currentValue);
            controlHtml = `
                <select
                    id="${this._escapeHtml(inputId)}"
                    class="ai-draft-input ai-draft-select"
                    data-draft-input="true"
                    data-field-type="boolean"
                    data-draft-operator-id="${this._escapeHtml(group.operatorId)}"
                    data-draft-parameter-name="${this._escapeHtml(field.parameterName)}"
                >
                    <option value="" ${normalizedBoolean === null ? 'selected' : ''}>待确认</option>
                    <option value="true" ${normalizedBoolean === true ? 'selected' : ''}>是</option>
                    <option value="false" ${normalizedBoolean === false ? 'selected' : ''}>否</option>
                </select>
            `;
        } else if (field.dataType === 'enum' || field.dataType === 'select' || field.dataType === 'camerabinding') {
            const options = field.dataType === 'camerabinding'
                ? this._buildCameraBindingOptions(currentValue)
                : this._buildEnumOptions(field.options || [], currentValue);
            const extraHint = field.dataType === 'camerabinding' && this.cameraBindingsCache.length === 0
                ? '<div class="ai-parameter-field-desc">正在加载相机绑定列表...</div>'
                : '';
            controlHtml = `
                <select
                    id="${this._escapeHtml(inputId)}"
                    class="ai-draft-input ai-draft-select"
                    data-draft-input="true"
                    data-field-type="${this._escapeHtml(field.dataType)}"
                    data-draft-operator-id="${this._escapeHtml(group.operatorId)}"
                    data-draft-parameter-name="${this._escapeHtml(field.parameterName)}"
                >
                    ${options}
                </select>
                ${extraHint}
            `;
        } else if (field.dataType === 'file') {
            controlHtml = `
                <div class="ai-draft-file-row">
                    <input
                        type="text"
                        id="${this._escapeHtml(inputId)}"
                        class="ai-draft-input ai-draft-text"
                        data-draft-input="true"
                        data-field-type="file"
                        data-draft-operator-id="${this._escapeHtml(group.operatorId)}"
                        data-draft-parameter-name="${this._escapeHtml(field.parameterName)}"
                        value="${this._escapeHtml(currentValueText)}"
                        placeholder="请选择或输入文件路径"
                    />
                    <button
                        class="ai-draft-file-btn"
                        type="button"
                        data-draft-file-pick="true"
                        data-draft-operator-id="${this._escapeHtml(group.operatorId)}"
                        data-draft-parameter-name="${this._escapeHtml(field.parameterName)}"
                    >
                        选择文件
                    </button>
                </div>
            `;
        } else if (['int', 'integer', 'double', 'float', 'number'].includes(field.dataType)) {
            const step = field.step ?? (['int', 'integer'].includes(field.dataType) ? 1 : 'any');
            const minAttr = field.min !== undefined && field.min !== null ? `min="${this._escapeHtml(String(field.min))}"` : '';
            const maxAttr = field.max !== undefined && field.max !== null ? `max="${this._escapeHtml(String(field.max))}"` : '';
            controlHtml = `
                <input
                    type="number"
                    id="${this._escapeHtml(inputId)}"
                    class="ai-draft-input ai-draft-number"
                    data-draft-input="true"
                    data-field-type="${this._escapeHtml(field.dataType)}"
                    data-draft-operator-id="${this._escapeHtml(group.operatorId)}"
                    data-draft-parameter-name="${this._escapeHtml(field.parameterName)}"
                    value="${this._escapeHtml(currentValueText)}"
                    step="${this._escapeHtml(String(step))}"
                    ${minAttr}
                    ${maxAttr}
                    placeholder="请输入数值"
                />
            `;
        } else {
            controlHtml = `
                <input
                    type="text"
                    id="${this._escapeHtml(inputId)}"
                    class="ai-draft-input ai-draft-text"
                    data-draft-input="true"
                    data-field-type="${this._escapeHtml(field.dataType || 'text')}"
                    data-draft-operator-id="${this._escapeHtml(group.operatorId)}"
                    data-draft-parameter-name="${this._escapeHtml(field.parameterName)}"
                    value="${this._escapeHtml(currentValueText)}"
                    placeholder="请输入参数值"
                />
            `;
        }

        return `
            <div class="ai-parameter-field">
                <label class="ai-parameter-field-label" for="${this._escapeHtml(inputId)}">
                    ${label}
                    <span class="ai-parameter-field-key">${this._escapeHtml(field.parameterName)}</span>
                </label>
                ${controlHtml}
                ${suggestionHtml}
                ${isBatchConfirmed ? `<div class="ai-parameter-field-status">当前状态：已确认</div>` : '<div class="ai-parameter-field-status is-unconfirmed">当前状态：待确认</div>'}
                ${sourceHint}
                ${description}
            </div>
        `;
    }

    _countPendingDraftProgress(groups) {
        let total = 0;
        let filled = 0;
        groups.forEach(group => {
            group.fields.forEach(field => {
                total += 1;
                if (this._hasPendingDraftValue(field.confirmedValue, field.dataType)) {
                    filled += 1;
                }
            });
        });
        return { total, filled };
    }

    _hasPendingParameterConfirmation() {
        return Boolean(this.pendingParameterConfirmedDraftSignature && this.pendingParameterConfirmedValueSignature);
    }

    _clearPendingParameterConfirmation() {
        this.pendingParameterConfirmedDraftSignature = '';
        this.pendingParameterConfirmedValueSignature = '';
    }

    _computePendingDraftValueSignature(pending, operators) {
        const safePending = this._normalizePendingParameters(pending);
        const safeOperators = Array.isArray(operators) ? operators : [];
        if (safePending.length === 0) return '';

        const parts = [];
        safePending.forEach(item => {
            const context = this._resolvePendingOperatorContext(item.operatorId, safeOperators);
            const metadata = this._getCachedOperatorMetadata(context.operatorType);
            item.parameterNames.forEach(parameterName => {
                const fieldType = this._normalizePendingFieldType(this._findMetadataParameter(metadata, parameterName));
                const value = this._getPendingDraftConfirmedValue(item.operatorId, parameterName);
                const valueText = this._hasPendingDraftValue(value, fieldType)
                    ? this._stringifyPendingDraftValue(value, fieldType)
                    : '';
                parts.push(`${String(item.operatorId || '').trim().toLowerCase()}::${String(parameterName || '').trim().toLowerCase()}::${String(fieldType || '').trim().toLowerCase()}::${valueText}`);
            });
        });

        return parts.sort().join('|');
    }

    _getPendingParameterConfirmationState(pending, operators, groups = null) {
        const safePending = this._normalizePendingParameters(pending);
        const safeOperators = Array.isArray(operators) ? operators : [];
        const resolvedGroups = Array.isArray(groups) ? groups : this._collectPendingDraftGroups(safePending, safeOperators);
        const totals = this._countPendingDraftProgress(resolvedGroups);
        const valueSignature = this._computePendingDraftValueSignature(safePending, safeOperators);
        const isConfirmed = Boolean(
            totals.total > 0 &&
            totals.filled === totals.total &&
            this.pendingParameterConfirmedDraftSignature &&
            this.pendingParameterConfirmedValueSignature &&
            this.pendingParameterConfirmedDraftSignature === this.pendingParameterDraftSignature &&
            this.pendingParameterConfirmedValueSignature === valueSignature
        );
        const hasCurrentFlow = Boolean(this.currentResult?.flow);

        return {
            groups: resolvedGroups,
            totals,
            valueSignature,
            isConfirmed,
            canConfirm: hasCurrentFlow && totals.total > 0 && totals.filled === totals.total && !isConfirmed,
            canReview: hasCurrentFlow && isConfirmed
        };
    }

    _updatePendingDraftSummary(data = this.currentResult, flow = null) {
        const container = this.container?.querySelector('#ai-result-parameter-editor');
        if (!container || container.classList.contains('is-empty')) return;

        const pending = this._normalizePendingParameters(
            data?.pendingParameters ?? data?.PendingParameters
        );
        if (pending.length === 0) return;

        const operators = this._getPendingOperatorSourceOperators(flow || data?.flow || data?.Flow || null);
        const confirmationState = this._getPendingParameterConfirmationState(pending, operators);
        const { totals } = confirmationState;
        const summary = container.querySelector('.ai-parameter-editor-summary');
        if (summary) {
            summary.innerHTML = `
                已填写 <span class="result-count">${totals.filled}</span> / <span class="result-count">${totals.total}</span> 项。
                <span class="ai-parameter-editor-summary-note">${confirmationState.isConfirmed ? '参数已确认，可直接提交审核。' : '请先填写并确认全部参数，再提交审核。'}</span>
            `;
        }

        const hint = container.querySelector('.ai-parameter-editor-actions-hint');
        if (hint) {
            hint.textContent = confirmationState.isConfirmed
                ? '参数已确认，提交审核会带上当前方案、已填写参数、仍未填写项和输入框中的补充说明。'
                : '请先确认全部参数，再提交审核。审核会带上当前方案、已填写参数、仍未填写项和输入框中的补充说明。';
        }

        container.querySelectorAll('.ai-parameter-field-status').forEach(statusEl => {
            statusEl.textContent = confirmationState.isConfirmed ? '当前状态：已确认' : '当前状态：待确认';
            statusEl.classList.toggle('is-unconfirmed', !confirmationState.isConfirmed);
        });

        const confirmButton = container.querySelector('#ai-btn-confirm-parameters');
        if (confirmButton) {
            confirmButton.disabled = this.isGenerating || !confirmationState.canConfirm;
        }

        const reviewButton = container.querySelector('#ai-btn-review-parameters');
        if (reviewButton) {
            reviewButton.disabled = this.isGenerating || !confirmationState.canReview;
        }
    }

    _syncPendingParameterDrafts(data, flow = null, options = {}) {
        const force = Boolean(options?.force);
        const pending = this._normalizePendingParameters(data?.pendingParameters ?? data?.PendingParameters);
        const operators = this._extractOperators(flow || data?.flow || data?.Flow || null);
        const signature = `${this.currentResultVersion || 0}::${this._computePendingDraftSignature(pending, operators)}`;
        const canvasOperators = this._isCurrentResultAppliedToCanvas()
            ? this._extractOperators(this.flowCanvas?.serialize?.() || null)
            : [];

        if (!force && signature === this.pendingParameterDraftSignature) {
            return;
        }

        if (pending.length === 0) {
            this._resetPendingDraftState();
            return;
        }

        const nextDrafts = force ? this.pendingParameterDrafts : {};
        pending.forEach(item => {
            const context = this._resolvePendingOperatorContext(item.operatorId, operators);
            const metadata = this._getCachedOperatorMetadata(context.operatorType);
            item.parameterNames.forEach(parameterName => {
                const parameterMetadata = this._findMetadataParameter(metadata, parameterName);
                const fieldType = this._normalizePendingFieldType(parameterMetadata);

                if (!nextDrafts[item.operatorId]) {
                    nextDrafts[item.operatorId] = {};
                }

                const entry = force
                    ? this._getPendingDraftEntry(item.operatorId, parameterName)
                    : this._createPendingDraftEntry();
                const suggestedValue = this._normalizePendingValueByType(
                    this._readOperatorParameterValue(context.operator, parameterName),
                    fieldType
                );
                const canvasValue = this._isCurrentResultAppliedToCanvas()
                    ? this._normalizePendingValueByType(
                        this._readOperatorParameterValue(
                            this._resolvePendingOperatorContext(item.operatorId, canvasOperators).operator,
                            parameterName
                        ),
                        fieldType
                    )
                    : null;

                let nextEntry = this._createPendingDraftEntry({
                    ...entry,
                    suggestedValue: this._hasPendingDraftValue(suggestedValue, fieldType) ? suggestedValue : null
                });

                if (!force) {
                    nextEntry.confirmedValue = null;
                    nextEntry.status = 'unconfirmed';
                    nextEntry.source = 'ai_suggestion';
                }

                if (this._isCurrentResultAppliedToCanvas() && this._hasPendingDraftValue(canvasValue, fieldType) && !this._arePendingDraftValuesEquivalent(canvasValue, nextEntry.suggestedValue, fieldType)) {
                    nextEntry = this._createPendingDraftEntry({
                        ...nextEntry,
                        confirmedValue: canvasValue,
                        status: 'confirmed',
                        source: 'canvas_override'
                    });
                } else if (force && nextEntry.source === 'canvas_override' && !this._hasPendingDraftValue(canvasValue, fieldType)) {
                    nextEntry = this._createPendingDraftEntry({
                        ...nextEntry,
                        confirmedValue: null,
                        status: 'unconfirmed',
                        source: this._hasPendingDraftValue(nextEntry.suggestedValue, fieldType) ? 'ai_suggestion' : 'user_input'
                    });
                }

                nextDrafts[item.operatorId][parameterName] = nextEntry;
            });
        });

        this.pendingParameterDrafts = nextDrafts;
        this.pendingParameterDraftSignature = signature;
        if (this._hasPendingParameterConfirmation()) {
            const confirmationState = this._getPendingParameterConfirmationState(pending, operators);
            if (!confirmationState.isConfirmed) {
                this._clearPendingParameterConfirmation();
            }
        }
    }

    _computePendingDraftSignature(pending, operators) {
        if (!Array.isArray(pending) || pending.length === 0) return '';
        const operatorPart = (Array.isArray(operators) ? operators : [])
            .map((operator, index) => {
                const operatorId = operator?.id ?? operator?.Id ?? operator?.tempId ?? operator?.TempId ?? `index-${index}`;
                const operatorType = operator?.type ?? operator?.Type ?? operator?.operatorType ?? operator?.OperatorType ?? '';
                return `${String(operatorId).trim()}:${String(operatorType).trim()}`;
            })
            .join('|');
        const pendingPart = pending
            .map(item => `${item.operatorId}:${item.parameterNames.join(',')}`)
            .join('|');
        return `${this.sessionId || 'no-session'}::${operatorPart}::${pendingPart}`;
    }

    _getPendingDraftGroupKey(operatorId) {
        const normalizedId = String(operatorId || '').trim();
        const binding = this.pendingOperatorBindings[normalizedId] || null;
        return `pending-${binding?.actualOperatorId || normalizedId || 'unknown'}`;
    }

    _buildPendingOperatorBinding({ pendingOperatorId, actualOperatorId = '', label = '', operatorType = '' }) {
        const normalizedPendingId = String(pendingOperatorId || '').trim();
        return {
            pendingOperatorId: normalizedPendingId,
            actualOperatorId: String(actualOperatorId || '').trim(),
            label: String(label || '').trim(),
            operatorType: String(operatorType || '').trim()
        };
    }

    _findOperatorByAnyId(operators, operatorId) {
        const normalizedId = String(operatorId || '').trim();
        if (!normalizedId) return null;

        return (Array.isArray(operators) ? operators : []).find(op => {
            const candidates = [
                op?.tempId,
                op?.TempId,
                op?.id,
                op?.Id
            ].map(value => String(value || '').trim()).filter(Boolean);
            return candidates.includes(normalizedId);
        }) || null;
    }

    _findOperatorByTempSequence(operators, operatorId) {
        const normalizedId = String(operatorId || '').trim();
        const match = normalizedId.match(/^op[_-](\d+)$/i);
        if (!match) return null;

        const index = Number.parseInt(match[1], 10) - 1;
        if (!Number.isInteger(index) || index < 0) {
            return null;
        }

        const safeOperators = Array.isArray(operators) ? operators : [];
        return safeOperators[index] || null;
    }

    _buildPendingOperatorDisplayLabel(operator, fallbackId = '') {
        const normalizedFallbackId = String(fallbackId || '').trim();
        const directName = String(
            operator?.displayName ??
            operator?.DisplayName ??
            operator?.name ??
            operator?.Name ??
            ''
        ).trim();
        if (directName) {
            return normalizedFallbackId ? `${directName}（${normalizedFallbackId}）` : directName;
        }
        return normalizedFallbackId ? `算子 ${normalizedFallbackId}` : '未命名算子';
    }

    _rebuildPendingOperatorBindings({ pending, flow = null, sourceFlow = null, preferIndexFallback = false }) {
        const normalizedPending = this._normalizePendingParameters(pending);
        const actualOperators = this._extractOperators(flow || null);
        const sourceOperators = this._extractOperators(sourceFlow || flow || null);
        const nextBindings = {};

        normalizedPending.forEach((item) => {
            const normalizedPendingId = String(item.operatorId || '').trim();
            const normalizedActualId = String(item.actualOperatorId || '').trim();
            if (!normalizedPendingId) return;

            let sourceMatch = this._findOperatorByAnyId(sourceOperators, normalizedPendingId);
            let actualMatch = normalizedActualId
                ? this._findOperatorByAnyId(actualOperators, normalizedActualId)
                : this._findOperatorByAnyId(actualOperators, normalizedPendingId);

            if (!sourceMatch && preferIndexFallback) {
                sourceMatch = this._findOperatorByTempSequence(sourceOperators, normalizedPendingId);
            }

            if (!actualMatch && sourceMatch) {
                const sourceActualId = String(sourceMatch?.id ?? sourceMatch?.Id ?? '').trim();
                if (sourceActualId) {
                    actualMatch = this._findOperatorByAnyId(actualOperators, sourceActualId);
                }
            }

            if (!actualMatch && preferIndexFallback) {
                actualMatch = this._findOperatorByTempSequence(actualOperators, normalizedPendingId);
            }

            const operatorForLabel = sourceMatch || actualMatch || null;
            nextBindings[normalizedPendingId] = this._buildPendingOperatorBinding({
                pendingOperatorId: normalizedPendingId,
                actualOperatorId: actualMatch?.id ?? actualMatch?.Id ?? normalizedActualId,
                label: this._buildPendingOperatorDisplayLabel(operatorForLabel, normalizedPendingId),
                operatorType: operatorForLabel?.type ?? operatorForLabel?.Type ?? operatorForLabel?.operatorType ?? operatorForLabel?.OperatorType ?? ''
            });
        });

        this.pendingOperatorBindings = nextBindings;
        return nextBindings;
    }

    _getPendingOperatorSourceFlow(fallbackFlow = null) {
        if (this._isCurrentResultAppliedToCanvas() && this.flowCanvas?.serialize) {
            return this.flowCanvas.serialize();
        }
        return fallbackFlow;
    }

    _getPendingOperatorSourceOperators(fallbackFlow = null) {
        return this._extractOperators(this._getPendingOperatorSourceFlow(fallbackFlow));
    }

    _buildPendingDraftInputId(operatorId, parameterName) {
        const normalize = (value) => String(value || '').replace(/[^a-zA-Z0-9_-]/g, '_');
        return `ai-draft-${normalize(operatorId)}-${normalize(parameterName)}`;
    }

    _resolvePendingOperatorContext(operatorId, operators) {
        const normalizedId = String(operatorId || '').trim();
        const safeOperators = Array.isArray(operators) ? operators : [];
        const binding = this.pendingOperatorBindings[normalizedId] || this._buildPendingOperatorBinding({
            pendingOperatorId: normalizedId,
            label: normalizedId ? `算子 ${normalizedId}` : '未命名算子'
        });

        const operator = binding.actualOperatorId
            ? this._findOperatorByAnyId(safeOperators, binding.actualOperatorId)
            : this._findOperatorByAnyId(safeOperators, normalizedId);
        const operatorType = String(
            operator?.type ??
            operator?.Type ??
            operator?.operatorType ??
            operator?.OperatorType ??
            binding.operatorType ??
            ''
        ).trim();
        const label = operator
            ? this._buildPendingOperatorDisplayLabel(operator, normalizedId)
            : binding.label;

        return {
            operator,
            operatorType,
            label
        };
    }

    _getCachedOperatorMetadata(type) {
        const normalizedType = String(type || '').trim().toLowerCase();
        if (!normalizedType) return null;

        if (this.operatorMetadataCache.has(normalizedType)) {
            return this.operatorMetadataCache.get(normalizedType);
        }

        const libraryOperators = window.operatorLibraryPanel?.getOperators?.() || [];
        const matched = libraryOperators.find(operator =>
            String(operator?.type || '').trim().toLowerCase() === normalizedType
        ) || null;
        if (matched) {
            this.operatorMetadataCache.set(normalizedType, matched);
            return matched;
        }

        return null;
    }

    async _ensureOperatorMetadata(type) {
        const normalizedType = String(type || '').trim();
        const cacheKey = normalizedType.toLowerCase();
        if (!normalizedType) return null;

        const cached = this._getCachedOperatorMetadata(normalizedType);
        if (cached) return cached;

        if (this.operatorMetadataLoading.has(cacheKey)) {
            return this.operatorMetadataLoading.get(cacheKey);
        }

        const loadingPromise = httpClient
            .get(`/operators/${encodeURIComponent(normalizedType)}/metadata`)
            .then(metadata => {
                if (metadata && typeof metadata === 'object') {
                    this.operatorMetadataCache.set(cacheKey, metadata);
                    return metadata;
                }
                this.operatorMetadataCache.set(cacheKey, null);
                return null;
            })
            .catch(error => {
                console.warn('[AiPanel] 获取算子元数据失败:', normalizedType, error);
                this.operatorMetadataCache.set(cacheKey, null);
                return null;
            })
            .finally(() => {
                this.operatorMetadataLoading.delete(cacheKey);
            });

        this.operatorMetadataLoading.set(cacheKey, loadingPromise);
        return loadingPromise;
    }

    _ensurePendingDraftMetadata(groups, signature) {
        const missingTypes = [...new Set(groups
            .map(group => group.operatorType)
            .filter(type => {
                const normalizedType = String(type || '').trim().toLowerCase();
                if (!normalizedType) return false;
                const cached = this._getCachedOperatorMetadata(type);
                return cached === null && !this.operatorMetadataCache.has(normalizedType);
            }))];

        missingTypes.forEach(type => {
            this._ensureOperatorMetadata(type).then(() => {
                if (this.pendingParameterDraftSignature !== signature || !this.currentResult?.flow) {
                    return;
                }
                this._renderParameterDraftEditor(this.currentResult, this.currentResult.flow);
            });
        });
    }

    async _ensureCameraBindings(signature) {
        if (this.cameraBindingsCache.length > 0) {
            return this.cameraBindingsCache;
        }

        if (this.cameraBindingsLoadingPromise) {
            return this.cameraBindingsLoadingPromise;
        }

        this.cameraBindingsLoadingPromise = httpClient
            .get('/cameras/bindings')
            .then(result => {
                this.cameraBindingsCache = Array.isArray(result) ? result : [];
                if (this.pendingParameterDraftSignature === signature && this.currentResult?.flow) {
                    this._renderParameterDraftEditor(this.currentResult, this.currentResult.flow);
                }
                return this.cameraBindingsCache;
            })
            .catch(error => {
                console.warn('[AiPanel] 获取相机绑定失败。', error);
                return [];
            })
            .finally(() => {
                this.cameraBindingsLoadingPromise = null;
            });

        return this.cameraBindingsLoadingPromise;
    }

    normalizeBooleanLike(value) {
        if (value === null || value === undefined) return null;
        if (typeof value === 'boolean') return value;
        if (typeof value === 'number') {
            if (value === 1) return true;
            if (value === 0) return false;
            return null;
        }

        const normalized = String(value).trim().toLowerCase();
        if (!normalized) return null;
        if (['true', '1', 'yes', 'y', 'on'].includes(normalized)) return true;
        if (['false', '0', 'no', 'n', 'off'].includes(normalized)) return false;
        return null;
    }

    _isPendingBooleanField(fieldType = '') {
        return ['boolean', 'bool'].includes(String(fieldType || '').trim().toLowerCase());
    }

    _isPendingNumericField(fieldType = '') {
        return ['int', 'integer', 'double', 'float', 'number'].includes(String(fieldType || '').trim().toLowerCase());
    }

    _parseNumericLike(value) {
        if (typeof value === 'number') {
            return Number.isFinite(value) ? value : null;
        }
        const normalized = String(value ?? '').trim();
        if (!normalized) return null;
        const parsed = Number(normalized);
        return Number.isFinite(parsed) ? parsed : null;
    }

    _normalizePendingFieldType(metadata) {
        const rawType = String(
            metadata?.dataType ??
            metadata?.DataType ??
            metadata?.type ??
            metadata?.Type ??
            'text'
        ).trim().toLowerCase();

        return ['string', 'text'].includes(rawType) ? 'text' : rawType;
    }

    _normalizePendingValueByType(value, fieldType = '') {
        if (value === undefined) return null;
        const normalizedFieldType = String(fieldType || '').trim().toLowerCase();

        if (this._isPendingBooleanField(normalizedFieldType)) {
            return this.normalizeBooleanLike(value);
        }

        if (this._isPendingNumericField(normalizedFieldType)) {
            return this._parseNumericLike(value);
        }

        if (value === null) return null;
        const normalized = String(value).trim();
        return normalized.length > 0 ? normalized : null;
    }

    _arePendingDraftValuesEquivalent(left, right, fieldType = '') {
        const normalizedFieldType = String(fieldType || '').trim().toLowerCase();
        if (this._isPendingBooleanField(normalizedFieldType)) {
            return this.normalizeBooleanLike(left) === this.normalizeBooleanLike(right);
        }

        if (this._isPendingNumericField(normalizedFieldType)) {
            return this._parseNumericLike(left) === this._parseNumericLike(right);
        }

        const leftValue = left === null || left === undefined ? '' : String(left).trim();
        const rightValue = right === null || right === undefined ? '' : String(right).trim();
        return leftValue === rightValue;
    }

    _hasPendingDraftValue(value, fieldType = '') {
        if (value === null || value === undefined) return false;

        if (this._isPendingBooleanField(fieldType)) {
            return this.normalizeBooleanLike(value) !== null;
        }

        if (this._isPendingNumericField(fieldType)) {
            return this._parseNumericLike(value) !== null;
        }

        if (typeof value === 'boolean') return true;
        if (typeof value === 'number') return Number.isFinite(value);
        return String(value).trim().length > 0;
    }

    _createPendingDraftEntry(overrides = {}) {
        return {
            confirmedValue: null,
            suggestedValue: null,
            status: 'unconfirmed',
            source: 'ai_suggestion',
            ...overrides
        };
    }

    _getPendingDraftEntry(operatorId, parameterName) {
        const operatorDrafts = this.pendingParameterDrafts[String(operatorId || '').trim()] || {};
        const normalizedName = String(parameterName || '').trim().toLowerCase();
        const matchedKey = Object.keys(operatorDrafts).find(key => key.toLowerCase() === normalizedName);
        const rawEntry = matchedKey ? operatorDrafts[matchedKey] : null;

        if (!rawEntry || typeof rawEntry !== 'object' || Array.isArray(rawEntry)) {
            return this._createPendingDraftEntry();
        }

        return this._createPendingDraftEntry(rawEntry);
    }

    _getPendingDraftConfirmedValue(operatorId, parameterName) {
        return this._getPendingDraftEntry(operatorId, parameterName).confirmedValue;
    }

    _getPendingDraftSuggestedValue(operatorId, parameterName) {
        return this._getPendingDraftEntry(operatorId, parameterName).suggestedValue;
    }

    _setPendingDraftConfirmedValue(operatorId, parameterName, value, fieldType = '', source = 'user_input') {
        const operatorKey = String(operatorId || '').trim();
        const parameterKey = this._resolvePendingDraftParameterKey(operatorKey, parameterName);
        if (!operatorKey || !parameterKey) return;

        const nextValue = this._normalizePendingValueByType(value, fieldType);
        const entry = this._getPendingDraftEntry(operatorKey, parameterKey);
        const previousValue = entry.confirmedValue;
        const hasValue = this._hasPendingDraftValue(nextValue, fieldType);

        if (!this.pendingParameterDrafts[operatorKey]) {
            this.pendingParameterDrafts[operatorKey] = {};
        }

        this.pendingParameterDrafts[operatorKey][parameterKey] = this._createPendingDraftEntry({
            ...entry,
            confirmedValue: hasValue ? nextValue : null,
            status: hasValue ? 'confirmed' : 'unconfirmed',
            source: hasValue ? source : (this._hasPendingDraftValue(entry.suggestedValue, fieldType) ? 'ai_suggestion' : source)
        });

        if (this._hasPendingParameterConfirmation() && !this._arePendingDraftValuesEquivalent(previousValue, hasValue ? nextValue : null, fieldType)) {
            this._clearPendingParameterConfirmation();
        }
    }

    _setPendingDraftSuggestedValue(operatorId, parameterName, value, fieldType = '') {
        const operatorKey = String(operatorId || '').trim();
        const parameterKey = this._resolvePendingDraftParameterKey(operatorKey, parameterName);
        if (!operatorKey || !parameterKey) return;

        if (!this.pendingParameterDrafts[operatorKey]) {
            this.pendingParameterDrafts[operatorKey] = {};
        }

        const entry = this._getPendingDraftEntry(operatorKey, parameterKey);
        const nextSuggestedValue = this._normalizePendingValueByType(value, fieldType);
        this.pendingParameterDrafts[operatorKey][parameterKey] = this._createPendingDraftEntry({
            ...entry,
            suggestedValue: this._hasPendingDraftValue(nextSuggestedValue, fieldType) ? nextSuggestedValue : null,
            source: entry.status === 'confirmed' ? entry.source : 'ai_suggestion'
        });
    }

    _formatPendingDraftValueForDisplay(value, field) {
        if (!this._hasPendingDraftValue(value, field?.dataType)) {
            return '待确认';
        }

        if (this._isPendingBooleanField(field?.dataType)) {
            return this.normalizeBooleanLike(value) ? '是' : '否';
        }

        if ((field?.dataType === 'enum' || field?.dataType === 'select' || field?.dataType === 'camerabinding') && Array.isArray(field?.options)) {
            const matched = field.options.find(option =>
                String(option?.value ?? option?.Value ?? option ?? '').trim() === String(value ?? '').trim()
            );
            if (matched) {
                return String(matched?.label ?? matched?.Label ?? matched?.value ?? matched?.Value ?? value);
            }
        }

        return String(value ?? '');
    }

    _findMetadataParameter(metadata, parameterName) {
        const parameters = metadata?.parameters || metadata?.Parameters || [];
        return parameters.find(item =>
            String(item?.name ?? item?.Name ?? '').trim().toLowerCase() === String(parameterName || '').trim().toLowerCase()
        ) || null;
    }

    _normalizePendingDraftField({ operatorId, parameterName, entry, metadata }) {
        const options = Array.isArray(metadata?.options ?? metadata?.Options)
            ? (metadata?.options ?? metadata?.Options)
            : [];
        const dataType = this._normalizePendingFieldType(metadata);

        return {
            operatorId,
            parameterName,
            displayName: String(metadata?.displayName ?? metadata?.DisplayName ?? parameterName).trim() || parameterName,
            description: String(metadata?.description ?? metadata?.Description ?? '').trim(),
            dataType,
            min: metadata?.min ?? metadata?.Min ?? metadata?.minValue ?? metadata?.MinValue,
            max: metadata?.max ?? metadata?.Max ?? metadata?.maxValue ?? metadata?.MaxValue,
            step: metadata?.step ?? metadata?.Step,
            options,
            defaultValue: metadata?.defaultValue ?? metadata?.DefaultValue ?? null,
            confirmedValue: entry?.confirmedValue ?? null,
            suggestedValue: entry?.suggestedValue ?? null,
            status: entry?.status ?? 'unconfirmed',
            source: entry?.source ?? 'ai_suggestion'
        };
    }

    _buildEnumOptions(options, currentValue) {
        const normalizedCurrent = currentValue == null ? '' : String(currentValue);
        const normalizedOptions = Array.isArray(options) ? options : [];
        const optionRows = normalizedOptions.map(option => {
            const value = option?.value ?? option?.Value ?? option;
            const label = option?.label ?? option?.Label ?? value;
            const selected = String(value ?? '') === normalizedCurrent ? 'selected' : '';
            return `<option value="${this._escapeHtml(String(value ?? ''))}" ${selected}>${this._escapeHtml(String(label ?? ''))}</option>`;
        });

        return [`<option value="">请选择</option>`, ...optionRows].join('');
    }

    _buildCameraBindingOptions(currentValue) {
        const normalizedCurrent = currentValue == null ? '' : String(currentValue);
        const optionRows = this.cameraBindingsCache.map(binding => {
            const value = String(binding?.id ?? '').trim();
            const label = `${binding?.displayName || value}${binding?.serialNumber ? ` (${binding.serialNumber})` : ''}`;
            const selected = value === normalizedCurrent ? 'selected' : '';
            return `<option value="${this._escapeHtml(value)}" ${selected}>${this._escapeHtml(label)}</option>`;
        });

        if (!optionRows.some(option => option.includes('selected')) && normalizedCurrent) {
            optionRows.unshift(`<option value="${this._escapeHtml(normalizedCurrent)}" selected>${this._escapeHtml(normalizedCurrent)}</option>`);
        }

        return [`<option value="">请选择相机绑定</option>`, ...optionRows].join('');
    }

    _readPendingDraftInputValue(inputEl) {
        if (!inputEl) return null;

        const fieldType = String(inputEl.dataset.fieldType || '').trim().toLowerCase();
        const rawValue = inputEl.value;
        return this._normalizePendingValueByType(rawValue, fieldType);
    }

    _resolvePendingDraftParameterKey(operatorId, parameterName) {
        const operatorDrafts = this.pendingParameterDrafts[String(operatorId || '').trim()] || {};
        const normalizedName = String(parameterName || '').trim().toLowerCase();
        const existingKey = Object.keys(operatorDrafts).find(key => key.toLowerCase() === normalizedName);
        return existingKey || String(parameterName || '').trim();
    }

    _getPendingDraftValue(operatorId, parameterName) {
        return this._getPendingDraftConfirmedValue(operatorId, parameterName);
    }

    _readOperatorParameterValue(operator, parameterName) {
        if (!operator || !parameterName) return '';

        const normalizedName = String(parameterName).trim().toLowerCase();
        const parameters = operator?.parameters ?? operator?.Parameters ?? null;

        if (Array.isArray(parameters)) {
            const matched = parameters.find(item =>
                String(item?.name ?? item?.Name ?? '').trim().toLowerCase() === normalizedName
            );
            if (!matched) return '';
            return matched?.value ?? matched?.Value ?? '';
        }

        if (parameters && typeof parameters === 'object') {
            const matchedKey = Object.keys(parameters).find(key => key.toLowerCase() === normalizedName);
            return matchedKey ? parameters[matchedKey] : '';
        }

        return '';
    }

    _buildFlowWithPendingDrafts(flow) {
        if (!flow || typeof flow !== 'object') return flow;

        const clonedFlow = typeof structuredClone === 'function'
            ? structuredClone(flow)
            : JSON.parse(JSON.stringify(flow));
        const operators = this._extractOperators(clonedFlow);
        const pending = this._normalizePendingParameters(
            this.currentResult?.pendingParameters ?? this.currentResult?.PendingParameters
        );

        pending.forEach(item => {
            const context = this._resolvePendingOperatorContext(item.operatorId, operators);
            if (!context.operator) return;

            item.parameterNames.forEach(parameterName => {
                const confirmedValue = this._getPendingDraftConfirmedValue(item.operatorId, parameterName);
                const fieldType = this._normalizePendingFieldType(
                    this._findMetadataParameter(
                        this._getCachedOperatorMetadata(context.operatorType),
                        parameterName
                    )
                );
                if (!this._hasPendingDraftValue(confirmedValue, fieldType)) return;
                this._writeOperatorParameterValue(
                    context.operator,
                    parameterName,
                    confirmedValue
                );
            });
        });

        return clonedFlow;
    }

    _writeOperatorParameterValue(operator, parameterName, value) {
        if (!operator || !parameterName) return;

        if (Array.isArray(operator.parameters)) {
            const matched = operator.parameters.find(item =>
                String(item?.name ?? item?.Name ?? '').trim().toLowerCase() === String(parameterName).trim().toLowerCase()
            );
            if (matched) {
                if ('value' in matched || !('Value' in matched)) {
                    matched.value = value;
                } else {
                    matched.Value = value;
                }
                return;
            }

            operator.parameters.push({
                name: parameterName,
                value
            });
            return;
        }

        if (operator.parameters && typeof operator.parameters === 'object') {
            const matchedKey = Object.keys(operator.parameters).find(key =>
                key.toLowerCase() === String(parameterName).trim().toLowerCase()
            );
            operator.parameters[matchedKey || parameterName] = value;
            return;
        }

        operator.parameters = [{ name: parameterName, value }];
    }

    _scrollToPendingDraftGroup(groupKey) {
        const scrollContainer = this.container?.querySelector('#ai-results-scroll');
        if (!scrollContainer || !groupKey) return;

        const group = Array.from(scrollContainer.querySelectorAll('[data-draft-group]'))
            .find(element => element.dataset.draftGroup === groupKey);
        if (!group) return;

        scrollContainer.scrollTo({
            top: Math.max(0, group.offsetTop - 12),
            behavior: 'smooth'
        });

        const firstInput = group.querySelector('[data-draft-input="true"]');
        if (firstInput && typeof firstInput.focus === 'function') {
            setTimeout(() => firstInput.focus(), 180);
        }

        this._highlightPendingDraftGroup(group);
    }

    _highlightPendingDraftGroup(group) {
        if (!group) return;
        group.classList.add('is-highlighted');
        if (this.pendingParameterHighlightTimer) {
            clearTimeout(this.pendingParameterHighlightTimer);
        }
        this.pendingParameterHighlightTimer = setTimeout(() => {
            group.classList.remove('is-highlighted');
            this.pendingParameterHighlightTimer = null;
        }, 1800);
    }

    _pickPendingDraftFile(operatorId, parameterName) {
        if (this.isGenerating) return;
        this.pendingParameterFilePickContext = {
            operatorId: String(operatorId || '').trim(),
            parameterName: String(parameterName || '').trim()
        };
        webMessageBridge.sendMessage('PickFileCommand', {
            parameterName: 'aiPendingParameterFile',
            filter: 'All Files|*.*'
        });
    }

    _buildPendingParameterReviewRequest() {
        const flow = this._getCurrentFlowJson();
        const pending = this._normalizePendingParameters(
            this.currentResult?.pendingParameters ?? this.currentResult?.PendingParameters
        );
        const operators = this._getPendingOperatorSourceOperators(flow || null);
        const input = this.container?.querySelector('#ai-input');
        const extraNote = String(input?.value || '').trim();
        const queuedHint = String(this.nextHintDraft || '').trim();

        if (!flow || pending.length === 0) {
            return null;
        }

        const lines = [
            '请严格基于当前 existingFlowJson 审核这套方案。',
            '要求：保持流程结构稳定，仅调整参数和必要补充信息；不要无关重建。'
        ];

        const filledLines = [];
        const unfilledLines = [];
        let filledCount = 0;
        let totalCount = 0;

        pending.forEach(item => {
            const context = this._resolvePendingOperatorContext(item.operatorId, operators);
            const filledPairs = [];
            const missingNames = [];
            const metadata = this._getCachedOperatorMetadata(context.operatorType);

            item.parameterNames.forEach(parameterName => {
                totalCount += 1;
                const fieldType = this._normalizePendingFieldType(this._findMetadataParameter(metadata, parameterName));
                const value = this._getPendingDraftConfirmedValue(item.operatorId, parameterName);
                if (this._hasPendingDraftValue(value, fieldType)) {
                    filledCount += 1;
                    filledPairs.push(`${parameterName}=${this._stringifyPendingDraftValue(value, fieldType)}`);
                } else {
                    missingNames.push(parameterName);
                }
            });

            if (filledPairs.length > 0) {
                filledLines.push(`- ${context.label}：${filledPairs.join('；')}`);
            }
            if (missingNames.length > 0) {
                unfilledLines.push(`- ${context.label}：仍缺少 ${missingNames.join('、')}`);
            }
        });

        if (filledLines.length > 0) {
            lines.push('我已补录以下参数：');
            lines.push(...filledLines);
        } else {
            lines.push('我暂时还没有填入任何参数值，请继续指出最关键的缺项。');
        }

        if (unfilledLines.length > 0) {
            lines.push('以下参数仍未填写，请继续保留为待确认项并说明还缺什么：');
            lines.push(...unfilledLines);
        }

        const missingResources = this._normalizeMissingResources(
            this.currentResult?.missingResources ?? this.currentResult?.MissingResources
        );
        if (missingResources.length > 0) {
            lines.push('当前仍存在缺失资源：');
            missingResources.forEach(item => {
                const detail = item.description || item.resourceKey || item.resourceType || '缺少资源';
                lines.push(`- ${detail}`);
            });
        }

        if (queuedHint) {
            lines.push(`附加提示：${queuedHint}`);
        }

        if (extraNote) {
            lines.push(`用户补充说明：${extraNote}`);
        }

        const userMessage = `提交参数审核：已填写 ${filledCount}/${totalCount} 项${extraNote ? '，已附加补充说明' : ''}。`;
        return {
            hint: lines.join('\n'),
            userMessage,
            existingFlowJson: flow
        };
    }

    _stringifyPendingDraftValue(value, fieldType = '') {
        if (this._isPendingBooleanField(fieldType)) {
            const normalized = this.normalizeBooleanLike(value);
            return normalized === null ? '' : (normalized ? 'true' : 'false');
        }
        return String(value ?? '');
    }

    _normalizePendingParameters(items) {
        if (!Array.isArray(items)) return [];

        return items
            .map(item => {
                const rawNames = item?.parameterNames ?? item?.ParameterNames;
                const parameterNames = Array.isArray(rawNames)
                    ? [...new Set(rawNames.map(name => String(name || '').trim()).filter(Boolean))]
                    : [];

                return {
                    operatorId: String(item?.operatorId ?? item?.OperatorId ?? '').trim(),
                    actualOperatorId: String(item?.actualOperatorId ?? item?.ActualOperatorId ?? '').trim(),
                    parameterNames
                };
            })
            .filter(item => item.operatorId || item.actualOperatorId || item.parameterNames.length > 0);
    }

    _normalizeMissingResources(items) {
        if (!Array.isArray(items)) return [];

        return items
            .map(item => ({
                resourceType: String(item?.resourceType ?? item?.ResourceType ?? '').trim(),
                resourceKey: String(item?.resourceKey ?? item?.ResourceKey ?? '').trim(),
                description: String(item?.description ?? item?.Description ?? '').trim()
            }))
            .filter(item => item.resourceType || item.resourceKey || item.description);
    }

    _normalizeRecommendedTemplate(item) {
        if (!item || typeof item !== 'object') return null;

        const templateName = String(item?.templateName ?? item?.TemplateName ?? '').trim();
        if (!templateName) return null;

        return {
            templateName,
            matchReason: String(item?.matchReason ?? item?.MatchReason ?? '').trim()
        };
    }

    _resolvePendingOperatorLabel(operatorId, operators) {
        return this._resolvePendingOperatorContext(operatorId, operators).label;
    }

    _buildFollowupHintText({ recommended, pending, missing, operators }) {
        const lines = ['请基于上一轮流程继续完善，优先补齐待确认参数和缺失资源，不要重建无关结构。'];

        if (recommended?.templateName) {
            lines.push(`优先沿用模板：${recommended.templateName}${recommended.matchReason ? `（${recommended.matchReason}）` : ''}。`);
        }

        if (pending.length > 0) {
            lines.push('待确认参数：');
            pending.forEach(item => {
                const label = this._resolvePendingOperatorLabel(item.operatorId, operators);
                const filledPairs = [];
                const missingNames = [];
                const context = this._resolvePendingOperatorContext(item.operatorId, operators);
                const metadata = this._getCachedOperatorMetadata(context.operatorType);

                item.parameterNames.forEach(parameterName => {
                    const fieldType = this._normalizePendingFieldType(this._findMetadataParameter(metadata, parameterName));
                    const value = this._getPendingDraftConfirmedValue(item.operatorId, parameterName);
                    if (this._hasPendingDraftValue(value, fieldType)) {
                        filledPairs.push(`${parameterName}=${this._stringifyPendingDraftValue(value, fieldType)}`);
                    } else {
                        missingNames.push(parameterName);
                    }
                });

                if (filledPairs.length > 0 && missingNames.length > 0) {
                    lines.push(`- ${label}：已填写 ${filledPairs.join('；')}；仍需补充 ${missingNames.join('、')}`);
                } else if (filledPairs.length > 0) {
                    lines.push(`- ${label}：已填写 ${filledPairs.join('；')}`);
                } else {
                    lines.push(`- ${label}：请补充 ${missingNames.join('、')}`);
                }
            });
        }

        if (missing.length > 0) {
            lines.push('缺失资源：');
            missing.forEach(item => {
                const name = item.resourceType || '资源';
                const detail = item.description || item.resourceKey || '缺少必要资源';
                lines.push(`- ${name}${item.resourceKey ? `（${item.resourceKey}）` : ''}：${detail}`);
            });
        }

        lines.push('如果仍缺文件、模型、地址或标定数据，请明确告诉我还需要补什么。');
        return lines.join('\n');
    }

    queueParameterOnlyFollowupHint(payload = {}) {
        const hint = buildWireSequenceFollowupHint(payload);
        if (!hint) {
            return '';
        }

        this.nextHintDraft = hint;
        this._renderQueuedHintBanner();
        return hint;
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
        if (!this.flowCanvas) return;
        if (!this.currentResult?.flow) {
            this._addMessage('system', '当前会话没有可应用的流程数据。');
            return;
        }

        const flow = this._buildFlowWithPendingDrafts(this.currentResult.flow);
        if (!flow) {
            this._addMessage('system', '当前会话没有可应用的流程数据。');
            return;
        }

        try {
            const flowBtn = document.querySelector('.nav-btn[data-view="flow"]');
            if (flowBtn) flowBtn.click();
            this.flowCanvas.deserialize(flow);
            this._markCurrentResultAppliedToCanvas();
            this._syncPendingParameterDrafts(this.currentResult, this.currentResult?.flow, { force: true });
            this._renderFollowupChecklist(this.currentResult, this.currentResult?.flow);
            this._renderParameterDraftEditor(this.currentResult, this.currentResult?.flow);
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
        if (!busy) {
            this.isCancellingGenerate = false;
        }
        const btn = this.container.querySelector('#ai-btn-gen');
        const cancelBtn = this.container.querySelector('#ai-btn-cancel');
        if(btn) {
            btn.disabled = busy;
            if(busy) {
                btn.innerHTML = `<svg viewBox="0 0 24 24" width="18" height="18" fill="white"><path d="M12 4V2A10 10 0 0 0 2 12h2a8 8 0 0 1 8-8z"><animateTransform attributeName="transform" type="rotate" from="0 12 12" to="360 12 12" dur="1s" repeatCount="indefinite"/></path></svg>`;
            } else {
                btn.innerHTML = `<svg viewBox="0 0 24 24" width="18" height="18" fill="white"><path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z"/></svg>`;
            }
        }
        if (cancelBtn) {
            cancelBtn.disabled = !busy || this.isCancellingGenerate;
            cancelBtn.classList.toggle('is-visible', busy);
        }
        const attachBtn = this.container.querySelector('#ai-btn-attach');
        if (attachBtn) attachBtn.disabled = busy;
        this.container.querySelectorAll('.ai-attachment-remove').forEach(btnEl => {
            btnEl.disabled = busy;
        });
        this.container.querySelectorAll('.ai-followup-action').forEach(btnEl => {
            btnEl.disabled = busy;
        });
        this.container.querySelectorAll('[data-draft-input="true"], [data-draft-file-pick], [data-followup-nav], [data-draft-adopt]').forEach(el => {
            el.disabled = busy;
        });
        const clearHintBtn = this.container.querySelector('#ai-btn-clear-followup-hint');
        if (clearHintBtn) clearHintBtn.disabled = busy;
        const input = this.container.querySelector('#ai-input');
        if(input) input.disabled = busy;
        this._updatePendingDraftSummary();
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
        if (payload.parameterName === 'aiPendingParameterFile') {
            const context = this.pendingParameterFilePickContext;
            this.pendingParameterFilePickContext = null;
            if (!context || payload.isCancelled || !payload.filePath) return;
            this._setPendingDraftConfirmedValue(
                context.operatorId,
                context.parameterName,
                String(payload.filePath || '').trim(),
                'file',
                'user_input'
            );
            if (this.currentResult?.flow) {
                this._renderFollowupChecklist(this.currentResult, this.currentResult.flow);
                this._renderParameterDraftEditor(this.currentResult, this.currentResult.flow);
            }
            return;
        }

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
        if (!this._shouldHandleGenerateRealtimePayload(payload)) return;

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

    _renderQueuedHintBanner() {
        const container = this.container?.querySelector('#ai-followup-hint-banner');
        if (!container) return;

        const draft = String(this.nextHintDraft || '').trim();
        if (!draft) {
            container.innerHTML = '';
            return;
        }

        const preview = draft.length > 120 ? `${draft.slice(0, 120)}...` : draft;
        container.innerHTML = `
            <div class="ai-followup-hint-card">
                <div class="ai-followup-hint-copy">
                    <div class="ai-followup-hint-title">下一轮已附加待补提示</div>
                    <div class="ai-followup-hint-preview">${this._escapeHtml(preview)}</div>
                </div>
                <button class="ai-followup-hint-clear" type="button" id="ai-btn-clear-followup-hint">清除</button>
            </div>
        `;

        const clearButton = container.querySelector('#ai-btn-clear-followup-hint');
        if (clearButton) {
            clearButton.disabled = this.isGenerating;
            clearButton.addEventListener('click', () => {
                this.nextHintDraft = '';
                this._renderQueuedHintBanner();
                this._addMessage('system', '已清除下一轮附加提示。');
            });
        }
    }

    async _copyTextToClipboard(text) {
        const value = String(text || '').trim();
        if (!value) return false;

        try {
            if (navigator?.clipboard?.writeText) {
                await navigator.clipboard.writeText(value);
                return true;
            }
        } catch (error) {
            console.warn('[AiPanel] navigator.clipboard 写入失败，准备回退。', error);
        }

        try {
            const textArea = document.createElement('textarea');
            textArea.value = value;
            textArea.setAttribute('readonly', 'readonly');
            textArea.style.position = 'fixed';
            textArea.style.left = '-9999px';
            document.body.appendChild(textArea);
            textArea.select();
            const copied = document.execCommand('copy');
            document.body.removeChild(textArea);
            return copied;
        } catch (error) {
            console.warn('[AiPanel] execCommand 复制失败。', error);
            return false;
        }
    }

    _appendFollowupTextToInput(text) {
        const input = this.container?.querySelector('#ai-input');
        if (!input) return;

        const value = String(text || '').trim();
        if (!value) return;

        const current = String(input.value || '').trim();
        input.value = current ? `${current}\n\n${value}` : value;
        input.focus();
        input.style.height = 'auto';
        input.style.height = `${input.scrollHeight}px`;
    }

    _createGenerateRequestId() {
        const randomPart = Math.random().toString(36).slice(2, 8);
        return `gen-${Date.now()}-${randomPart}`;
    }

    _getGenerateRequestId(payload) {
        return String(payload?.requestId ?? payload?.RequestId ?? '').trim();
    }

    _shouldHandleGenerateRealtimePayload(payload) {
        const requestId = this._getGenerateRequestId(payload);
        if (!requestId) {
            return this.isGenerating;
        }

        return Boolean(this.activeGenerateRequestId) && requestId === this.activeGenerateRequestId;
    }

    _shouldHandleGenerateTerminalPayload(payload) {
        const requestId = this._getGenerateRequestId(payload);
        if (!requestId) {
            return this.isGenerating;
        }

        return Boolean(this.activeGenerateRequestId) && requestId === this.activeGenerateRequestId;
    }

    _normalizeGenerateStatus(payload) {
        return String(payload?.status ?? payload?.Status ?? '').trim().toLowerCase();
    }

    _isCancelledResult(payload) {
        const status = this._normalizeGenerateStatus(payload);
        const failureType = String(payload?.failureType ?? payload?.FailureType ?? '').trim().toLowerCase();

        return ['cancelled', 'canceled', 'user_cancelled', 'user_canceled'].includes(status)
            || ['user_cancelled', 'user_canceled'].includes(failureType);
    }

    _clearActiveRequestState() {
        this.activeGenerateRequestId = null;
        this.activeGenerateSessionId = null;
    }
    
    _clearResultPane() {
        const e1 = this.container.querySelector('#ai-result-reasoning'); if(e1) e1.textContent = '';
        const e2 = this.container.querySelector('#ai-result-thinking'); if(e2) e2.textContent = '';
        const e3 = this.container.querySelector('#ai-result-summary'); if(e3) e3.textContent = '';
        const e4 = this.container.querySelector('#ai-result-ops'); if(e4) e4.innerHTML = '';
        const e5 = this.container.querySelector('#ai-result-followups');
        if (e5) {
            e5.classList.add('is-empty');
            e5.innerHTML = '<div class="ai-followup-empty">当前没有待确认参数或缺失资源。</div>';
        }
        const e6 = this.container.querySelector('#ai-result-parameter-editor');
        if (e6) {
            e6.classList.add('is-empty');
            e6.innerHTML = '<div class="ai-followup-empty">当前没有待确认参数，暂无需补录。</div>';
        }
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
        this.nextHintDraft = '';
        this._resetPendingDraftState();
        this._resetCurrentResultSyncState();
        this.pendingParameterFilePickContext = null;
        this._clearActiveRequestState();
        this._clearResultPane();
        this._renderQueuedHintBanner();

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

        const canvasFlowRaw = session.currentCanvasFlowJson ?? session.CurrentCanvasFlowJson;
        const aiFlowRaw = session.currentFlowJson ?? session.CurrentFlowJson;
        const parsedCanvasFlow = this._parseFlowJson(canvasFlowRaw);
        const parsedAiFlow = this._parseFlowJson(aiFlowRaw);
        const parsedFlow = parsedCanvasFlow || parsedAiFlow;
        const canvasFlow = this._normalizeSessionFlowForCanvas(parsedCanvasFlow, sessionId);

        if (!canvasFlow && parsedAiFlow && !parsedCanvasFlow) {
            console.warn('[AiPanel] 历史会话仅包含 AI 原始结构，未包含画布快照，无法直接应用到画布。', {
                sessionId
            });
            this._addMessage('system', '该历史会话缺少可回放的画布快照，已恢复对话内容，但无法直接还原到当前画布。');
        }

        const followupSource = parsedAiFlow || parsedFlow;
        const restoredResult = {
            flow: canvasFlow || parsedFlow || null,
            aiExplanation: parsedAiFlow?.explanation || parsedAiFlow?.Explanation ||
                parsedFlow?.explanation || parsedFlow?.Explanation || '--',
            reasoning: parsedAiFlow?.reasoning || parsedAiFlow?.Reasoning || '',
            recommendedTemplate: followupSource?.recommendedTemplate ?? followupSource?.RecommendedTemplate ?? null,
            pendingParameters: followupSource?.pendingParameters ?? followupSource?.PendingParameters ?? [],
            missingResources: followupSource?.missingResources ?? followupSource?.MissingResources ?? [],
            sessionId
        };

        if (canvasFlow) {
            this._setCurrentResult(restoredResult);
            this._rebuildPendingOperatorBindings({
                pending: restoredResult?.pendingParameters,
                flow: restoredResult?.flow,
                sourceFlow: followupSource,
                preferIndexFallback: true
            });
        } else {
            this._resetCurrentResultSyncState();
        }
        this._displayResult(restoredResult, { appendChatMessage: false });

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

    _parseFlowJson(raw) {
        if (!raw) return null;
        if (typeof raw === 'object') return raw;
        if (typeof raw !== 'string') return null;
        try {
            return JSON.parse(raw);
        } catch (error) {
            console.warn('[AiPanel] 解析会话 flow JSON 失败。', {
                rawLength: raw.length,
                error: error?.message || String(error)
            });
            return null;
        }
    }

    _extractOperators(flow) {
        if (!flow) return [];
        if (Array.isArray(flow.operators)) return flow.operators;
        if (Array.isArray(flow.Operators)) return flow.Operators;
        return [];
    }

    _extractConnections(flow) {
        if (!flow) return [];
        if (Array.isArray(flow.connections)) return flow.connections;
        if (Array.isArray(flow.Connections)) return flow.Connections;
        return [];
    }

    _isCanvasFlowLike(flow) {
        if (!flow || typeof flow !== 'object') return false;
        const operators = this._extractOperators(flow);
        const connections = this._extractConnections(flow);
        if (!Array.isArray(operators) || !Array.isArray(connections)) {
            return false;
        }

        const hasValue = (value) => {
            if (value === null || value === undefined) return false;
            if (typeof value === 'string') return value.trim().length > 0;
            return true;
        };

        if (operators.length === 0) {
            return connections.length === 0;
        }

        const hasOperatorId = operators.every(op => {
            const id = op?.id ?? op?.Id;
            const type = op?.type ?? op?.Type;
            return hasValue(id) && hasValue(type);
        });
        if (!hasOperatorId) return false;

        return connections.every(conn => {
            const source = conn?.sourceOperatorId ?? conn?.SourceOperatorId ?? conn?.source;
            const target = conn?.targetOperatorId ?? conn?.TargetOperatorId ?? conn?.target;
            return hasValue(source) && hasValue(target);
        });
    }

    _normalizeSessionFlowForCanvas(flow, sessionId = '') {
        if (!flow || typeof flow !== 'object') return null;
        if (this._isCanvasFlowLike(flow)) {
            return flow;
        }
        console.warn('[AiPanel] 历史会话中的 flow 不是可直接反序列化的画布结构，已跳过应用兜底。', {
            sessionId,
            flowKeys: Object.keys(flow || {})
        });
        return null;
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
