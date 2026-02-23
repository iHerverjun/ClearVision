<template>
  <div class="space-y-6">
    <div class="border-b border-[var(--color-border)] pb-4">
      <h3 class="text-lg font-bold text-[var(--color-text)]">数据库设置</h3>
      <p class="text-sm text-[var(--color-text-muted)]">配置历史检测数据存储。</p>
    </div>

    <div class="bg-yellow-50 dark:bg-yellow-900/20 p-4 rounded-xl border border-yellow-200 dark:border-yellow-700/30 shadow-sm flex items-start space-x-3">
      <AlertTriangleIcon class="w-5 h-5 text-yellow-500 flex-shrink-0 mt-0.5" />
      <div>
         <h4 class="text-sm font-bold text-yellow-800 dark:text-yellow-400">功能建设中</h4>
         <p class="text-xs text-yellow-700 dark:text-yellow-500 mt-1">
            数据库连接配置将在后续版本开放。
            当前日志通过 SQLite 保存在 AppData 目录。
         </p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { AlertTriangleIcon } from 'lucide-vue-next';
</script>
