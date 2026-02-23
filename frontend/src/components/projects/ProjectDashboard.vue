<template>
  <section class="flex-1 relative bg-[var(--color-background)] overflow-hidden flex flex-col">
    <div
      class="w-full h-20 bg-[var(--color-surface)]/80 backdrop-blur-sm border-b border-[var(--color-border)] flex items-center justify-between px-8 z-10 flex-shrink-0"
    >
      <div class="min-w-0 pr-4">
        <h1 class="text-2xl font-bold text-[var(--color-text)] truncate">工作台总览</h1>
        <p class="text-xs text-[var(--color-text-muted)] mt-1 truncate">
          管理工程并查看最近系统活动。
        </p>
      </div>
      <div class="flex items-center space-x-4 flex-shrink-0">
        <button
          class="flex items-center px-4 py-2.5 bg-[var(--color-surface)] border border-[var(--color-border)] rounded-lg shadow-sm hover:bg-gray-50 dark:hover:bg-gray-700 text-[var(--color-text)] transition-all font-medium whitespace-nowrap"
          @click="exportCurrentFlow"
        >
          <DownloadIcon class="w-5 h-5 mr-2 text-red-500" />
          导出 JSON
        </button>
        <button
          class="flex items-center px-5 py-2.5 bg-red-500 hover:bg-red-600 text-white rounded-lg shadow-lg shadow-red-200 dark:shadow-none transition-all font-bold tracking-wide whitespace-nowrap"
          @click="createProjectAndOpen"
        >
          <PlusIcon class="w-5 h-5 mr-2" />
          新建工程
        </button>
      </div>
    </div>

    <div class="flex-1 overflow-auto p-8 relative z-0">
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-8">
        <div
          v-for="stat in systemStats"
          :key="stat.id"
          class="bg-[var(--color-surface)] rounded-xl border border-[var(--color-border)] p-6 shadow-sm"
        >
          <div class="flex justify-between items-start mb-4">
            <div :class="['w-10 h-10 rounded-lg flex items-center justify-center', stat.iconBgClass, stat.iconColorClass]">
              <component :is="stat.icon" class="w-6 h-6" />
            </div>
            <span class="text-xs font-semibold text-[var(--color-text-muted)] bg-[var(--color-background)] px-2 py-1 rounded">
              {{ stat.badge }}
            </span>
          </div>
          <div class="text-3xl font-bold text-[var(--color-text)] mb-1">{{ stat.value }}</div>
          <div class="text-sm text-[var(--color-text-muted)]">{{ stat.label }}</div>
        </div>
      </div>

      <div class="bg-[var(--color-surface)] rounded-xl border border-[var(--color-border)] shadow-sm overflow-hidden mb-8">
        <div class="px-6 py-4 border-b border-[var(--color-border)] flex justify-between items-center bg-[var(--color-background)]">
          <h3 class="font-bold text-[var(--color-text)]">最近活动</h3>
          <button class="text-xs text-red-500 font-medium hover:underline flex-shrink-0 ml-4" @click="viewAllActivities">
            查看全部
          </button>
        </div>
        <div class="p-6">
          <div v-if="statsStore.activities.length === 0" class="text-xs text-[var(--color-text-muted)]">
            暂无活动记录。
          </div>
          <div v-else class="flex flex-col space-y-6">
            <div v-for="activity in statsStore.activities.slice(0, 8)" :key="activity.id" class="flex items-start">
              <div class="w-2 h-2 mt-2 rounded-full mr-4 flex-shrink-0 bg-red-500"></div>
              <div class="min-w-0 flex-1">
                <p class="text-sm font-medium text-[var(--color-text)] truncate">{{ activity.title }}</p>
                <p class="text-xs text-[var(--color-text-muted)] mt-1 truncate">
                  {{ formatActivityTime(activity.timestamp) }} - {{ activity.description }}
                </p>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div
        class="flex items-center justify-center p-12 border-2 border-dashed border-[var(--color-border)] bg-[var(--color-surface)]/50 rounded-xl hover:bg-[var(--color-surface)] transition-colors cursor-pointer"
        @dragover.prevent
        @drop.prevent="onDropProjectFile"
      >
        <div class="text-center flex flex-col items-center">
          <UploadCloudIcon class="w-10 h-10 text-gray-400 mb-2" />
          <p class="text-[var(--color-text-muted)] text-sm">
            拖拽工程 JSON 到此处导入
          </p>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import {
  DownloadIcon,
  PlusIcon,
  CheckCircleIcon,
  GaugeIcon,
  HardDriveIcon,
  UploadCloudIcon,
} from 'lucide-vue-next';
import { useFlowStore } from '../../stores/flow';
import { useProjectsStore } from '../../stores/projects';
import { useSystemStatsStore } from '../../stores/systemStats';

