<template>
  <div
    class="property-panel"
    v-if="selectedNode"
    :class="{ 'is-open': isOpen }"
  >
    <div class="panel-header">
      <div class="header-titles">
        <h3 class="panel-title">
          {{ schema?.displayName || selectedNode.data.name || "Configuration" }}
        </h3>
        <span class="panel-subtitle">{{ schema?.type || "Unknown Type" }}</span>
      </div>
      <button class="close-btn" @click="closePanel">
        <XIcon class="close-icon" />
      </button>
    </div>

    <div class="panel-content">
      <div v-if="!schema || !schema.parameters.length" class="empty-state">
        <div class="empty-icon-wrapper">
          <SettingsIcon class="empty-icon" />
        </div>
        <p>No configurable parameters for this operator.</p>
      </div>

      <div v-else class="property-form">
        <div
          v-for="param in schema.parameters"
          :key="param.name"
          class="form-group"
        >
          <label class="form-label">
            {{ param.displayName }}
            <span
              v-if="param.description"
              class="info-tooltip"
              :title="param.description"
            >
              <InfoIcon class="info-icon" />
            </span>
          </label>

          <!-- Enum / Select -->
          <select
            v-if="param.dataType === 'enum'"
            class="cv-input select-input"
            v-model="configProxy[String(param.name)]"
          >
            <option
              v-for="opt in param.options"
              :key="String(opt.value)"
              :value="opt.value"
            >
              {{ opt.label }}
            </option>
          </select>

          <!-- Boolean / Toggle -->
          <label class="cv-toggle" v-else-if="param.dataType === 'bool'">
            <input type="checkbox" v-model="configProxy[String(param.name)]" />
            <span class="toggle-slider"></span>
          </label>

          <!-- Int / Float -->
          <input
            v-else-if="['int', 'float'].includes(param.dataType)"
            type="number"
            class="cv-input"
            :step="param.dataType === 'int' ? 1 : 0.1"
            :min="param.min"
            :max="param.max"
            v-model.number="configProxy[String(param.name)]"
          />

          <!-- File Source -->
          <div v-else-if="param.dataType === 'file'" class="file-input-group">
            <input
              type="text"
              class="cv-input file-path-input"
              v-model="configProxy[String(param.name)]"
              readonly
              placeholder="Select a file..."
            />
            <button
              class="cv-btn file-browse-btn"
              @click="triggerFilePicker(param.name)"
            >
              <FolderOpenIcon class="browse-icon" />
            </button>
          </div>

          <!-- Fallback String / General input -->
          <input
            v-else
            type="text"
            class="cv-input"
            v-model="configProxy[String(param.name)]"
          />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch, reactive, onMounted, onBeforeUnmount } from "vue";
import { useFlowStore } from "../../stores/flow";
import { useExecutionStore } from "../../stores/execution";
import {
  getSchemaByType,
  type OperatorSchema,
} from "../../config/operatorSchema";
import { XIcon, SettingsIcon, InfoIcon, FolderOpenIcon } from "lucide-vue-next";
import { webMessageBridge } from "../../services/bridge";
import { BridgeMessageType } from "../../services/bridge.types";
import { resolveImageSource } from "../../services/imageSource";

const flowStore = useFlowStore();
const executionStore = useExecutionStore();
const isOpen = ref(false);
const pendingFilePickerContext = ref<{ nodeId: string; paramName: string } | null>(
  null,
);

const selectedNode = computed(() => flowStore.selectedNode);

const schema = computed<OperatorSchema | undefined>(() => {
  if (!selectedNode.value) return undefined;
  // Fallback to rawType if type is mapped to a generic node type
  const typeKey =
    selectedNode.value.data.rawType || selectedNode.value.data.category;
  return getSchemaByType(typeKey);
});

// Create a reactive proxy to the node's legacyConfig object.
// Changes here automatically mutate the flowStore's node reference, effectively achieving two-way binding.
const configProxy = reactive<Record<string, any>>({});

