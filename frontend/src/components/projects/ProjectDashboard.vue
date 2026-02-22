<template>
  <section class="flex-1 relative bg-[var(--color-background)] overflow-hidden flex flex-col">
    <!-- Header -->
    <div class="w-full h-20 bg-[var(--color-surface)]/80 backdrop-blur-sm border-b border-[var(--color-border)] flex items-center justify-between px-8 z-10 flex-shrink-0">
      <div class="min-w-0 pr-4">
        <h1 class="text-2xl font-bold text-[var(--color-text)] truncate">工作区 Dashboard</h1>
        <p class="text-xs text-[var(--color-text-muted)] mt-1 truncate">Manage and configure your vision projects.</p>
      </div>
      <div class="flex items-center space-x-4 flex-shrink-0">
        <button class="flex items-center px-4 py-2.5 bg-[var(--color-surface)] border border-[var(--color-border)] rounded-lg shadow-sm hover:bg-gray-50 dark:hover:bg-gray-700 text-[var(--color-text)] transition-all font-medium whitespace-nowrap">
          <DownloadIcon class="w-5 h-5 mr-2 text-red-500" />
          导出 JSON
        </button>
        <button class="flex items-center px-5 py-2.5 bg-red-500 hover:bg-red-600 text-white rounded-lg shadow-lg shadow-red-200 dark:shadow-none transition-all font-bold tracking-wide whitespace-nowrap">
          <PlusIcon class="w-5 h-5 mr-2" />
          新建工程
        </button>
      </div>
    </div>
    
    <div class="flex-1 overflow-auto p-8 relative z-0">
      <!-- Stats Row -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-8">
        <div v-for="stat in systemStats" :key="stat.id" class="bg-[var(--color-surface)] rounded-xl border border-[var(--color-border)] p-6 shadow-sm">
          <div class="flex justify-between items-start mb-4">
            <div :class="['w-10 h-10 rounded-lg flex items-center justify-center', stat.iconBgClass, stat.iconColorClass]">
              <component :is="stat.icon" class="w-6 h-6" />
            </div>
            <span class="text-xs font-semibold text-[var(--color-text-muted)] bg-[var(--color-background)] px-2 py-1 rounded">{{ stat.badge }}</span>
          </div>
          <div class="text-3xl font-bold text-[var(--color-text)] mb-1">{{ stat.value }}</div>
          <div class="text-sm text-[var(--color-text-muted)]">{{ stat.label }}</div>
        </div>
      </div>
      
      <!-- Recent Activity -->
      <div class="bg-[var(--color-surface)] rounded-xl border border-[var(--color-border)] shadow-sm overflow-hidden mb-8">
        <div class="px-6 py-4 border-b border-[var(--color-border)] flex justify-between items-center bg-[var(--color-background)]">
          <h3 class="font-bold text-[var(--color-text)]">最近活动 (Recent Activity)</h3>
          <button class="text-xs text-red-500 font-medium hover:underline flex-shrink-0 ml-4">查看全部</button>
        </div>
        <div class="p-6">
          <div class="flex flex-col space-y-6">
            <div v-for="activity in recentActivities" :key="activity.id" class="flex items-start">
              <div :class="['w-2 h-2 mt-2 rounded-full mr-4 flex-shrink-0', activity.dotClass]"></div>
              <div class="min-w-0 flex-1">
                <p class="text-sm font-medium text-[var(--color-text)] truncate">{{ activity.title }}</p>
                <p class="text-xs text-[var(--color-text-muted)] mt-1 truncate">{{ activity.time }} • {{ activity.description }}</p>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Dropzone -->
      <div class="flex items-center justify-center p-12 border-2 border-dashed border-[var(--color-border)] bg-[var(--color-surface)]/50 rounded-xl hover:bg-[var(--color-surface)] transition-colors cursor-pointer">
        <div class="text-center flex flex-col items-center">
          <UploadCloudIcon class="w-10 h-10 text-gray-400 mb-2" />
          <p class="text-[var(--color-text-muted)] text-sm">Drag and drop project files here to import</p>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { 
  DownloadIcon, 
  PlusIcon,
  CheckCircleIcon,
  GaugeIcon,
  HardDriveIcon,
  UploadCloudIcon
} from 'lucide-vue-next';

interface SystemStat {
  id: string;
  label: string;
  value: string;
  badge: string;
  icon: any;
  iconBgClass: string;
  iconColorClass: string;
}

interface ActivityLog {
  id: string;
  title: string;
  time: string;
  description: string;
  dotClass: string;
}

const systemStats = ref<SystemStat[]>([
  {
    id: 'stat_yield',
    label: '平均良率 (Average Yield)',
    value: '98.5%',
    badge: 'Last 24h',
    icon: CheckCircleIcon,
    iconBgClass: 'bg-green-50 dark:bg-green-900/20',
    iconColorClass: 'text-green-500'
  },
  {
    id: 'stat_cycle',
    label: '平均处理时间 (Avg Cycle Time)',
    value: '45ms',
    badge: 'Realtime',
    icon: GaugeIcon,
    iconBgClass: 'bg-blue-50 dark:bg-blue-900/20',
    iconColorClass: 'text-blue-500'
  },
  {
    id: 'stat_storage',
    label: '已用存储 (Storage Used)',
    value: '1.2 GB',
    badge: 'Local',
    icon: HardDriveIcon,
    iconBgClass: 'bg-purple-50 dark:bg-purple-900/20',
    iconColorClass: 'text-purple-500'
  }
]);

const recentActivities = ref<ActivityLog[]>([
  {
    id: 'act_1',
    title: '项目 "Vision_Insp_01" 已保存',
    time: '10 minutes ago',
    description: 'by Operator',
    dotClass: 'bg-red-500'
  },
  {
    id: 'act_2',
    title: '系统自检完成',
    time: '1 hour ago',
    description: 'All systems nominal',
    dotClass: 'bg-gray-300 dark:bg-gray-600'
  },
  {
    id: 'act_3',
    title: '导出项目配置 JSON',
    time: 'Yesterday at 16:20',
    description: 'System Export',
    dotClass: 'bg-gray-300 dark:bg-gray-600'
  }
]);
</script>

<style scoped>
</style>
