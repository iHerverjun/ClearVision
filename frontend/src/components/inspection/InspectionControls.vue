<template>
  <aside class="w-72 bg-[var(--color-surface)] border-r border-[var(--color-border)] flex flex-col z-10 shadow-sm flex-shrink-0">
    <div class="p-5 border-b border-[var(--color-border)]">
      <h2 class="text-sm font-bold text-[var(--color-text)] uppercase tracking-wider">控制面板</h2>
    </div>
    <div class="flex-1 overflow-y-auto p-4 space-y-6">
      <div>
        <h3 class="text-xs font-semibold text-[var(--color-text-muted)] uppercase tracking-wide mb-3">执行</h3>
        <div class="space-y-3">
          <button @click="handleSingleRun" class="w-full flex items-center justify-center space-x-2 bg-[var(--color-surface)] border border-[var(--color-border)] hover:border-red-500 text-[var(--color-text)] py-2.5 px-4 rounded-lg transition-colors font-medium text-sm shadow-sm group">
            <PlayIcon class="w-4 h-4 text-red-500 group-hover:scale-110 transition-transform" />
            <span>单次运行</span>
          </button>
          <button
            class="w-full flex items-center justify-center space-x-2 py-2.5 px-4 rounded-lg shadow-md transition-colors font-medium text-sm"
            :class="executionStore.isContinuousMode ? 'bg-red-600 text-white hover:bg-red-700' : 'bg-red-500 text-white hover:bg-red-600'"
            @click="handleContinuousRun"
          >
            <RepeatIcon class="w-4 h-4" :class="executionStore.isContinuousMode ? '' : 'animate-pulse'" />
            <span>{{ executionStore.isContinuousMode ? '停止连续' : '连续运行' }}</span>
          </button>
          <button @click="handleStop" class="w-full flex items-center justify-center space-x-2 bg-[var(--color-surface)] border border-[var(--color-border)] hover:bg-red-50 dark:hover:bg-red-900/20 hover:border-red-200 hover:text-red-600 dark:hover:text-red-400 text-[var(--color-text)] py-2.5 px-4 rounded-lg transition-colors font-medium text-sm shadow-sm">
            <SquareIcon class="w-4 h-4" />
            <span>停止</span>
          </button>
        </div>
      </div>
      <div>
        <h3 class="text-xs font-semibold text-[var(--color-text-muted)] uppercase tracking-wide mb-3">统计</h3>
        <div class="grid grid-cols-2 gap-3">
          <div class="bg-[var(--color-background)] p-3 rounded-xl border border-[var(--color-border)] flex flex-col items-center">
            <span class="text-xs text-[var(--color-text-muted)] uppercase font-medium">合格</span>
            <span class="text-xl font-bold text-green-500 mt-1">{{ stats.okCount }}</span>
          </div>
          <div class="bg-red-50 dark:bg-red-900/20 p-3 rounded-xl border border-red-500/30 shadow-[0_0_15px_rgba(239,68,68,0.25)] flex flex-col items-center">
            <span class="text-xs text-red-500 uppercase font-medium">不合格</span>
            <span class="text-xl font-bold text-red-500 mt-1">{{ stats.ngCount }}</span>
          </div>
          <div class="bg-[var(--color-background)] p-3 rounded-xl border border-[var(--color-border)] flex flex-col items-center col-span-2">
            <div class="flex justify-between w-full mb-1">
              <span class="text-xs text-[var(--color-text-muted)] uppercase font-medium">总数</span>
              <span class="text-xs font-bold text-[var(--color-text)] relative">{{ stats.totalCount }}</span>
            </div>
            <div class="w-full bg-gray-200 dark:bg-gray-700 rounded-full h-1.5 overflow-hidden">
              <div class="bg-green-500 h-1.5 rounded-full" :style="`width: ${stats.yieldRate}%`"></div>
            </div>
          </div>
          <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] flex flex-col items-center justify-center col-span-2">
            <span class="text-xs text-[var(--color-text-muted)] uppercase font-medium mb-1">良率</span>
            <span class="text-3xl font-bold text-[var(--color-text)]">{{ stats.yieldRate }}<span class="text-sm font-normal text-[var(--color-text-muted)] ml-1">%</span></span>
          </div>
        </div>
        <div class="mt-4 flex items-center justify-center">
          <button
            class="text-xs text-[var(--color-text-muted)] hover:text-red-500 flex items-center space-x-1 transition-colors"
            @click="executionStore.resetCounters"
          >
            <RotateCcwIcon class="w-3.5 h-3.5" />
            <span>重置计数</span>
          </button>
        </div>
      </div>

      <!-- Tools & Calibration -->
      <div>
        <h3 class="text-xs font-semibold text-[var(--color-text-muted)] uppercase tracking-wide mb-3">标定与工具</h3>
        <div class="space-y-3">
          <button @click="isCalibWizardOpen = true" class="w-full flex items-center justify-center space-x-2 bg-[var(--color-surface)] border border-[var(--color-border)] hover:bg-gray-50 dark:hover:bg-gray-800 text-[var(--color-text)] py-2.5 px-4 rounded-lg transition-colors font-medium text-sm shadow-sm">
            <ScanIcon class="w-4 h-4 text-gray-500" />
            <span>相机标定</span>
          </button>
          <button @click="isHandEyeWizardOpen = true" class="w-full flex items-center justify-center space-x-2 bg-[var(--color-surface)] border border-[var(--color-border)] hover:bg-gray-50 dark:hover:bg-gray-800 text-[var(--color-text)] py-2.5 px-4 rounded-lg transition-colors font-medium text-sm shadow-sm">
            <CombineIcon class="w-4 h-4 text-gray-500" />
            <span>手眼标定</span>
          </button>
        </div>
      </div>
    </div>

    <!-- Modals -->
    <CalibrationWizard :is-open="isCalibWizardOpen" @close="isCalibWizardOpen = false" />
    <HandEyeCalibWizard :is-open="isHandEyeWizardOpen" @close="isHandEyeWizardOpen = false" />
  </aside>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue';
import { 
  PlayIcon, 
  RepeatIcon, 
  SquareIcon, 
  RotateCcwIcon,
  ScanIcon,
  CombineIcon
} from 'lucide-vue-next';

import { useExecutionStore } from '../../stores/execution';

// Modals
import CalibrationWizard from '../settings/CalibrationWizard.vue';
import HandEyeCalibWizard from '../settings/HandEyeCalibWizard.vue';

const executionStore = useExecutionStore();

const isCalibWizardOpen = ref(false);
const isHandEyeWizardOpen = ref(false);

const handleSingleRun = () => {
  executionStore.startExecution();
};

const handleStop = () => {
  executionStore.stopContinuousRun();
};

const handleContinuousRun = () => {
  if (executionStore.isContinuousMode) {
    executionStore.stopContinuousRun();
  } else {
    executionStore.startContinuousRun();
  }
};

const stats = computed(() => {
  return {
    okCount: executionStore.okCount.toLocaleString(),
    ngCount: executionStore.ngCount.toLocaleString(),
    totalCount: executionStore.totalCount.toLocaleString(),
    yieldRate: executionStore.yieldRate,
  };
});
</script>

<style scoped>
/* Scoped styles if needed */
</style>
