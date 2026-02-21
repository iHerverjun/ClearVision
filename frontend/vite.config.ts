import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'

// https://vite.dev/config/
export default defineConfig({
  base: './', // 相对路径，适配 WebView2 本地加载
  build: {
    outDir: '../Acme.Product/src/Acme.Product.Desktop/wwwroot',
    emptyOutDir: true,
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    }
  },
  plugins: [
    vue(),
    tailwindcss(),
  ],
})
