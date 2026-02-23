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
      <div class="comm-channel" title="主桥接连接">
        <span class="status-dot" :class="isBridgeConnected ? 'healthy' : 'error'"></span>
        <span class="channel-name">主机连接</span>
      </div>

      <div class="divider"></div>

      <div class="comm-channel" title="相机状态">
        <span class="status-dot" :class="cameraHealthy ? 'healthy' : 'error'"></span>
        <span class="channel-name">{{ cameraLabel }}</span>
      </div>
    </div>

    <div class="status-right">
      <div class="hardware-stat">
        <CpuIcon class="icon-xs" />
        <span>{{ cpuText }}</span>
      </div>
      <div class="hardware-stat">
        <MemoryStickIcon class="icon-xs" />
        <span>{{ memoryText }}</span>
      </div>

      <div class="divider"></div>

      <div class="version-badge">v{{ statsStore.appVersion }}</div>
    </div>
  </footer>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue';
import { useAuthStore } from '../../stores/auth';
import { useSystemStatsStore } from '../../stores/systemStats';
import { webMessageBridge } from '../../services/bridge';
import { UserIcon, CpuIcon, BaselineIcon as MemoryStickIcon } from 'lucide-vue-next';

const authStore = useAuthStore();
const statsStore = useSystemStatsStore();

const bridgeConnected = ref(webMessageBridge.getConnectionState());
let unsubscribeConnection: (() => void) | null = null;

const userName = computed(() => authStore.currentUser?.username || '访客');
const roleName = computed(() => (authStore.isAdmin ? '管理员' : '操作员'));

const isBridgeConnected = computed(() => {
  if (statsStore.hardwareStatus) {
    return statsStore.hardwareStatus.isBridgeConnected && bridgeConnected.value;
  }
  return bridgeConnected.value;
});

const cpuText = computed(() => {
  const usage = statsStore.hardwareStatus?.cpuUsage;
  return typeof usage === 'number' ? `${Math.round(usage)}%` : '--';
});

const memoryText = computed(() => {
  const used = statsStore.hardwareStatus?.memoryUsedGb;
  const total = statsStore.hardwareStatus?.memoryTotalGb;
  if (typeof used === 'number' && typeof total === 'number' && total > 0) {
    return `${used.toFixed(1)} / ${total.toFixed(1)} GB`;
  }
  return '--';
});

const cameraHealthy = computed(() => statsStore.hardwareStatus?.cameraStatus === 'connected');
const cameraStatusText = computed(() => {
  const raw = String(statsStore.hardwareStatus?.cameraStatus || 'unknown').toLowerCase();
  switch (raw) {
    case 'connected':
      return '已连接';
    case 'connecting':
      return '连接中';
    case 'disconnected':
      return '未连接';
    case 'error':
      return '异常';
    default:
      return '未知';
  }
});
const cameraLabel = computed(() => `相机：${cameraStatusText.value}`);

onMounted(async () => {
  unsubscribeConnection = webMessageBridge.onConnectionStateChange((connected) => {
    bridgeConnected.value = connected;
  });

  await Promise.all([
    statsStore.loadHardwareStatus(),
    statsStore.loadAppVersion(),
  ]);
  statsStore.startHardwareMonitoring(2000);
});

onUnmounted(() => {
  if (unsubscribeConnection) {
    unsubscribeConnection();
    unsubscribeConnection = null;
  }
  statsStore.stopHardwareMonitoring();
});
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
