<template>
  <div class="space-y-6">
    <div class="border-b border-[var(--color-border)] pb-4">
      <h3 class="text-lg font-bold text-[var(--color-text)]">About ClearVision</h3>
      <p class="text-sm text-[var(--color-text-muted)]">System Information & Licensing.</p>
    </div>

    <div class="flex flex-col items-center justify-center py-8 space-y-4">
      <div class="w-24 h-24 bg-red-500/10 rounded-3xl flex items-center justify-center transform rotate-3">
        <SparklesIcon class="w-12 h-12 text-red-500" />
      </div>
      <h2 class="text-2xl font-black text-[var(--color-text)] tracking-tighter">ClearVision <span class="text-red-500">AI</span></h2>
      <p class="text-sm font-bold bg-[var(--color-surface)] border border-[var(--color-border)] px-3 py-1 rounded-full shadow-sm">Version 3.1.0-beta</p>
    </div>

    <div class="bg-[var(--color-background)] p-4 rounded-xl border border-[var(--color-border)] shadow-sm text-sm text-[var(--color-text)] space-y-2">
      <div class="flex justify-between border-b border-[var(--color-border)] pb-2">
        <span class="text-[var(--color-text-muted)]">Frontend FW</span>
        <span class="font-bold">Vue 3 + Vite + Tailwind CSS</span>
      </div>
      <div class="flex justify-between border-b border-[var(--color-border)] pb-2">
        <span class="text-[var(--color-text-muted)]">Backend FW</span>
        <span class="font-bold">.NET 9 + OpenCVSharp</span>
      </div>
      <div class="flex justify-between">
        <span class="text-[var(--color-text-muted)]">Host Environment</span>
        <span class="font-bold">WebView2</span>
      </div>
    </div>
    
  </div>
</template>

<script setup lang="ts">
import { SparklesIcon } from 'lucide-vue-next';
</script>
