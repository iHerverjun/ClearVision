/**
 * 检测控制模块
 * 负责单次检测、实时检测、相机控制
 * 【架构修复 v2】支持 SSE + WebMessage 双栈
 */

import httpClient from '../../core/messaging/httpClient.js';
import webMessageBridge from '../../core/messaging/webMessageBridge.js';
import { createSignal } from '../../core/state/store.js';
import { getStoredToken } from '../auth/authStorage.js';

// 检测状态
const [getInspectionState, setInspectionState, subscribeInspectionState] = createSignal({
    isRunning: false,
    isRealtime: false,
    progress: 0,
    currentOperator: null,
    status: 'idle' // idle, running, completed, error
});

const [getLastResult, setLastResult, subscribeLastResult] = createSignal(null);

class InspectionController {
    constructor() {
        this.projectId = null;
        this.cameraId = null;
        this.abortController = null;
        
        // 【架构修复 v2】SSE 相关
        this.eventSource = null;
        this.isSseSupported = typeof EventSource !== 'undefined';
        this.useSse = false;  // 是否使用 SSE（根据连接成功与否动态决定）
        
        // 初始化监听
        this.initializeWebMessage();
    }

    /**
     * 设置当前工程
     */
    setProject(projectId) {
        this.projectId = projectId;
    }

    /**
     * 设置相机
     */
    setCamera(cameraId) {
        this.cameraId = cameraId;
    }

    /**
     * 初始化 WebMessage 监听（降级方案）
     */
    initializeWebMessage() {
        // 监听算子执行事件
        webMessageBridge.on('operatorExecuted', (data) => {
            console.log('[InspectionController] 算子执行完成:', data);
            this.updateProgress(data);
        });

        // 【架构修复 v2】监听状态变更事件
        webMessageBridge.on('stateChanged', (data) => {
            console.log('[InspectionController] 状态变更:', data);
            this.handleStateChanged(data);
        });

        // 【架构修复 v2】监听检测结果事件
        webMessageBridge.on('resultProduced', (data) => {
            console.log('[InspectionController] 检测结果:', data);
            this.handleResultEvent(data);
        });

        // 【架构修复 v2】监听进度事件
        webMessageBridge.on('progressChanged', (data) => {
            console.log('[InspectionController] 进度更新:', data);
            this.updateProgress(data);
        });

        // 监听检测完成事件（兼容旧版）
        webMessageBridge.on('faulted', (data) => {
            console.error('[InspectionController] faulted:', data);
            this.handleInspectionError(new Error(data.errorMessage || 'Realtime inspection faulted'));
        });

        webMessageBridge.on('inspectionCompleted', (data) => {
            console.log('[InspectionController] 检测完成:', data);
            this.handleInspectionCompleted(data);
        });

        // 监听进度通知
        webMessageBridge.on('progressNotification', (data) => {
            this.updateProgress(data);
        });
    }

