<template>
  <div class="admin-app-wrapper">
    <!-- Desktop Sidebar -->
    <aside class="desktop-sidebar hidden-mobile">
      <div class="brand-header">
        <div class="brand-logo-icon">
          <el-icon :size="20"><Document /></el-icon>
        </div>
        <div class="brand-text">
          <div class="brand-title">Notes Admin</div>
          <div class="brand-subtitle">管理控制台</div>
        </div>
      </div>

      <div class="sidebar-menu-wrapper">
        <el-menu
          :default-active="activeMenu"
          router
          class="sidebar-menu"
          background-color="transparent"
          text-color="#94a3b8"
          active-text-color="#ffffff"
        >
          <el-menu-item index="/dashboard">
            <el-icon><Odometer /></el-icon>
            <span>仪表盘概览</span>
          </el-menu-item>
          <el-menu-item index="/users">
            <el-icon><User /></el-icon>
            <span>用户管理</span>
          </el-menu-item>
          <el-menu-item index="/notes">
            <el-icon><Notebook /></el-icon>
            <span>笔记管理</span>
          </el-menu-item>
          <el-menu-item index="/categories">
            <el-icon><FolderOpened /></el-icon>
            <span>分类管理</span>
          </el-menu-item>
          <el-menu-item index="/tags">
            <el-icon><PriceTag /></el-icon>
            <span>标签管理</span>
          </el-menu-item>
          <el-menu-item index="/attachments">
            <el-icon><Paperclip /></el-icon>
            <span>附件管理</span>
          </el-menu-item>
        </el-menu>
      </div>

      <div class="sidebar-footer">
        <a href="/" class="client-link-btn" title="访问前台用户端">
          <el-icon><Position /></el-icon>
          <span>进入笔记前台</span>
        </a>
      </div>
    </aside>

    <!-- Main Content Area -->
    <div class="admin-main-container">
      <!-- Top Navigation Bar -->
      <header class="admin-header">
        <div class="header-left">
          <!-- Mobile Menu Trigger -->
          <button class="mobile-menu-btn visible-mobile" @click="mobileDrawer = true" aria-label="打开菜单">
            <el-icon :size="22"><Fold /></el-icon>
          </button>
          
          <div class="page-title-box">
            <div class="mobile-logo-text visible-mobile">
              <span class="logo-emoji">📝</span>
              <span class="logo-name">Notes</span>
            </div>
            <h1 class="page-title">{{ currentTitle }}</h1>
          </div>
        </div>

        <div class="header-right">
          <!-- Theme Switcher Button -->
          <el-dropdown trigger="click" @command="handleThemeCommand">
            <button class="theme-toggle-btn" :title="`当前主题：${currentThemeMeta.label}`">
              <el-icon :size="18">
                <Sunny v-if="theme.mode === 'light'" />
                <Moon v-else-if="theme.mode === 'dark'" />
                <Monitor v-else />
              </el-icon>
              <span class="theme-mode-label hidden-mobile">{{ currentThemeMeta.label }}</span>
            </button>
            <template #dropdown>
              <el-dropdown-menu class="theme-dropdown-menu">
                <el-dropdown-item command="light" :class="{ 'is-active-theme': theme.mode === 'light' }">
                  <el-icon><Sunny /></el-icon>
                  <span>浅色模式</span>
                </el-dropdown-item>
                <el-dropdown-item command="dark" :class="{ 'is-active-theme': theme.mode === 'dark' }">
                  <el-icon><Moon /></el-icon>
                  <span>深色模式</span>
                </el-dropdown-item>
                <el-dropdown-item command="auto" :class="{ 'is-active-theme': theme.mode === 'auto' }">
                  <el-icon><Monitor /></el-icon>
                  <span>跟随系统</span>
                </el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>

          <a href="/" class="front-btn hidden-mobile">
            <el-icon><Compass /></el-icon>
            <span>前台主站</span>
          </a>

          <el-dropdown trigger="click" @command="handleCommand">
            <div class="user-pill">
              <el-avatar :size="32" class="user-avatar" :src="auth.avatarUrl">
                {{ initial }}
              </el-avatar>
              <div class="user-meta hidden-mobile">
                <span class="user-name">{{ auth.displayName || auth.email }}</span>
                <span class="user-role-tag">超级管理员</span>
              </div>
              <el-icon class="dropdown-caret"><ArrowDown /></el-icon>
            </div>
            <template #dropdown>
              <el-dropdown-menu class="user-dropdown-menu">
                <div class="dropdown-header visible-mobile">
                  <div class="dropdown-name">{{ auth.displayName || auth.email }}</div>
                  <div class="dropdown-email">{{ auth.email }}</div>
                </div>
                <el-dropdown-item command="toFront">
                  <el-icon><Position /></el-icon>
                  <span>访问笔记前台</span>
                </el-dropdown-item>
                <el-dropdown-item divided command="logout" class="logout-item">
                  <el-icon><SwitchButton /></el-icon>
                  <span>退出登录</span>
                </el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </header>

      <!-- Scrollable Main View -->
      <main class="admin-content-view">
        <router-view v-slot="{ Component }">
          <transition name="fade-transform" mode="out-in">
            <component :is="Component" />
          </transition>
        </router-view>
      </main>

      <!-- Mobile Bottom Navigation Bar for rapid one-thumb switching -->
      <nav class="mobile-bottom-nav visible-mobile">
        <router-link to="/dashboard" class="nav-tab-item" :class="{ active: activeMenu === '/dashboard' }">
          <el-icon><Odometer /></el-icon>
          <span>概览</span>
        </router-link>
        <router-link to="/users" class="nav-tab-item" :class="{ active: activeMenu === '/users' }">
          <el-icon><User /></el-icon>
          <span>用户</span>
        </router-link>
        <router-link to="/notes" class="nav-tab-item" :class="{ active: activeMenu === '/notes' }">
          <el-icon><Notebook /></el-icon>
          <span>笔记</span>
        </router-link>
        <button class="nav-tab-item menu-trigger-tab" @click="mobileDrawer = true">
          <el-icon><Menu /></el-icon>
          <span>更多</span>
        </button>
      </nav>
    </div>

    <!-- Mobile Drawer Menu -->
    <el-drawer
      v-model="mobileDrawer"
      direction="ltr"
      size="280px"
      :with-header="false"
      class="mobile-nav-drawer"
    >
      <div class="drawer-inner">
        <div class="drawer-header">
          <div class="drawer-brand">
            <div class="brand-logo-icon">
              <el-icon :size="20"><Document /></el-icon>
            </div>
            <div>
              <div class="drawer-brand-title">Notes 后台管理</div>
              <div class="drawer-brand-sub">移动端快速控制台</div>
            </div>
          </div>
          <button class="drawer-close-btn" @click="mobileDrawer = false">
            <el-icon><Close /></el-icon>
          </button>
        </div>

        <div class="drawer-user-card">
          <el-avatar :size="40" class="drawer-avatar" :src="auth.avatarUrl">{{ initial }}</el-avatar>
          <div class="drawer-user-info">
            <div class="drawer-user-name">{{ auth.displayName || '管理员' }}</div>
            <div class="drawer-user-email">{{ auth.email }}</div>
          </div>
        </div>

        <!-- Theme Mode Switcher in Drawer -->
        <div class="drawer-theme-section">
          <div class="drawer-section-title">界面外观</div>
          <div class="drawer-theme-group">
            <button
              class="drawer-theme-btn"
              :class="{ active: theme.mode === 'light' }"
              @click="theme.setMode('light')"
            >
              <el-icon><Sunny /></el-icon>
              <span>浅色</span>
            </button>
            <button
              class="drawer-theme-btn"
              :class="{ active: theme.mode === 'dark' }"
              @click="theme.setMode('dark')"
            >
              <el-icon><Moon /></el-icon>
              <span>深色</span>
            </button>
            <button
              class="drawer-theme-btn"
              :class="{ active: theme.mode === 'auto' }"
              @click="theme.setMode('auto')"
            >
              <el-icon><Monitor /></el-icon>
              <span>自动</span>
            </button>
          </div>
        </div>

        <div class="drawer-nav-list">
          <div class="drawer-section-title">业务管理</div>
          <router-link
            v-for="item in navItems"
            :key="item.path"
            :to="item.path"
            class="drawer-nav-link"
            :class="{ active: activeMenu === item.path }"
            @click="mobileDrawer = false"
          >
            <el-icon class="nav-icon"><component :is="item.icon" /></el-icon>
            <span class="nav-title">{{ item.title }}</span>
            <el-icon class="arrow-right"><ArrowRight /></el-icon>
          </router-link>
        </div>

        <div class="drawer-footer">
          <a href="/" class="drawer-action-btn front-link">
            <el-icon><Position /></el-icon>
            <span>返回笔记前台</span>
          </a>
          <button class="drawer-action-btn logout-link" @click="handleLogout">
            <el-icon><SwitchButton /></el-icon>
            <span>退出登录</span>
          </button>
        </div>
      </div>
    </el-drawer>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Sunny, Moon, Monitor } from '@element-plus/icons-vue'
