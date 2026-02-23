<template>
  <div
    class="reroute-node"
    :class="{ 'is-selected': selected }"
    :style="{
      '--reroute-color': color,
      '--reroute-glow': glow,
    }"
    :title="`Reroute · ${label}`"
  >
    <Handle
      id="in"
      type="target"
      :position="LEFT_POSITION"
      class="reroute-handle reroute-handle-in"
      :connectable="true"
      :connectable-start="true"
      :connectable-end="true"
    />

    <div class="reroute-dot" aria-hidden="true" />

    <Handle
      id="out"
      type="source"
      :position="RIGHT_POSITION"
      class="reroute-handle reroute-handle-out"
      :connectable="true"
      :connectable-start="true"
      :connectable-end="true"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, type PropType } from 'vue';
import { Handle, type Position } from '@vue-flow/core';
import {
  getPortColor,
  getPortConfig,
  getPortGlowColor,
} from '../../../config/portTypeRegistry';

const LEFT_POSITION = 'left' as Position;
const RIGHT_POSITION = 'right' as Position;

const props = defineProps({
  selected: { type: Boolean, default: false },
  data: {
    type: Object as PropType<{
      rerouteType?: string | null;
      inputs?: Array<{ type?: string | null }>;
      outputs?: Array<{ type?: string | null }>;
    }>,
    default: () => ({}),
  },
});

const rerouteType = computed(() => {
  return (
    props.data?.rerouteType ??
    props.data?.outputs?.[0]?.type ??
    props.data?.inputs?.[0]?.type ??
    'Any'
  );
});

const color = computed(() => getPortColor(rerouteType.value));
const glow = computed(() => getPortGlowColor(rerouteType.value, 0.5));
const label = computed(() => getPortConfig(rerouteType.value).label);
</script>

<style scoped>
.reroute-node {
  width: 16px;
  height: 16px;
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  pointer-events: auto;
}

.reroute-dot {
  width: 16px;
  height: 16px;
  border-radius: 50%;
  background: var(--reroute-color, #4d94ff);
  border: 2px solid rgba(255, 255, 255, 0.92);
  box-shadow:
    0 0 0 1px rgba(15, 23, 42, 0.18),
    0 0 8px var(--reroute-glow, rgba(77, 148, 255, 0.45));
  transition:
    transform 0.16s ease,
    box-shadow 0.16s ease;
}

.reroute-node:hover .reroute-dot {
  transform: scale(1.12);
  box-shadow:
    0 0 0 1px rgba(15, 23, 42, 0.2),
    0 0 12px var(--reroute-glow, rgba(77, 148, 255, 0.5));
}

.reroute-node.is-selected .reroute-dot {
  box-shadow:
    0 0 0 1px rgba(15, 23, 42, 0.24),
    0 0 14px var(--reroute-glow, rgba(77, 148, 255, 0.65));
}

:deep(.vue-flow__handle.reroute-handle) {
  width: 32px !important;
  height: 32px !important;
  min-width: 32px !important;
  min-height: 32px !important;
  border: none !important;
  background: transparent !important;
  box-shadow: none !important;
  opacity: 1 !important;
  cursor: pointer;
}

:deep(.vue-flow__handle.reroute-handle-in) {
  left: -18px !important;
  right: auto !important;
}

:deep(.vue-flow__handle.reroute-handle-out) {
  right: -18px !important;
  left: auto !important;
}
</style>
