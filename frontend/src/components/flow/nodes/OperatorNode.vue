<template>
  <div 
    class="operator-node" 
    :class="[
      { 'is-selected': selected },
      `status-${executionStatus}`
    ]"
  >
    <!-- Execution Status Indicator -->
    <div v-if="executionStatus !== 'idle'" class="status-indicator">
      <div v-if="executionStatus === 'running'" class="status-icon running">
        <LoaderIcon class="spin-icon" />
      </div>
      <div v-else-if="executionStatus === 'success'" class="status-icon success">
        <CheckIcon />
      </div>
      <div v-else-if="executionStatus === 'error'" class="status-icon error">
        <XIcon />
      </div>
    </div>
    <!-- Header -->
    <div class="node-header">
      <div class="icon-wrapper">
        <component :is="iconComponent" class="node-icon" v-if="iconComponent" />
      </div>
      <div class="title-wrapper">
        <div class="node-name">{{ data.name || "Operator" }}</div>
        <div class="node-category">{{ data.category || "General" }}</div>
      </div>
    </div>

    <!-- Inputs -->
    <div class="node-ports inputs">
      <div class="port-row" v-for="port in inputPorts" :key="port.handleId">
        <Handle
          type="target"
          :position="LEFT_POSITION"
          :id="port.handleId"
          class="custom-handle"
          :connectable="true"
          :connectable-end="true"
          :style="{ backgroundColor: getPortColor(port.type) }"
        />
        <span class="port-label">{{ port.label }}</span>
      </div>
    </div>

    <!-- Central Widget Slot / Property Previews -->
    <div class="node-content">
      <div v-if="hasProperties" class="property-preview-list">
        <div
          v-for="param in previewParameters"
          :key="param.name"
          class="preview-item"
        >
          <span class="preview-label"
            >{{ param.displayName || param.name }}:</span
          >
          <span class="preview-value">{{
            formatConfigValue((configProxy as any)[param.name], param)
          }}</span>
        </div>
      </div>
      <slot></slot>
    </div>

    <!-- Outputs -->
    <div class="node-ports outputs">
      <div class="port-row" v-for="port in outputPorts" :key="port.handleId">
        <span class="port-label">{{ port.label }}</span>
        <Handle
          type="source"
          :position="RIGHT_POSITION"
          :id="port.handleId"
          class="custom-handle"
          :connectable="true"
          :connectable-start="true"
          :style="{ backgroundColor: getPortColor(port.type) }"
        />
      </div>
    </div>

    <!-- Thumbnail (Phase 4c) -->
    <div class="node-thumbnail-container">
      <NodeThumbnail
        :output-image="outputImage"
        :node-id="id"
        :node-name="data.name"
      />
    </div>
    <div class="node-thumbnail-container" v-if="$slots.thumbnail">
      <slot name="thumbnail"></slot>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, type PropType } from "vue";
import { Handle, type Position } from "@vue-flow/core";
import { BoxIcon, CameraIcon, TargetIcon, NetworkIcon, LoaderIcon, CheckIcon, XIcon } from "lucide-vue-next";
import { useExecutionStore, type ExecutionStatus } from "../../../stores/execution";
import NodeThumbnail from "./NodeThumbnail.vue";

import {
  getSchemaByType,
  type ParameterSchema,
  type ParameterOption,
} from "../../../config/operatorSchema";

interface PortLike {
  id?: string | number | null;
  label?: string | null;
  type?: string | null;
}

interface NormalizedPort {
  handleId: string;
  label: string;
  type: string;
}

const LEFT_POSITION = "left" as Position;
const RIGHT_POSITION = "right" as Position;

const props = defineProps({
  id: { type: String, required: true },
  data: {
    type: Object as PropType<{
      name?: string;
      category?: string;
      inputs?: any[];
      outputs?: any[];
      iconType?: string;
      rawType?: string;
      legacyConfig?: Record<string, any>;
      [key: string]: any;
    }>,
    default: () => ({
      name: "",
      category: "",
      inputs: [],
      outputs: [],
      iconType: "",
    }),
  },
  selected: { type: Boolean, default: false },
});

const executionStore = useExecutionStore();

// 从 execution store 获取当前节点的执行状态
const executionStatus = computed<ExecutionStatus>(() => {
  return executionStore.getNodeStatus(props.id);
});

// 从 execution store 获取当前节点的输出图像
const outputImage = computed(() => {
  return executionStore.getNodeOutputImage(props.id);
});

const iconComponent = computed(() => {
  switch (props.data.iconType) {
    case "camera":
      return CameraIcon;
    case "target":
      return TargetIcon;
    case "network":
      return NetworkIcon;
    default:
      return BoxIcon;
  }
});

