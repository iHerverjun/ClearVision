<template>
  <defs>
    <linearGradient
      :id="gradientId"
      gradientUnits="userSpaceOnUse"
      :x1="props.sourceX"
      :y1="props.sourceY"
      :x2="props.targetX"
      :y2="props.targetY"
    >
      <stop offset="0%" :stop-color="sourceColor" />
      <stop offset="100%" :stop-color="targetColor" />
    </linearGradient>
  </defs>

  <path :d="path[0]" class="edge-glow-layer" :style="glowStyle" />

  <BaseEdge
    :id="id"
    :path="path[0]"
    :marker-start="markerStart"
    :marker-end="markerEnd"
    :interaction-width="interactionWidth ?? 36"
    :style="edgeStyle"
    :class="['typed-edge', { 'is-selected': selected }]"
  />

  <path :d="path[0]" class="edge-flow-particles" :style="flowParticleStyle" />

  <EdgeLabelRenderer>
    <div
      class="edge-actions-wrapper nodrag nopan"
      :style="{
        transform: `translate(-50%, -50%) translate(${path[1]}px,${path[2]}px)`,
      }"
      @mouseenter="showActions = true"
      @mouseleave="showActions = false"
    >
      <div
        class="edge-type-badge"
        :style="{ '--badge-color': edgeColor, '--badge-bg': badgeBgColor }"
      >
        <span class="badge-dot" :style="{ background: edgeColor }" />
        <span class="badge-text">{{ typeLabel }}</span>
      </div>
      <button
        v-show="showActions"
        class="edge-delete-btn"
        @click.stop.prevent="onDeleteEdge"
        title="Delete connection"
      >
        <svg width="10" height="10" viewBox="0 0 10 10" fill="none">
          <path
            d="M1 1L9 9M9 1L1 9"
            stroke="currentColor"
            stroke-width="1.5"
            stroke-linecap="round"
          />
        </svg>
      </button>
    </div>
    <div
      v-if="data?.label"
      class="edge-label nodrag nopan"
      :style="{
        transform: `translate(-50%, -80%) translate(${path[1]}px,${path[2]}px)`,
      }"
    >
      {{ data.label }}
    </div>
  </EdgeLabelRenderer>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import {
  BaseEdge,
  EdgeLabelRenderer,
  getBezierPath,
  getSmoothStepPath,
} from "@vue-flow/core";
import type { EdgeProps } from "@vue-flow/core";
import { useFlowStore } from "../../../stores/flow";
import {
  getPortColor,
  getPortConfig,
  hexToRgba,
} from "../../../config/portTypeRegistry";

interface PortLike {
  id?: string | number | null;
  type?: string | null;
}

interface TypedEdgeData {
  type?: string;
  label?: string;
  sourceType?: string | null;
  targetType?: string | null;
  sourceColor?: string;
  targetColor?: string;
  isDashed?: boolean;
  routeStyle?: "bezier" | "smoothstep";
}

const props = defineProps<EdgeProps<TypedEdgeData>>();
const flowStore = useFlowStore();

const showActions = ref(false);

const routeStyle = computed<"bezier" | "smoothstep">(() => {
  return props.data?.routeStyle === "smoothstep" ? "smoothstep" : "bezier";
});

const path = computed(() => {
  if (routeStyle.value === "smoothstep") {
    return getSmoothStepPath({
      ...props,
      offset: 20,
      borderRadius: 14,
    });
  }

  return getBezierPath(props);
});

const resolvePortType = (
  nodeId: string | null | undefined,
  handleId: string | null | undefined,
  direction: "inputs" | "outputs",
) => {
  if (!nodeId || !handleId) return null;

  const node = flowStore.getNodeById(nodeId);
  const ports = Array.isArray((node?.data as any)?.[direction])
    ? (((node?.data as any)?.[direction] ?? []) as PortLike[])
    : [];
  const port = ports.find((candidate) => `${candidate.id ?? ""}` === handleId);
  return port?.type ?? null;
};

const sourceType = computed(() => {
  if (props.data?.sourceType) return props.data.sourceType;
  const resolvedType = resolvePortType(
    props.source,
    props.sourceHandleId,
    "outputs",
  );
  return resolvedType ?? props.data?.type ?? null;
});

const targetType = computed(() => {
  if (props.data?.targetType) return props.data.targetType;
  const resolvedType = resolvePortType(
    props.target,
    props.targetHandleId,
    "inputs",
  );
  return resolvedType ?? sourceType.value;
});

const sourceColor = computed(() => {
  if (props.data?.sourceColor) return props.data.sourceColor;
  return getPortColor(sourceType.value);
});

