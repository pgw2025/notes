<template>
  <div class="page">
    <van-nav-bar title="个人与设置" />

    <div class="settings-container">
      <!-- 用户资料卡片 -->
      <div class="profile-card">
        <div class="profile-main">
          <div class="avatar-wrap" @click="triggerAvatarInput" title="点击更换头像">
            <van-image
              round
              width="68"
              height="68"
              :src="avatarDisplayUrl"
              fit="cover"
              class="avatar-img"
            />
            <div class="avatar-edit-mask">
              <van-icon name="photograph" size="18" />
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
              <span>{{ auth.user?.displayName || auth.user?.email || '未登录' }}</span>
              <button class="name-edit-btn" @click="startEdit" title="修改昵称">
                <van-icon name="edit" size="14" />
              </button>
            </div>
            <div v-else class="name-edit-wrap">
              <van-field
                v-model="form.displayName"
                placeholder="请输入新昵称"
                class="name-field"
                maxlength="50"
                clearable
              />
              <van-button size="small" type="primary" :loading="saving" @click="onSaveName">
                保存
              </van-button>
            </div>
            <div class="profile-email" v-if="auth.user?.email">
              <van-icon name="envelop-o" size="12" />
              <span>{{ auth.user.email }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- 主题外观选择器 -->
      <div class="settings-section">
        <div class="section-header">
          <span class="section-title">外观主题</span>
        </div>
        <div class="theme-cards-grid">
          <div
            v-for="item in themeCards"
            :key="item.mode"
            class="theme-card"
            :class="{ active: theme.mode === item.mode }"
            @click="setThemeMode(item.mode)"
          >
            <div class="theme-icon-wrap" :class="`theme-icon--${item.mode}`">
              <van-icon :name="item.icon" size="20" />
            </div>
            <span class="theme-name">{{ item.label }}</span>
            <span class="theme-desc">{{ item.desc }}</span>
            <div v-if="theme.mode === item.mode" class="theme-check-badge">
              <van-icon name="success" size="12" />
            </div>
          </div>
        </div>
      </div>

      <!-- 快速管理 -->
      <div class="settings-section">
        <div class="section-header">
          <span class="section-title">内容管理</span>
        </div>
        <div class="action-cells-box">
          <div class="action-cell" @click="$router.push('/categories')">
            <div class="cell-left">
              <div class="cell-icon-wrap icon-blue">
                <van-icon name="apps-o" size="18" />
              </div>
              <div class="cell-text">
                <span class="cell-title">分类管理</span>
                <span class="cell-sub">创建与整理笔记分类</span>
              </div>
            </div>
            <van-icon name="arrow" class="cell-arrow" />
          </div>

          <div class="action-cell" @click="$router.push('/tags')">
            <div class="cell-left">
              <div class="cell-icon-wrap icon-indigo">
                <van-icon name="bookmark-o" size="18" />
              </div>
              <div class="cell-text">
                <span class="cell-title">标签管理</span>
                <span class="cell-sub">管理所有标签索引</span>
              </div>
            </div>
            <van-icon name="arrow" class="cell-arrow" />
          </div>

          <div class="action-cell" @click="showColorPicker = true">
            <div class="cell-left">
              <div class="cell-icon-wrap icon-amber">
                <van-icon name="brush-o" size="18" />
              </div>
              <div class="cell-text">
                <span class="cell-title">默认笔记卡片颜色</span>
                <span class="cell-sub">新建笔记默认使用的底色</span>
              </div>
            </div>
            <div class="color-preview-badge" :style="{ background: defaultColorPreview }"></div>
          </div>
        </div>
      </div>

      <!-- 备份与迁移 -->
      <div class="settings-section">
        <div class="section-header">
          <span class="section-title">数据备份与迁移</span>
        </div>
        <div class="action-cells-box">
          <div class="action-cell" @click="showExportSheet = true">
            <div class="cell-left">
              <div class="cell-icon-wrap icon-emerald">
                <van-icon name="down" size="18" />
              </div>
              <div class="cell-text">
                <span class="cell-title">导出全部笔记</span>
                <span class="cell-sub">支持 JSON 备份或 Markdown 打包下载</span>
              </div>
            </div>
            <van-icon name="arrow" class="cell-arrow" />
          </div>

          <div class="action-cell" @click="triggerImportInput">
            <div class="cell-left">
              <div class="cell-icon-wrap icon-purple">
                <van-icon name="upgrade" size="18" />
              </div>
              <div class="cell-text">
                <span class="cell-title">导入外部笔记</span>
                <span class="cell-sub">支持导入本系统导出的 JSON 或 Markdown</span>
              </div>
            </div>
            <van-icon name="arrow" class="cell-arrow" />
          </div>
        </div>
      </div>

      <input
        ref="importInput"
        type="file"
        accept=".json,.md,.markdown"
        multiple
        style="display: none"
        @change="onImportFile"
      />

      <!-- 退出登录 -->
      <div class="logout-box">
        <van-button round block type="danger" plain @click="onLogout">
          退出登录账号
        </van-button>
      </div>
    </div>

    <!-- 默认颜色选择器 -->
    <ColorPicker
      v-model:show="showColorPicker"
      v-model="defaultNoteColor"
      @confirm="onSaveDefaultColor"
      @reset="onResetDefaultColor"
    />

    <!-- 导出格式选择 -->
    <van-action-sheet
      v-model:show="showExportSheet"
      title="选择导出格式"
      :actions="exportActions"
      @select="onExportSelect"
      cancel-text="取消"
      close-on-click-action
    />

    <!-- 导入进度 -->
    <van-overlay :show="importing" @click.stop>
      <div class="import-overlay">
        <van-loading size="32px" color="#fff" vertical>
          {{ importProgressText }}
        </van-loading>
      </div>
    </van-overlay>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { showConfirmDialog, showToast, showSuccessToast } from 'vant'
import { useAuthStore } from '../stores/auth'
import { useThemeStore, MODES } from '../stores/theme'
import ColorPicker from '../components/ColorPicker.vue'
import { DEFAULT_NOTE_COLOR, isValidHex } from '../utils/color'
import { exportAllNotes, importNotes } from '../utils/exportImport'

const router = useRouter()
const auth = useAuthStore()
const theme = useThemeStore()
const avatarInput = ref(null)
const importInput = ref(null)

const editing = ref(false)
const saving = ref(false)
const uploadingAvatar = ref(false)
const form = ref({ displayName: '' })

const showColorPicker = ref(false)
const defaultNoteColor = ref(DEFAULT_NOTE_COLOR)

const showExportSheet = ref(false)
const importing = ref(false)
const importProgressText = ref('准备导入…')

const themeCards = [
  { mode: MODES[0], label: '跟随系统', icon: 'desktop-o', desc: '自动匹配系统外观' },
  { mode: MODES[1], label: '浅色模式', icon: 'sun-o', desc: '明亮柔和经典外观' },
  { mode: MODES[2], label: '深色模式', icon: 'moon-o', desc: '沉浸护眼暗色外观' }
]

function setThemeMode(mode) {
  theme.setMode(mode)
  showToast(`已切换为${themeLabel.value}`)
}

const themeLabel = computed(() => {
  if (theme.mode === 'auto') return '跟随系统'
  if (theme.mode === 'light') return '浅色模式'
  if (theme.mode === 'dark') return '深色模式'
  return '默认'
})

const defaultColorPreview = computed(() => {
  return auth.user?.defaultNoteColor && isValidHex(auth.user.defaultNoteColor)
    ? auth.user.defaultNoteColor
    : DEFAULT_NOTE_COLOR
})

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

const exportActions = [
  { name: 'JSON 格式（完整备份）', subname: '包含所有字段，适合备份恢复', value: 'json' },
  { name: 'Markdown（纯正文）', subname: '每篇笔记一个 .md 文件，打包 ZIP', value: 'markdown' },
  { name: 'Markdown + 元数据', subname: '含 frontmatter 元数据，打包 ZIP', value: 'markdown-meta' }
]

function startEdit() {
  form.value.displayName = auth.user?.displayName || ''
  editing.value = true
}

async function onSaveName() {
  const name = form.value.displayName.trim()
  if (!name) {
    showToast('请输入昵称')
    return
  }
  saving.value = true
  try {
    await auth.updateProfile({ displayName: name })
    editing.value = false
    showToast('已更新昵称')
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
    await auth.updateProfile({ avatarUrl: url })
    showToast('头像已更新')
  } catch (err) {
    showToast('上传头像失败: ' + (err.message || ''))
  } finally {
    uploadingAvatar.value = false
    e.target.value = ''
  }
}

async function onSaveDefaultColor(color) {
  try {
    saving.value = true
    await auth.updateProfile({ defaultNoteColor: color || '' })
    showToast('已保存默认笔记颜色')
  } finally {
    saving.value = false
  }
}

async function onResetDefaultColor() {
  try {
    saving.value = true
    await auth.updateProfile({ defaultNoteColor: '' })
    defaultNoteColor.value = DEFAULT_NOTE_COLOR
    showToast('已重置为系统默认')
  } finally {
    saving.value = false
  }
}

async function onExportSelect({ value }) {
  showExportSheet.value = false
  showToast('正在导出...')
  try {
    const count = await exportAllNotes(value)
    showSuccessToast(`已导出 ${count} 篇笔记`)
  } catch (e) {
    showToast('导出失败：' + (e.message || '未知错误'))
  }
}

function triggerImportInput() {
  importInput.value?.click()
}

async function onImportFile(e) {
  const files = Array.from(e.target.files || [])
  if (!files.length) return
  e.target.value = ''

  importing.value = true
  importProgressText.value = '准备导入...'
  try {
    const result = await importNotes(files, (current, total, title) => {
      importProgressText.value = `导入中 ${current}/${total}：${title}`
    })
    const parts = []
    if (result.imported) parts.push(`成功 ${result.imported}`)
    if (result.skipped) parts.push(`跳过 ${result.skipped}`)
    if (result.failed) parts.push(`失败 ${result.failed}`)
    showSuccessToast(`导入完成：${parts.join('，')}`)
  } catch (e) {
    showToast('导入失败：' + (e.message || '未知错误'))
  } finally {
    importing.value = false
    importProgressText.value = ''
  }
}

async function onLogout() {
  try {
    await showConfirmDialog({
      title: '退出登录',
      message: '确定要退出登录当前账号吗？'
    })
    auth.logout()
    router.replace('/login')
  } catch {
    // 取消
  }
}

onMounted(async () => {
  if (auth.isLoggedIn && !auth.user) {
    try {
      await auth.fetchUser()
    } catch {}
  }
  if (auth.user?.defaultNoteColor) {
    defaultNoteColor.value = auth.user.defaultNoteColor
  }
})
</script>

<style scoped>
.page {
  min-height: 100vh;
  padding-bottom: 90px;
  background: var(--app-bg);
}

.settings-container {
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 18px;
}

/* 用户卡片 */
.profile-card {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 14px;
  padding: 18px 16px;
  box-shadow: var(--shadow-xs);
}

.profile-main {
  display: flex;
  align-items: center;
  gap: 16px;
}

.avatar-wrap {
  position: relative;
  width: 68px;
  height: 68px;
  border-radius: 50%;
  overflow: hidden;
  cursor: pointer;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
  flex-shrink: 0;
}

.avatar-img {
  width: 100%;
  height: 100%;
}

.avatar-edit-mask {
  position: absolute;
  inset: 0;
  background: rgba(0, 0, 0, 0.35);
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  opacity: 0;
  transition: opacity 0.2s ease;
}

.avatar-wrap:hover .avatar-edit-mask {
  opacity: 1;
}

.profile-info {
  flex: 1;
  min-width: 0;
}

.profile-name {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 18px;
  font-weight: 700;
  color: var(--text-primary);
}

.name-edit-btn {
  background: transparent;
  border: none;
  color: var(--text-tertiary);
  cursor: pointer;
  padding: 4px;
  border-radius: 4px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.name-edit-btn:hover {
  background: var(--surface-2);
  color: var(--color-primary);
}

.name-edit-wrap {
  display: flex;
  align-items: center;
  gap: 8px;
}

.name-field {
  padding: 4px 8px;
  background: var(--surface-2);
  border-radius: 6px;
}

.profile-email {
  display: flex;
  align-items: center;
  gap: 5px;
  font-size: 13px;
  color: var(--text-tertiary);
  margin-top: 4px;
}

/* 设置分区 */
.settings-section {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.section-header {
  padding: 0 4px;
}

.section-title {
  font-size: 13px;
  font-weight: 600;
  color: var(--text-tertiary);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

/* 主题三列卡片 */
.theme-cards-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 10px;
}

.theme-card {
  background: var(--surface);
  border: 1.5px solid var(--border);
  border-radius: 12px;
  padding: 14px 10px;
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  cursor: pointer;
  position: relative;
  transition: all 0.18s cubic-bezier(0.4, 0, 0.2, 1);
  box-shadow: var(--shadow-xs);
}

.theme-card:hover {
  border-color: var(--color-primary);
  transform: translateY(-2px);
  box-shadow: var(--shadow-sm);
}

.theme-card.active {
  border-color: var(--color-primary);
  background: rgba(59, 130, 246, 0.06);
}

:global(body.dark) .theme-card.active {
  background: rgba(56, 189, 248, 0.1);
}

.theme-icon-wrap {
  width: 38px;
  height: 38px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 8px;
}

.theme-icon--auto { background: var(--surface-2); color: var(--text-secondary); }
.theme-icon--light { background: #fef3c7; color: #d97706; }
.theme-icon--dark { background: #1e1b4b; color: #818cf8; }

.theme-name {
  font-size: 13.5px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 2px;
}

.theme-desc {
  font-size: 10.5px;
  color: var(--text-tertiary);
  line-height: 1.2;
}

.theme-check-badge {
  position: absolute;
  top: 6px;
  right: 6px;
  width: 18px;
  height: 18px;
  border-radius: 50%;
  background: var(--color-primary);
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
}

/* 操作项卡片列表 */
.action-cells-box {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 12px;
  overflow: hidden;
  box-shadow: var(--shadow-xs);
}

.action-cell {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 16px;
  border-bottom: 1px solid var(--divider);
  cursor: pointer;
  transition: background 0.15s ease;
}

.action-cell:last-child {
  border-bottom: none;
}

.action-cell:hover {
  background: var(--surface-2);
}

.cell-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.cell-icon-wrap {
  width: 36px;
  height: 36px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.icon-blue { background: rgba(59, 130, 246, 0.12); color: #2563eb; }
.icon-indigo { background: rgba(99, 102, 241, 0.12); color: #4f46e5; }
.icon-amber { background: rgba(245, 158, 11, 0.12); color: #d97706; }
.icon-emerald { background: rgba(16, 185, 129, 0.12); color: #059669; }
.icon-purple { background: rgba(168, 85, 247, 0.12); color: #9333ea; }

.cell-text {
  display: flex;
  flex-direction: column;
}

.cell-title {
  font-size: 14.5px;
  font-weight: 600;
  color: var(--text-primary);
}

.cell-sub {
  font-size: 12px;
  color: var(--text-tertiary);
  margin-top: 2px;
}

.cell-arrow {
  color: var(--text-tertiary);
  font-size: 14px;
}

.color-preview-badge {
  width: 26px;
  height: 26px;
  border-radius: 6px;
  border: 1.5px solid var(--border);
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.08);
}

.logout-box {
  margin-top: 12px;
}

.import-overlay {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}

/* 桌面端：居中阅读宽度 */
@media (min-width: 1024px) {
  .page {
    max-width: 820px;
    margin: 0 auto;
    padding-bottom: 40px;
  }
}
</style>
