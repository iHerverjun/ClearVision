<template>
  <div class="space-y-6">
    <div class="border-b border-[var(--color-border)] pb-4">
      <h3 class="text-lg font-bold text-[var(--color-text)]">AI Engine Settings</h3>
      <p class="text-sm text-[var(--color-text-muted)]">Configure connection to the Large Language Model.</p>
    </div>

    <!-- API Key -->
    <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm">
      <label class="block text-sm font-bold text-[var(--color-text)] mb-2">API Key</label>
      <input type="password" v-model="settings.ai.apiKey" class="w-full bg-[var(--color-surface)] border border-[var(--color-border)] text-[var(--color-text)] text-sm rounded-lg focus:ring-red-500 focus:border-red-500 block p-2.5" placeholder="sk-..." />
      <p class="text-xs text-[var(--color-text-muted)] mt-2">Saved securely via backend. Leave blank to inherit system environment variable.</p>
    </div>

    <!-- Model Selection -->
    <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm">
      <label class="block text-sm font-bold text-[var(--color-text)] mb-2">Model Version</label>
      <select v-model="settings.ai.model" class="w-full sm:w-64 bg-[var(--color-surface)] border border-[var(--color-border)] text-[var(--color-text)] text-sm rounded-lg focus:ring-red-500 focus:border-red-500 block p-2.5">
        <option value="DeepSeek-V3">DeepSeek V3 (Default)</option>
        <option value="DeepSeek-R1">DeepSeek R1 (Reasoning)</option>
        <option value="GPT-4o">OpenAI GPT-4o</option>
      </select>
    </div>

    <!-- Timeout -->
    <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm">
      <label class="block text-sm font-bold text-[var(--color-text)] mb-2">Request Timeout (MS)</label>
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
