/**
 * 主应用入口 - S4-006: 端到端集成
 * Sprint 4: 前后端集成与用户体验闭环
 */

import { Dialog } from './shared/components/dialog.js';
import { AiPanel } from './features/ai/aiPanel.js';

// ============================================
// 全局错误捕获 - 用于调试
// ============================================
// 存储错误日志（限制最大条目数防止内存泄漏）
window._errorLogs = [];

const MAX_ERROR_LOGS = 100;  // 【修复】限制错误日志最大数量

function addErrorLog(logEntry) {
    window._errorLogs.push(logEntry);
    // 【修复】当超过最大限制时，移除最旧的日志
    if (window._errorLogs.length > MAX_ERROR_LOGS) {
        window._errorLogs.shift();
    }
}

window.onerror = function(message, source, lineno, colno, error) {
    const errorInfo = `[Global Error] ${message} at ${source}:${lineno}`;
    console.error(errorInfo);
    addErrorLog({
        type: 'Error',
        message: message,
        source: source,
        line: lineno,
        time: new Date().toLocaleTimeString()
    });
    const debugDiv = document.getElementById('debug-errors');
    if (debugDiv) {
        // 【修复】限制调试面板的DOM元素数量
        const errorDiv = document.createElement('div');
        errorDiv.style.cssText = 'color:red;margin:2px 0';
        errorDiv.textContent = `❌ ${message} (Line ${lineno})`;
        debugDiv.appendChild(errorDiv);
        // 保持最多50条DOM记录
        while (debugDiv.children.length > 50) {
            debugDiv.removeChild(debugDiv.firstChild);
        }
    }
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
    const debugDiv = document.getElementById('debug-errors');
    if (debugDiv) {
        const errorDiv = document.createElement('div');
        errorDiv.style.cssText = 'color:orange;margin:2px 0';
        errorDiv.textContent = `⚠️ Promise: ${errorMsg}`;
        debugDiv.appendChild(errorDiv);
        // 【修复】限制调试面板的DOM元素数量
        while (debugDiv.children.length > 50) {
            debugDiv.removeChild(debugDiv.firstChild);
        }
    }
});

console.log('[App] Starting module imports...');

// ============================================
// 认证检查 - 未登录则跳转
// ============================================
import { initAuth, logout, validateTokenAsync } from './features/auth/auth.js';
if (!initAuth()) {
    // initAuth 会处理跳转，这里直接返回
    throw new Error('未登录，正在跳转...');
}

import webMessageBridge from './core/messaging/webMessageBridge.js';
import httpClient from './core/messaging/httpClient.js';
import { createSignal } from './core/state/store.js';
import FlowCanvas from './core/canvas/flowCanvas.js';
import { FlowEditorInteraction } from './features/flow-editor/flowEditorInteraction.js';
import { ImageViewerComponent } from './features/image-viewer/imageViewer.js';
import { OperatorLibraryPanel } from './features/operator-library/operatorLibrary.js';
import inspectionController from './features/inspection/inspectionController.js';
import { InspectionPanel } from './features/inspection/inspectionPanel.js';
import { showToast, createModal, closeModal, createInput, createLabeledInput, createButton } from './shared/components/uiComponents.js';
import { PropertyPanel } from './features/flow-editor/propertyPanel.js';
import { ProjectView } from './features/project/projectView.js';
import projectManager, { 
    getCurrentProject, 
    setCurrentProject,
    subscribeProject 
} from './features/project/projectManager.js';
import { ResultPanel } from './features/results/resultPanel.js';

// 全局状态
const [getCurrentView, setCurrentView, subscribeView] = createSignal('flow');
const [getSelectedOperator, setSelectedOperator, subscribeSelectedOperator] = createSignal(null);
const [getOperatorLibrary, setOperatorLibrary, subscribeOperatorLibrary] = createSignal([]);

// 【修复】订阅管理器，防止内存泄漏
const subscriptions = [];

// 【修复】包装订阅函数，自动跟踪取消函数
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
let projectView = null;
let resultPanel = null;
let inspectionPanel = null;
let aiPanel = null;

// 自动保存定时器
let autoSaveInterval = null;
const AUTO_SAVE_DELAY = 5 * 60 * 1000; // 5分钟

/**
 * 初始化应用
 */