import { useAuthStore } from '../stores/auth'
import { useAdminThemeStore } from '../stores/theme'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const theme = useAdminThemeStore()

const mobileDrawer = ref(false)

const activeMenu = computed(() => route.path)
const currentTitle = computed(() => route.meta.title || '控制台')

const THEME_LABELS = {
  auto: '跟随系统',
  light: '浅色模式',
  dark: '深色模式'
}

const currentThemeMeta = computed(() => ({
  mode: theme.mode,
  label: THEME_LABELS[theme.mode] || '跟随系统'
}))

function handleThemeCommand(mode) {
  theme.setMode(mode)
  ElMessage.success(`已切换为${THEME_LABELS[mode] || '默认模式'}`)
}

const initial = computed(() => {
  const name = auth.displayName || auth.email || 'A'
  return name.charAt(0).toUpperCase()
})

const navItems = [
  { path: '/dashboard', title: '仪表盘概览', icon: 'Odometer' },
  { path: '/users', title: '用户管理', icon: 'User' },
  { path: '/notes', title: '笔记管理', icon: 'Notebook' },
  { path: '/categories', title: '分类管理', icon: 'FolderOpened' },
  { path: '/tags', title: '标签管理', icon: 'PriceTag' },
  { path: '/attachments', title: '附件管理', icon: 'Paperclip' }
]

