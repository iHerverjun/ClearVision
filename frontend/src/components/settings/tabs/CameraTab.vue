<template>
  <div class="space-y-6">
    <div class="border-b border-[var(--color-border)] pb-4">
      <h3 class="text-lg font-bold text-[var(--color-text)]">相机设置</h3>
      <p class="text-sm text-[var(--color-text-muted)]">配置全局默认采集参数。</p>
    </div>

    <!-- Default Resolution -->
    <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm">
      <label class="block text-sm font-bold text-[var(--color-text)] mb-2">默认分辨率</label>
      <select v-model="settings.camera.defaultResolution" class="w-full sm:w-64 bg-[var(--color-surface)] border border-[var(--color-border)] text-[var(--color-text)] text-sm rounded-lg focus:ring-red-500 focus:border-red-500 block p-2.5">
        <option value="1920x1080">1080p (1920x1080)</option>
        <option value="2560x1440">1440p (2560x1440)</option>
        <option value="3840x2160">4K (3840x2160)</option>
      </select>
    </div>

    <!-- Exposure Target -->
    <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm">
      <label class="block text-sm font-bold text-[var(--color-text)] mb-2">自动曝光目标值（0-255）</label>
      <div class="flex items-center space-x-4">
        <input type="range" min="0" max="255" v-model.number="settings.camera.exposureTarget" class="w-full h-2 bg-gray-200 rounded-lg appearance-none cursor-pointer dark:bg-gray-700 accent-red-500">
        <span class="text-sm font-bold text-[var(--color-text)] w-8">{{ settings.camera.exposureTarget }}</span>
      </div>
      <p class="text-xs text-[var(--color-text-muted)] mt-2">用于自动曝光时的目标亮度值。</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { useSettingsStore } from '../../../stores/settings';

const settingsStore = useSettingsStore();
const { settings } = storeToRefs(settingsStore);
</script>
