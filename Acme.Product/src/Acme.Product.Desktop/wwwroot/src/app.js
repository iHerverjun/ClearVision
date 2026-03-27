/**
 * 主应用入口 - S4-006: 端到端集成
 * Sprint 4: 前后端集成与用户体验闭环
 */

import { Dialog } from './shared/components/dialog.js';
import { buildOperatorNodeConfig } from './shared/operatorVisuals.js';

// ============================================
// 全局错误捕获 - 用于调试
// ============================================
window._errorLogs = [];

const MAX_ERROR_LOGS = 100;
function addErrorLog(logEntry) {
    window._errorLogs.push(logEntry);
    if (window._errorLogs.length > MAX_ERROR_LOGS) {
        window._errorLogs.shift();
    }
}

window.onerror = function(message, source, lineno, colno, error) {
    const errorInfo = `[Global Error] ${message} at ${source}:${lineno}`;
    console.error(errorInfo);
    addErrorLog({
        type: 'Error',
        message,
        source,
        line: lineno,
        column: colno,
        time: new Date().toLocaleTimeString()
    });
    return false;
};

window.addEventListener('unhandledrejection', function(event) {
    const errorMsg = event.reason?.message || event.reason;
    console.error('[Unhandled Promise Rejection]', errorMsg);
    addErrorLog({
        type: 'Promise',
        message: errorMsg,
        time: new Date().toLocaleTimeString()
    });
});

console.log('[App] Starting module imports...');

// ============================================
// 认证检查 - 未登录则跳转
// ============================================
import { bootstrapAuthSession, logout } from './features/auth/auth.js';

import webMessageBridge from './core/messaging/webMessageBridge.js';
import httpClient from './core/messaging/httpClient.js';
import { createSignal } from './core/state/store.js';
import FlowCanvas from './core/canvas/flowCanvas.js';
import { FlowEditorInteraction } from './features/flow-editor/flowEditorInteraction.js';
import { ImageViewerComponent } from './features/image-viewer/imageViewer.js';
import { OperatorLibraryPanel } from './features/operator-library/operatorLibrary.js';
import inspectionController from './features/inspection/inspectionController.js';
import { showToast, createModal, closeModal, createInput, createLabeledInput, createButton } from './shared/components/uiComponents.js';
import { PropertyPanel } from './features/flow-editor/propertyPanel.js';
import PropertySidebarController from './features/flow-editor/propertySidebarController.mjs';
import { NodePreviewCoordinator, resolvePreviewInputImageBase64 } from './features/flow-editor/previewCoordinator.js';
import NodePreviewOverlay from './features/flow-editor/nodePreviewOverlay.js';
import projectManager, {
    getCurrentProject,
    subscribeProject
} from './features/project/projectManager.js';

// 全局状态
const [getCurrentView, setCurrentView, subscribeView] = createSignal('flow');
const [getSelectedOperator, setSelectedOperator, subscribeSelectedOperator] = createSignal(null);
const [getOperatorLibrary, setOperatorLibrary, subscribeOperatorLibrary] = createSignal([]);

// 订阅管理器，防止内存泄漏
const subscriptions = [];
function trackedSubscribe(subscribeFn, callback) {
    const unsubscribe = subscribeFn(callback);
    subscriptions.push(unsubscribe);
    return unsubscribe;
}

// 组件实例
let imageViewer = null;
let operatorLibraryPanel = null;
let flowCanvas = null;
let flowEditorInteraction = null;
let propertyPanel = null;
let propertySidebarController = null;
let nodePreviewCoordinator = null;
let nodePreviewOverlay = null;
let projectView = null;
let resultPanel = null;
let inspectionPanel = null;
let aiPanel = null;
let appInitialized = false;
let appBootstrapPromise = null;
let statusBarStarted = false;
let fpsAnimationFrameId = null;

let projectViewModulePromise = null;
let resultPanelModulePromise = null;
let inspectionPanelModulePromise = null;
let aiPanelModulePromise = null;

// 自动保存定时器
let autoSaveInterval = null;
const AUTO_SAVE_DELAY = 5 * 60 * 1000;

function loadProjectViewModule() {
    if (!projectViewModulePromise) {
        projectViewModulePromise = import('./features/project/projectView.js');
    }

    return projectViewModulePromise;
}

function loadResultPanelModule() {
    if (!resultPanelModulePromise) {
        resultPanelModulePromise = import('./features/results/resultPanel.js');
    }

    return resultPanelModulePromise;
}

function loadInspectionPanelModule() {
    if (!inspectionPanelModulePromise) {
        inspectionPanelModulePromise = import('./features/inspection/inspectionPanel.js');
    }

    return inspectionPanelModulePromise;
}

function loadAiPanelModule() {
    if (!aiPanelModulePromise) {
        aiPanelModulePromise = import('./features/ai/aiPanel.js');
    }

    return aiPanelModulePromise;
}

function updateAuthenticatedUserDisplay() {
    const userNameEl = document.getElementById('user-display-name');
    if (userNameEl && window.currentUser) {
        userNameEl.textContent = window.currentUser.displayName || window.currentUser.username || '--';
    }
}

function syncActiveNavButton(view) {
    document.querySelectorAll('.nav-btn').forEach(btn => {
        btn.classList.toggle('active', btn.dataset.view === view);
    });
}

async function handleProjectChange(project) {
    if (!project?.id) {
        inspectionController.setProject(null);
        resultPanel?.setProjectContext?.(null);
        resultPanel?.clear?.();
        return;
    }

    inspectionController.setProject(project.id);

    if (project.flow && window.flowCanvas) {
        console.log('[App] 当前工程已切换，加载流程数据:', project.flow);
        window.flowCanvas.deserialize(project.flow);
    } else if (window.flowCanvas) {
        console.log('[App] 当前工程没有流程数据，清空画布');
        window.flowCanvas.clear();
    }

    resultPanel?.setProjectContext?.(project.id);
    resultPanel?.clear?.();

    setCurrentView('flow');
    syncActiveNavButton('flow');
    await switchView('flow');
}

/**
 * 初始化应用
 */
async function initializeApp() {
    if (appInitialized) {
        return true;
    }

    console.log('[App] 初始化应用...');
    showLoadingScreen();

    const authState = await bootstrapAuthSession();
    if (!authState.ok) {
        console.warn(`[App] 认证启动失败: ${authState.reason}`);
        return false;
    }

    updateAuthenticatedUserDisplay();
    initializeNavigation();
    initializeOperatorLibraryPanel();
    initializeFlowEditor();
    initializeImageViewer();
    initializeWebMessage();
    initializeInspectionController();
    initializePropertyPanel();
    initializePropertySidebarController();
    initializeTheme();
    initializeToolbar();
    startStatusBarUpdates();
    trackedSubscribe(subscribeProject, (project) => {
        window.setTimeout(() => {
            void handleProjectChange(project).catch(error => {
                handleFeatureLoadError('工程切换', error);
            });
        }, 0);
    });

    appInitialized = true;

    console.log('[App] 应用初始化完成');
    showToast('ClearVision 已就绪', 'success');
    return true;
}

async function bootstrapApp() {
    if (appBootstrapPromise) {
        return appBootstrapPromise;
    }

    appBootstrapPromise = (async () => {
        const initialized = await initializeApp();
        if (!initialized) {
            hideLoadingScreen();
            return false;
        }

        setTimeout(() => {
            hideLoadingScreen();
            showWelcomeScreen();
        }, 500);

        return true;
    })();

    return appBootstrapPromise;
}

/**
 * 初始化导航
 */