const normalizePorts = (ports: unknown, direction: "in" | "out") => {
  if (!Array.isArray(ports)) return [] as NormalizedPort[];

  return ports.map((rawPort, index) => {
    const port = (rawPort ?? {}) as PortLike;
    const normalizedId =
      port.id !== undefined && port.id !== null && `${port.id}`.trim() !== ""
        ? `${port.id}`
        : `${props.id}-${direction}-${index}`;

    return {
      handleId: normalizedId,
      label: port.label ? `${port.label}` : `Port ${index + 1}`,
      type: port.type ? `${port.type}` : "Unknown",
    };
  });
};

const inputPorts = computed(() => normalizePorts(props.data?.inputs, "in"));
const outputPorts = computed(() => normalizePorts(props.data?.outputs, "out"));

const configProxy = computed<any>(() => props.data?.legacyConfig || {});

const schema = computed(() => {
  const typeKey = props.data.rawType || props.data.category || "";
  return getSchemaByType(typeKey);
});

const hasProperties = computed(() => {
  return (
    schema.value &&
    schema.value.parameters &&
    schema.value.parameters.length > 0
  );
});

// Show max 3 parameters on the node card preview to keep it clean
const previewParameters = computed(() => {
  if (!schema.value?.parameters) return [];
  return schema.value.parameters.slice(0, 3);
});

const formatConfigValue = (val: any, param: ParameterSchema) => {
  if (val === undefined || val === null) return "--";

  if (param.dataType === "bool") {
    return val ? "True" : "False";
  }

  if (param.dataType === "enum" && param.options) {
    const opt = param.options.find(
      (o: ParameterOption) => String(o.value) === String(val),
    );
    return opt ? opt.label : val;
  }

  if (param.dataType === "file") {
    // Just show the filename instead of full path
    if (typeof val === "string") {
      return val.split(/[\\/]/).pop() || val;
    }
  }

  return String(val);
};

// Port Data Type Colors (Light UI Theme Palette)
const getPortColor = (type: string) => {
  const mapping: Record<string, string> = {
    Image: "#4D94FF", // Blue
    Integer: "#00E676", // Green
    Float: "#FFA726", // Orange
    Boolean: "#FF4D4D", // Red
    String: "#BA68C8", // Purple
    Point: "#00BCD4", // Cyan
  };
  return mapping[type] || "#9CA3AF"; // Default Gray
};
</script>

<style scoped>
.operator-node {
  background: var(--glass-bg, rgba(255, 255, 255, 0.8));
  backdrop-filter: blur(24px);
  -webkit-backdrop-filter: blur(24px);
  border: 1px solid var(--border-glass, rgba(0, 0, 0, 0.05));
  border-radius: 16px;
  min-width: 220px;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.03);
  transition: all 0.2s cubic-bezier(0.25, 0.8, 0.25, 1);
  overflow: visible;
  position: relative;
  font-family: "Inter", sans-serif;
}

