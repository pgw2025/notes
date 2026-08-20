<template>
  <div class="auth-page">
    <van-nav-bar title="注册账号" left-arrow @click-left="$router.back()" />

    <van-form @submit="onSubmit">
      <van-cell-group inset style="margin-top: 16px">
        <van-field
          v-model="form.displayName"
          label="昵称"
          placeholder="选填，如何称呼你"
          maxlength="20"
        />
        <van-field
          v-model="form.email"
          name="email"
          label="邮箱"
          placeholder="请输入邮箱"
          :rules="[
            { required: true, message: '请输入邮箱' },
            { pattern: emailPattern, message: '邮箱格式不正确' }
          ]"
        />
        <van-field
          v-model="form.password"
          type="password"
          label="密码"
          placeholder="至少 6 位"
          :rules="[{ required: true, message: '请输入密码' }, { validator: minLen, message: '密码至少 6 位' }]"
        />
        <van-field
          v-model="form.confirm"
          type="password"
          label="确认密码"
          placeholder="请再次输入密码"
          :rules="[{ required: true, message: '请确认密码' }, { validator: samePassword, message: '两次密码不一致' }]"
        />
      </van-cell-group>

      <div class="auth-actions">
        <van-button round block type="primary" native-type="submit" :loading="loading">
          注册
        </van-button>
      </div>
    </van-form>

    <div class="auth-footer">
      已有账号？<router-link to="/login">去登录</router-link>
    </div>
  </div>
</template>

<script setup>
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const router = useRouter()
const auth = useAuthStore()
const loading = ref(false)
const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

const form = reactive({ displayName: '', email: '', password: '', confirm: '' })

const minLen = (v) => (v && v.length >= 6) || false
const samePassword = (v) => v === form.password

async function onSubmit() {
  loading.value = true
  try {
    await auth.register(form.email, form.password, form.displayName || undefined)
    router.replace('/notes')
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.auth-page {
  min-height: 100vh;
}
.auth-actions {
  margin: 20px 16px 0;
}
.auth-footer {
  text-align: center;
  margin-top: 20px;
  font-size: 14px;
  color: var(--text-tertiary);
}

/* 桌面端：卡片式表单 */
@media (min-width: 1024px) {
  .auth-page {
    max-width: 480px;
    margin: 40px auto;
    min-height: auto;
    padding: 0 24px;
  }
  .auth-page :deep(.van-form) {
    background: var(--surface);
    border-radius: 12px;
    padding: 24px 32px 32px;
    box-shadow: var(--shadow-sm);
  }
  .auth-page :deep(.van-cell-group--inset) {
    margin: 0;
  }
  .auth-actions {
    margin: 20px 0 0;
  }
}
</style>