watch(
  selectedNode,
  (newVal) => {
    if (newVal) {
      // 1. Initialize configProxy with existing config or defaults from schema
      if (!newVal.data.legacyConfig) {
        newVal.data.legacyConfig = {};
      }

      // Wipe old proxy props
      for (const key in configProxy) {
        delete configProxy[key];
      }

      // Apply schema defaults if completely empty, otherwise load existing config
      if (schema.value) {
        schema.value.parameters.forEach((param) => {
          if (newVal.data.legacyConfig[param.name] === undefined) {
            configProxy[param.name] = param.defaultValue;
          } else {
            configProxy[param.name] = newVal.data.legacyConfig[param.name];
          }
        });
      }

      isOpen.value = true;
    } else {
      isOpen.value = false;
    }
  },
  { immediate: true },
);

// Sync proxy mutations back to the store's deep object and mark as dirty.
watch(
  () => ({ ...configProxy }),
  (newConfig) => {
    if (selectedNode.value) {
      selectedNode.value.data.legacyConfig = { ...newConfig };
      // This is vital for undo/redo stack snapshotting
      flowStore.isDirty = true;
    }
  },
  { deep: true },
);

const closePanel = () => {
  flowStore.selectNode(null);
  isOpen.value = false;
};

const isDesktopWebView = (): boolean => {
  const w = window as any;
  return Boolean(w?.chrome?.webview);
};

const readBlobAsDataUrl = (blob: Blob): Promise<string> => {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(`${reader.result || ""}`);
    reader.onerror = () => reject(reader.error || new Error("Failed to read file."));
    reader.readAsDataURL(blob);
  });
};

const getNativeFileFilter = (paramName: string): string => {
  if (paramName === "filePath") {
    return "Image Files|*.bmp;*.jpg;*.jpeg;*.png|All Files|*.*";
  }
  return "All Files|*.*";
};

const updateNodeConfigValue = (
  nodeId: string,
  paramName: string,
  value: string,
): void => {
  const node = flowStore.getNodeById(nodeId) as any;
  if (!node) return;

  if (!node.data.legacyConfig) {
    node.data.legacyConfig = {};
  }

  node.data.legacyConfig = {
    ...node.data.legacyConfig,
    [paramName]: value,
  };

  if (selectedNode.value?.id === nodeId) {
    configProxy[paramName] = value;
  }

  flowStore.isDirty = true;
};

const applyPickedFile = (
  nodeId: string,
  paramName: string,
  filePath: string,
  previewSource?: string,
) => {
  updateNodeConfigValue(nodeId, paramName, filePath);

  if (paramName === "filePath") {
    const resolvedPreview = resolveImageSource(previewSource || "");
    const canRenderPreview =
      !!resolvedPreview &&
      !resolvedPreview.toLowerCase().startsWith("file:");

    if (canRenderPreview) {
      executionStore.setNodePreviewImage(nodeId, resolvedPreview);
      return;
    }

    // In WebView, file:// is commonly blocked for img src from app.local.
    // Avoid feeding an invalid url to thumbnail and showing "preview load failed".
    if (isDesktopWebView()) {
      executionStore.clearNodeOutputImage(nodeId);
      return;
    }

    const fallbackSource = resolveImageSource(filePath);
    if (fallbackSource && !fallbackSource.toLowerCase().startsWith("file:")) {
      executionStore.setNodePreviewImage(nodeId, fallbackSource);
    }
  }
};

const tryReadPreviewFromLocalFile = async (
  filePath: string,
): Promise<string | undefined> => {
  const resolved = resolveImageSource(filePath);
  if (!resolved || !resolved.toLowerCase().startsWith("file:")) {
    return undefined;
  }

  try {
    const response = await fetch(resolved);
    if (!response.ok) {
      return undefined;
    }

    const blob = await response.blob();
    return await readBlobAsDataUrl(blob);
  } catch {
    return undefined;
  }
};

