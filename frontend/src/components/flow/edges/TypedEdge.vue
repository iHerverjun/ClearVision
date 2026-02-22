<template>
  <BaseEdge
    :id="id"
    :path="path[0]"
    :marker-start="markerStart"
    :marker-end="markerEnd"
    :interaction-width="interactionWidth ?? 32"
    :style="edgeStyle"
    :class="['typed-edge', { 'is-selected': selected }]"
  />
  <EdgeLabelRenderer>
    <div
      v-if="data?.label"
      class="edge-label nodrag nopan"
      :style="{
        transform: `translate(-50%, -50%) translate(${path[1]}px,${path[2]}px)`,
      }"
    >
      {{ data.label }}
    </div>
  </EdgeLabelRenderer>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { BaseEdge, EdgeLabelRenderer, getBezierPath } from "@vue-flow/core";
import type { EdgeProps } from "@vue-flow/core";

const props = defineProps<EdgeProps>();

const path = computed(() => getBezierPath(props));

const hexToRgba = (hex: string, alpha: number): string => {
  const normalized = hex.replace("#", "");
  if (!/^[0-9a-fA-F]{6}$/.test(normalized)) {
    return `rgba(77, 148, 255, ${alpha})`;
  }

  const value = parseInt(normalized, 16);
  const r = (value >> 16) & 255;
  const g = (value >> 8) & 255;
  const b = value & 255;
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
};

// Extract color from source port type or data
const edgeColor = computed(() => {
  if (props.data?.color) return props.data.color;

  if (props.data?.type) {
    const mapping: Record<string, string> = {
      Image: "#4D94FF", // Blue
      Integer: "#00E676", // Green
      Float: "#FFA726", // Orange
      Boolean: "#FF4D4D", // Red
      String: "#BA68C8", // Purple
      Point: "#00BCD4", // Cyan
    };
    return mapping[props.data.type] || "#9CA3AF"; // Default Gray
  }

  // Fallback to elegant red/gray if no type is provided
  return "#FF4D4D";
});

const edgeGlowColor = computed(() =>
  hexToRgba(edgeColor.value, props.selected ? 0.45 : 0.2),
);

const edgeStyle = computed(() => ({
  stroke: edgeColor.value,
  strokeWidth: props.selected ? 3.8 : 2.4,
  opacity: props.selected ? 1 : 0.82,
  strokeLinecap: "round",
  strokeLinejoin: "round",
  strokeDasharray: props.data?.isDashed ? "5 5" : "none",
  filter: `drop-shadow(0 0 4px ${edgeGlowColor.value})`,
  transition: "stroke-width 0.18s ease, opacity 0.18s ease, filter 0.18s ease",
}));
</script>

<style scoped>
.typed-edge {
  cursor: pointer;
}

.typed-edge:hover {
  opacity: 1 !important;
  stroke-width: 3 !important;
}

.edge-label {
  position: absolute;
  pointer-events: all;
  background: var(--glass-bg, rgba(255, 255, 255, 0.9));
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
