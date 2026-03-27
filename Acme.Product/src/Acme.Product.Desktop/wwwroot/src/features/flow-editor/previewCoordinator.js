import {
    buildPreviewSummaryItems,
    isPreviewImageLikePayload
} from './previewOutputFormatter.mjs';

const DEFAULT_DEBOUNCE_MS = 500;
const IMAGE_NODE_TYPE_FALLBACKS = new Set([
    'ImageAcquisition'
]);

/**
 * @typedef {{
 *   eligible: boolean,
 *   reason: string | null,
 *   source: string | null
 * }} PreviewEligibility
 */

/**
 * @typedef {{
 *   requestKey: string | null,
 *   projectId: string | null,
 *   nodeId: string | null,
 *   flowRevision: number,
 *   parameterSnapshot: string,
 *   inputImageHash: string
 * }} PreviewRequestKey
 */

/**
 * @typedef {{
 *   title: string,
 *   statusText: string,
 *   inputImageSrc: string | null,
 *   outputImageSrc: string | null,
 *   summaryItems: Array<{ key: string, value: string, title?: string | null, kind?: string }>,
 *   overlayEnabled: boolean,
 *   canOpenImage: boolean,
 *   isLoading: boolean,
 *   hasError: boolean,
 *   errorMessage: string | null
 * }} PreviewPresenterState
 */

/**
 * @typedef {{
 *   activeNodeId: string | null,
 *   nodeType: string | null,
 *   title: string,
 *   status: 'idle' | 'loading' | 'success' | 'error',
 *   executionTimeMs: number | null,
 *   errorMessage: string | null,
 *   canvasEligibility: PreviewEligibility,
 *   request: PreviewRequestKey,
 *   inputImageBase64: string | null,
 *   outputImageBase64: string | null,
 *   outputData: Record<string, unknown> | null,
 *   presenter: PreviewPresenterState
 * }} PreviewState
 */

function readFirstDefined(source, keys) {
    for (const key of keys) {
        if (source?.[key] !== undefined && source?.[key] !== null) {
            return source[key];
        }
    }

    return undefined;
}

function normalizeBase64Image(imageValue) {
    if (!imageValue || typeof imageValue !== 'string') {
        return null;
    }

    const trimmed = imageValue.trim();
    const commaIndex = trimmed.indexOf(',');
    if (trimmed.startsWith('data:image/') && commaIndex > 0) {
        return trimmed.substring(commaIndex + 1);
    }

    return trimmed;
}

export function extractPreviewImageBase64(result) {
    if (!result || typeof result !== 'object') {
        return null;
    }

    const candidateKeys = [
        'outputImage',
        'OutputImage',
        'outputImageBase64',
        'OutputImageBase64',
        'imageBase64',
        'ImageBase64',
        'resultImageBase64',
        'ResultImageBase64',
        'inputImage',
        'InputImage'
    ];

    for (const key of candidateKeys) {
        const value = result[key];
        if (typeof value === 'string' && isPreviewImageLikePayload(value)) {
            return normalizeBase64Image(value);
        }
    }

    const outputData = result.outputData || result.OutputData;
    if (outputData && typeof outputData === 'object') {
        for (const value of Object.values(outputData)) {
            if (typeof value === 'string' && isImageLikePayload(value)) {
                return normalizeBase64Image(value);
            }
        }
    }

    return null;
}

export function resolvePreviewInputImageBase64(result) {
    return extractPreviewImageBase64(result);
}

function toImageSource(imageBase64OrDataUrl) {
    if (!imageBase64OrDataUrl || typeof imageBase64OrDataUrl !== 'string') {
        return null;
    }

    const trimmed = imageBase64OrDataUrl.trim();
    if (!trimmed) {
        return null;
    }

    if (trimmed.startsWith('data:image/') || trimmed.startsWith('blob:')) {
        return trimmed;
    }

    return `data:image/png;base64,${trimmed}`;
}

function normalizePortType(value) {
    if (value === 0 || value === '0') {
        return 'image';
    }

    return String(value ?? '').trim().toLowerCase();
}