    /**
     * 【架构修复 v2】订阅 SSE 事件流
     */
    subscribeToSseEvents(projectId) {
        if (!this.isSseSupported) {
            console.log('[InspectionController] 浏览器不支持 SSE，使用 WebMessage');
            return false;
        }

        // 关闭已有连接
        this.unsubscribeFromSseEvents();

        try {
            console.log('[InspectionController] 连接 SSE:', projectId);
            
            const token = getStoredToken();
            const tokenQuery = token ? `?token=${encodeURIComponent(token)}` : '';
            const eventUrl = `${httpClient.baseUrl}/inspection/realtime/${projectId}/events${tokenQuery}`;
            this.eventSource = new EventSource(eventUrl);

            // 初始状态
            this.eventSource.addEventListener('initialState', (e) => {
                const data = JSON.parse(e.data);
                console.log('[InspectionController] SSE 初始状态:', data);
                setInspectionState({
                    ...getInspectionState(),
                    isRealtime: data.status === 'Running' || data.status === 'Starting',
                    status: data.status === 'Running' ? 'running' : 'idle'
                });
            });

            // 状态变更
            this.eventSource.addEventListener('stateChanged', (e) => {
                const data = JSON.parse(e.data);
                console.log('[InspectionController] SSE 状态变更:', data);
                this.handleStateChanged(data);
            });

            // 检测结果
            this.eventSource.addEventListener('resultProduced', (e) => {
                const data = JSON.parse(e.data);
                console.log('[InspectionController] SSE 检测结果:', data);
                this.handleResultEvent(data);
            });

            // 进度更新
            this.eventSource.addEventListener('progressChanged', (e) => {
                const data = JSON.parse(e.data);
                console.log('[InspectionController] SSE 进度:', data);
                this.updateProgress(data);
            });

            // 心跳
            this.eventSource.addEventListener('faulted', (e) => {
                const data = JSON.parse(e.data);
                console.error('[InspectionController] SSE faulted:', data);
                this.handleInspectionError(new Error(data.errorMessage || 'Realtime inspection faulted'));
            });

            this.eventSource.addEventListener('heartbeat', (e) => {
                // 心跳只用于保活，不处理
                console.debug('[InspectionController] SSE 心跳');
            });

            // 打开连接
            this.eventSource.onopen = () => {
                console.log('[InspectionController] SSE 连接已建立');
                this.useSse = true;
            };

            // 错误处理
            this.eventSource.onerror = (error) => {
                console.error('[InspectionController] SSE 错误:', error);
                this.useSse = false;
                // 错误时回退到 WebMessage（已自动处理）
            };

            return true;
        } catch (error) {
            console.error('[InspectionController] SSE 连接失败:', error);
            this.useSse = false;
            return false;
        }
    }

    /**
     * 【架构修复 v2】取消 SSE 订阅
     */
    unsubscribeFromSseEvents() {
        if (this.eventSource) {
            console.log('[InspectionController] 关闭 SSE 连接');
            this.eventSource.close();
            this.eventSource = null;
            this.useSse = false;
        }
    }

    /**
     * 【架构修复 v2】处理状态变更
     */
    handleStateChanged(data) {
        const statusMap = {
            'Starting': 'running',
            'Running': 'running',
            'Stopping': 'running',
            'Stopped': 'idle',
            'Faulted': 'error'
        };

        setInspectionState({
            ...getInspectionState(),
            isRealtime: data.newState === 'Running' || data.newState === 'Starting',
            status: statusMap[data.newState] || 'idle'
        });

        if (data.newState === 'Stopped' || data.newState === 'Faulted') {
            console.error('[InspectionController] 检测故障:', data.errorMessage);
        }
    }

    /**
     * 【架构修复 v2】处理结果事件
     */
    handleResultEvent(data) {
        const result = this.normalizeResultPayload({
            id: data.resultId,
            projectId: data.projectId,
            imageId: data.imageId,
            status: data.status,
            defects: data.defects || [],
            defectCount: data.defectCount,
            processingTimeMs: data.processingTimeMs,
            timestamp: data.timestamp,
            outputData: data.outputData,
            outputDataJson: data.outputDataJson,
            analysisData: data.analysisData,
            analysisDataJson: data.analysisDataJson,
            outputImageBase64: data.outputImageBase64
        });

        setLastResult(result);

        // 如果有输出图像，显示它
        if (result.outputImageBase64) {
            const imageData = `data:image/png;base64,${result.outputImageBase64}`;
            if (window.inspectionImageViewer) {
                window.inspectionImageViewer.loadImage(imageData);
            }
            if (window.imageViewer) {
                window.imageViewer.loadImage(imageData);
            }
        }

        this.notifyInspectionCompleted(result);
    }

    /**
     * 执行单次检测
     */
    async executeSingle(imageData = null) {
        if (!this.projectId) {
            throw new Error('未选择工程');
        }

        setInspectionState({
            ...getInspectionState(),
            isRunning: true,
            progress: 0,
            status: 'running'
        });

        try {
            let result;

            if (imageData) {
                const base64Data = imageData instanceof Uint8Array 
                    ? btoa(String.fromCharCode(...imageData))
                    : imageData;

                result = await httpClient.post('/inspection/execute', {
                    projectId: this.projectId,
                    imageBase64: base64Data
                });
            } else if (this.cameraId) {
                result = await httpClient.post('/inspection/execute', {
                    projectId: this.projectId,
                    cameraId: this.cameraId
                });
            } else {
                let flowData = null;
                if (window.flowCanvas && typeof window.flowCanvas.serialize === 'function') {
                    flowData = window.flowCanvas.serialize();
                }
                
                result = await httpClient.post('/inspection/execute', {
                    projectId: this.projectId,
                    flowData: flowData
                });
            }

            this.handleInspectionCompleted(result);
            return result;

        } catch (error) {
            console.error('[InspectionController] 检测执行失败:', error);
            this.handleInspectionError(error);
            throw error;
        }
    }

