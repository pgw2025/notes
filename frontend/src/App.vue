<template>
  <div class="app-layout" :class="{ 'is-desktop': isDesktop }">
    <!-- 桌面端侧边栏 -->
    <aside v-if="showSidebar" class="app-sidebar">
      <div class="sidebar-brand">
        <div class="brand-logo-box">
          <img src="/pwa-192x192.png" alt="云笺笔记" class="brand-logo-icon" />
        </div>
        <div class="brand-text-wrap">
          <span class="brand-title">云笺笔记</span>
          <span class="brand-subtitle">Cloud Notes</span>
        </div>
      </div>

      <nav class="sidebar-nav">
        <router-link
          v-for="item in navItems"
          :key="item.to"
          :to="item.to"
          class="sidebar-item"
        >
          <div class="item-icon-box">
            <van-icon :name="item.icon" size="18" />
          </div>
          <span class="item-label">{{ item.label }}</span>
        </router-link>

        <!-- 侧边栏所有标签展示区域 -->
        <SidebarTags :tags="tagStore.tags" />
      </nav>

      <div class="sidebar-footer">
        <button class="sidebar-theme-toggle" @click="toggleTheme" :title="`当前主题: ${themeLabel}`">
          <van-icon :name="themeIcon" size="15" />
          <span>{{ themeLabel }}</span>
        </button>

        <router-link to="/notes/new" class="sidebar-new-btn">
          <van-icon name="plus" size="16" />
          <span>新建笔记</span>
        </router-link>
      </div>
    </aside>

    <!-- 主内容区 -->
    <main class="app-main">
      <!-- 全局离线提示横幅 -->
      <van-notice-bar
        v-if="offline.isOffline"
        left-icon="info-o"
        wrapable
        class="offline-bar"
      >当前离线，正在展示最近缓存的内容</van-notice-bar>
      <router-view v-slot="{ Component }">
        <keep-alive include="NotesList">
          <component :is="Component" />
        </keep-alive>
      </router-view>
    </main>

    <!-- 移动端底部导航 -->
    <van-tabbar v-if="!isDesktop && showTabbar" route safe-area-inset-bottom class="mobile-tabbar">
      <van-tabbar-item
        v-for="item in navItems"
        :key="item.to"
        :to="item.to"
        :icon="item.icon"
      >{{ item.label }}</van-tabbar-item>
    </van-tabbar>
  </div>
</template>