export function getCanvasPreviewEligibility(node, metadata = null) {
    const outputPorts = Array.isArray(node?.outputs) && node.outputs.length > 0
        ? node.outputs
        : (metadata?.outputPorts || metadata?.OutputPorts || []);

    const hasImageOutput = outputPorts.some(port => {
        const portType = port?.type ?? port?.Type ?? port?.dataType ?? port?.DataType;
        return normalizePortType(portType) === 'image';
    });

    if (hasImageOutput) {
        return {
            eligible: true,
            reason: null,
            source: Array.isArray(node?.outputs) && node.outputs.length > 0 ? 'node-ports' : 'metadata'
        };
    }

    if (IMAGE_NODE_TYPE_FALLBACKS.has(node?.type)) {
        return {
            eligible: true,
            reason: null,
            source: 'type-fallback'
        };
    }

    return {
        eligible: false,
        reason: 'no-image-output',
        source: null
    };
}

function stableSerialize(value) {
    if (value === null || value === undefined) {
        return 'null';
    }

    if (Array.isArray(value)) {
        return `[${value.map(item => stableSerialize(item)).join(',')}]`;
    }

    if (typeof value === 'object') {
        const keys = Object.keys(value).sort((a, b) => a.localeCompare(b));
        return `{${keys.map(key => `${JSON.stringify(key)}:${stableSerialize(value[key])}`).join(',')}}`;
    }

    return JSON.stringify(value);
}

function buildParameterSnapshot(parameters) {
    const normalized = (parameters || [])
        .map(parameter => ({
            name: String(parameter?.name || parameter?.Name || ''),
            value: parameter?.value ?? parameter?.Value ?? parameter?.defaultValue ?? parameter?.DefaultValue ?? null
        }))
        .sort((a, b) => a.name.localeCompare(b.name));

    return stableSerialize(normalized);
}

function hashString(input) {
    const text = String(input || '');
    let hash = 5381;
    for (let index = 0; index < text.length; index += 1) {
        hash = ((hash << 5) + hash) + text.charCodeAt(index);
        hash >>>= 0;
    }

    return hash.toString(16);
}

function createPresenterState(state) {
    let statusText = '等待预览';
    if (state.status === 'idle' && state.errorMessage) {
        statusText = state.errorMessage;
    } else if (state.status === 'loading') {
        statusText = '预览中...';
    } else if (state.status === 'success') {
        statusText = typeof state.executionTimeMs === 'number'
            ? `预览完成 (${state.executionTimeMs} ms)`
            : '预览完成';
    } else if (state.status === 'error') {
        statusText = `预览失败: ${state.errorMessage || '未知错误'}`;
    }

    return {
        title: state.title,
        statusText,
        inputImageSrc: toImageSource(state.inputImageBase64),
        outputImageSrc: toImageSource(state.outputImageBase64),
        summaryItems: buildPreviewSummaryItems(state.outputData, {
            maxItems: 3,
            stringMaxLength: 42,
            skipImageLikeValues: true
        }),
        overlayEnabled: state.canvasEligibility.eligible,
        canOpenImage: Boolean(state.outputImageBase64),
        isLoading: state.status === 'loading',
        hasError: state.status === 'error',
        errorMessage: state.errorMessage
    };
}

function getParameterValue(parameters, ...names) {
    const list = Array.isArray(parameters) ? parameters : [];
    for (const name of names) {
        const matched = list.find(parameter => String(parameter?.name || parameter?.Name || '').toLowerCase() === String(name).toLowerCase());
        if (!matched) {
            continue;
        }

        return matched?.value ?? matched?.Value ?? matched?.defaultValue ?? matched?.DefaultValue ?? null;
    }

    return null;
}

function validatePreviewPrerequisites(node, inputImageBase64) {
    if (!node) {
        return '未选中算子';
    }

    if (inputImageBase64) {
        return null;
    }

    if (node.type === 'ImageAcquisition') {
        const sourceTypeRaw = getParameterValue(node.parameters, 'SourceType', 'sourceType');
        const sourceType = String(sourceTypeRaw || 'File').trim().toLowerCase();
        const filePath = String(getParameterValue(node.parameters, 'FilePath', 'filePath') || '').trim();
        const cameraId = String(getParameterValue(node.parameters, 'CameraId', 'cameraId') || '').trim();

        if (filePath) {
            return null;
        }

        if (sourceType === 'file' && !filePath) {
            return '请先配置文件路径';
        }

        if (sourceType === 'camera' && !cameraId) {
            return '请先选择相机';
        }

        if (!filePath && !cameraId) {
            return '请先配置采集源';
        }

        return null;
    }

    return null;
}

