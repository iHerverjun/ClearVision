<template>
  <aside
    class="app-sidebar"
    :class="{ 'is-collapsed': isCollapsed }"
    @mouseenter="handleMouseEnter"
    @mouseleave="handleMouseLeave"
  >
    <div class="sidebar-inner">
      <nav class="nav-menu">
        <router-link
          v-for="item in navItems"
          :key="item.path"
          :to="item.path"
          class="nav-item"
          active-class="is-active"
        >
          <div class="nav-icon-wrapper">
            <component :is="item.icon" class="nav-icon" />
          </div>
          <span class="nav-label">{{ item.label }}</span>

          <!-- Tooltip for collapsed state if no hover expansion is used. But we use expansion! -->
        </router-link>
      </nav>

      <div class="spacer"></div>

      <div class="sidebar-footer">
        <button v-permission="['admin']" class="nav-item settings-btn" @click="openSettings">
          <div class="nav-icon-wrapper">
            <SettingsIcon class="nav-icon" />
          </div>
          <span class="nav-label">{{ $t('nav.settings') }}</span>
        </button>
      </div>
    </div>
  </aside>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import {
  NetworkIcon,
  CameraIcon,
  LineChartIcon,
  FolderIcon,
  SparklesIcon,
  SettingsIcon,
} from "lucide-vue-next";
import { useUiStore } from "../../stores/ui";

const { t } = useI18n();

// Make navItems reactive so they update when the language changes
const navItems = computed(() => [
  { path: "/flow-editor", label: t('nav.flow_editor'), icon: NetworkIcon },
  { path: "/inspection", label: t('nav.inspection'), icon: CameraIcon },
  { path: "/results", label: t('nav.results'), icon: LineChartIcon },
  { path: "/projects", label: t('nav.projects'), icon: FolderIcon },
  { path: "/ai-assistant", label: t('nav.ai_assistant'), icon: SparklesIcon },
]);

const router = useRouter();
const uiStore = useUiStore();
const isCollapsed = computed(() => uiStore.sidebarCollapsed);
let hoverTimer: number | null = null;

const handleMouseEnter = () => {
  if (hoverTimer) clearTimeout(hoverTimer);
  hoverTimer = window.setTimeout(() => {
    uiStore.setSidebar(false);
  }, 150); // slight delay to prevent accidental expansions
};

const handleMouseLeave = () => {
  if (hoverTimer) clearTimeout(hoverTimer);
  uiStore.setSidebar(true);
};

const openSettings = () => {
  router.push({ name: "Settings" });
};
</script>

<style scoped>
.app-sidebar {
  position: absolute;
  top: 96px; /* Below Header */
  bottom: 80px; /* Above StatusBar */
  left: 16px;
  width: 200px; /* Expanded Width */
  transition: width 0.3s cubic-bezier(0.2, 0, 0, 1);
  z-index: 90;
}

.app-sidebar.is-collapsed {
  width: 64px; /* Collapsed Width */
}

.sidebar-inner {
  height: 100%;
  width: 100%;
  background: var(--glass-bg, rgba(255, 255, 255, 0.7));
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
  border: 1px solid var(--border-glass, rgba(0, 0, 0, 0.05));
  border-radius: 24px;
  display: flex;
  flex-direction: column;
  padding: 16px 8px;
  box-shadow: 0 12px 32px rgba(0, 0, 0, 0.04);
  overflow: hidden;
}

.nav-menu {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.nav-item {
  display: flex;
  align-items: center;
  height: 48px;
  border-radius: 16px;
  color: var(--text-muted, #64748b);
  text-decoration: none;
  background: transparent;
  border: none;
  cursor: pointer;
  transition: all 0.2s ease;
  overflow: hidden;
  position: relative;
  white-space: nowrap;
}

.nav-item:hover {
  background: var(--hover-overlay, rgba(0, 0, 0, 0.04));
  color: var(--text-primary, #1c1c1e);
}

.nav-item.is-active {
  background: var(--active-overlay, rgba(255, 77, 77, 0.1));
  color: var(--accent-red, #ff4d4d);
}

.nav-item.is-active::before {
  content: "";
  position: absolute;
  left: 0;
  top: 50%;
  transform: translateY(-50%);
  height: 24px;
  width: 3px;
  background-color: var(--accent-red, #ff4d4d);
  border-radius: 0 4px 4px 0;
}

.nav-icon-wrapper {
  min-width: 48px;
  height: 48px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.nav-icon {
  width: 22px;
  height: 22px;
  stroke-width: 2px; /* Lighter stroke for elegant feel */
  transition: transform 0.2s ease;
}

.nav-item:hover .nav-icon {
  transform: scale(1.1);
}

.nav-label {
  font-family: inherit;
  font-size: 14px;
  font-weight: 600;
  opacity: 1;
  transform: translateX(0);
  transition:
    opacity 0.2s cubic-bezier(0.2, 0, 0, 1),
    transform 0.2s cubic-bezier(0.2, 0, 0, 1);
  padding-right: 16px;
}

.is-collapsed .nav-label {
  opacity: 0;
  transform: translateX(-10px);
  pointer-events: none;
}

.spacer {
  flex: 1;
}

.sidebar-footer {
  display: flex;
  flex-direction: column;
  padding-top: 16px;
  border-top: 1px solid var(--border-glass, rgba(0, 0, 0, 0.05));
}

.settings-btn {
  width: 100%;
}
</style>
