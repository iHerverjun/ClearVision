<template>
  <aside class="w-64 bg-[var(--color-surface)] border-r border-[var(--color-border)] flex flex-col z-10 shadow-sm flex-shrink-0">
    <div class="p-4 border-b border-[var(--color-border)]">
      <h2 class="text-xs font-bold text-[var(--color-text-muted)] uppercase tracking-wider flex items-center">
        <FilterIcon class="w-4 h-4 mr-2" />
        FILTER (筛选)
      </h2>
    </div>
    <div class="flex-1 overflow-y-auto p-4 space-y-6">
      <div class="space-y-2">
        <label class="text-xs font-semibold text-[var(--color-text)] block">日期范围</label>
        <div class="relative flex items-center">
          <span class="absolute left-3 flex items-center pointer-events-none text-[var(--color-text-muted)]">
            <CalendarIcon class="w-4 h-4" />
          </span>
          <input 
            v-model="filters.dateRange"
            class="block w-full pl-9 pr-3 py-2 border border-[var(--color-border)] bg-[var(--color-background)] rounded-md shadow-sm focus:ring-red-500 focus:border-red-500 text-xs text-[var(--color-text)]" 
            type="text" 
            placeholder="YYYY-MM-DD - YYYY-MM-DD"
          />
        </div>
      </div>
      <div class="space-y-2">
        <label class="text-xs font-semibold text-[var(--color-text)] block">状态 (Status)</label>
        <div class="relative flex items-center">
          <select 
            v-model="filters.status"
            class="block w-full pl-3 pr-10 py-2 text-xs border border-[var(--color-border)] bg-[var(--color-background)] focus:outline-none focus:ring-red-500 focus:border-red-500 rounded-md shadow-sm text-[var(--color-text)] appearance-none"
          >
            <option value="all">全部 (All)</option>
            <option value="ok">合格 (OK)</option>
            <option value="ng">不合格 (NG)</option>
          </select>
          <span class="absolute right-2 flex items-center pointer-events-none text-gray-400">
            <ChevronDownIcon class="w-4 h-4" />
          </span>
        </div>
      </div>
      <div class="space-y-2">
        <label class="text-xs font-semibold text-[var(--color-text)] block">搜索 (Batch/Serial)</label>
        <div class="relative flex items-center">
          <span class="absolute left-3 flex items-center pointer-events-none text-[var(--color-text-muted)]">
            <SearchIcon class="w-4 h-4" />
          </span>
          <input 
            v-model="filters.searchQuery"
            class="block w-full pl-9 pr-3 py-2 border border-[var(--color-border)] bg-[var(--color-background)] rounded-md shadow-sm focus:ring-red-500 focus:border-red-500 text-xs placeholder-[var(--color-text-muted)] text-[var(--color-text)]" 
            placeholder="输入批次号或序列号..." 
            type="text"
          />
        </div>
      </div>
      <div class="pt-4 border-t border-[var(--color-border)]">
        <button 
          @click="applyFilters"
          class="w-full bg-red-500 hover:bg-red-600 text-white text-xs font-medium py-2 px-4 rounded shadow-sm transition-colors flex items-center justify-center"
        >
          <CheckIcon class="w-4 h-4 mr-1" /> 应用筛选
        </button>
      </div>
    </div>
    <div class="p-4 bg-[var(--color-background)] border-t border-[var(--color-border)]">
      <div class="flex justify-between text-xs text-[var(--color-text-muted)] mb-1">
        <span>总记录数:</span>
        <span class="font-mono font-medium text-[var(--color-text)]">{{ stats.total }}</span>
      </div>
      <div class="flex justify-between text-xs text-[var(--color-text-muted)]">
        <span>NG 数量:</span>
        <span class="font-mono font-medium text-red-500">{{ stats.ngCount }}</span>
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
  CheckIcon 
} from 'lucide-vue-next';

interface Filters {
  dateRange: string;
  status: string;
  searchQuery: string;
}

const filters = reactive<Filters>({
  dateRange: '2023-10-27 - 2023-10-27',
  status: 'all',
  searchQuery: ''
});

const stats = reactive({
  total: '1,248',
  ngCount: '12'
});

const applyFilters = () => {
  console.log('Applying filters:', filters);
  // Implementation will be tied to a store later
};
</script>