function createEmptyState() {
    const state = {
        activeNodeId: null,
        nodeType: null,
        title: '',
        status: 'idle',
        executionTimeMs: null,
        errorMessage: null,
        canvasEligibility: {
            eligible: false,
            reason: null,
            source: null
        },
        request: {
            requestKey: null,
            projectId: null,
            nodeId: null,
            flowRevision: 0,
            parameterSnapshot: '',
            inputImageHash: ''
        },
        inputImageBase64: null,
        outputImageBase64: null,
        outputData: null,
        presenter: null
    };

    state.presenter = createPresenterState(state);
    return state;
}

function buildPreviewRequestKey({ projectId, nodeId, flowRevision, parameterSnapshot, inputImageBase64 }) {
    const inputImageHash = inputImageBase64 ? hashString(inputImageBase64) : 'none';
    return {
        requestKey: `${projectId || 'no-project'}:${nodeId || 'no-node'}:${flowRevision}:${hashString(parameterSnapshot)}:${inputImageHash}`,
        projectId: projectId || null,
        nodeId: nodeId || null,
        flowRevision: Number(flowRevision || 0),
        parameterSnapshot,
        inputImageHash
    };
}

function parsePreviewResponse(response) {
    const isSuccess = Boolean(readFirstDefined(response, ['success', 'Success']));
    return {
        isSuccess,
        inputImageBase64: normalizeBase64Image(readFirstDefined(response, ['inputImageBase64', 'InputImageBase64'])),
        outputImageBase64: normalizeBase64Image(readFirstDefined(response, ['outputImageBase64', 'OutputImageBase64'])),
        outputData: readFirstDefined(response, ['outputData', 'OutputData']) || null,
        executionTimeMs: readFirstDefined(response, ['executionTimeMs', 'ExecutionTimeMs']) ?? null,
        errorMessage: readFirstDefined(response, ['errorMessage', 'ErrorMessage']) || null,
        failedOperatorName: readFirstDefined(response, ['failedOperatorName', 'FailedOperatorName']) || null
    };
}

export class NodePreviewCoordinator {
    constructor(options = {}) {
        this.getProjectId = options.getProjectId ?? (() => null);
        this.getFlowRevision = options.getFlowRevision ?? (() => 0);
        this.getNodeById = options.getNodeById ?? (() => null);
        this.getOperatorMetadata = options.getOperatorMetadata ?? (() => null);
        this.getInputImageBase64 = options.getInputImageBase64 ?? (() => null);
        this.previewExecutor = options.previewExecutor ?? (async () => null);
        this.debounceMs = options.debounceMs ?? DEFAULT_DEBOUNCE_MS;

        this.listeners = new Set();
        this.cache = new Map();
        this.state = createEmptyState();
        this.pendingTimer = null;
        this.requestVersion = 0;
        this.unsubscribeStructure = typeof options.subscribeStructureState === 'function'
            ? options.subscribeStructureState(() => this.handleStructureChanged())
            : null;
    }

    destroy() {
        if (this.pendingTimer) {
            clearTimeout(this.pendingTimer);
            this.pendingTimer = null;
        }

        this.unsubscribeStructure?.();
        this.listeners.clear();
        this.cache.clear();
    }

    getState() {
        return this.state;
    }

    subscribe(listener) {
        if (typeof listener !== 'function') {
            return () => {};
        }

        this.listeners.add(listener);
        listener(this.state);
        return () => this.listeners.delete(listener);
    }

    updateState(patch) {
        this.state = {
            ...this.state,
            ...patch
        };
        this.state.presenter = createPresenterState(this.state);

        this.listeners.forEach(listener => {
            try {
                listener(this.state);
            } catch (error) {
                console.error('[NodePreviewCoordinator] Listener failed:', error);
            }
        });
    }

    setActiveNode(node) {
        if (this.pendingTimer) {
            clearTimeout(this.pendingTimer);
            this.pendingTimer = null;
        }

        this.requestVersion += 1;

        if (!node?.id) {
            this.updateState(createEmptyState());
            return;
        }

        const metadata = this.getOperatorMetadata(node.type);
        this.updateState({
            ...createEmptyState(),
            activeNodeId: node.id,
            nodeType: node.type,
            title: node.title || metadata?.displayName || node.type,
            canvasEligibility: getCanvasPreviewEligibility(node, metadata)
        });

        this.requestActivePreview();
    }

