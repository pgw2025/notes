<template>
  <div class="app-layout" :class="{ 'is-desktop': isDesktop }">
    <!-- 桌面端侧边栏 -->
    <aside v-if="showSidebar" class="app-sidebar">
      <div class="sidebar-brand">
        <span class="brand-icon">📝</span>
        <span class="brand-text">笔记</span>
      </div>
      <nav class="sidebar-nav">
        <router-link
          v-for="item in navItems"
          :key="item.to"
          :to="item.to"
          class="sidebar-item"
        >
          <van-icon :name="item.icon" size="20" />
          <span>{{ item.label }}</span>
        </router-link>
      </nav>
      <div class="sidebar-footer">
        <router-link to="/notes/new" class="sidebar-new-btn">
          <van-icon name="plus" size="16" />
          <span>新建笔记</span>
        </router-link>
      </div>
    </aside>

    <!-- 主内容区 -->
    <main class="app-main">
      <router-view v-slot="{ Component }">
        <keep-alive include="NotesList">
          <component :is="Component" />
        </keep-alive>
      </router-view>
    </main>

    <!-- 移动端底部导航 -->
    <van-tabbar v-if="!isDesktop && showTabbar" route safe-area-inset-bottom>
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
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useResponsive } from './composables/useResponsive'

const route = useRoute()
const { isDesktop } = useResponsive()

const showTabbar = computed(() => route.meta.tabbar !== false)
const showSidebar = computed(() => isDesktop.value && route.meta.public !== true)

const navItems = [
  { to: '/notes', icon: 'notes-o', label: '笔记' },
  { to: '/timeline', icon: 'clock-o', label: '时间线' },
  { to: '/search', icon: 'search', label: '搜索' },
  { to: '/categories', icon: 'apps-o', label: '分类' },
  { to: '/settings', icon: 'contact', label: '我的' }
]
</script>

<style scoped>
.app-layout {
  min-height: 100vh;
}

/* 桌面端：侧边栏 + 主内容 */
.app-layout.is-desktop {
  display: flex;
  align-items: flex-start;
}

.app-sidebar {
  width: 220px;
  flex-shrink: 0;
  position: sticky;
  top: 0;
  height: 100vh;
  background: var(--surface);
  border-right: 1px solid var(--border);
  display: flex;
  flex-direction: column;
  padding: 20px 0;
  box-sizing: border-box;
}

.sidebar-brand {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 0 20px 24px;
  font-size: 20px;
  font-weight: 700;
  color: var(--text-primary);
}
.brand-icon {
  font-size: 24px;
}

.sidebar-nav {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 0 12px;
  flex: 1;
}

.sidebar-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 11px 16px;
  border-radius: 8px;
  color: var(--text-primary);
  font-size: 15px;
  transition: background 0.2s, color 0.2s;
}
.sidebar-item:hover {
  background: var(--surface-2);
}
.sidebar-item.router-link-active {
  background: var(--color-primary);
  color: #fff;
}

.sidebar-footer {
  padding: 12px 12px 0;
  border-top: 1px solid var(--border);
  margin-top: 8px;
}
.sidebar-new-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 10px;
  border-radius: 8px;
  background: var(--color-primary);
  color: #fff;
  font-size: 14px;
  font-weight: 500;
}
.sidebar-new-btn:hover {
  filter: brightness(0.92);
}

.app-main {
  flex: 1;
  min-width: 0;
  background: var(--app-bg);
  min-height: 100vh;
}

/* 移动端：保持居中的手机视图（平板/大屏手机） */
@media (max-width: 1023px) {
  .app-main {
    max-width: 720px;
    margin: 0 auto;
    min-height: 100vh;
    background: var(--app-bg);
    box-shadow: var(--shadow-sm);
    position: relative;
  }
}
</style>