    /**
     * 开始实时检测
     */
    async startRealtime() {
        if (!this.projectId) {
            throw new Error('未选择工程');
        }

        if (!this.cameraId) {
            throw new Error('未选择相机');
        }

        // 【架构修复 v2】先订阅 SSE 事件
        this.subscribeToSseEvents(this.projectId);

        try {
            this.abortController = new AbortController();

            const flowData = window.flowCanvas?.serialize?.() || null;
            
            await httpClient.post('/inspection/realtime/start', {
                projectId: this.projectId,
                cameraId: this.cameraId,
                runMode: 'camera',
                flowData: flowData
            });

            console.log('[InspectionController] 实时检测已启动');

        } catch (error) {
            console.error('[InspectionController] 启动实时检测失败:', error);
            throw error;
        }
    }

    /**
     * 开始实时检测（流程驱动模式）
     */
    async startRealtimeFlowMode() {
        if (!this.projectId) {
            throw new Error('未选择工程');
        }

        // 【架构修复 v2】先订阅 SSE 事件
        this.subscribeToSseEvents(this.projectId);

        try {
            this.abortController = new AbortController();

            const flowData = window.flowCanvas?.serialize?.() || null;
            if (!flowData) {
                throw new Error('无法获取流程数据');
            }

            await httpClient.post('/inspection/realtime/start', {
                projectId: this.projectId,
                cameraId: this.cameraId || null,
                runMode: 'flow',
                flowData: flowData
            });

            console.log('[InspectionController] 实时检测已启动 (流程驱动)');

        } catch (error) {
            console.error('[InspectionController] 启动失败:', error);
            throw error;
        }
    }

    /**
     * 停止实时检测
     */
    async stopRealtime() {
        try {
            await httpClient.post('/inspection/realtime/stop', { projectId: this.projectId });
            
            if (this.abortController) {
                this.abortController.abort();
                this.abortController = null;
            }

            // 【架构修复 v2】取消 SSE 订阅


            console.log('[InspectionController] 实时检测已停止');

        } catch (error) {
            console.error('[InspectionController] 停止实时检测失败:', error);
        }
    }

    /**
     * 【Phase 3】预览工作流中指定节点的输出
     * 复用调试缓存机制，执行上游子图到目标节点
     * 
     * @param {Guid} targetNodeId - 目标节点ID
     * @param {Object} options - 预览选项
     * @param {string} options.debugSessionId - 调试会话ID（用于缓存复用）
     * @param {string} options.inputImageBase64 - 输入图像（可选）
     * @param {Object} options.parameters - 覆盖参数（可选）
     */
    async previewNode(targetNodeId, options = {}) {
        if (!this.projectId) {
            throw new Error('未选择工程');
        }

        try {
            const flowData = this.normalizePreviewFlowData(window.flowCanvas?.serialize?.() || null);
            if (!flowData) {
                throw new Error('无法获取流程数据');
            }

            console.log('[InspectionController] 请求预览节点:', targetNodeId);

            const result = await httpClient.post('/flows/preview-node', {
                projectId: this.projectId,
                targetNodeId: targetNodeId,
                debugSessionId: options.debugSessionId || this.generateSessionId(),
                flowData: flowData,
                inputImageBase64: options.inputImageBase64,
                parameters: options.parameters,
                imageFormat: options.imageFormat || '.png'
            });

            console.log('[InspectionController] 预览完成:', result);

            // 显示预览结果
            if (result.outputImageBase64) {
                const imageData = `data:image/png;base64,${result.outputImageBase64}`;
                if (window.inspectionImageViewer) {
                    window.inspectionImageViewer.loadImage(imageData);
                }
                if (window.imageViewer) {
                    window.imageViewer.loadImage(imageData);
                }
            }

            return result;

        } catch (error) {
            console.error('[InspectionController] 预览节点失败:', error);
            throw error;
        }
    }