function initializeNavigation() {
    const navButtons = document.querySelectorAll('.nav-btn');

    navButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            const view = btn.dataset.view;
            setCurrentView(view);
            syncActiveNavButton(view);
            void switchView(view).catch(error => handleFeatureLoadError('视图切换', error));
        });
    });
}

function handleFeatureLoadError(featureName, error) {
    console.error(`[App] ${featureName} 初始化失败:`, error);
    showToast(`${featureName} 初始化失败，请刷新后重试`, 'error');
}

async function ensureProjectView() {
    if (projectView) {
        return projectView;
    }

    const container = document.getElementById('project-view');
    if (!container) {
        console.warn('[App] 工程视图容器未找到，将在首次切换到工程视图时初始化');
        return null;
    }

    const { ProjectView } = await loadProjectViewModule();
    projectView = new ProjectView('project-view');

    console.log('[App] 工程视图初始化完成');
    return projectView;
}

async function ensureResultPanel() {
    if (resultPanel) {
        return resultPanel;
    }

    const container = document.getElementById('results-list-container');
    if (!container) {
        console.warn('[App] 结果视图容器未找到');
        return null;
    }

    const { ResultPanel } = await loadResultPanelModule();
    resultPanel = new ResultPanel('results-list-container');
    window.resultPanel = resultPanel;
    resultPanel.setProjectContext(getCurrentProject()?.id || null);
    resultPanel.setHistoryLoader(loadInspectionHistory);

    resultPanel.onResultClick = (result) => {
        console.log('[App] 点击结果:', result);
        if (resultPanel && result) {
            resultPanel.showResultDetail(result);
        }
    };

    const clearBtn = document.getElementById('btn-clear-results');
    if (clearBtn && !clearBtn.dataset.cvBound) {
        clearBtn.dataset.cvBound = 'true';
        clearBtn.addEventListener('click', () => {
            if (confirm('确定要清空当前结果视图吗？此操作不会删除后端历史记录。')) {
                resultPanel.clear();
                showToast('当前结果视图已清空，历史记录未删除', 'success');
            }
        });
    }

    console.log('[App] 结果面板初始化完成（现代化仪表盘）');
    return resultPanel;
}

async function ensureInspectionPanelReady() {
    const container = document.getElementById('inspection-control-panel');
    if (!container) {
        console.warn('[App] 检测控制面板容器未找到');
        return null;
    }

    if (inspectionPanel) {
        inspectionPanel.refresh();
        return inspectionPanel;
    }

    const { InspectionPanel } = await loadInspectionPanelModule();

    if (window.inspectionPanel && typeof window.inspectionPanel.dispose === 'function') {
        console.warn('[App] 发现残留的 InspectionPanel 实例，正在销毁...');
        window.inspectionPanel.dispose();
    }

    inspectionPanel = new InspectionPanel('inspection-control-panel');
    window.inspectionPanel = inspectionPanel;
    console.log('[App] 检测控制面板初始化完成');
    return inspectionPanel;
}

async function ensureAiPanel() {
    if (aiPanel) {
        return aiPanel;
    }

    if (!window.flowCanvas) {
        console.warn('[App] FlowCanvas 未就绪，无法初始化 AI 面板');
        return null;
    }

    const { AiPanel } = await loadAiPanelModule();
    aiPanel = new AiPanel('ai-view', window.flowCanvas);
    window.aiPanel = aiPanel;
    console.log('[App] AI 面板初始化完成');
    return aiPanel;
}

async function switchView(view) {
    console.log(`[App] 切换视图到 ${view}`);
    const flowEditor = document.getElementById('flow-editor');
    const imageViewerContainer = document.getElementById('image-viewer');
    const inspectionViewContainer = document.getElementById('inspection-view');
    const resultsViewContainer = document.getElementById('results-view');
    const projectViewContainer = document.getElementById('project-view');
    const aiViewContainer = document.getElementById('ai-view');
    const settingsViewContainer = document.getElementById('settings-view');

    flowEditor?.classList.add('hidden');
    imageViewerContainer?.classList.add('hidden');
    inspectionViewContainer?.classList.add('hidden');
    resultsViewContainer?.classList.add('hidden');
    projectViewContainer?.classList.add('hidden');
    aiViewContainer?.classList.add('hidden');
    settingsViewContainer?.classList.add('hidden');

    const leftSidebar = document.querySelector('.sidebar.left');
    const rightSidebar = document.querySelector('.sidebar.right');

    if (view === 'flow') {
        leftSidebar?.classList.remove('hidden');
        rightSidebar?.classList.remove('hidden');
    } else {
        leftSidebar?.classList.add('hidden');
        rightSidebar?.classList.add('hidden');
    }

    propertySidebarController?.sync(view);

    switch (view) {
        case 'flow':
            flowEditor?.classList.remove('hidden');
            requestAnimationFrame(() => {
                if (window.flowCanvas) {
                    window.flowCanvas.resize();
                }
            });
            break;
        case 'image':
            imageViewerContainer?.classList.remove('hidden');
            requestAnimationFrame(() => {
                if (window.imageViewer?.imageCanvas) {
                    window.imageViewer.imageCanvas.resize();
                }
            });
            break;
        case 'inspection': {
            inspectionViewContainer?.classList.remove('hidden');
            const panel = await ensureInspectionPanelReady();
            initializeInspectionImageViewer();

            requestAnimationFrame(() => {
                if (window.inspectionImageViewer?.imageCanvas) {
                    window.inspectionImageViewer.imageCanvas.resize();
                }

                if (window._lastInspectionResult?.outputImage && window.inspectionImageViewer) {
                    console.log('[App] 切换到检测视图，加载已保存的检测结果图像');
                    const imageData = `data:image/png;base64,${window._lastInspectionResult.outputImage}`;
                    window.inspectionImageViewer.loadImage(imageData);
                }

                panel?.refresh?.();
            });
            break;
        }
        case 'results': {
            resultsViewContainer?.classList.remove('hidden');
            console.log('[App] 切换到结果视图');
            const panel = await ensureResultPanel();
            if (panel) {
                panel.render();
                await loadInspectionHistory();
            }
            break;
        }
        case 'project': {
            projectViewContainer?.classList.remove('hidden');
            console.log('[App] 切换到工程视图');
            const viewInstance = await ensureProjectView();
            viewInstance?.refresh();
            break;
        }
        case 'ai': {
            aiViewContainer?.classList.remove('hidden');
            console.log('[App] 切换到 AI 视图');
            const panel = await ensureAiPanel();
            panel?.activate?.();
            break;
        }
        case 'settings':
            settingsViewContainer?.classList.remove('hidden');
            console.log('[App] 切换到设置视图');
            if (window.cvSettingsView) {
                window.cvSettingsView.refresh();
            } else if (typeof window.initializeSettingsView === 'function') {
                window.initializeSettingsView();
            }
            break;
        default:
            flowEditor?.classList.remove('hidden');
            break;
    }
}

/**
 * 初始化检测图像查看器
 */
function initializeInspectionImageViewer() {
    const container = document.getElementById('inspection-image-area');
    if (!container) {
        console.warn('[App] 检测图像查看器容器未找到');
        return;
    }
    
    // 如果查看器已存在且已初始化，只需调整大小
    if (window.inspectionImageViewer) {
        requestAnimationFrame(() => {
            window.inspectionImageViewer.imageCanvas?.resize();
            if (window.inspectionImageViewer.imageCanvas?.image) {
                window.inspectionImageViewer.imageCanvas.resetView();
            }
        });
        return;
    }
    
    try {
        // 复用现有的 ImageViewerComponent
        const inspectionImageViewer = new ImageViewerComponent('inspection-image-area');
        window.inspectionImageViewer = inspectionImageViewer;
        
        // 【关键修复】移除重复的回调设置，避免覆盖 initializeInspectionController 中设置的回调
        // 检测完成逻辑统一在 initializeInspectionController 中处理
        
        console.log('[App] 检测图像查看器初始化完成');
    } catch (error) {
        console.error('[App] 检测图像查看器初始化失败:', error);
    }
}


