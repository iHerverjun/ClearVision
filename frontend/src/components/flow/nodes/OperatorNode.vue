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
        <div class="node-name">{{ data.name || "算子" }}</div>
        <div class="node-category">{{ data.category || "通用" }}</div>
      </div>
    </div>

    <!-- Inputs -->
    <div class="node-ports inputs">
      <div
        class="port-row"
        v-for="port in inputPorts"
        :key="port.handleId"
        @mouseenter="hoveredPort = port.handleId"
        @mouseleave="hoveredPort = null"
      >
        <Handle
          type="target"
          :position="LEFT_POSITION"
          :id="port.handleId"
          :class="[
            'custom-handle',
            'custom-handle-input',
            `shape-${getShapeCategoryClass(port.type)}`,
            {
              'is-connected': isPortConnected(port.handleId, 'target'),
              'is-disconnected': !isPortConnected(port.handleId, 'target'),
              'handle-compatible': connectingType && connectingDir === 'source' && checkCompatibility(connectingType, port.type),
              'handle-incompatible': connectingType && connectingDir === 'source' && !checkCompatibility(connectingType, port.type),
            },
          ]"
          :connectable="true"
          :connectable-start="false"
          :connectable-end="true"
          :style="{
            '--port-color': getColor(port.type),
            '--port-glow': getGlow(port.type),
          }"
        >
          <span class="port-visual" aria-hidden="true">
            <svg
              class="port-shape"
              :class="`shape-${getShapeCategoryClass(port.type)}`"
              viewBox="0 0 14 14"
            >
              <circle class="shape-circle" cx="7" cy="7" r="4.4" />
              <rect
                class="shape-diamond"
                x="3.4"
                y="3.4"
                width="7.2"
                height="7.2"
                rx="1.2"
                transform="rotate(45 7 7)"
              />
              <rect class="shape-square" x="3.1" y="3.1" width="7.8" height="7.8" rx="1.2" />
            </svg>
          </span>
        </Handle>
        <span class="port-label">{{ port.label }}</span>
        <!-- Tooltip -->
        <div
          v-if="hoveredPort === port.handleId"
          class="port-tooltip port-tooltip-left"
        >
          <span
            class="tooltip-dot"
            :class="`shape-${getShapeCategoryClass(port.type)}`"
            :style="{ '--dot-color': getColor(port.type) }"
          />
          <span class="tooltip-text">{{ getLabel(port.type) }}</span>
        </div>
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
      <div
        class="port-row"
        v-for="port in outputPorts"
        :key="port.handleId"
        @mouseenter="hoveredPort = port.handleId"
        @mouseleave="hoveredPort = null"
      >
        <span class="port-label">{{ port.label }}</span>
        <Handle
          type="source"
          :position="RIGHT_POSITION"
          :id="port.handleId"
          :class="[
            'custom-handle',
            'custom-handle-output',
            `shape-${getShapeCategoryClass(port.type)}`,
            {
              'is-connected': isPortConnected(port.handleId, 'source'),
              'is-disconnected': !isPortConnected(port.handleId, 'source'),
              'handle-compatible': connectingType && connectingDir === 'target' && checkCompatibility(port.type, connectingType),
              'handle-incompatible': connectingType && connectingDir === 'target' && !checkCompatibility(port.type, connectingType),
            },
          ]"
          :connectable="true"
          :connectable-start="true"
          :connectable-end="false"
          :style="{
            '--port-color': getColor(port.type),
            '--port-glow': getGlow(port.type),
          }"
        >
          <span class="port-visual" aria-hidden="true">
            <svg
              class="port-shape"
              :class="`shape-${getShapeCategoryClass(port.type)}`"
              viewBox="0 0 14 14"
            >
              <circle class="shape-circle" cx="7" cy="7" r="4.4" />
              <rect
                class="shape-diamond"
                x="3.4"
                y="3.4"
                width="7.2"
                height="7.2"
                rx="1.2"
                transform="rotate(45 7 7)"
              />
              <rect class="shape-square" x="3.1" y="3.1" width="7.8" height="7.8" rx="1.2" />
            </svg>
          </span>
        </Handle>
        <!-- Tooltip -->
        <div
          v-if="hoveredPort === port.handleId"
          class="port-tooltip port-tooltip-right"
        >
          <span
            class="tooltip-dot"
            :class="`shape-${getShapeCategoryClass(port.type)}`"
            :style="{ '--dot-color': getColor(port.type) }"
          />
          <span class="tooltip-text">{{ getLabel(port.type) }}</span>
        </div>
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
import { computed, ref, type PropType } from "vue";
import { Handle, type Position } from "@vue-flow/core";
import { BoxIcon, CameraIcon, TargetIcon, NetworkIcon, LoaderIcon, CheckIcon, XIcon } from "lucide-vue-next";
import { useExecutionStore, type ExecutionStatus } from "../../../stores/execution";
import { useFlowStore } from "../../../stores/flow";
import NodeThumbnail from "./NodeThumbnail.vue";

