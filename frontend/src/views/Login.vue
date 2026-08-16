<template>
  <div class="auth-page">
    <div class="auth-header">
      <div class="logo">📝</div>
      <h1>笔记</h1>
      <p>记录你的灵感与思考</p>
    </div>

    <van-form @submit="onSubmit">
      <van-cell-group inset>
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
          name="password"
          label="密码"
          placeholder="请输入密码"
          :rules="[{ required: true, message: '请输入密码' }]"
        />
      </van-cell-group>

      <div class="auth-actions">
        <van-button round block type="primary" native-type="submit" :loading="loading">
          登录
        </van-button>
      </div>
    </van-form>

    <div class="auth-footer">
      还没有账号？<router-link to="/register">立即注册</router-link>
    </div>
  </div>
</template>

<script setup>
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()
const loading = ref(false)
const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

const form = reactive({ email: '', password: '' })

async function onSubmit() {
  loading.value = true
  try {
    await auth.login(form.email, form.password)
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
  padding: 24px;
}
.auth-header {
  text-align: center;
  margin-bottom: 32px;
}
.logo {
  font-size: 56px;
  line-height: 1;
  margin-bottom: 12px;
}
.auth-header h1 {
  margin: 0 0 6px;
  font-size: 26px;
  font-weight: 700;
}
.auth-header p {
  margin: 0;
  color: #969799;
  font-size: 14px;
}
.auth-actions {
  margin: 20px 16px 0;
}
.auth-footer {
  text-align: center;
  margin-top: 20px;
  font-size: 14px;
  color: #969799;
}

/* 桌面端：卡片式表单 */
@media (min-width: 1024px) {
  .auth-page {
    padding: 40px 24px;
    align-items: center;
  }
  .auth-page > .auth-header,
  .auth-page > .van-form {
    width: 100%;
    max-width: 420px;
    background: #fff;
    border-radius: 12px;
  }
  .auth-page > .auth-header {
    padding: 40px 32px 24px;
    margin-bottom: 0;
    box-shadow: 0 2px 12px rgba(0, 0, 0, 0.04);
  }
  .auth-page > .van-form {
    padding: 24px 32px 32px;
    margin-top: 12px;
    box-shadow: 0 2px 12px rgba(0, 0, 0, 0.04);
  }
  .auth-page > .van-form :deep(.van-cell-group--inset) {
    margin: 0;
  }
  .auth-actions {
    margin: 20px 0 0;
  }
  .auth-footer {
    max-width: 420px;
  }
}
</style>