    /**
     * 【Phase 3】生成调试会话ID
     */
    generateSessionId() {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
            const r = Math.random() * 16 | 0;
            const v = c === 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }

    normalizePreviewFlowData(flowData) {
        if (!flowData || typeof flowData !== 'object') {
            return null;
        }

        const operators = Array.isArray(flowData.operators)
            ? flowData.operators.map(operator => ({
                ...operator,
                parameters: this.normalizePreviewOperatorParameters(operator?.parameters)
            }))
            : [];

        return {
            ...flowData,
            operators
        };
    }

    normalizePreviewOperatorParameters(parameters) {
        if (!Array.isArray(parameters)) {
            return parameters && typeof parameters === 'object' ? parameters : {};
        }

        return parameters.reduce((accumulator, parameter) => {
            const name = String(parameter?.name || parameter?.Name || '').trim();
            if (!name) {
                return accumulator;
            }

            accumulator[name] = parameter?.value ?? parameter?.Value ?? parameter?.defaultValue ?? parameter?.DefaultValue ?? null;
            return accumulator;
        }, {});
    }

    /**
     * 处理检测完成
     */
    handleInspectionCompleted(result) {
        const normalizedResult = this.normalizeResultPayload(result);

        setLastResult(normalizedResult);

        setInspectionState({
            ...getInspectionState(),
            isRunning: false,
            progress: 100,
            status: normalizedResult.status === 'Error' ? 'error' : 'completed'
        });

        const outputImage = normalizedResult.outputImage
            || normalizedResult.resultImageBase64
            || normalizedResult.outputImageBase64;
        if (outputImage) {
            const imageData = `data:image/png;base64,${outputImage}`;

            if (window.inspectionImageViewer) {
                window.inspectionImageViewer.loadImage(imageData);
            }

            if (window.imageViewer) {
                window.imageViewer.loadImage(imageData);
            }
        }

        this.notifyInspectionCompleted(normalizedResult);
    }

    /**
     * 处理检测错误
     */
    handleInspectionError(error) {
        setInspectionState({
            ...getInspectionState(),
            isRunning: false,
            isRealtime: false,
            status: 'error'
        });

        if (this._onErrorCallbacks) {
            this._onErrorCallbacks.forEach(cb => {
                try {
                    cb(error);
                } catch (callbackError) {
                    console.error('[InspectionController] 错误回调执行失败:', callbackError);
                }
            });
        }
    }

    /**
     * 更新进度
     */
    updateProgress(data) {
        setInspectionState({
            ...getInspectionState(),
            progress: data.progress || data.progressPercentage || 0,
            currentOperator: data.operatorName || data.currentOperator || null
        });
    }

    /**
     * 获取检测历史
     */
    async getInspectionHistory(startTime, endTime, pageIndex = 0, pageSize = 20) {
        if (!this.projectId) {
            throw new Error('未选择工程');
        }

        try {
            const params = {
                startTime: startTime?.toISOString(),
                endTime: endTime?.toISOString(),
                pageIndex,
                pageSize
            };

            const results = await httpClient.get(
                `/inspection/history/${this.projectId}`,
                params
            );

            return results;
        } catch (error) {
            console.error('[InspectionController] 获取检测历史失败:', error);
            throw error;
        }
    }

    /**
     * 获取统计信息
     */
    async getStatistics(startTime, endTime) {
        if (!this.projectId) {
            throw new Error('未选择工程');
        }

        try {
            const params = {
                startTime: startTime?.toISOString(),
                endTime: endTime?.toISOString()
            };

            const stats = await httpClient.get(
                `/inspection/statistics/${this.projectId}`,
                params
            );

            return stats;
        } catch (error) {
            console.error('[InspectionController] 获取统计信息失败:', error);
            throw error;
        }
    }

