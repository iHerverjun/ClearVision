<template>
  <aside class="w-80 bg-[var(--color-surface)] border-l border-[var(--color-border)] flex flex-col z-10 shadow-sm flex-shrink-0">
    <div class="p-4 border-b border-[var(--color-border)] bg-[var(--color-background)]">
      <h2 class="text-xs font-bold text-[var(--color-text-muted)] uppercase tracking-wider flex items-center">
        <BarChart2Icon class="w-4 h-4 mr-2" />
        INSPECTION DETAILS (详情)
      </h2>
    </div>
    <div class="flex-1 overflow-y-auto p-5">
      <div class="flex items-center justify-between mb-6">
        <div>
          <div class="text-2xl font-bold text-[var(--color-text)]">{{ inspectionData.batchId }}</div>
          <div class="text-xs text-[var(--color-text-muted)] font-mono">SN: {{ inspectionData.serialNumber }}</div>
        </div>
        <div class="flex flex-col items-end">
          <span 
            class="px-3 py-1 rounded-full text-sm font-bold border"
            :class="inspectionData.status === 'OK' 
              ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400 border-green-200 dark:border-green-800' 
              : 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400 border-red-200 dark:border-red-800'"
          >
            {{ inspectionData.status }}
          </span>
        </div>
      </div>
      <div class="grid grid-cols-2 gap-4 mb-6">
        <div class="bg-[var(--color-background)] p-3 rounded-lg border border-[var(--color-border)]">
          <div class="text-[10px] text-[var(--color-text-muted)] uppercase tracking-wide mb-1">Score</div>
          <div class="text-lg font-semibold text-[var(--color-text)]">{{ inspectionData.score }}%</div>
        </div>
        <div class="bg-[var(--color-background)] p-3 rounded-lg border border-[var(--color-border)]">
          <div class="text-[10px] text-[var(--color-text-muted)] uppercase tracking-wide mb-1">Time</div>
          <div class="text-lg font-semibold text-[var(--color-text)]">{{ inspectionData.timeMs }}ms</div>
        </div>
      </div>
      <div class="space-y-4">
        <h3 class="text-xs font-semibold text-[var(--color-text)] border-b border-[var(--color-border)] pb-2">Measurement Data</h3>
        
        <div v-for="metric in inspectionData.metrics" :key="metric.label" class="flex justify-between items-center py-1">
          <span class="text-xs text-[var(--color-text-muted)]">{{ metric.label }}</span>
          <span class="text-xs font-mono" :class="metric.isAlert ? 'text-red-500 font-medium' : 'text-[var(--color-text)]'">
            {{ metric.value }}
          </span>
        </div>

        <div v-if="inspectionData.ocrText" class="mt-4 pt-4 border-t border-[var(--color-border)]">
          <h3 class="text-xs font-semibold text-[var(--color-text)] mb-2">OCR Text</h3>
          <div class="bg-[var(--color-background)] p-2 rounded text-xs font-mono text-[var(--color-text)] break-all border border-[var(--color-border)]">
            {{ inspectionData.ocrText }}
          </div>
        </div>
      </div>
      <div class="mt-8 space-y-2">
        <button class="w-full border border-[var(--color-border)] text-[var(--color-text)] hover:bg-[var(--color-background)] text-xs font-medium py-2 px-4 rounded transition-colors flex items-center justify-center">
          <FlagIcon class="w-4 h-4 mr-2" /> 标记为误报 (False Positive)
        </button>
        <button class="w-full bg-[var(--color-surface)] border border-[var(--color-border)] text-[var(--color-text)] hover:text-red-500 hover:border-red-500 text-xs font-medium py-2 px-4 rounded transition-colors flex items-center justify-center">
          <Share2Icon class="w-4 h-4 mr-2" /> 导出报告
        </button>
      </div>
    </div>
  </aside>
</template>

<script setup lang="ts">
import { reactive } from 'vue';
import { 
  BarChart2Icon, 
  FlagIcon, 
  Share2Icon 
} from 'lucide-vue-next';

interface Metric {
  label: string;
  value: string;
  isAlert?: boolean;
}

interface InspectionData {
  batchId: string;
  serialNumber: string;
  status: 'OK' | 'NG';
  score: number;
  timeMs: number;
  metrics: Metric[];
  ocrText?: string;
}

const inspectionData = reactive<InspectionData>({
  batchId: 'Batch_A01',
  serialNumber: '20231027-042',
  status: 'NG',
  score: 87.5,
  timeMs: 45,
  metrics: [
    { label: 'Defect Type', value: 'Surface Scratch', isAlert: true },
    { label: 'Coordinates (X, Y)', value: '1240, 856' },
    { label: 'Area Size', value: '45 px²' },
    { label: 'Confidence', value: '0.92' }
  ],
  ocrText: 'L88-421-B'
});
</script>
