<template>
  <aside class="w-80 bg-[var(--color-surface)] border-l border-[var(--color-border)] flex flex-col z-10 shadow-sm flex-shrink-0">
    <div class="p-5 border-b border-[var(--color-border)]">
      <h2 class="text-sm font-bold text-[var(--color-text)] uppercase tracking-wider">分析</h2>
    </div>
    <div class="flex-1 overflow-y-auto bg-gray-50/50 dark:bg-gray-900/50">
      <div class="p-4 space-y-6">
        
        <div v-if="!distanceMeasurement && !ocrResult && recentHistory.length === 0" class="text-center text-sm text-[var(--color-text-muted)] py-8">
          等待执行数据...
        </div>

        <!-- Dynamic Measurement Card -->
        <div v-if="distanceMeasurement" :class="[
          'bg-[var(--color-surface)] rounded-xl shadow-md border overflow-hidden ring-1',
          distanceMeasurement.isOk ? 'border-[var(--color-border)] border-l-[3px] border-l-green-500 ring-green-100 dark:ring-green-900/20' : 'border-[var(--color-border)] border-l-[3px] border-l-red-500 ring-red-100 dark:ring-red-900/20'
        ]">
          <div class="p-3 border-b border-[var(--color-border)] flex items-center justify-between cursor-pointer hover:bg-gray-50 dark:hover:bg-gray-700/50 transition-colors">
            <div class="flex items-center space-x-2">
              <RulerIcon :class="['w-4 h-4', distanceMeasurement.isOk ? 'text-green-500' : 'text-red-500']" />
              <h3 class="text-sm font-bold text-[var(--color-text)]">距离</h3>
            </div>
            <div class="flex items-center space-x-2">
              <span v-if="distanceMeasurement.isOk" class="px-1.5 py-0.5 rounded text-[10px] font-mono bg-green-100 dark:bg-green-900/30 text-green-600 dark:text-green-400 font-bold">合格</span>
              <span v-else class="px-1.5 py-0.5 rounded text-[10px] font-mono bg-red-100 dark:bg-red-900/30 text-red-600 dark:text-red-400 font-bold">不合格</span>
              <ChevronUpIcon :class="['w-4 h-4', distanceMeasurement.isOk ? 'text-green-500' : 'text-red-500']" />
            </div>
          </div>
          <div class="p-4">
            <div class="flex justify-center mb-2">
              <span class="text-4xl font-bold tracking-tight" :class="distanceMeasurement.isOk ? 'text-green-500 dark:text-green-400' : 'text-red-500 dark:text-red-400'">
                {{ distanceMeasurement.value }} <span class="text-base font-normal ml-1">{{ distanceMeasurement.unit }}</span>
              </span>
            </div>
          </div>
        </div>

        <!-- Dynamic OCR Card -->
        <div v-if="ocrResult" :class="[
          'bg-[var(--color-surface)] rounded-xl shadow-md border border-[var(--color-border)] border-l-4 overflow-hidden',
          ocrResult.isOk ? 'border-l-green-500' : 'border-l-red-500'
        ]">
          <div class="p-3 border-b border-[var(--color-border)] flex items-center justify-between cursor-pointer hover:bg-gray-50 dark:hover:bg-gray-700/50 transition-colors">
            <div class="flex items-center space-x-2">
              <TypeIcon :class="['w-4 h-4', ocrResult.isOk ? 'text-green-500' : 'text-red-500']" />
              <h3 class="text-sm font-bold text-[var(--color-text)]">OCR 文本</h3>
            </div>
            <div class="flex items-center space-x-2">
              <span class="px-1.5 py-0.5 rounded text-[10px] font-mono bg-[var(--color-background)] text-[var(--color-text-muted)]">{{ ocrResult.time }}</span>
              <ChevronUpIcon :class="['w-4 h-4', ocrResult.isOk ? 'text-green-500' : 'text-red-500']" />
            </div>
          </div>
          <div class="p-4 space-y-4">
            <div>
              <div class="text-[10px] text-[var(--color-text-muted)] uppercase tracking-wider mb-1.5">结果</div>
              <div class="bg-[var(--color-background)] rounded-lg p-3 border border-[var(--color-border)] flex items-center justify-between">
                <span class="font-mono text-lg font-bold text-[var(--color-text)] tracking-wide">{{ ocrResult.text }}</span>
                <CheckCircleIcon v-if="ocrResult.isOk" class="text-green-500 w-4 h-4" />
                <AlertCircleIcon v-else class="text-red-500 w-4 h-4" />
              </div>
            </div>
            <div>
              <div class="flex items-center justify-between mb-1.5">
                <span class="text-[10px] text-[var(--color-text-muted)] uppercase tracking-wider">置信度</span>
                <span class="text-xs font-bold" :class="ocrResult.isOk ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'">{{ ocrResult.confidence }}%</span>
              </div>
              <div class="w-full bg-gray-200 dark:bg-gray-700 rounded-full h-2 overflow-hidden">
                <div :class="['h-full rounded-full', ocrResult.isOk ? 'bg-green-500' : 'bg-red-500']" :style="'width: ' + ocrResult.confidence + '%'"></div>
              </div>
            </div>
          </div>
        </div>

        <!-- Dynamic History List -->
        <div v-if="recentHistory.length > 0">
          <div class="flex items-center justify-between mb-3 px-1">
            <h3 class="text-xs font-bold text-[var(--color-text-muted)] uppercase tracking-wide">最近执行</h3>
            <button
              class="text-xs text-[var(--color-text-muted)] hover:text-red-500 font-medium"
              @click="clearHistory"
            >
              清空
            </button>
          </div>
          <div class="space-y-2">
            <div v-for="node in recentHistory" :key="node.id" :class="[
              'p-3 rounded-lg border shadow-sm flex items-center justify-between group cursor-pointer hover:shadow-md transition-all',
              node.isOk ? 'bg-[var(--color-surface)] border-[var(--color-border)] opacity-90' : 'bg-red-50 dark:bg-red-900/10 border-red-200 dark:border-red-800'
            ]">
              <div class="flex items-center space-x-3">
                <div :class="[
                  'w-8 h-8 rounded-lg flex items-center justify-center font-bold text-xs',
                  node.isOk ? 'bg-green-100 dark:bg-green-900/30 text-green-600' : 'bg-red-500 text-white animate-pulse'
                ]">
                  {{ node.isOk ? '合格' : '不合格' }}
                </div>
                <div class="flex flex-col">
                  <span :class="['text-sm font-semibold', node.isOk ? 'text-[var(--color-text)]' : 'text-red-700 dark:text-red-300']">{{ node.summary }}</span>
                  <span class="text-[10px] text-[var(--color-text-muted)] font-mono">节点：{{ node.id }}</span>
                </div>
              </div>
              <div class="flex flex-col items-end">
                <span class="text-[10px]" :class="node.isOk ? 'text-[var(--color-text-muted)]' : 'text-red-400 font-medium'">{{ node.time }}</span>
                <span class="text-[10px] font-medium" :class="node.isOk ? 'text-green-500' : 'text-red-600 font-bold'">状态：{{ node.status }}</span>
              </div>
            </div>
          </div>
        </div>

      </div>
    </div>
  </aside>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { 
  RulerIcon, 
  ChevronUpIcon, 
  TypeIcon, 
  CheckCircleIcon,
  AlertCircleIcon
} from 'lucide-vue-next';
import { useExecutionStore } from '../../stores/execution';