const handleFilePickedEvent = async (message: any) => {
  const payload = (message?.data && typeof message.data === "object")
    ? message.data
    : message;

  const isCancelled = Boolean(payload?.isCancelled ?? payload?.IsCancelled);
  if (isCancelled) {
    pendingFilePickerContext.value = null;
    return;
  }

  const paramName = `${payload?.parameterName ?? payload?.ParameterName ?? pendingFilePickerContext.value?.paramName ?? ""}`.trim();
  const filePath = `${payload?.filePath ?? payload?.FilePath ?? ""}`.trim();
  if (!paramName || !filePath) {
    pendingFilePickerContext.value = null;
    return;
  }

  const targetNodeId =
    pendingFilePickerContext.value?.nodeId ?? selectedNode.value?.id;
  if (!targetNodeId) {
    pendingFilePickerContext.value = null;
    return;
  }

  const previewRaw = `${payload?.previewImageBase64 ?? payload?.PreviewImageBase64 ?? payload?.payload?.previewImageBase64 ?? payload?.payload?.PreviewImageBase64 ?? ""}`.trim();
  let previewSource = resolveImageSource(previewRaw);

  if (!previewSource && paramName === "filePath") {
    previewSource = (await tryReadPreviewFromLocalFile(filePath)) || "";
  }

  applyPickedFile(
    targetNodeId,
    paramName,
    filePath,
    previewSource || undefined,
  );
  pendingFilePickerContext.value = null;
};

onMounted(() => {
  webMessageBridge.on(BridgeMessageType.FilePickedEvent, handleFilePickedEvent);
});

onBeforeUnmount(() => {
  webMessageBridge.off(BridgeMessageType.FilePickedEvent);
});

const triggerFilePicker = async (paramName: string) => {
  const targetNodeId = selectedNode.value?.id;
  if (!targetNodeId) return;

  pendingFilePickerContext.value = {
    nodeId: targetNodeId,
    paramName,
  };

  if (isDesktopWebView()) {
    try {
      await webMessageBridge.sendMessage(BridgeMessageType.PickFileCommand, {
        parameterName: paramName,
        filter: getNativeFileFilter(paramName),
        includePreviewBase64: paramName === "filePath",
      });
      return;
    } catch (error) {
      console.warn("[PropertyPanel] Native file picker failed.", error);
    }
  }

  const input = document.createElement("input");
  input.type = "file";
  input.accept = paramName === "filePath" ? "image/*" : "*/*";
  input.onchange = async (e) => {
    const file = (e.target as HTMLInputElement).files?.[0];
    if (!file) {
      pendingFilePickerContext.value = null;
      return;
    }

    try {
      const previewDataUrl =
        paramName === "filePath" ? await readBlobAsDataUrl(file) : undefined;
      applyPickedFile(targetNodeId, paramName, file.name, previewDataUrl);
    } catch {
      applyPickedFile(targetNodeId, paramName, file.name);
    } finally {
      pendingFilePickerContext.value = null;
    }
  };
  input.click();
};
</script>

<style scoped>
.property-panel {
  position: absolute;
  top: 16px;
  right: -360px; /* Hide by default */
  width: 320px;
  height: calc(100% - 32px);
  background: var(--glass-bg, rgba(255, 255, 255, 0.85));
  backdrop-filter: blur(32px);
  -webkit-backdrop-filter: blur(32px);
  border: 1px solid var(--border-glass, rgba(0, 0, 0, 0.05));
  border-radius: 16px;
  box-shadow: -8px 0 32px rgba(0, 0, 0, 0.04);
  display: flex;
  flex-direction: column;
  transition: right 0.4s cubic-bezier(0.16, 1, 0.3, 1);
  z-index: 50;
}

.property-panel.is-open {
  right: 16px;
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 16px 20px;
  border-bottom: 1px solid var(--border-glass, rgba(0, 0, 0, 0.05));
  background: rgba(0, 0, 0, 0.01);
  border-top-left-radius: 16px;
  border-top-right-radius: 16px;
}

