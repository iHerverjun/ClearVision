import { defineStore } from 'pinia';
import { computed, ref } from 'vue';
import { apiClient } from '../services/api';
import { webMessageBridge } from '../services/bridge';
import { BridgeMessageType } from '../services/bridge.types';

export interface DashboardStats {
  averageYield: number;
  averageCycleTimeMs: number;
  storageUsedGb: number;
  totalInspections: number;
  okCount: number;
  ngCount: number;
}

export interface HardwareStatus {
  cpuUsage: number;
  memoryUsedGb: number;
  memoryTotalGb: number;
  isBridgeConnected: boolean;
  cameraStatus: string;
  runningInspections: number;
}

export interface ActivityLog {
  id: string;
  title: string;
  description: string;
  timestamp: string;
  type: string;
  userName?: string;
}

const normalizeNumber = (value: unknown, fallback = 0) => {
  const numeric = Number(value);
  return Number.isFinite(numeric) ? numeric : fallback;
};

const normalizeDashboardStats = (raw: any): DashboardStats => ({
  averageYield: normalizeNumber(raw?.averageYield),
  averageCycleTimeMs: normalizeNumber(raw?.averageCycleTimeMs),
  storageUsedGb: normalizeNumber(raw?.storageUsedGb),
  totalInspections: normalizeNumber(raw?.totalInspections),
  okCount: normalizeNumber(raw?.okCount),
  ngCount: normalizeNumber(raw?.ngCount),
});

const normalizeHardwareStatus = (raw: any): HardwareStatus => ({
  cpuUsage: normalizeNumber(raw?.cpuUsage),
  memoryUsedGb: normalizeNumber(raw?.memoryUsedGb),
  memoryTotalGb: normalizeNumber(raw?.memoryTotalGb),
  isBridgeConnected: Boolean(raw?.isBridgeConnected),
  cameraStatus: String(raw?.cameraStatus || 'unknown'),
  runningInspections: normalizeNumber(raw?.runningInspections),
});

const normalizeActivity = (raw: any): ActivityLog => ({
  id: String(raw?.id || `activity-${Date.now()}`),
  title: String(raw?.title || '活动'),
  description: String(raw?.description || ''),
  timestamp: String(raw?.timestamp || new Date().toISOString()),
  type: String(raw?.type || '未知'),
  userName: typeof raw?.userName === 'string' ? raw.userName : '',
});

export const useSystemStatsStore = defineStore('systemStats', () => {
  const dashboardStats = ref<DashboardStats | null>(null);
  const hardwareStatus = ref<HardwareStatus | null>(null);
  const activities = ref<ActivityLog[]>([]);
  const appVersion = ref('0.0.0');
  const isLoading = ref(false);
  const error = ref<string | null>(null);
  const monitoringTimer = ref<number | null>(null);

  const yieldRateText = computed(() =>
    dashboardStats.value ? `${dashboardStats.value.averageYield.toFixed(1)}%` : '--',
  );
  const cycleTimeText = computed(() =>
    dashboardStats.value ? `${Math.round(dashboardStats.value.averageCycleTimeMs)}ms` : '--',
  );

  async function loadDashboardStats() {
    isLoading.value = true;
    error.value = null;

    try {
      const bridgeResponse = await webMessageBridge.sendMessage(
        BridgeMessageType.SystemStatsQuery,
        {},
        true,
      );
      dashboardStats.value = normalizeDashboardStats(bridgeResponse);
    } catch {
      try {
        const response = await apiClient.get('/api/system/stats');
        dashboardStats.value = normalizeDashboardStats(response);
      } catch (loadError: any) {
        error.value = loadError?.message || '加载看板统计失败';
      }
    } finally {
      isLoading.value = false;
    }
  }

  async function loadHardwareStatus() {
    try {
      const bridgeResponse = await webMessageBridge.sendMessage(
        BridgeMessageType.HardwareStatusQuery,
        {},
        true,
      );
      hardwareStatus.value = normalizeHardwareStatus(bridgeResponse);
    } catch {
      try {
        const response = await apiClient.get('/api/system/hardware');
        hardwareStatus.value = normalizeHardwareStatus(response);
      } catch (loadError: any) {
        error.value = loadError?.message || '加载硬件状态失败';
      }
    }
  }

  async function loadActivities(count = 10) {
    try {
      const bridgeResponse = await webMessageBridge.sendMessage(
        BridgeMessageType.ActivityLogQuery,
        { count },
        true,
      );

      if (Array.isArray(bridgeResponse?.activities)) {
        activities.value = bridgeResponse.activities.map(normalizeActivity);
        return;
      }
    } catch {
      // fallback below
    }

    try {
      const response = await apiClient.get(`/api/activities?count=${count}`);
      activities.value = (Array.isArray(response) ? response : []).map(normalizeActivity);
    } catch (loadError: any) {
      error.value = loadError?.message || '加载活动记录失败';
    }
  }

  async function loadAppVersion() {
    try {
      const response: any = await apiClient.get('/api/system/version');
      appVersion.value = String(response?.version || response?.appVersion || appVersion.value);
    } catch {
      // Keep fallback value when backend endpoint is unavailable.
    }
  }

  function startHardwareMonitoring(intervalMs = 2000) {
    stopHardwareMonitoring();
    monitoringTimer.value = window.setInterval(() => {
      loadHardwareStatus().catch((loadError: any) => {
        error.value = loadError?.message || '加载硬件状态失败';
      });
    }, intervalMs);
  }

  function stopHardwareMonitoring() {
    if (monitoringTimer.value) {
      window.clearInterval(monitoringTimer.value);
      monitoringTimer.value = null;
    }
  }

  return {
    dashboardStats,
    hardwareStatus,
    activities,
    appVersion,
    isLoading,
    error,
    yieldRateText,
    cycleTimeText,
    loadDashboardStats,
    loadHardwareStatus,
    loadActivities,
    loadAppVersion,
    startHardwareMonitoring,
    stopHardwareMonitoring,
  };
});
