<template>
  <header class="app-header">
    <div class="header-left">
      <div class="logo">
        <span class="logo-text">Clear<span class="highlight">Vision</span></span>
      </div>

      <div class="project-switcher-wrapper">
        <GlassCard class="project-switcher" interactive @click="toggleProjectMenu">
          <FolderIcon class="icon-sm" />
          <div class="project-info">
            <span class="project-name">{{ currentProjectName }}</span>
            <span class="project-status">{{ currentProjectStatus }}</span>
          </div>
          <ChevronDownIcon class="icon-sm text-muted" />
        </GlassCard>

        <div v-if="isProjectMenuOpen" class="project-menu">
          <button
            v-for="project in projectsStore.projects"
            :key="project.id"
            class="project-menu-item"
            :class="{ active: project.id === projectsStore.currentProject?.id }"
            @click="openProject(project.id)"
          >
            <span class="project-menu-name">{{ project.name }}</span>
            <span class="project-menu-meta">{{ project.type }}</span>
          </button>
          <div v-if="projectsStore.projects.length === 0" class="project-menu-empty">
            暂无工程数据
          </div>
        </div>
      </div>
    </div>

    <div class="header-center">
      <GlassCard class="execution-dashboard">
        <div class="play-btn-wrapper" :class="{ 'is-running': executionStore.isRunning }">
          <svg class="progress-ring" width="40" height="40" v-if="executionStore.isRunning">
            <circle
              class="progress-ring__circle"
              stroke="rgba(232, 72, 85, 0.2)"
              stroke-width="2"
              fill="transparent"
              r="18"
              cx="20"
              cy="20"
            />
            <circle
              class="progress-ring__circle progress-ring__value"
              stroke="#e84855"
              stroke-width="2"
              fill="transparent"
              r="18"
              cx="20"
              cy="20"
              style="stroke-dasharray: 113; stroke-dashoffset: 40"
            />
          </svg>
          <IconButton
            :variant="executionStore.isRunning ? 'danger' : 'primary'"
            size="md"
            title="运行流程"
            @click="toggleRun"
          >
            <SquareIcon v-if="executionStore.isRunning" />
            <PlayIcon v-else />
          </IconButton>
        </div>

        <div class="execution-stats">
          <span class="stat-value">{{ cycleTimeText }}<span class="stat-unit">ms</span></span>
          <span class="stat-label">节拍</span>
        </div>

        <div class="divider"></div>

        <div class="execution-stats">
          <span class="stat-value success">{{ yieldRateText }}<span class="stat-unit">%</span></span>
          <span class="stat-label">良率</span>
        </div>
      </GlassCard>
    </div>

    <div class="header-right">
      <div class="actions-group">
        <input
          type="file"
          ref="fileInputRef"
          accept=".json"
          style="display: none"
          @change="onFileImport"
        />

        <IconButton title="导入旧版 JSON（Ctrl+O）" @click="triggerFileInput">
          <UploadIcon />
        </IconButton>

        <IconButton title="保存工程（Ctrl+S）" @click="saveProject">
          <SaveIcon />
        </IconButton>

        <IconButton title="撤销（Ctrl+Z）" :disabled="!flowStore.canUndo" @click="flowStore.undo">
          <UndoIcon />
        </IconButton>

        <IconButton title="重做（Ctrl+Y）" :disabled="!flowStore.canRedo" @click="flowStore.redo">
          <RedoIcon />
        </IconButton>
      </div>
    </div>
  </header>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import {
  FolderIcon,
  ChevronDownIcon,
  PlayIcon,
  SquareIcon,
  SaveIcon,
  UndoIcon,
  RedoIcon,
  UploadIcon,
} from 'lucide-vue-next';
import GlassCard from '../shared/GlassCard.vue';
import IconButton from '../shared/IconButton.vue';
import { useFlowStore } from '../../stores/flow';
import { useExecutionStore } from '../../stores/execution';
import { useProjectsStore } from '../../stores/projects';

const router = useRouter();
const flowStore = useFlowStore();
const executionStore = useExecutionStore();
const projectsStore = useProjectsStore();

const fileInputRef = ref<HTMLInputElement | null>(null);
const isProjectMenuOpen = ref(false);

const cycleTimeText = computed(() => Math.round(executionStore.cycleTimeMs || 0).toString());
const yieldRateText = computed(() => executionStore.yieldRate.toFixed(1));
const currentProjectName = computed(() => projectsStore.currentProject?.name || '未加载工程');
const currentProjectStatus = computed(() => (projectsStore.currentProject ? '已保存' : '未加载'));

const toggleProjectMenu = () => {
  isProjectMenuOpen.value = !isProjectMenuOpen.value;
};

const openProject = async (projectId: string) => {
  try {
    await projectsStore.openProject(projectId);
    isProjectMenuOpen.value = false;
    await router.push({ name: 'FlowEditor' });
  } catch (error) {
    console.error('[AppHeader] Failed to open project:', error);
  }
};

const toggleRun = () => {
  if (executionStore.isRunning) {
    executionStore.stopContinuousRun();
  } else {
    executionStore.startExecution();
  }
};