.header-titles {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.panel-title {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: var(--text-primary, #1c1c1e);
}

.panel-subtitle {
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: var(--accent-red, #ff4d4d);
}

.close-btn {
  background: transparent;
  border: none;
  border-radius: 6px;
  width: 28px;
  height: 28px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--text-muted, #64748b);
  cursor: pointer;
  transition: all 0.2s ease;
}

.close-btn:hover {
  background: rgba(0, 0, 0, 0.05);
  color: var(--text-primary, #1c1c1e);
}

.close-icon {
  width: 18px;
  height: 18px;
}

.panel-content {
  flex: 1;
  overflow-y: auto;
  padding: 20px;
}

/* Custom Scrollbar */
.panel-content::-webkit-scrollbar {
  width: 6px;
}
.panel-content::-webkit-scrollbar-track {
  background: transparent;
}
.panel-content::-webkit-scrollbar-thumb {
  background: rgba(0, 0, 0, 0.1);
  border-radius: 3px;
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: var(--text-muted, #64748b);
  text-align: center;
  gap: 16px;
}

.empty-icon-wrapper {
  width: 64px;
  height: 64px;
  border-radius: 50%;
  background: rgba(0, 0, 0, 0.03);
  display: flex;
  align-items: center;
  justify-content: center;
  color: rgba(100, 116, 139, 0.5);
}

.empty-icon {
  width: 32px;
  height: 32px;
}

.property-form {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.form-label {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  font-weight: 500;
  color: var(--text-primary, #1c1c1e);
}

.info-tooltip {
  display: flex;
  align-items: center;
  color: var(--text-muted, #64748b);
  cursor: help;
}

.info-icon {
  width: 14px;
  height: 14px;
}

/* Common form controls */
.cv-input {
  width: 100%;
  height: 36px;
  padding: 0 12px;
  background: rgba(255, 255, 255, 0.7);
  border: 1px solid var(--border-glass, rgba(0, 0, 0, 0.1));
  border-radius: 8px;
  font-family: inherit;
  font-size: 13px;
  color: var(--text-primary, #1c1c1e);
  outline: none;
  transition: all 0.2s ease;
}

.cv-input:focus {
  border-color: var(--accent-red, #ff4d4d);
  box-shadow: 0 0 0 2px rgba(255, 77, 77, 0.1);
  background: #ffffff;
}

.file-input-group {
  display: flex;
  gap: 8px;
}

.file-path-input {
  flex: 1;
  background: rgba(0, 0, 0, 0.02);
  cursor: not-allowed;
}

.file-browse-btn {
  width: 36px;
  height: 36px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--bg-secondary, #ffffff);
  border: 1px solid var(--border-glass, rgba(0, 0, 0, 0.1));
  border-radius: 8px;
  color: var(--text-primary, #1c1c1e);
  cursor: pointer;
  transition: all 0.2s ease;
}

.file-browse-btn:hover {
  background: rgba(0, 0, 0, 0.05);
}

.browse-icon {
  width: 18px;
  height: 18px;
}

/* Custom modern toggle switch */
.cv-toggle {
  position: relative;
  display: inline-block;
  width: 44px;
  height: 24px;
}

.cv-toggle input {
  opacity: 0;
  width: 0;
  height: 0;
}

.toggle-slider {
  position: absolute;
  cursor: pointer;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(0, 0, 0, 0.1);
  transition: 0.3s cubic-bezier(0.25, 0.8, 0.25, 1);
  border-radius: 24px;
}

.toggle-slider:before {
  position: absolute;
  content: "";
  height: 18px;
  width: 18px;
  left: 3px;
  bottom: 3px;
  background-color: white;
  transition: 0.3s cubic-bezier(0.25, 0.8, 0.25, 1);
  border-radius: 50%;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

input:checked + .toggle-slider {
  background-color: var(--accent-red, #ff4d4d);
}

input:checked + .toggle-slider:before {
  transform: translateX(20px);
}
</style>