/**
 * 更新检测视图结果面板
 */
function updateInspectionResultsPanel(result) {
    // 旧的实时结果面板已移除（inspection-ok-count, inspection-ng-count, inspection-results-detail）
    // 现在由 inspectionPanel.js 的 updateCounters() 和 renderRecentResults() 处理
    // 保留空函数以兼容 initializeInspectionController 中的调用
}

function tryParseJsonPayload(payload) {
    if (typeof payload !== 'string' || payload.trim().length === 0) {
        return null;
    }

    try {
        return JSON.parse(payload);
    } catch (error) {
        console.warn('[App] JSON payload 解析失败:', error);
        return null;
    }
}

function normalizeAnalysisData(result) {
    if (!result || typeof result !== 'object') {
        return null;
    }

    return result.analysisData
        || result.AnalysisData
        || tryParseJsonPayload(result.analysisDataJson)
        || tryParseJsonPayload(result.AnalysisDataJson)
        || null;
}

function normalizeOutputData(result) {
    if (!result || typeof result !== 'object') {
        return {};
    }

    return result.outputData
        || result.OutputData
        || tryParseJsonPayload(result.outputDataJson)
        || tryParseJsonPayload(result.OutputDataJson)
        || {};
}

function getInlineResultImageBase64(result) {
    if (!result || typeof result !== 'object') {
        return null;
    }

    return result.imageData
        || result.ImageData
        || result.outputImage
        || result.OutputImage
        || result.outputImageBase64
        || result.OutputImageBase64
        || result.resultImageBase64
        || result.ResultImageBase64
        || null;
}

function buildResultDefects(result) {
    const actualDefects = result?.defects || result?.Defects;
    if (Array.isArray(actualDefects) && actualDefects.length > 0) {
        return actualDefects;
    }

    const defectCount = Number(
        result?.defectCount
        ?? result?.DefectCount
        ?? 0
    );
    if (!Number.isFinite(defectCount) || defectCount <= 0) {
        return [];
    }

    return Array.from({ length: defectCount }, (_, index) => ({
        type: `目标 ${index + 1}`,
        description: '实时结果未携带缺陷详情'
    }));
}

/**
 * 初始化算子库面板
 */
function initializeOperatorLibraryPanel() {
    const container = document.getElementById('operator-library');
    if (!container) {
        console.error('[App] 找不到算子库容器');
        return;
    }
    
    operatorLibraryPanel = new OperatorLibraryPanel('operator-library');
    window.operatorLibraryPanel = operatorLibraryPanel;
    
    // 设置拖拽回调
    operatorLibraryPanel.onOperatorDragStart = (operatorData) => {
        console.log('[App] 开始拖拽算子:', operatorData.type);
    };
    
    // 设置选中回调
    operatorLibraryPanel.onOperatorSelected = (operatorData) => {
        console.log('[App] 选中算子:', operatorData.type);
        // 【修复】创建浅拷贝确保 Signal 能检测到变化（Signal 使用 !== 严格相等）
        // 同时补全 title 字段，确保 PropertyPanel 能正确显示标题
        const operatorCopy = {
            ...operatorData,
            title: operatorData.title || operatorData.displayName || operatorData.type,
            parameters: operatorData.parameters ? operatorData.parameters.map(p => ({...p})) : []
        };
        setSelectedOperator(operatorCopy);
    };
    
    console.log('[App] 算子库面板初始化完成');
}

/**
 * 初始化图像查看器
 */
function initializeImageViewer() {
    const container = document.getElementById('image-viewer');
    if (!container) {
        console.error('[App] 找不到图像查看器容器');
        return;
    }
    
    // 清空容器并初始化图像查看器组件
    imageViewer = new ImageViewerComponent('image-viewer');
    window.imageViewer = imageViewer;
    
    // 设置图像加载回调
    imageViewer.onImageLoaded = (img) => {
        console.log('[App] 图像已加载:', img.width, 'x', img.height);
    };
    
    // 设置标注点击回调
    imageViewer.onAnnotationClicked = (annotation) => {
        console.log('[App] 点击标注:', annotation);
    };
    
    console.log('[App] 图像查看器初始化完成');
}

function openImageViewerFromPreview(imageSource) {
    if (!imageSource || !window.imageViewer) {
        return;
    }

    void window.imageViewer.loadImage(imageSource)
        .then(() => {
            setCurrentView('image');
            syncActiveNavButton('image');
            return switchView('image');
        })
        .catch(error => {
            console.error('[App] 打开预览大图失败:', error);
            showToast(`打开预览大图失败: ${error.message}`, 'error');
        });
}

function initializeNodePreviewExperience() {
    if (!flowCanvas) {
        return;
    }

    if (!nodePreviewCoordinator) {
        nodePreviewCoordinator = new NodePreviewCoordinator({
            getProjectId: () => getCurrentProject()?.id || null,
            getFlowRevision: () => flowCanvas.getFlowRevision?.() || 0,
            getNodeById: nodeId => flowCanvas.nodes.get(nodeId) || null,
            getOperatorMetadata: type => findOperatorDefinition(type),
            getInputImageBase64: () => {
                const inspectionResult = window._lastInspectionResult || inspectionController.getLastResult?.();
                return resolvePreviewInputImageBase64(inspectionResult);
            },
            previewExecutor: (nodeId, options) => inspectionController.previewNode(nodeId, options),
            subscribeStructureState: listener => flowCanvas.subscribeStructureState(listener),
            debounceMs: 500
        });
        window.nodePreviewCoordinator = nodePreviewCoordinator;
    }

    if (!nodePreviewOverlay) {
        const container = document.querySelector('.flow-editor-container');
        if (container) {
            nodePreviewOverlay = new NodePreviewOverlay(container, flowCanvas, nodePreviewCoordinator, {
                onOpenImage: openImageViewerFromPreview
            });
            window.nodePreviewOverlay = nodePreviewOverlay;
        }
    }
}

/**
 * 初始化检测控制器
 */
