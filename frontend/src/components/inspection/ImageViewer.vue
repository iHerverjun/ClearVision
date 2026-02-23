<template>
  <section class="flex-1 relative bg-gray-900 overflow-hidden flex flex-col">
    <div class="absolute inset-0 z-0 pointer-events-none" :class="{ 'vignette-red': isNg }"></div>

    <div
      class="absolute top-6 left-6 flex flex-col bg-white/10 backdrop-blur-md rounded-lg shadow-[0_10px_15px_-3px_rgba(0,0,0,0.1),0_4px_6px_-2px_rgba(0,0,0,0.05)] border border-white/10 z-10"
    >
      <button
        class="p-2.5 text-white/70 hover:text-white hover:bg-white/10 rounded-t-lg border-b border-white/10 transition-colors"
        @click="zoomIn"
      >
        <PlusIcon class="w-4 h-4" />
      </button>
      <button
        class="p-2.5 text-white/70 hover:text-white hover:bg-white/10 border-b border-white/10 transition-colors"
        @click="zoomOut"
      >
        <MinusIcon class="w-4 h-4" />
      </button>
      <button
        class="p-2.5 text-white/70 hover:text-white hover:bg-white/10 border-b border-white/10 transition-colors"
        @click="fitToScreen"
      >
        <FocusIcon class="w-4 h-4" />
      </button>
      <button
        class="p-2.5 text-white/70 hover:text-white hover:bg-white/10 rounded-b-lg transition-colors"
        @click="toggleLock"
      >
        <LockIcon v-if="isLocked" class="w-4 h-4" />
        <LockOpenIcon v-else class="w-4 h-4" />
      </button>
    </div>

    <div class="flex-1 flex items-center justify-center relative overflow-hidden">
      <div
        class="relative z-0 flex items-center justify-center transition-transform duration-150"
        :style="{ transform: `scale(${zoom})` }"
      >
        <img
          v-if="currentImage"
          alt="检测图像"
          class="max-w-full max-h-full object-contain opacity-90 shadow-2xl"
          :src="currentImage"
        />
        <div v-else class="flex flex-col items-center justify-center text-gray-400">
          <ImageOffIcon class="w-16 h-16" />
          <span class="text-sm mt-3">暂无图像数据</span>
        </div>
      </div>

      <div
        v-if="isNg && currentImage"
        class="absolute top-1/2 left-1/2 transform -translate-x-1/2 -translate-y-1/2 w-48 h-48 border-4 border-red-500 shadow-[0_0_20px_rgba(239,68,68,0.6)] flex flex-col items-start z-10"
      >
        <div class="bg-red-500 text-white text-xs font-bold px-3 py-1.5 flex items-center space-x-1.5 shadow-md animate-pulse">
          <AlertCircleIcon class="w-3.5 h-3.5" />
          <span>不合格：检测到缺陷</span>
        </div>
        <div class="absolute -top-1.5 -left-1.5 w-4 h-4 border-t-4 border-l-4 border-red-500"></div>
        <div class="absolute -top-1.5 -right-1.5 w-4 h-4 border-t-4 border-r-4 border-red-500"></div>
        <div class="absolute -bottom-1.5 -left-1.5 w-4 h-4 border-b-4 border-l-4 border-red-500"></div>
        <div class="absolute -bottom-1.5 -right-1.5 w-4 h-4 border-b-4 border-r-4 border-red-500"></div>
      </div>

      <div
        class="absolute bottom-6 right-6 backdrop-blur text-white px-4 py-2 rounded-lg text-xs font-mono border z-10"
        :class="isNg ? 'bg-red-900/80 border-red-500/30' : 'bg-gray-900/80 border-white/10'"
      >
        <div class="flex items-center space-x-4">
          <span class="font-bold" :class="isNg ? 'text-red-100' : 'text-gray-200'">
            {{ cameraName }} {{ isNg ? '[报警]' : '' }}
          </span>
          <span class="text-white/50">|</span>
          <span>{{ resolutionText }}</span>
          <span class="text-white/50">|</span>
          <span>{{ cycleTimeText }}</span>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import {
  PlusIcon,
  MinusIcon,
  FocusIcon,
  LockOpenIcon,
  LockIcon,
  AlertCircleIcon,
  ImageOffIcon,
} from 'lucide-vue-next';
import { useExecutionStore } from '../../stores/execution';

const executionStore = useExecutionStore();

const zoom = ref(1);
const isLocked = ref(false);

const isNg = computed(() => executionStore.lastInspectionResult === 'NG');
const currentImage = computed(() => executionStore.latestCameraImage);
const cameraName = computed(() => executionStore.latestCameraId || '相机');
const resolutionText = computed(() => {
  if (!executionStore.latestCameraMeta) {
    return '--';
  }
  return `${executionStore.latestCameraMeta.width}x${executionStore.latestCameraMeta.height}`;
});
const cycleTimeText = computed(() =>
  executionStore.cycleTimeMs > 0 ? `${Math.round(executionStore.cycleTimeMs)}ms` : '--',
);

const zoomIn = () => {
  if (isLocked.value) return;
  zoom.value = Math.min(zoom.value + 0.1, 4);
};

const zoomOut = () => {
  if (isLocked.value) return;
  zoom.value = Math.max(zoom.value - 0.1, 0.2);
};

const fitToScreen = () => {
  if (isLocked.value) return;
  zoom.value = 1;
};

const toggleLock = () => {
  isLocked.value = !isLocked.value;
};
</script>

<style scoped>
.vignette-red {
  background: radial-gradient(circle, transparent 50%, rgba(239, 68, 68, 0.15) 100%);
}
</style>
