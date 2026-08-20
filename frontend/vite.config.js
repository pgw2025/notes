import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  server: {
    host: '0.0.0.0',
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5062',
        changeOrigin: true
      }
    }
  },
  build: {
    rollupOptions: {
      // 生产构建：这 5 个包不打入 bundle，改由 index.html 的 importmap 从 CDN 加载
      external: ['vue', 'pinia', 'vant', 'axios', 'vue-router']
    }
  }
})