function handleCommand(cmd) {
  if (cmd === 'logout') {
    handleLogout()
  } else if (cmd === 'toFront') {
    window.location.href = '/'
  }
}

function handleLogout() {
  auth.logout()
  mobileDrawer.value = false
  router.replace({ name: 'login' })
}
</script>

<style scoped>
.admin-app-wrapper {
  display: flex;
  width: 100vw;
  height: 100vh;
  overflow: hidden;
  background-color: var(--admin-bg);
}

/* Desktop Sidebar */
.desktop-sidebar {
  width: 230px;
  background-color: var(--admin-sidebar-bg);
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
  box-shadow: 2px 0 8px rgba(0, 0, 0, 0.15);
  z-index: 20;
}

.brand-header {
  height: 64px;
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 0 20px;
  background: rgba(255, 255, 255, 0.02);
  border-bottom: 1px solid rgba(255, 255, 255, 0.07);
}

.brand-logo-icon {
  width: 36px;
  height: 36px;
  border-radius: 8px;
  background: linear-gradient(135deg, #6366f1 0%, #4f46e5 100%);
  display: flex;
  align-items: center;
  justify-content: center;
  color: #ffffff;
  box-shadow: 0 4px 10px rgba(79, 70, 229, 0.4);
}

.brand-text {
  display: flex;
  flex-direction: column;
}

.brand-title {
  color: #f8fafc;
  font-size: 15px;
  font-weight: 700;
  letter-spacing: 0.5px;
}

.brand-subtitle {
  color: #64748b;
  font-size: 11px;
}

.sidebar-menu-wrapper {
  flex: 1;
  overflow-y: auto;
  padding: 12px 10px;
}

.sidebar-menu {
  border-right: none;
}

:deep(.sidebar-menu .el-menu-item) {
  height: 44px;
  line-height: 44px;
  border-radius: 8px;
  margin-bottom: 4px;
  font-size: 13.5px;
  transition: all 0.2s ease;
  font-weight: 500;
}

:deep(.sidebar-menu .el-menu-item:hover) {
  background-color: rgba(255, 255, 255, 0.06) !important;
  color: #f1f5f9 !important;
}

:deep(.sidebar-menu .el-menu-item.is-active) {
  background: linear-gradient(90deg, #4f46e5 0%, #6366f1 100%) !important;
  color: #ffffff !important;
  box-shadow: 0 4px 12px rgba(79, 70, 229, 0.35);
  font-weight: 600;
}

.sidebar-footer {
  padding: 16px;
  border-top: 1px solid rgba(255, 255, 255, 0.07);
}

.client-link-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  width: 100%;
  padding: 9px;
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  color: #cbd5e1;
  text-decoration: none;
  font-size: 13px;
  font-weight: 500;
  transition: all 0.2s ease;
}

.client-link-btn:hover {
  background: rgba(79, 70, 229, 0.2);
  border-color: #6366f1;
  color: #ffffff;
}

/* Main Container */
.admin-main-container {
  flex: 1;
  display: flex;
  flex-direction: column;
  height: 100vh;
  overflow: hidden;
  background-color: var(--admin-bg);
}

/* Header */
.admin-header {
  height: 60px;
  background-color: var(--admin-header-bg);
  border-bottom: 1px solid var(--admin-border);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 24px;
  flex-shrink: 0;
  z-index: 10;
  transition: background-color 0.2s ease, border-color 0.2s ease;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.mobile-menu-btn {
  background: none;
  border: none;
  padding: 6px;
  border-radius: 6px;
  color: var(--admin-text-main);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
}

.mobile-menu-btn:active {
  background: var(--admin-subcard-bg);
}

.page-title-box {
  display: flex;
  align-items: center;
  gap: 8px;
}

.mobile-logo-text {
  font-size: 14px;
  font-weight: 700;
  color: var(--admin-text-main);
  display: flex;
  align-items: center;
  gap: 4px;
  margin-right: 4px;
}

.page-title {
  margin: 0;
  font-size: 17px;
  font-weight: 600;
  color: var(--admin-text-main);
  letter-spacing: -0.2px;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

/* Theme Toggle Button */
.theme-toggle-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 10px;
  border-radius: 6px;
  border: 1px solid var(--admin-border);
  background: var(--admin-subcard-bg);
  color: var(--admin-text-main);
  cursor: pointer;
  font-size: 12.5px;
  font-weight: 500;
  transition: all 0.15s ease;
}

.theme-toggle-btn:hover {
  border-color: var(--admin-primary);
  color: var(--admin-primary);
}

.theme-mode-label {
  font-size: 12.5px;
}

.is-active-theme {
  color: var(--admin-primary) !important;
  font-weight: 600;
}

.front-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 14px;
  border-radius: 6px;
  font-size: 13px;
  color: var(--admin-text-sub);
  text-decoration: none;
  background: var(--admin-subcard-bg);
  border: 1px solid var(--admin-border);
  transition: all 0.15s ease;
}

.front-btn:hover {
  background: var(--admin-border);
  color: var(--admin-text-main);
}

.user-pill {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 4px 8px 4px 4px;
  border-radius: 20px;
  cursor: pointer;
  transition: background 0.2s ease;
}

.user-pill:hover {
  background: var(--admin-subcard-bg);
}

.user-avatar {
  background: linear-gradient(135deg, #6366f1 0%, #4f46e5 100%);
  color: #ffffff;
  font-weight: 600;
  font-size: 13px;
}

.user-meta {
  display: flex;
  flex-direction: column;
  text-align: left;
}

.user-name {
  font-size: 13px;
  font-weight: 600;
  color: var(--admin-text-main);
  line-height: 1.2;
}

.user-role-tag {
  font-size: 10px;
  color: var(--admin-primary);
  font-weight: 500;
}

.dropdown-caret {
  font-size: 12px;
  color: var(--admin-text-muted);
}

/* Content Area */
.admin-content-view {
  flex: 1;
  overflow-y: auto;
  padding: 24px;
  -webkit-overflow-scrolling: touch;
}

/* Transitions */
.fade-transform-enter-active,
.fade-transform-leave-active {
  transition: opacity 0.2s ease, transform 0.2s ease;
}

.fade-transform-enter-from {
  opacity: 0;
  transform: translateY(6px);
}

.fade-transform-leave-to {
  opacity: 0;
  transform: translateY(-6px);
}

/* Mobile Bottom Navigation Bar */
.mobile-bottom-nav {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  height: 56px;
  background: var(--admin-header-bg);
  border-top: 1px solid var(--admin-border);
  display: flex;
  align-items: center;
  justify-content: space-around;
  z-index: 100;
  padding-bottom: env(safe-area-inset-bottom, 0);
  box-shadow: 0 -2px 10px rgba(0, 0, 0, 0.04);
}

.nav-tab-item {
  flex: 1;
  height: 100%;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 3px;
  color: var(--admin-text-sub);
  text-decoration: none;
  font-size: 11px;
  border: none;
  background: transparent;
  cursor: pointer;
  transition: color 0.15s ease;
}

.nav-tab-item .el-icon {
  font-size: 20px;
}

.nav-tab-item.active {
  color: var(--admin-primary);
  font-weight: 600;
}

/* Mobile Drawer */
.drawer-inner {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #0f172a;
  color: #ffffff;
}

.drawer-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 20px 16px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

.drawer-brand {
  display: flex;
  align-items: center;
  gap: 10px;
}

.drawer-brand-title {
  font-size: 15px;
  font-weight: 700;
  color: #ffffff;
}

.drawer-brand-sub {
  font-size: 11px;
  color: #64748b;
}

.drawer-close-btn {
  background: rgba(255, 255, 255, 0.08);
  border: none;
  color: #cbd5e1;
  width: 32px;
  height: 32px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
}

.drawer-user-card {
  margin: 16px;
  padding: 12px;
  background: rgba(255, 255, 255, 0.04);
  border-radius: 12px;
  display: flex;
  align-items: center;
  gap: 12px;
  border: 1px solid rgba(255, 255, 255, 0.06);
}

.drawer-avatar {
  background: linear-gradient(135deg, #6366f1 0%, #4f46e5 100%);
  color: #fff;
  font-weight: 600;
}

.drawer-user-name {
  font-size: 14px;
  font-weight: 600;
  color: #f1f5f9;
}

.drawer-user-email {
  font-size: 12px;
  color: #94a3b8;
  word-break: break-all;
}

/* Theme Switcher inside Drawer */
.drawer-theme-section {
  padding: 0 16px 12px;
}

.drawer-theme-group {
  display: grid;
  grid-template-columns: 1fr 1fr 1fr;
  gap: 6px;
  background: rgba(255, 255, 255, 0.05);
  padding: 4px;
  border-radius: 8px;
  border: 1px solid rgba(255, 255, 255, 0.08);
}

.drawer-theme-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
  padding: 7px 4px;
  border-radius: 6px;
  border: none;
  background: transparent;
  color: #94a3b8;
  font-size: 12px;
  cursor: pointer;
  transition: all 0.15s ease;
}

.drawer-theme-btn.active {
  background: var(--admin-primary);
  color: #ffffff;
  font-weight: 600;
  box-shadow: 0 2px 6px rgba(99, 102, 241, 0.3);
}

.drawer-nav-list {
  flex: 1;
  overflow-y: auto;
  padding: 0 12px;
}

.drawer-section-title {
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: #64748b;
  padding: 8px 12px 6px;
  font-weight: 600;
}

.drawer-nav-link {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 14px;
  border-radius: 8px;
  color: #94a3b8;
  text-decoration: none;
  font-size: 14px;
  font-weight: 500;
  margin-bottom: 4px;
  transition: all 0.15s ease;
}

.drawer-nav-link .nav-icon {
  font-size: 18px;
}

.drawer-nav-link .arrow-right {
  margin-left: auto;
  font-size: 12px;
  color: #475569;
}

.drawer-nav-link.active {
  background: linear-gradient(90deg, #4f46e5 0%, #6366f1 100%);
  color: #ffffff;
  font-weight: 600;
}

.drawer-nav-link.active .arrow-right {
  color: #ffffff;
}

.drawer-footer {
  padding: 16px;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.drawer-action-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  width: 100%;
  padding: 10px;
  border-radius: 8px;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  text-decoration: none;
}

.drawer-action-btn.front-link {
  background: rgba(255, 255, 255, 0.06);
  border: 1px solid rgba(255, 255, 255, 0.1);
  color: #e2e8f0;
}

.drawer-action-btn.logout-link {
  background: rgba(239, 68, 68, 0.15);
  border: 1px solid rgba(239, 68, 68, 0.3);
  color: #f87171;
}

/* User Dropdown Header on Mobile */
.dropdown-header {
  padding: 8px 16px;
  border-bottom: 1px solid var(--admin-border);
}

.dropdown-name {
  font-size: 13px;
  font-weight: 600;
  color: var(--admin-text-main);
}

.dropdown-email {
  font-size: 11px;
  color: var(--admin-text-muted);
}

.logout-item {
  color: #ef4444;
}

/* Responsive Breakpoints */
@media (max-width: 768px) {
  .admin-header {
    height: 52px;
    padding: 0 14px;
  }
  .page-title {
    font-size: 15px;
  }
  .admin-content-view {
    padding: 14px 12px 76px 12px; /* space for bottom nav */
  }
}
</style>
