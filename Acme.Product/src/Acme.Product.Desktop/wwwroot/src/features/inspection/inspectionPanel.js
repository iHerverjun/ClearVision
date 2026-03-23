/**
 * 检测控制面板组件
 * 提供检测视图的完整控制界面，包括运行控制、状态显示、统计信息等功能
 */
console.log('[InspectionPanel] 模块已加载');

import inspectionController, { getInspectionState } from './inspectionController.js';
import httpClient from '../../core/messaging/httpClient.js';
import { createSignal } from '../../core/state/store.js';
import { getCurrentProject } from '../project/projectManager.js';
import { AnalysisCardsPanel, buildDiagnosticsAnalysisData } from './analysisCardsPanel.js';
import { showToast } from '../../shared/components/uiComponents.js';

// 检测统计状态
const [getStats, setStats, subscribeStats] = createSignal({
    total: 0,
    ok: 0,
    ng: 0,
    yield: 0
});

// 检测耗时统计
const [getTimingStats, setTimingStats, subscribeTimingStats] = createSignal({
    avg: 0,
    min: Infinity,
    max: 0,
    history: []
});

// 最近检测结果
const [getRecentResults, setRecentResults, subscribeRecentResults] = createSignal([]);

// 相机列表
const [getCameraList, setCameraList, subscribeCameraList] = createSignal([]);

class InspectionPanel {
    constructor(containerId) {
        this.container = document.getElementById(containerId);
        if (!this.container) {
            throw new Error(`找不到检测控制面板容器: ${containerId}`);
        }
        
        this.panelId = containerId;
        this.selectedCamera = null;
        this.isContinuous = false;
        this.selectedRunMode = 'camera';
        this.runtimeConfig = {
            autoRun: false,
            stopOnConsecutiveNg: 0,
            missingMaterialTimeoutSeconds: 30,
            applyProtectionRules: true
        };
        this.consecutiveNgCount = 0;
        this._autoRunTriggered = false;
        this._materialTimeoutHandle = null;
        this._materialTimeoutDeadline = null;
        this._lastProtectionReason = '';
        this._lastProtectionMessage = '';

        // 初始化 UI 和分析卡片面板
        this.initialize();
        this.analysisCardsPanel = new AnalysisCardsPanel('analysis-cards-container');
        this.syncAnalysisFlowContext();
        
        // 设置订阅
        this.setupSubscriptions();
        this.loadRuntimeConfig();
        
        console.log('[InspectionPanel] 检测控制面板初始化完成');
    }

    isProtectionEnabled() {
        return this.runtimeConfig?.applyProtectionRules !== false;
    }

    getMissingMaterialTimeoutMs() {
        const timeoutSeconds = Number(this.runtimeConfig?.missingMaterialTimeoutSeconds ?? 0);
        if (!Number.isFinite(timeoutSeconds) || timeoutSeconds <= 0) {
            return 0;
        }

        return timeoutSeconds * 1000;
    }

    getContinuousRunStatusText() {
        if (!this.isProtectionEnabled()) {
            return '连续运行中...';
        }

        const timeoutSeconds = Number(this.runtimeConfig?.missingMaterialTimeoutSeconds ?? 0);
        if (!Number.isFinite(timeoutSeconds) || timeoutSeconds <= 0) {
            return '连续运行中...';
        }

        return `连续运行中（运行保护已开启，${timeoutSeconds} 秒未等到新结果将自动停止）`;
    }

    getProtectionSummaryText() {
        if (!this.isProtectionEnabled()) {
            return '运行保护已关闭：连续运行不会因缺料超时或连续 NG 自动停止。';
        }

        const parts = [];
        const timeoutSeconds = Number(this.runtimeConfig?.missingMaterialTimeoutSeconds ?? 0);
        const stopThreshold = Number(this.runtimeConfig?.stopOnConsecutiveNg ?? 0);

        if (Number.isFinite(timeoutSeconds) && timeoutSeconds > 0) {
            parts.push(`${timeoutSeconds} 秒未等到新结果会自动停止连续运行`);
        }

        if (Number.isFinite(stopThreshold) && stopThreshold > 0) {
            parts.push(`连续 NG 达到 ${stopThreshold} 次会自动停止`);
        }

        if (parts.length === 0) {
            return '运行保护已开启，但当前未配置自动停机阈值。';
        }

        return `运行保护已开启：${parts.join('；')}。`;
    }

    getActiveFlowDefinition() {
        try {
            const canvasFlow = window.flowCanvas?.serialize?.();
            const operators = canvasFlow?.operators || canvasFlow?.Operators;
            if (Array.isArray(operators) && operators.length > 0) {
                return canvasFlow;
            }
        } catch (error) {
            console.warn('[InspectionPanel] Failed to serialize flow canvas for analysis context:', error);
        }

        return getCurrentProject()?.flow || null;
    }