function initializeInspectionController() {
    // 设置检测完成回调（调用方法注册回调，而非覆盖方法）
    inspectionController.onInspectionCompleted((result) => {
        console.log('[App] 检测完成:', result);

        // 【关键修复】保存最新的检测结果，以便切换视图时显示
        window._lastInspectionResult = result;

        // 如果在检测视图，立即更新检测面板和图像查看器
        if (getCurrentView() === 'inspection') {
            // 显示处理后的图像
            const outputImage = getInlineResultImageBase64(result);
            if (outputImage && window.inspectionImageViewer) {
                const imageData = `data:image/png;base64,${outputImage}`;
                window.inspectionImageViewer.loadImage(imageData);
            }



            // 更新结果面板
            updateInspectionResultsPanel(result);
        } else {
            // 【关键修复】如果不在检测视图，显示提示引导用户切换
            console.log('[App] 检测完成但不在检测视图，已保存结果');
        }

        const currentProject = getCurrentProject();
        const appendResultToPanel = (panel) => {
            if (!panel) {
                return;
            }

            const normalizedDefects = buildResultDefects(result);
            panel.setProjectContext(currentProject?.id || null);
            panel.addResult({
                status: result.status,
                defects: normalizedDefects,
                processingTime: result.processingTimeMs,
                timestamp: result.timestamp || result.inspectionTime || new Date().toISOString(),
                confidenceScore: result.confidenceScore,
                imageId: result.imageId || result.ImageId,
                imageData: getInlineResultImageBase64(result),
                outputImage: result.outputImage || result.OutputImage || null,
                outputImageBase64: result.outputImageBase64 || result.OutputImageBase64 || null,
                resultImageBase64: result.resultImageBase64 || result.ResultImageBase64 || null,
                outputData: normalizeOutputData(result),
                analysisData: normalizeAnalysisData(result)
            });

            if (currentProject?.id && typeof panel.loadServerAnalytics === 'function') {
                setTimeout(() => {
                    panel.loadServerAnalytics().catch(error => {
                        console.warn('[App] 刷新结果页服务端分析失败:', error);
                    });
                }, 300);
            }
        };

        if (resultPanel) {
            appendResultToPanel(resultPanel);
        } else {
            void ensureResultPanel()
                .then(panel => appendResultToPanel(panel))
                .catch(error => handleFeatureLoadError('结果面板', error));
        }

        // 显示结果提示
        let status = 'info';
        let message = '';

        if (result.status === 'OK') {
            status = 'success';
            message = '检测通过 (OK)';
        } else if (result.status === 'Error') {
            status = 'error';
            message = `检测错误: ${result.errorMessage || '未知错误'}`;
        } else {
            const defectCount = result.defectCount ?? result.DefectCount ?? buildResultDefects(result).length;
            status = 'warning';
            message = `检测到 ${defectCount} 个目标`;
        }
        showToast(message, status);
    });
    
    // 设置检测错误回调
    inspectionController.onInspectionError((error) => {
        console.error('[App] 检测错误:', error);
        showToast('检测失败: ' + error.message, 'error');
        
        // 更新检测面板状态
        if (inspectionPanel) {
            inspectionPanel.updateStatus('error', '检测错误');
            inspectionPanel.setButtonsState(false);
        }
    });
    
    console.log('[App] 检测控制器初始化完成');
}

/**
 * 初始化属性面板
 */
function initializePropertyPanel() {
    const container = document.getElementById('property-panel');
    if (!container) {
        console.error('[App] 找不到属性面板容器');
        return;
    }

    propertyPanel = new PropertyPanel('property-panel', {
        previewCoordinator: nodePreviewCoordinator,
        onOpenPreviewImage: openImageViewerFromPreview
    });
    window.propertyPanel = propertyPanel;

    // 【修复】订阅选中算子变化，使用trackedSubscribe防止内存泄漏
    trackedSubscribe(subscribeSelectedOperator, (operator) => {
        if (operator) {
            console.log('[App] 选中算子变化:', operator.title || operator.type);
            propertyPanel.setOperator(operator);
        } else {
            propertyPanel.clear();
        }
    });

    // 设置参数变更回调
    propertyPanel.onChange((values) => {
        console.log('[App] 算子参数变更:', values);
        // 更新流程图中对应节点的参数
        const operator = getSelectedOperator();
        if (operator && flowCanvas) {
            const node = flowCanvas.nodes.get(operator.id);
            if (node) {
                node.parameters = operator.parameters;
            }
        }
    });

    console.log('[App] 属性面板初始化完成');
}

/**
 * 加载检测历史数据
 */
function initializePropertySidebarController() {
    const handle = document.querySelector('[data-sidebar-resizer="property"]');
    if (!handle) {
        console.warn('[App] Property sidebar resizer not found');
        return;
    }

    propertySidebarController?.destroy?.();
    propertySidebarController = new PropertySidebarController({
        handle,
        root: document.documentElement,
        getCurrentView
    });
}

/**
 * 鍔犺浇妫€娴嬪巻鍙叉暟鎹?
 */
async function loadInspectionHistory({
    pageIndex = 0,
    pageSize = resultPanel?.pageSize ?? 12,
    startTime = resultPanel?.getAnalyticsQueryParams?.().startTime,
    endTime = resultPanel?.getAnalyticsQueryParams?.().endTime,
    status = resultPanel?.getAnalyticsQueryParams?.().status,
    defectType = resultPanel?.getAnalyticsQueryParams?.().defectType
} = {}) {
    const project = getCurrentProject();
    if (!project) {
        console.log('[App] 没有打开的工程，跳过加载历史数据');
        return false;
    }

    try {
        console.log('[App] 正在加载检测历史数据...');
        const response = await httpClient.get(`/inspection/history/${project.id}`, {
            pageIndex,
            pageSize,
            ...(startTime ? { startTime } : {}),
            ...(endTime ? { endTime } : {}),
            ...(status ? { status } : {}),
            ...(defectType ? { defectType } : {})
        });

        const results = Array.isArray(response)
            ? response
            : (response?.items || response?.Items || []);
        const totalCount = Array.isArray(response)
            ? results.length
            : (response?.totalCount ?? response?.TotalCount ?? results.length);
        const resolvedPageIndex = Array.isArray(response)
            ? pageIndex
            : (response?.pageIndex ?? response?.PageIndex ?? pageIndex);
        const resolvedPageSize = Array.isArray(response)
            ? pageSize
            : (response?.pageSize ?? response?.PageSize ?? pageSize);

        if (resultPanel) {
            resultPanel.setProjectContext(project.id);
        }

        if (Array.isArray(results) && resultPanel) {
            const normalizedResults = results.map(result => ({
                id: result.id || result.Id,
                status: result.status,
                defects: buildResultDefects(result),
                defectCount: result.defectCount ?? result.DefectCount ?? result.defects?.length ?? result.Defects?.length ?? 0,
                processingTime: result.processingTimeMs ?? result.ProcessingTimeMs ?? result.processingTime ?? result.executionTimeMs,
                timestamp: result.timestamp || result.Timestamp || result.inspectionTime || result.InspectionTime,
                confidenceScore: result.confidenceScore ?? result.ConfidenceScore,
                imageData: getInlineResultImageBase64(result),
                outputImage: result.outputImage || result.OutputImage || null,
                outputImageBase64: result.outputImageBase64 || result.OutputImageBase64 || null,
                resultImageBase64: result.resultImageBase64 || result.ResultImageBase64 || null,
                imageId: result.imageId || result.ImageId,
                outputData: normalizeOutputData(result),
                analysisData: normalizeAnalysisData(result)
            }));

            resultPanel.loadResults(normalizedResults, {
                totalCount,
                pageIndex: resolvedPageIndex,
                pageSize: resolvedPageSize,
                serverPaged: true
            });

            if (typeof resultPanel.loadServerAnalytics === 'function') {
                await resultPanel.loadServerAnalytics();
            }

            console.log(`[App] 已加载 ${normalizedResults.length} 条历史检测记录`);
        }

        return true;
    } catch (error) {
        console.error('[App] 加载检测历史数据失败:', error);
        return false;
    }
}

/**
 * 初始化算子库
 */
async function initializeOperatorLibrary() {
    try {
        // 从后端获取算子库
        const operators = await httpClient.get('/operators/library');
        setOperatorLibrary(operators);
        renderOperatorLibrary(operators);
    } catch (error) {
        console.error('[App] 加载算子库失败:', error);
        // 使用默认算子数据
        renderOperatorLibrary(getDeprecatedDefaultOperators());
    }
}

/**
 * 渲染算子库
 */
