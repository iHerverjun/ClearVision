<template>
  <div class="absolute bottom-0 left-0 right-0 z-40 flex flex-col transition-all duration-300 pointer-events-none">
    
    <!-- Lint Panel Trigger Button -->
    <div class="flex justify-end pr-4 pb-4 pointer-events-auto">
      <button 
        @click="togglePanel"
        :class="[
          'flex items-center space-x-2 px-4 py-2 rounded-full shadow-lg border backdrop-blur-md transition-colors',
          hasErrors ? 'bg-red-50/90 border-red-200 text-red-600 dark:bg-red-900/30 dark:border-red-800' :
          hasWarnings ? 'bg-amber-50/90 border-amber-200 text-amber-600 dark:bg-amber-900/30 dark:border-amber-800' :
          'bg-[var(--color-surface)] border-[var(--color-border)] text-green-600'
        ]"
      >
        <AlertTriangleIcon v-if="hasErrors || hasWarnings" class="w-4 h-4" />
        <CheckCircleIcon v-else class="w-4 h-4" />
        <span class="text-sm font-bold">
          {{ hasErrors ? `${errorCount} 个错误` : hasWarnings ? `${warningCount} 个警告` : '0 个问题' }}
        </span>
        <ChevronUpIcon 
          :class="['w-4 h-4 transition-transform duration-300', isExpanded ? 'rotate-180' : '']" 
        />
      </button>
    </div>

    <!-- Expanded Panel -->
    <div 
      class="bg-[var(--color-surface)] border-t border-[var(--color-border)] shadow-[0_-4px_20px_rgba(0,0,0,0.05)] pointer-events-auto transition-all duration-300"
      :style="{ height: isExpanded ? '200px' : '0px', opacity: isExpanded ? 1 : 0, overflow: 'hidden' }"
    >
      <div class="h-full flex flex-col p-4">
        <div class="flex items-center justify-between mb-3 border-b border-[var(--color-border)] pb-2">
          <h3 class="text-sm font-bold text-[var(--color-text)] flex items-center space-x-2">
            <ActivityIcon class="w-4 h-4 text-primary-500" />
            <span>流程检查</span>
          </h3>
          <button @click="isExpanded = false" class="text-[var(--color-text-muted)] hover:text-[var(--color-text)]">
            <XIcon class="w-4 h-4" />
          </button>
        </div>

        <div class="flex-1 overflow-y-auto pr-2 space-y-2">
          <div 
            v-if="flowStore.lintIssues.length === 0" 
            class="flex items-center justify-center h-full text-sm text-[var(--color-text-muted)]"
          >
            未发现问题，流程可执行。
          </div>

          <div 
            v-for="issue in flowStore.lintIssues" 
            :key="issue.id"
            :class="[
              'p-3 rounded-lg border flex items-start space-x-3 text-sm cursor-pointer hover:bg-gray-50/50 dark:hover:bg-gray-800/50 transition-colors',
              issue.type === 'error' ? 'bg-red-50/50 border-red-100 dark:bg-red-900/10 dark:border-red-900/30' : 
              issue.type === 'warning' ? 'bg-amber-50/50 border-amber-100 dark:bg-amber-900/10 dark:border-amber-900/30' : 
              'bg-blue-50/50 border-blue-100 dark:bg-blue-900/10 dark:border-blue-900/30'
            ]"
            @click="focusNode(issue.nodeId)"
          >
            <div :class="[
              'mt-0.5',
              issue.type === 'error' ? 'text-red-500' :
              issue.type === 'warning' ? 'text-amber-500' :
              'text-blue-500'
            ]">
              <XCircleIcon v-if="issue.type === 'error'" class="w-4 h-4" />
              <AlertTriangleIcon v-else-if="issue.type === 'warning'" class="w-4 h-4" />
              <InfoIcon v-else class="w-4 h-4" />
            </div>
            <div class="flex-1">
              <span :class="[
                'font-medium block mb-0.5',
                issue.type === 'error' ? 'text-red-700 dark:text-red-400' :
                issue.type === 'warning' ? 'text-amber-700 dark:text-amber-400' :
                'text-blue-700 dark:text-blue-400'
              ]">
                {{ issue.message }}
              </span>
              <span v-if="issue.nodeId" class="text-xs text-[var(--color-text-muted)] font-mono">
                节点：{{ issue.nodeId }}
              </span>
            </div>
          </div>
        </div>
      </div>
    </div>

  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue';
import { 
  AlertTriangleIcon, 
  CheckCircleIcon, 
  ChevronUpIcon, 
  XIcon, 
  XCircleIcon, 
  InfoIcon,
  ActivityIcon 
} from 'lucide-vue-next';
import { useFlowStore } from '../../stores/flow';

const flowStore = useFlowStore();
const isExpanded = ref(false);

const togglePanel = () => {
  isExpanded.value = !isExpanded.value;
};

const hasErrors = computed(() => flowStore.lintIssues.some(i => i.type === 'error'));
const hasWarnings = computed(() => flowStore.lintIssues.some(i => i.type === 'warning'));
const errorCount = computed(() => flowStore.lintIssues.filter(i => i.type === 'error').length);
const warningCount = computed(() => flowStore.lintIssues.filter(i => i.type === 'warning').length);

const focusNode = (nodeId?: string) => {
  if (nodeId) {
    flowStore.selectNode(nodeId);
    // Ideally we would trigger Vue Flow's fitView or center to node here
  }
};
</script>
