import { defineStore } from 'pinia';
import { ref, watch } from 'vue';

export type Theme = 'light' | 'dark';

export const useUiStore = defineStore('ui', () => {
  const persistedTheme = localStorage.getItem('app_theme');
  const theme = ref<Theme>(persistedTheme === 'dark' ? 'dark' : 'light');
  
  const sidebarCollapsed = ref<boolean>(true);

  // Sync theme to document 
  const applyTheme = (newTheme: Theme) => {
    document.documentElement.dataset.theme = newTheme;
    if (newTheme === 'dark') {
      document.documentElement.classList.add('dark');
    } else {
      document.documentElement.classList.remove('dark');
    }
    localStorage.setItem('app_theme', newTheme);
  };

  // Initial application
  applyTheme(theme.value);

  // Watch for changes
  watch(theme, (newTheme) => {
    applyTheme(newTheme);
  });

  function toggleTheme() {
    theme.value = theme.value === 'light' ? 'dark' : 'light';
  }

  function toggleSidebar() {
    sidebarCollapsed.value = !sidebarCollapsed.value;
  }

  function setSidebar(collapsed: boolean) {
    sidebarCollapsed.value = collapsed;
  }

  return {
    theme,
    sidebarCollapsed,
    toggleTheme,
    toggleSidebar,
    setSidebar
  };
});