    /**
     * 设置检测完成回调
     */
    onInspectionCompleted(callback) {
        if (!this._onCompletedCallbacks) {
            this._onCompletedCallbacks = [];
        }
        this._onCompletedCallbacks.push(callback);
        
        return () => {
            if (this._onCompletedCallbacks) {
                this._onCompletedCallbacks = this._onCompletedCallbacks.filter(cb => cb !== callback);
            }
        };
    }

    /**
     * 设置检测错误回调
     */
    onInspectionError(callback) {
        if (!this._onErrorCallbacks) {
            this._onErrorCallbacks = [];
        }
        this._onErrorCallbacks.push(callback);
        
        return () => {
            if (this._onErrorCallbacks) {
                this._onErrorCallbacks = this._onErrorCallbacks.filter(cb => cb !== callback);
            }
        };
    }

    /**
     * 获取当前状态
     */
    getState() {
        return getInspectionState();
    }

    /**
     * 获取最新结果
     */
    getLastResult() {
        return getLastResult();
    }

    /**
     * 是否正在运行
     */
    isRunning() {
        return getInspectionState().isRunning;
    }

    /**
     * 是否实时检测模式
     */
    isRealtime() {
        return getInspectionState().isRealtime;
    }

    normalizeResultPayload(result) {
        const normalized = { ...(result || {}) };

        const outputData = this.parseJsonField(
            this.readFirstDefined(normalized.outputData, normalized.OutputData),
            normalized.outputDataJson || normalized.OutputDataJson,
            'outputDataJson'
        );
        if (outputData) {
            normalized.outputData = outputData;
        }

        const analysisData = this.parseJsonField(
            this.readFirstDefined(normalized.analysisData, normalized.AnalysisData),
            normalized.analysisDataJson || normalized.AnalysisDataJson,
            'analysisDataJson'
        );
        if (analysisData) {
            normalized.analysisData = analysisData;
        }

        normalized.defects = Array.isArray(normalized.defects)
            ? normalized.defects
            : (Array.isArray(normalized.Defects) ? normalized.Defects : []);
        normalized.defectCount = this.readFirstDefined(
            normalized.defectCount,
            normalized.DefectCount,
            normalized.defects?.length,
            normalized.Defects?.length
        ) ?? 0;
        normalized.processingTimeMs = normalized.processingTimeMs
            ?? normalized.ProcessingTimeMs
            ?? normalized.processingTime
            ?? normalized.executionTimeMs
            ?? normalized.ExecutionTimeMs;
        normalized.timestamp = normalized.timestamp
            ?? normalized.Timestamp
            ?? normalized.inspectionTime
            ?? normalized.InspectionTime;

        normalized.outputImage = normalized.outputImage || normalized.OutputImage;
        normalized.outputImageBase64 = normalized.outputImageBase64 || normalized.OutputImageBase64;
        normalized.resultImageBase64 = normalized.resultImageBase64 || normalized.ResultImageBase64;
        normalized.imageId = normalized.imageId || normalized.ImageId;

        return normalized;
    }

    parseJsonField(directValue, serializedValue, fieldName) {
        if (this.hasMeaningfulStructuredValue(directValue)) {
            return directValue;
        }

        if (typeof serializedValue !== 'string' || serializedValue.trim().length === 0) {
            return directValue || null;
        }

        try {
            return JSON.parse(serializedValue);
        } catch (error) {
            console.warn(`[InspectionController] 解析 ${fieldName} 失败:`, error);
            return directValue || null;
        }
    }

    hasMeaningfulStructuredValue(value) {
        if (!value || typeof value !== 'object') {
            return false;
        }

        if (Array.isArray(value)) {
            return value.length > 0;
        }

        return Object.keys(value).length > 0;
    }

    readFirstDefined(...values) {
        return values.find(value => value !== undefined && value !== null);
    }

    notifyInspectionCompleted(result) {
        if (!this._onCompletedCallbacks) {
            return;
        }

        this._onCompletedCallbacks.forEach(cb => {
            try {
                cb(result);
            } catch (callbackError) {
                console.error('[InspectionController] 完成回调执行失败:', callbackError);
            }
        });
    }
}

// 创建单例
const inspectionController = new InspectionController();

export default inspectionController;
export { 
    inspectionController,
    getInspectionState,
    getLastResult
};
