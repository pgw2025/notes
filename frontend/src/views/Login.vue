<template>
  <div class="auth-page">
    <div class="auth-theme-toggle">
      <button class="theme-btn" @click="toggleTheme" :title="`当前模式: ${theme.mode}`">
        <van-icon :name="themeIcon" size="18" />
      </button>
    </div>

    <div class="auth-card">
      <div class="auth-header">
        <div class="brand-badge">📝</div>
        <h1>随心笔记</h1>
        <p>随时随地记录灵感与思考</p>
      </div>

      <van-form @submit="onSubmit" class="login-form">
        <div class="fields-box">
          <van-field
            v-model="form.account"
            name="account"
            label="邮箱 / 用户名"
            placeholder="请输入邮箱或用户名"
            clearable
            :rules="[
              { required: true, message: '请输入邮箱或用户名' }
            ]"
          />
          <van-field
            v-model="form.password"
            type="password"
            name="password"
            label="密码"
            placeholder="请输入密码"
            clearable
            :rules="[{ required: true, message: '请输入密码' }]"
          />
        </div>

        <div class="auth-actions">
          <van-button round block type="primary" native-type="submit" :loading="loading" size="large">
            登 录
          </van-button>
        </div>
      </van-form>

      <div class="auth-footer">
        <div class="auth-footer-links">
          <span>还没有账号？<router-link to="/register" class="register-link">免费注册</router-link></span>
        </div>
        <div class="admin-entry-wrap">
          <a href="/admin/login" class="admin-login-link" title="管理员进入后台系统">
            <van-icon name="manager-o" size="14" class="admin-icon" />
            <span>进入后台管理系统</span>
            <van-icon name="arrow" size="11" class="admin-arrow" />
          </a>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { useThemeStore, MODES } from '../stores/theme'

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()
const theme = useThemeStore()
const loading = ref(false)

const form = reactive({ account: '', password: '' })

const themeIcon = computed(() => {
  if (theme.mode === 'dark') return 'moon-o'
  if (theme.mode === 'light') return 'sun-o'
  return 'desktop-o'
})

function toggleTheme() {
  const next = theme.mode === 'dark' ? 'light' : theme.mode === 'light' ? 'auto' : 'dark'
  theme.setMode(next)
}

async function onSubmit() {
  loading.value = true
  try {
    await auth.login(form.account, form.password)
    router.replace(route.query.redirect || '/notes')
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.auth-page {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  padding: 24px 16px;
  background: var(--app-bg);
  position: relative;
}

.auth-theme-toggle {
  position: absolute;
  top: 16px;
  right: 16px;
}

.theme-btn {
  background: var(--surface);
  border: 1px solid var(--border);
  width: 36px;
  height: 36px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--text-secondary);
  cursor: pointer;
  box-shadow: var(--shadow-xs);
  transition: all 0.2s ease;
}

.theme-btn:hover {
  color: var(--color-primary);
  border-color: var(--color-primary);
  transform: rotate(15deg);
}

.auth-card {
  width: 100%;
  max-width: 400px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 16px;
  padding: 36px 24px 28px;
  box-shadow: var(--shadow-md);
}

.auth-header {
  text-align: center;
  margin-bottom: 28px;
}

.brand-badge {
  font-size: 44px;
  line-height: 1;
  margin-bottom: 12px;
  display: inline-block;
  transform: rotate(-5deg);
}

.auth-header h1 {
  margin: 0 0 6px;
  font-size: 22px;
  font-weight: 700;
  color: var(--text-primary);
}

.auth-header p {
  margin: 0;
  color: var(--text-tertiary);
  font-size: 13.5px;
}

.fields-box {
  background: var(--surface-2);
  border-radius: 12px;
  overflow: hidden;
  border: 1px solid var(--border);
  margin-bottom: 20px;
}

.fields-box :deep(.van-cell) {
  background: transparent;
  padding: 12px 14px;
}

.auth-actions {
  margin-top: 16px;
}

.auth-footer {
  text-align: center;
  margin-top: 20px;
  font-size: 13.5px;
  color: var(--text-tertiary);
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.auth-footer-links {
  display: flex;
  align-items: center;
  justify-content: center;
}

.register-link {
  color: var(--color-primary);
  font-weight: 600;
  text-decoration: none;
  margin-left: 4px;
}

.register-link:hover {
  text-decoration: underline;
}

.admin-entry-wrap {
  display: flex;
  justify-content: center;
  padding-top: 14px;
  border-top: 1px solid var(--divider);
}

.admin-login-link {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 14px;
  border-radius: 999px;
  font-size: 12.5px;
  font-weight: 500;
  color: var(--text-secondary);
  background: var(--surface-2);
  border: 1px solid var(--border);
  text-decoration: none;
  transition: all 0.2s ease;
}

.admin-login-link:hover {
  color: var(--color-primary);
  border-color: var(--color-primary);
  background: var(--surface-3, var(--surface));
  transform: translateY(-1px);
  box-shadow: var(--shadow-xs);
}

.admin-icon {
  color: var(--color-primary);
}

.admin-arrow {
  opacity: 0.6;
  transition: transform 0.2s ease;
}

.admin-login-link:hover .admin-arrow {
  transform: translateX(2px);
  opacity: 1;
}
</style>