function initializeApp() {
    console.error('!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!');
    console.error('[App] 初始化应用正在执行...');
    console.error('!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!');
    console.log('[App] 初始化应用...');
    
    // 后台验证 Token 有效性
    validateTokenAsync().then(isValid => {
        if (isValid) {
            // Update user info in status bar
            const userNameEl = document.getElementById('user-display-name');
            if (userNameEl && window.currentUser) {
                userNameEl.textContent = window.currentUser.displayName || window.currentUser.username || '--';
            }
        } else {
            console.warn('[App] Token 无效或已过期，正在跳转到登录页...');
            showToast('登录已过期，请重新登录', 'warning');
            // 延迟跳转，让用户看清提示
            setTimeout(() => logout(), 1500);
        }
    });
    
    // 显示加载骨架屏
    showLoadingScreen();
    
    console.log('[App] Debug indicators removed for production');
    
    // 初始化导航
    initializeNavigation();
    
    // 初始化算子库面板
    initializeOperatorLibraryPanel();
    
    // 初始化流程编辑器
    initializeFlowEditor();
    
    // 初始化图像查看器
    initializeImageViewer();
    
    // 初始化 WebMessage 通信
    initializeWebMessage();
    
    // 初始化检测控制器
    initializeInspectionController();
    
    // 初始化属性面板
    initializePropertyPanel();

    // 初始化工程视图
    initializeProjectView();

    // 初始化结果面板（数显功能）
    initializeResultPanel();

    // 初始化 AI 生成对话框

    // 初始化主题
    initializeTheme();

    console.log('[App] 应用初始化完成');

    // 初始化工具栏按钮
    initializeToolbar();
    
    // 【修复】启动状态栏更新（内存、FPS）
    startStatusBarUpdates();
    console.log('[App] 状态栏更新已启动');
    
    // 显示欢迎消息
    showToast('ClearVision 已就绪', 'success');
}

/**
 * 初始化导航
 */
function initializeNavigation() {
    const navButtons = document.querySelectorAll('.nav-btn');
    
    navButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            // 更新活动状态
            navButtons.forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            
            // 切换视图
            const view = btn.dataset.view;
            setCurrentView(view);
            switchView(view);
        });
    });
}

/**
 * 切换视图
 */