    syncAnalysisFlowContext() {
        if (!this.analysisCardsPanel) {
            return;
        }

        this.analysisCardsPanel.setFlowContext(this.getActiveFlowDefinition());
    }

    updateProtectionNotice(message = '', tone = 'info') {
        const summaryEl = this.container?.querySelector('#protection-summary');
        const statusEl = this.container?.querySelector('#protection-status');

        if (summaryEl) {
            summaryEl.textContent = this.getProtectionSummaryText();
        }

        if (statusEl) {
            const runtimeMessage = message || this._lastProtectionMessage || '待机中。启动连续运行后，这里会持续显示保护监控状态。';
            statusEl.textContent = runtimeMessage;
            statusEl.dataset.tone = tone;
            statusEl.style.color = tone === 'warning'
                ? '#b45309'
                : (tone === 'error' ? '#b91c1c' : 'var(--text-secondary)');
        }
    }

    armProtectionWatchdog(reason = '等待检测结果') {
        this.clearProtectionWatchdog();

        if (!this.isProtectionEnabled()) {
            return;
        }

        const timeoutMs = this.getMissingMaterialTimeoutMs();
        if (timeoutMs <= 0) {
            return;
        }

        this._lastProtectionReason = reason;
        this._materialTimeoutDeadline = Date.now() + timeoutMs;
        const timeoutSeconds = Math.round(timeoutMs / 1000);
        this._lastProtectionMessage = `${reason}，运行保护监控中；若 ${timeoutSeconds} 秒内没有新结果，将自动停止连续运行。`;
        this.updateProtectionNotice(this._lastProtectionMessage, 'info');
        this._materialTimeoutHandle = window.setTimeout(() => {
            this.handleProtectionTimeout(reason);
        }, timeoutMs);
    }

    clearProtectionWatchdog() {
        if (this._materialTimeoutHandle) {
            window.clearTimeout(this._materialTimeoutHandle);
            this._materialTimeoutHandle = null;
        }

        this._materialTimeoutDeadline = null;
        this._lastProtectionReason = '';
    }

    async handleProtectionTimeout(reason) {
        this._materialTimeoutHandle = null;

        if (!this.isProtectionEnabled()) {
            return;
        }

        const timeoutSeconds = Number(this.runtimeConfig?.missingMaterialTimeoutSeconds ?? 0);
        const message = `${reason}超时（${timeoutSeconds} 秒），已触发运行保护`;
        const detailMessage = `${message}。系统将其解释为“在约定时间内没有等到新的检测结果”，请优先检查上料、触发链路、相机连接或 PLC 信号，而不是把它当成程序无响应。`;
        const shouldStopContinuous = this.isContinuous;

        console.warn('[InspectionPanel] 运行保护超时触发:', {
            reason,
            timeoutSeconds,
            runMode: this.selectedRunMode,
            isContinuous: this.isContinuous
        });

        this.updateStatus('error', message);
        this._lastProtectionMessage = detailMessage;
        this.updateProtectionNotice(detailMessage, 'warning');
        showToast(`${message}${shouldStopContinuous ? '，连续运行已保护性停止' : ''}`, 'warning');

        if (shouldStopContinuous) {
            await this.handleStop();
            this.updateStatus('error', `${message}，请检查上料、触发链路或相机状态`);
            return;
        }

        this.setButtonsState(false);
    }

    async handleRunSingle() {
        try {
            this.syncAnalysisFlowContext();
            this.updateStatus('running', '运行中...');
            this.setButtonsState(true);
            this.updateProtectionNotice('单次运行中。若长时间没有返回结果，界面会在这里解释触发了哪条保护规则。', 'info');
            this.armProtectionWatchdog('等待单次检测结果');

            const project = getCurrentProject();
            if (project) {
                inspectionController.setProject(project.id);
            }

            await inspectionController.executeSingle();
        } catch (error) {
            this.clearProtectionWatchdog();
            console.error('[InspectionPanel] 单次运行失败:', error);
            this.updateStatus('error', '运行失败');
            this.setButtonsState(false);
        }
    }

    async handleRunContinuous() {
        try {
            this.syncAnalysisFlowContext();
            this.isContinuous = true;
            this.consecutiveNgCount = 0;
            this.updateStatus('running', this.getContinuousRunStatusText());
            this.setButtonsState(true);
            this.updateProtectionNotice('连续运行已启动，正在等待首个结果。', 'info');
            this.armProtectionWatchdog('等待连续运行结果');

            const project = getCurrentProject();
            if (project) {
                inspectionController.setProject(project.id);
            }

            const runMode = this.selectedRunMode || 'camera';
            console.log('[InspectionPanel] 启动连续检测，模式:', runMode);

            if (runMode === 'flow') {
                await inspectionController.startRealtimeFlowMode();
            } else {
                await inspectionController.startRealtime();
            }
        } catch (error) {
            this.clearProtectionWatchdog();
            console.error('[InspectionPanel] 连续运行失败:', error);
            this.updateStatus('error', '启动失败');
            this.setButtonsState(false);
            this.isContinuous = false;
        }
    }