.operator-node.is-selected {
  border-color: var(--accent-red, #ff4d4d);
  box-shadow: 0 8px 32px rgba(255, 77, 77, 0.15);
  transform: translateY(-2px);
}

.node-header {
  display: flex;
  align-items: center;
  padding: 12px 16px;
  background: rgba(0, 0, 0, 0.02);
  border-bottom: 1px solid var(--border-glass, rgba(0, 0, 0, 0.05));
  gap: 12px;
  /* Re-apply radius to top to prevent header bg overflow */
  border-top-left-radius: 15px;
  border-top-right-radius: 15px;
}

.icon-wrapper {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  background: var(--bg-secondary, #ffffff);
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
  color: var(--accent-red, #ff4d4d);
}

.node-icon {
  width: 16px;
  height: 16px;
  stroke-width: 2.5px;
}

.title-wrapper {
  display: flex;
  flex-direction: column;
}

.node-name {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary, #1c1c1e);
  line-height: 1.2;
}

.node-category {
  font-size: 11px;
  font-weight: 500;
  color: var(--text-muted, #64748b);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  margin-top: 2px;
}

.node-ports {
  padding: 12px 0;
  display: flex;
  flex-direction: column;
  gap: 8px;
  overflow: visible;
}

.port-row {
  display: flex;
  align-items: center;
  position: relative !important;
  height: 24px;
  overflow: visible;
}

.inputs .port-row {
  justify-content: flex-start;
  padding-left: 16px;
}

.outputs .port-row {
  justify-content: flex-end;
  padding-right: 16px;
}

.port-label {
  font-size: 12px;
  font-weight: 500;
  color: var(--text-muted, #64748b);
}

/* Base Handle Styling: Need to use :deep() because Handle is an external component and scoped css might not pierce it */
:deep(.vue-flow__handle.custom-handle) {
  width: 14px !important;
  height: 14px !important;
  min-width: 14px !important;
  min-height: 14px !important;
  display: block !important;
  opacity: 1 !important;
  visibility: visible !important;
  position: absolute !important;
  border-radius: 50% !important;
  border: 2px solid rgba(255, 255, 255, 0.96) !important;
  box-shadow:
    0 0 0 2px rgba(255, 255, 255, 0.9),
    0 4px 10px rgba(0, 0, 0, 0.18) !important;
  transition:
    transform 0.2s ease,
    box-shadow 0.2s ease,
    filter 0.2s ease;
  z-index: 999 !important;
  background-color: var(--vf-handle, #ff4d4d); /* Fallback */
}

:deep(.vue-flow__handle.custom-handle::before) {
  content: "";
  position: absolute;
  inset: -8px;
  border-radius: 50%;
  background: transparent;
}

:deep(.vue-flow__handle.custom-handle:hover),
:deep(.vue-flow__handle.custom-handle.connectable:hover) {
  transform: scale(1.18);
  filter: saturate(1.1);
  box-shadow:
    0 0 0 4px rgba(77, 148, 255, 0.25),
    0 6px 14px rgba(0, 0, 0, 0.2) !important;
}

:deep(.vue-flow__handle.custom-handle.connecting) {
  transform: scale(1.22);
  box-shadow:
    0 0 0 4px rgba(77, 148, 255, 0.28),
    0 8px 18px rgba(0, 0, 0, 0.22) !important;
}

/* Position overrides since parent padding messes with absolute handles */
:deep(.inputs .vue-flow__handle.custom-handle) {
  left: -10px !important;
  right: auto !important;
}

:deep(.outputs .vue-flow__handle.custom-handle) {
  right: -10px !important;
  left: auto !important;
}

.node-content {
  padding: 0 16px 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.property-preview-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
  background: rgba(0, 0, 0, 0.02);
  border-radius: 6px;
  padding: 6px 8px;
  border: 1px solid var(--border-glass, rgba(0, 0, 0, 0.03));
}

.preview-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 10px;
}

.preview-label {
  color: var(--text-muted, #64748b);
  max-width: 60px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.preview-value {
  color: var(--text-primary, #1c1c1e);
  font-weight: 600;
  max-width: 100px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  text-align: right;
}

.node-thumbnail-container {
  padding: 0 12px 12px;
}

/* ========== Execution Status Styles ========== */

.status-indicator {
  position: absolute;
  top: -8px;
  right: -8px;
  z-index: 10;
}

.status-icon {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
}

.status-icon.running {
  background: linear-gradient(135deg, #E84855 0%, #FF6B6B 100%);
  color: white;
}

.status-icon.success {
  background: linear-gradient(135deg, #10B981 0%, #34D399 100%);
  color: white;
  animation: success-pop 0.3s ease-out;
}

.status-icon.error {
  background: linear-gradient(135deg, #EF4444 0%, #F87171 100%);
  color: white;
  animation: error-shake 0.4s ease-out;
}

.status-icon svg {
  width: 14px;
  height: 14px;
}

.spin-icon {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

@keyframes success-pop {
  0% { transform: scale(0); }
  50% { transform: scale(1.2); }
  100% { transform: scale(1); }
}

@keyframes error-shake {
  0%, 100% { transform: translateX(0); }
  25% { transform: translateX(-4px); }
  75% { transform: translateX(4px); }
}

/* Running State - Cinnabar Pulse Animation */
.operator-node.status-running {
  border-color: #E84855;
  animation: running-pulse 1.5s ease-in-out infinite;
  box-shadow: 
    0 4px 16px rgba(0, 0, 0, 0.03),
    0 0 20px rgba(232, 72, 85, 0.3);
}

@keyframes running-pulse {
  0%, 100% {
    box-shadow: 
      0 4px 16px rgba(0, 0, 0, 0.03),
      0 0 20px rgba(232, 72, 85, 0.2);
  }
  50% {
    box-shadow: 
      0 4px 16px rgba(0, 0, 0, 0.03),
      0 0 30px rgba(232, 72, 85, 0.5);
  }
}

/* Success State - Green Border with Check Mark */
.operator-node.status-success {
  border-color: #10B981;
  box-shadow: 
    0 4px 16px rgba(0, 0, 0, 0.03),
    0 0 15px rgba(16, 185, 129, 0.25);
  animation: success-fade 2s ease-out forwards;
}

@keyframes success-fade {
  0% {
    box-shadow: 
      0 4px 16px rgba(0, 0, 0, 0.03),
      0 0 20px rgba(16, 185, 129, 0.4);
  }
  100% {
    box-shadow: 
      0 4px 16px rgba(0, 0, 0, 0.03),
      0 0 10px rgba(16, 185, 129, 0.1);
  }
}

/* Error State - Red Border with X Mark */
.operator-node.status-error {
  border-color: #EF4444;
  box-shadow: 
    0 4px 16px rgba(0, 0, 0, 0.03),
    0 0 20px rgba(239, 68, 68, 0.3);
  animation: error-blink 0.5s ease-out;
}

@keyframes error-blink {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.7; }
}
</style>