const triggerFileInput = () => {
  fileInputRef.value?.click();
};

const onFileImport = (event: Event) => {
  const target = event.target as HTMLInputElement;
  const file = target.files?.[0];
  if (!file) return;

  const reader = new FileReader();
  reader.onload = (e) => {
    try {
      const content = e.target?.result as string;
      const data = JSON.parse(content);
      flowStore.loadLegacyProject(data);
    } catch (err) {
      console.error('Failed to parse legacy JSON project file:', err);
      window.alert('工程文件无效。');
    }
  };
  reader.readAsText(file);

  if (fileInputRef.value) {
    fileInputRef.value.value = '';
  }
};

const saveProject = () => {
  const legacyData = flowStore.buildLegacyProject();
  const blob = new Blob([JSON.stringify(legacyData, null, 2)], {
    type: 'application/json',
  });
  const url = URL.createObjectURL(blob);

  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = `ClearVision_Export_${Date.now()}.json`;
  anchor.click();

  URL.revokeObjectURL(url);
};

onMounted(async () => {
  await projectsStore.loadProjects();
});
</script>

<style scoped>
.app-header {
  position: absolute;
  top: 16px;
  left: 16px;
  right: 16px;
  height: 64px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  z-index: 100;
  pointer-events: none;
}

.header-left,
.header-center,
.header-right {
  pointer-events: auto;
  display: flex;
  align-items: center;
  gap: 16px;
}

.logo {
  font-family: "Inter", sans-serif;
  font-size: 20px;
  font-weight: 700;
  letter-spacing: -0.5px;
  display: flex;
  align-items: center;
  padding: 0 8px;
}

.logo-text {
  color: var(--text-primary);
}

.logo-text .highlight {
  color: var(--accent-red);
}

.project-switcher-wrapper {
  position: relative;
}

.project-switcher {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px 16px;
  height: 48px;
}

.project-info {
  display: flex;
  flex-direction: column;
}

.project-name {
  font-size: 14px;
  font-weight: 600;
  line-height: 1.2;
}

.project-status {
  font-size: 11px;
  color: var(--text-muted);
}

.project-menu {
  position: absolute;
  top: 56px;
  left: 0;
  width: 260px;
  max-height: 320px;
  overflow-y: auto;
  background: rgba(255, 255, 255, 0.95);
  border: 1px solid var(--border-glass, rgba(0, 0, 0, 0.05));
  border-radius: 14px;
  backdrop-filter: blur(12px);
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.08);
  z-index: 120;
}

.project-menu-item {
  width: 100%;
  border: 0;
  background: transparent;
  text-align: left;
  padding: 10px 12px;
  cursor: pointer;
  display: flex;
  flex-direction: column;
}

.project-menu-item:hover {
  background: rgba(255, 77, 77, 0.06);
}

.project-menu-item.active {
  background: rgba(255, 77, 77, 0.12);
}

.project-menu-name {
  font-size: 13px;
  font-weight: 600;
  color: var(--text-primary);
}

.project-menu-meta {
  font-size: 11px;
  color: var(--text-muted);
  margin-top: 2px;
}

.project-menu-empty {
  font-size: 12px;
  color: var(--text-muted);
  padding: 12px;
}

.icon-sm {
  width: 16px;
  height: 16px;
  stroke-width: 2.5px;
}

.text-muted {
  color: var(--text-muted);
}

.header-center {
  position: absolute;
  left: 50%;
  transform: translateX(-50%);
}

.execution-dashboard {
  display: flex;
  align-items: center;
  height: 56px;
  padding: 0 24px 0 8px;
  gap: 20px;
  border-radius: 28px;
}

.play-btn-wrapper {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
}

.progress-ring {
  position: absolute;
  top: 0;
  left: 0;
  transform: rotate(-90deg);
}

.progress-ring__circle {
  transition: stroke-dashoffset 0.3s;
}

.progress-ring__value {
  animation: s-progress 2s linear infinite;
}

@keyframes s-progress {
  from {
    stroke-dashoffset: 113;
  }
  to {
    stroke-dashoffset: 0;
  }
}

.execution-stats {
  display: flex;
  flex-direction: column;
}

.stat-value {
  font-family: inherit;
  font-size: 16px;
  font-weight: 700;
  line-height: 1.1;
  color: var(--text-primary);
}

.stat-unit {
  font-size: 12px;
  color: var(--text-muted);
  font-weight: 500;
  margin-left: 2px;
}

.stat-value.success {
  color: #10b981;
}

.stat-label {
  font-size: 10px;
  text-transform: uppercase;
  color: var(--text-muted);
  letter-spacing: 0.5px;
  font-weight: 600;
}

.divider {
  width: 1px;
  height: 24px;
  background: var(--border-glass, rgba(0, 0, 0, 0.05));
}

.actions-group {
  display: flex;
  gap: 4px;
  background: var(--glass-bg, rgba(255, 255, 255, 0.7));
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
  padding: 4px;
  border-radius: 12px;
  border: 1px solid var(--border-glass, rgba(0, 0, 0, 0.05));
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.03);
}
</style>