    async handleStop() {
        try {
            this.clearProtectionWatchdog();
            if (this.isContinuous) {
                await inspectionController.stopRealtime();
                this.isContinuous = false;
            }
            this.consecutiveNgCount = 0;
            this.updateStatus('idle', '已停止');
            this.setButtonsState(false);
            this._lastProtectionMessage = '连续运行已停止。当前未在执行保护监控。';
            this.updateProtectionNotice(this._lastProtectionMessage, 'info');
        } catch (error) {
            console.error('[InspectionPanel] 停止失败:', error);
        }
    }

    handleInspectionResult(result) {
        this.clearProtectionWatchdog();

        const analysisPayload = this.getAnalysisPayload(result);
        if (this.analysisCardsPanel) {
            this.syncAnalysisFlowContext();
            if (analysisPayload) {
                this.analysisCardsPanel.updateCards(analysisPayload, result.status, result.processingTimeMs);
            } else {
                this.analysisCardsPanel.clear();
            }
        }

        const status = result.status === 'OK' ? 'ok' : result.status === 'Error' ? 'error' : 'ng';
        const statusText = result.status === 'OK' ? '通过' : result.status === 'Error' ? '错误' : '不通过';
        this.updateStatus(status, statusText);

        const stats = getStats();
        const newStats = {
            total: stats.total + 1,
            ok: stats.ok + (result.status === 'OK' ? 1 : 0),
            ng: stats.ng + (result.status !== 'OK' && result.status !== 'Error' ? 1 : 0)
        };
        newStats.yield = newStats.total > 0 ? ((newStats.ok / newStats.total) * 100).toFixed(1) : 0;
        setStats(newStats);

        if (result.processingTimeMs) {
            const timing = getTimingStats();
            const newHistory = [...timing.history, result.processingTimeMs].slice(-10);
            const avg = newHistory.reduce((a, b) => a + b, 0) / newHistory.length;
            setTimingStats({
                avg: Math.round(avg),
                min: Math.min(...newHistory),
                max: Math.max(...newHistory),
                history: newHistory
            });
        }

        this.updateCounters();
        this.setButtonsState(this.isContinuous);

        if (result.status === 'NG') {
            this.consecutiveNgCount += 1;
        } else {
            this.consecutiveNgCount = 0;
        }

        const stopThreshold = Number(this.runtimeConfig?.stopOnConsecutiveNg ?? 0);
        if (this.isContinuous && stopThreshold > 0 && this.consecutiveNgCount >= stopThreshold) {
            const thresholdMessage = `连续 NG 达到阈值 (${stopThreshold})，系统已按运行保护规则自动停止连续运行。请检查工件质量、阈值配置或上游触发条件。`;
            this._lastProtectionMessage = thresholdMessage;
            this.updateProtectionNotice(thresholdMessage, 'warning');
            showToast(`连续 NG 达到阈值 (${stopThreshold})，已自动停止`, 'warning');
            this.handleStop();
            return;
        }

        this.addRecentResult(result);

        if (this.isContinuous) {
            this.armProtectionWatchdog('等待下一次触发结果');
        } else {
            this._lastProtectionMessage = '最近一次检测已完成，当前未在连续运行。';
            this.updateProtectionNotice(this._lastProtectionMessage, 'info');
        }
    }

    async loadRuntimeConfig() {
        try {
            const settings = await httpClient.get('/settings');
            const runtime = settings?.runtime || {};
            this.runtimeConfig = {
                autoRun: !!runtime.autoRun,
                stopOnConsecutiveNg: Math.max(0, Number(runtime.stopOnConsecutiveNg || 0)),
                missingMaterialTimeoutSeconds: Math.max(0, Number(runtime.missingMaterialTimeoutSeconds || 0)),
                applyProtectionRules: runtime.applyProtectionRules !== false
            };
        } catch (error) {
            console.warn('[InspectionPanel] 加载运行时配置失败:', error);
            this.runtimeConfig = {
                autoRun: false,
                stopOnConsecutiveNg: 0,
                missingMaterialTimeoutSeconds: 30,
                applyProtectionRules: true
            };
        }

        this.updateProtectionNotice();
        this.tryAutoRunIfNeeded();
    }

