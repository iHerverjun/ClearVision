<template>
  <div v-if="settingsStore.isSettingsModalOpen" class="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6 bg-black/60 backdrop-blur-sm">
    <div class="bg-[var(--color-surface)] w-full max-w-4xl h-[80vh] rounded-2xl shadow-2xl overflow-hidden flex flex-col border border-[var(--color-border)] ring-1 ring-white/10 relative">
      <!-- Loading Overlay -->
      <div v-if="settingsStore.isLoading" class="absolute inset-0 bg-[var(--color-surface)]/80 backdrop-blur-sm z-50 flex items-center justify-center">
        <div class="flex flex-col items-center">
          <div class="animate-spin rounded-full h-10 w-10 border-b-2 border-red-500 mb-4"></div>
          <span class="text-sm font-bold text-[var(--color-text)]">Processing...</span>
        </div>
      </div>

      <!-- Header -->
      <div class="flex items-center justify-between p-4 border-b border-[var(--color-border)] bg-[var(--color-background)]">
        <div class="flex items-center space-x-3">
          <div class="w-8 h-8 rounded-lg bg-red-500/10 flex items-center justify-center">
            <SettingsIcon class="text-red-500 w-4 h-4" />
          </div>
          <div>
            <h2 class="text-lg font-bold text-[var(--color-text)]">Settings</h2>
            <p class="text-xs text-[var(--color-text-muted)]">Configure system preferences and defaults</p>
          </div>
        </div>
        <button @click="settingsStore.closeModal()" class="p-2 hover:bg-red-500/10 hover:text-red-500 rounded-lg transition-colors text-gray-500">
          <XIcon class="w-5 h-5" />
        </button>
      </div>

      <!-- Main Layout: Sidebar + Content -->
      <div class="flex flex-1 overflow-hidden">
        
        <!-- Sidebar Navigation -->
        <aside class="w-48 sm:w-64 border-r border-[var(--color-border)] bg-[var(--color-background)] overflow-y-auto">
          <nav class="p-3 space-y-1">
            <button 
              v-for="tab in tabs" :key="tab.id"
              @click="activeTab = tab.id"
              :class="[
                'w-full flex items-center space-x-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-all duration-200',
                activeTab === tab.id 
                  ? 'bg-red-500 text-white shadow-md shadow-red-500/20' 
                  : 'text-[var(--color-text)] hover:bg-[var(--color-surface)] hover:scale-[1.02]'
              ]"
            >
              <component :is="tab.icon" :class="['w-4 h-4', activeTab === tab.id ? 'text-white' : 'text-gray-400']" />
              <span>{{ tab.label }}</span>
            </button>
          </nav>
        </aside>

        <!-- Content Area -->
        <main class="flex-1 overflow-y-auto bg-[var(--color-surface)] p-6">
          <component :is="activeComponent" />
        </main>
      </div>

      <!-- Footer Actions -->
      <div class="p-4 border-t border-[var(--color-border)] bg-[var(--color-background)] flex justify-end space-x-3">
        <button @click="settingsStore.closeModal()" class="px-5 py-2 text-sm font-bold text-[var(--color-text)] hover:bg-[var(--color-surface)] rounded-xl transition-colors border border-[var(--color-border)] shadow-sm">
          Cancel
        </button>
        <button @click="handleSave" class="px-5 py-2 text-sm font-bold text-white bg-red-500 hover:bg-red-600 rounded-xl transition-all shadow-md hover:shadow-lg shadow-red-500/20 flex items-center">
          <SaveIcon class="w-4 h-4 mr-2" />
          Save Changes
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useSettingsStore } from '../../stores/settings';
import { 
  SettingsIcon, 
  XIcon, 
  SaveIcon,
  MonitorIcon,
  CameraIcon as CameraIconGen,
  NetworkIcon,
  DatabaseIcon,
  SparklesIcon,
  InfoIcon
} from 'lucide-vue-next';

// Tab Components
import GeneralTab from './tabs/GeneralTab.vue';
import CameraTab from './tabs/CameraTab.vue';
import CommunicationTab from './tabs/CommunicationTab.vue';
import DatabaseTab from './tabs/DatabaseTab.vue';
import AiTab from './tabs/AiTab.vue';
import AboutTab from './tabs/AboutTab.vue';

const settingsStore = useSettingsStore();
const route = useRoute();
const router = useRouter();

const tabs = [
  { id: 'general', label: 'General', icon: MonitorIcon, component: GeneralTab },
  { id: 'camera', label: 'Camera', icon: CameraIconGen, component: CameraTab },
  { id: 'communication', label: 'Communication', icon: NetworkIcon, component: CommunicationTab },
  { id: 'database', label: 'Database', icon: DatabaseIcon, component: DatabaseTab },
  { id: 'ai', label: 'AI Assistance', icon: SparklesIcon, component: AiTab },
  { id: 'about', label: 'About', icon: InfoIcon, component: AboutTab },
] as const;

const activeTab = ref<string>(tabs[0].id);

const activeComponent = computed(() => {
  const foundTab = tabs.find(t => t.id === activeTab.value);
  return foundTab ? foundTab.component : GeneralTab;
});

const handleSave = async () => {
  await settingsStore.saveSettings();
};

const checkRouteForModal = () => {
  if (route.query.modal === 'settings') {
    settingsStore.openModal();
    // remove query param so it doesn't reopen on refresh unless asked
    router.replace({ query: { ...route.query, modal: undefined } });
  }
};

watch(() => route.query.modal, () => {
  checkRouteForModal();
});

onMounted(() => {
  checkRouteForModal();
});
</script>
