<template>
  <aside class="w-64 bg-[var(--color-surface)] border-r border-[var(--color-border)] flex flex-col z-10 shadow-sm flex-shrink-0">
    <div class="p-4 border-b border-[var(--color-border)]">
      <h2 class="text-xs font-bold text-[var(--color-text-muted)] uppercase tracking-wider flex items-center">
        <FilterIcon class="w-4 h-4 mr-2" />
        筛选
      </h2>
    </div>

    <div class="flex-1 overflow-y-auto p-4 space-y-6">
      <div class="space-y-2">
        <label class="text-xs font-semibold text-[var(--color-text)] block">开始日期</label>
        <div class="relative flex items-center">
          <span class="absolute left-3 flex items-center pointer-events-none text-[var(--color-text-muted)]">
            <CalendarIcon class="w-4 h-4" />
          </span>
          <input
            v-model="localFilters.startDate"
            class="block w-full pl-9 pr-3 py-2 border border-[var(--color-border)] bg-[var(--color-background)] rounded-md shadow-sm focus:ring-red-500 focus:border-red-500 text-xs text-[var(--color-text)]"
            type="date"
          />
        </div>
      </div>

      <div class="space-y-2">
        <label class="text-xs font-semibold text-[var(--color-text)] block">结束日期</label>
        <div class="relative flex items-center">
          <span class="absolute left-3 flex items-center pointer-events-none text-[var(--color-text-muted)]">
            <CalendarIcon class="w-4 h-4" />
          </span>
          <input
            v-model="localFilters.endDate"
            class="block w-full pl-9 pr-3 py-2 border border-[var(--color-border)] bg-[var(--color-background)] rounded-md shadow-sm focus:ring-red-500 focus:border-red-500 text-xs text-[var(--color-text)]"
            type="date"
          />
        </div>
      </div>

      <div class="space-y-2">
        <label class="text-xs font-semibold text-[var(--color-text)] block">状态</label>
        <div class="relative flex items-center">
          <select
            v-model="localFilters.status"
            class="block w-full pl-3 pr-10 py-2 text-xs border border-[var(--color-border)] bg-[var(--color-background)] focus:outline-none focus:ring-red-500 focus:border-red-500 rounded-md shadow-sm text-[var(--color-text)] appearance-none"
          >
            <option value="all">全部</option>
            <option value="ok">合格</option>
            <option value="ng">不合格</option>
            <option value="error">错误</option>
          </select>
          <span class="absolute right-2 flex items-center pointer-events-none text-gray-400">
            <ChevronDownIcon class="w-4 h-4" />
          </span>
        </div>
      </div>

      <div class="space-y-2">
        <label class="text-xs font-semibold text-[var(--color-text)] block">搜索（批次/序列号）</label>
        <div class="relative flex items-center">
          <span class="absolute left-3 flex items-center pointer-events-none text-[var(--color-text-muted)]">
            <SearchIcon class="w-4 h-4" />
          </span>
          <input
            v-model="localFilters.searchQuery"
            class="block w-full pl-9 pr-3 py-2 border border-[var(--color-border)] bg-[var(--color-background)] rounded-md shadow-sm focus:ring-red-500 focus:border-red-500 text-xs placeholder-[var(--color-text-muted)] text-[var(--color-text)]"
            placeholder="批次号或序列号..."
            type="text"
            @keyup.enter="applyFilters"
          />
        </div>
      </div>

      <div class="pt-4 border-t border-[var(--color-border)]">
        <button
          class="w-full bg-red-500 hover:bg-red-600 text-white text-xs font-medium py-2 px-4 rounded shadow-sm transition-colors flex items-center justify-center"
          @click="applyFilters"
        >
          <CheckIcon class="w-4 h-4 mr-1" />
          应用筛选
        </button>
      </div>
    </div>

    <div class="p-4 bg-[var(--color-background)] border-t border-[var(--color-border)]">
      <div class="flex justify-between text-xs text-[var(--color-text-muted)] mb-1">
        <span>总数：</span>
        <span class="font-mono font-medium text-[var(--color-text)]">{{ resultsStore.stats.total.toLocaleString() }}</span>
      </div>
      <div class="flex justify-between text-xs text-[var(--color-text-muted)]">
        <span>不合格数：</span>
        <span class="font-mono font-medium text-red-500">{{ resultsStore.stats.ngCount.toLocaleString() }}</span>
      </div>
    </div>
  </aside>
</template>

<script setup lang="ts">
import { reactive } from 'vue';
import {
  FilterIcon,
  CalendarIcon,
  ChevronDownIcon,
  SearchIcon,
  CheckIcon,
} from 'lucide-vue-next';
import { useResultsStore } from '../../stores/results';

const resultsStore = useResultsStore();

const localFilters = reactive({
  startDate: resultsStore.filters.startDate || '',
  endDate: resultsStore.filters.endDate || '',
  status: resultsStore.filters.status,
  searchQuery: resultsStore.filters.searchQuery,
});

const applyFilters = async () => {
  await resultsStore.loadRecords({
    startDate: localFilters.startDate,
    endDate: localFilters.endDate,
    status: localFilters.status,
    searchQuery: localFilters.searchQuery,
  });
};
</script>