function renderOperatorLibrary(operators) {
    const container = document.getElementById('operator-library');
    
    // 按类别分组
    const categories = groupByCategory(operators);
    
    container.innerHTML = Object.entries(categories).map(([category, items]) => `
        <div class="operator-category">
            <div class="category-title">${category}</div>
            ${items.map(op => `
                <div class="operator-item" draggable="true" data-type="${op.type}">
                    <div class="operator-icon">${op.iconName?.charAt(0).toUpperCase() || '?'}</div>
                    <span class="operator-name">${op.displayName}</span>
                </div>
            `).join('')}
        </div>
    `).join('');
    
    // 添加拖拽事件
    container.querySelectorAll('.operator-item').forEach(item => {
        item.addEventListener('dragstart', handleDragStart);
    });
}

/**
 * 按类别分组
 */
function groupByCategory(operators) {
    return operators.reduce((acc, op) => {
        const category = op.category || '其他';
        if (!acc[category]) {
            acc[category] = [];
        }
        acc[category].push(op);
        return acc;
    }, {});
}

/**
 * 获取默认算子数据
 */
function getDeprecatedDefaultOperators() {
    return [];
    /*
        { type: 'ImageAcquisition', displayName: '图像采集', category: '输入', iconName: 'camera' },
        { type: 'Filtering', displayName: '滤波', category: '预处理', iconName: 'filter' },
        { type: 'EdgeDetection', displayName: '边缘检测', category: '特征提取', iconName: 'edge' },
        { type: 'Thresholding', displayName: '二值化', category: '预处理', iconName: 'threshold' },
        { type: 'ResultOutput', displayName: '结果输出', category: '输出', iconName: 'output' }
    ];
    */
}

/**
 * 处理拖拽开始
 */
function handleDragStart(event) {
    const operatorType = event.target.dataset.type;
    event.dataTransfer.setData('operatorType', operatorType);
}

/**
 * 初始化流程编辑器
 */
function initializeFlowEditor() {
    const canvas = document.getElementById('flow-canvas');
    if (!canvas) {
        console.error('[App] 找不到流程编辑器画布');
        return;
    }
    
    // 使用 FlowCanvas 类初始化
    flowCanvas = new FlowCanvas('flow-canvas');
    initializeNodePreviewExperience();
    
    // 设置节点选中回调
    flowCanvas.onNodeSelected = (node) => {
        if (node) {
            console.log('[App] 节点选中:', node.title || node.type);
            // 【修复】构造算子数据传递给属性面板 —— 使用算子库定义补全信息
            const operatorDef = findOperatorDefinition(node.type);
            setSelectedOperator({
                id: node.id,
                type: node.type,
                title: node.title || operatorDef?.displayName || node.type,
                displayName: operatorDef?.displayName || node.title || node.type,
                iconPath: node.iconPath || operatorDef?.iconPath || null,
                color: node.color || null,
                inputPorts: node.inputs || operatorDef?.inputPorts || [],
                outputPorts: node.outputs || operatorDef?.outputPorts || [],
                parameters: mergeParameters(operatorDef?.parameters, node.parameters)
            });
            nodePreviewCoordinator?.setActiveNode(node);
        } else {
            setSelectedOperator(null);
            nodePreviewCoordinator?.setActiveNode(null);
        }
    };
    
    // 保存到全局以便其他函数使用
    window.flowCanvas = flowCanvas;
    
    // 【阶段B】支持 ForEach 子图双击进入与退出
    const breadcrumbContainer = document.getElementById('subgraph-breadcrumb');
    const breadcrumbCurrent = document.getElementById('breadcrumb-current');
    const btnExitSubgraph = document.getElementById('btn-exit-subgraph');
    
    window._mainFlowState = null;
    window._currentSubgraphNodeId = null;

    flowCanvas.onNodeDoubleClicked = (node) => {
        if (node.type === 'ForEach') {
            console.log('[App] 进入 ForEach 子图:', node.id);
            // 序列化主图状态并挂载到临时全局变量
            window._mainFlowState = flowCanvas.serialize();
            window._currentSubgraphNodeId = node.id;
            
            // 读取 IoMode
            const ioModeParam = node.parameters?.find(p => p.name === 'IoMode' || p.Name === 'IoMode');
            const ioMode = ioModeParam?.value || 'Parallel';
            
            // 从 ForEach 参数中获取内部流程数据
            let subGraphData = null;
            const flowParam = node.parameters?.find(p => p.name === 'SubGraph' || p.Name === 'SubGraph');
            if (flowParam && flowParam.value) {
                try {
                    subGraphData = typeof flowParam.value === 'string' ? JSON.parse(flowParam.value) : flowParam.value;
                } catch (e) {
                    console.error('[App] 解析子图失败:', e);
                }
            }
            
            flowCanvas.clear();
            if (subGraphData) {
                flowCanvas.deserialize(subGraphData);
            }
            
            // 注入 CurrentItem 系统源节点（如果不存在）
            const existingCurrentItem = Array.from(flowCanvas.nodes.values()).find(n => n.type === 'CurrentItem');
            if (!existingCurrentItem) {
                flowCanvas.addNode('CurrentItem', 50, 100, {
                    title: '📦 CurrentItem',
                    color: '#722ed1',
                    outputs: [{ name: 'Item', type: 'Any' }, { name: 'Index', type: 'Integer' }, { name: 'Total', type: 'Integer' }],
                    _systemNode: true  // 标记为系统节点，不可删除
                });
                console.log('[App] 已注入 CurrentItem 系统源节点');
            }
            
            if (breadcrumbContainer) {
                breadcrumbContainer.classList.remove('hidden');
                const ioLabel = ioMode === 'Sequential' ? '🔗 串行' : '⚡ 并行';
                breadcrumbCurrent.textContent = `${node.title || 'ForEach'} [${ioLabel}]`;
            }
        }
    };

    if (btnExitSubgraph) {
        btnExitSubgraph.addEventListener('click', () => {
            if (window._mainFlowState && window._currentSubgraphNodeId) {
                console.log('[App] 退出并保存 ForEach 子图');
                const subFlowData = flowCanvas.serialize();
                let mainData = window._mainFlowState;
                
                // 将子图数据写回主图节点中
                try {
                    let mainObj = typeof mainData === 'string' ? JSON.parse(mainData) : mainData;
                    let targetNode = mainObj.nodes.find(n => n.id === window._currentSubgraphNodeId);
                    if (targetNode) {
                        if (!targetNode.parameters) targetNode.parameters = [];
                        let flowParam = targetNode.parameters.find(p => p.name === 'SubGraph' || p.Name === 'SubGraph');
                        
                        let subDataStr = typeof subFlowData === 'string' ? subFlowData : JSON.stringify(subFlowData);
                        
                        // 保留原有参数逻辑并更新或插入
                        if (flowParam) {
                            flowParam.value = subDataStr;
                        } else {
                            targetNode.parameters.push({
                                name: 'SubGraph',
                                value: subDataStr,
                                type: 'string'
                            });
                        }
                    }
                    mainData = typeof mainData === 'string' ? JSON.stringify(mainObj) : mainObj;
                } catch(e) { 
                    console.error('[App] 保存子图失败:', e); 
                }

                flowCanvas.clear();
                flowCanvas.deserialize(mainData);
                
                window._mainFlowState = null;
                window._currentSubgraphNodeId = null;
                
                if (breadcrumbContainer) {
                    breadcrumbContainer.classList.add('hidden');
                }
            }
        });
    }
    
    // 【阶段B】初始化流程编辑器交互增强（撤销/重做/复制/粘贴/框选）
    flowEditorInteraction = new FlowEditorInteraction(flowCanvas);
    window.flowEditorInteraction = flowEditorInteraction;
    console.log('[App] 流程编辑器交互增强已启用');
    
    // 【阶段B】启动自动保存
    startAutoSave();
    
    console.log('[App] 流程编辑器初始化完成');
}


