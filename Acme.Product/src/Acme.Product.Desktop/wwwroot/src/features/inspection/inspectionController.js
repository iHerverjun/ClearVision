/**
 * 检测控制模块
 * 负责单次检测、实时检测、相机控制
 */

import httpClient from '../../core/messaging/httpClient.js';
import webMessageBridge from '../../core/messaging/webMessageBridge.js';
import { createSignal } from '../../core/state/store.js';

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
        
        // 初始化 WebMessage 监听
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
     * 初始化 WebMessage 监听
     */
    initializeWebMessage() {
        // 监听算子执行事件
        webMessageBridge.on('operatorExecuted', (data) => {
            console.log('[InspectionController] 算子执行完成:', data);
            this.updateProgress(data);
        });

        // 监听检测完成事件
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
     * 执行单次检测
     */
    async executeSingle(imageData = null) {
        if (!this.projectId) {
            throw new Error('未选择工程');
        }

        // 更新状态
        setInspectionState({
            ...getInspectionState(),
            isRunning: true,
            progress: 0,
            status: 'running'
        });

        try {
            let result;

            if (imageData) {
                // 使用提供的图像数据
                const base64Data = imageData instanceof Uint8Array 
                    ? btoa(String.fromCharCode(...imageData))
                    : imageData;

                result = await httpClient.post('/inspection/execute', {
                    projectId: this.projectId,
                    imageBase64: base64Data
                });
            } else if (this.cameraId) {
                // 使用相机采集
                result = await httpClient.post('/inspection/execute', {
                    projectId: this.projectId,
                    cameraId: this.cameraId
                });
            } else {
                // 【关键修复】将当前流程数据（含最新参数）一起发送给后端
                // 这确保后端使用的是前端编辑过的参数值，而非数据库中的过时数据
                let flowData = null;
                if (window.flowCanvas && typeof window.flowCanvas.serialize === 'function') {
                    flowData = window.flowCanvas.serialize();
                    console.log('[InspectionController] 携带流程数据执行检测:', flowData);
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

        // 更新状态
        setInspectionState({
            ...getInspectionState(),
            isRealtime: true,
            status: 'running'
        });

        try {
            // 创建 AbortController 用于取消
            this.abortController = new AbortController();

            // 【第二优先级】携带流程数据和运行模式
            const flowData = window.flowCanvas?.serialize?.() || null;
            
            await httpClient.post('/inspection/realtime/start', {
                projectId: this.projectId,
                cameraId: this.cameraId,
                runMode: 'camera',
                flowData: flowData
            });

            console.log('[InspectionController] 实时检测已启动 (相机驱动模式)');

        } catch (error) {
            console.error('[InspectionController] 启动实时检测失败:', error);
            this.stopRealtime();
            throw error;
        }
    }

    /**
     * 开始实时检测（流程驱动模式）
     * 【第二优先级】支持PLC触发等流程驱动场景
     */
    async startRealtimeFlowMode() {
        if (!this.projectId) {
            throw new Error('未选择工程');
        }

        // 流程驱动模式下，相机是可选的（由流程内图像采集算子控制）
        // 不强制检查 cameraId

        // 更新状态
        setInspectionState({
            ...getInspectionState(),
            isRealtime: true,
            status: 'running'
        });

        try {
            // 创建 AbortController 用于取消
            this.abortController = new AbortController();

            // 获取当前流程数据
            const flowData = window.flowCanvas?.serialize?.() || null;
            if (!flowData) {
                throw new Error('无法获取流程数据，请确保流程编辑器已打开');
            }

            await httpClient.post('/inspection/realtime/start', {
                projectId: this.projectId,
                cameraId: this.cameraId || null, // 相机可选
                runMode: 'flow', // 【第二优先级】流程驱动模式
                flowData: flowData
            });

            console.log('[InspectionController] 实时检测已启动 (流程驱动模式)');

        } catch (error) {
            console.error('[InspectionController] 启动流程驱动检测失败:', error);
            this.stopRealtime();
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

            setInspectionState({
                ...getInspectionState(),
                isRealtime: false,
                status: 'idle'
            });

            console.log('[InspectionController] 实时检测已停止');

        } catch (error) {
            console.error('[InspectionController] 停止实时检测失败:', error);
        }
    }

    /**
     * 处理检测完成
     */
    handleInspectionCompleted(result) {
        // 【关键桥接】如果后端返回的是 raw JSON 字符串 (outputDataJson)，则在此处反序列化
        if (result.outputDataJson && (!result.outputData || Object.keys(result.outputData).length === 0)) {
            try {
                result.outputData = JSON.parse(result.outputDataJson);
            } catch (e) {
                console.warn('[InspectionController] 解析 outputDataJson 失败:', e);
            }
        }

        setLastResult(result);

        setInspectionState({
            ...getInspectionState(),
            isRunning: false,
            progress: 100,
            status: result.status === 'Error' ? 'error' : 'completed'
        });

        // 显示处理后的图像
        if (result.outputImage && window.imageViewer) {
            const imageData = `data:image/png;base64,${result.outputImage}`;
            window.imageViewer.loadImage(imageData);
        }

        // 触发所有已注册的完成回调
        if (this._onCompletedCallbacks) {
            this._onCompletedCallbacks.forEach(cb => cb(result));
        }
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

        // 触发所有已注册的错误回调
        if (this._onErrorCallbacks) {
            this._onErrorCallbacks.forEach(cb => cb(error));
        }
    }

    /**
     * 更新进度
     */
    updateProgress(data) {
        setInspectionState({
            ...getInspectionState(),
            progress: data.progress || 0,
            currentOperator: data.operatorName || null
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
        
        // 返回取消订阅函数
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
        
        // 返回取消订阅函数
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
}

// 创建单例
const inspectionController = new InspectionController();

export default inspectionController;
export { 
    inspectionController,
    getInspectionState,
    getLastResult
};