function switchView(view) {
    console.log(`[App] 切换视图到: ${view}`);
    const flowEditor = document.getElementById('flow-editor');
    const imageViewerContainer = document.getElementById('image-viewer');
    const inspectionViewContainer = document.getElementById('inspection-view');
    const resultsViewContainer = document.getElementById('results-view');
    const projectViewContainer = document.getElementById('project-view');
    const aiViewContainer = document.getElementById('ai-view');
    const settingsViewContainer = document.getElementById('settings-view');

    // 隐藏所有视图
    flowEditor?.classList.add('hidden');
    imageViewerContainer?.classList.add('hidden');
    inspectionViewContainer?.classList.add('hidden');
    resultsViewContainer?.classList.add('hidden');
    projectViewContainer?.classList.add('hidden');
    aiViewContainer?.classList.add('hidden');
    settingsViewContainer?.classList.add('hidden');

    // 获取侧边栏元素
    const leftSidebar = document.querySelector('.sidebar.left');
    const rightSidebar = document.querySelector('.sidebar.right');

    // 控制侧边栏可见性：仅流程视图显示
    if (view === 'flow') {
        leftSidebar?.classList.remove('hidden');
        rightSidebar?.classList.remove('hidden');
    } else {
        leftSidebar?.classList.add('hidden');
        rightSidebar?.classList.add('hidden');
    }

    switch (view) {
        case 'flow':
            flowEditor?.classList.remove('hidden');
            // 【关键修复】切换回流程视图时，强制触发 Resize 以确保画布尺寸正确
            requestAnimationFrame(() => {
                if (window.flowCanvas) {
                    window.flowCanvas.resize();
                }
            });
            break;
        case 'inspection':
            inspectionViewContainer?.classList.remove('hidden');
            // 初始化检测控制面板
            initializeInspectionPanel();
            // 初始化检测图像查看器
            initializeInspectionImageViewer();
            
            // 【关键修复】视图从 hidden 变为 visible 后，canvas 容器尺寸才变非0
            // 需要手动触发 resize 以消费 _pendingResetView，让已加载的图像正确渲染
            requestAnimationFrame(() => {
                if (window.inspectionImageViewer?.imageCanvas) {
                    window.inspectionImageViewer.imageCanvas.resize();
                }
                
                // 如果有已保存的检测结果但图像还没显示，重新加载
                if (window._lastInspectionResult?.outputImage && window.inspectionImageViewer) {
                    console.log('[App] 切换到检测视图，加载已保存的检测结果图像');
                    const imageData = `data:image/png;base64,${window._lastInspectionResult.outputImage}`;
                    window.inspectionImageViewer.loadImage(imageData);
                }

                inspectionPanel?.refresh?.();
            });
            break;
        case 'results':
            resultsViewContainer?.classList.remove('hidden');
            console.log('[App] 切换到结果视图');
            // 初始化并刷新结果面板
            if (resultPanel) {
                resultPanel.render();
                loadInspectionHistory();
            }
            break;
        case 'project':
            projectViewContainer?.classList.remove('hidden');
            console.log('[App] 切换到工程视图');
            // 刷新工程列表
            if (projectView) {
                projectView.refresh();
            }
            break;
        case 'ai':
            aiViewContainer?.classList.remove('hidden');
            console.log('[App] 切换到 AI 视图');
            if (aiPanel) {
                aiPanel.activate();
            } else {
                initializeAiPanel();
            }
            break;
        case 'settings':
            settingsViewContainer?.classList.remove('hidden');
            console.log('[App] 切换到 设置 视图');
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
 * 初始化检测控制面板
 */
function initializeInspectionPanel() {
    const container = document.getElementById('inspection-control-panel');
    if (!container) {
        console.warn('[App] 检测控制面板容器未找到');
        return;
    }
    
    // 如果面板已初始化，刷新显示
    if (inspectionPanel) {
        console.log('[App] 检测控制面板已存在，刷新显示');
        inspectionPanel.refresh();
        return;
    }
    
    try {
        console.log('[App] 创建新的 InspectionPanel 实例');
        
        // 【防抖修复】确保销毁旧实例，防止重复绑定和内存泄漏
        if (window.inspectionPanel && typeof window.inspectionPanel.dispose === 'function') {
            console.warn('[App] 发现残留的 InspectionPanel 实例，正在销毁...');
            window.inspectionPanel.dispose();
        }
        
        inspectionPanel = new InspectionPanel('inspection-control-panel');
        window.inspectionPanel = inspectionPanel;
        console.log('[App] 检测控制面板初始化完成');
    } catch (error) {
        console.error('[App] 检测控制面板初始化失败:', error);
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
            if (result.outputImage && window.inspectionImageViewer) {
                const imageData = `data:image/png;base64,${result.outputImage}`;
                window.inspectionImageViewer.loadImage(imageData);
            }



            // 更新结果面板
            updateInspectionResultsPanel(result);
        } else {
            // 【关键修复】如果不在检测视图，显示提示引导用户切换
            console.log('[App] 检测完成但不在检测视图，已保存结果');
        }

        // 添加结果到数显面板
        if (resultPanel) {
            const currentProject = getCurrentProject();
            resultPanel.setProjectContext(currentProject?.id || null);
            resultPanel.addResult({
                status: result.status,
                defects: result.defects || [],
                processingTime: result.processingTimeMs,
                timestamp: new Date().toISOString(),
                confidenceScore: result.confidenceScore,
                imageData: result.outputImage,
                outputData: result.outputData || {}
            });

            if (currentProject?.id && typeof resultPanel.loadServerAnalytics === 'function') {
                setTimeout(() => {
                    resultPanel.loadServerAnalytics().catch(error => {
                        console.warn('[App] 刷新结果页服务端分析失败:', error);
                    });
                }, 300);
            }
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
            status = 'warning';
            message = `检测到 ${result.defects?.length || 0} 个目标`;
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

    propertyPanel = new PropertyPanel('property-panel');

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
 * 初始化工程视图
 */
function initializeProjectView() {
    const container = document.getElementById('project-view');
    if (!container) {
        console.warn('[App] 工程视图容器未找到，将在首次切换到工程视图时初始化');
        return;
    }

    projectView = new ProjectView('project-view');

    // 监听工程打开事件
    window.addEventListener('projectOpened', (event) => {
        const project = event.detail;
        // 状态已由 projectManager.openProject() 设置，无需再次 setCurrentProject
        // projectManager 内部也已更新状态栏

        // 加载流程到画布
        if (project.flow && window.flowCanvas) {
            console.log('[App] projectOpened - 加载流程数据:', project.flow);
            window.flowCanvas.deserialize(project.flow);
        } else if (window.flowCanvas) {
            // 【修复】如果没有流程数据，清空画布
            console.log('[App] projectOpened - 工程没有流程数据，清空画布');
            window.flowCanvas.clear();
        }

        // 切换到流程视图
        setCurrentView('flow');
        switchView('flow');

        if (resultPanel) {
            resultPanel.setProjectContext(project.id);
            resultPanel.clear();
        }

        // 更新导航按钮
        document.querySelectorAll('.nav-btn').forEach(btn => {
            btn.classList.remove('active');
            if (btn.dataset.view === 'flow') {
                btn.classList.add('active');
            }
        });
    });

    console.log('[App] 工程视图初始化完成');
}

/**
 * 初始化结果面板（数显功能）
 */
function initializeResultPanel() {
    const container = document.getElementById('results-list-container');
    if (!container) {
        console.warn('[App] 结果视图容器未找到');
        return;
    }

    // 初始化结果面板（不传入容器ID，面板会管理整个视图）
    resultPanel = new ResultPanel('results-list-container');
    window.resultPanel = resultPanel;

    // 设置结果点击回调
    resultPanel.onResultClick = (result) => {
        console.log('[App] 点击结果:', result);
        // 显示详情弹窗
        if (resultPanel && result) {
            resultPanel.showResultDetail(result);
        }
    };

    // 绑定清空按钮
    const clearBtn = document.getElementById('btn-clear-results');
    if (clearBtn) {
        clearBtn.addEventListener('click', () => {
            if (confirm('确定要清空当前结果视图吗？此操作不会删除后端历史记录。')) {
                resultPanel.clear();
                showToast('当前结果视图已清空，历史记录未删除', 'success');
            }
        });
    }

    console.log('[App] 结果面板初始化完成（现代化仪表板）');
}

/**
 * 加载检测历史数据
 */
async function loadInspectionHistory() {
    const project = getCurrentProject();
    if (!project) {
        console.log('[App] 没有打开的工程，跳过加载历史数据');
        return;
    }

    try {
        console.log('[App] 正在加载检测历史数据...');
        const response = await httpClient.get(`/inspection/history/${project.id}`, {
            pageIndex: 0,
            pageSize: 50
        });

        const results = Array.isArray(response)
            ? response
            : (response?.items || response?.Items || []);

        if (resultPanel) {
            resultPanel.setProjectContext(project.id);
        }

        if (Array.isArray(results)) {
            const normalizedResults = results.map(result => ({
                status: result.status,
                defects: result.defects || result.Defects || [],
                processingTime: result.processingTimeMs ?? result.processingTime ?? result.executionTimeMs,
                timestamp: result.timestamp || result.Timestamp,
                confidenceScore: result.confidenceScore ?? result.ConfidenceScore,
                imageData: result.imageData || result.ImageData,
                imageId: result.imageId || result.ImageId,
                outputData: result.outputData || result.OutputData || {}
            }));

            resultPanel.loadResults(normalizedResults);
            if (typeof resultPanel.loadServerAnalytics === 'function') {
                await resultPanel.loadServerAnalytics();
            }
            console.log(`[App] 已加载 ${normalizedResults.length} 条历史检测记录`);
        }
    } catch (error) {
        console.error('[App] 加载检测历史数据失败:', error);
        // 不显示错误提示，因为这是后台加载
    }
}

/**
 * 更新右侧结果面板（简化显示）
 */
function updateResultsPanel(data) {
    // 更新结果面板 - 显示检测结果
    const resultsPanel = document.getElementById('inspection-results-panel') || document.getElementById('results-panel');
    if (resultsPanel) {
        // 清空现有内容
        resultsPanel.innerHTML = '';

        // 显示检测状态
        const statusDiv = document.createElement('div');
        statusDiv.className = 'result-status';
        statusDiv.textContent = `检测状态: ${data.status || '未知'}`;
        resultsPanel.appendChild(statusDiv);

        // 显示缺陷列表（如果有）
        if (data.defects && data.defects.length > 0) {
            const defectsList = document.createElement('ul');
            defectsList.className = 'defects-list';
            data.defects.forEach(defect => {
                const li = document.createElement('li');
                
                // 兼容字段名 (camelCase/PascalCase)
                const getProp = (d, key) => {
                    const camel = key.charAt(0).toLowerCase() + key.slice(1);
                    const pascal = key.charAt(0).toUpperCase() + key.slice(1);
                    return d[camel] !== undefined ? d[camel] : d[pascal];
                };

                const type = getProp(defect, 'type');
                const description = getProp(defect, 'description');
                // 后端实体为 ConfidenceScore, 字典中可能为 Confidence
                const confidence = getProp(defect, 'confidenceScore') ?? getProp(defect, 'confidence');

                const displayLabel = description || type || 'Unknown';
                const displayConf = confidence !== undefined ? (confidence * 100).toFixed(1) : 'NaN';

                li.textContent = `${displayLabel}: 置信度 ${displayConf}%`;
                defectsList.appendChild(li);
            });
            resultsPanel.appendChild(defectsList);
        }

        // 显示处理时间
        if (data.processingTimeMs) {
            const timeDiv = document.createElement('div');
            timeDiv.className = 'processing-time';
            timeDiv.textContent = `处理时间: ${data.processingTimeMs}ms`;
            resultsPanel.appendChild(timeDiv);
        }
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
                parameters: mergeParameters(operatorDef?.parameters, node.parameters)
            });
        } else {
            setSelectedOperator(null);
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
    
    // 算子配置
    const operatorConfigs = {
        // 输入
        'ImageAcquisition': { title: '图像采集', color: '#52c41a', iconPath: 'M9 3L7.17 5H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2h-3.17L15 3H9zm3 15c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5z' },
        
        // 预处理
        'Filtering': { title: '滤波', color: '#1890ff', iconPath: 'M10 18h4v-2h-4v2zM3 6v2h18V6H3zm3 7h12v-2H6v2z' },
        'Thresholding': { title: '二值化', color: '#eb2f96', iconPath: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z' },
        'Morphology': { title: '形态学', color: '#fa8c16', iconPath: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm5 11h-4v4h-2v-4H7v-2h4V7h2v4h4v2z' },
        'ColorConversion': { title: '颜色空间转换', color: '#fa8c16', iconPath: 'M12 3c-4.97 0-9 4.03-9 9s4.03 9 9 9c.83 0 1.5-.67 1.5-1.5 0-.39-.15-.74-.39-1.01-.23-.26-.38-.61-.38-.99 0-.83.67-1.5 1.5-1.5H16c2.76 0 5-2.24 5-5 0-4.42-4.03-8-9-8zm-5.5 9c-.83 0-1.5-.67-1.5-1.5S5.67 9 6.5 9 8 9.67 8 10.5 7.33 12 6.5 12zm3-4C8.67 8 8 7.33 8 6.5S8.67 5 9.5 5s1.5.67 1.5 1.5S10.33 8 9.5 8zm5 0c-.83 0-1.5-.67-1.5-1.5S13.67 5 14.5 5s1.5.67 1.5 1.5S15.33 8 14.5 8zm3 4c-.83 0-1.5-.67-1.5-1.5S16.67 9 17.5 9s1.5.67 1.5 1.5-.67 1.5-1.5 1.5z' },
        'AdaptiveThreshold': { title: '自适应阈值', color: '#eb2f96', iconPath: 'M3 5H1v16c0 1.1.9 2 2 2h16v-2H3V5zm18-4H7c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V3c0-1.1-.9-2-2-2zm0 16H7V3h14v14z' },
        'HistogramEqualization': { title: '直方图均衡化', color: '#2f54eb', iconPath: 'M5 9.2h3V19H5zM10.6 5h2.8v14h-2.8zm5.6 8H19v6h-2.8z' },
        
        // 特征提取
        'EdgeDetection': { title: '边缘检测', color: '#722ed1', iconPath: 'M3 17h18v2H3zm0-7h18v5H3zm0-7h18v5H3z' },
        'SubpixelEdgeDetection': { title: '亚像素边缘', color: '#722ed1', iconPath: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z' },
        'BlobAnalysis': { title: 'Blob分析', color: '#13c2c2', iconPath: 'M12 2C6.47 2 2 6.47 2 12s4.47 10 10 10 10-4.47 10-10S17.53 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z' },
        
        // 检测 / 匹配
        'TemplateMatching': { title: '模板匹配', color: '#f5222d', iconPath: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 13h4v-2h-4v2zm0-4h4V9h-4v2z' },
        'ShapeMatching': { title: '形状匹配', color: '#52c41a', iconPath: 'M12 6c3.31 0 6 2.69 6 6s-2.69 6-6 6-6-2.69-6-6 2.69-6 6-6m0-2c-4.42 0-8 3.58-8 8s3.58 8 8 8 8-3.58 8-8-3.58-8-8-8z' },
        'Measurement': { title: '测量', color: '#2f54eb', iconPath: 'M21 6H3c-1.1 0-2 .9-2 2v8c0 1.1.9 2 2 2h18c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2zm0 10H3V8h2v4h2V8h2v4h2V8h2v4h2V8h2v8z' },
        'GeometricFitting': { title: '几何拟合', color: '#eb2f96', iconPath: 'M12 6c3.31 0 6 2.69 6 6s-2.69 6-6 6-6-2.69-6-6 2.69-6 6-6m0-2c-4.42 0-8 3.58-8 8s3.58 8 8 8 8-3.58 8-8-3.58-8-8-8z' },
        'ColorDetection': { title: '颜色检测', color: '#fa541c', iconPath: 'M12 3c-4.97 0-9 4.03-9 9s4.03 9 9 9c.83 0 1.5-.67 1.5-1.5 0-.39-.15-.74-.39-1.01-.23-.26-.38-.61-.38-.99 0-.83.67-1.5 1.5-1.5H16c2.76 0 5-2.24 5-5 0-4.42-4.03-8-9-8zm-5.5 9c-.83 0-1.5-.67-1.5-1.5S5.67 9 6.5 9 8 9.67 8 10.5 7.33 12 6.5 12zm3-4C8.67 8 8 7.33 8 6.5S8.67 5 9.5 5s1.5.67 1.5 1.5S10.33 8 9.5 8zm5 0c-.83 0-1.5-.67-1.5-1.5S13.67 5 14.5 5s1.5.67 1.5 1.5S15.33 8 14.5 8zm3 4c-.83 0-1.5-.67-1.5-1.5S16.67 9 17.5 9s1.5.67 1.5 1.5-.67 1.5-1.5 1.5z' },

        // AI
        'DeepLearning': { title: '深度学习', color: '#a0d911', iconPath: 'M20 6h-8l-2-2H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2zm-6 10H6v-2h8v2zm4-4H6v-2h12v2z' },
        
        // ROI / 标定
        'RoiManager': { title: 'ROI管理器', color: '#1890ff', iconPath: 'M3 5v4h2V5h4V3H5c-1.1 0-2 .9-2 2zm2 10H3v4c0 1.1.9 2 2 2h4v-2H5v-4zm14 4h-4v2h4c1.1 0 2-.9 2-2v-4h-2v4zm0-16h-4v2h4v4h2V5c0-1.1-.9-2-2-2z' },

        // 通信
        'SerialCommunication': { title: '串口通信', color: '#13c2c2', iconPath: 'M20 18c1.1 0 1.99-.9 1.99-2L22 6c0-1.1-.9-2-2-2H4c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2H0v2h24v-2h-4zM4 6h16v10H4V6z' },
        'ModbusCommunication': { title: 'Modbus通信', color: '#13c2c2', iconPath: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 14H9V8h2v8zm4 0h-2V8h2v8z' },
        'TcpCommunication': { title: 'TCP通信', color: '#13c2c2', iconPath: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z' },
        
        // 输出
        'ResultOutput': { title: '结果输出', color: '#595959', iconPath: 'M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z' },
        'DatabaseWrite': { title: '数据库写入', color: '#595959', iconPath: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zm.31-8.86c-1.77-.45-2.34-.94-2.34-1.67 0-.84.79-1.43 2.1-1.43 1.38 0 1.9.66 1.94 1.64h1.71c-.05-1.34-.87-2.57-2.49-2.97V5H10.9v1.69c-1.51.32-2.72 1.3-2.72 2.81 0 1.79 1.49 2.69 3.66 3.21 1.95.46 2.34 1.15 2.34 1.87 0 .53-.39 1.39-2.1 1.39-1.6 0-2.23-.72-2.32-1.64H8.04c.1 1.7 1.36 2.66 2.86 2.97V19h2.34v-1.67c1.52-.29 2.72-1.16 2.73-2.77-.01-2.2-1.9-2.96-3.66-3.42z' }
    };
    
    // 优先使用传入数据的配置，否则使用默认配置
    const defaultConfig = operatorConfigs[type] || { title: type, color: '#1890ff', iconPath: 'M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L5.03 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z' };
    
    const nodeConfig = {
        title: data?.displayName || defaultConfig.title,
        color: defaultConfig.color,
        iconPath: data?.iconPath || defaultConfig.iconPath,
        // icon: data?.icon || defaultConfig.icon, // Removed legacy icon
        // 传递参数 - 使用深拷贝确保每个节点有独立的参数副本
        parameters: data?.parameters?.map(p => ({...p})) || [],
        // 传递端口配置 (如果有) 或使用默认值
        inputs: data?.inputPorts?.map(p => ({name: p.name, type: p.dataType})) || [{ name: 'input', type: 'any' }],
        outputs: data?.outputPorts?.map(p => ({name: p.name, type: p.dataType})) || [{ name: 'output', type: 'any' }]
    };
    
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
                switchView('inspection');
                
                // 更新导航按钮状态
                document.querySelectorAll('.nav-btn').forEach(btn => {
                    btn.classList.remove('active');
                    if (btn.dataset.view === 'inspection') {
                        btn.classList.add('active');
                    }
                });
                
                // 设置当前工程
                inspectionController.setProject(project.id);
                
                // 初始化检测面板并执行检测
                setTimeout(async () => {
                    // 确保检测面板已初始化
                    initializeInspectionPanel();
                    initializeInspectionImageViewer();
                    
                    // 更新检测面板状态
                    if (inspectionPanel) {
                        inspectionPanel.updateStatus('running', '运行中...');
                        inspectionPanel.setButtonsState(true);
                    }
                    
                    // 如果有加载的图像，执行检测
                    // 优先使用导入的测试图像
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
                }, 100);
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
    if (window.flowCanvas) {
        // aiGenerationDialog = new AiGenerationDialog(window.flowCanvas); // 已废弃
        // window.aiGenerationDialog = aiGenerationDialog;
        console.log('[App] AI 生成功能已升级为独立面板');
    } else {
        console.warn('[App] FlowCanvas 未就绪');
    }
}

/**
 * 初始化 AI 面板
 */
function initializeAiPanel() {
    if (window.flowCanvas) {
        aiPanel = new AiPanel('ai-view', window.flowCanvas);
        window.aiPanel = aiPanel;
        console.log('[App] AI 面板初始化完成');
    } else {
         console.warn('[App] FlowCanvas 未就绪，无法初始化 AI 面板');
    }
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
            project.flow = importData.project.flow;
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
    const now = performance.now();
    fpsCounter.frames++;
    
    if (now - fpsCounter.lastTime >= 1000) {
        const fps = Math.round(fpsCounter.frames * 1000 / (now - fpsCounter.lastTime));
        const fpsEl = document.querySelector('#fps-counter .metric-value');
        if (fpsEl) fpsEl.textContent = `${fps} FPS`;
        
        fpsCounter.frames = 0;
        fpsCounter.lastTime = now;
    }
    
    requestAnimationFrame(updateFPS);
}

// 启动状态栏更新
function startStatusBarUpdates() {
    // 每秒更新内存
    if (statusBarInterval !== null) {
        clearInterval(statusBarInterval);
    }
    statusBarInterval = setInterval(updateStatusBar, 1000);
    // 启动 FPS 计数
    requestAnimationFrame(updateFPS);
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
    initializeApp();
    startStatusBarUpdates();
    
    // 延迟显示欢迎页
    setTimeout(() => {
        hideLoadingScreen();
        showWelcomeScreen();
    }, 500);
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