    /**
     * 初始化面板UI
     */
    initialize() {
        this.container.innerHTML = `
            <div class="inspection-panel">
                <!-- 运行控制条 -->
                <div class="inspection-section run-controls cv-control-card">
                    <h4 class="section-title">运行控制</h4>
                    
                    <!-- 【第二优先级】运行模式选择 -->
                    <div class="run-mode-section" style="margin-bottom: 12px;">
                        <label class="form-label" style="display: block; margin-bottom: 6px; font-size: 12px; color: var(--text-secondary);">运行模式</label>
                        <select id="run-mode" class="form-select cv-input" style="width: 100%; border-radius: 6px;">
                            <option value="camera">相机驱动 (Camera)</option>
                            <option value="flow">流程驱动 (PLC触发)</option>
                        </select>
                        <small style="display: block; margin-top: 4px; font-size: 11px; color: var(--text-secondary);">
                            <span id="run-mode-desc">相机驱动：由相机采集触发检测</span>
                        </small>
                    </div>
                    
                    <div class="control-buttons">
                        <button class="btn-inspection btn-run-single" id="btn-run-single" title="单次运行">
                            <svg class="icon" viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                <circle cx="12" cy="12" r="10"></circle><polygon points="10 8 16 12 10 16 10 8"></polygon>
                            </svg>
                            <span>单次运行</span>
                        </button>
                        <button class="btn-inspection btn-run-continuous" id="btn-run-continuous" title="连续运行">
                            <svg class="icon" viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                <polyline points="15 14 20 9 15 4"></polyline><path d="M4 20v-7a4 4 0 0 1 4-4h12"></path>
                            </svg>
                            <span>连续运行</span>
                        </button>
                        <button class="btn-inspection btn-stop" id="btn-stop" title="停止" disabled>
                            <svg class="icon" viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                <rect x="3" y="3" width="18" height="18" rx="2" ry="2"></rect>
                            </svg>
                            <span>停止</span>
                        </button>
                    </div>
                    <div style="margin-top:14px; padding:12px; border-radius:10px; background:#fff7ed; border:1px solid #fed7aa;">
                        <div style="font-size:12px; font-weight:600; color:#9a3412; margin-bottom:6px;">运行保护说明</div>
                        <div id="protection-summary" style="font-size:12px; line-height:1.5; color:#7c2d12;"></div>
                        <div id="protection-status" style="margin-top:8px; font-size:12px; line-height:1.5; color:var(--text-secondary);"></div>
                    </div>
                </div>

                <!-- 实时计数器 (上移至此) -->
                <div class="inspection-section counters-section cv-control-card">
                    <h4 class="section-title">检测统计</h4>
                    <div class="counters-grid">
                        <div class="counter-item counter-ok">
                            <span class="counter-value" id="counter-ok">0</span>
                            <span class="counter-label">OK</span>
                        </div>
                        <div class="counter-item counter-ng">
                            <span class="counter-value" id="counter-ng">0</span>
                            <span class="counter-label">NG</span>
                        </div>
                        <div class="counter-item counter-total">
                            <span class="counter-value" id="counter-total">0</span>
                            <span class="counter-label">总计</span>
                        </div>
                        <div class="counter-item counter-yield">
                            <span class="counter-value" id="counter-yield">0%</span>
                            <span class="counter-label">良率</span>
                        </div>
                    </div>
                </div>

                <!-- 耗时统计 (移至左侧) -->
                <div class="inspection-section timing-section cv-control-card">
                    <h4 class="section-title">耗时统计 (CYCLE TIME)</h4>
                    <div class="timing-stats" id="timing-stats-panel">
                        <div class="timing-item">
                            <span class="timing-label">平均</span>
                            <span class="timing-value" id="timing-avg">-- ms</span>
                        </div>
                        <div class="timing-item">
                            <span class="timing-label">最小</span>
                            <span class="timing-value" id="timing-min">-- ms</span>
                        </div>
                        <div class="timing-item">
                            <span class="timing-label">最大</span>
                            <span class="timing-value" id="timing-max">-- ms</span>
                        </div>
                    </div>
                </div>
            </div>
        `;
        
        // 绑定事件
        this.bindEvents();
    }
    
    /**
     * 绑定事件处理
     */
    bindEvents() {
        console.warn('[InspectionPanel] !!! 正在进行事件绑定 !!!');
        console.log('[InspectionPanel] 正在绑定事件，容器:', this.container);
        // 单次运行按钮
        const runSingleBtn = this.container.querySelector('#btn-run-single');
        console.log('[InspectionPanel] btn-run-single 查找结果:', runSingleBtn);
        if (runSingleBtn) {
            runSingleBtn.addEventListener('click', (e) => {
                console.warn('[InspectionPanel] btn-run-single 点击触发!');
                this.handleRunSingle();
            });
        }
        
        // 连续运行按钮
        const runContinuousBtn = this.container.querySelector('#btn-run-continuous');
        console.log('[InspectionPanel] btn-run-continuous 查找结果:', runContinuousBtn);
        if (runContinuousBtn) {
            runContinuousBtn.addEventListener('click', () => {
                console.log('[InspectionPanel] btn-run-continuous 点击');
                this.handleRunContinuous();
            });
        }
        
        // 停止按钮
        const stopBtn = this.container.querySelector('#btn-stop');
        console.log('[InspectionPanel] btn-stop 查找结果:', stopBtn);
        if (stopBtn) {
            stopBtn.addEventListener('click', () => {
                console.log('[InspectionPanel] btn-stop 点击');
                this.handleStop();
            });
        }
        
        // 【第二优先级】运行模式选择
        const runModeSelect = this.container.querySelector('#run-mode');
        if (runModeSelect) {
            runModeSelect.addEventListener('change', (e) => {
                this.selectedRunMode = e.target.value;
                console.log('[InspectionPanel] 运行模式切换为:', this.selectedRunMode);
                
                // 更新描述文本
                const descEl = this.container.querySelector('#run-mode-desc');
                if (descEl) {
                    descEl.textContent = this.selectedRunMode === 'flow' 
                        ? '流程驱动：流程内PLC读取算子等待触发信号'
                        : '相机驱动：由相机采集触发检测';
                }
            });
        }
    }
    