const targetColor = computed(() => {
  if (props.data?.targetColor) return props.data.targetColor;
  return getPortColor(targetType.value);
});

const edgeColor = computed(() => sourceColor.value);
const gradientId = computed(
  () => `edge-gradient-${props.id.replace(/[^a-zA-Z0-9_-]/g, "-")}`,
);
const gradientStroke = computed(() => `url(#${gradientId.value})`);

const typeLabel = computed(() => {
  const type = sourceType.value ?? props.data?.type;
  return type ? getPortConfig(type).label : "Unknown";
});

const badgeBgColor = computed(() => hexToRgba(edgeColor.value, 0.12));

const edgeStyle = computed(() => ({
  stroke: gradientStroke.value,
  strokeWidth: props.selected ? 3.5 : 2.2,
  opacity: props.selected ? 1 : 0.88,
  strokeLinecap: "round",
  strokeLinejoin: "round",
  strokeDasharray: props.data?.isDashed ? "6 4" : "none",
  transition: "stroke-width 0.2s ease, opacity 0.2s ease",
}));

const glowStyle = computed(() => ({
  stroke: sourceColor.value,
  strokeWidth: props.selected ? 10 : 6,
  opacity: props.selected ? 0.18 : 0.08,
  fill: "none",
  strokeLinecap: "round" as const,
  filter: "blur(4px)",
  pointerEvents: "none" as const,
  transition: "stroke-width 0.2s ease, opacity 0.2s ease",
}));

const flowParticleStyle = computed(() => ({
  stroke: gradientStroke.value,
  strokeWidth: 2,
  strokeDasharray: "5 14",
  strokeLinecap: "round" as const,
  fill: "none",
  opacity: props.selected ? 0.7 : 0.4,
  pointerEvents: "none" as const,
  animation: "edge-flow 1.1s linear infinite",
}));

const onDeleteEdge = () => {
  flowStore.removeEdge(props.id);
};
</script>

<style scoped>
.typed-edge:hover {
  opacity: 1 !important;
  stroke-width: 3 !important;
}

@keyframes edge-flow {
  from {
    stroke-dashoffset: 0;
  }
  to {
    stroke-dashoffset: -19;
  }
}

.edge-flow-particles {
  mix-blend-mode: screen;
}

.edge-glow-layer {
  mix-blend-mode: normal;
}

.edge-actions-wrapper {
  position: absolute;
  display: flex;
  align-items: center;
  gap: 6px;
  pointer-events: all;
  z-index: 10;
}

.edge-type-badge {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 2px 8px 2px 6px;
  background: var(--badge-bg, rgba(77, 148, 255, 0.1));
  backdrop-filter: blur(10px);
  border: 1px solid color-mix(in srgb, var(--badge-color, #4d94ff) 20%, transparent);
  border-radius: 10px;
  cursor: default;
  transition: all 0.2s ease;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
}

.edge-type-badge:hover {
  background: var(--badge-bg, rgba(77, 148, 255, 0.15));
  box-shadow: 0 3px 12px rgba(0, 0, 0, 0.08);
}

.badge-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  flex-shrink: 0;
}

.badge-text {
  font-size: 10px;
  font-weight: 600;
  color: var(--text-primary, #1c1c1e);
  white-space: nowrap;
  letter-spacing: 0.3px;
}

.edge-delete-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 20px;
  height: 20px;
  border: none;
  border-radius: 50%;
  background: rgba(239, 68, 68, 0.9);
  color: white;
  cursor: pointer;
  box-shadow: 0 2px 8px rgba(239, 68, 68, 0.3);
  transition: all 0.15s ease;
  animation: delete-btn-appear 0.15s ease-out;
}

@keyframes delete-btn-appear {
  from {
    opacity: 0;
    transform: scale(0.5);
  }
  to {
    opacity: 1;
    transform: scale(1);
  }
}

.edge-delete-btn:hover {
  background: rgba(220, 38, 38, 1);
  transform: scale(1.15);
  box-shadow: 0 3px 12px rgba(239, 68, 68, 0.4);
}

.edge-label {
  position: absolute;
  pointer-events: all;
  background: var(--glass-bg, rgba(255, 255, 255, 0.92));
  backdrop-filter: blur(8px);
  border: 1px solid var(--border-glass, rgba(0, 0, 0, 0.05));
  padding: 2px 8px;
  border-radius: 12px;
  font-size: 10px;
  font-weight: 600;
  color: var(--text-primary, #1c1c1e);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.05);
  transition:
    transform 0.2s ease,
    opacity 0.2s ease;
}
</style>
