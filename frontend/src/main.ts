import { createApp } from 'vue'
import { createI18n } from 'vue-i18n'
import App from './App.vue'
import router from './router'
import pinia from './stores'
import { useExecutionStore } from './stores/execution'
import { vPermission } from './directives/v-permission'
import '@/styles/main.css'

import zhCN from './locales/zh-CN.json'
import enUS from './locales/en-US.json'

const savedLocale = localStorage.getItem('cv_language') || 'zh-CN'

const i18n = createI18n({
  legacy: false, 
  locale: savedLocale,
  fallbackLocale: 'en-US',
  messages: {
    'zh-CN': zhCN,
    'en-US': enUS
  }
})

const app = createApp(App)

app.use(router)
app.use(pinia)
app.use(i18n)

// Register global directive
app.directive('permission', vPermission)

const executionStore = useExecutionStore(pinia)
executionStore.initializeBridgeListeners()

app.mount('#app')