    /**
     * 设置状态订阅
     */
    setupSubscriptions() {
        // 订阅检测状态变化（调用方法注册回调，而非覆盖方法）
        this.unsubscribeCompleted = inspectionController.onInspectionCompleted((result) => {
            this.handleInspectionResult(result);
        });
        
        this.unsubscribeError = inspectionController.onInspectionError((error) => {
            this.clearProtectionWatchdog();
            this.updateStatus('error', '检测错误');
        });
    }
    

    
    /*
    /**
     * 处理单次运行
     */
    async _legacyHandleRunSingleDuplicate2() {
        try {
            this.updateStatus('running', '运行中...');
            this.setButtonsState(true);
            
            this.armProtectionWatchdog('等待单次检测结果');

            const project = getCurrentProject();
            if (project) {
                inspectionController.setProject(project.id);
            }
            
            await inspectionController.executeSingle();
        } catch (error) {
            console.error('[InspectionPanel] 单次运行失败:', error);
            this.updateStatus('error', '运行失败');
            this.setButtonsState(false);
        }
    }
    
    /**
     * 处理连续运行
     */
    async _legacyHandleRunContinuousDuplicate2() {
        try {
            this.isContinuous = true;
            this.consecutiveNgCount = 0;
            this.armProtectionWatchdog('等待连续运行结果');
            this.updateStatus('running', this.getContinuousRunStatusText());
            this.updateStatus('running', '连续运行中...');
            this.setButtonsState(true);
            this.armProtectionWatchdog('等待单次检测结果');
            
            const project = getCurrentProject();
            if (project) {
                inspectionController.setProject(project.id);
            }
            
            // 根据运行模式选择启动方式
            const runMode = this.selectedRunMode || 'camera';
            console.log('[InspectionPanel] 启动连续检测，模式:', runMode);
            
            if (runMode === 'flow') {
                // 流程驱动模式：不强制要求相机，由流程内部控制
                await inspectionController.startRealtimeFlowMode();
            } else {
                // 相机驱动模式：原有逻辑
                await inspectionController.startRealtime();
            }
        } catch (error) {
            console.error('[InspectionPanel] 连续运行失败:', error);
            this.updateStatus('error', '启动失败');
            this.setButtonsState(false);
            this.isContinuous = false;
        }
    }
    
    /**
     * 处理停止
     */
    async _legacyHandleStopDuplicate2() {
        try {
            if (this.isContinuous) {
                await inspectionController.stopRealtime();
                this.isContinuous = false;
            }
            this.consecutiveNgCount = 0;
            this.updateStatus('idle', '已停止');
            this.setButtonsState(false);

        } catch (error) {
            console.error('[InspectionPanel] 停止失败:', error);
        }
    }
    
    /**
     * 处理检测结果
     */
    _legacyHandleInspectionResultDuplicate2(result) {
        // 更新卡片分析
        const analysisPayload = this.getAnalysisPayload(result);
        if (this.analysisCardsPanel && analysisPayload) {
            this.analysisCardsPanel.updateCards(analysisPayload, result.status, result.processingTimeMs);
        }

        // 更新状态
        const status = result.status === 'OK' ? 'ok' : result.status === 'Error' ? 'error' : 'ng';
        const statusText = result.status === 'OK' ? '通过' : result.status === 'Error' ? '错误' : '不通过';
        this.updateStatus(status, statusText);
        
        // 更新统计
        const stats = getStats();
        const newStats = {
            total: stats.total + 1,
            ok: stats.ok + (result.status === 'OK' ? 1 : 0),
            ng: stats.ng + (result.status !== 'OK' && result.status !== 'Error' ? 1 : 0)
        };
        newStats.yield = newStats.total > 0 ? ((newStats.ok / newStats.total) * 100).toFixed(1) : 0;
        setStats(newStats);
        
        // 更新耗时统计
        if (result.processingTimeMs) {
            const timing = getTimingStats();
            const newHistory = [...timing.history, result.processingTimeMs].slice(-10);
            const avg = newHistory.reduce((a, b) => a + b, 0) / newHistory.length;
            setTimingStats({
                avg: Math.round(avg),
                min: Math.min(...newHistory),
                max: Math.max(...newHistory),
                history: newHistory
            });
        }
        
        // 【修复】立即更新计数器显示，解决延迟问题
        this.updateCounters();
        
        // 更新按钮状态
        this.setButtonsState(false);

        // 运行时消费：连续 NG 阈值达到时自动停机
        if (result.status === 'NG') {
            this.consecutiveNgCount += 1;
        } else {
            this.consecutiveNgCount = 0;
        }

        const stopThreshold = Number(this.runtimeConfig?.stopOnConsecutiveNg ?? 0);
        if (this.isContinuous && stopThreshold > 0 && this.consecutiveNgCount >= stopThreshold) {
            showToast(`连续NG达到阈值(${stopThreshold})，已自动停止`, 'warning');
            this.handleStop();
        }
        
        // 添加到最近结果
        this.addRecentResult(result);
    }
    