/**
 * 添加算子到流程（保留作为备用入口）
 */
function addOperatorToFlow(type, x, y, data = null) {
    console.log('[App] 添加算子:', type, '位置:', x, y);
    
    if (!window.flowCanvas) {
        console.error('[App] FlowCanvas 未初始化');
        return;
    }
    
    const nodeConfig = buildOperatorNodeConfig(type, data);
    
    // 添加节点到画布
    const node = window.flowCanvas.addNode(type, x, y, nodeConfig);
    
    console.log('[App] 算子已添加:', node);
    
    // 选中该节点
    window.flowCanvas.selectedNode = node.id;
    window.flowCanvas.render();
}

/**
 * 初始化 WebMessage 通信
 */
function initializeWebMessage() {
    // 注册消息处理器
    webMessageBridge.on('operatorExecuted', (data) => {
        console.log('[App] 算子执行完成:', data);
        updateResults(data);
    });
    
    webMessageBridge.on('inspectionCompleted', (data) => {
        console.log('[App] 检测完成:', data);
        updateResults(data);
    });
}

/**
 * 处理新建工程
 */
function handleNewProject(options = {}) {
    const { preserveCanvas = false } = options;
    const nameInput = createLabeledInput({ label: '工程名称', required: true, placeholder: 'Project_' + Date.now() });
    const descInput = createLabeledInput({ label: '描述', placeholder: '工程描述...' });
    
    const content = document.createElement('div');
    content.appendChild(nameInput);
    content.appendChild(descInput);
    
    let modalOverlay = null;

    const btnCancel = createButton({ 
        text: '取消', 
        type: 'secondary', 
        onClick: () => closeModal(modalOverlay) 
    });
    
    const btnCreate = createButton({ 
        text: '创建', 
        onClick: () => {
            const name = nameInput.querySelector('input').value;
            const desc = descInput.querySelector('input').value;
            
            if (!name) { 
                showToast('请输入工程名称', 'warning'); 
                return; 
            }
            
            createProject(name, desc, preserveCanvas)
                .then(() => {
                    closeModal(modalOverlay);
                    // 切换到流程视图
                    switchView('flow'); 
                    document.querySelector('[data-view="flow"]')?.click();
                })
                .catch(err => {
                    // error handled in createProject
                });
        } 
    });
    
    modalOverlay = createModal({
        title: preserveCanvas ? '保存为新工程' : '新建工程',
        content,
        footer: [btnCancel, btnCreate],
        width: '400px'
    });
}


/**
 * 初始化工具栏按钮
 */
function initializeToolbar() {
    // 注意："新建"和"导入图片"按钮已移至工程分页
    // 由 projectView.js 处理
    
    
    // 保存按钮
    const saveBtn = document.getElementById('btn-save');
    if (saveBtn) {
        saveBtn.addEventListener('click', async () => {
            console.log('[App] 保存工程');
            try {
                // 【修复】保存前强制提交当前属性面板的更改
                if (window.propertyPanel && window.propertyPanel.currentOperator) {
                    console.log('[App] 强制刷新当前属性面板更改');
                    window.propertyPanel.applyChanges();
                }

                const project = getCurrentProject();
                if (project) {
                    // 使用 projectManager.saveProject 正确保存工程
                    // PM 的状态已是最新的，无需手动赋值
                    
                    // 将流程数据序列化
                    if (window.flowCanvas) {
                        project.flow = window.flowCanvas.serialize();
                        console.log('[App] 流程数据已序列化:', project.flow);
                    }
                    
                    // 调用 projectManager 的保存方法
                    await projectManager.saveProject(project);
                    showToast('工程已保存', 'success');
                } else if (window.flowCanvas && window.flowCanvas.nodes.size > 0) {
                    // 没有打开工程但画布上有算子，弹出新建工程对话框（保留画布内容）
                    handleNewProject({ preserveCanvas: true });
                } else {
                    showToast('请先创建或打开工程', 'warning');
                }
            } catch (error) {
                console.error('[App] 保存失败:', error);
                showToast('保存失败: ' + error.message, 'error');
            }
        });
    }
    
    // 运行按钮
    const runBtn = document.getElementById('btn-run');
    if (runBtn) {
        runBtn.addEventListener('click', async () => {
            console.log('[App] 运行检测');
            const project = getCurrentProject();
            
            if (!project) {
                showToast('请先打开或创建工程', 'warning');
                return;
            }
            
            if (!window.flowCanvas || window.flowCanvas.nodes.size === 0) {
                showToast('请先添加算子到流程', 'warning');
                return;
            }
            
            try {
                // 切换到检测视图
                setCurrentView('inspection');
                syncActiveNavButton('inspection');
                await switchView('inspection');
                
                // 设置当前工程
                inspectionController.setProject(project.id);
                
                const panel = await ensureInspectionPanelReady();
                initializeInspectionImageViewer();
                
                if (panel) {
                    panel.updateStatus('running', '运行中...');
                    panel.setButtonsState(true);
                }
                
                const testImage = imageViewer?.currentTestImage;
                
                if (testImage) {
                    showToast('使用导入图像执行检测...', 'info');
                    await inspectionController.executeSingle(testImage);
                } else {
                    // 【关键修复】即使没有显式加载图像，也允许执行。
                    // 图像可能由流程内部的"图像采集"算子从文件加载。
                    showToast('开始执行检测流程...', 'info');
                    await inspectionController.executeSingle();
                }
            } catch (error) {
                console.error('[App] 运行检测失败:', error);
                showToast('检测失败: ' + error.message, 'error');
            }
        });
    }

    // 登出按钮
    const logoutBtn = document.getElementById('btn-logout');
    if (logoutBtn) {
        logoutBtn.addEventListener('click', async () => {
            console.log('[App] 用户登出');
            await logout();
        });
    }

    // AI 生成按钮
    const aiGenBtn = document.getElementById('btn-ai-gen');
    if (aiGenBtn) {
        aiGenBtn.addEventListener('click', () => {
            console.log('[App] 切换到 AI 视图');
            
            // 切换视图
            setCurrentView('ai');
            switchView('ai');
            
            // 更新导航按钮激活状态
            document.querySelectorAll('.nav-btn').forEach(btn => {
                btn.classList.remove('active');
                if (btn.dataset.view === 'ai') btn.classList.add('active');
            });
        });
    }
}

/**
 * 初始化 AI 生成对话框
 */
function initializeAiGeneration() {
    console.log('[App] AI 生成功能已升级为独立面板');
}

/**
 * 加载工程
 */
async function loadProject(projectId) {
    try {
        // 委托给 projectManager（内部会设置信号 + 更新状态栏）
        const project = await projectManager.openProject(projectId);
        
        // 加载流程到画布
        if (project.flow && window.flowCanvas) {
            console.log('[App] 加载流程数据:', project.flow);
            window.flowCanvas.deserialize(project.flow);
        } else if (window.flowCanvas) {
            // 【修复】如果没有流程数据，清空画布
            console.log('[App] 工程没有流程数据，清空画布');
            window.flowCanvas.clear();
        }
        
        // 设置检测控制器和结果页上下文
        inspectionController.setProject(projectId);
        if (resultPanel) {
            resultPanel.setProjectContext(projectId);
            resultPanel.clear();
        }
        
        showToast(`工程 "${project.name}" 已加载`, 'success');
        return project;
    } catch (error) {
        console.error('[App] 加载工程失败:', error);
        showToast('加载工程失败: ' + error.message, 'error');
        throw error;
    }
}

