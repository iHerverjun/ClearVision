<template>
  <section class="flex-1 bg-[var(--color-background)] flex flex-col min-w-0">
    <div
      ref="viewerRef"
      class="flex-1 bg-gray-900 relative flex items-center justify-center overflow-hidden border-b border-[var(--color-border)]"
    >
      <div class="relative h-full w-full flex items-center justify-center p-8">
        <div class="relative shadow-2xl border border-gray-700 rounded-sm bg-black max-h-full max-w-full overflow-hidden">
          <img
            v-if="selectedImage"
            :src="selectedImage"
            alt="检测结果"
            class="max-h-[70vh] max-w-[70vw] object-contain transition-transform duration-150"
            :style="{ transform: `scale(${zoomLevel})` }"
          />
          <div v-else class="w-[600px] h-[400px] flex items-center justify-center text-gray-400">
            暂无图像
          </div>

          <div
            v-for="defect in defectOverlays"
            :key="defect.id"
            class="absolute border-2 border-red-500 shadow-[0_0_10px_rgba(239,68,68,0.6)] bg-red-500/10"
            :style="{
              left: `${defect.left}%`,
              top: `${defect.top}%`,
              width: `${defect.width}%`,
              height: `${defect.height}%`,
            }"
          >
            <div class="absolute -top-5 left-0 bg-red-500 text-white text-[10px] px-1 rounded-sm whitespace-nowrap">
              缺陷：{{ defect.type }}
            </div>
          </div>
        </div>

        <div class="absolute bottom-6 left-1/2 transform -translate-x-1/2 bg-gray-800/80 backdrop-blur rounded-full px-4 py-2 flex items-center space-x-4 border border-gray-600">
          <button class="text-white hover:text-red-500 transition" @click="zoomIn">
            <ZoomInIcon class="w-5 h-5" />
          </button>
          <button class="text-white hover:text-red-500 transition" @click="zoomOut">
            <ZoomOutIcon class="w-5 h-5" />
          </button>
          <button class="text-white hover:text-red-500 transition" @click="resetZoom">
            <MaximizeIcon class="w-5 h-5" />
          </button>
          <div class="w-px h-4 bg-gray-500"></div>
          <button class="text-white hover:text-red-500 transition" @click="downloadCurrentImage">
            <DownloadIcon class="w-5 h-5" />
          </button>
          <button class="text-white hover:text-red-500 transition" @click="toggleFullscreen">
            <MaximizeIcon class="w-5 h-5" />
          </button>
        </div>
      </div>
    </div>

    <div class="h-48 bg-[var(--color-surface)] border-t border-[var(--color-border)] flex flex-col flex-shrink-0">
      <div class="px-4 py-2 border-b border-[var(--color-border)] flex justify-between items-center bg-[var(--color-background)]">
        <h3 class="text-xs font-bold text-[var(--color-text-muted)] uppercase">图像历史</h3>
        <div class="flex space-x-2">
          <button class="p-1 hover:bg-[var(--color-border)] rounded" @click="viewMode = 'grid'">
            <LayoutGridIcon class="w-4 h-4" :class="viewMode === 'grid' ? 'text-red-500' : 'text-[var(--color-text-muted)]'" />
          </button>
          <button class="p-1 hover:bg-[var(--color-border)] rounded" @click="viewMode = 'list'">
            <ListIcon class="w-4 h-4" :class="viewMode === 'list' ? 'text-red-500' : 'text-[var(--color-text-muted)]'" />
          </button>
        </div>
      </div>

      <div class="flex-1 overflow-x-auto p-3 flex space-x-3 items-center" :class="viewMode === 'list' ? 'flex-col items-stretch space-x-0 space-y-2' : ''">
        <button
          v-for="item in resultsStore.filteredRecords"
          :key="item.id"
          class="relative group cursor-pointer transition-opacity bg-transparent text-left"
          :class="viewMode === 'grid'
            ? ['flex-shrink-0 w-32', item.id === resultsStore.selectedRecord?.id ? 'opacity-100' : 'opacity-70 hover:opacity-100']
            : ['w-full p-2 rounded border', item.id === resultsStore.selectedRecord?.id ? 'border-red-500 bg-red-500/5' : 'border-[var(--color-border)]']"
          @click="resultsStore.selectRecord(item)"
        >
          <template v-if="viewMode === 'grid'">
            <div
              class="h-24 w-full rounded overflow-hidden relative"
              :class="item.id === resultsStore.selectedRecord?.id ? 'border-2 border-red-500' : 'border border-[var(--color-border)]'"
            >
              <img class="absolute inset-0 w-full h-full object-cover bg-gray-800" :src="item.outputImage" alt="历史图像" />
              <div
                class="absolute top-1 right-1 text-white text-[9px] font-bold px-1.5 py-0.5 rounded shadow-sm"
                :class="item.status === 'OK' ? 'bg-green-500' : item.status === 'NG' ? 'bg-red-500' : 'bg-gray-500'"
              >
                {{ item.status }}
              </div>
            </div>
            <div class="mt-1 flex justify-between items-center">
              <span class="text-[10px] text-[var(--color-text-muted)] font-mono">{{ formatTime(item.inspectionTime) }}</span>
              <span class="text-[10px] text-[var(--color-text-muted)]">{{ item.id.slice(-4) }}</span>
            </div>
          </template>

          <template v-else>
            <div class="flex items-center justify-between">
              <span class="text-xs font-mono text-[var(--color-text)]">{{ item.id }}</span>
              <span
                class="text-[10px] font-bold px-2 py-0.5 rounded"
                :class="item.status === 'OK' ? 'bg-green-500/10 text-green-600' : item.status === 'NG' ? 'bg-red-500/10 text-red-600' : 'bg-gray-500/10 text-gray-600'"
              >
                {{ item.status }}
              </span>
            </div>
            <div class="text-[10px] text-[var(--color-text-muted)] mt-1">
              {{ formatTime(item.inspectionTime) }} | {{ Math.round(item.processingTimeMs) }}ms
            </div>
          </template>
        </button>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import {
  ZoomInIcon,
  ZoomOutIcon,
  MaximizeIcon,
  DownloadIcon,
  LayoutGridIcon,
  ListIcon,
} from 'lucide-vue-next';
import { useResultsStore } from '../../stores/results';

