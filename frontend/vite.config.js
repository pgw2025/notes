import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { VitePWA } from 'vite-plugin-pwa'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue(),
    // 接入 PWA：生成 Service Worker + manifest
    // 注意：registerType 为 prompt 时，需要在入口手动监听 needRefresh 并提示用户更新（见 src/main.js）
    VitePWA({
      registerType: 'prompt',
      // 不自动在 index.html 注入注册脚本，改为在 src/main.js 用 virtual 模块手动注册，
      // 以便捕捉 needRefresh/offlineReady 事件做更新提示。
      injectRegister: false,
      includeAssets: [
        'favicon.svg',
        'apple-touch-icon-180x180.png'
      ],
      manifest: {
        name: '云笺笔记',
        short_name: '云笺',
        description: '云笺笔记 - 轻量级云笔记应用',
        lang: 'zh-CN',
        theme_color: '#0FA98C',
        background_color: '#ffffff',
        display: 'standalone',
        start_url: '/',
        scope: '/',
        icons: [
          {
            src: 'pwa-192x192.png',
            sizes: '192x192',
            type: 'image/png'
          },
          {
            src: 'pwa-512x512.png',
            sizes: '512x512',
            type: 'image/png'
          },
          {
            src: 'pwa-maskable-192x192.png',
            sizes: '192x192',
            type: 'image/png',
            purpose: 'maskable'
          },
          {
            src: 'pwa-maskable-512x512.png',
            sizes: '512x512',
            type: 'image/png',
            purpose: 'maskable'
          }
        ]
      },
      workbox: {
        // KaTeX 公式字体属于按需懒加载，不必预缓存，避免首次安装下载 1MB+ 字体，影响“秒开”
        globPatterns: ['**/*.{js,css,html,svg,png,ico}'],
        // 离线时 SPA 路由回落到 index.html，保证断网刷新不白屏
        navigateFallback: 'index.html',
        // 预缓存最多 60 个静态资源，避免过大的清单
        maximumFileSizeToCacheInBytes: 5 * 1024 * 1024,
        cleanupOutdatedCaches: true,
        runtimeCaching: [
          {
            // 资源型 API（笔记/分类/标签/统计，不含附件与头像）：
            // NetworkFirst，超时 3s 回退缓存，离线时也能浏览最近加载的数据。
            // 已用负向前瞻排除 /api/auth(不含头像) 与 /api/attachments。
            urlPattern: /\/api\/(?!(auth|attachments)\/)/,
            method: 'GET',
            handler: 'NetworkFirst',
            options: {
              networkTimeoutSeconds: 3,
              cacheName: 'api-notes',
              expiration: {
                maxEntries: 150,
                maxAgeSeconds: 30 * 24 * 60 * 60
              },
              cacheableResponse: { statuses: [0, 200] }
            }
          },
          {
            // 附件（图片/文档）：CacheFirst，离线时附件立即可见
            urlPattern: /\/api\/attachments\//,
            method: 'GET',
            handler: 'CacheFirst',
            options: {
              cacheName: 'api-attachments',
              expiration: {
                maxEntries: 200,
                maxAgeSeconds: 60 * 24 * 60 * 60
              },
              cacheableResponse: { statuses: [0, 200] }
            }
          },
          {
            // 头像：CacheFirst（头像 URL 不变但内容可更新，用较短保鲜期）
            urlPattern: /\/api\/auth\/avatar\//,
            method: 'GET',
            handler: 'CacheFirst',
            options: {
              cacheName: 'api-avatars',
              expiration: {
                maxEntries: 50,
                maxAgeSeconds: 7 * 24 * 60 * 60
              },
              cacheableResponse: { statuses: [0, 200] }
            }
          }
        ]
      }
    })
  ],
  server: {
    host: '0.0.0.0',
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5062',
        changeOrigin: true
      }
    }
  }
})