    /**
     * 更新状态显示
     */
    updateStatus(status, text) {
        console.log(`[InspectionPanel] 更新状态: ${status} - ${text}`);
        const statusTextEl = document.getElementById('status-text');
        if (statusTextEl) {
            statusTextEl.textContent = text;
            statusTextEl.className = `status-text status-${status}`;
        }
    }
    

    
    /**
     * 更新计数器显示
     */
    updateCounters() {
        const stats = getStats();
        const timing = getTimingStats();
        
        const okEl = this.container.querySelector('#counter-ok');
        const ngEl = this.container.querySelector('#counter-ng');
        const totalEl = this.container.querySelector('#counter-total');
        const yieldEl = this.container.querySelector('#counter-yield');
        
        if (okEl) okEl.textContent = stats.ok;
        if (ngEl) ngEl.textContent = stats.ng;
        if (totalEl) totalEl.textContent = stats.total;
        if (yieldEl) yieldEl.textContent = `${stats.yield}%`;
        
        // 更新耗时（移至右侧面板，使用 document.getElementById 全局查找）
        const avgEl = document.getElementById('timing-avg');
        const minEl = document.getElementById('timing-min');
        const maxEl = document.getElementById('timing-max');
        
        if (avgEl) avgEl.textContent = timing.avg > 0 ? `${timing.avg} ms` : '-- ms';
        if (minEl) minEl.textContent = timing.min !== Infinity ? `${timing.min} ms` : '-- ms';
        if (maxEl) maxEl.textContent = timing.max > 0 ? `${timing.max} ms` : '-- ms';
    }
    
    /**
     * 设置按钮状态
     */
    setButtonsState(isRunning) {
        const runSingleBtn = this.container.querySelector('#btn-run-single');
        const runContinuousBtn = this.container.querySelector('#btn-run-continuous');
        const stopBtn = this.container.querySelector('#btn-stop');
        
        if (runSingleBtn) {
            runSingleBtn.disabled = isRunning;
            runSingleBtn.classList.toggle('disabled', isRunning);
        }
        
        if (runContinuousBtn) {
            runContinuousBtn.disabled = isRunning;
            runContinuousBtn.classList.toggle('disabled', isRunning);
        }
        
        if (stopBtn) {
            stopBtn.disabled = !isRunning;
            stopBtn.classList.toggle('disabled', !isRunning);
        }
    }
    
    /**
     * 添加最近结果
     */
    addRecentResult(result) {
        const recent = getRecentResults();
        const newResult = {
            id: Date.now(),
            status: result.status,
            timestamp: new Date().toLocaleTimeString(),
            imageData: result.outputImage || result.outputImageBase64 || result.imageData,
            outputData: result.outputData || {},
            analysisData: result.analysisData || null
        };
        
        const updated = [newResult, ...recent].slice(0, 5);
        setRecentResults(updated);
        
        this.renderRecentResults();
    }
    
    /**
     * 渲染最近结果
     */
    renderRecentResults() {
        // 最近结果移至右侧面板，使用 document.getElementById 全局查找
        const grid = document.getElementById('inspection-recent-results-grid');
        if (!grid) return;
        
        const recent = getRecentResults();
        
        grid.innerHTML = recent.map(result => {
            // 提取文本摘要（OCR/条码等输出的文本数据）
            const textPreview = this.extractTextPreview(result.analysisData);
            
            return `
            <div class="recent-result-item ${result.status === 'OK' ? 'result-ok' : 'result-ng'}" data-id="${result.id}">
                <div class="result-thumb">
                    ${result.imageData 
                        ? `<img src="data:image/png;base64,${result.imageData}" alt="检测结果">`
                        : `<div class="result-placeholder"></div>`
                    }
                </div>
                ${textPreview ? `<div class="result-text-preview" title="${this.escapeHtml(textPreview)}">${this.escapeHtml(textPreview.substring(0, 20))}${textPreview.length > 20 ? '...' : ''}</div>` : ''}
                <div class="result-badge">${result.status}</div>
                <div class="result-time">${result.timestamp}</div>
            </div>
        `}).join('');
        
        // 添加点击事件
        grid.querySelectorAll('.recent-result-item').forEach(item => {
            item.addEventListener('click', () => {
                const id = parseInt(item.dataset.id);
                const result = getRecentResults().find(r => r.id === id);
                if (result && result.imageData) {
                    const imageData = `data:image/png;base64,${result.imageData}`;
                    if (window.inspectionImageViewer) {
                        window.inspectionImageViewer.loadImage(imageData);
                    } else if (window.imageViewer) {
                        window.imageViewer.loadImage(imageData);
                    }
                }
            });
        });
    }
    