interface DashboardStat {
  id: string;
  label: string;
  value: string;
  badge: string;
  icon: any;
  iconBgClass: string;
  iconColorClass: string;
}

const router = useRouter();
const flowStore = useFlowStore();
const projectsStore = useProjectsStore();
const statsStore = useSystemStatsStore();

const systemStats = computed<DashboardStat[]>(() => {
  const stats = statsStore.dashboardStats;
  return [
    {
      id: 'stat-yield',
      label: '平均良率',
      value: stats ? `${stats.averageYield.toFixed(1)}%` : '--',
      badge: '近 24 小时',
      icon: CheckCircleIcon,
      iconBgClass: 'bg-green-50 dark:bg-green-900/20',
      iconColorClass: 'text-green-500',
    },
    {
      id: 'stat-cycle',
      label: '平均节拍',
      value: stats ? `${Math.round(stats.averageCycleTimeMs)}ms` : '--',
      badge: '实时',
      icon: GaugeIcon,
      iconBgClass: 'bg-blue-50 dark:bg-blue-900/20',
      iconColorClass: 'text-blue-500',
    },
    {
      id: 'stat-storage',
      label: '存储占用',
      value: stats ? `${stats.storageUsedGb.toFixed(1)} GB` : '--',
      badge: '本地',
      icon: HardDriveIcon,
      iconBgClass: 'bg-purple-50 dark:bg-purple-900/20',
      iconColorClass: 'text-purple-500',
    },
  ];
});

const exportCurrentFlow = () => {
  const legacyData = flowStore.buildLegacyProject();
  const blob = new Blob([JSON.stringify(legacyData, null, 2)], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = `ClearVision_${Date.now()}.json`;
  link.click();
  URL.revokeObjectURL(url);
};

const createProjectAndOpen = async () => {
  try {
    const projectName = `工程_${new Date().toISOString().slice(0, 19).replace(/[T:]/g, '')}`;
    const created = await projectsStore.createProject(projectName, '通用', '由工作台创建');
    await projectsStore.openProject(created.id);
    await router.push({ name: 'FlowEditor' });
  } catch (error) {
    console.error('[ProjectDashboard] Failed to create project:', error);
  }
};

const viewAllActivities = async () => {
  await router.push({ name: 'Results' });
};

const onDropProjectFile = async (event: DragEvent) => {
  const file = event.dataTransfer?.files?.[0];
  if (!file) {
    return;
  }

  try {
    const text = await file.text();
    const parsed = JSON.parse(text);
    flowStore.loadLegacyProject(parsed);
    await router.push({ name: 'FlowEditor' });
  } catch (error) {
    console.error('[ProjectDashboard] Failed to import project:', error);
    window.alert('工程 JSON 文件无效。');
  }
};

const formatActivityTime = (timestamp: string) => {
  const date = new Date(timestamp);
  if (Number.isNaN(date.getTime())) {
    return '--';
  }
  return date.toLocaleString();
};

onMounted(async () => {
  await Promise.all([
    statsStore.loadDashboardStats(),
    statsStore.loadActivities(10),
  ]);
});
</script>
