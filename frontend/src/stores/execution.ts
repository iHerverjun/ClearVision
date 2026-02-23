import { defineStore } from 'pinia';
import { computed, ref } from 'vue';
import { webMessageBridge } from '../services/bridge';
import { BridgeMessageType } from '../services/bridge.types';
import { resolveImageSource } from '../services/imageSource';
import { useFlowStore } from './flow';

export type ExecutionStatus = 'idle' | 'running' | 'success' | 'error';

export interface NodeExecutionState {
  nodeId: string;
  status: ExecutionStatus;
  startTime?: number;
  endTime?: number;
  errorMessage?: string;
  outputImage?: string;
  outputData?: Record<string, unknown>;
}

interface OperatorExecutedEvent {
  operatorId: string;
  operatorName: string;
  isSuccess: boolean;
  outputData?: Record<string, unknown>;
  outputImageBase64?: string;
  executionTimeMs: number;
  errorMessage?: string;
}

interface ProgressNotificationEvent {
  progress: number;
  currentOperatorId?: string;
  currentOperatorName?: string;
  message?: string;
}

interface InspectionCompletedEvent {
  resultId: string;
  projectId: string;
  status: 'Pass' | 'Fail' | 'OK' | 'NG' | 'Error';
  processingTimeMs: number;
}

interface ImageStreamEvent {
  operatorId?: string;
  imageBase64?: string;
  outputImageBase64?: string;
}

interface SharedImageStreamEvent {
  buffer?: ArrayBuffer;
  width?: number;
  height?: number;
}

const PNG_SIGNATURE = [0x89, 0x50, 0x4e, 0x47];

const isPngBuffer = (bytes: Uint8Array): boolean =>
  bytes.length >= 4 && PNG_SIGNATURE.every((value, index) => bytes[index] === value);

const isJpegBuffer = (bytes: Uint8Array): boolean =>
  bytes.length >= 2 && bytes[0] === 0xff && bytes[1] === 0xd8;

const binaryToBase64 = (bytes: Uint8Array): string => {
  let binary = '';
  const chunkSize = 0x8000;

  for (let i = 0; i < bytes.length; i += chunkSize) {
    const chunk = bytes.subarray(i, i + chunkSize);
    binary += String.fromCharCode(...chunk);
  }

  return btoa(binary);
};

const encodedBufferToDataUrl = (bytes: Uint8Array): string | null => {
  if (isPngBuffer(bytes)) {
    return `data:image/png;base64,${binaryToBase64(bytes)}`;
  }

  if (isJpegBuffer(bytes)) {
    return `data:image/jpeg;base64,${binaryToBase64(bytes)}`;
  }

  return null;
};

const rawBufferToDataUrl = (bytes: Uint8Array, width: number, height: number): string | null => {
  if (!width || !height || width <= 0 || height <= 0 || typeof document === 'undefined') {
    return null;
  }

  const pixelCount = width * height;
  let rgba: Uint8ClampedArray | null = null;

  if (bytes.length === pixelCount * 4) {
    rgba = new Uint8ClampedArray(bytes);
  } else if (bytes.length === pixelCount * 3) {
    rgba = new Uint8ClampedArray(pixelCount * 4);
    for (let src = 0, dst = 0; src < bytes.length; src += 3, dst += 4) {
      rgba[dst] = bytes[src] ?? 0;
      rgba[dst + 1] = bytes[src + 1] ?? 0;
      rgba[dst + 2] = bytes[src + 2] ?? 0;
      rgba[dst + 3] = 255;
    }
  } else if (bytes.length === pixelCount) {
    rgba = new Uint8ClampedArray(pixelCount * 4);
    for (let src = 0, dst = 0; src < bytes.length; src += 1, dst += 4) {
      const gray = bytes[src] ?? 0;
      rgba[dst] = gray;
      rgba[dst + 1] = gray;
      rgba[dst + 2] = gray;
      rgba[dst + 3] = 255;
    }
  }

  if (!rgba) {
    return null;
  }

  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;

  const context = canvas.getContext('2d');
  if (!context) {
    return null;
  }

  const rgbaData = new Uint8ClampedArray(rgba.length);
  rgbaData.set(rgba);
  context.putImageData(new ImageData(rgbaData, width, height), 0, 0);
  return canvas.toDataURL('image/png');
};

const sharedBufferToDataUrl = (sharedBuffer: ArrayBuffer, width: number, height: number): string | null => {
  const bytes = new Uint8Array(sharedBuffer);
  return encodedBufferToDataUrl(bytes) ?? rawBufferToDataUrl(bytes, width, height);
};

