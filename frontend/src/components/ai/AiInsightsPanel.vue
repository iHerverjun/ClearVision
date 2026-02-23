<template>
  <aside class="w-80 bg-[var(--color-surface)] border-l border-[var(--color-border)] flex flex-col z-10 shadow-sm flex-shrink-0">
    <div class="p-5 border-b border-[var(--color-border)]">
      <h2 class="text-sm font-bold text-[var(--color-text)] uppercase tracking-wider">AI 洞察</h2>
    </div>
    <div class="flex-1 overflow-y-auto bg-[var(--color-background)] p-4 space-y-4">
      
      <!-- Generated Summary -->
      <div class="bg-[var(--color-surface)] rounded-xl shadow-sm border border-[var(--color-border)] p-4">
        <div class="flex items-center space-x-2 mb-3">
          <BookOpenIcon class="text-red-500 w-4 h-4" />
          <h3 class="text-xs font-bold text-[var(--color-text)] uppercase">生成摘要</h3>
        </div>
        <p v-if="aiStore.lastGeneratedFlowJson" class="text-xs text-[var(--color-text-muted)] leading-relaxed">
          AI 已根据你的需求生成流程。
          <br/><br/>
          <template v-if="aiStore.lastUsedTools && aiStore.lastUsedTools.length > 0">
            已添加 <span v-for="(tool, index) in aiStore.lastUsedTools" :key="index" class="font-mono text-red-500 bg-red-500/10 px-1 rounded mr-1 mb-1 inline-block">{{ tool }}</span>
          </template>
        </p>
        <p v-else class="text-xs text-[var(--color-text-muted)] leading-relaxed italic">
          等待 AI 生成中...
        </p>
      </div>

      <!-- Tools Used -->
      <div class="bg-[var(--color-surface)] rounded-xl shadow-sm border border-[var(--color-border)] p-4" v-if="aiStore.lastGeneratedFlowJson && toolSummary.length > 0">
        <div class="flex items-center space-x-2 mb-3">
          <SettingsIcon class="text-red-500 w-4 h-4" />
          <h3 class="text-xs font-bold text-[var(--color-text)] uppercase">使用的算子</h3>
        </div>
        <div class="space-y-2">
          <div v-for="tool in toolSummary" :key="tool.name" class="flex items-center justify-between p-2 rounded-lg bg-red-500/5 border border-red-500/20">
            <div class="flex items-center space-x-2">
              <SettingsIcon class="text-red-500 w-4 h-4" />
              <span class="text-xs font-medium text-[var(--color-text)]">{{ tool.name }}</span>
            </div>
            <span class="text-[10px] text-red-500 font-bold">x{{ tool.count }}</span>
          </div>
        </div>
      </div>

      <!-- JSON Preview -->
      <div class="bg-[var(--color-surface)] rounded-xl shadow-sm border border-[var(--color-border)] overflow-hidden flex flex-col h-64">
        <div class="p-3 border-b border-[var(--color-border)] bg-[var(--color-background)] flex justify-between items-center">
          <div class="flex items-center space-x-2">
            <CodeIcon class="text-red-500 w-4 h-4" />
            <h3 class="text-xs font-bold text-[var(--color-text)] uppercase">JSON 预览</h3>
          </div>
          <button @click="copyJson" class="text-xs text-red-500 hover:text-red-700 font-medium" :disabled="!aiStore.lastGeneratedFlowJson">复制</button>
        </div>
        <div class="p-0 flex-1 overflow-auto bg-[#1e1e1e]">
          <pre class="text-[10px] leading-4 text-gray-300 p-3 font-mono break-all whitespace-pre-wrap">{{ displayJson }}</pre>
        </div>
      </div>

    </div>
  </aside>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { 
  BookOpenIcon, 
  SettingsIcon, 
  CodeIcon 
} from 'lucide-vue-next';
import { useAiStore } from '../../stores/ai';

const aiStore = useAiStore();

const toolSummary = computed(() => {
  const counts = new Map<string, number>();
  aiStore.lastUsedTools.forEach((toolName) => {
    counts.set(toolName, (counts.get(toolName) || 0) + 1);
  });

  return Array.from(counts.entries()).map(([name, count]) => ({
    name,
    count,
  }));
});

const displayJson = computed(() => {
  if (aiStore.lastGeneratedFlowJson) {
    try {
      // Pretty print if it's parseable JSON string
      const parsed = JSON.parse(aiStore.lastGeneratedFlowJson);
      return JSON.stringify(parsed, null, 2);
    } catch {
      return aiStore.lastGeneratedFlowJson;
    }
  }
  return '// 暂无生成的流程数据';
});

const copyJson = () => {
  if (aiStore.lastGeneratedFlowJson) {
    navigator.clipboard.writeText(displayJson.value);
  }
};
</script>