<script setup>
import { computed, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { showToast } from 'vant'
import { useResponsive } from './composables/useResponsive'
import { useThemeStore } from './stores/theme'
import { useTagStore } from './stores/tag'
import { useOfflineStore } from './stores/offline'
import SidebarTags from './components/SidebarTags.vue'

const route = useRoute()
const theme = useThemeStore()
const tagStore = useTagStore()
const offline = useOfflineStore()
const { isDesktop } = useResponsive()

onMounted(() => {
  offline.init()
  const token = localStorage.getItem('token')
  if (token) {
    tagStore.fetchTags()
  }
})

// 当切换页面时（如创建新笔记或在编辑页添加标签后返回），同步刷新侧栏标签列表
watch(
  () => route.fullPath,
  () => {
    const token = localStorage.getItem('token')
    if (token && !route.meta.public) {
      tagStore.fetchTags()
    }
  }
)

const showTabbar = computed(() => route.meta.tabbar !== false)
const showSidebar = computed(() => isDesktop.value && route.meta.public !== true)

const navItems = [
  { to: '/notes', icon: 'notes-o', label: '笔记' },
  { to: '/timeline', icon: 'clock-o', label: '时间线' },
  { to: '/search', icon: 'search', label: '搜索' },
  { to: '/categories', icon: 'apps-o', label: '分类' },
  { to: '/settings', icon: 'contact', label: '我的' }
]

const themeLabel = computed(() => {
  if (theme.mode === 'dark') return '深色模式'
  if (theme.mode === 'light') return '浅色模式'
  return '跟随系统'
})

const themeIcon = computed(() => {
  if (theme.mode === 'dark') return 'moon-o'
  if (theme.mode === 'light') return 'sun-o'
  return 'desktop-o'
})

function toggleTheme() {
  const next = theme.toggleNext()
  const map = { auto: '跟随系统', light: '浅色模式', dark: '深色模式' }
  showToast({ message: `已切换为${map[next] || '默认模式'}`, position: 'bottom' })
}
</script>

<style scoped>
.app-layout {
  min-height: 100dvh;
}

/* 桌面端：侧边栏 + 主内容 */
.app-layout.is-desktop {
  display: flex;
  align-items: flex-start;
}

.app-sidebar {
  width: 240px;
  flex-shrink: 0;
  position: sticky;
  top: 0;
  height: 100dvh;
  background: var(--surface);
  border-right: 1px solid var(--border);
  display: flex;
  flex-direction: column;
  padding: 24px 0 18px;
  box-sizing: border-box;
  z-index: 50;
}

.sidebar-brand {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 0 20px 24px;
}

.brand-logo-box {
  width: 40px;
  height: 40px;
  border-radius: 10px;
  overflow: hidden;
  box-shadow: 0 4px 12px rgba(15, 169, 140, 0.25);
}

.brand-logo-icon {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}

.brand-text-wrap {
  display: flex;
  flex-direction: column;
}

.brand-title {
  font-size: 16px;
  font-weight: 700;
  color: var(--text-primary);
  letter-spacing: -0.2px;
  line-height: 1.2;
}

.brand-subtitle {
  font-size: 11px;
  font-weight: 500;
  color: var(--text-tertiary);
  margin-top: 2px;
}

.sidebar-nav {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 0 12px;
  flex: 1;
  overflow-y: auto;
  min-height: 0;
}

.sidebar-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 14px;
  border-radius: 10px;
  color: var(--text-secondary);
  font-size: 14px;
  font-weight: 500;
  transition: all 0.18s cubic-bezier(0.4, 0, 0.2, 1);
  text-decoration: none;
}

.item-icon-box {
  width: 28px;
  height: 28px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 6px;
  transition: all 0.18s ease;
}

.item-label {
  flex: 1;
}

.sidebar-item:hover {
  background: var(--surface-2);
  color: var(--text-primary);
}

.sidebar-item:hover .item-icon-box {
  transform: scale(1.08);
}

.sidebar-item.router-link-active {
  background: rgba(59, 130, 246, 0.12);
  color: var(--color-primary);
  font-weight: 600;
}

.sidebar-item.router-link-active .item-icon-box {
  color: var(--color-primary);
}

body.dark .sidebar-item.router-link-active {
  background: rgba(56, 189, 248, 0.16);
  color: var(--color-primary);
}

.sidebar-footer {
  padding: 16px 14px 0;
  border-top: 1px solid var(--border);
  margin-top: 8px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.sidebar-theme-toggle {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 9px 12px;
  border-radius: 8px;
  background: var(--surface-2);
  border: 1px solid var(--border);
  color: var(--text-secondary);
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.15s ease;
}

.sidebar-theme-toggle:hover {
  background: var(--surface-3);
  color: var(--text-primary);
  border-color: var(--border-strong);
}

.sidebar-new-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 11px 16px;
  border-radius: 10px;
  background: linear-gradient(135deg, #2563eb, #3b82f6);
  color: #ffffff;
  font-size: 14px;
  font-weight: 600;
  box-shadow: 0 4px 14px rgba(37, 99, 235, 0.28);
  transition: all 0.18s ease;
  text-decoration: none;
}

.sidebar-new-btn:hover {
  transform: translateY(-1px);
  box-shadow: 0 6px 18px rgba(37, 99, 235, 0.36);
  filter: brightness(1.05);
}

.sidebar-new-btn:active {
  transform: translateY(0);
}

.offline-bar {
  position: sticky;
  top: 0;
  z-index: 30;
}

.app-main {
  flex: 1;
  min-width: 0;
  background: var(--app-bg);
  min-height: 100dvh;
}

/* 移动端：保持居中的手机视图（平板/大屏手机） */
@media (max-width: 1023px) {
  .app-main {
    max-width: 720px;
    margin: 0 auto;
    min-height: 100dvh;
    background: var(--app-bg);
    box-shadow: var(--shadow-sm);
    position: relative;
  }
}
</style>
