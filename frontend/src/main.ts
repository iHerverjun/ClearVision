import { createApp } from 'vue'
import { createI18n } from 'vue-i18n'
import App from './App.vue'
import router from './router'
import pinia from './stores'
import '@/styles/main.css'

const i18n = createI18n({
  legacy: false, 
  locale: 'zh-CN',
  messages: {
    'zh-CN': {
      message: {
        hello: '你好 世界'
      }
    }
  }
})

const app = createApp(App)

app.use(router)
app.use(pinia)
app.use(i18n)

app.mount('#app')
