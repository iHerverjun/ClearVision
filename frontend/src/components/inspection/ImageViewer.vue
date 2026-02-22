<template>
  <section class="flex-1 relative bg-gray-900 overflow-hidden flex flex-col">
    <!-- Vignette overlay for NG state -->
    <div class="absolute inset-0 z-0 pointer-events-none" :class="{ 'vignette-red': isNg }"></div>
    
    <!-- Floating Toolbar -->
    <div class="absolute top-6 left-6 flex flex-col bg-white/10 backdrop-blur-md rounded-lg shadow-[0_10px_15px_-3px_rgba(0,0,0,0.1),0_4px_6px_-2px_rgba(0,0,0,0.05)] border border-white/10 z-10">
      <button class="p-2.5 text-white/70 hover:text-white hover:bg-white/10 rounded-t-lg border-b border-white/10 transition-colors">
        <PlusIcon class="w-4 h-4" />
      </button>
      <button class="p-2.5 text-white/70 hover:text-white hover:bg-white/10 border-b border-white/10 transition-colors">
        <MinusIcon class="w-4 h-4" />
      </button>
      <button class="p-2.5 text-white/70 hover:text-white hover:bg-white/10 border-b border-white/10 transition-colors">
        <FocusIcon class="w-4 h-4" />
      </button>
      <button class="p-2.5 text-white/70 hover:text-white hover:bg-white/10 rounded-b-lg transition-colors">
        <LockOpenIcon class="w-4 h-4" />
      </button>
    </div>

    <!-- Main Image Canvas area -->
    <div class="flex-1 flex items-center justify-center relative overflow-hidden group">
      <!-- Dynamic Image (Camera feed or Fallback) -->
      <img alt="Industrial Part Inspection" class="max-w-full max-h-full object-contain opacity-90 shadow-2xl relative z-0" :src="currentImage" />
      
      <!-- Defect Overlay (Only visible when NG) -->
      <div v-if="isNg" class="absolute top-1/2 left-1/2 transform -translate-x-1/2 -translate-y-1/2 w-48 h-48 border-4 border-red-500 shadow-[0_0_20px_rgba(239,68,68,0.6)] flex flex-col items-start z-10">
        <div class="bg-red-500 text-white text-xs font-bold px-3 py-1.5 flex items-center space-x-1.5 shadow-md animate-pulse">
          <AlertCircleIcon class="w-3.5 h-3.5" />
          <span>NG: Defect Detected</span>
        </div>
        <!-- Anchor points -->
        <div class="absolute -top-1.5 -left-1.5 w-4 h-4 border-t-4 border-l-4 border-red-500"></div>
        <div class="absolute -top-1.5 -right-1.5 w-4 h-4 border-t-4 border-r-4 border-red-500"></div>
        <div class="absolute -bottom-1.5 -left-1.5 w-4 h-4 border-b-4 border-l-4 border-red-500"></div>
        <div class="absolute -bottom-1.5 -right-1.5 w-4 h-4 border-b-4 border-r-4 border-red-500"></div>
      </div>

      <!-- Bottom Right Camera Details -->
      <div class="absolute bottom-6 right-6 backdrop-blur text-white px-4 py-2 rounded-lg text-xs font-mono border z-10" :class="isNg ? 'bg-red-900/80 border-red-500/30' : 'bg-gray-900/80 border-white/10'">
        <div class="flex items-center space-x-4">
          <span class="font-bold" :class="isNg ? 'text-red-100' : 'text-gray-200'">CAM_01 {{ isNg ? '[ALARM]' : '' }}</span>
          <span class="text-white/50">|</span>
          <span>2448x2048</span>
          <span class="text-white/50">|</span>
          <span>50ms</span>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { 
  PlusIcon, 
  MinusIcon, 
  FocusIcon, 
  LockOpenIcon, 
  AlertCircleIcon 
} from 'lucide-vue-next';
import { useExecutionStore } from '../../stores/execution';

const executionStore = useExecutionStore();

// Dynamically compute the NG state based on the last overall inspection result
const isNg = computed(() => executionStore.lastInspectionResult === 'Fail');

// If there's a live camera image, show it. Otherwise fall back to the mock or clear image.
const currentImage = computed(() => {
  return executionStore.latestCameraImage || 'https://lh3.googleusercontent.com/aida-public/AB6AXuADuwSQlGt9iEphOpgcITr8cDvD3JLuZo5yhsu-1Q6OuHyOmZW80DWGWJ8gzFe9u87NXIQwK7x2qYlivLzg5b-rD5eVV03z0rE3AeS7qqgX95LZ-gBL9XPWbWwWjGBrzTluWPjeTP1Ka7_6s1aa-0gs1CgONrDmWEU6nrPDzhooqlxjrmTWxDLMNDHTOee9ZFMhMbMBtghrPBsTPVggWceKAUMU6XKkrcZfO41-PDkq9Zzhj5CAh0idfhAS01Fz_FIRhO5ois-EGnIo';
});
</script>

<style scoped>
.vignette-red {
  background: radial-gradient(circle, transparent 50%, rgba(239, 68, 68, 0.15) 100%);
}
</style>