const getMessagePayload = <T>(message: any): T => {
  return (message?.data ?? message) as T;
};

const normalizeOutputImage = (raw: unknown): string | undefined => {
  if (typeof raw !== 'string') {
    return undefined;
  }

  const resolved = resolveImageSource(raw);
  return resolved || undefined;
};

const extractImageCandidate = (raw: unknown): string | undefined => {
  if (typeof raw === 'string') {
    const resolved = resolveImageSource(raw);
    return resolved ? raw : undefined;
  }

  if (!raw || typeof raw !== 'object') {
    return undefined;
  }

  const payload = raw as Record<string, unknown>;
  const candidateKeys = [
    'imageBase64',
    'outputImageBase64',
    'previewImageBase64',
    'base64',
    'DataBase64',
    'data',
    'value',
  ];

  for (const key of candidateKeys) {
    const candidate = payload[key];
    if (typeof candidate !== 'string') {
      continue;
    }

    const resolved = resolveImageSource(candidate);
    if (resolved) {
      return candidate;
    }
  }

  return undefined;
};

const extractImageFromOutputData = (
  outputData?: Record<string, unknown>,
): string | undefined => {
  if (!outputData) {
    return undefined;
  }

  const preferredKeys = [
    'Image',
    'image',
    'OutputImage',
    'outputImage',
    'OutputImageBase64',
    'outputImageBase64',
    'PreviewImageBase64',
    'previewImageBase64',
  ];

  for (const key of preferredKeys) {
    const candidate = extractImageCandidate(outputData[key]);
    if (candidate) {
      return candidate;
    }
  }

  for (const value of Object.values(outputData)) {
    const candidate = extractImageCandidate(value);
    if (candidate) {
      return candidate;
    }
  }

  return undefined;
};

