<template>
  <aside class="w-80 bg-[var(--color-surface)] border-r border-[var(--color-border)] flex flex-col z-10 shadow-sm flex-shrink-0">
    <div class="p-5 border-b border-[var(--color-border)] flex items-center justify-between">
      <h2 class="text-sm font-bold text-[var(--color-text)] uppercase tracking-wider">AI 对话</h2>
      <span class="bg-red-500/10 text-red-500 text-[10px] px-2 py-0.5 rounded-full font-bold">测试版</span>
    </div>
    
    <div class="flex-1 overflow-y-auto p-4 space-y-6 flex flex-col">
      <div class="flex-1 space-y-4 relative">
        <template v-for="msg in aiStore.messages" :key="msg.id">
          <!-- Assistant Message -->
          <div v-if="msg.role === 'assistant'" class="flex flex-col space-y-1">
            <div class="flex items-center space-x-2">
              <div class="w-6 h-6 rounded-full bg-red-500/10 flex items-center justify-center">
                <SparklesIcon class="text-red-500 w-3.5 h-3.5" />
              </div>
              <span class="text-xs font-bold text-[var(--color-text)]">ClearVision AI</span>
              <span class="text-[10px] text-[var(--color-text-muted)]">{{ new Date(msg.timestamp).toLocaleTimeString() }}</span>
            </div>
            <div :class="[
              'p-3 rounded-lg rounded-tl-none text-sm leading-relaxed shadow-sm',
              msg.isError ? 'bg-red-50 dark:bg-red-900/20 text-red-600 border border-red-200' : 'bg-[var(--color-background)] text-[var(--color-text)]'
            ]">
              <div v-html="formatMessage(msg.content)"></div>
            </div>
          </div>

          <!-- User Message -->
          <div v-else class="flex flex-col space-y-1 items-end">
            <div class="flex items-center space-x-2 flex-row-reverse space-x-reverse">
              <div class="w-6 h-6 rounded-full bg-gray-200 dark:bg-gray-700 flex items-center justify-center">
                <UserIcon class="text-gray-500 w-3.5 h-3.5" />
              </div>
              <span class="text-xs font-bold text-[var(--color-text)]">用户</span>
              <span class="text-[10px] text-[var(--color-text-muted)] mr-2">{{ new Date(msg.timestamp).toLocaleTimeString() }}</span>
            </div>
            <div class="bg-red-500 text-white p-3 rounded-lg rounded-tr-none text-sm leading-relaxed shadow-sm">
              {{ msg.content }}
            </div>
          </div>
        </template>

        <!-- Loading Indicator -->
        <div v-if="aiStore.isGenerating" class="flex flex-col space-y-1">
           <div class="flex items-center space-x-2">
            <div class="w-6 h-6 rounded-full bg-red-500/10 flex items-center justify-center">
              <SparklesIcon class="text-red-500 w-3.5 h-3.5 animate-pulse" />
            </div>
            <span class="text-xs font-bold text-[var(--color-text)]">ClearVision AI</span>
          </div>
          <div class="bg-[var(--color-background)] p-3 rounded-lg rounded-tl-none flex space-x-1 w-16 h-10 items-center justify-center">
            <div class="w-2 h-2 bg-red-400 rounded-full animate-bounce" style="animation-delay: 0ms"></div>
            <div class="w-2 h-2 bg-red-400 rounded-full animate-bounce" style="animation-delay: 150ms"></div>
            <div class="w-2 h-2 bg-red-400 rounded-full animate-bounce" style="animation-delay: 300ms"></div>
          </div>
        </div>

      </div>
    </div>

    <!-- Input Area -->
    <div class="p-4 border-t border-[var(--color-border)] bg-[var(--color-surface)]">
      <div class="mb-3">
        <div class="flex space-x-2 overflow-x-auto pb-1 hide-scrollbar">
          <button @click="sendQuickPrompt('一键测量')" class="flex-shrink-0 px-3 py-1.5 bg-[var(--color-surface)] border border-red-500 text-red-500 text-xs rounded-full hover:bg-red-500 hover:text-white transition-colors whitespace-nowrap disabled:opacity-50" :disabled="aiStore.isGenerating">
            一键测量
          </button>
          <button @click="sendQuickPrompt('PCB文字识别')" class="flex-shrink-0 px-3 py-1.5 bg-[var(--color-surface)] border border-red-500 text-red-500 text-xs rounded-full hover:bg-red-500 hover:text-white transition-colors whitespace-nowrap disabled:opacity-50" :disabled="aiStore.isGenerating">
            PCB文字识别
          </button>
          <button @click="sendQuickPrompt('缺陷检测')" class="flex-shrink-0 px-3 py-1.5 bg-[var(--color-surface)] border border-red-500 text-red-500 text-xs rounded-full hover:bg-red-500 hover:text-white transition-colors whitespace-nowrap disabled:opacity-50" :disabled="aiStore.isGenerating">
            缺陷检测
          </button>
        </div>
      </div>
      <div class="relative">
        <input 
          v-model="promptText"
          @keyup.enter="handleSend"
          :disabled="aiStore.isGenerating"
          class="w-full pl-4 pr-10 py-3 bg-[var(--color-background)] border border-[var(--color-border)] rounded-xl focus:ring-2 focus:ring-red-500 focus:border-red-500 text-sm shadow-sm transition-all outline-none text-[var(--color-text)] disabled:opacity-50" 
          placeholder="让 AI 帮你生成或修改流程..." 
          type="text"
        />
        <button 
          @click="handleSend"
          :disabled="!promptText.trim() || aiStore.isGenerating"
          class="absolute right-2 top-1/2 transform -translate-y-1/2 p-1.5 bg-red-500 hover:bg-red-600 text-white rounded-lg transition-colors shadow-sm flex items-center justify-center disabled:opacity-50 disabled:bg-gray-400"
        >
          <SendIcon class="w-4 h-4 ml-0.5" />
        </button>
      </div>
    </div>
  </aside>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { SparklesIcon, UserIcon, SendIcon } from 'lucide-vue-next';
import { useAiStore } from '../../stores/ai';

const aiStore = useAiStore();
const promptText = ref('');

const handleSend = () => {
  if (!promptText.value.trim() || aiStore.isGenerating) return;
  aiStore.sendPrompt(promptText.value);
  promptText.value = '';
};

const sendQuickPrompt = (text: string) => {
  aiStore.sendPrompt(text);
};

// Simple formatter for bold tags and newlines
const formatMessage = (msg: string) => {
  return msg
    .replace(/\n/g, "<br/>")
    .replace(/\*(.*?)\*/g, "<b>$1</b>");
};
</script>

<style scoped>
.hide-scrollbar::-webkit-scrollbar {
  display: none;
}
.hide-scrollbar {
  -ms-overflow-style: none;
  scrollbar-width: none;
}
</style>
