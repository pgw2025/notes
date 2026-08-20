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

    <van-cell-group inset style="margin-top: 16px" title="导入 / 导出">
      <van-cell title="导出全部笔记" is-link icon="down" @click="showExportSheet = true" />
      <van-cell title="导入笔记" is-link icon="upgrade" @click="triggerImportInput" />
      <div class="cell-hint">支持导入本系统导出的 JSON 或 Markdown 文件，重复笔记将自动跳过</div>
    </van-cell-group>

    <input
      ref="importInput"
      type="file"
      accept=".json,.md,.markdown"
      multiple
      style="display: none"
      @change="onImportFile"
    />

    <van-cell-group inset style="margin-top: 16px" title="显示">
      <van-cell title="外观主题" icon="eye-o" @click="showThemeSheet = true">
        <template #value>
          <span class="theme-pill" :class="`theme-pill--${theme.mode}`">{{ themeLabel }}</span>
        </template>
      </van-cell>
    </van-cell-group>

    <van-cell-group inset style="margin-top: 16px" title="笔记外观">
      <van-cell
        title="默认笔记颜色"
        is-link
        icon="brush-o"
        @click="showColorPicker = true"
      >
        <template #value>
          <div class="color-swatch" :style="{ background: defaultColorPreview }"></div>
        </template>
      </van-cell>
      <div class="cell-hint">新建笔记会自动使用此颜色（已有笔记不受影响）</div>
    </van-cell-group>

    <van-cell-group inset style="margin-top: 16px">
      <van-cell title="关于" value="笔记 v1.0" />
    </van-cell-group>

    <div class="logout">
      <van-button round block type="danger" plain @click="onLogout">退出登录</van-button>
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

    <!-- 外观主题选择 -->
    <van-action-sheet
      v-model:show="showThemeSheet"
      title="外观主题"
      :actions="themeActions"
      @select="onThemeSelect"
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
const editing = ref(false)
const saving = ref(false)
const uploadingAvatar = ref(false)

const form = ref({ displayName: '' })

// 外观主题
const showThemeSheet = ref(false)
const THEME_META = {
  auto:  { label: '跟随系统', desc: '自动匹配系统深色/浅色设置' },
  light: { label: '浅色模式', desc: '始终使用浅色界面' },
  dark:  { label: '深色模式', desc: '始终使用深色界面' }
}
const themeActions = computed(() => MODES.map((m) => ({
  name: THEME_META[m].label,
  subname: THEME_META[m].desc,
  value: m
})))
const themeLabel = computed(() => THEME_META[theme.mode]?.label || '跟随系统')
function onThemeSelect({ value }) {
  if (!MODES.includes(value)) return
  theme.setMode(value)
  showThemeSheet.value = false
  showToast(`已切换为「${THEME_META[value].label}」`)
}

// 默认笔记颜色（null 表示用系统白色）
const showColorPicker = ref(false)
const defaultNoteColor = ref(null)

// 导入 / 导出
const showExportSheet = ref(false)
const importing = ref(false)
const importProgressText = ref('')
const importInput = ref(null)
const exportActions = [
  { name: 'JSON 格式（完整备份）', subname: '包含所有字段，适合备份恢复', value: 'json' },
  { name: 'Markdown（纯正文）', subname: '每篇笔记一个 .md 文件，打包 ZIP', value: 'markdown' },
  { name: 'Markdown + 元数据', subname: '含 frontmatter 元数据，打包 ZIP', value: 'markdown-meta' }
]

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

// 颜色预览方块（用户没设默认色时显示白色）
const defaultColorPreview = computed(() => {
  const c = defaultNoteColor.value && isValidHex(defaultNoteColor.value)
    ? defaultNoteColor.value
    : DEFAULT_NOTE_COLOR
  return c
})

async function onSaveDefaultColor(color) {
  try {
    saving.value = true
    await auth.updateProfile({
      defaultNoteColor: color || ''  // null/空串表示清除为系统默认
    })
    showToast('默认颜色已保存')
  } finally {
    saving.value = false
  }
}

async function onResetDefaultColor() {
  try {
    saving.value = true
    await auth.updateProfile({ defaultNoteColor: '' })
    showToast('已重置为系统默认')
  } finally {
    saving.value = false
  }
}

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

onMounted(async () => {
  if (auth.isLoggedIn && !auth.user) {
    await auth.fetchUser()
  }
  // 同步用户已有的默认颜色
  defaultNoteColor.value = auth.user?.defaultNoteColor || null
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
  background: var(--surface);
  border-bottom: 1px solid var(--border);
  transition: background 0.2s, border-color 0.2s;
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
  color: var(--text-primary);
  display: flex;
  align-items: center;
  gap: 8px;
  overflow-wrap: break-word;
  word-break: break-all;
}
.name-edit-icon {
  cursor: pointer;
  flex-shrink: 0;
  color: var(--text-tertiary) !important;
}
.name-field {
  padding: 0;
}
.name-field :deep(.van-field__control) {
  font-size: 18px;
  font-weight: 600;
  color: var(--text-primary);
}
.profile-email {
  font-size: 13px;
  color: var(--text-tertiary);
  margin-top: 4px;
}
.save-bar {
  padding: 12px 16px;
  background: var(--surface);
  border-bottom: 1px solid var(--border);
}
.logout {
  margin: 32px 16px 0;
}
.theme-pill {
  display: inline-flex;
  align-items: center;
  padding: 3px 10px;
  font-size: 12px;
  line-height: 1.4;
  border-radius: 999px;
  background: var(--surface-2);
  color: var(--text-secondary);
  border: 1px solid var(--border);
  white-space: nowrap;
}
.theme-pill--auto { color: var(--text-secondary); }
.theme-pill--light {
  background: rgba(255, 220, 150, 0.18);
  color: #b37a00;
  border-color: rgba(179, 122, 0, 0.2);
}
body.dark .theme-pill--light {
  background: rgba(255, 220, 150, 0.14);
  color: #ffd36e;
  border-color: rgba(255, 211, 110, 0.25);
}
.theme-pill--dark {
  background: rgba(120, 140, 255, 0.18);
  color: #5468ff;
  border-color: rgba(84, 104, 255, 0.25);
}
body.dark .theme-pill--dark {
  background: rgba(120, 140, 255, 0.22);
  color: #a7b3ff;
  border-color: rgba(167, 179, 255, 0.3);
}
.color-swatch {
  width: 28px;
  height: 28px;
  border-radius: 6px;
  border: 1px solid var(--border);
  display: inline-block;
  vertical-align: middle;
}
.cell-hint {
  font-size: 12px;
  color: var(--text-tertiary);
  padding: 6px 16px 12px;
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
    max-width: 720px;
    margin: 0 auto;
    padding-bottom: 32px;
  }
}
</style>
