<template>
  <footer class="app-status-bar">
    <div class="status-left">
      <div class="auth-indicator">
        <UserIcon class="icon-xs" />
        <span class="user-role">{{ roleName }}</span>
        <span class="user-name">{{ userName }}</span>
      </div>
    </div>

    <div class="status-center">
      <!-- Communication Heartbeat -->
      <div class="comm-channel" title="Main Bridge Connection">
        <span
          class="status-dot"
          :class="isBridgeConnected ? 'healthy' : 'error'"
        ></span>
        <span class="channel-name">Host Link</span>
      </div>

      <div class="divider"></div>

      <div class="comm-channel" title="PLC Modbus Status (Placeholder)">
        <span class="status-dot healthy"></span>
        <span class="channel-name">PLC-1</span>
      </div>
    </div>

    <div class="status-right">
      <!-- Hardware Resource Placeholders -->
      <div class="hardware-stat">
        <CpuIcon class="icon-xs" />
        <span>12%</span>
      </div>
      <div class="hardware-stat">
        <MemoryStickIcon class="icon-xs" />
        <!-- Simulated Memory Icon -->
        <span>1.2 GB</span>
      </div>

      <div class="divider"></div>

      <div class="version-badge">v3.1.0</div>
    </div>
  </footer>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useAuthStore } from "../../stores/auth";
import {
  UserIcon,
  CpuIcon,
  BaselineIcon as MemoryStickIcon,
} from "lucide-vue-next";

const authStore = useAuthStore();

const userName = computed(() => authStore.currentUser?.username || "Guest");
const roleName = computed(() => (authStore.isAdmin ? "Admin" : "Operator"));

// Mocked healthy status for bridge
const isBridgeConnected = true;
</script>

<style scoped>
.app-status-bar {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  height: 40px;
  background: var(--bg-secondary, #ffffff);
  border-top: 1px solid var(--border-glass, rgba(0, 0, 0, 0.05));
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0 16px;
  font-family: inherit;
  font-size: 12px;
  color: var(--text-muted, #64748b);
  z-index: 100;
}

.status-left,
.status-center,
.status-right {
  display: flex;
  align-items: center;
  gap: 16px;
  height: 100%;
}

.icon-xs {
  width: 14px;
  height: 14px;
  stroke-width: 2.5px;
}

.divider {
  width: 1px;
  height: 16px;
  background: var(--border-glass, rgba(0, 0, 0, 0.05));
}

.auth-indicator {
  display: flex;
  align-items: center;
  gap: 6px;
}

.user-role {
  text-transform: uppercase;
  font-weight: 700;
  font-size: 10px;
  padding: 2px 6px;
  background: rgba(0, 0, 0, 0.05);
  border-radius: 4px;
  color: var(--text-primary, #1c1c1e);
  letter-spacing: 0.5px;
}

.user-name {
  font-weight: 500;
  color: var(--text-muted, #64748b);
}

/* Comm Channels Heartbeat */
.comm-channel {
  display: flex;
  align-items: center;
  gap: 6px;
  font-weight: 600;
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  position: relative;
}

.status-dot.healthy {
  background-color: #10b981;
  box-shadow: 0 0 8px rgba(16, 185, 129, 0.3);
}

.status-dot.healthy::after {
  content: "";
  position: absolute;
  top: -2px;
  left: -2px;
  right: -2px;
  bottom: -2px;
  border-radius: 50%;
  border: 1px solid #10b981;
  animation: s-ping 2s cubic-bezier(0, 0, 0.2, 1) infinite;
}

.status-dot.error {
  background-color: var(--accent-red, #ff4d4d);
  box-shadow: 0 0 8px rgba(255, 77, 77, 0.3);
}

.channel-name {
  color: var(--text-primary, #1c1c1e);
}

@keyframes s-ping {
  75%,
  100% {
    transform: scale(2);
    opacity: 0;
  }
}

/* Hardware Stats */
.hardware-stat {
  display: flex;
  align-items: center;
  gap: 6px;
  font-variant-numeric: tabular-nums;
  font-family: inherit;
  font-weight: 500;
}

.version-badge {
  font-family: monospace;
  font-weight: 600;
  color: var(--text-muted, #64748b);
}
</style>
