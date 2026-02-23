<template>
  <div class="space-y-6">
    <div class="border-b border-[var(--color-border)] pb-4">
      <h3 class="text-lg font-bold text-[var(--color-text)]">AI 引擎设置</h3>
      <p class="text-sm text-[var(--color-text-muted)]">配置与大语言模型的连接。</p>
    </div>

    <!-- API Key -->
    <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm">
      <label class="block text-sm font-bold text-[var(--color-text)] mb-2">API 密钥</label>
      <input type="password" v-model="settings.ai.apiKey" class="w-full bg-[var(--color-surface)] border border-[var(--color-border)] text-[var(--color-text)] text-sm rounded-lg focus:ring-red-500 focus:border-red-500 block p-2.5" placeholder="sk-..." />
      <p class="text-xs text-[var(--color-text-muted)] mt-2">由后端安全保存。留空时沿用系统环境变量。</p>
    </div>

    <!-- Model Selection -->
    <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm">
      <label class="block text-sm font-bold text-[var(--color-text)] mb-2">模型版本</label>
      <select v-model="settings.ai.model" class="w-full sm:w-64 bg-[var(--color-surface)] border border-[var(--color-border)] text-[var(--color-text)] text-sm rounded-lg focus:ring-red-500 focus:border-red-500 block p-2.5">
        <option value="DeepSeek-V3">DeepSeek V3（默认）</option>
        <option value="DeepSeek-R1">DeepSeek R1（推理）</option>
        <option value="GPT-4o">OpenAI GPT-4o</option>
      </select>
    </div>

    <!-- Timeout -->
    <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm">
      <label class="block text-sm font-bold text-[var(--color-text)] mb-2">请求超时（毫秒）</label>
      <input type="number" min="5000" max="60000" step="1000" v-model.number="settings.ai.timeoutMs" class="w-full sm:w-32 bg-[var(--color-surface)] border border-[var(--color-border)] text-[var(--color-text)] text-sm rounded-lg focus:ring-red-500 focus:border-red-500 block p-2.5" />
    </div>

  </div>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { useSettingsStore } from '../../../stores/settings';

const settingsStore = useSettingsStore();
const { settings } = storeToRefs(settingsStore);
</script>
