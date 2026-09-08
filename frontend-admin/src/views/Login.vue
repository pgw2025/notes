<template>
  <div class="login-page">
    <div class="login-decor-circle circle-1"></div>
    <div class="login-decor-circle circle-2"></div>

    <!-- Top Right Theme Switcher -->
    <div class="top-theme-toggle">
      <el-dropdown trigger="click" @command="handleThemeCommand">
        <button class="login-theme-btn" :title="`当前主题：${currentThemeMeta.label}`">
          <el-icon :size="16">
            <Sunny v-if="theme.mode === 'light'" />
            <Moon v-else-if="theme.mode === 'dark'" />
            <Monitor v-else />
          </el-icon>
          <span>{{ currentThemeMeta.label }}</span>
        </button>
        <template #dropdown>
          <el-dropdown-menu>
            <el-dropdown-item command="light">
              <el-icon><Sunny /></el-icon>
              <span>浅色模式</span>
            </el-dropdown-item>
            <el-dropdown-item command="dark">
              <el-icon><Moon /></el-icon>
              <span>深色模式</span>
            </el-dropdown-item>
            <el-dropdown-item command="auto">
              <el-icon><Monitor /></el-icon>
              <span>跟随系统</span>
            </el-dropdown-item>
          </el-dropdown-menu>
        </template>
      </el-dropdown>
    </div>

    <div class="login-card-box">
      <div class="login-brand-header">
        <div class="brand-badge-icon">
          <el-icon :size="24"><Document /></el-icon>
        </div>
        <h1 class="brand-title">Notes Admin</h1>
        <p class="brand-desc">笔记系统运营与管理控制台</p>
      </div>

      <div class="login-form-card">
        <el-form ref="formRef" :model="form" :rules="rules" label-position="top" @submit.prevent>
          <el-form-item label="管理员邮箱 / 用户名" prop="account">
            <el-input
              v-model="form.account"
              placeholder="请输入管理员邮箱或用户名"
              size="large"
              :prefix-icon="User"
              autocomplete="username"
            />
          </el-form-item>

          <el-form-item label="登录密码" prop="password">
            <el-input
              v-model="form.password"
              type="password"
              placeholder="请输入登录密码"
              size="large"
              :prefix-icon="Lock"
              show-password
              autocomplete="current-password"
              @keyup.enter="handleLogin"
            />
          </el-form-item>

          <el-button
            type="primary"
            size="large"
            class="submit-btn"
            :loading="loading"
            @click="handleLogin"
          >
            安全登录控制台
          </el-button>

          <!-- Quick Fill Demo Admin Account -->
          <div class="demo-fill-box">
            <div class="demo-tip">演示管理员账号：admin@notes.com / admin123</div>
            <el-button size="small" text type="primary" class="fill-btn" @click="fillAdminAccount">
              一键填入管理员账号
            </el-button>
          </div>
        </el-form>

        <div class="card-footer">
          <a href="/" class="front-link">
            <el-icon><Position /></el-icon>
            <span>返回笔记前台用户端</span>
          </a>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, computed } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { User, Lock, Document, Position, Sunny, Moon, Monitor } from '@element-plus/icons-vue'
import { useAuthStore } from '../stores/auth'
import { useAdminThemeStore } from '../stores/theme'

const router = useRouter()
const auth = useAuthStore()
const theme = useAdminThemeStore()
const formRef = ref()
const loading = ref(false)

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

const form = reactive({
  account: '',
  password: ''
})

const rules = {
  account: [{ required: true, message: '请输入管理员邮箱或用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }]
}

function fillAdminAccount() {
  form.account = 'admin@notes.com'
  form.password = 'admin123'
  ElMessage.info('已填入预设管理员账号')
}

async function handleLogin() {
  await formRef.value.validate()
  loading.value = true
  try {
    await auth.login(form.account, form.password)
    ElMessage.success('登录成功，欢迎进入管理控制台')
    router.replace({ name: 'dashboard' })
  } catch (e) {
    ElMessage.error(e.message || '登录失败，请检查账号密码')
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.login-page {
  min-height: 100vh;
  width: 100vw;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: var(--admin-bg);
  position: relative;
  overflow: hidden;
  padding: 20px 16px;
}

.top-theme-toggle {
  position: absolute;
  top: 16px;
  right: 16px;
  z-index: 20;
}

.login-theme-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  border-radius: 20px;
  background: var(--admin-card-bg);
  border: 1px solid var(--admin-border);
  color: var(--admin-text-main);
  cursor: pointer;
  font-size: 12px;
  font-weight: 500;
  box-shadow: var(--admin-shadow-sm);
  transition: all 0.15s ease;
}

.login-theme-btn:hover {
  border-color: var(--admin-primary);
  color: var(--admin-primary);
}

.login-decor-circle {
  position: absolute;
  border-radius: 50%;
  filter: blur(80px);
  pointer-events: none;
}

.circle-1 {
  width: 350px;
  height: 350px;
  background: rgba(99, 102, 241, 0.18);
  top: -80px;
  right: -80px;
}

.circle-2 {
  width: 400px;
  height: 400px;
  background: rgba(79, 70, 229, 0.15);
  bottom: -100px;
  left: -100px;
}

.login-card-box {
  width: 100%;
  max-width: 420px;
  z-index: 10;
}

.login-brand-header {
  text-align: center;
  margin-bottom: 24px;
}

.brand-badge-icon {
  width: 48px;
  height: 48px;
  margin: 0 auto 12px;
  background: linear-gradient(135deg, #6366f1 0%, #4f46e5 100%);
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #ffffff;
  box-shadow: 0 8px 20px rgba(79, 70, 229, 0.4);
}

.brand-title {
  font-size: 24px;
  font-weight: 700;
  color: var(--admin-text-main);
  margin: 0 0 6px 0;
  letter-spacing: -0.5px;
}

.brand-desc {
  font-size: 13.5px;
  color: var(--admin-text-sub);
  margin: 0;
}

.login-form-card {
  background: var(--admin-card-bg);
  border: 1px solid var(--admin-border);
  border-radius: 16px;
  padding: 28px 24px;
  box-shadow: var(--admin-shadow-lg);
}

.submit-btn {
  width: 100%;
  margin-top: 10px;
  height: 44px;
  font-size: 15px;
  font-weight: 600;
}

.demo-fill-box {
  margin-top: 18px;
  padding: 10px 12px;
  background: var(--admin-subcard-bg);
  border-radius: 8px;
  border: 1px dashed var(--admin-border);
  text-align: center;
}

.demo-tip {
  font-size: 11.5px;
  color: var(--admin-text-sub);
  margin-bottom: 4px;
}

.fill-btn {
  font-size: 12px;
  font-weight: 600;
  padding: 0;
}

.card-footer {
  margin-top: 20px;
  padding-top: 16px;
  border-top: 1px solid var(--admin-border);
  text-align: center;
}

.front-link {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: var(--admin-text-sub);
  font-size: 13px;
  text-decoration: none;
  transition: color 0.15s ease;
}

.front-link:hover {
  color: var(--admin-primary);
}

@media (max-width: 480px) {
  .login-form-card {
    padding: 22px 18px;
  }
}
</style>
