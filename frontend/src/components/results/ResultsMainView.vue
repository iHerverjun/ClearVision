<template>
  <section class="flex-1 bg-[var(--color-background)] flex flex-col min-w-0">
    <!-- Image Viewing Area -->
    <div class="flex-1 bg-gray-900 relative flex items-center justify-center overflow-hidden border-b border-[var(--color-border)]">
      <div class="relative h-full w-full flex items-center justify-center p-8">
        <div class="relative shadow-2xl border border-gray-700 rounded-sm bg-black max-h-full max-w-full">
          <div class="w-[600px] h-[400px] bg-gray-800 flex items-center justify-center relative overflow-hidden group">
            <div class="absolute inset-0 bg-[url('https://images.unsplash.com/photo-1581091226825-a6a2a5aee158?ixlib=rb-4.0.3&auto=format&fit=crop&w=1000&q=80')] bg-cover bg-center opacity-70"></div>
            <!-- Defect Highlights (Simulated) -->
            <div class="absolute top-[30%] left-[40%] w-[10%] h-[15%] border-2 border-red-500 shadow-[0_0_10px_rgba(239,68,68,0.5)] bg-red-500/10"></div>
            <div class="absolute bottom-[20%] right-[30%] w-[12%] h-[8%] border-2 border-red-500 shadow-[0_0_10px_rgba(239,68,68,0.5)] bg-red-500/10"></div>
            <div class="absolute top-[25%] left-[40%] bg-red-500 text-white text-[10px] px-1 rounded-sm">Defect: Scratch</div>
          </div>
        </div>
        
        <!-- Controls Floating -->
        <div class="absolute bottom-6 left-1/2 transform -translate-x-1/2 bg-gray-800/80 backdrop-blur rounded-full px-4 py-2 flex items-center space-x-4 border border-gray-600">
          <button class="text-white hover:text-red-500 transition"><ZoomInIcon class="w-5 h-5" /></button>
          <button class="text-white hover:text-red-500 transition"><ZoomOutIcon class="w-5 h-5" /></button>
          <button class="text-white hover:text-red-500 transition"><MaximizeIcon class="w-5 h-5" /></button>
          <div class="w-px h-4 bg-gray-500"></div>
          <button class="text-white hover:text-red-500 transition"><DownloadIcon class="w-5 h-5" /></button>
        </div>
      </div>
    </div>

    <!-- Horizontal Image History -->
    <div class="h-48 bg-[var(--color-surface)] border-t border-[var(--color-border)] flex flex-col flex-shrink-0">
      <div class="px-4 py-2 border-b border-[var(--color-border)] flex justify-between items-center bg-[var(--color-background)]">
        <h3 class="text-xs font-bold text-[var(--color-text-muted)] uppercase">Image History (结果回溯)</h3>
        <div class="flex space-x-2">
          <button class="p-1 hover:bg-[var(--color-border)] rounded"><LayoutGridIcon class="w-4 h-4 text-[var(--color-text-muted)]" /></button>
          <button class="p-1 hover:bg-[var(--color-border)] rounded"><ListIcon class="w-4 h-4 text-[var(--color-text-muted)]" /></button>
        </div>
      </div>
      <div class="flex-1 overflow-x-auto p-3 flex space-x-3 items-center">
        <!-- Dynamic History Items -->
        <div 
          v-for="item in imageHistory" 
          :key="item.id"
          class="relative group cursor-pointer flex-shrink-0 w-32 transition-opacity"
          :class="item.selected ? 'opacity-100' : 'opacity-70 hover:opacity-100'"
        >
          <div 
            class="h-24 w-full rounded overflow-hidden relative"
            :class="item.selected ? 'border-2 border-red-500' : 'border border-[var(--color-border)]'"
          >
            <div 
              class="absolute inset-0 bg-gray-800 bg-cover bg-center"
              :style="{ backgroundImage: `url(${item.url})` }"
            ></div>
            <div 
              class="absolute top-1 right-1 text-white text-[9px] font-bold px-1.5 py-0.5 rounded shadow-sm"
              :class="item.status === 'OK' ? 'bg-green-500' : 'bg-red-500'"
            >
              {{ item.status }}
            </div>
          </div>
          <div class="mt-1 flex justify-between items-center">
            <span class="text-[10px] text-[var(--color-text-muted)] font-mono">{{ item.time }}</span>
            <span class="text-[10px] text-[var(--color-text-muted)]">{{ item.serial }}</span>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { 
  ZoomInIcon, 
  ZoomOutIcon, 
  MaximizeIcon, 
  DownloadIcon, 
  LayoutGridIcon, 
  ListIcon 
} from 'lucide-vue-next';

interface HistoryItem {
  id: string;
  url: string;
  status: 'OK' | 'NG';
  time: string;
  serial: string;
  selected?: boolean;
}

const imageHistory = ref<HistoryItem[]>([
  {
    id: 'img_042',
    url: 'https://images.unsplash.com/photo-1581091226825-a6a2a5aee158?ixlib=rb-4.0.3&auto=format&fit=crop&w=200&q=60',
    status: 'NG',
    time: '10:42:05',
    serial: '#042',
    selected: true
  },
  {
    id: 'img_041',
    url: 'https://images.unsplash.com/photo-1565514020176-db793616a843?ixlib=rb-4.0.3&auto=format&fit=crop&w=200&q=60',
    status: 'OK',
    time: '10:41:58',
    serial: '#041',
    selected: false
  },
  {
    id: 'img_040',
    url: 'https://images.unsplash.com/photo-1531297461136-82lw9b6266d2?ixlib=rb-4.0.3&auto=format&fit=crop&w=200&q=60',
    status: 'OK',
    time: '10:41:45',
    serial: '#040',
    selected: false
  }
]);
</script>