import {
  getSchemaByType,
  type ParameterSchema,
  type ParameterOption,
} from "../../../config/operatorSchema";

import {
  getPortConfig,
  getPortColor,
  getPortGlowColor,
  getShapeCategory,
  isTypeCompatible,
} from "../../../config/portTypeRegistry";

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
const flowStore = useFlowStore();

// ─── Tooltip State ─────────────────────────────
const hoveredPort = ref<string | null>(null);

// ─── Connecting State (from flow store) ────────
const connectingType = computed(() => (flowStore as any).connectingSourceType ?? null);
const connectingDir = computed(() => (flowStore as any).connectingDirection ?? null);

// ─── Execution State ───────────────────────────
const executionStatus = computed<ExecutionStatus>(() => {
  return executionStore.getNodeStatus(props.id);
});

const outputImage = computed(() => {
  return executionStore.getNodeOutputImage(props.id);
});

// ─── Icon ──────────────────────────────────────
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

// ─── Port Normalization ────────────────────────
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
      label: port.label ? `${port.label}` : `端口 ${index + 1}`,
      type: port.type ? `${port.type}` : "未知",
    };
  });
};

const inputPorts = computed(() => normalizePorts(props.data?.inputs, "in"));
const outputPorts = computed(() => normalizePorts(props.data?.outputs, "out"));

// ─── Property Preview ──────────────────────────
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

const previewParameters = computed(() => {
  if (!schema.value?.parameters) return [];
  return schema.value.parameters.slice(0, 3);
});

const formatConfigValue = (val: any, param: ParameterSchema) => {
  if (val === undefined || val === null) return "--";

  if (param.dataType === "bool") {
    return val ? "是" : "否";
  }

  if (param.dataType === "enum" && param.options) {
    const opt = param.options.find(
      (o: ParameterOption) => String(o.value) === String(val),
    );
    return opt ? opt.label : val;
  }

  if (param.dataType === "file") {
    if (typeof val === "string") {
      return val.split(/[\\/]/).pop() || val;
    }
  }

  return String(val);
};

// ─── Port Helpers (delegated to registry) ──────
const getColor = (type: string) => getPortColor(type);
const getGlow = (type: string) => getPortGlowColor(type);
const getLabel = (type: string) => getPortConfig(type).label;
const getShapeCategoryClass = (type: string) => getShapeCategory(type);
const checkCompatibility = (src: string, tgt: string) => isTypeCompatible(src, tgt);

// ─── Port Connection Check ─────────────────────
const isPortConnected = (handleId: string, handleType: 'source' | 'target') => {
  return flowStore.edges.some(
    (e) =>
      (handleType === 'source' && e.source === props.id && e.sourceHandle === handleId) ||
      (handleType === 'target' && e.target === props.id && e.targetHandle === handleId),
  );
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
  height: 28px;
  overflow: visible;
}

.inputs .port-row {
  justify-content: flex-start;
  padding-left: 18px;
}

.outputs .port-row {
  justify-content: flex-end;
  padding-right: 18px;
}

