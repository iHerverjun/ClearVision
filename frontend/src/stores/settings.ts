import { defineStore } from 'pinia';
import { ref } from 'vue';
import { webMessageBridge } from '../services/bridge';
import { BridgeMessageType } from '../services/bridge.types';

export interface AppSettings {
  general: {
    theme: 'light' | 'dark' | 'system';
    language: string;
    autoSaveInterval: number;
  };
  camera: {
    defaultResolution: string;
    exposureTarget: number;
  };
  communication: {
    protocol: 'TCP' | 'Serial' | 'PLC' | 'HTTP' | 'MQTT';
    host: string;
    port: number;
  };
  ai: {
    apiKey: string;
    model: string;
    timeoutMs: number;
  };
}

export const useSettingsStore = defineStore('settings', () => {
  const settings = ref<AppSettings>({
    general: {
      theme: 'system',
      language: 'zh-CN',
      autoSaveInterval: 5,
    },
    camera: {
      defaultResolution: '1920x1080',
      exposureTarget: 120,
    },
    communication: {
      protocol: 'TCP',
      host: '127.0.0.1',
      port: 8080,
    },
    ai: {
      apiKey: '',
      model: 'DeepSeek-V3',
      timeoutMs: 30000,
    }
  });

  const isSettingsModalOpen = ref(false);
  const isLoading = ref(false);
  const errorMessage = ref<string | null>(null);

  const resolveErrorMessage = (error: unknown, fallback: string) => {
    if (error instanceof Error && error.message) {
      return error.message;
    }
    return fallback;
  };

  function openModal() {
    isSettingsModalOpen.value = true;
    errorMessage.value = null;
    loadSettings();
  }

  function closeModal() {
    isSettingsModalOpen.value = false;
  }

  async function loadSettings() {
    isLoading.value = true;
    errorMessage.value = null;
    try {
      const response = await webMessageBridge.sendMessage(
        BridgeMessageType.SettingsGet,
        {},
        true
      );
      if (response?.settings) {
        const remote = response.settings as Partial<AppSettings>;
        settings.value = {
          general: { ...settings.value.general, ...(remote.general || {}) },
          camera: { ...settings.value.camera, ...(remote.camera || {}) },
          communication: { ...settings.value.communication, ...(remote.communication || {}) },
          ai: { ...settings.value.ai, ...(remote.ai || {}) },
        };
      }
    } catch (error) {
      console.error('[SettingsStore] Failed to load settings', error);
      errorMessage.value = resolveErrorMessage(error, '加载设置失败，请稍后重试。');
    } finally {
      isLoading.value = false;
    }
  }

  async function saveSettings() {
    isLoading.value = true;
    errorMessage.value = null;
    try {
      await webMessageBridge.sendMessage(
        BridgeMessageType.SettingsSave,
        { settings: settings.value },
        true
      );
      // Show toast ideally, but for now just close
      closeModal();
    } catch (error) {
      console.error('[SettingsStore] Failed to save settings', error);
      errorMessage.value = resolveErrorMessage(error, '保存设置失败，请检查后重试。');
    } finally {
      isLoading.value = false;
    }
  }

  return {
    settings,
    isSettingsModalOpen,
    isLoading,
    errorMessage,
    openModal,
    closeModal,
    loadSettings,
    saveSettings
  };
});
