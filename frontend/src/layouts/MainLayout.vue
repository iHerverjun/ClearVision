<template>
  <div
    class="main-layout"
    :class="{ 'is-sidebar-collapsed': uiStore.sidebarCollapsed }"
  >
    <!-- Background Elements -->
    <div class="bg-lights">
      <div class="bg-orb red-orb"></div>
      <div class="bg-orb black-orb"></div>
    </div>

    <!-- Pattern Overlay -->
    <div class="bg-pattern pattern-dots"></div>

    <!-- Application Components -->
    <AppHeader />
    <AppSidebar />

    <!-- Main Content Area -->
    <main class="content-area">
      <router-view v-slot="{ Component }">
        <transition name="fade-slide" mode="out-in">
          <component :is="Component" />
        </transition>
      </router-view>
    </main>

    <AppStatusBar />
  </div>
</template>

<script setup lang="ts">
import { useUiStore } from "../stores/ui";
import AppHeader from "../components/layout/AppHeader.vue";
import AppSidebar from "../components/layout/AppSidebar.vue";
import AppStatusBar from "../components/layout/AppStatusBar.vue";

const uiStore = useUiStore();
</script>

<style scoped>
.main-layout {
  position: relative;
  width: 100vw;
  height: 100vh;
  overflow: hidden;
  background-color: var(--bg-primary, #f5f5f7);
  color: var(--text-primary, #1c1c1e);
}

/* Base Background Aesthetics */
.bg-lights {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  overflow: hidden;
  z-index: 0;
  pointer-events: none;
}

.bg-orb {
  position: absolute;
  border-radius: 50%;
  filter: blur(140px);
}

.red-orb {
  width: 800px;
  height: 800px;
  background: var(--accent-red);
  opacity: 0.12;
  top: -200px;
  right: -200px;
  animation: s-float-1 20s ease-in-out infinite alternate;
}

.black-orb {
  width: 600px;
  height: 600px;
  background: var(--text-primary);
  opacity: 0.03;
  bottom: -100px;
  left: -100px;
  animation: s-float-2 25s ease-in-out infinite alternate;
}

.bg-pattern {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  z-index: 1;
  pointer-events: none;
  opacity: 0.04;
}

.pattern-dots {
  background-image: radial-gradient(var(--text-primary) 1px, transparent 1px);
  background-size: 24px 24px;
}

/* Content Area */
.content-area {
  position: absolute;
  top: 96px; /* Below Header */
  bottom: 56px; /* Above StatusBar */
  left: 236px; /* Right of expanded Sidebar */
  right: 16px;
  z-index: 10;
  background: rgba(255, 255, 255, 0.6);
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
  border: 1px solid var(--border-glass);
  border-radius: 24px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.03);
  overflow: hidden;
  transition: left 0.3s cubic-bezier(0.2, 0, 0, 1);
}

.main-layout.is-sidebar-collapsed .content-area {
  left: 100px;
}

/* Transitions */
.fade-slide-enter-active,
.fade-slide-leave-active {
  transition:
    opacity 0.3s ease,
    transform 0.3s cubic-bezier(0.2, 0, 0, 1);
}

.fade-slide-enter-from {
  opacity: 0;
  transform: translateY(15px);
}

.fade-slide-leave-to {
  opacity: 0;
  transform: translateY(-15px);
}
</style>