/**
 * 创建新工程
 */
async function createProject(name, description = '', preserveCanvas = false) {
    try {
        // 委托给 projectManager（内部会设置信号 + 更新状态栏）
        const project = await projectManager.createProject(name, description);
        
        if (preserveCanvas) {
            // 保留画布内容，直接将当前流程保存到新工程
            if (window.flowCanvas) {
                project.flow = window.flowCanvas.serialize();
                await projectManager.saveProject(project);
                console.log('[App] 画布内容已保存到新工程:', project.name);
            }
        } else {
            // 新建空工程，清空画布
            if (window.flowCanvas) {
                window.flowCanvas.clear();
            }
        }
        
        // 设置检测控制器和结果页上下文
        inspectionController.setProject(project.id);
        if (resultPanel) {
            resultPanel.setProjectContext(project.id);
            resultPanel.clear();
        }
        
        showToast(`工程 "${name}" 已创建`, 'success');
        return project;
    } catch (error) {
        console.error('[App] 创建工程失败:', error);

        // 处理连接错误，提供更友好的提示
        let errorMsg = error.message;
        if (errorMsg.includes('无法连接到后端服务')) {
            Dialog.alert(
                '连接失败',
                errorMsg.replace(/\n/g, '<br>'),
                null
            );
        } else {
            showToast('创建工程失败: ' + errorMsg, 'error');
        }
        throw error;
    }
}

/**
 * 初始化主题
 */
function initializeTheme() {
    // 读取保存的主题
    const savedTheme = localStorage.getItem('cv_theme') || 'light';
    document.documentElement.dataset.theme = savedTheme;

    // 绑定切换按钮
    const themeToggle = document.getElementById('btn-theme-toggle');
    if (themeToggle) {
        themeToggle.addEventListener('click', toggleTheme);
    }
}

/**
 * 切换主题
 */
function toggleTheme() {
    const current = document.documentElement.dataset.theme;
    const next = current === 'dark' ? 'light' : 'dark';
    document.documentElement.dataset.theme = next;
    localStorage.setItem('cv_theme', next);

    // 显示提示
    const message = next === 'dark' ? '已切换到暗色模式' : '已切换到亮色模式';
    showToast(message, 'info');
}

/**
 * 【阶段B-B4】启动自动保存
 * 【修复】防止重复启动定时器
 */
function startAutoSave() {
    // 【修复】确保先停止现有的定时器
    stopAutoSave();
    
    autoSaveInterval = setInterval(async () => {
        const project = getCurrentProject();
        if (project && window.flowCanvas && window.flowCanvas.nodes.size > 0) {
            try {
                // 更新流程数据
                project.flow = window.flowCanvas.serialize();
                // 保存到本地存储作为备份
                localStorage.setItem('cv_autosave_backup', JSON.stringify({
                    projectId: project.id,
                    timestamp: new Date().toISOString(),
                    flow: project.flow
                }));
                console.log('[AutoSave] 自动保存完成:', new Date().toLocaleTimeString());
            } catch (err) {
                console.error('[AutoSave] 自动保存失败:', err);
            }
        }
    }, AUTO_SAVE_DELAY);
    
    console.log('[AutoSave] 自动保存已启动，间隔:', AUTO_SAVE_DELAY / 1000 / 60, '分钟');
}

/**
 * 【阶段B-B4】停止自动保存
 */
function stopAutoSave() {
    if (autoSaveInterval) {
        clearInterval(autoSaveInterval);
        autoSaveInterval = null;
        console.log('[AutoSave] 自动保存已停止');
    }
}

/**
 * 【阶段B-B4】立即执行自动保存
 */
async function triggerAutoSave() {
    const project = getCurrentProject();
    if (project && window.flowCanvas) {
        try {
            project.flow = window.flowCanvas.serialize();
            localStorage.setItem('cv_autosave_backup', JSON.stringify({
                projectId: project.id,
                timestamp: new Date().toISOString(),
                flow: project.flow
            }));
            console.log('[AutoSave] 手动触发保存完成');
            showToast('流程草稿已保存到本地缓存', 'success');
        } catch (err) {
            console.error('[AutoSave] 手动保存失败:', err);
            showToast('本地草稿保存失败', 'error');
        }
    }
}

/**
 * 【阶段B-B5】导出工程为JSON文件
 */