const resultsStore = useResultsStore();
const viewerRef = ref<HTMLElement | null>(null);
const zoomLevel = ref(1);
const viewMode = ref<'grid' | 'list'>('grid');

const selectedImage = computed(() => resultsStore.selectedRecord?.outputImage || '');

const defectOverlays = computed(() => {
  const record = resultsStore.selectedRecord;
  if (!record) {
    return [];
  }

  const imageWidth = Number(record.outputData?.imageWidth || 1920);
  const imageHeight = Number(record.outputData?.imageHeight || 1080);

  return record.defects.map((defect) => ({
    id: defect.id,
    type: defect.type,
    left: Math.max(0, Math.min(100, (defect.x / imageWidth) * 100)),
    top: Math.max(0, Math.min(100, (defect.y / imageHeight) * 100)),
    width: Math.max(1, Math.min(100, (defect.width / imageWidth) * 100)),
    height: Math.max(1, Math.min(100, (defect.height / imageHeight) * 100)),
  }));
});

const zoomIn = () => {
  zoomLevel.value = Math.min(zoomLevel.value + 0.1, 4);
};

const zoomOut = () => {
  zoomLevel.value = Math.max(zoomLevel.value - 0.1, 0.2);
};

const resetZoom = () => {
  zoomLevel.value = 1;
};

const toggleFullscreen = async () => {
  if (!viewerRef.value) {
    return;
  }
  if (!document.fullscreenElement) {
    await viewerRef.value.requestFullscreen();
  } else {
    await document.exitFullscreen();
  }
};

const downloadCurrentImage = () => {
  if (!selectedImage.value) {
    return;
  }
  const anchor = document.createElement('a');
  anchor.href = selectedImage.value;
  anchor.download = `${resultsStore.selectedRecord?.id || '检测图像'}.png`;
  anchor.click();
};

const formatTime = (iso: string) => {
  const date = new Date(iso);
  return Number.isNaN(date.getTime()) ? '--' : date.toLocaleTimeString();
};
</script>
