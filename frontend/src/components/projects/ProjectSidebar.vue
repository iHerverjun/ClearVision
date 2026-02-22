<template>
  <aside class="w-80 bg-[var(--color-surface)] border-r border-[var(--color-border)] flex flex-col z-10 shadow-sm flex-shrink-0">
    <div class="p-5 pb-3">
      <h2 class="text-sm font-bold text-[var(--color-text)] uppercase tracking-wider mb-4">工程列表 (PROJECTS)</h2>
      <div class="relative mb-3 flex items-center">
        <span class="absolute left-3 flex items-center pointer-events-none text-[var(--color-text-muted)]">
          <SearchIcon class="w-4 h-4" />
        </span>
        <input class="block w-full pl-9 pr-3 py-2 border border-[var(--color-border)] rounded-lg leading-5 bg-[var(--color-background)] placeholder-[var(--color-text-muted)] focus:outline-none focus:ring-1 focus:ring-red-500 focus:border-red-500 sm:text-sm text-[var(--color-text)] transition-shadow" placeholder="搜索工程..." type="text"/>
      </div>
      <div class="flex overflow-x-auto hide-scrollbar space-x-2 pb-1">
        <button class="flex-shrink-0 px-3 py-1 text-xs font-medium rounded-full bg-red-500 text-white border border-red-500 transition-colors">
          全部
        </button>
        <button class="flex-shrink-0 px-3 py-1 text-xs font-medium rounded-full bg-[var(--color-background)] text-[var(--color-text-muted)] border border-[var(--color-border)] hover:border-red-500 hover:text-red-500 transition-colors">
          PCB检测
        </button>
        <button class="flex-shrink-0 px-3 py-1 text-xs font-medium rounded-full bg-[var(--color-background)] text-[var(--color-text-muted)] border border-[var(--color-border)] hover:border-red-500 hover:text-red-500 transition-colors">
          测量
        </button>
      </div>
    </div>
    
    <div class="flex-1 overflow-y-auto px-3 py-2 space-y-3">
      <!-- Dynamic Project Items -->
      <div 
        v-for="project in projects" 
        :key="project.id"
        class="group flex items-center justify-between p-3 bg-[var(--color-surface)] border border-[var(--color-border)] rounded-lg hover:border-red-500 hover:shadow-md transition-all shadow-sm cursor-pointer relative overflow-hidden"
      >
        <div class="flex items-start overflow-hidden flex-1">
          <div :class="['w-10 h-10 rounded flex items-center justify-center flex-shrink-0 mt-0.5', project.iconBgColor, project.iconTextColor]">
            <component :is="project.icon" class="w-5 h-5" />
          </div>
          <div class="ml-3 min-w-0 flex flex-col flex-1">
             <div class="flex items-center gap-2 mb-0.5">
              <p class="text-sm font-bold text-[var(--color-text)] truncate">{{ project.name }}</p>
             </div>
             <div class="flex items-center gap-2">
               <span :class="['inline-flex items-center rounded-sm px-1.5 py-0.5 text-[9px] font-medium ring-1 ring-inset whitespace-nowrap', project.tagBgColor, project.tagTextColor, project.tagRingColor]">
                 {{ project.type }}
               </span>
               <p class="text-[10px] text-[var(--color-text-muted)] truncate">{{ project.time }}</p>
             </div>
          </div>
        </div>
        <div class="flex flex-col space-y-1 opacity-0 group-hover:opacity-100 transition-opacity absolute right-2 top-2 bottom-2 justify-center bg-[var(--color-surface)] pl-2">
          <button class="text-[10px] bg-red-500 text-white px-2 py-1 rounded hover:bg-red-600 shadow-sm mb-1 whitespace-nowrap">打开</button>
          <button class="text-[10px] text-red-500 hover:text-red-700 px-1 text-center whitespace-nowrap">删除</button>
        </div>
      </div>
    </div>
  </aside>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { SearchIcon, FileTextIcon, TypeIcon } from 'lucide-vue-next';

interface ProjectItem {
  id: string;
  name: string;
  type: string;
  time: string;
  icon: any;
  iconBgColor: string;
  iconTextColor: string;
  tagBgColor: string;
  tagTextColor: string;
  tagRingColor: string;
}

const projects = ref<ProjectItem[]>([
  {
    id: 'proj_01',
    name: 'Vision_Insp_01',
    type: '测量',
    time: '14:30',
    icon: FileTextIcon,
    iconBgColor: 'bg-blue-50 dark:bg-blue-900/20',
    iconTextColor: 'text-blue-500',
    tagBgColor: 'bg-purple-50 dark:bg-purple-900/30',
    tagTextColor: 'text-purple-700 dark:text-purple-400',
    tagRingColor: 'ring-purple-600/20'
  },
  {
    id: 'proj_02',
    name: 'Label_OCR_Final',
    type: 'OCR',
    time: '10-18',
    icon: TypeIcon,
    iconBgColor: 'bg-orange-50 dark:bg-orange-900/20',
    iconTextColor: 'text-orange-500',
    tagBgColor: 'bg-orange-50 dark:bg-orange-900/30',
    tagTextColor: 'text-orange-700 dark:text-orange-400',
    tagRingColor: 'ring-orange-600/20'
  }
]);
</script>

<style scoped>
.hide-scrollbar::-webkit-scrollbar {
  display: none;
}
.hide-scrollbar {
  -ms-overflow-style: none;
  scrollbar-width: none;
}
</style>
