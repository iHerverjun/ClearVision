<template>
  <aside
    class="w-80 bg-[var(--color-surface)] border-r border-[var(--color-border)] flex flex-col z-10 shadow-sm flex-shrink-0"
  >
    <div class="p-5 pb-3">
      <h2 class="text-sm font-bold text-[var(--color-text)] uppercase tracking-wider mb-4">
        工程列表
      </h2>

      <div class="relative mb-3 flex items-center">
        <span class="absolute left-3 flex items-center pointer-events-none text-[var(--color-text-muted)]">
          <SearchIcon class="w-4 h-4" />
        </span>
        <input
          v-model="searchKeyword"
          class="block w-full pl-9 pr-3 py-2 border border-[var(--color-border)] rounded-lg leading-5 bg-[var(--color-background)] placeholder-[var(--color-text-muted)] focus:outline-none focus:ring-1 focus:ring-red-500 focus:border-red-500 sm:text-sm text-[var(--color-text)] transition-shadow"
          placeholder="搜索工程..."
          type="text"
        />
      </div>

      <div class="flex overflow-x-auto hide-scrollbar space-x-2 pb-1">
        <button
          v-for="tag in tags"
          :key="tag.id"
          class="flex-shrink-0 px-3 py-1 text-xs font-medium rounded-full border transition-colors"
          :class="activeTag === tag.id
            ? 'bg-red-500 text-white border-red-500'
            : 'bg-[var(--color-background)] text-[var(--color-text-muted)] border-[var(--color-border)] hover:border-red-500 hover:text-red-500'"
          @click="activeTag = tag.id"
        >
          {{ tag.label }}
        </button>
      </div>
    </div>

    <div class="flex-1 overflow-y-auto px-3 py-2 space-y-3">
      <div
        v-if="projectsStore.isLoading"
        class="text-xs text-[var(--color-text-muted)] text-center py-6"
      >
        正在加载工程...
      </div>

      <div
        v-else-if="filteredProjects.length === 0"
        class="text-xs text-[var(--color-text-muted)] text-center py-6"
      >
        未找到工程。
      </div>

      <div
        v-for="project in filteredProjects"
        :key="project.id"
        class="group flex items-center justify-between p-3 bg-[var(--color-surface)] border rounded-lg hover:border-red-500 hover:shadow-md transition-all shadow-sm cursor-pointer relative overflow-hidden"
        :class="project.id === projectsStore.currentProject?.id ? 'border-red-500' : 'border-[var(--color-border)]'"
        @click="projectsStore.selectProject(project)"
      >
        <div class="flex items-start overflow-hidden flex-1">
          <div class="w-10 h-10 rounded flex items-center justify-center flex-shrink-0 mt-0.5 bg-red-50 text-red-500">
            <component :is="iconFor(project.type)" class="w-5 h-5" />
          </div>
          <div class="ml-3 min-w-0 flex flex-col flex-1">
            <div class="flex items-center gap-2 mb-0.5">
              <p class="text-sm font-bold text-[var(--color-text)] truncate">{{ project.name }}</p>
            </div>
            <div class="flex items-center gap-2">
              <span
                class="inline-flex items-center rounded-sm px-1.5 py-0.5 text-[9px] font-medium ring-1 ring-inset whitespace-nowrap bg-red-500/10 text-red-600 ring-red-500/30"
              >
                {{ project.type }}
              </span>
              <p class="text-[10px] text-[var(--color-text-muted)] truncate">{{ formatTime(project.updatedAt) }}</p>
            </div>
          </div>
        </div>

        <div class="flex flex-col space-y-1 opacity-0 group-hover:opacity-100 transition-opacity absolute right-2 top-2 bottom-2 justify-center bg-[var(--color-surface)] pl-2">
          <button
            class="text-[10px] bg-red-500 text-white px-2 py-1 rounded hover:bg-red-600 shadow-sm mb-1 whitespace-nowrap"
            @click.stop="openProject(project.id)"
          >
            打开
          </button>
          <button
            class="text-[10px] text-red-500 hover:text-red-700 px-1 text-center whitespace-nowrap"
            @click.stop="removeProject(project.id)"
          >
            删除
          </button>
        </div>
      </div>
    </div>
  </aside>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { SearchIcon, FileTextIcon, CpuIcon, RulerIcon } from 'lucide-vue-next';
import { useProjectsStore } from '../../stores/projects';

const router = useRouter();
const projectsStore = useProjectsStore();

const searchKeyword = ref('');
const activeTag = ref<'all' | 'pcb' | 'measure'>('all');

const tags = [
  { id: 'all' as const, label: '全部' },
  { id: 'pcb' as const, label: 'PCB' },
  { id: 'measure' as const, label: '测量' },
];

const normalizedType = (type: string) => type.toLowerCase();

const filteredProjects = computed(() => {
  const keyword = searchKeyword.value.trim().toLowerCase();
  return projectsStore.projects.filter((project) => {
    const type = normalizedType(project.type);
    const hitKeyword =
      !keyword ||
      project.name.toLowerCase().includes(keyword) ||
      (project.description || '').toLowerCase().includes(keyword);

    if (!hitKeyword) {
      return false;
    }

    if (activeTag.value === 'all') {
      return true;
    }

    if (activeTag.value === 'pcb') {
      return type.includes('pcb');
    }

    return type.includes('measure') || type.includes('measurement');
  });
});

const iconFor = (type: string) => {
  const normalized = normalizedType(type);
  if (normalized.includes('pcb')) return CpuIcon;
  if (normalized.includes('measure')) return RulerIcon;
  return FileTextIcon;
};

const formatTime = (isoTime: string) => {
  const target = new Date(isoTime);
  if (Number.isNaN(target.getTime())) {
    return '--';
  }

  return target.toLocaleString();
};

const openProject = async (projectId: string) => {
  try {
    await projectsStore.openProject(projectId);
    await router.push({ name: 'FlowEditor' });
  } catch (error) {
    console.error('[ProjectSidebar] Failed to open project:', error);
  }
};

const removeProject = async (projectId: string) => {
  const confirmed = window.confirm('确定删除该工程吗？');
  if (!confirmed) {
    return;
  }
  try {
    await projectsStore.deleteProject(projectId);
  } catch (error) {
    console.error('[ProjectSidebar] Failed to delete project:', error);
  }
};

onMounted(async () => {
  await projectsStore.loadProjects();
});
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
