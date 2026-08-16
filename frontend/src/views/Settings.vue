<template>
  <div class="page">
    <van-nav-bar title="我的" />

    <div class="profile">
      <van-image round width="64" height="64" :src="avatar" />
      <div class="profile-info">
        <div class="profile-name">{{ auth.user?.displayName || auth.user?.email || '未登录' }}</div>
        <div class="profile-email" v-if="auth.user?.email">{{ auth.user.email }}</div>
      </div>
    </div>

    <van-cell-group inset style="margin-top: 16px">
      <van-cell title="标签管理" is-link to="/tags" icon="bookmark-o" />
      <van-cell title="分类管理" is-link to="/categories" icon="apps-o" />
    </van-cell-group>

    <van-cell-group inset style="margin-top: 16px">
      <van-cell title="关于" value="笔记 v1.0" />
    </van-cell-group>

    <div class="logout">
      <van-button round block type="danger" plain @click="onLogout">退出登录</van-button>
    </div>
  </div>
</template>

<script setup>
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { showConfirmDialog, showToast } from 'vant'
import { useAuthStore } from '../stores/auth'

const router = useRouter()
const auth = useAuthStore()
const avatar = 'data:image/svg+xml,' + encodeURIComponent(
  '<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64"><rect width="64" height="64" rx="32" fill="#1989fa"/><text x="32" y="40" font-size="28" fill="#fff" text-anchor="middle" font-family="sans-serif">N</text></svg>'
)

onMounted(() => {
  if (auth.isLoggedIn && !auth.user) {
    auth.fetchUser()
  }
})

async function onLogout() {
  try {
    await showConfirmDialog({ title: '提示', message: '确定要退出登录吗？' })
    auth.logout()
    showToast('已退出登录')
    router.replace('/login')
  } catch {
    // 取消
  }
}
</script>

<style scoped>
.page {
  min-height: 100vh;
  padding-bottom: 80px;
}
.profile {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 24px 20px;
  background: #fff;
}
.profile-name {
  font-size: 18px;
  font-weight: 600;
}
.profile-email {
  font-size: 13px;
  color: #969799;
  margin-top: 4px;
}
.logout {
  margin: 32px 16px 0;
}
</style>
