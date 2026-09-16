<template>
  <div class="app-layout" :class="{ 'is-desktop': isDesktop, 'sidebar-collapsed': isSidebarCollapsed && isDesktop }">
    <!-- 桌面端侧边栏 -->
    <aside v-if="showSidebar" class="app-sidebar" :class="{ 'is-collapsed': isSidebarCollapsed }">
      <div class="sidebar-brand">
        <div class="brand-logo-box" @click="toggleSidebarCollapse" title="点击折叠/展开侧边栏 (Ctrl+\)">
          <img src="/pwa-192x192.png" alt="云笺笔记" class="brand-logo-icon" />
        </div>
        <div v-show="!isSidebarCollapsed" class="brand-text-wrap">
          <span class="brand-title">云笺笔记</span>
          <span class="brand-subtitle">Cloud Notes</span>
        </div>
        <button
          class="sidebar-collapse-btn"
          :title="isSidebarCollapsed ? '展开侧边栏 (Ctrl+\\)' : '收起侧边栏 (Ctrl+\\)'"
          @click="toggleSidebarCollapse"
        >
          <van-icon :name="isSidebarCollapsed ? 'arrow' : 'arrow-left'" size="14" />
        </button>
      </div>

      <!-- 搜索/命令快捷入口 -->
      <div v-if="!isSidebarCollapsed" class="sidebar-quick-search" @click="showPalette = true">
        <van-icon name="search" size="14" />
        <span class="search-placeholder">全局搜索或命令...</span>
        <kbd class="sidebar-kbd">⌘K</kbd>
      </div>
      <div v-else class="sidebar-collapsed-icon-btn" title="全局搜索 (Ctrl+K)" @click="showPalette = true">
        <van-icon name="search" size="18" />
      </div>

      <nav class="sidebar-nav">
        <router-link
          v-for="item in navItems"
          :key="item.to"
          :to="item.to"
          class="sidebar-item"
          :title="isSidebarCollapsed ? item.label : undefined"
        >
          <div class="item-icon-box">
            <van-icon :name="item.icon" size="18" />
          </div>
          <span v-show="!isSidebarCollapsed" class="item-label">{{ item.label }}</span>
        </router-link>

        <!-- 管理员后台直通车 -->
        <a
          v-if="isAdmin"
          href="/admin/"
          target="_blank"
          class="sidebar-item admin-portal-link"
          :title="isSidebarCollapsed ? '管理后台' : undefined"
        >
          <div class="item-icon-box">
            <van-icon name="shield-o" size="18" />
          </div>
          <span v-show="!isSidebarCollapsed" class="item-label">管理后台</span>
          <span v-show="!isSidebarCollapsed" class="admin-badge">Admin</span>
        </a>

        <!-- 侧边栏分类树 + 标签树 (折叠时隐藏) -->
        <template v-if="!isSidebarCollapsed">
          <SidebarCategories :categories="categoryStore.categories" />
          <SidebarTags :tags="tagStore.tags" />
        </template>
      </nav>

      <div class="sidebar-footer">
        <button
          class="sidebar-theme-toggle"
          @click="toggleTheme"
          :title="`当前主题: ${themeLabel}`"
        >
          <van-icon :name="themeIcon" size="15" />
          <span v-show="!isSidebarCollapsed">{{ themeLabel }}</span>
        </button>

        <router-link
          to="/notes/new"
          class="sidebar-new-btn"
          :title="isSidebarCollapsed ? '新建笔记 (Ctrl+N)' : undefined"
        >
          <van-icon name="plus" size="16" />
          <span v-show="!isSidebarCollapsed">新建笔记</span>
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

    <!-- 全局命令面板 (Ctrl+K / Cmd+K) -->
    <CommandPalette v-model:show="showPalette" />
  </div>
</template>

<script setup>
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showToast } from 'vant'
import { useResponsive } from './composables/useResponsive'
import { useAuthStore } from './stores/auth'
import { useThemeStore } from './stores/theme'
import { useTagStore } from './stores/tag'
import { useCategoryStore } from './stores/category'
import { useOfflineStore } from './stores/offline'
import SidebarTags from './components/SidebarTags.vue'
import SidebarCategories from './components/SidebarCategories.vue'
import CommandPalette from './components/CommandPalette.vue'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const theme = useThemeStore()
const tagStore = useTagStore()
const categoryStore = useCategoryStore()
const offline = useOfflineStore()
const { isDesktop } = useResponsive()

const showPalette = ref(false)
const isSidebarCollapsed = ref(localStorage.getItem('sidebar_collapsed') === 'true')

function toggleSidebarCollapse() {
  isSidebarCollapsed.value = !isSidebarCollapsed.value
  localStorage.setItem('sidebar_collapsed', isSidebarCollapsed.value ? 'true' : 'false')
}

const isAdmin = computed(() => {
  return auth.user?.roles?.includes('Admin') || auth.user?.isAdmin === true
})

