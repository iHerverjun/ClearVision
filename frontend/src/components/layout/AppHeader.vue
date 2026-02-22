<template>
  <header class="app-header">
    <div class="header-left">
      <div class="logo">
        <span class="logo-text"
          >Clear<span class="highlight">Vision</span></span
        >
      </div>

      <!-- Project Switcher Placeholder -->
      <GlassCard class="project-switcher" interactive>
        <FolderIcon class="icon-sm" />
        <div class="project-info">
          <span class="project-name">Default_Project_01</span>
          <span class="project-status">Saved</span>
        </div>
        <ChevronDownIcon class="icon-sm text-muted" />
      </GlassCard>
    </div>

    <!-- Execution Center Placeholder -->
    <div class="header-center">
      <GlassCard class="execution-dashboard">
        <div class="play-btn-wrapper" :class="{ 'is-running': isRunning }">
          <svg class="progress-ring" width="40" height="40" v-if="isRunning">
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
            :variant="isRunning ? 'danger' : 'primary'"
            size="md"
            title="Run Sequence"
            @click="toggleRun"
          >
            <SquareIcon v-if="isRunning" />
            <PlayIcon v-else />
          </IconButton>
        </div>

        <div class="execution-stats">
          <span class="stat-value">12.5<span class="stat-unit">ms</span></span>
          <span class="stat-label">Cycle Time</span>
        </div>

        <div class="divider"></div>

        <div class="execution-stats">
          <span class="stat-value success"
            >99.2<span class="stat-unit">%</span></span
          >
          <span class="stat-label">Yield Rate</span>
        </div>
      </GlassCard>
    </div>

    <div class="header-right">
      <!-- Action Buttons -->
      <div class="actions-group">
        <!-- Hidden file input for loading C# JSON -->
        <input
          type="file"
          ref="fileInputRef"
          accept=".json"
          style="display: none"
          @change="onFileImport"
        />

        <IconButton title="Load Legacy JSON (Ctrl+O)" @click="triggerFileInput">
          <UploadIcon />
        </IconButton>

        <IconButton title="Save Project (Ctrl+S)" @click="saveProject">
          <SaveIcon />
        </IconButton>

        <IconButton
          title="Undo (Ctrl+Z)"
          :disabled="!flowStore.canUndo"
          @click="flowStore.undo"
        >
          <UndoIcon />
        </IconButton>

        <IconButton
          title="Redo (Ctrl+Y)"
          :disabled="!flowStore.canRedo"
          @click="flowStore.redo"
        >
          <RedoIcon />
        </IconButton>
      </div>
    </div>
  </header>
</template>

<script setup lang="ts">
import { ref } from "vue";
import {
  FolderIcon,
  ChevronDownIcon,
  PlayIcon,
  SquareIcon,
  SaveIcon,
  UndoIcon,
  RedoIcon,
  UploadIcon,
} from "lucide-vue-next";
import GlassCard from "../shared/GlassCard.vue";
import IconButton from "../shared/IconButton.vue";
import { useFlowStore } from "../../stores/flow";

const isRunning = ref(false);
const flowStore = useFlowStore();
const fileInputRef = ref<HTMLInputElement | null>(null);

const toggleRun = () => {
  isRunning.value = !isRunning.value;
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
      // Pass the deserialized backend payload directly to the pinia store
      // which uses flowSerializer under the hood
      flowStore.loadLegacyProject(data);
    } catch (err) {
      console.error("Failed to parse legacy JSON project file:", err);
      alert("Invalid Project File");
    }
  };
  reader.readAsText(file);

  // Reset input so the same file could be loaded again
  if (fileInputRef.value) {
    fileInputRef.value.value = "";
  }
};

const saveProject = () => {
  const legacyData = flowStore.buildLegacyProject();
  const blob = new Blob([JSON.stringify(legacyData, null, 2)], {
    type: "application/json",
  });
  const url = URL.createObjectURL(blob);

  const a = document.createElement("a");
  a.href = url;
  a.download = `ClearVision_Export_${Date.now()}.json`;
  a.click();

  URL.revokeObjectURL(url);
};
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
  pointer-events: none; /* Let clicks pass through empty space to canvas */
}

/* Re-enable pointer events for child elements */
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

.icon-sm {
  width: 16px;
  height: 16px;
  stroke-width: 2.5px;
}

.text-muted {
  color: var(--text-muted);
}

/* Center Dashboard */
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

/* Right Section */
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

.ai-trigger {
  padding: 0 16px;
  gap: 8px;
  font-weight: 600;
  font-size: 14px;
  height: 40px;
  border-radius: 20px;
}

.ai-icon {
  color: var(--accent-red);
  width: 18px;
  height: 18px;
}
</style>