    invalidateActivePreview(options = {}) {
        this.requestActivePreview({
            ...options,
            force: true
        });
    }

    requestActivePreview(options = {}) {
        const { immediate = false, force = false, debounceMs = this.debounceMs } = options;
        if (!this.state.activeNodeId) {
            return;
        }

        const scheduledVersion = ++this.requestVersion;

        if (this.pendingTimer) {
            clearTimeout(this.pendingTimer);
            this.pendingTimer = null;
        }

        const execute = async () => {
            const activeNode = this.getNodeById(this.state.activeNodeId);
            if (scheduledVersion !== this.requestVersion) {
                return;
            }
            if (!activeNode) {
                this.setActiveNode(null);
                return;
            }

            const projectId = this.getProjectId();
            if (!projectId) {
                this.updateState({
                    status: 'error',
                    errorMessage: '未选择工程'
                });
                return;
            }

            const inputImageBase64 = await Promise.resolve(this.getInputImageBase64());
            const prerequisiteError = validatePreviewPrerequisites(activeNode, inputImageBase64);
            if (prerequisiteError) {
                this.updateState({
                    status: 'idle',
                    executionTimeMs: null,
                    errorMessage: prerequisiteError,
                    inputImageBase64: inputImageBase64 || null,
                    outputImageBase64: null,
                    outputData: null,
                    request: buildPreviewRequestKey({
                        projectId,
                        nodeId: activeNode.id,
                        flowRevision: this.getFlowRevision(),
                        parameterSnapshot: buildParameterSnapshot(activeNode.parameters),
                        inputImageBase64
                    })
                });
                return;
            }

            const request = buildPreviewRequestKey({
                projectId,
                nodeId: activeNode.id,
                flowRevision: this.getFlowRevision(),
                parameterSnapshot: buildParameterSnapshot(activeNode.parameters),
                inputImageBase64
            });

            const cached = this.cache.get(request.requestKey);
            if (!force && cached) {
                this.updateState({
                    ...cached,
                    request
                });
                return;
            }

            this.updateState({
                status: 'loading',
                errorMessage: null,
                executionTimeMs: null,
                request,
                inputImageBase64: inputImageBase64 || null
            });

            try {
                const response = await this.previewExecutor(activeNode.id, {
                    inputImageBase64,
                    parameters: null
                });

                if (scheduledVersion !== this.requestVersion || this.state.activeNodeId !== activeNode.id) {
                    return;
                }

                const parsed = parsePreviewResponse(response);
                const nextState = {
                    activeNodeId: activeNode.id,
                    nodeType: activeNode.type,
                    title: this.state.title,
                    status: parsed.isSuccess ? 'success' : 'error',
                    executionTimeMs: parsed.executionTimeMs,
                    errorMessage: parsed.isSuccess
                        ? null
                        : (parsed.failedOperatorName
                            ? `${parsed.failedOperatorName}: ${parsed.errorMessage || '预览执行失败'}`
                            : (parsed.errorMessage || '预览执行失败')),
                    canvasEligibility: this.state.canvasEligibility,
                    request,
                    inputImageBase64: parsed.inputImageBase64 || inputImageBase64 || null,
                    outputImageBase64: parsed.outputImageBase64,
                    outputData: parsed.outputData
                };

                this.cache.set(request.requestKey, nextState);
                this.updateState(nextState);
            } catch (error) {
                if (scheduledVersion !== this.requestVersion || this.state.activeNodeId !== activeNode.id) {
                    return;
                }

                this.updateState({
                    status: 'error',
                    executionTimeMs: null,
                    errorMessage: error?.message || '预览请求失败',
                    request,
                    inputImageBase64: inputImageBase64 || null,
                    outputImageBase64: null,
                    outputData: null
                });
            }
        };

        if (immediate) {
            void execute();
            return;
        }

        this.pendingTimer = setTimeout(() => {
            this.pendingTimer = null;
            void execute();
        }, debounceMs);
    }

    handleStructureChanged() {
        if (!this.state.activeNodeId) {
            return;
        }

        const activeNode = this.getNodeById(this.state.activeNodeId);
        if (!activeNode) {
            this.setActiveNode(null);
            return;
        }

        this.invalidateActivePreview({
            immediate: false
        });
    }
}
