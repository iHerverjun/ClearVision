<template>
  <div class="space-y-6">
    <div class="border-b border-[var(--color-border)] pb-4">
      <h3 class="text-lg font-bold text-[var(--color-text)]">通信设置</h3>
      <p class="text-sm text-[var(--color-text-muted)]">配置外部设备通信协议。</p>
    </div>

    <!-- Protocol Selection -->
    <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm">
      <label class="block text-sm font-bold text-[var(--color-text)] mb-2">主通信协议</label>
      <select v-model="settings.communication.protocol" class="w-full sm:w-64 bg-[var(--color-surface)] border border-[var(--color-border)] text-[var(--color-text)] text-sm rounded-lg focus:ring-red-500 focus:border-red-500 block p-2.5">
        <option value="TCP">TCP/IP 套接字</option>
        <option value="Serial">串口（RS-232/485）</option>
        <option value="PLC">PLC（FINS / MC / S7）</option>
        <option value="HTTP">HTTP 服务</option>
        <option value="MQTT">MQTT Broker</option>
      </select>
    </div>

    <!-- Host & Port -->
    <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm flex flex-col sm:flex-row space-y-4 sm:space-y-0 sm:space-x-4">
      <div class="flex-1">
        <label class="block text-sm font-bold text-[var(--color-text)] mb-2">主机 / IP / COM 口</label>
        <input type="text" v-model="settings.communication.host" class="w-full bg-[var(--color-surface)] border border-[var(--color-border)] text-[var(--color-text)] text-sm rounded-lg focus:ring-red-500 focus:border-red-500 block p-2.5" placeholder="127.0.0.1 或 COM3" />
      </div>
      <div class="w-32">
        <label class="block text-sm font-bold text-[var(--color-text)] mb-2">端口 / 波特率</label>
        <input type="number" v-model.number="settings.communication.port" class="w-full bg-[var(--color-surface)] border border-[var(--color-border)] text-[var(--color-text)] text-sm rounded-lg focus:ring-red-500 focus:border-red-500 block p-2.5" />
      </div>
    </div>
    
  </div>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { useSettingsStore } from '../../../stores/settings';

const settingsStore = useSettingsStore();
const { settings } = storeToRefs(settingsStore);
</script>