.port-label {
  font-size: 12px;
  font-weight: 500;
  color: var(--text-muted, #64748b);
}

/* Port Tooltip */
.port-tooltip {
  position: absolute;
  top: 50%;
  transform: translateY(-50%);
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 8px;
  background: rgba(15, 23, 42, 0.9);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 8px;
  white-space: nowrap;
  z-index: 1000;
  pointer-events: none;
  box-shadow: 0 4px 14px rgba(0, 0, 0, 0.24);
}

.port-tooltip-left {
  right: calc(100% + 16px);
}

.port-tooltip-right {
  left: calc(100% + 16px);
}

.tooltip-dot {
  width: 8px;
  height: 8px;
  background: var(--dot-color, #9CA3AF);
  flex-shrink: 0;
}

.tooltip-dot.shape-circle {
  border-radius: 50%;
}

.tooltip-dot.shape-diamond {
  transform: rotate(45deg);
  border-radius: 1px;
}

.tooltip-dot.shape-square {
  border-radius: 2px;
}

.tooltip-text {
  font-size: 11px;
  font-weight: 500;
  color: rgba(255, 255, 255, 0.9);
}

/* Port Handles: 32x32 hit area with inline SVG */
:deep(.vue-flow__handle.custom-handle) {
  width: 32px !important;
  height: 32px !important;
  min-width: 32px !important;
  min-height: 32px !important;
  display: flex !important;
  align-items: center;
  justify-content: center;
  opacity: 1 !important;
  visibility: visible !important;
  position: absolute !important;
  border: none !important;
  background: transparent !important;
  box-shadow: none !important;
  pointer-events: all !important;
  cursor: pointer;
  z-index: 999 !important;
  transition:
    transform 0.16s ease,
    opacity 0.16s ease,
    filter 0.16s ease;
}

:deep(.vue-flow__handle.custom-handle .port-visual) {
  width: 14px;
  height: 14px;
  display: block;
  transition:
    transform 0.16s ease,
    filter 0.16s ease;
}

:deep(.vue-flow__handle.custom-handle .port-shape) {
  width: 14px;
  height: 14px;
  display: block;
}

:deep(.vue-flow__handle.custom-handle .port-shape .shape-circle),
:deep(.vue-flow__handle.custom-handle .port-shape .shape-diamond),
:deep(.vue-flow__handle.custom-handle .port-shape .shape-square) {
  display: none;
  stroke: var(--port-color, #4d94ff);
  stroke-width: 1.8;
  vector-effect: non-scaling-stroke;
  transition:
    fill 0.16s ease,
    stroke 0.16s ease,
    opacity 0.16s ease,
    stroke-width 0.16s ease;
}

:deep(.vue-flow__handle.custom-handle .port-shape.shape-circle .shape-circle),
:deep(.vue-flow__handle.custom-handle .port-shape.shape-diamond .shape-diamond),
:deep(.vue-flow__handle.custom-handle .port-shape.shape-square .shape-square) {
  display: block;
}

:deep(.vue-flow__handle.custom-handle.is-disconnected .port-shape > *) {
  fill: transparent;
  opacity: 0.78;
}

:deep(.vue-flow__handle.custom-handle.is-connected .port-shape > *) {
  fill: var(--port-color, #4d94ff);
  opacity: 1;
  stroke-width: 1.2;
}

:deep(.vue-flow__handle.custom-handle:hover .port-visual) {
  transform: scale(1.14);
  filter: drop-shadow(0 0 5px var(--port-glow, rgba(77, 148, 255, 0.45)));
}

:deep(.vue-flow__handle.custom-handle.connecting .port-visual),
:deep(.vue-flow__handle.custom-handle.connectionindicator .port-visual) {
  transform: scale(1.2);
  filter: drop-shadow(0 0 7px var(--port-glow, rgba(77, 148, 255, 0.5)));
}

:deep(.vue-flow__handle.custom-handle.valid .port-visual),
:deep(.vue-flow__handle.custom-handle.vue-flow__handle-valid .port-visual) {
  filter: drop-shadow(0 0 8px rgba(16, 185, 129, 0.6));
}

/* Compatibility highlights during drag */
:deep(.vue-flow__handle.custom-handle.handle-compatible .port-visual) {
  transform: scale(1.3);
  filter: drop-shadow(0 0 9px rgba(16, 185, 129, 0.75));
  animation: handle-compatible-pulse 1.1s ease-in-out infinite;
}

:deep(.vue-flow__handle.custom-handle.handle-compatible .port-shape > *) {
  stroke: #10b981;
}

@keyframes handle-compatible-pulse {
  0%,
  100% {
    transform: scale(1.2);
  }
  50% {
    transform: scale(1.3);
  }
}

:deep(.vue-flow__handle.custom-handle.handle-incompatible) {
  pointer-events: none;
  opacity: 0.15;
  transform: scale(0.7);
  filter: grayscale(1);
}

/* Position overrides */
:deep(.inputs .vue-flow__handle.custom-handle) {
  left: -18px !important;
  right: auto !important;
}

:deep(.outputs .vue-flow__handle.custom-handle) {
  right: -18px !important;
  left: auto !important;
}

/* ════════════════════════════════════════════════
   Node Content & Property Preview
   ════════════════════════════════════════════════ */
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

/* ════════════════════════════════════════════════
   Execution Status Styles
   ════════════════════════════════════════════════ */
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

/* Running State */
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

/* Success State */
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

/* Error State */
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
