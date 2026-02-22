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

  function openModal() {
    isSettingsModalOpen.value = true;
    loadSettings();
  }

  function closeModal() {
    isSettingsModalOpen.value = false;
  }

  async function loadSettings() {
    isLoading.value = true;
    try {
      const response = await webMessageBridge.sendMessage(
        BridgeMessageType.SettingsGet,
        {},
        true
      );
      if (response && response.settings) {
         // Deep merge or replace
         settings.value = { ...settings.value, ...response.settings };
      }
    } catch (e) {
      console.error('[SettingsStore] Failed to load settings', e);
    } finally {
      isLoading.value = false;
    }
  }

  async function saveSettings() {
    isLoading.value = true;
    try {
      await webMessageBridge.sendMessage(
        BridgeMessageType.SettingsSave,
        { settings: settings.value },
        true
      );
      // Show toast ideally, but for now just close
      closeModal();
    } catch (e) {
      console.error('[SettingsStore] Failed to save settings', e);
    } finally {
      isLoading.value = false;
    }
  }

  return {
    settings,
    isSettingsModalOpen,
    isLoading,
    openModal,
    closeModal,
    loadSettings,
    saveSettings
  };
});