onMounted(() => {
  offline.init()
  const token = localStorage.getItem('token')
  if (token) {
    auth.fetchUser()
    tagStore.fetchTags()
    categoryStore.fetchCategories()
  }
  window.addEventListener('keydown', handleGlobalKey)
})

onUnmounted(() => {
  window.removeEventListener('keydown', handleGlobalKey)
})

function handleGlobalKey(e) {
  const isCtrlOrCmd = e.ctrlKey || e.metaKey

  // Ctrl/Cmd + K -> Command Palette
  if (isCtrlOrCmd && (e.key === 'k' || e.key === 'K')) {
    e.preventDefault()
    showPalette.value = true
    return
  }

  // Ctrl/Cmd + \ -> Toggle sidebar
  if (isCtrlOrCmd && e.key === '\\') {
    e.preventDefault()
    toggleSidebarCollapse()
    return
  }

  // Ctrl/Cmd + N -> New Note (when not in input/textarea)
  if (isCtrlOrCmd && (e.key === 'n' || e.key === 'N') && !['INPUT', 'TEXTAREA'].includes(e.target?.tagName)) {
    e.preventDefault()
    router.push('/notes/new')
    return
  }
}

// 当切换页面时，同步刷新侧栏标签/分类列表与用户信息
watch(
  () => route.fullPath,
  () => {
    const token = localStorage.getItem('token')
    if (token && !route.meta.public) {
      tagStore.fetchTags()
      categoryStore.fetchCategories()
      if (!auth.user) auth.fetchUser()
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
  if (theme.mode === 'dark') return '深色'
  if (theme.mode === 'light') return '浅色'
  return '系统'
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
  padding: 16px 0 16px;
  box-sizing: border-box;
  z-index: 50;
  transition: width 0.2s cubic-bezier(0.4, 0, 0.2, 1);
}

.app-sidebar.is-collapsed {
  width: 64px;
  padding: 16px 0;
}

.sidebar-brand {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 0 14px 16px;
  position: relative;
}

.app-sidebar.is-collapsed .sidebar-brand {
  padding: 0 12px 16px;
  justify-content: center;
}

.brand-logo-box {
  width: 36px;
  height: 36px;
  border-radius: 9px;
  overflow: hidden;
  box-shadow: 0 4px 12px rgba(15, 169, 140, 0.25);
  cursor: pointer;
  flex-shrink: 0;
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
  flex: 1;
  min-width: 0;
}

.brand-title {
  font-size: 15px;
  font-weight: 700;
  color: var(--text-primary);
  letter-spacing: -0.2px;
  line-height: 1.2;
  white-space: nowrap;
}

.brand-subtitle {
  font-size: 11px;
  font-weight: 500;
  color: var(--text-tertiary);
  margin-top: 2px;
  white-space: nowrap;
}

.sidebar-collapse-btn {
  width: 24px;
  height: 24px;
  border-radius: 6px;
  border: 1px solid var(--border);
  background: var(--surface-2);
  color: var(--text-tertiary);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.15s ease;
  flex-shrink: 0;
}

.app-sidebar.is-collapsed .sidebar-collapse-btn {
  display: none;
}

.sidebar-collapse-btn:hover {
  background: var(--surface-3);
  color: var(--text-primary);
  border-color: var(--border-strong);
}

.sidebar-quick-search {
  margin: 0 12px 14px;
  padding: 8px 10px;
  border-radius: 8px;
  background: var(--surface-2);
  border: 1px solid var(--border);
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  color: var(--text-tertiary);
  font-size: 12.5px;
  transition: all 0.15s ease;
}

.sidebar-quick-search:hover {
  border-color: var(--color-primary);
  color: var(--text-secondary);
  background: var(--surface);
}

.search-placeholder {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.sidebar-kbd {
  font-size: 10px;
  padding: 1px 5px;
  border-radius: 4px;
  background: var(--surface);
  border: 1px solid var(--border);
  font-family: inherit;
  color: var(--text-tertiary);
}

.sidebar-collapsed-icon-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  margin: 0 auto 12px;
  border-radius: 8px;
  background: var(--surface-2);
  color: var(--text-secondary);
  cursor: pointer;
  transition: all 0.15s ease;
}

.sidebar-collapsed-icon-btn:hover {
  background: rgba(59, 130, 246, 0.15);
  color: var(--color-primary);
}

.admin-portal-link {
  color: #8b5cf6;
}

.admin-badge {
  font-size: 10px;
  padding: 1px 6px;
  border-radius: 10px;
  background: rgba(139, 92, 246, 0.15);
  color: #8b5cf6;
  font-weight: 700;
  text-transform: uppercase;
}

.app-sidebar.is-collapsed .sidebar-item {
  padding: 10px 0;
  justify-content: center;
}

.app-sidebar.is-collapsed .sidebar-footer {
  padding: 12px 6px 0;
}

.app-sidebar.is-collapsed .sidebar-theme-toggle,
.app-sidebar.is-collapsed .sidebar-new-btn {
  padding: 10px 0;
  justify-content: center;
  width: 40px;
  margin: 0 auto;
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