const executionStore = useExecutionStore();

// Safely extract potential distance measurement data from the node outputs
const distanceMeasurement = computed(() => {
  for (const state of executionStore.nodeStates.values()) {
    if (state.outputData && 'Distance' in state.outputData) {
      const distance = Number(state.outputData.Distance);
      const isOk = state.status === 'success';
      return { value: distance.toFixed(2), isOk, unit: 'mm' };
    }
  }
  return null; // Return null if no measurement node has run yet
});

// Safely extract potential OCR data from the node outputs
const ocrResult = computed(() => {
  for (const state of executionStore.nodeStates.values()) {
    if (state.outputData && 'Text' in state.outputData) {
      return { 
        text: String(state.outputData.Text), 
        confidence: Number(state.outputData.Confidence || 0).toFixed(1),
        isOk: state.status === 'success',
        time: state.endTime && state.startTime ? `${state.endTime - state.startTime}ms` : '无'
      };
    }
  }
  return null;
});

// A pseudo-history list using recent node states as a proxy for "history" in this view
const recentHistory = computed(() => {
  return Array.from(executionStore.nodeStates.values())
    .filter(n => n.endTime)
    .sort((a, b) => (b.endTime || 0) - (a.endTime || 0))
    .slice(0, 5)
    .map(n => ({
      id: n.nodeId.substring(0, 6) + '...',
      status:
        n.status === 'success'
          ? '成功'
          : n.status === 'error'
            ? '失败'
            : n.status === 'running'
              ? '执行中'
              : '空闲',
      isOk: n.status === 'success',
      time: n.endTime ? new Date(n.endTime).toLocaleTimeString() : '',
      summary: n.errorMessage ? '异常' : (n.outputData ? '已生成数据' : '执行通过')
    }));
});

const clearHistory = () => {
  executionStore.resetAllNodeStates();
};
</script>

<style scoped>
/* Scoped overrides if necessary */
</style>
