<template>
  <div class="space-y-6">
    <div class="border-b border-[var(--color-border)] pb-4">
      <h3 class="text-lg font-bold text-[var(--color-text)]">General Configuration</h3>
      <p class="text-sm text-[var(--color-text-muted)]">Manage visual theme, language, and global app settings.</p>
    </div>

    <div class="space-y-5">
      <!-- Theme Selection -->
      <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm">
        <label class="block text-sm font-bold text-[var(--color-text)] mb-2">Theme</label>
        <div class="flex space-x-3">
          <label class="flex items-center space-x-2 cursor-pointer bg-[var(--color-surface)] px-4 py-2 rounded-lg border border-[var(--color-border)] hover:border-red-500 transition-colors">
            <input type="radio" v-model="settings.general.theme" value="light" class="text-red-500 focus:ring-red-500" />
            <span class="text-sm font-medium text-[var(--color-text)]">Light</span>
          </label>
          <label class="flex items-center space-x-2 cursor-pointer bg-[var(--color-surface)] px-4 py-2 rounded-lg border border-[var(--color-border)] hover:border-red-500 transition-colors">
            <input type="radio" v-model="settings.general.theme" value="dark" class="text-red-500 focus:ring-red-500" />
            <span class="text-sm font-medium text-[var(--color-text)]">Dark</span>
          </label>
          <label class="flex items-center space-x-2 cursor-pointer bg-[var(--color-surface)] px-4 py-2 rounded-lg border border-[var(--color-border)] hover:border-red-500 transition-colors">
            <input type="radio" v-model="settings.general.theme" value="system" class="text-red-500 focus:ring-red-500" />
            <span class="text-sm font-medium text-[var(--color-text)]">System</span>
          </label>
        </div>
      </div>

      <!-- Language Selection (Prep for i18n) -->
      <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm">
        <label class="block text-sm font-bold text-[var(--color-text)] mb-2">Language</label>
        <select v-model="settings.general.language" class="w-full sm:w-64 bg-[var(--color-surface)] border border-[var(--color-border)] text-[var(--color-text)] text-sm rounded-lg focus:ring-red-500 focus:border-red-500 block p-2.5">
          <option value="zh-CN">简体中文</option>
          <option value="en-US">English</option>
        </select>
        <p class="text-xs text-[var(--color-text-muted)] mt-2">Requires application restart for full effect if dynamic switching isn't enabled.</p>
      </div>

      <!-- Auto Save -->
      <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm">
        <label class="block text-sm font-bold text-[var(--color-text)] mb-2">Auto Save Interval (Minutes)</label>
        <input type="number" min="0" max="60" v-model.number="settings.general.autoSaveInterval" class="w-full sm:w-32 bg-[var(--color-surface)] border border-[var(--color-border)] text-[var(--color-text)] text-sm rounded-lg focus:ring-red-500 focus:border-red-500 block p-2.5" />
        <p class="text-xs text-[var(--color-text-muted)] mt-2">Set to 0 to disable automatic saving.</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { useSettingsStore } from '../../../stores/settings';
import { watch } from 'vue';
import { useUiStore } from '../../../stores/ui';
import { useI18n } from 'vue-i18n';

const settingsStore = useSettingsStore();
const uiStore = useUiStore();
const { settings } = storeToRefs(settingsStore);
const { locale } = useI18n();

// React to theme change instantly
watch(() => settings.value.general.theme, (newTheme) => {
    if (newTheme === 'dark' || newTheme === 'light') {
        uiStore.theme = newTheme;
    } else {
        // system
        const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
        uiStore.theme = prefersDark ? 'dark' : 'light';
    }
});

// React to language change instantly
watch(() => settings.value.general.language, (newLang) => {
    locale.value = newLang;
    localStorage.setItem('cv_language', newLang);
});
</script>
