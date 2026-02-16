/**
 * 检测控制面板组件
 * 提供检测视图的完整控制界面，包括运行控制、状态显示、统计信息等功能
 */

import inspectionController, { getInspectionState } from './inspectionController.js';
import httpClient from '../../core/messaging/httpClient.js';
import { createSignal } from '../../core/state/store.js';

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
        
        // 初始化UI
        this.initialize();
        
        // 订阅状态变化
        this.setupSubscriptions();
        
        // 加载相机列表
        this.loadCameras();
        
        console.log('[InspectionPanel] 检测控制面板初始化完成');
    }
    
    /**
     * 初始化面板UI
     */
    initialize() {
        this.container.innerHTML = `
            <div class="inspection-panel">
                <!-- 运行控制条 -->
                <div class="inspection-section run-controls">
                    <h4 class="section-title">运行控制</h4>
                    <div class="control-buttons">
                        <button class="btn-inspection btn-run-single" id="btn-run-single" title="单次运行">
                            <svg class="icon" viewBox="0 0 24 24" width="20" height="20">
                                <polygon points="5,3 19,12 5,21" fill="currentColor"/>
                            </svg>
                            <span>单次运行</span>
                        </button>
                        <button class="btn-inspection btn-run-continuous" id="btn-run-continuous" title="连续运行">
                            <svg class="icon" viewBox="0 0 24 24" width="20" height="20">
                                <circle cx="12" cy="12" r="8" fill="currentColor"/>
                            </svg>
                            <span>连续运行</span>
                        </button>
                        <button class="btn-inspection btn-stop" id="btn-stop" title="停止" disabled>
                            <svg class="icon" viewBox="0 0 24 24" width="20" height="20">
                                <rect x="6" y="6" width="12" height="12" fill="currentColor"/>
                            </svg>
                            <span>停止</span>
                        </button>
                    </div>
                </div>
                
                <!-- 状态仪表盘 -->
                <div class="inspection-section status-dashboard">
                    <h4 class="section-title">当前状态</h4>
                    <div class="status-indicator" id="status-indicator">
                        <div class="status-led" id="status-led"></div>
                        <span class="status-text" id="status-text">就绪</span>
                    </div>
                </div>
                
                <!-- 进度条 -->
                <div class="inspection-section progress-section">
                    <h4 class="section-title">检测进度</h4>
                    <div class="progress-container">
                        <div class="progress-bar" id="progress-bar">
                            <div class="progress-fill" id="progress-fill"></div>
                            <div class="progress-shimmer"></div>
                        </div>
                        <div class="progress-info">
                            <span class="progress-percent" id="progress-percent">0%</span>
                            <span class="current-operator" id="current-operator">等待中...</span>
                        </div>
                    </div>
                </div>
                
                <!-- 实时计数器 -->
                <div class="inspection-section counters-section">
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
                
                <!-- 相机选择器 -->
                <div class="inspection-section camera-section">
                    <h4 class="section-title">相机选择</h4>
                    <div class="camera-selector">
                        <select class="camera-dropdown" id="camera-dropdown">
                            <option value="">选择相机...</option>
                        </select>
                        <button class="btn-refresh-cameras" id="btn-refresh-cameras" title="刷新相机列表">
                            <svg class="icon" viewBox="0 0 24 24" width="16" height="16">
                                <path d="M17.65 6.35C16.2 4.9 14.21 4 12 4c-4.42 0-7.99 3.58-7.99 8s3.57 8 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08c-.82 2.33-3.04 4-5.65 4-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z" fill="currentColor"/>
                            </svg>
                        </button>
                    </div>
                </div>
                
                <!-- 耗时统计 -->
                <div class="inspection-section timing-section">
                    <h4 class="section-title">耗时统计</h4>
                    <div class="timing-stats">
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
                
                <!-- 最近结果缩略图 -->
                <div class="inspection-section recent-results">
                    <h4 class="section-title">最近结果</h4>
                    <div class="recent-results-grid" id="recent-results-grid">
                        <!-- 动态生成 -->
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
        // 单次运行按钮
        const runSingleBtn = this.container.querySelector('#btn-run-single');
        if (runSingleBtn) {
            runSingleBtn.addEventListener('click', () => this.handleRunSingle());
        }
        
        // 连续运行按钮
        const runContinuousBtn = this.container.querySelector('#btn-run-continuous');
        if (runContinuousBtn) {
            runContinuousBtn.addEventListener('click', () => this.handleRunContinuous());
        }
        
        // 停止按钮
        const stopBtn = this.container.querySelector('#btn-stop');
        if (stopBtn) {
            stopBtn.addEventListener('click', () => this.handleStop());
        }
        
        // 刷新相机按钮
        const refreshCamerasBtn = this.container.querySelector('#btn-refresh-cameras');
        if (refreshCamerasBtn) {
            refreshCamerasBtn.addEventListener('click', () => this.loadCameras());
        }
        
        // 相机选择器
        const cameraDropdown = this.container.querySelector('#camera-dropdown');
        if (cameraDropdown) {
            cameraDropdown.addEventListener('change', (e) => {
                this.selectedCamera = e.target.value || null;
                if (this.selectedCamera) {
                    inspectionController.setCamera(this.selectedCamera);
                }
            });
        }
    }
    
    /**
     * 设置状态订阅
     */
    setupSubscriptions() {
        // 订阅检测状态变化（调用方法注册回调，而非覆盖方法）
        inspectionController.onInspectionCompleted((result) => {
            this.handleInspectionResult(result);
        });
        
        inspectionController.onInspectionError((error) => {
            this.updateStatus('error', '检测错误');
        });
    }
    
    /**
     * 加载相机列表
     */
    async loadCameras() {
        try {
            const dropdown = this.container.querySelector('#camera-dropdown');
            if (dropdown) {
                dropdown.disabled = true;
                dropdown.innerHTML = '<option value="">加载中...</option>';
            }
            
            // 从后端获取相机列表
            const cameras = await httpClient.get('/cameras');
            setCameraList(cameras || []);
            
            // 更新下拉框
            if (dropdown) {
                dropdown.innerHTML = '<option value="">选择相机...</option>';
                (cameras || []).forEach(camera => {
                    const option = document.createElement('option');
                    option.value = camera.id;
                    option.textContent = camera.name || camera.id;
                    dropdown.appendChild(option);
                });
                dropdown.disabled = false;
            }
        } catch (error) {
            console.error('[InspectionPanel] 加载相机列表失败:', error);
            // 使用模拟数据
            const mockCameras = [
                { id: 'camera-1', name: '相机 1' },
                { id: 'camera-2', name: '相机 2' }
            ];
            setCameraList(mockCameras);
            
            const dropdown = this.container.querySelector('#camera-dropdown');
            if (dropdown) {
                dropdown.innerHTML = '<option value="">选择相机...</option>';
                mockCameras.forEach(camera => {
                    const option = document.createElement('option');
                    option.value = camera.id;
                    option.textContent = camera.name;
                    dropdown.appendChild(option);
                });
                dropdown.disabled = false;
            }
        }
    }
    
    /**
     * 处理单次运行
     */
    async handleRunSingle() {
        try {
            this.updateStatus('running', '运行中...');
            this.setButtonsState(true);
            
            const project = window.currentProject;
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
    async handleRunContinuous() {
        try {
            if (!this.selectedCamera) {
                alert('请先选择相机');
                return;
            }
            
            this.isContinuous = true;
            this.updateStatus('running', '连续运行中...');
            this.setButtonsState(true);
            
            const project = window.currentProject;
            if (project) {
                inspectionController.setProject(project.id);
            }
            
            await inspectionController.startRealtime();
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
    async handleStop() {
        try {
            if (this.isContinuous) {
                await inspectionController.stopRealtime();
                this.isContinuous = false;
            }
            this.updateStatus('idle', '已停止');
            this.setButtonsState(false);
            this.updateProgress(0, null);
        } catch (error) {
            console.error('[InspectionPanel] 停止失败:', error);
        }
    }
    
    /**
     * 处理检测结果
     */
    handleInspectionResult(result) {
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
        
        // 更新按钮状态
        this.setButtonsState(false);
        
        // 添加到最近结果
        this.addRecentResult(result);
    }
    
    /**
     * 更新状态显示
     */
    updateStatus(status, text) {
        const led = this.container.querySelector('#status-led');
        const statusText = this.container.querySelector('#status-text');
        
        if (led) {
            led.className = 'status-led';
            if (status) {
                led.classList.add(`status-${status}`);
            }
        }
        
        if (statusText) {
            statusText.textContent = text || '就绪';
        }
    }
    
    /**
     * 更新进度条
     */
    updateProgress(percent, operatorName) {
        const fill = this.container.querySelector('#progress-fill');
        const percentText = this.container.querySelector('#progress-percent');
        const operatorText = this.container.querySelector('#current-operator');
        
        if (fill) {
            fill.style.width = `${percent}%`;
        }
        
        if (percentText) {
            percentText.textContent = `${Math.round(percent)}%`;
        }
        
        if (operatorText) {
            operatorText.textContent = operatorName || '等待中...';
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
        
        // 更新耗时
        const avgEl = this.container.querySelector('#timing-avg');
        const minEl = this.container.querySelector('#timing-min');
        const maxEl = this.container.querySelector('#timing-max');
        
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
            imageData: result.outputImage || result.imageData
        };
        
        const updated = [newResult, ...recent].slice(0, 5);
        setRecentResults(updated);
        
        this.renderRecentResults();
    }
    
    /**
     * 渲染最近结果
     */
    renderRecentResults() {
        const grid = this.container.querySelector('#recent-results-grid');
        if (!grid) return;
        
        const recent = getRecentResults();
        
        grid.innerHTML = recent.map(result => `
            <div class="recent-result-item ${result.status === 'OK' ? 'result-ok' : 'result-ng'}" data-id="${result.id}">
                <div class="result-thumb">
                    ${result.imageData 
                        ? `<img src="data:image/png;base64,${result.imageData}" alt="检测结果">`
                        : `<div class="result-placeholder"></div>`
                    }
                </div>
                <div class="result-badge">${result.status}</div>
                <div class="result-time">${result.timestamp}</div>
            </div>
        `).join('');
        
        // 添加点击事件
        grid.querySelectorAll('.recent-result-item').forEach(item => {
            item.addEventListener('click', () => {
                const id = parseInt(item.dataset.id);
                const result = getRecentResults().find(r => r.id === id);
                if (result && result.imageData && window.imageViewer) {
                    window.imageViewer.loadImage(`data:image/png;base64,${result.imageData}`);
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
        this.updateProgress(0, null);
        this.updateStatus('idle', '就绪');
    }
    
    /**
     * 刷新显示
     */
    refresh() {
        this.updateCounters();
        this.renderRecentResults();
    }
}

export { InspectionPanel };
export default InspectionPanel;