    /**
     * 重置统计
     */
    reset() {
        setStats({ total: 0, ok: 0, ng: 0, yield: 0 });
        setTimingStats({ avg: 0, min: Infinity, max: 0, history: [] });
        setRecentResults([]);
        this.updateCounters();
        this.renderRecentResults();
        if (this.analysisCardsPanel) {
            this.syncAnalysisFlowContext();
            this.analysisCardsPanel.clear();
        }

        this.updateStatus('idle', '就绪');
    }
    
    /**
     * 从输出数据中提取文本摘要
     */
    extractTextPreview(analysisData) {
        if (analysisData && Array.isArray(analysisData.cards)) {
            for (const card of analysisData.cards) {
                const fields = Array.isArray(card?.fields) ? card.fields : [];
                for (const field of fields) {
                    if ((typeof field?.value === 'string' || typeof field?.value === 'number') && field.value !== '') {
                        return field.label ? `${field.label}: ${field.value}` : String(field.value);
                    }
                }
            }
        }

        return null;
    }

    getAnalysisPayload(result) {
        if (!result || typeof result !== 'object') {
            return null;
        }

        const explicitAnalysis = result.analysisData || result.AnalysisData || null;
        if (explicitAnalysis && Array.isArray(explicitAnalysis.cards) && explicitAnalysis.cards.length > 0) {
            return explicitAnalysis;
        }

        return buildDiagnosticsAnalysisData(result.outputData || result.OutputData, result.status || result.Status || 'OK') || null;
    }
    
    /**
     * HTML转义
     */
    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
    
    /**
     * 刷新显示
     */
    refresh() {
        this.updateCounters();
        this.renderRecentResults();
        this.tryAutoRunIfNeeded();
    }

    async _legacyLoadRuntimeConfigDuplicate2() {
        try {
            const settings = await httpClient.get('/settings');
            const runtime = settings?.runtime || {};
            this.runtimeConfig = {
                autoRun: !!runtime.autoRun,
                stopOnConsecutiveNg: Math.max(0, Number(runtime.stopOnConsecutiveNg || 0))
            };
        } catch (error) {
            console.warn('[InspectionPanel] 加载运行时配置失败:', error);
            this.runtimeConfig = { autoRun: false, stopOnConsecutiveNg: 0 };
        }

        this.tryAutoRunIfNeeded();
    }

    tryAutoRunIfNeeded() {
        if (!this.runtimeConfig?.autoRun || this._autoRunTriggered || this.isContinuous) {
            return;
        }

        const project = getCurrentProject();
        if (!project) {
            return;
        }

        // 优先使用流程驱动，避免未选择相机时自动启动失败
        if (!inspectionController.cameraId) {
            this.selectedRunMode = 'flow';
            const runModeSelect = this.container.querySelector('#run-mode');
            const descEl = this.container.querySelector('#run-mode-desc');
            if (runModeSelect) {
                runModeSelect.value = 'flow';
            }
            if (descEl) {
                descEl.textContent = '流程驱动：流程内PLC读取算子等待触发信号';
            }
        }

        this._autoRunTriggered = true;
        this.handleRunContinuous().catch((error) => {
            console.warn('[InspectionPanel] AutoRun 启动失败:', error);
            this._autoRunTriggered = false;
        });
    }

    async _legacyHandleRunSingleDuplicate() {
        try {
            this.updateStatus('running', '运行中...');
            this.setButtonsState(true);
            this.armProtectionWatchdog('等待单次检测结果');

            const project = getCurrentProject();
            if (project) {
                inspectionController.setProject(project.id);
            }

            await inspectionController.executeSingle();
        } catch (error) {
            this.clearProtectionWatchdog();
            console.error('[InspectionPanel] 单次运行失败:', error);
            this.updateStatus('error', '运行失败');
            this.setButtonsState(false);
        }
    }

