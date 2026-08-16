<template>
  <div class="page">
    <van-nav-bar title="我的" />

    <div class="profile">
      <div class="avatar-wrap" @click="triggerAvatarInput">
        <van-image
          round
          width="72"
          height="72"
          :src="avatarDisplayUrl"
          fit="cover"
          class="avatar-img"
        />
        <div class="avatar-edit-mask">
          <van-icon name="photograph" size="20" />
        </div>
      </div>
      <input
        ref="avatarInput"
        type="file"
        accept="image/jpeg,image/png,image/gif,image/webp"
        style="display: none"
        @change="onAvatarChange"
      />

      <div class="profile-info">
        <div class="profile-name" v-if="!editing">
          {{ auth.user?.displayName || auth.user?.email || '未登录' }}
          <van-icon
            v-if="auth.user"
            name="edit"
            size="16"
            color="#969799"
            class="name-edit-icon"
            @click="startEdit"
          />
        </div>
        <van-field
          v-else
          v-model="form.displayName"
          placeholder="请输入昵称"
          class="name-field"
          maxlength="50"
        />
        <div class="profile-email" v-if="auth.user?.email">
          {{ auth.user.email }}
        </div>
      </div>
    </div>

    <div v-if="editing" class="save-bar">
      <van-button block type="primary" :loading="saving" @click="onSaveName">
        保存昵称
      </van-button>
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
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { showConfirmDialog, showToast } from 'vant'
import { useAuthStore } from '../stores/auth'

const router = useRouter()
const auth = useAuthStore()
const avatarInput = ref(null)
const editing = ref(false)
const saving = ref(false)
const uploadingAvatar = ref(false)

const form = ref({ displayName: '' })

const avatarDisplayUrl = computed(() => {
  const raw = auth.user?.avatarUrl
  if (!raw) {
    const initial = (auth.user?.displayName || auth.user?.email || 'N').charAt(0).toUpperCase()
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="72" height="72"><rect width="72" height="72" rx="36" fill="#1989fa"/><text x="36" y="46" font-size="30" fill="#fff" text-anchor="middle" font-family="sans-serif">${initial}</text></svg>`
    return 'data:image/svg+xml,' + encodeURIComponent(svg)
  }
  const token = localStorage.getItem('token') || ''
  const sep = raw.includes('?') ? '&' : '?'
  return `${raw}${sep}access_token=${encodeURIComponent(token)}`
})

function startEdit() {
  form.value.displayName = auth.user?.displayName || ''
  editing.value = true
}

function cancelEdit() {
  editing.value = false
  form.value.displayName = ''
}

async function onSaveName() {
  saving.value = true
  try {
    await auth.updateProfile({ displayName: form.value.displayName })
    showToast('昵称已更新')
    editing.value = false
  } finally {
    saving.value = false
  }
}

function triggerAvatarInput() {
  if (!auth.user) return
  avatarInput.value?.click()
}

async function onAvatarChange(e) {
  const file = e.target.files?.[0]
  if (!file) return
  try {
    uploadingAvatar.value = true
    const url = await auth.uploadAvatar(file)
    // 上传成功后，将新头像 URL 保存到用户资料（自动删除旧头像逻辑后端负责）
    await auth.updateProfile({ avatarUrl: url })
    showToast('头像已更新')
  } finally {
    uploadingAvatar.value = false
    e.target.value = ''
  }
}

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
    cancelEdit()
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
  border-bottom: 1px solid #ebedf0;
}
.avatar-wrap {
  position: relative;
  cursor: pointer;
  flex-shrink: 0;
  border-radius: 50%;
}
.avatar-img {
  display: block;
  border-radius: 50%;
}
.avatar-edit-mask {
  position: absolute;
  inset: 0;
  border-radius: 50%;
  background: rgba(0, 0, 0, 0.5);
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  opacity: 0;
  transition: opacity 0.2s;
}
.avatar-wrap:hover .avatar-edit-mask {
  opacity: 1;
}
.profile-info {
  flex: 1;
  min-width: 0;
}
.profile-name {
  font-size: 18px;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 8px;
  overflow-wrap: break-word;
  word-break: break-all;
}
.name-edit-icon {
  cursor: pointer;
  flex-shrink: 0;
}
.name-field {
  padding: 0;
}
.name-field :deep(.van-field__control) {
  font-size: 18px;
  font-weight: 600;
}
.profile-email {
  font-size: 13px;
  color: #969799;
  margin-top: 4px;
}
.save-bar {
  padding: 12px 16px;
  background: #fff;
  border-bottom: 1px solid #ebedf0;
}
.logout {
  margin: 32px 16px 0;
}

/* 桌面端：居中阅读宽度 */
@media (min-width: 1024px) {
  .page {
    max-width: 720px;
    margin: 0 auto;
    padding-bottom: 32px;
  }
}
</style>