export const useExecutionStore = defineStore('execution', () => {
  const flowStore = useFlowStore();

  const isRunning = ref<boolean>(false);
  const isContinuousMode = ref<boolean>(false);
  const currentExecutingNodeId = ref<string | null>(null);
  const nodeStates = ref<Map<string, NodeExecutionState>>(new Map());
  const executionTime = ref<number>(0);
  const executionStartTime = ref<number | null>(null);
  const progress = ref<number>(0);
  const progressMessage = ref<string>('');
  const lastError = ref<string | null>(null);
  const bridgeListenersInitialized = ref<boolean>(false);
  
  // Real-time inspection state
  const lastInspectionResult = ref<'OK' | 'NG' | 'Error' | null>(null);
  const latestCameraImage = ref<string | null>(null);
  const latestCameraId = ref<string | null>(null);
  const latestCameraMeta = ref<{ width: number; height: number } | null>(null);
  const okCount = ref(0);
  const ngCount = ref(0);
  const cycleTimeMs = ref(0);

  const getNodeStatus = computed(() => {
    return (nodeId: string): ExecutionStatus => {
      return nodeStates.value.get(nodeId)?.status || 'idle';
    };
  });

  const getNodeOutputImage = computed(() => {
    return (nodeId: string): string | undefined => {
      return nodeStates.value.get(nodeId)?.outputImage;
    };
  });

  const getNodeOutputData = computed(() => {
    return (nodeId: string): Record<string, unknown> | undefined => {
      return nodeStates.value.get(nodeId)?.outputData;
    };
  });

  const hasRunningNodes = computed(() => {
    for (const state of nodeStates.value.values()) {
      if (state.status === 'running') return true;
    }
    return false;
  });

  const errorNodes = computed(() => {
    const errors: NodeExecutionState[] = [];
    for (const state of nodeStates.value.values()) {
      if (state.status === 'error') {
        errors.push(state);
      }
    }
    return errors;
  });

  const totalCount = computed(() => okCount.value + ngCount.value);
  const yieldRate = computed(() => {
    if (totalCount.value <= 0) {
      return 0;
    }
    return Number(((okCount.value / totalCount.value) * 100).toFixed(2));
  });

  function startExecution(): void {
    isRunning.value = true;
    executionStartTime.value = Date.now();
    progress.value = 0;
    progressMessage.value = '开始执行...';
    lastError.value = null;

    resetAllNodeStates();
  }

  function stopExecution(): void {
    isRunning.value = false;
    currentExecutingNodeId.value = null;

    if (executionStartTime.value) {
      executionTime.value = Date.now() - executionStartTime.value;
    }
  }

  function startContinuousRun(): void {
    isContinuousMode.value = true;
    startExecution();
  }

  function stopContinuousRun(): void {
    isContinuousMode.value = false;
    stopExecution();
  }

  function updateNodeState(
    nodeId: string,
    status: ExecutionStatus,
    options?: {
      errorMessage?: string;
      outputImage?: string;
      outputData?: Record<string, unknown>;
    },
  ): void {
    const existingState = nodeStates.value.get(nodeId);

    const newState: NodeExecutionState = {
      nodeId,
      status,
      startTime: existingState?.startTime || (status === 'running' ? Date.now() : undefined),
      endTime: status === 'success' || status === 'error' ? Date.now() : existingState?.endTime,
      errorMessage: status === 'error' ? options?.errorMessage ?? existingState?.errorMessage : undefined,
      outputImage: options?.outputImage ?? existingState?.outputImage,
      outputData: options?.outputData ?? existingState?.outputData,
    };

    nodeStates.value.set(nodeId, newState);

    if (status === 'running') {
      currentExecutingNodeId.value = nodeId;
    } else if (currentExecutingNodeId.value === nodeId) {
      currentExecutingNodeId.value = null;
    }
  }

  function setNodeRunning(nodeId: string): void {
    updateNodeState(nodeId, 'running');
  }

  function setNodeSuccess(
    nodeId: string,
    outputImage?: string,
    outputData?: Record<string, unknown>,
  ): void {
    updateNodeState(nodeId, 'success', {
      outputImage: normalizeOutputImage(outputImage),
      outputData,
    });
  }

  function setNodePreviewImage(nodeId: string, outputImage?: string): void {
    const normalizedOutputImage = normalizeOutputImage(outputImage);
    if (!normalizedOutputImage) {
      return;
    }

    const existingState = nodeStates.value.get(nodeId);

    nodeStates.value.set(nodeId, {
      nodeId,
      status: existingState?.status ?? 'idle',
      startTime: existingState?.startTime,
      endTime: existingState?.endTime,
      errorMessage: existingState?.errorMessage,
      outputImage: normalizedOutputImage,
      outputData: existingState?.outputData,
    });
  }

  function setNodeError(nodeId: string, errorMessage: string): void {
    updateNodeState(nodeId, 'error', { errorMessage });
    lastError.value = errorMessage;
  }

  function updateProgress(value: number, message?: string): void {
    progress.value = Math.min(100, Math.max(0, value));
    if (message) {
      progressMessage.value = message;
    }
  }

  function resetAllNodeStates(): void {
    nodeStates.value.clear();
    currentExecutingNodeId.value = null;
  }

  function clear(): void {
    isRunning.value = false;
    isContinuousMode.value = false;
    currentExecutingNodeId.value = null;
    nodeStates.value.clear();
    executionTime.value = 0;
    executionStartTime.value = null;
    progress.value = 0;
    progressMessage.value = '';
    lastError.value = null;
    latestCameraImage.value = null;
    latestCameraId.value = null;
    latestCameraMeta.value = null;
    resetCounters();
  }

  function resetCounters(): void {
    okCount.value = 0;
    ngCount.value = 0;
    cycleTimeMs.value = 0;
  }

  function clearNodeOutputImage(nodeId: string): void {
    const state = nodeStates.value.get(nodeId);
    if (state) {
      state.outputImage = undefined;
    }
  }

  const resolveCameraPreviewNodeId = (): string | null => {
    const selected = flowStore.selectedNode as any;

    if (
      selected?.id &&
      selected?.data?.rawType === 'ImageAcquisition' &&
      (selected?.data?.legacyConfig?.sourceType ?? 'camera') === 'camera'
    ) {
      return selected.id;
    }

    const cameraNode = flowStore.nodes.find((node: any) => {
      if (node?.data?.rawType !== 'ImageAcquisition') {
        return false;
      }

      const sourceType = node?.data?.legacyConfig?.sourceType ?? 'camera';
      return sourceType === 'camera';
    });

    return cameraNode?.id ?? null;
  };

  function handleOperatorExecuted(event: OperatorExecutedEvent): void {
    const { operatorId, isSuccess, outputData, outputImageBase64, errorMessage } = event;
    const nodeId = `${operatorId}`;
    const resolvedOutputImage = outputImageBase64 || extractImageFromOutputData(outputData);

    if (isSuccess) {
      setNodeSuccess(nodeId, resolvedOutputImage, outputData);
    } else {
      setNodeError(nodeId, errorMessage || '执行失败');
    }
  }

  function handleProgressNotification(event: ProgressNotificationEvent): void {
    updateProgress(event.progress, event.message);

    if (event.currentOperatorId) {
      setNodeRunning(`${event.currentOperatorId}`);
    }
  }

  function handleInspectionCompleted(event: InspectionCompletedEvent): void {
    stopExecution();
    updateProgress(100, '执行完成');
    executionTime.value = event.processingTimeMs;
    cycleTimeMs.value = event.processingTimeMs;

    const status = String(event.status || '').toUpperCase();
    if (status === 'PASS' || status === 'OK') {
      okCount.value += 1;
      lastInspectionResult.value = 'OK';
    } else if (status === 'FAIL' || status === 'NG') {
      ngCount.value += 1;
      lastInspectionResult.value = 'NG';
    } else {
      lastInspectionResult.value = 'Error';
    }
  }

  function handleImageStreamEvent(message: any): void {
    const event = getMessagePayload<ImageStreamEvent>(message);
    const outputImage = event.imageBase64 || event.outputImageBase64;

    if (!outputImage) {
      return;
    }

    const nodeId = event.operatorId ? `${event.operatorId}` : resolveCameraPreviewNodeId();
    if (!nodeId) {
      return;
    }

    setNodePreviewImage(nodeId, outputImage);
    latestCameraId.value = nodeId;
    if (nodeId === resolveCameraPreviewNodeId()) {
      latestCameraImage.value = outputImage;
    }
  }

  function handleSharedImageStream(message: any): void {
    const event = getMessagePayload<SharedImageStreamEvent>(message);
    if (!event?.buffer) {
      return;
    }

    const nodeId = resolveCameraPreviewNodeId();
    if (!nodeId) {
      return;
    }

    const width = Number(event.width) || 0;
    const height = Number(event.height) || 0;
    const imageDataUrl = sharedBufferToDataUrl(event.buffer, width, height);

    if (imageDataUrl) {
      setNodePreviewImage(nodeId, imageDataUrl);
      latestCameraImage.value = imageDataUrl;
      latestCameraId.value = nodeId;
      if (width > 0 && height > 0) {
        latestCameraMeta.value = { width, height };
      }
    }
  }

  function initializeBridgeListeners(): void {
    if (bridgeListenersInitialized.value) {
      return;
    }

    bridgeListenersInitialized.value = true;

    webMessageBridge.on('OperatorExecutedEvent', (message) => {
      handleOperatorExecuted(getMessagePayload<OperatorExecutedEvent>(message));
    });

    webMessageBridge.on('ProgressNotificationEvent', (message) => {
      handleProgressNotification(getMessagePayload<ProgressNotificationEvent>(message));
    });

    webMessageBridge.on('InspectionCompletedEvent', (message) => {
      handleInspectionCompleted(getMessagePayload<InspectionCompletedEvent>(message));
    });

    webMessageBridge.on('FlowExecutionStartedEvent', () => {
      startExecution();
    });

    webMessageBridge.on('FlowExecutionStoppedEvent', () => {
      stopExecution();
    });

    webMessageBridge.on('ImageStreamEvent', (message) => {
      handleImageStreamEvent(message);
    });

    webMessageBridge.on(BridgeMessageType.ImageStreamShared, (message) => {
      handleSharedImageStream(message);
    });
  }

  return {
    isRunning,
    currentExecutingNodeId,
    nodeStates,
    executionTime,
    cycleTimeMs,
    progress,
    progressMessage,
    lastError,
    
    lastInspectionResult,
    latestCameraImage,
    latestCameraId,
    latestCameraMeta,
    okCount,
    ngCount,
    totalCount,
    yieldRate,
    isContinuousMode,

    getNodeStatus,
    getNodeOutputImage,
    getNodeOutputData,
    hasRunningNodes,
    errorNodes,

    startExecution,
    stopExecution,
    startContinuousRun,
    stopContinuousRun,
    updateNodeState,
    setNodeRunning,
    setNodeSuccess,
    setNodePreviewImage,
    setNodeError,
    updateProgress,
    resetAllNodeStates,
    clear,
    clearNodeOutputImage,
    resetCounters,

    initializeBridgeListeners,
    handleOperatorExecuted,
    handleProgressNotification,
    handleInspectionCompleted,
  };
});