    async _legacyHandleRunContinuousDuplicate() {
        try {
            this.isContinuous = true;
            this.consecutiveNgCount = 0;
            this.updateStatus('running', this.getContinuousRunStatusText());
            this.setButtonsState(true);
            this.armProtectionWatchdog('等待连续运行结果');

            const project = getCurrentProject();
            if (project) {
                inspectionController.setProject(project.id);
            }

            const runMode = this.selectedRunMode || 'camera';
            console.log('[InspectionPanel] 启动连续检测，模式:', runMode);

            if (runMode === 'flow') {
                await inspectionController.startRealtimeFlowMode();
            } else {
                await inspectionController.startRealtime();
            }
        } catch (error) {
            this.clearProtectionWatchdog();
            console.error('[InspectionPanel] 连续运行失败:', error);
            this.updateStatus('error', '启动失败');
            this.setButtonsState(false);
            this.isContinuous = false;
        }
    }

    async _legacyHandleStopDuplicate() {
        try {
            this.clearProtectionWatchdog();
            if (this.isContinuous) {
                await inspectionController.stopRealtime();
                this.isContinuous = false;
            }
            this.consecutiveNgCount = 0;
            this.updateStatus('idle', '已停止');
            this.setButtonsState(false);
        } catch (error) {
            console.error('[InspectionPanel] 停止失败:', error);
        }
    }

    _legacyHandleInspectionResultDuplicate(result) {
        this.clearProtectionWatchdog();

        const analysisPayload = this.getAnalysisPayload(result);
        if (this.analysisCardsPanel && analysisPayload) {
            this.analysisCardsPanel.updateCards(analysisPayload, result.status, result.processingTimeMs);
        }

        const status = result.status === 'OK' ? 'ok' : result.status === 'Error' ? 'error' : 'ng';
        const statusText = result.status === 'OK' ? '通过' : result.status === 'Error' ? '错误' : '不通过';
        this.updateStatus(status, statusText);

        const stats = getStats();
        const newStats = {
            total: stats.total + 1,
            ok: stats.ok + (result.status === 'OK' ? 1 : 0),
            ng: stats.ng + (result.status !== 'OK' && result.status !== 'Error' ? 1 : 0)
        };
        newStats.yield = newStats.total > 0 ? ((newStats.ok / newStats.total) * 100).toFixed(1) : 0;
        setStats(newStats);

        if (result.processingTimeMs) {
            const timing = getTimingStats();
            const newHistory = [...timing.history, result.processingTimeMs].slice(-10);
            const avg = newHistory.reduce((a, b) => a + b, 0) / newHistory.length;
            setTimingStats({
                avg: Math.round(avg),
                min: Math.min(...newHistory),
                max: Math.max(...newHistory),
                history: newHistory
            });
        }

        this.updateCounters();
        this.setButtonsState(false);

        if (result.status === 'NG') {
            this.consecutiveNgCount += 1;
        } else {
            this.consecutiveNgCount = 0;
        }

        const stopThreshold = Number(this.runtimeConfig?.stopOnConsecutiveNg ?? 0);
        if (this.isContinuous && stopThreshold > 0 && this.consecutiveNgCount >= stopThreshold) {
            showToast(`连续 NG 达到阈值 (${stopThreshold})，已自动停止`, 'warning');
            this.handleStop();
            return;
        }

        this.addRecentResult(result);

        if (this.isContinuous) {
            this.armProtectionWatchdog('等待下一次触发结果');
        }
    }

    async _legacyLoadRuntimeConfigDuplicate() {
        try {
            const settings = await httpClient.get('/settings');
            const runtime = settings?.runtime || {};
            this.runtimeConfig = {
                autoRun: !!runtime.autoRun,
                stopOnConsecutiveNg: Math.max(0, Number(runtime.stopOnConsecutiveNg || 0)),
                missingMaterialTimeoutSeconds: Math.max(0, Number(runtime.missingMaterialTimeoutSeconds || 0)),
                applyProtectionRules: runtime.applyProtectionRules !== false
            };
        } catch (error) {
            console.warn('[InspectionPanel] 加载运行时配置失败:', error);
            this.runtimeConfig = {
                autoRun: false,
                stopOnConsecutiveNg: 0,
                missingMaterialTimeoutSeconds: 30,
                applyProtectionRules: true
            };
        }

        this.tryAutoRunIfNeeded();
    }

    /**
     * 销毁组件，清理资源
     */
    dispose() {
        console.log('[InspectionPanel] 正在销毁...');
        
        // 取消订阅
        if (this.unsubscribeCompleted) {
            this.unsubscribeCompleted();
            this.unsubscribeCompleted = null;
        }
        
        if (this.unsubscribeError) {
            this.unsubscribeError();
            this.unsubscribeError = null;
        }
        this.clearProtectionWatchdog();
        
        // 清理DOM事件（通过innerHTML置空自动处理大部分，但为了保险可以手动移除）
        // 这里主要依赖 innerHTML 清空或 DOM 移除
    }
}

export { InspectionPanel };
export default InspectionPanel;