function exportProjectToJson() {
    const project = getCurrentProject();
    if (!project) {
        showToast('没有可导出的工程', 'warning');
        return;
    }
    
    try {
        // 准备导出数据
        const exportData = {
            version: '1.0',
            exportTime: new Date().toISOString(),
            project: {
                id: project.id,
                name: project.name,
                description: project.description,
                createdAt: project.createdAt,
                updatedAt: new Date().toISOString(),
                flow: window.flowCanvas ? window.flowCanvas.serialize() : project.flow
            }
        };
        
        // 创建下载链接
        const blob = new Blob([JSON.stringify(exportData, null, 2)], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${project.name || 'project'}_${new Date().toISOString().slice(0, 10)}.cvproj.json`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
        
        showToast('工程导出成功', 'success');
        console.log('[Export] 工程已导出:', project.name);
    } catch (err) {
        console.error('[Export] 导出失败:', err);
        showToast('工程导出失败', 'error');
    }
}

/**
 * 【修复】根据算子类型查找算子库中的定义数据
 * @param {string} type - 算子类型
 * @returns {Object|null} 算子定义数据
 */
function findOperatorDefinition(type) {
    if (!operatorLibraryPanel) return null;
    const operators = operatorLibraryPanel.getOperators ? operatorLibraryPanel.getOperators() : [];
    return operators.find(op => op.type === type) || null;
}

/**
 * 【修复】合并参数定义与参数值
 * @param {Array} defParams - 算子库中的参数定义（基准）
 * @param {Array} nodeParams - 画布节点保存的参数值
 * @returns {Array} 合并后的参数列表
 */
function mergeParameters(defParams, nodeParams) {
    if (!defParams || defParams.length === 0) return nodeParams || [];
    
    return defParams.map(defP => {
        // [修复] 不区分大小写匹配，解决前端 (camelCase) 与后端 (PascalCase) 的差异
        const nodeP = (nodeParams || []).find(np => 
            (np.name && defP.name && np.name.toLowerCase() === defP.name.toLowerCase()) ||
            (np.Name && defP.name && np.Name.toLowerCase() === defP.name.toLowerCase())
        );
        
        const mergedParam = { 
            ...defP,
            // 优先使用节点保存的值 (Value 或 value)
            value: nodeP !== undefined ? (nodeP.value ?? nodeP.Value ?? nodeP.defaultValue ?? nodeP.DefaultValue) : defP.defaultValue
        };
        
        return mergedParam;
    });
}

/**
 * 【阶段B-B5】从JSON文件导入工程
 * @param {File} file - 用户选择的文件
 */
async function importProjectFromJson(file) {
    if (!file) return;
    
    try {
        const content = await file.text();
        const importData = JSON.parse(content);
        
        // 验证文件格式
        if (!importData.project || !importData.project.flow) {
            throw new Error('无效的工程文件格式');
        }
        
        // 确认导入
        const confirmed = confirm(`确定要导入工程 "${importData.project.name || '未命名'}" 吗？\n当前未保存的更改将会丢失。`);
        if (!confirmed) return;
        
        // 通过 projectManager 创建新工程（由后端生成 ID）
        const importName = (importData.project.name || '未命名') + ' (导入)';
        const importDesc = importData.project.description || '';
        const project = await projectManager.createProject(importName, importDesc);
        
        // 加载流程到画布
        if (window.flowCanvas && importData.project.flow) {
            window.flowCanvas.deserialize(importData.project.flow);
            // 将流程数据保存到后端
            project.flow = window.flowCanvas.serialize();
            await projectManager.saveProject(project);
        }
        
        // 设置检测控制器的工程
        inspectionController.setProject(project.id);
        
        // 切换到流程视图
        switchView('flow');
        document.querySelectorAll('.nav-btn').forEach(btn => {
            btn.classList.remove('active');
            if (btn.dataset.view === 'flow') btn.classList.add('active');
        });
        
        showToast('工程导入成功', 'success');
        console.log('[Import] 工程已导入:', project.name);
        
        // 刷新工程列表
        if (projectView) {
            projectView.refresh();
        }
    } catch (err) {
        console.error('[Import] 导入失败:', err);
        showToast('工程导入失败: ' + err.message, 'error');
    }
}

/**
 * 【阶段B-B5】显示导入对话框
 */
function showImportDialog() {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.cvproj.json,.json';
    input.onchange = (e) => {
        const file = e.target.files[0];
        if (file) {
            importProjectFromJson(file);
        }
    };
    input.click();
}

// Expose import/export functions globally for projectView.js
window.showImportDialog = showImportDialog;
window.exportProjectToJson = exportProjectToJson;

// ==========================================================================
// 阶段五：状态栏更新功能
// ==========================================================================

/**
 * 更新状态栏指标
 */
function updateStatusBar() {
    // 更新内存使用
    if (performance && performance.memory) {
        const memoryMB = Math.round(performance.memory.usedJSHeapSize / 1024 / 1024);
        const memoryEl = document.querySelector('#memory-usage .metric-value');
        if (memoryEl) memoryEl.textContent = `${memoryMB} MB`;
    }
}

/**
 * FPS 计数器
 */
let fpsCounter = {
    frames: 0,
    lastTime: performance.now()
};
let statusBarInterval = null;

function updateFPS() {
    if (!statusBarStarted) {
        fpsAnimationFrameId = null;
        return;
    }

    const now = performance.now();
    fpsCounter.frames++;
    
    if (now - fpsCounter.lastTime >= 1000) {
        const fps = Math.round(fpsCounter.frames * 1000 / (now - fpsCounter.lastTime));
        const fpsEl = document.querySelector('#fps-counter .metric-value');
        if (fpsEl) fpsEl.textContent = `${fps} FPS`;
        
        fpsCounter.frames = 0;
        fpsCounter.lastTime = now;
    }
    
    fpsAnimationFrameId = requestAnimationFrame(updateFPS);
}

// 启动状态栏更新
function startStatusBarUpdates() {
    if (statusBarStarted) {
        return;
    }

    statusBarStarted = true;
    statusBarInterval = setInterval(updateStatusBar, 1000);
    updateStatusBar();

    if (fpsAnimationFrameId === null) {
        fpsAnimationFrameId = requestAnimationFrame(updateFPS);
    }
}

// ==========================================================================
// 阶段五：视图切换过渡动画
// ==========================================================================

/**
 * 切换视图（带过渡动画）
 */
function switchViewWithTransition(view) {
    const views = ['flow', 'inspection', 'results', 'project'];
    const currentView = getCurrentView();
    
    if (currentView === view) return;
    
    // 获取当前显示的视图容器
    const currentContainer = document.getElementById(`${currentView}-view`) || 
                            document.getElementById(`${currentView}-editor`) ||
                            document.getElementById('flow-editor');
    
    if (currentContainer) {
        // 添加退出动画
        currentContainer.classList.add('view-exit');
        
        setTimeout(() => {
            currentContainer.classList.remove('view-exit');
            currentContainer.classList.add('hidden');
            
            // 显示新视图
            switchView(view);
            
            const newContainer = document.getElementById(`${view}-view`) || 
                               document.getElementById(`${view}-editor`) ||
                               document.getElementById('flow-editor');
            
            if (newContainer) {
                newContainer.classList.remove('hidden');
                newContainer.classList.add('view-enter');
                
                setTimeout(() => {
                    newContainer.classList.remove('view-enter');
                }, 300);
            }
        }, 300);
    } else {
        // 无动画直接切换
        switchView(view);
    }
}

// ==========================================================================
// 阶段五：加载骨架屏
// ==========================================================================

/**
 * 显示加载骨架屏
 */
function showLoadingScreen() {
    if (document.getElementById('loading-screen')) {
        return;
    }

    const loadingScreen = document.createElement('div');
    loadingScreen.id = 'loading-screen';
    loadingScreen.className = 'loading-screen';
    loadingScreen.innerHTML = `
        <div class="loading-logo">ClearVision</div>
        <div class="loading-spinner"></div>
        <div class="loading-text">正在加载...</div>
    `;
    document.body.appendChild(loadingScreen);
}

/**
 * 隐藏加载骨架屏
 */
function hideLoadingScreen() {
    const loadingScreen = document.getElementById('loading-screen');
    if (loadingScreen) {
        loadingScreen.classList.add('hidden');
        setTimeout(() => loadingScreen.remove(), 500);
    }
}

// ==========================================================================
// 阶段五：欢迎/引导页
// ==========================================================================

/**
 * 显示欢迎页
 */
function showWelcomeScreen() {
    // 检查是否首次运行
    const hasSeenWelcome = localStorage.getItem('cv_welcome_shown');
    if (hasSeenWelcome) return;
    
    const welcomeOverlay = document.createElement('div');
    welcomeOverlay.className = 'welcome-overlay';
    welcomeOverlay.innerHTML = `
        <div class="welcome-content">
            <h2 class="welcome-title">欢迎使用 ClearVision</h2>
            <p class="welcome-desc">工业级视觉检测平台，零代码搭建检测流程</p>
            <div class="welcome-features">
                <div class="welcome-feature">
                    <div class="welcome-feature-icon">🎨</div>
                    <div class="welcome-feature-title">拖拽式流程编排</div>
                </div>
                <div class="welcome-feature">
                    <div class="welcome-feature-icon">🔍</div>
                    <div class="welcome-feature-title">实时检测分析</div>
                </div>
                <div class="welcome-feature">
                    <div class="welcome-feature-icon">📊</div>
                    <div class="welcome-feature-title">数据可视化</div>
                </div>
            </div>
            <button class="btn btn-primary" id="btn-welcome-start">开始使用</button>
        </div>
    `;
    
    document.body.appendChild(welcomeOverlay);
    
    document.getElementById('btn-welcome-start').addEventListener('click', () => {
        localStorage.setItem('cv_welcome_shown', 'true');
        welcomeOverlay.style.opacity = '0';
        setTimeout(() => welcomeOverlay.remove(), 300);
    });
}

// 启动应用
document.addEventListener('DOMContentLoaded', () => {
    bootstrapApp().catch(error => {
        console.error('[App] 应用启动失败:', error);
        hideLoadingScreen();
        showToast(`应用启动失败: ${error.message}`, 'error');
    });
});

export { 
    getCurrentView, 
    setCurrentView, 
    getSelectedOperator, 
    setSelectedOperator,
    loadProject,
    createProject,
    imageViewer,
    operatorLibraryPanel,
    flowCanvas,
    flowEditorInteraction,
    exportProjectToJson,
    importProjectFromJson,
    showImportDialog,
    triggerAutoSave
};

