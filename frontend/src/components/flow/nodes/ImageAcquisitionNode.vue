<template>
  <OperatorNode :id="id" :data="data" :selected="selected" class="camera-node">
    <div class="camera-config">
      <label class="control-label">图像来源</label>
      <select class="modern-select" v-model="configProxy.sourceType">
        <option value="camera">-- 连接本地相机 --</option>
        <option value="file">-- 加载图像序列 --</option>
      </select>
    </div>
  </OperatorNode>
</template>

<script setup lang="ts">
import { computed } from "vue";
import type { PropType } from "vue";
import OperatorNode from "./OperatorNode.vue";

const props = defineProps({
  id: { type: String, required: true },
  data: {
    type: Object as PropType<{
      legacyConfig?: Record<string, any>;
      [key: string]: any;
    }>,
    required: true,
  },
  selected: { type: Boolean, default: false },
});

// A computed proxy that safely touches legacyConfig
// In a perfectly reactive store this writes directly to the object ref.
const configProxy = computed<Record<string, any>>({
  get: () => props.data.legacyConfig || {},
  set: (val) => {
    if (props.data) {
      props.data.legacyConfig = val;
    }
  },
});
</script>

<style scoped>
.camera-node {
  /* Extended width for select dropdown */
  min-width: 260px;
}

.camera-config {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.control-label {
  font-size: 11px;
  font-weight: 600;
  color: var(--text-muted, #64748b);
  text-transform: uppercase;
}

.modern-select {
  width: 100%;
  height: 32px;
  background: rgba(255, 255, 255, 0.5);
  border: 1px solid var(--border-glass, rgba(0, 0, 0, 0.1));
  border-radius: 8px;
  padding: 0 10px;
  font-family: inherit;
  font-size: 12px;
  color: var(--text-primary, #1c1c1e);
  outline: none;
  transition:
    border-color 0.2s ease,
    box-shadow 0.2s ease;
}

.modern-select:focus {
  border-color: var(--accent-red, #ff4d4d);
  background: #ffffff;
  box-shadow: 0 0 0 2px rgba(255, 77, 77, 0.1);
}
</style>
