<template>
  <aside class="w-80 bg-[var(--color-surface)] border-l border-[var(--color-border)] flex flex-col z-10 shadow-sm flex-shrink-0">
    <div class="p-4 border-b border-[var(--color-border)] bg-[var(--color-background)]">
      <h2 class="text-xs font-bold text-[var(--color-text-muted)] uppercase tracking-wider flex items-center">
        <BarChart2Icon class="w-4 h-4 mr-2" />
        检测详情
      </h2>
    </div>

    <div v-if="record" class="flex-1 overflow-y-auto p-5">
      <div class="flex items-center justify-between mb-6">
        <div>
          <div class="text-2xl font-bold text-[var(--color-text)]">{{ detail.batchId }}</div>
          <div class="text-xs text-[var(--color-text-muted)] font-mono">序列号：{{ detail.serialNumber }}</div>
        </div>
        <div class="flex flex-col items-end">
          <span
            class="px-3 py-1 rounded-full text-sm font-bold border"
            :class="record.status === 'OK'
              ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400 border-green-200 dark:border-green-800'
              : record.status === 'NG'
                ? 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400 border-red-200 dark:border-red-800'
                : 'bg-gray-100 text-gray-700 border-gray-200'"
          >
            {{ getStatusLabel(record.status) }}
          </span>
        </div>
      </div>

      <div class="grid grid-cols-2 gap-4 mb-6">
        <div class="bg-[var(--color-background)] p-3 rounded-lg border border-[var(--color-border)]">
          <div class="text-[10px] text-[var(--color-text-muted)] uppercase tracking-wide mb-1">得分</div>
          <div class="text-lg font-semibold text-[var(--color-text)]">{{ detail.score }}%</div>
        </div>
        <div class="bg-[var(--color-background)] p-3 rounded-lg border border-[var(--color-border)]">
          <div class="text-[10px] text-[var(--color-text-muted)] uppercase tracking-wide mb-1">耗时</div>
          <div class="text-lg font-semibold text-[var(--color-text)]">{{ Math.round(record.processingTimeMs) }}ms</div>
        </div>
      </div>

      <div class="space-y-4">
        <h3 class="text-xs font-semibold text-[var(--color-text)] border-b border-[var(--color-border)] pb-2">测量数据</h3>
        <div v-for="metric in detail.metrics" :key="metric.label" class="flex justify-between items-center py-1">
          <span class="text-xs text-[var(--color-text-muted)]">{{ metric.label }}</span>
          <span class="text-xs font-mono" :class="metric.isAlert ? 'text-red-500 font-medium' : 'text-[var(--color-text)]'">
            {{ metric.value }}
          </span>
        </div>

        <div v-if="detail.ocrText" class="mt-4 pt-4 border-t border-[var(--color-border)]">
          <h3 class="text-xs font-semibold text-[var(--color-text)] mb-2">OCR 文本</h3>
          <div class="bg-[var(--color-background)] p-2 rounded text-xs font-mono text-[var(--color-text)] break-all border border-[var(--color-border)]">
            {{ detail.ocrText }}
          </div>
        </div>
      </div>

      <div class="mt-8 space-y-2">
        <button
          class="w-full border border-[var(--color-border)] text-[var(--color-text)] hover:bg-[var(--color-background)] text-xs font-medium py-2 px-4 rounded transition-colors flex items-center justify-center"
          @click="resultsStore.markFalsePositive(record.id)"
        >
          <FlagIcon class="w-4 h-4 mr-2" />
          标记误报
        </button>
        <button
          class="w-full bg-[var(--color-surface)] border border-[var(--color-border)] text-[var(--color-text)] hover:text-red-500 hover:border-red-500 text-xs font-medium py-2 px-4 rounded transition-colors flex items-center justify-center"
          @click="resultsStore.exportReport(record.id)"
        >
          <Share2Icon class="w-4 h-4 mr-2" />
          导出报告
        </button>
      </div>
    </div>

    <div v-else class="flex-1 p-5 text-xs text-[var(--color-text-muted)]">
      请选择一条记录查看详情。
    </div>
  </aside>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { BarChart2Icon, FlagIcon, Share2Icon } from 'lucide-vue-next';
import { useResultsStore } from '../../stores/results';

const resultsStore = useResultsStore();

const record = computed(() => resultsStore.selectedRecord);

const getStatusLabel = (status: string) => {
  switch (status) {
    case 'OK':
      return '合格';
    case 'NG':
      return '不合格';
    default:
      return '错误';
  }
};

const detail = computed(() => {
  const selected = record.value;
  if (!selected) {
    return {
      batchId: '--',
      serialNumber: '--',
      score: 0,
      metrics: [] as Array<{ label: string; value: string; isAlert?: boolean }>,
      ocrText: '',
    };
  }

  const outputData = selected.outputData || {};
  const metrics: Array<{ label: string; value: string; isAlert?: boolean }> = [
    { label: '缺陷数量', value: String(selected.defects.length), isAlert: selected.defects.length > 0 },
    { label: '结果 ID', value: selected.id },
    { label: '工程 ID', value: selected.projectId },
    { label: '误报标记', value: outputData.falsePositive ? '是' : '否' },
  ];

  if (selected.defects[0]) {
    metrics.unshift({ label: '缺陷类型', value: selected.defects[0].type, isAlert: true });
  }

  return {
    batchId: String(outputData.batchId || selected.projectId || '--'),
    serialNumber: String(outputData.serialNumber || selected.id),
    score: Number(outputData.score || (selected.status === 'OK' ? 99 : selected.status === 'NG' ? 80 : 60)),
    metrics,
    ocrText: String(outputData.ocrText || ''),
  };
});
</script>
