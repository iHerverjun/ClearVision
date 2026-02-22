<template>
  <section class="flex-1 relative bg-[var(--color-background)] overflow-hidden flex flex-col">
    <!-- Overlay Pattern -->
    <div class="absolute inset-0 bg-dots opacity-30 pointer-events-none"></div>

    <!-- Zoom Controls -->
    <div class="absolute top-6 left-6 flex flex-col bg-[var(--color-surface)] rounded-lg shadow-sm border border-[var(--color-border)] z-10">
      <button class="p-2 text-[var(--color-text-muted)] hover:text-red-500 hover:bg-[var(--color-background)] rounded-t-lg border-b border-[var(--color-border)] transition-colors">
        <ZoomInIcon class="w-5 h-5" />
      </button>
      <button class="p-2 text-[var(--color-text-muted)] hover:text-red-500 hover:bg-[var(--color-background)] border-b border-[var(--color-border)] transition-colors">
        <ZoomOutIcon class="w-5 h-5" />
      </button>
      <button class="p-2 text-[var(--color-text-muted)] hover:text-red-500 hover:bg-[var(--color-background)] rounded-b-lg transition-colors">
        <MaximizeIcon class="w-5 h-5" />
      </button>
    </div>

    <!-- Graph Mock -->
    <div class="flex-1 relative overflow-auto flex items-center justify-center p-10">
      <div class="relative w-full h-full flex flex-col items-center justify-center">
        <div class="flex flex-col items-center space-y-12">
          
          <!-- Node 1 -->
          <div class="w-48 bg-[var(--color-surface)] rounded-xl shadow-lg border border-[var(--color-border)] p-1 relative group hover:border-red-500/50 transition-colors">
            <div class="h-8 bg-[var(--color-background)] rounded-t-lg flex items-center px-3 border-b border-[var(--color-border)]">
              <CameraIcon class="w-4 h-4 text-gray-400 mr-2" />
              <span class="text-xs font-bold text-[var(--color-text)]">Image Acquisition</span>
            </div>
            <div class="p-3">
              <div class="text-[10px] text-[var(--color-text-muted)] mb-1">Source: CAM_01</div>
              <div class="text-[10px] text-[var(--color-text-muted)]">Exp: 50ms</div>
            </div>
            <div class="absolute -bottom-1.5 left-1/2 transform -translate-x-1/2 w-3 h-3 bg-[var(--color-surface)] border border-gray-300 dark:border-gray-600 rounded-full z-10"></div>
          </div>
          
          <!-- Node 2 -->
          <div class="w-48 bg-[var(--color-surface)] rounded-xl shadow-lg border border-[var(--color-border)] p-1 relative group hover:border-red-500/50 transition-colors">
            <div class="absolute -top-1.5 left-1/2 transform -translate-x-1/2 w-3 h-3 bg-[var(--color-surface)] border border-gray-300 dark:border-gray-600 rounded-full z-10"></div>
            <div class="h-8 bg-[var(--color-background)] rounded-t-lg flex items-center px-3 border-b border-[var(--color-border)]">
              <SlidersHorizontalIcon class="w-4 h-4 text-gray-400 mr-2" />
              <span class="text-xs font-bold text-[var(--color-text)]">Preprocessing</span>
            </div>
            <div class="p-3">
              <div class="text-[10px] text-[var(--color-text-muted)] mb-1">Filter: Contrast</div>
              <div class="text-[10px] text-[var(--color-text-muted)]">Kernel: 3x3</div>
            </div>
            <div class="absolute -bottom-1.5 left-1/2 transform -translate-x-1/2 w-3 h-3 bg-[var(--color-surface)] border border-gray-300 dark:border-gray-600 rounded-full z-10"></div>
          </div>
          
          <!-- Branching Nodes -->
          <div class="flex space-x-12 relative">
            <div class="absolute -top-12 left-1/2 transform -translate-x-1/2 w-0.5 h-12 bg-gray-300 dark:bg-gray-600"></div>
            <div class="absolute -top-6 left-1/2 transform -translate-x-1/2 w-64 h-6 border-t-2 border-l-2 border-r-2 border-gray-300 dark:border-gray-600 rounded-t-xl pointer-events-none"></div>
            
            <div class="w-48 bg-[var(--color-surface)] rounded-xl shadow-lg border border-[var(--color-border)] p-1 relative group hover:border-red-500/50 transition-colors ring-2 ring-red-500 ring-offset-2 ring-offset-[var(--color-background)]">
              <div class="absolute -top-1.5 left-1/2 transform -translate-x-1/2 w-3 h-3 bg-[var(--color-surface)] border border-gray-300 dark:border-gray-600 rounded-full z-10"></div>
              <div class="h-8 bg-red-500/10 rounded-t-lg flex items-center px-3 border-b border-red-500/10">
                <TypeIcon class="w-4 h-4 text-red-500 mr-2" />
                <span class="text-xs font-bold text-[var(--color-text)]">OCR Recognition</span>
              </div>
              <div class="p-3">
                <div class="text-[10px] text-[var(--color-text-muted)] mb-1">Target: SN Code</div>
                <div class="text-[10px] text-[var(--color-text-muted)]">Mode: Auto</div>
              </div>
            </div>
            
            <div class="w-48 bg-[var(--color-surface)] rounded-xl shadow-lg border border-[var(--color-border)] p-1 relative group hover:border-red-500/50 transition-colors ring-2 ring-red-500 ring-offset-2 ring-offset-[var(--color-background)]">
              <div class="absolute -top-1.5 left-1/2 transform -translate-x-1/2 w-3 h-3 bg-[var(--color-surface)] border border-gray-300 dark:border-gray-600 rounded-full z-10"></div>
              <div class="h-8 bg-red-500/10 rounded-t-lg flex items-center px-3 border-b border-red-500/10">
                <RulerIcon class="w-4 h-4 text-red-500 mr-2" />
                <span class="text-xs font-bold text-[var(--color-text)]">Measurement</span>
              </div>
              <div class="p-3">
                <div class="text-[10px] text-[var(--color-text-muted)] mb-1">Type: Distance</div>
                <div class="text-[10px] text-[var(--color-text-muted)]">Tol: 10-15mm</div>
              </div>
            </div>
          </div>
          
        </div>

        <svg class="absolute inset-0 pointer-events-none w-full h-full overflow-visible z-0">
          <path d="M 50% 190 L 50% 240" fill="none" stroke="#D1D5DB" stroke-width="2"></path>
          <path d="M 50% 320 L 50% 370" fill="none" stroke="#D1D5DB" stroke-width="2"></path>
        </svg>
      </div>
    </div>

    <!-- Floating Action Button -->
    <div class="absolute bottom-8 left-1/2 transform -translate-x-1/2 z-20">
      <button class="bg-red-500 hover:bg-red-600 text-white px-6 py-3 rounded-full shadow-lg flex items-center space-x-2 transition-transform hover:-translate-y-1 font-medium">
        <CheckSquareIcon class="w-5 h-5" />
        <span>应用到流程 (Apply to Flow)</span>
      </button>
    </div>
  </section>
</template>

<script setup lang="ts">
import { 
  ZoomInIcon, 
  ZoomOutIcon, 
  MaximizeIcon,
  CameraIcon,
  SlidersHorizontalIcon,
  TypeIcon,
  RulerIcon,
  CheckSquareIcon
} from 'lucide-vue-next';
</script>

<style scoped>
.bg-dots {
  background-image: radial-gradient(var(--color-border) 1px, transparent 1px);
  background-size: 20px 20px;
}
</style>
